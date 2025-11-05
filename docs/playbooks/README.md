# Task Playbooks

This directory contains agent-friendly, step-by-step task playbooks. Each playbook is designed for autonomous execution by humans or LLM agents and follows the same structure and machine-readable cues.

Conventions:
- Use front matter with metadata: `id`, `title`, `version`, `last_reviewed`, `tags`, `required_roles`, `prerequisites`, `required_secrets`.
- Section order: Goal, Preconditions, Resources, Command Steps, File Edits, Configuration Snippets, Verification, Outputs, Completion Checklist.
- Prefix shell snippets with `Command:` and JSON/appsettings with `Config:`. Keep code fences command-only (no prose inside fences).
- Mark optional steps as `Optional Step X:` to enable selective execution.
- Include a Mermaid diagram block to show dependencies or call flows.

See `_template.md` for authoring guidance and `index.yaml` for the manifest used by tooling.
