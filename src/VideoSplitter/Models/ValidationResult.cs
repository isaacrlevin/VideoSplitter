namespace VideoSplitter.Models;

public class ValidationResult
{
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }

    public static ValidationResult Success() => new() { IsValid = true };
    
    public static ValidationResult Failure(string errorMessage) => new() 
    { 
        IsValid = false, 
        ErrorMessage = errorMessage 
    };
}
