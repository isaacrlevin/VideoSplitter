namespace VideoSplitter.Models.LLM;

public class LlmModelOption
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class LlmModelOptionsResult
{
    public bool Success { get; set; }
    public List<LlmModelOption> Models { get; set; } = [];
    public string? ErrorMessage { get; set; }

    public static LlmModelOptionsResult Ok(List<LlmModelOption> models) => new() { Success = true, Models = models };
    public static LlmModelOptionsResult Failure(string error) => new() { Success = false, ErrorMessage = error };
}
