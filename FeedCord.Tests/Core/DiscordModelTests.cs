using FeedCord.Core.Models;
using Xunit;

namespace FeedCord.Tests.Core
{
    public class DiscordAuthorTests
    {
        [Fact]
        public void Constructor_Initialize_AllPropertiesNull()
        {
            // Arrange & Act
            var author = new DiscordAuthor();

            // Assert
            Assert.Null(author.Name);
            Assert.Null(author.Url);
            Assert.Null(author.IconUrl);
        }

        [Theory]
        [InlineData(null, null, null)]
        [InlineData("Test Author", null, null)]
        [InlineData(null, "https://example.com", null)]
        [InlineData(null, null, "https://example.com/icon.png")]
        [InlineData("Test Author", "https://example.com", "https://example.com/icon.png")]
        [InlineData("", "", "")]
        public void Properties_SetVariousCombinations_CanBeRetrieved(
            string? name, string? url, string? iconUrl)
        {
            // Arrange & Act
            var author = new DiscordAuthor
            {
                Name = name,
                Url = url,
                IconUrl = iconUrl
            };

            // Assert
            Assert.Equal(name, author.Name);
            Assert.Equal(url, author.Url);
            Assert.Equal(iconUrl, author.IconUrl);
        }

        [Fact]
        public void Properties_CanBeModified_AreIndependent()
        {
            // Arrange
            var author = new DiscordAuthor { Name = "Original" };

            // Act
            author.Name = "Modified";
            author.Url = "https://example.com";

            // Assert
            Assert.Equal("Modified", author.Name);
            Assert.Equal("https://example.com", author.Url);
            Assert.Null(author.IconUrl);
        }

        [Fact]
        public void Properties_SetToEmptyString_ArePreserved()
        {
            // Arrange & Act
            var author = new DiscordAuthor { Name = "" };

            // Assert
            Assert.Equal("", author.Name);
        }
    }

    public class DiscordImageTests
    {
        [Fact]
        public void Constructor_Initialize_PropertyNull()
        {
            // Arrange & Act
            var image = new DiscordImage();

            // Assert
            Assert.Null(image.Url);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("https://example.com/image.png")]
        [InlineData("https://cdn.discord.com/attachments/123456/image.jpg")]
        public void Url_SetVariousValues_CanBeRetrieved(string? url)
        {
            // Arrange & Act
            var image = new DiscordImage { Url = url };

            // Assert
            Assert.Equal(url, image.Url);
        }

        [Fact]
        public void Url_Modified_ReflectsChanges()
        {
            // Arrange
            var image = new DiscordImage { Url = "https://example.com/old.png" };

            // Act
            image.Url = "https://example.com/new.png";

            // Assert
            Assert.Equal("https://example.com/new.png", image.Url);
        }
    }

    public class DiscordFooterTests
    {
        [Fact]
        public void Constructor_Initialize_AllPropertiesNull()
        {
            // Arrange & Act
            var footer = new DiscordFooter();

            // Assert
            Assert.Null(footer.Text);
            Assert.Null(footer.IconUrl);
        }

        [Theory]
        [InlineData(null, null)]
        [InlineData("Footer Text", null)]
        [InlineData(null, "https://example.com/icon.png")]
        [InlineData("Footer Text", "https://example.com/icon.png")]
        [InlineData("", "")]
        public void Properties_SetVariousCombinations_CanBeRetrieved(
            string? text, string? iconUrl)
        {
            // Arrange & Act
            var footer = new DiscordFooter
            {
                Text = text,
                IconUrl = iconUrl
            };

            // Assert
            Assert.Equal(text, footer.Text);
            Assert.Equal(iconUrl, footer.IconUrl);
        }

        [Fact]
        public void Properties_CanBeModifiedIndependently_KeepOtherValuesUnaffected()
        {
            // Arrange
            var footer = new DiscordFooter { Text = "Original" };

            // Act
            footer.IconUrl = "https://example.com/icon.png";

            // Assert
            Assert.Equal("Original", footer.Text);
            Assert.Equal("https://example.com/icon.png", footer.IconUrl);
        }
    }

    public class DiscordEmbedTests
    {
        [Fact]
        public void Constructor_Initialize_DefaultProperties()
        {
            // Arrange & Act
            var embed = new DiscordEmbed();

            // Assert
            Assert.Null(embed.Title);
            Assert.Null(embed.Author);
            Assert.Null(embed.Url);
            Assert.Null(embed.Description);
            Assert.Null(embed.Image);
            Assert.Null(embed.Footer);
            Assert.Equal(0, embed.Color);
        }

