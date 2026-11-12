# FanWorks

FanWorks is an independent, standalone Windows HVAC duct-sizing, fitting-loss, and system pressure-loss calculator. It is written in C# with .NET Windows Forms and does not use code or assets from McQuay DuctSizer.

## Capabilities

- Round and rectangular straight-duct calculations
- Entered-size checking and automatic sizing by target friction or maximum velocity
- Automatic mathematical rounding to the nearest increment, or explicit round-down sizing
- Imperial and metric units
- Actual and equivalent-round areas and velocities
- Huebscher or hydraulic equivalent diameter
- Actual-area or legacy equivalent-round pressure-loss basis
- Temperature-dependent air density and viscosity at fixed 50% RH and standard sea-level pressure
- Material roughness presets for galvanized, lined, plastic, stainless, aluminum, and fully extended flexible duct
- Responsive interface with compact, right-aligned input labels, value-sized dropdowns and text boxes, context-sensitive inputs, content-sized input regions, clean label/value summary outputs in the lower portion of each left column, and detailed Calculations panels on the right that can be hidden, restored, or expanded
- Professional HVAC application icon, roomier navigation tabs, rounded cards and buttons, and polished table styling
- Local Duct System tab with automatic IDs, editable descriptions, editable fitting IDs (including confirmed duplicates), per-item losses, static pressure in/out, and critical-route total pressure loss
- Context-sensitive **Save Duct Changes** and **Save Fitting Changes** actions appear only while an existing schedule or Designer item is being edited; the active item ID and description are shown above its inputs
- AFT-inspired Layout Designer with a horizontally scrolling toolbox above the grid, a collapsible and resizable Duct System quick-view schedule on the left, small free-standing symbols, selectable directional duct lines, and ID-only labels. New components remain unconnected wherever they are dropped or moved; topology is created only by explicitly dragging between connector circles
- Separate draggable Space Source and Space Sink boundaries: sources accept airflow, temperature, and gauge pressure, while sinks report calculated terminal airflow and temperature. A direct **Source/Sink Editor** Designer command lists every boundary, provides editable CFM fields, shows each calculated pressure as a read-only in. w.c. value, and prevents saving while source and sink totals are unbalanced
- Inline pressure, temperature, velocity, and airflow calculation points use distinct instrument symbols, insert into duct connections, and report only their applicable schedule measurement
- Isometric-axis provisional connection circles on every component; circles align with the X, Y, and Z grid directions, and unused candidates disappear after the fitting-specific connection capacity is reached
- Explicit connector-to-connector dragging is authoritative: the connector where the drag starts is upstream, the connector where it ends is downstream, and the duct is created in that direction
- A selected Designer duct exposes **Reverse Flow Direction**, which reverses its graphical arrow and its upstream/downstream schedule entries without redrawing the connection
- Automatic placement ranks every compatible unused inlet and outlet by its actual grid distance, skips components whose connection capacity is already filled, and interrupts an existing duct only when the component is dropped directly on its line
- Free-standing, consistently styled Apache-2.0 Material Design HVAC/equipment, fitting, GRD, space, and instrument symbols without enclosing component cards
- Designer Undo and Redo history for layout, connection, and linked calculation changes
- Removing a component rebases surviving connections against their unchanged absolute coordinates; removing a duct anchors the newly disconnected downstream system at its former absolute position so unrelated components never jump
- Click selection with a visible highlight and Delete action; components can be dragged onto an existing connection to insert them between its endpoints
- Automatically inferred connections that serve as duct segments: new ducts default to target-static-pressure autosizing from the upstream component airflow, while any connection that cannot be sized remains a valid red graphical link
- Signed one-row Relative ΔX/ΔY/ΔZ inputs are confirmed when a newly placed component becomes connected to a defined component. The proposed X/Y values are populated from the selected grid location in real drawing units and Z defaults to zero. The first component is the `0,0,0` origin, and the system grid also resolves read-only absolute X/Y/Z coordinates through the connection graph
- Projects saved with a coordinate origin keep it: its absolute coordinate offsets the resolved coordinates of the connected system without changing duct geometry (the command to set a new origin was removed in 0.97)
- Automatic two-way refresh when opening Duct System or Layout Designer keeps linked airflow, coordinates, air-system metadata, and geometry synchronized
- Canvas selections and the collapsible Duct System quick view stay synchronized in both directions, including exact duct-connection selection
- Full-lattice grid-intersection snapping with a visible blue drop-target dot at every isometric intersection, a boundary-free virtual viewport with stable grab-and-drag panning, mouse-wheel zoom, compact component editors, and selectable highlighted duct segments
- Permanently coordinate-scaled projection uses signed absolute X/Y/Z coordinates with an editable feet-per-grid-cell scale; changing the visual scale never changes engineering coordinate inputs. A projection dropdown provides Isometric Positive, an Isometric Negative camera angle formed by rotating the complete positive view 90 degrees counterclockwise (+Y in the former +X direction and +X in the former -Y direction), and Top-Down, where positive X runs right and positive Y runs down
- A construction-sheet PDF can be imported as a Top-Down drawing background. Multi-page files expose page selection; custom paper-inch-to-drawing-foot scale and absolute X/Y/Z center inputs align the sheet to the grid. The embedded page can be shown, hidden, moved, rescaled, replaced, or deleted, remains proportional when Grid Scale changes, and is retained with the local project while the grid and system symbols remain above it
- Component and duct deletion snapshot every surviving absolute coordinate before changing topology. Removing a component deletes its adjacent graphical duct connections and anchors each disconnected downstream system in place; it never bypasses the deleted component or shifts either side. Multi-axis connections are divided into axial runs with explicit elbow components, so no diagonal duct segment represents an unmodeled direction change
- A center-origin Designer workspace opens immediately on the first system component, can be panned in every direction, and includes a Center command that returns to the system start; toggleable Move mode repositions symbols for readability without changing existing links or calculations
- Initial component placement asks only for relative X/Y/Z location; undefined components remain red, do not create schedule values or an automatic duct size, and become defined through their normal double-click editor
- Component IDs sit beside their symbols and duct IDs are offset perpendicular to their routed line so labels do not cover the schematic. Prefixes identify the item class: `F-` fittings, `S-` sensors/calculation points, `T-` terminals/GRDs, `L-` source/sink spaces, `Fan-` system fans, and `C-` other components
- Designer ducts route only along the isometric X, Y, and Z axes, adding a grid-aligned bend when their endpoints do not share one axis
- Designer fitting Edit includes the filtered ASHRAE fitting gallery, while duct Edit supports Check Existing, Autosize (SP Loss), and Autosize (Velocity) without leaving the Designer; both compact editors require Calculate before their green update action is enabled
- Pressure-loss and velocity autosize targets appear directly below duct length in the Calculation section, while fitting summary outputs sit immediately below the fitting-specific inputs
- Titus DESV VAV performance selection in the Fitting Calculator and Layout Designer. **VAV with Reheat** accepts terminal size, hot-water coil row count, and airflow and interpolates the published coil airside ΔPs table. **VAV without Reheat** accepts terminal size and airflow and interpolates the published full-open-damper Min ΔPs table. Unsupported size/row combinations and airflow outside the supplied chart range are rejected rather than extrapolated
- Automatic branch numbering, Main Path/Branch Path selection for a split's first downstream connection, automatic use of its last remaining outlet for the second connection, and support for two downstream connections from a split
- Graph-sequential system scheduling follows each drawn route from its source through duct segments, fittings, equipment, branches, sinks, and calculation points rather than grouping rows by item type
- Air-system type, branch ID, upstream item ID, and downstream item ID fields in the system schedule; air-system choices include supply, return, exhaust, and other, and the air-system dropdown arrow appears only while its cell is being edited
- Airflow conservation automatically balances diverting outlets to the common inlet and sums converging inlets into the outlet. A user-defined split outlet locks that value and the remaining outlet is calculated automatically; accepted unbalanced overrides remain valid and are highlighted orange in the table and Designer
- Automatic longest-route analysis after every system change across every graphical root-to-terminal path; the net total pressure-loss summary sums the verified loss column only on the critical route, explicitly excludes positive fan static, and includes highlighted rows and a one-click table filter. **Auto-size Fan** assigns the single system fan's airflow and static pressure from the calculated system demand and critical-path net loss, while clear safeguards require a fan and prevent a second fan. Linked fitting airflow is synchronized before pressure propagation, so when entered fan static equals that same net route loss, the downstream Space Sink solves to `0 in. w.c.`
- Space boundary, airflow, temperature, pressure, and air-system values; fans accept system static pressure and numeric efficiency in percent, and fans, coils, filters, grilles, diffusers, spaces, and calculation points appear in the system schedule. Fan sizing reports required brake horsepower from airflow, static pressure, and efficiency
- A roomier project identity header above the tabs shows a larger company logo, project name, project number, creator, and last-edited time from **Settings**. Calculation defaults are available under **Settings > Calculation Method Inputs**.
- The green **Save** action writes a `.fanworks` project file (the complete model) plus a formatted PDF report and CSV schedule; **Settings → Open Project** reopens a `.fanworks` project, or imports ducts and fittings from a CSV schedule
- Exported PDFs use a landscape Letter title-block format throughout: a project/fan-sizing cover sheet, exact live-canvas Designer captures for Isometric Positive, Isometric Negative, Top-Down, and highlighted critical-path views, readable paginated system schedules, and one complete inputs/outputs/calculations sheet per connected item. Each component sheet adds a compact QA/QC quick-check overview of its main design values while retaining the complete detailed inputs, outputs, equations, assumptions, and source tables. Every sheet carries the FanWorks ribbon, company logo, project identity, print date, and page number
- Confirmed clear-layout choices for retaining or deleting table data, plus a dedicated Delete All system action
- Local JSON persistence with no database, account, or network dependency

