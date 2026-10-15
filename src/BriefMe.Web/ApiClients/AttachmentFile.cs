namespace BriefMe.Web.ApiClients;

/// <summary>
/// A file picked in the browser, already read into memory. Bytes rather than a stream: the refresh-on-401 retry
/// rebuilds the multipart body, so it must be producible twice (<c>AuthenticatedApiClient</c>).
/// <paramref name="ContentType"/> is only the browser's guess — the API detects the real type from the bytes.
/// </summary>
public sealed record AttachmentFile(string FileName, string ContentType, byte[] Content);
