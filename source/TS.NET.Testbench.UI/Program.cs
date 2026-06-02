using Photino.NET;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using TS.NET.Photino;
using TS.NET.Sequencer;
using TS.NET.Sequences;

namespace TS.NET.Testbench.UI;

// cd build/
// cmake --fresh .. -DENABLE_FACTORY_PROVISIONING=ON
// cmake --build .

class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        Sequence sequence = new();
        IJsonVariables variables = new SelfCalibrationVariables() { };
        var cancellationTokenSource = new CancellationTokenSource();
        var variablesFile = VariablesFile.FromJsonFile("variables.json");

        string loadPath = "../../../Resources/wwwroot/index.html";
#if RELEASE
        // CreateStaticFileServer allows the use of the embedded resources
        PhotinoServer
            .CreateStaticFileServer(args, out string baseUrl)
            .RunAsync();
        loadPath = $"{baseUrl}/index.html";
#endif

        PhotinoWindow? window = null;

        void modalUiUpdate(ModalUiUpdate modalUiUpdate)
        {
            window?.SendWebMessage(JsonSerializer.Serialize(ModalUiUpdateDto.FromModalUiUpdate(modalUiUpdate), CamelCaseContext.Default.ModalUiUpdateDto));
        }
        var modalUiContext = new ModalUiContext(modalUiUpdate);

        window = new PhotinoWindow()
            .SetLogVerbosity(0)
            .SetTitle("Testbench")
            .SetIconFile(ExtractEmbeddedResourceToTempFile("icon.ico", "TS.NET.Testbench.UI"))
            .SetUseOsDefaultSize(false)
            .SetNotificationsEnabled(false)
            .Center()
            .SetResizable(true)
            .RegisterWebMessageReceivedHandler((object sender, string message) =>
            {
                var window = (PhotinoWindow)sender;
                var json = JsonDocument.Parse(message);
                var command = json.RootElement.GetProperty("command").GetString();

                switch (command)
                {
                    case "app-loaded":
                        window.SendWebMessage(JsonSerializer.Serialize(LogDto.FromLog(), CamelCaseContext.Default.LogDto));
                        window.SendWebMessage(JsonSerializer.Serialize(VariablesDto.FromVariables(variables), DefaultCaseContext.Default.VariablesDto));
                        window.SendWebMessage(JsonSerializer.Serialize(SequenceDto.FromSequence(sequence), CamelCaseContext.Default.SequenceDto));
                        window.SendWebMessage(JsonSerializer.Serialize(SequencesDto.CreateForTypes(variablesFile.SequenceTypes), CamelCaseContext.Default.SequencesDto));
                        break;
                    case "load-sequence":
                        var sequenceString = json.RootElement.GetProperty("sequence").GetString();
                        switch (sequenceString)
                        {
                            case "self-calibration":
                                {
                                    var newVariables = new SelfCalibrationVariables() { };
                                    var newSequence = new SelfCalibrationSequence(modalUiContext, newVariables);
                                    variables = newVariables;
                                    sequence = newSequence;
                                    break;
                                }
                            case "noise-verification":
                                {
                                    var newVariables = new NoiseVerificationVariables() { };
                                    var newSequence = new NoiseVerificationSequence(modalUiContext, newVariables);
                                    variables = newVariables;
                                    sequence = newSequence;
                                    break;
                                }

                            // Factory sequences
                            case "factory-bring-up":
                                {
                                    var newVariables = new FactoryBringUpVariables
                                    {
                                        SigGen1Host = variablesFile.SigGen1Ip,
                                        SigGen2Host = variablesFile.SigGen2Ip,
                                        FlashImages = new(StringComparer.OrdinalIgnoreCase)
                                        {
                                            ["XC7A35T"] = "thunderscope_full_prod_0.5.0.bin",
                                            ["XC7A50T"] = "thunderscope_full_dev_0.5.0.bin"
                                        }
                                    };
                                    var newSequence = new FactoryBringUpSequence(modalUiContext, newVariables);
                                    variables = newVariables;
                                    sequence = newSequence;
                                    break;
                                }
                            case "factory-trim":
                                {
                                    var newVariables = new FactoryVariables
                                    {
                                        SigGen1Host = variablesFile.SigGen1Ip,
                                        SigGen2Host = variablesFile.SigGen2Ip
                                    };
                                    var newSequence = new FactoryTrimSequence(modalUiContext, newVariables);
                                    variables = newVariables;
                                    sequence = newSequence;
                                    break;
                                }
                            case "factory-calibration":
                                {
                                    var newVariables = new FactoryVariables
                                    {
                                        SigGen1Host = variablesFile.SigGen1Ip,
                                        SigGen2Host = variablesFile.SigGen2Ip
                                    };
                                    var newSequence = new FactoryCalibrationSequence(modalUiContext, newVariables);
                                    variables = newVariables;
                                    sequence = newSequence;
                                    break;
                                }
                            case "factory-verification":
                                {
                                    var newVariables = new FactoryVariables
                                    {
                                        SigGen1Host = variablesFile.SigGen1Ip,
                                        SigGen2Host = variablesFile.SigGen2Ip
                                    };
                                    var newSequence = new FactoryVerificationSequence(modalUiContext, newVariables);
                                    variables = newVariables;
                                    sequence = newSequence;
                                    break;
                                }
                            case "developer-calibration":
                                {
                                    var newVariables = new FactoryVariables
                                    {
                                        SigGen1Host = variablesFile.SigGen1Ip,
                                        SigGen2Host = variablesFile.SigGen2Ip
                                    };
                                    var newSequence = new DeveloperCalibrationSequence(modalUiContext, newVariables);
                                    variables = newVariables;
                                    sequence = newSequence;
                                    break;
                                }
                            case "developer-verification":
                                {
                                    var newVariables = new FactoryVariables
                                    {
                                        SigGen1Host = variablesFile.SigGen1Ip,
                                        SigGen2Host = variablesFile.SigGen2Ip
                                    };
                                    var newSequence = new DeveloperVerificationSequence(modalUiContext, newVariables);
                                    variables = newVariables;
                                    sequence = newSequence;
                                    break;
                                }
                            case "developer-hwid":
                                {
                                    var newVariables = new SelfCalibrationVariables() { };
                                    var newSequence = new DeveloperHwidSequence(modalUiContext, newVariables);
                                    variables = newVariables;
                                    sequence = newSequence;
                                }
                                break;
                            default:
                                throw new InvalidDataException();
                        }
                        Action<Step> uiPreStep = (Step step) =>
                        {
                            var preStep = new StepUpdateDto { Type = "step-update", Step = StepDto.FromStep(step) };
                            window.SendWebMessage(JsonSerializer.Serialize(preStep, CamelCaseContext.Default.StepUpdateDto));
                        };

                        Action<Step> uiPostStep = (Step step) =>
                        {
                            var postStep = new StepUpdateDto { Type = "step-update", Step = StepDto.FromStep(step) };
                            window.SendWebMessage(JsonSerializer.Serialize(postStep, CamelCaseContext.Default.StepUpdateDto));
                            window.SendWebMessage(JsonSerializer.Serialize(VariablesDto.FromVariables(variables), DefaultCaseContext.Default.VariablesDto));
                        };

                        Action<Status?> sequenceStatus = (Status? status) =>
                        {
                            var postStep = new SequenceStatusUpdateDto { Type = "sequence-status-update", Status = status };
                            window.SendWebMessage(JsonSerializer.Serialize(postStep, CamelCaseContext.Default.SequenceStatusUpdateDto));
                        };

                        sequence.PreStep += uiPreStep;
                        sequence.PostStep += uiPostStep;
                        sequence.SequenceStatusChanged += sequenceStatus;
                        //Variables.Instance.Sequence = sequence.Name;
                        window.SendWebMessage(JsonSerializer.Serialize(SequenceDto.FromSequence(sequence), CamelCaseContext.Default.SequenceDto));
                        window.SendWebMessage(JsonSerializer.Serialize(VariablesDto.FromVariables(variables), DefaultCaseContext.Default.VariablesDto));
                        break;
                    case "start-sequence":
                        cancellationTokenSource = new();
                        sequence.PreRun();
                        //Variables.Instance.Sequence = sequence.Name;
                        window.SendWebMessage(JsonSerializer.Serialize(SequenceDto.FromSequence(sequence), CamelCaseContext.Default.SequenceDto));
                        RunSequenceAndReport(sequence, cancellationTokenSource);
                        break;
                    case "stop-sequence":
                        cancellationTokenSource?.Cancel();
                        break;
                    case "set-skip":
                        var stepIndex = json.RootElement.GetProperty("stepIndex").GetInt32();
                        var skip = json.RootElement.GetProperty("skip").GetBoolean();
                        sequence.Steps[stepIndex - 1].Skip = skip;
                        break;
                    case "modal-ui-event":
                        try
                        {
                            modalUiContext?.EventHandler?.Invoke(json.RootElement.GetProperty("event"));
                        }
                        catch (Exception ex)
                        {
                            modalUiContext?.CaptureEventHandlerException(ex);
                        }
                        break;
                }
            }).Load(loadPath);

        bool initialized = false;
        var windowSizing = new WindowSizing(baseMinWidth: 1200, baseMinHeight: 800, baseWidth: 1440, baseHeight: 900);
        window.WindowCreated += (_, _) =>
        {
            windowSizing.Initialise(window);
            window.Center();
            initialized = true;
        };

        window.WindowSizeChanged += (_, _) =>
        {
            if (initialized)
                windowSizing.UpdateSize(window);
        };

        window.WindowLocationChanged += (_, _) =>
        {
            if (initialized)
                windowSizing.UpdateSize(window);
        };

        Logger.Instance.EventLogged += (logEvent) =>
        {
            window.SendWebMessage(JsonSerializer.Serialize(new LogUpdateDto { Type = "log-update", Timestamp = logEvent.Timestamp, Level = logEvent.Level, Message = logEvent.Message }, CamelCaseContext.Default.LogUpdateDto));
        };

