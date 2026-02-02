using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace VideoSplitter.Models
{
    /// <summary>
    /// Individual segment data from AI response
    /// </summary>
    public class AiSegmentData
    {
        [JsonPropertyName("Start")]
        public string Start { get; set; } = string.Empty;

        [JsonPropertyName("End")]
        public string End { get; set; } = string.Empty;

        [JsonPropertyName("Duration")]
        public int Duration { get; set; }

        [JsonPropertyName("Reasoning")]
        public string Reasoning { get; set; } = string.Empty;

        [JsonPropertyName("Excerpt")]
        public string Excerpt { get; set; } = string.Empty;
    }
}
