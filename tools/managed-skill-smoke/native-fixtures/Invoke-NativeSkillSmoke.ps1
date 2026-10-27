param(
    [Parameter(Mandatory)][string]$HostExecutable,
    [Parameter(Mandatory)][string]$SeedManifest,
    [Parameter(Mandatory)][string]$EvidenceDirectory,
    [switch]$KeepFixtures
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$utf8 = [System.Text.UTF8Encoding]::new($false)
. (Join-Path $PSScriptRoot 'ServiceResultValidation.ps1')
if (-not ('TableCloth.SkillSmokeDiagnostics.ProcessEvidenceObserver' -as [type])) {
    Add-Type -Path (Join-Path $PSScriptRoot '../ProcessEvidenceObserver.cs')
}

function Assert-LocalPath([string]$Path) {
    $resolved = [System.IO.Path]::GetFullPath($Path)
    if (-not [System.IO.Path]::IsPathFullyQualified($Path) -or $resolved.StartsWith('\\') -or $resolved.Substring(2).Contains(':')) {
        throw 'Only absolute local paths without alternate streams are accepted.'
    }
    for ($current = $resolved; $current; $current = [System.IO.Path]::GetDirectoryName($current)) {
        if ((Test-Path -LiteralPath $current) -and ((Get-Item -LiteralPath $current -Force).Attributes -band [System.IO.FileAttributes]::ReparsePoint)) {
            throw 'Reparse points are not accepted.'
        }
    }
    return $resolved
}

function Assert-ChildPath([string]$Root, [string]$Path) {
    $resolved = Assert-LocalPath $Path
    $prefix = [System.IO.Path]::TrimEndingDirectorySeparator((Assert-LocalPath $Root)) + [System.IO.Path]::DirectorySeparatorChar
    if (-not $resolved.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)) { throw 'Path is outside the intended root.' }
    return $resolved
}

function Write-Utf8([string]$Path, [string]$Content) {
    [System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($Path)) | Out-Null
    [System.IO.File]::WriteAllText($Path, $Content, $utf8)
}

function Get-Sha256([string]$Path) { return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant() }

