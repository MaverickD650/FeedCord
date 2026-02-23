using FeedCord.Common;
using FeedCord.Core.Interfaces;
using FeedCord.Services;
using FeedCord.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FeedCord.Tests.Services;

public class FeedManagerFactoryTests
{
  [Fact]
  public void ActivatorUtilities_Create_ReturnsFeedManagerInstance()
  {
    var services = new ServiceCollection();
    var httpClientMock = new Mock<ICustomHttpClient>(MockBehavior.Loose);
    var rssParserMock = new Mock<IRssParsingService>(MockBehavior.Loose);

    services.AddLogging();
    services.AddSingleton(httpClientMock.Object);
    services.AddSingleton(rssParserMock.Object);

    var provider = services.BuildServiceProvider();
    var config = new Config
    {
      Id = "FactoryFeed",
      RssUrls = ["https://feed.example.com"],
      YoutubeUrls = [],
      DiscordWebhookUrl = "https://discord.com/api/webhooks/1/2",
      DescriptionLimit = 250,
      ConcurrentRequests = 2,
      RssCheckIntervalSeconds = 10
    };

    var aggregatorMock = new Mock<ILogAggregator>(MockBehavior.Loose);
    var postFilterLogger = provider.GetRequiredService<ILogger<PostFilterService>>();
    var postFilterService = new PostFilterService(postFilterLogger, config);

    var feedManager = ActivatorUtilities.CreateInstance<FeedManager>(provider, config, aggregatorMock.Object, postFilterService);

    Assert.IsType<FeedManager>(feedManager);
    Assert.NotNull(feedManager.GetAllFeedData());
  }
}
