using System.Globalization;
using System.IO;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace AmentumRevit.Services;

/// <summary>
/// Scans the local environment around the active view or selection and exports
/// lightweight geometry (OBJ mesh or PLY point cloud).
///
/// Environment variable prefix: AMENTUM_  (e.g. AMENTUM_SCAN_RADIUS_FT)
/// Temp output folder: %TEMP%\AmentumScan\
/// </summary>
public static class EnvironmentScanService
{
    public const string TempFolderName = "AmentumScan";

    public static XYZ? TryGetActiveUIViewCenter(UIDocument uidoc)
    {
        try
        {
            var doc    = uidoc.Document;
            var uiview = uidoc.GetOpenUIViews().FirstOrDefault(v => v.ViewId == doc.ActiveView.Id);
            if (uiview is null) return null;

            var corners = uiview.GetZoomCorners();
            if (corners is null || corners.Count < 2) return null;

            return new XYZ(
                (corners[0].X + corners[1].X) * 0.5,
                (corners[0].Y + corners[1].Y) * 0.5,
                (corners[0].Z + corners[1].Z) * 0.5);
        }
        catch { return null; }
    }

    public static XYZ? TryGetSelectionCenter(UIDocument uidoc)
    {
        var doc = uidoc.Document;
        var sel = uidoc.Selection.GetElementIds();
        if (sel.Count == 0) return null;

        double sx = 0, sy = 0, sz = 0;
        int    count = 0;

        foreach (var id in sel)
        {
            var el = doc.GetElement(id);
            if (el is null) continue;

            BoundingBoxXYZ? bb = null;
            try { bb = el.get_BoundingBox(null); } catch { /* ignore */ }
            if (bb is null) continue;

            sx += (bb.Min.X + bb.Max.X) * 0.5;
            sy += (bb.Min.Y + bb.Max.Y) * 0.5;
            sz += (bb.Min.Z + bb.Max.Z) * 0.5;
            count++;
        }

        return count > 0 ? new XYZ(sx / count, sy / count, sz / count) : null;
    }

    public static IList<Element> CollectElements(Document doc, XYZ center,
        double radiusFt, int maxElements)
    {
        var min     = new XYZ(center.X - radiusFt, center.Y - radiusFt, center.Z - radiusFt);
        var max     = new XYZ(center.X + radiusFt, center.Y + radiusFt, center.Z + radiusFt);
        var outline = new Outline(min, max);
        var filter  = new BoundingBoxIntersectsFilter(outline);

        var collector = new FilteredElementCollector(doc)
            .WherePasses(filter)
            .WhereElementIsNotElementType();

        var results = new List<Element>(maxElements);
        foreach (var el in collector)
        {
            if (el?.Category is null) continue;
            if (el is View or RevitLinkInstance) continue;
            results.Add(el);
            if (results.Count >= maxElements) break;
        }

        return results;
    }

