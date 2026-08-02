[CmdletBinding()]
param(
    [ValidateSet('Analyze', 'Apply')]
    [string]$Mode = 'Analyze',

    [string]$RepoRoot,
    [string]$ReportPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$TestCondition = "'`$(IsTestProject)'=='true'"
$RequiredGlobalTestRunner = 'Microsoft.Testing.Platform'
$RequiredCentralPackages = [ordered]@{
    'Microsoft.Testing.Extensions.CodeCoverage' = '18.9.0'
    'Microsoft.Testing.Extensions.TrxReport' = '2.3.2'
    'Moq' = '4.20.72'
    'xunit.v3.mtp-v2' = '3.2.2'
}

$CentralTestProperties = [ordered]@{
    'OutputType' = 'Exe'
    'IsPackable' = 'false'
    'NoWarn' = '$(NoWarn);CS1591'
    'WarningsAsErrors' = '$(WarningsAsErrors);xUnit1051'
    'PreserveCompilationContext' = 'true'
    'UseMicrosoftTestingPlatformRunner' = 'true'
}

$SharedRunnablePackageIds = @(
    'microsoft.net.test.sdk',
    'xunit.runner.visualstudio',
    'xunit.v3',
    'xunit.v3.mtp-v2',
    'microsoft.testing.extensions.codecoverage',
    'microsoft.testing.extensions.trxreport',
    'moq'
)

$RunnerPropertyNames = @(
    'OutputType',
    'IsPackable',
    'UseMicrosoftTestingPlatformRunner',
    'PreserveCompilationContext',
    'TestingPlatformDotnetTestSupport'
)

function Resolve-TestConfigRepoRoot {
    $gitRoot = git rev-parse --show-toplevel 2>$null
    if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($gitRoot)) {
        return (Resolve-Path -Path $gitRoot.Trim()).Path
    }

    return $PWD.Path
}

function Get-RelativePath {
    param([string]$BasePath, [string]$Path)

    $resolvedBase = (Resolve-Path -Path $BasePath).Path
    $resolvedPath = if (Test-Path -Path $Path) { (Resolve-Path -Path $Path).Path } else { $Path }

    if (-not $resolvedBase.EndsWith([System.IO.Path]::DirectorySeparatorChar)) {
        $resolvedBase += [System.IO.Path]::DirectorySeparatorChar
    }

    $baseUri = [System.Uri]::new($resolvedBase)
    $pathUri = [System.Uri]::new($resolvedPath)
    return [System.Uri]::UnescapeDataString($baseUri.MakeRelativeUri($pathUri).ToString()) -replace '/', '/'
}

function Get-FileTextOrProject {
    param([string]$Path)

    if (Test-Path -Path $Path) {
        return Get-Content -Path $Path -Raw
    }

    return "<Project>`r`n</Project>`r`n"
}

function Get-FileTextOrDefault {
    param(
        [string]$Path,
        [string]$DefaultContent
    )

    if (Test-Path -Path $Path) {
        return Get-Content -Path $Path -Raw
    }

    return $DefaultContent
}

function Add-Drift {
    param(
        [System.Collections.Generic.List[object]]$Drift,
        [string]$File,
        [string]$Kind,
        [string]$Message
    )

    $Drift.Add([pscustomobject]@{
        file = $File
        kind = $Kind
        message = $Message
    })
}

function Write-IfChanged {
    param(
        [string]$Path,
        [string]$OldContent,
        [string]$NewContent,
        [string]$Mode,
        [System.Collections.Generic.List[string]]$ChangedFiles,
        [string]$RepoRoot
    )

    if ($OldContent -eq $NewContent) {
        return
    }

    if ($Mode -eq 'Apply') {
        $directory = Split-Path -Path $Path -Parent
        if (-not (Test-Path -Path $directory)) {
            New-Item -Path $directory -ItemType Directory -Force | Out-Null
        }

        [System.IO.File]::WriteAllText($Path, $NewContent, [System.Text.UTF8Encoding]::new($false))
    }

    $ChangedFiles.Add((Get-RelativePath -BasePath $RepoRoot -Path $Path))
}

function Add-OrReplace-TestConditionBlock {
    param(
        [string]$Content,
        [string]$ElementName,
        [string]$Block
    )

    $conditionPattern = "Condition\s*=\s*[`"']" + [regex]::Escape($TestCondition) + "[`"']"
    $blockPattern = "(?ms)\r?\n?\s*<$ElementName\s+$conditionPattern\s*>.*?</$ElementName>"

    $updated = [regex]::Replace($Content, $blockPattern, '', [System.Text.RegularExpressions.RegexOptions]::None)

    if ($updated -notmatch '(?is)</Project>\s*$') {
        $updated = "<Project>`r`n</Project>`r`n"
    }

    return [regex]::Replace($updated, '(?is)\s*</Project>\s*$', "`r`n$Block`r`n`r`n</Project>`r`n")
}

function Get-TestConditionBlocks {
    param(
        [string]$Content,
        [string]$ElementName
    )

    $conditionPattern = "Condition\s*=\s*[`"']" + [regex]::Escape($TestCondition) + "[`"']"
    $blockPattern = "(?ms)<$ElementName\s+$conditionPattern\s*>.*?</$ElementName>"
    return @([regex]::Matches($Content, $blockPattern) | ForEach-Object { $_.Value })
}