## ASHRAE fitting calculator

The Fitting Calculator tab follows the numbering and catalog in the supplied ASHRAE supplemental fitting document: `1-1` through `7-15`, covering 103 entries, exits, elbows, transitions, junctions, obstructions, and fan-system connections.

All 108 fitting selections are embedded in the standalone program: 103 ASHRAE reference sections, three manufacturer GRD selectors, and two Titus DESV VAV selectors. A first dropdown groups them as Elbows, Transitions, Diverting Fittings, Converging Fittings, Dampers, Terminals & Takeoffs, VAVs, GRDs, or Fan Exits and Plenums; the second dropdown displays only the fitting numbers in that group. The GRD gallery is limited to the Titus 350R grille, Price SCD square diffuser, and Titus TBD-80 D linear diffuser. The VAV group contains Titus DESV selections with and without hot-water reheat. The selected image and its number and description appear beside the selectors. Clicking the image opens a scrollable full-size viewer.

For each ordinary fitting, the user enters airflow and a round diameter or rectangular width and height. Junction fittings expose common, straight/main, and branch airflow and geometry, plus a calculated-path selector. Fitting-specific categorical values—such as branch angle, converging/diverging flow, published area-ratio configuration, and second-branch flow ratio—are displayed as dropdowns. Velocity pressure is calculated at the entered air temperature, and fitting pressure change is calculated as signed `C × VP` using the ASHRAE reference section.

The Fitting Calculator infers and locks the reference duct shape for round-only and rectangular-only ASHRAE fittings. Dual-shape fittings retain a user-selectable shape. Fitting group, fitting number, airflow, temperature, reference shape, and reference geometry are collected in one compact block beside the fitting image; rectangular width and height share a concise geometry row. The gallery button matches the image width, and fitting-specific inputs follow immediately below.

One hundred one fittings have built-in equations or one-, two-, and three-dimensional table interpolation. This includes signed junction coefficients, ragged triangular tables, velocity-band tables, multi-branch wye/cross models, and the two-direction 4-6 transition table. Fittings `5-4` and `5-5` remain clearly marked reference-coefficient entries because their very large conical-main table families have not yet been safely digitized; the program does not invent those values. Compatible fittings saved by earlier versions are upgraded and supplied with new default parameters when loaded.

## Calculation methods

- Darcy-Weisbach pressure loss
- Hagen-Poiseuille `f = 64/Re` for round laminar flow
- Shah-London rectangular correction for rectangular laminar flow
- Implicit Colebrook equation solved by safeguarded bisection for turbulent flow
- Unblended laminar and turbulent bounds from `Re = 2300` through `Re = 4000`
- Outer duct sizing solved by adaptive bracketing and bisection

The straight-duct model assumes steady, fully developed flow. Leakage, flex-duct compression, elevation pressure, and nonstandard atmospheric pressure are not included in version 0.59.1. Entered ΔZ is used for layout and duct length, not buoyancy or elevation-pressure calculations. ASHRAE table values are linearly interpolated between published points; inputs are restricted to the safely digitized bounds.

Version 0.39 adds non-destructive pressure, temperature, velocity, and airflow calculation points that sit on an existing duct without splitting it; signed static-pressure propagation through connected components; a static-pressure column for ducts and fittings; calculation-point details in the PDF report; and a more prominent project/company header with a refined technical interface and Designer toolbox.

Version 0.40 adds mass-conserving airflow allocation at diverting and converging fittings, saved per-connection airflow definitions and orange imbalance overrides, calculate-before-update duct and fitting editors, target-static-pressure sizing as the default duct workflow, a horizontal Designer toolbox, and a collapsible/resizable system quick view.

Version 0.41 expands the project header so the DuctSizer brand is not clipped and trims imported-logo margins before filling the logo canvas. The Duct Calculator now presents a complete color-coded equation worksheet with Colebrook bisection bounds, residual, convergence status, and iteration count. The Fitting Loss worksheet now displays the selected ASHRAE source table image together with table inputs, published limits, interpolation/selection logic, and the coefficient and pressure-loss equations.

