using VideoSplitter.Models;

namespace VideoSplitter.Tests.Models;

public class ValidationResultTests
{
    [Fact]
    public void Success_CreatesSuccessfulResult()
    {
        // Act
        var result = ValidationResult.Success();

        // Assert
        result.IsValid.Should().BeTrue();
        result.ErrorMessage.Should().BeNullOrEmpty();
    }

    [Fact]
    public void Failure_WithSingleError_CreatesFailedResult()
    {
        // Arrange
        var errorMessage = "Test error";

        // Act
        var result = ValidationResult.Failure(errorMessage);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Be(errorMessage);
    }

    [Fact]
    public void ValidationResult_WithNoErrors_IsValid()
    {
        // Act
        var result = new ValidationResult { IsValid = true };

        // Assert
        result.IsValid.Should().BeTrue();
        result.ErrorMessage.Should().BeNullOrEmpty();
    }
}