function Get-CentralSemanticState {
    param(
        [string]$Content,
        [string]$Kind
    )

    if ($Kind -eq 'props') {
        $blocks = Get-TestConditionBlocks -Content $Content -ElementName 'ItemGroup'
        $hasXunitUsing = $false
        foreach ($block in $blocks) {
            if ($block -match '<Using\s+Include\s*=\s*"Xunit"\s*/>') {
                $hasXunitUsing = $true
            }
        }

        return ([pscustomobject]@{ hasXunitUsing = $hasXunitUsing } | ConvertTo-Json -Compress)
    }

    if ($Kind -eq 'targets') {
        $blocks = Get-TestConditionBlocks -Content $Content -ElementName 'PropertyGroup'
        $values = [ordered]@{}
        foreach ($key in $CentralTestProperties.Keys) {
            $values[$key] = $null
        }

        foreach ($block in $blocks) {
            foreach ($key in $CentralTestProperties.Keys) {
                $match = [regex]::Match($block, "(?is)<$key>(.*?)</$key>")
                if ($match.Success) {
                    $values[$key] = $match.Groups[1].Value
                }
            }
        }

        return ([pscustomobject]$values | ConvertTo-Json -Compress)
    }

    if ($Kind -eq 'packages') {
        $blocks = Get-TestConditionBlocks -Content $Content -ElementName 'ItemGroup'
        $values = [ordered]@{}
        foreach ($key in $RequiredCentralPackages.Keys) {
            $values[$key] = $null
        }

        foreach ($block in $blocks) {
            foreach ($key in $RequiredCentralPackages.Keys) {
                $match = [regex]::Match($block, "<PackageReference\s+[^>]*Include\s*=\s*`"$([regex]::Escape($key))`"[^>]*Version\s*=\s*`"([^`"]+)`"[^>]*/>")
                if ($match.Success) {
                    $values[$key] = $match.Groups[1].Value
                }
            }
        }

        return ([pscustomobject]$values | ConvertTo-Json -Compress)
    }

    if ($Kind -eq 'global-json') {
        try {
            $json = $Content | ConvertFrom-Json -ErrorAction Stop
            $runner = $null
            $testProperty = $json.PSObject.Properties['test']

            if ($null -ne $testProperty -and $null -ne $testProperty.Value) {
                $runnerProperty = $testProperty.Value.PSObject.Properties['runner']
                if ($null -ne $runnerProperty) {
                    $runner = [string]$runnerProperty.Value
                }
            }

            return ([pscustomobject]@{ testRunner = $runner } | ConvertTo-Json -Compress)
        }
        catch {
            return ([pscustomobject]@{ testRunner = $null; parseError = $true } | ConvertTo-Json -Compress)
        }
    }

    throw "Unknown central file kind '$Kind'."
}

function Set-GlobalJsonContent {
    param([string]$Content)

    if ([string]::IsNullOrWhiteSpace($Content)) {
        $Content = '{}'
    }

    $json = $Content | ConvertFrom-Json -ErrorAction Stop
    if ($null -eq $json) {
        $json = [pscustomobject]@{}
    }

    $testProperty = $json.PSObject.Properties['test']

    if ($null -eq $testProperty -or $null -eq $testProperty.Value -or $testProperty.Value -isnot [pscustomobject]) {
        $json | Add-Member -MemberType NoteProperty -Name 'test' -Value ([pscustomobject]@{}) -Force
        $testProperty = $json.PSObject.Properties['test']
    }

    $testProperty.Value | Add-Member -MemberType NoteProperty -Name 'runner' -Value $RequiredGlobalTestRunner -Force

    return (ConvertTo-PrettyJson -Value $json) + "`r`n"
}

function ConvertTo-PrettyJson {
    param(
        [AllowNull()]
        [object]$Value,
        [int]$Depth = 0
    )

    if ($null -eq $Value) {
        return 'null'
    }

    if ($Value -is [string] -or $Value -is [bool] -or $Value -is [char] -or $Value.GetType().IsPrimitive -or $Value -is [decimal]) {
        return ($Value | ConvertTo-Json -Compress)
    }

    $indent = ' ' * ($Depth * 2)
    $childIndent = ' ' * (($Depth + 1) * 2)

    if ($Value -is [System.Collections.IDictionary]) {
        $entries = @($Value.Keys | ForEach-Object {
            "$childIndent$($_ | ConvertTo-Json -Compress): $(ConvertTo-PrettyJson -Value $Value[$_] -Depth ($Depth + 1))"
        })

        if ($entries.Count -eq 0) {
            return '{}'
        }

        return "{`r`n$($entries -join ",`r`n")`r`n$indent}"
    }

    if ($Value -is [System.Collections.IEnumerable] -and $Value -isnot [string]) {
        $items = @($Value | ForEach-Object { "$childIndent$(ConvertTo-PrettyJson -Value $_ -Depth ($Depth + 1))" })

        if ($items.Count -eq 0) {
            return '[]'
        }

        return "[`r`n$($items -join ",`r`n")`r`n$indent]"
    }

    $properties = @($Value.PSObject.Properties | Where-Object { $_.MemberType -eq [System.Management.Automation.PSMemberTypes]::NoteProperty -or $_.MemberType -eq [System.Management.Automation.PSMemberTypes]::Property })
    $entries = @($properties | ForEach-Object {
        "$childIndent$($_.Name | ConvertTo-Json -Compress): $(ConvertTo-PrettyJson -Value $_.Value -Depth ($Depth + 1))"
    })

    if ($entries.Count -eq 0) {
        return '{}'
    }

    return "{`r`n$($entries -join ",`r`n")`r`n$indent}"
}

function Set-DirectoryBuildPropsContent {
    param([string]$Content)

    if ($Content -notmatch '(?is)</Project>\s*$') {
        $Content = "<Project>`r`n</Project>`r`n"
    }

    $conditionPattern = "Condition\s*=\s*[`"']" + [regex]::Escape($TestCondition) + "[`"']"
    $itemGroupPattern = "(?ms)<ItemGroup\s+$conditionPattern\s*>.*?</ItemGroup>"
    $usingLine = "`t`t<Using Include=`"Xunit`" />"

    $match = [regex]::Match($Content, $itemGroupPattern)
    if ($match.Success) {
        if ($match.Value -match '<Using\s+Include\s*=\s*"Xunit"\s*/>') {
            return $Content
        }

        $newGroup = [regex]::Replace($match.Value, '(?is)\s*</ItemGroup>', "`r`n$usingLine`r`n`t</ItemGroup>")
        return $Content.Remove($match.Index, $match.Length).Insert($match.Index, $newGroup)
    }

    $block = @"
	<ItemGroup Condition="'`$(IsTestProject)'=='true'">
		<Using Include="Xunit" />
	</ItemGroup>
"@

    return [regex]::Replace($Content, '(?is)\s*</Project>\s*$', "`r`n$block`r`n`r`n</Project>`r`n")
}

function Merge-CentralDelimitedPropertyValue {
    param(
        [string]$Content,
        [string]$PropertyName,
        [string]$RequiredValue
    )

    $values = [System.Collections.Generic.List[string]]::new()
    $seen = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)

    foreach ($value in @($RequiredValue -split ';' | ForEach-Object { $_.Trim() } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })) {
        if ($seen.Add($value)) {
            $values.Add($value)
        }
    }

    foreach ($block in @(Get-TestConditionBlocks -Content $Content -ElementName 'PropertyGroup')) {
        $match = [regex]::Match($block, "(?is)<$PropertyName>(.*?)</$PropertyName>")
        if (-not $match.Success) {
            continue
        }

        foreach ($value in @($match.Groups[1].Value -split ';' | ForEach-Object { $_.Trim() } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })) {
            if ($seen.Add($value)) {
                $values.Add($value)
            }
        }
    }

    return $values -join ';'
}

