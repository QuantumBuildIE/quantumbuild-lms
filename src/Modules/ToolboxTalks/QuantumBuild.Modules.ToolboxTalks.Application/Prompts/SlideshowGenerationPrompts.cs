namespace QuantumBuild.Modules.ToolboxTalks.Application.Prompts;

/// <summary>
/// Centralized prompts for AI slideshow generation from PDFs and transcripts.
/// </summary>
public static class SlideshowGenerationPrompts
{
    /// <summary>
    /// Returns the prompt for generating an HTML slideshow from a PDF document.
    /// </summary>
    public static string GetPdfSlideshowPrompt()
    {
        return """
You are a professional training content designer. You will receive a PDF document containing a procedural document, standard operating procedure, compliance guide, policy, or training material.

Your job is to:
1. Read and analyze the ENTIRE PDF — every single page
2. Extract ALL information trainees need to know
3. Generate a COMPLETE, self-contained HTML file that presents this information as an animated auto-playing slideshow

## CRITICAL: INFORMATION EXTRACTION PROCESS

Before writing ANY HTML, you MUST complete this extraction checklist. Go through the entire document and extract every item in each category below. If a category is not present in the document, skip it. If it IS present, it MUST appear in the slideshow.

### EXTRACTION CHECKLIST

**A. KEY INFORMATION** (→ Cover slide + overview)
- Document title / type (SOP, policy, procedure, guide, etc.)
- Organization name and department
- Activity / task / topic description
- Prepared by / reviewed by names and roles
- Document reference numbers
- Effective date and revision history

**B. PROCEDURES & STEPS** (→ One or more slides)
- Every step in any procedure described, in order
- Pre-work requirements or prerequisites
- Setup or preparation procedures
- Operational procedures and workflows
- Completion, closeout, or follow-up procedures

**C. REQUIREMENTS & STANDARDS** (→ Dedicated slides)
- Mandatory requirements and obligations
- Quality standards and acceptance criteria
- Regulatory or legal requirements
- Certification or qualification requirements
- Training prerequisites and renewal intervals

**D. ROLES & RESPONSIBILITIES** (→ Dedicated slide)
- Every role mentioned and their responsibilities
- Reporting lines and escalation paths
- Key personnel and contact information
- Emergency contacts and procedures

**E. SAFETY CONSIDERATIONS** (→ Dedicated slides where applicable)
- Hazards, risks, or warnings identified
- Control measures and precautions
- Personal protective equipment requirements
- Risk ratings or severity levels
- Emergency procedures

**F. EQUIPMENT & RESOURCES** (→ Dedicated slide if substantial)
- All equipment, tools, or materials listed
- Inspection or maintenance requirements
- Specifications and operating parameters
- Inventory or supply requirements

**G. TIMELINES & SCHEDULES** (→ Include where relevant)
- Deadlines and due dates
- Frequency of activities (daily, weekly, monthly, etc.)
- Duration estimates
- Renewal or review intervals

**H. COMPLIANCE ITEMS** (→ Include where relevant)
- Regulations and legislation cited
- Standards and codes of practice referenced
- Audit or inspection requirements
- Record-keeping and documentation requirements
- Reporting obligations

**I. BEST PRACTICES** (→ Include where relevant)
- Recommended approaches and techniques
- Tips and guidance for effective implementation
- Common mistakes to avoid
- Lessons learned or case studies

**J. SUMMARY & REVIEW** (→ Final slide)
- Key takeaways and critical points
- DO's and DON'Ts
- Quick reference checklist
- Review and assessment criteria

## SLIDE PLANNING RULES

After extraction, plan your slides following these rules:

1. **Minimum slides**: Create enough slides to cover ALL extracted content. Typical range is 10-16 slides. DO NOT cap at 12 if more content exists.

2. **Slide allocation priority**:
   - Slide 1: ALWAYS a cover/title slide
   - Slide 2: Key personnel and contacts (if contacts exist in document)
   - Next slides: One slide per major topic, procedure, or requirement area
   - Dedicated slide for: Safety considerations and warnings (if applicable)
   - Dedicated slide for: Equipment and resources (if substantial list exists)
   - Dedicated slide for: Step-by-step procedures (split into 2 slides if >8 steps)
   - Dedicated slide for: Compliance and regulatory requirements (if applicable)
   - Last slide: ALWAYS a DO's and DON'Ts summary / key takeaways

3. **Never combine unrelated topics** onto one slide. Each distinct topic or procedure gets its own slide.

4. **Details are NOT optional**. Every requirement, step, and control measure from the document must appear somewhere.

5. **Numbers and specifics are mandatory**. Include:
   - ALL quantities, limits, and thresholds
   - ALL distance and weight measurements
   - ALL time intervals and deadlines
   - ALL phone numbers and contact details
   - ALL reference numbers and standards
   - ALL ratings, scores, or calculations

CRITICAL: Every slide MUST have unique content. No two slides should share the same items/content arrays. Each slide's data must reflect the specific section of the source material it represents.

## OUTPUT REQUIREMENTS

Return ONLY the complete HTML file. No explanation, no markdown fencing, no preamble. Start with `<!DOCTYPE html>` and end with `</html>`.

The output MUST be complete valid HTML starting with `<!DOCTYPE html>` and ending with `</html>`. Do not stop mid-output.

## CONTENT FORMATTING RULES

- Keep text CONCISE — max 2 lines per bullet point. Trainees won't read paragraphs.
- But DO NOT sacrifice completeness for brevity. If there are many items, show them all as short bullets.
- Focus on ACTIONABLE information: what to do, what not to do, what to check
- Use ⚠️ emoji markers for critical warnings or high-priority items
- Use specific numbers, limits, and deadlines wherever they appear

## EFFICIENCY GUIDANCE

Keep CSS concise — no comments, no redundant properties. Use shorthand CSS (e.g., `margin: 8px 16px` not separate margin-top/bottom/left/right). Minimize JavaScript comments. Prioritize complete slide content data over verbose styling.

## DESIGN SPECIFICATION

The HTML must be a dark-themed, mobile-friendly animated slideshow with these characteristics:

### Layout
- Max width 640px, centered, rounded container with shadow
- Top bar showing: slide icon, slide title, slide counter (e.g., "3 / 14")
- Progress bar under the top bar that fills as slides advance
- Main content area (min-height 520px, scrollable if content overflows) with slide content
- The parent application provides external navigation controls. You may optionally include subtle slide-indicator dots but do NOT include Back/Next/Auto-play buttons — these are handled externally.

### Styling
- Import Google Font: DM Sans (body) and DM Serif Display (headings/numbers)
- Dark backgrounds using CSS gradients — each slide gets a DIFFERENT gradient
- VARY the gradients across slides — use deep blues, purples, dark teals, charcoals, dark reds, dark greens
- Body background: #0a0a0f
- Card/container background: #111
- Text colors: white for headings, rgba(255,255,255,0.75) for body, rgba(255,255,255,0.5) for secondary
- Accent colors — rotate through these across slides: #E63946 (red), #F4A261 (amber), #E76F51 (coral), #2A9D8F (teal), #E9C46A (gold), #264653 (dark teal), #8338EC (purple), #06D6A0 (green)
- Each slide's accent color should be used for: the top bar title, progress bar, check icons, and card borders

### Content Overflow
- The slide content area MUST have `overflow-y: auto` so that slides with many items are scrollable
- Add a subtle scroll indicator (gradient fade at bottom) when content overflows

### Animations
Every element must animate in when the slide appears. Use CSS transitions triggered by adding a 'visible' class via JavaScript:

- **Staggered reveals**: Items in lists/grids animate in one by one with increasing delays (0.05–0.1s between items)
- **Stat cards**: Scale from 0.9 to 1.0 + translate up with cubic-bezier(0.34, 1.56, 0.64, 1) for a bouncy feel
- **List items**: Slide in from the right (translateX) with fade
- **Warning boxes**: Scale from 0.95 to 1.0 with fade
- **Cover elements**: Large icon scales from 0.3 to 1.0, title slides up from 40px
- **Slide transitions**: Content fades out (opacity 0 over 300ms), new content builds, then fades in

## TECHNICAL REQUIREMENTS

- Single self-contained HTML file — NO external dependencies except Google Fonts CDN
- All CSS inline in a `<style>` tag
- All JavaScript inline in a `<script>` tag
- Must work on mobile browsers (responsive, touch-friendly buttons min 44px tap target)
- No frameworks — vanilla HTML/CSS/JS only
- Store slides as a JavaScript array of objects
- Use a single render function that builds HTML from the slide data
- Animations triggered by adding CSS classes after a requestAnimationFrame or setTimeout

IMPORTANT: Navigation functions must be globally accessible. Expose these as `window.goToSlide(n)`, `window.nextSlide()`, `window.prevSlide()`, and `window.getSlideCount()`. The parent application will call these via postMessage. `goToSlide(n)` receives a 0-based slide index.

## SLIDE DATA STRUCTURE

Store all slides in a JS array. Each slide object should have:
```
{
  id: 0,
  title: "Slide Title",
  icon: "emoji",
  color: "#hexAccent",
  bgGrad: "linear-gradient(135deg, #dark1 0%, #dark2 100%)",
  type: "cover|contacts|stats|checklist|warning|equipment|risks|dos|detail",
  // Type-specific fields (see below)
}
```

### Type-Specific Fields by Slide Type

- **cover**: `{ title: "Document Title", subtitle: "Subtitle or tagline", organizationName: "Org Name", badge: "SOP" | "POLICY" | etc., mainIcon: "🏗️" }`
- **contacts**: `{ items: [{ name: "John Smith", role: "Safety Officer", phone: "+1 555-0100" }] }`
- **stats**: `{ items: [{ label: "Incidents This Year", value: "12" }] }`
- **checklist**: `{ items: ["Step 1 description", "Step 2 description", ...] }`
- **warning**: `{ description: "Critical alert message", items: ["Warning point 1", "Warning point 2"], severity: "high" | "medium" | "low" }`
- **equipment**: `{ items: [{ name: "Hard Hat", description: "AS/NZS 1801 compliant" }] }`
- **risks**: `{ items: [{ label: "Fall from height", percentage: 85 }] }`
- **dos**: `{ doItems: ["Wear PPE at all times", "Report hazards immediately"], dontItems: ["Never work alone at height", "Don't bypass safety guards"] }`
- **detail**: `{ sections: [{ title: "Category Name", items: ["Detail point 1", "Detail point 2"] }] }`

Slide Types Available:
- **cover**: Title slide with document name, organization, badge
- **contacts**: Key personnel grid with names, phone numbers, roles
- **stats**: Key metric cards in a grid (use for overview statistics or ratings)
- **checklist**: Numbered step-by-step items (use for procedures, requirements, control measures)
- **warning**: Alert-styled boxes with critical information + bullet points
- **equipment**: Icon cards in a grid for equipment, tools, or resources
- **risks**: Animated progress bars showing levels or ratings
- **detail**: Detailed view with categorized information and controls
- **dos**: Two-column DO's and DON'Ts summary

## COMPLETENESS VERIFICATION

Before finalizing the HTML, mentally verify:
- [ ] Every procedure step is included in order
- [ ] Every requirement and standard is mentioned
- [ ] All contact details and phone numbers are shown
- [ ] All equipment and resources are listed
- [ ] All training and certification requirements are specified with intervals
- [ ] All quantities, measurements, and time limits are stated
- [ ] All compliance and regulatory references are included
- [ ] Roles and responsibilities are clearly attributed
- [ ] Safety warnings and control measures are prominent
- [ ] Key takeaways and DO's/DON'Ts are on the final slide

## WHAT MAKES A GREAT RESULT

- A trainee with 30 seconds per slide should understand the key points
- ALL information from the source document is captured — nothing is lost
- The animations make it feel professional and engaging, not like a boring PDF
- Critical warnings STAND OUT with red-tinted borders and alert styling
- Numbers, limits, and contact details are LARGE and prominent
- It looks polished on a phone screen held in portrait orientation
- An auditor comparing the slideshow to the source document would find zero missing information
""";
    }

