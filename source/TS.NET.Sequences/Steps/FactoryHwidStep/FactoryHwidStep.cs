using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;
using System.Text.Json;
using TS.NET.JTAG;
using TS.NET.Sequencer;

namespace TS.NET.Sequences;

public class FactoryHwidStep : ModalUiStep
{
    private const string LookupEvent = "lookupSerial";
    private const string LookupCsvFileName = "FactoryHwid.csv";

    public FactoryHwidStep(string name, ModalUiContext modalUiContext, FactoryBringUpVariables variables) : base(name, modalUiContext)
    {
        Action = (CancellationToken cancellationToken) =>
        {
            var status = Status.Running;
            var continueLoop = true;
            FactoryHwidCsvRow? pendingGeneratedRow = null;

            RegisterEventHandler((JsonElement eventData) =>
            {
                if (eventData.TryGetProperty("action", out var action) && action.GetString() == LookupEvent)
                {
                    var serialForLookup = eventData.TryGetProperty("serialNumber", out var serialProperty)
                        ? (serialProperty.GetString() ?? string.Empty).Trim()
                        : string.Empty;

                    if (string.IsNullOrWhiteSpace(serialForLookup))
                    {
                        return;
                    }

                    var fuseDnaValue = variables.FpgaDna ?? 0UL;
                    var row = GenerateMetadata(serialForLookup, fuseDnaValue);
                    pendingGeneratedRow = row;
                    _ = UpdateUi<FactoryHwid>(new Dictionary<string, object?>
                    {
                        { "Serial", serialForLookup },
                        { "BoardRevision", row.BoardRevision },
                        { "BuildConfiguration", row.BuildConfiguration },
                        { "BuildDate", row.BuildDate },
                        { "ManufacturingSignature", row.ManufacturingSignature }
                    });

                    return;
                }

                if (eventData.TryGetProperty("buttonClicked", out var buttonClicked))
                {
                    var button = buttonClicked.GetString();
                    if (button == "ok" || button == "cancel")
                    {
                        HideUi();

                        if (button == "ok")
                        {
                            var serial = eventData.GetProperty("serialNumber").GetString() ?? string.Empty;
                            if (string.IsNullOrWhiteSpace(serial))
                            {
                                return;
                            }

                            PersistMetadata(pendingGeneratedRow.Serial, pendingGeneratedRow.BuildDate, pendingGeneratedRow.ManufacturingSignature);

                            variables.Hwid.SerialNumber = pendingGeneratedRow.Serial;

                            var boardRevStr = eventData.GetProperty("boardRevision").GetString();
                            if (!string.IsNullOrWhiteSpace(boardRevStr) && double.TryParse(boardRevStr, out var br))
                                variables.Hwid.BoardRevision = br;
                            var buildConfig = eventData.GetProperty("buildConfig").GetString();
                            if (!string.IsNullOrWhiteSpace(buildConfig))
                                variables.Hwid.BuildConfig = buildConfig.Trim();
                            var buildDate = eventData.GetProperty("buildDate").GetString();
                            if (!string.IsNullOrWhiteSpace(buildDate))
                                variables.Hwid.BuildDate = buildDate.Trim();
                            var mfgSig = eventData.GetProperty("manufacturingSignature").GetString();
                            if (!string.IsNullOrWhiteSpace(mfgSig))
                                variables.Hwid.ManufacturingSignature = mfgSig.Trim();

                            status = Status.Done;
                        }
                        else
                        {
                            status = Status.Cancelled;
                        }

                        continueLoop = false;
                    }
                }
            });

            UpdateUi<FactoryHwid>(new Dictionary<string, object?>() { { "BuildDate", DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")} });

            while (continueLoop)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Thread.Sleep(50);
            }

            

            return status;
        };
    }

    private static FactoryHwidCsvRow GenerateMetadata(string serialNumber, ulong fuseDna)
    {
        var rows = ReadRows();

        var row = rows.FirstOrDefault(record => string.Equals(record.Serial, serialNumber, StringComparison.OrdinalIgnoreCase));
        if (row == null)
        {
            throw new InvalidOperationException($"Serial '{serialNumber}' was not found in {LookupCsvFileName}.");
        }

        row.BuildDate = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        row.ManufacturingSignature = $"{fuseDna:X16}";

        return row;
    }

    private static void PersistMetadata(string serialNumber, string buildDate, string manufacturingSignature)
    {
        var csvPath = Path.Combine(AppContext.BaseDirectory, LookupCsvFileName);
        var rows = ReadRows();

        var row = rows.FirstOrDefault(record => string.Equals(record.Serial, serialNumber, StringComparison.OrdinalIgnoreCase));
        if (row == null)
        {
            throw new InvalidOperationException($"Serial '{serialNumber}' was not found in {LookupCsvFileName}.");
        }

        row.BuildDate = buildDate;
        row.ManufacturingSignature = manufacturingSignature;

        using var writer = new StreamWriter(csvPath, false);
        using var csvWriter = new CsvWriter(writer, CultureInfo.InvariantCulture);
        csvWriter.Context.RegisterClassMap<FactoryHwidCsvRowMap>();
        csvWriter.WriteHeader<FactoryHwidCsvRow>();
        csvWriter.NextRecord();
        foreach (var existingRow in rows)
        {
            csvWriter.WriteRecord(existingRow);
            csvWriter.NextRecord();
        }
    }

    private static List<FactoryHwidCsvRow> ReadRows()
    {
        var csvPath = Path.Combine(AppContext.BaseDirectory, LookupCsvFileName);
        if (!File.Exists(csvPath))
        {
            throw new FileNotFoundException($"CSV file was not found: {csvPath}", csvPath);
        }

        List<FactoryHwidCsvRow> rows;
        using (var reader = new StreamReader(csvPath))
        using (var csvReader = new CsvReader(reader, new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture)
        {
            PrepareHeaderForMatch = args => args.Header.Trim()
        }))
        {
            csvReader.Context.RegisterClassMap<FactoryHwidCsvRowMap>();
            rows = csvReader.GetRecords<FactoryHwidCsvRow>().ToList();
        }

        return rows;
    }

    private sealed class FactoryHwidCsvRow
    {
        public string Serial { get; set; } = string.Empty;
        public string BoardRevision { get; set; } = string.Empty;
        public string BuildConfiguration { get; set; } = string.Empty;
        public string BuildDate { get; set; } = string.Empty;
        public string ManufacturingSignature { get; set; } = string.Empty;
    }

    private sealed class FactoryHwidCsvRowMap : ClassMap<FactoryHwidCsvRow>
    {
        public FactoryHwidCsvRowMap()
        {
            Map(x => x.Serial).Name("Serial");
            Map(x => x.BoardRevision).Name("Board revision");
            Map(x => x.BuildConfiguration).Name("Build configuration");
            Map(x => x.BuildDate).Name("Build date");
            Map(x => x.ManufacturingSignature).Name("Manufacturing signature");
        }
    }
}
