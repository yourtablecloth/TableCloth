# 파라미터화된 Express `.wsb` 스펙 (Parameterized WSB Spec)

> 상태: **확정 (2026-07-03)**. 빠른 실행 웹앱, MCP 서버, macOS(macSandbox) 세 소비자가 공유하는
> 정규 계약. 핵심 설계(설계 B, `{arch}` 토큰, 사이트 위치 인자, 체크섬 맵)는 확정됐고, 소비자별
> 세부(§9)와 추후 결정(§12: 웹앱 호스팅, MCP 전송)만 각 트랙 착수 시 확정한다.
> 관련 배경: [PORTABLE_MODE2_TODO.md](PORTABLE_MODE2_TODO.md) (모드 2, 무설치 코어, 자산명 계약).
> 부트스트랩 GUI화: [EXPRESS_BOOTSTRAPPER_DESIGN.md](EXPRESS_BOOTSTRAPPER_DESIGN.md) (§4의 다운로드 이후
> 단계를 Win32/GDI + NativeAOT exe로 위임, `.ps1`은 shim으로 축소).
>
> **간소화 (2026-07-05):** 런처가 자기 자신을 가리키는 **고정 URL**과 받을 Spork zip의 **고정 URL + API
> 폴백**을 내장하면서 `.wsb`가 크게 줄었다(플레이스홀더 4 → 1, 호스팅 ps1 불필요). 아래
> **"간소화된 기본형"**이 권장 기본이며, §3~§4의 완전 파라미터화형은 버전 핀/오프라인/사설 호스팅용으로 보존한다.

## 0. 목적과 범위

호스트 TableCloth 설치 없이, **하나의 파라미터화된 `.wsb` + 정적 부트스트랩**으로 단독 Spork를 띄우는
"Express(빠른 실행) 모드"의 정규 계약을 정의한다. 이 계약은 **세 소비자**가 공유한다.

| 소비자 | 하는 일 | 리포 스코프 |
| --- | --- | --- |
| **① 빠른 실행 웹앱** (yourtablecloth.app) | 최신 릴리스 resolve → 플레이스홀더 치환 → `.wsb`+부트스트랩 HTTPS 호스팅 | 스코프 밖(웹앱) |
| **② 독립형 MCP 서버** | 카탈로그 조회 → 사이트별 `.wsb` 로컬 생성 → 러너 실행 | 리포 내(신규) |
| **③ macOS (macSandbox)** | 동일 `.wsb`를 `MacSandbox`로 실행 (Apple Silicon) | 리포 내(문서, 부트스트랩) + macSandbox 리포 |

**이 문서가 소유하는 것:** `.wsb` 템플릿 형태, 플레이스홀더 계약, 부트스트랩 인자 계약, 아키텍처/사이트
파라미터 처리, 크로스플랫폼 러너 계약, 신뢰 모델.
**이 문서가 소유하지 않는 것:** 최신 릴리스 태그 → zip URL resolve, 실제 HTTPS 호스팅/치환(=웹앱),
MCP 전송 방식(별도 결정), macSandbox 내부 구현.

## 0.5 간소화된 기본형 (2026-07-05, 무설치 런처 이후) — 권장

[무설치 런처](EXPRESS_BOOTSTRAPPER_DESIGN.md)가 (a) 자기 자신을 가리키는 **고정 URL**(GitHub
`latest/download` 버전프리 별칭)과 (b) 받을 Spork zip의 **고정 URL + GitHub API 폴백**을 내장하면서, 이
스펙의 파라미터 대부분이 **불필요**해졌다. `.wsb`는 아래처럼 크게 줄어든다.

| 항목 | 완전 파라미터화형(§3.2, 아래 보존) | 간소화된 기본형(권장) |
| --- | --- | --- |
| 호스팅 아티팩트 | ps1 + (웹앱이 resolve한) zip URL | **웹앱 호스팅 없음**: 릴리스 자산 고정 URL 2종(런처 exe + 준비 ps1)만 사용 |
| `.wsb` 플레이스홀더 | 4개 | **1개**(`__SPORK_SITE_IDS__`, 선택) |
| 다운로드 URL/체크섬 | `.wsb`가 운반 | **런처가 자체 해석**(고정 URL → API 폴백) |
| 신뢰 | HTTPS + 체크섬맵 + 서명 | HTTPS 오리진 + **서명된 런처/Spork.exe** |