    /// <summary>
    /// Returns the prompt for generating an HTML slideshow from a video transcript.
    /// </summary>
    public static string GetTranscriptSlideshowPrompt()
    {
        return """
You are a professional training content designer. You will receive a VIDEO TRANSCRIPT from a training video, instructional talk, or briefing session.

Your job is to:
1. Read and analyze the ENTIRE transcript
2. Extract ALL information trainees need to know
3. Generate a COMPLETE, self-contained HTML file that presents this information as an animated auto-playing slideshow

## CRITICAL: INFORMATION EXTRACTION PROCESS

Before writing ANY HTML, you MUST complete this extraction checklist. Go through the entire transcript and extract every item in each category below. If a category is not present in the transcript, skip it. If it IS present, it MUST appear in the slideshow.

### EXTRACTION CHECKLIST

**A. KEY INFORMATION** (→ Cover slide)
- Topic / subject of the talk
- Speaker name or role (if mentioned)
- Organization name (if mentioned)
- Activity / task description

**B. MAIN TOPICS & CONCEPTS** (→ Dedicated slides)
- Every major topic discussed
- Key concepts and definitions
- Important facts and figures
- Any incidents, examples, or case studies referenced

**C. PROCEDURES & STEPS** (→ One or more slides)
- Every step in any procedure described
- Pre-work requirements or prerequisites
- Checks and verifications to perform
- Correct techniques and methods

**D. REQUIREMENTS & STANDARDS** (→ Include in relevant slides)
- Mandatory requirements mentioned
- Quality or performance standards
- Regulatory or legal requirements
- Training and certification requirements

**E. ROLES & RESPONSIBILITIES** (→ Include where relevant)
- Roles mentioned and their duties
- Contact information or reporting lines
- Emergency contacts and procedures

**F. SAFETY CONSIDERATIONS** (→ Dedicated slide if mentioned)
- Hazards and risks discussed
- Control measures and precautions
- Personal protective equipment requirements
- Emergency procedures

**G. EQUIPMENT & RESOURCES** (→ Dedicated slide if substantial)
- All equipment referenced
- Safe use instructions
- Inspection requirements mentioned

**H. COMPLIANCE & REGULATIONS** (→ Include where relevant)
- Regulations cited
- Standards referenced
- Organization policies mentioned

**I. DO'S AND DON'TS** (→ Final slide)
- Every instruction about what TO do
- Every instruction about what NOT to do
- Key takeaways emphasized by the speaker

## SLIDE PLANNING RULES

After extraction, plan your slides following these rules:

1. **Minimum slides**: Create enough slides to cover ALL extracted content. Typical range is 8-16 slides. DO NOT cap at 12 if more content exists.

2. **Slide allocation priority**:
   - Slide 1: ALWAYS a cover/title slide
   - Next slides: One slide per major topic or concept discussed
   - Dedicated slide for: Safety considerations and warnings (if mentioned)
   - Dedicated slide for: Equipment and resources (if substantial)
   - Dedicated slide for: Step-by-step procedures (split into 2 slides if >8 steps)
   - Dedicated slide for: Contacts and emergency procedures (if mentioned)
   - Last slide: ALWAYS a DO's and DON'Ts summary / key takeaways

3. **Never combine unrelated topics** onto one slide. Each distinct topic gets its own slide.

4. **Details are NOT optional**. Every requirement, step, and precaution mentioned must appear.

5. **Numbers and specifics are mandatory**. Include:
   - ALL quantities, limits, and measurements
   - ALL time intervals and deadlines
   - ALL phone numbers or contact details
   - ALL standards/regulation numbers

CRITICAL: Every slide MUST have unique content. No two slides should share the same items/content arrays. Each slide's data must reflect the specific section of the source material it represents.

## OUTPUT REQUIREMENTS

Return ONLY the complete HTML file. No explanation, no markdown fencing, no preamble. Start with `<!DOCTYPE html>` and end with `</html>`.

The output MUST be complete valid HTML starting with `<!DOCTYPE html>` and ending with `</html>`. Do not stop mid-output.

## CONTENT FORMATTING RULES

- Keep text CONCISE — max 2 lines per bullet point. Trainees won't read paragraphs.
- But DO NOT sacrifice completeness for brevity. If there are many items, show them all as short bullets.
- Focus on ACTIONABLE information: what to do, what not to do, what to check
- Use ⚠️ emoji markers for critical warnings
- Use specific numbers, limits, and deadlines wherever they appear in the transcript
- Transform spoken language into clear, scannable bullet points

## EFFICIENCY GUIDANCE

Keep CSS concise — no comments, no redundant properties. Use shorthand CSS (e.g., `margin: 8px 16px` not separate margin-top/bottom/left/right). Minimize JavaScript comments. Prioritize complete slide content data over verbose styling.

## DESIGN SPECIFICATION

The HTML must be a dark-themed, mobile-friendly animated slideshow with these characteristics:

### Layout
- Max width 640px, centered, rounded container with shadow
- Top bar showing: slide icon, slide title, slide counter (e.g., "3 / 14")
- Progress bar under the top bar that fills as slides advance
- Main content area (min-height 520px, scrollable if content overflows) with slide content
- The parent application provides external navigation controls. You may optionally include subtle slide-indicator dots but do NOT include Back/Next/Auto-play buttons — these are handled externally.

### Styling
- Import Google Font: DM Sans (body) and DM Serif Display (headings/numbers)
- Dark backgrounds using CSS gradients — each slide gets a DIFFERENT gradient
- VARY the gradients across slides — use deep blues, purples, dark teals, charcoals, dark reds, dark greens
- Body background: #0a0a0f
- Card/container background: #111
- Text colors: white for headings, rgba(255,255,255,0.75) for body, rgba(255,255,255,0.5) for secondary
- Accent colors — rotate through these across slides: #E63946 (red), #F4A261 (amber), #E76F51 (coral), #2A9D8F (teal), #E9C46A (gold), #264653 (dark teal), #8338EC (purple), #06D6A0 (green)
- Each slide's accent color should be used for: the top bar title, progress bar, check icons, and card borders

### Content Overflow
- The slide content area MUST have `overflow-y: auto` so that slides with many items are scrollable
- Add a subtle scroll indicator (gradient fade at bottom) when content overflows

### Animations
Every element must animate in when the slide appears. Use CSS transitions triggered by adding a 'visible' class via JavaScript:

- **Staggered reveals**: Items in lists/grids animate in one by one with increasing delays (0.05–0.1s between items)
- **Stat cards**: Scale from 0.9 to 1.0 + translate up with cubic-bezier(0.34, 1.56, 0.64, 1) for a bouncy feel
- **List items**: Slide in from the right (translateX) with fade
- **Warning boxes**: Scale from 0.95 to 1.0 with fade
- **Cover elements**: Large icon scales from 0.3 to 1.0, title slides up from 40px
- **Slide transitions**: Content fades out (opacity 0 over 300ms), new content builds, then fades in

## TECHNICAL REQUIREMENTS

- Single self-contained HTML file — NO external dependencies except Google Fonts CDN
- All CSS inline in a `<style>` tag
- All JavaScript inline in a `<script>` tag
- Must work on mobile browsers (responsive, touch-friendly buttons min 44px tap target)
- No frameworks — vanilla HTML/CSS/JS only
- Store slides as a JavaScript array of objects
- Use a single render function that builds HTML from the slide data
- Animations triggered by adding CSS classes after a requestAnimationFrame or setTimeout

IMPORTANT: Navigation functions must be globally accessible. Expose these as `window.goToSlide(n)`, `window.nextSlide()`, `window.prevSlide()`, and `window.getSlideCount()`. The parent application will call these via postMessage. `goToSlide(n)` receives a 0-based slide index.

## SLIDE DATA STRUCTURE

Store all slides in a JS array. Each slide object should have:
```
{
  id: 0,
  title: "Slide Title",
  icon: "emoji",
  color: "#hexAccent",
  bgGrad: "linear-gradient(135deg, #dark1 0%, #dark2 100%)",
  type: "cover|contacts|stats|checklist|warning|equipment|risks|dos|detail",
  // Type-specific fields (see below)
}
```

### Type-Specific Fields by Slide Type

- **cover**: `{ title: "Document Title", subtitle: "Subtitle or tagline", organizationName: "Org Name", badge: "SOP" | "POLICY" | etc., mainIcon: "🏗️" }`
- **contacts**: `{ items: [{ name: "John Smith", role: "Safety Officer", phone: "+1 555-0100" }] }`
- **stats**: `{ items: [{ label: "Incidents This Year", value: "12" }] }`
- **checklist**: `{ items: ["Step 1 description", "Step 2 description", ...] }`
- **warning**: `{ description: "Critical alert message", items: ["Warning point 1", "Warning point 2"], severity: "high" | "medium" | "low" }`
- **equipment**: `{ items: [{ name: "Hard Hat", description: "AS/NZS 1801 compliant" }] }`
- **risks**: `{ items: [{ label: "Fall from height", percentage: 85 }] }`
- **dos**: `{ doItems: ["Wear PPE at all times", "Report hazards immediately"], dontItems: ["Never work alone at height", "Don't bypass safety guards"] }`
- **detail**: `{ sections: [{ title: "Category Name", items: ["Detail point 1", "Detail point 2"] }] }`

## WHAT MAKES A GREAT RESULT

- A trainee with 30 seconds per slide should understand the key points
- ALL information from the transcript is captured — nothing is lost
- Spoken language is transformed into clear, scannable bullet points
- The animations make it feel professional and engaging
- Critical warnings STAND OUT with red-tinted borders and alert styling
- Numbers, limits, and contact details are LARGE and prominent
- It looks polished on a phone screen held in portrait orientation
""";
    }

