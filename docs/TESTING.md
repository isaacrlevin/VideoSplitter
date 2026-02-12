# Testing Guide for VideoSplitter

This document describes the testing infrastructure, strategy, and best practices for the VideoSplitter project.

## Test Infrastructure

### Frameworks and Libraries

- **xUnit** - Primary testing framework for .NET
- **Moq** - Mocking library for creating test doubles
- **FluentAssertions** - Assertion library for more readable tests
- **Microsoft.EntityFrameworkCore.InMemory** - In-memory database for testing data layer
- **RichardSzalay.MockHttp** - Mock HTTP client for testing external API calls

### Test Project Structure

```
VideoSplitter.Tests/
├── Data/                          # Database and EF Core tests
├── Models/                        # Model validation and behavior tests
├── Services/
│   ├── Audio/                    # Audio extraction service tests
│   ├── Clipping/                 # AI and segment service tests
│   ├── Transcript/               # Transcription service tests
│   └── SocialMediaPublishers/    # Social media integration tests
├── Helpers/                       # Test utilities and helpers
└── Fixtures/                      # Test data fixtures
```

## Running Tests

### Command Line

```bash
# Run all tests
dotnet test

# Run with detailed output
dotnet test --verbosity detailed

# Run with code coverage
dotnet test /p:CollectCoverage=true

# Run specific test class
dotnet test --filter SegmentServiceTests

# Run specific test method
dotnet test --filter GetSegmentsByProjectIdAsync_ReturnsSegmentsOrderedByStartTime
```

### Visual Studio

1. Open Test Explorer (Test > Test Explorer)
2. Click "Run All" or right-click specific tests to run

## Test Categories

### Unit Tests

Test individual components in isolation:

- **Model Tests** - Validate model properties, defaults, and validation
- **Service Tests** - Test business logic with mocked dependencies
- **Data Layer Tests** - Test EF Core models and database operations

Example:
```csharp
[Fact]
public async Task CreateSegmentAsync_SetsTimestamps()
{
    var context = TestDbContextFactory.CreateInMemoryContext();
    var service = new SegmentService(context);
    
    var segment = new Segment { /* ... */ };
    var result = await service.CreateSegmentAsync(segment);
    
    result.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
}
```

### Integration Tests

Test interactions between components:

- **LLM Provider Tests** - Test AI provider factory and configuration
- **Transcript Provider Tests** - Test transcription provider selection
- **Database Integration** - Test full database operations with relationships

Example:
```csharp
[Fact]
public void GetProvider_ReturnsOpenAiProvider_ForOpenAI()
{
    var factory = new LlmProviderFactory(httpClient);
    var provider = factory.GetProvider("OpenAI");
    
    provider.Should().BeOfType<OpenAiProvider>();
}
```

### End-to-End Tests

Full workflow tests (to be implemented):

- Complete video processing pipeline
- Social media publishing workflow
- User preference persistence

## Test Helpers and Fixtures

### TestDbContextFactory

Creates in-memory EF Core database contexts for testing:

```csharp
// Empty database
var context = TestDbContextFactory.CreateInMemoryContext();

// Database with test data
var context = TestDbContextFactory.CreateInMemoryContextWithData();
```

### TestDataFixtures

Provides consistent test data:

```csharp
// Create test project
var project = TestDataFixtures.CreateTestProject();

// Create test segments
var segments = TestDataFixtures.CreateTestSegments(projectId: 1, count: 5);

// Create test app settings
var settings = TestDataFixtures.CreateTestAppSettings();

// Create test transcript
var transcript = TestDataFixtures.CreateTestTranscript();
```

## Writing New Tests

### Naming Conventions

Follow the pattern: `MethodName_StateUnderTest_ExpectedBehavior`

Examples:
- `GetSegmentsByProjectIdAsync_ReturnsSegmentsOrderedByStartTime`
- `CreateSegmentAsync_SetsTimestamps`
- `GenerateSegmentsAsync_ReturnsError_WhenTranscriptIsEmpty`

### Test Structure (AAA Pattern)

```csharp
[Fact]
public async Task MethodName_Scenario_ExpectedResult()
{
    // Arrange - Set up test data and dependencies
    var service = new MyService();
    var input = "test";
    
    // Act - Execute the method being tested
    var result = await service.MethodAsync(input);
    
    // Assert - Verify the expected outcome
    result.Should().NotBeNull();
    result.Value.Should().Be("expected");
}
```