Version 0.42 repairs wide system-schedule PDF exports, renames Fitting Loss to Fitting Calculator, moves both calculators' summary outputs higher in the left column, and presents their detailed equations in Imperial HVAC units. The fitting worksheet now includes calculated Reynolds number, hydraulic diameter, rectangular ratios, and available junction ratios. System rows and report pages show static pressure in and out, while fan inputs accept a positive system static pressure that is propagated as a pressure rise but excluded from total pressure-loss reporting.

Version 0.43 restores a full content-sized input region and places calculator outputs near the lower portion of the left cards; replaces hand-rendered Designer symbols and the application icon with a consistent locally embedded Pictogrammers Material Design icon set; synchronizes selection between the Designer canvas and quick-view schedule; and assigns type-specific Designer IDs while migrating legacy `L-###` connection references safely.

Version 0.45 adds a first-save destination/file-name dialog followed by in-place overwriting on later saves, complete paginated equation and ASHRAE reference-table pages in the PDF report, wrapped schedule headings, and synchronized PDF schematic symbols. The Designer now uses a circular Q airflow point, a larger bold **Transition** symbol, a discharge-hood terminal, and a clearer filter symbol. The **Settings**, **Solver Settings**, and **Save** controls share one aligned action row.

Version 0.46 synchronizes fitting airflow before connected static-pressure propagation so fan-rise and critical-route loss close to a zero-gauge sink; adds optional coordinate-true isometric scaling with editable feet per grid cell; replaces zoom buttons with cursor-centered mouse-wheel zoom; adds hide/restore/expand controls to both calculation worksheets; simplifies calculator outputs to label/value rows; enforces material-compatible duct shapes with explicit size-unit suffixes; and replaces the elbow with a smooth closed two-line CC0 HVAC symbol.

Version 0.47 opens in a near-square 1200 × 1200 window, places **Legacy Reference** at the bottom of the Duct Material list while retaining it as the default selection, renames the fitting group to **Terminals & Takeoffs** with compatibility for older saved projects, and relabels the Solver Settings field to **Duct Area Calculation Method**.

Version 0.48 replaces the rectangular autosizing aspect-ratio field with a **Width/Height** dropdown and an explicit dimensional input in inches (or millimeters in Metric mode). The selected dimension remains fixed while the calculator solves and rounds the other rectangular dimension.

Version 0.49 derives scaled component-placement X/Y deltas directly from the selected isometric grid point with Z fixed at zero; replaces the scale stepper with a validated text field; fully exposes **Show Calculations**, **Expand**, and **Restore** labels; removes the redundant manual longest-route action; adds guarded one-click fan airflow/static-pressure sizing; and enforces one fan per system.

Version 0.50 makes resolved absolute coordinates the graphical source of truth after connection, uses one axis-consistent isometric transform, and restricts a component inserted on a duct to that duct's running axis while preserving the other coordinates and splitting the connection. A fixed lower-right XYZ legend remains visible while panning. The Duct System actions now place **Show Critical Path** before **Auto-size Fan** and add **View Fan Information** for airflow and static pressure. Both calculator restore controls fully display **Show Calculations**, and the terminal symbol is Google's Apache-2.0 range-hood icon instead of a speaker glyph.

Version 0.51 preserves the complete downstream network when a component is connected upstream and automatically decomposes every confirmed multi-axis connection into grid-aligned X, Y, and Z duct runs. An undefined red elbow is inserted at each direction change (one elbow for two axes and two elbows for three axes) so each bend can be reviewed and defined explicitly. Relative coordinate entries now accept signed values only at the active Designer grid increment, matching graphical snap placement.

Version 0.52 makes the Layout Designer permanently coordinate-scaled with a clearly labeled **Grid Scale** input and centers the XYZ axes inside a true square legend. **Settings → Automatic Sizing Inputs** now stores project defaults for ducts, coils, filters, elbows, transitions, splits, merges, dampers, terminals, and GRDs. The Designer's **Size Undefined Components** action applies those defaults to supported red components and unsized ducts while deriving airflow and temperature from connected defined items and duct length from the drawn coordinates.

Version 0.53 adds fan-efficiency input and fan brake-horsepower reporting, gives fan components dedicated `Fan-` IDs, and adds an icon-backed fan-information summary. The Designer can switch between editable isometric and top-down views without changing engineering coordinates. Connected component edits now support downstream Z translation and valid straight-run X/Y repositioning while recalculating linked duct geometry. Automatic duct defaults leave solved dimensions blank for airflow-and-target sizing, and the Duct System schedule now aligns every value from Airflow through Temperature with its correct heading.

Version 0.54 corrects the Fan Information layout with a square, undistorted fan image and a fully visible horsepower line. The Designer view control became a dropdown for **Isometric Positive**, **Isometric Negative**, and **Top-Down** projections. Component and duct deletion preserve every surviving component's absolute X/Y/Z position; newly disconnected sub-systems retain a persistent absolute coordinate anchor instead of falling back to stale screen coordinates.

Version 0.55 corrects **Isometric Negative** to rotate the positive isometric grid 90 degrees counterclockwise, placing +Y in the former +X direction and +X in the former -Y direction. Projection, snapping, routing, and the axis legend now use the same mapping, and the Layout Designer's Clear button has been removed.

Version 0.56 makes component and duct deletion position-invariant by rebasing all surviving nodes to their pre-delete absolute X/Y/Z coordinates. Deleting a component now disconnects its adjacent runs instead of silently bypassing it. Isometric Negative now applies its 90-degree counterclockwise camera rotation to connected, anchored, and newly placed nodes alike. Proximity auto-connect and drop-on-duct insertion are removed for normal components; only an explicit connector-to-connector drag creates topology. Every multi-axis connection is split into axial duct runs with visible elbow fittings.

Version 0.57 translates the complete connected network—upstream, downstream, and its duct geometry—when any connected component elevation changes, while leaving disconnected systems untouched. The Duct System schedule now leads with ID, description, airflow, inlet/outlet static pressure, air type, temperature, pressure rate, velocity, length, and duct size. A Designer **Highlight Critical Path** toggle marks every solved critical-route component and duct in amber.

Version 0.58 adds draggable center dividers to the Duct and Fitting calculators so input and calculation panes can be resized independently while retaining the responsive stacked view. Duct System columns can now be reordered by dragging their headers. The project ribbon uses a dimensional blue gradient, subtle duct-line geometry, airflow arcs, and a teal accent for a more polished HVAC presentation.

Version 0.59 rebuilds the exported project report as a professional landscape Letter drawing set. The first sheet summarizes project and fan sizing information; four following sheets embed fitted captures from the actual Layout Designer renderer for Isometric Positive, Isometric Negative, Top-Down, and critical-path-highlighted views. Schedule headings and values wrap without clipping, each system item receives one consolidated inputs/outputs/equations sheet, and every page shares a bordered DuctSizer/company-logo title block with designer, project, print-date, and page-number fields.

