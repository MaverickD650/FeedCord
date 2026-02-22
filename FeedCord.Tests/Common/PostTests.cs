using Xunit;
using FeedCord.Common;

namespace FeedCord.Tests.Common;

public class PostTests
{
  private static readonly DateTime FixedPublishDate = new(2026, 2, 20, 10, 30, 0);

  [Fact]
  public void Post_CreatedWithAllProperties_PreservesValues()
  {
    var post = new Post(
        Title: "Test Post Title",
        ImageUrl: "http://example.com/image.jpg",
        Description: "This is a test post description",
        Link: "http://example.com/post",
        Tag: "test-tag",
        PublishDate: FixedPublishDate,
        Author: "Test Author"
    );

    Assert.Equal("Test Post Title", post.Title);
    Assert.Equal("http://example.com/image.jpg", post.ImageUrl);
    Assert.Equal("This is a test post description", post.Description);
    Assert.Equal("http://example.com/post", post.Link);
    Assert.Equal("test-tag", post.Tag);
    Assert.Equal(FixedPublishDate, post.PublishDate);
    Assert.Equal("Test Author", post.Author);
    Assert.Null(post.Labels);
  }

  public static IEnumerable<object?[]> LabelScenarios()
  {
    yield return new object?[] { null };
    yield return new object?[] { Array.Empty<string>() };
    yield return new object?[] { new[] { "breaking-news", "important", "featured" } };
  }

  [Theory]
  [MemberData(nameof(LabelScenarios))]
  public void Post_Labels_PreserveAssignedValues(string[]? labels)
  {
    var post = new Post(
        Title: "Titled Post",
        ImageUrl: "http://example.com/img.jpg",
        Description: "Description",
        Link: "http://example.com",
        Tag: "tag",
        PublishDate: FixedPublishDate,
        Author: "Author",
        Labels: labels
    );

    if (labels is null)
    {
      Assert.Null(post.Labels);
      return;
    }

    Assert.NotNull(post.Labels);
    Assert.Equal(labels, post.Labels);
  }

  [Theory]
  [InlineData(true)]
  [InlineData(false)]
  public void Post_RecordEqualityAndHashCode_AreConsistent(bool useDifferentTitle)
  {
    var post1 = CreatePost("Same Post");
    var post2 = useDifferentTitle ? CreatePost("Different Post") : CreatePost("Same Post");

    if (useDifferentTitle)
    {
      Assert.NotEqual(post1, post2);
      return;
    }

    Assert.Equal(post1, post2);
    Assert.Equal(post1.GetHashCode(), post2.GetHashCode());
  }

  [Fact]
  public void Post_EmptyPropertiesAllowed()
  {
    var post = new Post(
        Title: "",
        ImageUrl: "",
        Description: "",
        Link: "",
        Tag: "",
        PublishDate: FixedPublishDate,
        Author: ""
    );

    Assert.Equal("", post.Title);
    Assert.Equal("", post.ImageUrl);
    Assert.NotNull(post);
  }

  [Fact]
  public void Post_WithExpression_CreatesNewInstanceWithUpdatedValues()
  {
    var original = CreatePost("Original Title");
    var updated = original with { Title = "Updated Title" };

    Assert.Equal("Original Title", original.Title);
    Assert.Equal("Updated Title", updated.Title);
    Assert.Equal(original.Description, updated.Description);
    Assert.NotSame(original, updated);
  }

  [Fact]
  public void Post_CanBeStoredInCollection()
  {
    var posts = new List<Post>
        {
            CreatePost("Title 1"),
            CreatePost("Title 2"),
            CreatePost("Title 3")
        };

    Assert.Collection(posts,
      post => Assert.Equal("Title 1", post.Title),
      post => Assert.Equal("Title 2", post.Title),
      post => Assert.Equal("Title 3", post.Title)
    );
  }

  private static Post CreatePost(string title)
  {
    return new Post(
        Title: title,
        ImageUrl: "http://example.com/image.jpg",
        Description: "Description",
        Link: "http://example.com/post",
        Tag: "tag",
        PublishDate: FixedPublishDate,
        Author: "Author"
    );
  }
}
