using System.Buffers.Text;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using BriefMe.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace BriefMe.Infrastructure.Storage;

/// <summary>
/// Token = <c>{expiresAtUnixSeconds}.{base64url(HMAC-SHA256(key, "{briefingId:N}:{attachmentId:N}:{expiresAt}"))}</c>.
/// Stateless: nothing is stored, so a token cannot be revoked early — the short lifetime is the bound.
/// </summary>
public sealed class HmacAttachmentLinkSigner : IAttachmentLinkSigner
{
    private readonly byte[] _key;
    private readonly TimeSpan _lifetime;
    private readonly TimeProvider _timeProvider;

    public HmacAttachmentLinkSigner(IOptions<AttachmentLinkSettings> settings, TimeProvider timeProvider)
    {
        _key = Encoding.UTF8.GetBytes(settings.Value.SigningKey);
        if (_key.Length < AttachmentLinkSettings.MinimumKeyBytes)
        {
            // A missing key must stop the feature loudly, never produce links anyone could forge.
            throw new InvalidOperationException(
                $"{AttachmentLinkSettings.SectionName}:{nameof(AttachmentLinkSettings.SigningKey)} must be at least " +
                $"{AttachmentLinkSettings.MinimumKeyBytes} bytes.");
        }

        _lifetime = TimeSpan.FromMinutes(settings.Value.LifetimeMinutes);
        _timeProvider = timeProvider;
    }

    public string Issue(Guid briefingId, Guid attachmentId)
    {
        var expiresAt = _timeProvider.GetUtcNow().Add(_lifetime).ToUnixTimeSeconds();
        var signature = Sign(briefingId, attachmentId, expiresAt);

        return $"{expiresAt.ToString(CultureInfo.InvariantCulture)}.{Base64Url.EncodeToString(signature)}";
    }

    public bool Verify(Guid briefingId, Guid attachmentId, string? token)
    {
        if (string.IsNullOrEmpty(token))
            return false;

        var separator = token.IndexOf('.');
        if (separator <= 0)
            return false;

        if (!long.TryParse(token.AsSpan(0, separator), NumberStyles.None, CultureInfo.InvariantCulture, out var expiresAt))
            return false;

        if (_timeProvider.GetUtcNow().ToUnixTimeSeconds() > expiresAt)
            return false;

        byte[] presented;
        try
        {
            presented = Base64Url.DecodeFromChars(token.AsSpan(separator + 1));
        }
        catch (FormatException)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(presented, Sign(briefingId, attachmentId, expiresAt));
    }

    private byte[] Sign(Guid briefingId, Guid attachmentId, long expiresAt)
    {
        var payload = Encoding.UTF8.GetBytes(
            string.Create(CultureInfo.InvariantCulture, $"{briefingId:N}:{attachmentId:N}:{expiresAt}"));

        return HMACSHA256.HashData(_key, payload);
    }
}