### Using Theory for Parameterized Tests

```csharp
[Theory]
[InlineData(SegmentStatus.Generated)]
[InlineData(SegmentStatus.Approved)]
[InlineData(SegmentStatus.Extracted)]
public void Segment_CanSetAllStatusValues(SegmentStatus status)
{
    var segment = new Segment();
    segment.Status = status;
    
    segment.Status.Should().Be(status);
}
```

## Mocking External Dependencies

### Mocking HTTP Clients

```csharp
var mockHttp = new MockHttpMessageHandler();
mockHttp.When("https://api.example.com/*")
    .Respond("application/json", "{'result': 'success'}");
    
var httpClient = mockHttp.ToHttpClient();
var service = new MyService(httpClient);
```

### Mocking with Moq

```csharp
var mockService = new Mock<IMyService>();
mockService.Setup(s => s.GetDataAsync())
    .ReturnsAsync(new Data { Value = "test" });

var consumer = new MyConsumer(mockService.Object);
```

## Test Data Management

### In-Memory Database

Each test gets a fresh in-memory database:

```csharp
public class MyServiceTests : IDisposable
{
    private readonly VideoSplitterDbContext _context;
    
    public MyServiceTests()
    {
        _context = TestDbContextFactory.CreateInMemoryContext();
    }
    
    public void Dispose()
    {
        _context?.Dispose();
    }
}
```

### Test File Resources

For tests requiring actual files (video, audio, images):

1. Add files to `VideoSplitter.Tests/TestData/` directory
2. Mark as "Copy to Output Directory" in project
3. Reference using relative paths

## Best Practices

### DO

✅ Test one thing per test
✅ Use descriptive test names
✅ Follow AAA (Arrange-Act-Assert) pattern
✅ Clean up resources (implement IDisposable)
✅ Use FluentAssertions for readable assertions
✅ Mock external dependencies (APIs, file system)
✅ Test edge cases and error conditions
✅ Keep tests fast and independent

### DON'T

❌ Test implementation details
❌ Have tests depend on each other
❌ Use real external services (APIs, databases)
❌ Hardcode paths or environment-specific values
❌ Ignore test failures
❌ Write tests that are flaky or non-deterministic

## Edge Cases to Consider

### Video Processing
- Empty or corrupted video files
- Videos with no audio track
- Extremely long or short videos
- Invalid file formats

### Transcription
- Silent videos
- Multiple languages
- Poor audio quality
- Missing transcript files

### AI Segment Generation
- Empty transcripts
- Transcripts with no engaging content
- API timeouts or failures
- Invalid API credentials
- Response parsing errors

### Database Operations
- Concurrent modifications
- Foreign key constraints
- Null values and optional fields
- TimeSpan edge cases (negative, zero, max)

## Code Coverage Goals

- **Critical Services**: 80%+ coverage
- **Data Layer**: 90%+ coverage
- **Models**: 70%+ coverage
- **Overall Project**: 75%+ coverage

## Continuous Integration

Tests are automatically run on:
- Pull requests
- Commits to main branch
- Scheduled nightly builds

See `.github/workflows/` for CI/CD configuration.

## Troubleshooting

### Common Issues

**Issue**: Tests fail with "database is locked"
**Solution**: Ensure each test uses its own in-memory database instance

**Issue**: Async tests hang or timeout
**Solution**: Always await async operations, check for deadlocks

**Issue**: Flaky tests that pass/fail randomly
**Solution**: Check for timing dependencies, shared state, or external factors

**Issue**: Moq setup not working
**Solution**: Verify the interface/virtual method is properly set up and called

## Future Improvements

- [ ] Add E2E tests for complete workflows
- [ ] Performance benchmarking tests
- [ ] Stress testing for large video files
- [ ] UI/Component tests for MAUI interface
- [ ] Contract tests for external API integrations
- [ ] Mutation testing for test quality validation

## Resources

- [xUnit Documentation](https://xunit.net/)
- [Moq Documentation](https://github.com/moq/moq4)
- [FluentAssertions Documentation](https://fluentassertions.com/)
- [EF Core Testing Documentation](https://docs.microsoft.com/en-us/ef/core/testing/)