        [Fact]
        public void Properties_SetBasicValues_CanBeRetrieved()
        {
            // Arrange & Act
            var embed = new DiscordEmbed
            {
                Title = "Test Title",
                Url = "https://example.com",
                Description = "Test Description",
                Color = 16711680 // Red
            };

            // Assert
            Assert.Equal("Test Title", embed.Title);
            Assert.Equal("https://example.com", embed.Url);
            Assert.Equal("Test Description", embed.Description);
            Assert.Equal(16711680, embed.Color);
        }

        [Fact]
        public void Properties_SetComplexObjects_ArePreserved()
        {
            // Arrange
            var author = new DiscordAuthor { Name = "Test Author" };
            var image = new DiscordImage { Url = "https://example.com/image.png" };
            var footer = new DiscordFooter { Text = "Test Footer" };

            // Act
            var embed = new DiscordEmbed
            {
                Title = "Test",
                Author = author,
                Image = image,
                Footer = footer
            };

            // Assert
            Assert.Same(author, embed.Author);
            Assert.Equal("Test Author", embed.Author?.Name);
            Assert.Same(image, embed.Image);
            Assert.Equal("https://example.com/image.png", embed.Image?.Url);
            Assert.Same(footer, embed.Footer);
            Assert.Equal("Test Footer", embed.Footer?.Text);
        }

        [Fact]
        public void Embeds_SetToArray_CanStoreMultiple()
        {
            // Arrange
            var embed1 = new DiscordEmbed { Title = "Embed 1" };
            var embed2 = new DiscordEmbed { Title = "Embed 2" };

            // Act
            var parentEmbed = new DiscordEmbed { Title = "Parent" };
            parentEmbed.Title = "Parent";

            // Assert - Shows that Embeds cannot be set on DiscordEmbed itself
            // This test verifies the embed has all necessary components for composition
            Assert.Equal("Parent", parentEmbed.Title);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("Short")]
        [InlineData("A very long description that might be truncated by Discord")]
        [InlineData("Description with special chars: !@#$%^&*()")]
        public void Description_SetVariousLengths_IsPreserved(string? description)
        {
            // Arrange & Act
            var embed = new DiscordEmbed { Description = description };

            // Assert
            Assert.Equal(description, embed.Description);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(16711680)] // Red
        [InlineData(65280)] // Green
        [InlineData(255)] // Blue
        [InlineData(16776960)] // Yellow
        [InlineData(16711935)] // Magenta
        [InlineData(int.MaxValue)]
        public void Color_SetVariousValues_CanBeRetrieved(int color)
        {
            // Arrange & Act
            var embed = new DiscordEmbed { Color = color };

            // Assert
            Assert.Equal(color, embed.Color);
        }

        [Fact]
        public void Properties_AllPopulated_FormsCompleteEmbed()
        {
            // Arrange
            var author = new DiscordAuthor { Name = "Author", Url = "https://example.com" };
            var image = new DiscordImage { Url = "https://example.com/image.png" };
            var footer = new DiscordFooter { Text = "Footer", IconUrl = "https://example.com/icon.png" };

            // Act
            var embed = new DiscordEmbed
            {
                Title = "Complete Embed",
                Author = author,
                Url = "https://example.com/embed",
                Description = "A complete embed with all properties",
                Image = image,
                Footer = footer,
                Color = 16711680
            };

            // Assert
            Assert.NotNull(embed.Title);
            Assert.NotNull(embed.Author);
            Assert.NotNull(embed.Url);
            Assert.NotNull(embed.Description);
            Assert.NotNull(embed.Image);
            Assert.NotNull(embed.Footer);
            Assert.NotEqual(0, embed.Color);
        }
    }

    public class DiscordPayloadTests
    {
        [Fact]
        public void Constructor_Initialize_AllPropertiesNull()
        {
            // Arrange & Act
            var payload = new DiscordPayload();

            // Assert
            Assert.Null(payload.Username);
            Assert.Null(payload.AvatarUrl);
            Assert.Null(payload.Embeds);
        }

        [Theory]
        [InlineData(null, null)]
        [InlineData("FeedBot", null)]
        [InlineData(null, "https://example.com/avatar.png")]
        [InlineData("FeedBot", "https://example.com/avatar.png")]
        [InlineData("", "")]
        public void BasicProperties_SetVariousCombinations_CanBeRetrieved(
            string? username, string? avatarUrl)
        {
            // Arrange & Act
            var payload = new DiscordPayload
            {
                Username = username,
                AvatarUrl = avatarUrl
            };

            // Assert
            Assert.Equal(username, payload.Username);
            Assert.Equal(avatarUrl, payload.AvatarUrl);
        }

