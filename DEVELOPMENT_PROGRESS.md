# FanWorks development progress

Living record of the correction plan from the v0.92.0 code review
(Claude Doc "FanWorks v0.92.0 — Code QA/QC Review"). Update after every milestone and
before starting a major task. If work is interrupted, read this file and inspect the code
(and `git log`) before resuming.

Last updated: 2026-11-09 (all ten steps done; v0.95.0 built and verified on the owner's laptop; history on GitHub)

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
- (next) Test warnings fixed; v0.95.0 Windows verification recorded

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
- Owner's folder: `MainForm.FittingLoss.cs`, `MainForm.DuctSystem.cs`, `MainForm.LayoutDesigner.cs` are one-line
  stubs left because the remote-file tool cannot delete; the owner can delete them (the build is fine either way).

## Current task detail

None in progress. Open items needing people or documents: independent re-check of the table transcriptions,
ASHRAE page citations and licensing (owner), code signing, and the reference project's fan airflow (1,000 cfm set
vs 1,200 cfm served).
