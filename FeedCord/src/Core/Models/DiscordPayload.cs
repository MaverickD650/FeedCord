using System.Text.Json.Serialization;

namespace FeedCord.Core.Models
{
    public class DiscordPayload
    {
        [JsonPropertyName("username")]
        public string? Username { get; set; }

        [JsonPropertyName("avatar_url")]
        public string? AvatarUrl { get; set; }

        [JsonPropertyName("embeds")]
        public DiscordEmbed[]? Embeds { get; set; }
    }
}
