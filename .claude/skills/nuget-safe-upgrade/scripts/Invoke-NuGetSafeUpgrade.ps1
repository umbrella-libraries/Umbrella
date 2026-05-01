[CmdletBinding()]
param(
    [ValidateSet('Analyze', 'Apply')]
    [string]$Mode = 'Analyze',
    [string]$RepoRoot,
    [string]$SolutionPath,
    [string]$ExclusionsPath = 'nuget-upgrade-exclusions.json',
    [string[]]$PackageId,
    [string[]]$Project,
    [string[]]$OverrideBlockedPackageId,
    [switch]$AllowPrerelease,
    [string]$ReportPath
)

$ErrorActionPreference = 'Stop'
$sharedScript = Resolve-Path -Path (Join-Path -Path $PSScriptRoot -ChildPath '..\..\..\..\.ai-shared\nuget-safe-upgrade\scripts\Invoke-NuGetSafeUpgrade.ps1')

$parameters = @{
    Mode = $Mode
}

if ($PSBoundParameters.ContainsKey('RepoRoot')) { $parameters.RepoRoot = $RepoRoot }
if ($PSBoundParameters.ContainsKey('SolutionPath')) { $parameters.SolutionPath = $SolutionPath }
if ($PSBoundParameters.ContainsKey('ExclusionsPath')) { $parameters.ExclusionsPath = $ExclusionsPath }
if ($PSBoundParameters.ContainsKey('PackageId')) { $parameters.PackageId = $PackageId }
if ($PSBoundParameters.ContainsKey('Project')) { $parameters.Project = $Project }
if ($PSBoundParameters.ContainsKey('OverrideBlockedPackageId')) { $parameters.OverrideBlockedPackageId = $OverrideBlockedPackageId }
if ($AllowPrerelease) { $parameters.AllowPrerelease = $true }
if ($PSBoundParameters.ContainsKey('ReportPath')) { $parameters.ReportPath = $ReportPath }

& $sharedScript @parameters
if (-not $?) {
    exit 1
}