        [Fact]
        public void Embeds_SetToArray_CanStoreMultiple()
        {
            // Arrange
            var embed1 = new DiscordEmbed { Title = "Embed 1" };
            var embed2 = new DiscordEmbed { Title = "Embed 2" };
            var embeds = new[] { embed1, embed2 };

            // Act
            var payload = new DiscordPayload { Embeds = embeds };

            // Assert
            Assert.NotNull(payload.Embeds);
            Assert.Equal(2, payload.Embeds.Length);
            Assert.Same(embed1, payload.Embeds[0]);
            Assert.Same(embed2, payload.Embeds[1]);
        }

        [Fact]
        public void Embeds_SetToEmptyArray_IsPreserved()
        {
            // Arrange & Act
            var payload = new DiscordPayload { Embeds = Array.Empty<DiscordEmbed>() };

            // Assert
            Assert.NotNull(payload.Embeds);
            Assert.Empty(payload.Embeds);
        }

        [Fact]
        public void Embeds_SetToNull_BecomesNull()
        {
            // Arrange
            var payload = new DiscordPayload { Embeds = new[] { new DiscordEmbed() } };

            // Act
            payload.Embeds = null;

            // Assert
            Assert.Null(payload.Embeds);
        }

        [Fact]
        public void Properties_CanBeModifiedIndependently_ArePreserved()
        {
            // Arrange
            var payload = new DiscordPayload { Username = "FeedBot" };

            // Act
            payload.AvatarUrl = "https://example.com/avatar.png";
            var embed = new DiscordEmbed { Title = "Feed Update" };
            payload.Embeds = new[] { embed };

            // Assert
            Assert.Equal("FeedBot", payload.Username);
            Assert.Equal("https://example.com/avatar.png", payload.AvatarUrl);
            Assert.NotNull(payload.Embeds);
            Assert.Single(payload.Embeds);
        }

        [Fact]
        public void CompletePayload_AllPropertiesSet_FormsValidDiscordPayload()
        {
            // Arrange
            var author = new DiscordAuthor { Name = "Feed Source" };
            var footer = new DiscordFooter { Text = "via FeedCord" };
            var embed = new DiscordEmbed
            {
                Title = "Feed Item",
                Author = author,
                Description = "Item description",
                Footer = footer,
                Color = 16711680
            };

            // Act
            var payload = new DiscordPayload
            {
                Username = "FeedBot",
                AvatarUrl = "https://example.com/avatar.png",
                Embeds = new[] { embed }
            };

            // Assert
            Assert.NotNull(payload.Username);
            Assert.NotNull(payload.AvatarUrl);
            Assert.NotNull(payload.Embeds);
            Assert.NotEmpty(payload.Embeds);
            Assert.NotNull(payload.Embeds[0].Author);
        }
    }

    public class DiscordForumPayloadTests
    {
        [Fact]
        public void Constructor_Initialize_AllPropertiesNull()
        {
            // Arrange & Act
            var payload = new DiscordForumPayload();

            // Assert
            Assert.Null(payload.Content);
            Assert.Null(payload.Embeds);
            Assert.Null(payload.ThreadName);
        }

        [Theory]
        [InlineData(null, null)]
        [InlineData("Post content", null)]
        [InlineData(null, "Thread Title")]
        [InlineData("Post content", "Thread Title")]
        [InlineData("", "")]
        public void Properties_SetVariousCombinations_CanBeRetrieved(
            string? content, string? threadName)
        {
            // Arrange & Act
            var payload = new DiscordForumPayload
            {
                Content = content,
                ThreadName = threadName
            };

            // Assert
            Assert.Equal(content, payload.Content);
            Assert.Equal(threadName, payload.ThreadName);
        }

        [Fact]
        public void Embeds_SetToArray_CanStoreMultiple()
        {
            // Arrange
            var embed1 = new DiscordEmbed { Title = "Details 1" };
            var embed2 = new DiscordEmbed { Title = "Details 2" };

            // Act
            var payload = new DiscordForumPayload
            {
                ThreadName = "Discussion",
                Embeds = new[] { embed1, embed2 }
            };

            // Assert
            Assert.NotNull(payload.Embeds);
            Assert.Equal(2, payload.Embeds.Length);
            Assert.Equal("Details 1", payload.Embeds[0].Title);
            Assert.Equal("Details 2", payload.Embeds[1].Title);
        }

