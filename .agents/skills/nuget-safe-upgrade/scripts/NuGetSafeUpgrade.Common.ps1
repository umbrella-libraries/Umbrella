Set-StrictMode -Version Latest

function Resolve-NuGetUpgradeRepoRoot {
    param(
        [string]$StartPath = (Get-Location).Path
    )

    $current = (Resolve-Path -Path $StartPath).Path

    while ($true) {
        if (Test-Path -Path (Join-Path -Path $current -ChildPath ".git")) {
            return $current
        }

        $solution = Get-ChildItem -Path $current -Filter *.sln -File -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($null -ne $solution) {
            return $current
        }

        $parent = Split-Path -Path $current -Parent
        if ([string]::IsNullOrWhiteSpace($parent) -or $parent -eq $current) {
            break
        }

        $current = $parent
    }

    throw "Unable to determine the repository root from '$StartPath'."
}

function Resolve-NuGetUpgradeRelativePath {
    param(
        [string]$RepoRoot,
        [string]$Path
    )

    $rootUri = [System.Uri]((Resolve-Path -Path $RepoRoot).Path + [System.IO.Path]::DirectorySeparatorChar)
    $pathUri = [System.Uri]((Resolve-Path -Path $Path).Path)
    return [System.Uri]::UnescapeDataString($rootUri.MakeRelativeUri($pathUri).ToString()).Replace('/', '\')
}

function Get-NuGetUpgradeSolutionPath {
    param(
        [string]$RepoRoot,
        [string]$SolutionPath
    )

    if (-not [string]::IsNullOrWhiteSpace($SolutionPath)) {
        $candidate = if ([System.IO.Path]::IsPathRooted($SolutionPath)) {
            $SolutionPath
        }
        else {
            Join-Path -Path $RepoRoot -ChildPath $SolutionPath
        }

        if (-not (Test-Path -Path $candidate -PathType Leaf)) {
            throw "Solution '$candidate' does not exist."
        }

        return (Resolve-Path -Path $candidate).Path
    }

    $solution = Get-ChildItem -Path $RepoRoot -Filter *.sln -File -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($null -eq $solution) {
        throw "No solution file was found under '$RepoRoot'."
    }

    return $solution.FullName
}

function Read-NuGetUpgradeJsonFile {
    param(
        [string]$Path
    )

    $raw = Get-Content -Path $Path -Raw -ErrorAction Stop
    if ([string]::IsNullOrWhiteSpace($raw)) {
        throw "JSON file '$Path' is empty."
    }

    return $raw | ConvertFrom-Json
}

function Get-NuGetUpgradeXmlAttributeValue {
    param(
        [System.Xml.XmlNode]$Node,
        [string]$Name
    )

    if ($null -eq $Node -or $null -eq $Node.Attributes) {
        return ''
    }

    $attribute = $Node.Attributes[$Name]
    if ($null -eq $attribute) {
        return ''
    }

    return [string]$attribute.Value
}

function Get-NuGetUpgradeConfig {
    param(
        [string]$RepoRoot,
        [string]$ConfigPath = 'nuget-upgrade-exclusions.json'
    )

    $path = if ([System.IO.Path]::IsPathRooted($ConfigPath)) {
        $ConfigPath
    }
    else {
        Join-Path -Path $RepoRoot -ChildPath $ConfigPath
    }

    if (-not (Test-Path -Path $path -PathType Leaf)) {
        return [pscustomobject]@{
            Path = $path
            Packages = @()
            Patterns = @()
            FrameworkCoupledFamilies = @(
                'Microsoft.Extensions.'
                'Microsoft.AspNetCore.'
                'Microsoft.EntityFrameworkCore'
            )
            Options = [pscustomobject]@{
                AllowPrerelease = $false
            }
        }
    }

    $data = Read-NuGetUpgradeJsonFile -Path $path

    return [pscustomobject]@{
        Path = $path
        Packages = @($data.packages)
        Patterns = @($data.patterns)
        FrameworkCoupledFamilies = @(
            if ($null -ne $data.frameworkCoupledFamilies -and $data.frameworkCoupledFamilies.Count -gt 0) {
                $data.frameworkCoupledFamilies
            }
            else {
                'Microsoft.Extensions.'
                'Microsoft.AspNetCore.'
                'Microsoft.EntityFrameworkCore'
            }
        )
        Options = [pscustomobject]@{
            AllowPrerelease = [bool]($data.options.allowPrerelease)
        }
    }
}

function Test-NuGetUpgradePackageExcluded {
    param(
        [string]$PackageId,
        [pscustomobject]$Config
    )

    foreach ($package in @($Config.Packages)) {
        if ($null -ne $package -and $PackageId.Equals([string]$package.id, [System.StringComparison]::OrdinalIgnoreCase)) {
            return [pscustomobject]@{
                IsExcluded = $true
                Reason = [string]$package.reason
                Rule = [string]$package.id
            }
        }
    }

    foreach ($pattern in @($Config.Patterns)) {
        if ($null -eq $pattern) {
            continue
        }

        $wildcard = New-Object System.Management.Automation.WildcardPattern([string]$pattern.pattern, [System.Management.Automation.WildcardOptions]::IgnoreCase)
        if ($wildcard.IsMatch($PackageId)) {
            return [pscustomobject]@{
                IsExcluded = $true
                Reason = [string]$pattern.reason
                Rule = [string]$pattern.pattern
            }
        }
    }

    return [pscustomobject]@{
        IsExcluded = $false
        Reason = $null
        Rule = $null
    }
}

function Get-NuGetUpgradeProjectTargetFrameworks {
    param(
        [xml]$ProjectXml
    )

    $values = New-Object System.Collections.Generic.List[string]
    $nodes = $ProjectXml.SelectNodes("//*[local-name()='TargetFramework' or local-name()='TargetFrameworks']")

    foreach ($node in $nodes) {
        if ([string]::IsNullOrWhiteSpace($node.InnerText)) {
            continue
        }

        foreach ($framework in ($node.InnerText -split ';')) {
            $trimmed = $framework.Trim()
            if (-not [string]::IsNullOrWhiteSpace($trimmed) -and -not $values.Contains($trimmed)) {
                $values.Add($trimmed)
            }
        }
    }

    return $values.ToArray()
}

function Get-NuGetUpgradeApplicableFrameworks {
    param(
        [string]$Condition,
        [string[]]$ProjectTargetFrameworks
    )

    if ($null -eq $ProjectTargetFrameworks -or $ProjectTargetFrameworks.Count -eq 0) {
        return @()
    }

    if ([string]::IsNullOrWhiteSpace($Condition)) {
        return @($ProjectTargetFrameworks)
    }

    $applicable = New-Object System.Collections.Generic.List[string]

    foreach ($framework in $ProjectTargetFrameworks) {
        $escaped = [regex]::Escape($framework)
        if ($Condition -match "'$escaped'" -or $Condition -match '"' + $escaped + '"') {
            $applicable.Add($framework)
        }
    }

    if ($applicable.Count -gt 0) {
        return $applicable.ToArray()
    }

    return @($ProjectTargetFrameworks)
}

function Get-NuGetUpgradeProjectReferences {
    param(
        [string]$RepoRoot
    )

    $projects = Get-ChildItem -Path $RepoRoot -Recurse -Filter *.csproj -File |
        Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' }

    $results = New-Object System.Collections.Generic.List[object]

    foreach ($project in $projects) {
        [xml]$xml = Get-Content -Path $project.FullName -Raw -ErrorAction Stop
        $projectTargetFrameworks = Get-NuGetUpgradeProjectTargetFrameworks -ProjectXml $xml
        $packageNodes = $xml.SelectNodes("//*[local-name()='PackageReference']")

        foreach ($node in $packageNodes) {
            $include = Get-NuGetUpgradeXmlAttributeValue -Node $node -Name 'Include'
            $update = Get-NuGetUpgradeXmlAttributeValue -Node $node -Name 'Update'
            $version = Get-NuGetUpgradeXmlAttributeValue -Node $node -Name 'Version'
            $packageId = if (-not [string]::IsNullOrWhiteSpace($include)) { $include } else { $update }
            if ([string]::IsNullOrWhiteSpace($packageId)) {
                continue
            }

            $conditionParts = @(@(
                (Get-NuGetUpgradeXmlAttributeValue -Node $node.ParentNode -Name 'Condition')
                (Get-NuGetUpgradeXmlAttributeValue -Node $node -Name 'Condition')
            ) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })

            $combinedCondition = [string]::Join(' AND ', $conditionParts)

            $results.Add([pscustomobject]@{
                ProjectPath = $project.FullName
                ProjectName = [System.IO.Path]::GetFileNameWithoutExtension($project.Name)
                RelativeProjectPath = Resolve-NuGetUpgradeRelativePath -RepoRoot $RepoRoot -Path $project.FullName
                PackageId = $packageId
                HasVersion = -not [string]::IsNullOrWhiteSpace($version)
                CurrentVersion = $version
                Condition = $combinedCondition
                ApplicableTargetFrameworks = @(Get-NuGetUpgradeApplicableFrameworks -Condition $combinedCondition -ProjectTargetFrameworks $projectTargetFrameworks)
                ProjectTargetFrameworks = @($projectTargetFrameworks)
            })
        }
    }

    return $results.ToArray()
}

