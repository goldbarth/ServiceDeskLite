## Key Files for Orientation

| File                                                                          | 	Purpose                                      |
|-------------------------------------------------------------------------------|-----------------------------------------------|
| `src/ServiceDeskLite.Api/Program.cs`                                          | 	API composition root and middleware pipeline |
| `src/ServiceDeskLite.Web/Program.c`                                           | 	Blazor composition root                      |
| `src/ServiceDeskLite.Api/Composition/InfrastructureComposition.cs`            | 	Provider switch (InMemory vs Sqlite)         |
| `src/ServiceDeskLite.Api/Endpoints/TicketsEndpoints.cs`	All API endpoint      | definitions                                   |
| `src/ServiceDeskLite.Api/Mapping/Tickets/TicketEnumMapping.cs`                | 	Contracts ↔ Domain/App mapping               |
| `src/ServiceDeskLite.Application/Common/Result.cs`                            | 	Result monad (void)                          |
| `src/ServiceDeskLite.Application/Common/ApplicationError.cs`                  | 	Error record + ErrorType enum                |
| `src/ServiceDeskLite.Application/Common/PagingPolicy.cs`                      | 	Paging constants                             |
| `src/ServiceDeskLite.Domain/Tickets/Ticket.cs`                                | 	Aggregate root                               |
| `src/ServiceDeskLite.Domain/Tickets/TicketWorkflow.cs`                        | 	Status transition rules                      |
| `src/ServiceDeskLite.Domain/Common/Guard.cs`                                  | 	Invariant enforcement                        |
| `src/ServiceDeskLite.Api/Http/ProblemDetails/ResultToProblemDetailsMapper.cs` | 	Result → HTTP mapping                        |
| `src/ServiceDeskLite.Infrastructure/Persistence/ServiceDeskLiteDbContext.cs`  | 	EF Core DbContext                            |
| `src/ServiceDeskLite.Infrastructure.InMemory/Persistence/InMemoryStore.cs`    | 	In-memory data store                         |
| `src/ServiceDeskLite.Web/ApiClient/TicketsApiClient.cs`                       | 	HTTP client for API                          |
| `tests/ServiceDeskLite.Tests.EndToEnd/Composition/TestServiceProvider.cs`     | 	E2E DI bootstrap                             |
| `tests/ServiceDeskLite.Tests.Api/Infrastructure/ApiWebApplicationFactory.cs`  | 	API test factory                             |
