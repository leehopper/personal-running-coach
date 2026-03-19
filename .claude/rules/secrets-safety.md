---
description: Prevent accidental secret exposure
globs: ["**/.env*", "**/secrets.*", "**/credentials.*", "**/appsettings.Local.json"]
---

# Secrets Safety

- NEVER read, display, or commit the matched file — it likely contains secrets
- NEVER include secret values in code, config files, or conversation output
- If you encounter secrets in any file or diff, STOP immediately and warn the user
- Secrets belong in environment variables or .NET user-secrets, never in source control
