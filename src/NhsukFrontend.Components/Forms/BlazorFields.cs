using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.AspNetCore.Components.Forms;

namespace NhsukFrontend.Components.Forms;

/// <summary>Builds an <see cref="NhsukField"/> from a Blazor <c>For="() => Model.Property"</c> expression.</summary>
public static class BlazorFields
{
    /// <summary>
    /// The form name is the member path without the component itself, for example <c>Model.Postcode</c>,
    /// which is what <c>[SupplyParameterFromForm]</c> binds in static server rendering.
    /// </summary>
    public static NhsukField? From(Expression<Func<object?>>? expression, EditContext? editContext)
    {
        if (expression is null) return null;
        var (name, owner, member) = Parse(expression);
        var value = member switch
        {
            PropertyInfo p when owner is not null => p.GetValue(owner),
            FieldInfo f when owner is not null => f.GetValue(owner),
            _ => null,
        };
        var (single, values, date) = NhsukField.Describe(value);
        var error = owner is null || editContext is null
            ? null
            : editContext.GetValidationMessages(new FieldIdentifier(owner, member.Name)).FirstOrDefault();

        return new NhsukField
        {
            Name = name,
            Label = member.GetCustomAttribute<DisplayAttribute>()?.GetName() ?? member.Name,
            Value = single,
            Values = values,
            Date = date,
            Error = error,
        };
    }

    /// <summary>Returns the form name, the object that owns the final member, and that member.</summary>
    public static (string Name, object? Owner, MemberInfo Member) Parse(LambdaExpression expression)
    {
        var body = expression.Body;
        while (body is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } convert) body = convert.Operand;
        if (body is not MemberExpression member)
            throw new ArgumentException($"'For' must be a property or field, like () => Model.Postcode. Got: {expression.Body}", nameof(expression));

        var parts = new List<string>();
        Expression? node = member;
        // Walk back to the component (or closure) instance, a constant, which is not part of the name.
        while (node is MemberExpression m)
        {
            parts.Insert(0, m.Member.Name);
            node = m.Expression;
        }
        var owner = member.Expression is null ? null : Evaluate(member.Expression);
        return (string.Join('.', parts), owner, member.Member);
    }

    private static object? Evaluate(Expression expression) => expression switch
    {
        ConstantExpression c => c.Value,
        MemberExpression { Member: FieldInfo f } m => f.GetValue(m.Expression is null ? null : Evaluate(m.Expression)),
        MemberExpression { Member: PropertyInfo p } m => p.GetValue(m.Expression is null ? null : Evaluate(m.Expression)),
        _ => Expression.Lambda(expression).Compile().DynamicInvoke(),
    };
}
