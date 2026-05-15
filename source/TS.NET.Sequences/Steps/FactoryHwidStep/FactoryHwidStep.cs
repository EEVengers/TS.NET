using System.Text.Json;
using TS.NET.Sequencer;

namespace TS.NET.Sequences;

public class FactoryHwidStep : ModalUiStep
{
    public FactoryHwidStep(string name, ModalUiContext modalUiContext, FactoryBringUpVariables variables) : base(name, modalUiContext)
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

                            variables.Hwid.SerialNumber = serial.Trim();

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

            UpdateUi<FactoryHwid>(new Dictionary<string, object?>() { { "DNA", $"0x{variables.FpgaDna:X16}" }, { "BuildDate", DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")} });

            while (continueLoop)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Thread.Sleep(50);
            }

            

            return status;
        };
    }
}
