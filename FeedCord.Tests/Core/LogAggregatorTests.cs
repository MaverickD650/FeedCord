using Xunit;
using Moq;
using FeedCord.Core;
using FeedCord.Core.Interfaces;
using FeedCord.Common;
using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;

namespace FeedCord.Tests.Core;

public class LogAggregatorTests
{
  private readonly Mock<IBatchLogger> _mockBatchLogger;

  public LogAggregatorTests()
  {
    _mockBatchLogger = new Mock<IBatchLogger>(MockBehavior.Loose);
  }

  private Config CreateTestConfig(string id = "TestConfig")
  {
    return new Config
    {
      Id = id,
      RssUrls = new string[] { },
      YoutubeUrls = new string[] { },
      DiscordWebhookUrl = "https://discord.com/api/webhooks/123/abc",
      RssCheckIntervalSeconds = 30,
      DescriptionLimit = 250,
      Forum = false,
      MarkdownFormat = false,
      PersistenceOnShutdown = false
    };
  }

  private Post CreateTestPost(string title = "Test Post")
  {
    return new Post(
        Title: title,
        ImageUrl: "http://example.com/image.jpg",
        Description: "Test description",
        Link: "http://example.com/post",
        Tag: "test",
        PublishDate: DateTime.Now,
        Author: "Test Author"
    );
  }

  [Fact]
  public void Constructor_InitializesWithConfigId()
  {
    // Arrange
    var config = CreateTestConfig("MyFeed");

    // Act
    var aggregator = new LogAggregator(_mockBatchLogger.Object, config);

    // Assert
    Assert.Equal("MyFeed", aggregator.InstanceId);
  }

  [Fact]
  public void Constructor_InitializesEmptyCollections()
  {
    // Arrange
    var config = CreateTestConfig();

    // Act
    var aggregator = new LogAggregator(_mockBatchLogger.Object, config);

    // Assert
    Assert.Empty(aggregator.UrlStatuses);
    Assert.Empty(aggregator.LatestPosts);
    Assert.Equal(0, aggregator.NewPostCount);
    Assert.Null(aggregator.LatestPost);
  }

  [Fact]
  public void SetStartTime_SetsStartTimeCorrectly()
  {
    // Arrange
    var config = CreateTestConfig();
    var aggregator = new LogAggregator(_mockBatchLogger.Object, config);
    var startTime = new DateTime(2026, 2, 20, 10, 30, 0);

    // Act
    aggregator.SetStartTime(startTime);

    // Assert
    Assert.Equal(startTime, aggregator.StartTime);
  }

  [Fact]
  public void SetEndTime_SetsEndTimeCorrectly()
  {
    // Arrange
    var config = CreateTestConfig();
    var aggregator = new LogAggregator(_mockBatchLogger.Object, config);
    var endTime = new DateTime(2026, 2, 20, 10, 45, 0);

    // Act
    aggregator.SetEndTime(endTime);

    // Assert
    Assert.Equal(endTime, aggregator.EndTime);
  }

  [Fact]
  public void SetNewPostCount_SetsCountCorrectly()
  {
    // Arrange
    var config = CreateTestConfig();
    var aggregator = new LogAggregator(_mockBatchLogger.Object, config);

    // Act
    aggregator.SetNewPostCount(5);

    // Assert
    Assert.Equal(5, aggregator.NewPostCount);
  }

  [Fact]
  public void SetRecentPost_SetsLatestPostCorrectly()
  {
    // Arrange
    var config = CreateTestConfig();
    var aggregator = new LogAggregator(_mockBatchLogger.Object, config);
    var post = CreateTestPost("Recent Post");

    // Act
    aggregator.SetRecentPost(post);

    // Assert
    Assert.Equal(post, aggregator.LatestPost);
    Assert.Equal("Recent Post", aggregator.LatestPost?.Title);
  }

  [Fact]
  public void SetRecentPost_CanBeSetToNull()
  {
    // Arrange
    var config = CreateTestConfig();
    var aggregator = new LogAggregator(_mockBatchLogger.Object, config);
    aggregator.SetRecentPost(CreateTestPost());

    // Act
    aggregator.SetRecentPost(null);

    // Assert
    Assert.Null(aggregator.LatestPost);
  }

  [Fact]
  public void AddUrlResponse_AddsStatusCodeCorrectly()
  {
    // Arrange
    var config = CreateTestConfig();
    var aggregator = new LogAggregator(_mockBatchLogger.Object, config);
    var url = "http://example.com/rss";

    // Act
    aggregator.AddUrlResponse(url, 200);

    // Assert
    Assert.True(aggregator.UrlStatuses.ContainsKey(url));
    Assert.Equal(200, aggregator.UrlStatuses[url]);
  }

