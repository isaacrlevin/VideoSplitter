# Customizing Prompts

VideoSplitter uses customizable prompts to instruct the AI on how to analyze transcripts and select video segments. Understanding and customizing these prompts allows you to optimize segment selection for your specific content type.

## Prompt Architecture

VideoSplitter uses a two-prompt system:

1. **System Prompt** - Defines the AI's role, rules, and output format
2. **User Prompt** - Contains the specific request and the transcript to analyze

Both prompts are located in:
```
VideoSplitter/Resources/Raw/Prompts/
??? SystemPrompt.md
??? UserPrompt.md
```

## Available Placeholders

Both prompts support these dynamic placeholders that are replaced at runtime:

| Placeholder | Description | Source |
|-------------|-------------|--------|
| `{segmentCount}` | Number of segments to generate | Settings ? Default Segment Count |
| `{segmentLength}` | Maximum segment length in seconds | Settings ? Default Segment Length |
| `{transcript}` | The full video transcript | Generated from transcription |

## System Prompt Breakdown

The system prompt establishes the AI's behavior. Here's the default with explanations:

```markdown
You are an expert technical content editor for software developers.
```
**Role Definition** - Tells the AI what persona to assume. Change this for different content types (e.g., "You are an expert entertainment content curator" for entertainment videos).

```markdown
You analyze video transcripts and extract the most valuable standalone segments
for a developer or engineering audience.
```
**Task Description** - Explains what the AI should do and for whom. Customize the audience description.

```markdown
Transcript Format:
[start -> end] transcript text
Example: [00:00:00 -> 00:00:08] Welcome everybody...
```
**Input Format** - Helps the AI understand the transcript structure it receives.

```markdown
## CRITICAL: Segment Length Rules

**{segmentLength} seconds is the MAXIMUM ALLOWED, NOT a target length.**

You MUST determine segment length by CONTENT BOUNDARIES:
- END a segment when the speaker COMPLETES their thought
- END a segment when the topic CHANGES  
- A 45-second segment with one complete insight is BETTER than a 180-second segment
```
**Length Guidelines** - Critical for quality. Emphasizes content-based endings over arbitrary length targets.

```markdown
## Segment Selection Criteria

Look for:
1. System design insights or architecture decisions
2. Coding best practices or tips
3. Engineering tradeoffs being explained
4. Developer workflow recommendations
```
**Selection Criteria** - **This is the most important section to customize.** Change these criteria based on your content type.

```markdown
## IMPORTANT: Output Format

You MUST return ONLY a valid JSON array...
```
**Output Format** - Ensures the AI returns parseable JSON. Don't modify this unless you're also updating the parsing code.

## User Prompt Breakdown

The user prompt is sent with each analysis request:

```markdown
Analyze this transcript and extract up to {segmentCount} valuable segments.

RULES:
- Maximum length per segment: {segmentLength} seconds (this is a CEILING, not a target)
- Ideal length: Whatever captures ONE complete thought (often 30-90 seconds)
- End each segment where the IDEA naturally concludes
```
**Instructions** - Reinforces the rules for the specific request.

```markdown
BEFORE SETTING EACH END TIME, ASK YOURSELF:
"Am I ending here because the thought is complete, or because I'm approaching the time limit?"
Only the first reason is valid.
```
**Quality Check** - Encourages the AI to prioritize content completeness.

```markdown
Transcript:
{transcript}
```
**The Actual Content** - The transcript is inserted here.

## Customization Examples

### For Educational/Tutorial Content

Modify the system prompt's selection criteria:

```markdown
## Segment Selection Criteria

Look for:
1. Step-by-step explanations of concepts
2. "Aha moment" explanations that clarify confusion
3. Practical demonstrations or examples
4. Common mistakes and how to avoid them
5. Quick tips that can be applied immediately
```

### For Entertainment/Podcast Content

```markdown
## Segment Selection Criteria

Look for:
1. Funny or memorable moments
2. Controversial or surprising statements
3. Emotional storytelling peaks
4. Quotable one-liners or insights
5. Heated debates or disagreements
```

