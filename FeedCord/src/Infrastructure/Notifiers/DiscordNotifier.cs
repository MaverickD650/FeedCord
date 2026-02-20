using FeedCord.Common;
using FeedCord.Core.Interfaces;
using FeedCord.Services.Interfaces;

namespace FeedCord.Infrastructure.Notifiers
{
    internal class DiscordNotifier : INotifier
    {
        private readonly ICustomHttpClient _httpClient;
        private readonly IDiscordPayloadService _discordPayloadService;
        private readonly string _webhook;
        private readonly bool _forum;
        public DiscordNotifier(Config config, ICustomHttpClient httpClient, IDiscordPayloadService discordPayloadService)
        {
            _httpClient = httpClient;
            _discordPayloadService = discordPayloadService;
            _webhook = config.DiscordWebhookUrl;
            _forum = config.Forum;
        }
        public async Task SendNotificationsAsync(List<Post> newPosts)
        {
            foreach (var post in newPosts)
            {
                try
                {
                    // Build appropriate payload based on configured channel type
                    // CustomHttpClient.PostAsyncWithFallback handles fallback to opposite type if needed
                    var primaryPayload = _forum
                        ? _discordPayloadService.BuildForumWithPost(post)
                        : _discordPayloadService.BuildPayloadWithPost(post);

                    var fallbackPayload = _forum
                        ? _discordPayloadService.BuildPayloadWithPost(post)
                        : _discordPayloadService.BuildForumWithPost(post);

                    await _httpClient.PostAsyncWithFallback(_webhook, primaryPayload, fallbackPayload, _forum);
                }
                catch (Exception ex)
                {
                    // Log failure but continue processing remaining posts
                    // CustomHttpClient logs specific Discord API errors; this catches any transport/serialization issues
                    throw new InvalidOperationException($"Failed to send notification for post: {post.Title}", ex);
                }
            }
        }
    }
}
