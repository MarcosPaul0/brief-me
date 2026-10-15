using BriefMe.Infrastructure.Storage;
using BriefMe.Infrastructure.Tests.TestDoubles;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace BriefMe.Infrastructure.Tests.Storage;

public class HmacAttachmentLinkSignerTests
{
    private const string SigningKey = "infrastructure-tests-attachment-link-key-32b";

    private static readonly DateTime IssuedAt = new(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid BriefingId = Guid.NewGuid();
    private static readonly Guid AttachmentId = Guid.NewGuid();

    private static HmacAttachmentLinkSigner CreateSigner(DateTime utcNow, string signingKey = SigningKey) =>
        new(Options.Create(new AttachmentLinkSettings { SigningKey = signingKey, LifetimeMinutes = 15 }), new FixedTimeProvider(utcNow));

    [Fact]
    public void Verify_WhenTokenIsUnexpiredAndForSameIds_ReturnsTrue()
    {
        // Arrange
        var token = CreateSigner(IssuedAt).Issue(BriefingId, AttachmentId);
        var verifier = CreateSigner(IssuedAt.AddMinutes(15));

        // Act
        var valid = verifier.Verify(BriefingId, AttachmentId, token);

        // Assert
        valid.Should().BeTrue();
    }

    [Fact]
    public void Verify_WhenTokenIsExpired_ReturnsFalse()
    {
        // Arrange
        var token = CreateSigner(IssuedAt).Issue(BriefingId, AttachmentId);
        var verifier = CreateSigner(IssuedAt.AddMinutes(15).AddSeconds(1));

        // Act
        var valid = verifier.Verify(BriefingId, AttachmentId, token);

        // Assert
        valid.Should().BeFalse();
    }

    [Fact]
    public void Verify_WhenTokenWasIssuedForAnotherBriefing_ReturnsFalse()
    {
        // Arrange
        var signer = CreateSigner(IssuedAt);
        var token = signer.Issue(Guid.NewGuid(), AttachmentId);

        // Act
        var valid = signer.Verify(BriefingId, AttachmentId, token);

        // Assert
        valid.Should().BeFalse();
    }

    [Fact]
    public void Verify_WhenTokenWasIssuedForAnotherAttachment_ReturnsFalse()
    {
        // Arrange
        var signer = CreateSigner(IssuedAt);
        var token = signer.Issue(BriefingId, Guid.NewGuid());

        // Act
        var valid = signer.Verify(BriefingId, AttachmentId, token);

        // Assert
        valid.Should().BeFalse();
    }

    [Fact]
    public void Verify_WhenSignatureCharacterIsChanged_ReturnsFalse()
    {
        // Arrange
        var signer = CreateSigner(IssuedAt);
        var token = signer.Issue(BriefingId, AttachmentId);
        // First signature character: all six of its bits are payload (the last one also carries padding bits).
        var index = token.IndexOf('.') + 1;
        var tampered = token[..index] + (token[index] == 'A' ? 'B' : 'A') + token[(index + 1)..];

        // Act
        var valid = signer.Verify(BriefingId, AttachmentId, tampered);

        // Assert
        valid.Should().BeFalse();
    }

    [Fact]
    public void Verify_WhenExpiryIsExtended_ReturnsFalse()
    {
        // Arrange
        var signer = CreateSigner(IssuedAt);
        var token = signer.Issue(BriefingId, AttachmentId);
        var signature = token[(token.IndexOf('.') + 1)..];
        var extended = $"{new DateTimeOffset(IssuedAt.AddDays(30)).ToUnixTimeSeconds()}.{signature}";

        // Act
        var valid = signer.Verify(BriefingId, AttachmentId, extended);

        // Assert
        valid.Should().BeFalse();
    }

    [Fact]
    public void Verify_WhenSignedWithAnotherKey_ReturnsFalse()
    {
        // Arrange
        var token = CreateSigner(IssuedAt, "another-attachment-link-signing-key-of-32b").Issue(BriefingId, AttachmentId);

        // Act
        var valid = CreateSigner(IssuedAt).Verify(BriefingId, AttachmentId, token);

        // Assert
        valid.Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("garbage")]
    [InlineData(".abc")]
    [InlineData("-5.abc")]
    [InlineData("99999999999.not*base64url")]
    public void Verify_WhenTokenIsMalformed_ReturnsFalse(string? token)
    {
        // Arrange
        var signer = CreateSigner(IssuedAt);

        // Act
        var valid = signer.Verify(BriefingId, AttachmentId, token);

        // Assert
        valid.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("too-short-key")]
    public void Constructor_WhenSigningKeyIsTooShort_Throws(string signingKey)
    {
        // Arrange
        // (key supplied by the theory)

        // Act
        var act = () => CreateSigner(IssuedAt, signingKey);

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }
}