function Set-DirectoryBuildTargetsContent {
    param([string]$Content)

    $lines = foreach ($entry in $CentralTestProperties.GetEnumerator()) {
        $value = if ($entry.Key -in @('NoWarn', 'WarningsAsErrors')) {
            Merge-CentralDelimitedPropertyValue -Content $Content -PropertyName $entry.Key -RequiredValue $entry.Value
        }
        else {
            $entry.Value
        }

        "`t`t<$($entry.Key)>$value</$($entry.Key)>"
    }

    $block = @"
	<PropertyGroup Condition="'`$(IsTestProject)'=='true'">
$($lines -join "`r`n")
	</PropertyGroup>
"@

    return Add-OrReplace-TestConditionBlock -Content $Content -ElementName 'PropertyGroup' -Block $block
}

function Get-PackageVersionParts {
    param([string]$Version)

    if ([string]::IsNullOrWhiteSpace($Version)) {
        return $null
    }

    $match = [regex]::Match($Version.Trim(), '^(?<core>\d+(?:\.\d+){1,3})(?:-(?<prerelease>[0-9A-Za-z.-]+))?(?:\+[0-9A-Za-z.-]+)?$')
    if (-not $match.Success) {
        return $null
    }

    $numericParts = @($match.Groups['core'].Value.Split('.') | ForEach-Object { [int]$_ })
    while ($numericParts.Count -lt 4) {
        $numericParts += 0
    }

    return [pscustomobject]@{
        NumericParts = $numericParts
        Prerelease = $match.Groups['prerelease'].Value
    }
}

function Test-PackageVersionAtLeast {
    param(
        [string]$CandidateVersion,
        [string]$BaselineVersion
    )

    $candidate = Get-PackageVersionParts -Version $CandidateVersion
    $baseline = Get-PackageVersionParts -Version $BaselineVersion

    if ($null -eq $candidate -or $null -eq $baseline) {
        return $true
    }

    for ($index = 0; $index -lt 4; $index++) {
        if ($candidate.NumericParts[$index] -gt $baseline.NumericParts[$index]) {
            return $true
        }

        if ($candidate.NumericParts[$index] -lt $baseline.NumericParts[$index]) {
            return $false
        }
    }

    $candidateIsPrerelease = -not [string]::IsNullOrWhiteSpace($candidate.Prerelease)
    $baselineIsPrerelease = -not [string]::IsNullOrWhiteSpace($baseline.Prerelease)

    if ($candidateIsPrerelease -ne $baselineIsPrerelease) {
        return -not $candidateIsPrerelease
    }

    if (-not $candidateIsPrerelease) {
        return $true
    }

    return [string]::Compare($candidate.Prerelease, $baseline.Prerelease, [System.StringComparison]::OrdinalIgnoreCase) -ge 0
}

function Get-CentralPackageVersion {
    param(
        [string]$Content,
        [string]$PackageId
    )

    foreach ($block in @(Get-TestConditionBlocks -Content $Content -ElementName 'ItemGroup')) {
        $match = [regex]::Match($block, "<PackageReference\s+[^>]*Include\s*=\s*`"$([regex]::Escape($PackageId))`"[^>]*Version\s*=\s*`"([^`"]+)`"[^>]*/>")
        if ($match.Success) {
            return $match.Groups[1].Value
        }
    }

    return $null
}

function Set-DirectoryPackagesPropsContent {
    param([string]$Content)

    $lines = foreach ($entry in $RequiredCentralPackages.GetEnumerator()) {
        $existingVersion = Get-CentralPackageVersion -Content $Content -PackageId $entry.Key
        $selectedVersion = if (-not [string]::IsNullOrWhiteSpace($existingVersion) -and
            (Test-PackageVersionAtLeast -CandidateVersion $existingVersion -BaselineVersion $entry.Value)) {
            $existingVersion
        }
        else {
            $entry.Value
        }

        "    <PackageReference Include=`"$($entry.Key)`" Version=`"$selectedVersion`" />"
    }

    $block = @"
  <ItemGroup Condition="'`$(IsTestProject)'=='true'">
$($lines -join "`r`n")
  </ItemGroup>
