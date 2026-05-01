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

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

. (Join-Path -Path $PSScriptRoot -ChildPath 'NuGetSafeUpgrade.Common.ps1')

if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    $RepoRoot = Resolve-NuGetUpgradeRepoRoot
}
else {
    $RepoRoot = (Resolve-Path -Path $RepoRoot).Path
}

$SolutionPath = Get-NuGetUpgradeSolutionPath -RepoRoot $RepoRoot -SolutionPath $SolutionPath
$config = Get-NuGetUpgradeConfig -RepoRoot $RepoRoot -ConfigPath $ExclusionsPath
$inventory = Get-NuGetUpgradeInventory -RepoRoot $RepoRoot

$definitions = @($inventory.VersionDefinitions)

if (@($PackageId).Count -gt 0) {
    $definitions = $definitions | Where-Object {
        $requested = $_.PackageId
        $PackageId | Where-Object { $requested.Equals($_, [System.StringComparison]::OrdinalIgnoreCase) }
    }
}

if (@($Project).Count -gt 0) {
    $requestedProjects = $Project | ForEach-Object { $_.ToLowerInvariant() }
    $definitions = $definitions | Where-Object {
        foreach ($validation in @($_.ValidationReferences)) {
            $normalizedPath = $validation.RelativeProjectPath.ToLowerInvariant()
            $normalizedName = $validation.ProjectName.ToLowerInvariant()
            foreach ($requested in $requestedProjects) {
                if ($normalizedPath -eq $requested -or $normalizedName -eq $requested -or $normalizedPath.EndsWith($requested)) {
                    return $true
                }
            }
        }

        return $false
    }
}

$allowPrerelease = $AllowPrerelease.IsPresent -or $config.Options.AllowPrerelease
$overrideIds = @(@($OverrideBlockedPackageId) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | ForEach-Object { $_.ToLowerInvariant() })

$report = [ordered]@{
    mode = $Mode
    repoRoot = $RepoRoot
    solutionPath = $SolutionPath
    exclusionsPath = $config.Path
    successful = New-Object System.Collections.Generic.List[object]
    skipped = New-Object System.Collections.Generic.List[object]
    blocked = New-Object System.Collections.Generic.List[object]
}

