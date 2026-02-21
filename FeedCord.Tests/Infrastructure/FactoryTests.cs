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
        var httpClientMock = new Mock<ICustomHttpClient>(MockBehavior.Loose);
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

        var payloadServiceMock = new Mock<IDiscordPayloadService>(MockBehavior.Loose);
        var notifier = sut.Create(config, payloadServiceMock.Object);

        Assert.IsType<DiscordNotifier>(notifier);
    }

    [Fact]
    public void FeedWorkerFactory_Create_ReturnsFeedWorkerAndLogsCreation()
    {
        var services = new ServiceCollection();
        var lifetimeMock = new Mock<IHostApplicationLifetime>(MockBehavior.Loose);
        services.AddSingleton(lifetimeMock.Object);
        services.AddLogging();

        var provider = services.BuildServiceProvider();
        var factoryLoggerMock = new Mock<ILogger<FeedWorkerFactory>>(MockBehavior.Loose);
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

        var aggregatorMock = new Mock<ILogAggregator>(MockBehavior.Loose);
        var feedManagerMock = new Mock<IFeedManager>(MockBehavior.Loose);
        var notifierMock = new Mock<INotifier>(MockBehavior.Loose);

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
