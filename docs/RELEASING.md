# TableCloth 릴리스 절차 (Runbook)

새 버전을 출시할 때 따르는 단계별 절차입니다. 핵심 흐름:

> 버전 bump → 태그 push(CI가 미서명 draft 생성) → `build.cmd --sign`(로컬 전체 서명) → `gh release upload --clobber` → `UNSIGNED` 마커 제거 후 Publish → winget 자동 PR 확인

관련 구성요소: [`.github/workflows/build.yml`](../.github/workflows/build.yml) (빌드+draft), [`build.cs`](../build.cs)/[`build.cmd`](../build.cmd) (로컬 빌드+서명), [`.github/workflows/winget_publish.yml`](../.github/workflows/winget_publish.yml) + [`tools/winget/submit-winget.cs`](../tools/winget/submit-winget.cs) (winget 자동 제출), [`Directory.Build.Props`](../Directory.Build.Props) (버전 단일 출처).

---

## 0. 사전 조건 (매 릴리스)

- **SimplySign Desktop 로그인**(서명 세션 활성) — 코드 서명 인증서가 `CurrentUser\My`에 개인 키와 함께 있어야 함.
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

```powershell
gh release upload vX.Y.Z Releases\Release\x64\* Releases\Release\arm64\* --clobber
```

- 이 glob은 **두 앱(TableCloth + Spork)의 설치 관리자 + Portable + Velopack 메타데이터 전체**를 올린다. 파일명이 CI와 동일한 4-part 버전이라 `--clobber`가 정확히 **교체**한다(중복 추가가 아님). nupkg/메타데이터도 로컬 서명본으로 교체되어 설치 관리자와 일관된다.
- SBOM은 CI 빌드 산출물이 그대로 유지된다(하이브리드).

## 5. 게시

- 릴리스 노트에서 **`UNSIGNED` 마커 블록 제거**.
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

# 아티팩트는 publish\... 구조를 그대로 담고 있다. 두 arch 를 한 트리로 합친다.
Get-ChildItem .artifacts -Directory | ForEach-Object {
  Copy-Item "$($_.FullName)\publish" . -Recurse -Force
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

§4-1 의 파일별 업로드 + 크기 대조 절차를 그대로 쓴다(태그만 프리뷰 태그로). 게시는 **prerelease 를
유지**해야 한다.

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
