using Microsoft.AspNetCore.Components.Forms;

namespace BriefMe.Web.Ui;

/// <summary><see cref="InputSelect{TValue}"/> styled as <see cref="NativeSelect"/>, for fields inside an <c>EditForm</c>.</summary>
public sealed class FormSelect<TValue> : InputSelect<TValue>
{
    protected override void OnParametersSet()
    {
        AdditionalAttributes = FormControlClasses.Prepend(AdditionalAttributes, UiClasses.Control + " pr-8");
        base.OnParametersSet();
    }
}
