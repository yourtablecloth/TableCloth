# 무설치 부트스트래퍼 설계 (Spork.Bootstrapper)

> 상태: **확정 (2026-07-05)**. 무설치(Express) 레인에서 포터블 Spork zip을 받는 동안 진행 상황을
> 보여주는 작은 GUI 다운로더의 설계. 부모 계약: [PARAMETERIZED_WSB_SPEC.md](PARAMETERIZED_WSB_SPEC.md)
> (파라미터화된 `.wsb` + 부트스트랩 정규 계약), [PORTABLE_MODE2_TODO.md](PORTABLE_MODE2_TODO.md)
> (모드 2, 무설치 코어, 자산명 계약).
>
> **확정 결정(2026-07-05):** (1) 최신 버전은 **인자 수신 + GitHub 폴백**, (2) UI는 **Win32/GDI(순수 P/Invoke) + NativeAOT**,
> (3) `spork-bootstrap.ps1`은 **얇은 shim**으로 축소하고 `.wsb` 플레이스홀더 계약(SPEC §3.3)은 **불변**.

## 0. 목적과 범위

Express 레인의 다운로드 대상(포터블 Spork zip)은 크다. [Spork.csproj](../src/Spork/Spork.csproj)가
`UseWPF=true` + `SelfContained` + `PublishSingleFile` + `PublishReadyToRun` + `PublishTrimmed=false`이므로,
WPF는 NativeAOT/트리밍이 불가해 zip이 압축 후에도 수십 MB(대략 60~90MB급)다. 현재는 이 큰 파일을
`$ProgressPreference='SilentlyContinue'`인 `Invoke-WebRequest`로 조용히 받아, 게스트 화면에 아무 창도
없이 긴 무반응 구간이 생긴다. 본 문서는 그 앞에 **작은 GUI 부트스트래퍼**를 세워 다운로드/검증/해제/실행을
시각화하는 도구의 설계를 정의한다.

**이 문서가 소유하는 것:** 부트스트래퍼 프로젝트 구조, 인자 계약, 최신 버전 해석 규칙, UI/UX 규약,
크기/AOT 예산, 서명/빌드/자산명 계약, `spork-bootstrap.ps1`의 shim 개정.
**이 문서가 소유하지 않는 것:** 웹앱 HTTPS 호스팅/치환, `.wsb` 플레이스홀더 계약(SPEC §3.3 그대로 승계),
최신 태그의 정상 resolve(웹앱/MCP 책임. 본 도구의 GitHub 폴백은 인자가 없을 때만 도는 보조 경로).

## 1. 문제 정의

- **무반응 구간이 길다.** 큰 zip을 조용히 받는 동안 게스트 화면은 비어 있어 사용자가 "샌드박스가 멈췄나"로
  오인하고 이탈한다.
- **실패가 사라진다.** [SPEC §4.2](PARAMETERIZED_WSB_SPEC.md)는 "창을 즉시 닫지 말고 화면에 메시지를
  남겨라"를 요구하는데, 헤드리스 `.ps1`은 실패 시 콘솔이 순식간에 닫혀 원인 확인이 어렵다.
- **작아야 한다(선행 다운로드).** 부트스트래퍼 자체가 게스트에 먼저 내려오므로, 이게 크면 문제를 앞으로
  옮길 뿐이다. 바 WSB엔 .NET 런타임이 없어 self-contained가 강제되고, 런타임을 통째로 번들하면
  100MB급이 된다. 작게 만들 유일한 길이 **NativeAOT**(단일 네이티브 exe, 런타임 불필요)다.