"@

    return Add-OrReplace-TestConditionBlock -Content $Content -ElementName 'ItemGroup' -Block $block
}

function Get-XmlDocument {
    param([string]$Path)

    $xml = [System.Xml.XmlDocument]::new()
    # Project files are developer-authored. Preserve their existing layout so Apply
    # changes only the settings and references that this workflow owns.
    $xml.PreserveWhitespace = $true
    $xml.Load($Path)
    return $xml
}

function Get-FirstUnconditionedPropertyGroup {
    param([System.Xml.XmlDocument]$Xml)

    foreach ($node in @($Xml.Project.ChildNodes)) {
        if ($node.NodeType -eq [System.Xml.XmlNodeType]::Element -and
            $node.Name -eq 'PropertyGroup' -and
            [string]::IsNullOrWhiteSpace($node.GetAttribute('Condition'))) {
            return $node
        }
    }

    $propertyGroup = $Xml.CreateElement('PropertyGroup')
    $firstElement = @($Xml.Project.ChildNodes | Where-Object { $_.NodeType -eq [System.Xml.XmlNodeType]::Element } | Select-Object -First 1)
    if ($firstElement.Count -gt 0) {
        $leadingWhitespace = $firstElement[0].PreviousSibling
        $groupIndentation = if ($null -ne $leadingWhitespace -and
            $leadingWhitespace.NodeType -in @([System.Xml.XmlNodeType]::Whitespace, [System.Xml.XmlNodeType]::SignificantWhitespace)) {
            $leadingWhitespace.Value
        }
        else {
            "`r`n  "
        }

        [void]$Xml.Project.InsertBefore($propertyGroup, $firstElement[0])
        if ($null -eq $leadingWhitespace -or
            $leadingWhitespace.NodeType -notin @([System.Xml.XmlNodeType]::Whitespace, [System.Xml.XmlNodeType]::SignificantWhitespace)) {
            [void]$Xml.Project.InsertBefore($Xml.CreateWhitespace($groupIndentation), $propertyGroup)
        }
        [void]$Xml.Project.InsertBefore($Xml.CreateWhitespace($groupIndentation), $firstElement[0])
        [void]$propertyGroup.AppendChild($Xml.CreateWhitespace($groupIndentation))
    }
    else {
        $trailingWhitespace = $Xml.Project.LastChild
        $newLine = if ($null -ne $trailingWhitespace -and $trailingWhitespace.Value.Contains("`r`n")) { "`r`n" } else { "`n" }
        $groupIndentation = "$newLine  "

        if ($null -ne $trailingWhitespace -and
            $trailingWhitespace.NodeType -in @([System.Xml.XmlNodeType]::Whitespace, [System.Xml.XmlNodeType]::SignificantWhitespace)) {
            [void]$Xml.Project.InsertBefore($Xml.CreateWhitespace($groupIndentation), $trailingWhitespace)
            [void]$Xml.Project.InsertBefore($propertyGroup, $trailingWhitespace)
        }
        else {
            [void]$Xml.Project.AppendChild($Xml.CreateWhitespace($groupIndentation))
            [void]$Xml.Project.AppendChild($propertyGroup)
            [void]$Xml.Project.AppendChild($Xml.CreateWhitespace($newLine))
        }

        [void]$propertyGroup.AppendChild($Xml.CreateWhitespace($groupIndentation))
    }

    return $propertyGroup
}

function Set-PropertyValue {
    param(
        [System.Xml.XmlDocument]$Xml,
        [string]$Name,
        [string]$Value
    )

    $propertyGroup = Get-FirstUnconditionedPropertyGroup -Xml $Xml
    $node = $null
    foreach ($child in @($propertyGroup.ChildNodes)) {
        if ($child.NodeType -eq [System.Xml.XmlNodeType]::Element -and $child.Name -eq $Name) {
            $node = $child
            break
        }
    }

    if ($null -eq $node) {
        $node = $Xml.CreateElement($Name)
        $trailingWhitespace = $propertyGroup.LastChild

        if ($null -ne $trailingWhitespace -and
            $trailingWhitespace.NodeType -in @([System.Xml.XmlNodeType]::Whitespace, [System.Xml.XmlNodeType]::SignificantWhitespace)) {
            $elementIndentation = $null
            $previousElement = @($propertyGroup.ChildNodes |
                Where-Object { $_.NodeType -eq [System.Xml.XmlNodeType]::Element } |
                Select-Object -Last 1)

            if ($previousElement.Count -gt 0 -and
                $null -ne $previousElement[0].PreviousSibling -and
                $previousElement[0].PreviousSibling.NodeType -in @([System.Xml.XmlNodeType]::Whitespace, [System.Xml.XmlNodeType]::SignificantWhitespace)) {
                $elementIndentation = $previousElement[0].PreviousSibling.Value
            }

            if ([string]::IsNullOrEmpty($elementIndentation)) {
                $nextElement = $propertyGroup.NextSibling
                while ($null -ne $nextElement -and $nextElement.NodeType -ne [System.Xml.XmlNodeType]::Element) {
                    $nextElement = $nextElement.NextSibling
                }

                if ($null -ne $nextElement) {
                    $firstNestedElement = @($nextElement.ChildNodes |
                        Where-Object { $_.NodeType -eq [System.Xml.XmlNodeType]::Element } |
                        Select-Object -First 1)

                    if ($firstNestedElement.Count -gt 0 -and
                        $null -ne $firstNestedElement[0].PreviousSibling -and
                        $firstNestedElement[0].PreviousSibling.NodeType -in @([System.Xml.XmlNodeType]::Whitespace, [System.Xml.XmlNodeType]::SignificantWhitespace)) {
                        $elementIndentation = $firstNestedElement[0].PreviousSibling.Value
                    }
                }
            }

            if ([string]::IsNullOrEmpty($elementIndentation)) {
                $closingIndentation = $trailingWhitespace.Value
                $newLine = if ($closingIndentation.Contains("`r`n")) { "`r`n" } else { "`n" }
                $lastNewLineIndex = $closingIndentation.LastIndexOf($newLine)
                $closingSpaces = if ($lastNewLineIndex -ge 0) { $closingIndentation.Substring($lastNewLineIndex + $newLine.Length) } else { $closingIndentation }
                $elementIndentation = "$newLine$closingSpaces  "
            }

            [void]$propertyGroup.InsertBefore($Xml.CreateWhitespace($elementIndentation), $trailingWhitespace)
            [void]$propertyGroup.InsertBefore($node, $trailingWhitespace)
        }
        else {
            [void]$propertyGroup.AppendChild($node)
        }
    }

    $node.InnerText = $Value
}

