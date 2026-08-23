# TableCloth 릴리스 절차 (Runbook)

새 버전을 출시할 때 따르는 단계별 절차입니다. 핵심 흐름:

> 버전 bump → 태그 push(CI가 미서명 draft 생성) → `build.cmd --sign`(로컬 전체 서명) → `gh release upload --clobber` → `UNSIGNED` 마커 제거 후 Publish → winget 자동 PR 확인

관련 구성요소: [`.github/workflows/build.yml`](../.github/workflows/build.yml) (빌드+draft), [`build.cs`](../build.cs)/[`build.cmd`](../build.cmd) (로컬 빌드+서명), [`.github/workflows/winget_publish.yml`](../.github/workflows/winget_publish.yml) + [`tools/winget/submit-winget.cs`](../tools/winget/submit-winget.cs) (winget 자동 제출), [`Directory.Build.Props`](../Directory.Build.Props) (버전 단일 출처).

> **릴리스 채널(Retail/Preview):** 이 런북은 **Retail(안정)** 정식 릴리스 절차다. Avalonia+Native AOT 전환처럼 변화 폭이 큰
> 릴리스는 **Preview** 링으로 먼저 프리릴리스(prerelease)로 낸다 — GitHub 프리릴리스라 `/releases/latest`·winget·무설치
> 웹앱은 자동으로 Retail 만 가리키고, 사용자는 앱 옵션(미리 보기 탭 → 업데이트 채널)에서 미리 보기로 전환해 받는다.
> v1.20.7 부터 이 in-app 채널 토글이 탑재된다. 채널 설계·자산명·버전(`X.Y.Z-preview.N`)·CI 규칙은
> [RELEASE_CHANNELS.md](RELEASE_CHANNELS.md) 참조. Preview 게시 절차는 §8에서 다룬다.

---

## 0. 사전 조건 (매 릴리스)

- **SimplySign Desktop 로그인**(서명 세션 활성) — 코드 서명 인증서가 `CurrentUser\My`에 개인 키와 함께 있어야 함.
  - 확인: `Get-ChildItem Cert:\CurrentUser\My | ? { $_.Subject -like '*Jung Hyun Nam*' -and $_.HasPrivateKey }`
- ⚠️ **`signtool`이 PATH에 없을 수 있다.** 이 경우 `tools/sign-release.ps1`은 시작하자마자 실패한다.
  Velopack CLI에 동봉된 것을 쓰면 된다 — `~\.dotnet\tools\.store\vpk\<ver>\vpk\<ver>\vendor\signing\signtool.exe`.

  ```powershell
  $signtool = (Get-ChildItem "$env:USERPROFILE\.dotnet\tools\.store\vpk" -Recurse -Filter signtool.exe |
               Select-Object -First 1).FullName
  ```

- ⚠️ **NativeAOT 부트스트래퍼는 C++ 빌드 도구가 있어야 만들어진다**(Visual Studio의 *Desktop
  Development for C++*, arm64까지 내려면 *C++ ARM64 build tools*). 없으면 `build.cs`가
  `Platform linker not found` 경고와 함께 **조용히 건너뛰고**, `SporkBootstrap_*.exe` 4종이 로컬
  산출물에서 빠진다. 이 경우 릴리스에는 CI가 만든 **미서명본**이 그대로 남으므로 4단계에서 별도로
  서명해야 한다(§4-2).
- 저장소 시크릿 **`TABLECLOTH_GITHUB_PAT` = classic PAT + `public_repo` 스코프** (winget 제출용).
  - 선택: `delete_repo` — 제출 실패 시 wingetcreate가 자기 포크를 정리.
  - fine-grained PAT은 wingetcreate가 지원하지 않음.
- 최신 winget 수정(포크 동기화 등)이 `main`에 머지돼 있을 것 → 2단계에서 **main HEAD에 태깅**하면 자동 충족.

## 1. 버전 올리기

- [`Directory.Build.Props`](../Directory.Build.Props)의 `TableClothVersionMajor/Minor/Patch/Revision` 수정.
  - ⚠️ 태그 검증(`validate-version`)은 **3-part(Major.Minor.Patch)만** 비교한다. `Revision`(4번째)은 검증하지 않으며 파일명에만 쓰인다.
