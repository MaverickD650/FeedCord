using CodeHollow.FeedReader;
using FeedCord.Common;
using FeedCord.Services.Helpers;
using Xunit;

namespace FeedCord.Tests.Services;

public class PostBuilderCoverageTests
{
    [Fact]
    public void TryBuildPost_RedditThumbnailAndFooterRemoval_AppliesExpectedValues()
    {
        var redditXml = """
            <feed xmlns="http://www.w3.org/2005/Atom" xmlns:media="http://search.yahoo.com/mrss/">
              <title>r/test</title>
              <entry>
                <title>Reddit Title</title>
                <id>t3_abc</id>
                <published>2025-01-01T00:00:00Z</published>
                <author><name>/u/testuser</name></author>
                <link rel="alternate" href="https://reddit.com/r/test/comments/abc/post" />
                <content type="html"><![CDATA[submitted by /u/testuser [link] [comments]]]></content>
                <media:thumbnail url="https://img.test/thumb.jpg" />
              </entry>
            </feed>
            """;

        var parsed = FeedReader.ReadFromString(redditXml);
        var feed = new Feed { Title = parsed.Title, Link = "https://reddit.com/r/test" };
        var item = parsed.Items[0];

        var result = PostBuilder.TryBuildPost(item, feed, 0, "https://fallback.test/fallback.jpg");

        Assert.Equal("https://img.test/thumb.jpg", result.ImageUrl);
        Assert.Equal("/u/testuser", result.Author);
        Assert.Equal("https://reddit.com/r/test/comments/abc/post", result.Link);
        Assert.Equal(string.Empty, result.Description);
    }

    [Fact]
    public void TryBuildPost_RedditContentImageFallback_UsesFirstHtmlImage()
    {
        var redditXml = """
            <feed xmlns="http://www.w3.org/2005/Atom">
              <title>r/test</title>
              <entry>
                <title>Reddit Title</title>
                <id>t3_def</id>
                <published>2025-01-01T00:00:00Z</published>
                <author><name>/u/example</name></author>
                <content type="html"><![CDATA[<p><img src='https://img.test/from-content.png' />Body</p>]]></content>
              </entry>
            </feed>
            """;

        var parsed = FeedReader.ReadFromString(redditXml);
        var feed = new Feed { Title = parsed.Title, Link = "https://reddit.com/r/test" };
        var item = parsed.Items[0];

        var result = PostBuilder.TryBuildPost(item, feed, 0, "https://fallback.test/fallback.jpg");

        Assert.Equal("https://img.test/from-content.png", result.ImageUrl);
    }

    [Fact]
    public void TryBuildPost_GitLabLabelsAndTrim_ParsesAndTrims()
    {
        var gitlabXml = """
            <feed xmlns="http://www.w3.org/2005/Atom">
              <title>GitLab Feed</title>
              <entry>
                <title>Issue title</title>
                <id>https://gitlab.com/group/proj/-/issues/42</id>
                <published>2025-02-01T10:00:00Z</published>
                <link rel="alternate" href="https://gitlab.com/group/proj/-/issues/42" />
                <content type="html"><![CDATA[This is a long gitlab description body]]></content>
                <labels>
                  <label>bug</label>
                  <label> </label>
                  <label>feature</label>
                </labels>
              </entry>
            </feed>
            """;

        var parsed = FeedReader.ReadFromString(gitlabXml);
        var feed = new Feed { Title = parsed.Title, Link = "https://example.com/atom" };
        var item = parsed.Items[0];
        item.Description = "This is a long gitlab description body";

        var result = PostBuilder.TryBuildPost(item, feed, 10, "ignored.jpg");

        Assert.Equal("Issue title", result.Title);
        Assert.NotNull(result.Labels);
        Assert.Equal(["bug", "feature"], result.Labels);
        Assert.EndsWith("...", result.Description);
    }

    [Fact]
    public void TryBuildPost_DecodeContent_ConvertsBrToEnvironmentNewLine()
    {
        var feed = new Feed { Title = "Feed", Link = "https://example.com/feed" };
        var item = new FeedItem
        {
            Title = "Title",
            Description = "Line1<br>Line2<br/>Line3",
            Link = "https://example.com/post",
            PublishingDate = DateTime.UtcNow
        };

        var result = PostBuilder.TryBuildPost(item, feed, 0, string.Empty);

        Assert.Contains(Environment.NewLine, result.Description);
    }
}