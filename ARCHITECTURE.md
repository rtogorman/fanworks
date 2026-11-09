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
                    ──► CriticalPathAnalyzer (worst route; per-route junction path; Issues and RouteAssumptions)
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
- A drawn connection with no duct assigned is treated as close-coupled (no loss) and is listed in
  `CriticalPathResult.RouteAssumptions`.
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
- Positions are stored as X/Y/Z offsets from the upstream component. `LayoutCoordinateResolver` derives absolute
  positions (deterministic, first route wins) and `LayoutGeometryCheck` reports offsets that do not close.
- `LayoutModelCheck` documents and enforces which fields apply to which component type.

## Persistence

| What | Where | Notes |
|---|---|---|
| Working copy | `%LOCALAPPDATA%\DuctSizer\duct-system.json` | saved after every change; an unreadable file is moved aside, never overwritten |
| Project file | `<name>.fanworks` (green Save / Settings > Open Project) | same JSON as the working copy; written before the report |
| Report | `<name>.pdf`, `<name>.csv` | exports; the CSV holds ducts and fittings only |
| Logs | `%LOCALAPPDATA%\FanWorks\logs` | daily log and crash reports, 30-day retention |

`DuctSystemStore.Save` writes to a temporary file, keeps the previous file as `.bak`, then replaces it, so an
interrupted save cannot destroy the last good file. Files carry `SchemaVersion` (currently 12) and
`DuctSystemStore.Load` upgrades older versions step by step; a file from a newer FanWorks is refused with an update
message. Changing the saved format means: increment the version, add an upgrade step, add a round-trip check.

## The application

`MainForm` is one window split across partial files by tab: `MainForm.cs` (Duct Calculator), `MainForm.FittingLoss.cs`
(Fitting Calculator), `MainForm.DuctSystem.cs` (Duct System schedule, report, project files) and
`MainForm.LayoutDesigner.cs` (Layout Designer). Dialogs, the Designer canvas and drawing helpers are separate classes
in their own files. The window holds UI state only; calculations are delegated to Core.

`Program.cs` starts logging and installs the crash handlers. `AutomationOptions` holds test-only switches (dialog
suppression, storage path); they are internal and reachable only by the interface test assembly.

## Known structural limits (see DEVELOPMENT_PROGRESS.md)

- The main window is still large and the interface test reaches many of its private members by name.
- Positions are stored as relative offsets rather than absolute coordinates.
- Every component type shares one saved record (`SavedLayoutNode`); type rules are enforced by `LayoutModelCheck`.
