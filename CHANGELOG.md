# Changelog

## v1.0.0

### Summary

ServiceDeskLite 1.0.0 is the first complete release of this reference project.
It includes ticket workflow, comments, audit log, search/paging, a dashboard,
Docker setup, and architecture/API documentation.

### Highlights

- Clean Architecture / Layered Structure
- Ticket workflow with explicit transition rules
- Comments and audit history
- Search, filter and paging
- Dashboard with KPIs
- Docker Compose for local demo setup
- OpenAPI and architecture documentation

### Demo Notes

#### Suggested demo flow

1. Start application
2. Open ticket list
3. Filter/search tickets
4. Open ticket details
5. Change status
6. Add comment
7. Inspect audit history
8. Open dashboard
9. Show API docs / architecture docs

#### What to pay attention to

- Thin API / encapsulated application logic
- Centralized domain rules
- Consistent ProblemDetails error handling
- Traceable workflow behavior
- Clean project structure and documentation

### Included scope

- M1 walking skeleton
- M2 workflow and substance
- M3 polish items required for portfolio readiness

### Known limitations

- Not intended for production use
- Demo/reference project with intentionally limited scope
- Security, auth, and multi-user concerns are only partially addressed or not implemented

### Links

- [Repository](https://github.com/FelixStaworski/ServiceDeskLite)
- [Documentation / GitHub Pages](https://felixstaworski.github.io/ServiceDeskLite/)
- [OpenAPI](https://felixstaworski.github.io/ServiceDeskLite/api/)
