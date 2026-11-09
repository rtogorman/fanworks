# FanWorks developer guide

How to build, test, change and release FanWorks. Read `ARCHITECTURE.md` first for how the code is organized, and
`DEVELOPMENT_PROGRESS.md` for the current state of the work.

## Requirements

- Windows 10 or 11 (x64 or ARM64).
- .NET 8 SDK, any 8.0 feature band (`global.json` pins major version 8). The owner's working copy also has a local SDK
  under `work\dotnet-sdk`, which `BuildRelease.ps1` prefers when present.
- Visual Studio 2022 with the ".NET desktop development" workload is optional.

## Build and test

```powershell
# Everything: engineering checks, interface checks, then a self-contained single-file release
.\BuildRelease.ps1                     # x64 (runs under emulation on ARM64)
.\BuildRelease.ps1 -Runtime win-arm64  # native ARM64

# Or double-click RunBuild.cmd: same as the first command, output written to build-log.txt

# Individually
dotnet run --project tests\DuctSizer.Verification -c Release
dotnet build tests\DuctSizer.UiVerification -c Release -p:UseAppHost=false
dotnet tests\DuctSizer.UiVerification\bin\Release\net8.0-windows\DuctSizer.UiVerification.dll
```

The release goes to `outputs\FanWorks-<runtime>-v<version>\` with `SHA256.txt`. The version is the `<Version>` in
`src\DuctSizer.WinForms\DuctSizer.WinForms.csproj`.

Warnings are errors in the product and engineering-test projects. The CI workflow
(`.github/workflows/build.yml`) runs the same three stages on every push once the repository is on GitHub.

## Rules for changes

These follow the project's priorities: engineering accuracy, then stability and preservation of user work, then
reproducibility, then code clarity.

1. **Calculations live in `DuctSizer.Core`.** Never calculate in a form or a dialog. SI inside; convert units only at
   the UI/report edge with `UnitConversions`.
2. **No silent fallbacks.** Invalid input throws `ArgumentOutOfRangeException`; numerical failure throws
   `InvalidOperationException`; a system value that cannot be calculated is NaN and is named as an issue. Do not
   `catch` and return 0. Catch only the specific exception types you expect.
3. **State the method.** Every public calculation documents its equation, constants, units, source and limits in XML
   documentation. New table data needs its source edition and page, and a row in `docs/TABLE_VERIFICATION.md`.
4. **Deterministic.** Same inputs, same method, same data give bit-identical results. Rounding and tie-breaking rules
   are explicit (see `DuctSizingSolver`).
5. **Culture-invariant files.** Anything written to a file (JSON, CSV, PDF commands) uses
   `CultureInfo.InvariantCulture`. The check "Exports do not depend on regional settings" guards this.
6. **Positions are stored absolute and entered relative.** Never store a position only as an offset; never show the
   user raw absolute coordinates for a connected component. Use `LayoutPositionReference` in dialogs.
7. **Saved format changes are versioned.** Increment `DuctSystemDocument.SchemaVersion`, add the upgrade step in
   `DuctSystemStore.Load`, and add a round-trip check. Never make an old file unreadable.
8. **Every defect fix gets a regression check** in `tests\DuctSizer.Verification` (engineering) or the interface suite.
9. **Pin behavior before moving code.** For refactoring, compare the solved state of a reference project before and
   after (see "Reference comparison" below) and expect bit-identical output.

## Adding an engineering check

Add a `static void` method that throws on failure (in an existing `*Tests.cs` file or a new one) and register it in
the `tests` array at the top of `tests\DuctSizer.Verification\Program.cs`. Prefer expected values from an
independent source (ASHRAE, NIST, a manufacturer table) over values produced by the code itself.

## Adding or correcting a fitting table

1. Find the definition by its ASHRAE code in `FittingCatalog.Build()` (`src\DuctSizer.Core\FittingCatalog.cs`).
2. Enter the table exactly as published: axis values and coefficients, in the published order. Use the existing
   interpolation helpers (`I1`, `I2`, `RaggedJunction`); they flag out-of-range inputs automatically.
3. Put the source in the definition's source note, and update the register row in `docs\TABLE_VERIFICATION.md`.
4. Add a check that reproduces at least one published value exactly and one edge value.

## Diagnostics

Logs and crash reports are in `%LOCALAPPDATA%\FanWorks\logs`. A crash report contains the exception chain, the
FanWorks version, OS and architecture. Ask users to send the latest `crash-*.txt` with any problem report.

## Interface tests

`tests\DuctSizer.UiVerification` builds a real `MainForm`, drives it, and checks the results. It uses the internal
`AutomationOptions` (via `InternalsVisibleTo`) to store its working file in a temporary folder and to suppress
dialogs; it never touches the user's own project. It also reaches private members by name through `UiMember`, which
looks on `MainForm` and then on its screen classes, so a renamed private method can break it: search the test for the
old name when renaming.

## Reference comparison

`DEVELOPMENT_PROGRESS.md` records the owner's reference project results (per-item losses and the critical path). Before
and after any change that should not alter results, load the reference project, run `SystemSolver.Solve` and
`CriticalPathAnalyzer.Analyze`, and compare. The reference file itself is not in the repository because it contains
a client drawing.

## Release checklist

1. Update `<Version>` in `src\DuctSizer.WinForms\DuctSizer.WinForms.csproj` and the README "What changed" section.
2. Run `.\BuildRelease.ps1` (or `RunBuild.cmd`); every check must pass.
3. Compare the reference project against the recorded results.
4. Record the release in `DEVELOPMENT_PROGRESS.md`; tag the commit `v<version>`.