function Get-NuGetUpgradeVersionDefinitions {
    param(
        [string]$RepoRoot,
        [object[]]$ProjectReferences
    )

    $definitions = New-Object System.Collections.Generic.List[object]

    foreach ($reference in $ProjectReferences | Where-Object { $_.HasVersion }) {
        $definitions.Add([pscustomobject]@{
            FilePath = $reference.ProjectPath
            PackageId = $reference.PackageId
            CurrentVersion = $reference.CurrentVersion
            ItemName = 'PackageReference'
            Condition = $reference.Condition
            RelativePath = $reference.RelativeProjectPath
            ValidationReferences = @(
                [pscustomobject]@{
                    ProjectPath = $reference.ProjectPath
                    ProjectName = $reference.ProjectName
                    RelativeProjectPath = $reference.RelativeProjectPath
                    TargetFrameworks = @($reference.ApplicableTargetFrameworks)
                }
            )
        })
    }

    $propsFiles = Get-ChildItem -Path $RepoRoot -Recurse -Filter Directory.Packages.props -File |
        Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' }

    foreach ($propsFile in $propsFiles) {
        [xml]$propsXml = Get-Content -Path $propsFile.FullName -Raw -ErrorAction Stop
        $packageNodes = $propsXml.SelectNodes("//*[local-name()='PackageVersion']")

        foreach ($node in $packageNodes) {
            $include = Get-NuGetUpgradeXmlAttributeValue -Node $node -Name 'Include'
            $update = Get-NuGetUpgradeXmlAttributeValue -Node $node -Name 'Update'
            $packageId = if (-not [string]::IsNullOrWhiteSpace($include)) { $include } else { $update }
            $currentVersion = Get-NuGetUpgradeXmlAttributeValue -Node $node -Name 'Version'

            if ([string]::IsNullOrWhiteSpace($packageId) -or [string]::IsNullOrWhiteSpace($currentVersion)) {
                continue
            }

            $referencingProjects = $ProjectReferences | Where-Object {
                $_.PackageId.Equals($packageId, [System.StringComparison]::OrdinalIgnoreCase)
            }

            $validationReferences = $referencingProjects |
                Group-Object -Property ProjectPath |
                ForEach-Object {
                    [pscustomobject]@{
                        ProjectPath = $_.Name
                        ProjectName = ($_.Group | Select-Object -First 1).ProjectName
                        RelativeProjectPath = ($_.Group | Select-Object -First 1).RelativeProjectPath
                        TargetFrameworks = @(
                            $_.Group |
                                ForEach-Object { $_.ApplicableTargetFrameworks } |
                                Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
                                Sort-Object -Unique
                        )
                    }
                }

            $definitions.Add([pscustomobject]@{
                FilePath = $propsFile.FullName
                PackageId = $packageId
                CurrentVersion = $currentVersion
                ItemName = 'PackageVersion'
                Condition = Get-NuGetUpgradeXmlAttributeValue -Node $node -Name 'Condition'
                RelativePath = Resolve-NuGetUpgradeRelativePath -RepoRoot $RepoRoot -Path $propsFile.FullName
                ValidationReferences = @($validationReferences)
            })
        }
    }

    return @(
        $definitions |
            Group-Object -Property {
                '{0}|{1}|{2}|{3}|{4}' -f $_.FilePath, $_.PackageId.ToLowerInvariant(), $_.CurrentVersion, $_.ItemName, $_.Condition
            } |
            ForEach-Object { $_.Group | Select-Object -First 1 }
    )
}

