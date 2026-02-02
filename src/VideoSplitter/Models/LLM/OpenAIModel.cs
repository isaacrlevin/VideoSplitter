using System;
using System.Collections.Generic;
using System.Text;

namespace VideoSplitter.Models.LLM
{
    public class OpenAiModelsResult
    {
        public bool Success { get; set; }
        public List<string> Models { get; set; } = [];
        public string? ErrorMessage { get; set; }

        public static OpenAiModelsResult Ok(List<string> models) => new() { Success = true, Models = models };
        public static OpenAiModelsResult Failure(string error) => new() { Success = false, ErrorMessage = error };
    }

    internal class OpenAiModelsResponse
    {
        public List<OpenAiModelInfo> Data { get; set; } = [];
    }

    internal class OpenAiModelInfo
    {
        public string? Id { get; set; }
    }
}
