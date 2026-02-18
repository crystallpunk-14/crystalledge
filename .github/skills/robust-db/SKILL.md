---
name: robust-db
description: 'RobustToolbox database helper: migration, model, and CLI workflows. Use when working with EF Core migrations, adding schema-backed entities, updating model snapshots.'
---

## When to Use This Skill

- You are adding a new EF-backed entity or table to RobustToolbox-based projects.
- You must create matching migrations for both `SqliteServerDbContext` and `PostgresServerDbContext`.
- You need to update model snapshots or tooling that reference the latest migration id.
- You are implementing DB manager methods and server-side console commands that read/write the new entity.

## Prerequisites

- `dotnet` SDK installed and accessible.
- `dotnet-ef` installed as global tool: `dotnet tool install --global dotnet-ef`
- Repository checked out and `dotnet build` succeeds from Content.Server.Database project.
- **CRITICAL:** Both SQLite and PostgreSQL use different data types and constraints - both migration sets are REQUIRED.

## Step-by-step Workflows

1. Add entity and model changes
   - Edit `Content.Server.Database/Model.cs` to add `DbSet<T>` and entity type with `[Table(...)]` and navigation properties.
   - Add EF Fluent API configuration in `OnModelCreating` (indexes, FK, unique constraints).

2. Generate migrations (ALWAYS for both databases)
   - **NEVER generate migrations manually for individual contexts** - always use the provided scripts.
   - Ensure `dotnet-ef` is available: `dotnet tool list -g | findstr dotnet-ef`
   - From project folder `Content.Server.Database` run the appropriate script:

```powershell
# Windows PowerShell
.\add-migration.ps1 MigrationName

# Linux/macOS Bash
./add-migration.sh MigrationName
```

   - The script will AUTOMATICALLY run `dotnet ef migrations add` for BOTH contexts:
     - `SqliteServerDbContext` → `Migrations/Sqlite/`
     - `PostgresServerDbContext` → `Migrations/Postgres/`
   - **WARNING:** If only SQLite migrations are generated, PostgreSQL migrations are missing and MUST be created.

3. Inspect generated files and verify BOTH databases
   - **CRITICAL:** Verify both `Migrations/Sqlite/` and `Migrations/Postgres/` contain new migration files with matching timestamps.
   - Compare migration logic - they should create equivalent tables/indices but with different SQL syntax:
     - **SQLite:** Uses `INTEGER` with `Sqlite:Autoincrement`, `TEXT` for strings/UUIDs
     - **PostgreSQL:** Uses `integer` with `NpgsqlValueGenerationStrategy.IdentityByDefaultColumn`, `text`/`uuid` types
   - Check `Designer.cs` files and ensure model snapshots are updated for both contexts.
   - If PostgreSQL migration is missing, manually generate it: `dotnet ef migrations add --context PostgresServerDbContext -o Migrations/Postgres MigrationName`

4. Update repo tools/constants
   - If your repo contains scripts expecting a `LATEST_DB_MIGRATION` string (example: `Tools/dump_user_data.py`, `Tools/erase_user_data.py`), update them to reference the new migration id (the string inside `[Migration("...")]`).

5. Implement DB API and usage
   - Add `IServerDbManager` interface methods to expose add/check/remove/list operations.
   - Implement actual EF logic in `ServerDbBase` using `DbContext` and async EF methods.
   - Add wrappers in `ServerDbManager` to forward calls through existing `RunDbCommand` pattern and update metrics counters.

## Troubleshooting

- **"Missing PostgreSQL migration"**: Common issue when only SQLite migrations are generated. Run: `dotnet ef migrations add --context PostgresServerDbContext -o Migrations/Postgres MigrationName` from Content.Server.Database folder.
- **"PendingModelChangesWarning"**: Run migration scripts from Content.Server.Database project folder and ensure snapshots are generated for BOTH contexts.
- **`dotnet ef` not found**: Install via `dotnet tool install --global dotnet-ef`. Verify with `dotnet tool list -g`.
- **Migration file names**: EF generates timestamped filenames. Don't rename files, but `[Migration("...")]` attribute value is recorded in `__EFMigrationsHistory`.
- **Script doesn't generate both migrations**: Ensure you run the script from `Content.Server.Database` directory, not from repo root.
- **Different data types in migrations**: This is expected and correct - SQLite and PostgreSQL have different type systems.

## Best Practices & Notes

- **NEVER skip PostgreSQL migrations** - production environments typically use PostgreSQL while development uses SQLite.
- Keep migration `Up`/`Down` logic symmetric and review FK names and index names to match repository conventions.
- Store prototype identifiers as strings in DB when referencing `ProtoId<T>` to avoid cross-layer serialization complexity.
- Add unique composite indexes for (player_user_id, proto_id) to prevent duplicate entries.
- Use `GetAwaiter().GetResult()` sparingly in completion code only when the environment requires synchronous completions; prefer async everywhere else.
- **Data type differences are normal**: SQLite uses TEXT for UUIDs while PostgreSQL uses native uuid type.
- Both `DesignTimeContextFactoryPostgres` and `DesignTimeContextFactorySqlite` exist in `DesignTimeContextFactories.cs` for EF tooling.

## Useful Paths & Scripts

- `Content.Server.Database/add-migration.ps1` — **PRIMARY SCRIPT** for Windows that creates migrations for both contexts.
- `Content.Server.Database/add-migration.sh` — **PRIMARY SCRIPT** for Linux/macOS that creates migrations for both contexts.
- `Content.Server.Database/DesignTimeContextFactories.cs` — Contains factory classes for both SQLite and PostgreSQL contexts.
- Model file: `Content.Server.Database/Model.cs` — where entities, `DbSet<>` properties, and `OnModelCreating` configuration live.
- SQLite migrations: `Content.Server.Database/Migrations/Sqlite/` — Contains all SQLite-specific migrations.
- PostgreSQL migrations: `Content.Server.Database/Migrations/Postgres/` — Contains all PostgreSQL-specific migrations.
- Model snapshots: `*ServerDbContextModelSnapshot.cs` files in each migration folder.
- Tools to update after migrations: `Tools/dump_user_data.py`, `Tools/erase_user_data.py` (search for `LATEST_DB_MIGRATION`).

## Example Checklist to Run After Changes

- [ ] Add entity and update `Model.cs` with `DbSet<T>` property and `OnModelCreating` configuration.
- [ ] **CRITICAL:** Run `Content.Server.Database\add-migration.ps1 <Name>` from `Content.Server.Database` directory.
- [ ] **VERIFY BOTH:** Inspect both `Migrations/Sqlite` and `Migrations/Postgres` folders contain new migration files.
- [ ] **IF MISSING:** Generate missing PostgreSQL migration manually if script didn't create it.
- [ ] Update any `LATEST_DB_MIGRATION` constants in `Tools/*` scripts.
- [ ] Implement and wire `IServerDbManager` methods in `ServerDbBase.cs` and `ServerDbManager.cs`.
- [ ] Add console commands and verify completions.
- [ ] Run `dotnet build` from both repo root and `Content.Server.Database` and confirm no pending model changes.
- [ ] Test with both database types if possible (SQLite for dev, PostgreSQL for production).

## References

- EF Core migrations docs: https://aka.ms/efcore-docs-pending-changes
- Agent Skill template used to create this file: make-skill-template SKILL.md