**간소화된 `.wsb`** (마운트 0. 인라인 LogonCommand: DNS 선보정 → arch 판별 → 고정 URL로 런처 다운로드 → 실행):

```xml
<Configuration>
  <Networking>Enable</Networking>
  <vGPU>Disable</vGPU>
  <LogonCommand>
    <Command>powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "Start-Process powershell.exe -WindowStyle Normal -ArgumentList '-NoProfile','-ExecutionPolicy','Bypass','-NoExit','-Command','$Host.UI.RawUI.WindowTitle = ''식탁보 준비''; Write-Host '' 식탁보를 준비하고 있습니다...'' -ForegroundColor Cyan; if (-not (Resolve-DnsName -Name github.com -QuickTimeout -ErrorAction SilentlyContinue)) { Get-NetAdapter | Where-Object Status -eq ''Up'' | Set-DnsClientServerAddress -ServerAddresses 8.8.8.8,1.1.1.1 }; $p = Join-Path $env:TEMP ''tablecloth-prepare.ps1''; Invoke-WebRequest ''https://github.com/yourtablecloth/TableCloth/releases/latest/download/tablecloth-prepare.ps1'' -OutFile $p; if (Test-Path $p) { . $p ''__SPORK_SITE_IDS__'' } else { Write-Host '' 준비 스크립트를 내려받지 못했습니다. 네트워크 연결을 확인해 주세요.'' -ForegroundColor Red }'"</Command>
  </LogonCommand>
</Configuration>
```