$HostExecutable = Assert-LocalPath $HostExecutable
$SeedManifest = Assert-LocalPath $SeedManifest
$EvidenceDirectory = Assert-LocalPath $EvidenceDirectory
if (-not (Test-Path -LiteralPath $HostExecutable -PathType Leaf) -or [System.IO.Path]::GetFileName($HostExecutable) -ine 'TableCloth.exe') {
    throw 'An actual TableCloth.exe is required.'
}
$seed = Get-Content -LiteralPath $SeedManifest -Raw | ConvertFrom-Json
$ownedRoot = Assert-LocalPath ([string]$seed.ownedRoot)
$expectedRoot = [System.IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'TableCloth.Data\ManagedAi\skill-execution'))
if (-not $ownedRoot.Equals($expectedRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'Live fixture seed ownedRoot must use TableCloth.Data\ManagedAi\skill-execution outside the installer-owned directory. Legacy seeds are offline historical evidence only.'
}
foreach ($runtime in @($seed.runtimeFiles) + @(
    [pscustomobject]@{ path = $seed.pythonExecutable; sha256 = $seed.pythonSha256 },
    [pscustomobject]@{ path = $seed.nodeExecutable; sha256 = $seed.nodeSha256 }
)) {
    $runtimePath = Assert-ChildPath $ownedRoot ([string]$runtime.path)
    if ((Get-Sha256 $runtimePath) -cne ([string]$runtime.sha256).ToLowerInvariant()) { throw 'Seed runtime hash no longer matches.' }
}

[System.IO.Directory]::CreateDirectory($EvidenceDirectory) | Out-Null
$fixtureBase = Assert-ChildPath $ownedRoot (Join-Path $ownedRoot 'native-fixtures')
$fixtureDirectory = Assert-ChildPath $fixtureBase (Join-Path $fixtureBase ([guid]::NewGuid().ToString('N')))
[System.IO.Directory]::CreateDirectory($fixtureDirectory) | Out-Null
$package = Assert-ChildPath $fixtureDirectory (Join-Path $fixtureDirectory 'package')
[System.IO.Directory]::CreateDirectory($package) | Out-Null
$process = $null
$processStarted = $false
$processObserver = $null

try {
    Write-Utf8 (Join-Path $package 'local_package\__init__.py') ''
    Write-Utf8 (Join-Path $package 'local_package\helper.py') "VALUE = 17`n"
    Write-Utf8 (Join-Path $package 'empty.txt') ''
    Write-Utf8 (Join-Path $package 'helper.cjs') "module.exports={value:17};`n"
    Write-Utf8 (Join-Path $package 'helper.mjs') "export const value=17;`n"
    Write-Utf8 (Join-Path $package 'python-import.py') @'
import json,os,pathlib,sys
from local_package import helper
payload=json.load(sys.stdin)
print(json.dumps({'kind':'python-import','helper':helper.VALUE,'argv':sys.argv[1:],'input':payload,'emptyBytes':len(pathlib.Path(__file__).with_name('empty.txt').read_bytes()),'canaryAbsent':os.environ.get('TABLECLOTH_TEST_CANARY') is None},ensure_ascii=False))
'@
    Write-Utf8 (Join-Path $package 'node-cjs.cjs') @'
const fs=require('node:fs');
const {value}=require('./helper.cjs');
const input=JSON.parse(fs.readFileSync(0,'utf8'));
console.log(JSON.stringify({kind:'node-cjs',helper:value,argv:process.argv.slice(2),input,main:require.main===module,canaryAbsent:process.env.TABLECLOTH_TEST_CANARY===undefined}));
'@
    Write-Utf8 (Join-Path $package 'node-esm.mjs') @'
import fs from 'node:fs';
import {value} from './helper.mjs';
const input=JSON.parse(fs.readFileSync(0,'utf8'));
console.log(JSON.stringify({kind:'node-esm',helper:value,argv:process.argv.slice(2),input,canaryAbsent:process.env.TABLECLOTH_TEST_CANARY===undefined}));
'@
    Write-Utf8 (Join-Path $package 'node-cjs-network.cjs') @'
const https=require('node:https');
const {value}=require('./helper.cjs');
const url='https://ecos.bok.or.kr/api/KeyStatisticList/sample/json/kr/1/1/';
function summarize(status,text){let data;try{data=JSON.parse(text);}catch{}return {status,rows:data?.KeyStatisticList?.row?.length||0,json:data!==undefined};}
async function fetchOne(){const response=await fetch(url);return summarize(response.status,await response.text());}
function streamOne(useRequest){return new Promise((resolve,reject)=>{const callback=response=>{let text='';response.setEncoding('utf8');response.on('data',part=>text+=part);response.on('end',()=>resolve(summarize(response.statusCode,text)));response.on('error',reject);};const request=useRequest?https.request(url,callback):https.get(url,callback);request.on('error',reject);if(useRequest)request.end();});}
(async()=>{const checks=[await fetchOne(),await fetchOne(),await streamOne(false),await streamOne(true)];console.log(JSON.stringify({kind:'node-cjs-network',helper:value,main:require.main===module,checks,serviceOk:checks.every(x=>x.status===200&&x.rows>0)}));})().catch(error=>{console.error(error.message);process.exitCode=1;});
'@
    Write-Utf8 (Join-Path $package 'node-esm-network.mjs') @'
import {get,request} from 'node:https';
import {value} from './helper.mjs';
const url='https://ecos.bok.or.kr/api/KeyStatisticList/sample/json/kr/1/1/';
function summarize(status,text){let data;try{data=JSON.parse(text);}catch{}return {status,rows:data?.KeyStatisticList?.row?.length||0,json:data!==undefined};}
function streamOne(useRequest){return new Promise((resolve,reject)=>{const callback=response=>{let text='';response.setEncoding('utf8');response.on('data',part=>text+=part);response.on('end',()=>resolve(summarize(response.statusCode,text)));response.on('error',reject);};const call=useRequest?request(url,callback):get(url,callback);call.on('error',reject);if(useRequest)call.end();});}
const first=await fetch(url);const checks=[summarize(first.status,await first.text()),await streamOne(false),await streamOne(true)];
console.log(JSON.stringify({kind:'node-esm-network',helper:value,checks,serviceOk:checks.every(x=>x.status===200&&x.rows>0)}));
'@
    Write-Utf8 (Join-Path $package 'node-kosis.cjs') @'
const url='https://k-skill-proxy.nomadamas.org/v1/kosis/search?method=getList&format=json&jsonVD=Y&searchNm=%EC%9D%B8%EA%B5%AC&resultCount=1&startCount=1';
(async()=>{const response=await fetch(url);const responseBody=await response.text();console.log(JSON.stringify({kind:'node-kosis',status:response.status,responseBody}));})().catch(error=>{console.error(error.message);process.exitCode=1;});
'@
    Write-Utf8 (Join-Path $package 'denied.py') @'
import json,os,socket,sys
try:
    open(sys.argv[1],'r',encoding='utf-8').read()
    file_denied=False
except OSError:
    file_denied=True
connection=socket.socket();connection.settimeout(2)
try:
    connection.connect(('1.1.1.1',443));network_denied=False
except OSError:
    network_denied=True
finally:
    connection.close()
print(json.dumps({'kind':'denied-probe','fileDenied':file_denied,'networkDenied':network_denied,'canaryAbsent':os.environ.get('TABLECLOTH_TEST_CANARY') is None}))
'@

    $packageFiles = @(Get-ChildItem -LiteralPath $package -File -Recurse | Sort-Object FullName | ForEach-Object {
        [pscustomobject]@{ path = $_.FullName; sha256 = Get-Sha256 $_.FullName }
    })
    $cases = @(
        @{ Name = 'python-import-fixture'; Entry = 'python-import.py'; Kind = 'python-script'; Network = $false; Expected = 'python-import' },
        @{ Name = 'node-cjs-fixture'; Entry = 'node-cjs.cjs'; Kind = 'node-script'; Network = $false; Expected = 'node-cjs' },
        @{ Name = 'node-esm-fixture'; Entry = 'node-esm.mjs'; Kind = 'node-script'; Network = $false; Expected = 'node-esm' },
        @{ Name = 'node-cjs-network-fixture'; Entry = 'node-cjs-network.cjs'; Kind = 'node-script'; Network = $true; Expected = 'node-cjs-network' },
        @{ Name = 'node-esm-network-fixture'; Entry = 'node-esm-network.mjs'; Kind = 'node-script'; Network = $true; Expected = 'node-esm-network' },
        @{ Name = 'node-kosis-fixture'; Entry = 'node-kosis.cjs'; Kind = 'node-script'; Network = $true; Expected = 'node-kosis' },
        @{ Name = 'denied-fixture'; Entry = 'denied.py'; Kind = 'python-script'; Network = $false; Expected = 'denied-probe' }
    )
    $registrations = @($cases | ForEach-Object {
        $entry = Join-Path $package $_.Entry
        $networkHosts = [string[]]@()
        if ($_.Network) { $networkHosts = [string[]]@('ecos.bok.or.kr','k-skill-proxy.nomadamas.org') }
        [ordered]@{ skillId = $_.Name; kind = $_.Kind; scriptPath = $entry; sha256 = Get-Sha256 $entry;
            packageRoot = $package; packageFiles = $packageFiles; description = 'Native product verification fixture';
            requiresNetwork = $_.Network; networkHosts = $networkHosts }
    })
    $manifest = [ordered]@{ ownedRoot = $ownedRoot; pythonExecutable = $seed.pythonExecutable; pythonSha256 = $seed.pythonSha256;
        nodeExecutable = $seed.nodeExecutable; nodeSha256 = $seed.nodeSha256; runtimeFiles = @($seed.runtimeFiles); skills = $registrations }
    $manifestPath = Join-Path $fixtureDirectory 'manifest.json'
    $manifestJson = $manifest | ConvertTo-Json -Depth 16
    Write-Utf8 $manifestPath $manifestJson
    Write-Utf8 (Join-Path $EvidenceDirectory 'fixture-manifest.json') $manifestJson
    $hostMarker = Join-Path $EvidenceDirectory 'host-denied.txt'
    Write-Utf8 $hostMarker 'Native fixture marker, no credentials or user data.'

    $messages = [System.Collections.Generic.List[string]]::new()
    $messages.Add(([ordered]@{jsonrpc='2.0';id=1;method='initialize';params=@{protocolVersion='2025-06-18';capabilities=@{};clientInfo=@{name='tablecloth-native-smoke';version='1'}}} | ConvertTo-Json -Depth 10 -Compress))
    $messages.Add((@{jsonrpc='2.0';method='notifications/initialized'} | ConvertTo-Json -Compress))
    $messages.Add((@{jsonrpc='2.0';id=2;method='tools/list';params=@{}} | ConvertTo-Json -Compress))
    for ($index=0; $index -lt $cases.Count; $index++) {
        $arguments = [string[]]@('alpha beta','한글')
        if ($cases[$index].Name -eq 'denied-fixture') { $arguments = [string[]]@($hostMarker) }
        $messages.Add((@{jsonrpc='2.0';id=($index+3);method='tools/call';params=@{name=$cases[$index].Name;arguments=@{arguments=$arguments;stdin="{`"value`":7}`n"}}} | ConvertTo-Json -Depth 10 -Compress))
    }
    Write-Utf8 (Join-Path $EvidenceDirectory 'requests.jsonl') (($messages -join "`n")+"`n")
    $start = [System.Diagnostics.ProcessStartInfo]::new()
    $start.FileName = $HostExecutable
    $start.ArgumentList.Add('--skill-mcp'); $start.ArgumentList.Add($manifestPath)
    $start.UseShellExecute = $false; $start.CreateNoWindow = $true
    $start.RedirectStandardInput = $true; $start.RedirectStandardOutput = $true; $start.RedirectStandardError = $true
    $start.StandardInputEncoding = $utf8; $start.StandardOutputEncoding = $utf8; $start.StandardErrorEncoding = $utf8
    $start.Environment['TABLECLOTH_TEST_CANARY'] = 'must-not-be-inherited'
    $processObserver = [TableCloth.SkillSmokeDiagnostics.ProcessEvidenceObserver]::new($PID, $HostExecutable, $seed.pythonExecutable, $seed.nodeExecutable)
    $processObserver.Start()
    $process = [System.Diagnostics.Process]::new(); $process.StartInfo = $start
    if (-not $process.Start()) { throw 'Native product broker did not start.' }
    $processStarted = $true
    $stdoutTask = $process.StandardOutput.ReadToEndAsync(); $stderrTask = $process.StandardError.ReadToEndAsync()
    foreach ($message in $messages) { $process.StandardInput.WriteLine($message) }
    $process.StandardInput.Close()
    if (-not $process.WaitForExit(480000)) { $process.Kill($true); throw 'Native smoke broker exceeded its deadline.' }
    $stdout = $stdoutTask.GetAwaiter().GetResult(); $stderr = $stderrTask.GetAwaiter().GetResult()
    $observations = @($processObserver.Stop())
    Write-Utf8 (Join-Path $EvidenceDirectory 'processes.json') (ConvertTo-Json -InputObject $observations -Depth 8)
    Write-Utf8 (Join-Path $EvidenceDirectory 'responses.jsonl') $stdout
    Write-Utf8 (Join-Path $EvidenceDirectory 'stderr.txt') $stderr
    $responses = @($stdout -split "`r?`n" | Where-Object { $_ } | ForEach-Object { $_ | ConvertFrom-Json })
    $results = [System.Collections.Generic.List[object]]::new()
    for ($index=0; $index -lt $cases.Count; $index++) {
        $response = @($responses | Where-Object { $_.id -eq ($index+3) })
        $execution = $null; $payload = $null
        if ($response.Count -eq 1 -and $response[0].PSObject.Properties.Name -contains 'result') {
            $execution = $response[0].result.content[0].text | ConvertFrom-Json
            try { $payload = $execution.stdout | ConvertFrom-Json } catch { }
        }
        $passed = $null -ne $execution -and $execution.status -eq 'completed' -and $execution.exitCode -eq 0 -and $execution.wasAppContainer -and
            -not $execution.timedOut -and -not $execution.outputLimited -and $null -ne $payload -and $payload.kind -eq $cases[$index].Expected
        if ($passed -and -not $cases[$index].Network) {
            $passed = $payload.canaryAbsent
            if ($cases[$index].Name -eq 'denied-fixture') { $passed = $passed -and $payload.fileDenied -and $payload.networkDenied }
            else { $passed = $passed -and $payload.helper -eq 17 -and $payload.input.value -eq 7 -and ($payload.argv -join '|') -eq 'alpha beta|한글' }
            if ($cases[$index].Name -eq 'python-import-fixture') { $passed = $passed -and $payload.emptyBytes -eq 0 }
            if ($cases[$index].Name -eq 'node-cjs-fixture') { $passed = $passed -and $payload.main }
        }
        if ($passed -and $cases[$index].Name -eq 'node-cjs-network-fixture') { $passed = $passed -and $payload.main }
        $serviceValidation = $null
        if ($passed -and $cases[$index].Name -eq 'node-kosis-fixture') {
            $serviceValidation = Test-KosisSearchResponse -Status $payload.status -ResponseBody $payload.responseBody
        }
        $results.Add([ordered]@{name=$cases[$index].Name;passed=[bool]$passed;execution=$execution;payload=$payload;serviceValidation=$serviceValidation})
    }
    $networkCaseCount = @($cases | Where-Object { $_.Network }).Count
    $verifiedServiceCount = @($results | Where-Object {
        if ($_.name -eq 'node-kosis-fixture') { $_.passed -and $_.serviceValidation -and $_.serviceValidation.serviceOk }
        else { $_.passed -and $_.payload -and $_.payload.PSObject.Properties.Name -contains 'serviceOk' -and $_.payload.serviceOk }
    }).Count
    $brokerObservations = @($observations | Where-Object { $_.Kind -eq 'broker' -and $_.ProcessId -eq $process.Id -and
        $_.ImagePath -ieq $HostExecutable -and $_.Sha256 -eq (Get-Sha256 $HostExecutable) -and $_.TokenQuerySucceeded -and $_.IsAppContainer -eq $false })
    $workerObservations = @($observations | Where-Object { $_.Kind -eq 'worker' -and $_.ParentProcessId -eq $process.Id -and
        $_.TokenQuerySucceeded -and $_.IsAppContainer -eq $true -and $null -eq $_.QueryError -and
        (($_.ImagePath -ieq $seed.pythonExecutable -and $_.Sha256 -eq $seed.pythonSha256) -or
         ($_.ImagePath -ieq $seed.nodeExecutable -and $_.Sha256 -eq $seed.nodeSha256)) })
    $independentProcessChecksPassed = $brokerObservations.Count -eq 1 -and $workerObservations.Count -eq $cases.Count -and
        @($workerObservations | Where-Object ImagePath -IEQ $seed.pythonExecutable).Count -eq 2 -and
        @($workerObservations | Where-Object ImagePath -IEQ $seed.nodeExecutable).Count -eq 5
    $summary = [ordered]@{recordedUtc=[DateTimeOffset]::UtcNow.ToString('O');host=$HostExecutable;hostSha256=Get-Sha256 $HostExecutable;
        hostProductVersion=(Get-Item -LiteralPath $HostExecutable).VersionInfo.ProductVersion;brokerExitCode=$process.ExitCode;
        allExecutionChecksPassed=(@($results | Where-Object { -not $_.passed }).Count -eq 0);
        allLiveServiceChecksPassed=($verifiedServiceCount -eq $networkCaseCount);
        independentProcessChecksPassed=[bool]$independentProcessChecksPassed;
        cases=$results}
    Write-Utf8 (Join-Path $EvidenceDirectory 'summary.json') ($summary | ConvertTo-Json -Depth 30)
    if ($process.ExitCode -ne 0 -or -not $summary.allExecutionChecksPassed) { throw 'At least one native AppContainer execution check failed; inspect summary.json and responses.jsonl.' }
    if (-not $summary.allLiveServiceChecksPassed) { throw 'Scripts executed, but at least one live public API check failed; inspect summary.json before making a service-support claim.' }
    if (-not $summary.independentProcessChecksPassed) { throw 'Independent Win32 process/token observations did not establish every expected AppContainer worker; inspect processes.json.' }
    Write-Output ('Native execution checks passed: ' + $results.Count + '; live public API checks passed.')
}
finally {
    if ($null -ne $processObserver) { $processObserver.Dispose() }
    if ($null -ne $process) {
        if ($processStarted -and -not $process.HasExited) { $process.Kill($true); $process.WaitForExit() }
        $process.Dispose()
    }
    if (-not $KeepFixtures -and (Test-Path -LiteralPath $fixtureDirectory)) {
        $checkedFixture = Assert-ChildPath $fixtureBase $fixtureDirectory
        Get-ChildItem -LiteralPath $checkedFixture -Force -Recurse | ForEach-Object { Assert-ChildPath $checkedFixture $_.FullName | Out-Null }
        Remove-Item -LiteralPath $checkedFixture -Recurse -Force
    }
}
