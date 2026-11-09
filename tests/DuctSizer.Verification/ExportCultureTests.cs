using System.Globalization;
using DuctSizer.Core;

/// <summary>
/// Exports must not depend on the Windows regional settings: a PC set to German or French (comma decimal separator)
/// must write exactly the same CSV and PDF as an English one. A comma inside a PDF drawing command corrupts the file.
/// </summary>
internal static class ExportCultureTests
{
    public static void ExportsDoNotDependOnRegionalSettings()
    {
        var system = SystemTests.BuildDivergingSystem();
        SystemSolver.Solve(system.Document);
        var directory = Path.Combine(Path.GetTempPath(), "fanworks-culture-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var original = CultureInfo.CurrentCulture;
        var originalUi = CultureInfo.CurrentUICulture;
        try
        {
            var reference = Export(CultureInfo.InvariantCulture, "invariant");
            foreach (var name in new[] { "de-DE", "fr-FR", "en-US" })
            {
                var culture = CultureInfo.GetCultureInfo(name);
                if (culture.NumberFormat.NumberDecimalSeparator != "," && name != "en-US")
                    throw new Exception($"Culture {name} is not available with its comma decimal separator on this machine.");
                var exported = Export(culture, name);
                if (!exported.Csv.SequenceEqual(reference.Csv))
                    throw new Exception($"The CSV export differs under {name} regional settings.");
                if (!exported.Pdf.SequenceEqual(reference.Pdf))
                    throw new Exception($"The PDF export differs under {name} regional settings.");
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
            CultureInfo.CurrentUICulture = originalUi;
            try { Directory.Delete(directory, recursive: true); } catch (IOException) { }
        }

        (byte[] Csv, byte[] Pdf) Export(CultureInfo culture, string tag)
        {
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
            var csv = Path.Combine(directory, tag + ".csv");
            var pdf = Path.Combine(directory, tag + ".pdf");
            DuctSystemCsv.Export(csv, system.Document);
            var headers = new[] { "ID", "Description", "Airflow", "Size", "Loss" };
            var rows = new[] { new[] { "D-001", "Main", "2,000 cfm", "16 in", "0.123 in. w.c." } };
            var calculations = new[]
            {
                new DuctSystemPdfCalculationSection("REQUIRED EQUATIONS", new[]
                {
                    new DuctSystemPdfCalculationLine("\\Delta p = f \\frac{L}{D} \\frac{\\rho V^{2}}{2}", "0.123 in. w.c.", IsEquation: true),
                    new DuctSystemPdfCalculationLine("\\rho = 1.2 kg/m3, \\mu = 1.8e-5 Pa s", "1.2", IsEquation: true, HighlightAsInput: true)
                })
            };
            var items = new[]
            {
                new DuctSystemPdfReportItem("D-001", "Main", "STRAIGHT DUCT",
                    new[] { new DuctSystemPdfField("Airflow", "2,000 cfm") },
                    new[] { new DuctSystemPdfField("Pressure drop", "0.123 in. w.c.") }, calculations)
            };
            var metadata = new DuctSystemPdfMetadata("Engineer", "Culture check", "1", "2026-11-09",
                PrintedDate: "November 9, 2026", CriticalPath: "L-001 to L-003");
            DuctSystemPdf.Export(pdf, "Duct System Schedule", "Culture check", headers, rows,
                "Critical-path total pressure loss: 0.123 in. w.c.", metadata, reportItems: items);
            return (File.ReadAllBytes(csv), File.ReadAllBytes(pdf));
        }
    }
}
