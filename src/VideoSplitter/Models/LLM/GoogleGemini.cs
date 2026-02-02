using System;
using System.Collections.Generic;
using System.Text;

namespace VideoSplitter.Models.LLM
{
    public class GoogleGeminiModelsResult
    {
        public bool Success { get; set; }
        public List<string> Models { get; set; } = [];
        public string? ErrorMessage { get; set; }

        public static GoogleGeminiModelsResult Ok(List<string> models) => new() { Success = true, Models = models };
        public static GoogleGeminiModelsResult Failure(string error) => new() { Success = false, ErrorMessage = error };
    }

    internal class GoogleGeminiModelsResponse
    {
        public List<GoogleGeminiModelInfo> Models { get; set; } = [];
    }

    internal class GoogleGeminiModelInfo
    {
        public string? Name { get; set; }
        public string? DisplayName { get; set; }
        public string? Description { get; set; }
        public List<string> SupportedGenerationMethods { get; set; } = [];
    }
}
