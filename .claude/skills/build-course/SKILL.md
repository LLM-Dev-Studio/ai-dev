---
name: build-course
description: Explore a target codebase and generate a complete course for it in the M:\Courses platform. Use when the user wants to create a course about another project, codebase, or tool — e.g. "build a course for M:\ai-dev-net", "create a course about this repo", "make a course on X".
---

# Build Course

Explore a target codebase, design a curriculum, then write all course files into `M:\Courses\courses\`.

**Before writing a single lesson, read the authoring reference:**
`M:\Courses\courses\How to Create Courses\` — every lesson in that course is a spec. Follow it exactly for structure, callout syntax, quiz frontmatter, and quality rules.

## Inputs

The user provides:
- **Target codebase path** — the repo to learn from (e.g. `M:\ai-dev-net`)
- **Audience** (optional) — who will take the course; default to "developers"
- **Depth** (optional) — "intro" (3–4 modules) or "deep" (6–8 modules); default "intro"

## Phase 1 — Read the Authoring Guide

Spawn an `Explore` subagent against `M:\Courses\courses\How to Create Courses\` and extract:
- Required file/folder structure and naming conventions
- Lesson writing pattern (4-part structure, Key Takeaway rule)
- Callout syntax and available types
- Quiz frontmatter schema
- Content quality checklist

Hold this as your authoring rulebook for the rest of the skill.

## Phase 2 — Explore the Target Codebase

Spawn a second `Explore` subagent against the target path. Ask it to return:

1. **What the project is** — purpose, problem it solves, primary tech stack
2. **Key concepts & mental models** — domain terms, core abstractions, architecture patterns
3. **User-facing features** — what someone *does* with it day to day
4. **Getting-started path** — setup steps, entry points, first meaningful thing a learner can do
5. **Non-obvious gotchas** — anything that trips up newcomers

Search across README files, docs/, src/ entry points, config files, and example/demo directories.

Run Phases 1 and 2 in parallel.

## Phase 3 — Design the Curriculum

Using both outputs, design the course before writing any files:

1. **Course metadata**
   - `courseId`: slugified title (lowercase, hyphens, no spaces)
   - `title`, `subtitle`, `audience`

2. **Module plan** (3–4 for intro, 6–8 for deep)
   - Each module: a thematic cluster of 4–6 lessons + 1 quiz
   - Module overview title pattern: `# Module N Overview: {Theme}`
   - Progression: orient → understand → apply → assess

3. **Lesson plan per module**
   - Files numbered from `1-module-overview.md` through `N-{topic}.md`
   - Final file per module: the quiz (e.g. `7-{theme}-quiz.md`)
   - File names: lowercase, hyphen-separated, numeric prefix

4. **Quiz design** (5–8 questions per module)
   - Four options each; one correct answer that exactly matches an option string
   - Include a brief explanation per question

Output the plan as a brief list and confirm the structure is sound before writing.

## Phase 4 — Write the Course Files

**Courses root:** `M:\Courses\courses\`

### Folder layout

```
M:\Courses\courses\{Course Folder Name}\
├── course.json
├── 1-intro.md
└── modules\
    ├── 01\
    │   ├── 1-module-overview.md
    │   ├── 2-{topic}.md
    │   ├── ...
    │   └── N-{theme}-quiz.md
    ├── 02\
    └── ...
```

- Course folder name = title as written (spaces preserved), e.g. `AI Dev Studio Fundamentals`
- Module folders: zero-padded two digits (`01`, `02`, … `10`)

### course.json

```json
{
  "courseId": "{{courseId}}",
  "title": "{{title}}",
  "subtitle": "{{subtitle}}",
  "audience": "{{audience}}"
}
```

### 1-intro.md frontmatter

```yaml
---
courseId: {{courseId}}
title: {{title}}
subtitle: {{subtitle}}
audience: {{audience}}
---
```

### Lesson template (follow the 4-part structure from the authoring guide)

Every content lesson must end with a `## Key Takeaway` section — one crisp sentence.

### Quiz frontmatter

```yaml
---
lessonType: quiz
passThreshold: 80
maxAttempts: 2
resetScopeOnFail: module
questions:
  - prompt: "{{Question?}}"
    options:
      - "{{A}}"
      - "{{B}}"
      - "{{C}}"
      - "{{D}}"
    answer: "{{A}}"
    explanation: "{{Why A is correct}}"
---
```

## Quality Checklist

- [ ] Read `How to Create Courses` authoring guide before writing any content
- [ ] `courseId` in `course.json` matches frontmatter in `1-intro.md`
- [ ] Every module has a `1-module-overview.md`
- [ ] Every module ends with a quiz file
- [ ] Every quiz `answer` exactly matches one of its `options` strings
- [ ] Every lesson ends with `## Key Takeaway`
- [ ] Callouts use `> [!TYPE]` syntax (NOTE, TIP, WARNING, SUCCESS, INFORMATION, IMPORTANT)
- [ ] Content references real artefacts from the target codebase (file names, functions, concepts)
- [ ] No module has more than 8 lesson files
- [ ] Run `npm run build` from `M:\Courses\site` to confirm no loading errors
