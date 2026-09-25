using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NhsukFrontend.Components;
using NhsukFrontend.Components.Forms;
using Xunit;

namespace NhsukFrontend.Tests;

public sealed class BindingTests
{
    private sealed class Details
    {
        [Display(Name = "Full name")] [Required(ErrorMessage = "Enter your full name")] public string? FullName { get; set; }
        [Display(Name = "Date of birth")] [NhsukDate(RequiredMessage = "Enter your date of birth")] public NhsukDate? DateOfBirth { get; set; } = new();
        public List<string>? Needs { get; set; } = ["b"];
    }

    private Details Model { get; } = new();

    [Fact]
    public void For_names_the_field_like_Blazor_form_binding()
    {
        var (name, owner, member) = BlazorFields.Parse((System.Linq.Expressions.Expression<Func<object?>>)(() => Model.FullName));
        Assert.Equal("Model.FullName", name);
        Assert.Same(Model, owner);
        Assert.Equal("FullName", member.Name);
    }

    [Fact]
    public async Task Input_takes_name_label_value_and_error_from_the_edit_context()
    {
        Model.FullName = "Ada";
        var editContext = new EditContext(Model);
        var messages = new ValidationMessageStore(editContext);
        messages.Add(editContext.Field(nameof(Details.FullName)), "Enter your full name");

        var html = await RenderInEditContext<NhsukInput>(editContext, new() { ["For"] = (System.Linq.Expressions.Expression<Func<object?>>)(() => Model.FullName) });

        Assert.Contains("name=\"Model.FullName\"", html);
        Assert.Contains("id=\"Model.FullName\"", html);
        Assert.Contains("value=\"Ada\"", html);
        Assert.Contains(">Full name</label>", html);
        Assert.Contains("Enter your full name", html);
        Assert.Contains("aria-describedby=\"Model.FullName-error\"", html);
    }

    [Fact]
    public async Task Explicit_options_win_over_the_model()
    {
        var html = await Render<NhsukInput>(new()
        {
            ["Field"] = new NhsukField { Name = "FullName", Label = "Full name", Value = "Ada" },
            ["Label"] = (LabelOptions)"What is your name?",
        });
        Assert.Contains("What is your name?", html);
        Assert.DoesNotContain(">Full name<", html);
        Assert.Contains("value=\"Ada\"", html);
    }

    [Fact]
    public async Task Checkboxes_tick_the_bound_values()
    {
        var html = await Render<NhsukCheckboxes>(new()
        {
            ["Field"] = new NhsukField { Name = "Needs", Values = ["b"] },
            ["Items"] = new List<CheckboxesItemsItem?> { new() { Value = "a", Text = "A" }, new() { Value = "b", Text = "B" } },
        });
        Assert.Contains("value=\"b\" checked", html);
        Assert.DoesNotContain("value=\"a\" checked", html);
    }

    [Fact]
    public async Task Date_input_names_parts_for_binding_and_highlights_only_the_missing_part()
    {
        var html = await Render<NhsukDateInput>(new()
        {
            ["Field"] = new NhsukField
            {
                Name = "DateOfBirth",
                Label = "Date of birth",
                Date = new NhsukDate { Day = "1", Year = "1990" },
                Error = "Date of birth must include a month",
            },
        });
        Assert.Contains("name=\"DateOfBirth.Day\"", html);
        Assert.Contains("name=\"DateOfBirth.Month\"", html);
        Assert.Contains("id=\"DateOfBirth-day\"", html);
        Assert.Contains("class=\"nhsuk-input nhsuk-input--error nhsuk-input--width-2 nhsuk-date-input__input\" id=\"DateOfBirth-month\"", html);
        Assert.Contains("class=\"nhsuk-input nhsuk-input--width-2 nhsuk-date-input__input\" id=\"DateOfBirth-day\"", html);
    }

    [Theory]
    [InlineData(null, null, null, "Enter your date of birth")]
    [InlineData("1", null, "1990", "Date of birth must include a month")]
    [InlineData("31", "2", "1990", "Date of birth must be a real date")]
    [InlineData("29", "2", "2024", null)]
    public void Date_validation_uses_service_manual_messages(string? day, string? month, string? year, string? expected)
    {
        var model = new Details { FullName = "x", DateOfBirth = new NhsukDate { Day = day, Month = month, Year = year } };
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
        Assert.Equal(expected, results.FirstOrDefault(r => r.MemberNames.Contains(nameof(Details.DateOfBirth)))?.ErrorMessage);
    }

    private static Task<string> Render<T>(Dictionary<string, object?> parameters) where T : IComponent =>
        RenderFragment(builder =>
        {
            builder.OpenComponent<T>(0);
            foreach (var (name, value) in parameters) builder.AddComponentParameter(1, name, value);
            builder.CloseComponent();
        });

    private static Task<string> RenderInEditContext<T>(EditContext editContext, Dictionary<string, object?> parameters) where T : IComponent =>
        RenderFragment(builder =>
        {
            builder.OpenComponent<CascadingValue<EditContext>>(0);
            builder.AddComponentParameter(1, "Value", editContext);
            builder.AddComponentParameter(2, "ChildContent", (RenderFragment)(inner =>
            {
                inner.OpenComponent<T>(0);
                foreach (var (name, value) in parameters) inner.AddComponentParameter(1, name, value);
                inner.CloseComponent();
            }));
            builder.CloseComponent();
        });

    private static async Task<string> RenderFragment(RenderFragment fragment)
    {
        await using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () =>
            (await renderer.RenderComponentAsync<FragmentHost>(ParameterView.FromDictionary(new Dictionary<string, object?> { ["Fragment"] = fragment }))).ToHtmlString());
    }

    private sealed class FragmentHost : ComponentBase
    {
        [Parameter] public RenderFragment? Fragment { get; set; }
        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder) => builder.AddContent(0, Fragment);
    }
}
