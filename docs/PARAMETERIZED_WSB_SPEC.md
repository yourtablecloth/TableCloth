# 파라미터화된 Express `.wsb` 스펙 (Parameterized WSB Spec)

> 상태: **확정 (2026-07-03)**. 빠른 실행 웹앱, MCP 서버, macOS(macSandbox) 세 소비자가 공유하는
> 정규 계약. 핵심 설계(설계 B, `{arch}` 토큰, 사이트 위치 인자, 체크섬 맵)는 확정됐고, 소비자별
> 세부(§9)와 추후 결정(§12: 웹앱 호스팅, MCP 전송)만 각 트랙 착수 시 확정한다.
> 모드 2(무설치 코어) 배경과 자산명 계약은 본 문서로 통합됐다(§7).
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
| `.wsb` 플레이스홀더 | 4개 | **2개**(`__SPORK_SITE_IDS__`, `__SPORK_TARGET_URL__`, 둘 다 선택) |
| 다운로드 URL/체크섬 | `.wsb`가 운반 | **런처가 자체 해석**(고정 URL → API 폴백) |
| 신뢰 | HTTPS + 체크섬맵 + 서명 | HTTPS 오리진 + **서명된 런처/Spork.exe** |

**간소화된 `.wsb`** (마운트 0. 인라인 LogonCommand: DNS 선보정 → arch 판별 → 고정 URL로 런처 다운로드 → 실행):

```xml
<Configuration>
  <Networking>Enable</Networking>
  <vGPU>Disable</vGPU>
  <LogonCommand>
    <Command>powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "Start-Process powershell.exe -WindowStyle Normal -ArgumentList '-NoProfile','-ExecutionPolicy','Bypass','-Command','$Host.UI.RawUI.WindowTitle = ''TableCloth Setup''; Write-Host '' Getting TableCloth ready...'' -ForegroundColor Cyan; if (-not (Resolve-DnsName -Name github.com -QuickTimeout -ErrorAction SilentlyContinue)) { Get-NetAdapter | Where-Object Status -eq ''Up'' | Set-DnsClientServerAddress -ServerAddresses 8.8.8.8,1.1.1.1 }; [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor 3072; $env:TABLECLOTH_SITE_IDS = ''__SPORK_SITE_IDS__''; try { iex ((New-Object Net.WebClient).DownloadString(''https://github.com/yourtablecloth/TableCloth/releases/latest/download/tablecloth-prepare.ps1'')) } catch { Write-Host ('' Failed: '' + $_.Exception.Message) -ForegroundColor Red; $null = Read-Host '' Press Enter to close'' }'"</Command>
  </LogonCommand>
</Configuration>
```

