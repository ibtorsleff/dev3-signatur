# DB Patches

SQL patches to apply after restoring a production backup to a dev environment.
They bring the database to the state required by the Blazor portal.

## When to run

After every production backup restore, run all patch scripts in order.

## How to run

Open SSMS or run via sqlcmd against `SignaturAnnoncePortal`:

```
sqlcmd -S localhost -d SignaturAnnoncePortal -i db\patches\001_blazor_localization_keys.sql
```

Or run all patches in order with PowerShell:

```powershell
Get-ChildItem "db\patches\*.sql" | Sort-Object Name | ForEach-Object {
    Write-Host "Applying $($_.Name)..."
    sqlcmd -S localhost -d SignaturAnnoncePortal -i $_.FullName
}
```

## Rules for new patches

- Always use `IF NOT EXISTS` guards — scripts must be safe to re-run.
- Name files sequentially: `002_...sql`, `003_...sql`, etc.
- One concern per file (localization keys, schema changes, seed data, etc.).
- Use `Area = 'BlazorPortal'` for Blazor-only additions so they are easy to identify.
- Add both DK (LanguageId = 3) and EN (LanguageId = 1) rows for every new key.
