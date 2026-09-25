using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace NhsukFrontend.Components.Forms;

/// <summary>
/// The value of a date input: day, month and year exactly as the user typed them, so they can be shown
/// back unchanged when the date is not valid. Binds from <c>Name.Day</c>, <c>Name.Month</c> and
/// <c>Name.Year</c> in both MVC and Blazor.
/// </summary>
public sealed class NhsukDate
{
    public string? Day { get; set; }
    public string? Month { get; set; }
    public string? Year { get; set; }

    /// <summary>A date to show in the form, for example when editing a saved answer.</summary>
    /// <remarks>A factory rather than a constructor: Blazor form binding needs a single public constructor.</remarks>
    public static NhsukDate From(DateOnly date) => new()
    {
        Day = date.Day.ToString(CultureInfo.InvariantCulture),
        Month = date.Month.ToString(CultureInfo.InvariantCulture),
        Year = date.Year.ToString(CultureInfo.InvariantCulture),
    };

    /// <summary>True when nothing has been entered.</summary>
    public bool IsEmpty => string.IsNullOrWhiteSpace(Day) && string.IsNullOrWhiteSpace(Month) && string.IsNullOrWhiteSpace(Year);

    /// <summary>The date, or null when it is incomplete or not a real date.</summary>
    public DateOnly? Date =>
        int.TryParse(Day?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var d)
        && int.TryParse(Month?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var m)
        && int.TryParse(Year?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var y)
        && y is >= 1000 and <= 9999 && m is >= 1 and <= 12 && d >= 1 && d <= DateTime.DaysInMonth(y, m)
            ? new DateOnly(y, m, d)
            : null;

    /// <summary>The first part left empty, for messages like "Date of birth must include a month".</summary>
    public string? MissingPart =>
        string.IsNullOrWhiteSpace(Day) ? "day" : string.IsNullOrWhiteSpace(Month) ? "month" : string.IsNullOrWhiteSpace(Year) ? "year" : null;
}

/// <summary>
/// Validates an <see cref="NhsukDate"/> with the NHS digital service manual's error messages.
/// <c>{0}</c> in each message is the field's display name.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class NhsukDateAttribute : ValidationAttribute
{
    public bool Required { get; set; } = true;
    public string RequiredMessage { get; set; } = "Enter {0}";
    public string MissingPartMessage { get; set; } = "{0} must include a {1}";
    public string InvalidMessage { get; set; } = "{0} must be a real date";

    /// <summary>Set to reject dates in the future (for example a date of birth).</summary>
    public bool MustBeInPast { get; set; }
    public string PastMessage { get; set; } = "{0} must be in the past";

    protected override ValidationResult? IsValid(object? value, ValidationContext context)
    {
        var name = context.DisplayName;
        var members = context.MemberName is null ? null : new[] { context.MemberName };
        var date = value as NhsukDate;

        if (date is null || date.IsEmpty)
            return Required ? new ValidationResult(Format(RequiredMessage, name.ToLowerInvariant()), members) : ValidationResult.Success;
        if (date.MissingPart is { } part)
            return new ValidationResult(Format(MissingPartMessage, name, part), members);
        if (date.Date is not { } parsed)
            return new ValidationResult(Format(InvalidMessage, name), members);
        if (MustBeInPast && parsed >= DateOnly.FromDateTime(DateTime.Today))
            return new ValidationResult(Format(PastMessage, name), members);
        return ValidationResult.Success;
    }

    private static string Format(string message, params object[] args) => string.Format(CultureInfo.CurrentCulture, message, args);
}
