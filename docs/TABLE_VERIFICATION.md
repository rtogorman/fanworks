# Digitized table verification register

Every fitting, terminal and VAV table that FanWorks calculates from, generated from `FittingCatalog.All`
(108 entries). This register is the checklist for the independent second check of the transcriptions
(correction plan step 10). Nobody has completed that check yet: all entries are **Not verified**.

## How to verify an entry

1. Open the source named in the "Source in code" column (ASHRAE Duct Fitting Database / Handbook fitting of the
   same code, or the named Titus / Price catalog page).
2. Compare every digitized number (table axes and values) with the source. The arrays are in
   `src/DuctSizer.Core/FittingCatalog.cs` (search for the code), `GrdPerformanceCatalog.cs`,
   `VavPerformanceCatalog.cs` and `src/DuctSizer.Core/Data/*.csv`.
3. Spot-check the calculator: enter a value from the middle of the table and one on each edge; the coefficient
   must match the source (edge inputs outside the table now show "Outside published table").
4. Record the source edition and page/table number, your name, the date and the result below. A mismatch is a
   defect: open an issue and add a regression check in `tests/DuctSizer.Verification`.

"Manual" entries take a coefficient entered by the user (no digitized table) and need no transcription check.

## Register

| Code | Category | Description | Kind | Inputs | Source in code | Source edition / page | Verified by / date | Result |
|---|---|---|---|---|---|---|---|---|
| 1-1 | Entries | Duct mounted in wall (hood, nonenclosing, flanged and unflanged) | digitized | td, ld | ASHRAE supplemental fitting 1-1; coefficients are interpolated from its published table. |  |  | Not verified |
| 1-2 | Entries | Smooth converging bellmouth without end wall | digitized | rd | ASHRAE supplemental fitting 1-2; coefficients are interpolated from its published table. |  |  | Not verified |
| 1-3 | Entries | Smooth converging bellmouth with end wall | digitized | rd | ASHRAE supplemental fitting 1-3; coefficients are interpolated from its published table. |  |  | Not verified |
| 1-4 | Entries | Conical converging bellmouth without end wall, round and rectangular | digitized | ld, angle | ASHRAE supplemental fitting 1-4; coefficients are interpolated from its published table. |  |  | Not verified |
| 1-5 | Entries | Conical converging bellmouth with end wall, round and rectangular | digitized | ld, angle | ASHRAE supplemental fitting 1-5; coefficients are interpolated from its published table. |  |  | Not verified |
| 1-6 | Entries | Intake hood | digitized | angle, ld | ASHRAE supplemental fitting 1-6; coefficients are interpolated from its published table. |  |  | Not verified |
| 1-7 | Entries | Hood, tapered, flanged or unflanged | digitized | shape, angle | ASHRAE supplemental fitting 1-7; coefficients are interpolated from its published table. |  |  | Not verified |
| 1-8 | Entries | Orifice, sharp-edged, inlet duct | digitized | re, area | ASHRAE supplemental fitting 1-8; bilinear interpolation by Reynolds number and orifice/duct area ratio. |  |  | Not verified |
| 2-1 | Exits | Exit, abrupt, round and rectangular | digitized |  | ASHRAE supplemental fitting 2-1; coefficients are interpolated from its published table. |  |  | Not verified |
| 2-2 | Exits | Exit, abrupt, round and rectangular, with end | digitized | shape, aspectBand, angle, velocityRatio | ASHRAE supplemental fitting 2-2; shape and published rectangular aspect-ratio band are discrete selections. |  |  | Not verified |
| 2-3 | Exits | Exit, duct flush with wall, flow along wall | digitized |  | ASHRAE supplemental fitting 2-3. |  |  | Not verified |
| 2-4 | Exits | Exit, round, diverging | digitized | area, angle | ASHRAE supplemental fitting 2-4. |  |  | Not verified |
| 2-5 | Exits | Exit, round, with end wall transition | digitized | ld | ASHRAE supplemental fitting 2-5 at the published optimum angle. |  |  | Not verified |
| 2-6 | Exits | Exit, rectangular, two sides parallel, diverging, symmetrical | digitized | area, angle | ASHRAE supplemental fitting 2-6; valid for 0.5 <= H/W <= 2.0. |  |  | Not verified |
| 2-7 | Exits | Exit, rectangular, with wall, two sides parallel, symmetrical, diverging | digitized | lh | ASHRAE supplemental fitting 2-7 at the published optimum angle. |  |  | Not verified |
| 2-8 | Exits | Exit, rectangular, pyramidal, diverging | digitized | area, angle | ASHRAE supplemental fitting 2-8. |  |  | Not verified |
| 2-9 | Exits | Exit, rectangular, with wall, pyramidal, diverging | digitized | ld | ASHRAE supplemental fitting 2-9 at the published optimum angle. |  |  | Not verified |
| 2-10 | Exits | Exit, discharge to atmosphere from a 90° elbow, rectangular and round | digitized | shape, rw, lw, ld | ASHRAE supplemental fitting 2-10; includes elbow loss. |  |  | Not verified |
| 2-11 | Exits | Exhaust hood | digitized | ld | ASHRAE supplemental fitting 2-11. |  |  | Not verified |
| 2-12 | Exits | Stackhead | digitized | dd | ASHRAE supplemental fitting 2-12. |  |  | Not verified |
| 3-1 | Elbows | Elbow, smooth radius (die stamped), round | digitized | rd, angle | ASHRAE supplemental fitting 3-1; coefficients are interpolated from its published table. |  |  | Not verified |
| 3-2 | Elbows | Elbows, 5-, 4-, and 3-piece, round | digitized | pieces, rd | ASHRAE supplemental fitting 3-2; coefficients are interpolated from its published table. |  |  | Not verified |
| 3-3 | Elbows | Elbow, mitered, round | digitized | angle, re | ASHRAE supplemental fitting 3-3; coefficients are interpolated from its published table. |  |  | Not verified |
| 3-4 | Elbows | Elbows, 30° Z-shaped, round | digitized | ld | ASHRAE supplemental fitting 3-4; coefficients are interpolated from its published table. |  |  | Not verified |
| 3-5 | Elbows | Elbow, without vanes, rectangular | digitized | rw, hw, angle, re | ASHRAE supplemental fitting 3-5; Co = K-angle × K-Re × C'o. |  |  | Not verified |
| 3-6 | Elbows | Elbow, mitered, rectangular | digitized | angle, hw, re | ASHRAE supplemental fitting 3-6; Co = K-Re × C'o. |  |  | Not verified |
| 3-7 | Elbows | Elbow, smooth radius with splitter vanes, rectangular | digitized | vanes, rw, hw, angle | ASHRAE supplemental fitting 3-7; discrete vane count with table interpolation and angle correction. |  |  | Not verified |
| 3-8 | Elbows | Elbow, mitered, with single-thickness vanes, rectangular | digitized | design | ASHRAE supplemental fitting 3-8; design number is a discrete selection. |  |  | Not verified |
| 3-9 | Elbows | Elbow, mitered, with double-thickness vanes, rectangular | digitized | design, velocity | ASHRAE supplemental fitting 3-9; design number is discrete and velocity is interpolated. |  |  | Not verified |
| 3-10 | Elbows | Elbow, variable inlet/outlet areas, rectangular | digitized | hw, w1w, re | ASHRAE supplemental fitting 3-10; Co = K-Re × C'o. |  |  | Not verified |
| 3-11 | Elbows | Elbows, 90° Z-shaped, rectangular | digitized | lh, wh, re | ASHRAE supplemental fitting 3-11; Co = K-aspect × K-Re × C'o. |  |  | Not verified |
| 3-12 | Elbows | Combined 90° elbows lying in different planes, rectangular | digitized | lw, hw, re | ASHRAE supplemental fitting 3-12; Co = K-aspect × K-Re × C'o. |  |  | Not verified |
| 3-13 | Elbows | Offset, S-shaped (gooseneck), rectangular and round | digitized | base, angle, ld | ASHRAE supplemental fitting 3-13; enter the applicable single-elbow C'o from fitting 3-1, 3-2, or 3-5, then Co = K × C'o. |  |  | Not verified |
| 3-14 | Elbows | Offset, S-shaped in two planes 90° apart, rectangular and round | digitized | base, angle, ld | ASHRAE supplemental fitting 3-14; enter the applicable single-elbow C'o, then Co = K × C'o. |  |  | Not verified |
| 3-15 | Elbows | Four 45° smooth-radius rectangular elbows arranged around an obstruction | digitized | velocity | ASHRAE supplemental fitting 3-15; valid for W/H = 4, r/H = 1.5, and L/H = 1.5. |  |  | Not verified |
| 4-1 | Transitions | Transition, round | digitized | area, angle | ASHRAE supplemental fitting 4-1. |  |  | Not verified |
| 4-2 | Transitions | Transition, rectangular, two sides parallel, symmetrical | digitized | area, angle | ASHRAE supplemental fitting 4-2. |  |  | Not verified |
| 4-3 | Transitions | Transition, rectangular, three sides straight | digitized | area, angle | ASHRAE supplemental fitting 4-3; published table is tentative. |  |  | Not verified |
| 4-4 | Transitions | Transition, rectangular, pyramidal | digitized | area, angle | ASHRAE supplemental fitting 4-4. |  |  | Not verified |
| 4-5 | Transitions | Transition, round/rectangular | digitized | area, angle | ASHRAE supplemental fitting 4-5. |  |  | Not verified |
| 4-6 | Transitions | Transition, rectangular to round | digitized | direction, re, ld, b | ASHRAE supplemental fitting 4-6; trilinear interpolation by Reynolds number, L/D, and B for the selected area direction. |  |  | Not verified |
| 4-7 | Transitions | Transition, rectangular to round, stepped, conical | digitized | area, lh, angle | ASHRAE supplemental fitting 4-7; trilinear interpolation by area ratio, length ratio, and angle. |  |  | Not verified |
| 5-1 | Junctions | Wye, 30°, converging | digitized |  | ASHRAE supplemental fitting 5-1; branch and straight/main coefficients are selected separately and interpolated from the published table. |  |  | Not verified |
| 5-2 | Junctions | Wye, 45°, converging, round | digitized |  | ASHRAE supplemental fitting 5-2; branch and straight/main coefficients are selected separately and interpolated from the published table. |  |  | Not verified |
| 5-3 | Junctions | Tee, converging, round | digitized |  | ASHRAE supplemental fitting 5-3; branch and straight/main coefficients are selected separately and interpolated from the published table. |  |  | Not verified |
| 5-4 | Junctions | Wye, 30°, converging, round, conical main | manual | c | ASHRAE supplemental fitting 5-4. Reference coefficient entry is provided because the source logic has not been safely digitized. |  |  | Not verified |
| 5-5 | Junctions | Wye, 45°, converging, round, conical main | manual | c | ASHRAE supplemental fitting 5-5. Reference coefficient entry is provided because the source logic has not been safely digitized. |  |  | Not verified |
| 5-6 | Junctions | Tee, converging, rectangular | digitized | configuration | ASHRAE supplemental fitting 5-6; branch and straight/main coefficients are selected separately and interpolated from the published table. Select the published Ab/As and Ab/Ac row. |  |  | Not verified |
| 5-7 | Junctions | Tee, converging, round tap to rectangular main | digitized |  | ASHRAE supplemental fitting 5-7; branch and straight/main coefficients are selected separately and interpolated from the published table. |  |  | Not verified |
| 5-8 | Junctions | Tee, converging, rectangular main and tap | digitized |  | ASHRAE supplemental fitting 5-8; branch and straight/main coefficients are selected separately and interpolated from the published table. |  |  | Not verified |
| 5-9 | Junctions | Converging rectangular main and tap, 45° entry | digitized |  | ASHRAE supplemental fitting 5-9; branch and straight/main coefficients are selected separately and interpolated from the published table. |  |  | Not verified |
| 5-10 | Junctions | Tee, diverging, round, conical branch | digitized |  | ASHRAE supplemental fitting 5-10; branch and straight/main coefficients are selected separately and interpolated from the published table. |  |  | Not verified |
| 5-11 | Junctions | Wye, 45°, diverging, round, conical branch | digitized |  | ASHRAE supplemental fitting 5-11; branch and straight/main coefficients are selected separately and interpolated from the published table. |  |  | Not verified |
| 5-12 | Junctions | Tee, diverging, round, with 90° elbow, branch 90° to main | digitized |  | ASHRAE supplemental fitting 5-12; branch and straight/main coefficients are selected separately and interpolated from the published table. |  |  | Not verified |
| 5-13 | Junctions | Tee, diverging, round, with 45° elbow, branch 90° to main | digitized |  | ASHRAE supplemental fitting 5-13; branch and straight/main coefficients are selected separately and interpolated from the published table. |  |  | Not verified |
| 5-14 | Junctions | Tee, diverging, round, conical branch with 45° elbow, branch 90° to main | digitized |  | ASHRAE supplemental fitting 5-14; branch and straight/main coefficients are selected separately and interpolated from the published table. |  |  | Not verified |
| 5-15 | Junctions | Wye, 45°, round, with 60° elbow, branch 90° to main | digitized |  | ASHRAE supplemental fitting 5-15; branch and straight/main coefficients are selected separately and interpolated from the published table. |  |  | Not verified |
| 5-16 | Junctions | Wye, 45°, diverging, round conical branch with 60° elbow, branch 90° to main | digitized |  | ASHRAE supplemental fitting 5-16; branch and straight/main coefficients are selected separately and interpolated from the published table. |  |  | Not verified |
| 5-17 | Junctions | Wye, 48°, diverging, conical main and branch with 45° elbow, branch 90° to main | digitized |  | ASHRAE supplemental fitting 5-17; branch and straight/main coefficients are selected separately and interpolated from the published table. |  |  | Not verified |
| 5-18 | Junctions | Tee, diverging, round, with 60° elbow, branch 45° to main | digitized |  | ASHRAE supplemental fitting 5-18; branch and straight/main coefficients are selected separately and interpolated from the published table. |  |  | Not verified |
| 5-19 | Junctions | Tee, diverging, round conical branch with 60° elbow, branch 45° to main | digitized |  | ASHRAE supplemental fitting 5-19; branch and straight/main coefficients are selected separately and interpolated from the published table. |  |  | Not verified |
| 5-20 | Junctions | Wye, 45°, diverging, round, with 30° elbow, branch 45° to main | digitized |  | ASHRAE supplemental fitting 5-20; branch and straight/main coefficients are selected separately and interpolated from the published table. |  |  | Not verified |
| 5-21 | Junctions | Wye, 45°, diverging, round conical branch with 30° elbow, branch 45° to main | digitized |  | ASHRAE supplemental fitting 5-21; branch and straight/main coefficients are selected separately and interpolated from the published table. |  |  | Not verified |
| 5-22 | Junctions | Tee, diverging, rectangular | digitized | configuration | ASHRAE supplemental fitting 5-22; branch and straight/main coefficients are selected separately and interpolated from the published table. Select the published Ab/As and Ab/Ac row. |  |  | Not verified |
| 5-23 | Junctions | Wye, diverging, rectangular and round | digitized | angle | ASHRAE supplemental fitting 5-23; branch and straight/main coefficients are selected separately and interpolated from the published table. The complete branch interpolation domain is 0.2 <= Ab/Ac <= 0.8; the partial 0.1 row is intentionally excluded. |  |  | Not verified |
| 5-24 | Junctions | Diverging wye, rectangular | digitized | angle | ASHRAE supplemental fitting 5-24; branch and straight/main coefficients are selected separately and interpolated from the published table. |  |  | Not verified |
| 5-25 | Junctions | Tee, diverging, rectangular main to round tap | digitized |  | ASHRAE supplemental fitting 5-25; branch and straight/main coefficients are selected separately and interpolated from the published table. |  |  | Not verified |
| 5-26 | Junctions | Tee, diverging, rectangular main to round tap, conical | digitized |  | ASHRAE supplemental fitting 5-26; branch and straight/main coefficients are selected separately and interpolated from the published table. |  |  | Not verified |
| 5-27 | Junctions | Tee, diverging, rectangular main and tap, 45° entry | digitized |  | ASHRAE supplemental fitting 5-27; branch and straight/main coefficients are selected separately and interpolated from the published table. |  |  | Not verified |
| 5-28 | Junctions | Tee, diverging, rectangular main and tap | digitized |  | ASHRAE supplemental fitting 5-28; branch and straight/main coefficients are selected separately and interpolated from the published table. |  |  | Not verified |
| 5-29 | Junctions | Tee, diverging, rectangular main and 45° tap, with damper | digitized |  | ASHRAE supplemental fitting 5-29; branch and straight/main coefficients are selected separately and interpolated from the published table. ASHRAE labels this dampered arrangement poor and says it should not be used. |  |  | Not verified |
| 5-30 | Junctions | Tee, diverging, rectangular main and tap, with damper | digitized |  | ASHRAE supplemental fitting 5-30; branch and straight/main coefficients are selected separately and interpolated from the published table. ASHRAE labels this dampered arrangement poor and says it should not be used. |  |  | Not verified |
| 5-31 | Junctions | Tee, diverging, rectangular, with extractor | digitized |  | ASHRAE supplemental fitting 5-31; branch and straight/main coefficients are selected separately and interpolated from the published table. ASHRAE labels this extractor arrangement poor and says it should not be used. |  |  | Not verified |
| 5-32 | Junctions | Symmetrical wye, dovetail, rectangular | digitized | direction | ASHRAE supplemental fitting 5-32; branch and straight/main coefficients are selected separately and interpolated from the published table. The published geometry has two equal branches, each at Qb/Qc = 0.5. |  |  | Not verified |
| 5-33 | Junctions | Wye, rectangular and round | digitized | direction, angle | ASHRAE supplemental fitting 5-33; branch and straight/main coefficients are selected separately and interpolated from the published table. The coefficient applies to either symmetrical branch. |  |  | Not verified |
| 5-34 | Junctions | Double 45° wye, rectangular and round | digitized | direction, branchArea, secondFlow | ASHRAE supplemental fitting 5-34; branch and straight/main coefficients are selected separately and interpolated from the published table. For converging flow, select the published second-branch flow and first-branch area ratios; for diverging flow, fitting 5-23 is used. |  |  | Not verified |
| 5-35 | Junctions | Cross, 90°, rectangular and round | digitized | direction, branchArea, secondFlow | ASHRAE supplemental fitting 5-35; branch and straight/main coefficients are selected separately and interpolated from the published table. For converging flow, select the published second-branch flow and first-branch area ratios; for diverging flow, fitting 5-23 is used. |  |  | Not verified |
| 6-1 | Obstructions | Damper, butterfly, round | digitized | dd, angle | ASHRAE supplemental fitting 6-1; built-in range is limited to the complete 0°-60° portion of the table. |  |  | Not verified |
| 6-2 | Obstructions | Damper, butterfly, rectangular | digitized | hw, angle | ASHRAE supplemental fitting 6-2. |  |  | Not verified |
| 6-3 | Obstructions | Damper, gate, round | digitized | hd | ASHRAE supplemental fitting 6-3; coefficients are interpolated from its published table. |  |  | Not verified |
| 6-4 | Obstructions | Damper, gate, rectangular | digitized | hw, hh | ASHRAE supplemental fitting 6-4; coefficients are interpolated from its published table. |  |  | Not verified |
| 6-5 | Obstructions | Damper, rectangular, parallel blades | digitized | lr, angle | ASHRAE supplemental fitting 6-5; coefficients are interpolated from its published table. |  |  | Not verified |
| 6-6 | Obstructions | Damper, rectangular, opposed blades | digitized | lr, angle | ASHRAE supplemental fitting 6-6; coefficients are interpolated from its published table. |  |  | Not verified |
| 6-7 | Obstructions | Obstruction, screen, round and rectangular | digitized | area, free | ASHRAE supplemental fitting 6-7. |  |  | Not verified |
| 6-8 | Obstructions | Obstruction, perforated plate, thick, round and rectangular | digitized | td, free | ASHRAE supplemental fitting 6-8; coefficients are interpolated from its published table. |  |  | Not verified |
| 6-9 | Obstructions | Obstruction, smooth cylinder in round and rectangular ducts | digitized | re, blocked, offset | ASHRAE supplemental fitting 6-9; Co = K × C'o. |  |  | Not verified |
| 6-10 | Obstructions | Round duct, depressed to avoid an obstruction | digitized |  | ASHRAE supplemental fitting 6-10; coefficients are interpolated from its published table. |  |  | Not verified |
| 6-11 | Obstructions | Rectangular duct, depressed to avoid an obstruction | digitized | wh, lh | ASHRAE supplemental fitting 6-11; coefficients are interpolated from its published table. |  |  | Not verified |
| 7-1 | Fan-system connections | Fans discharging into a plenum | digitized | blast, length | ASHRAE supplemental fitting 7-1. |  |  | Not verified |
| 7-2 | Fan-system connections | Single-width single-inlet fan with an outlet duct elbow | digitized | blast, position, length | ASHRAE supplemental fitting 7-2; outlet position is discrete: 0=A, 1=B, 2=C, 3=D. |  |  | Not verified |
| 7-3 | Fan-system connections | Double-width double-inlet fan with an outlet duct elbow | digitized | blast, position, length | ASHRAE supplemental fitting 7-3; outlet position is discrete: 0=A, 1=B, 2=C, 3=D. |  |  | Not verified |
| 7-4 | Fan-system connections | Nonuniform elbow into a fan inlet induced by a 90° round smooth-radius elbow without vanes | digitized | rd, ld | ASHRAE supplemental fitting 7-4. |  |  | Not verified |
| 7-5 | Fan-system connections | Nonuniform elbow into a fan inlet induced by 90° mitered and multipiece elbows without vanes | digitized | type, rd, ld | ASHRAE supplemental fitting 7-5; elbow type is discrete: 0=mitered, 1=three-piece, 2=four-piece or more. |  |  | Not verified |
| 7-6 | Fan-system connections | Nonuniform elbow into a fan inlet induced by a 90° square smooth-radius elbow | digitized | type, rd, ld | ASHRAE supplemental fitting 7-6; vane type is discrete: 0=no vanes, 1=full-radius vanes, 2=short vanes. |  |  | Not verified |
| 7-7 | Fan-system connections | Fans located in plenums and cabinet enclosures | digitized | ld | ASHRAE supplemental fitting 7-7. |  |  | Not verified |
| 7-8 | Fan-system connections | Fan without an outlet diffuser | digitized | blast | ASHRAE supplemental fitting 7-8; poor configuration noted in the source. |  |  | Not verified |
| 7-9 | Fan-system connections | Plane asymmetric diffuser at fan outlet without ductwork | digitized | angle, area | ASHRAE supplemental fitting 7-9; coefficients are interpolated from its published table. |  |  | Not verified |
| 7-10 | Fan-system connections | Pyramidal diffuser at fan outlet without ductwork | digitized | angle, area | ASHRAE supplemental fitting 7-10; coefficients are interpolated from its published table. |  |  | Not verified |
| 7-11 | Fan-system connections | Plane symmetric diffuser at fan outlet with ductwork | digitized | angle, area | ASHRAE supplemental fitting 7-11. |  |  | Not verified |
| 7-12 | Fan-system connections | Plane asymmetric diffuser at fan outlet with ductwork - arrangement 1 | digitized | angle, area | ASHRAE supplemental fitting 7-12. |  |  | Not verified |
| 7-13 | Fan-system connections | Plane asymmetric diffuser at fan outlet with ductwork - arrangement 2 | digitized | angle, area | ASHRAE supplemental fitting 7-13. |  |  | Not verified |
| 7-14 | Fan-system connections | Plane asymmetric diffuser at fan outlet with ductwork - arrangement 3 | digitized | angle, area | ASHRAE supplemental fitting 7-14. |  |  | Not verified |
| 7-15 | Fan-system connections | Pyramidal diffuser at fan outlet with ductwork | digitized | angle, area | ASHRAE supplemental fitting 7-15. |  |  | Not verified |
| GRD-GRILLE | GRDs | Grille - Titus 350R | digitized | sizeIndex | Titus 350R published performance tables. Select a nominal duct size; requested airflow is rounded up to the next listed cfm value. Performance data courtesy of Titus. |  |  | Not verified |
| GRD-DIFFUSER | GRDs | Diffuser - Price SCD Square Cone | digitized | face, neck | Price SCD published performance table. Requested airflow is rounded up to the next published cfm value for the selected face and neck size. |  |  | Not verified |
| GRD-LINEAR-DIFFUSER | GRDs | Linear Diffuser - Titus TBD-80 D | digitized | length, slotWidth, slotCount | Titus TBD-80 D published performance tables. The smallest published inlet that covers the requested airflow is selected, then airflow is rounded up to the next listed cfm value. Performance data courtesy of Titus. |  |  | Not verified |
| VAV-REHEAT | VAVs | VAV with Reheat - Titus DESV | digitized | sizeIndex, rows | Titus PESV, AESV, DESV Hot Water Coil Capacity, pages M20-M22; airside ΔPs data. Coil performance in accordance with AHRI 410-2001. Airside pressure drop is linearly interpolated between published airflow points. |  |  | Not verified |
| VAV-COOLING | VAVs | VAV without Reheat - Titus DESV | digitized | sizeIndex | Titus PESV, AESV, DESV Discharge Sound Performance, page M18; Min ΔPs is the inlet-to-outlet static pressure drop with the primary damper full open. Tests in accordance with ASHRAE 130-2008 and AHRI 880-2011. Minimum pressure drop is linearly interpolated between published airflow points. |  |  | Not verified |
