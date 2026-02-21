using FeedCord.Common;
using FeedCord.Helpers;
using System.Text.Json;
using Xunit;

namespace FeedCord.Tests.Helpers;

public class JsonReferencePostStoreTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly string _testFilePath;

    public JsonReferencePostStoreTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"feedcord_json_store_{Guid.NewGuid():N}");
        _testFilePath = Path.Combine(_testDirectory, "feed_dump.json");
        Directory.CreateDirectory(_testDirectory);
    }

    [Fact]
    public void LoadReferencePosts_WhenFileDoesNotExist_ReturnsEmptyDictionary()
    {
        var store = new JsonReferencePostStore(_testFilePath);

        var result = store.LoadReferencePosts();

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void LoadReferencePosts_WhenJsonIsMalformed_ReturnsEmptyDictionary()
    {
        File.WriteAllText(_testFilePath, "{ bad json");
        var store = new JsonReferencePostStore(_testFilePath);

        var result = store.LoadReferencePosts();

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void SaveReferencePosts_ThenLoadReferencePosts_RoundTripsData()
    {
        var store = new JsonReferencePostStore(_testFilePath);
        var now = DateTime.UtcNow;

        var data = new Dictionary<string, FeedState>
        {
            ["https://example.com/rss"] = new FeedState
            {
                IsYoutube = false,
                LastPublishDate = now,
                ErrorCount = 0
            },
            ["https://youtube.com/channel/test"] = new FeedState
            {
                IsYoutube = true,
                LastPublishDate = now.AddMinutes(-5),
                ErrorCount = 1
            }
        };

        store.SaveReferencePosts(data);
        var reloaded = store.LoadReferencePosts();

        Assert.Equal(2, reloaded.Count);
        Assert.False(reloaded["https://example.com/rss"].IsYoutube);
        Assert.True(reloaded["https://youtube.com/channel/test"].IsYoutube);
        Assert.Equal(now, reloaded["https://example.com/rss"].LastRunDate);
        Assert.Equal(now.AddMinutes(-5), reloaded["https://youtube.com/channel/test"].LastRunDate);
    }

    [Fact]
    public void LoadReferencePosts_WithDuplicateUrls_LastEntryWins()
    {
        var payload = new
        {
            Version = 1,
            Entries = new[]
            {
                new { Url = "https://example.com/rss", IsYoutube = false, LastRunDate = "2026-02-20T10:30:00Z" },
                new { Url = "https://example.com/rss", IsYoutube = true, LastRunDate = "2026-02-21T10:30:00Z" }
            }
        };

        File.WriteAllText(_testFilePath, JsonSerializer.Serialize(payload));
        var store = new JsonReferencePostStore(_testFilePath);

        var result = store.LoadReferencePosts();

        Assert.Single(result);
        Assert.True(result["https://example.com/rss"].IsYoutube);
        Assert.Equal(DateTime.Parse("2026-02-21T10:30:00Z").ToUniversalTime(), result["https://example.com/rss"].LastRunDate.ToUniversalTime());
    }

    [Fact]
    public void SaveReferencePosts_OverwritesExistingFile()
    {
        File.WriteAllText(_testFilePath, "legacy-content");
        var store = new JsonReferencePostStore(_testFilePath);

        var data = new Dictionary<string, FeedState>
        {
            ["https://example.com/rss"] = new FeedState
            {
                IsYoutube = false,
                LastPublishDate = DateTime.UtcNow,
                ErrorCount = 0
            }
        };

        store.SaveReferencePosts(data);

        var fileContent = File.ReadAllText(_testFilePath);
        Assert.Contains("\"Version\"", fileContent);
        Assert.DoesNotContain("legacy-content", fileContent);
        Assert.False(File.Exists($"{_testFilePath}.tmp"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }
}
