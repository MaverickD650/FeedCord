# FeedCord Unit Testing Guide

This document explains the unit testing setup for FeedCord and how to run and extend tests.

## Overview

- **Framework**: xUnit with Moq for mocking
- **Test Project**: `FeedCord.Tests/`
- **CI Integration**: GitHub Actions workflows
- **Coverage**: Targets core services and business logic

## Project Structure

```bash
FeedCord.Tests/
├── FeedCord.Tests.csproj    # Test project configuration
├── Common/                   # Tests for Common models
│   └── ConfigTests.cs       # Configuration validation tests
├── Services/                # Tests for Service layer
│   ├── FeedManagerTests.cs
│   ├── PostFilterServiceTests.cs
│   └── RssParsingServiceTests.cs
└── Infrastructure/          # Tests for Infrastructure layer (not yet added)
    └── (e.g., DiscordNotifierTests.cs, CustomHttpClientTests.cs)
```

## Running Tests Locally

### Prerequisites

- .NET SDK 9.0 or 10.0
- Visual Studio Code or Visual Studio 2022+

### Run All Tests

```bash
dotnet test
```

### Run Specific Test Class

```bash
dotnet test --filter ClassName=PostFilterServiceTests
```

### Run with Verbose Output

```bash
dotnet test --verbosity detailed
```

### Run with Code Coverage

```bash
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
```

### Watch Mode (Auto-run on file changes)

```bash
dotnet watch test
```

## CI Integration

### Test Workflow

- **File**: `.github/workflows/test.yaml`
- **Triggers**: Pull requests and pushes to `main`
- **Runtime**: Tests run on .NET 10.0
- **Coverage**: Reports uploaded to Codecov

### Release Workflow

- **File**: `.github/workflows/release.yaml`
- **Requirement**: Tests must pass before Docker build
- **Dependency**: Release build depends on `test` job completing successfully
- **Matrix**: Release validation runs tests on .NET 10.0

### GitHub Actions Features

1. **Test Result Publishing**: xUnit results published as check annotations
2. **Coverage Reports**: OpenCover XML uploaded to Codecov
3. **Matrix Testing**: Runs on multiple .NET versions
4. **Artifact Storage**: Test results retained for 1 day

## Test Categories

### 1. Config Tests (`ConfigTests.cs`)

Tests configuration model validation:

- Required field validation
- Default value initialization
- Optional field handling

**Key Test Pattern**:

```csharp
[Fact]
public void Config_IdRequired_FailsValidationWhenMissing()
{
    // Arrange, Act, Assert format
}
```

### 2. PostFilterService Tests (`PostFilterServiceTests.cs`)

Tests the filtering logic for blog posts:

- No filters → include all posts
- URL-specific filters
- Global "all" filter
- Filter priority (URL-specific > global)

**Key Dependency**: None (only ILogger)

### 3. RssParsingService Tests (`RssParsingServiceTests.cs`)

Tests RSS feed parsing and error handling:

- Valid RSS/Atom feeds
- Malformed XML handling
- Empty feeds
- Description trimming
- YouTube feed delegation

**Key Dependencies**: Mocks for IYoutubeParsingService, IImageParserService

### 4. FeedManager Tests (`FeedManagerTests.cs`)

Tests the core feed management logic:

- URL initialization and validation
- Empty URL filtering
- Post filtering application
- Concurrent request management
- Direct YouTube feed URL handling

**Key Dependencies**: Mocks for 5 different interfaces

## Test Patterns & Best Practices

### Arrange-Act-Assert (AAA)

```csharp
[Fact]
public void TestName_Condition_ExpectedResult()
{
    // Arrange - Setup test data and mocks
    var config = CreateTestConfig();
    var service = new MyService(mockDep.Object);
    
    // Act - Execute the method under test
    var result = service.DoSomething(input);
    
    // Assert - Verify the result
    Assert.True(result);
}
```

### Parameterized Tests with [Theory]

