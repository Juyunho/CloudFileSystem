# In Memory Persistence Boundary

## Context

The assignment supplies a fixed sample tree and does not require a database. Adding a database would increase setup cost without improving the evaluated recursive domain behavior.

## Decision

Keep runtime data in memory. Isolate source data behind `IFileDao` and `IDirectoryDao`, inject these into managers, and inject manager abstractions into the application handler. Keep the name DAO because these classes provide data records rather than aggregate-oriented persistence behavior.

The relational ERD is a proposed production persistence model, not an implemented database.

## Alternatives Considered

- Add Entity Framework Core and SQL Server or SQLite.
- Replace DAO and manager layers with one repository.
- Keep direct construction inside managers.

## Consequences

The submission stays easy to run and data sources are replaceable in tests or a later database implementation. Runtime state is process-local and resets at restart. There are no transactions, durable history, or cross-process concurrency controls.