function Remove-XmlNodePreservingLayout {
    param([System.Xml.XmlNode]$Node)

    $parent = $Node.ParentNode
    if ($null -eq $parent) {
        return
    }

    $leadingWhitespace = $Node.PreviousSibling
    [void]$parent.RemoveChild($Node)

    if ($null -ne $leadingWhitespace -and
        $leadingWhitespace.NodeType -in @([System.Xml.XmlNodeType]::Whitespace, [System.Xml.XmlNodeType]::SignificantWhitespace)) {
        [void]$parent.RemoveChild($leadingWhitespace)
    }
}

function Remove-PropertyName {
    param(
        [System.Xml.XmlDocument]$Xml,
        [string]$Name
    )

    foreach ($propertyGroup in @($Xml.Project.ChildNodes | Where-Object { $_.NodeType -eq [System.Xml.XmlNodeType]::Element -and $_.Name -eq 'PropertyGroup' })) {
        foreach ($child in @($propertyGroup.ChildNodes)) {
            if ($child.NodeType -eq [System.Xml.XmlNodeType]::Element -and $child.Name -eq $Name) {
                Remove-XmlNodePreservingLayout -Node $child
            }
        }
    }
}

function Get-PropertyValue {
    param(
        [System.Xml.XmlDocument]$Xml,
        [string]$Name
    )

    foreach ($propertyGroup in @($Xml.Project.ChildNodes | Where-Object { $_.NodeType -eq [System.Xml.XmlNodeType]::Element -and $_.Name -eq 'PropertyGroup' })) {
        foreach ($child in @($propertyGroup.ChildNodes)) {
            if ($child.NodeType -eq [System.Xml.XmlNodeType]::Element -and $child.Name -eq $Name) {
                return $child.InnerText
            }
        }
    }

    return $null
}

function Get-XmlElementText {
    param(
        [object]$Parent,
        [string]$Name
    )

    foreach ($child in @($Parent.ChildNodes)) {
        if ($child.NodeType -eq [System.Xml.XmlNodeType]::Element -and $child.Name -eq $Name) {
            return $child.InnerText
        }
    }

    return $null
}

function Remove-PackageReference {
    param(
        [System.Xml.XmlDocument]$Xml,
        [string[]]$PackageIds
    )

    $idSet = @{}
    foreach ($packageId in $PackageIds) {
        $idSet[$packageId.ToLowerInvariant()] = $true
    }

    foreach ($itemGroup in @($Xml.Project.ChildNodes | Where-Object { $_.NodeType -eq [System.Xml.XmlNodeType]::Element -and $_.Name -eq 'ItemGroup' })) {
        foreach ($packageReference in @($itemGroup.ChildNodes | Where-Object { $_.NodeType -eq [System.Xml.XmlNodeType]::Element -and $_.Name -eq 'PackageReference' })) {
            $include = $packageReference.GetAttribute('Include')
            if ([string]::IsNullOrWhiteSpace($include)) {
                continue
            }

            if ($idSet.ContainsKey($include.ToLowerInvariant())) {
                Remove-XmlNodePreservingLayout -Node $packageReference
            }
        }

        $significantChildren = @($itemGroup.ChildNodes | Where-Object {
            $_.NodeType -eq [System.Xml.XmlNodeType]::Element -or
            $_.NodeType -eq [System.Xml.XmlNodeType]::Comment -or
            ($_.NodeType -eq [System.Xml.XmlNodeType]::Text -and -not [string]::IsNullOrWhiteSpace($_.Value))
        })

        if ($significantChildren.Count -eq 0) {
            Remove-XmlNodePreservingLayout -Node $itemGroup
        }
    }
}