Version 0.59.1 repairs the green **Save** workflow with an explicit saving state, protected temporary PDF/CSV generation, overwrite-safe finalization, visible success confirmation, and a detailed error dialog when a destination is unavailable. Automated UI verification now invokes the same Save handler and confirms that both export files and the four Designer-view images are written.

Version 0.59.2 constrains every component-detail field and calculation to the printable title-block area, gives highlighted calculation values a separately wrapped value column, and renders critical-path direction as readable `to` text instead of an unsupported PDF glyph.

Version 0.60 replaces the Designer component family with the supplied blue engineering-toolbox artwork. Fan, duct, tee/wye, elbow, transition, coil, filter, damper, terminal, source/sink, Q airflow, P pressure, and general-component symbols now use the provided transparent PNGs in their original colors. Temperature, velocity, and GRD symbols use matching black outlines and blue fills, while undefined and override states retain visible status borders. The same icons flow through the toolbox, canvas, fan summary, and exported Designer views.

Version 0.60.1 updates the supplied artwork mappings so Coil uses **Round-Duct-Coil**, Damper uses **Volume Balance**, and Terminal uses **Dead End** throughout the toolbox, layout canvas, and exported Designer views.

Version 0.61 replaces flattened equation strings in both calculator worksheets with Cambria Math typography, true baseline-shifted subscripts and superscripts, fraction styling, radical notation, and properly rendered Greek variables. Project reports use the same equation markup through a portable vector PDF renderer with stacked fractions, scripts, radicals, and viewer-independent Greek symbols. Calculated results remain separated and highlighted in red, and the on-screen version badge now follows the packaged assembly version automatically.

Version 0.61.1 refines the engineering worksheet and report typography. Duct and fitting calculator fractions now use full-size, vertically stacked numerators and denominators instead of diagonal fraction slashes. Report schedule pagination, field spacing, calculation-line spacing, and equation/result clearances were increased so wrapped text, dense calculation pages, and title-block content remain within their designated bounds with consistent type sizing.

Version 0.65.0 simplifies the centrifugal blower emblem into a flat two-color icon with no corner hardware, background tile, gloss, nested rings, or decorative frame borders. The smooth volute, four broad curved vanes, straight inlet, and tangential outlet remain legible at header and Windows taskbar sizes. The main and page ribbon lines are now anchored at the far-right edge and extend left, while still shortening and fading evenly from top to bottom.

Version 0.66.0 unifies the Layout Designer Space Source, Space Sink, and Fan artwork with the blue component palette; replaces the Undo and Redo labels with standard curved-arrow icons; and adds Ctrl+Z/Ctrl+Y Designer shortcuts. It also removes the cyan navigation underline and page-header rail, extends the restrained horizontal ribbon detail across the header, replaces double highlights and corner shadows with single clean borders, improves dropdown contrast, compacts the Duct System schedule, and introduces a simpler unframed flat centrifugal-fan application icon.

Version 0.67.0 replaces the software's main application artwork with the user-supplied blue centrifugal blower illustration. The original transparent artwork is preserved as the icon source, centered without distortion in the header PNG, and converted into a multi-resolution Windows icon for the executable, title bar, taskbar, and project header.

Version 0.68.0 updates the software's main application artwork to the newer user-supplied square blue centrifugal-fan illustration with visible inlet, outlet, impeller, and airflow-direction arrows. The transparent source remains undistorted and is regenerated for the project header and every embedded Windows icon size.

Version 0.69.0 enlarges the project-header centrifugal-fan artwork to nearly twice its previous displayed size while preserving the existing title-bar, taskbar, and executable icon sizing.

Version 0.70.0 renames the product to FanWorks throughout the application and exported reports, adds breathing room beneath the main navigation buttons, removes the contrasting equation-image backgrounds, and reorganizes the Duct Calculator worksheet into required equations, variable definitions, red input values, and blue solved parameters.

Version 0.71.0 presents duct and fitting calculations in dependency order with governing equations first, two-column parameter definitions, red substituted inputs, and blue solved results inside the typeset equations. Duct System row editing and removal now use a right-click context menu, and **Calculation Method Inputs** is available from the **Settings** menu instead of a separate ribbon button.

Version 0.72.0 removes the fan-positive-static note from the Duct System summary, normalizes rounded static-pressure zero values so they never display as negative zero, and makes equation artwork use the exact calculation-pane background. The Duct Calculator solution is now narrated in five dependency-ordered stages covering final geometry, Sutherland air properties, flow-regime selection, Colebrook convergence, and final duct pressure loss.

Version 0.73.0 rebuilds each exported component sheet as a QA/QC calculation worksheet. Governing equations, red input/substitution values, blue solved results, flow-regime logic, Colebrook convergence evidence, and final pressure-loss calculations now follow the same dependency order as the calculators while remaining inside the landscape title-block bounds. The application ribbon aligns the HVAC Airside Design tagline with FanWorks and groups right-aligned project metadata beside the imported company logo.

Version 0.74.0 reorganizes the fitting gallery: ASHRAE 6-7, 6-8, and 6-9 are grouped under Dampers; 6-10 and 6-11 are grouped under Transitions; and the former Others group is labeled Fan Exits and Plenums. Older saved projects using the previous group label remain compatible.

Version 0.75.0 adds manufacturer-table GRD performance selection. The Price SCD square cone diffuser accepts airflow, one of three face sizes, and a compatible neck diameter. Published selections round requested airflow upward to the next tabulated row and report total pressure drop, NC, and 150/100/50-fpm throw. Recreated performance tables appear in the calculation pane and exported QA/QC report, and the GRD gallery artwork uses clean ASHRAE-style technical linework.

Version 0.76.0 adds explicit component insertion when a Designer item is dropped directly on a duct. The user can insert and split the run, place the item unconnected, or cancel. Both new axial duct segments inherit the original airflow, temperature, geometry, material, humidity, atmospheric pressure, and calculation methods; their graphical lengths are recalculated, their existing calculation points remain associated with the correct segment, and both segments are flagged red until re-sized manually or through **Size Undefined Components**. The Designer toolbox is now a compact, borderless icon row while retaining each component's name and explanation as hover help.

Version 0.77.0 replaces the limited Titus linear-diffuser shortcut with the complete 2019 TBD-80 D performance catalog: 132 published configurations and 924 performance points. The calculator and Designer now accept airflow, 2/4/5 ft length, 3/4/1/1-1/2 in slot size, and one through four slots. FanWorks selects the smallest published 6/8/10/12 in inlet whose table covers the request, rounds airflow upward to the next listed value, and reports total/static pressure, NC, and 150/100/50-fpm throw. The selected Titus chart/lookup table and a **Performance data courtesy of Titus** reference appear in the calculation pane and exported QA/QC report.

