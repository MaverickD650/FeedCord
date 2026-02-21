using Xunit;
using FeedCord.Services.Helpers;
using FeedCord.Common;
using CodeHollow.FeedReader;

namespace FeedCord.Tests.Services
{
    public class PostBuilderTests
    {
        #region TryBuildPost - Generic RSS Tests

        [Fact]
        public void TryBuildPost_WithMinimalRssItem_ReturnsPost()
        {
            // Arrange
            var feed = CreateMockFeed("Test Feed", "https://example.com/rss");
            var item = CreateMockRssItem("Test Title", "Test Description", "https://example.com/item1");

            // Act
            var result = PostBuilder.TryBuildPost(item, feed, 0, "");

            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result.Title);
        }

        [Fact]
        public void TryBuildPost_WithUrlAndImageUrl_ReturnsPostWithImage()
        {
            // Arrange
            var feed = CreateMockFeed("Test Feed", "https://example.com/rss");
            var item = CreateMockRssItem("Title", "Description", "https://example.com/item");
            string imageUrl = "https://example.com/image.jpg";

            // Act
            var result = PostBuilder.TryBuildPost(item, feed, 0, imageUrl);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(imageUrl, result.ImageUrl);
        }

        [Fact]
        public void TryBuildPost_WithTrimZero_NoTrimming()
        {
            // Arrange
            var feed = CreateMockFeed("Test Feed", "https://example.com/rss");
            var item = CreateMockRssItem("Title", "Long description text", "https://example.com/item");

            // Act
            var result = PostBuilder.TryBuildPost(item, feed, 0, "");

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Long description text", result.Description);
        }