function Normalize-DelimitedProperty {
    param(
        [System.Xml.XmlDocument]$Xml,
        [string]$PropertyName,
        [string[]]$RemoveValues
    )

    $remove = @{}
    foreach ($value in $RemoveValues) {
        $remove[$value.ToLowerInvariant()] = $true
    }

    foreach ($propertyGroup in @($Xml.Project.ChildNodes | Where-Object { $_.NodeType -eq [System.Xml.XmlNodeType]::Element -and $_.Name -eq 'PropertyGroup' })) {
        foreach ($child in @($propertyGroup.ChildNodes)) {
            if ($child.NodeType -ne [System.Xml.XmlNodeType]::Element -or $child.Name -ne $PropertyName) {
                continue
            }

            $values = @($child.InnerText -split ';' | ForEach-Object { $_.Trim() } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
            $kept = New-Object System.Collections.Generic.List[string]
            foreach ($value in $values) {
                if (-not $remove.ContainsKey($value.ToLowerInvariant()) -and -not $kept.Contains($value)) {
                    $kept.Add($value)
                }
            }

            if ($kept.Count -eq 0 -or ($kept.Count -eq 1 -and $kept[0] -eq ('$(' + $PropertyName + ')'))) {
                Remove-XmlNodePreservingLayout -Node $child
            }
            else {
                $child.InnerText = ($kept -join ';')
            }
        }
    }
}

function Convert-XmlToString {
    param([System.Xml.XmlDocument]$Xml)

    $builder = [System.Text.StringBuilder]::new()
    $settings = [System.Xml.XmlWriterSettings]::new()
    $settings.Indent = $true
    $settings.IndentChars = "`t"
    $settings.OmitXmlDeclaration = $true
    $settings.NewLineChars = "`r`n"
    $settings.NewLineHandling = [System.Xml.NewLineHandling]::Replace

    $writer = [System.Xml.XmlWriter]::Create($builder, $settings)
    $Xml.Save($writer)
    $writer.Close()
    return $builder.ToString().TrimEnd([char[]]@("`r", "`n")) + "`r`n"
}

function Get-ProjectSemanticState {
    param([System.Xml.XmlDocument]$Xml)

    $propertyNames = @(
        'TargetFramework',
        'TargetFrameworks',
        'IsTestProject',
        'OutputType',
        'IsPackable',
        'NoWarn',
        'WarningsAsErrors',
        'UseMicrosoftTestingPlatformRunner',
        'PreserveCompilationContext',
        'TestingPlatformDotnetTestSupport'
    )

    $properties = [ordered]@{}
    foreach ($propertyName in $propertyNames) {
        $properties[$propertyName] = Get-PropertyValue -Xml $Xml -Name $propertyName
    }

    $packageValues = New-Object System.Collections.Generic.List[string]
    foreach ($itemGroup in @($Xml.Project.ChildNodes | Where-Object { $_.NodeType -eq [System.Xml.XmlNodeType]::Element -and $_.Name -eq 'ItemGroup' })) {
        foreach ($packageReference in @($itemGroup.ChildNodes | Where-Object { $_.NodeType -eq [System.Xml.XmlNodeType]::Element -and $_.Name -eq 'PackageReference' })) {
            $include = $packageReference.GetAttribute('Include')
            $version = $packageReference.GetAttribute('Version')
            if (-not [string]::IsNullOrWhiteSpace($include)) {
                $packageValues.Add("$($include.ToLowerInvariant())|$version")
            }
        }
    }

    $packages = @($packageValues | Sort-Object)

    return ([pscustomobject]@{
        properties = [pscustomobject]$properties
        packages = @($packages)
    } | ConvertTo-Json -Compress -Depth 5)
}

function Test-CentralTargetFramework {
    param([string]$DirectoryBuildPropsPath)

    if (-not (Test-Path -Path $DirectoryBuildPropsPath)) {
        return $false
    }

    try {
        [xml]$xml = Get-Content -Path $DirectoryBuildPropsPath -Raw
        foreach ($propertyGroup in @($xml.Project.ChildNodes | Where-Object { $_.NodeType -eq [System.Xml.XmlNodeType]::Element -and $_.Name -eq 'PropertyGroup' })) {
            $targetFramework = Get-XmlElementText -Parent $propertyGroup -Name 'TargetFramework'
            $targetFrameworks = Get-XmlElementText -Parent $propertyGroup -Name 'TargetFrameworks'
            if ([string]::IsNullOrWhiteSpace($propertyGroup.Condition) -and
                (-not [string]::IsNullOrWhiteSpace($targetFramework) -or
                 -not [string]::IsNullOrWhiteSpace($targetFrameworks))) {
                return $true
            }
        }
    }
    catch {
        return $false
    }

    return $false
}

function Get-ProjectClassification {
    param(
        [string]$RelativePath,
        [string]$ProjectName,
        [string]$IsTestProjectValue
    )

    $pathHasTestFolder = $RelativePath -match '(^|/)test(/|$)' -or $RelativePath -match '(^|/)Test(/|$)'
    $isPluralTestName = $ProjectName -match '\.Tests$'
    $isSingularRunnableName = $ProjectName -match '\.Test$'
    $isHelperName = $ProjectName -match '\.Test\.(Shared|Mocks|Support|Infrastructure)$'

    if ($IsTestProjectValue -eq 'true') {
        return [pscustomobject]@{ Kind = 'Runnable'; IsPlural = $isPluralTestName; IsHelperName = $isHelperName }
    }

    if ($IsTestProjectValue -eq 'false') {
        return [pscustomobject]@{ Kind = 'Helper'; IsPlural = $isPluralTestName; IsHelperName = $isHelperName }
    }

    if ($isHelperName) {
        return [pscustomobject]@{ Kind = 'Helper'; IsPlural = $isPluralTestName; IsHelperName = $true }
    }

    if ($isSingularRunnableName -or $isPluralTestName) {
        return [pscustomobject]@{ Kind = 'Runnable'; IsPlural = $isPluralTestName; IsHelperName = $false }
    }

    if ($pathHasTestFolder) {
        return [pscustomobject]@{ Kind = 'Ambiguous'; IsPlural = $isPluralTestName; IsHelperName = $false }
    }

    return [pscustomobject]@{ Kind = 'None'; IsPlural = $isPluralTestName; IsHelperName = $false }
}

function Set-RunnableTestProject {
    param(
        [string]$ProjectPath,
        [bool]$HasCentralTargetFramework
    )

    $oldContent = Get-Content -Path $ProjectPath -Raw
    $xml = Get-XmlDocument -Path $ProjectPath
    $oldState = Get-ProjectSemanticState -Xml $xml

    Set-PropertyValue -Xml $xml -Name 'IsTestProject' -Value 'true'

    foreach ($name in $RunnerPropertyNames) {
        Remove-PropertyName -Xml $xml -Name $name
    }

    if ($HasCentralTargetFramework) {
        Remove-PropertyName -Xml $xml -Name 'TargetFramework'
        Remove-PropertyName -Xml $xml -Name 'TargetFrameworks'
    }

    Normalize-DelimitedProperty -Xml $xml -PropertyName 'NoWarn' -RemoveValues @('CS1591')
    Normalize-DelimitedProperty -Xml $xml -PropertyName 'WarningsAsErrors' -RemoveValues @('xUnit1051')
    Remove-PackageReference -Xml $xml -PackageIds $SharedRunnablePackageIds
    $newState = Get-ProjectSemanticState -Xml $xml

    return [pscustomobject]@{
        OldContent = $oldContent
        NewContent = Convert-XmlToString -Xml $xml
        SemanticChanged = $oldState -ne $newState
    }
}

function Set-HelperTestProject {
    param([string]$ProjectPath)

    $oldContent = Get-Content -Path $ProjectPath -Raw
    $xml = Get-XmlDocument -Path $ProjectPath
    $oldState = Get-ProjectSemanticState -Xml $xml

    Set-PropertyValue -Xml $xml -Name 'IsTestProject' -Value 'false'

    foreach ($name in @('OutputType', 'UseMicrosoftTestingPlatformRunner', 'PreserveCompilationContext', 'TestingPlatformDotnetTestSupport')) {
        Remove-PropertyName -Xml $xml -Name $name
    }

    Normalize-DelimitedProperty -Xml $xml -PropertyName 'WarningsAsErrors' -RemoveValues @('xUnit1051')
    Remove-PackageReference -Xml $xml -PackageIds @(
        'microsoft.net.test.sdk',
        'xunit.runner.visualstudio',
        'xunit.v3',
        'xunit.v3.mtp-v2',
        'microsoft.testing.extensions.codecoverage',
        'microsoft.testing.extensions.trxreport'
    )
    $newState = Get-ProjectSemanticState -Xml $xml

    return [pscustomobject]@{
        OldContent = $oldContent
        NewContent = Convert-XmlToString -Xml $xml
        SemanticChanged = $oldState -ne $newState
    }
}

if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    $RepoRoot = Resolve-TestConfigRepoRoot
}
else {
    $RepoRoot = (Resolve-Path -Path $RepoRoot).Path
}

