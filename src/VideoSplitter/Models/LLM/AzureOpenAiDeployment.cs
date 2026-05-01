namespace VideoSplitter.Models.LLM;

public class AzureOpenAiDeploymentsResult
{
    public bool Success { get; set; }
    public List<AzureOpenAiDeploymentInfo> Deployments { get; set; } = [];
    public string? ErrorMessage { get; set; }

    public static AzureOpenAiDeploymentsResult Ok(List<AzureOpenAiDeploymentInfo> deployments) => new() { Success = true, Deployments = deployments };
    public static AzureOpenAiDeploymentsResult Failure(string error) => new() { Success = false, ErrorMessage = error };
}

public class AzureOpenAiDeploymentInfo
{
    public string Id { get; set; } = string.Empty;
    public string? Model { get; set; }
}

internal class AzureOpenAiDeploymentsResponse
{
    public List<AzureOpenAiDeploymentItem> Data { get; set; } = [];
}

internal class AzureOpenAiDeploymentItem
{
    public string? Id { get; set; }
    public string? Model { get; set; }
}
