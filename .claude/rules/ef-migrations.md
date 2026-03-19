---
description: EF Core migration safety rules
globs: ["**/Migrations/**/*.cs", "**/Migrations/**"]
---

# EF Core Migration Safety

- NEVER modify or delete existing migration files
- NEVER edit the migration snapshot manually
- Only add new migrations via `dotnet ef migrations add {ShortUniqueDescription}`
- If a migration needs to be undone, create a new migration that reverses the changes
- Run migrations against a local database before committing
