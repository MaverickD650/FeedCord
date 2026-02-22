using FeedCord.Common;
using FeedCord.Core;
using FeedCord.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FeedCord.Tests.Core;

public class BatchLoggerTests
{
  private static async Task WaitForProcessingAsync(Mock<ILogger<BatchLogger>> loggerMock)
  {
    for (var attempt = 0; attempt < 30; attempt++)
    {
      if (loggerMock.Invocations.Any(i => i.Method.Name == nameof(ILogger.Log)))
      {
        return;
      }

      await Task.Delay(20, TestContext.Current.CancellationToken);
    }
  }

  private static LogAggregator CreateLogAggregator(string id = "TestFeed")
  {
    var batchLoggerMock = new Mock<IBatchLogger>(MockBehavior.Loose);
    var config = new Config
    {
      Id = id,
      RssUrls = [],
      YoutubeUrls = [],
      DiscordWebhookUrl = "https://discord.com/api/webhooks/1/2"
    };

    return new LogAggregator(batchLoggerMock.Object, config);
  }

  [Fact(Timeout = 10000)]
  public async Task ConsumeLogData_WhenNoNewPosts_LogsLatestPostsAndResets()
  {
    var loggerMock = new Mock<ILogger<BatchLogger>>(MockBehavior.Loose);
    var sut = new BatchLogger(loggerMock.Object);
    var logItem = CreateLogAggregator("DailyRun");

    logItem.SetStartTime(new DateTime(2026, 2, 20, 9, 0, 0));
    logItem.SetEndTime(new DateTime(2026, 2, 20, 9, 5, 0));
    logItem.AddLatestUrlPost("https://feed.example.com", new Post(
        "Title",
        "https://image.example.com",
        "Description",
        "https://post.example.com",
        "Tag",
        new DateTime(2026, 2, 20, 8, 50, 0),
        "Author"));

    await sut.ConsumeLogData(logItem);
    await WaitForProcessingAsync(loggerMock);

    loggerMock.Verify(
        x => x.Log(
            LogLevel.Information,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((value, _) => value.ToString()!.Contains("No new posts found")),
            It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
        Times.AtLeastOnce);

    Assert.Empty(logItem.UrlStatuses);
    Assert.Empty(logItem.LatestPosts);
    Assert.Equal(0, logItem.NewPostCount);
  }

  [Fact(Timeout = 10000)]
  public async Task ConsumeLogData_WithFailures_LogsFailedStatusDetails()
  {
    var loggerMock = new Mock<ILogger<BatchLogger>>(MockBehavior.Loose);
    var sut = new BatchLogger(loggerMock.Object);
    var logItem = CreateLogAggregator();

    logItem.AddUrlResponse("https://ok.example.com", 200);
    logItem.AddUrlResponse("https://timeout.example.com", -99);
    logItem.AddUrlResponse("https://bad.example.com", 500);

    await sut.ConsumeLogData(logItem);
    await WaitForProcessingAsync(loggerMock);

    loggerMock.Verify(
        x => x.Log(
            LogLevel.Information,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((value, _) =>
                value.ToString()!.Contains("Request Timed Out") &&
                value.ToString()!.Contains("Response Status: 500")),
            It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
        Times.AtLeastOnce);
  }

  [Fact(Timeout = 10000)]
  public async Task ConsumeLogData_WithNewPosts_LogsNewPostCount()
  {
    var loggerMock = new Mock<ILogger<BatchLogger>>(MockBehavior.Loose);
    var sut = new BatchLogger(loggerMock.Object);
    var logItem = CreateLogAggregator();

    logItem.SetNewPostCount(3);

    await sut.ConsumeLogData(logItem);
    await WaitForProcessingAsync(loggerMock);

    loggerMock.Verify(
        x => x.Log(
            LogLevel.Information,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((value, _) => value.ToString()!.Contains("3 new posts found in the feed")),
            It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
        Times.AtLeastOnce);
  }

  [Fact]
  public async Task ConsumeLogData_WithNullLatestPost_LogsUrlSummarySafely()
  {
    var loggerMock = new Mock<ILogger<BatchLogger>>(MockBehavior.Loose);
    var sut = new BatchLogger(loggerMock.Object);
    var logItem = CreateLogAggregator("NullPostRun");

    logItem.AddLatestUrlPost("https://feed.example.com/null", null);

    await sut.ConsumeLogData(logItem);
    await WaitForProcessingAsync(loggerMock);

    loggerMock.Verify(
        x => x.Log(
            LogLevel.Information,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((value, _) => value.ToString()!.Contains("https://feed.example.com/null")),
            It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
        Times.AtLeastOnce);
  }
}
