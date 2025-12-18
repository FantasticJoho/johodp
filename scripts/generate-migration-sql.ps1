<#
.SYNOPSIS
Generate SQL scripts for EF Core migrations for JohodpDbContext and PersistedGrantDbContext.

.DESCRIPTION
Generates SQL migration scripts using `dotnet ef migrations script`:
- JohodpDbContext -> default output file `migration-johodp.sql`
- PersistedGrantDbContext -> default output file `migration-identityserver.sql`

.PARAMETER OutputDir
Directory where SQL files will be written. Defaults to `migrations-sql`.

.PARAMETER NoIdempotent
Switch: disable idempotent output and generate a non-idempotent script. By default the generated scripts are idempotent.

.PARAMETER WhatIf
Dry-run mode: show commands but do not execute them.

.PARAMETER Help
Show full help.

.EXAMPLE
# Generate idempotent scripts into ./sql
.\generate-migration-sql.ps1 -OutputDir sql -Idempotent

.NOTES
- Requires dotnet-ef tools installed and available in PATH.
- Runs from repository root by default.
#>

param(
    [string]$OutputDir = "migrations-sql",
    [switch]$NoIdempotent,
    [switch]$WhatIf,
    [switch]$Help
)

if ($Help) {
    Get-Help -Full -ErrorAction SilentlyContinue $MyInvocation.MyCommand.Path
    return
}

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
Push-Location $scriptRoot/..  # run from repo root

if ($WhatIf) { Write-Host "WhatIf: would create output dir '$OutputDir' and generate scripts" -ForegroundColor Yellow }
else { New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null }

# Helper build of command
function Run-Command([string]$cmd, [string]$description) {
    if ($WhatIf) {
        Write-Host "WhatIf: $cmd" -ForegroundColor Cyan
        return 0
    }

    Write-Host $description -ForegroundColor Green
    Write-Host $cmd -ForegroundColor DarkGray
    $proc = Start-Process -FilePath pwsh -ArgumentList "-NoProfile", "-Command", $cmd -NoNewWindow -PassThru -Wait -RedirectStandardError "$OutputDir/error.log" -RedirectStandardOutput "$OutputDir/output.log"
    if ($proc.ExitCode -ne 0) {
        Write-Error "Command failed with exit code $($proc.ExitCode). See $OutputDir/output.log and $OutputDir/error.log"
        Pop-Location
        exit $proc.ExitCode
    }
}

# Build options
$idem = if ($NoIdempotent) { '' } else { '--idempotent' }

# JohodpDbContext
$JohodpOut = Join-Path $OutputDir 'migration-johodp.sql'
$cmd1 = "dotnet ef migrations script $idem -p src/Johodp.Infrastructure -s src/Johodp.Api --context JohodpDbContext --output $JohodpOut"
Run-Command $cmd1 "Generating JohodpDbContext migration script -> $JohodpOut"

# PersistedGrantDbContext
$IdSrvOut = Join-Path $OutputDir 'migration-identityserver.sql'
$cmd2 = "dotnet ef migrations script $idem -p src/Johodp.Infrastructure -s src/Johodp.Api --context Duende.IdentityServer.EntityFramework.DbContexts.PersistedGrantDbContext --output $IdSrvOut"
Run-Command $cmd2 "Generating PersistedGrantDbContext migration script -> $IdSrvOut"

Write-Host "All scripts generated in: $OutputDir" -ForegroundColor Green
Pop-Location
