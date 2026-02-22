using System.Text.Json.Serialization;

namespace FeedCord.Core.Models
{
  public class DiscordFooter
  {
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("icon_url")]
    public string? IconUrl { get; set; }
  }
}
