using System.Reflection;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Autodesk.Revit.UI;

namespace AmentumRevit;

/// <summary>
/// Revit external application entry point.
/// Builds the "Amentum Tools" ribbon tab with two panels:
///   1. Excel + CAD  — Excel import/export, in-process WPF sync panel, DWG export/import
///   2. Mapping + Scan — shared parameter map tools, geometry scan/upload
/// </summary>
public class App : IExternalApplication
{
    public Result OnStartup(UIControlledApplication application)
    {
        const string tabName = "Amentum Tools";

        try { application.CreateRibbonTab(tabName); }
        catch { /* Tab probably already exists (Revit restart). */ }

        string asm = Assembly.GetExecutingAssembly().Location;

        // ── Panel 1: Excel + CAD ─────────────────────────────────────────────
        RibbonPanel panel1 = application.CreateRibbonPanel(tabName, "Excel + CAD");

        AddButton(panel1, asm, "AmentumExportExcel", "Export\nExcel",
            typeof(Commands.ExportSelectedToExcelCommand), "ExportExcel.png",
            "Export selected Revit elements to Excel (.xlsx) with geometry, electrical, and mapped parameters.");

        AddButton(panel1, asm, "AmentumImportExcel", "Import\nExcel",
            typeof(Commands.ImportUpdatesFromExcelCommand), "ImportExcel.png",
            "Read parameter updates from an Excel 'Updates' sheet and apply them to model elements.");

        panel1.AddSeparator();

        AddButton(panel1, asm, "AmentumPanelView", "Panel\nView",
            typeof(Commands.SyncElementsCommand), "PanelView.png",
            "Open the Amentum in-process panel view to review and edit element parameters interactively.");

        panel1.AddSeparator();

        AddButton(panel1, asm, "AmentumExportImage", "Export\nImage",
            typeof(Commands.ExportActiveViewImageCommand), "ExportImage.png",
            "Export the active Revit view as a PNG image.");

        AddButton(panel1, asm, "AmentumExportDWG", "Export\nDWG",
            typeof(Commands.ExportActive3DToDwgCommand), "ExportDWG.png",
            "Export the active 3D view to DWG and write the Revit exchange baseline for CAD round-trip sync.");

        AddButton(panel1, asm, "AmentumImportDWG", "Import\nDWG",
            typeof(Commands.ImportFromDwgJsonCommand), "ImportDWG.png",
            "Import a CAD exchange JSON and move matching Revit elements from the exported DWG baseline.");

        // ── Panel 2: Mapping + Scan ───────────────────────────────────────────
        RibbonPanel panel2 = application.CreateRibbonPanel(tabName, "Mapping + Scan");

        AddButton(panel2, asm, "AmentumBuildParameters", "Build\nParameters",
            typeof(Commands.BuildParameterMapCommand), "BuildParameters.png",
            "Create a Revit shared parameter file from the model's category parameters and merge the generated GUIDs into the Amentum parameter map.");

        AddButton(panel2, asm, "AmentumParameterJam", "Parameter\nJam",
            typeof(Commands.ParameterJamCommand), "ParameterJam.png",
            "Find duplicate project parameter bindings and keep one unique binding set per normalized parameter name.");

        panel2.AddSeparator();

        AddButton(panel2, asm, "AmentumUploadMeshPts", "Upload\nMesh + Pts",
            typeof(Commands.ScanAndUploadCommand), "UploadMeshPts.png",
            "Scan nearby elements once, export both OBJ mesh and PLY point cloud, and upload both when AMENTUM_UPLOAD_URL is configured.");

        return Result.Succeeded;
    }

    public Result OnShutdown(UIControlledApplication application)
    {
        return Result.Succeeded;
    }

    private static void AddButton(
        RibbonPanel panel,
        string assemblyPath,
        string name,
        string text,
        Type commandType,
        string iconFileName,
        string tooltip)
    {
        var data = new PushButtonData(
            name: name,
            text: text,
            assemblyName: assemblyPath,
            className: commandType.FullName!
        )
        {
            ToolTip = tooltip,
            LongDescription = tooltip,
            Image = LoadIcon(iconFileName),
            LargeImage = LoadIcon(iconFileName)
        };

        panel.AddItem(data);
    }

    private static ImageSource? LoadIcon(string fileName)
    {
        string assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
                             ?? AppContext.BaseDirectory;
        string path = Path.Combine(assemblyDir, "Resources", "Icons", fileName);
        if (!File.Exists(path))
            return null;

        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.UriSource = new Uri(path, UriKind.Absolute);
        image.EndInit();
        image.Freeze();
        return image;
    }
}
