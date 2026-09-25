# TableCloth 릴리스 실행 절차

TableCloth의 Retail과 Preview 릴리스는 태그 Push로 CI 빌드를 시작하고 미서명 Draft를 생성합니다. 릴리스 담당자는 이 PC의 SimplySign 인증서로 CI 게시 산출물을 다시 패키징하고 서명한 뒤 Draft 자산을 교체합니다.

이 문서는 릴리스 유형 선택부터 태그 검증, 로컬 서명, 게시와 후속 자동화 확인까지 다룹니다. 브랜치 수명 주기는 [BRANCHING.md](BRANCHING.md), 채널별 동작은 [RELEASE_CHANNELS.md](RELEASE_CHANNELS.md)에서 설명합니다.

> 기준일: 2026년 9월 26일. 현재 Retail은 v1.21.1입니다. v1.22.0 Preview는 .NET 11 RC1 SDK `11.0.100-rc.1.26425.128`로 빌드합니다. x64와 arm64 Native AOT 빌드는 GitHub Actions의 각 네이티브 러너가 담당하고 로컬 x64 PC는 두 아키텍처의 패키징과 Authenticode 서명을 담당합니다.

## 릴리스 유형 선택

| 목적 | 소스 | 버전과 태그 | GitHub 상태 | 후속 자동화 |
| --- | --- | --- | --- | --- |
| 현재 정식 버전 패치 | `main` | `v1.21.1` | 정식 Release | WinGet, Discord |
| 다음 버전 선행 검증 | `develop` | `v1.22.0-preview.1` | Prerelease | 없음 |
| 다음 버전 정식 승격 | `main`에 병합한 `develop` | `v1.22.0` | 정식 Release | WinGet, Discord |

Retail 태그는 [`build.yml`](../.github/workflows/build.yml)을 실행하고 Preview 태그는 [`preview.yml`](../.github/workflows/preview.yml)을 실행합니다. 두 워크플로 모두 x64와 arm64 빌드, 미서명 Draft와 `PublishPayload-<arch>` 아티팩트를 생성합니다.

## 공통 사전 조건

릴리스 작업을 시작하기 전에 다음 상태를 확인합니다.

- 깨끗한 Git 작업 트리와 최신 원격 태그
- 초기화한 `external/TableClothCatalog` 서브모듈
- GitHub CLI 로그인과 저장소 Release 쓰기 권한
- SimplySign Desktop 로그인과 활성 서명 세션
- `CurrentUser\My`에 개인 키를 포함한 코드 서명 인증서
- Velopack CLI가 제공하는 `signtool.exe` 또는 PATH에서 찾을 수 있는 SignTool
- x64와 arm64 GitHub Actions 러너의 가용 상태

SimplySign 인증서는 다음 방식으로 확인할 수 있습니다.

```powershell
Get-ChildItem Cert:\CurrentUser\My |
  Where-Object HasPrivateKey |
  Select-Object Subject, Thumbprint, NotAfter
```

인증서 개인 키나 PFX를 저장소 또는 CI로 내보내지 않습니다. 서명은 SimplySign 세션이 열린 로컬 PC에서만 수행합니다.

## 버전과 태그 준비

버전의 단일 출처인 [`Directory.Build.Props`](../Directory.Build.Props)에서 `Major`, `Minor`, `Patch`, `Revision`을 설정합니다. `Revision`은 `0`으로 유지하고 태그에는 세 자리 SemVer만 사용합니다.

Retail 태그는 새 `origin/main` HEAD와 정확히 일치해야 합니다. 태그를 만들기 전에 다음 조건을 검사합니다.

```powershell
git fetch origin --prune --tags
git status --short --branch

$head = (git rev-parse HEAD).Trim()
$main = (git rev-parse origin/main).Trim()
if ($head -ne $main) { throw 'Retail tag target must equal origin/main HEAD.' }
```

Preview 태그는 `vX.Y.Z-preview.N` 형식을 사용하며 `origin/develop` 이력에 포함된 커밋만 가리킵니다. `N`은 1 이상의 정수이고 이미 사용한 번호를 재사용하지 않습니다.

```powershell
$tag = 'v1.22.0-preview.1'
if ($tag -notmatch '^v\d+\.\d+\.\d+-preview\.[1-9]\d*$') {
  throw 'Invalid Preview tag format.'
}

git merge-base --is-ancestor HEAD origin/develop
if ($LASTEXITCODE -ne 0) { throw 'Preview tag target is not contained in origin/develop.' }
```

태그의 버전 코어와 `Directory.Build.Props`가 일치하는지 검토한 뒤 태그를 Push합니다.

```powershell
git tag $tag
git push origin $tag
```

현재 CI는 태그와 버전 파일을 비교하지만 브랜치 소속을 강제하지 않습니다. 위 브랜치 검사를 생략하지 않습니다.

