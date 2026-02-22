using System.Text.Json.Serialization;

namespace FeedCord.Core.Models
{
  /// <summary>
  /// Payload for markdown-formatted messages sent to Discord
  /// </summary>
  public class DiscordMarkdownPayload
  {
    [JsonPropertyName("content")]
    public string? Content { get; set; }

    [JsonPropertyName("thread_name")]
    public string? ThreadName { get; set; }
  }
}