Version 0.79.0 centers the collapsed **SYSTEM QUICKVIEW** label, replaces viewer-dependent report math glyphs with polished vector Greek characters and improved square-root geometry, and limits GRD selection to the Titus 350R grille, Price square diffuser, and Titus TBD-80 D linear diffuser. The Layout Designer now separates Fan/Plenum from a new Custom Component tool: Fan/Plenum uses a dedicated blue plenum symbol, while Custom Component preserves the former general-component symbol and accepts a user description plus static pressure drop for system calculations and QA/QC reporting.

Version 0.80.0 reformats the turbulent Colebrook relation as a clean two-line equation with an explicitly defined argument so nested fractions, radicals, and delimiters do not collide in the application or QA/QC report. Fan/Plenum is now a plain blue square without internal fins, using the same blue palette and lighter stroke weight as the other Designer components; GRD outlines and internal details use the same lighter component stroke system in the live Designer and exported schematic views.

Version 0.81.0 gives converging fittings independent primary- and secondary-inlet geometry so connecting or editing the second inlet cannot move the first inlet, the fitting, or any previously connected components. Secondary-run duct length, sizing, editing, splitting, persistence, and absolute-coordinate resolution now use the correct inlet vector. Equal-offset automatic turns are configured as 45-degree elbows and carry that angle into fitting sizing instead of defaulting every generated bend to 90 degrees.

Version 0.81.1 stabilizes converging-fitting outlet connections. New connector drags derive relative X/Y/Z from the endpoints' exact pre-connection coordinates instead of rounded screen positions, while visually collinear isometric placements resolve to one X, Y, or Z run. The connected target and its existing downstream system therefore stay in place. Ordinary fitting edits and Designer refreshes no longer mutate topology or insert elbows; automatic bend insertion is limited to an actual new connection or explicit rewire.

Version 0.82.0 adds a dedicated QA/QC quick-check overview to every component report sheet without removing or reordering the existing engineering detail. Duct sheets summarize duct size, airflow, length, and pressure drop; fitting sheets summarize fitting type/name, C-value, and pressure drop; equipment, GRD, space, fan, calculation-point, and undefined-component sheets show their corresponding principal review values.

Version 0.82.1 relocates each component's QA/QC quick-check overview below the detailed Inputs and Calculated Outputs sections and condenses it into a shallow, low-profile review strip so it does not consume unnecessary report space.

Version 0.83.0 keeps the overall project and fan-sizing summary as report page 1, then places all component worksheets in connected source-to-sink order with a dedicated fan worksheet at the fan's actual system position. Graphical system views and schedule sheets follow the component sequence. At converging fittings, both upstream branches are traversed before the convergence and downstream worksheets are emitted. On worksheets with inline engineering calculations, the Engineering Calculation Review, Input/Substitution, and Solved Result header row now appears above the detailed inputs, outputs, and calculation body.

Version 0.84.0 makes Space Source and Space Sink design airflow editable after placement, propagates changed sink demand upstream, flags every connected duct for re-sizing, and adds **Recalculate All Ductwork** to rebuild existing duct sizes from the current boundary flows. Connected equipment and fittings now expose editable absolute X/Y/Z coordinates. The isometric grid independently renders both line families across the full pan and zoom range. Layout Designer moves its compact drag-and-drop toolbox into a collapsible right-side drawer, retains the collapsible system quick view on the left, and reduces header chrome so the drawing canvas receives more of the window.

Version 0.85.0 adds balanced boundary-flow editing. With one source, changing a sink automatically sets the source to the sum of all sinks; ambiguous multi-source or multi-sink edits open a dedicated **Source and Sink Editor** where every boundary CFM can be reviewed and changed together. The editor prevents saving until source and sink totals agree, and accepted changes flag connected ducts red for batch re-sizing. The right-side toolbox is now a single vertically scrolling component column. Layout Designer also adds independent **Show/Hide Labels** and **Show/Hide Duct Sizes** controls; displayed duct sizes use diameter for round ducts and width/height for rectangular ducts.

Version 0.86.0 adds scaled construction-sheet PDF underlays to Layout Designer. The PDF importer supports multi-page page selection, a custom paper-inch-to-drawing-foot scale, and an absolute X/Y/Z sheet center defaulting to the system origin. Imported sheets render beneath the grid and components in Top-Down view and can be shown, hidden, moved, rescaled, replaced, or deleted. Their drawing scale remains proportional when the grid scale changes, and the rendered page plus its placement metadata persist in the local project without requiring the original PDF file.

Version 0.86.1 repairs the imported-PDF command menu so Hide/Show, Move/Change Scale, Replace, Switch to Top-Down, and Delete finish their selected action before the popup is disposed. PDF command failures now report within the Layout Designer instead of surfacing as an unrelated application error. Mouse-wheel zoom-out is extended from 35% to 10%, and the drawing grid can shrink with that range instead of visually stopping at its former minimum spacing.

Version 0.87.0 adds Titus DESV VAV components and manufacturer performance calculations. The Designer VAV symbol matches the Custom component's blue duct graphic with a black center square. The Fitting Calculator adds **VAV with Reheat** size/coil-row/airflow selection from the supplied hot-water coil Airside ΔPs tables and **VAV without Reheat** size/airflow selection from the supplied full-open-damper Min ΔPs table. Both use in-range linear interpolation, retain the selected published bracket, contribute their calculated pressure drop to the duct system, and reproduce the relevant table, interpolation basis, and Titus reference in the QA/QC report.

Version 0.88.0 adds a dedicated **Source/Sink Editor** button to Layout Designer. Its compact form lists every source and sink with editable CFM, read-only calculated pressure in inches water column, a prominent live balance status, and save protection until total source and sink airflow agree. Accepted edits update boundary components, flag connected ductwork for re-sizing, refresh the schedule and drawing, persist locally, and participate in Designer Undo. The **Size Undefined Components** action is widened and re-typeset so its complete label remains visible.

Version 0.89.0 restores the report's four graphical system sheets immediately after the project summary: Isometric Positive, Isometric Negative, Top-Down plan, and the highlighted critical path. The plan sheet now includes the imported construction-PDF underlay beneath the grid and components, even when that underlay was hidden in the live Designer. Every graphical sheet includes a fitted architectural-style scale bar, its grid spacing in feet, and the exact fit-to-sheet inches-per-foot scale.

Version 0.90.0 labels every blue solved result in the exported QA/QC calculation worksheets. Equation outputs now identify the engineering quantity before its value - including duct/reference area, equivalent or hydraulic diameter, air density, dynamic viscosity, velocity, relative roughness, Reynolds number, Darcy friction factor, loss coefficient, velocity pressure, pressure rate, and component pressure drop. The report height calculation uses the same labeled text so wrapped result labels remain separated and readable.

