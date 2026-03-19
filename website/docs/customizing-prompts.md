---
title: Customizing Prompts
layout: page
parent: Documentation
nav_order: 4
---

# Customizing Prompts

VideoSplitter uses customizable prompts to instruct the AI on how to analyze transcripts and select video segments. Understanding and customizing these prompts allows you to optimize segment selection for your specific content type.

## Prompt Architecture

VideoSplitter uses a two-prompt system:

1. **System Prompt** — Defines the AI's role, rules, and output format
2. **User Prompt** — Contains the specific request and the transcript to analyze

Both prompts are located in:
```
VideoSplitter/Resources/Raw/Prompts/
├── SystemPrompt.md
└── UserPrompt.md
```

## Available Placeholders

Both prompts support these dynamic placeholders that are replaced at runtime:

| Placeholder | Description | Source |
|-------------|-------------|--------|
| `{segmentCount}` | Number of segments to generate | Settings → Default Segment Count |
| `{segmentLength}` | Maximum segment length in seconds | Settings → Default Segment Length |
| `{transcript}` | The full video transcript | Generated from transcription |

---

## System Prompt Breakdown

The system prompt establishes the AI's behavior. Here are the key sections:

**Role Definition**
```markdown
You are an expert technical content editor for software developers.
```
Change this for different content types (e.g., "You are an expert entertainment content curator").

**Task Description**
```markdown
You analyze video transcripts and extract the most valuable standalone segments
for a developer or engineering audience.
```
Customize the audience description to match your target viewers.

**Length Guidelines**
```markdown
## CRITICAL: Segment Length Rules

{segmentLength} seconds is the MAXIMUM ALLOWED, NOT a target length.

You MUST determine segment length by CONTENT BOUNDARIES:
- END a segment when the speaker COMPLETES their thought
- END a segment when the topic CHANGES
- A 45-second segment with one complete insight is BETTER than a 180-second segment
```

**Selection Criteria** — *This is the most important section to customize.*
```markdown
## Segment Selection Criteria

Look for:
1. System design insights or architecture decisions
2. Coding best practices or tips
3. Engineering tradeoffs being explained
4. Developer workflow recommendations
```

---

## Customization Examples

### For Educational/Tutorial Content

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

---

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

### Handling Long Videos

For very long videos, add:

```markdown
## Long Video Guidance

For this longer transcript:
- Spread segments across the entire video duration
- Don't cluster all segments in one section
- Ensure at least 5 minutes between segment start times
```

---

## Testing Your Prompts

1. **Start with small changes** — Modify one thing at a time
2. **Use a consistent test video** — Compare results across prompt versions
3. **Check JSON validity** — Ensure your changes don't break the output format
4. **Review segment quality** — Watch the generated segments to verify improvement

---

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

Ensure the output format section includes a clear example:
```markdown
CORRECT response format:
[
  {"Start": "00:01:23", "End": "00:02:45", ...},
  {"Start": "00:05:10", "End": "00:06:02", ...}
]
```

---

## Local Model Considerations

When using Ollama with local models, prompts are automatically enhanced with additional JSON formatting reminders. Local models may also benefit from:

1. **Simpler instructions** — Remove complex nested rules
2. **More examples** — Include 2–3 complete JSON examples
3. **Explicit format reminders** — Repeat the JSON format requirements

---

## Resetting to Defaults

If your customizations cause issues:

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
| `Start` | String | Segment start time in `hh:mm:ss` format |
| `End` | String | Segment end time in `hh:mm:ss` format |
| `Duration` | Integer | Length in seconds (End − Start) |
| `Reasoning` | String | Why this segment was selected |
| `Excerpt` | String | The transcript text within this segment |

---

Return to [Documentation]({{ site.baseurl }}/docs/)
