using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace TS.NET.Testbench.UI;

[YamlSerializable]
public class TestbenchSettings
{
    public int Version { get; set; } = 1;
    public string SigGen1Ip { get; set; } = "";
    public string SigGen2Ip { get; set; } = "";
    public string[] SequenceTypes { get; set; } = [];

    public static TestbenchSettings Load()
    {
        // Order of priority for settings file:
        // 1. Settings file loaded from LocalApplicationData
        // 2. Default settings file created in LocalApplicationData
        // 3. Default settings file created in working directory
        // 4. Default settings from memory
        try
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var localApplicationDataVariablesFile = Path.Combine(localAppData, "ThunderScope", "TS.NET.Testbench.UI", "settings.yaml");
            // Windows: %LocalAppData%\ThunderScope\TS.NET.Testbench.UI\settings.yaml
            // macOS: ~/Library/Application Support/ThunderScope/TS.NET.Testbench.UI/settings.yaml
            // Linux: $XDG_CONFIG_HOME/ThunderScope/TS.NET.Testbench.UI/settings.yaml or $HOME/.config/ThunderScope/TS.NET.Testbench.UI/settings.yaml
            Directory.CreateDirectory(Path.GetDirectoryName(localApplicationDataVariablesFile)!);
            if (!File.Exists(localApplicationDataVariablesFile))
                WriteDefaultVariables(localApplicationDataVariablesFile);

            return FromYamlFile(localApplicationDataVariablesFile);
        }
        catch { }

        const string workingDirectoryVariablesFile = "settings.yaml";
        try
        {
            if (!File.Exists(workingDirectoryVariablesFile))
                WriteDefaultVariables(workingDirectoryVariablesFile);

            return FromYamlFile(workingDirectoryVariablesFile);
        }
        catch { }

        return ReadDefaultVariables();
    }

    private static TestbenchSettings FromYamlFile(string file)
    {
        if (!File.Exists(file))
            throw new FileNotFoundException(file);

        return FromYaml(File.ReadAllText(file));
    }

    private static TestbenchSettings FromYaml(string yaml)
    {
        var context = new StaticContext();
        var deserializer = new StaticDeserializerBuilder(context)
            .WithNamingConvention(PascalCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        return deserializer.Deserialize<TestbenchSettings>(yaml);
    }

    private static void WriteDefaultVariables(string variablesFile)
    {
        using var resourceStream = OpenDefaultVariablesStream();
        using var outputStream = File.Create(variablesFile);
        resourceStream.CopyTo(outputStream);
    }

    private static TestbenchSettings ReadDefaultVariables()
    {
        using var resourceStream = OpenDefaultVariablesStream();
        using var reader = new StreamReader(resourceStream);
        return FromYaml(reader.ReadToEnd());
    }

    private static Stream OpenDefaultVariablesStream()
    {
        return typeof(TestbenchSettings).Assembly.GetManifestResourceStream("TS.NET.Testbench.UI.settings.yaml")
            ?? throw new InvalidOperationException("Embedded default variables were not found.");
    }
}

[YamlStaticContext]
public partial class StaticContext : YamlDotNet.Serialization.StaticContext { }
