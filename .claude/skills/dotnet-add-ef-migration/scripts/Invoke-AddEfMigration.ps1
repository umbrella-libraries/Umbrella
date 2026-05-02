[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$MigrationName,

    [string]$RepoRoot,
    [string]$MigrationsProject,
    [string]$StartupProject,
    [string]$Context
)

$ErrorActionPreference = 'Stop'
$sharedScript = Resolve-Path -Path (Join-Path -Path $PSScriptRoot -ChildPath '..\..\..\..\.ai-shared\dotnet-add-ef-migration\scripts\Invoke-AddEfMigration.ps1')

$parameters = @{ MigrationName = $MigrationName }

if ($PSBoundParameters.ContainsKey('RepoRoot'))          { $parameters.RepoRoot          = $RepoRoot }
if ($PSBoundParameters.ContainsKey('MigrationsProject')) { $parameters.MigrationsProject = $MigrationsProject }
if ($PSBoundParameters.ContainsKey('StartupProject'))    { $parameters.StartupProject    = $StartupProject }
if ($PSBoundParameters.ContainsKey('Context'))           { $parameters.Context           = $Context }

& $sharedScript @parameters
if (-not $?) {
    exit 1
}