foreach ($definition in $definitions) {
    $exclusion = Test-NuGetUpgradePackageExcluded -PackageId $definition.PackageId -Config $config
    if ($exclusion.IsExcluded) {
        $report.skipped.Add([pscustomobject]@{
            packageId = $definition.PackageId
            currentVersion = $definition.CurrentVersion
            file = $definition.RelativePath
            reason = "Excluded by '$($exclusion.Rule)': $($exclusion.Reason)"
        })
        continue
    }

    $currentInfo = ConvertTo-NuGetUpgradeVersionInfo -Version $definition.CurrentVersion
    $availableVersions = @(
        Get-NuGetUpgradeAvailableVersions -PackageId $definition.PackageId |
        ForEach-Object { ConvertTo-NuGetUpgradeVersionInfo -Version $_ } |
        Where-Object {
            ($allowPrerelease -or -not $_.IsPrerelease) -and
            (Compare-NuGetUpgradeVersionInfo -Left $_ -Right $currentInfo) -gt 0
        } |
        Sort-Object -Property Major, Minor, Patch, Revision, @{ Expression = { if ($_.IsPrerelease) { 0 } else { 1 } } }, Suffix -Descending
    )

    if (@($availableVersions).Count -eq 0) {
        $report.skipped.Add([pscustomobject]@{
            packageId = $definition.PackageId
            currentVersion = $definition.CurrentVersion
            file = $definition.RelativePath
            reason = 'No newer allowed versions were found.'
        })
        continue
    }

    $attemptReasons = New-Object System.Collections.Generic.List[string]
    $selectedCandidate = $null

    foreach ($candidate in $availableVersions) {
        $candidateVersion = $candidate.Original
        $candidateGuardrailViolations = Test-NuGetUpgradeCandidateAgainstGuardrails -Definition $definition -CandidateVersion $candidateVersion -FrameworkCoupledFamilies $config.FrameworkCoupledFamilies
        $isOverride = $overrideIds -contains $definition.PackageId.ToLowerInvariant()

        if (@($candidateGuardrailViolations).Count -gt 0 -and -not $isOverride) {
            foreach ($violation in $candidateGuardrailViolations) {
                $attemptReasons.Add(
                    "Candidate $candidateVersion exceeds the allowed major version for $($violation.Project) ($($violation.TargetFramework)): max major $($violation.MaxAllowedMajor)."
                )
            }
            continue
        }

        $originalContent = Get-Content -Path $definition.FilePath -Raw -ErrorAction Stop

        try {
            Set-NuGetUpgradeVersionInFile -FilePath $definition.FilePath -ItemName $definition.ItemName -PackageId $definition.PackageId -CurrentVersion $definition.CurrentVersion -NewVersion $candidateVersion

            $restoreTargets = if (@($definition.ValidationReferences).Count -gt 0) {
                @($definition.ValidationReferences | Select-Object -ExpandProperty ProjectPath -Unique)
            }
            else {
                @($SolutionPath)
            }

            $restoreSucceeded = $true
            $restoreMessages = New-Object System.Collections.Generic.List[string]

            foreach ($restoreTarget in $restoreTargets) {
                $restore = Invoke-NuGetUpgradeRestore -Path $restoreTarget
                if ($restore.ExitCode -ne 0) {
                    $restoreSucceeded = $false
                    $restoreMessages.Add($restore.Output)
                    break
                }
            }

            if (-not $restoreSucceeded) {
                $attemptReasons.Add("Candidate $candidateVersion failed restore: $($restoreMessages -join ' ')")
                continue
            }

            $graphViolations = New-Object System.Collections.Generic.List[object]

            foreach ($validation in @($definition.ValidationReferences)) {
                foreach ($framework in @($validation.TargetFrameworks)) {
                    $resolvedPackages = Get-NuGetUpgradeResolvedPackages -ProjectPath $validation.ProjectPath -TargetFramework $framework
                    foreach ($violation in @(Test-NuGetUpgradeResolvedPackages -ResolvedPackages $resolvedPackages -TargetFramework $framework -FrameworkCoupledFamilies $config.FrameworkCoupledFamilies)) {
                        $graphViolations.Add([pscustomobject]@{
                            Project = $validation.RelativeProjectPath
                            TargetFramework = $framework
                            PackageId = $violation.PackageId
                            Version = $violation.Version
                            Kind = $violation.Kind
                            MaxAllowedMajor = $violation.MaxAllowedMajor
                        })
                    }
                }
            }

            if ($graphViolations.Count -gt 0 -and -not $isOverride) {
                foreach ($violation in $graphViolations) {
                    $attemptReasons.Add(
                        "Candidate $candidateVersion resolves $($violation.PackageId) $($violation.Version) ($($violation.Kind)) for $($violation.Project) [$($violation.TargetFramework)], above max major $($violation.MaxAllowedMajor)."
                    )
                }
                continue
            }

            $selectedCandidate = [pscustomobject]@{
                packageId = $definition.PackageId
                currentVersion = $definition.CurrentVersion
                newVersion = $candidateVersion
                file = $definition.RelativePath
                validationProjects = @($definition.ValidationReferences | ForEach-Object { $_.RelativeProjectPath })
                action = if ($Mode -eq 'Apply') { 'Applied' } else { 'Analyzed' }
            }

            if ($Mode -eq 'Analyze') {
                $encoding = Get-NuGetUpgradeFileEncoding -Path $definition.FilePath
                [System.IO.File]::WriteAllText($definition.FilePath, $originalContent, $encoding)
            }

            break
        }
        finally {
            if ($null -eq $selectedCandidate) {
                $encoding = Get-NuGetUpgradeFileEncoding -Path $definition.FilePath
                [System.IO.File]::WriteAllText($definition.FilePath, $originalContent, $encoding)
            }
        }
    }

    if ($null -ne $selectedCandidate) {
        $report.successful.Add($selectedCandidate)
        continue
    }

    $report.blocked.Add([pscustomobject]@{
        packageId = $definition.PackageId
        currentVersion = $definition.CurrentVersion
        file = $definition.RelativePath
        reasons = @($attemptReasons | Select-Object -Unique)
        options = @(
            "Skip $($definition.PackageId)"
            "Review the package family and target-framework constraints"
            "Rerun with -OverrideBlockedPackageId $($definition.PackageId) after manual review"
        )
    })
}

$reportObject = [pscustomobject]$report

if ([string]::IsNullOrWhiteSpace($ReportPath)) {
    $ReportPath = Join-Path -Path ([System.IO.Path]::GetTempPath()) -ChildPath 'nuget-safe-upgrade-report.json'
}
elseif (-not [System.IO.Path]::IsPathRooted($ReportPath)) {
    $ReportPath = Join-Path -Path $RepoRoot -ChildPath $ReportPath
}

$reportDirectory = Split-Path -Path $ReportPath -Parent
if (-not [string]::IsNullOrWhiteSpace($reportDirectory) -and -not (Test-Path -Path $reportDirectory)) {
    New-Item -Path $reportDirectory -ItemType Directory -Force | Out-Null
}

$reportObject | ConvertTo-Json -Depth 100 | Set-Content -Path $ReportPath -Encoding utf8

Write-Host ("NuGet safe upgrade mode: {0}`nReport written to: {1}`n" -f $Mode, $ReportPath)
Write-Host ("Successful items: {0}" -f $reportObject.successful.Count)
foreach ($item in $reportObject.successful) {
    Write-Host "  - $($item.packageId): $($item.currentVersion) -> $($item.newVersion) [$($item.action)]"
}

Write-Host ""
Write-Host ("Skipped items: {0}" -f $reportObject.skipped.Count)
foreach ($item in $reportObject.skipped) {
    Write-Host "  - $($item.packageId): $($item.reason)"
}

Write-Host ""
Write-Host ("Blocked items: {0}" -f $reportObject.blocked.Count)
foreach ($item in $reportObject.blocked) {
    Write-Host "  - $($item.packageId):"
    foreach ($reason in @($item.reasons)) {
        Write-Host "      * $reason"
    }
    Write-Host "      * Options: $($item.options -join '; ')"
}