## CI Draft와 게시 산출물

태그 Push 후 해당 워크플로 실행이 성공할 때까지 기다립니다. 실패한 Job이 있으면 Draft를 게시하지 않고 원인을 수정한 새 커밋과 새 태그로 다시 진행합니다. 이미 외부에 Push한 태그를 다른 커밋으로 이동하지 않습니다.

CI가 남기는 아티팩트는 다음과 같습니다.

| 아티팩트 | 내용 | 사용처 |
| --- | --- | --- |
| `Velopack-<arch>-Release` 또는 `Preview-<arch>` | 미서명 패키지와 메타데이터 | Draft의 최초 자산 |
| `PublishPayload-<arch>` | 패키징 전 Native AOT 게시 산출물 | 로컬 전체 서명 |
| `SBOM-<arch>` | SPDX SBOM | Draft와 최종 Release |

로컬 서명에는 `PublishPayload-x64`와 `PublishPayload-arm64`가 모두 필요합니다. 한쪽 아키텍처가 빠진 상태에서는 패키징을 시작하지 않습니다.

## 로컬 SimplySign 전체 서명

저장소 루트를 확인하고 이전 패키징 산출물만 정리한 뒤 CI 아티팩트를 내려받습니다. 다음 예시는 Preview 워크플로를 사용합니다. Retail에서는 워크플로 이름을 `build.yml`로 바꿉니다.

```powershell
$repo = (git rev-parse --show-toplevel).Trim()
Set-Location $repo

$tag = 'v1.22.0-preview.1'
$workflow = 'preview.yml'
$artifactRoot = Join-Path $env:TEMP "tablecloth-$tag-artifacts"

Remove-Item -LiteralPath (Join-Path $repo 'publish') -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath (Join-Path $repo 'Releases') -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $artifactRoot -Recurse -Force -ErrorAction SilentlyContinue

$run = (gh run list --workflow $workflow --branch $tag --limit 1 --json databaseId |
  ConvertFrom-Json).databaseId
if (-not $run) { throw "No workflow run found for $tag." }

gh run download $run --pattern 'PublishPayload-*' --dir $artifactRoot
New-Item -ItemType Directory -Path (Join-Path $repo 'publish') -Force | Out-Null
foreach ($payload in Get-ChildItem -LiteralPath $artifactRoot -Directory) {
  foreach ($item in Get-ChildItem -LiteralPath $payload.FullName) {
    Copy-Item -LiteralPath $item.FullName -Destination (Join-Path $repo 'publish') -Recurse -Force
  }
}
```

다음 네 폴더가 모두 존재하는지 확인합니다.

```text
publish\Release\win-x64
publish\Release\win-arm64
publish\spork\Release\win-x64
publish\spork\Release\win-arm64
```

SimplySign 세션을 연 뒤 인증서 주체를 지정하여 다시 패키징합니다. Preview 번호는 태그와 같은 값을 명시합니다.

```powershell
$env:TABLECLOTH_SIGN_SUBJECT = '<certificate subject>'

# Retail
.\build.cmd --skip-build --sign

# Preview
.\build.cmd --skip-build --sign --preview --preview-number 1
```

