using Xunit;
using FeedCord.Helpers;
using FeedCord.Common;
using System.Globalization;

namespace FeedCord.Tests.Helpers;

public class CsvReaderTests
{
    private readonly string _testFilePath;

    public CsvReaderTests()
    {
        _testFilePath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.csv");
    }

    private void WriteTestFile(string content)
    {
        File.WriteAllText(_testFilePath, content);
    }

    private void CleanupTestFile()
    {
        if (File.Exists(_testFilePath))
        {
            File.Delete(_testFilePath);
        }
    }

    [Fact]
    public void LoadReferencePosts_FileDoesNotExist_ReturnsEmptyDictionary()
    {
        // Arrange
        var nonExistentPath = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid()}.csv");

        // Act
        var result = CsvReader.LoadReferencePosts(nonExistentPath);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void LoadReferencePosts_ValidCsvData_ReturnsCorrectDictionary()
    {
        try
        {
            // Arrange
            var date1 = DateTime.ParseExact("2026-02-20 10:30:00", "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            var date2 = DateTime.ParseExact("2026-02-19 15:45:30", "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            var csvContent = $"http://example.com/rss1,false,{date1:yyyy-MM-dd HH:mm:ss}\n" +
                            $"http://example.com/rss2,true,{date2:yyyy-MM-dd HH:mm:ss}";
            WriteTestFile(csvContent);

            // Act
            var result = CsvReader.LoadReferencePosts(_testFilePath);

            // Assert
            Assert.Equal(2, result.Count);
            Assert.True(result.ContainsKey("http://example.com/rss1"));
            Assert.True(result.ContainsKey("http://example.com/rss2"));
            Assert.False(result["http://example.com/rss1"].IsYoutube);
            Assert.True(result["http://example.com/rss2"].IsYoutube);
            Assert.Equal(date1, result["http://example.com/rss1"].LastRunDate);
            Assert.Equal(date2, result["http://example.com/rss2"].LastRunDate);
        }
        finally
        {
            CleanupTestFile();
        }
    }

    [Fact]
    public void LoadReferencePosts_EmptyFile_ReturnsEmptyDictionary()
    {
        try
        {
            // Arrange
            WriteTestFile(string.Empty);

            // Act
            var result = CsvReader.LoadReferencePosts(_testFilePath);

            // Assert
            Assert.Empty(result);
        }
        finally
        {
            CleanupTestFile();
        }
    }

    [Fact]
    public void LoadReferencePosts_BlankLines_SkipsBlankLines()
    {
        try
        {
            // Arrange
            var date = DateTime.ParseExact("2026-02-20 10:30:00", "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            var csvContent = $"\n\n" +
                            $"http://example.com/rss,false,{date:yyyy-MM-dd HH:mm:ss}\n" +
                            $"\n";
            WriteTestFile(csvContent);

            // Act
            var result = CsvReader.LoadReferencePosts(_testFilePath);

            // Assert
            Assert.Single(result);
            Assert.True(result.ContainsKey("http://example.com/rss"));
        }
        finally
        {
            CleanupTestFile();
        }
    }

    [Fact]
    public void LoadReferencePosts_InvalidBooleanValue_SkipsLine()
    {
        try
        {
            // Arrange
            var date = DateTime.ParseExact("2026-02-20 10:30:00", "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            var csvContent = $"http://example.com/valid,false,{date:yyyy-MM-dd HH:mm:ss}\n" +
                            $"http://example.com/invalid,notbool,{date:yyyy-MM-dd HH:mm:ss}";
            WriteTestFile(csvContent);

            // Act
            var result = CsvReader.LoadReferencePosts(_testFilePath);

            // Assert
            Assert.Single(result);  // Only the valid line is loaded
            Assert.True(result.ContainsKey("http://example.com/valid"));
            Assert.False(result.ContainsKey("http://example.com/invalid"));
        }
        finally
        {
            CleanupTestFile();
        }
    }

    [Fact]
    public void LoadReferencePosts_InvalidDateTime_SkipsLine()
    {
        try
        {
            // Arrange
            var validDate = DateTime.ParseExact("2026-02-20 10:30:00", "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            var csvContent = $"http://example.com/valid,false,{validDate:yyyy-MM-dd HH:mm:ss}\n" +
                            $"http://example.com/invalid,false,not-a-date";
            WriteTestFile(csvContent);

            // Act
            var result = CsvReader.LoadReferencePosts(_testFilePath);

            // Assert
            Assert.Single(result);  // Only the valid line is loaded
            Assert.True(result.ContainsKey("http://example.com/valid"));
            Assert.False(result.ContainsKey("http://example.com/invalid"));
        }
        finally
        {
            CleanupTestFile();
        }
    }

    [Fact]
    public void LoadReferencePosts_InsufficientColumns_SkipsLine()
    {
        try
        {
            // Arrange
            var validDate = DateTime.ParseExact("2026-02-20 10:30:00", "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            var csvContent = $"http://example.com/valid,false,{validDate:yyyy-MM-dd HH:mm:ss}\n" +
                            $"http://example.com/incomplete,false";  // Missing date column
            WriteTestFile(csvContent);

            // Act
            var result = CsvReader.LoadReferencePosts(_testFilePath);

            // Assert
            Assert.Single(result);  // Only the valid line is loaded
            Assert.True(result.ContainsKey("http://example.com/valid"));
            Assert.False(result.ContainsKey("http://example.com/incomplete"));
        }
        finally
        {
            CleanupTestFile();
        }
    }

    [Fact]
    public void LoadReferencePosts_WhitespaceAroundValues_TrimsCorrectly()
    {
        try
        {
            // Arrange
            var date = DateTime.ParseExact("2026-02-20 10:30:00", "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            var csvContent = $"  http://example.com/rss  ,  false  ,  {date:yyyy-MM-dd HH:mm:ss}  ";
            WriteTestFile(csvContent);

            // Act
            var result = CsvReader.LoadReferencePosts(_testFilePath);

            // Assert
            Assert.Single(result);
            Assert.True(result.ContainsKey("http://example.com/rss"));  // URL should be trimmed
        }
        finally
        {
            CleanupTestFile();
        }
    }

    [Fact]
    public void LoadReferencePosts_CaseSensitiveBoolean_ParsesTrueFalse()
    {
        try
        {
            // Arrange
            var date = DateTime.ParseExact("2026-02-20 10:30:00", "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            var csvContent = $"http://example.com/rss1,True,{date:yyyy-MM-dd HH:mm:ss}\n" +
                            $"http://example.com/rss2,False,{date:yyyy-MM-dd HH:mm:ss}";
            WriteTestFile(csvContent);

            // Act
            var result = CsvReader.LoadReferencePosts(_testFilePath);

            // Assert
            Assert.Equal(2, result.Count);
            Assert.True(result["http://example.com/rss1"].IsYoutube);
            Assert.False(result["http://example.com/rss2"].IsYoutube);
        }
        finally
        {
            CleanupTestFile();
        }
    }

    [Fact]
    public void LoadReferencePosts_MultipleValidEntries_LoadsAllCorrectly()
    {
        try
        {
            // Arrange
            var date1 = DateTime.ParseExact("2026-02-20 10:30:00", "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            var date2 = DateTime.ParseExact("2026-02-19 15:45:30", "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            var date3 = DateTime.ParseExact("2026-02-18 08:15:00", "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            var csvContent = $"http://example.com/rss1,false,{date1:yyyy-MM-dd HH:mm:ss}\n" +
                            $"http://example.com/rss2,true,{date2:yyyy-MM-dd HH:mm:ss}\n" +
                            $"http://example.com/rss3,false,{date3:yyyy-MM-dd HH:mm:ss}";
            WriteTestFile(csvContent);

            // Act
            var result = CsvReader.LoadReferencePosts(_testFilePath);

            // Assert
            Assert.Equal(3, result.Count);
            Assert.False(result["http://example.com/rss1"].IsYoutube);
            Assert.True(result["http://example.com/rss2"].IsYoutube);
            Assert.False(result["http://example.com/rss3"].IsYoutube);
        }
        finally
        {
            CleanupTestFile();
        }
    }

    [Fact]
    public void LoadReferencePosts_DuplicateUrls_LastOneWins()
    {
        try
        {
            // Arrange
            var date1 = DateTime.ParseExact("2026-02-20 10:30:00", "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            var date2 = DateTime.ParseExact("2026-02-19 15:45:30", "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            var csvContent = $"http://example.com/rss,false,{date1:yyyy-MM-dd HH:mm:ss}\n" +
                            $"http://example.com/rss,true,{date2:yyyy-MM-dd HH:mm:ss}";
            WriteTestFile(csvContent);

            // Act
            var result = CsvReader.LoadReferencePosts(_testFilePath);

            // Assert
            Assert.Single(result);
            Assert.True(result["http://example.com/rss"].IsYoutube);  // Last entry wins
            Assert.Equal(date2, result["http://example.com/rss"].LastRunDate);
        }
        finally
        {
            CleanupTestFile();
        }
    }

    [Fact]
    public void LoadReferencePosts_ExceptionDuringFileRead_ReturnsEmptyDictionary()
    {
        // Arrange
        var invalidPath = "/invalid/path/that/does/not/exist/file.csv";

        // Act
        var result = CsvReader.LoadReferencePosts(invalidPath);

        // Assert - should return empty dict instead of throwing
        Assert.NotNull(result);
        Assert.Empty(result);
    }
}
