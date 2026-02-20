## Test Projects

| Project                         | 	Scope                                  | 	Key Patterns                                                          |
|---------------------------------|-----------------------------------------|------------------------------------------------------------------------|
| `Tests.Domain`                  | 	Domain aggregate, workflow transitions | 	`Ticket` constructor, `TicketWorkflow` guard v                        |
| `Tests.Application`             | 	Handler logic, Result monad            | 	`CreateTicketHandler`, `GetTicketByIdHandler`, `SearchTicketsHandler` |
| `Tests.Api`                     | 	HTTP binding, exception handling       | 	`ApiWebApplicationFactory` (InMemory)                                 |
| `Tests.Infrastructure.InMemory` | 	In-memory repository                   | 	`InMemoryTicketRepository`                                            |
| `Tests.EndToEnd`                | 	Full DI pipeline, both providers       | 	`[ProviderMatrix]` attribute                                          |
| `Tests.Integration`             | 	HTTP query binding                     | 	`SearchTicketsRequest` parameter parsing                              |
| `Tests.Web`                     | 	API client deserialization             | 	`TicketsApiClient` + ProblemDetails parsing                           |

---

## Testing Conventions

#### Project Layout Mirrors Source

```
tests/ServiceDeskLite.Tests.Application/Tickets/CreateTicket/
  ↳ tests src/ServiceDeskLite.Application/Tickets/CreateTicket/
```

#### Test Naming

`<MethodOrScenario>_<Condition>_<ExpectedOutcome>`

Examples:

- `NewTicket_StartsWithStatus_New`
- `ChangeStatus_InvalidTransition_ThrowsDomainException_WithExpectedErrorCode`
- `HandleAsync_NullCommand_ReturnsValidationFailure`
-
#### Assertion Library

All tests use FluentAssertions. Prefer `.Should().Be(...)` over `Assert.Equal.`
