namespace BriefMe.Web.Ui;

/// <summary>
/// Surface of a <see cref="Card"/> — DESIGN-clay.md's six-colour feature-card palette plus the plain content card.
/// A saturated tone is a highlight, not a default: never put two cards of the same tone side by side.
/// </summary>
public enum CardTone
{
    /// <summary><c>product-mockup-card</c>: canvas fill, hairline rule. Every form, list and record.</summary>
    Default,

    /// <summary><c>feature-card-cream</c>: the low-key highlight.</summary>
    Cream,

    /// <summary><c>feature-card-pink</c>: white text.</summary>
    Pink,

    /// <summary><c>feature-card-teal</c>: white text; the "featured" signal.</summary>
    Teal,

    /// <summary><c>feature-card-lavender</c>: ink text.</summary>
    Lavender,

    /// <summary><c>feature-card-peach</c>: ink text.</summary>
    Peach,

    /// <summary><c>feature-card-ochre</c>: ink text.</summary>
    Ochre
}
