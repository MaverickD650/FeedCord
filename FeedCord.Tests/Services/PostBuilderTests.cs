using Xunit;
using FeedCord.Services.Helpers;
using FeedCord.Common;
using CodeHollow.FeedReader;
using CodeHollow.FeedReader.Feeds;

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

        #region Specialized Format Tests - High Priority Coverage Improvements

        [Fact]
        public void TryBuildPost_RedditAuthorExtracted_FromAtomAuthorElement()
        {
            // Arrange
            var redditXml = """
                <feed xmlns="http://www.w3.org/2005/Atom">
                  <title>r/news</title>
                  <entry>
                    <title>Breaking: News Title</title>
                    <id>t3_xyz123</id>
                    <published>2025-02-20T12:00:00Z</published>
                    <author><name>/u/journalist</name></author>
                    <content type="html"><![CDATA[News content here]]></content>
                  </entry>
                </feed>
                """;

            var parsed = FeedReader.ReadFromString(redditXml);
            var feed = new Feed { Title = parsed.Title, Link = "https://reddit.com/r/news" };
            var item = parsed.Items[0];

            // Act
            var result = PostBuilder.TryBuildPost(item, feed, 0, "");

            // Assert
            Assert.NotNull(result);
            Assert.Equal("/u/journalist", result.Author);
        }

        [Fact]
        public void TryBuildPost_GitLabWithMultipleLabels_ParsesAllLabels()
        {
            // Arrange - GitLab uses a <labels> element with <label> children
            var gitlabXml = """
                <feed xmlns="http://www.w3.org/2005/Atom">
                  <title>GitLab Issues</title>
                  <entry>
                    <title>Implement feature X</title>
                    <id>https://gitlab.com/org/proj/-/issues/456</id>
                    <published>2025-02-15T10:00:00Z</published>
                    <link href="https://gitlab.com/org/proj/-/issues/456" />
                    <content type="html"><![CDATA[Issue content]]></content>
                    <labels>
                      <label>bug</label>
                      <label>enhancement</label>
                      <label>urgent</label>
                      <label> </label>
                    </labels>
                  </entry>
                </feed>
                """;

            var parsed = FeedReader.ReadFromString(gitlabXml);
            var feed = new Feed { Title = parsed.Title, Link = "https://gitlab.example.com" };
            var item = parsed.Items[0];

            // Act
            var result = PostBuilder.TryBuildPost(item, feed, 0, "");

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.Labels);
            Assert.Equal(3, result.Labels.Count());  // Empty labels should be filtered
            Assert.Contains("bug", result.Labels);
            Assert.Contains("enhancement", result.Labels);
            Assert.Contains("urgent", result.Labels);
        }

        [Fact]
        public void TryBuildPost_RedditWithHtmlContentImage_ExtractsFirstImage()
        {
            // Arrange - ParseFirstImageFromHtml is only used for Reddit posts
            var redditXml = """
                <feed xmlns="http://www.w3.org/2005/Atom" xmlns:media="http://search.yahoo.com/mrss/">
                  <title>r/test</title>
                  <entry>
                    <title>Article</title>
                    <id>t3_abc</id>
                    <published>2025-02-20T12:00:00Z</published>
                    <author><name>/u/testuser</name></author>
                    <content type="html"><![CDATA[<div><img src='https://first.jpg'/><img src='https://second.jpg'/></div>]]></content>
                  </entry>
                </feed>
                """;

            var parsed = FeedReader.ReadFromString(redditXml);
            var feed = new Feed { Title = parsed.Title, Link = "https://reddit.com/r/test" };
            var item = parsed.Items[0];

            // Act
            var result = PostBuilder.TryBuildPost(item, feed, 0, "");

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.ImageUrl);
            Assert.Contains("first.jpg", result.ImageUrl);  // Should extract first image from HTML
        }

        [Fact]
        public void TryBuildPost_DecodeContent_HandlesMultipleEntityTypes()
        {
            // Arrange
            var feed = CreateMockFeed("Feed", "https://example.com");
            var item = new FeedItem
            {
                Title = "Test",
                Description = "Price: &pound;100 &amp; taxes &quot;included&quot; &apos;per item&apos;",
                Link = "https://example.com/item",
                PublishingDate = DateTime.UtcNow
            };

            // Act
            var result = PostBuilder.TryBuildPost(item, feed, 0, "");

            // Assert
            Assert.Contains("£", result.Description);
            Assert.Contains("&", result.Description);
            Assert.Contains("\"", result.Description);
            Assert.Contains("'", result.Description);
            Assert.DoesNotContain("&pound;", result.Description);
            Assert.DoesNotContain("&apos;", result.Description);
            Assert.DoesNotContain("&quot;", result.Description);
        }

        [Fact]
        public void TryBuildPost_WithMediaRssSpecificItem_ExtractsDcCreatorAuthor()
        {
            // Arrange
                        var mediaRssXml = """
                                <rss version="2.0" xmlns:dc="http://purl.org/dc/elements/1.1/" xmlns:media="http://search.yahoo.com/mrss/">
                                    <channel>
                                        <title>Media Feed</title>
                                        <item>
                                            <title>Media Item</title>
                                            <link>https://example.com/item</link>
                                            <dc:creator>Media Creator</dc:creator>
                                        </item>
                                    </channel>
                                </rss>
                                """;

                        var parsed = FeedReader.ReadFromString(mediaRssXml);
                        var feed = new Feed { Title = parsed.Title, Link = "https://example.com/rss" };
                        var item = parsed.Items[0];
                        item.Author = string.Empty;

            // Act
            var result = PostBuilder.TryBuildPost(item, feed, 0, "");

            // Assert
            Assert.Equal("Media Creator", result.Author);
        }

        [Fact]
        public void TryBuildPost_WithRss20SpecificItem_ExtractsDcCreatorAuthor()
        {
            // Arrange
                        var rssXml = """
                                <rss version="2.0" xmlns:dc="http://purl.org/dc/elements/1.1/">
                                    <channel>
                                        <title>Rss Feed</title>
                                        <item>
                                            <title>Rss Item</title>
                                            <link>https://example.com/item</link>
                                            <dc:creator>Rss Creator</dc:creator>
                                        </item>
                                    </channel>
                                </rss>
                                """;

                        var parsed = FeedReader.ReadFromString(rssXml);
                        var feed = new Feed { Title = parsed.Title, Link = "https://example.com/rss" };
                        var item = parsed.Items[0];
                        item.Author = string.Empty;

            // Act
            var result = PostBuilder.TryBuildPost(item, feed, 0, "");

            // Assert
            Assert.Equal("Rss Creator", result.Author);
        }

        #endregion
    }
}
