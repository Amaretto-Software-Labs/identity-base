# Contributing

## Before You Start
- Read the [Engineering Principles](docs/Engineering_Principles.md) and [Database Design Guidelines](docs/Database_Design_Guidelines.md).
- Review the current sprint plan under `docs/sprints/` to align with active stories and priorities.
- Ensure prerequisites in `Identity.Base/docs/README.md` are satisfied locally.

## Workflow
1. Create a story-aligned branch and keep commits focused.
2. Keep feature work encapsulated within feature folders (Authentication, Users, Email) and update extension modules as needed.
3. Pair documentation updates with code changes, including ERDs and README adjustments.

## Code Review Expectations
- Open pull requests with a concise summary, acceptance criteria validation, and testing notes.
- Request review from at least one domain owner; include relevant sprint story IDs.
- Address feedback promptly and note follow-up tasks if deferring fixes.
- Tests and health checks should run clean locally before requesting review.

## Definition of Done
- Code, tests, and documentation updated.
- Builds (`dotnet build Identity.sln`) and critical tests pass locally.
- Configuration changes validated through options binding or feature toggles.
- Ticket status updated with links to the merged PR and any follow-up tasks.
