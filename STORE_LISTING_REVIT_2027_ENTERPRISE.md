# Autodesk App Store Listing Draft — Amentum Revit 2027 (Enterprise Version)

## Upload Package

`Amentum-Revit-2027-1.0.0.0.zip`

## Product Title

Amentum Revit

## Version

1.0.0.0

## Supported Product

Autodesk Revit 2027

## Category

Productivity

## Short Summary

Amentum Revit is a Revit 2027 add-in for structured parameter exchange, CAD coordination, shared-parameter administration, and controlled model export workflows.

## Enterprise Description

Amentum Revit is a Windows-based Autodesk Revit 2027 add-in intended for disciplined project delivery and internal engineering workflows.

The add-in provides controlled tools for parameter exchange with Excel, CAD round-trip synchronization, category-based shared parameter generation, batch parameter processing, and 3D DWG export with associated metadata outputs.

This release is scoped specifically to Autodesk Revit 2027 and is packaged for straightforward deployment through standard Autodesk bundle installation paths. The package is branded for Amentum and includes updated support and contact metadata for enterprise distribution.

The included workflows are designed to support repeatable project execution, reduce manual parameter handling, and improve consistency across model coordination tasks.

## Functional Scope

- Export Revit element parameters to Excel workbooks
- Import parameter updates from Excel workbooks
- Export active 3D views to DWG with metadata sidecar files
- Apply CAD index deltas back to mapped Revit elements
- Generate category-based shared parameter files
- Run batch family and parameter update tools
- Open the local Amentum web dashboard when configured

## Compatibility

- Autodesk Revit 2027 only
- Windows 64-bit
- Package version: 1.0.0.0

## Installation

1. Download `Amentum-Revit-2027-1.0.0.0.zip`.
2. Extract the archive.
3. Copy the `Amentum.bundle` folder to one of the following paths:
   - `%APPDATA%\Autodesk\Revit\Addins\2027\`
   - `%PROGRAMDATA%\Autodesk\Revit\Addins\2027\`
4. Restart Autodesk Revit 2027.
5. Confirm that the **Amentum** ribbon tab is available.

## Deployment Notes

- This package is prepared for Revit 2027 only.
- This release does not include the companion mobile application.
- The package is intended to be distributed as the provided Autodesk bundle zip without modification.
- Support and contact metadata are aligned to Amentum.

## Operational Notes

- Excel-based workflows depend on valid workbook input and expected parameter mappings.
- CAD round-trip workflows depend on valid mapping data between CAD objects and Revit elements.
- Optional local web workflows depend on the availability and configuration of the local Amentum web service.
- If web endpoints are not configured or available, local Revit and Excel workflows remain the primary supported use case.

## Release Notes

### 1.0.0.0

- Established Amentum-branded Revit 2027 package
- Updated company, support, and contact metadata
- Removed legacy BIM GUI branding from packaged documentation
- Removed companion mobile app deliverables from this release
- Aligned package manifest and deliverable naming to Revit 2027 scope

## Support

- Website: https://amentum.com
- Email: support@amentum.com

## Keywords

Amentum, Autodesk Revit 2027, enterprise, Excel parameter exchange, CAD sync, DWG export, shared parameters, batch workflows

## Submission Guidance

- Use this description where a formal or procurement-oriented tone is preferred.
- Do not describe this package as supporting Revit 2025 or Revit 2026.
- Ensure screenshots, captions, and support fields use Amentum branding only.
