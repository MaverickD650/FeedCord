using System.Text.Json.Serialization;

namespace FeedCord.Core.Models
{
  public class DiscordEmbed
  {
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("author")]
    public DiscordAuthor? Author { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("image")]
    public DiscordImage? Image { get; set; }

    [JsonPropertyName("footer")]
    public DiscordFooter? Footer { get; set; }

    [JsonPropertyName("color")]
    public int Color { get; set; }
  }
}
