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
            var localApplicationDataSettingsFile = Path.Combine(localAppData, "ThunderScope", "TS.NET.Testbench.UI", "settings.yaml");
            // Windows: %LocalAppData%\ThunderScope\TS.NET.Testbench.UI\settings.yaml
            // macOS: ~/Library/Application Support/ThunderScope/TS.NET.Testbench.UI/settings.yaml
            // Linux: $XDG_CONFIG_HOME/ThunderScope/TS.NET.Testbench.UI/settings.yaml or $HOME/.config/ThunderScope/TS.NET.Testbench.UI/settings.yaml
            Directory.CreateDirectory(Path.GetDirectoryName(localApplicationDataSettingsFile)!);
            if (!File.Exists(localApplicationDataSettingsFile))
                WriteDefaultSettings(localApplicationDataSettingsFile);

            return FromYamlFile(localApplicationDataSettingsFile);
        }
        catch { }

        const string workingDirectorySettingsFile = "settings.yaml";
        try
        {
            if (!File.Exists(workingDirectorySettingsFile))
                WriteDefaultSettings(workingDirectorySettingsFile);

            return FromYamlFile(workingDirectorySettingsFile);
        }
        catch { }

        return ReadDefaultSettings();
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

    private static void WriteDefaultSettings(string settingsFile)
    {
        using var resourceStream = OpenDefaultSettingsStream();
        using var outputStream = File.Create(settingsFile);
        resourceStream.CopyTo(outputStream);
    }

    private static TestbenchSettings ReadDefaultSettings()
    {
        using var resourceStream = OpenDefaultSettingsStream();
        using var reader = new StreamReader(resourceStream);
        return FromYaml(reader.ReadToEnd());
    }

    private static Stream OpenDefaultSettingsStream()
    {
        return typeof(TestbenchSettings).Assembly.GetManifestResourceStream("TS.NET.Testbench.UI.settings.yaml")
            ?? throw new InvalidOperationException("Embedded default settings were not found.");
    }
}

[YamlStaticContext]
public partial class StaticContext : YamlDotNet.Serialization.StaticContext { }
