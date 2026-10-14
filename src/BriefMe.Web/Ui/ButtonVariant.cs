namespace BriefMe.Web.Ui;

public enum ButtonVariant
{
    /// <summary><c>button-primary</c>: the one filled blue action on a surface.</summary>
    Default,

    /// <summary>Neutral fill on the chrome grey, for the actions beside a primary one.</summary>
    Secondary,

    /// <summary><c>button-outline</c>: canvas fill, hairline rule, deep-blue label.</summary>
    Outline,

    /// <summary>No fill until hovered — low-emphasis actions inside rows and toolbars.</summary>
    Ghost,

    /// <summary><c>text-link</c>: reads as an inline link, behaves as a button.</summary>
    Link,

    /// <summary>Destructive confirmation (cancel a briefing, ...).</summary>
    Destructive
}

public enum ButtonSize
{
    /// <summary>44px tall — the spec's touch-target height for <c>button-primary</c>.</summary>
    Default,

    /// <summary>Compact, for table rows and pagers.</summary>
    Sm
}