Version 0.92.0 renders the actual engineering variable in every blue solved result. QA/QC worksheets now use true mathematical notation - for example **Air density (rho symbol)**, **Dynamic viscosity (mu symbol)**, **Relative roughness (epsilon sub r)**, and **Pressure drop (Delta P)** - in the form `quantity (variable) = value and units`, rather than spelling Greek variables as words.

Version 0.91.0 adds the solved engineering variable to every labeled blue equation result. QA/QC worksheets now pair the descriptive quantity with its calculation symbol - for example **Air density (rho)**, **Reynolds number (Re)**, **Darcy friction factor (f)**, **Velocity pressure (VP)**, and **Pressure drop (Delta P)** - before displaying the calculated value and units.

Version 0.78.0 adds the complete Titus 350R grille performance catalog with 78 nominal duct-size charts and 702 published airflow points. Grille calculations select a nominal size from the manufacturer chart, round requested airflow upward to the next published value, and report negative static-pressure magnitude as system pressure loss together with core velocity, velocity pressure, and NC. The selected chart and Titus reference appear in both the Fitting Calculator and exported QA/QC report. The Layout Designer adds a vertically lettered **SYSTEM QUICKVIEW** reopen tab, Escape cancellation for pending component placement, a tighter toolbox row, explicit highlight/unhighlight critical-path labels, and the Duct System moves **Delete all** to the far-right destructive-action area while using **Isolate Critical Path** / **Show All Items** filter labels.

Version 0.44 adds signed relative-coordinate confirmation for newly connected components, graph-resolved absolute X/Y/Z schedule coordinates, first-component origin handling, and a selected-component **Set Coordinate Origin** workflow. Project identity and calculation defaults are separated into **Settings** and **Solver Settings**. The critical-path pressure audit now verifies loss-column summation and fan-to-sink pressure balance, while coil, filter, transition, terminal, elbow, and airflow symbols use locally embedded openly licensed HVAC-oriented artwork.

For target-pressure-rate and target-velocity sizing, the project-wide increment and rounding method are selected under **Settings > Calculation Method Inputs** and no longer repeated on each duct. Equivalent-diameter calculation and the **Duct Area Calculation Method** are configured there as well. A positive increment enables nominal-size rounding. **Automatic** uses standard mathematical rounding to the nearest increment: fractions below `0.5` round down and `0.5` or above round up. **Round Down** always selects the lower increment. Either method can select a smaller duct than the exact result, so the program warns whenever the rounded size can exceed the requested pressure-rate or velocity target.

## What changed in 0.100.0

- **Pressure labels**: a **Pressure** checkbox beside Labels / Flow / Sizes on the Project tab shows each duct's
  static pressure at its inlet (for example "SP 0.35 in. w.c.") from the last calculation — the pressure, not the
  drop across the duct.
- **Movable labels and leaders**: drag any label away from its item. Right-click a label for **Add leader** /
  **Remove leader** and **Reset label position**; drag the leader's end handle to point it at the duct or
  fitting. Label positions are saved with the project (file format 17) and Ctrl+Z undoes a move.
- **Free rotation**: hold **Ctrl** and drag with the middle mouse button to rotate the model in any direction. A
  **view cube** beside the axis legend rotates to any of its 8 corners when clicked. The view list shows
  "Free view"; choosing Top-Down or an isometric view snaps back. Components are placed and moved in the standard
  views.
- **PDF underlay in every view**: the sheet is drawn in top-down, isometric and free views at its **Center Z**
  elevation (0 to start). The Move / Change Scale window has a Center Z (ft) field.
- **Analysis**: the pressure-drop card lists every critical-path item, highest loss first and shaded from the
  highest down, with its own scroll bar, the same height as the System Overview; the Critical column is gone.
- **Customize Duct Class** (Analysis ribbon): the calculated pressure class and recommended seal class, each with
  an Override box and a choice of the 7 SMACNA pressure classes or seal class A / B / C. Saving recalculates the
  **Leakage factor F** and **Duct leakage** (renamed from "allowable ...").
- **Auto Duct Size Rounding** (renamed from Round Sizes): 1 in., 2 in. or Custom (enter any increment).
- **Redesigned pop-up windows**: rounded input boxes with a blue focus ring, white rounded cards, blue primary
  buttons, a navy title bar with the FanWorks icon, and new layouts for Sizing Criteria, the fitting gallery,
  the underlay Move / Change Scale window and the Source and Sink Editor (slim rounded scroll bar, tighter rows).

## What changed in 0.99.0

- **Duct pressure class and leakage (SMACNA)** in the Analysis tab's System overview and on the report title page:
  - **Pressure class**: the smallest SMACNA static pressure class (½, 1, 2, 3, 4, 6 or 10 in. w.c.) at or above the
    design pressure, which is the larger of the critical-path pressure loss and the fan static pressure (rounded up,
    never down). Shown with its criteria: per SMACNA HVAC Duct Construction Standards.
  - **Seal class** from the pressure class: A for 4 in. w.c. and up, B for 3 in. w.c., C for 2 in. w.c. and below.
  - **Leakage class** CL from the seal class and duct type (SMACNA HVAC Air Duct Leakage Test Manual): Seal Class A
    3 round / 6 rectangular, B 6 / 12, C 12 / 24.
  - **Allowable leakage** F = CL × P^0.65 (cfm per 100 ft², P = pressure class in in. w.c.), and the system's
    allowable duct leakage = F × (surface area of all ducts and fittings) / 100, with the working shown.
  - Coils, filters, GRDs, VAV boxes and other components are taken as 0 cfm/ft² (no surface area).
- **Surface area** of every duct segment (πDL round, 2(W + H)L rectangular) and fitting (estimated: perimeter ×
  an equivalent length — the centerline arc for elbows with a radius ratio, otherwise one reference dimension).
  New *Surface area* column in the Analysis table and the System Overview (fittings marked ≈).
