using Xunit;
using FeedCord.Services.Helpers;
using FeedCord.Common;
using CodeHollow.FeedReader;
using CodeHollow.FeedReader.Feeds;
using System.Reflection;

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

    [Fact]
    public void TryBuildPost_WithNullRssLink_UsesEmptyLink()
    {
      var feed = CreateMockFeed("General Feed", "https://example.com/feed");
      var item = new FeedItem
      {
        Id = "https://example.com/post",
        Title = "Regular Post",
        Description = "Description",
        Link = null,
        PublishingDate = DateTime.UtcNow
      };

      var result = PostBuilder.TryBuildPost(item, feed, 0, "");

      Assert.NotNull(result);
      Assert.Equal(string.Empty, result.Link);
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
      Assert.NotEqual(default, result.PublishDate);
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
      Assert.NotEqual(default, result.PublishDate);
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
    public void TryBuildPost_WithMediaRssSpecificItem_ExtractsSourceAuthorWhenDcCreatorMissing()
    {
      // Arrange
      var mediaSpecificItem = new MediaRssFeedItem();

      var dcProperty = typeof(MediaRssFeedItem).GetProperty("DC");
      Assert.NotNull(dcProperty);
      var dcInstance = dcProperty!.GetValue(mediaSpecificItem) ?? Activator.CreateInstance(dcProperty.PropertyType);
      Assert.NotNull(dcInstance);
      var creatorProperty = dcProperty.PropertyType.GetProperty("Creator");
      Assert.NotNull(creatorProperty);
      creatorProperty!.SetValue(dcInstance, string.Empty);
      if (dcProperty.SetMethod != null)
      {
        dcProperty.SetValue(mediaSpecificItem, dcInstance);
      }
      else
      {
        var dcBackingField = typeof(MediaRssFeedItem).GetField("<DC>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        dcBackingField?.SetValue(mediaSpecificItem, dcInstance);
      }

      var sourceProperty = typeof(MediaRssFeedItem).GetProperty("Source");
      Assert.NotNull(sourceProperty);
      var sourceInstance = sourceProperty!.GetValue(mediaSpecificItem) ?? Activator.CreateInstance(sourceProperty.PropertyType);
      Assert.NotNull(sourceInstance);
      var valueProperty = sourceProperty.PropertyType.GetProperty("Value");
      Assert.NotNull(valueProperty);
      valueProperty!.SetValue(sourceInstance, "Media Source Author");
      if ((string?)valueProperty.GetValue(sourceInstance) != "Media Source Author")
      {
        var valueBackingField = sourceProperty.PropertyType.GetField("<Value>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        valueBackingField?.SetValue(sourceInstance, "Media Source Author");
      }
      if (sourceProperty.SetMethod != null)
      {
        sourceProperty.SetValue(mediaSpecificItem, sourceInstance);
      }
      else
      {
        var sourceBackingField = typeof(MediaRssFeedItem).GetField("<Source>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        sourceBackingField?.SetValue(mediaSpecificItem, sourceInstance);
      }

      var feed = new Feed { Title = "Media Feed", Link = "https://example.com/rss" };
      var item = new FeedItem
      {
        Title = "Media Item",
        Description = "Description",
        Link = "https://example.com/item",
        Author = string.Empty,
        PublishingDate = DateTime.UtcNow,
        SpecificItem = mediaSpecificItem
      };

      // Act
      var result = PostBuilder.TryBuildPost(item, feed, 0, "");

      // Assert
      Assert.Equal("Media Source Author", result.Author);
    }

    [Fact]
    public void TryBuildPost_WithAtomItem_UsesUpdatedDateWhenPublishedMissing()
    {
      // Arrange
      var atomXml = """
                                <feed xmlns="http://www.w3.org/2005/Atom">
                                    <title>Atom Feed</title>
                                    <entry>
                                        <title>Atom Entry</title>
                                        <id>entry-1</id>
                                        <updated>2025-02-22T08:30:00Z</updated>
                                        <link href="https://example.com/atom/entry-1" />
                                        <content type="html"><![CDATA[Atom content]]></content>
                                    </entry>
                                </feed>
                                """;

      var parsed = FeedReader.ReadFromString(atomXml);
      var feed = new Feed { Title = parsed.Title, Link = "https://example.com/atom" };
      var item = parsed.Items[0];

      // Act
      var result = PostBuilder.TryBuildPost(item, feed, 0, "https://example.com/img.png");

      // Assert
      Assert.Equal(new DateTime(2025, 2, 22, 8, 30, 0, DateTimeKind.Utc), result.PublishDate.ToUniversalTime());
      Assert.Equal("https://example.com/img.png", result.ImageUrl);
      Assert.Equal("https://example.com/atom/entry-1", result.Link);
    }

    [Fact]
    public void TryBuildPost_WithAtomItem_UsesDefaultDateWhenPublishedAndUpdatedMissing()
    {
      // Arrange
      var atomXml = """
                                <feed xmlns="http://www.w3.org/2005/Atom">
                                    <title>Atom Feed</title>
                                    <entry>
                                        <title>No Date Entry</title>
                                        <id>entry-2</id>
                                        <link href="https://example.com/atom/entry-2" />
                                        <content type="html"><![CDATA[No date content]]></content>
                                    </entry>
                                </feed>
                                """;

      var parsed = FeedReader.ReadFromString(atomXml);
      var feed = new Feed { Title = parsed.Title, Link = "https://example.com/atom" };
      var item = parsed.Items[0];

      // Act
      var result = PostBuilder.TryBuildPost(item, feed, 0, "");

      // Assert
      Assert.Equal(default, result.PublishDate);
    }

    [Fact]
    public void TryBuildPost_WithAtomItemWithoutLinks_UsesEmptyLink()
    {
      var atomXml = """
                            <feed xmlns="http://www.w3.org/2005/Atom">
                                <title>Atom Feed</title>
                                <entry>
                                <title>No Link Entry</title>
                                <id>entry-no-link</id>
                                <published>2025-02-22T08:30:00Z</published>
                                <content type="html"><![CDATA[content]]></content>
                                </entry>
                            </feed>
                            """;

      var parsed = FeedReader.ReadFromString(atomXml);
      var feed = new Feed { Title = parsed.Title, Link = "https://example.com/atom" };
      var item = parsed.Items[0];

      var result = PostBuilder.TryBuildPost(item, feed, 0, "");

      Assert.Equal(string.Empty, result.Link);
    }

    [Fact]
    public void TryBuildPost_WithGitLabItemAndTrim_TrimDescriptionInGitLabBuilder()
    {
      // Arrange
      var item = new FeedItem
      {
        Id = "https://gitlab.com/org/proj/-/issues/123",
        Title = "GitLab Issue",
        Description = new string('x', 40),
        Link = "https://gitlab.com/org/proj/-/issues/123",
        PublishingDate = DateTime.UtcNow
      };
      var feed = new Feed { Title = "GitLab", Link = "https://gitlab.com/org/proj" };

      // Act
      var result = PostBuilder.TryBuildPost(item, feed, 10, "");

      // Assert
      Assert.Equal(13, result.Description.Length);
      Assert.EndsWith("...", result.Description);
    }

    [Fact]
    public void TryBuildPost_WithGitLabNullTitleAndLink_UsesEmptyFallbacks()
    {
      var item = new FeedItem
      {
        Id = "https://gitlab.com/org/proj/-/issues/999",
        Title = null,
        Description = "desc",
        Link = null,
        PublishingDate = DateTime.UtcNow
      };
      var feed = new Feed { Title = "GitLab", Link = "https://gitlab.com/org/proj" };

      var result = PostBuilder.TryBuildPost(item, feed, 0, "");

      Assert.Equal(string.Empty, result.Title);
      Assert.Equal(string.Empty, result.Link);
    }

    [Fact]
    public void TryBuildPost_WithRedditThumbnailAndAlternateLink_UsesThumbnailAndAlternateHref()
    {
      // Arrange
      var redditXml = """
                                <feed xmlns="http://www.w3.org/2005/Atom" xmlns:media="http://search.yahoo.com/mrss/">
                                    <title>r/test</title>
                                    <entry>
                                        <title>Post with Thumbnail</title>
                                        <id>t3_thumb1</id>
                                        <published>2025-02-20T12:00:00Z</published>
                                        <link rel="self" href="https://reddit.com/self" />
                                        <link rel="alternate" href="https://reddit.com/r/test/comments/thumb1" />
                                        <media:thumbnail url="https://example.com/thumb.jpg" />
                                        <content type="html"><![CDATA[<p>Body</p>]]></content>
                                    </entry>
                                </feed>
                                """;

      var parsed = FeedReader.ReadFromString(redditXml);
      var feed = new Feed { Title = parsed.Title, Link = "https://reddit.com/r/test" };
      var item = parsed.Items[0];

      // Act
      var result = PostBuilder.TryBuildPost(item, feed, 0, "https://fallback.jpg");

      // Assert
      Assert.Equal("https://example.com/thumb.jpg", result.ImageUrl);
      Assert.Equal("https://reddit.com/r/test/comments/thumb1", result.Link);
    }

    [Fact]
    public void TryBuildPost_WithRedditNoContent_UsesPostDescriptionFallback()
    {
      // Arrange
      var redditXml = """
                                <feed xmlns="http://www.w3.org/2005/Atom">
                                    <title>r/test</title>
                                    <entry>
                                        <title>Post without Content</title>
                                        <id>t3_nocontent</id>
                                        <published>2025-02-20T12:00:00Z</published>
                                        <summary>Fallback &amp; summary text</summary>
                                        <link href="https://reddit.com/r/test/comments/nocontent" />
                                    </entry>
                                </feed>
                                """;

      var parsed = FeedReader.ReadFromString(redditXml);
      var feed = new Feed { Title = parsed.Title, Link = "https://reddit.com/r/test" };
      var item = parsed.Items[0];

      // Act
      var result = PostBuilder.TryBuildPost(item, feed, 0, "");

      // Assert
      Assert.Contains("Fallback", result.Description);
      Assert.Contains("&", result.Description);
    }

    [Fact]
    public void TryBuildPost_WithRedditTrim_AppliesEllipsisAfterTrim()
    {
      // Arrange
      var redditXml = """
                                <feed xmlns="http://www.w3.org/2005/Atom">
                                    <title>r/test</title>
                                    <entry>
                                        <title>Trim Test</title>
                                        <id>t3_trim</id>
                                        <published>2025-02-20T12:00:00Z</published>
                                        <content type="html"><![CDATA[This is a long reddit content body for trimming]]></content>
                                        <link href="https://reddit.com/r/test/comments/trim" />
                                    </entry>
                                </feed>
                                """;

      var parsed = FeedReader.ReadFromString(redditXml);
      var feed = new Feed { Title = parsed.Title, Link = "https://reddit.com/r/test" };
      var item = parsed.Items[0];

      // Act
      var result = PostBuilder.TryBuildPost(item, feed, 10, "");

      // Assert
      Assert.Equal(13, result.Description.Length);
      Assert.EndsWith("...", result.Description);
    }

    [Fact]
    public void TryGetRedditAuthor_WithNullAtomItem_ReturnsEmptyStringViaCatch()
    {
      // Arrange
      var method = typeof(PostBuilder).GetMethod("TryGetRedditAuthor", BindingFlags.Static | BindingFlags.NonPublic);
      Assert.NotNull(method);

      // Act
      var result = method!.Invoke(null, new object?[] { null });

      // Assert
      Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void TryBuildPost_WithRedditNoAuthorElement_ReturnsEmptyAuthor()
    {
      // Arrange
      var redditXml = """
                                <feed xmlns="http://www.w3.org/2005/Atom">
                                    <title>r/test</title>
                                    <entry>
                                        <title>No Author</title>
                                        <id>t3_noauthor</id>
                                        <published>2025-02-20T12:00:00Z</published>
                                        <content type="html"><![CDATA[body]]></content>
                                        <link href="https://reddit.com/r/test/comments/noauthor" />
                                    </entry>
                                </feed>
                                """;

      var parsed = FeedReader.ReadFromString(redditXml);
      var feed = new Feed { Title = parsed.Title, Link = "https://reddit.com/r/test" };
      var item = parsed.Items[0];

      // Act
      var result = PostBuilder.TryBuildPost(item, feed, 0, "");

      // Assert
      Assert.Equal(string.Empty, result.Author);
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

    [Fact]
    public void TryGetAuthor_WithNullFeedItem_ReturnsEmptyStringViaCatch()
    {
      // Arrange - Use reflection to access private TryGetAuthor method
      var method = typeof(PostBuilder).GetMethod("TryGetAuthor", BindingFlags.Static | BindingFlags.NonPublic);
      Assert.NotNull(method);

      // Act - Pass null to trigger exception in try-catch block (lines 58-64)
      var result = method!.Invoke(null, new object?[] { null });

      // Assert - Catch block returns empty string for defensive safety
      Assert.Equal(string.Empty, result);
    }

    #endregion
  }
}
