# Amentum Revit

Corporate Revit add-in for Amentum. It provides Excel parameter exchange, Panel View parameter review/editing, shared parameter generation, duplicate project-parameter cleanup, DWG round-trip sync, image export, and scan upload tools.

## Supported Revit Versions

| Version | Status |
| --- | --- |
| Revit 2026 | Supported |

## Local Development Build

The project targets Revit 2026 and .NET 8.

```powershell
cd Amentum-Rvt-Addin
dotnet build -c Release
```

The local Revit manifest is:

```text
%APPDATA%\Autodesk\Revit\Addins\2026\AmentumRevit.addin
```

It points to:

```text
Amentum-Rvt-Addin\bin\Release\net8.0-windows\AmentumRevit.dll
```

Restart Revit after a successful build so the **Amentum Tools** ribbon refreshes.

## Ribbon Tools

- **Export Excel**: exports selected Revit elements to Excel with geometry, electrical data, and mapped parameters.
- **Import Excel**: reads an Excel `Updates` sheet and applies parameter edits back to matching Revit elements.
- **Panel View**: with a selection, opens the editable sync panel; with no selection, opens a project parameter browser with family/type preview thumbnails, parameter scope, storage/data type, shared status, and model path.
- **Export Image**: exports the active Revit view as a PNG.
- **Export DWG**: exports the active 3D view and writes the Revit exchange baseline used for round-trip sync.
- **Import DWG**: imports an Amentum or BIM-GUI CAD JSON index and moves matching Revit elements by detected CAD deltas.
- **Build Parameters**: opens a WebView2 preview of candidate shared parameters, lets the user check individual rows or check all, creates a shared parameter file, and can add selected definitions to Project Parameters.
- **Parameter Jam**: finds duplicate project parameter bindings by normalized name and keeps one unique binding set after user confirmation.
- **Upload Mesh + Pts**: scans once, exports OBJ mesh and PLY points, and uploads both when `AMENTUM_UPLOAD_URL` is configured.

## Build Parameters Workflow

1. Open **Build Parameters** from the **Amentum Tools** ribbon.
2. Review the WebView2 preview table.
3. Filter, check individual parameters, use **Check All**, or clear visible rows.
4. Choose **Create File** to generate only a Revit shared parameter file.
5. Choose **Create + Add**, or check **Add selected to Project Parameters**, to bind selected definitions to matching Revit categories in the active project.

The command also merges generated GUIDs into `shared_parameter_map.json`.

## Panel View Notes

Panel View now works in two modes:

- Open it with selected elements to use the existing editable parameter sync panel.
- Open it with no selection to browse project and element/type parameters across the model.

The preview column uses Revit type thumbnails when Revit can generate them. Revit does not reliably expose the original loaded `.rfa` source path after a family is loaded into a project, so the path column reports the active model path and pairs it with category, family, and type information.

## Store Submission Bundle

The store submission zip is staged under `Archive/` as an Autodesk `.bundle` package. The bundle contains:

```text
Amentum.bundle/
|-- PackageContents.xml
`-- Contents/
    |-- AmentumRevit.addin
    |-- AmentumRevit.dll
    |-- AmentumRevit.deps.json
    |-- dependency DLLs
    |-- shared_parameter_map.json
    `-- Resources/
        |-- Icons/
        |-- QuickStart.html
        `-- LICENSE.txt
```

For a local bundle-style smoke test, copy `Amentum.bundle` to:

```text
%APPDATA%\Autodesk\ApplicationPlugins\
```

For the current development setup, use the normal Revit add-in manifest in `%APPDATA%\Autodesk\Revit\Addins\2026\`.

## Updating Button Icons

Ribbon icons live in:

```text
Amentum-Rvt-Addin\Resources\Icons\
```

The icon file names are wired in `Amentum-Rvt-Addin\App.cs` through the `AddButton(...)` calls. To replace an existing icon, overwrite the PNG while keeping the same file name, then rebuild and restart Revit. To add a new icon, place the PNG in `Resources\Icons`, update the matching `AddButton` icon file name, rebuild, and restart Revit.

The project file already copies `Resources\Icons\*.png` into the build output and package contents.

## Configuration

`shared_parameter_map.json` controls shared parameter GUID mappings and name normalization. Build Parameters updates this file when it creates new shared parameter definitions.

For scan uploads, set `AMENTUM_UPLOAD_URL` before starting Revit. Optional scan tuning environment variables include `AMENTUM_SCAN_RADIUS_FT`, `AMENTUM_SCAN_GRID_FT`, `AMENTUM_MESH_OBJ_PATH`, and `AMENTUM_POINTS_PLY_PATH`.

For DWG round-trip movement scaling, set `AMENTUM_CAD_TO_REVIT_SCALE` when the CAD JSON coordinates need unit conversion.

## Support

Contact Amentum at [support@amentum.com](mailto:support@amentum.com) or visit [amentum.com](https://amentum.com).
