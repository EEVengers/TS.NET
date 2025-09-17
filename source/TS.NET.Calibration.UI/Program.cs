using Photino.NET;
using System.Drawing;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using TS.NET.Sequencer;

namespace TS.NET.Calibration.UI;

class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        Sequence sequence = new();
        var cancellationTokenSource = new CancellationTokenSource();
        var variablesFile = VariablesFile.FromJsonFile("variables.json");
        Variables.Instance.ThunderScopeIp = variablesFile.ThunderScopeIp;
        Variables.Instance.SigGen1Ip = variablesFile.SigGen1Ip;
        Variables.Instance.SigGen2Ip = variablesFile.SigGen2Ip;

        string loadPath = "../../../../Resources/wwwroot/index.html";
#if RELEASE
        // CreateStaticFileServer allows the use of the embedded resources
        PhotinoServer
            .CreateStaticFileServer(args, out string baseUrl)
            .RunAsync();
        loadPath = $"{baseUrl}/index.html";
#endif

        var window = new PhotinoWindow()
            .SetTitle("ThunderScope Calibration")
            .SetIconFile(ExtractEmbeddedResourceToTempFile("icon.ico", "TS.NET.Calibration.UI"))
            .SetUseOsDefaultSize(false)
            .SetSize(new Size(1440, 900))
            .Center()
            .SetResizable(true)
            .RegisterWebMessageReceivedHandler((object sender, string message) =>
            {
                var window = (PhotinoWindow)sender;
                var json = JsonDocument.Parse(message);
                var command = json.RootElement.GetProperty("command").GetString();

                Func<Dialog, DialogResult> uiDialog = (Dialog dialog) =>
                {
                    var photinoButtons = dialog.Buttons switch
                    {
                        DialogButtons.Ok => PhotinoDialogButtons.Ok,
                        DialogButtons.OkCancel => PhotinoDialogButtons.OkCancel,
                        DialogButtons.YesNo => PhotinoDialogButtons.YesNo,
                        DialogButtons.YesNoCancel => PhotinoDialogButtons.YesNoCancel,
                        DialogButtons.RetryCancel => PhotinoDialogButtons.RetryCancel,
                        DialogButtons.AbortRetryIgnore => PhotinoDialogButtons.AbortRetryIgnore,
                        _ => throw new NotImplementedException()
                    };
                    var photinoIcon = dialog.Icon switch
                    {
                        DialogIcon.Info => PhotinoDialogIcon.Info,
                        DialogIcon.Warning => PhotinoDialogIcon.Warning,
                        DialogIcon.Error => PhotinoDialogIcon.Error,
                        DialogIcon.Question => PhotinoDialogIcon.Question,
                        _ => throw new NotImplementedException()
                    };
                    var dialogResult = window.ShowMessage(dialog.Title, dialog.Text, photinoButtons, photinoIcon);
                    return dialogResult switch
                    {
                        PhotinoDialogResult.Cancel => DialogResult.Cancel,
                        PhotinoDialogResult.Ok => DialogResult.Ok,
                        PhotinoDialogResult.Yes => DialogResult.Yes,
                        PhotinoDialogResult.No => DialogResult.No,
                        PhotinoDialogResult.Abort => DialogResult.Abort,
                        PhotinoDialogResult.Retry => DialogResult.Retry,
                        PhotinoDialogResult.Ignore => DialogResult.Ignore,
                        _ => throw new NotImplementedException()
                    };
                };

                switch (command)
                {
                    case "app-loaded":
                        window.SendWebMessage(JsonSerializer.Serialize(LogDto.FromLog(), CamelCaseContext.Default.LogDto));
                        window.SendWebMessage(JsonSerializer.Serialize(VariablesDto.FromVariables(), DefaultCaseContext.Default.VariablesDto));
                        window.SendWebMessage(JsonSerializer.Serialize(SequenceDto.FromSequence(sequence), CamelCaseContext.Default.SequenceDto));
                        break;
                    case "load-sequence":
                        var sequenceString = json.RootElement.GetProperty("sequence").GetString();
                        sequence = sequenceString switch
                        {
                            "self-cal" => new SelfCalibrationSequence(uiDialog),
                            "bench-cal" => new BenchCalibrationSequence(uiDialog),
                            _ => throw new InvalidDataException(),
                        };
                        Action<Step> uiPreStep = (Step step) =>
                        {
                            var preStep = new StepUpdateDto { Type = "step-update", Step = StepDto.FromStep(step) };
                            window.SendWebMessage(JsonSerializer.Serialize(preStep, CamelCaseContext.Default.StepUpdateDto));
                        };

                        Action<Step> uiPostStep = (Step step) =>
                        {
                            var postStep = new StepUpdateDto { Type = "step-update", Step = StepDto.FromStep(step) };
                            window.SendWebMessage(JsonSerializer.Serialize(postStep, CamelCaseContext.Default.StepUpdateDto));
                            window.SendWebMessage(JsonSerializer.Serialize(VariablesDto.FromVariables(), DefaultCaseContext.Default.VariablesDto));
                        };

                        Action<Status?> sequenceStatus = (Status? status) =>
                        {
                            var postStep = new SequenceStatusUpdateDto { Type = "sequence-status-update", Status = status };
                            window.SendWebMessage(JsonSerializer.Serialize(postStep, CamelCaseContext.Default.SequenceStatusUpdateDto));
                        };

                        sequence.PreStep += uiPreStep;
                        sequence.PostStep += uiPostStep;
                        sequence.SequenceStatusChanged += sequenceStatus;
                        Variables.Instance.Sequence = sequence.Name;
                        window.SendWebMessage(JsonSerializer.Serialize(SequenceDto.FromSequence(sequence), CamelCaseContext.Default.SequenceDto));
                        window.SendWebMessage(JsonSerializer.Serialize(VariablesDto.FromVariables(), DefaultCaseContext.Default.VariablesDto));
                        break;
                    case "start-sequence":
                        cancellationTokenSource = new();
                        sequence.PreRun();
                        Variables.Instance.Sequence = sequence.Name;
                        window.SendWebMessage(JsonSerializer.Serialize(SequenceDto.FromSequence(sequence), CamelCaseContext.Default.SequenceDto));
                        Task.Run(() => sequence.Run(cancellationTokenSource));
                        break;
                    case "stop-sequence":
                        cancellationTokenSource?.Cancel();
                        break;
                }
            }).Load(loadPath);

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
}