```csharp
[Theory]
[InlineData("value1", true)]
[InlineData("value2", false)]
public void TestName(string input, bool expected)
{
    // Shared test logic runs multiple times
}
```

### Mocking with Moq

```csharp
// Setup
var mockService = new Mock<IService>();
mockService
    .Setup(x => x.GetData(It.IsAny<string>()))
    .ReturnsAsync(expectedResult);

// Verify
mockService.Verify(x => x.GetData("specific-value"), Times.Once);
```

## Adding New Tests

### 1. Create Test File

Place in appropriate folder matching source structure:

```bash
FeedCord.Tests/Services/YourServiceTests.cs
```

### 2. Follow Naming Convention

```csharp
public class YourServiceTests
{
    [Fact]
    public void MethodName_Condition_ExpectedResult()
    {
        // ...
    }
}
```

### 3. Use Helper Methods

```csharp
private Config CreateTestConfig(/* params */)
{
    return new Config 
    { 
        // Common setup
    };
}
```

### 4. Mock External Dependencies

```csharp
private readonly Mock<IExternalService> _mockService;

public YourServiceTests()
{
    _mockService = new Mock<IExternalService>();
}
```

## Example: Testing FeedWorker

Add to `FeedCord.Tests/Infrastructure/Workers/FeedWorkerTests.cs`:

```csharp
public class FeedWorkerTests
{
    [Fact]
    public async Task ExecuteAsync_InitializesUrlsOnFirstRun()
    {
        // Arrange
        var mockFeedManager = new Mock<IFeedManager>();
        mockFeedManager
                    .Setup(x => x.InitializeUrlsAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var worker = new FeedWorker(
            mockLifetime.Object,
            mockLogger.Object,
            mockFeedManager.Object,
            // ... other mocks
        );

        // Act
        var cts = new CancellationTokenSource();
        cts.CancelAfter(timespan);
        await worker.ExecuteAsync(cts.Token);

        // Assert
        mockFeedManager.Verify(x => x.InitializeUrlsAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

## Coverage Goals

| Component | Target Coverage | Status |
| --- | --- | --- |
| Config | 95%+ | ✅ Tests created |
| PostFilterService | 90%+ | ✅ Tests created |
| RssParsingService | 85%+ | ✅ Tests created |
| FeedManager | 80%+ | ✅ Tests created |
| DiscordNotifier | 75%+ | ✅ Tests created |
| CustomHttpClient | 80%+ | ✅ Tests created |
| FeedWorker | 70%+ | ✅ Tests created |

## Continuous Integration Status

Tests are automatically run on:

- ✅ Push to `main`
- ✅ Pull requests (all numbers of .NET versions)
- ✅ Before release build (blocks Docker image build if failing)

View results:

- Check "Checks" tab in PR
- View GitHub Actions workflows
- Download test artifacts

## Troubleshooting

### Tests won't compile

- Ensure test project references main project: `<ProjectReference Include="..\FeedCord\FeedCord.csproj" />`
- Check that mock objects are initialized in constructor

### Tests timeout

- Increase timeout value in test if testing async operations
- Check for deadlocks in mocked dependencies

### Coverage not working

- Install coverlet: `dotnet add package coverlet.collector`
- Use `/p:CollectCoverage=true` flag

### CI tests fail but local pass

- Ensure .NET SDK version matches GH Actions matrix
- Check for path separators (`\` vs `/`) in file operations
- Watch for timezone-dependent tests

### Test discovery shows 0 tests

- Build errors in either project can prevent test discovery.
- Run `dotnet build` first, then rerun `dotnet test`.

## References

- [xUnit Documentation](https://xunit.net/)
- [Moq Documentation](https://github.com/Moq/moq4)
- [MSTest vs xUnit vs NUnit](https://docs.microsoft.com/dotnet/core/testing/)
- [GitHub Actions .NET Testing](https://github.com/actions/setup-dotnet)
