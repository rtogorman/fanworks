# FanWorks development progress

Living record of the correction plan from the v0.92.0 code review
(Claude Doc "FanWorks v0.92.0 — Code QA/QC Review"). Update after every milestone and
before starting a major task. If work is interrupted, read this file and inspect the code
(and `git log`) before resuming.

Last updated: 2026-11-08 (step 7 done; step 8 starting)

## Where things live

| What | Where |
|---|---|
| Owner's working copy (Windows, ARM64 laptop) | `C:\Users\rtogo\Documents\Codex\2026-09-25\what-x20` |
| Build + all checks + publish | `BuildRelease.ps1`, or double-click `RunBuild.cmd` (writes `build-log.txt`) |
| Release output | `outputs\FanWorks-win-x64-v<version>\FanWorks.exe` |
| Engineering checks | `tests/DuctSizer.Verification` (console program, `dotnet run -c Release`) |
| Interface checks | `tests/DuctSizer.UiVerification` (WinForms, Windows only) |
| Reference project (not in repo: contains a client drawing underlay) | owner's `%LOCALAPPDATA%\DuctSizer\duct-system.json`; golden results recorded below |

Git history exists in Claude's working repository (commits listed below). The owner's folder
does not have `.git` yet; a private GitHub repository is pending (owner to connect GitHub and
name the repo).

## Correction plan status

| Step | Status | Notes |
|---|---|---|
| 1. Baseline and safety net | Done | v0.92.0 committed as baseline `b5f8096`; reference project captured (golden results below) |
| 2. Failing tests for findings 1-4 | Done | `tests/DuctSizer.Verification/SystemTests.cs` |
| 3. Junction calculations (findings 1, 2, 5) | Done | `JunctionLegBuilder`, per-route path in `CriticalPathAnalyzer`, `TableRangeLog` |
| 4. Failures visible (findings 3, 8, 10) | Done | no silent zeros; transitional upper bound; fan hp validation; fan airflow from own system |
| 5. Project file (finding 4) | Done | `.fanworks` via `ProjectFile`; Save writes it first; Settings > Open Project |
| 6. Logging + v0.93 release (findings 9, 11) | Done | `DiagnosticLog`, crash reports, `AutomationOptions` replaces env vars; v0.93.0 built on owner's laptop: 41/41 engineering, 16/16 interface checks |
| 7. System solver into Core (finding 6) | Done | `SystemSolver` (Core) replaces the window's solver; bit-identical on reference project + 3 variants; 8 new solver checks (49/49); UI 16/16 (Wine) |
| 8. One coordinate model (finding 7) | Not started | |
| 9. Break up main window (findings 12-14) | Not started | |
| 10. Docs, independent verification, CI (findings 15-16) | Not started | table re-check and IP clearance need the owner / a second person |

## Completed changes (v0.93.0)

- Junction legs use the connected duct sizes (`JunctionLegBuilder`); legs with no sized duct fall back to the fitting's reference geometry and are listed as assumed.
- Critical path (`CriticalPathAnalyzer`, Core) charges each route the ASHRAE path it takes through a junction.
- An item that fails to calculate makes the total "not available" and is named; Auto-size Fan refuses and restores the fan.
- Drawn connections with no duct assigned are close-coupled (zero loss) and reported as `RouteAssumptions` (owner decision still open: keep, or block the total).
- Out-of-range table lookups produce `TableRangeWarnings` on fitting results, shown in the UI and PDF.
- Transitional-flow headline rate = upper bound (no midpoint blend).
- `FanPerformance` throws on invalid input; UI shows "not available".
- `.fanworks` project file (same versioned JSON as the working file, schema 12); newer formats refused with an update message; `.bak` kept.
- Unreadable working file is moved aside instead of overwritten.
- `DiagnosticLog`: `%LOCALAPPDATA%\FanWorks\logs`, daily log + crash reports, 30-day retention.
- `AutomationOptions` (internal, test-only via `InternalsVisibleTo`) replaces all `DUCTSIZER_*` environment variables.

## Step 7 record

- New `src/DuctSizer.Core/SystemSolver.cs`: `SystemSolver.Solve` (airflow balancing, temperature, junction
  legs, static pressure, calculation points), `OutletPressureToward`, `NodeLoss`, `DuctLoss`, `FittingLoss`,
  `BalanceDivergingOutletFlows`; `SpaceRoles`; `SystemSolution` (inlet pressures).
