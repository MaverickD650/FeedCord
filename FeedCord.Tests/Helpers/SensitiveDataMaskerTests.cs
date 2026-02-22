using Xunit;
using FeedCord.Helpers;

namespace FeedCord.Tests.Helpers;

public class SensitiveDataMaskerTests
{
  #region MaskDiscordWebhook Tests

  [Fact]
  public void MaskDiscordWebhook_ValidWebhookUrl_MasksCorrectly()
  {
    // Arrange
    var input = "https://discord.com/api/webhooks/123456789/abcdefghijklmnopqrst";

    // Act
    var result = SensitiveDataMasker.MaskDiscordWebhook(input);

    // Assert
    Assert.Equal("https://discord.com/api/webhooks/[WEBHOOK_ID]/[TOKEN]", result);
    Assert.DoesNotContain("123456789", result);
    Assert.DoesNotContain("abcdefghijklmnopqrst", result);
  }

  [Fact]
  public void MaskDiscordWebhook_MultipleWebhookUrlsInText_MasksAll()
  {
    // Arrange
    var input = "First: https://discord.com/api/webhooks/111/aaa Second: https://discord.com/api/webhooks/222/bbb";

    // Act
    var result = SensitiveDataMasker.MaskDiscordWebhook(input);

    // Assert
    Assert.Contains("[WEBHOOK_ID]/[TOKEN]", result);
    Assert.DoesNotContain("111", result);
    Assert.DoesNotContain("222", result);
    Assert.DoesNotContain("aaa", result);
    Assert.DoesNotContain("bbb", result);
  }

  [Fact]
  public void MaskDiscordWebhook_NullInput_ReturnsNull()
  {
    // Arrange
    string? input = null;

    // Act
    var result = SensitiveDataMasker.MaskDiscordWebhook(input!);

    // Assert
    Assert.Null(result);
  }

  [Fact]
  public void MaskDiscordWebhook_EmptyString_ReturnsEmpty()
  {
    // Arrange
    var input = "";

    // Act
    var result = SensitiveDataMasker.MaskDiscordWebhook(input);

    // Assert
    Assert.Empty(result);
  }

  [Fact]
  public void MaskDiscordWebhook_NoWebhookUrl_ReturnsUnchanged()
  {
    // Arrange
    var input = "This is a regular message with no sensitive data";

    // Act
    var result = SensitiveDataMasker.MaskDiscordWebhook(input);

    // Assert
    Assert.Equal(input, result);
  }

  [Fact]
  public void MaskDiscordWebhook_PartialWebhookUrl_DoesNotMask()
  {
    // Arrange
    var input = "https://discord.com/api/webhooks/incomplete";

    // Act
    var result = SensitiveDataMasker.MaskDiscordWebhook(input);

    // Assert
    // Should not mask incomplete URLs
    Assert.Equal(input, result);
  }

  [Fact]
  public void MaskDiscordWebhook_CaseInsensitive_MasksCorrectly()
  {
    // Arrange
    var input = "HTTPS://DISCORD.COM/API/WEBHOOKS/123456789/abcdefghijklmnopqrst";

    // Act
    var result = SensitiveDataMasker.MaskDiscordWebhook(input);

    // Assert
    Assert.Equal("https://discord.com/api/webhooks/[WEBHOOK_ID]/[TOKEN]", result);
  }

  [Fact]
  public void MaskDiscordWebhook_HyphensInToken_MasksCorrectly()
  {
    // Arrange
    var input = "https://discord.com/api/webhooks/123456789/abc-def-ghi-jkl-mno";

    // Act
    var result = SensitiveDataMasker.MaskDiscordWebhook(input);

    // Assert
    Assert.Equal("https://discord.com/api/webhooks/[WEBHOOK_ID]/[TOKEN]", result);
  }

  [Fact]
  public void MaskDiscordWebhook_UnderscoresInToken_MasksCorrectly()
  {
    // Arrange
    var input = "https://discord.com/api/webhooks/123456789/abc_def_ghi_jkl_mno";

    // Act
    var result = SensitiveDataMasker.MaskDiscordWebhook(input);

    // Assert
    Assert.Equal("https://discord.com/api/webhooks/[WEBHOOK_ID]/[TOKEN]", result);
  }

  #endregion

  #region MaskUrlCredentials Tests

  [Fact]
  public void MaskUrlCredentials_ValidCredentialsInUrl_MasksCorrectly()
  {
    // Arrange
    var input = "http://user:password@example.com/path";

    // Act
    var result = SensitiveDataMasker.MaskUrlCredentials(input);

    // Assert
    Assert.Contains("[CREDENTIALS]", result);
    Assert.DoesNotContain("user", result);
    Assert.DoesNotContain("password", result);
    Assert.Contains("example.com", result);
  }

  [Fact]
  public void MaskUrlCredentials_HttpsUrl_MasksCorrectly()
  {
    // Arrange
    var input = "https://admin:secret123@feed.example.com/rss";

    // Act
    var result = SensitiveDataMasker.MaskUrlCredentials(input);

    // Assert
    Assert.Contains("[CREDENTIALS]", result);
    Assert.DoesNotContain("admin", result);
    Assert.DoesNotContain("secret123", result);
    Assert.Contains("feed.example.com", result);
  }

  [Fact]
  public void MaskUrlCredentials_MultipleUrlsWithCredentials_MasksAll()
  {
    // Arrange
    var input = "First: http://user1:pass1@example.com Second: https://user2:pass2@other.com";

    // Act
    var result = SensitiveDataMasker.MaskUrlCredentials(input);

    // Assert
    Assert.DoesNotContain("user1", result);
    Assert.DoesNotContain("user2", result);
    Assert.DoesNotContain("pass1", result);
    Assert.DoesNotContain("pass2", result);
    var count = result.Split("[CREDENTIALS]").Length - 1;
    Assert.Equal(2, count);
  }