#if DEBUG
        // Initialise live reload service in debug mode
        var sourceDirectory = Path.GetFullPath(Path.Combine(GetThisDirectory(), @"Resources/wwwroot"));
        var liveReloadService = new LiveReloadService(window, sourceDirectory, loadPath);
        liveReloadService.Start();
#endif

        window.WaitForClose();

#if DEBUG
        liveReloadService?.Dispose();
#endif
    }

#if DEBUG
    private static string GetThisDirectory([CallerFilePath] string path = null)
    {
        return Path.GetDirectoryName(path);
    }
#endif

    private static string ExtractEmbeddedResourceToTempFile(string fileName, string resourceNamespace)
    {
        string resourceName = $"{resourceNamespace}.{fileName}";

        Assembly assembly = Assembly.GetExecutingAssembly();
        //var names = assembly.GetManifestResourceNames();
        using (Stream resourceStream = assembly.GetManifestResourceStream(resourceName))
        {
            if (resourceStream == null)
            {
                return null;
            }

            string tempFile = Path.Combine(Path.GetTempPath(), fileName);

            using (FileStream fileStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write))
            {
                resourceStream.CopyTo(fileStream);
            }

            return tempFile;
        }
    }

    private async static Task RunSequenceAndReport(Sequence sequence, CancellationTokenSource cancellationTokenSource)
    {
        try
        {
            await sequence.Run(cancellationTokenSource);
            var fileName = $"{sequence.Name} - {sequence.StartTimestamp:yyyy-MM-dd HHmmss}";
            sequence.ToXml(fileName + ".xml");
            var reportGenerator = new HtmlReportGenerator();
            reportGenerator.Render(sequence, fileName + ".html");
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
        }
    }
}
