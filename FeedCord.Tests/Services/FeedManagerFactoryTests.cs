using FeedCord.Common;
using FeedCord.Core.Interfaces;
using FeedCord.Services;
using FeedCord.Services.Factories;
using FeedCord.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace FeedCord.Tests.Services;

public class FeedManagerFactoryTests
{
    [Fact]
    public void Create_ReturnsFeedManagerInstance()
    {
        var services = new ServiceCollection();
        var httpClientMock = new Mock<ICustomHttpClient>(MockBehavior.Loose);
        var rssParserMock = new Mock<IRssParsingService>(MockBehavior.Loose);

        services.AddLogging();
        services.AddSingleton(httpClientMock.Object);
        services.AddSingleton(rssParserMock.Object);

        var provider = services.BuildServiceProvider();
        var sut = new FeedManagerFactory(provider);
        var config = new Config
        {
            Id = "FactoryFeed",
            RssUrls = ["https://feed.example.com"],
            YoutubeUrls = [],
            DiscordWebhookUrl = "https://discord.com/api/webhooks/1/2",
            DescriptionLimit = 250,
            ConcurrentRequests = 2,
            RssCheckIntervalMinutes = 10
        };

        var aggregatorMock = new Mock<ILogAggregator>(MockBehavior.Loose);

        var feedManager = sut.Create(config, aggregatorMock.Object);

        Assert.IsType<FeedManager>(feedManager);
        Assert.NotNull(feedManager.GetAllFeedData());
    }
}
