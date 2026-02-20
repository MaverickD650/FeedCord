using System.Text.Json.Serialization;

namespace FeedCord.Core.Models
{
    public class DiscordImage
    {
        [JsonPropertyName("url")]
        public string? Url { get; set; }
    }
}