- 커밋 후 `git push origin main`.
  - 참고: 이 main 푸시도 build.yml을 한 번 돌리지만 **릴리스는 만들지 않는다**(릴리스 생성·버전 검증은 태그에서만 동작).

## 2. 태그 생성·푸시 → CI가 미서명 draft 생성

```bash
git tag vX.Y.Z        # 버전 올린 main HEAD 에
git push origin vX.Y.Z
```

- build.yml: `validate-version`(태그 == props 3-part) → x64/arm64 빌드 → **미서명 draft 릴리스 생성**(`UNSIGNED` 마커, 자동 릴리스 노트, SBOM, build attestation). 완료까지 약 20–30분.
- ⚠️ **`released` 이벤트는 "태그가 가리키는 커밋"을 체크아웃**해서 winget 스크립트를 실행한다(main HEAD가 아님). 따라서 태그는 반드시 최신 `tools/winget/submit-winget.cs` + 포크 동기화 수정(commit `997cd0f` 이후)을 **포함한 커밋**이어야 한다 → **main HEAD에 태깅하면 해결**. 옛 커밋에 태깅하면 winget 단계가 깨질 수 있다.

## 3. 로컬 전체 서명 빌드

작업 트리가 릴리스 버전(= 태그 커밋/main HEAD)인지 확인한 뒤, SimplySign 세션을 연 상태에서:

```powershell
$env:TABLECLOTH_SIGN_SUBJECT = 'Jung Hyun Nam'   # 필수 (또는 --sign-subject "<CN>")
.\build.cmd --sign
```

