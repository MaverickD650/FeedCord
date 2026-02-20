using Xunit;
using FeedCord.Common;

namespace FeedCord.Tests.Common;

public class FeedStateTests
{
    [Fact]
    public void FeedState_CreatedWithInitValues_PreservesValuesAndAllowsErrorCountSet()
    {
        // Arrange
        var isYoutube = true;
        var lastPublishDate = new DateTime(2026, 2, 20, 10, 30, 0);

        // Act
        var feedState = new FeedState
        {
            IsYoutube = isYoutube,
            LastPublishDate = lastPublishDate,
            ErrorCount = 0
        };

        // Assert
        Assert.Equal(isYoutube, feedState.IsYoutube);
        Assert.Equal(lastPublishDate, feedState.LastPublishDate);
        Assert.Equal(0, feedState.ErrorCount);
    }

    [Fact]
    public void FeedState_IsYoutubeFalse_PreservesValue()
    {
        // Arrange & Act
        var feedState = new FeedState
        {
            IsYoutube = false,
            LastPublishDate = DateTime.Now,
            ErrorCount = 0
        };

        // Assert
        Assert.False(feedState.IsYoutube);
    }

    [Fact]
    public void FeedState_IsYoutubeTrue_PreservesValue()
    {
        // Arrange & Act
        var feedState = new FeedState
        {
            IsYoutube = true,
            LastPublishDate = DateTime.Now,
            ErrorCount = 0
        };

        // Assert
        Assert.True(feedState.IsYoutube);
    }

    [Fact]
    public void FeedState_LastPublishDate_CanBeSetAndUpdated()
    {
        // Arrange
        var feedState = new FeedState
        {
            IsYoutube = false,
            LastPublishDate = new DateTime(2026, 1, 1),
            ErrorCount = 0
        };

        var newDate = new DateTime(2026, 2, 20, 10, 30, 0);

        // Act
        feedState.LastPublishDate = newDate;

        // Assert
        Assert.Equal(newDate, feedState.LastPublishDate);
    }

    [Fact]
    public void FeedState_ErrorCount_CanBeSetAndIncremented()
    {
        // Arrange
        var feedState = new FeedState
        {
            IsYoutube = false,
            LastPublishDate = DateTime.Now,
            ErrorCount = 0
        };

        // Act
        feedState.ErrorCount = 5;
        feedState.ErrorCount++;

        // Assert
        Assert.Equal(6, feedState.ErrorCount);
    }

    [Fact]
    public void FeedState_ErrorCount_CanBeDecremented()
    {
        // Arrange
        var feedState = new FeedState
        {
            IsYoutube = false,
            LastPublishDate = DateTime.Now,
            ErrorCount = 5
        };

        // Act
        feedState.ErrorCount--;

        // Assert
        Assert.Equal(4, feedState.ErrorCount);
    }

    [Fact]
    public void FeedState_ErrorCount_CanBeSetToZero()
    {
        // Arrange
        var feedState = new FeedState
        {
            IsYoutube = false,
            LastPublishDate = DateTime.Now,
            ErrorCount = 10
        };

        // Act
        feedState.ErrorCount = 0;

        // Assert
        Assert.Equal(0, feedState.ErrorCount);
    }

    [Fact]
    public void FeedState_MultipleInstances_AreIndependent()
    {
        // Arrange
        var feedState1 = new FeedState
        {
            IsYoutube = true,
            LastPublishDate = new DateTime(2026, 1, 1),
            ErrorCount = 0
        };

        var feedState2 = new FeedState
        {
            IsYoutube = false,
            LastPublishDate = new DateTime(2026, 2, 1),
            ErrorCount = 5
        };

        // Act
        feedState1.ErrorCount++;
        feedState2.ErrorCount--;

        // Assert
        Assert.Equal(1, feedState1.ErrorCount);
        Assert.Equal(4, feedState2.ErrorCount);
        Assert.True(feedState1.IsYoutube);
        Assert.False(feedState2.IsYoutube);
    }

    [Fact]
    public void FeedState_IsInitProperty_CannotBeChangedAfterInit()
    {
        // Arrange
        var feedState = new FeedState
        {
            IsYoutube = true,
            LastPublishDate = DateTime.Now,
            ErrorCount = 0
        };

        // Assert - IsYoutube is init-only, so attempting reassignment would be a compile error
        // This test verifies it was set correctly
        Assert.True(feedState.IsYoutube);
    }

    [Fact]
    public void FeedState_DefaultValues_AreAsExpected()
    {
        // Arrange & Act
        var feedState = new FeedState();

        // Assert
        Assert.False(feedState.IsYoutube);  // Default bool is false
        Assert.Equal(default(DateTime), feedState.LastPublishDate);
        Assert.Equal(0, feedState.ErrorCount);
    }

    [Fact]
    public void FeedState_CanBeClearedOfErrors()
    {
        // Arrange
        var feedState = new FeedState
        {
            IsYoutube = false,
            LastPublishDate = DateTime.Now,
            ErrorCount = 100
        };

        // Act
        feedState.ErrorCount = 0;

        // Assert
        Assert.Equal(0, feedState.ErrorCount);
    }
}
