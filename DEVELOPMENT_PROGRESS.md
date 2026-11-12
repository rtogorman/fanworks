# FanWorks development progress

Living record of the correction plan from the v0.92.0 code review
(Claude Doc "FanWorks v0.92.0 — Code QA/QC Review"). Update after every milestone and
before starting a major task. If work is interrupted, read this file and inspect the code
(and `git log`) before resuming.

Last updated: 2026-11-09 (correction plan done; INTERFACE REDESIGN in progress — see "Interface redesign (v0.96)" at the end)

## Where things live

| What | Where |
|---|---|
| Owner's working copy (Windows, ARM64 laptop) | `C:\Users\rtogo\Documents\Codex\2026-09-25\what-x20` |
| Build + all checks + publish | `BuildRelease.ps1`, or double-click `RunBuild.cmd` (writes `build-log.txt`) |
| Release output | `outputs\FanWorks-win-x64-v<version>\FanWorks.exe` |
| Engineering checks | `tests/DuctSizer.Verification` (console program, `dotnet run -c Release`) |
| Interface checks | `tests/DuctSizer.UiVerification` (WinForms, Windows only) |
| Reference project (not in repo: contains a client drawing underlay) | owner's `%LOCALAPPDATA%\DuctSizer\duct-system.json`; golden results recorded below |

Git history: https://github.com/rtogorman/fanworks (branch `main`, pushed 2026-11-09). The owner's
laptop folder is a plain copy (no `.git`); it is kept in step by copying changed files.

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
| 8. One coordinate model (finding 7) | Done in full 2026-11-09 (see "Step 8 (full) record"); earlier partial record kept below | `LayoutGeometryCheck` detects non-closing loops, duct offset/length mismatches, unplaced components; shown on the Duct System summary, tooltip and PDF; resolver documented. Storage still relative (decision recorded) |
| 9. Break up main window (findings 12-14) | Done in full 2026-11-09 (see "Step 9 (full)" records A and B) | undo capped; 14 classes moved to own files; `LayoutModelCheck` per-type rules. Per-tab screen classes deferred (decision recorded) |
| 10. Docs, independent verification, CI (findings 15-16) | Done in code; human items open | XML docs with equations/sources; `docs/TABLE_VERIFICATION.md` (108 entries, all "Not verified"); ARCHITECTURE/DEVELOPER_GUIDE; culture test; warnings as errors; `global.json`; CI workflow (now on GitHub). Open: independent table re-check, IP clearance (owner) |

## Completed changes (v0.93.0)

- Junction legs use the connected duct sizes (`JunctionLegBuilder`); legs with no sized duct fall back to the fitting's reference geometry and are listed as assumed.
- Critical path (`CriticalPathAnalyzer`, Core) charges each route the ASHRAE path it takes through a junction.
- An item that fails to calculate makes the total "not available" and is named; Auto-size Fan refuses and restores the fan.
- Drawn connections with no duct assigned: v0.93 treated them as close-coupled; superseded 2026-11-09 by the owner's decision (they now block the total).
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

## Step 8 record

- New `src/DuctSizer.Core/LayoutGeometryCheck.cs` (4 issue kinds, 1 mm tolerance); 4 new checks (53/53).
- Duct System tab: the route label adds "Layout geometry: N issues to review" (orange) with a tooltip listing them;
  the PDF project summary carries the same note. Reference project: 0 issues.
- `LayoutCoordinateResolver` documented (deterministic: breadth-first from the origin in document order, first
  position wins). Its order was deliberately NOT changed: for a project whose offsets disagree, a different order
  would move components, which the plan forbids. The check reports the disagreement instead.
- Decision (deviation from the plan, owner to confirm): storing absolute X/Y/Z as the only position was deferred.
  Offsets are written in about 60 places in the Designer (connect, insert, remove-while-preserving-coordinates,
  move subtree, origin changes, table edits), and the interface suite pins many of those behaviors. Converting the
  storage is a Designer rewrite with real regression risk; detection removes the silent-failure part of finding 7
  now. Recommended as a separate, dedicated task: add stored absolute positions (schema 13) with offsets derived on
  demand, migrating each project through the resolver and refusing to migrate a project whose geometry check fails
  until the user resolves it.

## Step 9 record

- `BoundedHistory<T>` (Core): Designer undo and redo each keep at most 100 snapshots; snapshots exclude the PDF
  underlay and the company logo (both restored from the current project on undo).
- Pure moves (verified line-for-line): `LayoutTypes.cs`, `LayoutDesignerCanvas.cs`, `LayoutToolControls.cs`,
  `SourceSinkEditorDialog.cs`, `LayoutEquipmentDialog.cs`, `LayoutDialogControls.cs`,
  `LayoutComponentPlacementDialog.cs`, `LayoutCoordinateOriginDialog.cs`, `LayoutBranchChoiceDialog.cs`,
  `LayoutFittingNodeDialog.cs`, `LayoutFittingEditDialog.cs`, `LayoutDuctConnectionDialog.cs`,
  `LayoutDuctEditDialog.cs`, `FanInformationDialog.cs`. `MainForm.LayoutDesigner.cs` 8,718 -> 4,457 lines.
