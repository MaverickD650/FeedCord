using FeedCord.Common;
using FeedCord.Core.Interfaces;
using FeedCord.Core.Models;
using System.Text;
using System.Text.Json;
using System.Net.Http;

namespace FeedCord.Core
{
    public class DiscordPayloadService : IDiscordPayloadService
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private readonly Config _config;

        public DiscordPayloadService(Config config)
        {
            _config = config;
        }

        public StringContent BuildPayloadWithPost(Post post)
        {
            if (_config.MarkdownFormat)
                return GenerateMarkdown(post);

            var embed = BuildEmbed(post);
            var payload = new DiscordPayload
            {
                Username = _config.Username ?? "FeedCord",
                AvatarUrl = _config.AvatarUrl,
                Embeds = new[] { embed }
            };

            return SerializeToJsonContent(payload);
        }

        public StringContent BuildForumWithPost(Post post)
        {
            if (_config.MarkdownFormat)
                return GenerateMarkdown(post);

            var embed = BuildEmbed(post);
            var payload = new DiscordForumPayload
            {
                Content = post.Tag,
                Embeds = new[] { embed },
                ThreadName = GetThreadName(post.Title)
            };

            return SerializeToJsonContent(payload);
        }

        private DiscordEmbed BuildEmbed(Post post)
        {
            return new DiscordEmbed
            {
                Title = post.Title,
                Author = new DiscordAuthor
                {
                    Name = _config.AuthorName ?? post.Author,
                    Url = _config.AuthorUrl,
                    IconUrl = _config.AuthorIcon
                },
                Url = post.Link,
                Description = post.Description,
                Image = new DiscordImage
                {
                    Url = string.IsNullOrEmpty(post.ImageUrl) ? _config.FallbackImage : post.ImageUrl
                },
                Footer = new DiscordFooter
                {
                    Text = $"{post.Tag} - {post.PublishDate:MM/dd/yyyy h:mm tt}",
                    IconUrl = _config.FooterImage
                },
                Color = _config.Color
            };
        }

        private StringContent GenerateMarkdown(Post post)
        {
            var markdownPost = $"""
                                # {post.Title}

                                > **Published**: {post.PublishDate:MMMM dd, yyyy}
                                > **Author**: {post.Author}
                                > **Feed**: {post.Tag}

                                {post.Description}

                                [Source]({post.Link})

                                """;

            DiscordMarkdownPayload payload = new()
            {
                Content = markdownPost,
                ThreadName = _config.Forum ? GetThreadName(post.Title) : null
            };

            return SerializeToJsonContent(payload);
        }

        private static string GetThreadName(string title)
        {
            return title.Length > 100 ? title[..99] : title;
        }

        private static StringContent SerializeToJsonContent<TPayload>(TPayload payload)
        {
            var payloadJson = JsonSerializer.Serialize(payload, JsonOptions);
            return new StringContent(payloadJson, Encoding.UTF8, "application/json");
        }
    }
}