- **NativeAOT 가능한 GUI만 후보다.** 공식 Native AOT 제약에 "Windows: No built-in COM"이 있어,
  내장 COM에 의존하는 **WPF와 WinForms는 AOT 불가**(WinForms는 클립보드/드래그드롭/다이얼로그 등
  내장 COM 사용, dotnet/sdk#34129). 이들을 self-contained로 내면 런타임 번들로 100MB급이 되어
  "작게" 전제가 깨진다. 남는 AOT 후보는 순수 Win32/PInvoke, Avalonia, MewUI 셋뿐이다.
- **확실성이 최우선.** 이 도구의 본질은 "예쁜 UI"가 아니라 vGPU-off 바 WSB에서 반드시 받아 Spork를
  띄우는 것이다. Express `.wsb`는 `<vGPU>Disable</vGPU>`라 GPU 가속이 없어 D2D/Skia는 소프트웨어
  폴백으로 떨어지지만, **순수 GDI는 GPU 의존이 0**이라 가장 확실하다. 필요한 UI(진행 막대 + 라벨 +
  버튼 2개)는 COM 없는 Win32/GDI로 충분하고, 페이로드(HTTP/TLS/압축/SHA256)가 크기 하한을 정하므로
  UI 프레임워크의 MB 차이는 이 용도에서 부차적이다. 따라서 **Win32/CsWin32(GDI)** 로 확정한다.

## 2. 확정된 결정 (2026-07-05)

| # | 결정 | 확정 |
| --- | --- | --- |
| 1 | 최신 버전 해석 | **인자 수신 + GitHub 폴백**: `{arch}` zip URL 템플릿을 인자로 받으면 그걸 쓴다(정상/스펙 B). 인자가 없으면(예: 사용자 VM에서 exe 단독 실행) GitHub Releases API로 최신을 스스로 해석한다. |
| 2 | UI 스택 | **Win32/GDI(순수 P/Invoke) + NativeAOT**. GPU 비의존(vGPU-off 확실성), 제3자 런타임 리스크 0, COM 미사용으로 AOT 클린. P/Invoke는 CsWin32 없이 손수 작성한 `[LibraryImport]`(외부 패키지 0). WPF/WinForms는 내장 COM으로 AOT 불가라 제외, Avalonia(과함)/MewUI(POC 리스크)도 제외. |
| 3 | `.ps1` 역할 | **얇은 shim**: DNS 선보정 + arch 판별 + 부트스트래퍼 exe 다운로드 + 인자 포워딩. SPEC §4의 다운로드/검증/해제/실행 단계는 exe로 위임. |
| 4 | `.wsb` 계약 | **불변**(SPEC §3.3). exe URL은 `.ps1`의 정적 기본값(스펙 B 유지)이라 `.wsb` 플레이스홀더가 늘지 않는다. |

## 3. 아키텍처 / 흐름

```text
LogonCommand (정적, .wsb 그대로: SPEC §3.2)
  └─ spork-bootstrap.ps1  (얇은 shim, 정적)
       1) DNS 선보정 (netsh 8.8.8.8 / 1.1.1.1)   ← 닭-달걀: 어떤 다운로드보다 먼저, ps1에 유지
       2) arch 판별 (PROCESSOR_ARCHITECTURE / ARCHITEW6432) → x64 | arm64
       3) 부트스트래퍼 exe 다운로드 (arch별)  (+ 선택: exe SHA256 검증)
       4) exe 실행, 인자 포워딩: -PortableZipUrlTemplate / -SiteIds / -Sha256Map
              │
              ▼
  Spork.Bootstrapper.exe  (Win32/GDI + NativeAOT, 단일 네이티브 exe. 측정 x64 4.7MB)
       A) URL 확정: 인자 템플릿의 {arch}를 자기 프로세스 arch로 치환.
                    인자가 없으면 → GitHub Releases 폴백으로 최신 zip URL 해석.
       B) 다운로드 (결정형 진행률: Content-Length 기반 스트림 카운트)
       C) SHA256 검증 (Sha256Map에 해당 arch 항목이 있으면. 불일치 시 중단)
       D) Expand → Desktop\Spork
       E) Spork.exe <SiteIds…> 실행
       (실패 시: 창 유지 + 에러 표시 + 재시도, Desktop\spork-bootstrap.log 기록)
```

핵심: `.wsb`의 4개 플레이스홀더 계약이 그대로다. `.ps1`은 여전히 `__SPORK_BOOTSTRAP_URL__` 자리에서
서빙되고, exe는 그 뒤의 구현 디테일로 숨는다. SPEC §4의 논리 단계는 동일하게 일어나되, 다운로드 이후가
`.ps1`이 아니라 exe에서 실행된다.

## 4. 컴포넌트 책임 분담

| 단계 | `spork-bootstrap.ps1` (shim) | `Spork.Bootstrapper.exe` |
| --- | --- | --- |
| DNS 선보정 | **담당** (닭-달걀. exe/zip 어떤 다운로드보다 먼저) | 안 함(이미 됨). Spork 내부 DNS 보정은 기존대로 Spork가 나중에 수행 |
| arch 판별 | **담당** (올바른 exe를 받기 위해) | 자기 `RuntimeInformation.ProcessArchitecture`로 `{arch}` 치환 |
| 부트스트래퍼 exe 획득 | **담당** (정적 기본 URL 템플릿) | 해당 없음 |
| 인자 전달 | **담당** (`.wsb`가 넘긴 3개 인자를 그대로 포워딩) | **수신** |
| zip URL 확정 | 안 함 | **담당** (인자 우선, 없으면 GitHub 폴백) |
| zip 다운로드/진행률 | 안 함 | **담당** (결정형 진행률) |
| SHA256 검증 | (선택) exe 자신에 대해서만 | **담당** (zip에 대해. Sha256Map 있으면) |
| 압축 해제 / 실행 | 안 함 | **담당** |
| 오류 표시 / 재시도 | 최소(콘솔) | **담당** (창 유지, §4.2) |

## 5. 부트스트래퍼 인자 계약

exe는 `.ps1`이 포워딩하는 인자를 받는다. 이름/의미는 [SPEC §4](PARAMETERIZED_WSB_SPEC.md)의 `.ps1`
파라미터를 승계한다. 인자가 전혀 없어도(사용자가 exe만 더블클릭) 동작해야 한다(§6 폴백).

| 인자 | 필수 | 의미 | 예시 |
| --- | --- | --- | --- |
| `--zip-url-template` | 선택 | `{arch}` 토큰 포함 zip URL 템플릿. 있으면 정상 경로 | `https://…/Spork_1.20.4.0_Release_{arch}_Portable.zip` |
| `--site-ids` | 선택 | 공백 구분 사이트 `Id`. 비면 일반 런처 | `Shinhan KB` |
| `--sha256-map` | 선택 | `x64=…;arm64=…` 형태 체크섬 맵 | `x64=ab12…;arm64=cd34…` |
| `--github-repo` | 선택 | 폴백 대상 `owner/repo`. 기본은 공식 저장소 | `yourtablecloth/TableCloth` |
| `--dest` | 선택 | 해제 대상. 기본 `%USERPROFILE%\Desktop\Spork` | |

미치환 플레이스홀더(`__SPORK_PORTABLE_ZIP_URL_TEMPLATE__` 같은 값)가 그대로 들어오면 "값 없음"으로
간주해 폴백으로 넘어간다(치환 누락을 조용한 실패가 아니라 폴백으로 흡수).

## 6. 최신 버전 해석 (인자 → 고정 URL → GitHub API 폴백)

**우선순위:** (1) `--zip-url-template`(Express 레인, 웹앱/MCP 공급) → (2) **고정 URL**(GitHub
`latest/download`의 버전프리 별칭 `Spork_{arch}_Portable.zip`, 런처 baked default. HttpClient가 리다이렉트를
따라감) → (3) GitHub API 폴백(아래). 인자가 있으면 (2)(3)을 건너뛴다. 고정 URL이 404(별칭 없는 구 릴리스
등)면 (3)으로 폴백한다.

> 실측(2026-07-05): 무인자 실행 시 고정 URL 시도 → (별칭 미존재) 404 → API 폴백 → 버전드 자산 해석
> 순으로 동작 확인. 별칭이 포함된 릴리스부터는 (2)에서 바로 성공(무-API).

**(3) GitHub API 폴백 절차:**

1. `GET https://api.github.com/repos/{owner}/{repo}/releases/latest` (`User-Agent` 헤더 필수).
2. `assets[]`에서 이름이 `Spork_`로 시작하고 `_{arch}_Portable.zip`으로 끝나는 자산의 `browser_download_url`
   채택(정규식 아닌 접두/접미 문자열 비교). `{arch}`는 자기 프로세스 arch(x64 | arm64).
   실측(2026-07-05): v1.20.4 릴리스에서 `Spork_1.20.4.0_Release_x64_Portable.zip`을 정확히 유일 매칭.
3. (선택) 릴리스에 `*_SHA256SUMS.txt`류 체크섬 자산이 있으면 받아 파싱해 검증. 없으면 검증 생략
   (SPEC §11 결정 3: 있으면 검증, 강제 아님).

**주의:**

- **레이트리밋:** 비인증 GitHub API는 IP당 시간 60회. 폴백은 보조 경로이고 호출 1회라 무해하지만,
  정상 Express 경로가 인자를 공급해 GitHub를 아예 타지 않는 게 기본이다.
- **AOT 안전:** GitHub 릴리스 JSON은 `System.Text.Json` **소스 생성 컨텍스트**(`JsonSerializerContext`)로
  파싱한다(리플렉션 회피, 트리밍/AOT 경고 없음).

## 7. UI / UX 규약

- **상태 흐름:** 준비 → 다운로드(결정형 진행률: 수신 바이트 / `Content-Length`. 헤더 없으면 비결정형) →
  검증(SHA256) → 해제 → 실행. 각 상태에 짧은 한국어 레이블과 arch 표기.
- **오류:** 어떤 단계 실패든 **창을 유지**하고 원인 메시지 + 재시도/닫기 버튼을 보인다(SPEC §4.2 충족).
  진단은 `%USERPROFILE%\Desktop\spork-bootstrap.log`에도 남긴다(Express는 호스트로 로그를 뺄 수 없음).
- **성공:** Spork.exe 기동 후 자동 종료(짧은 지연 허용). 성공 시엔 즉시 닫아도 무방(§4.2는 실패 케이스 규정).
- **다운로드 중 취소 확인:** 다운로드/작업이 진행 중일 때 닫기(닫기 버튼 + **표준 닫기**: 제목표시줄 X /
  Alt+F4 / 시스템 메뉴)를 시도하면 취소 확인 대화상자(`MessageBox`, 기본 포커스 '아니오')를 띄우고 '예'일
  때만 닫는다. 오류/취소안내/유휴 상태에선 바로 닫는다. 두 닫기 경로 모두 공통 `TryClose`를 거치고, 진행
  여부는 `s_workerRunning`으로 판단. 취소 = 창 닫기 = 프로세스 종료(다운로드는 프로세스 종료로 중단).
- **정직한 가치:** `Invoke-WebRequest`도 진행률 표시는 가능하다. GUI의 실이득은 (1) "멈춘 게 아니라 받는 중"
  이라는 시각적 안심, (2) 사라지지 않는 오류, (3) 재시도, (4) 깔끔한 결정형 진행률이다.
- **구현 형태:** 클래식 Win32 다이얼로그 1개. `CreateWindowEx` 최상위 창 + `InitCommonControlsEx`로 켠
  `msctls_progress32` 진행 막대 + 정적 텍스트 + 버튼 2개(재시도/닫기). comctl32 v6 매니페스트로 테마 적용.
  다운로드는 백그라운드 스레드에서 돌리고 진행값은 `PostMessage`(`PBM_SETPOS`)로 UI 스레드에 전달. COM 미사용.
- **Spork 실행(권한 상승):** Spork.exe 는 `requireAdministrator` 매니페스트라 `Process.Start` 를
  **`UseShellExecute=true`(ShellExecute)** 로 호출해야 상승된다(`false` 는 Win32 error 740 ELEVATION_REQUIRED
  로 실패 — 런타임 실측으로 발견). 샌드박스의 이미 상승된 LogonCommand 트리에선 프롬프트 없이, 일반
  호스트(패턴 A)에선 UAC 1회. 참조 `spork-bootstrap.ps1` 의 `Start-Process`(기본 ShellExecute)와 동일 동작.
- **실행 취소는 오류가 아니다:** 사용자가 그 UAC 상승을 취소하면 `ERROR_CANCELLED`(1223)이 온다.
  이를 오류로 처리하지 않고 **"다시 실행" 안내**로 분기한다(`WM_APP_CANCELED`). 이미 받아둔 파일을
  재다운로드 없이 **실행만 재시도**하는 버튼을 노출한다(`s_launchOnlyRetry`). 일반 오류(재시도=처음부터)와
  구분된다.
- **다국어(i18n):** 모든 UI 문자열은 [Localization.cs](../src/Spork.Bootstrapper/Localization.cs)의 `Loc`
  리소스 테이블(현재 `ko`/`en`)에서 온다. 브랜드는 **"식탁보"** 로 통일("무설치 식탁보" 표현 폐기).
  언어 선택은 Win32 `GetUserDefaultUILanguage` 자동 감지(+ `--lang` 강제). .NET 컬처/위성 어셈블리를
  쓰지 않아 **InvariantGlobalization(크기)** 을 유지하면서 NativeAOT 동적 로딩 제약도 피한다. 새 언어는
  `UiStrings` 인스턴스 하나 추가 + `Loc.Resolve` 매핑이면 된다.
- **High DPI(PerMonitorV2):** 매니페스트를 PerMonitorV2 로 두고, `GetDpiForWindow` 로 배율을 얻어 폰트
  (`CreateFontW`, 9pt × dpi/96)와 자식 컨트롤 좌표/창 크기를 스케일한다. 모니터 간 이동 시 `WM_DPICHANGED`
  의 제안 사각형으로 창을 옮기고 재-스케일한다. 폰트 페이스도 언어 테이블에서(한국어 Malgun Gothic /
  영어 Segoe UI).
- **아이콘:** EXE 파일 아이콘은 `<ApplicationIcon>` 로 TableCloth 메인 앱과 동일한 [App.ico](../src/TableCloth/App.ico)
  를 임베드. 창/작업표시줄 아이콘은 런타임에 `ExtractIconExW`(자기 exe, 인덱스 0)로 꺼내 클래스
  `hIcon`/`hIconSm` + `WM_SETICON` 으로 적용(제목표시줄=small, 작업표시줄=big).
- **최소화 버튼 없음:** 창 스타일에서 `WS_MINIMIZEBOX` 제외(`WS_CAPTION | WS_SYSMENU` 로 닫기 버튼만).
- **작업 표시줄 진행률:** `ITaskbarList3` 로 작업 표시줄 버튼에 진행률을 투영한다. NativeAOT 는 내장 COM
  불가라 **소스 생성 COM**(`[GeneratedComInterface]` + `StrategyBasedComWrappers`)으로 구현
  ([TaskbarInterop.cs](../src/Spork.Bootstrapper/TaskbarInterop.cs)). 상태 매핑: 다운로드=NORMAL(값 %),
  버전확인/검증/해제=INDETERMINATE, 완료=NOPROGRESS, 오류=ERROR(빨강), 실행취소=PAUSED(노랑). 모든 COM
  호출은 UI(STA) 스레드에서만, 실패 시 조용히 무시(best-effort).

## 8. 크기 / NativeAOT 예산과 제약

- **크기(측정):** x64 self-contained 단일 exe **4.7MB**(2026-07-05, `OptimizationPreference=Size` +
  feature switch + 트리밍, ILC 경고 0). Spork 포터블 zip이 60~90MB급이라 선행 다운로드가 이 수준으로
  줄면 무반응 구간이 큰 폭으로 짧아진다. 하한은 UI가 아니라 HTTP/TLS/`System.IO.Compression`/SHA256
  (+GitHub JSON) 페이로드가 정한다.
- **P/Invoke 규칙:** UI는 순수 Win32/GDI 공통 컨트롤(`msctls_progress32` 등). P/Invoke는 CsWin32 없이
  손수 작성한 `[LibraryImport]`(외부 패키지 0)로 두고, 내장 `LibraryImportGenerator`가 마샬링을 소스
  생성해 리플렉션 0. **내장 COM 미사용**이라 AOT 클린(COM이 필요해지면 내장 COM이 아니라 소스 생성
  `[GeneratedComInterface]` ComWrappers로 간다). WndProc는 `[UnmanagedCallersOnly]` + 함수 포인터.
  `PublishAot=true`, arch별 `-r win-x64` / `-r win-arm64`.
- **툴체인:** NativeAOT는 빌드 에이전트에 MSVC(C++) 빌드 도구/`ilc` 링커 필요. CI(build.yml)는 각 arch를
  **네이티브 러너에서 게시**(x64=windows-latest, arm64=windows-11-arm)해 크로스컴파일이 불필요하다.
  로컬(build.cs)에선 x64에서 `-r win-arm64` 크로스컴파일도 가능(arm64 AOT 컴파일러 팩 필요).
- **의존 최소화:** HttpClient, `System.IO.Compression`, `System.Text.Json`(소스 생성) 외 제3자 패키지
  0개. 트리밍 경고는 전부 해소한다. **Sentry/원격 진단은 넣지 않는다**(크기 우선. Spork 본체가 이미
  Sentry 보유). 진단은 `Desktop\spork-bootstrap.log` 파일로만.

## 9. 신뢰 / 보안 모델

- **원격 실행 코드다.** exe는 게스트가 네트워크에서 받아 실행한다. [SPEC §10](PARAMETERIZED_WSB_SPEC.md)의
  서명 신뢰모델에 포함되어야 한다. 릴리스 경로에선 부트스트래퍼가 `.exe` 자산(버전드 + 버전프리 별칭)이라
  [tools/sign-release.ps1](../tools/sign-release.ps1)이 draft 릴리스의 모든 `*.exe`를 일괄 서명(Certum
  SimplySign)하므로 별도 서명 스텝이 필요 없다. 로컬 `build.cs`는 미서명 산출물만 만든다. 배포는 공식
  오리진만 안내한다.
- **이중 체크섬 지점:** (a) `.ps1`이 받는 exe는 (선택) 자기 체크섬으로, (b) exe가 받는 zip은 `Sha256Map`으로
  검증한다. 어느 쪽도 강제는 아니지만(자체 호스팅/오프라인 유연성, SPEC §11-3), 있으면 검증하고 불일치 시 중단.
- **마운트 0 불변:** 부트스트래퍼는 호스트 파일 접근이 없다. 게스트 내부 파일(zip, 해제물, 로그)만 다룬다.

## 10. 빌드 / 패키징 / 자산명 계약

- **신규 프로젝트:** `src/Spork.Bootstrapper/Spork.Bootstrapper.csproj` (AssemblyName `Spork.Bootstrapper`,
  손수 작성 `[LibraryImport]`(외부 패키지 0), `OutputType=WinExe`, `net10.0`, `PublishAot=true`).
  솔루션(`TableCloth.slnx`)/`build.cs`에 편입 완료.
- **publish:** arch별로 `dotnet publish src/Spork.Bootstrapper -c Release -r win-<arch> -p:PublishAot=true`.
  CI([build.yml](../.github/workflows/build.yml))는 매트릭스 레그에서 네이티브 게시 후 `releases\`에 스테이징,
  로컬([build.cs](../build.cs))도 동일 자산명으로 산출.
- **자산명(공개 계약):** 릴리스마다 아래 4종을 게시한다(버전드 = 아카이브/재현, 버전프리 = 고정 URL).
  - 런처 버전드: `SporkBootstrap_<4파트버전>_<config>_<arch>.exe`
  - 런처 버전프리(고정 URL): `SporkBootstrap_<arch>.exe`
  - 포터블 버전드(기존): `Spork_<4파트버전>_<config>_<arch>_Portable.zip`
  - 포터블 버전프리(고정 URL): `Spork_<arch>_Portable.zip`
- **고정 URL(런처 특성):** GitHub `latest/download`는 버전-비의존 자산명일 때 영구 URL이 된다.
  - 런처 배포: `https://github.com/<repo>/releases/latest/download/SporkBootstrap_<arch>.exe`
    (`.wsb`/웹/사용자가 영구 링크로 참조).
  - 런처가 받는 포터블: `https://github.com/<repo>/releases/latest/download/Spork_<arch>_Portable.zip`
    (런처 **baked default**, §6-(2)). `latest`는 게시된 최신을 가리켜 draft→서명→게시 흐름과 안 부딪힌다.
- **서명:** §9대로 `.exe`(버전드+버전프리)는 `tools/sign-release.ps1`이 draft에서 일괄 서명.

## 11. `.wsb` / `spork-bootstrap.ps1` (데뷔: 간소화 인라인)

- **`.wsb` (데뷔 형태):** [tools/no-install/no-install-spork.wsb](../tools/no-install/no-install-spork.wsb)는
  **소형 포인터**다([SPEC §0.5](PARAMETERIZED_WSB_SPEC.md)): 보이는 PowerShell 창(-WindowStyle Normal)을
  띄우고, 그 안에서 DNS 선보정(인라인 잔류, 닭-달걀) → TLS 1.2 보정 → 준비 스크립트
  [tablecloth-prepare.ps1](../tools/no-install/tablecloth-prepare.ps1)(릴리스 자산, 고정 URL)을
  **Chocolatey식 `iex`+`DownloadString`으로 무파일 실행**. 준비 스크립트가 arch 판별 + **고정 URL로 런처
  exe 다운로드**(단계 메시지만. 바이트 진행률은 PS 5.1 IWR 렌더링 페널티 + 소용량 사유로 의도적으로 끔 —
  진짜 진행률 UX는 런처 GUI 소관) + 실행을 담당하고, 세션 스코프 실행이라 `exit`가 곧 창 닫기(완료 시 자동 종료).
  zip/체크섬 플레이스홀더 없음(일반 런처는 플레이스홀더 0), 웹앱 호스팅 없음(릴리스 자산만).
- **`spork-bootstrap.ps1` (레거시/완전형):** 데뷔 기본형은 ps1을 쓰지 않는다. §14-6의 "ps1 shim 개정"은
  간소화가 **대체**했다(shim 대신 인라인). ps1은 버전 핀/오프라인/사설 미러 등 완전 파라미터화형(SPEC §3~§4)의
  참조로만 보존하며, 헤더에 그 취지를 명시했다.
- **SPEC 반영 완료:** SPEC §0.5(간소화 기본형)에 반영. §3~§4는 완전형 계약으로 보존.

## 12. 스코프 밖 / 비목표

- 웹앱의 최신 태그 resolve, 플레이스홀더 치환, HTTPS 호스팅, exe 별칭 URL 게시.
- 호스트 옵션(테마/컬처) 주입(Express 기본값. SPEC §13 승계).
- 데이터 영속, 즐겨찾기 마운트(마운트가 생기면 Express 정의 이탈).
- 부트스트래퍼의 Sentry/원격 진단(로컬 로그 파일로만).

## 13. 열린 결정

- [x] ~~호스팅 exe 별칭 URL 안정형/버전드~~ → **버전프리 고정 URL**(GitHub `latest/download`)로 확정.
- [ ] 폴백 시 `SHA256SUMS` 자산 규약을 릴리스에 추가할지(추가 시 폴백에서도 검증 가능).
- [x] ~~`.ps1`의 잔여 무반응 구간 콘솔 알림~~ → 간소화로 ps1 자체가 기본형에서 사라져 무의미.
- [ ] exe 단독 실행(사용자 VM, 패턴 A) 시 UI에 "사이트 선택" 카탈로그를 노출할지, 순수 다운로더로 둘지.

## 14. 작업 항목 (구현 착수 시)

- [x] **1. 설계 문서**: 본 문서.
- [x] **2. 프로젝트 스캐폴딩**: `src/Spork.Bootstrapper` (Win32/GDI + `PublishAot`, `WinExe`), 솔루션/`build.cs`
      편입. `dotnet build` 클린(경고 0), NativeAOT x64 게시 성공(4.7MB, ILC 경고 0).
- [x] **3. 다운로드 코어**: HttpClient 스트림 + 결정형 진행률, SHA256 검증, `System.IO.Compression` 해제,
      Spork.exe 기동(위치 인자 = SiteIds). (`Program.cs`)
- [x] **4. GitHub 폴백**: Releases API + STJ 소스 생성 파싱 + 자산명 접두/접미 매칭(`Spork_*_{arch}_Portable.zip`)
      + 오류 처리. (`BootstrapOptions.cs`)
- [x] **5. 진행/오류 UI**: 상태 흐름, 재시도, 창 유지, `Desktop\spork-bootstrap.log`.
- [x] **6. (대체됨) `.ps1` shim → 간소화 인라인 `.wsb`**: SPEC §0.5 간소화가 ps1 shim을 대체. 데뷔 형태는
      [no-install-spork.wsb](../tools/no-install/no-install-spork.wsb) 인라인(ps1 없음). ps1은 완전형 참조로 보존.
- [x] **7. 서명/자산명 계약**: build.cs/build.yml rename + 버전프리 별칭 완료. 서명은 `tools/sign-release.ps1`이
      draft의 모든 `*.exe`(별칭 포함)를 일괄 처리(별도 스텝 불필요).
- [x] **8. 런타임 실측(컨트롤드)**: 실제 GitHub 폴백 → 84MB 다운로드 → 해제까지 실행 검증. 런처 실행
      단계에서 Spork 의 `requireAdministrator` 로 인한 **error 740** 을 발견해 `UseShellExecute=true` 로 수정.
      로컬 HTTP + 더미 Spork.exe 로 실행/인자전달/작업디렉터리까지 재검증(마커 확인). 오류 시 창 유지(§4.2)도
      실측 확인. 남은 것은 실제 창 시각 확인 + 샌드박스 내 동작(수동/샌드박스 세션).
- [x] **9. 정식 데뷔**: 간소화 인라인 `.wsb` 확정([no-install-spork.wsb](../tools/no-install/no-install-spork.wsb)),
      README "무설치(Express) 실행" 정식 승격, `.wsb`를 릴리스 자산으로 게시(build.yml/build.cs) + 릴리스 노트
      진입점 추가. 남은 것은 릴리스 컷(버전 범프 + 태그 → 서명 → 게시)뿐.

## 변경 이력

- (초안, 2026-07-05) 확정 결정(인자 수신 + GitHub 폴백 / Win32/CsWin32(GDI) + NativeAOT / `.ps1` shim /
  `.wsb` 불변)을 반영해 최초 작성. 부모 계약([PARAMETERIZED_WSB_SPEC](PARAMETERIZED_WSB_SPEC.md),
  [PORTABLE_MODE2_TODO](PORTABLE_MODE2_TODO.md))에서 자산명/부트스트랩 계약 승계.
- (정정, 2026-07-05) UI 스택을 Avalonia에서 Win32/GDI로 변경. WinForms/WPF는 내장 COM으로
  NativeAOT 불가(공식 제약 "No built-in COM", dotnet/sdk#34129)임을 확인해 후보에서 제외했고,
  vGPU-off 확실성 + 제3자 런타임 리스크 0을 위해 순수 GDI를 채택.
- (스캐폴딩, 2026-07-05) `src/Spork.Bootstrapper` 생성. P/Invoke는 CsWin32 대신 손수 작성한
  `[LibraryImport]`(외부 패키지 0)로 결정(API 표면 소규모, 최대 확실성). `dotnet build` 클린 +
  NativeAOT x64 게시 성공(단일 exe 4.7MB, ILC 경고 0). 솔루션/`build.cs` 편입, 자산명
  `SporkBootstrap_<ver>_<config>_<arch>.exe`.
- (런타임 실측 + 수정, 2026-07-05) AOT exe 를 실제 실행: GitHub 폴백 자산 매칭 → 84MB 다운로드 →
  해제까지 정상. 런처 실행에서 Spork `requireAdministrator` 로 인한 Win32 error 740 발견 →
  `LaunchSpork` 를 `UseShellExecute=true` 로 수정(참조 ps1 의 `Start-Process` 와 동일). 로컬 HTTP +
  더미 Spork.exe 로 실행/인자/작업디렉터리 재검증 통과. `.ps1` shim 개정(§14-6)과 서명(§14-7)은 미착수.
- (기능 확장, 2026-07-05) (1) 브랜드 "식탁보"로 통일. (2) 다국어 리소스 테이블(`Loc`, ko/en) +
  `GetUserDefaultUILanguage` 자동 감지 + `--lang`. (3) High DPI PerMonitorV2(폰트/좌표 스케일 +
  WM_DPICHANGED). (4) 실행 취소(error 1223)를 오류 아닌 "다시 실행" 안내로 분기(재다운로드 없이 실행만
  재시도). AOT 게시 4.72MB(크기 거의 불변, ILC 경고 0). 실측: happy 경로(ko) 정상, 창 제목 en=
  "TableCloth Setup"/ko="식탁보 준비" 확인. DPI 시각 스케일과 취소(UAC 거부) 분기는 대화형 확인 필요.
- (기능 확장 2, 2026-07-05) (5) EXE/창/작업표시줄 아이콘을 TableCloth App.ico 로 통일(ApplicationIcon +
  ExtractIconExW + WM_SETICON). (6) 최소화 버튼 제거(WS_MINIMIZEBOX 제외). (7) 작업 표시줄 진행률
  (ITaskbarList3, 소스 생성 COM). AOT 게시 4.93MB(+0.2MB, ILC 경고 0). 실측: EXE 임베드 아이콘 1개 확인,
  창 스타일에 MINIMIZEBOX 없음/SYSMENU 있음 확인, happy 경로(작업표시줄 COM 활성)에서 크래시 없이 실행/
  자기종료 확인. 작업표시줄 진행 막대와 창 아이콘의 시각 표시는 대화형 확인.
- (URL 고정 + CI/CD, 2026-07-05) 런처에 **고정 URL**(GitHub `latest/download` 버전프리 별칭)을 baked
  default 로 추가, API 폴백 유지(§6). build.cs/build.yml 이 버전프리 별칭(`Spork_<arch>_Portable.zip`,
  `SporkBootstrap_<arch>.exe`)을 릴리스 자산으로 게시. CI(build.yml)에 런처 NativeAOT 네이티브 게시 +
  자산 스테이징 + 심볼 + 릴리스 노트 링크 추가. 서명은 기존 `sign-release.ps1`이 `*.exe`를 일괄 처리.
  실측: 무인자 실행 시 고정 URL 404 → API 폴백 동작 확인(현 릴리스엔 별칭 부재).
- (취소 확인, 2026-07-05) 다운로드/작업 진행 중 닫기(닫기 버튼 + 표준 닫기 WM_CLOSE) 시 취소 확인
  대화상자(`MessageBox`) 추가. 두 경로 모두 공통 `TryClose`를 거치고 `s_workerRunning`으로 진행 여부 판단.
  실측: 진행 중 close→인터셉트(프로세스 유지), 유휴/오류 close→직접 종료 확인. AOT 4.94MB.
- (정식 데뷔, 2026-07-05) `tools/no-install/no-install-spork.wsb`를 **간소화 인라인 기본형**(고정 URL로 런처
  직접 다운로드, ps1 없음)으로 확정. `spork-bootstrap.ps1`은 완전형 참조로 격하(헤더 명시). README를
  "무설치(Express) 실행" 정식 기능으로 승격, `.wsb`를 릴리스 자산으로 게시(진입점 고정 URL). §11/§13/§14
  갱신(§14-6은 간소화가 대체). 남은 것은 릴리스 컷.
- (LogonCommand 리팩토링, 2026-07-05) 콜드부팅 피드백 작업 중 `.wsb` 인라인이 비대해져(약 2,400자), 준비
  로직을 `tools/no-install/tablecloth-prepare.ps1`(릴리스 자산, 고정 URL)로 분리. `.wsb`는 보이는 창 기동 +
  DNS 프로브 + ps1 다운로드/dot-source만 남는 소형 포인터(약 1,000자). §11 갱신. 상세는
  [SPEC §0.5](PARAMETERIZED_WSB_SPEC.md) 및 변경 이력 참조.
