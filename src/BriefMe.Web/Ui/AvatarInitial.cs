using System.Globalization;

namespace BriefMe.Web.Ui;

/// <summary>
/// What <see cref="Avatar"/> draws when there is no image (AVT-07): the first letter of the name, and a disc
/// tone picked from it so different people get different colours.
/// </summary>
public static class AvatarInitial
{
    private const string Fallback = "?";

    /// <summary>
    /// First text element of the trimmed name, upper-cased — a whole grapheme, so "Érica" written with a
    /// combining accent still yields "É". Blank names give "?".
    /// </summary>
    public static string From(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Fallback;

        var first = StringInfo.GetNextTextElement(name.Trim());
        return first.ToUpper(CultureInfo.CurrentCulture);
    }

    /// <summary>
    /// Stable index in <c>[0, toneCount)</c> for the name. Not <see cref="string.GetHashCode()"/>, which is
    /// randomised per process — the same user must keep the same colour across circuits and restarts.
    /// </summary>
    public static int ToneIndex(string? name, int toneCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(toneCount);

        var sum = 0;
        foreach (var ch in (name ?? string.Empty).Trim())
            sum = unchecked(sum * 31 + ch);

        return (int)((uint)sum % (uint)toneCount);
    }
}