- Window methods `UpdateCalculatedLayoutValues`, `OutletPressureToward`, `NodePressureLoss`, `Safe*PressureLoss`,
  role/topology helpers now delegate; `ResolveConnectionAirflow`, `BalanceDivergingOutletFlows`,
  `Update*Junction` removed from the window (about 300 lines).
- Equivalence evidence: a harness loaded the reference project and 3 variants (locked unbalanced outlet,
  failing duct, unsized connection) into the real window under Wine before and after the move, and the Core
  solver on Linux; all solved states (every node pressure/airflow/temperature, duct and fitting flows, junction
  legs) were bit-identical. Harness lives outside the repo (`/home/claude/harness`, `/home/claude/equiv`).
- Observation (pre-existing behavior, kept): the diverging split uses the previous solve's stored straight/branch
  flows as weights, so re-solving an unchanged system can move the last floating-point digit. Same inputs give
  bit-identical results.
- Not moved (UI workflows, not calculations): source/sink editor reconciliation (`ReconcileBoundaryAirflowAfterDirectEdit`,
  `ApplyBalancedBoundaryAirflows`), automatic sizing dialogs.

## Commits (Claude working repo)

- `b5f8096` Baseline v0.92.0
- `f738f56` Core: junction legs, per-path critical path, table-range warnings, project file
- `ce7d9a6` App: use Core critical path, real junction legs, honest pressures
- `2c0047f` v0.93.0: project files, visible failures, diagnostics, no hidden switches

## Reference project golden results (v0.93.0 Core)

Owner's working project: schema 12, 18 ducts, 10 fittings, 21 layout nodes, PDF underlay.
Critical path 160.1122 Pa (0.6428 in. w.c.), L-013 -> L-012, complete, no assumptions.
Per-item losses (Pa): D-003 0.315, D-004 0.5338, D-016 1.3639, D-019 0.6722, D-020 0.4482,
D-012 0.3981, D-023 0.4071, D-007 0.2002, D-024 0.2002, D-025 0.2728, D-026 0.2728,
D-022 2.1823, D-027 1.5003, D-032 0.2473, D-002 0.1575, D-035 0.2002, D-034 0.038,
D-036 0.2728; F-001 3.8628, F-002 0.9657, F-004 0.9657, F-006 0.2414, F-007 0.618,
F-008 2.5752, F-009 0, F-010 2.5752, F-013 14.9453, F-014 44.836.
Steps 7-9 must reproduce these exactly (same inputs, same method).

Data observation for the owner (not a code defect): fan set to 1,000 cfm while the source and
the three sinks it feeds total 1,200 cfm.

## Outstanding issues and open decisions

- Owner decision: unsized connections = zero loss with a visible note (current) vs. block the total.
- GitHub repository not yet connected (needed for CI to run and to carry history to the owner's folder).
- Independent re-check of ASHRAE / Titus / Price table transcriptions needs the source documents and a second person.
- IP / licensing clearance for embedded ASHRAE tables and images, Titus catalog data and toolbox icons (owner / legal).
- Interface tests write to the real user log folder while running (harmless; could be redirected).

## Architecture notes and decisions

- `DuctSizer.Core` (net8.0, no UI dependency) holds all engineering calculations, SI internally; unit conversion at UI/report boundaries.
- `DuctSizer.WinForms` (net8.0-windows) holds the window, Designer, dialogs, PDF rendering hooks.
- Dependencies: `bblanchon.PDFium.Win32` 156.0.8076 (PDF underlay rendering). No other packages.
- Saved format: versioned JSON (`DuctSystemDocument.SchemaVersion`, migration chain in `DuctSystemStore.Load`).
- Tests are self-contained console programs (no test framework); the build script runs both.
- Linux verification aid (Claude's environment only): UI tests run under Wine with the win-x64 runtime; final verification is always the owner's Windows build.

## Next steps

See the step currently "In progress" above; its detailed plan is in the section below.

## Current task detail

Step 8: one coordinate model (finding 7). Survey how positions are stored (grid cells, parent-relative deltas,
absolute anchors, duct deltas) and resolved (`LayoutCoordinateResolver`), then make absolute coordinates the
stored source of truth with a schema migration, a deterministic resolver, and a closure check that flags loops
whose deltas disagree. Validate with coordinate tests, the reference project, and the interface suite.
