using System.ComponentModel.DataAnnotations;
using NhsukFrontend.Components;
using NhsukFrontend.Components.Forms;

namespace NhsukFrontend.Demo.Models;

/// <summary>
/// One model shared by the Blazor and Razor Pages examples. Validation is ordinary DataAnnotations;
/// the components pick up names, values, labels and errors from it.
/// </summary>
public sealed class AppointmentRequest
{
    [Display(Name = "Full name")]
    [Required(ErrorMessage = "Enter your full name")]
    public string? FullName { get; set; }

    [Display(Name = "NHS number")]
    [Required(ErrorMessage = "Enter your NHS number")]
    [RegularExpression(@"^\s*\d{3}[\s-]?\d{3}[\s-]?\d{4}\s*$", ErrorMessage = "NHS number must be 10 numbers")]
    public string? NhsNumber { get; set; }

    [Display(Name = "Date of birth")]
    [NhsukDate(RequiredMessage = "Enter your date of birth", MustBeInPast = true)]
    public NhsukDate? DateOfBirth { get; set; } = new();

    [Display(Name = "How would you like to be contacted?")]
    [Required(ErrorMessage = "Select how you would like to be contacted")]
    public string? Contact { get; set; }

    [Display(Name = "Do you need any of these?")]
    public List<string>? Needs { get; set; } = [];

    [Display(Name = "Anything else we should know? (optional)")]
    [MaxLength(200, ErrorMessage = "Anything else we should know must be 200 characters or fewer")]
    public string? Notes { get; set; }

    // Form binding leaves a collection null when nothing is ticked, so treat null as empty.

    public static readonly List<RadiosItemsItem?> ContactOptions =
    [
        new() { Value = "email", Text = "Email" },
        new() { Value = "phone", Text = "Phone" },
        new() { Value = "post", Text = "Letter by post" },
    ];

    public static readonly List<CheckboxesItemsItem?> NeedOptions =
    [
        new() { Value = "interpreter", Text = "An interpreter" },
        new() { Value = "step-free", Text = "Step-free access" },
        new() { Value = "longer", Text = "A longer appointment" },
    ];

    public string Describe(string? value, IEnumerable<RadiosItemsItem?> options) =>
        options.FirstOrDefault(o => o?.Value == value)?.Text ?? "";

    public List<SummaryListRowsItem?> Summary(string changeHref) =>
    [
        Row("Full name", FullName, changeHref),
        Row("NHS number", NhsNumber, changeHref),
        Row("Date of birth", DateOfBirth?.Date?.ToString("d MMMM yyyy"), changeHref),
        Row("Contact", Describe(Contact, ContactOptions), changeHref),
        Row("Needs", Needs is not { Count: > 0 } ? "None" : string.Join(", ", Needs.Select(n => NeedOptions.First(o => o!.Value == n)!.Text)), changeHref),
        Row("Anything else", string.IsNullOrWhiteSpace(Notes) ? "None" : Notes, changeHref),
    ];

    private static SummaryListRowsItem Row(string key, string? value, string href) => new()
    {
        Key = key,
        Value = value ?? "",
        Actions = new SummaryListRowsActionsOptions { Items = [new() { Href = href, Text = "Change", VisuallyHiddenText = key.ToLowerInvariant() }] },
    };
}