- **웹앱 호스팅 0**: 모든 아티팩트는 GitHub 릴리스 자산 고정 URL 2종(런처 `SporkBootstrap_<arch>.exe` + 준비 스크립트 `tablecloth-prepare.ps1`)에서 받는다. 웹앱이 별도 호스팅할 것이 없다(§12의 "ps1 호스팅" 결정은 릴리스 자산으로 흡수). 준비 스크립트를 릴리스 자산으로 둔 이유: `.wsb` 안에 스크립트 전문을 내장하면 LogonCommand가 수천 자로 비대해져 유지보수가 어렵고, 자산으로 두면 로직 수정이 `.wsb` 재배포 없이 다음 릴리스부터 반영된다(`.wsb`는 안정된 소형 포인터).
- **플레이스홀더 1개(선택)**: `__SPORK_SITE_IDS__`. 은행별 딥링크가 아니면 이마저 비우거나 `-ArgumentList` 항목을 빼면 일반 런처로 뜬다. 즉 웹앱은 사실상 **거의 정적인 `.wsb`** 하나만 서빙하면 된다. 런처는 미치환 플레이스홀더(`__SPORK_...`)를 "없음"으로 처리하므로 치환 누락도 안전하다.
- **DNS 선보정은 LogonCommand에 인라인**: 런처 exe 다운로드 자체가 DNS를 필요로 하므로(닭-달걀), 어떤 다운로드보다 먼저 `Set-DnsClientServerAddress`로 처리한다. ps1 shim 없이 성립. (netsh 대신 이 cmdlet을 써 인용부호 중첩을 피한다 → XML/PowerShell 이스케이프 불필요.)
  - **사내 DNS 정책(probe-then-fallback, 이슈 #285 구현):** 위 인라인 DNS는 **먼저 이름 해석을 시도해 실패할 때만** 공용 DNS로 폴백한다(`Resolve-DnsName` 프로브). 정상 DNS(사내 내부 리졸버 등)는 덮어쓰지 않으므로 split-horizon/공용DNS차단 환경에서도 기존 해석을 깨지 않는다. 다만 공용 DNS가 완전 차단되고 게스트 DNS도 안 잡히는 환경이면 폴백해도 해석이 안 되니 내부 리졸버 사용을 권장한다. 모드 1(TableCloth 빌드 샌드박스)은 같은 probe-then-fallback + **옵션 토글**(`PreferenceSettings.EnableSandboxPublicDnsFallback`, 기본 켜짐)로 제어한다([#285](https://github.com/yourtablecloth/TableCloth/issues/285)).
- **arch 판별은 런처 exe 선택용**: 런처 바이너리가 arch별이라 다운로드 전에 판별한다. 받은 뒤 Spork zip의 arch는 런처가 자체 판별한다.
- **신뢰**: 통제된 HTTPS 오리진 + 서명된 런처/Spork.exe. 체크섬 맵은 고정 URL 경로에선 생략한다(별도 파라미터 없음).
- **콜드부팅 피드백(2026-07-05):** WSB는 **LogonCommand의 콘솔 창 자체를 숨긴 채** 실행하므로 같은 창
  출력은 보이지 않는다(실측). 그래서 숨은 셸은 `Start-Process -WindowStyle Normal -NoExit`로 **새 보이는
  PowerShell 창** 하나만 띄우고, 이후 전 과정(제목/안내 → DNS 프로브 → 준비 스크립트 다운로드 →
  dot-source 실행 → 런처 다운로드 진행 막대)이 그 창 안에서 진행된다. 성공 시 준비 스크립트의 `exit`가
  창을 닫고, 어느 단계든 실패하면 `-NoExit` 덕에 오류가 창에 남는다. 명령/스크립트는
  **Base64(-EncodedCommand)로 감추지 않고 평문**으로 쓴다: 인코딩된 PowerShell은 AV/EDR 휴리스틱의 대표
  플래그 대상이고 `.wsb` 투명성(§10 신뢰 모델)을 해치기 때문. (WSB 콜드부팅 자체 구간은 WSB UI가 담당 →
  손댈 수 없음. "또 다운로드 UI를 만들면 같은 빈 구간 반복"이라, 게스트에 이미 있는 PowerShell만 사용한다.)

> **완전 파라미터화형(§3~§4)은 언제 쓰나?** 특정 버전 핀 고정, 오프라인/사설 미러, 체크섬 강제, MCP 자체
> 호스팅처럼 **명시적 통제가 필요할 때**. 그 경우 `.wsb`가 런처에 `--zip-url-template`(+`--sha256-map`)을
> 넘기면 되고(런처는 인자를 고정 URL보다 우선 사용), ps1 shim이나 4-플레이스홀더 형태가 필요하면 아래
> 계약을 그대로 쓴다. 즉 §3~§4는 **삭제가 아니라 "완전형" 옵션으로 남긴다.**

## 1. 구성요소

- **`.wsb` 템플릿**: 마운트 0개의 Windows Sandbox 설정 XML. **유일한 파라미터 주입면.**
- **부트스트랩** (`spork-bootstrap.ps1`): `.wsb`의 `LogonCommand`가 받아 실행하는 **정적, 인자구동**
  스크립트. 버전/사이트에 무관하게 고정이며, 모든 변형은 인자로 들어온다.
- **포터블 Spork**: self-contained 단일 파일 zip. 부트스트랩이 아키텍처에 맞게 받아 실행.
- **소비자(consumer)**: 플레이스홀더를 실제 값으로 치환해 최종 `.wsb`를 만드는 주체(웹앱/MCP).
- **러너(runner)**: 최종 `.wsb`를 실행하는 도구. Windows=`WindowsSandbox.exe`, macOS=`MacSandbox`.

## 2. 흐름 개요

```text
소비자(웹앱/MCP)                        러너                     게스트(Sandbox)
  │ 플레이스홀더 치환                     │                          │
  ├─ 최종 .wsb 생성 ───────────────────▶ │                          │
  │                                      ├─ .wsb 실행 ────────────▶ │ 부팅
  │                                      │                          ├─ LogonCommand
  │                                      │                          │   └─ 부트스트랩 fetch+실행
  │                                      │                          │       1) DNS 선보정
  │                                      │                          │       2) arch 판별 → zip URL 확정
  │                                      │                          │       3) 다운로드 (+체크섬) → 해제
  │                                      │                          │       4) Spork.exe <siteIds…> 실행
```

## 3. `.wsb` 템플릿 스펙

> **참고:** 아래 §3~§4는 **완전 파라미터화형**(버전 핀, 오프라인/사설 미러, 체크섬 강제, MCP 자체 호스팅용)
> 계약이다. 대부분의 경우 위 **§0.5 간소화된 기본형**(플레이스홀더 1개, ps1 없음)을 쓴다. §3.1 고정 요소
> (`Networking`/`vGPU`/마운트 0)는 두 형태 공통이다.

### 3.1 고정 요소 (변경 금지)

- `<Networking>Enable</Networking>`: 부트스트랩 다운로드에 필요.
- `<vGPU>Disable</vGPU>`: WSB에서 무해, macSandbox에서 무시(부분지원). 예측가능성을 위해 명시.
- **MappedFolders 절대 없음**: 호스트 파일 접근 0 = 유출 벡터 제거. (파일 기반 NPKI 불가 → 모바일 인증 전제)
  - 이는 샌드박스 마운트 by-design(호스트 Desktop 노출) 결정과 별개이며, Express 레인은 애초에 마운트를 두지 않는다.

### 3.2 LogonCommand 규약 (설계 B 권장)

`.wsb`가 **모든 파라미터를 담고**, 부트스트랩은 정적으로 둔다. LogonCommand는 (1) 정적 부트스트랩을
내려받아 (2) 파라미터를 **인자로** 넘겨 실행한다.

```xml
<LogonCommand>
  <Command>powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "$ProgressPreference='SilentlyContinue'; Invoke-WebRequest -Uri '__SPORK_BOOTSTRAP_URL__' -OutFile $env:TEMP\spork-bootstrap.ps1; powershell.exe -NoProfile -ExecutionPolicy Bypass -File $env:TEMP\spork-bootstrap.ps1 -PortableZipUrlTemplate '__SPORK_PORTABLE_ZIP_URL_TEMPLATE__' -SiteIds '__SPORK_SITE_IDS__' -Sha256Map '__SPORK_ZIP_SHA256_MAP__'"</Command>
</LogonCommand>
```

> **설계 B의 이유:** 부트스트랩을 정적으로 유지하면 웹앱은 URL 하나만 호스팅하면 되고, **MCP 서버는
> 로컬에서 `.wsb`만 생성**해 같은 정적 부트스트랩을 가리킬 수 있다(치환 대상이 `.wsb` 한 곳). 현재
> 참조 부트스트랩은 zip URL을 스크립트 기본값으로 굽는데(설계 A), 본 스펙은 이를 **인자 수신**으로
> 바꾼다. → 부트스트랩 변경 필요(§4).

### 3.3 플레이스홀더 계약

| 플레이스홀더 | 필수 | 의미 | 치환 주체 | 예시 |
| --- | --- | --- | --- | --- |
| `__SPORK_BOOTSTRAP_URL__` | 필수 | 정적 부트스트랩 스크립트의 HTTPS URL | 웹앱(호스팅) / MCP(자체 호스팅 or 로컬경로) | `https://yourtablecloth.app/express/spork-bootstrap.ps1` |
| `__SPORK_PORTABLE_ZIP_URL_TEMPLATE__` | 필수 | `{arch}` 토큰을 포함한 zip URL 템플릿 | 웹앱/MCP (최신 버전 확정 후) | `https://.../Spork_1.20.4.0_Release_{arch}_Portable.zip` |
| `__SPORK_SITE_IDS__` | 선택 | 공백 구분 사이트 `Id`. 비면 일반 런처(카탈로그 UI) | 웹앱/MCP | `Shinhan` / `KB WooriBank` / `` |
| `__SPORK_ZIP_SHA256_MAP__` | 권장 | `arch=sha256;arch=sha256` 형태 체크섬 맵 | 웹앱/MCP | `x64=ab12…;arm64=cd34…` |

**플레이스홀더 문법:** `__UPPER_SNAKE__`. 이유: XML/PowerShell 어디서도 특수문자가 아니어서
단순 문자열 치환이 안전하고, 미치환 시 눈에 띈다.

**XML 이스케이프(치환 주체 책임):** 값에 `&` `<` `>` `"`가 있으면 XML 엔티티로 이스케이프한다.
특히 다운로드 URL의 쿼리스트링 `&` → `&amp;`. `'`로 감싼 PowerShell 문자열 안의 값에 `'`가 있으면
`''`로 이스케이프. **사이트 `Id`는 카탈로그가 통제하는 화이트리스트에서만 오므로 임의 주입은 불가**하지만,
치환 주체는 위 이스케이프를 항상 적용한다.

## 4. 부트스트랩 스펙 (`spork-bootstrap.ps1` 계약)

정적, 멱등, 인자구동. 파라미터:

```powershell
param(
  [Parameter(Mandatory)] [string] $PortableZipUrlTemplate,  # '{arch}' 토큰 포함
  [string] $SiteIds   = '',                                 # 공백 구분
  [string] $Sha256Map = ''                                  # 'x64=…;arm64=…'
)
```

### 4.1 단계

1. **DNS 선보정 (다운로드 전).** 공용 DNS(8.8.8.8/1.1.1.1)를 Up 어댑터에 강제. 실패해도 진행.
   - **닭-달걀 주의:** Spork 내부 DNS 보정은 Spork를 *받은 뒤*에 도므로, 받기 *전에* 부트스트랩이 먼저 한다.
   - **사내 DNS 정책 주의:** 공용 리졸버가 차단된 관리형 네트워크에선 이 강제가 오히려 해석을 깨뜨릴 수 있다(§0.5의 주의와 동일). 옵션화 검토: [#285](https://github.com/yourtablecloth/TableCloth/issues/285).
2. **아키텍처 판별 → URL 확정.** `$env:PROCESSOR_ARCHITECTURE` 매핑:
   - `AMD64` → `x64`, `ARM64` → `arm64`. (그 외/`x86`은 오류로 중단, Express는 x64/arm64만 지원)
   - WOW 우회 대비 `PROCESSOR_ARCHITEW6432`도 확인. WSB/macSandbox의 로그온 PowerShell은 네이티브라 통상 불필요.
   - `$url = $PortableZipUrlTemplate -replace '\{arch\}', $arch`
3. **다운로드 → (선택) 체크섬 → 해제.**
   - `Invoke-WebRequest $url -OutFile $zip`
   - `$Sha256Map`에 해당 arch 항목이 있으면 `Get-FileHash -Algorithm SHA256` 로 검증, 불일치 시 중단.
   - `Expand-Archive` → `Desktop\Spork`.
4. **실행.** `Start-Process -FilePath Desktop\Spork\Spork.exe -ArgumentList $SiteIds`
   - `$SiteIds`가 비면 인자 없이 실행(일반 런처). Spork은 위치 인자 = `SelectedServices`로 받아 자동 오픈(§6).

### 4.2 오류/로깅

- Express는 호스트 마운트가 없어 **로그를 호스트로 뺄 수 없다.** 진단은 게스트 화면 메시지 + 게스트 내
  파일(`Desktop\spork-bootstrap.log`)로 남긴다. (테스트 하네스는 별도로 마운트 있는 `.wsb`를 쓴다, §9)
- 어떤 단계 실패든 사용자에게 보이는 메시지를 남기고, 창을 즉시 닫지 않는다(원인 확인 가능하도록).

## 5. 아키텍처(arch) 처리

| 러너 | 게스트 arch | 부트스트랩 판별 결과 | 받는 zip |
| --- | --- | --- | --- |
| Windows Sandbox (x64 호스트) | x64 | `x64` | `…_x64_Portable.zip` |
| Windows Sandbox (arm64 호스트) | arm64 | `arm64` | `…_arm64_Portable.zip` |
| macSandbox (Apple Silicon) | **arm64 고정** | `arm64` | `…_arm64_Portable.zip` |

**단일 `.wsb`가 세 경우를 모두 커버**한다. arch 판별이 게스트 런타임에서 일어나므로, 소비자는 arch를
알 필요 없이 `{arch}` 토큰 템플릿만 넣으면 된다.

## 6. 사이트 사전선택 규약

- `SiteId` = **카탈로그 서비스의 `Id`** (예: `Shinhan`). Spork은 위치 인자를 `SelectedServices`로 받아
  카탈로그에서 URL을 resolve → OpenWebSiteStep으로 자동 오픈. (기존 경로, 신규 코드 없음)
- 다중 사이트 허용: 공백 구분(`'Shinhan KB'`).
- **알 수 없는 `Id` 처리(확정 필요):** 카탈로그에 없는 Id가 오면 현재 Spork 동작을 확인해 명문화한다
  (무시하고 런처 표시 / 경고). → "열린 결정".
- MCP, 웹앱은 카탈로그의 실제 `Id`만 넘긴다(화이트리스트). 임의 URL을 여는 경로는 제공하지 않는다.

## 7. 자산명 계약 (PORTABLE_MODE2_TODO에서 승계)

- 포터블: `Spork_<4파트버전>_<config>_<platform>_Portable.zip`
  - `<platform>` ∈ { `x64`, `arm64` }, `<config>` = `Release`. 예: `Spork_1.20.4.0_Release_arm64_Portable.zip`
- self-contained 단일 파일 → 런타임/마운트 없는 바 WSB에서 해제 후 즉시 실행.
- 최신 태그 → 자산 URL resolve는 **웹앱/MCP 책임**(GitHub Releases API로 매칭). 이 계약이 바뀌면
  `build.cs` rename 규칙과 함께 갱신한다.

## 8. 크로스플랫폼 러너 계약

**동일한 `.wsb` 하나**를 두 러너가 실행한다.

| `.wsb` 요소 | Windows Sandbox | macSandbox | Express 사용 |
| --- | --- | --- | --- |
| `Networking` Enable | ✅ | ✅ (NAT, NetKVM 주입) | **사용** |
| `LogonCommand` | ✅ | ✅ (config 디스크로 전달, 로그온 시 실행) | **사용** |
| `vGPU` Disable | ✅ | ⚠️ 부분(무시, 무해) | 명시만 |
| `MappedFolders` | ✅ | ✅ 단 **ReadOnly 미강제**, RW 고정 | **미사용**(마운트 0) |
| `MemoryInMB` 등 | ✅ | ✅ 값 clamp | 미사용(기본) |

- **Windows 실행:** `.wsb` 더블클릭 또는 `WindowsSandbox.exe <path>.wsb`.
- **macOS 실행:** `MacSandbox <path>.wsb` (Apple Silicon 전용). CLI가 `.wsb`를 직접 해석.
- Express가 쓰는 요소(`Networking`+`LogonCommand`)는 **양쪽 모두 ✅**라 이식성이 온전하다.

## 9. 소비자별 사용 (세 시나리오 매핑)

- **① 빠른 실행 웹앱:** 최신 릴리스 resolve → `__SPORK_*__` 치환(체크섬 포함) → `.wsb`+정적 부트스트랩을
  HTTPS로 호스팅. 사용자는 `.wsb`를 받아 더블클릭. (일반 런처 또는 은행별 딥링크 `.wsb`)
- **② MCP 서버:** `list_sites`(카탈로그 조회) → `launch_sandbox(siteId)`가 §3 템플릿을 **로컬에서 치환**해
  임시 `.wsb` 생성 → 러너 실행(`WindowsSandbox.exe`/`MacSandbox`). 정적 부트스트랩은 공개 HTTPS(웹앱)나
  MCP 자체 호스팅을 가리킴. **전송(STDIO/HTTP)은 이 스펙과 독립**(별도 결정).
- **③ macOS:** 웹앱이 준(또는 MCP가 만든) **동일 `.wsb`**를 `MacSandbox`로 실행. arch 판별이 arm64를
  선택. 리포 측 추가 코드는 부트스트랩 arch 처리(§4)와 문서뿐.

## 10. 신뢰, 보안 모델

- **마운트 0 → 호스트 파일 접근 없음.** 유출된/악성 `.wsb`가 호스트 데이터를 빼갈 수 없다(구조적 차단).
- **원격 코드 실행 `.wsb`:** LogonCommand가 스크립트/실행파일을 네트워크에서 받는다. 신뢰는
  ① 통제된 HTTPS 오리진 + ② 체크섬(`__SPORK_ZIP_SHA256_MAP__`) + ③ **서명된 Spork.exe(1.20.4~)** 로 관리.
  배포는 공식 오리진만 안내한다.
- **파일 인증서 불가 → 모바일 인증 전제**(가장 안전: 파일 자체가 없음).
- **데이터 영속 없음**(마운트 0): 일회성. 영속이 필요하면 Express가 아니라 패턴 A(사용자 VM).
- 다운로드한 `.wsb`의 실행 허용 여부는 일반 SW 설치와 동일한 **가치중립 결정**(사용자/조직 정책, 스코프 밖).

## 11. 확정된 결정 (2026-07-03)

| # | 결정 | 확정 |
| --- | --- | --- |
| 1 | 부트스트랩 설계 | **B**: 모든 파라미터는 `.wsb`에, 부트스트랩은 정적, 인자구동. 참조 부트스트랩(`spork-bootstrap.ps1`)을 인자 수신형으로 개정한다(착수 시). |
| 2 | zip URL 표현 | **단일 `{arch}` 토큰 템플릿**. arch는 게스트 런타임 판별. |
| 3 | 체크섬 | 웹앱/MCP는 **`__SPORK_ZIP_SHA256_MAP__` 게시 권장**, 부트스트랩은 **있으면 검증, 불일치 시 중단**. 필수 강제는 하지 않음(자체 호스팅, 오프라인 테스트 유연성). |
| 4 | 사이트 `Id` | **다중 허용**(공백 구분). 알 수 없는 `Id`는 Spork 기존 동작을 따르되, **착수 시 실측해 §6에 명문화**(현재 미검증 항목). |
| 5 | `SiteId` 전달 채널 | **위치 인자**(기존 `SelectedServices` 경로, 신규 코드 0). |

## 12. 추후 결정 (트랙 착수 시, 이 스펙과 독립)

- **정적 부트스트랩의 소유/호스팅**: `tools/no-install/`의 파일을 웹앱이 그대로 호스팅할지, MCP가 동봉할지.
- **MCP 전송(STDIO/HTTP)**: 트랙 2 착수 시 결정, §9에 반영.
- (PORTABLE_MODE2_TODO 승계) **세션 종료 wipe 옵션** 유무: 기본 권장은 두지 않음.

## 13. 비목표 (v1 제외)

- 테마/컬처 등 호스트 옵션 주입(Express는 기본값). 필요 시 `SporkAnswers.json`로 별도 확장.
- 데이터 영속, 즐겨찾기 마운트(마운트가 생기면 Express 정의에서 벗어남).
- 파일 인증서 반입(패턴 A, 사용자 VM 영역).

---

## 변경 이력

- (초안) 세 시나리오(빠른 실행/MCP/macOS) 공유 계약으로 최초 작성. 설계 B, `{arch}` 토큰, 사이트 위치
  인자, 체크섬 맵, 크로스플랫폼 러너 표를 권장안으로 제시. "열린 결정" 확정 후 정식 계약으로 승격.
- (간소화, 2026-07-05) 무설치 런처의 고정 URL + 자체 해석을 반영해 **§0.5 "간소화된 기본형"** 추가:
  플레이스홀더 4 → 1(`__SPORK_SITE_IDS__`만, 선택), 호스팅 ps1 제거, 다운로드 URL/체크섬은 런처가 자체
  해석, DNS 선보정은 `Set-DnsClientServerAddress`로 LogonCommand에 인라인. §3~§4는 완전 파라미터화형(핀/오프라인/
  MCP)으로 보존. 부수 첨삭: §3.1의 깨진 링크(`../`) 정리, §3에 완전형/간소형 구분 참고 추가.
- (DNS 주의, 2026-07-05) §0.5/§4.1에 **사내 DNS 정책 주의** 추가: 공용 리졸버(8.8.8.8/1.1.1.1) 차단/
  split-horizon 환경에선 강제 설정이 오히려 이름 해석을 깨뜨릴 수 있음. 코드 측 DNS 강제의 옵션화는
  이슈 #285로 분리(지금 구현 아님).
- (DNS #285 구현, 2026-07-05) **probe-then-fallback** 채택: 이름 해석이 되면 두고 실패할 때만 공용 DNS로
  폴백. §0.5 `.wsb`는 `Resolve-DnsName` 프로브 후 폴백하도록 인라인 개정. 모드 1은 `SandboxBootstrap`이
  동일 로직 + 옵션 토글(`PreferenceSettings.EnableSandboxPublicDnsFallback`, 기본 켜짐)로 제어.
- (콜드부팅 피드백, 2026-07-05) 런처 GUI 전 "준비 중" 표시 추가. 1차 시도(같은 콘솔에 출력)는 **WSB가
  LogonCommand 콘솔 창을 숨긴 채 실행해 무효**(실측)였고, 숨은 셸이 평문 준비 스크립트를 쓰고
  `Start-Process -WindowStyle Normal`로 새 보이는 PowerShell 창을 띄우는 방식으로 개정. 안내 메시지 +
  다운로드 진행 막대 + 실패 시 창 유지. Base64 인코딩은 AV 휴리스틱/투명성 사유로 의도적으로 배제.
  §0.5 예시와 `no-install-spork.wsb` 동기화.
- (LogonCommand 리팩토링, 2026-07-05) 준비 스크립트 전문을 `.wsb` 인라인(문자열 배열, 약 2,400자)으로
  내장하던 방식을 **릴리스 자산 `tablecloth-prepare.ps1`(고정 URL) 분리**로 개정 — LogonCommand 약 1,000자
  로 축소, 스크립트는 `tools/no-install/tablecloth-prepare.ps1` 리포 파일로 관리(버전/리뷰 가능). 보이는
  창이 `-NoExit`로 뜬 뒤 DNS 프로브 → ps1 다운로드 → dot-source 하므로 실패는 항상 창에 남는다. 사이트
  사전선택은 dot-source 인자(`. $p '__SPORK_SITE_IDS__'`)로 전달. 자산은 build.yml/build.cs가 게시하며
  해당 자산 포함 릴리스 게시 후 활성화.
