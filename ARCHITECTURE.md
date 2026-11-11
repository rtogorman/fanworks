# FanWorks architecture

FanWorks is a Windows desktop application for HVAC duct sizing and duct-system design. This document describes how
the code is organized and how a result is produced, so a developer can trace any number on screen or in a report
back to the code that calculated it.

## Projects

| Project | Target | Purpose |
|---|---|---|
| `src/DuctSizer.Core` | `net8.0` | All engineering calculations, the saved data model, file formats and system analysis. No UI dependency. |
| `src/DuctSizer.WinForms` | `net8.0-windows` | The application (`FanWorks.exe`): main window, Layout Designer, dialogs, PDF page rendering. |
| `tests/DuctSizer.Verification` | `net8.0` | Engineering checks: a console program; exit code 0 means every check passed. |
| `tests/DuctSizer.UiVerification` | `net8.0-windows` | Interface checks that drive the real main window (Windows only). |

Only one package is used: `bblanchon.PDFium.Win32` (PDF underlay rendering). Everything else is the .NET 8 base
library and Windows Forms.

## Units

Calculations are SI throughout: metres, m3/s, Pa, degrees C, kg/m3, Pa s. Conversion to and from inch-pound
units happens only at the edges (input parsing, display, reports) through `UnitConversions` (`Models.cs`). Saved
files store SI values.

## Calculation path

```
Inputs (UI) ──► DuctCalculationInput ──► DuctCalculator ──► AirProperties   (density, viscosity)
                                                 │         └► GeometryCalculator (area, Dh, Huebscher De)
                                                 └► FrictionModel (laminar / Colebrook / transitional bounds)
                                                 = DuctCalculationResult (rate, bounds, total, all intermediates)

DuctSizingInput ──► DuctSizingSolver (velocity or friction target, bisection, rounding) ──► DuctCalculator

FittingLossInput ──► FittingCatalog.All[code] (ASHRAE / manufacturer table, interpolated, range-flagged)
                  = FittingCalculationResult (C, reference velocity, loss, TableRangeWarnings)

DuctSystemDocument ──► SystemSolver.Solve   (airflow balance, temperatures, junction legs, static pressure)
                    ──► CriticalPathAnalyzer (worst route; per-route junction path; Issues)
                    ──► LayoutGeometryCheck / LayoutModelCheck (consistency reports, change nothing)
                    ──► FanSizing / FanPerformance (required airflow, brake horsepower)
```

Each public calculation class documents its equations, constants, sources and limits in its XML documentation.
Results carry their intermediates (Reynolds number, regime, friction factor bounds, Colebrook iterations and
residual, basis description) so the PDF report can show the full substitution for every item.

### Failure rules

- Core methods reject invalid input with `ArgumentOutOfRangeException` and numerical failure with
  `InvalidOperationException`. They never return a plausible fallback number.
- In the system analysis an item that cannot be calculated is NaN ("not available") and is named in
  `CriticalPathResult.Issues`; totals that depend on it are not shown as valid, and Auto-size Fan refuses.
- A drawn connection with no sized duct has an unknown loss: it is an issue that blocks the total and Auto-size Fan,
  and pressures downstream of it are NaN. It is never counted as zero loss.
- Digitized tables never extrapolate; an input outside a table uses the edge value and adds a
  `TableRangeWarnings` entry that appears in the calculator and the PDF.

## The system model

`DuctSystemDocument` (`DuctSystem.cs`) is the single authoritative model: the schedule (`Ducts`, `Fittings`), the
Layout Designer components (`LayoutNodes`), project identity, calculation defaults and the optional PDF underlay.
The window edits this model and redraws from it; nothing is inferred from drawing positions.

- Connections are stored on the downstream component: `ParentNodeId` (+ `ConnectionDuctSequence` for the duct on
  that connection), and for a converging fitting a second inlet (`SecondaryParentNodeId`,
  `SecondaryConnectionDuctSequence`). `LayoutTopology` (`SystemAnalysis.cs`) answers topology questions.
- A diverging fitting's main (straight-through) outlet is the downstream component on the fitting's own branch
  (`BranchId`); the other outlet is the branch.
- Positions: each component stores its absolute X/Y/Z (`XM/YM/ZM`, schema 13+), and these are authoritative when a
  file is opened (`LayoutPositions.DeriveOffsetsFromAbsolute` rebuilds every connection offset from them). In memory
  the Designer edits offsets, because users enter positions relative to the upstream component (or the downstream
  component when there is none); `LayoutCoordinateResolver` derives positions from the offsets and
  `LayoutPositions.StoreAbsolute` writes them back as absolute on every save, rewriting second-inlet offsets so a
  converging loop always closes. `LayoutGeometryCheck` reports any remaining geometric inconsistency.
- Component data: common fields on `SavedLayoutNode`; type-specific data in per-type blocks (`Fan`, `Grd`, `Space`,
  `CalculationPoint`, `Fitting`), written only for that type (schema 14). `LayoutModelCheck` enforces the per-type
  rules.