function Get-NuGetUpgradeInventory {
    param(
        [string]$RepoRoot
    )

    $projectReferences = Get-NuGetUpgradeProjectReferences -RepoRoot $RepoRoot
    $versionDefinitions = Get-NuGetUpgradeVersionDefinitions -RepoRoot $RepoRoot -ProjectReferences $projectReferences

    return [pscustomobject]@{
        ProjectReferences = @($projectReferences)
        VersionDefinitions = @($versionDefinitions)
    }
}

function ConvertTo-NuGetUpgradeVersionInfo {
    param(
        [string]$Version
    )

    $match = [regex]::Match($Version, '^(?<major>\d+)(?:\.(?<minor>\d+))?(?:\.(?<patch>\d+))?(?:\.(?<revision>\d+))?(?<suffix>-.+)?$')
    if (-not $match.Success) {
        throw "Version '$Version' is not in a supported format."
    }

    return [pscustomobject]@{
        Original = $Version
        Major = [int]$match.Groups['major'].Value
        Minor = if ($match.Groups['minor'].Success) { [int]$match.Groups['minor'].Value } else { 0 }
        Patch = if ($match.Groups['patch'].Success) { [int]$match.Groups['patch'].Value } else { 0 }
        Revision = if ($match.Groups['revision'].Success) { [int]$match.Groups['revision'].Value } else { 0 }
        Suffix = if ($match.Groups['suffix'].Success) { $match.Groups['suffix'].Value } else { '' }
        IsPrerelease = $match.Groups['suffix'].Success
    }
}

