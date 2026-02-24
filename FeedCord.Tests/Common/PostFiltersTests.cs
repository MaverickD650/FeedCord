using Xunit;
using FeedCord.Common;

namespace FeedCord.Tests.Common;

public class PostFiltersTests
{
  public static IEnumerable<object[]> ConstructionScenarios()
  {
    yield return new object[] { "http://example.com/rss", new[] { "keyword1", "keyword2", "keyword3" } };
    yield return new object[] { string.Empty, new[] { "filter" } };
    yield return new object[] { "all", new[] { "important" } };
    yield return new object[] { "http://example.com:8080/api/v1/feeds/special?param=value&other=123", new[] { "filter" } };
    yield return new object[] { "http://example.com/rss", Array.Empty<string>() };
    yield return new object[] { "http://example.com", new[] { "C#", "C++", "node.js", "asp.net", "f#" } };
  }

  [Theory]
  [MemberData(nameof(ConstructionScenarios))]
  public void PostFilters_CreatedWithValues_PreservesValues(string url, string[] filters)
  {
    var postFilters = new PostFilters
    {
      Url = url,
      Filters = filters
    };

    Assert.Equal(url, postFilters.Url);
    Assert.Equal(filters, postFilters.Filters);
  }

  [Fact]
  public void PostFilters_Url_CanBeUpdated()
  {
    var postFilters = new PostFilters
    {
      Url = "http://example.com/rss1",
      Filters = new[] { "filter" }
    };

    postFilters.Url = "http://example.com/rss2";

    Assert.Equal("http://example.com/rss2", postFilters.Url);
  }

  [Fact]
  public void PostFilters_Filters_CanBeUpdated()
  {
    var postFilters = new PostFilters
    {
      Url = "http://example.com/rss",
      Filters = new[] { "filter1" }
    };

    var newFilters = new[] { "filter2", "filter3", "filter4" };
    postFilters.Filters = newFilters;

    Assert.Equal(newFilters, postFilters.Filters);
  }

  [Fact]
  public void PostFilters_MultipleInstances_AreIndependent()
  {
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

    filter1.Url = "http://modified1.com";
    filter2.Url = "http://modified2.com";

    Assert.Equal("http://modified1.com", filter1.Url);
    Assert.Equal("http://modified2.com", filter2.Url);
    Assert.Equal(new[] { "tag1", "tag2" }, filter1.Filters);
    Assert.Equal(new[] { "tag3", "tag4" }, filter2.Filters);
  }

  [Fact]
  public void PostFilters_RequiredProperties_AreSetWhenInitialized()
  {
    var postFilters = new PostFilters
    {
      Url = "http://example.com",
      Filters = new[] { "filter" }
    };

    Assert.NotNull(postFilters.Url);
    Assert.NotNull(postFilters.Filters);
  }
}
