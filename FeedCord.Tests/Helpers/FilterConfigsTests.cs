using Xunit;
using FeedCord.Services.Helpers;
using FeedCord.Common;

namespace FeedCord.Tests.Helpers;

public class FilterConfigsTests
{
  public static IEnumerable<object?[]> EmptyFilterInputs()
  {
    yield return new object?[] { null };
    yield return new object?[] { Array.Empty<string>() };
  }

  [Theory]
  [MemberData(nameof(EmptyFilterInputs))]
  public void GetFilterSuccess_NoOrEmptyFilters_ReturnsTrue(string[]? filters)
  {
    var post = CreatePost();

    var result = filters is null
        ? FilterConfigs.GetFilterSuccess(post, null!)
        : FilterConfigs.GetFilterSuccess(post, filters);

    Assert.True(result);
  }

  [Theory]
  [InlineData("Breaking News: Important Update", "Some description", "Breaking", true)]
  [InlineData("Post Title", "This is a breaking story", "breaking", true)]
  [InlineData("IMPORTANT NEWS", "Description", "important", true)]
  [InlineData("This is a long title with breaking news", "Description", "break", true)]
  [InlineData("C# Programming Tutorial", "Description", "C#", true)]
  [InlineData("Post Title", "Post description", "nonexistent", false)]
  public void GetFilterSuccess_TextFilter_EvaluatesTitleAndDescription(
      string title,
      string description,
      string filter,
      bool expected)
  {
    var post = CreatePost(title: title, description: description);

    var result = FilterConfigs.GetFilterSuccess(post, filter);

    Assert.Equal(expected, result);
  }

  [Theory]
  [InlineData(new[] { "nonexistent", "breaking", "another" }, true)]
  [InlineData(new[] { "filter1", "filter2", "filter3" }, false)]
  public void GetFilterSuccess_MultipleTextFilters_ReturnsExpectedResult(string[] filters, bool expected)
  {
    var post = CreatePost(title: "Breaking News Story");

    var result = FilterConfigs.GetFilterSuccess(post, filters);

    Assert.Equal(expected, result);
  }

  [Theory]
  [InlineData(new[] { "breaking", "important", "featured" }, "label:breaking", true)]
  [InlineData(new[] { "BREAKING", "important", "featured" }, "label:breaking", true)]
  [InlineData(new[] { "breaking", "important" }, "label:nonexistent", false)]
  [InlineData(new[] { "", "important" }, "label:", true)]
  public void GetFilterSuccess_LabelFilter_ReturnsExpectedResult(string[] labels, string filter, bool expected)
  {
    var post = CreatePost(labels: labels);

    var result = FilterConfigs.GetFilterSuccess(post, filter);

    Assert.Equal(expected, result);
  }

  [Fact]
  public void GetFilterSuccess_LabelFilterWithoutLabels_ReturnsFalse()
  {
    var post = CreatePost(labels: null);

    var result = FilterConfigs.GetFilterSuccess(post, "label:breaking");

    Assert.False(result);
  }

  [Theory]
  [InlineData(new[] { "nonexistent-text", "label:important" }, true)]
  [InlineData(new[] { "label:nonexistent", "breaking" }, true)]
  [InlineData(new[] { "label:missing", "not-present" }, false)]
  public void GetFilterSuccess_MixedFilters_ReturnExpectedResult(string[] filters, bool expected)
  {
    var post = CreatePost(title: "Breaking News", labels: new[] { "important" });

    var result = FilterConfigs.GetFilterSuccess(post, filters);

    Assert.Equal(expected, result);
  }

  [Fact]
  public void GetFilterSuccess_NullTitleAndDescription_WithFilter_ReturnsFalse()
  {
    var post = CreatePost(title: null!, description: null!);

    var result = FilterConfigs.GetFilterSuccess(post, "anything");

    Assert.False(result);
  }

  private static Post CreatePost(string? title = "Post Title", string? description = "Description", string[]? labels = null)
  {
    return new Post(
        Title: title!,
        ImageUrl: "http://example.com/image.jpg",
        Description: description!,
        Link: "http://example.com/post",
        Tag: "news",
        PublishDate: DateTime.Now,
        Author: "Author",
        Labels: labels
    );
  }
}
