// Model binding for the form components: a Blazor `For` expression or an MVC `asp-for` (through `Field`)
// fills in the name, value, label and error message. Anything set explicitly is left alone.
#nullable enable
using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using NhsukFrontend.Components.Forms;

namespace NhsukFrontend.Components;

/// <summary>Remembers which options were filled from the model, so the next render can refill them.</summary>
internal sealed class BoundOptions
{
    private readonly List<Action> _resets = [];

    public void Reset()
    {
        foreach (var reset in _resets) reset();
        _resets.Clear();
    }

    public void Fill<T>(T? current, T? value, Action<T?> set) where T : class
    {
        if (current is not null || value is null) return;
        set(value);
        _resets.Add(() => set(null));
    }
}

public partial class NhsukInput
{
    /// <summary>Blazor: the model property this field edits, for example <c>() =&gt; Model.Postcode</c>.
    /// Fills in the name, value, label and validation error from the enclosing <c>EditForm</c>.</summary>
    [Parameter] public Expression<Func<object?>>? For { get; set; }

    /// <summary>A field description built elsewhere, for example by the MVC <c>asp-for</c> tag helpers.</summary>
    [Parameter] public NhsukField? Field { get; set; }

    [CascadingParameter] private EditContext? CascadedEditContext { get; set; }

    private readonly BoundOptions _bound = new();

    public override Task SetParametersAsync(ParameterView parameters)
    {
        _bound.Reset();
        parameters.SetParameterProperties(this);
        if ((Field ?? BlazorFields.From(For, CascadedEditContext)) is { } field) Bind(field);
        return base.SetParametersAsync(ParameterView.Empty);
    }

    private void Bind(NhsukField field)
    {
        _bound.Fill(Name, field.Name, v => Name = v);
        _bound.Fill(Value, field.Value, v => Value = v);
        _bound.Fill(Label, field.Label is null ? null : (LabelOptions)field.Label, v => Label = v);
        _bound.Fill(ErrorMessage, field.Error is null ? null : (ErrorMessageOptions)field.Error, v => ErrorMessage = v);
    }
}

public partial class NhsukTextarea
{
    /// <summary>Blazor: the model property this field edits, for example <c>() =&gt; Model.Postcode</c>.
    /// Fills in the name, value, label and validation error from the enclosing <c>EditForm</c>.</summary>
    [Parameter] public Expression<Func<object?>>? For { get; set; }

    /// <summary>A field description built elsewhere, for example by the MVC <c>asp-for</c> tag helpers.</summary>
    [Parameter] public NhsukField? Field { get; set; }

    [CascadingParameter] private EditContext? CascadedEditContext { get; set; }

    private readonly BoundOptions _bound = new();

    public override Task SetParametersAsync(ParameterView parameters)
    {
        _bound.Reset();
        parameters.SetParameterProperties(this);
        if ((Field ?? BlazorFields.From(For, CascadedEditContext)) is { } field) Bind(field);
        return base.SetParametersAsync(ParameterView.Empty);
    }

    private void Bind(NhsukField field)
    {
        _bound.Fill(Name, field.Name, v => Name = v);
        _bound.Fill(Value, field.Value, v => Value = v);
        _bound.Fill(Label, field.Label is null ? null : (LabelOptions)field.Label, v => Label = v);
        _bound.Fill(ErrorMessage, field.Error is null ? null : (ErrorMessageOptions)field.Error, v => ErrorMessage = v);
    }
}

public partial class NhsukCharacterCount
{
    /// <summary>Blazor: the model property this field edits, for example <c>() =&gt; Model.Postcode</c>.
    /// Fills in the name, value, label and validation error from the enclosing <c>EditForm</c>.</summary>
    [Parameter] public Expression<Func<object?>>? For { get; set; }

    /// <summary>A field description built elsewhere, for example by the MVC <c>asp-for</c> tag helpers.</summary>
    [Parameter] public NhsukField? Field { get; set; }

    [CascadingParameter] private EditContext? CascadedEditContext { get; set; }

    private readonly BoundOptions _bound = new();

    public override Task SetParametersAsync(ParameterView parameters)
    {
        _bound.Reset();
        parameters.SetParameterProperties(this);
        if ((Field ?? BlazorFields.From(For, CascadedEditContext)) is { } field) Bind(field);
        return base.SetParametersAsync(ParameterView.Empty);
    }

    private void Bind(NhsukField field)
    {
        _bound.Fill(Name, field.Name, v => Name = v);
        _bound.Fill(Value, field.Value, v => Value = v);
        _bound.Fill(Label, field.Label is null ? null : (LabelOptions)field.Label, v => Label = v);
        _bound.Fill(ErrorMessage, field.Error is null ? null : (ErrorMessageOptions)field.Error, v => ErrorMessage = v);
    }
}

public partial class NhsukSelect
{
    /// <summary>Blazor: the model property this field edits, for example <c>() =&gt; Model.Postcode</c>.
    /// Fills in the name, value, label and validation error from the enclosing <c>EditForm</c>.</summary>
    [Parameter] public Expression<Func<object?>>? For { get; set; }

    /// <summary>A field description built elsewhere, for example by the MVC <c>asp-for</c> tag helpers.</summary>
    [Parameter] public NhsukField? Field { get; set; }

    [CascadingParameter] private EditContext? CascadedEditContext { get; set; }

    private readonly BoundOptions _bound = new();

    public override Task SetParametersAsync(ParameterView parameters)
    {
        _bound.Reset();
        parameters.SetParameterProperties(this);
        if ((Field ?? BlazorFields.From(For, CascadedEditContext)) is { } field) Bind(field);
        return base.SetParametersAsync(ParameterView.Empty);
    }