    public static string ExportMeshObj(
        Document doc, IList<Element> elements, string outPath,
        int maxTriangles, View? viewForDetail = null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outPath) ?? Environment.CurrentDirectory);

        var inv = CultureInfo.InvariantCulture;
        using var writer = new StreamWriter(outPath);

        writer.WriteLine("# Amentum Tools environment scan mesh");
        writer.WriteLine("# units: ft (Revit internal)");
        writer.WriteLine($"# elements: {elements.Count}");
        writer.WriteLine($"# maxTriangles: {maxTriangles}");

        var opt = new Options
        {
            ComputeReferences       = false,
            IncludeNonVisibleObjects = false,
            DetailLevel             = ViewDetailLevel.Fine,
            View                    = viewForDetail
        };

        int triCount = 0;
        int vIndex   = 0;

        foreach (var el in elements)
        {
            if (triCount >= maxTriangles) break;

            GeometryElement? ge = null;
            try { ge = el.get_Geometry(opt); } catch { /* ignore */ }
            if (ge is null) continue;

            ProcessGeometry(ge, Transform.Identity, onTriangle: (a, b, cc) =>
            {
                if (triCount >= maxTriangles) return;
                writer.WriteLine($"v {a.X.ToString(inv)} {a.Y.ToString(inv)} {a.Z.ToString(inv)}");
                writer.WriteLine($"v {b.X.ToString(inv)} {b.Y.ToString(inv)} {b.Z.ToString(inv)}");
                writer.WriteLine($"v {cc.X.ToString(inv)} {cc.Y.ToString(inv)} {cc.Z.ToString(inv)}");
                writer.WriteLine($"f {vIndex + 1} {vIndex + 2} {vIndex + 3}");
                vIndex += 3;
                triCount++;
            });
        }

        writer.WriteLine($"# trianglesWritten: {triCount}");
        return outPath;
    }

    public static string ExportPointsPly(
        Document doc, IList<Element> elements, string outPath,
        int maxPoints, View? viewForDetail = null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outPath) ?? Environment.CurrentDirectory);

        var opt = new Options
        {
            ComputeReferences       = false,
            IncludeNonVisibleObjects = false,
            DetailLevel             = ViewDetailLevel.Fine,
            View                    = viewForDetail
        };

        var rng   = new Random(42);
        long seen = 0;
        var  pts  = new List<XYZ>(Math.Min(maxPoints, 100_000));

        void Consider(XYZ p)
        {
            seen++;
            if (pts.Count < maxPoints) { pts.Add(p); return; }
            long j = rng.NextInt64(seen);
            if (j < maxPoints) pts[(int)j] = p;
        }

        foreach (var el in elements)
        {
            GeometryElement? ge = null;
            try { ge = el.get_Geometry(opt); } catch { /* ignore */ }
            if (ge is null) continue;
            ProcessGeometry(ge, Transform.Identity, onPoint: Consider);
        }

        var inv = CultureInfo.InvariantCulture;
        using var writer = new StreamWriter(outPath);

        writer.WriteLine("ply");
        writer.WriteLine("format ascii 1.0");
        writer.WriteLine("comment Amentum Tools environment scan points");
        writer.WriteLine("comment units ft (Revit internal)");
        writer.WriteLine($"comment elements {elements.Count}");
        writer.WriteLine($"comment maxPoints {maxPoints}");
        writer.WriteLine($"comment seenPoints {seen}");
        writer.WriteLine($"element vertex {pts.Count}");
        writer.WriteLine("property float x");
        writer.WriteLine("property float y");
        writer.WriteLine("property float z");
        writer.WriteLine("end_header");

        foreach (var p in pts)
            writer.WriteLine($"{p.X.ToString(inv)} {p.Y.ToString(inv)} {p.Z.ToString(inv)}");

        return outPath;
    }

    // ── Geometry traversal ────────────────────────────────────────────────────

    private static void ProcessGeometry(GeometryElement ge, Transform tr,
        Action<XYZ, XYZ, XYZ>? onTriangle = null, Action<XYZ>? onPoint = null)
    {
        foreach (var obj in ge)
        {
            if (obj is null) continue;
            switch (obj)
            {
                case Solid solid:
                    if (solid.Faces is null || solid.Faces.Size == 0) break;
                    foreach (Face face in solid.Faces)
                    {
                        Mesh? m = null;
                        try { m = face.Triangulate(); } catch { /* ignore */ }
                        if (m is not null)
                            ProcessMesh(m, tr, onTriangle, onPoint);
                    }
                    break;

                case Mesh mesh:
                    ProcessMesh(mesh, tr, onTriangle, onPoint);
                    break;

                case GeometryInstance inst:
                    GeometryElement? sym = null;
                    try { sym = inst.GetSymbolGeometry(); } catch { /* ignore */ }
                    if (sym is not null)
                        ProcessGeometry(sym, tr.Multiply(inst.Transform), onTriangle, onPoint);
                    break;
            }
        }
    }

    private static void ProcessMesh(Mesh mesh, Transform tr,
        Action<XYZ, XYZ, XYZ>? onTriangle, Action<XYZ>? onPoint)
    {
        if (onPoint is not null)
        {
            try { foreach (XYZ v in mesh.Vertices) onPoint(tr.OfPoint(v)); }
            catch { /* ignore */ }
        }

        if (onTriangle is not null)
        {
            int n;
            try { n = mesh.NumTriangles; } catch { return; }
            for (int i = 0; i < n; i++)
            {
                MeshTriangle? tri = null;
                try { tri = mesh.get_Triangle(i); } catch { /* ignore */ }
                if (tri is null) continue;
                onTriangle(
                    tr.OfPoint(tri.get_Vertex(0)),
                    tr.OfPoint(tri.get_Vertex(1)),
                    tr.OfPoint(tri.get_Vertex(2)));
            }
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    public static string SafeFilePart(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "model";
        var bad     = Path.GetInvalidFileNameChars();
        var cleaned = new string(s.Select(ch => bad.Contains(ch) ? '_' : ch).ToArray());
        return cleaned.Length > 80 ? cleaned[..80] : cleaned;
    }

    public static double GetEnvDouble(string name, double fallback)
    {
        var raw = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(raw)) return fallback;
        if (double.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v)) return v;
        if (double.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.CurrentCulture,   out v))     return v;
        return fallback;
    }

    public static int GetEnvInt(string name, int fallback)
    {
        var raw = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(raw)) return fallback;
        if (int.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)) return v;
        if (int.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.CurrentCulture,   out v))     return v;
        return fallback;
    }
}
