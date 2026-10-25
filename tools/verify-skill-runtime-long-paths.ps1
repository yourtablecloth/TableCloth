[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release',
    [Parameter(Mandatory)]
    [string] $RuntimeRoot
)

$ErrorActionPreference = 'Stop'
if ($env:GITHUB_ACTIONS -ne 'true' -or $env:RUNNER_ENVIRONMENT -ne 'github-hosted') {
    throw 'This policy-toggle test runs only on disposable GitHub-hosted Windows runners.'
}

$policyKey = 'HKLM:\SYSTEM\CurrentControlSet\Control\FileSystem'
$policyName = 'LongPathsEnabled'
$originalPolicy = Get-ItemProperty -LiteralPath $policyKey
$hadPolicy = $null -ne $originalPolicy.PSObject.Properties[$policyName]
$originalValue = $originalPolicy.$policyName
$originalKind = if ($hadPolicy) { (Get-Item -LiteralPath $policyKey).GetValueKind($policyName) } else { $null }
try {
    foreach ($policyCase in @(@{ Value = 0; Expectation = '--expect-disabled' }, @{ Value = 1; Expectation = '--expect-enabled' })) {
        Set-ItemProperty -LiteralPath $policyKey -Name $policyName -Type DWord -Value $policyCase.Value
        Write-Host "Checking long-path behavior in fresh processes with policy=$($policyCase.Value)."
        & dotnet run --project src/TableCloth.ManagedAi.Poc -c $Configuration --no-build -- skill-runtime-diagnose $policyCase.Expectation --root $RuntimeRoot
        if ($LASTEXITCODE -ne 0) { throw "Long-path diagnostics failed for policy=$($policyCase.Value)." }
    }
}
finally {
    if ($hadPolicy) {
        Set-ItemProperty -LiteralPath $policyKey -Name $policyName -Type $originalKind -Value $originalValue
    }
    else {
        Remove-ItemProperty -LiteralPath $policyKey -Name $policyName
    }
}
