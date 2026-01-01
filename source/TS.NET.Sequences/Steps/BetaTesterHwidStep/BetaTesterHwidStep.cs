using System.Text.Json;
using TS.NET.Sequencer;

namespace TS.NET.Sequences;

public class BetaTesterHwidStep : ModalUiStep
{
    public FactoryHwidJson Hwid { get; private set; } = new();

    public BetaTesterHwidStep(string name, ModalUiContext modalUiContext, CommonVariables variables) : base(name, modalUiContext)
    {
        Action = (CancellationToken cancellationToken) =>
        {
            var status = Status.Running;
            var continueLoop = true;

            RegisterEventHandler((JsonElement eventData) =>
            {
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
                                return; // minimal validation: require serial number
                            }

                            Hwid.SerialNumber = serial.Trim();

                            var boardRevStr = eventData.GetProperty("boardRevision").GetString();
                            if (!string.IsNullOrWhiteSpace(boardRevStr) && double.TryParse(boardRevStr, out var br))
                                Hwid.BoardRevision = br;

                            var buildConfig = eventData.GetProperty("buildConfig").GetString();
                            if (!string.IsNullOrWhiteSpace(buildConfig))
                                Hwid.BuildConfig = buildConfig.Trim();

                            var buildDate = eventData.GetProperty("buildDate").GetString();
                            if (!string.IsNullOrWhiteSpace(buildDate))
                                Hwid.BuildDate = buildDate.Trim();

                            var mfgSig = eventData.GetProperty("manufacturingSignature").GetString();
                            if (!string.IsNullOrWhiteSpace(mfgSig))
                                Hwid.ManufacturingSignature = mfgSig.Trim();

                            // DNA from form; allow hex with or without 0x, or decimal
                            ulong dna = 0;
                            if (eventData.TryGetProperty("dna", out var dnaProp))
                            {
                                var dnaStr = dnaProp.GetString() ?? string.Empty;
                                dnaStr = dnaStr.Trim();

                                if (!string.IsNullOrEmpty(dnaStr))
                                {
                                    if (dnaStr.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                                    {
                                        ulong.TryParse(dnaStr[2..], System.Globalization.NumberStyles.HexNumber,
                                            System.Globalization.CultureInfo.InvariantCulture, out dna);
                                    }
                                    else
                                    {
                                        // try hex first, then decimal
                                        if (!ulong.TryParse(dnaStr, System.Globalization.NumberStyles.HexNumber,
                                                System.Globalization.CultureInfo.InvariantCulture, out dna))
                                        {
                                            ulong.TryParse(dnaStr, out dna);
                                        }
                                    }
                                }
                            }

                            Instruments.Instance.EraseFactoryDataAndAppendHwid(dna, Hwid);

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

            UpdateUi<BetaTesterHwid>(new Dictionary<string, object?>());

            while (continueLoop)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Thread.Sleep(50);
            }

            

            return status;
        };
    }
}
