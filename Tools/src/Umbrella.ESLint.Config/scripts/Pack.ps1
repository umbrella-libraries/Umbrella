[CmdletBinding()]
param(
	[string] $OutputDirectory,
	[string] $VersionFile
)

$ErrorActionPreference = 'Stop'
$packageDirectory = Split-Path $PSScriptRoot -Parent
$repositoryDirectory = [IO.Path]::GetFullPath((Join-Path $packageDirectory '../../..'))
if (!$OutputDirectory) { $OutputDirectory = Join-Path $repositoryDirectory 'artifacts/npm' }
if (!$VersionFile) { $VersionFile = Join-Path $repositoryDirectory 'Directory.Build.props' }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
$repositoryPrefix = $repositoryDirectory.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
$artifactPrefix = (Join-Path $repositoryDirectory 'artifacts') + [IO.Path]::DirectorySeparatorChar
if ($OutputDirectory.Equals($repositoryDirectory, [StringComparison]::OrdinalIgnoreCase) -or
	($OutputDirectory.StartsWith($repositoryPrefix, [StringComparison]::OrdinalIgnoreCase) -and
	!$OutputDirectory.StartsWith($artifactPrefix, [StringComparison]::OrdinalIgnoreCase))) {
	throw 'Package output inside the repository must be in a subdirectory of artifacts, never a source directory.'
}
[xml] $versionDocument = Get-Content -LiteralPath $VersionFile -Raw
$versions = @($versionDocument.SelectNodes('/Project/PropertyGroup/Version'))
if ($versions.Count -ne 1) { throw 'Expected one shared Version in Directory.Build.props.' }
$packageVersion = $versions[0].InnerText.Trim()

# Use npm's SemVer rules without maintaining a second version parser.
$semverPath = Join-Path $repositoryDirectory 'node_modules/semver'
& node -e 'const valid = require(process.argv[1]).valid(process.argv[2]); if (!valid || valid !== process.argv[2] || valid === "0.0.0") process.exit(1);' $semverPath $packageVersion
if ($LASTEXITCODE -ne 0) { throw "Invalid release version '$packageVersion'. Run npm ci before packing." }

$stageDirectory = Join-Path ([IO.Path]::GetTempPath()) ('umbrella-eslint-pack-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $stageDirectory | Out-Null
try {
	$manifest = Get-Content -LiteralPath (Join-Path $packageDirectory 'package.json') -Raw | ConvertFrom-Json -AsHashtable
	$manifest.version = $packageVersion
	$manifest.Remove('private') | Out-Null
	$manifest.Remove('scripts') | Out-Null
	$manifest.Remove('devDependencies') | Out-Null
	foreach ($file in $manifest.files) {
		Copy-Item -LiteralPath (Join-Path $packageDirectory $file) -Destination (Join-Path $stageDirectory $file)
	}
	$manifest | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path $stageDirectory 'package.json') -Encoding utf8NoBOM
	New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
	Push-Location $stageDirectory
	try {
		$packJson = & npm pack --json --ignore-scripts --pack-destination $OutputDirectory
		if ($LASTEXITCODE -ne 0) { throw 'npm pack failed.' }
	} finally { Pop-Location }
	$packed = @($packJson | ConvertFrom-Json)[0]
	$tag = if ($packageVersion.Contains('-')) { 'next' } else { 'latest' }
	@{ name = $manifest.name; version = $packageVersion; filename = $packed.filename; integrity = $packed.integrity; tag = $tag } |
		ConvertTo-Json | Set-Content -LiteralPath (Join-Path $OutputDirectory 'pack-info.json') -Encoding utf8NoBOM
	# These files belong to the pipeline artifact, never to the published tarball.
	Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Publish.cjs') -Destination (Join-Path $OutputDirectory 'Publish.cjs')
	@{ name = 'umbrella-npm-publish'; private = $true; scripts = @{ 'publish:feed' = 'node Publish.cjs' } } |
		ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'package.json') -Encoding utf8NoBOM
	Write-Host "Packed $($manifest.name)@$packageVersion into $OutputDirectory"
} finally {
	# Only delete the unique temporary staging directory created by this invocation.
	$resolvedStage = [IO.Path]::GetFullPath($stageDirectory)
	$tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
	if ((Split-Path $resolvedStage -Parent).TrimEnd('\', '/') -ne $tempRoot.TrimEnd('\', '/') -or
		!(Split-Path $resolvedStage -Leaf).StartsWith('umbrella-eslint-pack-')) {
		throw 'Refusing to remove an unexpected staging directory.'
	}
	Remove-Item -LiteralPath $resolvedStage -Recurse -Force
}