- `LayoutModelCheck` (Core): the per-type field rules in one documented place; reports dangling references, a
  second inlet on a non-converging item, invalid space roles, fan efficiency outside (0, 100], negative pressure
  drops, invalid calculation points and closed upstream loops. Shown with the geometry review ("Layout: N items to
  review", tooltip, PDF, log on change). The saved format is unchanged. Reference project: 0 issues.
- Decision (deviation, owner to confirm): one screen class per tab and a separate saved model per component type
  were deferred. The interface test reaches 99 private members of `MainForm` by name, so splitting the window
  means rewriting that test at the same time, and a per-type saved model means a format migration. Recommended
  order for a dedicated task: (1) replace the reflection in the interface test with an internal test API,
  (2) extract the Fitting Loss tab (least shared state), then Duct System, then Layout Designer, (3) per-type
  saved models with a schema 13 migration.

## Step 10 record

- XML documentation with equations, constants, sources and limits: `FrictionModel`, `AirProperties`,
  `DuctCalculator`, `GeometryCalculator`, `DuctSizingSolver` (incl. exact rounding/tie rules), `DuctMaterialCatalog`
  (finding 16: Legacy Reference 0.0007 ft default vs ASHRAE galvanized 0.0003 ft), `UnitConversions` (finding 16:
  249.08891 Pa per in. w.c. at 39.2 F), `FittingCatalog.All`, plus all new Core types from steps 7-9.
- Duct calculation report now states the pressure unit factor.
- `docs/TABLE_VERIFICATION.md`: generated register of all 108 catalog entries (code, inputs, source note) with blank
  columns for source page, verifier, date and result.
- `ARCHITECTURE.md`, `DEVELOPER_GUIDE.md`.
- New check: CSV and PDF exports are byte-identical under invariant, de-DE, fr-FR and en-US regional settings (58/58).
- `TreatWarningsAsErrors` in Core, app and engineering tests (default analyzers are clean; the stricter
  `latest-recommended` set reports ~270 style/perf items, mostly false-positive CA1305 on string holes and CA1861
  constant arrays, left for later).
- `global.json` (SDK 8.0.x, latest feature band); `.github/workflows/build.yml` (Windows: engineering checks,
  interface checks, publish, artifact). Not yet run: needs the GitHub repository.
- Version 0.94.0; README "What changed in 0.94.0".
- Reference project after all steps: solved state and per-item/critical-path results identical to v0.93.
- Not done (need people/documents): independent re-check of the transcriptions, ASHRAE page citations per table
  (needs the owner's ASHRAE source), image/icon and catalog-data rights, a LICENSE decision for commercial sale,
  code signing.

## Commits (Claude working repo)

- `b5f8096` Baseline v0.92.0
- `f738f56` Core: junction legs, per-path critical path, table-range warnings, project file
- `ce7d9a6` App: use Core critical path, real junction legs, honest pressures
- `2c0047f` v0.93.0: project files, visible failures, diagnostics, no hidden switches
- `8496ef2` Step 7: system solver into Core
- `448ba62` Step 8: layout geometry check
- `bc773c9` / `7905688` Step 9: bounded undo, file split, model check
- `ef46add` Step 10: documentation, register, CI; v0.94.0
- `659fda1` Progress: v0.94.0 verified on Windows
- `6eaccc0` Unsized connections block totals; Auto-size Fan isolates the critical path
- `bd496b2` Step 8: absolute positions stored (schema 13); positions edited relative
- `08e97aa` Step 9: per-type component data (schema 14)
- `9f86bbb` / `589371d` / `70265c9` / `1949647` Step 9: one screen class per tab
- `a3b08c6` v0.95.0: docs, unsized display fix
- `bebe54e` Test warnings fixed; v0.95.0 Windows verification recorded
- `b19b520` Window fits small screens; CI display size; CI green

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

- Independent re-check of ASHRAE / Titus / Price table transcriptions needs the source documents and a second person.
- IP / licensing clearance for embedded ASHRAE tables and images, Titus catalog data and toolbox icons (owner / legal).
- Interface tests write to the real user log folder while running (harmless; could be redirected).
- Remaining structural debt: `DuctSystemScreen` and `LayoutDesignerScreen` are each ~4,000 lines; splitting them
  further (e.g. table, filters, export; canvas, dialogs, wiring) is a later refactor, not a correctness issue.
- Stricter analyzer set (`latest-recommended`) not yet adopted (~270 findings, mostly style).

## Architecture notes and decisions

- `DuctSizer.Core` (net8.0, no UI dependency) holds all engineering calculations, SI internally; unit conversion at UI/report boundaries.
- `DuctSizer.WinForms` (net8.0-windows) holds the window, Designer, dialogs, PDF rendering hooks.
- Dependencies: `bblanchon.PDFium.Win32` 156.0.8076 (PDF underlay rendering). No other packages.
- Saved format: versioned JSON (`DuctSystemDocument.SchemaVersion`, migration chain in `DuctSystemStore.Load`).
- Tests are self-contained console programs (no test framework); the build script runs both.
- Linux verification aid (Claude's environment only): UI tests run under Wine with the win-x64 runtime; final verification is always the owner's Windows build.

## Next steps

No step is in progress; see "Current task detail" at the end for open items.

## Owner directives (2026-11-09)

1. Unsized connections: block the critical-path total until sized; never count them as zero loss. (Replaces the
   v0.93 "close-coupled assumption".)
2. Auto Size Fan: after sizing, isolate the critical path in the Duct System table (same as "Isolate critical path")
   instead of showing all items.
3. Step 8 in full: store absolute X/Y/Z as the position. Users still enter and see positions relative to the
   upstream component (or the downstream component when there is no upstream one).
4. Step 9 in full and step 10: continue through everything.
5. ASHRAE sourcing and licensing are being handled by the owner: not my tasks.
6. GitHub: https://github.com/rtogorman/fanworks (empty). Push refused until the Claude GitHub App is installed
   on the owner's account; remote `origin` is configured in the working repo.

Work order: directive 1, directive 2, step 8 (absolute storage), step 9 (per-tab screens, per-type models),
step 10 refresh, release v0.95.0.

## Directive 1 and 2 record (done)

- Unsized connections: `CriticalPathAnalyzer` reports "no sized duct" as an issue (total not available, Auto-size
  Fan refuses); `SystemSolver` makes downstream pressures NaN. `RouteAssumptions` and the window's assumption note
  removed. Checks: "An unsized connection blocks the total", "System solver: unsized connection makes downstream
  pressure not available". Interface test updated: refusal first, then the test sizes the connections.
- Auto-size Fan sets the Duct System filter to the isolated critical path (`_criticalPathFilterEnabled = true`);
  interface test asserts it.
- 58/58 engineering, 16/16 interface (Wine).

## Step 8 (full) record — absolute positions stored

- Schema 13: `SavedLayoutNode.XM/YM/ZM` + `HasStoredPosition`. `DuctSystemStore.Save` calls
  `LayoutPositions.StoreAbsolute` (resolve, store, rewrite second-inlet offsets and their duct records from the two
  positions, so a converging loop always closes). `Load` of a schema-13 file rebuilds every connection offset and
  the coordinate origin from the stored absolute positions (`DeriveOffsetsFromAbsolute`): the absolute positions
  are authoritative. Schema 12 files migrate by resolving their offsets (no component moves). Anchors of detached
  fragments are absolute inputs and are kept as entered.
- In memory the Designer still edits offsets (that is how users enter positions); positions are derived from them
  by the resolver, and written as absolute on every save. Stored positions are not used to place components in
  memory (avoids stale positions between saves).
- Dialogs: the equipment and fitting "position" row now shows and accepts the OFFSET FROM the upstream component
  (primary, else second inlet), or FROM the first downstream component (labelled DOWNSTREAM) when there is no
  upstream one; only an unconnected component uses absolute X/Y/Z. `LayoutPositionReference` converts
  offset <-> absolute; the commit path (`ApplyAbsoluteCoordinateEdit`) is unchanged, so downstream components stay
  put when a component is moved, as before. New-component placement was already relative to the upstream item.
- Checks: 3 new engineering checks (absolute positions saved; stored absolute positions win over offsets; schema 12
  migrates without moving anything) -> 61/61; interface check for the offset caption and offset->absolute
  conversion (upstream and downstream reference) -> 16/16 (Wine). Reference project: positions identical after
  save + load; results identical.

## Step 9 (full) record — part A: per-type component data (schema 14)

- `SavedLayoutNode` keeps common data; type-specific data lives in per-type blocks written only for that type:
  `Fan` (static pressure, efficiency), `Grd` (all GRD/terminal selection fields), `Space` (role),
  `CalculationPoint` (observed connection, fraction, point type, observed duct), `Fitting` (group, elbow angle).
  The old flat property names remain as forwarding properties so existing code is unchanged. `BranchOutletBelow`
  stays common (used by non-fittings too).
- Load moves flat fields from schema 1-13 files into the blocks before reading (`MoveFlatComponentFieldsIntoTypeBlocks`).
- Checks: each type saves only its own block; schema-13 flat fields load unchanged -> 63/63; interface 16/16 (Wine);
  reference project identical (schema 14 round trip, positions unchanged).

## Step 9 (full) record — part B: one class per tab

- `MainForm.FittingLoss.cs` -> `FittingCalculatorScreen`, `MainForm.DuctSystem.cs` -> `DuctSystemScreen`,
  `MainForm.LayoutDesigner.cs` -> `LayoutDesignerScreen`, and the calculator part of `MainForm.cs` ->
  `DuctCalculatorScreen`. `MainForm` (about 1,000 lines) keeps the window frame, tabs, units, menus, keyboard
  routing (`ProcessCmdKey` -> `LayoutScreen.TryHandleCommandKey`) and shared static control builders.
- Each screen receives the window (`_host`); cross-tab use is explicit (`_host.DuctSystemScreen._ductSystem`, etc.).
  Done with a compiler-driven tool (`/home/claude/extract_screen.py`, outside the repo) that fixed each compile error
  at its position; no logic was edited by hand apart from the keyboard routing and qualifying shared nested types.
- Interface test: reflection goes through `UiMember` (finds a member on the window or any `*Screen` property and
  redirects the target), so the test reaches the same members after the split.
- Verification after each extraction: 16/16 interface (Wine); start-up smoke test logs normally. Final: 63/63 and
  16/16.
- Remaining structural debt: the two system screens are still ~4,000 lines each and share state through the window.

## Step 10 refresh (2026-11-09)

- README "What changed in 0.95.0"; ARCHITECTURE (positions, per-type blocks, screens, unsized rule) and
  DEVELOPER_GUIDE (UiMember, position rule) updated; version 0.95.0.
- Fixed a last display path that still treated an unsized connection as zero loss (Duct System inlet pressure column).

## v0.95.0 Windows verification (2026-11-09)

- `RunBuild.cmd` on the owner's laptop: 63/63 engineering checks, 16/16 interface checks, published
  `outputs\FanWorks-win-x64-v0.95.0\FanWorks.exe`. 0 errors.
- The build reported four nullable warnings (CS8602) in the interface test program (RTF/tooltip text and a
  reflected method). Fixed afterwards by guarding those lookups; tests re-run 63/63 and 16/16 (Linux/Wine). No
  application code changed.
- History pushed to GitHub (`origin/main`); the CI workflow `.github/workflows/build.yml` runs on each push.
- First CI runs failed at the interface checks' opening-size check: the hosted runner's display is 1024 x 768,
  so Windows clipped the 1200 x 1200 window. Fixes (`b19b520`): (1) the app now opens fitted to the screen's
  working area when it is smaller than 1200 x 1200, centred (`MainForm.OpeningSize`, tested); (2) the workflow sets
  the runner display to 1920 x 1080 first, because other interface checks exercise real window sizes (under Wine at
  1024 x 768 a fitting-image layout check fails; at 1920 x 1080 all 16 pass). CI run 42716963071: 63/63, 16/16,
  FanWorks-win-x64 artifact uploaded.
- Laptop rebuilt after `b19b520`: 63/63, 16/16, 0 warnings, 0 errors; `outputs\FanWorks-win-x64-v0.95.0\FanWorks.exe`
  now matches `main`.
- CI annotation (not an error): actions/checkout, setup-dotnet and upload-artifact v4 target Node 20, which GitHub
  is retiring; bump them to their next major versions when convenient.
- Owner's folder: `MainForm.FittingLoss.cs`, `MainForm.DuctSystem.cs`, `MainForm.LayoutDesigner.cs` are one-line
  stubs left because the remote-file tool cannot delete; the owner can delete them (the build is fine either way).

## Current task detail

None in progress. Open items needing people or documents: independent re-check of the table transcriptions,
ASHRAE page citations and licensing (owner), code signing, and the reference project's fan airflow (1,000 cfm set
vs 1,200 cfm served).


## Interface redesign (v0.96) — owner spec 2026-11-09

Reference picture: `docs/ui-reference.jpg` (a style template, not to copy exactly). Owner approved four stages;
each stage ends with a Windows build on the laptop and a push. **Calculation code must not change**; the reference
project must give identical results (golden results above). Layout Designer canvas behaviour must stay identical
(grid, component icons, labels, arrows, drag and drop, PDF underlay, scroll-to-zoom, grid sizing).

### Spec

**Top tabs** (replace the Duct Calculator / Fitting Calculator / Duct System / Layout Designer buttons):
Project · Analysis · Reports · Quick Calc. Ribbon content changes per tab.

**Project tab ribbon**, left to right:
- Project group: New Project, Open, Save, Save As (replace the Settings dropdown's project items, incl. open/import).
- Project Settings (gear): a form with units (Imperial/Metric), project info (user name, project number, date,
  company logo — as today), and Calculation Settings (auto-sizing increment, auto-size rounding, equivalent
  diameter method, duct area calculation).
- Sizing Criteria (gear-style icon): the automatic sizing inputs form, as today's form, all current inputs.
- Round Sizes: dropdown — round auto-sized ductwork to nearest 0.5 in., 1 in. or 2 in.
- Select, Move.
- Components group: today's drag-and-drop toolbox (currently on the right of the Layout Designer) moved into the
  ribbon, grouped by kind; drag from the ribbon onto the grid.
- Auto Size: one button = today's "Size Undefined Components" + "Recalculate All Ductwork".
- Calculate: replaces "go to Duct System tab, click Auto-size Fan" (auto-sizes the fan / solves; shows critical path).
- Highlight Critical Path: toggle highlight on/off in the views.
- No Libraries, no Reports group, no Analysis Results.

**Project tab body**:
- Centre: today's Layout Designer, unchanged. Its toolbar: Select/pan as today, zoom-in and zoom-out magnifier
  buttons (scroll-wheel zoom stays), PDF import icon left of the grid icon, grid scale as a clean dropdown of
  ft-per-cell values (keep the ft/cell concept, not architectural scales), checkboxes Labels / Flow / Sizes.
- Left: Project Explorer. One system per file (no multi-system tree). Collapsible lists, collapsed by default:
  Equipment (fans, VAVs, coils...), Sources, Sinks, Dampers, Fittings, Duct Segments. Search box. Clicking an
  entry selects it and highlights it in the layout (like today's quick view row click).
- Right: **Selection Inspector** (keep that name). Three tabs: **Description**, **Calculations**, **Notes**.
  - Description: what the item is (duct: shape, material; fitting: type, ID, picture — click picture to enlarge),
    then editable inputs (method, airflow, air temp, length, target static pressure, sizes, radius ...), then outputs.
  - Calculations: the full existing worksheet for that item (duct calculator worksheet; fitting worksheet with all
    parameters, sub-equations, ASHRAE selections, flow and geometry) unchanged, scrollable. Items with no
    equations (e.g. a coil with manual static pressure drop): say "Manual entry" and show outputs.
  - Notes: free-text notes per component, saved in the project file (schema 15).
  - Works for every component type (ducts, fittings, diffusers/terminals, coils, fans, VAVs, dampers, ...).
- Bottom panel tabs (style as the picture): **System Overview** (today's system quick view, rows click-to-highlight),
  **Errors** (all errors in the project), **Validation** (QA/QC: ducts show friction/static-pressure sizing rate and
  velocity; fittings show the loss across the fitting). No Schedules tab.

**Analysis tab**: equivalent of today's Duct System tab. Top: system overview — airflow, critical-path pressure
loss, items in the critical route; then the 10 highest pressure-drop components; then the full item table with a
filter to isolate the critical path.

**Reports tab**: ribbon has Print Report (PDF). Body: scrollable page-by-page preview of the report (like a PDF
viewer).

**Quick Calc tab**: ribbon buttons Duct Calculator and Fitting Calculator; body shows today's calculator screens.
For quick one-off sizing without a project.

**Style**: tab strip, ribbon and icon style similar to the picture (line icons, grouped captions); Selection
Inspector tabs, collapsible section headers and label/value input rows like the picture; bottom tabs like the picture.
Icons drawn in-house (no new packages).

### Stages and status

| Stage | Scope | Status |
|---|---|---|
| R1 | Window shell, top tabs, Project ribbon, canvas toolbar; screens re-hosted (Analysis = Duct System screen, Quick Calc = calculators) | **Done** — laptop build 2026-11-09: 63/63, UI 17/17, 0 warnings, `outputs\FanWorks-win-x64-v0.96.0\FanWorks.exe`; GitHub CI green |
| R2 | Selection Inspector (Description / Calculations / Notes), notes in file (schema 15) | **Done** — laptop build: 65/65, UI 18/18, 0 warnings; CI green |
| R3 | Project Explorer, bottom panel (System Overview / Errors / Validation) | **Done** — laptop build: 65/65, UI 19/19, 0 warnings; CI green |
| R4 | Analysis summary + top 10, Reports preview, Quick Calc polish; interface tests rewritten; v0.96.0 | Done — laptop build 0 warnings, 65/65, UI 20/20; CI green; FanWorks-win-x64-v0.96.0 |

### R1 record

- New: `RibbonControls.cs` (tab button, ribbon group, large ribbon button, toolbar icon button, GDI+ line icons),
  `MainRibbon.cs` (tabs, ribbon rows per tab, page mapping, state sync). Version 0.96.0.
- `DuctSystemScreen.InitializeTabsAndSystem`: shell = top bar 46 px + ribbon 100 px + pages. Old header (118 px),
  navigation bar, Settings menu and Save button removed. Page 4 = Reports (placeholder text until R4). Opens on
  the Project page. Window title shows the project name.
- `ProjectSettingsDialog`: tabs Project Information / Units / Calculation Settings; `SolverSettingsDialog` deleted
  (its fields moved). `EditProjectSettings(page)` applies identity, calculation settings and units.
- `SetSizingIncrement` (Round Sizes) writes `DefaultSizingIncrementM` (same field as Calculation Settings);
  a non-standard saved increment shows as "Custom" in the menu.
- Layout page: blue header and right-hand toolbox removed; new `LayoutCanvasToolbar` (undo, redo, delete, zoom
  out/in + %, centre, PDF, grid icon + `LayoutScaleSelector` dropdown, view mode, Labels/Flow/Sizes check boxes,
  origin, source/sink editor). Move/Size Undefined/Recalculate/Highlight buttons are no longer on screen (objects kept
  for state text); the ribbon calls `ToggleLayoutRearrangeMode`, `AutoSizeLayout` (new: undefined components then all
  connected ducts, one undo step), `ToggleLayoutCriticalPathHighlight`. `UseLayoutSelectMode` (new).
- `CalculateSystem` (new, DuctSystemScreen): `TryAutoSizeFan`, save, refresh, highlight critical path, status text.
- Flow captions: `LayoutConnectionSizeCaption` returns size and/or airflow per the check boxes; canvas shows the
  caption when either box is on.
- Interface checks rewritten for the new frame; new group "Main tabs and Project ribbon commands" (17 groups).
- Analysis page still shows its own title band and buttons (Isolate / Auto-size Fan / View Fan) — R4 redesigns it.
  Quick Calc pages still have their blue title bands — R4.
- Owner's laptop: `src/DuctSizer.WinForms/SolverSettingsDialog.cs` must be emptied (stub) because the remote-file
  tool cannot delete; the interface check requires that class to be gone.

### R2 record

- `SelectionInspector.cs` (new): right column of the Project page (340 px, collapsible to a vertical tab). Header,
  item icon/title/subtitle, tabs Description / Calculations / Notes.
  - Content comes from `DuctSystemScreen.BuildDuctReportItem` / `BuildFittingReportItem` / `BuildNodeReportItem`
    (split out of `BuildPdfReportItems`; report unchanged), so the inspector and the PDF always agree.
  - Description: collapsible Details (type, ID, description, quick-check values, fitting picture with click to
    enlarge via `FittingImageViewer`), Inputs (+ **Edit Inputs…**, which opens the item's existing editor — same as
    double-click — so validation and connected-system rules stay in one place), Outputs.
  - Calculations: report calculation sections; equation lines rendered with `EngineeringMathRichText` (blank under
    Wine, which cannot draw RTF pictures — same as the calculators). No sections → "Manual entry" + outputs.
  - Notes: `DuctSystemDocument.ItemNotes` (schema 15) keyed by `L:` for drawing items (stable when a fitting gets
    defined), `D:` for connection ducts and off-drawing records, `F:` for off-drawing fittings. Saved 1.2 s after
    typing / on leave; pruned on save; undo/redo keep current notes.
  - Unsized connection: explains it blocks the total; Edit Inputs sizes it.
  - Quick-view rows for records not on the drawing show via `ShowRecord`; `EditSystemRecord` opens them in their
    calculator.
- Decision to confirm with owner: inputs are shown read-only with an Edit Inputs button rather than edited inline.

### R3 record

- Project page columns: `ProjectExplorer` (236 px, collapsible) | drawing over the bottom panel | Selection Inspector.
  `LayoutWorkspaceSplitter` is now horizontal: Panel1 = drawing, Panel2 = bottom panel (230 px, "Hide" button,
  reopen bar `LayoutSummaryReopenButton` under the drawing).
- `LayoutDesignerScreen.Panels.cs` (new partial): bottom panel tabs System Overview (the former quick-view grid,
  unchanged), Errors (`BuildProjectIssues`: critical-path analysis issues, layout model check, layout geometry
  check, undefined components, duct/fitting calculation failures; de-duplicated; count in the tab), Validation
  (every duct: size, airflow, velocity, static-pressure rate via the schedule's formatter, loss; every fitting:
  category, code, airflow, C, loss). Lists refresh after each edit and when their tab is opened; they navigate
  only on a user's click/arrow key (a grid auto-selects rows when filled, which used to move the drawing).
- `ProjectExplorer.cs` (new): TreeView groups Equipment (fan, coil, filter, custom, VAV), Sources, Sinks,
  Terminals (GRDs, terminal fittings), Dampers, Fittings (+ records not on the drawing), Duct Segments (every
  connection, sized or not, + off-drawing ducts), Calculation Points. Collapsed by default; search filters and opens
  matching groups. Selecting selects on the drawing, centres it, shows it in the inspector.
  Terminals and Calculation Points are extra groups beyond the owner's list (GRDs and points had no home).

### R4 record

- Analysis page (`DuctSystemScreen.Analysis.cs`): blue title band replaced by a summary — System overview tiles
  (airflow = fan's required airflow, critical-path pressure drop or "Not available", fan static pressure, items in
  the critical route) with the existing total/route/count labels, and "Highest pressure drops (top 10)" from
  `HighestPressureDrops` (SystemSolver.DuctLoss / FittingLoss and entered component drops; NaN items left out;
  clicking selects the row in the table). Page buttons Auto-size Fan / View Fan Information removed (ribbon
  Calculate / Fan Information); Isolate Critical Path and Delete all stay.
- Reports page (`ReportPreview.cs`): `WriteReportPdf` (extracted from Save, unchanged output) to a temp file,
  rendered with `PdfPageRenderSession` at 1100 px, shown as 820 px pages; rebuilt when the tab opens and on Refresh
  Preview. Print Report = Save dialog → PDF (atomic temp + move) → opened with the default viewer
  (`AutomationOptions.ReportPdfPath` for tests).
- Calculator pages: title bands 88 → 54 px.
- Unit fix: airflow shows cfm/L/s in the fan-sizing message, fan information and the report's design airflow.
- Interface checks: 20 groups (new: Analysis summary, report preview and Print Report).

### Polish round (P1–P5) — requested 2026-11-09 after R4

Ryan's requests (verbatim intent):
- P1 Ribbon: no cut-off words; groups spread to fill the whole ribbon width (Project tab especially). Logo symbol
  bigger; "FanWorks" title not cut off. Quick Calc duct calculator icon = a duct, not a calculator.
- P2 Explorer (left), inspector (right) and bottom panel draggable to resize. Bottom lists: add more useful columns
  to fill the width.
- P3 Selection Inspector: fixed values/outputs as labels (not text boxes); editable inputs as the proper control
  (dropdown/text) editable in place; Update/Save + Cancel at the bottom instead of Edit Inputs.
- P4 Report PDF: keep borders, title block, colours, symbols and equations; ONE page per duct / fitting / component
  (now two); title page keeps title block but centre redesigned (fuller, professional); view sheets unchanged except
  better placement of the graphic scale and grid-spacing note.
- P5 Canvas toolbar: PDF underlay button obvious (text + dropdown; may sit far right); Source/Sink editor button
  gets text; remove the crosshair "set coordinate origin" tool and function.

| Stage | Status |
|---|---|
| P1 | Done in code (0ba1f8b): root cause of cut-off text was Windows display scaling (Ryan's laptop 175 %): fonts grew, pixel sizes did not. `DpiScaling` + `DpiScaledForm` scale every window once; ribbon `RibbonPage`/`RibbonGroup`/`RibbonToolGrid` measure captions and fill the width; header logo 36 px, title auto-sized; duct icon. |
| P2 | Done in code: `PanelSplitter` drag bars (double-click resets); bottom splitter grip; System Overview 16 columns, Fill. |
| P3 | Done in code (4352cba): `IInlineEditable` on the 5 editors, `InlineEditSession`, shared Begin/Apply methods (`LayoutDesignerScreen.InlineEdit.cs`). Also fixed: component editor locked the pressure drop for all non-GRD types. |
| P4 | Done in code (70e50f3): `BuildItemSheets` one sheet per item with continuation; title sheet with contents; scale strip. Sample report 20 -> 16 pages. |
| P5 | Done in code (ea63ab6): captioned toolbar buttons; origin command + dialog removed (laptop copy of LayoutCoordinateOriginDialog.cs must be stubbed). |

Also fixed at 175 %: drawing table had no column style (canvas grew off-screen); bottom panel set up after Shown. v0.97.0.

**Polish round complete — v0.97.0** (synced at 16c63fe; laptop build 0 warnings, 65/65, UI 21/21 at the laptop's 150 % scale; viewed on the laptop).
Lessons: interface checks must run at a scaled display too (`/home/claude/ui150.sh`; `UICONTINUE=1` lists all failures);
`TextBoxBase.AppendText` restores the caret when the box has no size, so rich text is written with `EngineeringMathRichText.AppendAtEnd`;
`Control.Scale` scales "no limit" size limits (handled in `DpiScaling.ScaleControlTree`); laptop build is started with `outputs\_RunBuild.cmd`.
CI check "Designer workspace should pan upward and leftward" failed once on 0095a9a and passed on the next push — watch for repeats.

Wine DPI test: `/home/claude/dpi.sh 168` then screenshot at 2688x1680 (`/home/claude/shotboth.sh prefix`); always set back to 96 for UI tests.

### Round Q (requested 2026-11-10 evening, after v0.97.0) — target v0.98.0

- Q1 Inspector: X/Y/Z labels must line up with their boxes; tidy the "Offset from ... ΔX / ΔY / ΔZ" caption.
- Q2 Drawing: Esc deselects the selected/just-dropped item; Delete deletes it; right-click a placed item → menu
  Edit (opens the Selection Inspector on it) / Delete.
- Q3 Analysis Settings (ribbon button on Analysis tab → dialog): expected low/high for duct pressure rate
  (in. w.c./100 ft), duct velocity, fitting velocity, fitting loss (in. w.c.). Saved with the project. Top-10 list
  gets a "Within expected range" Yes/No column.
- Q4 Analysis graph under overview/top 10: line graph, x = component number along the path, y = static pressure
  (in. w.c.), one line per path (every source-to-sink path, not just critical).
- Q5 Report title sheet: condensed; number of branches/paths; the same graph; System Summary becomes a donut
  (duct segments / fittings / components) with leader labels and quantities.
- Q6 All pop-up forms: inputs far too long, poor formatting (e.g. Sizing Criteria). Redesign consistently.

| Q1 | Done | Q2 | Done | Q3 | Done | Q4 | Done | Q5 | Done | Q6 | Done |

Record:
- Q6: `DialogStyle` (shared field-layout pass for every `DpiScaledForm` except `MainForm`); axis labels sized to text
  (`LayoutDialogControls.CreateLocationEditor/CreatePositionEditor`).
- Q1: `InspectorAxisGroup`, `InlineEditRow.ForPosition`, sentence-case captions.
- Q2: Esc/Delete in `TryHandleCommandKey`; right-click menu `LayoutItemMenu`. Owner decision 2026-11-11: no delete
  confirmation for a component or duct anywhere (Delete key, menu, toolbar); status names it, Ctrl+Z undoes.
  Clear Layout keeps its three-way prompt.
- Terms (owner, 2026-11-11): a *path* = start of the system to one terminal outlet (the graph's lines, the
  alternatives compared with the critical path); a *branch* = anything tapped off the main. The graph and report
  name the path closest to the critical one (for balancing paths by rerouting).
- Q3: `AnalysisExpectations`/`AnalysisRangeCheck` (Core), schema 16, `AnalysisSettingsDialog`, top-10 column "Range".
- Q4: `SystemPathProfiles` (Core, checked against the critical-path analysis), `PressureProfileChart`.
- Q5: `BuildTitleSheet` rewritten (condensed project block, contents + donut left, path graph right).
- Checks: engine 71/71 (6 new), interface 23/23 at 100 % and 150 % under Wine (2 new groups).
- Test project with 4 paths: `/home/claude/mkproj` writes `/tmp/branches.fanworks`; report with
  `REPORTPROJ=branches /home/claude/report.sh`.

### Resume notes (update as work proceeds)

- Work in `/home/claude/fanworks` (git, origin = GitHub). Commit and push after every working sub-step so nothing
  is lost; the laptop folder gets changed files copied at the end of each stage.
- R1 synced to the laptop at commit 1920f58 (SolverSettingsDialog.cs stubbed there). R2 synced at b911839, R3 at 2cf58d1, R4 at 3cce1f1; all verified on the laptop. Redesign (R1–R4) complete.
- Open for Ryan: inline editing in the Selection Inspector (now read-only + Edit Inputs); extra explorer groups
  Terminals and Calculation Points kept (not in the original list).
- Sample project for screenshots: run the UI tests with `SAMPLE='Z:\tmp\sample.fanworks'`, copy it to the Wine prefix's `AppData/Local/DuctSizer/duct-system.json`. Linux helpers: `/home/claude/shot.sh` (screenshot of the app under Wine),
  `/home/claude/tour.sh` + `xclick.py` (click through tabs), `/home/claude/run-uitests.sh`.

### Round R (requested 2026-11-11 evening, after v0.98.1) — v0.99.0

Done: donut on the Analysis tab; Analysis page scrolls as a whole with the full-height table (top-10 keeps its own
scroll); SMACNA pressure class (rounded up from max(critical-path loss, fan static)), seal class by pressure class
(A ≥ 4, B = 3, C ≤ 2 in. w.c.), leakage class (A 3/6, B 6/12, C 12/24 round/rect, SMACNA Air Duct Leakage Test
Manual), F = CL·P^0.65 and total leakage = Σ F·A/100, on the Analysis overview and report title page (contents
condensed); surface area of ducts (exact) and fittings (estimated) in the tables and as a secondary worksheet after
the assumptions with a divider; worksheet titles Duct/Fitting/Component sizing calculation.
Checks: engine 74/74 (3 new), interface 24/24 at 100 % and 150 % under Wine.
Open with Ryan: newer SMACNA editions recommend Seal Class A for all ducts (leakage classes then by pressure class);
FanWorks follows seal class by pressure class as he specified. Item sheets with the surface-area worksheet usually
need a continuation sheet (report 28 -> 39 pages on the 4-path test project).

### Round S (requested 2026-11-11 22:13, after v0.99.0) — v0.100.0

Done: Pressure checkbox (duct inlet static pressure labels); draggable labels with right-click Add/Remove leader
and Reset, leader end draggable, saved (schema 17) and undoable; Analysis critical-path drops list (all items,
sorted and shaded, own scroll, System Overview height, no Critical column); Customize Duct Class window with
pressure/seal class overrides and Leakage factor F / Duct leakage renames; all pop-up windows restyled (rounded
inputs, cards, blue accents, navy title bar and app icon in place of the default symbol/brown border); new layouts
for Sizing Criteria, fitting gallery, underlay Move / Change Scale (Center Z added, nothing cut off) and Source and
Sink Editor (slim rounded scroll bar, tighter rows); Ctrl + middle-drag free rotation and a clickable view cube with
snap-back from the view list; PDF underlay in all views at its Z; Auto Duct Size Rounding 1 in. / 2 in. / Custom.
Checks: engine 74/74, interface 24/24 (+ new label/orbit/underlay check) at 100 % and 150 % under Wine.
Notes for Ryan: components are placed and moved only in the standard views (free view is for looking); the report's
isometric drawings do not show the underlay (only the Designer does); the title-bar colour needs Windows 11.
