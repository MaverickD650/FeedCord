using Xunit;
using FeedCord.Services.Helpers;
using FeedCord.Common;

namespace FeedCord.Tests.Helpers;

public class FilterConfigsTests
{
    [Fact]
    public void GetFilterSuccess_NoFilters_ReturnsTrue()
    {
        // Arrange
        var post = new Post(
            Title: "Test Post",
            ImageUrl: "http://example.com/image.jpg",
            Description: "Test description",
            Link: "http://example.com/post",
            Tag: "test",
            PublishDate: DateTime.Now,
            Author: "Test Author"
        );

        // Act
        var result = FilterConfigs.GetFilterSuccess(post);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void GetFilterSuccess_EmptyFilters_ReturnsTrue()
    {
        // Arrange
        var post = new Post(
            Title: "Test Post",
            ImageUrl: "http://example.com/image.jpg",
            Description: "Test description",
            Link: "http://example.com/post",
            Tag: "test",
            PublishDate: DateTime.Now,
            Author: "Test Author"
        );
        var filters = new string[] { };

        // Act
        var result = FilterConfigs.GetFilterSuccess(post, filters);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void GetFilterSuccess_NullFilters_ReturnsTrue()
    {
        // Arrange
        var post = new Post(
            Title: "Test Post",
            ImageUrl: "http://example.com/image.jpg",
            Description: "Test description",
            Link: "http://example.com/post",
            Tag: "test",
            PublishDate: DateTime.Now,
            Author: "Test Author"
        );

        // Act
        var result = FilterConfigs.GetFilterSuccess(post, null!);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void GetFilterSuccess_FilterFoundInTitle_ReturnsTrue()
    {
        // Arrange
        var post = new Post(
            Title: "Breaking News: Important Update",
            ImageUrl: "http://example.com/image.jpg",
            Description: "Some description",
            Link: "http://example.com/post",
            Tag: "news",
            PublishDate: DateTime.Now,
            Author: "Author"
        );

        // Act
        var result = FilterConfigs.GetFilterSuccess(post, "Breaking");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void GetFilterSuccess_FilterFoundInDescription_ReturnsTrue()
    {
        // Arrange
        var post = new Post(
            Title: "Post Title",
            ImageUrl: "http://example.com/image.jpg",
            Description: "This is a breaking story about something important",
            Link: "http://example.com/post",
            Tag: "news",
            PublishDate: DateTime.Now,
            Author: "Author"
        );

        // Act
        var result = FilterConfigs.GetFilterSuccess(post, "breaking");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void GetFilterSuccess_FilterCasInsensitive_ReturnsTrue()
    {
        // Arrange
        var post = new Post(
            Title: "IMPORTANT NEWS",
            ImageUrl: "http://example.com/image.jpg",
            Description: "Description",
            Link: "http://example.com/post",
            Tag: "news",
            PublishDate: DateTime.Now,
            Author: "Author"
        );

        // Act
        var result = FilterConfigs.GetFilterSuccess(post, "important");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void GetFilterSuccess_FilterNotFound_ReturnsFalse()
    {
        // Arrange
        var post = new Post(
            Title: "Post Title",
            ImageUrl: "http://example.com/image.jpg",
            Description: "Post description",
            Link: "http://example.com/post",
            Tag: "news",
            PublishDate: DateTime.Now,
            Author: "Author"
        );

        // Act
        var result = FilterConfigs.GetFilterSuccess(post, "nonexistent");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void GetFilterSuccess_MultipleFilters_OneMatchesReturnTrue()
    {
        // Arrange
        var post = new Post(
            Title: "Breaking News Story",
            ImageUrl: "http://example.com/image.jpg",
            Description: "Description",
            Link: "http://example.com/post",
            Tag: "news",
            PublishDate: DateTime.Now,
            Author: "Author"
        );

        // Act
        var result = FilterConfigs.GetFilterSuccess(post, "nonexistent", "breaking", "another");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void GetFilterSuccess_MultipleFilters_NoneMatches_ReturnsFalse()
    {
        // Arrange
        var post = new Post(
            Title: "Post Title",
            ImageUrl: "http://example.com/image.jpg",
            Description: "Description",
            Link: "http://example.com/post",
            Tag: "news",
            PublishDate: DateTime.Now,
            Author: "Author"
        );

        // Act
        var result = FilterConfigs.GetFilterSuccess(post, "filter1", "filter2", "filter3");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void GetFilterSuccess_LabelFilterFound_ReturnsTrue()
    {
        // Arrange
        var post = new Post(
            Title: "Post Title",
            ImageUrl: "http://example.com/image.jpg",
            Description: "Description",
            Link: "http://example.com/post",
            Tag: "news",
            PublishDate: DateTime.Now,
            Author: "Author",
            Labels: new[] { "breaking", "important", "featured" }
        );

        // Act
        var result = FilterConfigs.GetFilterSuccess(post, "label:breaking");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void GetFilterSuccess_LabelFilterCaseInsensitive_ReturnsTrue()
    {
        // Arrange
        var post = new Post(
            Title: "Post Title",
            ImageUrl: "http://example.com/image.jpg",
            Description: "Description",
            Link: "http://example.com/post",
            Tag: "news",
            PublishDate: DateTime.Now,
            Author: "Author",
            Labels: new[] { "BREAKING", "important", "featured" }
        );

        // Act
        var result = FilterConfigs.GetFilterSuccess(post, "label:breaking");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void GetFilterSuccess_LabelFilterNotFound_ReturnsFalse()
    {
        // Arrange
        var post = new Post(
            Title: "Post Title",
            ImageUrl: "http://example.com/image.jpg",
            Description: "Description",
            Link: "http://example.com/post",
            Tag: "news",
            PublishDate: DateTime.Now,
            Author: "Author",
            Labels: new[] { "breaking", "important" }
        );

        // Act
        var result = FilterConfigs.GetFilterSuccess(post, "label:nonexistent");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void GetFilterSuccess_LabelFilterNoLabels_ReturnsFalse()
    {
        // Arrange
        var post = new Post(
            Title: "Post Title",
            ImageUrl: "http://example.com/image.jpg",
            Description: "Description",
            Link: "http://example.com/post",
            Tag: "news",
            PublishDate: DateTime.Now,
            Author: "Author"
            // No labels
        );

        // Act
        var result = FilterConfigs.GetFilterSuccess(post, "label:breaking");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void GetFilterSuccess_MixedFiltersLabelAndText_MatchesLabel_ReturnsTrue()
    {
        // Arrange
        var post = new Post(
            Title: "Post Title",
            ImageUrl: "http://example.com/image.jpg",
            Description: "Description",
            Link: "http://example.com/post",
            Tag: "news",
            PublishDate: DateTime.Now,
            Author: "Author",
            Labels: new[] { "important" }
        );

        // Act
        var result = FilterConfigs.GetFilterSuccess(post, "nonexistent-text", "label:important");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void GetFilterSuccess_MixedFiltersLabelAndText_MatchesText_ReturnsTrue()
    {
        // Arrange
        var post = new Post(
            Title: "Breaking News",
            ImageUrl: "http://example.com/image.jpg",
            Description: "Description",
            Link: "http://example.com/post",
            Tag: "news",
            PublishDate: DateTime.Now,
            Author: "Author"
        );

        // Act
        var result = FilterConfigs.GetFilterSuccess(post, "label:nonexistent", "breaking");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void GetFilterSuccess_NullTitleAndDescription_WithFilter_ReturnsFalse()
    {
        // Arrange
        var post = new Post(
            Title: null!,
            ImageUrl: "http://example.com/image.jpg",
            Description: null!,
            Link: "http://example.com/post",
            Tag: "news",
            PublishDate: DateTime.Now,
            Author: "Author"
        );

        // Act
        var result = FilterConfigs.GetFilterSuccess(post, "anything");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void GetFilterSuccess_EmptyLabelFilter_ReturnsTrue()
    {
        // Arrange
        var post = new Post(
            Title: "Post Title",
            ImageUrl: "http://example.com/image.jpg",
            Description: "Description",
            Link: "http://example.com/post",
            Tag: "news",
            PublishDate: DateTime.Now,
            Author: "Author",
            Labels: new[] { "", "important" }
        );

        // Act
        var result = FilterConfigs.GetFilterSuccess(post, "label:");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void GetFilterSuccess_PartialMatch_ReturnsTrue()
    {
        // Arrange
        var post = new Post(
            Title: "This is a long title with breaking news",
            ImageUrl: "http://example.com/image.jpg",
            Description: "Description",
            Link: "http://example.com/post",
            Tag: "news",
            PublishDate: DateTime.Now,
            Author: "Author"
        );

        // Act
        var result = FilterConfigs.GetFilterSuccess(post, "break");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void GetFilterSuccess_SpecialCharactersInFilter_ReturnsTrue()
    {
        // Arrange
        var post = new Post(
            Title: "C# Programming Tutorial",
            ImageUrl: "http://example.com/image.jpg",
            Description: "Description",
            Link: "http://example.com/post",
            Tag: "tech",
            PublishDate: DateTime.Now,
            Author: "Author"
        );

        // Act
        var result = FilterConfigs.GetFilterSuccess(post, "C#");

        // Assert
        Assert.True(result);
    }
}