`--skip-build`는 CI가 생성한 Native AOT 산출물을 사용합니다. `build.cs`는 TableCloth, 함께 배포하는 TableClothCli, Spork의 앱 바이너리와 `Update.exe`, `Setup.exe`를 서명하면서 x64와 arm64 패키지를 다시 만듭니다. [Velopack의 기본 서명 동작](https://github.com/velopack/velopack.docs/discussions/15)은 패키지 안의 PE 실행 파일에도 적용됩니다. Authenticode는 대상 실행 파일을 실행하지 않으므로 x64 호스트에서 arm64 바이너리를 서명할 수 있습니다.

Preview의 `--preview-number`가 태그와 다르면 패키지 버전도 달라집니다. 패키징 로그와 생성한 메타데이터에서 전체 SemVer를 대조합니다.

## Draft 자산 교체와 서명 검증

로컬 결과는 `Releases\Release\x64`와 `Releases\Release\arm64`에 생성됩니다. 파일별로 업로드하고 각 명령의 성공 여부를 확인합니다. 한 번에 여러 Glob을 넘기면 일부 자산만 교체된 상태를 놓칠 수 있습니다.

```powershell
$tag = 'v1.22.0-preview.1'
$files = Get-ChildItem 'Releases\Release\x64\*','Releases\Release\arm64\*' -File

foreach ($file in $files) {
  $uploaded = $false
  for ($attempt = 1; $attempt -le 3 -and -not $uploaded; $attempt++) {
    gh release upload $tag $file.FullName --clobber
    $uploaded = $LASTEXITCODE -eq 0
    if (-not $uploaded) { Start-Sleep -Seconds 5 }
  }
  if (-not $uploaded) { throw "Upload failed: $($file.Name)" }
}
```

업로드 뒤에는 원격 자산의 이름과 크기를 로컬 결과와 비교합니다.

```powershell
$remote = (gh release view $tag --json assets | ConvertFrom-Json).assets
$mismatch = foreach ($file in $files) {
  $asset = $remote | Where-Object name -eq $file.Name
  if (-not $asset) { "Missing: $($file.Name)" }
  elseif ($asset.size -ne $file.Length) { "Size mismatch: $($file.Name)" }
}
if ($mismatch) { throw ($mismatch -join [Environment]::NewLine) }
```

최종 검증에서는 Draft의 모든 `.exe` 자산을 다시 내려받아 Authenticode 상태가 `Valid`인지 확인합니다. Portable ZIP 내부의 `TableCloth.exe`, `TableClothCli.exe`, `Spork.exe`도 풀어서 같은 검사를 수행합니다. x64와 arm64 자산, Velopack 채널 메타데이터와 SBOM이 모두 있는지도 함께 확인합니다.

서명 검증이나 자산 대조가 하나라도 실패하면 Draft를 유지합니다. CI 미서명 자산을 남겨 둔 채 일부 파일만 게시하지 않습니다.

## 릴리스 노트와 게시

릴리스 노트 첫 부분에서 `UNSIGNED` 경고 블록을 제거하고 사용자 관점의 변경 요약을 추가합니다. Preview에는 불안정 가능성과 Retail 복귀 절차를 남깁니다. 정식 버전에는 주요 변경, 호환성 영향과 알려진 문제를 기록합니다.

Preview는 Prerelease 상태를 유지하여 게시합니다.

```powershell
gh release edit v1.22.0-preview.1 --draft=false --prerelease
```

Retail은 Prerelease가 아닌 정식 Release로 게시하고 최신 정식 버전으로 지정합니다.

```powershell
gh release edit v1.21.1 --draft=false --latest
```

게시 명령은 외부 사용자에게 자산을 노출합니다. 사용자가 릴리스 게시를 요청한 범위에서만 실행합니다. Draft 생성이나 서명까지만 요청한 경우에는 검증 결과와 남은 단계를 보고하고 Draft를 유지합니다.

## 정식 게시 후 자동화

Retail 정식 게시에서는 `release: released` 이벤트가 다음 워크플로를 시작합니다.

- [`winget_publish.yml`](../.github/workflows/winget_publish.yml): `microsoft/winget-pkgs` 업데이트 Pull Request 생성
- [`discord_release.yml`](../.github/workflows/discord_release.yml): Discord 출시 공지 게시

두 워크플로의 실행 성공과 실제 외부 결과를 각각 확인합니다. 워크플로가 시작됐다는 사실만으로 WinGet Pull Request나 Discord 메시지가 생성됐다고 판단하지 않습니다.

Preview 게시에서는 두 워크플로가 실행되지 않아야 합니다. Preview를 `/releases/latest`로 지정하지 않고 WinGet에 수동 제출하지 않습니다.

## 출시 후 검증과 브랜치 정리

릴리스 게시 후 다음 결과를 기록합니다.

1. GitHub Release의 Draft와 Prerelease 상태를 확인합니다.
2. 태그 커밋과 대상 브랜치의 관계를 다시 확인합니다.
3. x64와 arm64 설치 관리자 및 Portable 자산을 확인합니다.
4. 서명 상태와 Velopack 채널 메타데이터를 확인합니다.
5. Retail에서는 WinGet Pull Request와 Discord 공지를 확인합니다.
6. 핫픽스에서는 수정 커밋을 `develop`으로 순방향 전파합니다.
7. 병합과 전파를 확인한 뒤 임시 브랜치 정리 여부를 결정합니다.

원격 브랜치를 삭제하기 전에 병합 커밋과 필요한 태그가 원격에 존재하는지 확인합니다. 이전 Minor 버전의 유지보수 계획이 남아 있다면 해당 브랜치를 삭제하지 않습니다.

## 실패 복구 경로

CI 빌드가 실패하면 같은 태그를 이동하지 않고 수정 커밋 뒤에 새 Preview 번호나 새 Patch 버전을 사용합니다. 아직 Push하지 않은 로컬 태그는 원인을 수정한 뒤 다시 만들 수 있습니다.

자산 업로드가 부분 실패하면 Draft를 유지하고 로컬 파일 목록과 원격 자산을 다시 대조합니다. `--clobber`가 기존 자산을 먼저 제거할 수 있으므로 실패한 파일만 다시 올린 뒤 전체 목록을 검증합니다.

WinGet 자동 제출이 실패하면 Actions의 `Submit to winget-pkgs repo` 워크플로를 `release_tag` 입력과 함께 수동 실행할 수 있습니다. 기존 Pull Request가 아직 병합되지 않았다면 중복 제출 여부를 먼저 확인합니다.