function Compare-NuGetUpgradeVersionInfo {
    param(
        [pscustomobject]$Left,
        [pscustomobject]$Right
    )

    foreach ($property in 'Major', 'Minor', 'Patch', 'Revision') {
        if ($Left.$property -gt $Right.$property) {
            return 1
        }

        if ($Left.$property -lt $Right.$property) {
            return -1
        }
    }

    if ($Left.IsPrerelease -and -not $Right.IsPrerelease) {
        return -1
    }

    if (-not $Left.IsPrerelease -and $Right.IsPrerelease) {
        return 1
    }

    if ($Left.Suffix -gt $Right.Suffix) {
        return 1
    }

    if ($Left.Suffix -lt $Right.Suffix) {
        return -1
    }

    return 0
}

function Get-NuGetUpgradeAvailableVersions {
    param(
        [string]$PackageId
    )

    $lowerId = $PackageId.ToLowerInvariant()
    $url = "https://api.nuget.org/v3-flatcontainer/$lowerId/index.json"
    try {
        $response = Invoke-RestMethod -Uri $url -Method Get -ErrorAction Stop
        return @($response.versions)
    }
    catch {
        return @()
    }
}

function Get-NuGetUpgradeFrameworkMajor {
    param(
        [string]$TargetFramework
    )

    $match = [regex]::Match($TargetFramework, '^net(?<major>\d+)(?:\.\d+)?$')
    if (-not $match.Success) {
        return $null
    }

    $major = [int]$match.Groups['major'].Value
    if ($major -lt 5) {
        return $null
    }

    return $major
}

