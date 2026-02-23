using FeedCord.Common;
using FeedCord.Core;
using FeedCord.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace FeedCord.Tests.Core;

public class FactoryTests
{
  [Fact]
  public void ActivatorUtilities_CreateDiscordPayloadService_ReturnsServiceWithConfig()
  {
    var services = new ServiceCollection();
    var provider = services.BuildServiceProvider();
    var config = new Config
    {
      Id = "TestFeed",
      RssUrls = [],
      YoutubeUrls = [],
      DiscordWebhookUrl = "https://discord.com/api/webhooks/1/2",
      MarkdownFormat = false,
      Forum = false
    };

    var service = ActivatorUtilities.CreateInstance<DiscordPayloadService>(provider, config);
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
