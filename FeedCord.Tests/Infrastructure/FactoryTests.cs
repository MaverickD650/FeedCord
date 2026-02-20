using FeedCord.Common;
using FeedCord.Core.Interfaces;
using FeedCord.Infrastructure.Factories;
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
    public void NotifierFactory_Create_ReturnsDiscordNotifier()
    {
        var services = new ServiceCollection();
        var httpClientMock = new Mock<ICustomHttpClient>();
        services.AddSingleton(httpClientMock.Object);

        var provider = services.BuildServiceProvider();
        var sut = new NotifierFactory(provider);

        var config = new Config
        {
            Id = "NotifierFactory",
            RssUrls = [],
            YoutubeUrls = [],
            DiscordWebhookUrl = "https://discord.com/api/webhooks/1/2"
        };

        var payloadServiceMock = new Mock<IDiscordPayloadService>();
        var notifier = sut.Create(config, payloadServiceMock.Object);

        Assert.IsType<DiscordNotifier>(notifier);
    }

    [Fact]
    public void FeedWorkerFactory_Create_ReturnsFeedWorkerAndLogsCreation()
    {
        var services = new ServiceCollection();
        var lifetimeMock = new Mock<IHostApplicationLifetime>();
        services.AddSingleton(lifetimeMock.Object);
        services.AddLogging();

        var provider = services.BuildServiceProvider();
        var factoryLoggerMock = new Mock<ILogger<FeedWorkerFactory>>();
        var sut = new FeedWorkerFactory(provider, factoryLoggerMock.Object);

        var config = new Config
        {
            Id = "WorkerFactory",
            RssUrls = [],
            YoutubeUrls = [],
            DiscordWebhookUrl = "https://discord.com/api/webhooks/1/2",
            RssCheckIntervalMinutes = 1,
            PersistenceOnShutdown = false
        };

        var aggregatorMock = new Mock<ILogAggregator>();
        var feedManagerMock = new Mock<IFeedManager>();
        var notifierMock = new Mock<INotifier>();

        var worker = sut.Create(config, aggregatorMock.Object, feedManagerMock.Object, notifierMock.Object);

        Assert.IsType<FeedWorker>(worker);
        factoryLoggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((value, _) => value.ToString()!.Contains("Creating new RssCheckerBackgroundService instance")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