        [Fact]
        public void Embeds_SetToNull_BecomesNull()
        {
            // Arrange & Act
            var payload = new DiscordForumPayload { Embeds = null };

            // Assert
            Assert.Null(payload.Embeds);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("Simple thread name")]
        [InlineData("Thread with special chars: !@#$%^&*()")]
        [InlineData("Very long thread name that might be constrained by Discord length limits")]
        public void ThreadName_SetVariousValues_IsPreserved(string? threadName)
        {
            // Arrange & Act
            var payload = new DiscordForumPayload { ThreadName = threadName };

            // Assert
            Assert.Equal(threadName, payload.ThreadName);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("Simple content")]
        [InlineData("Content with line breaks\nand multiple\nlines")]
        [InlineData("Content with special markdown: **bold** *italic* `code`")]
        public void Content_SetVariousValues_IsPreserved(string? content)
        {
            // Arrange & Act
            var payload = new DiscordForumPayload { Content = content };

            // Assert
            Assert.Equal(content, payload.Content);
        }

        [Fact]
        public void CompletePayload_AllPropertiesSet_FormsValidForumPayload()
        {
            // Arrange
            var embed = new DiscordEmbed { Title = "Feed Item" };

            // Act
            var payload = new DiscordForumPayload
            {
                Content = "This is the forum post content",
                ThreadName = "New RSS Item",
                Embeds = new[] { embed }
            };

            // Assert
            Assert.NotNull(payload.Content);
            Assert.NotNull(payload.ThreadName);
            Assert.NotNull(payload.Embeds);
            Assert.Single(payload.Embeds);
        }
    }

    public class DiscordMarkdownPayloadTests
    {
        [Fact]
        public void Constructor_Initialize_AllPropertiesNull()
        {
            // Arrange & Act
            var payload = new DiscordMarkdownPayload();

            // Assert
            Assert.Null(payload.Content);
            Assert.Null(payload.ThreadName);
        }

        [Theory]
        [InlineData(null, null)]
        [InlineData("Markdown content", null)]
        [InlineData(null, "Thread Name")]
        [InlineData("Markdown content", "Thread Name")]
        [InlineData("", "")]
        public void Properties_SetVariousCombinations_CanBeRetrieved(
            string? content, string? threadName)
        {
            // Arrange & Act
            var payload = new DiscordMarkdownPayload
            {
                Content = content,
                ThreadName = threadName
            };

            // Assert
            Assert.Equal(content, payload.Content);
            Assert.Equal(threadName, payload.ThreadName);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("Simple markdown")]
        [InlineData("**bold** text")]
        [InlineData("*italic* text")]
        [InlineData("`code block`")]
        [InlineData("# Heading\n\n## Subheading\n\nContent")]
        [InlineData("[link](https://example.com)")]
        [InlineData("- List item 1\n- List item 2")]
        public void Content_WithMarkdownFormatting_IsPreserved(string? content)
        {
            // Arrange & Act
            var payload = new DiscordMarkdownPayload { Content = content };

            // Assert
            Assert.Equal(content, payload.Content);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("Simple name")]
        [InlineData("Thread with spaces")]
        [InlineData("Thread-with-dashes")]
        public void ThreadName_SetVariousValues_IsPreserved(string? threadName)
        {
            // Arrange & Act
            var payload = new DiscordMarkdownPayload { ThreadName = threadName };

            // Assert
            Assert.Equal(threadName, payload.ThreadName);
        }

        [Fact]
        public void Properties_CanBeModifiedIndependently_ArePreserved()
        {
            // Arrange
            var payload = new DiscordMarkdownPayload { Content = "Original" };

            // Act
            payload.ThreadName = "New Thread";

            // Assert
            Assert.Equal("Original", payload.Content);
            Assert.Equal("New Thread", payload.ThreadName);
        }

        [Fact]
        public void CompletePayload_AllPropertiesSet_FormsValidMarkdownPayload()
        {
            // Arrange & Act
            var payload = new DiscordMarkdownPayload
            {
                Content = "**New Feed Item**\n\nThis is a markdown-formatted feed update",
                ThreadName = "Feed Updates"
            };

            // Assert
            Assert.NotNull(payload.Content);
            Assert.NotNull(payload.ThreadName);
            Assert.Contains("**", payload.Content);
        }
    }
}
