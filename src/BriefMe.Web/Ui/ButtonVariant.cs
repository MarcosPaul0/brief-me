namespace BriefMe.Web.Ui;

public enum ButtonVariant
{
    /// <summary><c>button-primary</c>: the one near-black filled action on a surface.</summary>
    Default,

    /// <summary>Cream-card fill, for the actions beside a primary one.</summary>
    Secondary,

    /// <summary><c>button-secondary</c>: canvas fill, hairline rule, ink label.</summary>
    Outline,

    /// <summary>No fill until hovered — low-emphasis actions inside rows and toolbars.</summary>
    Ghost,

    /// <summary><c>button-text-link</c>: underlined ink text, behaves as a button.</summary>
    Link,

    /// <summary>Destructive confirmation (cancel a briefing, ...).</summary>
    Destructive
}

public enum ButtonSize
{
    /// <summary>44px tall — the spec's touch-target height for <c>button-primary</c>.</summary>
    Default,

    /// <summary>36px, 8px corner — compact, for table rows, pagers and the top nav.</summary>
    Sm
}
