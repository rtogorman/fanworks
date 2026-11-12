using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DuctSizer.Core;

public static class DuctSystemCsv
{
    private const string Version = "4";
    private const string Version3 = "3";
    private const string Version2 = "2";
    private const string LegacyVersion = "1";
    private static readonly string[] LegacyHeader =
    {
        "DuctSizer CSV Version", "Record Type", "Sequence", "Description", "Fitting Code",
        "Airflow m3/s", "Temperature C", "Relative Humidity", "Atmospheric Pressure Pa", "Shape",
        "Diameter m", "Width m", "Height m", "Material", "Roughness ft",
        "Equivalent Diameter Method", "Pressure Loss Basis", "Length m", "Reference Velocity m/s",
        "Fitting Inputs JSON", "Junction JSON"
    };
    private static readonly string[] Version2Header =
    {
        "DuctSizer CSV Version", "Record Type", "Sequence", "Description", "Fitting Code",
        "Airflow m3/s", "Temperature C", "Relative Humidity", "Atmospheric Pressure Pa", "Shape",
        "Diameter m", "Width m", "Height m", "Material", "Roughness ft",
        "Equivalent Diameter Method", "Pressure Loss Basis", "Length m", "Reference Velocity m/s",
        "Fitting Inputs JSON", "Junction JSON", "Custom Item ID"
    };
    private static readonly string[] Version3Header = Version2Header
        .Concat(new[] { "Air System Type", "Branch ID" })
        .ToArray();
    private static readonly string[] Header = Version3Header
        .Concat(new[] { "Delta X m", "Delta Y m", "Delta Z m" })
        .ToArray();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static void Export(string path, DuctSystemDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        EnsureParentDirectory(path);
        var builder = new StringBuilder();
        WriteRow(builder, Header);
        foreach (var duct in document.Ducts.OrderBy(x => x.Sequence))
        {
            WriteRow(builder, new[]
            {
                Version, "Duct", I(duct.Sequence), duct.Description, "", R(duct.AirflowM3PerS), R(duct.TemperatureC),
                R(duct.RelativeHumidity), R(duct.AtmosphericPressurePa), duct.Shape.ToString(), R(duct.DiameterM),
                R(duct.WidthM), R(duct.HeightM), duct.MaterialName, R(duct.RoughnessFeet),
                duct.EquivalentDiameterMethod.ToString(), duct.PressureLossBasis.ToString(), R(duct.LengthM), "", "", "", "",
                duct.AirSystemType, duct.BranchId, R(duct.DeltaXM), R(duct.DeltaYM), R(duct.DeltaZM)
            });
        }
        foreach (var fitting in document.Fittings.OrderBy(x => x.Sequence))
        {
            WriteRow(builder, new[]
            {
                Version, "Fitting", I(fitting.Sequence), fitting.Description, fitting.FittingCode,
                R(fitting.AirflowM3PerS), R(fitting.TemperatureC), R(fitting.RelativeHumidity),
                R(fitting.AtmosphericPressurePa), fitting.Shape.ToString(), R(fitting.DiameterM), R(fitting.WidthM),
                R(fitting.HeightM), "", "", "", "", "", R(fitting.ReferenceVelocityMPerS),
                JsonSerializer.Serialize(fitting.Inputs, JsonOptions),
                fitting.Junction is null ? "" : JsonSerializer.Serialize(fitting.Junction, JsonOptions),
                fitting.CustomId, fitting.AirSystemType, fitting.BranchId,
                R(fitting.DeltaXM), R(fitting.DeltaYM), R(fitting.DeltaZM)
            });
        }
        File.WriteAllText(path, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }

    public static DuctSystemDocument Import(string path)
    {
        var records = Parse(File.ReadAllText(path, Encoding.UTF8));
        if (records.Count == 0)
            throw new InvalidDataException("This is not a FanWorks system CSV file, or its columns were changed.");
        var isLegacy = records[0].SequenceEqual(LegacyHeader, StringComparer.Ordinal);
        var isVersion2 = records[0].SequenceEqual(Version2Header, StringComparer.Ordinal);
        var isVersion3 = records[0].SequenceEqual(Version3Header, StringComparer.Ordinal);
        if (!isLegacy && !isVersion2 && !isVersion3 && !records[0].SequenceEqual(Header, StringComparer.Ordinal))
            throw new InvalidDataException("This is not a FanWorks system CSV file, or its columns were changed.");
        var expectedColumnCount = isLegacy ? LegacyHeader.Length : isVersion2 ? Version2Header.Length
            : isVersion3 ? Version3Header.Length : Header.Length;
        var expectedVersion = isLegacy ? LegacyVersion : isVersion2 ? Version2 : isVersion3 ? Version3 : Version;

        var document = new DuctSystemDocument();
        for (var rowNumber = 1; rowNumber < records.Count; rowNumber++)
        {
            var row = records[rowNumber];
            if (row.Count == 1 && string.IsNullOrWhiteSpace(row[0])) continue;
            if (row.Count != expectedColumnCount)
                throw new InvalidDataException($"CSV row {rowNumber + 1} has {row.Count} columns; expected {expectedColumnCount}.");
            if (row[0] != expectedVersion)
                throw new InvalidDataException($"CSV row {rowNumber + 1} uses unsupported format version '{row[0]}'.");

            if (string.Equals(row[1], "Duct", StringComparison.OrdinalIgnoreCase))
            {
                var duct = new SavedDuct
                {
                    Sequence = PositiveInt(row[2], rowNumber, "Sequence"),
                    Description = row[3].Trim(),
                    AirflowM3PerS = Number(row[5], rowNumber, "Airflow m3/s"),
                    TemperatureC = Number(row[6], rowNumber, "Temperature C"),
                    RelativeHumidity = Number(row[7], rowNumber, "Relative Humidity"),
                    AtmosphericPressurePa = Number(row[8], rowNumber, "Atmospheric Pressure Pa"),
                    Shape = EnumValue<DuctShape>(row[9], rowNumber, "Shape"),
                    DiameterM = Number(row[10], rowNumber, "Diameter m"),
                    WidthM = Number(row[11], rowNumber, "Width m"),
                    HeightM = Number(row[12], rowNumber, "Height m"),
                    MaterialName = row[13].Trim(),
                    RoughnessFeet = Number(row[14], rowNumber, "Roughness ft"),
                    EquivalentDiameterMethod = EnumValue<EquivalentDiameterMethod>(row[15], rowNumber, "Equivalent Diameter Method"),
                    PressureLossBasis = EnumValue<PressureLossBasis>(row[16], rowNumber, "Pressure Loss Basis"),
                    LengthM = Number(row[17], rowNumber, "Length m"),
                    AirSystemType = isLegacy || isVersion2 ? "Supply Air" : TextValue(row[22], "Supply Air"),
                    BranchId = isLegacy || isVersion2 ? "Main" : TextValue(row[23], "Main"),
                    DeltaXM = isLegacy || isVersion2 || isVersion3 ? 0 : Number(row[24], rowNumber, "Delta X m"),
                    DeltaYM = isLegacy || isVersion2 || isVersion3 ? 0 : Number(row[25], rowNumber, "Delta Y m"),
                    DeltaZM = isLegacy || isVersion2 || isVersion3 ? 0 : Number(row[26], rowNumber, "Delta Z m")
                };
                _ = duct.Calculate();
                document.Ducts.Add(duct);
            }
            else if (string.Equals(row[1], "Fitting", StringComparison.OrdinalIgnoreCase))
            {
                var fitting = new SavedFitting
                {
                    Sequence = PositiveInt(row[2], rowNumber, "Sequence"),
                    CustomId = isLegacy ? "" : row[21].Trim(),
                    Description = row[3].Trim(),
                    FittingCode = row[4].Trim(),
                    AirflowM3PerS = Number(row[5], rowNumber, "Airflow m3/s"),
                    TemperatureC = Number(row[6], rowNumber, "Temperature C"),
                    RelativeHumidity = Number(row[7], rowNumber, "Relative Humidity"),
                    AtmosphericPressurePa = Number(row[8], rowNumber, "Atmospheric Pressure Pa"),
                    Shape = EnumValue<DuctShape>(row[9], rowNumber, "Shape"),
                    DiameterM = Number(row[10], rowNumber, "Diameter m"),
                    WidthM = Number(row[11], rowNumber, "Width m"),
                    HeightM = Number(row[12], rowNumber, "Height m"),
                    ReferenceVelocityMPerS = Number(row[18], rowNumber, "Reference Velocity m/s"),
                    Inputs = JsonValue<Dictionary<string, double>>(row[19], rowNumber, "Fitting Inputs JSON") ?? new(),
                    Junction = JsonValue<JunctionFlowData>(row[20], rowNumber, "Junction JSON"),
                    AirSystemType = isLegacy || isVersion2 ? "Supply Air" : TextValue(row[22], "Supply Air"),
                    BranchId = isLegacy || isVersion2 ? "Main" : TextValue(row[23], "Main"),
                    DeltaXM = isLegacy || isVersion2 || isVersion3 ? 0 : Number(row[24], rowNumber, "Delta X m"),
                    DeltaYM = isLegacy || isVersion2 || isVersion3 ? 0 : Number(row[25], rowNumber, "Delta Y m"),
                    DeltaZM = isLegacy || isVersion2 || isVersion3 ? 0 : Number(row[26], rowNumber, "Delta Z m")
                };
                _ = fitting.Calculate();
                document.Fittings.Add(fitting);
            }
            else
                throw new InvalidDataException($"CSV row {rowNumber + 1} has unknown record type '{row[1]}'.");
        }

        if (document.Ducts.Select(x => x.Sequence).Distinct().Count() != document.Ducts.Count)
            throw new InvalidDataException("The CSV contains duplicate duct sequence numbers.");
        if (document.Fittings.Select(x => x.Sequence).Distinct().Count() != document.Fittings.Count)
            throw new InvalidDataException("The CSV contains duplicate fitting sequence numbers.");
        document.NextSequence = document.Ducts.Count == 0 ? 1 : document.Ducts.Max(x => x.Sequence) + 1;
        document.NextFittingSequence = document.Fittings.Count == 0 ? 1 : document.Fittings.Max(x => x.Sequence) + 1;
        return document;
    }

    private static string R(double value) => value.ToString("R", CultureInfo.InvariantCulture);
    private static string I(int value) => value.ToString(CultureInfo.InvariantCulture);
    private static string TextValue(string text, string fallback) => string.IsNullOrWhiteSpace(text) ? fallback : text.Trim();

    private static void WriteRow(StringBuilder builder, IEnumerable<string> fields) =>
        builder.AppendLine(string.Join(',', fields.Select(Escape)));

    private static string Escape(string value) =>
        value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) < 0 ? value : $"\"{value.Replace("\"", "\"\"")}\"";

    private static List<List<string>> Parse(string text)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        var quoted = false;
        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            if (quoted)
            {
                if (character == '"' && index + 1 < text.Length && text[index + 1] == '"')
                {
                    field.Append('"'); index++;
                }
                else if (character == '"') quoted = false;
                else field.Append(character);
            }
            else if (character == '"' && field.Length == 0) quoted = true;
            else if (character == ',') { row.Add(field.ToString()); field.Clear(); }
            else if (character is '\r' or '\n')
            {
                if (character == '\r' && index + 1 < text.Length && text[index + 1] == '\n') index++;
                row.Add(field.ToString()); field.Clear(); rows.Add(row); row = new List<string>();
            }
            else field.Append(character);
        }
        if (quoted) throw new InvalidDataException("The CSV ends inside a quoted value.");
        if (field.Length > 0 || row.Count > 0) { row.Add(field.ToString()); rows.Add(row); }
        return rows;
    }

    private static int PositiveInt(string text, int row, string column) =>
        int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value > 0
            ? value : throw Bad(row, column, text);

    private static double Number(string text, int row, string column) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && double.IsFinite(value)
            ? value : throw Bad(row, column, text);

    private static T EnumValue<T>(string text, int row, string column) where T : struct, Enum =>
        Enum.TryParse<T>(text, ignoreCase: true, out var value) ? value : throw Bad(row, column, text);

    private static T? JsonValue<T>(string text, int row, string column) where T : class
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        try { return JsonSerializer.Deserialize<T>(text, JsonOptions); }
        catch (JsonException) { throw Bad(row, column, text); }
    }

    private static InvalidDataException Bad(int row, string column, string value) =>
        new($"CSV row {row + 1} has an invalid {column} value: '{value}'.");

    private static void EnsureParentDirectory(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
    }
}

public sealed record DuctSystemPdfMetadata(
    string UserName,
    string ProjectName,
    string ProjectNumber,
    string ProjectDate,
    string LogoJpegBase64 = "",
    int LogoPixelWidth = 0,
    int LogoPixelHeight = 0,
    string PrintedDate = "",
    string FanId = "",
    string FanAirflow = "",
    string FanStaticPressure = "",
    string FanEfficiency = "",
    string FanHorsepower = "",
    string CriticalPath = "",
    DuctSystemPdfPressureProfile? PressureProfile = null,
    DuctSystemPdfLeakageSummary? Leakage = null);

/// <summary>
/// The system's SMACNA duct pressure class, seal class, leakage class and allowable leakage, ready to show (Analysis
/// tab and report title page). Built once in project units so screen and report agree.
/// </summary>
/// <param name="Available">False when the leakage could not be calculated; <paramref name="Note"/> says why.</param>
/// <param name="PressureClass">e.g. "1 in. w.c."</param>
/// <param name="PressureBasis">The design pressure and where it came from, e.g. "0.993 in. w.c. critical-path loss, rounded up".</param>
/// <param name="SealClass">e.g. "Seal Class C"</param>
/// <param name="SealDetail">What the seal class seals, e.g. "Transverse joints only".</param>
/// <param name="LeakageClass">e.g. "CL 12 round, 24 rectangular"</param>
/// <param name="Factor">Allowable leakage F, e.g. "12 / 24 cfm per 100 ft2 (round / rect.)"</param>
/// <param name="Leakage">Total allowable leakage, e.g. "61.5 cfm"</param>
/// <param name="LeakageDetail">e.g. "2.6 % of 2,400 cfm; 612 ft2 of duct and fittings"</param>
/// <param name="CalculationLines">The worked calculation, one line per duct type plus the total.</param>
/// <param name="Criteria">The standards applied.</param>
public sealed record DuctSystemPdfLeakageSummary(
    bool Available,
    string PressureClass,
    string PressureBasis,
    string SealClass,
    string SealDetail,
    string LeakageClass,
    string Factor,
    string Leakage,
    string LeakageDetail,
    IReadOnlyList<string> CalculationLines,
    string Criteria,
    string Note = "");

/// <summary>
/// Running pressure loss along every route of the system, in display units, for the report title page (and the
/// Analysis tab graph). Series hold complete routes only; <paramref name="Note"/> says what was left out and why.
/// </summary>
/// <param name="UnitLabel">Unit of the values, e.g. "in. w.c." or "Pa".</param>
/// <param name="PathCount">Paths in the system (start to each terminal outlet), including any left out of <paramref name="Series"/>.</param>
/// <param name="Comparison">How close the next-highest path is to the critical path, e.g. for balancing paths; empty when not known.</param>
public sealed record DuctSystemPdfPressureProfile(
    string UnitLabel,
    IReadOnlyList<DuctSystemPdfProfileSeries> Series,
    int PathCount,
    string Note = "",
    string Comparison = "");

/// <summary>One route: Values[i] is the running loss after component i + 1 along the route.</summary>
public sealed record DuctSystemPdfProfileSeries(string Label, IReadOnlyList<double> Values, bool IsCritical,
    IReadOnlyList<string>? ItemIds = null);

public sealed record DuctSystemPdfSchematic(
    string Title,
    string ImageJpegBase64,
    int PixelWidth,
    int PixelHeight,
    bool CriticalPathHighlighted = false,
    double FeetPerGridCell = 1.0,
    double PixelsPerFoot = 28.0);

public sealed record DuctSystemPdfLayoutNode(
    string Id,
    string Caption,
    string ComponentType,
    string FittingGroup,
    int GridX,
    int GridY,
    int InletPorts,
    int OutletPorts,
    string AirSystemType,
    string BranchId = "Main",
    bool BranchOutletBelow = false,
    bool IsDefined = true,
    bool AirflowOverride = false);

public sealed record DuctSystemPdfLayoutConnection(
    string UpstreamId,
    string DownstreamId,
    bool SecondaryInput,
    bool IsSized,
    string DuctId = "",
    string BranchId = "Main",
    string PressureLoss = "—");

public sealed record DuctSystemPdfLayout(
    IReadOnlyList<DuctSystemPdfLayoutNode> Nodes,
    IReadOnlyList<DuctSystemPdfLayoutConnection> Connections);

public sealed record DuctSystemPdfField(string Label, string Value);

public sealed record DuctSystemPdfCalculationLine(
    string Formula,
    string HighlightValue = "",
    bool IsEquation = false,
    bool HighlightAsInput = false,
    string InputSubstitution = "");

/// <param name="IsTitle">
/// A worksheet title ("DUCT SIZING CALCULATION", "SURFACE AREA CALCULATION") rather than a section. A title after the
/// first starts a secondary calculation: it is drawn below the assumptions, after a divider line.
/// </param>
public sealed record DuctSystemPdfCalculationSection(
    string Heading,
    IReadOnlyList<DuctSystemPdfCalculationLine> Lines,
    bool IsTitle = false)
{
    /// <summary>A worksheet title with no lines.</summary>
    public static DuctSystemPdfCalculationSection Title(string text) => new(text, Array.Empty<DuctSystemPdfCalculationLine>(), true);
}

public sealed record DuctSystemPdfReportItem(
    string Id,
    string Description,
    string ItemType,
    IReadOnlyList<DuctSystemPdfField> Inputs,
    IReadOnlyList<DuctSystemPdfField> Outputs,
    IReadOnlyList<DuctSystemPdfCalculationSection>? Calculations = null,
    string ReferenceImageJpegBase64 = "",
    int ReferenceImagePixelWidth = 0,
    int ReferenceImagePixelHeight = 0,
    IReadOnlyList<DuctSystemPdfField>? QuickCheck = null);

public static class DuctSystemPdf
{
    private sealed record EmbeddedImage(string Name, byte[] Data, int Width, int Height);

    public static void Export(string path, string title, string subtitle, IReadOnlyList<string> headers,
        IReadOnlyList<IReadOnlyList<string>> rows, string total,
        DuctSystemPdfMetadata? metadata = null, DuctSystemPdfLayout? layout = null,
        IReadOnlyList<DuctSystemPdfReportItem>? reportItems = null,
        IReadOnlyList<DuctSystemPdfSchematic>? schematics = null)
    {
        if (headers.Count is < 5 or > 24 || rows.Any(x => x.Count != headers.Count))
            throw new ArgumentException("The system PDF requires between five and twenty-four consistently sized table columns.");
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);

        byte[]? logo = null;
        if (metadata is { LogoPixelWidth: > 0, LogoPixelHeight: > 0 }
            && !string.IsNullOrWhiteSpace(metadata.LogoJpegBase64))
        {
            try { logo = Convert.FromBase64String(metadata.LogoJpegBase64); }
            catch (FormatException)
            {
                metadata = metadata with { LogoJpegBase64 = "", LogoPixelWidth = 0, LogoPixelHeight = 0 };
            }
        }

