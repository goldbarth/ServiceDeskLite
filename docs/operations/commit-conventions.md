#### Commit Conventions

Pattern: `type(optional-scope): short description` (imperative mood)

| Type       | 	Meaning                               |
|------------|----------------------------------------|
| `feat`     | 	New functionality                     |
| `fix`      | 	Bug fix                               |
| `refactor` | 	Structural change, no behavior change |
| `chore`    | 	Setup, config, dependencies           |
| `docs`     | 	Documentation only                    |
| `test`     | 	Tests only                            |
| `build`    | 	Build system changes                  |
| `ci`       | 	CI/CD configuration                   |
| `perf`     | 	Performance improvements              |
| `revert`   | 	Revert a previous commit              |

Active scopes: `web`, `domain`, `app`, `infra`, `api`, `ci`, `contracts`, `repo`

If a change spans multiple scopes, omit the scope or split into separate commits.
