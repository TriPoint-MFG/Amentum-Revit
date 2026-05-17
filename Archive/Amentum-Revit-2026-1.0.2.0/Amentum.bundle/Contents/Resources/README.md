# Amentum Revit 2026

Version: 1.0.2.0

This Revit add-in adds the **Amentum Tools** ribbon tab with Excel parameter exchange, Panel View parameter review/editing, shared parameter generation, duplicate project-parameter cleanup, DWG round-trip sync, image export, and scan upload tools.

## What Changed In 1.0.2.0

- Ribbon button images are scaled onto the correct Revit thumbnail sizes so the icons fit cleanly on each button.
- Excel export now includes an **Edit Guide** sheet explaining which exported columns are reference-only and which parameter keys can be round-tripped through import.
- Excel export now includes an **Update Status** sheet showing, per selected element and parameter key, whether Revit can apply an imported change and why.
- Import Excel ignores the placeholder template row in the **Updates** sheet.

## Install

1. Download `Amentum-Revit-2026-1.0.2.0.zip`.
2. Right-click the zip file, choose **Properties**, check **Unblock** if Windows shows it, then choose **OK**.
3. Close Revit.
4. Extract the zip file.
5. Copy the extracted `Amentum.bundle` folder to:

```text
%APPDATA%\Autodesk\ApplicationPlugins\
```

For a machine-wide install, an administrator can copy it to:

```text
%ProgramData%\Autodesk\ApplicationPlugins\
```

6. Confirm the installed folder looks like this:

```text
Amentum.bundle/
|-- PackageContents.xml
`-- Contents/
    |-- AmentumRevit.addin
    |-- AmentumRevit.dll
    |-- shared_parameter_map.json
    |-- dependency DLLs
    `-- Resources/
        |-- Icons/
        |-- README.md
        |-- QuickStart.html
        `-- LICENSE.txt
```

7. Start Revit 2026.
8. Open the **Amentum Tools** ribbon tab.

## Excel Export And Import

1. Select one or more Revit elements.
2. Choose **Amentum Tools > Export Excel**.
3. Open the exported workbook.
4. Use **Elements** as the exported reference data.
5. Use **Edit Guide** to see which columns are reference-only and which parameter keys are intended for round-trip edits.
6. Use **Update Status** to see whether each selected element can accept each parameter change. Reasons include missing parameters, read-only parameters, or unsupported storage types.
7. Add rows to **Updates** with:

```text
UniqueId | ParamKey | ParamGuid | Value
```

`ParamGuid` is optional. Leave it blank to use `ParamKey`.

8. Save the workbook.
9. In Revit, choose **Amentum Tools > Import Excel** and select the workbook.

Important: editing values directly on the **Elements** sheet does not change Revit. Import Excel only reads the **Updates** sheet.

## Other Tools

- **Panel View**: edit selected element parameters or browse project parameters when nothing is selected.
- **Build Parameters**: generate a shared parameter file and optionally bind selected definitions to the project.
- **Parameter Jam**: find duplicate project parameter bindings and keep one normalized binding set.
- **Export Image**: export the active Revit view to PNG.
- **Export DWG / Import DWG**: round-trip CAD exchange data.
- **Upload Mesh + Pts**: export and optionally upload OBJ mesh and PLY point cloud data.

## Configuration

`shared_parameter_map.json` controls the parameter keys, Revit names, and optional shared parameter GUIDs used by Excel import/export and other sync tools.

For scan uploads, set `AMENTUM_UPLOAD_URL` before starting Revit. Optional scan tuning variables include `AMENTUM_SCAN_RADIUS_FT`, `AMENTUM_SCAN_GRID_FT`, `AMENTUM_MESH_OBJ_PATH`, and `AMENTUM_POINTS_PLY_PATH`.

For DWG coordinate scaling, set `AMENTUM_CAD_TO_REVIT_SCALE` before starting Revit.

## Troubleshooting

- If the ribbon tab does not appear, confirm `Amentum.bundle` is directly under an Autodesk `ApplicationPlugins` folder and restart Revit.
- If Windows blocks the add-in, unblock the zip before extracting it.
- If **Build Parameters** preview does not open, install the Microsoft Edge WebView2 Runtime.
- If an Excel value does not import, check the **Update Status** sheet for the exact reason.

## Support

Contact Amentum at support@amentum.com or visit https://amentum.com.
