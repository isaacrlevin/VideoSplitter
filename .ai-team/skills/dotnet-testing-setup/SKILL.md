---
name: "dotnet-testing-setup"
description: "Setting up comprehensive test infrastructure for .NET projects with xUnit, Moq, and EF Core InMemory"
domain: "testing"
confidence: "medium"
source: "earned"
tools:
  - name: "powershell"
    description: "Execute dotnet CLI commands"
    when: "Creating test projects, restoring packages, building, running tests"
  - name: "edit"
    description: "Modify .csproj and source files"
    when: "Configuring test project dependencies and target frameworks"
---

## Context

When setting up test infrastructure for a .NET project that uses Entity Framework Core, MAUI, or other platform-specific frameworks. This skill covers project creation, dependency selection, and test helper patterns.

## Patterns

### 1. Test Project Setup

**Create xUnit test project:**
```bash
dotnet new xunit -n {ProjectName}.Tests -o {ProjectName}.Tests
```

**Match target framework to main project:**
- If main project uses `net10.0-windows10.0.19041.0`, test project must match
- Add platform-specific properties from main .csproj:
  ```xml
  <TargetFrameworks>net10.0-windows10.0.19041.0</TargetFrameworks>
  <SupportedOSPlatformVersion>10.0.17763.0</SupportedOSPlatformVersion>
  <TargetPlatformMinVersion>10.0.17763.0</TargetPlatformMinVersion>
  ```

### 2. Test Dependencies

**Standard .NET testing stack:**
```xml
<PackageReference Include="xunit" Version="2.9.3" />
<PackageReference Include="xunit.runner.visualstudio" Version="3.1.4" />
<PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
<PackageReference Include="Moq" Version="4.20.72" />
<PackageReference Include="FluentAssertions" Version="7.0.0" />
```

**For EF Core testing:**
```xml
<PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" Version="{match-main-version}" />
```

**For HTTP client testing:**
```xml
<PackageReference Include="RichardSzalay.MockHttp" Version="7.0.0" />
```

**Add global usings:**
```xml
<ItemGroup>
  <Using Include="Xunit" />
  <Using Include="Moq" />
  <Using Include="FluentAssertions" />
</ItemGroup>
```

### 3. Test Directory Structure

Mirror source structure:
```
ProjectName.Tests/
├── Data/                    # Database and EF Core tests
├── Models/                  # Model validation tests
├── Services/
│   ├── Audio/              # Service-specific tests
│   ├── Clipping/
│   └── Transcript/
├── Helpers/                 # Test utilities
└── Fixtures/                # Test data factories
```

### 4. Test Helper Patterns

**In-Memory Database Factory:**
```csharp
public static class TestDbContextFactory
{
    public static MyDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<MyDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        var context = new MyDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }
}
```

**Test Data Fixtures:**
```csharp
public static class TestDataFixtures
{
    public static MyEntity CreateTestEntity(int id = 1)
    {
        return new MyEntity
        {
            Id = id,
            Name = "Test Entity",
            CreatedAt = DateTime.UtcNow
        };
    }
}
```

### 5. Test Class Pattern

```csharp
public class MyServiceTests : IDisposable
{
    private readonly MyDbContext _context;
    private readonly MyService _service;

    public MyServiceTests()
    {
        _context = TestDbContextFactory.CreateInMemoryContext();
        _service = new MyService(_context);
    }

    [Fact]
    public async Task MethodName_Scenario_ExpectedBehavior()
    {
        // Arrange
        var entity = TestDataFixtures.CreateTestEntity();
        
        // Act
        var result = await _service.ProcessAsync(entity);
        
        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ProcessStatus.Success);
    }

    public void Dispose()
    {
        _context?.Dispose();
    }
}
```

## Examples

**Version compatibility check:**
When adding EF Core InMemory, match the version from main project:
```bash
# Check main project version
grep "Microsoft.EntityFrameworkCore" src/MainProject/MainProject.csproj

# Use same version in test project
<PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" Version="10.0.2" />
```

**Running tests:**
```bash
# Build and run
dotnet test

# With detailed output
dotnet test --verbosity detailed

# Build once, run multiple times
dotnet build
dotnet test --no-build
```

## Anti-Patterns

❌ **Don't** use different target frameworks between main and test projects
❌ **Don't** share DbContext instances between tests (use unique database names)
❌ **Don't** hard-code entity IDs in test fixtures that will conflict
❌ **Don't** use latest package versions without checking compatibility
❌ **Don't** forget to dispose of DbContext in test cleanup

✅ **Do** use Guid.NewGuid() for unique in-memory database names
✅ **Do** match all framework versions between projects
✅ **Do** let database assign IDs for entities to avoid tracking conflicts
✅ **Do** implement IDisposable for proper resource cleanup
✅ **Do** use FluentAssertions for readable test assertions
