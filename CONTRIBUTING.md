# Contributing

## Architecture

Current structure:

- `src/Web` → Blazor UI (Components, Pages, JS-Interop)
- `src/Domain` → Domain Model (Entities, Value Objects, Rules)

Rules:
- No domain logic inside `Web`.
- `Domain` must not depend on `Web`.
- Keep dependencies pointing inward.

---

## Commit Convention

Pattern:

>type(optional-scope): short description

Example:

>feat(web): add layout shell  
fix(domain): validate playlist invariant  
chore: initialize solution structure

### Required Types

| Type     | Meaning |
|----------|----------|
| feat     | New functionality |
| fix      | Bug fix |
| refactor | Structural change without behavior change |
| chore    | Setup, config, dependencies |
| docs     | Documentation only |
| test     | Tests only |

### Optional Types (when needed)

| Type  | Meaning |
|-------|----------|
| build | Build system changes |
| ci    | CI/CD configuration |
| perf  | Performance improvements |
| revert| Revert previous commit |

>Keep the set minimal.

---

## Scopes

Current scopes:

| Scope  | Layer |
|--------|--------|
| web    | UI layer |
| domain | Domain layer |
| repo   | Repository/meta changes |

Future scopes may include:

| Scope | Layer |
|-------|--------|
| app   | Application / UseCases |
| infra | Infrastructure |
| ci    | CI/CD automation |

>If a change affects multiple layers, split the commit or omit the scope.

---

## Pull Requests

Before opening a PR:

- Solution builds
- Tests pass
- No debug code
- Focused change

Use imperative style in commit messages:
>✔ add playlist support  
✘ added playlist support

Consistency is more important than strictness.
