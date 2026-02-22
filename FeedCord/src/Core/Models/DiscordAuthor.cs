using System.Text.Json.Serialization;

namespace FeedCord.Core.Models
{
  public class DiscordAuthor
  {
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("icon_url")]
    public string? IconUrl { get; set; }
  }
}