- ⚠️ `--sign`에 주체가 없거나(`TABLECLOTH_SIGN_SUBJECT`/`--sign-subject`) `CurrentUser\My`에 개인 키 인증서가 없으면 **빌드 전에 즉시 실패**한다(안전장치).
- ⚠️ **연속 릴리스 주의**: Velopack 은 `Releases` 폴더에 남은 이전 버전 산출물을 보고 델타를 만들어 버전을 섞는다. 새 릴리스 전에 `Releases\`(와 `publish\`)를 비우고 빌드한다.
- 결과: `Releases\Release\x64\`, `Releases\Release\arm64\` 에 **TableCloth 와 Spork 두 앱**이 함께 —
  - 서명된 `TableCloth_<4파트버전>_Release_<arch>.exe` / `Spork_<버전>_Release_<arch>.exe` (+ 각 `_Portable.zip`)
  - **+ Velopack 메타데이터**(`.nupkg`, `RELEASES-*`, `releases.*.json`, `assets.*.json`) — Spork 는 채널 `spork-<arch>` 라 TableCloth(채널 `<arch>`)와 이름이 겹치지 않는다.
  - 서명 범위: 앱 바이너리 + `Update.exe` + `Setup.exe` (Release 구성만).
- ⚠️ 빌드 로그 끝에서 **`(skip) bootstrapper exe not found`** 가 있는지 확인한다. 있으면 §0의 C++
  빌드 도구가 없다는 뜻이고, `SporkBootstrap_*.exe` 는 §4-2에서 따로 서명해야 한다. 이 경고는
  빌드를 실패시키지 않으므로(종료 코드 0) 놓치기 쉽다.

### 3-1. 서명 호스트와 아키텍처 (x64 PC 에서 arm64 를 서명해도 되는가)

**된다. 그리고 이미 매 릴리스 그렇게 하고 있다.** Authenticode 는 PE 파일의 바이트를 해시해 인증서
테이블에 서명 블록을 덧붙이는 작업이고, signtool 은 대상 바이너리를 실행하지 않는다. PE 헤더의
`Machine` 필드는 해시 대상 바이트 중 하나일 뿐이라 서명 절차와 무관하다.

실측 근거(2026-08-01) — 1.20.9 arm64 패키지 안의 앱 바이너리는 ARM64 네이티브인데 서명이 유효하고,
그 서명은 x64 개발 PC 의 `build.cmd --sign` 이 붙인 것이다. 같은 방법으로 언제든 재확인할 수 있다:

```powershell
# 배포된 arm64 패키지에서 앱 바이너리를 꺼내 PE 아키텍처와 서명을 함께 확인
Add-Type -AssemblyName System.IO.Compression.FileSystem
$a = [System.IO.Compression.ZipFile]::OpenRead('Releases\Release\arm64\Spork_<버전>_Release_arm64_Portable.zip')
$e = $a.Entries | Where-Object { $_.FullName -eq 'current/Spork.exe' }
[System.IO.Compression.ZipFileExtensions]::ExtractToFile($e, "$env:TEMP\check.exe", $true); $a.Dispose()
$fs = [IO.File]::OpenRead("$env:TEMP\check.exe"); $br = [IO.BinaryReader]::new($fs)
$fs.Position = 0x3C; $fs.Position = $br.ReadInt32() + 4
'{0:X}' -f $br.ReadUInt16()          # AA64 = ARM64, 8664 = x64, 14C = x86
$br.Close(); Get-AuthenticodeSignature "$env:TEMP\check.exe" | Select-Object Status, SignerCertificate
```

- **패키징도 아키텍처 중립이다.** Velopack 의 `Setup.exe` 와 패키지 내부 `Update.exe` 는 x64/arm64
  패키지 **양쪽 모두 x86 범용 스텁**이라(PE 헤더 확인) 패킹한 호스트의 아키텍처가 산출물에 새지 않는다.
  `build.cs` 가 `vpk pack` 에 `--runtime` 을 넘기지 않고 `--channel` 로만 arch 를 구분하는 것도 이 때문에
  문제가 되지 않는다.
- **반대 방향(arm64 PC 에서 x64 서명)** 도 원리는 같지만, SimplySign 가상 스마트카드 CSP/미들웨어가
  ARM64 Windows 에뮬레이션에서 정상 동작하는지는 **미검증**이다. 서명 호스트는 x64 하나로 고정하는 편이
  안전하다(서명 의미론이 아니라 드라이버 호환성 문제).
- **자유롭지 않은 것은 빌드다.** Native AOT 는 대상 아키텍처 툴체인이 필요하므로 크로스로 자유로운 것은
  서명·패키징뿐이다. 이 구분이 프리뷰 레인(AOT)에서 CI 산출물에 의존해야 하는 이유다.

## 4. 서명 자산 업로드 (CI 미서명본 교체)

올릴 대상은 **두 앱(TableCloth + Spork)의 설치 관리자 + Portable + Velopack 메타데이터 전체**다.
파일명이 CI와 동일한 4-part 버전이라 `--clobber`가 정확히 **교체**한다(중복 추가가 아님).
nupkg/메타데이터도 로컬 서명본으로 교체되어 설치 관리자와 일관된다. SBOM은 CI 산출물이 그대로
유지된다(하이브리드).

### 4-1. 파일별 업로드 + 대조 검증

> ⚠️ **한 줄 glob 업로드(`gh release upload ... x64\* arm64\* --clobber`)를 쓰지 말 것.**
> v1.20.9 에서 대량 업로드가 중간에 `HTTP 404` 로 끊겼는데, `--clobber` 는 **먼저 기존 자산을 지우고**
> 올리기 때문에 **x64 설치 관리자와 nupkg 가 아예 사라진 채 남았고**, arm64 7개는 CI 미서명본이
> 그대로 유지됐다. 명령은 부분 성공으로 끝나 조용히 넘어간다.

```powershell
$tag = 'vX.Y.Z'
foreach ($f in (Get-ChildItem "Releases\Release\x64\*","Releases\Release\arm64\*" -File)) {
  $ok = $false
  for ($i = 1; $i -le 3 -and -not $ok; $i++) {
    gh release upload $tag $f.FullName --clobber 2>&1 | Out-Null
    if ($LASTEXITCODE -eq 0) { $ok = $true } else { Start-Sleep -Seconds 5 }
  }
  '{0,-50} {1}' -f $f.Name, $(if ($ok) { 'OK' } else { 'FAILED' })
}
```

업로드 후 **반드시 크기 대조**로 교체 여부를 확인한다. 로컬 서명본은 CI 미서명본보다 크므로,
크기가 같으면 교체가 안 된 것이다.

```powershell
$assets = (gh release view $tag --json assets | ConvertFrom-Json).assets
$local  = @(Get-ChildItem "Releases\Release\x64\*","Releases\Release\arm64\*" -File) |
          Select-Object -ExpandProperty Name -Unique