## Persistence

| What | Where | Notes |
|---|---|---|
| Working copy | `%LOCALAPPDATA%\DuctSizer\duct-system.json` | saved after every change; an unreadable file is moved aside, never overwritten |
| Project file | `<name>.fanworks` (green Save / Settings > Open Project) | same JSON as the working copy; written before the report |
| Report | `<name>.pdf`, `<name>.csv` | exports; the CSV holds ducts and fittings only |
| Logs | `%LOCALAPPDATA%\FanWorks\logs` | daily log and crash reports, 30-day retention |

`DuctSystemStore.Save` writes to a temporary file, keeps the previous file as `.bak`, then replaces it, so an
interrupted save cannot destroy the last good file. Files carry `SchemaVersion` (currently 14) and
`DuctSystemStore.Load` upgrades older versions step by step; a file from a newer FanWorks is refused with an update
message. Changing the saved format means: increment the version, add an upgrade step, add a round-trip check.

## The application

`MainForm` is the window frame: tab setup, units, menus, keyboard routing and shared control builders. Each tab is
its own class that receives the window (`_host`): `DuctCalculatorScreen`, `FittingCalculatorScreen`,
`DuctSystemScreen` (schedule, report, project files, critical path display) and `LayoutDesignerScreen`. The window
exposes them as `CalculatorScreen`, `FittingScreen`, `DuctSystemScreen` and `LayoutScreen`; cross-tab calls go
through these properties, so every dependency between tabs is visible in the code. Dialogs, the Designer canvas and
drawing helpers are separate classes. Screens hold UI state only; calculations are delegated to Core.

Window frame (v0.96): a dark top bar (brand, the main tabs Project / Analysis / Reports / Quick Calc, project
identity) over a ribbon, over the page host `_mainTabs` (headers hidden). `MainRibbon` builds the tabs and one ribbon
row per tab and only calls existing screen commands; `RibbonControls.cs` holds the reusable pieces (`AppTabButton`,
`RibbonGroup`, `RibbonButton`, `ToolbarIconButton`) and `RibbonIcons`, line icons drawn with GDI+ (no image files).
Page mapping: Project = Layout Designer, Analysis = Duct System, Reports = report page, Quick Calc = duct or fitting
calculator. Page order in `_mainTabs` is kept (0 calculator, 1 fitting, 2 system, 3 layout, 4 reports). The Designer's
component tools are created by `LayoutDesignerScreen.CreateRibbonLayoutTool` and placed in ribbon groups.

Project page (v0.96): `ProjectExplorer` | drawing over the bottom panel (`LayoutDesignerScreen.Panels.cs`: System
Overview, Errors from `BuildProjectIssues`, Validation) | `SelectionInspector`. The inspector shows the per-item
report content (`DuctSystemScreen.BuildDuctReportItem` / `BuildFittingReportItem` / `BuildNodeReportItem`, also used
by the PDF), so screen and report cannot disagree; item notes live in `DuctSystemDocument.ItemNotes` (schema 15).
Analysis page summary: `DuctSystemScreen.Analysis.cs` (`HighestPressureDrops`). Reports page: `ReportPreview`
(renders `WriteReportPdf` output with the PDFium session used for underlays).

`Program.cs` starts logging and installs the crash handlers. `AutomationOptions` holds test-only switches (dialog
suppression, storage path); they are internal and reachable only by the interface test assembly.

## Known structural limits (see DEVELOPMENT_PROGRESS.md)

- The screens still share state through the window (for example the Designer reads the Duct System screen's
  project), and the two system screens are large (about 4,000 lines each). Further splitting should follow the
  feature seams inside them (report building, project files, Designer editing operations).
- The interface test reaches private members by name through `UiMember`, which looks on the window and its screens.

## Display scaling (v0.97)

Windows are built in code with sizes in 96-DPI pixels; text is in points. `DpiScaledForm` (base of every form)
scales the whole window once in `OnLoad` (`DpiScaling.ScaleControlTree`: bounds, margins, table styles, grid
columns and rows, tree rows). Controls created or painted later use `DpiScaling.Scale`. The app is system-DPI
aware, so one factor applies to the process. At 100 % the factor is 1 and nothing changes.

## In-place editing (v0.97)

Editor windows implement `IInlineEditable` (rows of their own controls, a status line, `TryAcceptInline` = the OK
checks). `LayoutDesignerScreen.CreateInlineEditSession` builds an `InlineEditSession` from the same Begin/Apply
methods the editor windows use (`LayoutDesignerScreen.InlineEdit.cs`), so the inspector and the windows cannot
apply edits differently. The inspector rebuilds its rows when an editor's rows change and defers its own refresh
while an edit is being applied.

## Report sheets (v0.97)

`DuctSystemPdf.BuildItemSheets` lays out one item per sheet (left column `DrawSideRows`, worksheet
`DrawCalculationBlocks` in two columns) and continues on extra sheets when needed; page numbers are counted in a
first pass. `BuildTitleSheet` receives the report contents for its page list.
