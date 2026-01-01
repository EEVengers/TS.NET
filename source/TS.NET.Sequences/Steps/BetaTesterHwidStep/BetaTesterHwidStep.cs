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

            HideUi();

            return status;
        };
    }
}