function Get-TfmSortKey {
    param([string]$Tfm)
    if ($Tfm -match '^netstandard(\d+)\.(\d+)') {
        return [pscustomobject]@{ Tier = 0; Major = [int]$Matches[1]; Minor = [int]$Matches[2] }
    }
    if ($Tfm -match '^net4') {
        $ver = $Tfm -replace '^net', ''
        if    ($ver -match '^(\d)\.(\d+)') { $maj = [int]$Matches[1]; $min = [int]$Matches[2] }
        elseif ($ver -match '^(\d)(\d+)')  { $maj = [int]$Matches[1]; $min = [int]$Matches[2] }
        elseif ($ver -match '^(\d+)')       { $maj = [int]$Matches[1]; $min = 0 }
        else                               { $maj = 4; $min = 0 }
        return [pscustomobject]@{ Tier = 1; Major = $maj; Minor = $min }
    }
    if ($Tfm -match '^net(\d+)\.(\d+)') {
        return [pscustomobject]@{ Tier = 2; Major = [int]$Matches[1]; Minor = [int]$Matches[2] }
    }
    return [pscustomobject]@{ Tier = 3; Major = 0; Minor = 0 }
}

function Sort-NuGetUpgradeTargetFrameworks {
    param([string[]]$Frameworks)
    $Frameworks | Sort-Object `
        @{ Expression = { (Get-TfmSortKey -Tfm $_).Tier  } }, `
        @{ Expression = { (Get-TfmSortKey -Tfm $_).Major } }, `
        @{ Expression = { (Get-TfmSortKey -Tfm $_).Minor } }
}

function Get-NuGetUpgradeGuardrail {
    param(
        [string]$PackageId,
        [string]$TargetFramework,
        [string]$CurrentVersion,
        [string[]]$FrameworkCoupledFamilies
    )

    $family = $FrameworkCoupledFamilies | Where-Object {
        $PackageId.StartsWith($_, [System.StringComparison]::OrdinalIgnoreCase)
    } | Select-Object -First 1

    if ($null -eq $family) {
        return $null
    }

    $tfmMajor = Get-NuGetUpgradeFrameworkMajor -TargetFramework $TargetFramework
    if ($null -ne $tfmMajor) {
        return $tfmMajor
    }

    return (ConvertTo-NuGetUpgradeVersionInfo -Version $CurrentVersion).Major
}

function Test-NuGetUpgradeCandidateAgainstGuardrails {
    param(
        [pscustomobject]$Definition,
        [string]$CandidateVersion,
        [string[]]$FrameworkCoupledFamilies
    )

    $candidateInfo = ConvertTo-NuGetUpgradeVersionInfo -Version $CandidateVersion
    $violations = New-Object System.Collections.Generic.List[object]

    foreach ($validation in @($Definition.ValidationReferences)) {
        foreach ($framework in @($validation.TargetFrameworks)) {
            $maxMajor = Get-NuGetUpgradeGuardrail -PackageId $Definition.PackageId -TargetFramework $framework -CurrentVersion $Definition.CurrentVersion -FrameworkCoupledFamilies $FrameworkCoupledFamilies
            if ($null -ne $maxMajor -and $candidateInfo.Major -gt $maxMajor) {
                $violations.Add([pscustomobject]@{
                    Project = $validation.RelativeProjectPath
                    TargetFramework = $framework
                    MaxAllowedMajor = $maxMajor
                    CandidateVersion = $CandidateVersion
                })
            }
        }
    }

    return $violations.ToArray()
}

function Get-NuGetUpgradeFileEncoding {
    param(
        [string]$Path
    )

    $bytes = [System.IO.File]::ReadAllBytes($Path)
    if ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) {
        return [System.Text.UTF8Encoding]::new($true)
    }

    return [System.Text.UTF8Encoding]::new($false)
}

function Set-NuGetUpgradeVersionInFile {
    param(
        [string]$FilePath,
        [string]$ItemName,
        [string]$PackageId,
        [string]$CurrentVersion,
        [string]$NewVersion
    )

    $content = Get-Content -Path $FilePath -Raw -ErrorAction Stop
    $packagePattern = [regex]::Escape($PackageId)
    $versionAttribute = 'Version="' + [regex]::Escape($CurrentVersion) + '"'
    $pattern = "<$ItemName\b(?=[^>]*\b(?:Include|Update)\s*=\s*`"$packagePattern`")[^>]*\b$versionAttribute"
    $regex = [regex]::new($pattern, [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
    $match = $regex.Match($content)

    if (-not $match.Success) {
        throw "Unable to find $ItemName '$PackageId' with version '$CurrentVersion' in '$FilePath'."
    }

    $replacement = $match.Value -replace ([regex]::Escape('Version="' + $CurrentVersion + '"')), ('Version="' + $NewVersion + '"')
    $updatedContent = $content.Remove($match.Index, $match.Length).Insert($match.Index, $replacement)
    $encoding = Get-NuGetUpgradeFileEncoding -Path $FilePath
    [System.IO.File]::WriteAllText($FilePath, $updatedContent, $encoding)
}

function Invoke-NuGetUpgradeRestore {
    param(
        [string]$Path
    )

    $output = & dotnet restore $Path --verbosity minimal 2>&1
    return [pscustomobject]@{
        ExitCode = $LASTEXITCODE
        Output = ($output -join [Environment]::NewLine)
    }
}

function Get-NuGetUpgradeResolvedPackages {
    param(
        [string]$ProjectPath,
        [string]$TargetFramework
    )

    $output = & dotnet list $ProjectPath package --include-transitive --framework $TargetFramework --format json --no-restore 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to inspect resolved packages for '$ProjectPath' ($TargetFramework): $($output -join [Environment]::NewLine)"
    }

    $json = ($output -join [Environment]::NewLine) | ConvertFrom-Json
    $packages = New-Object System.Collections.Generic.List[object]

    foreach ($project in @($json.projects)) {
        foreach ($framework in @($project.frameworks)) {
            if ($framework.framework -ne $TargetFramework) {
                continue
            }

            foreach ($package in @($framework.topLevelPackages)) {
                $packages.Add([pscustomobject]@{
                    PackageId = [string]$package.id
                    Version = [string]$(if ($package.resolvedVersion) { $package.resolvedVersion } else { $package.requestedVersion })
                    Kind = 'TopLevel'
                })
            }

            foreach ($package in @($framework.transitivePackages)) {
                $packages.Add([pscustomobject]@{
                    PackageId = [string]$package.id
                    Version = [string]$package.resolvedVersion
                    Kind = 'Transitive'
                })
            }
        }
    }

    return $packages.ToArray()
}