$bad = foreach ($n in $local) {
  $a = $assets | Where-Object name -eq $n
  $f = Get-ChildItem "Releases\Release\*\$n" -File | Select-Object -First 1
  if (-not $a)                   { "MISSING: $n" }
  elseif ($a.size -ne $f.Length) { "SIZE MISMATCH: $n (release=$($a.size) local=$($f.Length))" }
}
if ($bad) { $bad } else { "OK - 로컬 산출물 $($local.Count)개 전부 일치" }
```

### 4-2. CI 산출 부트스트래퍼 서명 (로컬 빌드가 건너뛴 경우)

§0/§3에서 `(skip) bootstrapper exe not found` 를 만났다면, `SporkBootstrap_*.exe` 4종은 CI 미서명본이다.
내려받아 서명하고 되올린다.

```powershell
$work = "$env:TEMP\tc-sign-bootstrap"
New-Item -ItemType Directory -Path $work -Force | Out-Null
Set-Location $work
gh release download $tag --repo yourtablecloth/TableCloth --pattern "SporkBootstrap*.exe"
& $signtool sign /n "Jung Hyun Nam" /tr "http://time.certum.pl" /td sha256 /fd sha256 (Get-ChildItem *.exe).FullName
Get-ChildItem *.exe | ForEach-Object { gh release upload $tag $_.FullName --repo yourtablecloth/TableCloth --clobber }
```

### 4-3. 게시 전 서명 전수 확인

```powershell
$verify = "$env:TEMP\tc-verify-$tag"
New-Item -ItemType Directory -Path $verify -Force | Out-Null
Set-Location $verify
gh release download $tag --repo yourtablecloth/TableCloth --pattern "*.exe"
Get-ChildItem *.exe | ForEach-Object {
  '{0,-48} {1}' -f $_.Name, (Get-AuthenticodeSignature $_.FullName).Status
}
```

**모든 항목이 `Valid` 여야 한다.** 하나라도 `NotSigned` 면 게시하지 않는다.
(v1.20.9 기준 대상은 8개 — TableCloth/Spork 설치 관리자 각 2 + SporkBootstrap 4.)

## 5. 게시

- **§4-3의 서명 전수 확인을 통과했는지 먼저 볼 것.**
- 릴리스 노트에서 **`UNSIGNED` 마커 블록 제거**. 자동 생성 노트는 커밋 목록이라, UI 변화가 없는
  유지 보수 릴리스일수록 **맨 앞에 사용자용 요약 문단을 덧붙이는 편**이 좋다(v1.20.9 참고).
- draft 해제(Publish) — prerelease가 아니므로 → **`released` 이벤트 발생**.

## 6. winget 자동 제출 (자동)

- winget_publish.yml(`released` 트리거)이 자동 실행: 포크(`rkttu/winget-pkgs`) 동기화 → `wingetcreate update --submit` → **microsoft/winget-pkgs PR** 생성.
- Actions에서 *Submit to winget-pkgs repo* 성공 + PR 생성 확인.
- **복구 경로**: 자동 실행이 실패/누락되면 Actions → *Submit to winget-pkgs repo* → **Run workflow** → `release_tag`에 `vX.Y.Z` 입력(수동 재실행).
- ⚠️ 멱등성은 "winget master에 버전 폴더 존재 여부"로 판단한다. PR이 **머지되기 전**에는 폴더가 없으므로, 재게시/재실행 시 **같은 버전의 중복 PR**이 생긴다. 첫 PR이 머지될 때까지 `released` 재발생을 피하거나 중복 PR을 닫을 것.

## 7. 출시 후

- (선택) SNS/닷넷데브 포럼에 릴리스 소식 공유.
- winget PR이 Microsoft 측에서 검증·머지되는지 모니터링.

---

## 8. 프리뷰(Preview) 릴리스 — CI 게시 + 로컬 서명

프리뷰는 리테일 흐름(§3 의 로컬 전체 빌드)을 쓸 수 없다. **arm64 Native AOT 는 x64 개발 PC 에서 빌드할 수
없기 때문**이다. 대신 [`preview.yml`](../.github/workflows/preview.yml) 이 x64 는 `windows-latest`, arm64 는
**`windows-11-arm` 네이티브 러너**에서 빌드하고, **로컬은 그 산출물을 받아 다시 pack 하면서 서명만** 한다.
서명도 패키징도 아키텍처 중립이므로(§3-1) x64 PC 에서 arm64 패키지를 만들 수 있다 — 로컬에서 못 하는 것은
arm64 **빌드**뿐이다.

```text
CI (windows-latest / windows-11-arm)        로컬 x64 PC
  dotnet publish -r win-<arch>
    → publish\Release\win-<arch>            PublishPayload-<arch> 아티팩트 다운로드
    → publish\spork\Release\win-<arch>  ──▶   → 리포 루트의 publish\ 로 전개
  vpk pack (미서명) → draft 프리릴리스       build.cmd --skip-build --sign --preview
                                              → 서명된 자산으로 draft 자산 교체 → 게시