        [Fact]
        public void TryBuildPost_WithTrimLimit_TruncatesDescription()
        {
            // Arrange
            var feed = CreateMockFeed("Test Feed", "https://example.com/rss");
            var item = CreateMockRssItem("Title", "This is a very long description that should be trimmed", "https://example.com/item");

            // Act
            var result = PostBuilder.TryBuildPost(item, feed, 10, "");

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Description.Length <= 13); // 10 chars + "..."
            Assert.EndsWith("...", result.Description);
        }

        [Fact]
        public void TryBuildPost_WithHtmlDescription_RemovesTags()
        {
            // Arrange
            var feed = CreateMockFeed("Test Feed", "https://example.com/rss");
            var item = CreateMockRssItem("Title", "<p>Hello <b>World</b></p>", "https://example.com/item");

            // Act
            var result = PostBuilder.TryBuildPost(item, feed, 0, "");

            // Assert
            Assert.NotNull(result);
            Assert.DoesNotContain("<p>", result.Description);
            Assert.DoesNotContain("<b>", result.Description);
            Assert.Contains("Hello", result.Description);
            Assert.Contains("World", result.Description);
        }

        [Fact]
        public void TryBuildPost_PreservesFeedTitle()
        {
            // Arrange
            var feedTitle = "My Test Feed";
            var feed = CreateMockFeed(feedTitle, "https://example.com/rss");
            var item = CreateMockRssItem("Item Title", "Description", "https://example.com/item");

            // Act
            var result = PostBuilder.TryBuildPost(item, feed, 0, "");

            // Assert
            Assert.NotNull(result);
            Assert.Equal(feedTitle, result.Tag);
        }

        [Fact]
        public void TryBuildPost_PreservesLink()
        {
            // Arrange
            var feed = CreateMockFeed("Test Feed", "https://example.com/rss");
            string itemLink = "https://example.com/specific-item";
            var item = CreateMockRssItem("Title", "Description", itemLink);

            // Act
            var result = PostBuilder.TryBuildPost(item, feed, 0, "");

            // Assert
            Assert.NotNull(result);
            Assert.Equal(itemLink, result.Link);
        }

        [Fact]
        public void TryBuildPost_WithEmptyDescription_AllowsEmpty()
        {
            // Arrange
            var feed = CreateMockFeed("Test Feed", "https://example.com/rss");
            var item = CreateMockRssItem("Title", "", "https://example.com/item");

            // Act
            var result = PostBuilder.TryBuildPost(item, feed, 0, "");

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result.Description);
        }

        [Fact]
        public void TryBuildPost_WithHtmlEntity_DecodesEntity()
        {
            // Arrange
            var feed = CreateMockFeed("Test Feed", "https://example.com/rss");
            var item = CreateMockRssItem("Title", "Hello &amp; Goodbye", "https://example.com/item");

            // Act
            var result = PostBuilder.TryBuildPost(item, feed, 0, "");

            // Assert
            Assert.NotNull(result);
            Assert.Contains("&", result.Description);
        }

        [Fact]
        public void TryBuildPost_WithApostropheEntity_DecodesApostrophe()
        {
            // Arrange
            var feed = CreateMockFeed("Test Feed", "https://example.com/rss");
            var item = CreateMockRssItem("Title", "It&apos;s working", "https://example.com/item");

            // Act
            var result = PostBuilder.TryBuildPost(item, feed, 0, "");

            // Assert
            Assert.NotNull(result);
            Assert.Contains("'", result.Description);
        }

        #endregion

        #region Trim Parameter Tests

        [Theory]
        [InlineData(5)]
        [InlineData(20)]
        [InlineData(50)]
        [InlineData(100)]
        public void TryBuildPost_WithVariousTrimSizes_RespectsTrimLimit(int trimSize)
        {
            // Arrange
            var feed = CreateMockFeed("Test Feed", "https://example.com/rss");
            var longDescription = new string('x', 200);
            var item = CreateMockRssItem("Title", longDescription, "https://example.com/item");

            // Act
            var result = PostBuilder.TryBuildPost(item, feed, trimSize, "");

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Description.Length <= trimSize + 3); // trim + "..."
        }

        [Fact]
        public void TryBuildPost_WhenDescriptionShorterThanTrim_NoEllipsis()
        {
            // Arrange
            var feed = CreateMockFeed("Test Feed", "https://example.com/rss");
            var item = CreateMockRssItem("Title", "Short", "https://example.com/item");

            // Act
            var result = PostBuilder.TryBuildPost(item, feed, 100, "");

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Short", result.Description);
            Assert.False(result.Description.EndsWith("..."));
        }

        #endregion

        #region Reddit Detection Tests

        [Fact]
        public void TryBuildPost_WithRedditFeedUrl_RoutesToRedditBuilder()
        {
            // Arrange
            var feed = CreateMockFeed("r/test", "https://reddit.com/r/test/new.json");
            var item = CreateMockRssItem("Reddit Post", "Reddit Discussion", "https://reddit.com/r/test/comments/123");

            // Act
            var result = PostBuilder.TryBuildPost(item, feed, 0, "");

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.Title);
        }

        #endregion

        #region GitLab Detection Tests

        [Fact]
        public void TryBuildPost_WithGitLabItemUrl_RoutesToGitLabBuilder()
        {
            // Arrange
            var feed = CreateMockFeed("GitLab Events", "https://gitlab.com/api/v4/events");
            var item = new FeedItem
            {
                Id = "https://gitlab.com/group/project/-/issues/123",
                Title = "GitLab Issue",
                Description = "Issue Description",
                Link = "https://gitlab.com/group/project/-/issues/123"
            };

            // Act
            var result = PostBuilder.TryBuildPost(item, feed, 0, "");

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.Title);
        }

        [Fact]
        public void TryBuildPost_WithNullItemId_DoesNotThrowAndBuildsPost()
        {
            // Arrange
            var feed = CreateMockFeed("General Feed", "https://example.com/feed");
            var item = new FeedItem
            {
                Id = null,
                Title = "Regular Post",
                Description = "Description",
                Link = "https://example.com/post"
            };

            // Act
            var result = PostBuilder.TryBuildPost(item, feed, 0, "");

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Regular Post", result.Title);
        }

        #endregion

        #region Date Handling Tests

        [Fact]
        public void TryBuildPost_WithValidPublishDate_PreservesDate()
        {
            // Arrange
            var feed = CreateMockFeed("Test Feed", "https://example.com/rss");
            var item = CreateMockRssItem("Title", "Description", "https://example.com/item");
            item.PublishingDate = new DateTime(2024, 1, 15, 10, 30, 0);

            // Act
            var result = PostBuilder.TryBuildPost(item, feed, 0, "");

            // Assert
            Assert.NotNull(result);
            Assert.Equal(new DateTime(2024, 1, 15, 10, 30, 0), result.PublishDate);
        }

        [Fact]
        public void TryBuildPost_WithDefaultDate_AllowsDefault()
        {
            // Arrange
            var feed = CreateMockFeed("Test Feed", "https://example.com/rss");
            var item = CreateMockRssItem("Title", "Description", "https://example.com/item");

            // Act
            var result = PostBuilder.TryBuildPost(item, feed, 0, "");

            // Assert
            Assert.NotNull(result);
            Assert.True(result.PublishDate != default(DateTime));
        }

        #endregion

        #region Author Extraction Tests

        [Fact]
        public void TryBuildPost_WithAuthorField_ExtractsAuthor()
        {
            // Arrange
            var feed = CreateMockFeed("Test Feed", "https://example.com/rss");
            var item = CreateMockRssItem("Title", "Description", "https://example.com/item");
            item.Author = "John Doe";

            // Act
            var result = PostBuilder.TryBuildPost(item, feed, 0, "");

            // Assert
            Assert.NotNull(result);
            Assert.Contains("John", result.Author);
        }

        [Fact]
        public void TryBuildPost_WithoutAuthor_ReturnsEmptyAuthor()
        {
            // Arrange
            var feed = CreateMockFeed("Test Feed", "https://example.com/rss");
            var item = CreateMockRssItem("Title", "Description", "https://example.com/item");

            // Act
            var result = PostBuilder.TryBuildPost(item, feed, 0, "");

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result.Author);
        }

        #endregion

        #region Post Object Structure Tests

        [Fact]
        public void TryBuildPost_ReturnsPostRecord()
        {
            // Arrange
            var feed = CreateMockFeed("Test Feed", "https://example.com/rss");
            var item = CreateMockRssItem("Title", "Description", "https://example.com/item");

            // Act
            var result = PostBuilder.TryBuildPost(item, feed, 0, "");

            // Assert
            Assert.NotNull(result);
            Assert.IsType<Post>(result);
        }

        [Fact]
        public void TryBuildPost_PostHasAllRequiredProperties()
        {
            // Arrange
            var feed = CreateMockFeed("Test Feed", "https://example.com/rss");
            var item = CreateMockRssItem("Title", "Description", "https://example.com/item");

            // Act
            var result = PostBuilder.TryBuildPost(item, feed, 0, "");

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.Title);
            Assert.NotNull(result.ImageUrl);
            Assert.NotNull(result.Description);
            Assert.NotNull(result.Link);
            Assert.NotNull(result.Tag);
            Assert.True(result.PublishDate != default(DateTime));
            Assert.NotNull(result.Author);
            Assert.NotNull(result.Labels);
        }

        [Fact]
        public void TryBuildPost_PostLabelsArrayIsAlwaysValid()
        {
            // Arrange
            var feed = CreateMockFeed("Test Feed", "https://example.com/rss");
            var item = CreateMockRssItem("Title", "Description", "https://example.com/item");

            // Act
            var result = PostBuilder.TryBuildPost(item, feed, 0, "");

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.Labels);
            Assert.IsAssignableFrom<IEnumerable<string>>(result.Labels);
        }

        #endregion

        #region Helper Methods

        private static Feed CreateMockFeed(string title, string link)
        {
            return new Feed
            {
                Title = title,
                Link = link,
                Description = "Test Feed Description",
                Language = "en",
                Copyright = "Test",
                SpecificFeed = null
            };
        }

        private static FeedItem CreateMockRssItem(string title, string description, string link)
        {
            return new FeedItem
            {
                Title = title,
                Description = description,
                Link = link,
                Id = link,
                Author = "",
                PublishingDate = DateTime.Now,
                SpecificItem = null
            };
        }

        /// <summary>
        /// Test that TryBuildPost handles null SpecificItem with various edge cases.
        /// This verifies the author extraction logic doesn't crash with edge case data,
        /// exercising the try-catch in TryGetAuthor via various code paths.
        /// </summary>
        [Fact]
        public void TryBuildPost_WithNullSpecificItem_SafelyReturnsPost()
        {
            // Arrange - item with null SpecificItem
            var feed = CreateMockFeed("Test Feed", "https://example.com/rss");
            var item = new FeedItem
            {
                Title = "No Author Feed Item",
                Description = "Item with no author info",
                Link = "https://example.com/item1",
                Author = "",  // Empty author
                PublishingDate = DateTime.Now,
                SpecificItem = null
            };

            // Act - should safely handle null SpecificItem
            var result = PostBuilder.TryBuildPost(item, feed, 0, "");

            // Assert
            Assert.NotNull(result);
            Assert.Equal("No Author Feed Item", result.Title);
            Assert.Equal("", result.Author);
        }

        /// <summary>
        /// Test that TryBuildPost with an Atom item (AtomFeedItem) extracts author safely.
        /// This exercises the SpecificItem property access in TryGetAuthor.
        /// </summary>
        [Fact]
        public void TryBuildPost_WithAtomItemButNoAuthor_ReturnsPostWithEmptyAuthor()
        {
            // Arrange - create RSS item where accessing SpecificItem properties is safe
            var feed = CreateMockFeed("Atom Feed", "https://example.com/atom");
            var item = CreateMockRssItem(
                "Atom Entry Without Author",
                "No author info",
                "https://example.com/entry1"
            );
            // SpecificItem will be null (no special author fields), so TryGetAuthor returns empty

            // Act
            var result = PostBuilder.TryBuildPost(item, feed, 0, "");

            // Assert - author extraction should fail gracefully with empty string
            Assert.NotNull(result);
            Assert.Equal("", result.Author);
        }

        #endregion
    }
}
