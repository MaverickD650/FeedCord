using Xunit;
using FeedCord.Core;
using FeedCord.Common;
using System.Text.Json;

namespace FeedCord.Tests.Core;

public class DiscordPayloadServiceTests
{
    private Config CreateTestConfig(
        bool isMarkdownFormat = false,
        bool isForum = false,
        string? username = null,
        string? avatarUrl = null,
        string? authorName = null,
        string? authorUrl = null,
        string? authorIcon = null,
        string? fallbackImage = null,
        string? footerImage = null,
        int color = 0)
    {
        return new Config
        {
            Id = "TestFeed",
            RssUrls = new string[] { },
            YoutubeUrls = new string[] { },
            DiscordWebhookUrl = "https://discord.com/api/webhooks/123/abc",
            RssCheckIntervalMinutes = 30,
            DescriptionLimit = 250,
            Forum = isForum,
            MarkdownFormat = isMarkdownFormat,
            PersistenceOnShutdown = false,
            Username = username,
            AvatarUrl = avatarUrl,
            AuthorName = authorName,
            AuthorUrl = authorUrl,
            AuthorIcon = authorIcon,
            FallbackImage = fallbackImage,
            FooterImage = footerImage,
            Color = color
        };
    }

    private Post CreateTestPost(
        string title = "Test Post",
        string description = "Test description",
        string imageUrl = "http://example.com/image.jpg",
        string link = "http://example.com/post",
        string tag = "test",
        string author = "Test Author")
    {
        return new Post(
            Title: title,
            ImageUrl: imageUrl,
            Description: description,
            Link: link,
            Tag: tag,
            PublishDate: new DateTime(2026, 2, 20, 10, 30, 0),
            Author: author
        );
    }

