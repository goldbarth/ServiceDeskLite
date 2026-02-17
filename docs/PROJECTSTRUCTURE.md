# Project Structure

```
ServiceDeskLite/
├── .editorconfig
├── .github/
│   └── workflows/
│       └── ci.yml
├── CLAUDE.md
├── CONTRIBUTING.md
├── Directory.Build.props
├── global.json
├── LICENSE
├── README.md
├── requests/
│   └── api.http
├── ServiceDeskLite.slnx
│
├── src/
│   ├── ServiceDeskLite.Domain/
│   │   ├── Common/
│   │   │   ├── DomainError.cs
│   │   │   ├── DomainException.cs
│   │   │   └── Guard.cs
│   │   └── Tickets/
│   │       ├── Ticket.cs
│   │       ├── TicketErrors.cs
│   │       ├── TicketId.cs
│   │       ├── TicketPriority.cs
│   │       ├── TicketStatus.cs
│   │       └── TicketWorkflow.cs
│   │
│   ├── ServiceDeskLite.Application/
│   │   ├── Abstractions/
│   │   │   └── Persistence/
│   │   │       ├── ITicketRepository.cs
│   │   │       └── IUnitOfWork.cs
│   │   ├── Common/
│   │   │   ├── ApplicationError.cs
│   │   │   ├── DomainExceptionMapper.cs
│   │   │   ├── ErrorType.cs
│   │   │   ├── PersistenceExceptionMapper.cs
│   │   │   ├── Result.cs
│   │   │   └── ResultOfT.cs
│   │   ├── DependencyInjection/
│   │   │   └── DependencyInjection.cs
│   │   └── Tickets/
│   │       ├── CreateTicket/
│   │       │   ├── CreateTicketCommand.cs
│   │       │   ├── CreateTicketHandler.cs
│   │       │   └── CreateTicketResult.cs
│   │       ├── GetTicketById/
│   │       │   ├── GetTicketByIdHandler.cs
│   │       │   ├── GetTicketByIdQuery.cs
│   │       │   └── TicketDetailsDto.cs
│   │       ├── SearchTickets/
│   │       │   ├── SearchTickesResult.cs
│   │       │   ├── SearchTicketsHandler.cs
│   │       │   └── SearchTicketsQuery.cs
│   │       └── Shared/
│   │           ├── PagedResult.cs
│   │           ├── Paging.cs
│   │           ├── SortSpec.cs
│   │           ├── TicketListItemDto.cs
│   │           └── TicketSearchCriteria.cs
│   │
│   ├── ServiceDeskLite.Contracts/
│   │   └── V1/
│   │       ├── Common/
│   │       │   ├── ContractsProblemDetailsConventions.cs
│   │       │   ├── PagedResponse.cs
│   │       │   └── SortDirection.cs
│   │       └── Tickets/
│   │           ├── CreateTicketRequest.cs
│   │           ├── CreateTicketResponse.cs
│   │           ├── SearchTicketsRequest.cs
│   │           ├── TicketListItemResponse.cs
│   │           ├── TicketPriority.cs
│   │           ├── TicketResponse.cs
│   │           └── TicketSortField.cs
│   │
│   ├── ServiceDeskLite.Infrastructure/
│   │   └── Persistence/
│   │       ├── Configurations/
│   │       │   ├── TicketConfiguration.cs
│   │       │   └── TicketIdConverter.cs
│   │       ├── DependencyInjection/
│   │       │   └── InfrastructureServiceCollectionExtensions.cs
│   │       ├── Repositories/
│   │       │   └── EfTicketRepository.cs
│   │       ├── ServiceDeskLiteDbContext.cs
│   │       └── UnitOfWork/
│   │           └── EfUnitOfWork.cs
│   │
│   ├── ServiceDeskLite.Infrastructure.InMemory/
│   │   ├── DependencyInjection/
│   │   │   └── InMemoryServiceCollectionExtensions.cs
│   │   └── Persistence/
│   │       ├── InMemoryStore.cs
│   │       ├── InMemoryTicketRepository.cs
│   │       └── InMemoryUnitOfWork.cs
│   │
│   ├── ServiceDeskLite.Api/
│   │   ├── Composition/
│   │   │   ├── ApiErrorHandlingExtensions.cs
│   │   │   ├── ApiLoggingExtensions.cs
│   │   │   ├── InfrastructureComposition.cs
│   │   │   └── OpenApi.cs
│   │   ├── Endpoints/
│   │   │   └── TicketsEndpoints.cs
│   │   ├── Http/
│   │   │   ├── ExceptionHandling/
│   │   │   │   ├── ApiExceptionHandler.cs
│   │   │   │   ├── ExceptionClassification.cs
│   │   │   │   └── ExceptionToApplicationErrorMapper.cs
│   │   │   ├── Observability/
│   │   │   │   ├── Correlation.cs
│   │   │   │   └── LogEvents.cs
│   │   │   └── ProblemDetails/
│   │   │       ├── ApiProblemDetailsConventions.cs
│   │   │       ├── ApiProblemDetailsFactory.cs
│   │   │       ├── HttpContextAccessorHolder.cs
│   │   │       ├── ResultMappingExtensions.cs
│   │   │       └── ResultToProblemDetailsMapper.cs
│   │   ├── Mapping/
│   │   │   └── Tickets/
│   │   │       ├── SearchTicketsMapping.cs
│   │   │       ├── TicketEnumMapping.cs
│   │   │       └── TicketsMapping.cs
│   │   ├── Program.cs
│   │   ├── appsettings.json
│   │   └── appsettings.Development.json
│   │
│   └── ServiceDeskLite.Web/
│       ├── Components/
│       │   ├── App.razor
│       │   ├── Routes.razor
│       │   ├── _Imports.razor
│       │   ├── Layout/
│       │   │   ├── MainLayout.razor
│       │   │   ├── MainLayout.razor.css
│       │   │   ├── NavMenu.razor
│       │   │   ├── NavMenu.razor.css
│       │   │   ├── ReconnectModal.razor
│       │   │   ├── ReconnectModal.razor.css
│       │   │   └── ReconnectModal.razor.js
│       │   └── Pages/
│       │       ├── Counter.razor
│       │       ├── Error.razor
│       │       ├── Home.razor
│       │       ├── NotFound.razor
│       │       └── Weather.razor
│       ├── Program.cs
│       ├── appsettings.json
│       ├── appsettings.Development.json
│       └── wwwroot/
│           ├── app.css
│           ├── favicon.png
│           └── lib/bootstrap/dist/...
│
└── tests/
    ├── ServiceDeskLite.Tests.Domain/
    │   └── Tickets/
    │       ├── TicketTests.cs
    │       └── TicketWorkflowTests.cs
    │
    ├── ServiceDeskLite.Tests.Application/
    │   ├── Common/
    │   │   └── ResultTests.cs
    │   └── Tickets/
    │       ├── CreateTicket/
    │       │   └── CreateTicketHandlerTests.cs
    │       ├── GetTicketById/
    │       │   └── GetTicketByIdHandlerTests.cs
    │       └── SearchTickets/
    │           └── SearchTicketsHandlerTests.cs
    │
    ├── ServiceDeskLite.Tests.Api/
    │   ├── ErrorHandling/
    │   │   ├── BadRequest_Binding_Tests.cs
    │   │   ├── DomainException_Fallback_Tests.cs
    │   │   └── UnhandledException_Logging_Tests.cs
    │   └── Infrastructure/
    │       ├── ApiWebApplicationFactory.cs
    │       ├── InMemorySink.cs
    │       └── TestEndpointFilter.cs
    │
    ├── ServiceDeskLite.Tests.Infrastructure.InMemory/
    │   └── InMemoryTicketRepositoryTests.cs
    │
    └── ServiceDeskLite.Tests.EndToEnd/
        ├── Composition/
        │   ├── ProviderMatrixAttribute.cs
        │   ├── TestServiceProvider.cs
        │   └── TicketFactory.cs
        └── Tickets/
            ├── CommitBoundaryTests.cs
            ├── DeterministicPagingSortingTests.cs
            ├── DuplicateDetectionTests.cs
            └── ReadIsolationTests.cs
```