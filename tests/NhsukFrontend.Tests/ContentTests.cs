using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NhsukFrontend.Components;
using Xunit;

namespace NhsukFrontend.Tests;

/// <summary>Behaviour upstream's fixtures can't express, because it only exists in a typed port.</summary>
public class ContentTests
{
    private static async Task<string> Render<T>(Dictionary<string, object?> parameters) where T : IComponent
    {
        var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () =>
            (await renderer.RenderComponentAsync<T>(ParameterView.FromDictionary(parameters))).ToHtmlString());
    }

    [Fact]
    public async Task Environment_banner_replaces_the_name_in_plain_child_content()
    {
        var html = await Render<NhsukEnvironmentBanner>(new()
        {
            ["Tag"] = (TagOptions)"Review",
            ["ChildContent"] = (RenderFragment)(b => b.AddMarkupContent(0, "A <b>%{name}</b> build")),
        });

        Assert.Contains("A <b>review</b> build", html);
    }

    [Fact]
    public async Task Environment_banner_keeps_child_content_that_contains_components()
    {
        var html = await Render<NhsukEnvironmentBanner>(new()
        {
            ["Tag"] = (TagOptions)"Review",
            ["ChildContent"] = (RenderFragment)(b =>
            {
                b.OpenComponent<NhsukTag>(0);
                b.AddComponentParameter(1, "Text", "Build 42");
                b.CloseComponent();
            }),
        });

        Assert.Contains("Build 42", html);
    }

    [Fact]
    public async Task Link_opens_in_a_new_tab_when_target_is_a_plain_razor_attribute()
    {
        var html = await Render<NhsukLink>(new() { ["Text"] = "Guidance", ["Href"] = "/guidance", ["target"] = "_blank" });

        Assert.Contains("rel=\"noopener noreferrer\"", html);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(html, "target="));
    }

    [Theory]
    [InlineData("")]
    [InlineData("false")]
    public async Task Link_without_a_base_class_has_no_class_attribute(string className)
    {
        var html = await Render<NhsukLink>(new() { ["Text"] = "Guidance", ["Href"] = "/guidance", ["ClassName"] = className });

        Assert.Equal("<a href=\"/guidance\">Guidance</a>", html.Trim());
    }
}