- **Worksheets** (Selection Inspector Calculations tab and report item sheets) are titled *Duct / Fitting /
  Component sizing calculation*; after the assumptions, a divider and the **Surface area calculation** (equations
  and working, plus the item's allowable leakage).
- **Analysis tab**: the whole page scrolls; the full table shows every row (no scroll bar of its own; the top-10 list
  keeps its own). The System summary donut is shown beside the path graph.
- Report title page: report contents condensed to make room for the SMACNA block.

## What changed in 0.98.0 and 0.98.1

- **Pop-up windows**: every editor and settings window lays out its fields to suit their content — numbers in
  short boxes, names and descriptions in wider ones, drop-downs as wide as their longest choice — level with
  their labels. Lists have roomier, clearly selected rows. Offset positions are labelled ΔX / ΔY / ΔZ.
- **Selection Inspector**: each X / Y / Z (or ΔX / ΔY / ΔZ) label sits directly beside its box, and the caption
  reads, for example, "Offset from Fan-001 (ft)".
- **Drawing shortcuts**: Esc deselects the selected (or just-dropped) item; Delete removes it at once (Ctrl+Z
  restores it); right-clicking a component or duct offers **Edit** (opens it in the Selection Inspector) and
  **Delete**. Deleting a component or duct no longer asks for confirmation anywhere (the status line names what
  was deleted; Ctrl+Z undoes it). Clear Layout still asks.
- **Analysis Settings** (Analysis ribbon): optional low and high values you expect for duct pressure drop rate
  (in. w.c./100 ft), duct velocity, fitting velocity and fitting pressure loss. FanWorks supplies no defaults; an
  empty value is not checked. Once saved (with the project, file format 16), the "Highest pressure drops" list
  shows **Within expected range** — Yes, No, or Not set — for each duct and fitting, with the comparison in a
  tooltip. A duct in transitional flow is within range only if its whole friction band is.
- **Pressure drop along each path** (Analysis tab): a path runs from the start of the system to one terminal
  outlet. The line graph has one line per path — component number along the path across, running pressure drop
  (in. w.c.) up — with the critical path in amber, so the alternatives compared against it are visible. The path
  closest to the critical one is named with how far below it is (useful when rerouting ducts to balance paths);
  hovering a point gives that path's total and its difference from the critical path. A path with an item that
  cannot be calculated is left out and named in a note. (A *branch* remains anything tapped off the main.)
- **Report title page**: condensed; shows the number of paths and the same graph, and the System Summary
  is a donut of duct segments, fittings and components, each labelled with its quantity.

## What changed in 0.97.0 (polish)

- **Display scaling**: FanWorks now scales to the Windows display setting (125 %, 150 %, 175 % ...). Before, text
  grew with the setting but buttons, columns and panels did not, so captions were cut off on high-resolution
  laptops. Every window is scaled once when it opens; the main window opens maximized.
- **Ribbon**: every caption is measured so it is never cut off, and on the Project tab the groups spread across
  the whole ribbon. Larger logo; the FanWorks name is never clipped. The Quick Calc duct calculator shows a duct.
- **Resizable panels**: drag the bars beside the Project Explorer and the Selection Inspector (double-click a bar
  to reset it) and the grip above the bottom panel.
- **System Overview**: 16 columns across the full panel (description, branch, size, airflow, velocity, SP rate,
  SP drop, SP in/out, length, upstream/downstream, X/Y/Z), with units from the System table.
- **Edit in the Selection Inspector**: fixed values and results are plain text; inputs you can change appear as
  their own text boxes and drop-downs. **Update Component / Duct / Fitting** checks and applies the change exactly
  as the editor window does (same checks, undo, save), **Cancel** discards it, and a refused value is explained
  under the inputs with the item left unchanged. Ducts also offer Reverse Flow Direction.
- **Drawing toolbar**: labelled **Insert PDF Underlay / PDF Underlay ▾** and **Source / Sink Editor** buttons.
  The "set coordinate origin" command is removed (projects that already have an origin keep it).
- **Report**: one sheet per component, duct and fitting (inputs, outputs and quick check on the left; the full
  worksheet with required equations, substitutions, solved results and assumptions on the right; fitting figure
  under the inputs). A sheet that would overflow continues on a "(continued)" sheet rather than dropping lines.
  New title sheet with project block, fan-selection figures, critical path, report contents and system summary.
  View sheets carry a labelled strip with graphic scale, grid spacing, sheet scale and view.
- **Fixed**: the component editor showed the pressure drop as read-only for coils, filters, custom components,
  fan static pressure and source pressure (a GRD-only setting applied to every type).

## What changed in 0.96.0 (interface redesign)

Calculations are unchanged; this release reorganises the window.

- **Main tabs:** Project, Analysis, Reports and Quick Calc replace the Duct Calculator / Fitting Calculator /
  Duct System / Layout Designer buttons. FanWorks opens on the Project tab (the Layout Designer).
- **Project ribbon:** New Project, Open, Save, Save As; Project Settings; Sizing Criteria; Round Sizes (½, 1 or
  2 in.); Select and Move; every drawing component, grouped as Spaces, Equipment, Fittings, Terminals and Points
  (drag them onto the grid, or click then click the grid); Auto Size; Calculate; Highlight Critical Path.
- **Auto Size** does what Size Undefined Components and Recalculate All Ductwork did, in one step (one undo).
- **Calculate** sizes the fan to the critical path (as Auto-size Fan did) and highlights the critical path.
- **Project Settings** is one form with Project Information, Units and Calculation Settings (autosize increment and
  rounding, equivalent-diameter method, duct area method). The Settings menu is gone.
- **Drawing toolbar:** undo/redo/delete, zoom out / in buttons (mouse-wheel zoom still works), centre view, PDF
  underlay, grid scale as a dropdown of feet-per-cell values, view direction, and Labels / Flow / Sizes check boxes
  (Flow shows each duct's airflow).
- The window frame is a fixed height on any screen, so larger monitors give the drawing more room.
- **Selection Inspector** (right of the drawing): Description (what the item is, its inputs and outputs; fitting
  pictures enlarge on click), Calculations (every equation and value — the same content as the item's report page)
  and Notes (saved with the project, file format 15). Edit Inputs opens the item's editor.
- **Project Explorer** (left): all items of the system grouped as Equipment, Sources, Sinks, Terminals, Dampers,
  Fittings, Duct Segments and Calculation Points, with search. Clicking an item selects it on the drawing.
- **Bottom panel**: System Overview (the former quick view), Errors (every current problem, with a count) and
  Validation (ducts: size, airflow, velocity, static-pressure rate, loss; fittings: C and loss) for QA/QC.
- **Analysis tab**: airflow, critical-path pressure drop, fan static pressure and the items in the critical route;
  the ten highest pressure drops; then the full system table with the Isolate Critical Path filter.
- **Reports tab**: a page-by-page preview of the project report; **Print Report** saves the PDF and opens it.
- **Quick Calc tab**: the duct and fitting calculators, usable without a project.
- Airflow values in the fan-sizing message, fan information and the report's design airflow now show their unit.

## What changed in 0.95.0

- **Unsized connections block results.** A drawn connection with no sized duct is no longer counted as zero loss: the critical-path total shows "not available" and names it, Auto-size Fan refuses until it is sized, and pressures downstream show "—".
- **Auto-size Fan isolates the critical path.** After sizing, the Duct System table shows only the items whose losses set the fan static pressure (the same view as "Isolate Critical Path"); "Show All Items" restores the full list.
- **Positions are stored as absolute coordinates.** Project files (format 14) store each component's absolute X/Y/Z; when a file is opened, the connection offsets are rebuilt from those positions. Older projects convert automatically without moving anything. Converging loops can no longer have offsets that disagree.
- **Positions are entered relative to a neighbour.** The component and fitting position fields show and accept the offset from the upstream component, or from the downstream component when there is no upstream one (labelled DOWNSTREAM). Only an unconnected component uses absolute X/Y/Z.
- **Each component type saves only its own data** (fan, terminal, space, calculation point, fitting blocks).
- **The window fits smaller screens.** FanWorks opens at 1200 x 1200, or at the size of the desktop (minus the taskbar) when that is smaller, centred.
- **For developers:** each tab is its own class (`DuctCalculatorScreen`, `FittingCalculatorScreen`, `DuctSystemScreen`, `LayoutDesignerScreen`); `MainForm` keeps the window frame, units and shared control builders.

## What changed in 0.94.0

Version 0.94.0 restructures the code for maintainability; calculated results are unchanged from 0.93.0.

- **The system solver is part of the engineering library.** Airflow balancing and pressure propagation moved out of the window into `DuctSizer.Core` (`SystemSolver`) with their own checks. Results were confirmed bit-identical before and after on a real project.
- **Layout review.** The Duct System tab now flags layout items to review (orange text with a list on hover; also in the PDF): connections whose X/Y/Z offsets do not close around a loop, ducts whose offset or length disagrees with the drawing (a duct shorter than the straight run understates its loss), components with no defined position, and invalid component data such as missing linked items or a fan efficiency above 100%.
- **Undo** keeps the last 100 Designer changes.
- **Reports** state the pressure unit used (1 in. w.c. = 249.08891 Pa).
- **For developers:** `ARCHITECTURE.md`, `DEVELOPER_GUIDE.md`, `DEVELOPMENT_PROGRESS.md`, a register of every digitized table for independent checking (`docs/TABLE_VERIFICATION.md`), equation and source documentation in the engineering library, an exports-independent-of-regional-settings check, warnings treated as errors, an SDK pin and a CI workflow.

## What changed in 0.93.0

Version 0.93.0 is a correctness and stability release that follows the v0.92.0 code review.

- **Junction fittings use the real connected duct sizes.** Diverging and converging tees and wyes take their common, straight, and branch areas from the ducts actually connected to each leg. When a leg has no sized duct yet, the fitting's own reference geometry is used and the leg is listed as assumed.
- **Each junction outlet is charged its own loss.** The critical path charges a route through the main outlet the ASHRAE straight-path coefficient and a route through the branch outlet the branch-path coefficient (and likewise for converging inlets). Earlier versions charged every route the single coefficient selected on the fitting, which could understate a branch route.
- **Failures are never silent zeros.** A duct or fitting that cannot be calculated makes the critical-path total "not available" and names the item. Auto-size Fan refuses, and leaves the fan unchanged, until the route is complete. Fan horsepower rejects invalid airflow, pressure, or efficiency.
- **Unsized connections are shown as an assumption.** (Superseded in 0.95.0: unsized connections now block the total.)
- **Out-of-range table lookups are flagged.** When an ASHRAE table input falls outside the published range, the edge value is still used but the fitting result, the Fitting Calculator status, and the PDF report carry a "review required" note.
- **Transitional flow** uses the conservative upper-bound friction rate everywhere, matching the documented method.
- **Fan airflow** comes from the fan's own connected system rather than the largest airflow anywhere in the project.
- **Real project files.** Save writes `<name>.fanworks` (the full model: layout, connections, equipment, settings, PDF underlay) before the PDF and CSV, so a report failure cannot cost the model. The previous project is kept as `<name>.fanworks.bak`. **Settings → Open Project** reopens it. Projects from a newer FanWorks are refused with an update message.
- **Diagnostics.** A daily log and crash reports are written to `%LOCALAPPDATA%\FanWorks\logs`. An unreadable working file is kept aside as `duct-system.unreadable-<time>.json` instead of being overwritten.
- **No hidden switches.** The `DUCTSIZER_*` environment variables that suppressed dialogs and redirected storage have been removed; the automated UI tests use an internal test-only hook.

## Local duct-system storage

The Duct System tab keeps a working copy at `%LOCALAPPDATA%\DuctSizer\duct-system.json`, saved automatically after each change so an unexpected close does not lose work. Use the green **Save** to keep a named `.fanworks` project file. The file contains saved duct and fitting inputs, equipment, graphical layout, connection topology, IDs, and descriptions; calculated outputs are regenerated by the current calculation engine when the program opens. A `.bak` copy of the prior save is retained.

The green **Save** button asks for one base filename on the first save and creates the `.fanworks` project file, the PDF report, and the CSV schedule; later saves in the same session (or after **Open Project**) overwrite those files without another prompt. The PDF is a landscape system schedule using the currently selected display units, includes the configured company logo and project metadata, includes a graphical Designer page that mirrors the application grid and current component symbols, and appends an individually formatted report for every duct, fitting, and equipment item. Connected item pages follow system flow order: starting component, connecting duct, downstream component, and then each branch. Each item report lists stored inputs and calculated outputs, then adds paginated calculation-detail sections with equations, interpolation inputs/ranges, friction convergence data, and relevant intermediate results highlighted in red. Fittings also include their complete embedded ASHRAE reference image/table page. The CSV stores SI calculation inputs, fitting parameters, junction geometry, air-system type, and branch ID so it can reproduce calculated ducts and fittings without rounding loss. The CSV does not contain the graphical layout or connections; the `.fanworks` project file does. **Settings → Open Project** opens a `.fanworks` project, or imports a CSV schedule after warning that layout and connections will not be restored.

The displayed total pressure loss is the automatically calculated critical root-to-terminal route, including straight ducts, fittings, coils, filters, and GRDs on that route. Parallel branches are evaluated separately rather than added together. Junction fittings are charged the ASHRAE path coefficient for the outlet or inlet each route actually takes. If any item on a possible route cannot be calculated, the total is shown as not available and the item is named. Entered positive fan static pressure is shown in pressure-in/pressure-out propagation but is not counted as a system loss.

## Build

Open `DuctSizer.sln` in Visual Studio with the .NET desktop development workload, or run:

```powershell
.\BuildRelease.ps1 -Runtime win-x64
```

The script runs the engineering verification program and the interface verification program before creating a self-contained, single-file executable.

The locally embedded Designer icons combine Pictogrammers Material Design SVGs (`@mdi/svg` 7.4.47, Apache License 2.0), a public-domain ISO transition/reducer symbol, and a CC0 HVAC pipe-elbow symbol adapted from Innerscene. Source SVGs, attribution, and licenses are retained in `src/DuctSizer.WinForms/Assets/OpenSourceIcons`, and the release includes the applicable third-party notices.

## Verification

The dependency-free verification projects check unit conversions, moist-air properties, geometry, round and rectangular laminar factors, the implicit Colebrook residual, transition behavior, all three sizing modes, exact and rounded sizing, published engineering reference points, ASHRAE fitting table anchors and interpolation, geometry-driven fitting velocity and pressure loss, JSON and CSV round trips, multipage PDF generation, compatible code migration, add/edit/update behavior, invalid-input handling, responsive layout behavior, and calculation-control availability.

The published McQuay example available to this project is reproduced within normal displayed precision. A full comparison grid against the original executable is still recommended if it becomes available.
