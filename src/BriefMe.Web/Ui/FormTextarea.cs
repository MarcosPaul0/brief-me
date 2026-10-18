using Microsoft.AspNetCore.Components.Forms;

namespace BriefMe.Web.Ui;

/// <summary><see cref="InputTextArea"/> styled as <see cref="Textarea"/>, for fields inside an <c>EditForm</c>.</summary>
public sealed class FormTextarea : InputTextArea
{
    protected override void OnParametersSet()
    {
        AdditionalAttributes = FormControlClasses.Prepend(AdditionalAttributes, UiClasses.Control + " min-h-20");
        base.OnParametersSet();
    }
}
