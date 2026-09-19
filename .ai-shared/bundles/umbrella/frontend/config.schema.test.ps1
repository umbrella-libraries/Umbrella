#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$schemaPath = Join-Path $PSScriptRoot 'config.schema.json'

# Exercise the published schema with a real JSON Schema validator, not checks
# that merely assert the presence of particular schema keywords.
$cases = @(
    @{ Name = 'copy requires destination'; Valid = $false; Helpers = @{ source = 'Styles'; strategy = 'copy' } }
    @{ Name = 'copy accepts destination'; Valid = $true; Helpers = @{ source = 'Styles'; strategy = 'copy'; destination = 'Client/Styles' } }
    @{ Name = 'copy rejects empty destination'; Valid = $false; Helpers = @{ source = 'Styles'; strategy = 'copy'; destination = '' } }
    @{ Name = 'copy rejects escaping destination'; Valid = $false; Helpers = @{ source = 'Styles'; strategy = 'copy'; destination = '../outside' } }
    @{ Name = 'direct does not require destination'; Valid = $true; Helpers = @{ source = 'Styles'; strategy = 'direct' } }
    @{ Name = 'strategy remains required'; Valid = $false; Helpers = @{ source = 'Styles'; destination = 'Client/Styles' } }
    @{ Name = 'unknown strategy is rejected'; Valid = $false; Helpers = @{ source = 'Styles'; strategy = 'unknown' } }
)

foreach ($case in $cases) {
    $config = @{
        baselineVersion = 1
        profile = 'blazor'
        projects = @(@{ packagePath = '.'; sharedHelpers = $case.Helpers })
    } | ConvertTo-Json -Depth 8
    $actual = Test-Json -Json $config -SchemaFile $schemaPath -ErrorAction SilentlyContinue
    if ($actual -ne $case.Valid) {
        throw "Schema regression: $($case.Name); expected $($case.Valid), got $actual."
    }
}
Write-Output "Passed $($cases.Count) front-end schema regression cases."