  [Fact]
  public void MaskUrlCredentials_NoCredentials_ReturnsUnchanged()
  {
    // Arrange
    var input = "http://example.com/path";

    // Act
    var result = SensitiveDataMasker.MaskUrlCredentials(input);

    // Assert
    Assert.Equal(input, result);
  }

  [Fact]
  public void MaskUrlCredentials_NullInput_ReturnsNull()
  {
    // Arrange
    string? input = null;

    // Act
    var result = SensitiveDataMasker.MaskUrlCredentials(input!);

    // Assert
    Assert.Null(result);
  }

  [Fact]
  public void MaskUrlCredentials_EmptyString_ReturnsEmpty()
  {
    // Arrange
    var input = "";

    // Act
    var result = SensitiveDataMasker.MaskUrlCredentials(input);

    // Assert
    Assert.Empty(result);
  }

  [Fact]
  public void MaskUrlCredentials_SpecialCharactersInPassword_MasksCorrectly()
  {
    // Arrange
    var input = "http://user:p@ss!word#123@example.com/path";

    // Act
    var result = SensitiveDataMasker.MaskUrlCredentials(input);

    // Assert
    Assert.Contains("[CREDENTIALS]", result);
    Assert.DoesNotContain("p@ss", result);
  }

  [Fact]
  public void MaskUrlCredentials_CaseInsensitive_MasksCorrectly()
  {
    // Arrange
    var input = "HTTPS://USER:PASS@EXAMPLE.COM";

    // Act
    var result = SensitiveDataMasker.MaskUrlCredentials(input);

    // Assert
    Assert.Contains("[CREDENTIALS]", result);
  }

  #endregion

  #region MaskException Tests

  [Fact]
  public void MaskException_ExceptionWithWebhook_MasksWebhook()
  {
    // Arrange
    var exception = new Exception("Failed to send to https://discord.com/api/webhooks/123/abc");

    // Act
    var result = SensitiveDataMasker.MaskException(exception);

    // Assert
    Assert.Contains("[WEBHOOK_ID]/[TOKEN]", result);
    Assert.DoesNotContain("123", result);
    Assert.DoesNotContain("abc", result);
  }

  [Fact]
  public void MaskException_ExceptionWithCredentials_MasksCredentials()
  {
    // Arrange
    var exception = new Exception("Connection failed: http://user:pass@example.com");

    // Act
    var result = SensitiveDataMasker.MaskException(exception);

    // Assert
    Assert.Contains("[CREDENTIALS]", result);
    Assert.DoesNotContain("user:pass", result);
  }

  [Fact]
  public void MaskException_ExceptionWithBothSensitiveData_MasksBoth()
  {
    // Arrange
    var exception = new Exception(
        "Failed to post to https://discord.com/api/webhooks/456/xyz with credentials http://user:pass@feed.com");

    // Act
    var result = SensitiveDataMasker.MaskException(exception);

    // Assert
    Assert.Contains("[WEBHOOK_ID]/[TOKEN]", result);
    Assert.Contains("[CREDENTIALS]", result);
    Assert.DoesNotContain("456", result);
    Assert.DoesNotContain("xyz", result);
    Assert.DoesNotContain("user:pass", result);
  }

  [Fact]
  public void MaskException_ExceptionWithInnerException_MasksBoth()
  {
    // Arrange
    var innerException = new Exception("Inner: https://discord.com/api/webhooks/123/abc");
    var exception = new Exception("Outer: http://user:pass@example.com", innerException);

    // Act
    var result = SensitiveDataMasker.MaskException(exception);

    // Assert
    Assert.Contains("[CREDENTIALS]", result);
    Assert.Contains("[WEBHOOK_ID]/[TOKEN]", result);
    Assert.DoesNotContain("user:pass", result);
    Assert.Contains("=>", result);  // Inner exception separator
  }

  [Fact]
  public void MaskException_NullException_ReturnsEmpty()
  {
    // Arrange
    Exception? exception = null;

    // Act
    var result = SensitiveDataMasker.MaskException(exception!);

    // Assert
    Assert.Empty(result);
  }

  [Fact]
  public void MaskException_ExceptionWithNoSensitiveData_ReturnsMessageUnchanged()
  {
    // Arrange
    var exception = new Exception("Simple error message");

    // Act
    var result = SensitiveDataMasker.MaskException(exception);

    // Assert
    Assert.Equal("Simple error message", result);
  }

  [Fact]
  public void MaskException_ExceptionWithInnerButNoSensitiveData_ReturnsBothMessages()
  {
    // Arrange
    var innerException = new Exception("Inner error");
    var exception = new Exception("Outer error", innerException);

    // Act
    var result = SensitiveDataMasker.MaskException(exception);

    // Assert
    Assert.Contains("Outer error", result);
    Assert.Contains("Inner error", result);
    Assert.Contains("=>", result);
  }

  [Fact]
  public void MaskException_MultipleWebhooksInException_MasksAll()
  {
    // Arrange
    var exception = new Exception(
        "First webhook: https://discord.com/api/webhooks/111/aaa, Second: https://discord.com/api/webhooks/222/bbb");

    // Act
    var result = SensitiveDataMasker.MaskException(exception);

    // Assert
    Assert.DoesNotContain("111", result);
    Assert.DoesNotContain("222", result);
    var count = result.Split("[WEBHOOK_ID]/[TOKEN]").Length - 1;
    Assert.Equal(2, count);
  }

  #endregion
}
