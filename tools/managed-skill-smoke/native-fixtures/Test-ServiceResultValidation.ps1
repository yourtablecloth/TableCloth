$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'ServiceResultValidation.ps1')
$cases = @(
    @{ Name='valid-table'; Status=200; Body='[{"ORG_ID":"101","TBL_ID":"DT_1IN1502"}]'; Expected=$true },
    @{ Name='http-200-error'; Status=200; Body='{"err":"20","errMsg":"API error"}'; Expected=$false },
    @{ Name='http-200-null'; Status=200; Body='null'; Expected=$false },
    @{ Name='http-200-empty'; Status=200; Body='[]'; Expected=$false },
    @{ Name='missing-table'; Status=200; Body='[{"ORG_ID":"101"}]'; Expected=$false },
    @{ Name='invalid-table'; Status=200; Body='[{"ORG_ID":"101","TBL_ID":" "}]'; Expected=$false },
    @{ Name='boolean-id'; Status=200; Body='[{"ORG_ID":true,"TBL_ID":"DT_X"}]'; Expected=$false },
    @{ Name='zero-org'; Status=200; Body='[{"ORG_ID":"000","TBL_ID":"DT_X"}]'; Expected=$false },
    @{ Name='mixed-error-row'; Status=200; Body='[{"ORG_ID":"101","TBL_ID":"DT_X","err":"500"}]'; Expected=$false },
    @{ Name='wrong-http-status'; Status=500; Body='[{"ORG_ID":"101","TBL_ID":"DT_X"}]'; Expected=$false },
    @{ Name='malformed-json'; Status=200; Body='{"ORG_ID"'; Expected=$false }
)
foreach ($case in $cases) {
    $actual = Test-KosisSearchResponse -Status $case.Status -ResponseBody $case.Body
    if ($actual.serviceOk -ne $case.Expected) { throw "KOSIS result validation failed: $($case.Name)" }
    if ($case.Name -eq 'http-200-error' -and @($actual.applicationErrors).Count -lt 1) { throw 'The API error code was not retained.' }
    [pscustomobject]@{ name=$case.Name; expected=$case.Expected; actual=$actual.serviceOk; reason=$actual.reason }
}
