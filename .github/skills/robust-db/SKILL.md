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

- `dotnet` SDK and `dotnet-ef` installed (global tool or restored via tool manifest).
- Repository checked out and `dotnet build` succeeds.

## Step-by-step Workflows

1. Add entity and model changes
   - Edit `Content.Server.Database/Model.cs` to add `DbSet<T>` and entity type with `[Table(...)]` and navigation properties.
   - Add EF Fluent API configuration in `OnModelCreating` (indexes, FK, unique constraints).

2. Generate migrations (recommended)
   - Ensure `dotnet-ef` is available.
   - From project folder `Content.Server.Database` run:

```powershell
pwsh
Set-Location .\Content.Server.Database
.\add-migration.ps1 <MigrationName>
```

   - The script will run `dotnet ef migrations add` for both `SqliteServerDbContext` and `PostgresServerDbContext` and place files under `Migrations/Sqlite` and `Migrations/Postgres` respectively.

3. Inspect generated files
   - Verify both contexts have matching logical migrations (Up/Down create the same table/indices).
   - Check `Designer.cs` and the `SqliteServerDbContextModelSnapshot.cs` or Postgres snapshot updated accordingly.

4. Update repo tools/constants
   - If your repo contains scripts expecting a `LATEST_DB_MIGRATION` string (example: `Tools/dump_user_data.py`, `Tools/erase_user_data.py`), update them to reference the new migration id (the string inside `[Migration("...")]`).

5. Implement DB API and usage
   - Add `IServerDbManager` interface methods to expose add/check/remove/list operations.
   - Implement actual EF logic in `ServerDbBase` using `DbContext` and async EF methods.
   - Add wrappers in `ServerDbManager` to forward calls through existing `RunDbCommand` pattern and update metrics counters.

## Troubleshooting

- "PendingModelChangesWarning": run `add-migration` from the project folder and ensure snapshots and migration files are generated for both contexts.
- `dotnet ef` not found: install via `dotnet tool install --global dotnet-ef` or `dotnet tool restore` if repo has a manifest.
- Migration file names: EF generates timestamped filenames; renaming files is OK as long as the `[Migration("...")]` attribute remains consistent and `__EFMigrationsHistory` will record the attribute value.
- If only one context got a migration (e.g., Sqlite auto-generated but Postgres looked manual), re-run the add-migration script from `Content.Server.Database` and inspect output; EF will produce both when run from the project directory.

## Best Practices & Notes

- Keep migration `Up`/`Down` logic symmetric and review FK names and index names to match repository conventions.
- Store prototype identifiers as strings in DB when referencing `ProtoId<T>` to avoid cross-layer serialization complexity.
- Add unique composite indexes for (player_user_id, proto_id) to prevent duplicate entries.
- Use `GetAwaiter().GetResult()` sparingly in completion code only when the environment requires synchronous completions; prefer async everywhere else.

## Useful Paths & Scripts (examples from the repo)

- `Content.Server.Database/add-migration.ps1` — script that runs `dotnet ef migrations add` for both contexts.
- `RobustToolbox/Robust.Benchmarks/add-migration.ps1` — similar helper used elsewhere.
- Model file: `Content.Server.Database/Model.cs` — where entities and `DbSet<>` live.
- Migrations folders: `Content.Server.Database/Migrations/Sqlite` and `.../Postgres`.
- Tools to update after migrations: `Tools/dump_user_data.py`, `Tools/erase_user_data.py` (search for `LATEST_DB_MIGRATION`).

## Example Checklist to Run After Changes

- [ ] Add entity and update `Model.cs`.
- [ ] Run `Content.Server.Database\add-migration.ps1 <Name>` from `Content.Server.Database`.
- [ ] Inspect both `Migrations/Sqlite` and `Migrations/Postgres` files.
- [ ] Update any `LATEST_DB_MIGRATION` constants in `Tools/*`.
- [ ] Implement and wire `IServerDbManager` methods.
- [ ] Add console commands and verify completions.
- [ ] Run `dotnet build` and confirm no pending model changes.

## References

- EF Core migrations docs: https://aka.ms/efcore-docs-pending-changes
- Agent Skill template used to create this file: make-skill-template SKILL.md
