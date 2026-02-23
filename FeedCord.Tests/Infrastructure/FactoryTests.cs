using FeedCord.Common;
using FeedCord.Core.Interfaces;
using FeedCord.Infrastructure.Notifiers;
using FeedCord.Infrastructure.Workers;
using FeedCord.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FeedCord.Tests.Infrastructure;

public class FactoryTests
{
  [Fact]
  public void ActivatorUtilities_CreateNotifier_ReturnsDiscordNotifier()
  {
    var services = new ServiceCollection();
    var httpClientMock = new Mock<ICustomHttpClient>(MockBehavior.Loose);
    services.AddSingleton(httpClientMock.Object);

    var provider = services.BuildServiceProvider();

    var config = new Config
    {
      Id = "NotifierFactory",
      RssUrls = [],
      YoutubeUrls = [],
      DiscordWebhookUrl = "https://discord.com/api/webhooks/1/2"
    };

    var payloadServiceMock = new Mock<IDiscordPayloadService>(MockBehavior.Loose);
    var notifier = ActivatorUtilities.CreateInstance<DiscordNotifier>(provider, config, payloadServiceMock.Object);

    Assert.IsType<DiscordNotifier>(notifier);
  }

  [Fact]
  public void ActivatorUtilities_CreateFeedWorker_ReturnsFeedWorker()
  {
    var services = new ServiceCollection();
    var lifetimeMock = new Mock<IHostApplicationLifetime>(MockBehavior.Loose);
    services.AddSingleton(lifetimeMock.Object);
    services.AddLogging();

    var provider = services.BuildServiceProvider();

    var config = new Config
    {
      Id = "WorkerFactory",
      RssUrls = [],
      YoutubeUrls = [],
      DiscordWebhookUrl = "https://discord.com/api/webhooks/1/2",
      RssCheckIntervalSeconds = 1,
      PersistenceOnShutdown = false
    };

    var aggregatorMock = new Mock<ILogAggregator>(MockBehavior.Loose);
    var feedManagerMock = new Mock<IFeedManager>(MockBehavior.Loose);
    var notifierMock = new Mock<INotifier>(MockBehavior.Loose);

    var worker = ActivatorUtilities.CreateInstance<FeedWorker>(provider, config, aggregatorMock.Object, feedManagerMock.Object, notifierMock.Object);

    Assert.IsType<FeedWorker>(worker);
  }
}