    [Fact]
    public void BuildPayloadWithPost_CreatesValidJson()
    {
        // Arrange
        var config = CreateTestConfig(username: "TestBot");
        var service = new DiscordPayloadService(config);
        var post = CreateTestPost();

        // Act
        var result = service.BuildPayloadWithPost(post);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("application/json", result.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task BuildPayloadWithPost_IncludesUsername()
    {
        // Arrange
        var config = CreateTestConfig(username: "CustomBot");
        var service = new DiscordPayloadService(config);
        var post = CreateTestPost();

        // Act
        var content = service.BuildPayloadWithPost(post);
        var jsonString = await content.ReadAsStringAsync();

        // Assert
        Assert.Contains("\"username\":\"CustomBot\"", jsonString);
    }

    [Fact]
    public async Task BuildPayloadWithPost_DefaultsUsernameToFeedCord()
    {
        // Arrange
        var config = CreateTestConfig(username: null);
        var service = new DiscordPayloadService(config);
        var post = CreateTestPost();

        // Act
        var content = service.BuildPayloadWithPost(post);
        var jsonString = await content.ReadAsStringAsync();

        // Assert
        Assert.Contains("FeedCord", jsonString);
    }

    [Fact]
    public async Task BuildPayloadWithPost_IncludesEmbedTitle()
    {
        // Arrange
        var config = CreateTestConfig();
        var service = new DiscordPayloadService(config);
        var post = CreateTestPost(title: "Breaking News Title");

        // Act
        var content = service.BuildPayloadWithPost(post);
        var jsonString = await content.ReadAsStringAsync();

        // Assert
        Assert.Contains("Breaking News Title", jsonString);
    }

    [Fact]
    public async Task BuildPayloadWithPost_IncludesEmbedDescription()
    {
        // Arrange
        var config = CreateTestConfig();
        var service = new DiscordPayloadService(config);
        var post = CreateTestPost(description: "Detailed description text");

        // Act
        var content = service.BuildPayloadWithPost(post);
        var jsonString = await content.ReadAsStringAsync();

        // Assert
        Assert.Contains("Detailed description text", jsonString);
    }

    [Fact]
    public async Task BuildPayloadWithPost_IncludesPostLink()
    {
        // Arrange
        var config = CreateTestConfig();
        var service = new DiscordPayloadService(config);
        var post = CreateTestPost(link: "http://example.com/article/123");

        // Act
        var content = service.BuildPayloadWithPost(post);
        var jsonString = await content.ReadAsStringAsync();

        // Assert
        Assert.Contains("http://example.com/article/123", jsonString);
    }

    [Fact]
    public async Task BuildPayloadWithPost_IncludesAuthor()
    {
        // Arrange
        var config = CreateTestConfig();
        var service = new DiscordPayloadService(config);
        var post = CreateTestPost(author: "John Doe");

        // Act
        var content = service.BuildPayloadWithPost(post);
        var jsonString = await content.ReadAsStringAsync();

        // Assert
        Assert.Contains("John Doe", jsonString);
    }

    [Fact]
    public async Task BuildPayloadWithPost_IncludesImageUrl()
    {
        // Arrange
        var config = CreateTestConfig();
        var service = new DiscordPayloadService(config);
        var post = CreateTestPost(imageUrl: "http://example.com/image.jpg");

        // Act
        var content = service.BuildPayloadWithPost(post);
        var jsonString = await content.ReadAsStringAsync();

        // Assert
        Assert.Contains("http://example.com/image.jpg", jsonString);
    }

    [Fact]
    public async Task BuildPayloadWithPost_UsesFallbackImageWhenPostImageEmpty()
    {
        // Arrange
        var config = CreateTestConfig(fallbackImage: "http://fallback.example.com/default.jpg");
        var service = new DiscordPayloadService(config);
        var post = CreateTestPost(imageUrl: "");

        // Act
        var content = service.BuildPayloadWithPost(post);
        var jsonString = await content.ReadAsStringAsync();

        // Assert
        Assert.Contains("http://fallback.example.com/default.jpg", jsonString);
    }

    [Fact]
    public async Task BuildPayloadWithPost_IncludesFooterImage()
    {
        // Arrange
        var config = CreateTestConfig(footerImage: "http://example.com/footer.png");
        var service = new DiscordPayloadService(config);
        var post = CreateTestPost();

        // Act
        var content = service.BuildPayloadWithPost(post);
        var jsonString = await content.ReadAsStringAsync();

        // Assert
        Assert.Contains("http://example.com/footer.png", jsonString);
    }

    [Fact]
    public async Task BuildPayloadWithPost_IncludesColor()
    {
        // Arrange
        var config = CreateTestConfig(color: 16711680);  // Red color
        var service = new DiscordPayloadService(config);
        var post = CreateTestPost();

        // Act
        var content = service.BuildPayloadWithPost(post);
        var jsonString = await content.ReadAsStringAsync();

        // Assert
        Assert.Contains("16711680", jsonString);
    }

    [Fact]
    public async Task BuildPayloadWithPost_IncludesPublishDate()
    {
        // Arrange
        var config = CreateTestConfig();
        var service = new DiscordPayloadService(config);
        var post = CreateTestPost();

        // Act
        var content = service.BuildPayloadWithPost(post);
        var jsonString = await content.ReadAsStringAsync();

        // Assert
        Assert.Contains("02/20/2026", jsonString);  // MM/dd/yyyy format
    }

    [Fact]
    public void BuildPayloadWithPost_ReturnsUtf8Encoding()
    {
        // Arrange
        var config = CreateTestConfig();
        var service = new DiscordPayloadService(config);
        var post = CreateTestPost();

        // Act
        var result = service.BuildPayloadWithPost(post);

        // Assert
        Assert.NotNull(result.Headers.ContentEncoding);
        // UTF-8 is the default for application/json
        Assert.Equal("utf-8", result.Headers.ContentType?.CharSet);
    }

    [Fact]
    public void BuildForumWithPost_CreatesValidJson()
    {
        // Arrange
        var config = CreateTestConfig(isForum: true);
        var service = new DiscordPayloadService(config);
        var post = CreateTestPost();

        // Act
        var result = service.BuildForumWithPost(post);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("application/json", result.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task BuildForumWithPost_IncludesPostTag()
    {
        // Arrange
        var config = CreateTestConfig(isForum: true);
        var service = new DiscordPayloadService(config);
        var post = CreateTestPost(tag: "forum-tag");

        // Act
        var content = service.BuildForumWithPost(post);
        var jsonString = await content.ReadAsStringAsync();

        // Assert
        Assert.Contains("forum-tag", jsonString);
    }

    [Fact]
    public async Task BuildForumWithPost_ThreadNameUsesPost_TitleWhenShort()
    {
        // Arrange
        var config = CreateTestConfig(isForum: true);
        var service = new DiscordPayloadService(config);
        var post = CreateTestPost(title: "Short Title");

        // Act
        var content = service.BuildForumWithPost(post);
        var jsonString = await content.ReadAsStringAsync();

        // Assert
        Assert.Contains("Short Title", jsonString);
    }

    [Fact]
    public async Task BuildForumWithPost_ThreadNameTruncatedTo99Chars()
    {
        // Arrange
        var config = CreateTestConfig(isForum: true);
        var service = new DiscordPayloadService(config);
        var longTitle = new string('a', 150);
        var post = CreateTestPost(title: longTitle);

        // Act
        var content = service.BuildForumWithPost(post);
        var jsonString = await content.ReadAsStringAsync();

        // Assert
        // Parse JSON and check thread_name is exactly 99 characters
        using var doc = JsonDocument.Parse(jsonString);
        var root = doc.RootElement;
        Assert.True(root.TryGetProperty("thread_name", out var threadNameProp));
        Assert.Equal(99, threadNameProp.GetString()?.Length);
        Assert.Equal(new string('a', 99), threadNameProp.GetString());
    }

    [Fact]
    public async Task BuildPayloadWithPost_WithMarkdownFormat_ReturnsMarkdown()
    {
        // Arrange
        var config = CreateTestConfig(isMarkdownFormat: true);
        var service = new DiscordPayloadService(config);
        var post = CreateTestPost(title: "Test Post", description: "Test description");

        // Act
        var content = service.BuildPayloadWithPost(post);
        var jsonString = await content.ReadAsStringAsync();

        // Assert
        Assert.Contains("# Test Post", jsonString);  // Markdown header
        Assert.Contains("Test description", jsonString);
        Assert.Contains("[Source]", jsonString);  // Markdown link
    }

    [Fact]
    public async Task BuildForumWithPost_WithMarkdownFormat_ReturnsMarkdown()
    {
        // Arrange
        var config = CreateTestConfig(isMarkdownFormat: true, isForum: true);
        var service = new DiscordPayloadService(config);
        var post = CreateTestPost();

        // Act
        var content = service.BuildForumWithPost(post);
        var jsonString = await content.ReadAsStringAsync();

        // Assert
        Assert.Contains("# Test Post", jsonString);
    }

    [Fact]
    public async Task BuildPayloadWithPost_WithMarkdownFormatAndNonForum_OmitsThreadName()
    {
        // Arrange
        var config = CreateTestConfig(isMarkdownFormat: true, isForum: false);
        var service = new DiscordPayloadService(config);
        var post = CreateTestPost(title: "Markdown Non Forum");

        // Act
        var content = service.BuildPayloadWithPost(post);
        var jsonString = await content.ReadAsStringAsync();

        // Assert
        using var jsonDoc = JsonDocument.Parse(jsonString);
        var root = jsonDoc.RootElement;
        Assert.True(root.TryGetProperty("content", out _));
        Assert.False(root.TryGetProperty("thread_name", out _));
    }

    [Fact]
    public async Task BuildPayloadWithPost_WithCustomAuthorName_OverridesPostAuthor()
    {
        // Arrange
        var config = CreateTestConfig(authorName: "Custom Author");
        var service = new DiscordPayloadService(config);
        var post = CreateTestPost(author: "Original Author");

        // Act
        var content = service.BuildPayloadWithPost(post);
        var jsonString = await content.ReadAsStringAsync();

        // Assert
        Assert.Contains("Custom Author", jsonString);
        Assert.DoesNotContain("Original Author", jsonString);
    }

    [Fact]
    public async Task BuildPayloadWithPost_WithCustomAuthorUrl_IncludesUrl()
    {
        // Arrange
        var config = CreateTestConfig(authorUrl: "http://author.example.com");
        var service = new DiscordPayloadService(config);
        var post = CreateTestPost();

        // Act
        var content = service.BuildPayloadWithPost(post);
        var jsonString = await content.ReadAsStringAsync();

        // Assert
        Assert.Contains("http://author.example.com", jsonString);
    }

    [Fact]
    public async Task BuildPayloadWithPost_WithCustomAuthorIcon_IncludesIcon()
    {
        // Arrange
        var config = CreateTestConfig(authorIcon: "http://example.com/author.png");
        var service = new DiscordPayloadService(config);
        var post = CreateTestPost();

        // Act
        var content = service.BuildPayloadWithPost(post);
        var jsonString = await content.ReadAsStringAsync();

        // Assert
        Assert.Contains("http://example.com/author.png", jsonString);
    }

    [Fact]
    public async Task BuildPayloadWithPost_WithAvatarUrl_IncludesAvatar()
    {
        // Arrange
        var config = CreateTestConfig(avatarUrl: "http://example.com/avatar.png");
        var service = new DiscordPayloadService(config);
        var post = CreateTestPost();

        // Act
        var content = service.BuildPayloadWithPost(post);
        var jsonString = await content.ReadAsStringAsync();

        // Assert
        Assert.Contains("http://example.com/avatar.png", jsonString);
    }

    [Fact]
    public async Task BuildPayloadWithPost_WithTagInFooter_IncludesTag()
    {
        // Arrange
        var config = CreateTestConfig();
        var service = new DiscordPayloadService(config);
        var post = CreateTestPost(tag: "breaking-news");

        // Act
        var content = service.BuildPayloadWithPost(post);
        var jsonString = await content.ReadAsStringAsync();

        // Assert
        Assert.Contains("breaking-news", jsonString);
    }

    [Fact]
    public async Task BuildPayloadWithPost_ProducesValidJsonStructure()
    {
        // Arrange
        var config = CreateTestConfig(username: "TestBot");
        var service = new DiscordPayloadService(config);
        var post = CreateTestPost();

        // Act
        var content = service.BuildPayloadWithPost(post);
        var jsonString = await content.ReadAsStringAsync();

        // Assert - verify it's valid JSON
        var jsonDoc = JsonDocument.Parse(jsonString);
        Assert.NotNull(jsonDoc);
        var root = jsonDoc.RootElement;
        Assert.True(root.TryGetProperty("username", out _));
        Assert.True(root.TryGetProperty("embeds", out _));
    }

    [Fact]
    public async Task BuildForumWithPost_ProducesValidJsonStructure()
    {
        // Arrange
        var config = CreateTestConfig(isForum: true);
        var service = new DiscordPayloadService(config);
        var post = CreateTestPost();

        // Act
        var content = service.BuildForumWithPost(post);
        var jsonString = await content.ReadAsStringAsync();

        // Assert
        var jsonDoc = JsonDocument.Parse(jsonString);
        Assert.NotNull(jsonDoc);
        var root = jsonDoc.RootElement;
        Assert.True(root.TryGetProperty("content", out _));
        Assert.True(root.TryGetProperty("embeds", out _));
        Assert.True(root.TryGetProperty("thread_name", out _));
    }

    [Fact]
    public async Task BuildPayloadWithPost_MarkdownFormat_ProducesValidJsonStructure()
    {
        // Arrange
        var config = CreateTestConfig(isMarkdownFormat: true);
        var service = new DiscordPayloadService(config);
        var post = CreateTestPost();

        // Act
        var content = service.BuildPayloadWithPost(post);
        var jsonString = await content.ReadAsStringAsync();

        // Assert
        var jsonDoc = JsonDocument.Parse(jsonString);
        Assert.NotNull(jsonDoc);
        var root = jsonDoc.RootElement;
        Assert.True(root.TryGetProperty("content", out _));
    }
}