$drift = [System.Collections.Generic.List[object]]::new()
$warnings = [System.Collections.Generic.List[string]]::new()
$changedFiles = [System.Collections.Generic.List[string]]::new()
$centralFiles = [System.Collections.Generic.List[object]]::new()
$projects = [System.Collections.Generic.List[object]]::new()

$directoryBuildPropsPath = Join-Path -Path $RepoRoot -ChildPath 'Directory.Build.props'
$directoryBuildTargetsPath = Join-Path -Path $RepoRoot -ChildPath 'Directory.Build.targets'
$directoryPackagesPropsPath = Join-Path -Path $RepoRoot -ChildPath 'Directory.Packages.props'
$globalJsonPath = Join-Path -Path $RepoRoot -ChildPath 'global.json'

$centralTransforms = @(
    [pscustomobject]@{
        Path = $globalJsonPath
        RelativePath = 'global.json'
        Kind = 'global-json'
        DefaultContent = "{}"
        Transform = { param($content) Set-GlobalJsonContent -Content $content }
        Description = 'Microsoft Testing Platform dotnet test runner opt-in'
    },
    [pscustomobject]@{
        Path = $directoryBuildPropsPath
        RelativePath = 'Directory.Build.props'
        Kind = 'props'
        DefaultContent = "<Project>`r`n</Project>`r`n"
        Transform = { param($content) Set-DirectoryBuildPropsContent -Content $content }
        Description = 'Xunit global using for IsTestProject=true'
    },
    [pscustomobject]@{
        Path = $directoryBuildTargetsPath
        RelativePath = 'Directory.Build.targets'
        Kind = 'targets'
        DefaultContent = "<Project>`r`n</Project>`r`n"
        Transform = { param($content) Set-DirectoryBuildTargetsContent -Content $content }
        Description = 'Runnable test output, package, MTP, and warning settings'
    },
    [pscustomobject]@{
        Path = $directoryPackagesPropsPath
        RelativePath = 'Directory.Packages.props'
        Kind = 'packages'
        DefaultContent = "<Project>`r`n</Project>`r`n"
        Transform = { param($content) Set-DirectoryPackagesPropsContent -Content $content }
        Description = 'Shared test package references'
    }
)

foreach ($entry in $centralTransforms) {
    $oldContent = Get-FileTextOrDefault -Path $entry.Path -DefaultContent $entry.DefaultContent
    $newContent = & $entry.Transform $oldContent
    $hasDrift = (Get-CentralSemanticState -Content $oldContent -Kind $entry.Kind) -ne (Get-CentralSemanticState -Content $newContent -Kind $entry.Kind)

    if ($hasDrift) {
        Add-Drift -Drift $drift -File $entry.RelativePath -Kind 'central' -Message $entry.Description
        Write-IfChanged -Path $entry.Path -OldContent $oldContent -NewContent $newContent -Mode $Mode -ChangedFiles $changedFiles -RepoRoot $RepoRoot
    }

    $centralFiles.Add([pscustomobject]@{
        file = $entry.RelativePath
        status = if ($hasDrift) { if ($Mode -eq 'Apply') { 'updated' } else { 'drift' } } else { 'ok' }
        rule = $entry.Description
    })
}

$hasCentralTargetFramework = Test-CentralTargetFramework -DirectoryBuildPropsPath $directoryBuildPropsPath

$projectFiles = Get-ChildItem -Path $RepoRoot -Recurse -Filter '*.csproj' |
    Where-Object { $_.FullName -notmatch '[\\/]bin[\\/]' -and $_.FullName -notmatch '[\\/]obj[\\/]' } |
    Sort-Object FullName

