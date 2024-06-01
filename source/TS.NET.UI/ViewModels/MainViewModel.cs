using ReactiveUI;

namespace TS.NET.UI.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private ThunderscopeScpiClient scpiClient = new("127.0.0.1", 5025);

    public MainViewModel()
    {
        scpiClient.Connect();
    }

    private string scpiConsole = string.Empty;
    public string ScpiConsole
    {
        get => scpiConsole;
        set => this.RaiseAndSetIfChanged(ref scpiConsole, value);
    }

    private string scpiInput = string.Empty;
    public string ScpiInput
    {
        get => scpiInput;
        set => this.RaiseAndSetIfChanged(ref scpiInput, value);
    }

    public void RunCommand()
    {
        scpiClient.Send("RUN");
        ScpiConsole += "RUN\n";
    }

    public void StopCommand()
    {
        scpiClient.Send("STOP");
        ScpiConsole += "STOP\n";
    }

    public void SingleCommand()
    {
        scpiClient.Send("SINGLE");
        ScpiConsole += "SINGLE\n";
    }

    public void NormalCommand()
    {
        scpiClient.Send("NORMAL");
        ScpiConsole += "NORMAL\n";
    }

    public void AutoCommand()
    {
        scpiClient.Send("AUTO");
        ScpiConsole += "AUTO\n";
    }

    public void ForceCommand()
    {
        scpiClient.Send("FORCE");
        ScpiConsole += "FORCE\n";
    }

    public void ScpiInputOnEnter()
    {
        if (ScpiInput != string.Empty)
        {
            scpiClient.Send(ScpiInput);
            ScpiConsole += $"{ScpiInput}\n";
            ScpiInput = string.Empty;
        }
    }

    public void TriggerLevelChange(double voltage)
    {
        var command = $":TRIG:LEV {voltage:F6}\n";
        scpiClient.Send(command);
        ScpiConsole += command;
    }

    public void TriggerDelayChange(ulong fsDelay)
    {
        var command = $":TRIG:DELAY {fsDelay}\n";
        scpiClient.Send(command);
        ScpiConsole += command;
    }
    
}
