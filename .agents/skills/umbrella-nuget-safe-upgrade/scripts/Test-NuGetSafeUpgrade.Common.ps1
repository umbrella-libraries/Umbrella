[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

. (Join-Path -Path $PSScriptRoot -ChildPath 'NuGetSafeUpgrade.Common.ps1')

function Assert-NuGetUpgradeTest {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

$testRoot = Join-Path -Path ([System.IO.Path]::GetTempPath()) -ChildPath ('umbrella-nuget-safe-upgrade-' + [guid]::NewGuid().ToString('N'))
[System.IO.Directory]::CreateDirectory($testRoot) | Out-Null

try {
    $commentTestRoot = Join-Path -Path $testRoot -ChildPath 'commented-item'
    [System.IO.Directory]::CreateDirectory($commentTestRoot) | Out-Null
    $commentTestPath = Join-Path -Path $commentTestRoot -ChildPath 'Directory.Packages.props'
    $commentTestContent = @'
<Project>
  <ItemGroup>
    <!-- <PackageVersion Include="Example.Package" Version="1.0.0" /> -->
    <PackageVersion Include="Example.Package" Version="1.0.0" />
  </ItemGroup>
</Project>
'@
    [System.IO.File]::WriteAllText($commentTestPath, $commentTestContent, [System.Text.UTF8Encoding]::new($false))

    $commentDefinition = Get-NuGetUpgradeVersionDefinitions -RepoRoot $commentTestRoot -ProjectReferences @() | Select-Object -First 1
    Set-NuGetUpgradeVersionInFile -FilePath $commentDefinition.FilePath -ItemName $commentDefinition.ItemName -PackageId $commentDefinition.PackageId -CurrentVersion $commentDefinition.CurrentVersion -NewVersion '2.0.0' -ItemIndex $commentDefinition.ItemIndex

    $commentTestResult = Get-Content -Path $commentTestPath -Raw
    Assert-NuGetUpgradeTest -Condition ($commentTestResult.Contains('<!-- <PackageVersion Include="Example.Package" Version="1.0.0" /> -->')) -Message 'The commented PackageVersion was modified.'
    Assert-NuGetUpgradeTest -Condition ($commentTestResult.Contains('<PackageVersion Include="Example.Package" Version="2.0.0" />')) -Message 'The live PackageVersion was not modified.'

    $precedenceTestRoot = Join-Path -Path $testRoot -ChildPath 'condition-precedence'
    [System.IO.Directory]::CreateDirectory($precedenceTestRoot) | Out-Null
    $precedenceTestPath = Join-Path -Path $precedenceTestRoot -ChildPath 'Directory.Packages.props'
    $precedenceTestContent = @'
<Project>
  <ItemGroup Condition="'$(TargetFramework)' == 'net8.0' Or '$(TargetFramework)' == 'net9.0'">
    <PackageVersion Include="Example.Package" Version="1.0.0" Condition="'$(MSBuildProjectName)' == 'Included.Project'" />
  </ItemGroup>
</Project>
'@
    [System.IO.File]::WriteAllText($precedenceTestPath, $precedenceTestContent, [System.Text.UTF8Encoding]::new($false))

    $projectReferences = @(
        [pscustomobject]@{
            ProjectPath = Join-Path -Path $precedenceTestRoot -ChildPath 'Included.Project.csproj'
            ProjectName = 'Included.Project'
            RelativeProjectPath = 'Included.Project.csproj'
            PackageId = 'Example.Package'
            HasVersion = $false
            ApplicableTargetFrameworks = @('net8.0', 'net9.0', 'net10.0')
        }
        [pscustomobject]@{
            ProjectPath = Join-Path -Path $precedenceTestRoot -ChildPath 'Excluded.Project.csproj'
            ProjectName = 'Excluded.Project'
            RelativeProjectPath = 'Excluded.Project.csproj'
            PackageId = 'Example.Package'
            HasVersion = $false
            ApplicableTargetFrameworks = @('net8.0', 'net9.0', 'net10.0')
        }
    )

    $precedenceDefinition = Get-NuGetUpgradeVersionDefinitions -RepoRoot $precedenceTestRoot -ProjectReferences $projectReferences | Select-Object -First 1
    $expectedCondition = "('`$(TargetFramework)' == 'net8.0' Or '`$(TargetFramework)' == 'net9.0') AND ('`$(MSBuildProjectName)' == 'Included.Project')"
    Assert-NuGetUpgradeTest -Condition ($precedenceDefinition.Condition -eq $expectedCondition) -Message "Combined condition did not preserve grouping: $($precedenceDefinition.Condition)"
    Assert-NuGetUpgradeTest -Condition ($precedenceDefinition.ValidationReferences.Count -eq 1) -Message 'The grouped condition included an excluded project.'
    Assert-NuGetUpgradeTest -Condition ($precedenceDefinition.ValidationReferences[0].ProjectName -eq 'Included.Project') -Message 'The grouped condition selected the wrong project.'
    Assert-NuGetUpgradeTest -Condition (($precedenceDefinition.ValidationReferences[0].TargetFrameworks -join ',') -eq 'net8.0,net9.0') -Message 'The grouped condition selected the wrong target frameworks.'

    Write-Host 'NuGet safe-upgrade regression tests passed.'
}
finally {
    if (Test-Path -LiteralPath $testRoot) {
        Remove-Item -LiteralPath $testRoot -Recurse -Force
    }
}