        // Fewer schedule rows provide enough vertical room for wrapped values without
        // clipping the last line against the title-block footer.
        const int rowsPerPage = 8;
        var tablePageCount = Math.Max(1, (int)Math.Ceiling(rows.Count / (double)rowsPerPage));
        var reports = reportItems ?? Array.Empty<DuctSystemPdfReportItem>();
        var embeddedImages = new List<EmbeddedImage>();
        var referenceImages = new Dictionary<int, EmbeddedImage>();
        for (var index = 0; index < reports.Count; index++)
        {
            var report = reports[index];
            if (report.ReferenceImagePixelWidth <= 0 || report.ReferenceImagePixelHeight <= 0
                || string.IsNullOrWhiteSpace(report.ReferenceImageJpegBase64))
                continue;
            try
            {
                referenceImages[index] = new EmbeddedImage($"Ref{index}",
                    Convert.FromBase64String(report.ReferenceImageJpegBase64),
                    report.ReferenceImagePixelWidth, report.ReferenceImagePixelHeight);
                embeddedImages.Add(referenceImages[index]);
            }
            catch (FormatException)
            {
                // A malformed optional reference image must not prevent the engineering report from exporting.
            }
        }
        var schematicImages = new List<(DuctSystemPdfSchematic Schematic, EmbeddedImage Image)>();
        foreach (var (schematic, index) in (schematics ?? Array.Empty<DuctSystemPdfSchematic>()).Select((value, index) => (value, index)))
        {
            if (schematic.PixelWidth <= 0 || schematic.PixelHeight <= 0 || string.IsNullOrWhiteSpace(schematic.ImageJpegBase64))
                continue;
            try
            {
                var image = new EmbeddedImage($"View{index}", Convert.FromBase64String(schematic.ImageJpegBase64),
                    schematic.PixelWidth, schematic.PixelHeight);
                schematicImages.Add((schematic, image));
                embeddedImages.Add(image);
            }
            catch (FormatException)
            {
                // A malformed optional schematic must not prevent the engineering report from exporting.
            }
        }
        // Each component normally fills one sheet; one with more calculations than fit continues on the next.
        var itemSheetCounts = reports.Select((item, index) => BuildItemSheets(subtitle, total, metadata, item,
            referenceImages.GetValueOrDefault(index), 1, 1).Count).ToArray();
        var pageCount = 1 + schematicImages.Count + tablePageCount + itemSheetCounts.Sum();
        var contents = new List<byte[]>();
        var reportContents = new ReportContents(schematicImages.Select(entry => entry.Schematic.Title).ToArray(),
            reports, itemSheetCounts, tablePageCount);
        contents.Add(Encoding.ASCII.GetBytes(BuildTitleSheet(title, subtitle, total, metadata, 1, pageCount, reportContents)));
        // Keep the graphical system views at the front of the report, immediately after the
        // project/fan-sizing cover, so reviewers can orient themselves before the calculations.
        foreach (var schematic in schematicImages)
            contents.Add(Encoding.ASCII.GetBytes(BuildSchematicSheet(schematic.Schematic, schematic.Image,
                subtitle, metadata, contents.Count + 1, pageCount)));
        // Component worksheets follow the graphical overview and retain connected-system order.
        for (var reportIndex = 0; reportIndex < reports.Count; reportIndex++)
        {
            referenceImages.TryGetValue(reportIndex, out var referenceImage);
            foreach (var sheet in BuildItemSheets(subtitle, total, metadata, reports[reportIndex], referenceImage,
                         contents.Count + 1, pageCount))
                contents.Add(Encoding.ASCII.GetBytes(sheet));
        }
        for (var page = 0; page < tablePageCount; page++)
            contents.Add(Encoding.ASCII.GetBytes(BuildScheduleSheet(title, subtitle, headers,
                rows.Skip(page * rowsPerPage).Take(rowsPerPage).ToArray(), total, metadata, contents.Count + 1, pageCount)));
        WritePdf(path, contents, logo, metadata?.LogoPixelWidth ?? 0, metadata?.LogoPixelHeight ?? 0,
            embeddedImages);
    }

    /// <summary>What the report holds, for the title sheet's contents list and system summary.</summary>
    private sealed record ReportContents(IReadOnlyList<string> ViewTitles, IReadOnlyList<DuctSystemPdfReportItem> Items,
        IReadOnlyList<int> ItemSheetCounts, int SchedulePageCount);

    private static string BuildTitleSheet(string title, string subtitle, string total,
        DuctSystemPdfMetadata? metadata, int page, int pageCount, ReportContents contents)
    {
        var builder = new StringBuilder();
        DrawSheetChrome(builder, metadata, "Project Summary", page, pageCount);
        const string accent = "0.16 0.43 0.58", ink = "0.06 0.20 0.31", muted = "0.36 0.42 0.47";
        string Or(string? value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value;

        // Project block (condensed, round Q): name, then number, who, when and what on one row.
        builder.Append("0.94 0.97 0.99 rg 0.72 0.80 0.87 RG 0.8 w ");
        RoundedRectangle(builder, 32, 446, 728, 82, 9, fillAndStroke: true);
        builder.Append("0.12 0.66 0.72 rg 32 446 5 82 re f\n");
        TextColor(builder, 52, 512, 7.2, "PROJECT", accent, bold: true);
        FittedText(builder, 52, 490, 690, 20, 12, Or(metadata?.ProjectName, "Untitled Project"), bold: true, color: ink);
        var info = new[]
        {
            ("PROJECT NO.", Or(metadata?.ProjectNumber, "Not entered")),
            ("DESIGNER", Or(metadata?.UserName, "Not entered")),
            ("PROJECT DATE", Or(metadata?.ProjectDate, "Not entered")),
            ("PRINTED", Or(metadata?.PrintedDate, DateTime.Now.ToString("MMMM d, yyyy", CultureInfo.CurrentCulture))),
            ("REPORT", title)
        };
        for (var index = 0; index < info.Length; index++)
        {
            var x = 52 + index * 140;
            TextColor(builder, x, 471, 6.4, info[index].Item1, accent, bold: true);
            FittedText(builder, x, 457, 130, 9.5, 6.5, info[index].Item2, color: ink);
        }

        // Fan selection: the numbers a reviewer looks for first.
        TextColor(builder, 32, 428, 9.5, "FAN SELECTION", ink, bold: true);
        builder.Append("0.12 0.66 0.72 rg 32 423 46 1.6 re f\n");
        var fanValues = new[]
        {
            ("FAN", metadata?.FanId), ("DESIGN AIRFLOW", metadata?.FanAirflow),
            ("SYSTEM STATIC PRESSURE", metadata?.FanStaticPressure), ("FAN EFFICIENCY", metadata?.FanEfficiency),
            ("REQUIRED MOTOR POWER", metadata?.FanHorsepower)
        };
        const double tileGap = 10, tileTop = 414, tileHeight = 46;
        var tileWidth = (728 - tileGap * (fanValues.Length - 1)) / fanValues.Length;
        for (var index = 0; index < fanValues.Length; index++)
        {
            var x = 32 + index * (tileWidth + tileGap);
            builder.Append("1 1 1 rg 0.72 0.80 0.87 RG 0.7 w ");
            RoundedRectangle(builder, x, tileTop - tileHeight, tileWidth, tileHeight, 6, fillAndStroke: true);
            builder.Append(FormattableString.Invariant($"0.12 0.30 0.47 rg {x + 6:0.##} {tileTop - 3:0.##} {tileWidth - 12:0.##} 2 re f\n"));
            TextColor(builder, x + 10, tileTop - 15, 6.2, fanValues[index].Item1, accent, bold: true);
            FittedText(builder, x + 10, tileTop - 35, tileWidth - 20, 13, 8, Or(fanValues[index].Item2, "Not available"),
                bold: true, color: ink);
        }
        TextColor(builder, 32, 354, 6.6, "CRITICAL PATH", accent, bold: true);
        foreach (var (line, index) in WrapPdfText(Or(metadata?.CriticalPath, "Not available"), 150).Take(2)
                     .Select((value, index) => (value, index)))
            Text(builder, 112, 354 - index * 10, 8.2, line, bold: index == 0);
        TextColor(builder, 32, 334, 8.2, Fit(total, 150), ink, bold: true);
        TextColor(builder, 32, 323, 7, Fit(
            "System static pressure is the fan selection pressure. The critical-path total excludes fan positive static.", 160), muted);

        // Lower area. Left: report contents (compact), duct pressure class and leakage (SMACNA), system summary donut.
        // Right: pressure drop along every path.
        const double areaTop = 312, areaBottom = 62, leftX = 32, leftWidth = 420, rightX = 462, rightWidth = 298;
        const double contentsBottom = 262, leakageTop = 254, leakageBottom = 146, summaryTop = 138;
        foreach (var (top, bottom) in new[] { (areaTop, contentsBottom), (leakageTop, leakageBottom), (summaryTop, areaBottom) })
        {
            builder.Append("1 1 1 rg 0.72 0.80 0.87 RG 0.7 w ");
            RoundedRectangle(builder, leftX, bottom, leftWidth, top - bottom, 7, fillAndStroke: true);
        }
        builder.Append("1 1 1 rg 0.72 0.80 0.87 RG 0.7 w ");
        RoundedRectangle(builder, rightX, areaBottom, rightWidth, areaTop - areaBottom, 7, fillAndStroke: true);

        // Report contents: two columns of entries with their pages.
        TextColor(builder, leftX + 14, areaTop - 14, 8, "REPORT CONTENTS", ink, bold: true);
        var entries = new List<(string Name, int First, int Last)> { ("Project summary", 1, 1) };
        var next = 2;
        if (contents.ViewTitles.Count > 0)
        {
            entries.Add(("System views", next, next + contents.ViewTitles.Count - 1));
            next += contents.ViewTitles.Count;
        }
        static bool IsDuct(DuctSystemPdfReportItem item) => item.ItemType.Contains("DUCT", StringComparison.OrdinalIgnoreCase);
        static bool IsFitting(DuctSystemPdfReportItem item) => item.ItemType.Equals("FITTING", StringComparison.OrdinalIgnoreCase);
        if (contents.Items.Count > 0)
            entries.Add(("Item sheets, in system order", next, next + contents.ItemSheetCounts.Sum() - 1));
        next += contents.ItemSheetCounts.Sum();
        entries.Add(("Duct system schedule", next, next + contents.SchedulePageCount - 1));
        for (var index = 0; index < entries.Count && index < 4; index++)
        {
            var column = index % 2;
            var row = index / 2;
            var x = leftX + 14 + column * 200;
            var y = areaTop - 29 - row * 14;
            Text(builder, x, y, 7.4, entries[index].Name, bold: true);
            var pages = entries[index].First == entries[index].Last
                ? entries[index].First.ToString(CultureInfo.InvariantCulture)
                : $"{entries[index].First}-{entries[index].Last}";
            TextColor(builder, x + 176 - EstimatedPdfWidth(pages, 7.4, true), y, 7.4, pages, accent, bold: true);
        }

        DrawLeakagePanel(builder, metadata?.Leakage, leftX, leakageBottom, leftWidth, leakageTop - leakageBottom, accent, ink, muted);

        // System summary: a donut of duct segments, fittings and components, each labelled with a leader arrow.
        TextColor(builder, leftX + 14, summaryTop - 14, 8, "SYSTEM SUMMARY", ink, bold: true);
        TextColor(builder, leftX + 14, summaryTop - 24, 6.2,
            $"{pageCount} report page{(pageCount == 1 ? string.Empty : "s")}", muted);
        DrawSystemSummaryDonut(builder, leftX + leftWidth / 2 + 20, (summaryTop + areaBottom) / 2 - 2, 29, 17, new[]
        {
            ("Duct segments", contents.Items.Count(IsDuct), "0.16 0.43 0.58"),
            ("Fittings", contents.Items.Count(IsFitting), "0.12 0.66 0.72"),
            ("Components", contents.Items.Count(item => !IsDuct(item) && !IsFitting(item)), "0.55 0.64 0.73")
        }, leftX + 120, leftX + leftWidth - 16, ink, muted, areaBottom + 14, summaryTop - 24);

        DrawPressureProfilePanel(builder, metadata?.PressureProfile, rightX, areaBottom, rightWidth, areaTop - areaBottom,
            accent, ink, muted);
        return builder.ToString();
    }

    /// <summary>
    /// The title page's SMACNA block: pressure class, seal class, leakage class and allowable leakage, then the basis,
    /// the worked calculation (F = CL x P^0.65; leakage = F x A / 100) and the standards applied.
    /// </summary>
    private static void DrawLeakagePanel(StringBuilder builder, DuctSystemPdfLeakageSummary? leakage, double left,
        double bottom, double width, double height, string accent, string ink, string muted)
    {
        var top = bottom + height;
        TextColor(builder, left + 14, top - 14, 8, "DUCT PRESSURE CLASS AND LEAKAGE (SMACNA)", ink, bold: true);
        if (leakage is null)
        {
            TextColor(builder, left + 14, top - 34, 7, "Not available.", muted);
            return;
        }
        var values = new[]
        {
            ("PRESSURE CLASS", leakage.PressureClass), ("SEAL CLASS", leakage.SealClass),
            ("LEAKAGE CLASS", leakage.LeakageClass), ("DUCT LEAKAGE", leakage.Leakage)
        };
        const double columnWidth = 98;
        for (var index = 0; index < values.Length; index++)
        {
            var x = left + 14 + index * columnWidth;
            TextColor(builder, x, top - 28, 5.8, values[index].Item1, accent, bold: true);
            FittedText(builder, x, top - 41, columnWidth - 8, 11, 6.2, values[index].Item2, bold: true,
                color: index == 3 && !leakage.Available ? "0.70 0.13 0.13" : ink);
        }
        var lines = new List<(string Text, bool Bold)>();
        if (leakage.PressureBasis.Length > 0)
            lines.Add(($"{leakage.PressureBasis}. {leakage.SealDetail}.", false));
        lines.AddRange(leakage.CalculationLines.Select(line => (line, true)));
        if (!string.IsNullOrWhiteSpace(leakage.LeakageDetail))
            lines.Add(("Duct leakage is " + leakage.LeakageDetail + ".", false));
        if (!string.IsNullOrWhiteSpace(leakage.Note))
            lines.Add(("Not available: " + leakage.Note, true));
        var y = top - 53;
        var criteria = WrapPdfText(leakage.Criteria, 118).ToArray();
        var lastLineY = bottom + 6 + (criteria.Length - 1) * 7;
        foreach (var (text, bold) in lines)
            foreach (var wrapped in WrapPdfText(text, 112))
            {
                if (y < lastLineY + 8)
                    break;
                TextColor(builder, left + 14, y, 6, wrapped, bold ? ink : muted, bold);
                y -= 7.6;
            }
        for (var index = 0; index < criteria.Length; index++)
            TextColor(builder, left + 14, bottom + 6 + (criteria.Length - 1 - index) * 7, 5.6, criteria[index], accent);
    }

    /// <summary>
    /// A donut chart of item counts, each segment labelled "Name / Quantity n" at the side of the chart with a leader
    /// line ending in an arrowhead on its segment. Labels on one side are spread so they never overlap.
    /// </summary>
    private static void DrawSystemSummaryDonut(StringBuilder builder, double cx, double cy, double outer, double inner,
        IReadOnlyList<(string Name, int Count, string Rgb)> segments, double minX, double maxX, string ink, string muted,
        double minY, double maxY)
    {
        var total = segments.Sum(x => x.Count);
        if (total <= 0)
        {
            builder.Append("0.88 0.91 0.94 rg ");
            DonutSegment(builder, cx, cy, outer, inner, 0, 359.99);
            TextColor(builder, cx + outer + 24, cy - 3, 8, "No items in the report yet", muted);
            return;
        }
        var start = 90.0; // clockwise from 12 o'clock
        var labels = new List<(string Name, int Count, string Rgb, double Mid)>();
        foreach (var (name, count, rgb) in segments)
        {
            if (count <= 0)
            {
                labels.Add((name, count, rgb, double.NaN));
                continue;
            }
            var sweep = 360.0 * count / total;
            builder.Append(rgb + " rg ");
            DonutSegment(builder, cx, cy, outer, inner, start - sweep, start);
            if (count < total)
            {
                // White separators between segments.
                builder.Append("1 1 1 RG 1.2 w ");
                foreach (var angle in new[] { start, start - sweep })
                {
                    var r = angle * Math.PI / 180;
                    builder.Append(FormattableString.Invariant(
                        $"{cx + inner * Math.Cos(r):0.##} {cy + inner * Math.Sin(r):0.##} m {cx + outer * Math.Cos(r):0.##} {cy + outer * Math.Sin(r):0.##} l S\n"));
                }
            }
            labels.Add((name, count, rgb, start - sweep / 2));
            start -= sweep;
        }
        TextColor(builder, cx - EstimatedPdfWidth(total.ToString(CultureInfo.InvariantCulture), 15, true) / 2, cy - 2, 15,
            total.ToString(CultureInfo.InvariantCulture), ink, bold: true);
        TextColor(builder, cx - EstimatedPdfWidth("ITEMS", 5.6, true) / 2, cy - 11, 5.6, "ITEMS", muted, bold: true);

        // Leader labels: right side for segments whose middle faces right, left side otherwise. Items with a count of
        // zero are listed on the right without a leader.
        const double spacing = 26;
        foreach (var right in new[] { true, false })
        {
            var side = labels.Where(x => double.IsFinite(x.Mid) && (Math.Cos(x.Mid * Math.PI / 180) >= 0) == right)
                .Select(x => (x.Name, x.Count, x.Rgb, x.Mid, Y: cy + (outer + 12) * Math.Sin(x.Mid * Math.PI / 180)))
                .OrderByDescending(x => x.Y).ToList();
            if (side.Count > 0 && side[0].Y > maxY)
                side[0] = side[0] with { Y = maxY };
            for (var index = 1; index < side.Count; index++)
                if (side[index - 1].Y - side[index].Y < spacing)
                    side[index] = side[index] with { Y = side[index - 1].Y - spacing };
            // Keep the lowest label (and its "Quantity" line) inside the panel; shift the side up if needed.
            var lift = side.Count == 0 ? 0 : Math.Max(0, minY - side[^1].Y);
            if (lift > 0)
                side = side.Select(x => x with { Y = x.Y + lift }).ToList();
            foreach (var label in side)
            {
                var radians = label.Mid * Math.PI / 180;
                var tipX = cx + (outer + 1.5) * Math.Cos(radians);
                var tipY = cy + (outer + 1.5) * Math.Sin(radians);
                var elbowX = cx + (outer + 14) * Math.Cos(radians);
                var textX = right ? cx + outer + 26 : cx - outer - 26;
                builder.Append("0.42 0.50 0.58 RG 0.7 w ");
                builder.Append(FormattableString.Invariant(
                    $"{textX + (right ? -4 : 4):0.##} {label.Y:0.##} m {elbowX:0.##} {label.Y:0.##} l {tipX:0.##} {tipY:0.##} l S\n"));
                // Arrowhead pointing at the segment along the last leg of the leader.
                var dx = tipX - elbowX;
                var dy = tipY - label.Y;
                var length = Math.Max(0.01, Math.Sqrt(dx * dx + dy * dy));
                var ux = dx / length;
                var uy = dy / length;
                builder.Append(FormattableString.Invariant(
                    $"0.42 0.50 0.58 rg {tipX:0.##} {tipY:0.##} m {tipX - 4.5 * ux - 2.2 * uy:0.##} {tipY - 4.5 * uy + 2.2 * ux:0.##} l {tipX - 4.5 * ux + 2.2 * uy:0.##} {tipY - 4.5 * uy - 2.2 * ux:0.##} l h f\n"));
                DrawLeaderLabel(builder, right ? textX : Math.Max(minX, textX - LabelWidth(label.Name, label.Count)), label.Y,
                    label.Name, label.Count, label.Rgb, ink, muted);
            }
        }
        var missing = labels.Where(x => !double.IsFinite(x.Mid)).ToArray();
        for (var index = 0; index < missing.Length; index++)
            DrawLeaderLabel(builder, Math.Min(maxX - 80, cx + outer + 34), cy - outer - 4 + index * -12 + 6,
                missing[index].Name, 0, missing[index].Rgb, ink, muted, compact: true);
    }

    private static double LabelWidth(string name, int count) =>
        Math.Max(EstimatedPdfWidth(name, 8, true), EstimatedPdfWidth($"Quantity {count}", 7.4, false)) + 12;

    private static void DrawLeaderLabel(StringBuilder builder, double x, double y, string name, int count, string rgb,
        string ink, string muted, bool compact = false)
    {
        builder.Append(FormattableString.Invariant($"{rgb} rg {x:0.##} {y - 3:0.##} 7 7 re f\n"));
        if (compact)
        {
            TextColor(builder, x + 11, y - 2, 7, $"{name}, quantity 0", muted);
            return;
        }
        TextColor(builder, x + 11, y + 1, 8, name, ink, bold: true);
        TextColor(builder, x + 11, y - 9, 7.4, $"Quantity {count.ToString(CultureInfo.InvariantCulture)}", muted);
    }

    /// <summary>A filled ring segment between two angles (degrees, counter-clockwise from 3 o'clock).</summary>
    private static void DonutSegment(StringBuilder builder, double cx, double cy, double outer, double inner,
        double fromDegrees, double toDegrees)
    {
        var from = fromDegrees * Math.PI / 180;
        var to = toDegrees * Math.PI / 180;
        builder.Append(FormattableString.Invariant($"{cx + outer * Math.Cos(from):0.###} {cy + outer * Math.Sin(from):0.###} m "));
        AppendArc(builder, cx, cy, outer, from, to);
        builder.Append(FormattableString.Invariant($"{cx + inner * Math.Cos(to):0.###} {cy + inner * Math.Sin(to):0.###} l "));
        AppendArc(builder, cx, cy, inner, to, from);
        builder.Append("h f\n");
    }

    /// <summary>Appends a circular arc as cubic Bezier pieces of at most 45 degrees (the current point is its start).</summary>
    private static void AppendArc(StringBuilder builder, double cx, double cy, double radius, double from, double to)
    {
        var pieces = Math.Max(1, (int)Math.Ceiling(Math.Abs(to - from) / (Math.PI / 4)));
        var step = (to - from) / pieces;
        var k = 4.0 / 3.0 * Math.Tan(step / 4);
        for (var index = 0; index < pieces; index++)
        {
            var a = from + index * step;
            var b = a + step;
            var (ax, ay) = (Math.Cos(a), Math.Sin(a));
            var (bx, by) = (Math.Cos(b), Math.Sin(b));
            builder.Append(FormattableString.Invariant(
                $"{cx + radius * (ax - k * ay):0.###} {cy + radius * (ay + k * ax):0.###} {cx + radius * (bx + k * by):0.###} {cy + radius * (by - k * bx):0.###} {cx + radius * bx:0.###} {cy + radius * by:0.###} c "));
        }
    }

    /// <summary>
    /// The title page's graph of running pressure loss along every path (the Analysis tab graph): x is the component
    /// number along a path, y the loss from its start. The critical path is drawn in amber on top of the others.
    /// </summary>
    private static void DrawPressureProfilePanel(StringBuilder builder, DuctSystemPdfPressureProfile? profile,
        double left, double bottom, double width, double height, string accent, string ink, string muted)
    {
        var top = bottom + height;
        TextColor(builder, left + 14, top - 17, 8.4, "PRESSURE DROP ALONG EACH PATH", ink, bold: true);
        if (profile is null || profile.PathCount == 0)
        {
            TextColor(builder, left + 14, top - 40, 8, "No connected paths: draw the system in the Designer.", muted);
            return;
        }
        var pathText = profile.PathCount.ToString(CultureInfo.InvariantCulture);
        TextColor(builder, left + 14, top - 42, 18, pathText, ink, bold: true);
        TextColor(builder, left + 20 + EstimatedPdfWidth(pathText, 18, true), top - 37, 6.6,
            profile.PathCount == 1 ? "PATH THROUGH THE SYSTEM" : "PATHS THROUGH THE SYSTEM", accent, bold: true);
        TextColor(builder, left + 20 + EstimatedPdfWidth(pathText, 18, true), top - 46, 6.4,
            "Running pressure drop from the start of the system to each terminal outlet", muted);
        var noteLines = string.IsNullOrWhiteSpace(profile.Note)
            ? Array.Empty<string>()
            : WrapPdfText(profile.Note, 92).Take(2).ToArray();
        for (var index = 0; index < noteLines.Length; index++)
            TextColor(builder, left + 14, bottom + 10 + (noteLines.Length - 1 - index) * 8, 6.2, noteLines[index], "0.62 0.36 0.02");

        var series = profile.Series.Where(x => x.Values.Count > 0).ToArray();
        var plotLeft = left + 50;
        var plotRight = left + width - 14;
        var comparisonLines = string.IsNullOrWhiteSpace(profile.Comparison)
            ? Array.Empty<string>()
            : WrapPdfText(profile.Comparison, 82).Take(2).ToArray();
        var plotTop = top - 72 - comparisonLines.Length * 9;
        var plotBottom = bottom + 34 + (noteLines.Length > 0 ? noteLines.Length * 8 + 2 : 0);
        if (series.Length == 0)
        {
            TextColor(builder, plotLeft, (plotTop + plotBottom) / 2, 8, "No path can be shown yet.", muted);
            return;
        }
        var xMax = Math.Max(2, series.Max(x => x.Values.Count));
        var (yMax, yStep) = ProfileAxis(series.SelectMany(x => x.Values).Max());
        var decimals = yStep >= 1 ? 0 : Math.Min(4, (int)Math.Ceiling(-Math.Log10(yStep) - 1e-9));
        double Px(int component) => plotLeft + (component - 1) * (plotRight - plotLeft) / (xMax - 1);
        double Py(double value) => plotBottom + value / yMax * (plotTop - plotBottom);

        builder.Append("0.89 0.92 0.95 RG 0.5 w ");
        for (var index = 0; index <= (int)Math.Round(yMax / yStep); index++)
        {
            var value = index * yStep;
            builder.Append(FormattableString.Invariant($"{plotLeft:0.##} {Py(value):0.##} m {plotRight:0.##} {Py(value):0.##} l S\n"));
            var text = value.ToString("F" + decimals, CultureInfo.InvariantCulture);
            TextColor(builder, plotLeft - 5 - EstimatedPdfWidth(text, 6.4, false), Py(value) - 2.2, 6.4, text, muted);
            builder.Append("0.89 0.92 0.95 RG 0.5 w ");
        }
        var xStep = new[] { 1, 2, 5, 10, 20, 25, 50, 100 }.FirstOrDefault(step => xMax / step <= 12, Math.Max(1, xMax / 10));
        for (var component = 1; component <= xMax; component += xStep)
        {
            var text = component.ToString(CultureInfo.InvariantCulture);
            TextColor(builder, Px(component) - EstimatedPdfWidth(text, 6.4, false) / 2, plotBottom - 10, 6.4, text, muted);
        }
        builder.Append(FormattableString.Invariant(
            $"0.63 0.69 0.75 RG 0.7 w {plotLeft:0.##} {plotTop:0.##} m {plotLeft:0.##} {plotBottom:0.##} l {plotRight:0.##} {plotBottom:0.##} l S\n"));
        var xTitle = "Component number along the path";
        TextColor(builder, (plotLeft + plotRight) / 2 - EstimatedPdfWidth(xTitle, 6.6, false) / 2, plotBottom - 21, 6.6, xTitle, muted);
        var yTitle = PdfText($"Pressure drop ({profile.UnitLabel})");
        var yTitleX = left + 16;
        var yTitleY = (plotTop + plotBottom) / 2 - EstimatedPdfWidth(yTitle, 6.6, false) / 2;
        builder.Append($"{muted} rg ");
        builder.Append(FormattableString.Invariant($"BT /F1 6.6 Tf 0 1 -1 0 {yTitleX:0.##} {yTitleY:0.##} Tm ({EscapePdf(yTitle)}) Tj ET\n"));

        void Line(DuctSystemPdfProfileSeries line, string rgb, double lineWidth, double dot)
        {
            builder.Append(FormattableString.Invariant($"{rgb} RG {lineWidth:0.##} w 1 J 1 j "));
            for (var index = 0; index < line.Values.Count; index++)
                builder.Append(FormattableString.Invariant($"{Px(index + 1):0.##} {Py(line.Values[index]):0.##} {(index == 0 ? "m" : "l")} "));
            builder.Append("S\n");
            if (line.Values.Count > 40)
                return;
            builder.Append($"{rgb} rg ");
            for (var index = 0; index < line.Values.Count; index++)
                builder.Append(FormattableString.Invariant(
                    $"{Px(index + 1) - dot:0.##} {Py(line.Values[index]) - dot:0.##} {dot * 2:0.##} {dot * 2:0.##} re "));
            builder.Append("f\n");
        }
        foreach (var line in series.Where(x => !x.IsCritical))
            Line(line, "0.52 0.68 0.80", 0.9, 0.9);
        foreach (var line in series.Where(x => x.IsCritical))
            Line(line, "0.84 0.50 0.00", 1.8, 1.4);
        builder.Append("0 J 0 j\n");

        for (var index = 0; index < comparisonLines.Length; index++)
            TextColor(builder, left + 14, top - 71 - index * 8.5, 6.4, comparisonLines[index], ink, bold: true);

        // Legend under the headline.
        var legendY = top - 60;
        var legendX = plotLeft;
        var critical = series.FirstOrDefault(x => x.IsCritical);
        if (critical is not null)
        {
            builder.Append(FormattableString.Invariant($"0.84 0.50 0.00 RG 1.8 w {legendX:0.##} {legendY + 2:0.##} m {legendX + 14:0.##} {legendY + 2:0.##} l S\n"));
            var text = Fit($"Critical path ({critical.Label})", 44);
            TextColor(builder, legendX + 18, legendY, 6.4, text, ink);
            legendX += 26 + EstimatedPdfWidth(PdfText(text), 6.4, false);
        }
        var others = series.Count(x => !x.IsCritical);
        if (others > 0)
        {
            builder.Append(FormattableString.Invariant($"0.52 0.68 0.80 RG 0.9 w {legendX:0.##} {legendY + 2:0.##} m {legendX + 14:0.##} {legendY + 2:0.##} l S\n"));
            TextColor(builder, legendX + 18, legendY, 6.4, others == 1 ? "Other path" : $"Other paths ({others})", ink);
        }
    }

    /// <summary>A rounded axis maximum (at least the data) and a 1-2-2.5-5 step giving about five intervals.</summary>
    private static (double Max, double Step) ProfileAxis(double dataMax)
    {
        if (!double.IsFinite(dataMax) || dataMax <= 0)
            return (1, 0.2);
        var rough = dataMax / 5;
        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(rough)));
        var step = new[] { 1, 2, 2.5, 5, 10 }.Select(x => x * magnitude).First(x => x >= rough);
        return (Math.Ceiling(dataMax / step - 1e-9) * step, step);
    }

    private static string BuildSchematicSheet(DuctSystemPdfSchematic schematic, EmbeddedImage image,
        string subtitle, DuctSystemPdfMetadata? metadata, int page, int pageCount)
    {
        var builder = new StringBuilder();
        DrawSheetChrome(builder, metadata, schematic.Title, page, pageCount);
        Text(builder, 40, 519, 8, schematic.CriticalPathHighlighted
            ? "Critical-path ducts and components are highlighted in amber."
            : subtitle, bold: schematic.CriticalPathHighlighted);
        const double boxX = 32, boxY = 61, boxWidth = 728, boxHeight = 448;
        const double legendHeight = 48;
        builder.Append("1 1 1 rg 0.66 0.76 0.84 RG 0.8 w ");
        builder.Append(FormattableString.Invariant($"{boxX} {boxY} {boxWidth} {boxHeight} re B\n"));
        var imageAreaY = boxY + legendHeight;
        var imageAreaHeight = boxHeight - legendHeight;
        var scale = Math.Min((boxWidth - 8) / image.Width, (imageAreaHeight - 8) / image.Height);
        var imageWidth = image.Width * scale;
        var imageHeight = image.Height * scale;
        builder.Append(FormattableString.Invariant(
            $"q {imageWidth:0.##} 0 0 {imageHeight:0.##} {boxX + (boxWidth - imageWidth) / 2:0.##} {imageAreaY + (imageAreaHeight - imageHeight) / 2:0.##} cm /{image.Name} Do Q\n"));
        DrawSchematicScaleLegend(builder, schematic, boxX, boxY, boxWidth, legendHeight, scale);
        return builder.ToString();
    }

    /// <summary>
    /// The scale strip along the foot of a view sheet, laid out like a drawing title strip: graphic scale bar,
    /// grid spacing, and the approximate sheet scale, each in its own labelled cell.
    /// </summary>
    private static void DrawSchematicScaleLegend(StringBuilder builder, DuctSystemPdfSchematic schematic,
        double left, double bottom, double width, double height, double imageScale)
    {
        const string ink = "0.10 0.29 0.43", accent = "0.16 0.43 0.58", muted = "0.36 0.42 0.47";
        var feetPerGridCell = Math.Max(0.01, schematic.FeetPerGridCell);
        var pointsPerFoot = Math.Max(0.001, imageScale * Math.Max(0.001, schematic.PixelsPerFoot));
        var inchesPerFoot = pointsPerFoot / 72.0;

        builder.Append("0.955 0.973 0.985 rg ");
        builder.Append(FormattableString.Invariant($"{left:0.##} {bottom:0.##} {width:0.##} {height:0.##} re f\n"));
        builder.Append("0.66 0.76 0.84 RG 0.7 w ");
        builder.Append(FormattableString.Invariant($"{left:0.##} {bottom + height:0.##} m {left + width:0.##} {bottom + height:0.##} l S\n"));
        var cellWidths = new[] { width * .44, width * .18, width * .22, width * .16 };
        var cellLefts = new double[cellWidths.Length];
        var x = left;
        for (var index = 0; index < cellWidths.Length; index++)
        {
            cellLefts[index] = x;
            if (index > 0)
                builder.Append(FormattableString.Invariant($"0.72 0.80 0.87 RG 0.6 w {x:0.##} {bottom + 6:0.##} m {x:0.##} {bottom + height - 6:0.##} l S\n"));
            x += cellWidths[index];
        }
        var labelY = bottom + height - 13;

        // Graphic scale: a four-part bar with 0, half and full lengths in feet.
        var barLeft = cellLefts[0] + 14;
        var maximumBarWidth = cellWidths[0] - 40;
        var barFeet = NiceScaleLength(maximumBarWidth / pointsPerFoot);
        var barWidth = Math.Min(maximumBarWidth, barFeet * pointsPerFoot);
        const int segments = 4;
        const double barHeight = 6;
        var barBottom = bottom + 14;
        var segmentWidth = barWidth / segments;
        TextColor(builder, barLeft, labelY, 6.4, "GRAPHIC SCALE (FEET)", accent, bold: true);
        for (var index = 0; index < segments; index++)
        {
            builder.Append(index % 2 == 0 ? "0.10 0.29 0.43 rg " : "1 1 1 rg ");
            builder.Append("0.10 0.29 0.43 RG 0.55 w ");
            builder.Append(FormattableString.Invariant(
                $"{barLeft + index * segmentWidth:0.##} {barBottom:0.##} {segmentWidth:0.##} {barHeight:0.##} re B\n"));
        }
        TextColor(builder, barLeft - 1.5, bottom + 5, 5.8, "0", ink);
        CenteredTextColor(builder, barLeft + barWidth / 2, bottom + 5, 5.8,
            (barFeet / 2).ToString("0.###", CultureInfo.InvariantCulture), 14, ink);
        CenteredTextColor(builder, barLeft + barWidth, bottom + 5, 5.8,
            barFeet.ToString("0.###", CultureInfo.InvariantCulture) + " ft", 18, ink);

        // Grid spacing and sheet scale.
        TextColor(builder, cellLefts[1] + 12, labelY, 6.4, "GRID SPACING:", accent, bold: true);
        TextColor(builder, cellLefts[1] + 12, bottom + 12, 11,
            $"{feetPerGridCell.ToString("0.###", CultureInfo.InvariantCulture)} ft / cell", ink, bold: true);
        TextColor(builder, cellLefts[2] + 12, labelY, 6.4, "SHEET SCALE:", accent, bold: true);
        TextColor(builder, cellLefts[2] + 12, bottom + 12, 11, $"{ArchitecturalScale(inchesPerFoot)}\" = 1'-0\"", ink, bold: true);
        TextColor(builder, cellLefts[2] + 12, bottom + 3.5, 5.6,
            $"approx. {inchesPerFoot.ToString("0.###", CultureInfo.InvariantCulture)} in/ft on a letter sheet", muted);
        TextColor(builder, cellLefts[3] + 12, labelY, 6.4, "VIEW", accent, bold: true);
        FittedText(builder, cellLefts[3] + 12, bottom + 12, cellWidths[3] - 20, 8.5, 6,
            schematic.Title.Replace("Layout Designer - ", string.Empty, StringComparison.Ordinal), bold: true, color: ink);
    }

    private static double NiceScaleLength(double maximumFeet)
    {
        maximumFeet = Math.Max(0.001, maximumFeet);
        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(maximumFeet)));
        foreach (var multiplier in new[] { 5D, 2D, 1D })
        {
            var candidate = multiplier * magnitude;
            if (candidate <= maximumFeet + 0.0000001)
                return candidate;
        }
        return magnitude / 2D;
    }

    private static string ArchitecturalScale(double inchesPerFoot)
    {
        var standardScales = new (double Value, string Label)[]
        {
            (1D / 64D, "1/64"), (1D / 32D, "1/32"), (3D / 64D, "3/64"),
            (1D / 16D, "1/16"), (3D / 32D, "3/32"), (1D / 8D, "1/8"),
            (3D / 16D, "3/16"), (1D / 4D, "1/4"), (3D / 8D, "3/8"),
            (1D / 2D, "1/2"), (3D / 4D, "3/4"), (1D, "1"),
            (1.5D, "1 1/2"), (2D, "2"), (3D, "3")
        };
        return standardScales.MinBy(scale => Math.Abs(scale.Value - inchesPerFoot)).Label;
    }

    private static string BuildScheduleSheet(string title, string subtitle, IReadOnlyList<string> headers,
        IReadOnlyList<IReadOnlyList<string>> rows, string total, DuctSystemPdfMetadata? metadata,
        int page, int pageCount)
    {
        var builder = new StringBuilder();
        DrawSheetChrome(builder, metadata, title, page, pageCount);
        Text(builder, 40, 519, 8.2, subtitle);
        Text(builder, 40, 503, 8.2, total, bold: true);
        var widths = ScheduleWidths(headers);
        const double left = 30, top = 484, headerHeight = 44;
        builder.Append("0.12 0.30 0.47 rg ");
        builder.Append(FormattableString.Invariant($"{left} {top - headerHeight} 732 {headerHeight} re f\n"));
        DrawScheduleRow(builder, headers, widths, left, top, headerHeight, bold: true);
        var y = top - headerHeight;
        foreach (var row in rows)
        {
            var rowHeight = ScheduleRowHeight(row, widths);
            if (((int)((top - y) / Math.Max(1, rowHeight))) % 2 == 1)
            {
                builder.Append("0.96 0.98 0.99 rg ");
                builder.Append(FormattableString.Invariant($"{left} {y - rowHeight:0.##} 732 {rowHeight:0.##} re f\n"));
            }
            DrawScheduleRow(builder, row, widths, left, y, rowHeight, bold: false);
            y -= rowHeight;
        }
        return builder.ToString();
    }

    private static double[] ScheduleWidths(IReadOnlyList<string> headers)
    {
        var preferred = headers.Select(header =>
        {
            var lower = header.ToLowerInvariant();
            if (lower.Contains("description")) return 110d;
            if (lower.Contains("item id")) return 48d;
            if (lower.Contains("duct size")) return 70d;
            if (lower.Contains("pressure")) return 78d;
            if (lower.Contains("airflow")) return 58d;
            if (lower.Contains("temperature")) return 62d;
            if (lower.Contains("air system")) return 66d;
            return 54d;
        }).ToArray();
        var factor = 732d / preferred.Sum();
        return preferred.Select(value => value * factor).ToArray();
    }

    private static double ScheduleRowHeight(IReadOnlyList<string> values, double[] widths)
    {
        var lines = values.Select((value, index) => WrapPdfText(value,
            Math.Max(4, (int)((widths[index] - 6) / 3.25))).Count()).DefaultIfEmpty(1).Max();
        return Math.Clamp(8 + lines * 7.5, 23, 47);
    }

    private static void DrawScheduleRow(StringBuilder builder, IReadOnlyList<string> values, double[] widths,
        double left, double top, double height, bool bold)
    {
        var x = left;
        for (var column = 0; column < values.Count; column++)
        {
            builder.Append(bold ? "0.37 0.55 0.68 RG 0.5 w " : "0.78 0.84 0.88 RG 0.45 w ");
            builder.Append(FormattableString.Invariant($"{x:0.##} {top - height:0.##} {widths[column]:0.##} {height:0.##} re S\n"));
            var maximum = Math.Max(4, (int)((widths[column] - 6) / (bold ? 3.05 : 3.25)));
            var lines = WrapPdfText(values[column], maximum).Take(bold ? 4 : 6).ToArray();
            const double lineHeight = 7.5;
            var blockHeight = lines.Length * lineHeight;
            var startY = top - (height - blockHeight) / 2 - lineHeight + 1.2;
            for (var index = 0; index < lines.Length; index++)
            {
                if (bold)
                    CenteredTextColor(builder, x + widths[column] / 2, startY - index * lineHeight,
                        5.7, lines[index], maximum, "1 1 1", true);
                else
                    Text(builder, x + 3, startY - index * lineHeight, 5.7, lines[index]);
            }
            x += widths[column];
        }
    }

    /// <summary>A line of the item sheet's left column: a heading, a labelled value, or a note.</summary>
    private sealed record SideRow(SideRowKind Kind, string Label, string Value);

    private enum SideRowKind { Heading, Field, Check, Note }

    private const double ItemContentTop = 458, ItemContentBottom = 72;
    private const double SideLeft = 32, SideWidth = 216, CalculationLeft = 262, CalculationWidth = 498;

    /// <summary>
    /// The sheet(s) of one component, duct or fitting: inputs, outputs, quick check and assumptions in the left
    /// column, and the complete engineering worksheet (governing equations, substitutions and solved results) in
    /// two columns on the right. Everything normally fits one sheet; anything that does not continues on a
    /// following sheet, so no value or equation is ever left out.
    /// </summary>
    private static List<string> BuildItemSheets(string subtitle, string total, DuctSystemPdfMetadata? metadata,
        DuctSystemPdfReportItem item, EmbeddedImage? referenceImage, int firstPage, int pageCount)
    {
        var allSections = item.Calculations ?? Array.Empty<DuctSystemPdfCalculationSection>();
        // A title after the first starts a secondary calculation (for example the surface area): it follows the
        // primary calculation's assumptions, after a divider.
        var secondaryStart = allSections.Select((section, index) => (section, index))
            .Where(pair => pair.section.IsTitle && pair.index > 0).Select(pair => pair.index).DefaultIfEmpty(allSections.Count).First();
        var sections = allSections.Take(secondaryStart).ToArray();
        var assumptions = sections.FirstOrDefault(section => section.Heading.Equals("ASSUMPTIONS", StringComparison.OrdinalIgnoreCase));
        // The worksheet ends with its assumptions; the input values are already listed in the left column.
        var blocks = CalculationBlocks(sections
            .Where(section => !section.Heading.Equals("ASSUMPTIONS", StringComparison.OrdinalIgnoreCase)
                              && !section.Heading.Equals("INPUT VALUES", StringComparison.OrdinalIgnoreCase))
            .Concat(assumptions is null ? Array.Empty<DuctSystemPdfCalculationSection>() : new[] { assumptions })
            .Concat(allSections.Skip(secondaryStart)));

        var side = new List<SideRow> { new(SideRowKind.Heading, "INPUTS", string.Empty) };
        side.AddRange(item.Inputs.Select(field => new SideRow(SideRowKind.Field, field.Label, field.Value)));
        side.Add(new SideRow(SideRowKind.Heading, "CALCULATED OUTPUTS", string.Empty));
        side.AddRange(item.Outputs.Select(field => new SideRow(SideRowKind.Field, field.Label, field.Value)));
        if (item.QuickCheck is { Count: > 0 })
        {
            side.Add(new SideRow(SideRowKind.Heading, "QA/QC QUICK CHECK", string.Empty));
            side.AddRange(item.QuickCheck.Select(field => new SideRow(SideRowKind.Check, field.Label, field.Value)));
        }

        // Type sizes: as large as fits on one sheet, within readable limits.
        var calculationTop = ItemContentTop - 16;
        var columnWidth = (CalculationWidth - 12) / 2;
        var capacity = 2 * (calculationTop - ItemContentBottom);
        var calculationFont = 5.9;
        var neededHeight = CalculationBlocksHeight(blocks, columnWidth, calculationFont);
        if (neededHeight > capacity * .94)
            calculationFont = Math.Max(5.0, calculationFont * capacity * .94 / neededHeight);
        // Largest left-column line height (6.4 pt down to 5.0 pt) at which every row fits the sheet.
        var sideLine = 6.4;
        while (sideLine > 5.0 && SideRowsHeight(side, sideLine) > ItemContentTop - ItemContentBottom - 4)
            sideLine = Math.Round(sideLine - 0.1, 2);

        var sheets = new List<string>();
        var nextBlock = 0;
        var nextSide = 0;
        do
        {
            var continued = sheets.Count > 0;
            var builder = new StringBuilder();
            DrawSheetChrome(builder, metadata, continued ? $"{item.Id} - {item.ItemType} (continued)" : $"{item.Id} - {item.ItemType}",
                firstPage + sheets.Count, pageCount);
            builder.Append("0.12 0.30 0.47 rg ");
            RoundedRectangle(builder, 32, 486, 728, 36, 6, fillAndStroke: false);
            Text(builder, 46, 499, 12, item.Id, bold: true, white: true);
            var descriptionLines = WrapPdfText(item.Description, 88).Take(2).ToArray();
            for (var index = 0; index < descriptionLines.Length; index++)
                Text(builder, 126, 503 - index * 10, 8.3, descriptionLines[index], bold: true, white: true);
            if (referenceImage is not null && !continued)
                DrawBandReferenceImage(builder, referenceImage);
            Text(builder, 32, 470, 7.2, continued ? $"{subtitle} - continued from the previous sheet." : subtitle);

            // Worksheet first (right), then the left column.
            DrawCalculationReviewHeading(builder, CalculationLeft, ItemContentTop - 4);
            if (nextBlock < blocks.Count)
                nextBlock = DrawCalculationBlocks(builder, blocks, nextBlock, CalculationLeft, calculationTop,
                    CalculationWidth, ItemContentBottom, 2, calculationFont);
            else
                TextColor(builder, CalculationLeft, calculationTop - 10, 6.4,
                    blocks.Count == 0 ? "No calculation worksheet applies to this item; its values are entered or taken from the system."
                                      : "Worksheet complete on the previous sheet.", "0.36 0.42 0.47");
            builder.Append("0.80 0.86 0.90 RG 0.5 w ");
            builder.Append(FormattableString.Invariant(
                $"{CalculationLeft - 7:0.##} {ItemContentBottom:0.##} m {CalculationLeft - 7:0.##} {ItemContentTop + 2:0.##} l S\n"));
            nextSide = DrawSideRows(builder, side, nextSide, sideLine, out var sideEnd);
            // The published fitting figure, as large as the space left under the left column allows.
            if (referenceImage is not null && !continued && nextSide >= side.Count && sideEnd - ItemContentBottom >= 70)
            {
                var room = sideEnd - ItemContentBottom - 18;
                var imageScale = Math.Min((SideWidth - 4) / referenceImage.Width, room / referenceImage.Height);
                var imageWidth = referenceImage.Width * imageScale;
                var imageHeight = referenceImage.Height * imageScale;
                TextColor(builder, SideLeft, sideEnd - 10, 7.2, "PUBLISHED FITTING REFERENCE", "0.16 0.43 0.58", bold: true);
                var imageX = SideLeft + (SideWidth - imageWidth) / 2;
                var imageY = sideEnd - 16 - imageHeight;
                builder.Append("1 1 1 rg 0.65 0.76 0.84 RG 0.45 w ");
                builder.Append(FormattableString.Invariant($"{imageX - 2:0.##} {imageY - 2:0.##} {imageWidth + 4:0.##} {imageHeight + 4:0.##} re B\n"));
                builder.Append(FormattableString.Invariant(
                    $"q {imageWidth:0.##} 0 0 {imageHeight:0.##} {imageX:0.##} {imageY:0.##} cm /{referenceImage.Name} Do Q\n"));
            }
            TextColor(builder, 32, 64, 6.2, Fit(total, 186), "0.36 0.42 0.47");
            sheets.Add(builder.ToString());
        }
        while ((nextBlock < blocks.Count || nextSide < side.Count) && sheets.Count < 20);
        return sheets;
    }

    private static void DrawBandReferenceImage(StringBuilder builder, EmbeddedImage referenceImage)
    {
        const double imageWidthLimit = 116, imageHeightLimit = 26;
        var imageScale = Math.Min(imageWidthLimit / referenceImage.Width, imageHeightLimit / referenceImage.Height);
        var imageWidth = referenceImage.Width * imageScale;
        var imageHeight = referenceImage.Height * imageScale;
        var imageX = 760 - imageWidth - 5;
        var imageY = 491 + (26 - imageHeight) / 2;
        builder.Append("1 1 1 rg 0.65 0.76 0.84 RG 0.45 w ");
        builder.Append(FormattableString.Invariant($"{imageX - 2:0.##} {imageY - 2:0.##} {imageWidth + 4:0.##} {imageHeight + 4:0.##} re B\n"));
        builder.Append(FormattableString.Invariant(
            $"q {imageWidth:0.##} 0 0 {imageHeight:0.##} {imageX:0.##} {imageY:0.##} cm /{referenceImage.Name} Do Q\n"));
    }

    private const double SideLabelShare = .45;

    private static (string[] Label, string[] Value) SideRowLines(SideRow row)
    {
        if (row.Kind == SideRowKind.Note)
            return (Array.Empty<string>(), WrapPdfText(row.Value, 62).ToArray());
        var labelWidth = SideWidth * SideLabelShare - 8;
        var valueWidth = SideWidth * (1 - SideLabelShare) - 6;
        return (WrapPdfText(row.Label.ToUpperInvariant(), Math.Max(8, (int)(labelWidth / 2.9))).Take(2).ToArray(),
            WrapPdfText(row.Value, Math.Max(8, (int)(valueWidth / (row.Kind == SideRowKind.Check ? 3.35 : 3.05)))).ToArray());
    }

    private static double SideRowHeight(SideRow row, double lineHeight)
    {
        if (row.Kind == SideRowKind.Heading)
            return 14;
        var (label, value) = SideRowLines(row);
        return 3 + Math.Max(1, Math.Max(label.Length, value.Length)) * lineHeight;
    }

    private static double SideRowsHeight(IEnumerable<SideRow> rows, double lineHeight) =>
        rows.Sum(row => SideRowHeight(row, lineHeight));

    /// <summary>Draws the left column from <paramref name="start"/>; returns the first row that did not fit.</summary>
    private static int DrawSideRows(StringBuilder builder, IReadOnlyList<SideRow> rows, int start, double lineHeight,
        out double endY)
    {
        var y = ItemContentTop;
        var index = start;
        var labelX = SideLeft + 4;
        var valueX = SideLeft + SideWidth * SideLabelShare;
        var shade = false;
        while (index < rows.Count)
        {
            var row = rows[index];
            var height = SideRowHeight(row, lineHeight);
            var keepWithNext = row.Kind == SideRowKind.Heading && index + 1 < rows.Count
                ? SideRowHeight(rows[index + 1], lineHeight) : 0;
            if (y - height - keepWithNext < ItemContentBottom && y < ItemContentTop)
                break;
            switch (row.Kind)
            {
                case SideRowKind.Heading:
                    TextColor(builder, SideLeft, y - 10, 7.2, row.Label, "0.16 0.43 0.58", bold: true);
                    builder.Append("0.16 0.43 0.58 RG 0.6 w ");
                    builder.Append(FormattableString.Invariant($"{SideLeft:0.##} {y - 13:0.##} m {SideLeft + SideWidth:0.##} {y - 13:0.##} l S\n"));
                    shade = false;
                    break;
                case SideRowKind.Note:
                {
                    var (_, value) = SideRowLines(row);
                    for (var line = 0; line < value.Length; line++)
                        Text(builder, SideLeft + 2, y - lineHeight - line * lineHeight + 1, Math.Min(5.8, lineHeight - .5), value[line]);
                    break;
                }
                default:
                {
                    var (label, value) = SideRowLines(row);
                    if (shade || row.Kind == SideRowKind.Check)
                    {
                        builder.Append(row.Kind == SideRowKind.Check ? "0.93 0.965 0.985 rg " : "0.965 0.978 0.988 rg ");
                        builder.Append(FormattableString.Invariant($"{SideLeft:0.##} {y - height:0.##} {SideWidth:0.##} {height:0.##} re f\n"));
                    }
                    shade = !shade;
                    var labelSize = Math.Clamp(lineHeight - 1.4, 4.6, 5.1);
                    var valueSize = Math.Clamp(lineHeight - .8, 5.0, 5.7);
                    for (var line = 0; line < label.Length; line++)
                        TextColor(builder, labelX, y - lineHeight - line * lineHeight + 1.2, labelSize, label[line], "0.29 0.42 0.51", bold: true);
                    for (var line = 0; line < value.Length; line++)
                    {
                        if (row.Kind == SideRowKind.Check)
                            TextColor(builder, valueX, y - lineHeight - line * lineHeight + 1.2, valueSize + .3, value[line], "0.06 0.30 0.45", bold: true);
                        else
                            Text(builder, valueX, y - lineHeight - line * lineHeight + 1.2, valueSize, value[line]);
                    }
                    break;
                }
            }
            y -= height;
            index++;
        }
        endY = y;
        return index;
    }

    private static double DrawQuickCheckOverview(StringBuilder builder,
        IReadOnlyList<DuctSystemPdfField> fields, double left, double top, double width)
    {
        var visible = fields.Take(4).ToArray();
        if (visible.Length == 0)
            return top;

        const double boxHeight = 24;
        var boxTop = top;
        var boxBottom = boxTop - boxHeight;
        builder.Append("0.95 0.975 0.99 rg ");
        builder.Append(FormattableString.Invariant($"{left:0.##} {boxBottom:0.##} {width:0.##} {boxHeight:0.##} re f\n"));
        builder.Append("0.65 0.76 0.84 RG 0.45 w ");
        builder.Append(FormattableString.Invariant($"{left:0.##} {boxTop:0.##} m {left + width:0.##} {boxTop:0.##} l S\n"));
        TextColor(builder, left + 7, boxTop - 15, 6.6, "QA/QC QUICK CHECK", "0.16 0.43 0.58", bold: true);

        const double headingWidth = 126;
        var fieldsLeft = left + headingWidth;
        var fieldsWidth = width - headingWidth;
        var widths = visible.Length == 3
            ? new[] { fieldsWidth * .50, fieldsWidth * .25, fieldsWidth * .25 }
            : Enumerable.Repeat(fieldsWidth / visible.Length, visible.Length).ToArray();
        var x = fieldsLeft;
        for (var index = 0; index < visible.Length; index++)
        {
            if (index >= 0)
            {
                builder.Append("0.72 0.80 0.87 RG 0.45 w ");
                builder.Append(FormattableString.Invariant($"{x:0.##} {boxBottom + 3:0.##} m {x:0.##} {boxTop - 3:0.##} l S\n"));
            }

            var field = visible[index];
            var columnWidth = widths[index];
            FittedText(builder, x + 7, boxTop - 8, columnWidth - 14, 4.9, 4.3,
                field.Label.ToUpperInvariant(), bold: true, color: "0.29 0.42 0.51");
            FittedText(builder, x + 7, boxTop - 18, columnWidth - 14, 6.9, 5.0,
                field.Value, bold: true, color: "0.06 0.30 0.45");
            x += columnWidth;
        }

        return boxBottom - 9;
    }

    private static void DrawCalculationReviewHeading(StringBuilder builder, double left, double top)
    {
        TextColor(builder, left, top, 7.4, "ENGINEERING CALCULATION REVIEW", "0.16 0.43 0.58", bold: true);
        TextColor(builder, left + 184, top, 6.3, "INPUT / SUBSTITUTION", "0.74 0.17 0.17", bold: true);
        TextColor(builder, left + 294, top, 6.3, "SOLVED RESULT", "0.00 0.41 0.60", bold: true);
    }

    private static IReadOnlyList<int> CompactFieldRowLineCounts(IReadOnlyList<DuctSystemPdfField> fields,
        double width)
    {
        var counts = new List<int>();
        var maximum = Math.Max(18, (int)((width / 2 - 12) / 3.2));
        for (var index = 0; index < fields.Count; index += 2)
        {
            var first = WrapPdfText(fields[index].Value, maximum).Count();
            var second = index + 1 < fields.Count ? WrapPdfText(fields[index + 1].Value, maximum).Count() : 0;
            counts.Add(Math.Max(1, Math.Max(first, second)));
        }
        return counts;
    }

    private static double DrawCompactFieldSection(StringBuilder builder, string heading,
        IReadOnlyList<DuctSystemPdfField> fields, double left, double top, double width, double lineHeight)
    {
        TextColor(builder, left, top, 7.4, heading, "0.16 0.43 0.58", bold: true);
        top -= 13;
        var columnWidth = width / 2;
        var maximum = Math.Max(18, (int)((columnWidth - 12) / 3.2));
        for (var index = 0; index < fields.Count; index += 2)
        {
            var first = fields[index];
            var second = index + 1 < fields.Count ? fields[index + 1] : null;
            var firstLines = WrapPdfText(first.Value, maximum).ToArray();
            var secondLines = second is null ? Array.Empty<string>() : WrapPdfText(second.Value, maximum).ToArray();
            var lineCount = Math.Max(1, Math.Max(firstLines.Length, secondLines.Length));
            var rowHeight = 8 + lineCount * lineHeight;
            builder.Append("0.79 0.85 0.89 RG 0.4 w ");
            builder.Append(FormattableString.Invariant($"{left} {top - rowHeight:0.##} {width} {rowHeight:0.##} re S\n"));
            DrawCompactField(builder, first, left + 5, top - 6, firstLines, lineHeight, maximum);
            if (second is not null)
                DrawCompactField(builder, second, left + columnWidth + 5, top - 6, secondLines, lineHeight, maximum);
            top -= rowHeight;
        }
        return top;
    }

    private static void DrawCompactField(StringBuilder builder, DuctSystemPdfField field, double x, double y,
        IReadOnlyList<string> valueLines, double lineHeight, int maximum)
    {
        var labelFont = Math.Clamp(lineHeight - .7, 4.8, 5.3);
        var valueFont = Math.Clamp(lineHeight - .4, 5.0, 5.6);
        TextColor(builder, x, y, labelFont, Fit(field.Label.ToUpperInvariant(), maximum), "0.29 0.42 0.51", bold: true);
        for (var index = 0; index < valueLines.Count; index++)
            Text(builder, x, y - lineHeight - index * lineHeight, valueFont, valueLines[index]);
    }

    /// <summary>
    /// One piece of a calculation worksheet: a section heading, one line, or two governing equations side by
    /// side (equations listed without substitution or result, such as the required-equation summary).
    /// </summary>
    private sealed record CalculationBlock(bool Heading, string Text, DuctSystemPdfCalculationLine? Line,
        DuctSystemPdfCalculationLine? Partner = null, bool Title = false, bool Divider = false);

    private static List<CalculationBlock> CalculationBlocks(IEnumerable<DuctSystemPdfCalculationSection> sections)
    {
        static bool EquationOnly(DuctSystemPdfCalculationLine line) => line.IsEquation
            && string.IsNullOrWhiteSpace(line.InputSubstitution) && string.IsNullOrWhiteSpace(line.HighlightValue);

        var blocks = new List<CalculationBlock>();
        foreach (var section in sections)
        {
            if (section.IsTitle)
            {
                blocks.Add(new CalculationBlock(true, PdfText(section.Heading), null, Title: true, Divider: blocks.Count > 0));
                continue;
            }
            blocks.Add(new CalculationBlock(true, PdfText(section.Heading), null));
            for (var index = 0; index < section.Lines.Count; index++)
            {
                var line = section.Lines[index];
                if (EquationOnly(line) && index + 1 < section.Lines.Count && EquationOnly(section.Lines[index + 1]))
                {
                    blocks.Add(new CalculationBlock(false, string.Empty, line, section.Lines[index + 1]));
                    index++;
                    continue;
                }
                blocks.Add(new CalculationBlock(false, string.Empty, line));
            }
        }
        return blocks;
    }

    private const double CalculationHeadingGap = 3;

    private static double CalculationBlockHeight(CalculationBlock block, double columnWidth, double fontSize)
    {
        if (block.Title)
            return Math.Max(12.5, fontSize * 2.1) + (block.Divider ? 7 : 0);
        if (block.Heading)
            return Math.Max(11.25, fontSize * 1.82);
        var height = CalculationBlockHeight(false, block.Line, string.Empty, columnWidth, fontSize);
        return block.Partner is null
            ? height
            : Math.Max(height, CalculationBlockHeight(false, block.Partner, string.Empty, columnWidth, fontSize));
    }

    /// <summary>Total height of the blocks, as laid out in one column (used to choose the type size).</summary>
    private static double CalculationBlocksHeight(IReadOnlyList<CalculationBlock> blocks, double columnWidth, double fontSize) =>
        blocks.Sum(block => CalculationBlockHeight(block, columnWidth, fontSize) + (block.Heading ? CalculationHeadingGap : 0));

    /// <summary>
    /// Draws worksheet blocks from <paramref name="start"/> down the given columns and returns the index of the
    /// first block that did not fit (blocks.Count when all were drawn). Nothing is ever cut off: the caller
    /// continues the remaining blocks on another sheet. A heading is never left alone at the foot of a column.
    /// </summary>
    private static int DrawCalculationBlocks(StringBuilder builder, IReadOnlyList<CalculationBlock> blocks, int start,
        double left, double top, double width, double bottom, int columns, double fontSize)
    {
        const double columnGap = 12;
        var columnWidth = (width - columnGap * (columns - 1)) / columns;
        var column = 0;
        var y = top;
        var index = start;
        while (index < blocks.Count)
        {
            var block = blocks[index];
            var gap = block.Heading && y < top ? CalculationHeadingGap : 0;
            var blockHeight = CalculationBlockHeight(block, columnWidth, fontSize);
            var needed = gap + blockHeight;
            if (block.Heading && index + 1 < blocks.Count)
                needed += CalculationBlockHeight(blocks[index + 1], columnWidth, fontSize);
            if (y - needed < bottom)
            {
                if (y == top)
                {
                    // Taller than a whole column: draw it anyway rather than lose it.
                }
                else
                {
                    column++;
                    y = top;
                    if (column >= columns)
                        return index;
                    continue;
                }
            }
            y -= gap;
            var x = left + column * (columnWidth + columnGap);
            if (block.Title)
            {
                var titleTop = y;
                if (block.Divider)
                {
                    builder.Append(FormattableString.Invariant(
                        $"0.10 0.29 0.43 RG 0.9 w {x:0.##} {y - 3:0.##} m {x + columnWidth:0.##} {y - 3:0.##} l S\n"));
                    titleTop -= 7;
                }
                TextColor(builder, x, titleTop - Math.Max(7.2, fontSize * 1.3) - 1.5, Math.Max(7.2, fontSize * 1.3),
                    Fit(block.Text, Math.Max(22, (int)(columnWidth / 4.2))), "0.06 0.20 0.31", bold: true);
                builder.Append(FormattableString.Invariant(
                    $"0.12 0.66 0.72 rg {x:0.##} {titleTop - blockHeight + (block.Divider ? 7 : 0) + 1:0.##} 34 1.4 re f\n"));
            }
            else if (block.Heading)
            {
                builder.Append("0.91 0.95 0.98 rg ");
                RoundedRectangle(builder, x, y - blockHeight + 1, columnWidth, blockHeight - 1, 2, fillAndStroke: false);
                TextColor(builder, x + 5, y - blockHeight + fontSize + 2, Math.Max(5.2, fontSize),
                    Fit(block.Text, Math.Max(22, (int)(columnWidth / 3.2))), "0.10 0.29 0.43", bold: true);
            }
            else if (block.Partner is not null)
            {
                var half = columnWidth / 2;
                DrawCalculationLine(builder, block.Line!, x, y, half, fontSize, equationOnly: true);
                DrawCalculationLine(builder, block.Partner, x + half, y, half, fontSize, equationOnly: true);
            }
            else
            {
                DrawCalculationLine(builder, block.Line!, x, y, columnWidth, fontSize, equationOnly: false);
            }
            y -= blockHeight;
            index++;
        }
        return index;
    }

    private static void DrawCalculationLine(StringBuilder builder, DuctSystemPdfCalculationLine line, double x, double y,
        double columnWidth, double fontSize, bool equationOnly)
    {
        const double valueShare = .29;
        var valueWidth = columnWidth * valueShare;
        var formulaWidth = equationOnly ? columnWidth - 6 : columnWidth - valueWidth - 12;
        var valueX = x + columnWidth - valueWidth;
        if (line.IsEquation)
        {
            var fraction = line.Formula.Contains("\\frac", StringComparison.Ordinal);
            var root = line.Formula.Contains("\\sqrt", StringComparison.Ordinal);
            var formulaBaseline = y - (fraction ? fontSize * 1.95 : root ? fontSize * 1.35 : fontSize * 1.18);
            DrawMathEquation(builder, x + 5, formulaBaseline, fontSize, line.Formula, formulaWidth - 7);
            if (equationOnly)
                return;
            var substitutionCount = 0;
            if (!string.IsNullOrWhiteSpace(line.InputSubstitution))
            {
                var substitutionLines = WrapPdfText(line.InputSubstitution,
                    Math.Max(14, (int)((valueWidth - 6) / (fontSize * .48)))).Take(3).ToArray();
                substitutionCount = substitutionLines.Length;
                for (var index = 0; index < substitutionLines.Length; index++)
                    TextColor(builder, valueX, y - fontSize - index * (fontSize * 1.22), fontSize,
                        substitutionLines[index], "0.74 0.17 0.17", bold: true);
            }
            if (!string.IsNullOrWhiteSpace(line.HighlightValue))
            {
                var color = line.HighlightAsInput ? "0.74 0.17 0.17" : "0.00 0.41 0.60";
                DrawCalculationResult(builder, line, valueX,
                    y - fontSize - substitutionCount * (fontSize * 1.22), valueWidth - 6,
                    fontSize, color, maximumLines: 4);
            }
            return;
        }
        var formulaLines = WrapPdfText(line.Formula,
            Math.Max(22, (int)(formulaWidth / (fontSize * .48)))).ToArray();
        for (var index = 0; index < formulaLines.Length; index++)
            Text(builder, x + 5, y - fontSize - index * (fontSize * 1.22), fontSize, formulaLines[index]);
        if (!string.IsNullOrWhiteSpace(line.HighlightValue))
            DrawCalculationResult(builder, line, valueX, y - fontSize, valueWidth - 5, fontSize,
                line.HighlightAsInput ? "0.74 0.17 0.17" : "0.00 0.41 0.60");
    }

    private static double CalculationBlockHeight(bool heading, DuctSystemPdfCalculationLine? line,
        string headingText, double width, double fontSize)
    {
        if (heading)
            return Math.Max(11.25, fontSize * 1.82);
        if (line is null)
            return fontSize * 1.35;
        var valueWidth = width * .29;
        var formulaWidth = width - valueWidth - 12;
        var formulaLines = line.IsEquation ? 1 : WrapPdfText(line.Formula,
            Math.Max(22, (int)(formulaWidth / (fontSize * .48)))).Count();
        var valueLines = string.IsNullOrWhiteSpace(line.HighlightValue) ? 0
            : CalculationResultLineCount(line, valueWidth - 5, fontSize);
        var substitutionLines = string.IsNullOrWhiteSpace(line.InputSubstitution) ? 0 : WrapPdfText(line.InputSubstitution,
            Math.Max(14, (int)((valueWidth - 5) / (fontSize * .48)))).Count();
        var ordinaryHeight = Math.Max(formulaLines, valueLines + substitutionLines) * fontSize * 1.22 + 3;
        if (!line.IsEquation)
            return Math.Max(fontSize * 1.55, ordinaryHeight);
        var hasFraction = line.Formula.Contains("\\frac", StringComparison.Ordinal);
        var hasRoot = line.Formula.Contains("\\sqrt", StringComparison.Ordinal);
        var mathHeight = hasFraction
            ? fontSize * (hasRoot ? 3.85 : 3.55)
            : hasRoot ? fontSize * 2.2 : fontSize * 1.75;
        return Math.Max(mathHeight, ordinaryHeight) + 2;
    }

    private static string CalculationResultText(DuctSystemPdfCalculationLine line)
    {
        if (line.HighlightAsInput || string.IsNullOrWhiteSpace(line.HighlightValue))
            return line.HighlightValue;
        var descriptor = CalculationResultDescriptorFor(line.Formula);
        if (descriptor is not null)
            return $"{descriptor.Description} ({descriptor.PlainVariable}) = {line.HighlightValue}";
        var genericLabel = GenericCalculationResultLabel(line.Formula);
        return string.IsNullOrWhiteSpace(genericLabel)
            ? line.HighlightValue
            : $"{genericLabel}: {line.HighlightValue}";
    }

    private static string GenericCalculationResultLabel(string formula)
    {
        var value = formula.Trim();
        if (value.Length == 0 || value.Contains('|') || value.StartsWith(">", StringComparison.Ordinal)
                              || double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _)
                              || value.Contains('\\') || value.Contains('{') || value.Contains('}'))
            return string.Empty;
        return value.Split('=', 2)[0].Trim().TrimEnd(' ', '=');
    }

    private sealed record CalculationResultDescriptor(string Description, string VariableMarkup,
        string PlainVariable);

    private static CalculationResultDescriptor? CalculationResultDescriptorFor(string formula)
    {
        var value = formula.Trim();
        if (value.Length == 0 || value.Contains('|') || value.StartsWith(">", StringComparison.Ordinal)
                              || double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
            return null;
        var leftSide = value.Split('=', 2)[0].Trim();
        if (leftSide.Contains("\\Delta P", StringComparison.Ordinal) && leftSide.Contains("100 ft", StringComparison.Ordinal))
            return new("Pressure rate", "\\DeltaP/100 ft", "Delta P / 100 ft");
        if (leftSide.Contains("\\Delta P_{VAV}", StringComparison.Ordinal))
            return new("VAV pressure drop", "\\DeltaP_{VAV}", "Delta P_VAV");
        if (leftSide.Contains("\\Delta P_{t}", StringComparison.Ordinal))
            return new("Fitting pressure drop", "\\DeltaP_{t}", "Delta P_t");
        if (leftSide.Contains("\\Delta P", StringComparison.Ordinal))
            return new("Pressure drop", "\\DeltaP", "Delta P");
        if (leftSide.Contains("V_{actual}", StringComparison.Ordinal))
            return new("Actual velocity", "V_{actual}", "V_actual");
        if (leftSide.Contains("V_{calc}", StringComparison.Ordinal))
            return new("Calculation velocity", "V_{calc}", "V_calc");
        if (leftSide.Contains("V_{ref}", StringComparison.Ordinal))
            return new("Reference velocity", "V_{ref}", "V_ref");
        if (leftSide.Contains("A_{actual}", StringComparison.Ordinal))
            return new("Actual duct area", "A_{actual}", "A_actual");
        if (leftSide.Contains("A_{calc}", StringComparison.Ordinal))
            return new("Calculation area", "A_{calc}", "A_calc");
        if (leftSide.Contains("A_{ref}", StringComparison.Ordinal) || leftSide.Equals("A", StringComparison.Ordinal))
            return leftSide.Contains("ref", StringComparison.OrdinalIgnoreCase)
                ? new("Reference area", "A_{ref}", "A_ref")
                : new("Duct area", "A", "A");
        if (leftSide.Contains("D_{h}", StringComparison.Ordinal))
            return new("Hydraulic diameter", "D_{h}", "D_h");
        if (leftSide.Contains("D_{e}", StringComparison.Ordinal))
            return new("Equivalent diameter", "D_{e}", "D_e");
        if (leftSide.Contains("Colebrook", StringComparison.OrdinalIgnoreCase)
            || leftSide.Equals("f", StringComparison.Ordinal))
            return new("Darcy friction factor", "f", "f");
        if (leftSide.Contains("\\epsilon_{r}", StringComparison.Ordinal))
            return new("Relative roughness", "\\epsilon_{r}", "epsilon_r");
        if (leftSide.Equals("Re", StringComparison.Ordinal))
            return new("Reynolds number", "Re", "Re");
        if (leftSide.StartsWith("\\rho", StringComparison.Ordinal))
            return new("Air density", "\\rho", "rho");
        if (leftSide.StartsWith("\\mu", StringComparison.Ordinal))
            return new("Dynamic viscosity", "\\mu", "mu");
        if (leftSide.StartsWith("VP", StringComparison.Ordinal))
            return new("Velocity pressure", "VP", "VP");
        if (leftSide.Equals("C", StringComparison.Ordinal))
            return new("Loss coefficient", "C", "C");
        if (leftSide.StartsWith("\\frac{W}{H}", StringComparison.Ordinal))
            return new("Width/height ratio", "W/H", "W/H");
        return null;
    }

    private static int CalculationResultLineCount(DuctSystemPdfCalculationLine line,
        double maximumWidth, double fontSize)
    {
        if (line.HighlightAsInput || CalculationResultDescriptorFor(line.Formula) is null)
            return WrapPdfText(CalculationResultText(line),
                Math.Max(14, (int)(maximumWidth / (fontSize * .48)))).Count();
        return SolvedResultFitsOneLine(line, maximumWidth, fontSize) ? 1 : 2;
    }

    private static bool SolvedResultFitsOneLine(DuctSystemPdfCalculationLine line,
        double maximumWidth, double fontSize)
    {
        var descriptor = CalculationResultDescriptorFor(line.Formula);
        if (descriptor is null) return true;
        var prefix = $"{descriptor.Description} ";
        var variableExpression = $"({descriptor.VariableMarkup}) = ";
        return EstimatedPdfWidth(PdfText(prefix), fontSize, bold: true)
               + MeasureMathMarkup(variableExpression, fontSize)
               + EstimatedPdfWidth(PdfText(line.HighlightValue), fontSize, bold: true) <= maximumWidth;
    }

    private static int DrawCalculationResult(StringBuilder builder, DuctSystemPdfCalculationLine line,
        double x, double y, double maximumWidth, double fontSize, string rgb, int maximumLines = int.MaxValue)
    {
        var descriptor = line.HighlightAsInput ? null : CalculationResultDescriptorFor(line.Formula);
        if (descriptor is null)
        {
            var lines = WrapPdfText(CalculationResultText(line),
                Math.Max(14, (int)(maximumWidth / (fontSize * .48)))).Take(maximumLines).ToArray();
            for (var index = 0; index < lines.Length; index++)
                TextColor(builder, x, y - index * (fontSize * 1.22), fontSize,
                    lines[index], rgb, bold: true);
            return lines.Length;
        }

        var prefix = $"{descriptor.Description} ";
        var variableExpression = $"({descriptor.VariableMarkup}) = ";
        var variableWidth = MeasureMathMarkup(variableExpression, fontSize);
        var prefixWidth = EstimatedPdfWidth(PdfText(prefix), fontSize, bold: true);
        var oneLine = SolvedResultFitsOneLine(line, maximumWidth, fontSize);
        var tightenedPrefixWidth = Math.Max(0, prefixWidth - fontSize * .20);
        TextColor(builder, x, y, fontSize, prefix, rgb, bold: true);
        DrawMathMarkup(builder, x + tightenedPrefixWidth, y, fontSize, variableExpression, rgb);
        if (oneLine)
            TextColor(builder, x + tightenedPrefixWidth + variableWidth, y, fontSize,
                line.HighlightValue, rgb, bold: true);
        else if (maximumLines > 1)
        {
            TextColor(builder, x, y - fontSize * 1.22, fontSize, line.HighlightValue,
                rgb, bold: true);
            return 2;
        }
        return 1;
    }

    private abstract record PdfMathElement;
    private sealed record PdfMathText(string Value, bool Symbol = false) : PdfMathElement;
    private sealed record PdfMathScript(IReadOnlyList<PdfMathElement> Value, bool Superscript) : PdfMathElement;
    private sealed record PdfMathFraction(IReadOnlyList<PdfMathElement> Numerator,
        IReadOnlyList<PdfMathElement> Denominator) : PdfMathElement;
    private sealed record PdfMathRoot(IReadOnlyList<PdfMathElement> Radicand) : PdfMathElement;

    private static void DrawMathEquation(StringBuilder builder, double x, double y, double size,
        string markup, double maximumWidth)
    {
        var index = 0;
        var elements = ParseMath(markup, ref index, stopAtBrace: false);
        var width = MeasureMath(elements, size);
        if (width > maximumWidth && width > 0)
            size = Math.Max(3.2, size * maximumWidth / width);
        DrawMath(builder, elements, x, y, size, "0.10 0.17 0.23");
    }

    private static double MeasureMathMarkup(string markup, double size)
    {
        var index = 0;
        return MeasureMath(ParseMath(markup, ref index, stopAtBrace: false), size);
    }

    private static double DrawMathMarkup(StringBuilder builder, double x, double y, double size,
        string markup, string rgb)
    {
        var index = 0;
        return DrawMath(builder, ParseMath(markup, ref index, stopAtBrace: false), x, y, size, rgb);
    }

    private static IReadOnlyList<PdfMathElement> ParseMath(string markup, ref int index, bool stopAtBrace)
    {
        var elements = new List<PdfMathElement>();
        var text = new StringBuilder();
        void Flush()
        {
            if (text.Length == 0) return;
            elements.Add(new PdfMathText(text.ToString()));
            text.Clear();
        }

        while (index < markup.Length)
        {
            var current = markup[index];
            if (current == '}' && stopAtBrace)
            {
                index++;
                break;
            }
            if (current is '_' or '^' && index + 1 < markup.Length && markup[index + 1] == '{')
            {
                Flush();
                var superscript = current == '^';
                index += 2;
                elements.Add(new PdfMathScript(ParseMath(markup, ref index, stopAtBrace: true), superscript));
                continue;
            }
            if (current == '\\')
            {
                Flush();
                var start = ++index;
                while (index < markup.Length && char.IsLetter(markup[index])) index++;
                var command = markup[start..index];
                if (command == "frac" && index < markup.Length && markup[index] == '{')
                {
                    index++;
                    var numerator = ParseMath(markup, ref index, stopAtBrace: true);
                    if (index < markup.Length && markup[index] == '{') index++;
                    var denominator = ParseMath(markup, ref index, stopAtBrace: true);
                    elements.Add(new PdfMathFraction(numerator, denominator));
                    continue;
                }
                if (command == "sqrt" && index < markup.Length && markup[index] == '{')
                {
                    index++;
                    elements.Add(new PdfMathRoot(ParseMath(markup, ref index, stopAtBrace: true)));
                    continue;
                }
                if (command == "DeltaP")
                {
                    elements.Add(new PdfMathText("Delta", Symbol: true));
                    elements.Add(new PdfMathText("P"));
                    continue;
                }
                var greek = command switch
                {
                    "rho" => "rho", "mu" => "mu", "epsilon" => "epsilon", "pi" => "pi",
                    "Delta" => "Delta", "theta" => "theta", _ => string.Empty
                };
                if (greek.Length > 0)
                    elements.Add(new PdfMathText(greek, Symbol: true));
                else
                    elements.Add(new PdfMathText(command switch
                    {
                        "times" => " x ", "cdot" => " . ", _ => command
                    }));
                continue;
            }
            text.Append(current);
            index++;
        }
        Flush();
        return elements;
    }

    private static double MeasureMath(IReadOnlyList<PdfMathElement> elements, double size) =>
        elements.Sum(element => MeasureMath(element, size));

    private static double MeasureMath(PdfMathElement element, double size) => element switch
    {
        PdfMathText value => EstimateMathTextWidth(value.Value, size),
        PdfMathScript value => MeasureMath(value.Value, size * .72),
        PdfMathFraction value => Math.Max(MeasureMath(value.Numerator, size),
            MeasureMath(value.Denominator, size)) + size * .55,
        PdfMathRoot value => size * .92 + MeasureMath(value.Radicand, size),
        _ => 0
    };

    private static double DrawMath(StringBuilder builder, IReadOnlyList<PdfMathElement> elements,
        double x, double y, double size, string rgb)
    {
        foreach (var element in elements)
            x += DrawMath(builder, element, x, y, size, rgb);
        return x;
    }

    private static double DrawMath(StringBuilder builder, PdfMathElement element,
        double x, double y, double size, string rgb)
    {
        switch (element)
        {
            case PdfMathText value:
            {
                if (value.Symbol)
                    return DrawMathSymbol(builder, value.Value, x, y, size, rgb);
                var clean = MathPdfText(value.Value);
                builder.Append($"{rgb} rg ");
                builder.Append(FormattableString.Invariant(
                    $"BT /F3 {size:0.##} Tf {x:0.##} {y:0.##} Td ({EscapePdf(clean)}) Tj ET\n"));
                return EstimateMathTextWidth(clean, size);
            }
            case PdfMathScript script:
            {
                var scriptSize = size * .72;
                var scriptY = y + (script.Superscript ? size * .43 : -size * .28);
                DrawMath(builder, script.Value, x, scriptY, scriptSize, rgb);
                return MeasureMath(script.Value, scriptSize);
            }
            case PdfMathFraction fraction:
            {
                // Numerator and denominator intentionally remain at the equation's full type size.
                var fractionSize = size;
                var numeratorWidth = MeasureMath(fraction.Numerator, fractionSize);
                var denominatorWidth = MeasureMath(fraction.Denominator, fractionSize);
                var width = Math.Max(numeratorWidth, denominatorWidth) + size * .55;
                DrawMath(builder, fraction.Numerator, x + (width - numeratorWidth) / 2,
                    y + size * 1.02, fractionSize, rgb);
                DrawMath(builder, fraction.Denominator, x + (width - denominatorWidth) / 2,
                    y - size * 1.02, fractionSize, rgb);
                builder.Append($"{rgb} RG 0.55 w ");
                builder.Append(FormattableString.Invariant(
                    $"{x + size * .08:0.##} {y + size * .16:0.##} m {x + width - size * .08:0.##} {y + size * .16:0.##} l S\n"));
                return width;
            }
            case PdfMathRoot root:
            {
                var radicalWidth = size * .92;
                var contentWidth = MeasureMath(root.Radicand, size);
                var top = y + size * .78;
                builder.Append($"{rgb} RG {Math.Max(.65, size * .085):0.##} w 1 J 1 j ");
                builder.Append(FormattableString.Invariant(
                    $"{x + size * .03:0.##} {y + size * .02:0.##} m {x + size * .22:0.##} {y - size * .20:0.##} l {x + size * .47:0.##} {top:0.##} l {x + radicalWidth + contentWidth + size * .04:0.##} {top:0.##} l S\n"));
                DrawMath(builder, root.Radicand, x + radicalWidth, y, size, rgb);
                return radicalWidth + contentWidth;
            }
            default:
                return 0;
        }
    }

    private static double DrawMathSymbol(StringBuilder builder, string symbol, double x, double y,
        double size, string rgb)
    {
        static string Point(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
        string X(double factor) => Point(x + size * factor);
        string Y(double factor) => Point(y + size * factor);

        builder.Append($"{rgb} RG {Point(Math.Max(.55, size * .072))} w 1 J 1 j ");
        switch (symbol)
        {
            case "rho":
                builder.Append($"{X(.20)} {Y(-.30)} m {X(.20)} {Y(.36)} l ");
                builder.Append($"{X(.20)} {Y(.70)} {X(.42)} {Y(.91)} {X(.66)} {Y(.78)} c ");
                builder.Append($"{X(.90)} {Y(.65)} {X(.88)} {Y(.21)} {X(.63)} {Y(.06)} c ");
                builder.Append($"{X(.41)} {Y(-.07)} {X(.20)} {Y(.07)} {X(.20)} {Y(.36)} c S\n");
                return size * .94;
            case "mu":
                builder.Append($"{X(.18)} {Y(.72)} m {X(.18)} {Y(-.27)} l ");
                builder.Append($"{X(.18)} {Y(.06)} m {X(.24)} {Y(-.08)} {X(.50)} {Y(-.10)} {X(.62)} {Y(.13)} c ");
                builder.Append($"{X(.62)} {Y(.72)} l {X(.62)} {Y(.13)} m ");
                builder.Append($"{X(.67)} {Y(.01)} {X(.79)} {Y(.02)} {X(.86)} {Y(.15)} c S\n");
                return size * .94;
            case "epsilon":
                builder.Append($"{X(.80)} {Y(.66)} m {X(.65)} {Y(.84)} {X(.30)} {Y(.80)} {X(.20)} {Y(.55)} c ");
                builder.Append($"{X(.12)} {Y(.34)} {X(.29)} {Y(.23)} {X(.60)} {Y(.28)} c ");
                builder.Append($"{X(.29)} {Y(.23)} m {X(.15)} {Y(.13)} {X(.31)} {Y(-.08)} {X(.55)} {Y(-.07)} c ");
                builder.Append($"{X(.68)} {Y(-.06)} {X(.78)} {Y(.00)} {X(.84)} {Y(.10)} c S\n");
                return size * .92;
            case "pi":
                builder.Append($"{X(.10)} {Y(.67)} m {X(.88)} {Y(.67)} l ");
                builder.Append($"{X(.31)} {Y(.67)} m {X(.31)} {Y(-.08)} l ");
                builder.Append($"{X(.70)} {Y(.67)} m {X(.70)} {Y(.08)} l ");
                builder.Append($"{X(.70)} {Y(.08)} {X(.72)} {Y(-.04)} {X(.84)} {Y(-.04)} c S\n");
                return size * .96;
            case "Delta":
                builder.Append($"{X(.50)} {Y(.82)} m {X(.10)} {Y(-.08)} l {X(.90)} {Y(-.08)} l h S\n");
                return size;
            case "theta":
                builder.Append($"{X(.50)} {Y(.84)} m {X(.22)} {Y(.84)} {X(.13)} {Y(.60)} {X(.13)} {Y(.35)} c ");
                builder.Append($"{X(.13)} {Y(.08)} {X(.25)} {Y(-.09)} {X(.50)} {Y(-.09)} c ");
                builder.Append($"{X(.77)} {Y(-.09)} {X(.87)} {Y(.14)} {X(.87)} {Y(.37)} c ");
                builder.Append($"{X(.87)} {Y(.64)} {X(.76)} {Y(.84)} {X(.50)} {Y(.84)} c ");
                builder.Append($"{X(.16)} {Y(.36)} m {X(.84)} {Y(.36)} l S\n");
                return size;
            default:
                return 0;
        }
    }

    private static double EstimateMathTextWidth(string value, double size) =>
        value.Sum(character => character == ' ' ? size * .28 : character is 'i' or 'l' or '1'
            ? size * .28 : character is 'W' or 'M' ? size * .78 : size * .52);

    private static string MathPdfText(string value)
    {
        var replacements = value.Replace("−", "-").Replace("×", "x").Replace("·", ".")
            .Replace("⁄", "/").Replace("–", "-").Replace("—", "-");
        return new string(replacements.Select(character => character is >= ' ' and <= '~' ? character : '?').ToArray());
    }

    private static void DrawSheetChrome(StringBuilder builder, DuctSystemPdfMetadata? metadata,
        string sheetTitle, int page, int pageCount)
    {
        builder.Append("1 1 1 rg 0.10 0.29 0.43 RG 1.2 w 18 18 756 576 re B\n");
        builder.Append("0.035 0.145 0.22 rg 18 538 756 56 re f\n");
        builder.Append("0.055 0.255 0.35 rg 184 538 464 56 re f\n");
        builder.Append("0.12 0.66 0.72 rg 18 538 630 3 re f\n");
        builder.Append("0.08 0.43 0.54 RG 1.05 w ");
        for (var x = 392; x < 644; x += 34)
            builder.Append(FormattableString.Invariant($"{x} 541 m {x + 40} 594 l S\n"));
        DrawDuctSizerMark(builder, 41, 566);
        Text(builder, 63, 560, 18.5, "FanWorks", bold: true, white: true);
        TextColor(builder, 64, 550, 5.8, "HVAC AIRSIDE DESIGN", "0.30 0.86 0.89", bold: true);
        FittedText(builder, 207, 563, 418, 10.5, 7.4, sheetTitle, bold: true, white: true);
        if (metadata is { LogoPixelWidth: > 0, LogoPixelHeight: > 0 }
            && !string.IsNullOrWhiteSpace(metadata.LogoJpegBase64))
        {
            const double boxX = 654, boxY = 547, boxWidth = 108, boxHeight = 40;
            builder.Append(FormattableString.Invariant($"1 1 1 rg {boxX} {boxY} {boxWidth} {boxHeight} re f\n"));
            builder.Append(FormattableString.Invariant($"0.51 0.76 0.81 RG 0.6 w {boxX} {boxY} {boxWidth} {boxHeight} re S\n"));
            var scale = Math.Min(boxWidth / metadata.LogoPixelWidth, boxHeight / metadata.LogoPixelHeight);
            var width = metadata.LogoPixelWidth * scale;
            var height = metadata.LogoPixelHeight * scale;
            builder.Append(FormattableString.Invariant(
                $"q {width:0.##} 0 0 {height:0.##} {boxX + (boxWidth - width) / 2:0.##} {boxY + (boxHeight - height) / 2:0.##} cm /Logo Do Q\n"));
        }
        builder.Append("0.965 0.98 0.99 rg 18 18 756 31 re f\n");
        builder.Append("0.12 0.30 0.47 RG 0.8 w 18 18 756 31 re S 255 18 m 255 49 l S 628 18 m 628 49 l S\n");
        builder.Append("0.12 0.66 0.72 rg 18 47.5 756 1.5 re f\n");
        var printed = string.IsNullOrWhiteSpace(metadata?.PrintedDate)
            ? DateTime.Now.ToString("MMMM d, yyyy") : metadata.PrintedDate;
        FittedText(builder, 29, 30, 212, 7.2, 6.2, $"Printed {printed}");
        var center = $"{(string.IsNullOrWhiteSpace(metadata?.UserName) ? "Designer not entered" : metadata.UserName)}  |  " +
                     $"{(string.IsNullOrWhiteSpace(metadata?.ProjectName) ? "Untitled Project" : metadata.ProjectName)}  |  " +
                     $"{(string.IsNullOrWhiteSpace(metadata?.ProjectNumber) ? "No project number" : metadata.ProjectNumber)}";
        CenteredFittedText(builder, 441, 30, 350, 7.1, 5.8, center);
        FittedText(builder, 648, 30, 102, 7.2, 6.2, $"Page {page} of {pageCount}", bold: true);
    }

    private static void DrawDuctSizerMark(StringBuilder builder, double x, double y)
    {
        // Four swept blades inside a double duct ring match the detailed desktop icon.
        CircleStroke(builder, x, y, 13, "0.22 0.82 0.87");
        CircleStroke(builder, x, y, 10.8, "0.08 0.47 0.67");
        for (var index = 0; index < 4; index++)
        {
            var radians = index * Math.PI / 2;
            var cosine = Math.Cos(radians);
            var sine = Math.Sin(radians);
            builder.Append(FormattableString.Invariant(
                $"q {cosine:0.####} {sine:0.####} {-sine:0.####} {cosine:0.####} {x:0.##} {y:0.##} cm "));
            builder.Append("0.10 0.62 0.82 rg 2.3 1.3 m 4.1 4.2 8.9 5.5 9.8 1.6 c 10.3 -1.2 7.6 -4.2 3.2 -3.1 c 1.7 -2.3 1.3 -0.2 2.3 1.3 c h f Q\n");
        }
        Circle(builder, x, y, 3.5, "0.16 0.78 0.82");
        Circle(builder, x, y, 1.6, "0.04 0.21 0.34");
    }

    private static void FittedText(StringBuilder builder, double x, double y, double maximumWidth,
        double preferredSize, double minimumSize, string value, bool bold = false, bool white = false,
        string? color = null)
    {
        var clean = PdfText(value);
        var widthAtOnePoint = Math.Max(.01, EstimatedPdfWidth(clean, 1, bold));
        var size = Math.Min(preferredSize, maximumWidth / widthAtOnePoint);
        if (size < minimumSize)
        {
            size = minimumSize;
            var characters = Math.Max(4, (int)(maximumWidth / Math.Max(.01, minimumSize * .52)));
            clean = Fit(clean, characters);
        }
        if (!string.IsNullOrWhiteSpace(color))
            TextColor(builder, x, y, size, clean, color, bold);
        else
            Text(builder, x, y, size, clean, bold, white);
    }

    private static void CenteredFittedText(StringBuilder builder, double centerX, double y,
        double maximumWidth, double preferredSize, double minimumSize, string value, bool bold = false)
    {
        var clean = PdfText(value);
        var widthAtOnePoint = Math.Max(.01, EstimatedPdfWidth(clean, 1, bold));
        var size = Math.Min(preferredSize, maximumWidth / widthAtOnePoint);
        if (size < minimumSize)
        {
            size = minimumSize;
            var characters = Math.Max(4, (int)(maximumWidth / Math.Max(.01, minimumSize * .52)));
            clean = Fit(clean, characters);
        }
        Text(builder, centerX - EstimatedPdfWidth(clean, size, bold) / 2, y, size, clean, bold);
    }

    private static double EstimatedPdfWidth(string value, double size, bool bold) =>
        value.Sum(character => character == ' ' ? size * .28 : character is 'i' or 'l' or '1'
            ? size * .28 : character is 'W' or 'M' ? size * .78 : size * (bold ? .55 : .52));

    private static void CenteredTextColor(StringBuilder builder, double centerX, double y, double size,
        string value, int maximum, string rgb, bool bold = false)
    {
        var fitted = Fit(value, maximum);
        var estimatedWidth = fitted.Length * size * (bold ? 0.54 : 0.50);
        TextColor(builder, centerX - estimatedWidth / 2, y, size, fitted, rgb, bold);
    }

    private static string BuildReferencePage(string subtitle, string total, DuctSystemPdfMetadata? metadata,
        DuctSystemPdfReportItem item, string imageName, int pixelWidth, int pixelHeight, int page, int pageCount)
    {
        const double left = 36, width = 720;
        var builder = new StringBuilder();
        Text(builder, left, 570, 18, "Published Fitting Reference", bold: true);
        DrawMetadata(builder, metadata, subtitle, 550);
        Text(builder, 600, 596, 8, $"Page {page} of {pageCount}");
        builder.Append("0.12 0.30 0.47 rg ");
        RoundedRectangle(builder, left, 474, width, 38, 7, fillAndStroke: false);
        Text(builder, left + 16, 487, 14, $"{item.Id} - {item.Description}", bold: true, white: true);

        const double boxX = 56, boxY = 78, boxWidth = 680, boxHeight = 372;
        builder.Append("1 1 1 rg 0.78 0.83 0.87 RG 0.8 w ");
        builder.Append(FormattableString.Invariant($"{boxX} {boxY} {boxWidth} {boxHeight} re B\n"));
        var scale = Math.Min(boxWidth / pixelWidth, boxHeight / pixelHeight);
        var imageWidth = pixelWidth * scale;
        var imageHeight = pixelHeight * scale;
        builder.Append(FormattableString.Invariant(
            $"q {imageWidth:0.##} 0 0 {imageHeight:0.##} {boxX + (boxWidth - imageWidth) / 2:0.##} {boxY + (boxHeight - imageHeight) / 2:0.##} cm /{imageName} Do Q\n"));
        builder.Append("0.12 0.30 0.47 rg ");
        builder.Append(FormattableString.Invariant($"{left} 20 {width} 26 re f\n"));
        Text(builder, left + 10, 29, 9, total, bold: true, white: true);
        Text(builder, 654, 29, 7.5, "FanWorks", white: true);
        return builder.ToString();
    }

    private static int CalculationPageCount(DuctSystemPdfReportItem item)
    {
        var lineCount = (item.Calculations ?? Array.Empty<DuctSystemPdfCalculationSection>())
            .Sum(section => 2 + section.Lines.Sum(line => Math.Max(1, (int)Math.Ceiling(line.Formula.Length / 104d))));
        // Each section contributes one spacer, but the final section does not need a trailing blank line.
        if (lineCount > 0)
            lineCount--;
        return lineCount == 0 ? 0 : (int)Math.Ceiling(lineCount / 31d);
    }

    private static string BuildCalculationPage(string subtitle, string total, DuctSystemPdfMetadata? metadata,
        DuctSystemPdfReportItem item, int detailPage, int detailPageCount, int page, int pageCount)
    {
        const double left = 36, width = 720;
        var builder = new StringBuilder();
        Text(builder, left, 570, 18, "Calculation Detail", bold: true);
        DrawMetadata(builder, metadata, subtitle, 550);
        Text(builder, 600, 596, 8, $"Page {page} of {pageCount}");

        builder.Append("0.12 0.30 0.47 rg ");
        RoundedRectangle(builder, left, 474, width, 38, 7, fillAndStroke: false);
        Text(builder, left + 16, 487, 14, $"{item.Id} - {item.ItemType}", bold: true, white: true);

        var logicalLines = new List<(bool Heading, string Formula, string Highlight)>();
        foreach (var section in item.Calculations ?? Array.Empty<DuctSystemPdfCalculationSection>())
        {
            logicalLines.Add((true, section.Heading, string.Empty));
            foreach (var line in section.Lines)
                foreach (var wrapped in WrapCalculationLine(line))
                    logicalLines.Add((false, wrapped.Formula, wrapped.Highlight));
            logicalLines.Add((false, string.Empty, string.Empty));
        }
        if (logicalLines.Count > 0 && !logicalLines[^1].Heading &&
            string.IsNullOrWhiteSpace(logicalLines[^1].Formula) && string.IsNullOrWhiteSpace(logicalLines[^1].Highlight))
            logicalLines.RemoveAt(logicalLines.Count - 1);

        var pageLines = logicalLines.Skip(detailPage * 31).Take(31).ToArray();
        var y = 448d;
        foreach (var line in pageLines)
        {
            if (line.Heading)
            {
                builder.Append("0.91 0.95 0.98 rg ");
                RoundedRectangle(builder, left, y - 4, width, 16, 3, fillAndStroke: false);
                Text(builder, left + 8, y, 8.3, line.Formula, bold: true);
            }
            else if (!string.IsNullOrWhiteSpace(line.Formula) || !string.IsNullOrWhiteSpace(line.Highlight))
            {
                Text(builder, left + 8, y, 7.7, line.Formula);
                if (!string.IsNullOrWhiteSpace(line.Highlight))
                {
                    var estimate = PdfText(line.Formula).Length * 3.75;
                    TextColor(builder, Math.Min(left + width - 180, left + 8 + estimate), y, 7.7,
                        line.Highlight, "0.74 0.17 0.17", bold: true);
                }
            }
            y -= 12.5;
        }
        Text(builder, left, 50, 7.5, $"Calculation detail {detailPage + 1} of {detailPageCount}");
        builder.Append("0.12 0.30 0.47 rg ");
        builder.Append(FormattableString.Invariant($"{left} 20 {width} 26 re f\n"));
        Text(builder, left + 10, 29, 9, total, bold: true, white: true);
        Text(builder, 654, 29, 7.5, "FanWorks", white: true);
        return builder.ToString();
    }

    private static IEnumerable<(string Formula, string Highlight)> WrapCalculationLine(DuctSystemPdfCalculationLine line)
    {
        var formula = PdfText(line.Formula);
        if (formula.Length <= 104)
            return new[] { (formula, PdfText(line.HighlightValue)) };
        var wrapped = WrapPdfText(formula, 104).ToArray();
        return wrapped.Select((part, index) => (part, index == wrapped.Length - 1 ? PdfText(line.HighlightValue) : string.Empty));
    }

    private static string BuildReportPage(string subtitle, string total, DuctSystemPdfMetadata? metadata,
        DuctSystemPdfReportItem item, int page, int pageCount)
    {
        const double left = 36, width = 720;
        var builder = new StringBuilder();
        Text(builder, left, 570, 18, "System Item Report", bold: true);
        DrawMetadata(builder, metadata, subtitle, 550);
        Text(builder, 600, 596, 8, $"Page {page} of {pageCount}");

        builder.Append("0.12 0.30 0.47 rg ");
        RoundedRectangle(builder, left, 474, width, 42, 7, fillAndStroke: false);
        Text(builder, left + 16, 489, 16, item.Id, bold: true, white: true);
        var typeWidth = Math.Max(1, item.ItemType.Length) * 7.2;
        Text(builder, left + width - typeWidth - 16, 490, 10, item.ItemType, bold: true, white: true);

        Text(builder, left, 451, 8, "DESCRIPTION", bold: true);
        var descriptionLines = WrapPdfText(item.Description, 112).Take(2).ToArray();
        for (var index = 0; index < descriptionLines.Length; index++)
            Text(builder, left + 88, 451 - index * 13, 9.5, descriptionLines[index]);
        builder.Append("0.80 0.85 0.89 RG 0.7 w ");
        builder.Append(FormattableString.Invariant($"{left} 423 m {left + width} 423 l S\n"));

        Text(builder, left, 405, 11, "INPUTS", bold: true);
        var inputBottom = DrawReportFields(builder, item.Inputs, left, 387, width);
        var outputHeadingY = Math.Max(170, inputBottom - 28);
        Text(builder, left, outputHeadingY, 11, "CALCULATED OUTPUTS", bold: true);
        DrawReportFields(builder, item.Outputs, left, outputHeadingY - 18, width);

        builder.Append("0.12 0.30 0.47 rg ");
        builder.Append(FormattableString.Invariant($"{left} 20 {width} 26 re f\n"));
        Text(builder, left + 10, 29, 9, total, bold: true, white: true);
        Text(builder, 654, 29, 7.5, "FanWorks", white: true);
        return builder.ToString();
    }

    private static double DrawReportFields(StringBuilder builder, IReadOnlyList<DuctSystemPdfField> fields,
        double left, double top, double width)
    {
        const double rowHeight = 14;
        var columnWidth = width / 2;
        for (var index = 0; index < fields.Count; index++)
        {
            var row = index / 2;
            var column = index % 2;
            var x = left + column * columnWidth;
            var y = top - row * rowHeight;
            Text(builder, x, y, 6.7, Fit(fields[index].Label, 36), bold: true);
            Text(builder, x + 150, y, 7.5, Fit(fields[index].Value, 38));
            builder.Append("0.90 0.92 0.94 RG 0.35 w ");
            builder.Append(FormattableString.Invariant($"{x} {y - 3:0.##} m {x + columnWidth - 12:0.##} {y - 3:0.##} l S\n"));
        }
        return top - Math.Ceiling(fields.Count / 2.0) * rowHeight;
    }

    private static IEnumerable<string> WrapPdfText(string value, int maximum)
    {
        var clean = PdfText(value);
        if (clean.Length == 0)
            return new[] { "-" };
        var lines = new List<string>();
        while (clean.Length > maximum)
        {
            var split = clean.LastIndexOf(' ', maximum);
            if (split < maximum / 2)
                split = maximum;
            lines.Add(clean[..split].Trim());
            clean = clean[split..].Trim();
        }
        lines.Add(clean);
        return lines;
    }

    private static string BuildPage(string title, string subtitle, IReadOnlyList<string> headers,
        IReadOnlyList<IReadOnlyList<string>> rows, string total, DuctSystemPdfMetadata? metadata,
        int page, int pageCount)
    {
        var widths = TableWidths(headers);
        const double left = 36, top = 510, headerHeight = 46, rowHeight = 21;
        var builder = new StringBuilder();
        Text(builder, left, 577, 18, title, bold: true);
        DrawMetadata(builder, metadata, subtitle);
        Text(builder, 600, 596, 8, $"Page {page} of {pageCount}");
        builder.Append("0.90 0.94 0.97 rg ");
        builder.Append(FormattableString.Invariant($"{left} {top - headerHeight} 720 {headerHeight} re f\n"));
        DrawRow(builder, headers, widths, left, top, headerHeight, bold: true);
        var y = top - headerHeight;
        foreach (var row in rows)
        {
            DrawRow(builder, row, widths, left, y, rowHeight, bold: false);
            y -= rowHeight;
        }
        builder.Append("0.12 0.30 0.47 rg ");
        builder.Append(FormattableString.Invariant($"{left} 42 720 28 re f\n"));
        Text(builder, left + 10, 52, 11, total, bold: true, white: true);
        Text(builder, 610, 52, 8, "Generated by FanWorks", white: true);
        return builder.ToString();
    }

    private static string BuildLayoutPage(string title, string subtitle, string total,
        DuctSystemPdfMetadata metadata, DuctSystemPdfLayout layout, int page, int pageCount)
    {
        const double left = 36, bottom = 86, width = 720, height = 408;
        var builder = new StringBuilder();
        Text(builder, left, 570, 18, "Layout Designer", bold: true);
        DrawMetadata(builder, metadata, subtitle, 550);
        Text(builder, 600, 596, 8, $"Page {page} of {pageCount}");
        builder.Append("0.98 0.99 1 rg 0.72 0.79 0.85 RG 0.8 w ");
        builder.Append(FormattableString.Invariant($"{left} {bottom} {width} {height} re B\n"));
        builder.Append(FormattableString.Invariant($"q {left} {bottom} {width} {height} re W n\n"));

        var minX = layout.Nodes.Min(x => x.GridX / 2.0);
        var maxX = layout.Nodes.Max(x => x.GridX / 2.0);
        var minY = layout.Nodes.Min(x => x.GridY / 4.0);
        var maxY = layout.Nodes.Max(x => x.GridY / 4.0);
        var spanX = Math.Max(1, maxX - minX);
        var spanY = Math.Max(1, maxY - minY);
        var scale = Math.Min(42d, Math.Min((width - 100) / (spanX + 2.2), (height - 130) / (spanY + 3.5)));
        scale = Math.Max(9d, scale);
        var nodeSize = Math.Clamp(scale * 1.25, 28, 46);
        var nodeHalf = nodeSize / 2;
        var usedWidth = spanX * scale;
        var usedHeight = spanY * scale;
        var originX = left + (width - usedWidth) / 2;
        var originY = bottom + (height + usedHeight) / 2 + 10;
        var positions = layout.Nodes.ToDictionary(x => x.Id, x => (
            X: originX + (x.GridX / 2.0 - minX) * scale,
            Y: originY - (x.GridY / 4.0 - minY) * scale), StringComparer.OrdinalIgnoreCase);

        builder.Append("0.36 0.83 0.88 RG 0.4 w ");
        var risingBase = originX - 2 * originY + 2 * bottom;
        var fallingBase = originX + 2 * originY - 2 * (bottom + height);
        for (var index = -80; index <= 80; index++)
        {
            var rising = risingBase + index * scale;
            var falling = fallingBase + index * scale;
            builder.Append(FormattableString.Invariant($"{rising:0.##} {bottom:0.##} m {rising + height * 2:0.##} {bottom + height:0.##} l S\n"));
            builder.Append(FormattableString.Invariant($"{falling:0.##} {bottom + height:0.##} m {falling + height * 2:0.##} {bottom:0.##} l S\n"));
        }
        builder.Append("0.76 0.82 0.88 RG 1 w ");
        builder.Append(FormattableString.Invariant($"{left + 22:0.##} {bottom + height - 34:0.##} m {left + 45:0.##} {bottom + height - 23:0.##} l S\n"));
        builder.Append(FormattableString.Invariant($"{left + 22:0.##} {bottom + height - 34:0.##} m {left:0.##} {bottom + height - 23:0.##} l S\n"));
        builder.Append(FormattableString.Invariant($"{left + 22:0.##} {bottom + height - 34:0.##} m {left + 22:0.##} {bottom + height - 7:0.##} l S\n"));
        Text(builder, left + 48, bottom + height - 25, 6, "X", bold: true);
        Text(builder, left + 4, bottom + height - 25, 6, "Y", bold: true);
        Text(builder, left + 25, bottom + height - 8, 6, "Z", bold: true);

        foreach (var connection in layout.Connections)
        {
            if (!positions.TryGetValue(connection.UpstreamId, out var upstream)
                || !positions.TryGetValue(connection.DownstreamId, out var downstream))
                continue;
            var path = PdfConnectionPath(connection, layout, positions, nodeHalf, scale);
            if (path.Count < 2)
                continue;
            builder.Append(connection.IsSized ? "0.44 0.49 0.51 RG [] 0 d " : "0.80 0.20 0.20 RG [4 3] 0 d ");
            builder.Append("1.8 w 1 J ");
            builder.Append(FormattableString.Invariant($"{path[0].X:0.##} {path[0].Y:0.##} m "));
            foreach (var point in path.Skip(1))
                builder.Append(FormattableString.Invariant($"{point.X:0.##} {point.Y:0.##} l "));
            builder.Append("S\n");
            DrawPdfFlowArrow(builder, path, connection.IsSized ? "0.44 0.49 0.51" : "0.80 0.20 0.20");
            var label = PdfAdjacentLabelPoint(path, 10);
            var labelX = label.X;
            var labelY = label.Y;
            if (string.IsNullOrWhiteSpace(connection.DuctId))
                continue;
            builder.Append("1 1 1 rg ");
            RoundedRectangle(builder, labelX - 23, labelY - 3, 46, 13, 3, fillAndStroke: false);
            CenteredText(builder, labelX, labelY + 1, 6.4,
                connection.DuctId, 12, bold: true);
        }

        foreach (var node in layout.Nodes)
        {
            var point = positions[node.Id];
            DrawPdfPorts(builder, node, point, nodeHalf, layout, positions, scale);
            DrawLayoutIcon(builder, point.X, point.Y, node.ComponentType, node.FittingGroup,
                node.IsDefined, node.AirflowOverride);
            var captionLines = node.Caption.Replace("\r", string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var caption = Fit(captionLines.FirstOrDefault() ?? node.Id, 16);
            var captionX = point.X + nodeHalf + 5;
            var captionY = point.Y + nodeHalf - 3;
            builder.Append("1 1 1 rg ");
            RoundedRectangle(builder, captionX - 2, captionY - 3, caption.Length * 4.1 + 6, 12, 2, fillAndStroke: false);
            Text(builder, captionX, captionY, 6.5, caption, bold: true);
        }

        builder.Append("Q\n");

        builder.Append("0.12 0.30 0.47 rg ");
        builder.Append(FormattableString.Invariant($"{left} 42 720 28 re f\n"));
        Text(builder, left + 10, 52, 11, total, bold: true, white: true);
        Text(builder, 610, 52, 8, "Generated by FanWorks", white: true);
        return builder.ToString();
    }

    private static void DrawMetadata(StringBuilder builder, DuctSystemPdfMetadata? metadata, string subtitle,
        double topY = 559)
    {
        if (metadata is { LogoPixelWidth: > 0, LogoPixelHeight: > 0 }
            && !string.IsNullOrWhiteSpace(metadata.LogoJpegBase64))
        {
            const double boxWidth = 112, boxHeight = 31;
            var scale = Math.Min(boxWidth / metadata.LogoPixelWidth, boxHeight / metadata.LogoPixelHeight);
            var width = metadata.LogoPixelWidth * scale;
            var height = metadata.LogoPixelHeight * scale;
            builder.Append(FormattableString.Invariant(
                $"q {width:0.##} 0 0 {height:0.##} {756 - width:0.##} {topY - 5:0.##} cm /Logo Do Q\n"));
        }
        var project = string.IsNullOrWhiteSpace(metadata?.ProjectName) ? "Project: -" : $"Project: {metadata.ProjectName}";
        var number = string.IsNullOrWhiteSpace(metadata?.ProjectNumber) ? "Project No.: -" : $"Project No.: {metadata.ProjectNumber}";
        var date = string.IsNullOrWhiteSpace(metadata?.ProjectDate) ? "Date: -" : $"Date: {metadata.ProjectDate}";
        var user = string.IsNullOrWhiteSpace(metadata?.UserName) ? "Prepared by: -" : $"Prepared by: {metadata.UserName}";
        Text(builder, 36, topY, 9, Fit(project, 66), bold: true);
        Text(builder, 385, topY, 8.5, Fit(number, 30));
        Text(builder, 36, topY - 17, 8.5, Fit(user, 52));
        Text(builder, 385, topY - 17, 8.5, Fit(date, 34));
        Text(builder, 36, topY - 34, 8.5, Fit(subtitle, 82));
    }

    private static double[] TableWidths(IReadOnlyList<string> headers)
    {
        var weights = headers.Select(header =>
        {
            var lower = header.ToLowerInvariant();
            if (lower.Contains("description")) return 2.1;
            if (lower.Contains("upstream") || lower.Contains("downstream") || lower.Contains("system")) return 1.35;
            if (lower.Contains("pressure") || lower.Contains("static")) return 1.55;
            if (lower.Contains("size") || lower.Contains("component")) return 1.25;
            return 1.0;
        }).ToArray();
        var totalWeight = weights.Sum();
        return weights.Select(x => 720d * x / totalWeight).ToArray();
    }

    private static void DrawPdfPorts(StringBuilder builder, DuctSystemPdfLayoutNode node,
        (double X, double Y) center, double nodeHalf, DuctSystemPdfLayout layout,
        IReadOnlyDictionary<string, (double X, double Y)> positions, double scale)
    {
        for (var index = 0; index < Math.Clamp(node.InletPorts, 0, 2); index++)
        {
            var connection = layout.Connections.FirstOrDefault(x =>
                string.Equals(x.DownstreamId, node.Id, StringComparison.OrdinalIgnoreCase)
                && x.SecondaryInput == (index == 1));
            var point = connection is not null && positions.ContainsKey(connection.UpstreamId)
                ? PdfConnectionPath(connection, layout, positions, nodeHalf, scale).Last()
                : PdfPortPoint(node, center, output: false, index, nodeHalf);
            Circle(builder, point.X, point.Y, 2.8, "0.15 0.45 0.47");
        }
        for (var index = 0; index < Math.Clamp(node.OutletPorts, 0, 2); index++)
        {
            var connection = layout.Connections.FirstOrDefault(x =>
                string.Equals(x.UpstreamId, node.Id, StringComparison.OrdinalIgnoreCase)
                && (node.OutletPorts == 1 || (index == 0
                    ? string.Equals(x.BranchId, node.BranchId, StringComparison.OrdinalIgnoreCase)
                    : !string.Equals(x.BranchId, node.BranchId, StringComparison.OrdinalIgnoreCase))));
            var point = connection is not null && positions.ContainsKey(connection.DownstreamId)
                ? PdfConnectionPath(connection, layout, positions, nodeHalf, scale).First()
                : PdfPortPoint(node, center, output: true, index, nodeHalf);
            Circle(builder, point.X, point.Y, 2.8, "0.15 0.45 0.47");
        }
    }

    private static void DrawPdfFlowArrow(StringBuilder builder, IReadOnlyList<(double X, double Y)> path, string rgb)
    {
        var longest = Enumerable.Range(1, path.Count - 1)
            .Select(index => (Start: path[index - 1], End: path[index], Length: PdfDistance(path[index - 1], path[index])))
            .OrderByDescending(x => x.Length).First();
        var start = longest.Start;
        var end = longest.End;
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var length = Math.Sqrt(dx * dx + dy * dy);
        if (length < 18)
            return;
        var ux = dx / length;
        var uy = dy / length;
        var centerX = start.X + dx * .62;
        var centerY = start.Y + dy * .62;
        var tipX = centerX + ux * 6;
        var tipY = centerY + uy * 6;
        var leftX = centerX - ux * 2 - uy * 3;
        var leftY = centerY - uy * 2 + ux * 3;
        var rightX = centerX - ux * 2 + uy * 3;
        var rightY = centerY - uy * 2 - ux * 3;
        builder.Append(FormattableString.Invariant(
            $"{rgb} rg {tipX:0.##} {tipY:0.##} m {leftX:0.##} {leftY:0.##} l {rightX:0.##} {rightY:0.##} l h f\n"));
    }

    private static (double X, double Y) PdfPortPointToward((double X, double Y) center,
        (double X, double Y) peer, double nodeHalf)
    {
        var dx = peer.X - center.X;
        var dy = peer.Y - center.Y;
        (double X, double Y)[] directions =
            [(2d, 1d), (-2d, 1d), (0d, 1d), (-2d, -1d), (2d, -1d), (0d, -1d)];
        var length = Math.Max(1e-9, Math.Sqrt(dx * dx + dy * dy));
        var direction = directions.OrderByDescending(axis =>
            (dx * axis.X + dy * axis.Y) / (length * Math.Sqrt(axis.X * axis.X + axis.Y * axis.Y))).First();
        var scaleX = Math.Abs(direction.X) < 1e-9 ? double.MaxValue : nodeHalf / Math.Abs(direction.X);
        var scaleY = Math.Abs(direction.Y) < 1e-9 ? double.MaxValue : nodeHalf / Math.Abs(direction.Y);
        var scale = Math.Min(scaleX, scaleY);
        return (center.X + direction.X * scale, center.Y + direction.Y * scale);
    }

    private static (double X, double Y) PdfPortPoint(DuctSystemPdfLayoutNode node,
        (double X, double Y) center, bool output, int index, double nodeHalf)
    {
        var count = Math.Clamp(output ? node.OutletPorts : node.InletPorts, 1, 2);
        index = Math.Clamp(index, 0, count - 1);
        (double X, double Y)[] directions = output
            ? [(2d, 1d), (-2d, 1d), (0d, 1d)]
            : [(-2d, -1d), (2d, -1d), (0d, -1d)];
        var direction = directions[Math.Min(index, directions.Length - 1)];
        return PdfPortPointToward(center, (center.X + direction.X, center.Y + direction.Y), nodeHalf);
    }

    private static IReadOnlyList<(double X, double Y)> PdfConnectionPath(
        DuctSystemPdfLayoutConnection connection, DuctSystemPdfLayout layout,
        IReadOnlyDictionary<string, (double X, double Y)> positions, double nodeHalf, double scale)
    {
        var upstreamNode = layout.Nodes.First(x => string.Equals(x.Id, connection.UpstreamId, StringComparison.OrdinalIgnoreCase));
        var downstreamNode = layout.Nodes.First(x => string.Equals(x.Id, connection.DownstreamId, StringComparison.OrdinalIgnoreCase));
        var startCenter = positions[connection.UpstreamId];
        var endCenter = positions[connection.DownstreamId];
        var deltaColumn = downstreamNode.GridX - upstreamNode.GridX;
        var deltaRow = downstreamNode.GridY - upstreamNode.GridY;
        var centers = new List<(double X, double Y)> { startCenter };
        if (deltaColumn != 0 && deltaColumn != deltaRow && deltaColumn != -deltaRow)
        {
            var axisXSteps = (deltaColumn + deltaRow) / 2;
            var axisYSteps = (deltaRow - deltaColumn) / 2;
            var branchFirst = connection.SecondaryInput
                              || !string.Equals(connection.BranchId, upstreamNode.BranchId, StringComparison.OrdinalIgnoreCase);
            var bendColumn = branchFirst ? upstreamNode.GridX - axisYSteps : upstreamNode.GridX + axisXSteps;
            var bendRow = branchFirst ? upstreamNode.GridY + axisYSteps : upstreamNode.GridY + axisXSteps;
            var bend = (
                X: startCenter.X + (bendColumn - upstreamNode.GridX) / 2.0 * scale,
                Y: startCenter.Y - (bendRow - upstreamNode.GridY) / 4.0 * scale);
            if (PdfDistance(bend, startCenter) > 1e-6 && PdfDistance(bend, endCenter) > 1e-6)
                centers.Add(bend);
        }
        centers.Add(endCenter);
        var path = new List<(double X, double Y)> { PdfPortPointToward(startCenter, centers[1], nodeHalf) };
        path.AddRange(centers.Skip(1).Take(Math.Max(0, centers.Count - 2)));
        path.Add(PdfPortPointToward(endCenter, centers[^2], nodeHalf));
        return path;
    }

    private static double PdfDistance((double X, double Y) first, (double X, double Y) second) =>
        Math.Sqrt(Math.Pow(second.X - first.X, 2) + Math.Pow(second.Y - first.Y, 2));

    private static (double X, double Y) PdfPathMidpoint(IReadOnlyList<(double X, double Y)> path)
    {
        var total = Enumerable.Range(1, path.Count - 1).Sum(index => PdfDistance(path[index - 1], path[index]));
        var target = total / 2.0;
        var traversed = 0.0;
        for (var index = 1; index < path.Count; index++)
        {
            var length = PdfDistance(path[index - 1], path[index]);
            if (traversed + length >= target && length > 0)
            {
                var ratio = (target - traversed) / length;
                return (path[index - 1].X + (path[index].X - path[index - 1].X) * ratio,
                    path[index - 1].Y + (path[index].Y - path[index - 1].Y) * ratio);
            }
            traversed += length;
        }
        return path[^1];
    }

    private static (double X, double Y) PdfAdjacentLabelPoint(IReadOnlyList<(double X, double Y)> path,
        double labelHeight)
    {
        var segment = Enumerable.Range(1, path.Count - 1)
            .Select(index => (Start: path[index - 1], End: path[index], Length: PdfDistance(path[index - 1], path[index])))
            .OrderByDescending(item => item.Length)
            .First();
        if (segment.Length < 1e-6)
            return PdfPathMidpoint(path);
        var dx = segment.End.X - segment.Start.X;
        var dy = segment.End.Y - segment.Start.Y;
        var nx = -dy / segment.Length;
        var ny = dx / segment.Length;
        if (ny < 0 || Math.Abs(ny) < .05 && nx < 0)
        {
            nx *= -1;
            ny *= -1;
        }
        var offset = labelHeight / 2 + 6;
        return ((segment.Start.X + segment.End.X) / 2 + nx * offset,
            (segment.Start.Y + segment.End.Y) / 2 + ny * offset);
    }

    private static void RoundedRectangle(StringBuilder builder, double x, double y, double width, double height,
        double radius, bool fillAndStroke)
    {
        const double k = 0.5522847498;
        radius = Math.Clamp(radius, 0, Math.Min(width, height) / 2);
        var c = radius * k;
        builder.Append(FormattableString.Invariant($"{x + radius:0.##} {y:0.##} m {x + width - radius:0.##} {y:0.##} l "));
        builder.Append(FormattableString.Invariant($"{x + width - radius + c:0.##} {y:0.##} {x + width:0.##} {y + radius - c:0.##} {x + width:0.##} {y + radius:0.##} c "));
        builder.Append(FormattableString.Invariant($"{x + width:0.##} {y + height - radius:0.##} l "));
        builder.Append(FormattableString.Invariant($"{x + width:0.##} {y + height - radius + c:0.##} {x + width - radius + c:0.##} {y + height:0.##} {x + width - radius:0.##} {y + height:0.##} c "));
        builder.Append(FormattableString.Invariant($"{x + radius:0.##} {y + height:0.##} l "));
        builder.Append(FormattableString.Invariant($"{x + radius - c:0.##} {y + height:0.##} {x:0.##} {y + height - radius + c:0.##} {x:0.##} {y + height - radius:0.##} c "));
        builder.Append(FormattableString.Invariant($"{x:0.##} {y + radius:0.##} l "));
        builder.Append(FormattableString.Invariant($"{x:0.##} {y + radius - c:0.##} {x + radius - c:0.##} {y:0.##} {x + radius:0.##} {y:0.##} c h "));
        builder.Append(fillAndStroke ? "B\n" : "f\n");
    }

    private static void CenteredText(StringBuilder builder, double centerX, double y, double size, string value,
        int maximum, bool bold = false)
    {
        var fitted = Fit(value, maximum);
        var estimatedWidth = fitted.Length * size * (bold ? 0.54 : 0.50);
        Text(builder, centerX - estimatedWidth / 2, y, size, fitted, bold);
    }

    private static void DrawLayoutIcon(StringBuilder builder, double centerX, double centerY,
        string componentType, string fittingGroup, bool isDefined, bool airflowOverride)
    {
        var color = airflowOverride ? "0.87 0.49 0.13" : isDefined ? "0.12 0.30 0.47" : "0.77 0.19 0.19";
        var fill = airflowOverride ? "1.00 0.93 0.84" : isDefined ? "0.86 0.93 0.98" : "0.98 0.87 0.87";
        builder.Append($"{color} RG [] 0 d 1.8 w ");
        if (componentType.Equals("Reservoir", StringComparison.OrdinalIgnoreCase))
        {
            builder.Append(FormattableString.Invariant($"{fill} rg {color} RG {centerX - 10:0.##} {centerY - 10:0.##} 20 20 re B "));
            builder.Append(FormattableString.Invariant($"{centerX - 5:0.##} {centerY + 5:0.##} m {centerX + 5:0.##} {centerY + 5:0.##} l "));
            builder.Append(FormattableString.Invariant($"{centerX - 5:0.##} {centerY + 5:0.##} m {centerX - 5:0.##} {centerY - 5:0.##} l "));
            builder.Append(FormattableString.Invariant($"{centerX - 5:0.##} {centerY - 5:0.##} m {centerX:0.##} {centerY - 5:0.##} l "));
            builder.Append(FormattableString.Invariant($"{centerX:0.##} {centerY - 5:0.##} m {centerX + 5:0.##} {centerY:0.##} l S\n"));
        }
        else if (componentType.Equals("Fan", StringComparison.OrdinalIgnoreCase))
        {
            builder.Append(FormattableString.Invariant($"{fill} rg {color} RG {centerX - 1:0.##} {centerY + 10:0.##} m "));
            builder.Append(FormattableString.Invariant($"{centerX + 12:0.##} {centerY + 10:0.##} l {centerX + 12:0.##} {centerY + 2:0.##} l "));
            builder.Append(FormattableString.Invariant($"{centerX + 4:0.##} {centerY + 2:0.##} l "));
            builder.Append(FormattableString.Invariant($"{centerX + 6:0.##} {centerY - 2:0.##} {centerX + 4:0.##} {centerY - 8:0.##} {centerX:0.##} {centerY - 10:0.##} c "));
            builder.Append(FormattableString.Invariant($"{centerX - 6:0.##} {centerY - 12:0.##} {centerX - 11:0.##} {centerY - 5:0.##} {centerX - 10:0.##} {centerY + 1:0.##} c "));
            builder.Append(FormattableString.Invariant($"{centerX - 10:0.##} {centerY + 7:0.##} {centerX - 6:0.##} {centerY + 10:0.##} {centerX - 1:0.##} {centerY + 10:0.##} c h B\n"));
        }
        else if (componentType.Equals("Coil", StringComparison.OrdinalIgnoreCase))
        {
            builder.Append(FormattableString.Invariant($"{centerX - 12:0.##} {centerY - 9:0.##} 24 18 re S\n"));
            builder.Append(FormattableString.Invariant(
                $"{centerX - 10:0.##} {centerY - 7:0.##} m {centerX - 6:0.##} {centerY + 7:0.##} l {centerX - 2:0.##} {centerY - 7:0.##} l {centerX + 2:0.##} {centerY + 7:0.##} l {centerX + 6:0.##} {centerY - 7:0.##} l {centerX + 10:0.##} {centerY + 7:0.##} l S\n"));
        }
        else if (componentType.Equals("Filter", StringComparison.OrdinalIgnoreCase))
        {
            builder.Append(FormattableString.Invariant($"{centerX - 9:0.##} {centerY - 12:0.##} 18 24 re S\n"));
            builder.Append(FormattableString.Invariant($"{centerX - 5:0.##} {centerY + 8:0.##} m {centerX - 1:0.##} {centerY + 4:0.##} {centerX - 1:0.##} {centerY:0.##} {centerX - 5:0.##} {centerY - 4:0.##} c {centerX - 1:0.##} {centerY - 8:0.##} {centerX + 3:0.##} {centerY - 8:0.##} {centerX + 6:0.##} {centerY - 4:0.##} c S\n"));
        }
        else if (componentType.Equals("Grd", StringComparison.OrdinalIgnoreCase))
        {
            DrawGrdLayoutIcon(builder, centerX, centerY, fittingGroup);
        }
        else if (componentType.Equals("CalculationPoint", StringComparison.OrdinalIgnoreCase))
        {
            var letter = fittingGroup.StartsWith("Temperature", StringComparison.OrdinalIgnoreCase) ? "T"
                : fittingGroup.StartsWith("Velocity", StringComparison.OrdinalIgnoreCase) ? "V"
                : fittingGroup.StartsWith("Airflow", StringComparison.OrdinalIgnoreCase) ? "Q" : "P";
            if (letter == "Q")
            {
                builder.Append(FormattableString.Invariant($"{fill} rg {color} RG 1.8 w "));
                CircleStroke(builder, centerX, centerY, 10, color);
                builder.Append(FormattableString.Invariant($"{centerX + 5:0.##} {centerY - 6:0.##} m {centerX + 11:0.##} {centerY - 12:0.##} l S\n"));
            }
            else
                builder.Append(FormattableString.Invariant(
                    $"{fill} rg {color} RG 1.5 w {centerX:0.##} {centerY + 11:0.##} m {centerX + 11:0.##} {centerY:0.##} l {centerX:0.##} {centerY - 11:0.##} l {centerX - 11:0.##} {centerY:0.##} l h B\n"));
            CenteredText(builder, centerX, centerY - 3, 9, letter, 1, bold: true);
        }
        else
        {
            DrawFittingLayoutIcon(builder, centerX, centerY, fittingGroup, color, fill);
        }
    }

    private static void DrawFittingLayoutIcon(StringBuilder builder, double x, double y, string group,
        string color, string fill)
    {
        if (group.Contains("Fan Exits", StringComparison.OrdinalIgnoreCase)
            || group.Contains("Plenum", StringComparison.OrdinalIgnoreCase))
        {
            var plenumFill = color == "0.12 0.30 0.47" ? "0.28 0.62 0.88" : fill;
            builder.Append(FormattableString.Invariant(
                $"{plenumFill} rg {color} RG 1.25 w {x - 11:0.##} {y - 11:0.##} 22 22 re B\n"));
        }
        else if (group.Contains("VAV", StringComparison.OrdinalIgnoreCase))
        {
            builder.Append(FormattableString.Invariant(
                $"{fill} rg {color} RG 1.5 w {x - 16:0.##} {y + 6:0.##} m {x + 16:0.##} {y + 6:0.##} l {x - 16:0.##} {y - 6:0.##} m {x + 16:0.##} {y - 6:0.##} l S "));
            builder.Append(FormattableString.Invariant(
                $"0 0 0 rg 0 0 0 RG {x - 6:0.##} {y - 6:0.##} 12 12 re B\n"));
        }
        else if (group.Contains("Transition", StringComparison.OrdinalIgnoreCase))
            builder.Append(FormattableString.Invariant(
                $"{fill} rg {color} RG 2.3 w {x - 14:0.##} {y + 10:0.##} m {x + 10:0.##} {y + 5:0.##} l {x + 10:0.##} {y - 5:0.##} l {x - 14:0.##} {y - 10:0.##} l h B {x - 14:0.##} {y + 6:0.##} m {x - 18:0.##} {y + 6:0.##} l {x - 14:0.##} {y - 6:0.##} m {x - 18:0.##} {y - 6:0.##} l {x + 10:0.##} {y + 3:0.##} m {x + 16:0.##} {y + 3:0.##} l {x + 10:0.##} {y - 3:0.##} m {x + 16:0.##} {y - 3:0.##} l S\n"));
        else if (group.Contains("Diverting", StringComparison.OrdinalIgnoreCase))
            builder.Append(FormattableString.Invariant(
                $"{fill} rg {color} RG 1.5 w {x - 12:0.##} {y + 3:0.##} m {x - 1:0.##} {y + 3:0.##} l {x + 9:0.##} {y + 11:0.##} l {x + 12:0.##} {y + 7:0.##} l {x + 3:0.##} {y:0.##} l {x + 12:0.##} {y - 7:0.##} l {x + 9:0.##} {y - 11:0.##} l {x - 1:0.##} {y - 3:0.##} l {x - 12:0.##} {y - 3:0.##} l h B\n"));
        else if (group.Contains("Converging", StringComparison.OrdinalIgnoreCase))
            builder.Append(FormattableString.Invariant(
                $"{fill} rg {color} RG 1.5 w {x + 12:0.##} {y + 3:0.##} m {x + 1:0.##} {y + 3:0.##} l {x - 9:0.##} {y + 11:0.##} l {x - 12:0.##} {y + 7:0.##} l {x - 3:0.##} {y:0.##} l {x - 12:0.##} {y - 7:0.##} l {x - 9:0.##} {y - 11:0.##} l {x + 1:0.##} {y - 3:0.##} l {x + 12:0.##} {y - 3:0.##} l h B\n"));
        else if (group.Contains("Damper", StringComparison.OrdinalIgnoreCase))
            builder.Append(FormattableString.Invariant($"{x - 10:0.##} {y - 9:0.##} 20 18 re S {x - 7:0.##} {y - 6:0.##} m {x + 7:0.##} {y + 6:0.##} l S\n"));
        else if (group.Contains("Terminal", StringComparison.OrdinalIgnoreCase))
            builder.Append(FormattableString.Invariant($"{fill} rg {color} RG 1.8 w {x - 13:0.##} {y + 8:0.##} m {x + 5:0.##} {y + 12:0.##} l {x + 5:0.##} {y - 12:0.##} l {x - 13:0.##} {y - 8:0.##} l h B {x + 5:0.##} {y + 4:0.##} m {x + 14:0.##} {y + 4:0.##} l {x + 5:0.##} {y - 4:0.##} m {x + 14:0.##} {y - 4:0.##} l S\n"));
        else if (group.Contains("Elbow", StringComparison.OrdinalIgnoreCase))
            builder.Append(FormattableString.Invariant($"1.5 w {x - 6:0.##} {y - 12:0.##} m {x - 6:0.##} {y + 1:0.##} l {x - 6:0.##} {y + 5:0.##} {x - 2:0.##} {y + 8:0.##} {x + 3:0.##} {y + 8:0.##} c {x + 12:0.##} {y + 8:0.##} l {x:0.##} {y - 12:0.##} m {x:0.##} {y + 1:0.##} l {x:0.##} {y + 2:0.##} {x + 1:0.##} {y + 3:0.##} {x + 3:0.##} {y + 3:0.##} c {x + 12:0.##} {y + 3:0.##} l {x - 6:0.##} {y - 12:0.##} m {x:0.##} {y - 12:0.##} l {x + 12:0.##} {y + 8:0.##} m {x + 12:0.##} {y + 3:0.##} l S\n"));
        else
            builder.Append(FormattableString.Invariant(
                $"{fill} rg {color} RG 1.5 w {x:0.##} {y + 11:0.##} m {x + 11:0.##} {y:0.##} l {x:0.##} {y - 11:0.##} l {x - 11:0.##} {y:0.##} l h B\n"));
    }

    private static void DrawGrdLayoutIcon(StringBuilder builder, double x, double y, string kind)
    {
        builder.Append(FormattableString.Invariant($"1.2 w {x - 10:0.##} {y - 10:0.##} 20 20 re S "));
        if (kind.Contains("Grille", StringComparison.OrdinalIgnoreCase))
        {
            for (var offset = -5; offset <= 5; offset += 5)
                builder.Append(FormattableString.Invariant($"{x + offset:0.##} {y - 9:0.##} m {x + offset:0.##} {y + 9:0.##} l {x - 9:0.##} {y + offset:0.##} m {x + 9:0.##} {y + offset:0.##} l "));
        }
        else if (kind.Contains("Diffuser", StringComparison.OrdinalIgnoreCase))
            builder.Append(FormattableString.Invariant($"{x - 5:0.##} {y - 5:0.##} 10 10 re "));
        else
            for (var offset = -5; offset <= 5; offset += 5)
                builder.Append(FormattableString.Invariant($"{x - 8:0.##} {y + offset:0.##} m {x + 8:0.##} {y + offset:0.##} l "));
        builder.Append("S\n");
    }

    private static void CircleStroke(StringBuilder builder, double centerX, double centerY, double radius, string rgb)
    {
        const double k = 0.5522847498;
        var c = radius * k;
        builder.Append($"{rgb} RG ");
        builder.Append(FormattableString.Invariant(
            $"{centerX + radius:0.##} {centerY:0.##} m {centerX + radius:0.##} {centerY + c:0.##} {centerX + c:0.##} {centerY + radius:0.##} {centerX:0.##} {centerY + radius:0.##} c {centerX - c:0.##} {centerY + radius:0.##} {centerX - radius:0.##} {centerY + c:0.##} {centerX - radius:0.##} {centerY:0.##} c {centerX - radius:0.##} {centerY - c:0.##} {centerX - c:0.##} {centerY - radius:0.##} {centerX:0.##} {centerY - radius:0.##} c {centerX + c:0.##} {centerY - radius:0.##} {centerX + radius:0.##} {centerY - c:0.##} {centerX + radius:0.##} {centerY:0.##} c S\n"));
    }

    private static void Circle(StringBuilder builder, double centerX, double centerY, double radius, string rgb)
    {
        const double k = 0.5522847498;
        var c = radius * k;
        builder.Append($"{rgb} rg {rgb} RG ");
        builder.Append(FormattableString.Invariant(
            $"{centerX + radius:0.##} {centerY:0.##} m {centerX + radius:0.##} {centerY + c:0.##} {centerX + c:0.##} {centerY + radius:0.##} {centerX:0.##} {centerY + radius:0.##} c {centerX - c:0.##} {centerY + radius:0.##} {centerX - radius:0.##} {centerY + c:0.##} {centerX - radius:0.##} {centerY:0.##} c {centerX - radius:0.##} {centerY - c:0.##} {centerX - c:0.##} {centerY - radius:0.##} {centerX:0.##} {centerY - radius:0.##} c {centerX + c:0.##} {centerY - radius:0.##} {centerX + radius:0.##} {centerY - c:0.##} {centerX + radius:0.##} {centerY:0.##} c f\n"));
    }

    private static void DrawRow(StringBuilder builder, IReadOnlyList<string> values, double[] widths,
        double left, double top, double height, bool bold)
    {
        var x = left;
        for (var column = 0; column < values.Count; column++)
        {
            builder.Append("0.70 0.76 0.81 RG 0.5 w ");
            builder.Append(FormattableString.Invariant($"{x} {top - height} {widths[column]} {height} re S\n"));
            var max = Math.Max(3, (int)((widths[column] - 4) / (bold ? 2.55 : 4.5)));
            if (bold)
            {
                var lines = WrapPdfText(values[column], max).Take(4).ToArray();
                var startY = top - 10;
                for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
                    CenteredText(builder, x + widths[column] / 2, startY - lineIndex * 8.5, 4.8,
                        lines[lineIndex], max, bold: true);
            }
            else
                Text(builder, x + 3, top - 13, 7.2, Fit(values[column], max), bold: false);
            x += widths[column];
        }
    }

    private static string Fit(string value, int maximum)
    {
        var clean = PdfText(value);
        return clean.Length <= maximum ? clean : clean[..Math.Max(1, maximum - 3)] + "...";
    }

    private static void Text(StringBuilder builder, double x, double y, double size, string value,
        bool bold = false, bool white = false)
    {
        builder.Append(white ? "1 1 1 rg " : "0.10 0.17 0.23 rg ");
        builder.Append(FormattableString.Invariant($"BT /{(bold ? "F2" : "F1")} {size:0.##} Tf {x:0.##} {y:0.##} Td ({EscapePdf(PdfText(value))}) Tj ET\n"));
    }

    private static void TextColor(StringBuilder builder, double x, double y, double size, string value,
        string rgb, bool bold = false)
    {
        builder.Append($"{rgb} rg ");
        builder.Append(FormattableString.Invariant($"BT /{(bold ? "F2" : "F1")} {size:0.##} Tf {x:0.##} {y:0.##} Td ({EscapePdf(PdfText(value))}) Tj ET\n"));
    }

    private static string PdfText(string value)
    {
        var replacements = value.Replace(" → ", " to ").Replace("→", " to ")
            .Replace(" ← ", " from ").Replace("←", " from ")
            .Replace("×", "x").Replace("—", "-").Replace("–", "-").Replace("•", "-")
            .Replace("°", " deg").Replace("²", "2").Replace("³", "3").Replace("Δ", "Delta ")
            .Replace("ρ", "rho").Replace("μ", "mu").Replace("ε", "epsilon").Replace("π", "pi");
        return new string(replacements.Select(x => x is >= ' ' and <= '~' ? x : '?').ToArray());
    }

    private static string EscapePdf(string value) => value.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");

    private static void WritePdf(string path, IReadOnlyList<byte[]> contents, byte[]? logoJpeg,
        int logoPixelWidth, int logoPixelHeight, IReadOnlyList<EmbeddedImage> referenceImages)
    {
        var fontRegular = 3 + contents.Count * 2;
        var fontBold = fontRegular + 1;
        var fontItalic = fontBold + 1;
        var hasLogo = logoJpeg is { Length: > 0 } && logoPixelWidth > 0 && logoPixelHeight > 0;
        var nextObject = fontItalic + 1;
        var logoObject = hasLogo ? nextObject++ : 0;
        var referenceObjects = referenceImages.ToDictionary(image => image.Name, _ => nextObject++,
            StringComparer.Ordinal);
        var objects = new List<byte[]>();
        objects.Add(Encoding.ASCII.GetBytes("<< /Type /Catalog /Pages 2 0 R >>"));
        var kids = string.Join(' ', Enumerable.Range(0, contents.Count).Select(i => $"{3 + i * 2} 0 R"));
        objects.Add(Encoding.ASCII.GetBytes($"<< /Type /Pages /Kids [{kids}] /Count {contents.Count} >>"));
        for (var index = 0; index < contents.Count; index++)
        {
            var xObjects = new List<string>();
            if (hasLogo) xObjects.Add($"/Logo {logoObject} 0 R");
            xObjects.AddRange(referenceImages.Select(image => $"/{image.Name} {referenceObjects[image.Name]} 0 R"));
            var imageResources = xObjects.Count > 0 ? $" /XObject << {string.Join(' ', xObjects)} >>" : string.Empty;
            objects.Add(Encoding.ASCII.GetBytes($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 792 612] /Resources << /Font << /F1 {fontRegular} 0 R /F2 {fontBold} 0 R /F3 {fontItalic} 0 R >>{imageResources} >> /Contents {4 + index * 2} 0 R >>"));
            var prefix = Encoding.ASCII.GetBytes($"<< /Length {contents[index].Length} >>\nstream\n");
            var suffix = Encoding.ASCII.GetBytes("\nendstream");
            objects.Add(prefix.Concat(contents[index]).Concat(suffix).ToArray());
        }
        objects.Add(Encoding.ASCII.GetBytes("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"));
        objects.Add(Encoding.ASCII.GetBytes("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold >>"));
        objects.Add(Encoding.ASCII.GetBytes("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Oblique >>"));
        if (hasLogo)
        {
            var prefix = Encoding.ASCII.GetBytes(FormattableString.Invariant(
                $"<< /Type /XObject /Subtype /Image /Width {logoPixelWidth} /Height {logoPixelHeight} /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length {logoJpeg!.Length} >>\nstream\n"));
            var suffix = Encoding.ASCII.GetBytes("\nendstream");
            objects.Add(prefix.Concat(logoJpeg).Concat(suffix).ToArray());
        }
        foreach (var image in referenceImages)
        {
            var prefix = Encoding.ASCII.GetBytes(FormattableString.Invariant(
                $"<< /Type /XObject /Subtype /Image /Width {image.Width} /Height {image.Height} /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length {image.Data.Length} >>\nstream\n"));
            var suffix = Encoding.ASCII.GetBytes("\nendstream");
            objects.Add(prefix.Concat(image.Data).Concat(suffix).ToArray());
        }

        using var stream = File.Create(path);
        Write(stream, "%PDF-1.4\n%DSZR\n");
        var offsets = new List<long> { 0 };
        for (var index = 0; index < objects.Count; index++)
        {
            offsets.Add(stream.Position);
            Write(stream, $"{index + 1} 0 obj\n");
            stream.Write(objects[index]);
            Write(stream, "\nendobj\n");
        }
        var xref = stream.Position;
        Write(stream, $"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        for (var index = 1; index < offsets.Count; index++)
            Write(stream, $"{offsets[index]:D10} 00000 n \n");
        Write(stream, $"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
    }

    private static void Write(Stream stream, string value) => stream.Write(Encoding.ASCII.GetBytes(value));
}
