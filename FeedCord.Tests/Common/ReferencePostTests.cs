using Xunit;
using FeedCord.Common;

namespace FeedCord.Tests.Common;

public class ReferencePostTests
{
  [Theory]
  [InlineData(true)]
  [InlineData(false)]
  public void ReferencePost_IsYoutube_PreservesConfiguredValue(bool isYoutube)
  {
    var lastRunDate = new DateTime(2026, 2, 20, 10, 30, 0);

    var referencePost = new ReferencePost
    {
      IsYoutube = isYoutube,
      LastRunDate = lastRunDate
    };

    Assert.Equal(isYoutube, referencePost.IsYoutube);
    Assert.Equal(lastRunDate, referencePost.LastRunDate);
  }

  [Fact]
  public void ReferencePost_LastRunDate_CanOnlyBeSetDuringInitialization()
  {
    // Arrange
    var expectedDate = new DateTime(2026, 2, 20, 10, 30, 0);

    // Act
    var referencePost = new ReferencePost
    {
      IsYoutube = true,
      LastRunDate = expectedDate
    };

    // Assert
    Assert.Equal(expectedDate, referencePost.LastRunDate);
    // LastRunDate is init-only, so it cannot be changed after initialization
  }

  [Fact]
  public void ReferencePost_IsYoutubeCanBeChanged()
  {
    // Arrange
    var referencePost = new ReferencePost
    {
      IsYoutube = false,
      LastRunDate = DateTime.Now
    };

    // Act
    referencePost.IsYoutube = true;

    // Assert
    Assert.True(referencePost.IsYoutube);
  }

  [Theory]
  [InlineData(false, true)]
  [InlineData(true, false)]
  public void ReferencePost_IsYoutubeCanBeToggled(bool initialValue, bool toggledValue)
  {
    var referencePost = new ReferencePost
    {
      IsYoutube = initialValue,
      LastRunDate = DateTime.Now
    };

    referencePost.IsYoutube = toggledValue;

    Assert.Equal(toggledValue, referencePost.IsYoutube);
  }

  [Fact]
  public void ReferencePost_MultipleInstances_AreIndependent()
  {
    // Arrange
    var date1 = new DateTime(2026, 1, 1);
    var date2 = new DateTime(2026, 2, 1);

    var ref1 = new ReferencePost
    {
      IsYoutube = true,
      LastRunDate = date1
    };

    var ref2 = new ReferencePost
    {
      IsYoutube = false,
      LastRunDate = date2
    };

    // Act
    ref1.IsYoutube = false;
    ref2.IsYoutube = true;

    // Assert
    Assert.False(ref1.IsYoutube);
    Assert.True(ref2.IsYoutube);
    Assert.Equal(date1, ref1.LastRunDate);
    Assert.Equal(date2, ref2.LastRunDate);
  }

  [Fact]
  public void ReferencePost_DefaultValues()
  {
    var referencePost = new ReferencePost();

    Assert.False(referencePost.IsYoutube);
    Assert.Equal(default(DateTime), referencePost.LastRunDate);
  }

  [Theory]
  [InlineData(1, 1, 1)]
  [InlineData(9999, 12, 31)]
  [InlineData(1970, 1, 1)]
  public void ReferencePost_LastRunDate_AcceptsBoundaryAndHistoricalValues(int year, int month, int day)
  {
    var expectedDate = new DateTime(year, month, day);
    var referencePost = new ReferencePost
    {
      IsYoutube = false,
      LastRunDate = expectedDate
    };

    Assert.Equal(expectedDate, referencePost.LastRunDate);
  }
}
