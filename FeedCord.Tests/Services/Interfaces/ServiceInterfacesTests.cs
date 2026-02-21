using System.Reflection;
using FeedCord.Common;
using FeedCord.Services.Interfaces;
using Xunit;

namespace FeedCord.Tests.Services.Interfaces;

public class ServiceInterfacesTests
{
    [Fact]
    public void AllServiceInterfaceTypes_ArePublicInterfaces()
    {
        var serviceInterfaceTypes = new[]
        {
            typeof(ICustomHttpClient),
            typeof(IFeedManager),
            typeof(IImageParserService),
            typeof(INotifier),
            typeof(IPostFilterService),
            typeof(IRssParsingService),
            typeof(IYoutubeParsingService)
        };

        foreach (var interfaceType in serviceInterfaceTypes)
        {
            Assert.True(interfaceType.IsInterface);
            Assert.True(interfaceType.IsPublic);
        }
    }

    [Fact]
    public void ICustomHttpClient_HasExpectedMethods()
    {
        var interfaceType = typeof(ICustomHttpClient);

        var getMethod = GetRequiredMethod(interfaceType, nameof(ICustomHttpClient.GetAsyncWithFallback), typeof(string), typeof(CancellationToken));
        Assert.Equal(typeof(Task<HttpResponseMessage?>), getMethod.ReturnType);

        var postMethod = GetRequiredMethod(interfaceType, nameof(ICustomHttpClient.PostAsyncWithFallback), typeof(string), typeof(StringContent), typeof(StringContent), typeof(bool), typeof(CancellationToken));
        Assert.Equal(typeof(Task), postMethod.ReturnType);
    }

    [Fact]
    public void IFeedManager_HasExpectedMethods()
    {
        var interfaceType = typeof(IFeedManager);

        var checkMethod = GetRequiredMethod(interfaceType, nameof(IFeedManager.CheckForNewPostsAsync), typeof(CancellationToken));
        Assert.Equal(typeof(Task<List<Post>>), checkMethod.ReturnType);

        var initializeMethod = GetRequiredMethod(interfaceType, nameof(IFeedManager.InitializeUrlsAsync), typeof(CancellationToken));
        Assert.Equal(typeof(Task), initializeMethod.ReturnType);

        var getAllDataMethod = GetRequiredMethod(interfaceType, nameof(IFeedManager.GetAllFeedData));
        Assert.Equal(typeof(IReadOnlyDictionary<string, FeedState>), getAllDataMethod.ReturnType);
    }

    [Fact]
    public void IImageParserService_HasExpectedMethods()
    {
        var interfaceType = typeof(IImageParserService);

        var parseMethod = GetRequiredMethod(interfaceType, nameof(IImageParserService.TryExtractImageLink), typeof(string), typeof(string));
        Assert.Equal(typeof(Task<string?>), parseMethod.ReturnType);
    }

    [Fact]
    public void INotifier_HasExpectedMethods()
    {
        var interfaceType = typeof(INotifier);

        var notifyMethod = GetRequiredMethod(interfaceType, nameof(INotifier.SendNotificationsAsync), typeof(List<Post>), typeof(CancellationToken));
        Assert.Equal(typeof(Task), notifyMethod.ReturnType);
    }

    [Fact]
    public void IPostFilterService_HasExpectedMethods()
    {
        var interfaceType = typeof(IPostFilterService);

        var filterMethod = GetRequiredMethod(interfaceType, nameof(IPostFilterService.ShouldIncludePost), typeof(Post), typeof(string));
        Assert.Equal(typeof(bool), filterMethod.ReturnType);
    }

    [Fact]
    public void IRssParsingService_HasExpectedMethods()
    {
        var interfaceType = typeof(IRssParsingService);

        var parseRssMethod = GetRequiredMethod(interfaceType, nameof(IRssParsingService.ParseRssFeedAsync), typeof(string), typeof(int));
        Assert.Equal(typeof(Task<List<Post?>>), parseRssMethod.ReturnType);

        var parseYoutubeMethod = GetRequiredMethod(interfaceType, nameof(IRssParsingService.ParseYoutubeFeedAsync), typeof(string));
        Assert.Equal(typeof(Task<Post?>), parseYoutubeMethod.ReturnType);
    }

    [Fact]
    public void IYoutubeParsingService_HasExpectedMethods()
    {
        var interfaceType = typeof(IYoutubeParsingService);

        var parseMethod = GetRequiredMethod(interfaceType, nameof(IYoutubeParsingService.GetXmlUrlAndFeed), typeof(string));
        Assert.Equal(typeof(Task<Post?>), parseMethod.ReturnType);
    }

    private static MethodInfo GetRequiredMethod(Type type, string methodName, params Type[] parameterTypes)
    {
        var method = type.GetMethod(methodName, parameterTypes);

        Assert.NotNull(method);
        return method!;
    }
}
