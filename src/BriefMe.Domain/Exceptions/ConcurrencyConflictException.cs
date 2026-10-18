namespace BriefMe.Domain.Exceptions;

/// <summary>
/// The aggregate being saved was changed by someone else since it was loaded — e.g. two designers claiming
/// the same briefing at once. Raised by the unit of work so the Application layer can answer a conflict
/// without knowing how persistence detects it.
/// </summary>
public sealed class ConcurrencyConflictException(string message, Exception? innerException = null)
    : Exception(message, innerException);
