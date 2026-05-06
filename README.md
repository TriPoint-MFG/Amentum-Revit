# Amentum Revit

Corporate Revit add-in for Amentum — providing Excel parameter exchange, CAD round-trip indexing, shared parameter batch workflows, and BIM export tools.

## Supported Revit Versions

| Version | Status |
|---------|--------|
| Revit 2025 | ✅ Supported |
| Revit 2026 | ✅ Supported |
| Revit 2027 | ✅ Supported |

## Installation

### Corporate (Machine-Wide) Deployment

Copy the `Amentum.bundle` folder to:

```
%PROGRAMDATA%\Autodesk\Revit\Addins\<year>\
```

Replace `<year>` with `2025`, `2026`, or `2027` as appropriate. Restart Revit — the **Amentum** tab will appear in the ribbon automatically.

### Per-User Deployment

Copy the `Amentum.bundle` folder to:

```
%APPDATA%\Autodesk\Revit\Addins\<year>\
```

> **Note:** This add-in is pre-licensed for all Amentum workstations. No Autodesk App Store account is required.

## Features

- **Excel Parameter Sync** — Import and export Revit element parameters to/from Excel for bulk editing and reporting.
- **Export 3D DWG** — Export the active 3D view with Revit index and metadata sidecars.
- **CAD Sync** — Move mapped Revit elements based on detected deltas from an AutoCAD JSON index.
- **Category Shared Params** — Generate a shared parameter file grouped by category with normalised engineering names.
- **Batch Tools** — Export family/parameter sheets, apply parameter updates, and merge shared parameter GUID maps.
- **BIM Web App** — Opens the local dashboard at `http://127.0.0.1:8765/app` when the local service is running.

## Configuration

The `shared_parameter_map.json` file in `Amentum.bundle/Contents/` controls shared parameter name normalisation mappings and can be updated by your BIM manager.

For local BIM web workflows, start the Amentum BIM web service and open `http://127.0.0.1:8765/app`. The add-in posts exports to `/api/bim-gui/model-exports` and pulls staged edits through `/api/revit/sync`.

## Bundle Structure

```
Amentum.bundle/
├── PackageContents.xml          # Autodesk bundle manifest (Revit 2025–2027)
└── Contents/
    ├── AmentumRevit.addin       # Revit add-in registration
    ├── AmentumRevit.deps.json   # .NET dependency manifest
    ├── amentum-entitlement.json # Corporate entitlement config (no App Store required)
    ├── RevitExcelSync.dll       # Core add-in assembly
    ├── ClosedXML.dll
    ├── ClosedXML.Parser.dll
    ├── DocumentFormat.OpenXml.dll
    ├── DocumentFormat.OpenXml.Framework.dll
    ├── ExcelNumberFormat.dll
    ├── RBush.dll
    ├── SixLabors.Fonts.dll
    ├── shared_parameter_map.json
    └── Resources/
        ├── QuickStart.html
        └── LICENSE.txt
```

## Support

Contact your Amentum BIM team or email [support@tripointmfg.com](mailto:support@tripointmfg.com).