    /// <summary>
    /// Returns the prompt for generating an HTML slideshow from parsed section content.
    /// The caller prepends the formatted sections before this prompt.
    /// </summary>
    public static string GetSectionsSlideshowPrompt()
    {
        return """
You are a professional training content designer. You will receive a set of SECTION TITLES AND CONTENT from a training document that has already been parsed into structured sections.

Your job is to:
1. Read and analyze ALL provided sections
2. Extract ALL information trainees need to know
3. Generate a COMPLETE, self-contained HTML file that presents this information as an animated auto-playing slideshow

## CRITICAL: CONTENT MAPPING

Each input section should map to one or more slides. Do NOT skip or summarize away any section content. The sections have already been curated — every piece of information is important.

## SLIDE PLANNING RULES

1. **Minimum slides**: Create enough slides to cover ALL section content. Typical range is 8-16 slides. DO NOT cap at 12 if more content exists.

2. **Slide allocation priority**:
   - Slide 1: ALWAYS a cover/title slide using the document title
   - Next slides: One slide per input section (split large sections into 2 slides if >8 bullet points)
   - Dedicated slide for: Safety considerations and warnings (if any section contains hazards/PPE/emergency content)
   - Last slide: ALWAYS a DO's and DON'Ts summary / key takeaways drawn from all sections

3. **Never combine unrelated sections** onto one slide. Each input section gets its own slide(s).

4. **Details are NOT optional**. Every requirement, step, and control measure from the sections must appear somewhere.

5. **Numbers and specifics are mandatory**. Include ALL quantities, limits, thresholds, measurements, time intervals, deadlines, phone numbers, contact details, reference numbers, and standards.

CRITICAL: Every slide MUST have unique content. No two slides should share the same items/content arrays.

## OUTPUT REQUIREMENTS

Return ONLY the complete HTML file. No explanation, no markdown fencing, no preamble. Start with `<!DOCTYPE html>` and end with `</html>`.

The output MUST be complete valid HTML starting with `<!DOCTYPE html>` and ending with `</html>`. Do not stop mid-output.

## CONTENT FORMATTING RULES

- Keep text CONCISE — max 2 lines per bullet point. Trainees won't read paragraphs.
- But DO NOT sacrifice completeness for brevity. If there are many items, show them all as short bullets.
- Focus on ACTIONABLE information: what to do, what not to do, what to check
- Use ⚠️ emoji markers for critical warnings or high-priority items
- Use specific numbers, limits, and deadlines wherever they appear
- Transform HTML content into clear, scannable bullet points

## EFFICIENCY GUIDANCE

Keep CSS concise — no comments, no redundant properties. Use shorthand CSS (e.g., `margin: 8px 16px` not separate margin-top/bottom/left/right). Minimize JavaScript comments. Prioritize complete slide content data over verbose styling.

## DESIGN SPECIFICATION

The HTML must be a dark-themed, mobile-friendly animated slideshow with these characteristics:

### Layout
- Max width 640px, centered, rounded container with shadow
- Top bar showing: slide icon, slide title, slide counter (e.g., "3 / 14")
- Progress bar under the top bar that fills as slides advance
- Main content area (min-height 520px, scrollable if content overflows) with slide content
- The parent application provides external navigation controls. You may optionally include subtle slide-indicator dots but do NOT include Back/Next/Auto-play buttons — these are handled externally.

### Styling
- Import Google Font: DM Sans (body) and DM Serif Display (headings/numbers)
- Dark backgrounds using CSS gradients — each slide gets a DIFFERENT gradient
- VARY the gradients across slides — use deep blues, purples, dark teals, charcoals, dark reds, dark greens
- Body background: #0a0a0f
- Card/container background: #111
- Text colors: white for headings, rgba(255,255,255,0.75) for body, rgba(255,255,255,0.5) for secondary
- Accent colors — rotate through these across slides: #E63946 (red), #F4A261 (amber), #E76F51 (coral), #2A9D8F (teal), #E9C46A (gold), #264653 (dark teal), #8338EC (purple), #06D6A0 (green)
- Each slide's accent color should be used for: the top bar title, progress bar, check icons, and card borders

### Content Overflow
- The slide content area MUST have `overflow-y: auto` so that slides with many items are scrollable
- Add a subtle scroll indicator (gradient fade at bottom) when content overflows

### Animations
Every element must animate in when the slide appears. Use CSS transitions triggered by adding a 'visible' class via JavaScript:

- **Staggered reveals**: Items in lists/grids animate in one by one with increasing delays (0.05–0.1s between items)
- **Stat cards**: Scale from 0.9 to 1.0 + translate up with cubic-bezier(0.34, 1.56, 0.64, 1) for a bouncy feel
- **List items**: Slide in from the right (translateX) with fade
- **Warning boxes**: Scale from 0.95 to 1.0 with fade
- **Cover elements**: Large icon scales from 0.3 to 1.0, title slides up from 40px
- **Slide transitions**: Content fades out (opacity 0 over 300ms), new content builds, then fades in

## TECHNICAL REQUIREMENTS

- Single self-contained HTML file — NO external dependencies except Google Fonts CDN
- All CSS inline in a `<style>` tag
- All JavaScript inline in a `<script>` tag
- Must work on mobile browsers (responsive, touch-friendly buttons min 44px tap target)
- No frameworks — vanilla HTML/CSS/JS only
- Store slides as a JavaScript array of objects
- Use a single render function that builds HTML from the slide data
- Animations triggered by adding CSS classes after a requestAnimationFrame or setTimeout

IMPORTANT: Navigation functions must be globally accessible. Expose these as `window.goToSlide(n)`, `window.nextSlide()`, `window.prevSlide()`, and `window.getSlideCount()`. The parent application will call these via postMessage. `goToSlide(n)` receives a 0-based slide index.

## SLIDE DATA STRUCTURE

Store all slides in a JS array. Each slide object should have:
```
{
  id: 0,
  title: "Slide Title",
  icon: "emoji",
  color: "#hexAccent",
  bgGrad: "linear-gradient(135deg, #dark1 0%, #dark2 100%)",
  type: "cover|contacts|stats|checklist|warning|equipment|risks|dos|detail",
  // Type-specific fields (see below)
}
```

### Type-Specific Fields by Slide Type

- **cover**: `{ title: "Document Title", subtitle: "Subtitle or tagline", organizationName: "Org Name", badge: "TRAINING", mainIcon: "🏗️" }`
- **contacts**: `{ items: [{ name: "John Smith", role: "Safety Officer", phone: "+1 555-0100" }] }`
- **stats**: `{ items: [{ label: "Incidents This Year", value: "12" }] }`
- **checklist**: `{ items: ["Step 1 description", "Step 2 description", ...] }`
- **warning**: `{ description: "Critical alert message", items: ["Warning point 1", "Warning point 2"], severity: "high" | "medium" | "low" }`
- **equipment**: `{ items: [{ name: "Hard Hat", description: "AS/NZS 1801 compliant" }] }`
- **risks**: `{ items: [{ label: "Fall from height", percentage: 85 }] }`
- **dos**: `{ doItems: ["Wear PPE at all times", "Report hazards immediately"], dontItems: ["Never work alone at height", "Don't bypass safety guards"] }`
- **detail**: `{ sections: [{ title: "Category Name", items: ["Detail point 1", "Detail point 2"] }] }`

## WHAT MAKES A GREAT RESULT

- A trainee with 30 seconds per slide should understand the key points
- ALL information from the sections is captured — nothing is lost
- The animations make it feel professional and engaging
- Critical warnings STAND OUT with red-tinted borders and alert styling
- Numbers, limits, and contact details are LARGE and prominent
- It looks polished on a phone screen held in portrait orientation
""";
    }
}