  [Fact]
  public void AddUrlResponse_MultipleUrls_AllAreStored()
  {
    // Arrange
    var config = CreateTestConfig();
    var aggregator = new LogAggregator(_mockBatchLogger.Object, config);

    // Act
    aggregator.AddUrlResponse("http://example1.com", 200);
    aggregator.AddUrlResponse("http://example2.com", 404);
    aggregator.AddUrlResponse("http://example3.com", 500);

    // Assert
    Assert.Equal(3, aggregator.UrlStatuses.Count);
    Assert.Equal(200, aggregator.UrlStatuses["http://example1.com"]);
    Assert.Equal(404, aggregator.UrlStatuses["http://example2.com"]);
    Assert.Equal(500, aggregator.UrlStatuses["http://example3.com"]);
  }

  [Fact]
  public void AddUrlResponse_DuplicateUrl_OnlyFirstIsStored()
  {
    // Arrange
    var config = CreateTestConfig();
    var aggregator = new LogAggregator(_mockBatchLogger.Object, config);
    var url = "http://example.com/rss";

    // Act
    aggregator.AddUrlResponse(url, 200);
    aggregator.AddUrlResponse(url, 404);  // Attempt to add again

    // Assert
    Assert.Single(aggregator.UrlStatuses);
    Assert.Equal(404, aggregator.UrlStatuses[url]);  // Latest value updated
  }

  [Fact]
  public void AddLatestUrlPost_AddsPostCorrectly()
  {
    // Arrange
    var config = CreateTestConfig();
    var aggregator = new LogAggregator(_mockBatchLogger.Object, config);
    var url = "http://example.com/rss";
    var post = CreateTestPost("Latest Post");

    // Act
    aggregator.AddLatestUrlPost(url, post);

    // Assert
    Assert.True(aggregator.LatestPosts.ContainsKey(url));
    Assert.Equal(post, aggregator.LatestPosts[url]);
  }

  [Fact]
  public void AddLatestUrlPost_MultipleUrls_AllAreStored()
  {
    // Arrange
    var config = CreateTestConfig();
    var aggregator = new LogAggregator(_mockBatchLogger.Object, config);

    // Act
    aggregator.AddLatestUrlPost("http://feed1.com", CreateTestPost("Post 1"));
    aggregator.AddLatestUrlPost("http://feed2.com", CreateTestPost("Post 2"));

    // Assert
    Assert.Equal(2, aggregator.LatestPosts.Count);
    Assert.Equal("Post 1", aggregator.LatestPosts["http://feed1.com"]?.Title);
    Assert.Equal("Post 2", aggregator.LatestPosts["http://feed2.com"]?.Title);
  }

  [Fact]
  public void AddLatestUrlPost_CanAddNull()
  {
    // Arrange
    var config = CreateTestConfig();
    var aggregator = new LogAggregator(_mockBatchLogger.Object, config);
    var url = "http://example.com/rss";

    // Act
    aggregator.AddLatestUrlPost(url, null);

    // Assert
    Assert.True(aggregator.LatestPosts.ContainsKey(url));
    Assert.Null(aggregator.LatestPosts[url]);
  }

  [Fact]
  public void GetUrlResponses_ReturnsUrlStatusDictionary()
  {
    // Arrange
    var config = CreateTestConfig();
    var aggregator = new LogAggregator(_mockBatchLogger.Object, config);
    aggregator.AddUrlResponse("http://example.com", 200);

    // Act
    var result = aggregator.GetUrlResponses();

    // Assert
    Assert.NotNull(result);
    Assert.IsType<ConcurrentDictionary<string, int>>(result);
    Assert.Single(result);
  }

  [Fact]
  public async Task SendToBatchAsync_CallsBatchLoggerConsumeLogData()
  {
    // Arrange
    var config = CreateTestConfig();
    var aggregator = new LogAggregator(_mockBatchLogger.Object, config);

    // Act
    await aggregator.SendToBatchAsync();

    // Assert
    _mockBatchLogger.Verify(x => x.ConsumeLogData(aggregator), Times.Once);
  }

  [Fact]
  public void Reset_ClearsAllData()
  {
    // Arrange
    var config = CreateTestConfig();
    var aggregator = new LogAggregator(_mockBatchLogger.Object, config);
    aggregator.SetStartTime(DateTime.Now);
    aggregator.SetEndTime(DateTime.Now);
    aggregator.SetNewPostCount(5);
    aggregator.SetRecentPost(CreateTestPost());
    aggregator.AddUrlResponse("http://example.com", 200);
    aggregator.AddLatestUrlPost("http://example.com", CreateTestPost());

    // Act
    aggregator.Reset();

    // Assert
    Assert.Equal(default(DateTime), aggregator.StartTime);
    Assert.Equal(default(DateTime), aggregator.EndTime);
    Assert.Equal(0, aggregator.NewPostCount);
    Assert.Null(aggregator.LatestPost);
    Assert.Empty(aggregator.UrlStatuses);
    Assert.Empty(aggregator.LatestPosts);
  }

