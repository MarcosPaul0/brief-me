using Microsoft.AspNetCore.Components.Forms;

namespace BriefMe.Web.Ui;

/// <summary><see cref="InputText"/> styled as <see cref="Input"/>, for fields inside an <c>EditForm</c>.</summary>
public sealed class FormInput : InputText
{
    protected override void OnParametersSet()
    {
        AdditionalAttributes = FormControlClasses.Prepend(AdditionalAttributes, UiClasses.Control);
        base.OnParametersSet();
    }
}
