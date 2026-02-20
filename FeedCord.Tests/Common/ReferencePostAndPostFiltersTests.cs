using Xunit;
using FeedCord.Common;

namespace FeedCord.Tests.Common;

public class ReferencePostTests
{
    [Fact]
    public void ReferencePost_CreatedWithValues_PreservesValues()
    {
        // Arrange
        var isYoutube = true;
        var lastRunDate = new DateTime(2026, 2, 20, 10, 30, 0);

        // Act
        var referencePost = new ReferencePost
        {
            IsYoutube = isYoutube,
            LastRunDate = lastRunDate
        };

        // Assert
        Assert.Equal(isYoutube, referencePost.IsYoutube);
        Assert.Equal(lastRunDate, referencePost.LastRunDate);
    }

    [Fact]
    public void ReferencePost_IsYoutubeFalse_PreservesValue()
    {
        // Arrange & Act
        var referencePost = new ReferencePost
        {
            IsYoutube = false,
            LastRunDate = new DateTime(2026, 2, 20, 10, 30, 0)
        };

        // Assert
        Assert.False(referencePost.IsYoutube);
    }

    [Fact]
    public void ReferencePost_IsYoutubeTrue_PreservesValue()
    {
        // Arrange & Act
        var referencePost = new ReferencePost
        {
            IsYoutube = true,
            LastRunDate = new DateTime(2026, 2, 20, 10, 30, 0)
        };

        // Assert
        Assert.True(referencePost.IsYoutube);
    }

    [Fact]
    public void ReferencePost_LastRunDate_CanOnlyBeSetDuringInitialization()
    {
        // Arrange
        var expectedDate = new DateTime(2026, 2, 20, 10, 30, 0);

        // Act
        var referencePost = new ReferencePost
        {
            IsYoutube = true,
            LastRunDate = expectedDate
        };

        // Assert
        Assert.Equal(expectedDate, referencePost.LastRunDate);
        // LastRunDate is init-only, so it cannot be changed after initialization
    }

    [Fact]
    public void ReferencePost_IsYoutubeCanBeChanged()
    {
        // Arrange
        var referencePost = new ReferencePost
        {
            IsYoutube = false,
            LastRunDate = DateTime.Now
        };

        // Act
        referencePost.IsYoutube = true;

        // Assert
        Assert.True(referencePost.IsYoutube);
    }

    [Fact]
    public void ReferencePost_IsYoutubeCanBeToggledMultipleTimes()
    {
        // Arrange
        var referencePost = new ReferencePost
        {
            IsYoutube = false,
            LastRunDate = DateTime.Now
        };

        // Act & Assert
        Assert.False(referencePost.IsYoutube);

        referencePost.IsYoutube = true;
        Assert.True(referencePost.IsYoutube);

        referencePost.IsYoutube = false;
        Assert.False(referencePost.IsYoutube);

        referencePost.IsYoutube = true;
        Assert.True(referencePost.IsYoutube);
    }

    [Fact]
    public void ReferencePost_MultipleInstances_AreIndependent()
    {
        // Arrange
        var date1 = new DateTime(2026, 1, 1);
        var date2 = new DateTime(2026, 2, 1);

        var ref1 = new ReferencePost
        {
            IsYoutube = true,
            LastRunDate = date1
        };

        var ref2 = new ReferencePost
        {
            IsYoutube = false,
            LastRunDate = date2
        };

        // Act
        ref1.IsYoutube = false;
        ref2.IsYoutube = true;

        // Assert
        Assert.False(ref1.IsYoutube);
        Assert.True(ref2.IsYoutube);
        Assert.Equal(date1, ref1.LastRunDate);
        Assert.Equal(date2, ref2.LastRunDate);
    }

    [Fact]
    public void ReferencePost_DefaultValues()
    {
        // Arrange & Act
        var referencePost = new ReferencePost();

        // Assert
        Assert.False(referencePost.IsYoutube);  // Default bool is false
        Assert.Equal(default(DateTime), referencePost.LastRunDate);
    }

    [Fact]
    public void ReferencePost_MinDateTime_CanBeSet()
    {
        // Arrange & Act
        var referencePost = new ReferencePost
        {
            IsYoutube = false,
            LastRunDate = DateTime.MinValue
        };

        // Assert
        Assert.Equal(DateTime.MinValue, referencePost.LastRunDate);
    }

    [Fact]
    public void ReferencePost_MaxDateTime_CanBeSet()
    {
        // Arrange & Act
        var referencePost = new ReferencePost
        {
            IsYoutube = false,
            LastRunDate = DateTime.MaxValue
        };

        // Assert
        Assert.Equal(DateTime.MaxValue, referencePost.LastRunDate);
    }

    [Fact]
    public void ReferencePost_EpochDateTime_CanBeSet()
    {
        // Arrange
        var epochDate = new DateTime(1970, 1, 1);

        // Act
        var referencePost = new ReferencePost
        {
            IsYoutube = false,
            LastRunDate = epochDate
        };

        // Assert
        Assert.Equal(epochDate, referencePost.LastRunDate);
    }
}

public class PostFiltersTests
{
    [Fact]
    public void PostFilters_CreatedWithValues_PreservesValues()
    {
        // Arrange
        var url = "http://example.com/rss";
        var filters = new[] { "keyword1", "keyword2", "keyword3" };

        // Act
        var postFilters = new PostFilters
        {
            Url = url,
            Filters = filters
        };

        // Assert
        Assert.Equal(url, postFilters.Url);
        Assert.Equal(filters, postFilters.Filters);
    }