- **웹앱 호스팅 0**: 모든 아티팩트는 GitHub 릴리스 자산 고정 URL 2종(런처 `SporkBootstrap_<arch>.exe` + 준비 스크립트 `tablecloth-prepare.ps1`)에서 받는다. 웹앱이 별도 호스팅할 것이 없다(§12의 "ps1 호스팅" 결정은 릴리스 자산으로 흡수). 준비 스크립트를 릴리스 자산으로 둔 이유: `.wsb` 안에 스크립트 전문을 내장하면 LogonCommand가 수천 자로 비대해져 유지보수가 어렵고, 자산으로 두면 로직 수정이 `.wsb` 재배포 없이 다음 릴리스부터 반영된다(`.wsb`는 안정된 소형 포인터).
- **플레이스홀더 2개(둘 다 선택)**: `__SPORK_SITE_IDS__`, `__SPORK_TARGET_URL__`. 전달 채널은 **환경 변수 `TABLECLOTH_SITE_IDS` / `TABLECLOTH_TARGET_URL`**(`iex`는 인자를 못 넘기므로 Chocolatey식 env 채널; 준비 스크립트의 `param` 기본값이 읽는다). 딥링크가 아니면 두 `$env:...` 문장을 통째로 빼면 일반 런처로 뜬다. 즉 웹앱은 사실상 **거의 정적인 `.wsb`** 하나만 서빙하면 된다. 준비 스크립트와 런처 모두 미치환 플레이스홀더(`__SPORK_...`)를 "없음"으로 처리하므로 치환 누락도 안전하다. 치환형 참조 파일은 `tools/no-install/no-install-spork-deeplink.wsb`(템플릿), 무파라미터형은 같은 폴더의 `no-install-spork.wsb`.
- **`__SPORK_TARGET_URL__`(대상 URL)**: 사이트 대표 URL이 아니라 **사용자가 실제로 보던 페이지**를 그대로 열기 위한 채널이다(§6.5). 브라우저 익스텐션처럼 현재 탭 주소를 아는 생산자가 채운다. 이 값은 카탈로그 화이트리스트가 아니라 **임의 문자열**이라 신뢰 모델이 달라지므로(§10), 게스트 측 도메인 게이트를 반드시 통과해야 한다.
- **DNS 선보정은 LogonCommand에 인라인**: 런처 exe 다운로드 자체가 DNS를 필요로 하므로(닭-달걀), 어떤 다운로드보다 먼저 `Set-DnsClientServerAddress`로 처리한다. ps1 shim 없이 성립. (netsh 대신 이 cmdlet을 써 인용부호 중첩을 피한다 → XML/PowerShell 이스케이프 불필요.)
  - **사내 DNS 정책(probe-then-fallback, 이슈 #285 구현):** 위 인라인 DNS는 **먼저 이름 해석을 시도해 실패할 때만** 공용 DNS로 폴백한다(`Resolve-DnsName` 프로브). 정상 DNS(사내 내부 리졸버 등)는 덮어쓰지 않으므로 split-horizon/공용DNS차단 환경에서도 기존 해석을 깨지 않는다. 다만 공용 DNS가 완전 차단되고 게스트 DNS도 안 잡히는 환경이면 폴백해도 해석이 안 되니 내부 리졸버 사용을 권장한다. 모드 1(TableCloth 빌드 샌드박스)은 같은 probe-then-fallback + **옵션 토글**(`PreferenceSettings.EnableSandboxPublicDnsFallback`, 기본 켜짐)로 제어한다([#285](https://github.com/yourtablecloth/TableCloth/issues/285)).
- **arch 판별은 런처 exe 선택용**: 런처 바이너리가 arch별이라 다운로드 전에 판별한다. 받은 뒤 Spork zip의 arch는 런처가 자체 판별한다.
- **신뢰**: 통제된 HTTPS 오리진 + 서명된 런처/Spork.exe. 체크섬 맵은 고정 URL 경로에선 생략한다(별도 파라미터 없음).
- **인코딩 규칙(ASCII 전용, 2026-07-05):** `.wsb`와 준비 스크립트 등 **무설치 정적 자산은 ASCII 전용**으로
  작성한다(표시 문자열 포함 전체 영문). Windows PowerShell 5.1은 BOM 없는 스크립트를 레거시 ANSI
  코드페이지로 읽고, 게스트 코드페이지는 호스트 언어팩에 따라 달라 비-ASCII 문자열이 깨져 **파싱 자체가
  실패**할 수 있다(한국어로 실측). `WebClient.DownloadString`도 charset 헤더 없는 응답(GitHub 릴리스 자산)을
  같은 방식으로 디코드하므로 ASCII 전용이어야 안전하다. 사용자 대상 현지화 문자열은 런타임에 언어를
  처리하는 런처 GUI가 담당한다.
- **콜드부팅 피드백(2026-07-05):** WSB는 **LogonCommand의 콘솔 창 자체를 숨긴 채** 실행하므로 같은 창
  출력은 보이지 않는다(실측). 그래서 숨은 셸은 `Start-Process -WindowStyle Normal`로 **새 보이는
  PowerShell 창** 하나만 띄우고, 이후 전 과정(제목/안내 → DNS 프로브 → TLS 1.2 보정 → 준비 스크립트를
  **Chocolatey식 `iex`+`DownloadString`으로 무파일 실행** → 런처 다운로드[단계 메시지])이 그 창 안에서
  진행된다. 런처 다운로드의 **바이트 진행률은 의도적으로 끈다**(PS 5.1의 IWR 진행률 렌더링이 전송 자체를
  늦추고, 런처는 소용량이며, 진짜 진행률 UX[대용량 식탁보 다운로드]는 런처 GUI 소관). `iex`는 세션 스코프 실행이라 준비 스크립트의 `exit`가 곧 창 닫기이고, 명령이 끝나면 창도
  저절로 닫힌다(작업 완료 시 잔여 콘솔 없음). 실패 가시성은 인라인 catch와 스크립트 자체 catch의
  `Read-Host`(Enter 대기)가 보장한다. 명령/스크립트는
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
| `__SPORK_SITE_IDS__` | 선택 | 공백 구분 사이트 `Id`. 비면 일반 런처(카탈로그 UI) | 웹앱/MCP/익스텐션 | `Shinhan` / `KB WooriBank` / `` |
| `__SPORK_TARGET_URL__` | 선택 | 설치 후 열 페이지 주소(http/https 절대 URL). 비면 카탈로그의 대표 URL을 연다 | 익스텐션/웹앱/MCP | `https://spib.wooribank.com/pib/Dream?withyou=CTCER0149&amp;fromSite=pib` |
| `__SPORK_ZIP_SHA256_MAP__` | 권장 | `arch=sha256;arch=sha256` 형태 체크섬 맵 | 웹앱/MCP | `x64=ab12…;arm64=cd34…` |

**플레이스홀더 문법:** `__UPPER_SNAKE__`. 이유: XML/PowerShell 어디서도 특수문자가 아니어서
단순 문자열 치환이 안전하고, 미치환 시 눈에 띈다.

**이스케이프(치환 주체 책임):** 값은 **두 층**을 통과하므로 층마다 규칙이 다르다.

1. **XML 층** — `&` `<` `>`는 XML 엔티티로 이스케이프한다(`&` → `&amp;`). 실제 은행 URL의
   쿼리스트링에 `&`가 들어오므로(`...?withyou=CTCER0149&fromSite=pib`) 선택이 아니다. XML 파서가
   원문자를 복원하고, 작은따옴표 PowerShell 문자열은 그 문자들을 리터럴로 다루므로 여기서 끝난다.
2. **PowerShell/argv 층** — `'`와 `"`는 XML 층 문제가 아니다. `&apos;`/`&quot;`로 써도 파서가
   원문자를 되돌려주는 순간 중첩 인용 구조가 깨진다(값이 `-Command "…-ArgumentList '…''값''…'"`
   안쪽에 놓인다). 따라서 **퍼센트 인코딩**한다: `'` → `%27`, `"` → `%22`. `$`나 백틱은 argv 인용
   이라 그대로 두어도 안전하다.

사이트 `Id`는 카탈로그 화이트리스트에서만 오지만, **대상 URL은 임의 문자열**이다. 생산자는 위
이스케이프 외에 (a) http/https 절대 URL만, (b) 자격 증명(`user@host`) 없음, (c) 2,048자 이하를
스스로 검증하고, 게스트도 같은 검증을 다시 한다(§6.5).

## 4. 부트스트랩 스펙 (`spork-bootstrap.ps1` 계약)

정적, 멱등, 인자구동. 파라미터:

```powershell
param(
  [Parameter(Mandatory)] [string] $PortableZipUrlTemplate,  # '{arch}' 토큰 포함
  [string] $SiteIds   = '',                                 # 공백 구분
  [string] $TargetUrl = '',                                 # 설치 후 열 페이지(검증하지 않고 포워딩)
  [string] $Sha256Map = ''                                  # 'x64=…;arm64=…'
)
```

간소화된 기본형의 `tablecloth-prepare.ps1`도 같은 두 값을 환경 변수 기본값(`TABLECLOTH_SITE_IDS`,
`TABLECLOTH_TARGET_URL`)으로 받아 런처에 `--site-ids` / `--target-url`로 넘긴다. **준비 스크립트와
런처는 URL을 검증하지 않는다** — 그 단계엔 카탈로그가 없어 판정이 불가능하다. 판정은 카탈로그를
로드한 Spork(§6.5)에서만 일어난다.

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
- MCP, 웹앱은 카탈로그의 실제 `Id`만 넘긴다(화이트리스트). 임의 URL은 §6.5의 게이트를 통해서만 열린다.

## 6.5 대상 URL 게이트 (`__SPORK_TARGET_URL__`)

카탈로그 대표 URL이 아니라 **사용자가 보던 정확한 페이지**로 진입하기 위한 채널이다. 대표 소비자는
브라우저 익스텐션으로, 현재 탭이 카탈로그가 아는 도메인이면 그 URL을 박은 `.wsb`를 만들어 내려준다.

`.wsb`는 신뢰 경계 밖에서 재배포될 수 있으므로 이 값은 **신뢰할 수 없는 입력**이다. 게스트의
`CatalogTargetUrlMatcher`(TableCloth.Core)가 유일한 방어선이며 판정 규칙은 다음과 같다.

1. **형식 검증** — http/https 절대 URL, 자격 증명(`user@host`) 없음, 2,048자 이하, 공백/제어문자/
   큰따옴표 없음(브라우저 인자 주입 차단), 미치환 플레이스홀더는 "없음"으로 취급.
2. **도메인 일치** — **퍼블릭 서픽스를 인식한 라벨 단위 등록 도메인 비교**. 문자열 `EndsWith`/
   `Contains`는 쓰지 않는다(`evilwooribank.com`이 통과한다). `co.kr`/`or.kr`/`go.kr` 등 kr 2단계
   서픽스를 인식하지 않으면 `www.ibk.co.kr`의 등록 도메인이 `co.kr`이 되어 **모든 .co.kr 사이트가
   서로 일치**하므로, 이 목록은 필수 구성요소다.
3. **사이트 `Id`가 함께 온 경우(정상 경로)** — 넘어온 **모든** Id가 URL과 같은 등록 도메인이어야
   수락한다. URL과 무관한 Id를 끼워 제3자 보안 프로그램 설치를 편승시키는 `.wsb`를 막는다.
4. **URL만 온 경우** — 같은 등록 도메인 후보 중 호스트 라벨이 가장 많이 일치하는 하나를 고른다.
   **사이트 `Id` 판정은 항상 하나로 끝난다.** 여러 후보를 함께 설치하면 겹치는 패키지가 중복
   설치되어(우리은행 개인/기업은 AnySign·AhnLabSafeTx·nProtect·IPInside가 겹친다) 설치 단계가
   지저분해지기 때문이다. 동점이면 **카탈로그에 먼저 적힌 항목**을 택한다(카탈로그가 대표 서비스를
   앞에 두므로 개인뱅킹처럼 흔한 쪽이 선택된다).
   - 예외: 한 등록 도메인에 서비스가 **4개 이상**인데 동점이면 URL을 버린다. 우리은행 개인/기업(2),
     하나은행 개인/기업/저축(3)은 **같은 회사의 갈래**라 어느 쪽을 골라도 무리가 없지만,
     `fsb.or.kr` 아래 저축은행 25곳은 **서로 다른 회사**가 호스팅만 공유하는 것이라 하나를 골라
     남의 은행 보안 프로그램을 설치해선 안 된다. 둘을 자동으로 구분할 신호가 도메인당 서비스 수뿐이라
     이 값을 경계로 쓴다.
   - 그래도 **생산자가 Id를 같이 넘기는 것이 정상 경로**다(익스텐션 팝업이 사용자에게 고르게 한다).
     URL 단독은 편의 경로다.
5. **거부 시** — URL만 버리고 사이트 `Id` 채널은 기존 동작을 유지한다. Id도 없었다면 사이트 지정이
   없는 일반 실행과 동일하게 카탈로그 UI가 열린다(요청된 동작: 엉뚱한 주소면 기본 식탁보만 열기).
   사유는 게스트 로그에 남기고 URL 원문은 기록하지 않는다.

수락되면 그 URL이 설치 완료 후 열리는 유일한 주소가 된다(카탈로그 대표 URL은 열지 않는다).

## 7. 자산명 계약

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
- **대상 URL은 신뢰하지 않는다:** `__SPORK_TARGET_URL__`이 생기면서 이 계약에 처음으로 카탈로그
  화이트리스트 밖의 값이 흐른다. 방어는 §6.5의 게이트가 전담하고, 게이트 통과 여부와 무관하게
  ① 마운트 0(호스트 파일 접근 없음)과 ② 일회성 게스트라는 구조적 방어는 그대로다. 진짜 은행
  플러그인을 설치해 신뢰를 얻은 뒤 임의 페이지를 여는 편승 시도는 "URL이 그 은행 도메인 하위여야
  한다"는 게이트로 차단된다.
- **URL을 평문으로 두는 것은 의도된 선택이다:** Base64로 감싸면 §3.3의 이스케이프가 필요 없어지지만,
  `.wsb`를 열어본 사람이 어떤 페이지가 열릴지 확인할 수 없게 된다. 투명성이 이 파일의 신뢰 모델이며
  (LogonCommand를 `-EncodedCommand`로 숨기지 않는 것과 같은 이유), 편승 시도를 사람이 알아볼 수 있게
  하는 마지막 방어선이기도 하다.
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
- **세션 종료 wipe 옵션**(모드 2) 유무: 기본 권장은 두지 않음.

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
- (IWR 진행률 끔, 2026-07-05) 준비 스크립트의 런처 다운로드에서 IWR 바이트 진행률을 명시적으로 끔
  (`$ProgressPreference='SilentlyContinue'` — 세션 기본이 Continue라 명시 필요). PS 5.1 IWR 진행률
  렌더링이 전송을 늦추는 문제 회피. 단계 메시지는 유지, 진짜 진행률 UX는 런처 GUI 소관.
- (Chocolatey식 iex, 2026-07-05) 준비 스크립트 실행을 임시 파일 + dot-source에서 **`iex`+
  `WebClient.DownloadString` 무파일 실행**으로 전환(Chocolatey install 관용구). TLS 1.2 보정
  (`SecurityProtocol -bor 3072`) 추가, `-NoExit` 제거로 **작업 완료 시 창 자동 종료**(실패 가시성은 양쪽
  catch의 `Read-Host`로 유지). 딥링크 채널은 dot-source 인자 → **환경 변수 `TABLECLOTH_SITE_IDS`**로 변경.
  `Set-ExecutionPolicy`는 불필요해 배제(-ExecutionPolicy Bypass 플래그로 충분, iex는 정책 미적용).
- (ASCII 전용, 2026-07-05) 샌드박스 실물 테스트에서 준비 스크립트의 한글이 모지바케로 **파싱 실패**
  (PowerShell 5.1의 BOM 없는 파일 = ANSI 코드페이지 해석). `.wsb`/준비 스크립트 전체(주석/표시 문자열 포함)를
  영문 ASCII 전용으로 전환하고 §0.5에 인코딩 규칙 명문화. 현지화는 런처 GUI 담당.
- (LogonCommand 리팩토링, 2026-07-05) 준비 스크립트 전문을 `.wsb` 인라인(문자열 배열, 약 2,400자)으로
  내장하던 방식을 **릴리스 자산 `tablecloth-prepare.ps1`(고정 URL) 분리**로 개정 — LogonCommand 약 1,000자
  로 축소, 스크립트는 `tools/no-install/tablecloth-prepare.ps1` 리포 파일로 관리(버전/리뷰 가능). 보이는
  창이 `-NoExit`로 뜬 뒤 DNS 프로브 → ps1 다운로드 → dot-source 하므로 실패는 항상 창에 남는다. 사이트
  사전선택은 dot-source 인자(`. $p '__SPORK_SITE_IDS__'`)로 전달. 자산은 build.yml/build.cs가 게시하며
  해당 자산 포함 릴리스 게시 후 활성화.
- (대상 URL 채널, 2026-07-30) 카탈로그 도메인에 속하는 **임의 하위 페이지**를 직접 열 수 있도록
  `__SPORK_TARGET_URL__` 플레이스홀더와 `TABLECLOTH_TARGET_URL` 환경 변수 채널을 추가(§0.5, §3.3, §4).
  판정은 게스트의 `CatalogTargetUrlMatcher`가 전담하며(§6.5), 준비 스크립트/런처는 검증 없이 포워딩만
  한다(그 단계엔 카탈로그가 없음). 이 변경으로 §6의 "임의 URL을 여는 경로는 제공하지 않는다"가
  "게이트를 통해서만 열린다"로 개정됐고, 신뢰 모델에 "대상 URL은 신뢰하지 않는다" 항목이 추가됐다(§10).
  치환형 참조 `.wsb`는 `tools/no-install/no-install-spork-deeplink.wsb`(릴리스 자산으로도 게시 —
  생산자가 고정 URL로 받아 치환하면 LogonCommand 수정이 생산자 재배포 없이 반영된다). 이스케이프는
  XML 층(`&`)과 PowerShell/argv 층(`'`, `"` → 퍼센트 인코딩)을 구분해 명문화했다.
- (ZScaler #292 구현, 2026-07-06) SSL 검사(ZScaler 등) 환경에서 샌드박스 HTTPS가 차단되는 문제 대응.
  옵션 토글(`PreferenceSettings.EnableZScalerRootCertPropagation`, 기본 꺼짐)이 켜지면 호스트가
  `LocalMachine\Root`에서 Subject에 "Zscaler"가 포함된 루트 인증서를 추출해 staging `App\zscaler\zscaler.pem`
  으로 전달하고, `SandboxBootstrap`이 게스트 진입 시 `CurrentUser\Root` 등록 + `NODE_EXTRA_CA_CERTS`(User) +
  git `http.sslBackend=schannel`을 구성한다. 호스트에 해당 인증서가 없으면 켜져 있어도 자동으로 건너뛴다.
  **이 옵션은 모드 1(TableCloth 빌드 샌드박스) 전용이며, 무설치(모드 2/§0.5) 식탁보에서는 지원되지 않는다**
  (무설치 정적 자산은 ASCII 전용·호스트 인증서 접근 경로가 없으므로).