  [Fact]
  public void UrlStatuses_IsConcurrentDictionary()
  {
    // Arrange
    var config = CreateTestConfig();

    // Act
    var aggregator = new LogAggregator(_mockBatchLogger.Object, config);

    // Assert
    Assert.IsType<ConcurrentDictionary<string, int>>(aggregator.UrlStatuses);
  }

  [Fact]
  public void LatestPosts_IsConcurrentDictionary()
  {
    // Arrange
    var config = CreateTestConfig();

    // Act
    var aggregator = new LogAggregator(_mockBatchLogger.Object, config);

    // Assert
    Assert.IsType<ConcurrentDictionary<string, Post?>>(aggregator.LatestPosts);
  }

  [Fact]
  public void MultipleOperations_MaintainCorrectState()
  {
    // Arrange
    var config = CreateTestConfig();
    var aggregator = new LogAggregator(_mockBatchLogger.Object, config);
    var startTime = new DateTime(2026, 2, 20, 10, 0, 0);
    var endTime = new DateTime(2026, 2, 20, 10, 15, 0);

    // Act
    aggregator.SetStartTime(startTime);
    aggregator.SetEndTime(endTime);
    aggregator.SetNewPostCount(3);
    aggregator.SetRecentPost(CreateTestPost("Most Recent"));
    aggregator.AddUrlResponse("http://feed1.com", 200);
    aggregator.AddUrlResponse("http://feed2.com", 404);
    aggregator.AddLatestUrlPost("http://feed1.com", CreateTestPost("Post from Feed 1"));
    aggregator.AddLatestUrlPost("http://feed2.com", null);

    // Assert
    Assert.Equal(startTime, aggregator.StartTime);
    Assert.Equal(endTime, aggregator.EndTime);
    Assert.Equal(3, aggregator.NewPostCount);
    Assert.Equal("Most Recent", aggregator.LatestPost?.Title);
    Assert.Equal(2, aggregator.UrlStatuses.Count);
    Assert.Equal(2, aggregator.LatestPosts.Count);
    Assert.Equal(200, aggregator.UrlStatuses["http://feed1.com"]);
    Assert.Equal(404, aggregator.UrlStatuses["http://feed2.com"]);
  }

  [Fact]
  public void AddUrlResponse_WithRepeatedCall_UpdatesValue()
  {
    // Arrange
    var config = CreateTestConfig();
    var aggregator = new LogAggregator(_mockBatchLogger.Object, config);
    var url = "http://example.com";

    // Act
    aggregator.AddUrlResponse(url, 200);
    aggregator.AddUrlResponse(url, 500);  // Update same URL with different status

    // Assert
    Assert.Single(aggregator.UrlStatuses);
    Assert.Equal(500, aggregator.UrlStatuses[url]);  // Should reflect the latest value
  }

  [Fact]
  public void AddLatestUrlPost_WithRepeatedCall_UpdatesValue()
  {
    // Arrange
    var config = CreateTestConfig();
    var aggregator = new LogAggregator(_mockBatchLogger.Object, config);
    var url = "http://example.com";
    var post1 = CreateTestPost("First Post");
    var post2 = CreateTestPost("Second Post");

    // Act
    aggregator.AddLatestUrlPost(url, post1);
    aggregator.AddLatestUrlPost(url, post2);  // Update same URL with different post

    // Assert
    Assert.Single(aggregator.LatestPosts);
    Assert.Equal("Second Post", aggregator.LatestPosts[url]?.Title);  // Should reflect the latest value
  }
}

public class LogAggregatorFactoryTests
{
  [Fact]
  public void ActivatorUtilities_CreateLogAggregator_ReturnsLogAggregatorWithInstanceId()
  {
    var services = new ServiceCollection();
    var batchLogger = new Mock<IBatchLogger>(MockBehavior.Loose);
    services.AddSingleton(batchLogger.Object);

    var provider = services.BuildServiceProvider();
    var config = new Config
    {
      Id = "FactoryId",
      RssUrls = [],
      YoutubeUrls = [],
      DiscordWebhookUrl = "https://discord.com/api/webhooks/1/2"
    };

    var aggregator = ActivatorUtilities.CreateInstance<LogAggregator>(provider, config);

    var concreteAggregator = Assert.IsType<LogAggregator>(aggregator);
    Assert.Equal("FactoryId", concreteAggregator.InstanceId);
  }
}