    private void Bind(NhsukField field)
    {
        _bound.Fill(Name, field.Name, v => Name = v);
        _bound.Fill(Value, field.Value, v => Value = v);
        _bound.Fill(Label, field.Label is null ? null : (LabelOptions)field.Label, v => Label = v);
        _bound.Fill(ErrorMessage, field.Error is null ? null : (ErrorMessageOptions)field.Error, v => ErrorMessage = v);
    }
}

public partial class NhsukRadios
{
    /// <summary>Blazor: the model property this field edits, for example <c>() =&gt; Model.Postcode</c>.
    /// Fills in the name, value, label and validation error from the enclosing <c>EditForm</c>.</summary>
    [Parameter] public Expression<Func<object?>>? For { get; set; }

    /// <summary>A field description built elsewhere, for example by the MVC <c>asp-for</c> tag helpers.</summary>
    [Parameter] public NhsukField? Field { get; set; }

    [CascadingParameter] private EditContext? CascadedEditContext { get; set; }

    private readonly BoundOptions _bound = new();

    public override Task SetParametersAsync(ParameterView parameters)
    {
        _bound.Reset();
        parameters.SetParameterProperties(this);
        if ((Field ?? BlazorFields.From(For, CascadedEditContext)) is { } field) Bind(field);
        return base.SetParametersAsync(ParameterView.Empty);
    }

    private void Bind(NhsukField field)
    {
        _bound.Fill(Name, field.Name, v => Name = v);
        _bound.Fill(Value, field.Value, v => Value = v);
        _bound.Fill(Fieldset, field.Label is null ? null : new FieldsetOptions { Legend = field.Label }, v => Fieldset = v);
        _bound.Fill(ErrorMessage, field.Error is null ? null : (ErrorMessageOptions)field.Error, v => ErrorMessage = v);
    }
}

public partial class NhsukCheckboxes
{
    /// <summary>Blazor: the model property this field edits, for example <c>() =&gt; Model.Postcode</c>.
    /// Fills in the name, value, label and validation error from the enclosing <c>EditForm</c>.</summary>
    [Parameter] public Expression<Func<object?>>? For { get; set; }

    /// <summary>A field description built elsewhere, for example by the MVC <c>asp-for</c> tag helpers.</summary>
    [Parameter] public NhsukField? Field { get; set; }

    [CascadingParameter] private EditContext? CascadedEditContext { get; set; }

    private readonly BoundOptions _bound = new();

    public override Task SetParametersAsync(ParameterView parameters)
    {
        _bound.Reset();
        parameters.SetParameterProperties(this);
        if ((Field ?? BlazorFields.From(For, CascadedEditContext)) is { } field) Bind(field);
        return base.SetParametersAsync(ParameterView.Empty);
    }

    private void Bind(NhsukField field)
    {
        _bound.Fill(Name, field.Name, v => Name = v);
        _bound.Fill(Values, field.Values?.ToList() ?? (field.Value is null ? null : [field.Value]), v => Values = v);
        _bound.Fill(Fieldset, field.Label is null ? null : new FieldsetOptions { Legend = field.Label }, v => Fieldset = v);
        _bound.Fill(ErrorMessage, field.Error is null ? null : (ErrorMessageOptions)field.Error, v => ErrorMessage = v);
    }
}

public partial class NhsukDateInput
{
    /// <summary>Blazor: the model property this field edits, for example <c>() =&gt; Model.Postcode</c>.
    /// Fills in the name, value, label and validation error from the enclosing <c>EditForm</c>.</summary>
    [Parameter] public Expression<Func<object?>>? For { get; set; }

    /// <summary>A field description built elsewhere, for example by the MVC <c>asp-for</c> tag helpers.</summary>
    [Parameter] public NhsukField? Field { get; set; }

    [CascadingParameter] private EditContext? CascadedEditContext { get; set; }

    private readonly BoundOptions _bound = new();

    public override Task SetParametersAsync(ParameterView parameters)
    {
        _bound.Reset();
        parameters.SetParameterProperties(this);
        if ((Field ?? BlazorFields.From(For, CascadedEditContext)) is { } field) Bind(field);
        return base.SetParametersAsync(ParameterView.Empty);
    }

    private void Bind(NhsukField field)
    {
        _bound.Fill(Id, field.Name, v => Id = v);
        _bound.Fill(Fieldset, field.Label is null ? null : new FieldsetOptions { Legend = field.Label }, v => Fieldset = v);
        _bound.Fill(ErrorMessage, field.Error is null ? null : (ErrorMessageOptions)field.Error, v => ErrorMessage = v);

        // When only part of the date is missing, highlight just that part.
        var missing = field.Error is not null && field.Date is { IsEmpty: false } date ? date.MissingPart : null;
        _bound.Fill(Day, BoundPart(field, "Day", field.Date?.Day, 2, missing), v => Day = v);
        _bound.Fill(Month, BoundPart(field, "Month", field.Date?.Month, 2, missing), v => Month = v);
        _bound.Fill(Year, BoundPart(field, "Year", field.Date?.Year, 4, missing), v => Year = v);
    }

    /// <summary>Names the parts <c>Name.Day</c> and so on, which MVC and Blazor both bind to <see cref="NhsukDate"/>.</summary>
    private DateInputItemsItem BoundPart(NhsukField field, string part, string? value, int width, string? missing) => new()
    {
        Name = field.Name + "." + part,
        Id = (Id ?? field.Name) + "-" + part.ToLowerInvariant(),
        Label = part,
        Value = value,
        Width = width,
        Error = missing == part.ToLowerInvariant() ? true : null,
    };
}