```

### 8-1. 태그 푸시 → CI

```bash
git tag v1.21.0-preview.N        # -preview.N 접미사 필수
git push origin v1.21.0-preview.N
```

`preview.yml` 이 x64/arm64 를 빌드해 **draft 프리릴리스**(미서명)를 만들고, 아티팩트 두 종류를 남긴다.

| 아티팩트 | 내용 | 용도 |
| --- | --- | --- |
| `Preview-<arch>` | `releases/*` (미서명 패키지) | draft 릴리스 자산 + 폴백 |
| `PublishPayload-<arch>` | `publish/**` (pack 이전 게시 산출물, pdb 제외) | **로컬 서명 인계용** |

### 8-2. 게시 산출물 내려받아 전개

리포 루트에서 (SimplySign 세션은 아직 필요 없다):

```powershell
# 이전 잔여물 제거 — Velopack 이 남은 산출물로 델타를 만들어 버전을 섞는다(§3 주의와 동일).
Remove-Item publish, Releases -Recurse -Force -ErrorAction SilentlyContinue

$run = (gh run list --workflow preview.yml --limit 1 --json databaseId | ConvertFrom-Json).databaseId
gh run download $run --pattern 'PublishPayload-*' --dir .artifacts

# 아티팩트 루트는 publish\ 의 *내용물*이다(actions/upload-artifact 가 매칭된 파일들의 공통 조상을
# 루트로 잡으므로 publish\ 접두사가 빠진다 — v1.21.0-preview.2 에서 실측). 즉 아티팩트 안은
# Release\win-<arch>\... 와 spork\Release\win-<arch>\... 이므로 publish\ 아래로 부어 넣는다.
New-Item -ItemType Directory publish -Force | Out-Null
foreach ($a in (Get-ChildItem .artifacts -Directory)) {
  foreach ($child in (Get-ChildItem $a.FullName)) { Copy-Item $child.FullName publish -Recurse -Force }
}
Get-ChildItem publish -Directory -Recurse | Where-Object Name -like 'win-*' |
  Select-Object -ExpandProperty FullName
```

기대되는 네 폴더가 모두 있어야 한다. **한쪽 arch 만 있으면 실패한다** — `build.cs` 는 x64 → arm64 순서로
돌면서 각 arch 의 TableCloth·Spork 를 모두 pack 하므로, x64 가 없으면 거기서 중단되고 arm64 는 시도조차
하지 않는다(실측 확인).

```text
publish\Release\win-x64          publish\spork\Release\win-x64
publish\Release\win-arm64        publish\spork\Release\win-arm64
```

### 8-3. 로컬 서명 패키징

SimplySign 세션을 연 상태에서:

```powershell
$env:TABLECLOTH_SIGN_SUBJECT = 'Jung Hyun Nam'
.\build.cmd --skip-build --sign --preview --preview-number N   # N = 태그의 -preview.N 과 동일
```

- `--skip-build` 가 빌드·게시를 건너뛰고 위 `publish\` 폴더만으로 pack 한다(실측: 빌드 로그 없이 곧장
  pack, 4개 패키지 ~35초).
- `--preview` 는 채널(`preview-<arch>` / `spork-preview-<arch>`)·자산명(`-Preview`)·버전(`X.Y.Z-preview.N`)을
  프리뷰 규칙으로 맞추고, 무설치 고정 URL 별칭은 만들지 않는다(Retail 전용 계약).
- ⚠️ `--preview-number` 가 태그와 어긋나면 **조용히 다른 버전**이 나온다. 릴리스 자산명과 대조할 것.
- 결과는 §3 과 같은 자리(`Releases\Release\<arch>\`)에 나오며, 서명 범위도 리테일과 같다
  (앱 바이너리 + `Update.exe` + `Setup.exe`).

### 8-4. 업로드 · 게시

§4-1 의 파일별 업로드 + 크기 대조 절차를 그대로 쓴다(태그만 프리뷰 태그로).

⚠️ **게시 전에 릴리스 노트의 `UNSIGNED — DO NOT PUBLISH` 블록을 제거한다.** 리테일(§5)과 같은 절차인데
프리뷰 절에는 빠져 있어 v1.21.0-preview.2 를 그 블록이 남은 채 게시했고, 사후에 고쳐야 했다(자산은
서명돼 있었고 노트만 틀렸다). 프리뷰 노트는 `> [!NOTE]` 로 시작하는 미리 보기 배너부터 시작해야 한다.

```powershell
$tag  = 'v1.21.0-preview.N'
$body = (gh release view $tag --json body | ConvertFrom-Json).body
$idx  = $body.IndexOf('> [!NOTE]')      # 미리 보기 배너 앞(=UNSIGNED 블록)을 잘라낸다
$tmp  = Join-Path $env:TEMP 'notes.md'
[IO.File]::WriteAllText($tmp, $body.Substring($idx), (New-Object Text.UTF8Encoding $false))
gh release edit $tag --notes-file $tmp
```

게시는 **prerelease 를 유지**해야 한다.

```powershell
gh release edit v1.21.0-preview.N --draft=false --prerelease
```

`prereleased` 이벤트만 발생하므로 winget·discord 는 발동하지 않는다. `--latest` 를 붙이지 말 것.

> **폴백(설치 관리자 외피만 서명):** 위 경로가 막히면 draft 의 `Setup.exe` 4개만 `sign-release.ps1` 로
> 서명할 수 있다. v1.21.0-preview.1 이 이 방식이었고, 이 경우 **nupkg 내부 앱 바이너리와 `Portable.zip`
> 내용물은 미서명으로 남는다**(nupkg 는 Velopack RELEASES 해시에 묶여 있어 사후 재서명 불가). 어디까지나
> 폴백이며, 정상 경로는 8-2/8-3 이다.

---

## 대안 / 참고

- **외부 설치 관리자만 서명**: [`tools/sign-release.ps1`](../tools/sign-release.ps1) `-Tag vX.Y.Z` → 릴리스의 **모든 `.exe`(x64+arm64 Setup)**를 내려받아 signtool로 서명·재업로드한다. 단 패키지 내부 앱 바이너리/`Update.exe`와 `Portable.zip`은 서명되지 않는다(설치 관리자 외피만 서명). 프리뷰 레인이 현재 이 방식을 쓴다(§8).
- 릴리스 바이너리는 로컬 서명 빌드 산출물이고 SBOM/노트는 CI 빌드 기준인 **하이브리드** 구조다. 장기적으로 CI 클라우드 서명으로 전환할 수 있다(자동 메모 `code_signing_approach` 참고: Azure Artifact Signing은 한국 개인 가입 제약, SSL.com eSigner는 유료 무인 옵션).
