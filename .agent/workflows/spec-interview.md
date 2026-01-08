---
description: Deep-dive spec interview - systematically question and refine a specification file until complete
---

# Spec Interview Workflow

Invoke with `/spec-interview` or `/spec` when you have a rough spec or feature idea that needs thorough exploration.

## Trigger

User provides a `@SPEC` file reference (or any specification document) and wants it fully fleshed out through structured questioning.

## Process

### 1. Read the Spec File

// turbo
Read the referenced specification file completely before asking any questions. If none was provided, ask the user for the spec.

### 2. Analyze for Gaps

Identify areas that need clarification across these dimensions:

- **Technical Implementation** - Architecture, data models, integrations, dependencies
- **UI/UX** - User flows, edge cases, accessibility, responsive behavior
- **Concerns** - Security, performance, scalability, maintainability
- **Tradeoffs** - Alternative approaches, what's being sacrificed, why this choice

### 3. Conduct the Interview

Use `notify_user` to ask questions. Follow these rules:

**Question Quality:**

- Questions must be non-obvious - don't ask things easily inferred from the spec
- Be very in-depth - dig into implications, edge cases, failure modes
- Challenge assumptions - "What happens if X is not true?"
- Explore alternatives - "Have you considered Y instead of Z?"

**Question Categories:**

- **Clarifying** - "When you say X, do you mean A or B?"
- **Probing** - "What's the expected behavior when X fails?"
- **Challenging** - "This approach has tradeoff Y - is that acceptable?"
- **Expanding** - "Should this also handle the case of Z?"

**Interview Flow:**

- Ask 2-4 related questions per round (batch to minimize interruptions)
- Wait for user response before next round
- Build on previous answers - don't repeat or ask redundant questions
- Track answered vs. unanswered areas

### 4. Continue Until Complete

Keep interviewing until:

- All dimensions (technical, UI/UX, concerns, tradeoffs) are addressed
- No major ambiguities remain
- User confirms the spec feels complete
- Edge cases and error handling are defined

### 5. Write the Final Spec

Once complete:

- Synthesize all answers into the spec file
- Preserve the original structure where sensible
- Add new sections for areas uncovered during interview
- Mark any remaining open questions with [TBD]
- Show the user the final spec for approval

## Example Questions by Category

**Technical Implementation:**

- "How should the system behave if the external API is down for >30 seconds?"
- "What's the expected data volume? This affects whether we need pagination."
- "Should this operation be idempotent? What about concurrent requests?"

**UI/UX:**

- "What feedback does the user see while this operation is in progress?"
- "How should validation errors be displayed - inline or summary?"
- "What's the mobile experience for this feature?"

**Concerns:**

- "What data here is sensitive? Does it need encryption at rest?"
- "What's the acceptable latency for this operation?"
- "Who can access this? Do we need role-based permissions?"

**Tradeoffs:**

- "This approach is simpler but less flexible - is that okay for v1?"
- "We could cache this, but it adds complexity. Is freshness critical?"
- "Building vs. buying this component - what's your preference?"

## Output

The final deliverable is a complete, unambiguous specification file that:

- A developer could implement without further questions
- Covers happy path AND error cases
- Documents all design decisions and their rationale
- Includes acceptance criteria where applicable