### For Product Demo/Marketing Content

```markdown
## Segment Selection Criteria

Look for:
1. Feature demonstrations with clear benefits
2. Before/after comparisons
3. Problem statements that resonate with viewers
4. Customer pain points being addressed
5. Impressive results or statistics
```

### For Interview Content

```markdown
## Segment Selection Criteria

Look for:
1. Personal stories or anecdotes
2. Unique insights from the guest's experience
3. Actionable advice for the audience
4. Controversial or contrarian opinions
5. Emotional or vulnerable moments
```

## Advanced Customizations

### Changing the AI's Tone

Add tone guidance to the system prompt:

```markdown
When writing the "Reasoning" field, be specific about WHY this segment 
would perform well as a short-form clip. Reference specific viewer psychology
or platform algorithm preferences.
```

### Adding Platform-Specific Guidance

For TikTok-optimized selections:

```markdown
## Platform Optimization (TikTok)

Prioritize segments that:
- Start with an immediate hook (no long intros)
- Have high energy or emotion
- Contain visual demonstrations if applicable
- Would make viewers want to watch until the end
```

### Requesting Specific Segment Types

```markdown
## Segment Diversity

Ensure variety in your selections:
- At least one segment should be actionable advice
- At least one should be a surprising fact or insight
- At least one should have emotional impact
```

### Handling Long Videos

For very long videos, add:

```markdown
## Long Video Guidance

For this longer transcript:
- Spread segments across the entire video duration
- Don't cluster all segments in one section
- Ensure at least 5 minutes between segment start times
```

## Testing Your Prompts

1. **Start with small changes** - Modify one thing at a time
2. **Use a consistent test video** - Compare results across prompt versions
3. **Check JSON validity** - Ensure your changes don't break the output format
4. **Review segment quality** - Watch the generated segments to verify improvement

## Troubleshooting

### AI Returns Fewer Segments Than Requested

Add explicit reinforcement:
```markdown
You MUST return EXACTLY {segmentCount} segments, no more, no less.
```

### Segments Are Too Long/Short

Adjust the guidance:
```markdown
Target segment length: 45-75 seconds
NEVER exceed {segmentLength} seconds
NEVER create segments shorter than 20 seconds unless content is complete
```

### AI Misunderstands the Task

Some models need more explicit instructions. Add:
```markdown
DO NOT:
- Summarize the entire video
- Answer questions about the content
- Provide commentary outside the JSON

DO:
- Return ONLY the JSON array
- Start your response with the [ character
- End your response with the ] character
```

### JSON Parsing Errors

Ensure the output format section is clear and includes examples:
```markdown
CORRECT response format:
[
  {"Start": "00:01:23", "End": "00:02:45", ...},
  {"Start": "00:05:10", "End": "00:06:02", ...}
]

INCORRECT (do not do this):
Here are the segments:
[...]
```

## Local Model Considerations

When using Ollama with local models, the prompts are automatically enhanced with additional JSON formatting reminders. However, local models may benefit from:

1. **Simpler instructions** - Remove complex nested rules
2. **More examples** - Include 2-3 complete JSON examples
3. **Explicit format reminders** - Repeat the JSON format requirements

## Resetting to Defaults

If your customizations cause issues, you can:

1. Delete the modified prompt files
2. The application will regenerate defaults on next launch
3. Or copy the original prompts from this documentation

---

## Quick Reference: JSON Output Format

Always ensure your prompts maintain this output structure:

```json
[
  {
    "Start": "00:01:23",
    "End": "00:02:45",
    "Duration": 82,
    "Reasoning": "Clear explanation of why this segment is valuable",
    "Excerpt": "The exact transcript text from this segment"
  }
]
```

| Field | Type | Description |
|-------|------|-------------|
| `Start` | String | Segment start time in "hh:mm:ss" format |
| `End` | String | Segment end time in "hh:mm:ss" format |
| `Duration` | Integer | Length in seconds (End - Start) |
| `Reasoning` | String | Why this segment was selected |
| `Excerpt` | String | The transcript text within this segment |

---

Return to [Documentation Index](README.md)
