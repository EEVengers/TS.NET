using RazorLight;

namespace TS.NET.Sequencer;

public class HtmlReportGenerator
{
    public async void Render(Sequence sequence, string outputFilePath)
    {
        var engine = new RazorLightEngineBuilder()
            .UseEmbeddedResourcesProject(typeof(HtmlReportGenerator).Assembly)
            .UseMemoryCachingProvider()
            .Build();

        string html = await engine.CompileRenderAsync("TS.NET.Sequencer.HTML.HtmlReport", sequence);
        File.WriteAllText(outputFilePath, html);
    }
}
