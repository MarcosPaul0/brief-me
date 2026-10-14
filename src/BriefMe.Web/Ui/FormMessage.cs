using Microsoft.AspNetCore.Components.Forms;

namespace BriefMe.Web.Ui;

/// <summary><see cref="ValidationMessage{TValue}"/> in the field-error style, placed under its control.</summary>
public sealed class FormMessage<TValue> : ValidationMessage<TValue>
{
    protected override void OnParametersSet()
    {
        AdditionalAttributes = FormControlClasses.Prepend(AdditionalAttributes, UiClasses.FieldMessage);
        base.OnParametersSet();
    }
}
