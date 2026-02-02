using System;
using System.Collections.Generic;
using System.Text;

namespace VideoSplitter.Models.LLM
{
    public class AnthropicModelsResult
    {
        public bool Success { get; set; }
        public List<string> Models { get; set; } = [];
        public string? ErrorMessage { get; set; }

        public static AnthropicModelsResult Ok(List<string> models) => new() { Success = true, Models = models };
        public static AnthropicModelsResult Failure(string error) => new() { Success = false, ErrorMessage = error };
    }

    internal class AnthropicModelsResponse
    {
        public List<AnthropicModelInfo> Data { get; set; } = [];
        public bool Has_More { get; set; }
    }

    internal class AnthropicModelInfo
    {
        public string? Id { get; set; }
        public string? Display_Name { get; set; }
        public string? Type { get; set; }
    }
}
