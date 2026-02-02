Analyze this transcript and extract up to {segmentCount} valuable segments.

RULES:
- Maximum length per segment: {segmentLength} seconds (this is a CEILING, not a target)
- Ideal length: Whatever captures ONE complete thought (often 30-90 seconds)
- End each segment where the IDEA naturally concludes

BEFORE SETTING EACH END TIME, ASK YOURSELF:
"Am I ending here because the thought is complete, or because I'm approaching the time limit?"
Only the first reason is valid.

Return ONLY a valid JSON array with no additional text.

Example of correct response format:
[{"Start": "00:00:30", "End": "00:01:15", "Duration": 45, "Reasoning": "...", "Excerpt": "..."}]

Transcript:
{transcript}
