using System.Text.RegularExpressions;

namespace FeedCord.Helpers
{
  /// <summary>
  /// Masks only critical sensitive information in log messages.
  /// Focuses on Discord webhooks and embedded credentials in URLs.
  /// </summary>
  public static partial class SensitiveDataMasker
  {
    /// <summary>
    /// Masks Discord webhook URLs - these contain the bot token and must be protected
    /// </summary>
    public static string MaskDiscordWebhook(string input)
    {
      if (string.IsNullOrEmpty(input))
        return input;

      // Mask Discord webhook URLs: https://discord.com/api/webhooks/{id}/{token}
      return DiscordWebhookRegex().Replace(input, "https://discord.com/api/webhooks/[WEBHOOK_ID]/[TOKEN]");
    }

    /// <summary>
    /// Masks embedded credentials in URLs (e.g., user:pass@host)
    /// Used to protect accidental credential exposure in feed URLs
    /// </summary>
    public static string MaskUrlCredentials(string input)
    {
      if (string.IsNullOrEmpty(input))
        return input;

      // Mask credentials: scheme://user:pass@host
      return UrlCredentialsRegex().Replace(input, "$1://[CREDENTIALS]@$4");
    }

    /// <summary>
    /// Masks an exception message by removing Discord webhooks and embedded credentials
    /// </summary>
    public static string MaskException(Exception ex)
    {
      if (ex == null)
        return string.Empty;

      var message = ex.Message;
      message = MaskDiscordWebhook(message);
      message = MaskUrlCredentials(message);

      if (!string.IsNullOrEmpty(ex.InnerException?.Message))
      {
        var innerMsg = ex.InnerException.Message;
        innerMsg = MaskDiscordWebhook(innerMsg);
        innerMsg = MaskUrlCredentials(innerMsg);
        message += " => " + innerMsg;
      }

      return message;
    }

    /// <summary>
    /// Matches Discord webhook URLs: https://discord.com/api/webhooks/{id}/{token}
    /// </summary>
    [GeneratedRegex(@"https://discord\.com/api/webhooks/\d+/[\w\-]+", RegexOptions.IgnoreCase, "en-US")]
    private static partial Regex DiscordWebhookRegex();

    /// <summary>
    /// Matches URLs with embedded credentials: scheme://user:pass@host
    /// </summary>
    [GeneratedRegex(@"(https?://)([^:/@]+):([^@/]+)@([^\s/]+)", RegexOptions.IgnoreCase, "en-US")]
    private static partial Regex UrlCredentialsRegex();
  }
}
