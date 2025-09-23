# ERD Template

Use this template to capture aggregate boundaries and entity relationships for the Identity API domain. Each diagram should be stored in this folder and referenced from the stream-specific documentation.

## Mermaid Template
```mermaid
ergDiagram
  CUSTOMER ||--o{ USER_PROFILE : has
  USER_PROFILE }|..|| MFA_CREDENTIAL : owns
  CUSTOMER ||--o{ EXTERNAL_IDENTITY : links
```

## Documentation Checklist
- Title each diagram with the bounded context or aggregate root name.
- Annotate relationship cardinality (e.g., `||--o{`).
- Describe key invariants or constraints below the diagram.
- Cross-link to requirements or sprint stories driving the change.
