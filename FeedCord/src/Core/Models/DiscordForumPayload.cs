using System.Text.Json.Serialization;

namespace FeedCord.Core.Models
{
    public class DiscordForumPayload
    {
        [JsonPropertyName("content")]
        public string? Content { get; set; }

        [JsonPropertyName("embeds")]
        public DiscordEmbed[]? Embeds { get; set; }

        [JsonPropertyName("thread_name")]
        public string? ThreadName { get; set; }
    }
}