function Test-NuGetUpgradeResolvedPackages {
    param(
        [object[]]$ResolvedPackages,
        [string]$TargetFramework,
        [string[]]$FrameworkCoupledFamilies
    )

    $violations = New-Object System.Collections.Generic.List[object]

    foreach ($package in $ResolvedPackages) {
        $family = $FrameworkCoupledFamilies | Where-Object {
            $package.PackageId.StartsWith($_, [System.StringComparison]::OrdinalIgnoreCase)
        } | Select-Object -First 1

        if ($null -eq $family) {
            continue
        }

        $maxMajor = Get-NuGetUpgradeFrameworkMajor -TargetFramework $TargetFramework
        if ($null -eq $maxMajor) {
            continue
        }

        $resolvedVersion = ConvertTo-NuGetUpgradeVersionInfo -Version $package.Version
        if ($resolvedVersion.Major -gt $maxMajor) {
            $violations.Add([pscustomobject]@{
                PackageId = $package.PackageId
                Version = $package.Version
                Kind = $package.Kind
                TargetFramework = $TargetFramework
                MaxAllowedMajor = $maxMajor
            })
        }
    }

    return $violations.ToArray()
}

function Build-NuGetUpgradeFrameworkCondition {
    param([string[]]$Frameworks)
    $parts = $Frameworks | ForEach-Object { "'`$(TargetFramework)' == '$_'" }
    return $parts -join ' Or '
}

