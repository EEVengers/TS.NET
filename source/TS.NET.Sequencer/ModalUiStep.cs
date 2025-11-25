using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace TS.NET.Sequencer;

public class ModalUiContext
{
    public Action<ModalUiUpdate> Update { get; init; }
    public Action<JsonElement>? EventHandler { get; set; }

    public ModalUiContext(Action<ModalUiUpdate> update )
    { 
        Update = update;
    }
}

public class ModalUiUpdate
{
    public required string Html;
    public required string? Script;
}

public class ModalUiStep : Step
{
    private readonly ModalUiContext modalUiContext;
    private readonly IServiceCollection services = new ServiceCollection();
    private readonly IServiceProvider serviceProvider;
    private readonly ILoggerFactory loggerFactory;

    public ModalUiStep(string name, ModalUiContext modalUiContext) : base(name)
    {
        this.modalUiContext = modalUiContext;

        services.AddLogging();
        serviceProvider = services.BuildServiceProvider();
        loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();      
    }

    public void RegisterEventHandler(Action<JsonElement>? eventHandler)
    {
        modalUiContext.EventHandler = eventHandler;
    }

    public Task UpdateUi<T>(Dictionary<string, object?> viewModel) where T : IComponent
    {
        // https://learn.microsoft.com/en-us/aspnet/core/blazor/components/render-components-outside-of-aspnetcore?view=aspnetcore-10.0
        return Task.Run(async () =>
        {
            await using var htmlRenderer = new HtmlRenderer(serviceProvider, loggerFactory);
            var html = await htmlRenderer.Dispatcher.InvokeAsync(async () =>
            {
                var parameters = ParameterView.FromDictionary(viewModel);
                var output = await htmlRenderer.RenderComponentAsync<T>(parameters);
                return output.ToHtmlString();
            });

            var scriptRegex = new Regex(@"<script[^>]*>([\s\S]*?)</script>", RegexOptions.IgnoreCase);
            var matches = scriptRegex.Matches(html);
            var scriptContent = string.Join("\n", matches.Select(m => m.Groups[1].Value.Trim()));        
            var htmlWithoutScripts = scriptRegex.Replace(html, "");

            modalUiContext.Update(new ModalUiUpdate 
            { 
                Html = htmlWithoutScripts, 
                Script = string.IsNullOrWhiteSpace(scriptContent) ? null : scriptContent 
            });
        });
    }

    public void HideUi()
    {
        modalUiContext.Update(new ModalUiUpdate { Html = "", Script = null });
    }
}