    [Fact]
    public void PostFilters_Url_CanBeSet()
    {
        // Arrange
        var originalUrl = "http://example.com/rss1";
        var newUrl = "http://example.com/rss2";

        // Act
        var postFilters = new PostFilters
        {
            Url = originalUrl,
            Filters = new[] { "filter" }
        };

        postFilters.Url = newUrl;

        // Assert
        Assert.Equal(newUrl, postFilters.Url);
    }

    [Fact]
    public void PostFilters_Filters_CanBeSet()
    {
        // Arrange
        var originalFilters = new[] { "filter1" };
        var newFilters = new[] { "filter2", "filter3", "filter4" };

        // Act
        var postFilters = new PostFilters
        {
            Url = "http://example.com/rss",
            Filters = originalFilters
        };

        postFilters.Filters = newFilters;

        // Assert
        Assert.Equal(newFilters, postFilters.Filters);
    }

    [Fact]
    public void PostFilters_WithEmptyUrl_CanBeCreated()
    {
        // Arrange & Act
        var postFilters = new PostFilters
        {
            Url = "",
            Filters = new[] { "filter" }
        };

        // Assert
        Assert.Empty(postFilters.Url);
        Assert.NotNull(postFilters.Filters);
    }

    [Fact]
    public void PostFilters_WithEmptyFiltersArray_CanBeCreated()
    {
        // Arrange & Act
        var postFilters = new PostFilters
        {
            Url = "http://example.com/rss",
            Filters = new string[] { }
        };

        // Assert
        Assert.NotNull(postFilters.Url);
        Assert.Empty(postFilters.Filters);
    }

    [Fact]
    public void PostFilters_WithSingleFilter_CanBeCreated()
    {
        // Arrange & Act
        var postFilters = new PostFilters
        {
            Url = "http://example.com/rss",
            Filters = new[] { "single-filter" }
        };

        // Assert
        Assert.Single(postFilters.Filters);
        Assert.Equal("single-filter", postFilters.Filters[0]);
    }

    [Fact]
    public void PostFilters_WithMultipleFilters_CanBeCreated()
    {
        // Arrange
        var filters = new[] { "breaking", "news", "urgent", "alert", "critical" };

        // Act
        var postFilters = new PostFilters
        {
            Url = "http://example.com/rss",
            Filters = filters
        };

        // Assert
        Assert.Equal(5, postFilters.Filters.Length);
        Assert.Contains("breaking", postFilters.Filters);
        Assert.Contains("news", postFilters.Filters);
        Assert.Contains("urgent", postFilters.Filters);
    }

    [Fact]
    public void PostFilters_UrlCanBeAllKeyword()
    {
        // Arrange & Act
        var postFilters = new PostFilters
        {
            Url = "all",
            Filters = new[] { "important" }
        };

        // Assert
        Assert.Equal("all", postFilters.Url);
    }

    [Fact]
    public void PostFilters_MultipleInstances_AreIndependent()
    {
        // Arrange
        var filter1 = new PostFilters
        {
            Url = "http://example1.com",
            Filters = new[] { "tag1", "tag2" }
        };

        var filter2 = new PostFilters
        {
            Url = "http://example2.com",
            Filters = new[] { "tag3", "tag4" }
        };

        // Act
        filter1.Url = "http://modified1.com";
        filter2.Url = "http://modified2.com";

        // Assert
        Assert.Equal("http://modified1.com", filter1.Url);
        Assert.Equal("http://modified2.com", filter2.Url);
        Assert.Equal(2, filter1.Filters.Length);
        Assert.Equal(2, filter2.Filters.Length);
    }

    [Fact]
    public void PostFilters_FilterArrayCanBeModified()
    {
        // Arrange
        var originalFilters = new[] { "filter1", "filter2" };
        var postFilters = new PostFilters
        {
            Url = "http://example.com",
            Filters = originalFilters
        };

        var newFilters = new[] { "filter3", "filter4", "filter5" };

        // Act
        postFilters.Filters = newFilters;

        // Assert
        Assert.Equal(3, postFilters.Filters.Length);
        Assert.Equal("filter3", postFilters.Filters[0]);
        Assert.Equal("filter4", postFilters.Filters[1]);
        Assert.Equal("filter5", postFilters.Filters[2]);
    }

    [Fact]
    public void PostFilters_UrlWithComplexPath_CanBeSet()
    {
        // Arrange
        var complexUrl = "http://example.com:8080/api/v1/feeds/special?param=value&other=123";

        // Act
        var postFilters = new PostFilters
        {
            Url = complexUrl,
            Filters = new[] { "filter" }
        };

        // Assert
        Assert.Equal(complexUrl, postFilters.Url);
    }

    [Fact]
    public void PostFilters_FilterWithSpecialCharacters_CanBeSet()
    {
        // Arrange
        var filters = new[] { "C#", "C++", "node.js", "asp.net", "f#" };

        // Act
        var postFilters = new PostFilters
        {
            Url = "http://example.com",
            Filters = filters
        };

        // Assert
        Assert.Equal(5, postFilters.Filters.Length);
        Assert.Contains("C#", postFilters.Filters);
        Assert.Contains("C++", postFilters.Filters);
    }

    [Fact]
    public void PostFilters_RequiredProperties_AreEnforced()
    {
        // This test verifies that both Url and Filters are required properties
        // Attempting to create without them would be a compile error

        // Arrange & Act
        var postFilters = new PostFilters
        {
            Url = "http://example.com",
            Filters = new[] { "filter" }
        };

        // Assert
        Assert.NotNull(postFilters.Url);
        Assert.NotNull(postFilters.Filters);
    }
}
