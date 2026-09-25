using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.DependencyInjection;
using NhsukFrontend.Components;
using NhsukFrontend.Parity;
using Xunit;

namespace NhsukFrontend.Tests;

public sealed class TagHelperTests
{
    private static async Task<string> Render(Type component, object options, TagHelperAttributeList? html = null, string? content = null)
    {
        await using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        var fixture = new Fixture { Name = "test", Context = JsonSerializer.SerializeToElement(options), CallBlock = content };
        return await TagHelperParity.ProcessAsync(TagHelperParity.Prepare(component, fixture),
            new DefaultHttpContext { RequestServices = services }, html);
    }

    [Fact]
    public async Task Plain_html_attributes_pass_through_to_the_component()
    {
        var html = await Render(typeof(NhsukInput), new { name = "postcode", label = "Postcode" },
            new TagHelperAttributeList { { "data-test", "a&b" }, new TagHelperAttribute("required") });
        Assert.Contains("data-test=\"a&amp;b\"", html);
        Assert.Contains(" required", html);
        Assert.DoesNotContain("<nhsuk-input", html);
    }

    [Fact]
    public async Task Tag_content_becomes_component_content()
    {
        var html = await Render(typeof(NhsukInsetText), new { }, content: "<p>Inside the tag</p>");
        Assert.Contains("<p>Inside the tag</p>", html);
        Assert.Contains("nhsuk-inset-text", html);
    }

    [Fact]
    public async Task Plain_text_attribute_fills_an_options_object()
    {
        var html = await Render(typeof(NhsukPanel), new { heading = "Application complete" });
        Assert.Contains("Application complete</h1>", html);
    }

    [Fact]
    public async Task One_renderer_is_shared_by_every_tag_in_a_request()
    {
        await using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = services };
        var prepared = TagHelperParity.Prepare(typeof(NhsukTag), new Fixture { Name = "t", Context = JsonSerializer.SerializeToElement(new { text = "Done" }) });

        await TagHelperParity.ProcessAsync(prepared, http);
        var first = http.Items["NhsukFrontend.HtmlRenderer"];
        await TagHelperParity.ProcessAsync(prepared, http);
        Assert.NotNull(first);
        Assert.Same(first, http.Items["NhsukFrontend.HtmlRenderer"]);
    }
}