function Remove-NuGetUpgradePackageReferenceLine {
    param(
        [string]$FilePath,
        [string]$ItemName,
        [string]$PackageId,
        [string]$Version
    )

    $content = Get-Content -Path $FilePath -Raw -ErrorAction Stop
    $packageEscaped = [regex]::Escape($PackageId)
    $versionEscaped = [regex]::Escape($Version)
    $pattern = '[^\S\r\n]*<' + [regex]::Escape($ItemName) + '\b(?=[^>]*\b(?:Include|Update)\s*=\s*"' + $packageEscaped + '")[^>]*\bVersion="' + $versionEscaped + '"[^>]*/>\r?\n?'
    $regex = [regex]::new($pattern, [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)

    if (-not $regex.IsMatch($content)) {
        throw "Unable to find $ItemName '$PackageId' Version '$Version' in '$FilePath'."
    }

    $updatedContent = $regex.Replace($content, '', 1)
    $encoding = Get-NuGetUpgradeFileEncoding -Path $FilePath
    [System.IO.File]::WriteAllText($FilePath, $updatedContent, $encoding)
}

function Add-NuGetUpgradePackageReferenceBlock {
    param(
        [string]$FilePath,
        [string]$ItemName,
        [string]$PackageId,
        [string]$Version,
        [string]$Condition
    )

    $content = Get-Content -Path $FilePath -Raw -ErrorAction Stop
    $newline = if ($content -match '\r\n') { "`r`n" } else { "`n" }
    $conditionAttr = if (-not [string]::IsNullOrWhiteSpace($Condition)) { " Condition=`"$Condition`"" } else { '' }
    $block = "`t<ItemGroup${conditionAttr}>${newline}`t`t<${ItemName} Include=`"${PackageId}`" Version=`"${Version}`" />${newline}`t</ItemGroup>${newline}${newline}"

    $closeTagPattern = [regex]::new('[^\S\r\n]*</Project>', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
    $match = $closeTagPattern.Match($content)
    if (-not $match.Success) {
        throw "Unable to find </Project> closing tag in '$FilePath'."
    }

    $updatedContent = $content.Insert($match.Index, $block)
    $encoding = Get-NuGetUpgradeFileEncoding -Path $FilePath
    [System.IO.File]::WriteAllText($FilePath, $updatedContent, $encoding)
}

function Split-NuGetUpgradePackageReference {
    param(
        [string]$FilePath,
        [string]$ItemName,
        [string]$PackageId,
        [string]$CurrentVersion,
        [string]$NewVersion,
        [string[]]$UpgradeFrameworks,
        [string[]]$KeepFrameworks
    )

    Remove-NuGetUpgradePackageReferenceLine -FilePath $FilePath -ItemName $ItemName -PackageId $PackageId -Version $CurrentVersion

    $sortedKeep    = @(Sort-NuGetUpgradeTargetFrameworks -Frameworks $KeepFrameworks)
    $sortedUpgrade = @(Sort-NuGetUpgradeTargetFrameworks -Frameworks $UpgradeFrameworks)

    $keepCondition    = Build-NuGetUpgradeFrameworkCondition -Frameworks $sortedKeep
    $upgradeCondition = Build-NuGetUpgradeFrameworkCondition -Frameworks $sortedUpgrade

    # Write the block whose lowest TFM sorts first (semantic order: netstandard* < net4* < net5+).
    $keepKey    = Get-TfmSortKey -Tfm $sortedKeep[0]
    $upgradeKey = Get-TfmSortKey -Tfm $sortedUpgrade[0]
    $writeKeepFirst = ($keepKey.Tier -lt $upgradeKey.Tier) -or
                      ($keepKey.Tier -eq $upgradeKey.Tier -and $keepKey.Major -lt $upgradeKey.Major) -or
                      ($keepKey.Tier -eq $upgradeKey.Tier -and $keepKey.Major -eq $upgradeKey.Major -and $keepKey.Minor -le $upgradeKey.Minor)

    if ($writeKeepFirst) {
        Add-NuGetUpgradePackageReferenceBlock -FilePath $FilePath -ItemName $ItemName -PackageId $PackageId -Version $CurrentVersion -Condition $keepCondition
        Add-NuGetUpgradePackageReferenceBlock -FilePath $FilePath -ItemName $ItemName -PackageId $PackageId -Version $NewVersion -Condition $upgradeCondition
    }
    else {
        Add-NuGetUpgradePackageReferenceBlock -FilePath $FilePath -ItemName $ItemName -PackageId $PackageId -Version $NewVersion -Condition $upgradeCondition
        Add-NuGetUpgradePackageReferenceBlock -FilePath $FilePath -ItemName $ItemName -PackageId $PackageId -Version $CurrentVersion -Condition $keepCondition
    }
}
