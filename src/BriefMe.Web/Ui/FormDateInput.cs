using Microsoft.AspNetCore.Components.Forms;

namespace BriefMe.Web.Ui;

/// <summary><see cref="InputDate{TValue}"/> styled as <see cref="Input"/>, for fields inside an <c>EditForm</c>.</summary>
public sealed class FormDateInput<TValue> : InputDate<TValue>
{
    protected override void OnParametersSet()
    {
        AdditionalAttributes = FormControlClasses.Prepend(AdditionalAttributes, UiClasses.Control);
        base.OnParametersSet();
    }
}
