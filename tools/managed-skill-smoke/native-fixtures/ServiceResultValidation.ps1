function Test-KosisSearchResponse {
    param([int]$Status, [AllowEmptyString()][string]$ResponseBody)
    $result = [ordered]@{ serviceOk = $false; status = $Status; rowCount = 0; tableIdentifiers = @(); applicationErrors = @(); reason = 'invalid-json' }
    try { $data = ConvertFrom-Json -InputObject $ResponseBody -NoEnumerate -Depth 24 -ErrorAction Stop }
    catch { return [pscustomobject]$result }
    $errors = [System.Collections.Generic.List[object]]::new()
    foreach ($candidate in @($data)) {
        if ($null -eq $candidate) { continue }
        foreach ($field in @('err','error','errCd','errorCode','errMsg')) {
            $property = $candidate.PSObject.Properties[$field]
            if ($null -ne $property -and $null -ne $property.Value -and [string]$property.Value -notin @('', '0')) {
                $value = [string]$property.Value
                $errors.Add([ordered]@{ field = $field; value = $value.Substring(0, [Math]::Min(512, $value.Length)) })
            }
        }
    }
    $result.applicationErrors = @($errors.ToArray())
    if ($Status -ne 200) { $result.reason = 'http-status'; return [pscustomobject]$result }
    if ($errors.Count -ne 0) { $result.reason = 'application-error'; return [pscustomobject]$result }
    if ($data -isnot [array] -or $data.Count -ne 1) { $result.reason = 'expected-one-search-row'; return [pscustomobject]$result }
    $row = $data[0]
    if ($null -eq $row) { $result.reason = 'invalid-identifiers'; return [pscustomobject]$result }
    $org = $row.PSObject.Properties['ORG_ID']
    $table = $row.PSObject.Properties['TBL_ID']
    if ($null -eq $org -or $null -eq $table -or $org.Value -isnot [string] -or $table.Value -isnot [string] -or
        $org.Value -notmatch '^[0-9]{1,16}$' -or $org.Value -notmatch '[1-9]' -or $table.Value -notmatch '^[A-Za-z0-9_-]{1,128}$') {
        $result.reason = 'invalid-identifiers'; return [pscustomobject]$result
    }
    $result.serviceOk = $true
    $result.rowCount = 1
    $result.tableIdentifiers = @([ordered]@{ ORG_ID = $org.Value; TBL_ID = $table.Value })
    $result.reason = 'validated-search-data'
    return [pscustomobject]$result
}
