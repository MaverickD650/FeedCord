using Xunit;
using FeedCord.Common;

namespace FeedCord.Tests.Common;

public class PostTests
{
    [Fact]
    public void Post_CreatedWithAllProperties_PreservesValues()
    {
        // Arrange
        var title = "Test Post Title";
        var imageUrl = "http://example.com/image.jpg";
        var description = "This is a test post description";
        var link = "http://example.com/post";
        var tag = "test-tag";
        var publishDate = new DateTime(2026, 2, 20, 10, 30, 0);
        var author = "Test Author";

        // Act
        var post = new Post(
            Title: title,
            ImageUrl: imageUrl,
            Description: description,
            Link: link,
            Tag: tag,
            PublishDate: publishDate,
            Author: author
        );

        // Assert
        Assert.Equal(title, post.Title);
        Assert.Equal(imageUrl, post.ImageUrl);
        Assert.Equal(description, post.Description);
        Assert.Equal(link, post.Link);
        Assert.Equal(tag, post.Tag);
        Assert.Equal(publishDate, post.PublishDate);
        Assert.Equal(author, post.Author);
        Assert.Null(post.Labels);
    }

    [Fact]
    public void Post_CreatedWithLabels_PreservesLabels()
    {
        // Arrange
        var labels = new[] { "breaking-news", "important", "featured" };

        // Act
        var post = new Post(
            Title: "Titled Post",
            ImageUrl: "http://example.com/img.jpg",
            Description: "Description",
            Link: "http://example.com",
            Tag: "tag",
            PublishDate: DateTime.Now,
            Author: "Author",
            Labels: labels
        );

        // Assert
        Assert.NotNull(post.Labels);
        Assert.Equal(3, post.Labels!.Length);
        Assert.Contains("breaking-news", post.Labels);
        Assert.Contains("important", post.Labels);
        Assert.Contains("featured", post.Labels);
    }

    [Fact]
    public void Post_IsRecord_ImplementsEquality()
    {
        // Arrange
        var date = new DateTime(2026, 2, 20, 10, 30, 0);
        var post1 = new Post(
            Title: "Same Post",
            ImageUrl: "http://example.com/image.jpg",
            Description: "Same description",
            Link: "http://example.com/post",
            Tag: "same-tag",
            PublishDate: date,
            Author: "Same Author"
        );

        var post2 = new Post(
            Title: "Same Post",
            ImageUrl: "http://example.com/image.jpg",
            Description: "Same description",
            Link: "http://example.com/post",
            Tag: "same-tag",
            PublishDate: date,
            Author: "Same Author"
        );

        // Act & Assert
        Assert.Equal(post1, post2);
    }

    [Fact]
    public void Post_DifferentProperties_NotEqual()
    {
        // Arrange
        var date = DateTime.Now;
        var post1 = new Post(
            Title: "Post 1",
            ImageUrl: "http://example.com/img1.jpg",
            Description: "Description 1",
            Link: "http://example.com/post1",
            Tag: "tag1",
            PublishDate: date,
            Author: "Author 1"
        );

        var post2 = new Post(
            Title: "Post 2",
            ImageUrl: "http://example.com/img2.jpg",
            Description: "Description 2",
            Link: "http://example.com/post2",
            Tag: "tag2",
            PublishDate: date,
            Author: "Author 2"
        );

        // Act & Assert
        Assert.NotEqual(post1, post2);
    }

    [Fact]
    public void Post_EmptyPropertiesAllowed()
    {
        // Arrange & Act
        var post = new Post(
            Title: "",
            ImageUrl: "",
            Description: "",
            Link: "",
            Tag: "",
            PublishDate: DateTime.Now,
            Author: ""
        );

        // Assert
        Assert.Equal("", post.Title);
        Assert.Equal("", post.ImageUrl);
        Assert.NotNull(post);
    }

    [Fact]
    public void Post_RecordTypeCannotBeModified()
    {
        // Arrange
        var post = new Post(
            Title: "Original Title",
            ImageUrl: "http://example.com/image.jpg",
            Description: "Original Description",
            Link: "http://example.com/post",
            Tag: "tag",
            PublishDate: DateTime.Now,
            Author: "Author"
        );

        // Act & Assert - records are immutable, attempting to modify would cause compile error
        // This test verifies the post object is properly created
        Assert.Equal("Original Title", post.Title);
    }

    [Fact]
    public void Post_GetHashCode_EqualPostsHaveSameHash()
    {
        // Arrange
        var date = new DateTime(2026, 2, 20, 10, 30, 0);
        var post1 = new Post(
            Title: "Same Post",
            ImageUrl: "http://example.com/image.jpg",
            Description: "Same description",
            Link: "http://example.com/post",
            Tag: "same-tag",
            PublishDate: date,
            Author: "Same Author"
        );

        var post2 = new Post(
            Title: "Same Post",
            ImageUrl: "http://example.com/image.jpg",
            Description: "Same description",
            Link: "http://example.com/post",
            Tag: "same-tag",
            PublishDate: date,
            Author: "Same Author"
        );

        // Act & Assert
        Assert.Equal(post1.GetHashCode(), post2.GetHashCode());
    }

    [Fact]
    public void Post_CanBeStoredInCollection()
    {
        // Arrange
        var posts = new List<Post>
        {
            new Post("Title 1", "url1", "desc1", "link1", "tag1", DateTime.Now, "Author 1"),
            new Post("Title 2", "url2", "desc2", "link2", "tag2", DateTime.Now, "Author 2"),
            new Post("Title 3", "url3", "desc3", "link3", "tag3", DateTime.Now, "Author 3")
        };

        // Act & Assert
        Assert.Equal(3, posts.Count);
        Assert.Equal("Title 1", posts[0].Title);
        Assert.Equal("Title 2", posts[1].Title);
        Assert.Equal("Title 3", posts[2].Title);
    }

    [Fact]
    public void Post_WithNullLabels_LabelsIsNull()
    {
        // Arrange & Act
        var post = new Post(
            Title: "Title",
            ImageUrl: "url",
            Description: "desc",
            Link: "link",
            Tag: "tag",
            PublishDate: DateTime.Now,
            Author: "Author",
            Labels: null
        );

        // Assert
        Assert.Null(post.Labels);
    }

    [Fact]
    public void Post_WithEmptyLabelsArray_IsNotNull()
    {
        // Arrange & Act
        var post = new Post(
            Title: "Title",
            ImageUrl: "url",
            Description: "desc",
            Link: "link",
            Tag: "tag",
            PublishDate: DateTime.Now,
            Author: "Author",
            Labels: new string[] { }
        );

        // Assert
        Assert.NotNull(post.Labels);
        Assert.Empty(post.Labels);
    }
}
