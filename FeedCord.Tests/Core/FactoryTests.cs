using FeedCord.Common;
using FeedCord.Core;
using FeedCord.Core.Factories;
using FeedCord.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace FeedCord.Tests.Core;

public class FactoryTests
{
    [Fact]
    public void DiscordPayloadServiceFactory_Create_ReturnsServiceWithConfig()
    {
        var services = new ServiceCollection();
        var provider = services.BuildServiceProvider();
        var sut = new DiscordPayloadServiceFactory(provider);
        var config = new Config
        {
            Id = "TestFeed",
            RssUrls = [],
            YoutubeUrls = [],
            DiscordWebhookUrl = "https://discord.com/api/webhooks/1/2",
            MarkdownFormat = false,
            Forum = false
        };

        var service = sut.Create(config);
        var payload = service.BuildPayloadWithPost(new Post(
            "Title",
            "",
            "Description",
            "https://post.example.com",
            "Tag",
            DateTime.UtcNow,
            "Author"));

        Assert.IsType<DiscordPayloadService>(service);
        Assert.NotNull(payload);
    }

    [Fact]
    public void LogAggregatorFactory_Create_ReturnsLogAggregatorWithInstanceId()
    {
        var services = new ServiceCollection();
        var batchLogger = new Mock<IBatchLogger>();
        services.AddSingleton(batchLogger.Object);

        var provider = services.BuildServiceProvider();
        var sut = new LogAggregatorFactory(provider);
        var config = new Config
        {
            Id = "FactoryId",
            RssUrls = [],
            YoutubeUrls = [],
            DiscordWebhookUrl = "https://discord.com/api/webhooks/1/2"
        };

        var aggregator = sut.Create(config);

        var concreteAggregator = Assert.IsType<LogAggregator>(aggregator);
        Assert.Equal("FactoryId", concreteAggregator.InstanceId);
    }
}