foreach ($projectFile in $projectFiles) {
    $relativePath = Get-RelativePath -BasePath $RepoRoot -Path $projectFile.FullName
    $projectName = [System.IO.Path]::GetFileNameWithoutExtension($projectFile.Name)

    try {
        [xml]$projectXml = Get-Content -Path $projectFile.FullName -Raw
    }
    catch {
        $warnings.Add("Could not parse project '$relativePath': $($_.Exception.Message)")
        continue
    }

    $isTestProjectValue = $null
    foreach ($propertyGroup in @($projectXml.Project.ChildNodes | Where-Object { $_.NodeType -eq [System.Xml.XmlNodeType]::Element -and $_.Name -eq 'PropertyGroup' })) {
        $candidateIsTestProject = Get-XmlElementText -Parent $propertyGroup -Name 'IsTestProject'
        if (-not [string]::IsNullOrWhiteSpace($candidateIsTestProject)) {
            $isTestProjectValue = $candidateIsTestProject.Trim()
            break
        }
    }

    $classification = Get-ProjectClassification -RelativePath $relativePath -ProjectName $projectName -IsTestProjectValue $isTestProjectValue
    if ($classification.Kind -eq 'None') {
        continue
    }

    if ($classification.IsPlural) {
        Add-Drift -Drift $drift -File $relativePath -Kind 'naming' -Message 'Test projects must use singular .Test naming; rename manually with solution updates.'
        $warnings.Add("Plural test naming found: $relativePath")
    }

    if ($classification.Kind -eq 'Ambiguous') {
        $warnings.Add("Ambiguous project under test folder; leaving unchanged: $relativePath")
        $projects.Add([pscustomobject]@{
            file = $relativePath
            kind = 'ambiguous'
            status = 'unchanged'
        })
        continue
    }

    if ($classification.Kind -eq 'Runnable' -and $classification.IsHelperName) {
        $warnings.Add("Project is marked runnable but looks like a helper: $relativePath")
    }

    if ($classification.Kind -eq 'Runnable') {
        $transform = Set-RunnableTestProject -ProjectPath $projectFile.FullName -HasCentralTargetFramework $hasCentralTargetFramework
        $hasDrift = $transform.SemanticChanged

        if ($hasDrift) {
            Add-Drift -Drift $drift -File $relativePath -Kind 'project' -Message 'Runnable test project differs from shared IsTestProject=true pattern.'
            Write-IfChanged -Path $projectFile.FullName -OldContent $transform.OldContent -NewContent $transform.NewContent -Mode $Mode -ChangedFiles $changedFiles -RepoRoot $RepoRoot
        }

        $projects.Add([pscustomobject]@{
            file = $relativePath
            kind = 'runnable'
            status = if ($hasDrift) { if ($Mode -eq 'Apply') { 'updated' } else { 'drift' } } else { 'ok' }
        })
        continue
    }

    if ($classification.Kind -eq 'Helper') {
        $transform = Set-HelperTestProject -ProjectPath $projectFile.FullName
        $hasDrift = $transform.SemanticChanged

        if ($hasDrift) {
            Add-Drift -Drift $drift -File $relativePath -Kind 'project' -Message 'Helper project should be explicitly non-runnable and avoid test runner setup.'
            Write-IfChanged -Path $projectFile.FullName -OldContent $transform.OldContent -NewContent $transform.NewContent -Mode $Mode -ChangedFiles $changedFiles -RepoRoot $RepoRoot
        }

        $projects.Add([pscustomobject]@{
            file = $relativePath
            kind = 'helper'
            status = if ($hasDrift) { if ($Mode -eq 'Apply') { 'updated' } else { 'drift' } } else { 'ok' }
        })
    }
}

if ([string]::IsNullOrWhiteSpace($ReportPath)) {
    $ReportPath = Join-Path -Path ([System.IO.Path]::GetTempPath()) -ChildPath 'umbrella-dotnet-standardize-test-projects-report.json'
}
elseif (-not [System.IO.Path]::IsPathRooted($ReportPath)) {
    $ReportPath = Join-Path -Path $RepoRoot -ChildPath $ReportPath
}

$reportDirectory = Split-Path -Path $ReportPath -Parent
if (-not [string]::IsNullOrWhiteSpace($reportDirectory) -and -not (Test-Path -Path $reportDirectory)) {
    New-Item -Path $reportDirectory -ItemType Directory -Force | Out-Null
}

$report = [pscustomobject]@{
    mode = $Mode
    repoRoot = $RepoRoot
    hasCentralTargetFramework = $hasCentralTargetFramework
    centralFiles = @($centralFiles)
    projects = @($projects)
    drift = @($drift)
    changedFiles = @($changedFiles | Sort-Object -Unique)
    warnings = @($warnings | Sort-Object -Unique)
}

$report | ConvertTo-Json -Depth 20 | Set-Content -Path $ReportPath -Encoding utf8

Write-Host "umbrella-dotnet-standardize-test-projects mode: $Mode"
Write-Host "Report written to: $ReportPath"
Write-Host ""
Write-Host ("Central files checked: {0}" -f $centralFiles.Count)
Write-Host ("Projects checked     : {0}" -f $projects.Count)
Write-Host ("Drift items          : {0}" -f $drift.Count)
Write-Host ("Warnings             : {0}" -f $warnings.Count)

if ($changedFiles.Count -gt 0) {
    Write-Host ""
    Write-Host "$(if ($Mode -eq 'Apply') { 'Changed files' } else { 'Files that would change' }):"
    foreach ($file in @($changedFiles | Sort-Object -Unique)) {
        Write-Host "  - $file"
    }
}

if ($warnings.Count -gt 0) {
    Write-Host ""
    Write-Host "Warnings:"
    foreach ($warning in @($warnings | Sort-Object -Unique)) {
        Write-Host "  - $warning"
    }
}
