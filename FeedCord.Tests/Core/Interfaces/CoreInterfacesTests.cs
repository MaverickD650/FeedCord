using System.Collections.Concurrent;
using System.Reflection;
using FeedCord.Common;
using FeedCord.Core;
using FeedCord.Core.Interfaces;
using Xunit;

namespace FeedCord.Tests.Core.Interfaces;

public class CoreInterfacesTests
{
  [Fact]
  public void AllCoreInterfaceTypes_ArePublicInterfaces()
  {
    var coreInterfaceTypes = new[]
    {
            typeof(IBatchLogger),
            typeof(IDiscordPayloadService),
            typeof(ILogAggregator)
        };

    foreach (var interfaceType in coreInterfaceTypes)
    {
      Assert.True(interfaceType.IsInterface);
      Assert.True(interfaceType.IsPublic);
    }
  }

  [Fact]
  public void IBatchLogger_HasExpectedMethods()
  {
    var interfaceType = typeof(IBatchLogger);

    var consumeMethod = GetRequiredMethod(interfaceType, nameof(IBatchLogger.ConsumeLogData), typeof(LogAggregator));
    Assert.Equal(typeof(Task), consumeMethod.ReturnType);
  }

  [Fact]
  public void IDiscordPayloadService_HasExpectedMethods()
  {
    var interfaceType = typeof(IDiscordPayloadService);

    var forumMethod = GetRequiredMethod(interfaceType, nameof(IDiscordPayloadService.BuildForumWithPost), typeof(Post));
    Assert.Equal(typeof(StringContent), forumMethod.ReturnType);

    var payloadMethod = GetRequiredMethod(interfaceType, nameof(IDiscordPayloadService.BuildPayloadWithPost), typeof(Post));
    Assert.Equal(typeof(StringContent), payloadMethod.ReturnType);
  }

  [Fact]
  public void ILogAggregator_HasExpectedMethods()
  {
    var interfaceType = typeof(ILogAggregator);

    var sendMethod = GetRequiredMethod(interfaceType, nameof(ILogAggregator.SendToBatchAsync));
    Assert.Equal(typeof(Task), sendMethod.ReturnType);

    var setStartTimeMethod = GetRequiredMethod(interfaceType, nameof(ILogAggregator.SetStartTime), typeof(DateTime));
    Assert.Equal(typeof(void), setStartTimeMethod.ReturnType);

    var setEndTimeMethod = GetRequiredMethod(interfaceType, nameof(ILogAggregator.SetEndTime), typeof(DateTime));
    Assert.Equal(typeof(void), setEndTimeMethod.ReturnType);

    var setNewPostCountMethod = GetRequiredMethod(interfaceType, nameof(ILogAggregator.SetNewPostCount), typeof(int));
    Assert.Equal(typeof(void), setNewPostCountMethod.ReturnType);

    var addLatestUrlPostMethod = GetRequiredMethod(interfaceType, nameof(ILogAggregator.AddLatestUrlPost), typeof(string), typeof(Post));
    Assert.Equal(typeof(void), addLatestUrlPostMethod.ReturnType);

    var addUrlResponseMethod = GetRequiredMethod(interfaceType, nameof(ILogAggregator.AddUrlResponse), typeof(string), typeof(int));
    Assert.Equal(typeof(void), addUrlResponseMethod.ReturnType);

    var resetMethod = GetRequiredMethod(interfaceType, nameof(ILogAggregator.Reset));
    Assert.Equal(typeof(void), resetMethod.ReturnType);

    var getResponsesMethod = GetRequiredMethod(interfaceType, nameof(ILogAggregator.GetUrlResponses));
    Assert.Equal(typeof(ConcurrentDictionary<string, int>), getResponsesMethod.ReturnType);
  }

  private static MethodInfo GetRequiredMethod(Type type, string methodName, params Type[] parameterTypes)
  {
    var method = type.GetMethod(methodName, parameterTypes);

    Assert.NotNull(method);
    return method!;
  }
}
