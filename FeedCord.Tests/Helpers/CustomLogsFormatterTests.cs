using FeedCord.Helpers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FeedCord.Tests.Helpers
{
  public class CustomLogsFormatterTests
  {
    private readonly CustomLogsFormatter _formatter;

    public CustomLogsFormatterTests()
    {
      _formatter = new CustomLogsFormatter();
    }

    [Fact]
    public void Constructor_InitializesWithCustomFormatterName()
    {
      // Act & Assert
      Assert.Equal("customlogsformatter", _formatter.Name);
    }

    [Theory]
    [InlineData(LogLevel.Trace, "T")]
    [InlineData(LogLevel.Debug, "D")]
    [InlineData(LogLevel.Information, "I")]
    [InlineData(LogLevel.Warning, "W")]
    [InlineData(LogLevel.Error, "E")]
    [InlineData(LogLevel.Critical, "C")]
    [InlineData(LogLevel.None, "N")]
    public void Write_WithVariousLogLevels_OutputsCorrectLevelInitial(
        LogLevel logLevel, string expectedInitial)
    {
      // Arrange
      var testMessage = "Test message";
      var logEntry = CreateLogEntry(logLevel, testMessage);
      var writer = new StringWriter();

      // Act
      _formatter.Write(logEntry, null, writer);
      var output = writer.ToString();

      // Assert
      Assert.StartsWith($"{expectedInitial}:", output);
    }

    [Theory]
    [InlineData("Simple message")]
    [InlineData("Message with numbers 123")]
    [InlineData("Message with special chars !@#$%^&*()")]
    [InlineData("")]
    [InlineData("Very long message that contains a lot of text and should be displayed completely without truncation")]
    public void Write_WithVariousMessages_IncludesMessageInOutput(string message)
    {
      // Arrange
      var logEntry = CreateLogEntry(LogLevel.Information, message);
      var writer = new StringWriter();

      // Act
      _formatter.Write(logEntry, null, writer);
      var output = writer.ToString();

      // Assert
      Assert.Contains(message, output);
    }

    [Fact]
    public void Write_WithTraceLevel_OutputsWithTracePrefix()
    {
      // Arrange
      var logEntry = CreateLogEntry(LogLevel.Trace, "Trace message");
      var writer = new StringWriter();

      // Act
      _formatter.Write(logEntry, null, writer);
      var output = writer.ToString();

      // Assert
      Assert.StartsWith("T:", output);
      Assert.Contains("Trace message", output);
    }

    [Fact]
    public void Write_WithDebugLevel_OutputsWithDebugPrefix()
    {
      // Arrange
      var logEntry = CreateLogEntry(LogLevel.Debug, "Debug message");
      var writer = new StringWriter();

      // Act
      _formatter.Write(logEntry, null, writer);
      var output = writer.ToString();

      // Assert
      Assert.StartsWith("D:", output);
      Assert.Contains("Debug message", output);
    }

    [Fact]
    public void Write_WithInformationLevel_OutputsWithInformationPrefix()
    {
      // Arrange
      var logEntry = CreateLogEntry(LogLevel.Information, "Info message");
      var writer = new StringWriter();

      // Act
      _formatter.Write(logEntry, null, writer);
      var output = writer.ToString();

      // Assert
      Assert.StartsWith("I:", output);
      Assert.Contains("Info message", output);
    }

    [Fact]
    public void Write_WithWarningLevel_OutputsWithWarningPrefix()
    {
      // Arrange
      var logEntry = CreateLogEntry(LogLevel.Warning, "Warning message");
      var writer = new StringWriter();

      // Act
      _formatter.Write(logEntry, null, writer);
      var output = writer.ToString();

      // Assert
      Assert.StartsWith("W:", output);
      Assert.Contains("Warning message", output);
    }

    [Fact]
    public void Write_WithErrorLevel_OutputsWithErrorPrefix()
    {
      // Arrange
      var logEntry = CreateLogEntry(LogLevel.Error, "Error message");
      var writer = new StringWriter();

      // Act
      _formatter.Write(logEntry, null, writer);
      var output = writer.ToString();

      // Assert
      Assert.StartsWith("E:", output);
      Assert.Contains("Error message", output);
    }

    [Fact]
    public void Write_WithCriticalLevel_OutputsWithCriticalPrefix()
    {
      // Arrange
      var logEntry = CreateLogEntry(LogLevel.Critical, "Critical message");
      var writer = new StringWriter();

      // Act
      _formatter.Write(logEntry, null, writer);
      var output = writer.ToString();

      // Assert
      Assert.StartsWith("C:", output);
      Assert.Contains("Critical message", output);
    }

    [Fact]
    public void Write_WithNoneLevel_OutputsWithNonePrefix()
    {
      // Arrange
      var logEntry = CreateLogEntry(LogLevel.None, "None message");
      var writer = new StringWriter();

      // Act
      _formatter.Write(logEntry, null, writer);
      var output = writer.ToString();

      // Assert
      Assert.StartsWith("N:", output);
    }

    [Fact]
    public void Write_OutputIsFollowedByNewline()
    {
      // Arrange
      var logEntry = CreateLogEntry(LogLevel.Information, "Test");
      var writer = new StringWriter();

      // Act
      _formatter.Write(logEntry, null, writer);
      var output = writer.ToString();

      // Assert
      Assert.EndsWith(Environment.NewLine, output);
    }

    [Fact]
    public void Write_WithMultipleWrites_EachOutputIsOnSeparateLine()
    {
      // Arrange
      var logEntry1 = CreateLogEntry(LogLevel.Information, "Message 1");
      var logEntry2 = CreateLogEntry(LogLevel.Warning, "Message 2");
      var writer = new StringWriter();

      // Act
      _formatter.Write(logEntry1, null, writer);
      _formatter.Write(logEntry2, null, writer);
      var output = writer.ToString();

      // Assert
      var lines = output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
      Assert.Collection(lines,
          line => Assert.Contains("Message 1", line),
          line => Assert.Contains("Message 2", line)
      );
    }

    [Theory]
    [InlineData("Message1234567890")]
    [InlineData("Message with\ttab")]
    [InlineData("Message with\nnewline")]
    [InlineData("Unicode: 你好世界")]
    public void Write_WithSpecialCharactersInMessage_PreservesCharacters(string message)
    {
      // Arrange
      var logEntry = CreateLogEntry(LogLevel.Information, message);
      var writer = new StringWriter();

      // Act
      _formatter.Write(logEntry, null, writer);
      var output = writer.ToString();

      // Assert
      Assert.Contains(message, output);
    }

    [Fact]
    public void Write_WithNullScopeProvider_StillWrites()
    {
      // Arrange
      var logEntry = CreateLogEntry(LogLevel.Information, "Test message");

      // Act & Assert (should not throw)
      var writer = new StringWriter();
      _formatter.Write(logEntry, null, writer);
      Assert.NotEmpty(writer.ToString());
    }

    [Fact]
    public void Write_FormatsCorrectly_PrefixSpaceMessage()
    {
      // Arrange
      var logEntry = CreateLogEntry(LogLevel.Information, "test");
      var writer = new StringWriter();

      // Act
      _formatter.Write(logEntry, null, writer);
      var output = writer.ToString();

      // Assert
      // Output should be "I: test" followed by newline
      Assert.Matches(@"^I: test\r?\n$", output);
    }

    [Fact]
    public void Write_WithEmptyMessage_StillFormatsPrefix()
    {
      // Arrange
      var logEntry = CreateLogEntry(LogLevel.Information, "");
      var writer = new StringWriter();

      // Act
      _formatter.Write(logEntry, null, writer);
      var output = writer.ToString();

      // Assert
      Assert.StartsWith("I:", output);
    }

    // Helper method to create LogEntry safely
    private LogEntry<string> CreateLogEntry(LogLevel logLevel, string message)
    {
      return new LogEntry<string>(
          logLevel: logLevel,
          category: "TestCategory",
          eventId: new EventId(0),
          state: message,
          exception: null,
          formatter: (state, exception) => state ?? string.Empty);
    }
  }
}
