# 샌드박스 진입 무음 실패 트리아지 — Spork가 뜨지 않음 (이슈 #304)

> 상태: **SAC 가설 기각 · citool 대기 경로는 방어 수정 반영 · 근본 원인 미확정(로컬 재현 실패)** · 2026-07-26
> 최신 결론은 §0 을 먼저 읽을 것. §3 의 가설 서술은 진단 결과가 오기 전의 기록이다.
> 대상: 식탁보 1.20.7(Retail/WPF), Windows 11 25H2 빌드 26200.8875
> 선행 이슈: [#256](https://github.com/yourtablecloth/TableCloth/issues/256)(SAC 차단) ·
> [#277](https://github.com/yourtablecloth/TableCloth/issues/277)(#256 중복) ·
> [#237](https://github.com/yourtablecloth/TableCloth/issues/237)(DNS/hang)

## 0. 결론 (2026-07-26 2차 — 제보자 진단 결과 반영)

### 0-1. SAC 가설(§3 H1)은 **기각**

제보자 환경의 게스트에서:

| 항목 | 실측값 | 해석 |
| --- | --- | --- |
| `VerifiedAndReputablePolicyState` | `0x2` | **평가(evaluation) 모드 = 차단하지 않음** |
| 활성 CI 정책 | `VerifiedAndReputableDesktopEvaluation(+FlightSupplemental)` | 강제 정책 없음 |
| 미서명 `TableCloth.exe` 수동 실행 | **PID 4320, 20초 후에도 262MB 점유 생존** | 차단 없음 |
| CodeIntegrity 이벤트 | 차단(3077 등) 0건, 전부 정보성 refresh | 차단 없음 |

이슈 #256과는 **다른 문제**다. 코드 서명 유무는 이번 건과 무관하다.

### 0-2. 원인 — `citool --refresh`가 표준 입력을 기다린다

게스트 이미지 `10.0.26100.8875`에서 `citool.exe --refresh`는 작업을 마친 뒤
**"계속하려면 Enter 키를 누르세요."로 stdin을 기다린다.** 제보자가 직접 확인:

```text
C:\...\App>"C:\Windows\System32\citool.exe" --refresh
작업 성공
계속하려면 Enter 키를 누르세요.      <- CiTool.exe 가 살아서 대기
```

Enter를 치자 그 자리에서 Spork가 실행됐다. 즉:

- `StartupScript.cmd`에서 **블로킹될 수 있는 유일한 줄이 `citool`** 이다(`reg add`는 즉시 반환).
- 그 바로 다음 줄이 `TableCloth.exe spork`이므로 **Spork 실행에 영원히 도달하지 못한다**.
- 원본 스크립트는 이 줄이 `>nul 2>&1`로 묶여 있어 **프롬프트조차 보이지 않는다** = "아무 메시지 없음".

**수정**(반영됨): stdin을 nul로 리다이렉트해 프롬프트가 즉시 EOF를 받게 한다.

```bat
"%SystemRoot%\System32\citool.exe" --refresh <nul >nul 2>&1
```

### 0-3. 로컬 재현 결과 (2026-07-26 23:15, 메인테이너 PC)

[tools/issue304/repro-boot-chain.ps1](../tools/issue304/repro-boot-chain.ps1)로 부팅 체인을
그대로 재현했다. **결론: 제보자 증상은 로컬에서 재현되지 않으며, 아래 추정 몇 개가 뒤집혔다.**

```text
[00] logon batch started 23:15:20.15
[01] pushd ok cwd=C:\Users\WDAGUtilityAccount\Desktop\App
[01b] VerifiedAndReputablePolicyState  0x2      <- 게스트 기본값은 평가 모드
[02]~[05] reg add 4건 모두 errorlevel=0
[06] BEFORE citool : CI=0x0, Edge LNA=* (기록 성공)
[07] calling citool 23:15:21.15
[08] citool returned errorlevel=0 at 23:15:21.69   <- 0.54초, 대기 없음
[08b] citool 무(無)리다이렉트 프로브: 10초 뒤 "No tasks are running"  <- 프롬프트 안 뜸
[09] AFTER citool  : CI=0x0, Edge LNA=* (그대로 유지)
[10] launching TableCloth.exe spork 23:15:32.22    <- 체인 정상 완주
```

여기서 확정된 사실:

| 항목 | 결과 |
| --- | --- |
| LogonCommand가 마운트의 `.cmd`를 직접 실행 | **정상 동작** (로그온 후 6~9초). §3 H2 는 최소한 이 환경에선 성립하지 않는다 |
| 게스트 기본 SAC 상태 | **`0x2`(평가)** — 호스트가 `0x0`이어도 그렇다. 호스트 상태를 상속하지 않는다 |
| `citool --refresh` 대기 | **재현 안 됨.** stdin 리다이렉트 없이 백그라운드로 띄워도 10초 내 종료 |
| citool이 레지스트리를 되돌리는가 | **아니다.** `[06]`/`[09]` 값이 동일하게 유지된다 |

→ 따라서 §0-2에서 "citool 대기가 원인"이라고 본 것은 **제보자 환경에 국한된 조건부 현상**이다.
`<nul` 수정은 그 실패 경로 자체를 없애므로 유지하되, **제보자 증상의 근본 원인으로 단정할 수 없다.**
특히 `[09]`가 값 유지를 보여주므로, "citool이 `VerifiedAndReputablePolicyState`를 `0x2`로 되돌렸다"는
§0-2의 보조 설명도 성립하지 않는다. 제보자 환경에서 레지스트리 4건이 모두 없었다는 것은
**그 세션에서 배치가 실제로 실행되지 않았다**는 뜻에 가깝다.

> 하니스 자체의 함정: 초기 템플릿에 한글 주석을 넣었더니 UTF-8 로 기록된 바이트를 cmd 가 OEM
> 코드페이지로 읽으며 스크립트가 통째로 무동작이 되어, "LogonCommand 가 실행되지 않는다"는
> 잘못된 결론을 한 번 냈다. 실제 생성 스크립트는 순수 ASCII 이므로 이 문제가 없다.
> **게스트에서 실행할 진단 스크립트는 반드시 ASCII 로 작성할 것.**

### 0-4. 다음 단계

로컬 재현이 안 되므로 제보자 환경의 실제 진행 지점을 알아야 한다. §7-1의 **부팅 브레드크럼**을
제품에 넣어(영속 마운트 `Data`에 기록) 제보자가 한 번 실행하면 위와 같은 로그가 남게 하는 것이
가장 빠른 경로다.

### 0-4. 부수 확인

- 게스트 DNS 정상(`yourtablecloth.app` 해석 OK), PowerShell 5.1 정상 → §3 H3의 배제 판단 유효.
- 게스트에서 `Get-MpComputerStatus`의 `SmartAppControlState`는 빈 값 → SAC 상태는 레지스트리로만 판정할 것.
- 1차 진단 스크립트에 버그가 있었다: `reg query ... /v 1>> "%LOG%"`에서 cmd가 `1>>`을 stdout
  리다이렉션으로 파싱해 가장 중요한 항목이 무효화됐다. `/v "1"`로 수정 완료.

## 1. 증상

- 식탁보로 샌드박스를 띄우면 **샌드박스는 정상적으로 부팅**되고 바탕 화면에 `App` / `Data` / `NPKI` 폴더가 보인다.
- 그런데 Spork가 뜨지 않고, **오류 대화상자도 토스트도 콘솔 창도 전혀 없다.**
- 제보자는 같은 PC의 **Windows 10에서는 정상 동작**했고, Windows 11 인플레이스 업그레이드 후부터 재현. 버전을 올려도 동일.

바탕 화면의 세 폴더는 wsb `MappedFolders`가 붙었다는 뜻일 뿐, LogonCommand 실행 여부와는 무관하다
([SandboxMountPaths.cs](../src/TableCloth.Core/Models/WindowsSandbox/SandboxMountPaths.cs) 주석 참조 —
`SandboxFolder`를 쓰지 않으므로 모든 마운트는 `Desktop\<leaf>`로 노출된다).

## 2. 부팅 체인과 "무음" 구조

```text
wsb LogonCommand
  └─ Desktop\App\StartupScript.cmd                        SandboxBuilder.cs:258, 445-452
       ├─ reg add ...CI\Policy VerifiedAndReputablePolicyState=0     (SAC 끄기 시도)
       ├─ reg add ...Edge\HardwareAccelerationModeEnabled            (GPU OFF일 때)
       ├─ reg add ...Edge\LocalNetworkAccessAllowedForUrls           (항상)
       ├─ citool.exe --refresh
       └─ Desktop\App\TableCloth.exe spork <ids>
            └─ RunSpork → UseSpork → AppStartup.InitializeAsync → SandboxBootstrap → MainWindow
```

| 실패 지점 | 사용자에게 보이는 것 | 근거 |
| --- | --- | --- |
| LogonCommand 미실행 | **없음** | 배치가 안 돌면 콘솔 자체가 안 뜬다 |
| `TableCloth.exe` 프로세스 생성 실패 | **없음** | [SandboxBuilder.cs:449-450](../src/TableCloth.App/Components/Implementations/SandboxBuilder.cs#L449-L450) — exit code를 검사하지 않고 곧바로 `popd`. 콘솔이 즉시 닫혀 `액세스가 거부되었습니다` 도 사라진다 |
| Spork 프로세스 내부 예외 | 대화상자 | `Program.RunSpork`의 `catch` → `MessageBox` |
| 카탈로그 로드 실패 | 대화상자 | `AppStartup.InitializeAsync` 3회 재시도 후 critical 오류 |

**따라서 "메시지가 하나도 없다"는 것은 제보자 환경의 특이점이 아니라, 프로세스가 뜨기 전에 죽었다는 신호다.**
v1.14 시절(#256)에는 SAC 차단 시 대화상자가 보였으나, v1.20.0(c2ec7d7)에서 실행 지점이 cmd 배치로 옮겨가며 완전 무음이 되었다.

### 2-1. 로그가 남지 않는 이유

- [UseSporkExtensions.cs:62-64](../src/Spork.App/DependencyInjection/UseSporkExtensions.cs#L62-L64) — `AddSerilog()`만 호출하고
  **sink 구성이 리포지토리 어디에도 없다**(`LoggerConfiguration` / `WriteTo` 검색 결과 0건). `AddConsole()`은 WinExe라 무의미.
- Sentry는 프로세스가 떠야 전송되므로 "실행 전 실패"는 아무 데이터도 남기지 못한다.
- 호스트 staging(`%LocalAppData%\TableCloth.Data\Sandbox`)은 메인 창을 닫을 때
  [MainWindowViewModel.cs:62](../src/TableCloth.App/ViewModels/MainWindowViewModel.cs#L62)에서 통째로 삭제된다.
  → **로그를 남긴다면 영속 마운트인 `Data` 쪽**(기본값 `문서\TableCloth\Data`)이어야 한다.

## 3. 가설

### H1. Smart App Control이 미서명 `TableCloth.exe`를 커널 단에서 차단 (유력)

| 근거 | 내용 |
| --- | --- |
| 회귀 시점 | SAC는 Windows 11 전용 기능. "Win10 정상 → Win11 업그레이드 후 실패"와 정확히 일치 |
| 전례 | #256/#277이 **동일 증상**(`SAC에 차단되면서 아무런 반응 없음`) |
| 바이너리 서명 | 서명이 수동 경로(`build.cs --sign`, [sign-release.ps1](../tools/sign-release.ps1))라 CI 산출물은 미서명일 수 있다. 메인테이너 PC의 설치본(1.21.0-preview.1)은 `Get-AuthenticodeSignature` = **NotSigned** 확인. 1.20.7 릴리스 자산의 실제 서명 여부는 별도 확인 필요 |
| 우회책 수명 | `reg + CiTool -r` 우회가 최근 빌드에서 듣지 않는다는 보고 다수(MS Q&A). 2026년 들어 SAC 토글 방식 자체가 개편됨 |
| 무음성 | SAC 차단은 `PsCreateProcess` 커널 단에서 일어나 부모 프로세스는 그대로 진행 |

하위 시나리오: SAC는 신뢰되지 않은 출처의 `.exe/.bat/.cmd` 자체도 차단한다. `StartupScript.cmd`가 걸리면
**SAC를 끄는 코드가 SAC 때문에 실행되지 않는** 닭-달걀 구조가 되어 H1과 H2가 같은 증상으로 수렴한다.

### H2. LogonCommand 자체가 실행되지 않음 / 마운트 레이스

- 25H2에서 Windows Sandbox는 인박스 컴포넌트가 아니라 **MSIX 앱**(`MicrosoftWindows.WindowsSandbox`)으로 제공된다.
  #237에서 제보자가 추측했던 "24H2에서 샌드박스가 스토어 앱처럼 변했다"가 사실로 확인됨.
- LogonCommand가 마운트 완료보다 먼저 발사되면 `Desktop\App\StartupScript.cmd`를 찾지 못하고 조용히 끝난다.
  스토어 경유로 클라이언트 버전이 사람마다 다를 수 있어 "나는 되는데 저 사람은 안 되는" 양상과도 맞는다.

### H3. 배제된 후보

| 후보 | 배제 근거 |
| --- | --- |
| 게스트 PowerShell 미구성 | 08819eb에서 **부팅 경로의 PowerShell이 제거**됨. 현재 배치는 `reg`/`citool`만 사용. PowerShell은 UI 진입 **이후** 사이트별 설치 단계([PowerShellScriptRunStep.cs](../src/Spork.App/Steps/Implementations/PowerShellScriptRunStep.cs))에서만 쓰인다 |
| DNS/카탈로그 실패(#237류) | 실패 시 오류 대화상자가 뜬다. "메시지 없음"과 모순 |
| `citool --refresh` hang | 배치가 멈춰 있으면 검은 콘솔 창이 바탕 화면에 남아 있어야 한다 |
| 배치 파일 인코딩(mojibake) | 생성되는 스크립트 내용이 전부 ASCII(경로·서비스 ID). `Encoding.Default`(=UTF-8)여도 무해 |

## 4. 판별 프로토콜

### 4-1. 결정적 분기

게스트 안에서 아래 한 줄의 결과가 조사 범위를 절반으로 줄인다. 이 레지스트리 값은 `StartupScript.cmd`만 기록한다.

```cmd
reg query "HKLM\SOFTWARE\Policies\Microsoft\Edge\LocalNetworkAccessAllowedForUrls" /v 1
```

| 결과 | 해석 | 다음 단계 |
| --- | --- | --- |
| 값이 있음 | 배치는 실행됨 → `TableCloth.exe` 실행 단계에서 실패 | H1 계열. SAC 상태와 CodeIntegrity 이벤트 확인 |
| 값이 없음 | LogonCommand가 아예 안 돌았음 | H2 계열. 샌드박스 클라이언트 버전·마운트 타이밍 확인 |

### 4-2. 게스트 측 나머지

```cmd
reg query "HKLM\SYSTEM\CurrentControlSet\Control\CI\Policy" /v VerifiedAndReputablePolicyState
powershell -c "(Get-MpComputerStatus).SmartAppControlState"
cd /d "%USERPROFILE%\Desktop\App" && TableCloth.exe spork
wevtutil qe Microsoft-Windows-CodeIntegrity/Operational /c:30 /rd:true /f:text | findstr /i tablecloth
```

### 4-3. 호스트 측

```powershell
Get-AppxPackage -Name "*WindowsSandbox*" | Select-Object Name, Version
reg query "HKLM\SYSTEM\CurrentControlSet\Control\CI\Policy" /v VerifiedAndReputablePolicyState
```

## 5. 진단 도구

### 5-1. 제보자용 — 실제 세션에서 증거 수집

[tools/diagnose_sandbox_boot.cmd](../tools/diagnose_sandbox_boot.cmd) — §4의 항목을 한 번에 수집해
`tablecloth-diag.txt`로 남긴다.

1. 호스트의 **Data 폴더**(기본값 `문서\TableCloth\Data`)에 스크립트를 넣는다.
2. 식탁보로 평소처럼 샌드박스를 실행한다.
3. Spork가 뜨지 않으면 게스트에서 `%USERPROFILE%\Desktop\Data\diagnose_sandbox_boot.cmd` 실행.
4. Data는 읽기·쓰기 마운트라 결과 파일이 **호스트에서도 그대로 보인다** → 이슈에 첨부.

출력은 의도적으로 ASCII만 사용한다(게스트 콘솔 코드페이지가 보장되지 않아 한글은 깨질 수 있다).
제보자에게 실제로 게시한 안내 전문은 [tools/issue304/reporter-comment-2026-07-26.md](../tools/issue304/reporter-comment-2026-07-26.md)에 보존해 두었다.

### 5-2. 메인테이너용 — 맨 샌드박스로 우회책 유효성 측정

[tools/issue304/](../tools/issue304/) — 식탁보와 무관한 빈 샌드박스를 띄워 **게스트의 SAC 기본 상태**와
**`reg + citool` 우회가 이 빌드에서 아직 듣는지**를 직접 측정한다. 5-1이 "제보자 환경에서 무슨 일이
벌어졌나"라면 이쪽은 "이 빌드에서 우회책이 유효한가"를 본다.

```powershell
cd tools/issue304
./run-local-repro.ps1
```

읽는 법과 각 프로브의 의미는 [tools/issue304/README.md](../tools/issue304/README.md) 참조.

## 6. 확인된 환경 사실 (2026-07-26, 메인테이너 PC)

| 항목 | 값 | 의미 |
| --- | --- | --- |
| OS | Windows 11 Pro 25H2 **26200.8875** | 제보자와 **완전히 동일한 빌드** → 로컬 재현 시도 가능 |
| Windows Sandbox | MSIX `MicrosoftWindows.WindowsSandbox` **0.8.107.0** | 인박스가 아닌 스토어 배포 클라이언트(H2 관련) |
| 호스트 SAC | `VerifiedAndReputablePolicyState = 0` | 호스트는 꺼짐. **게스트 기본값은 별도 확인 필요** |
| 설치본 서명 | `NotSigned` (1.21.0-preview.1) | H1의 전제와 부합 |

## 7. 대응 후보

### 7-1. 원인과 무관하게 필요 (관측 가능성 확보)

1. **부팅 브레드크럼** — `StartupScript.cmd`가 단계별 결과를 `Desktop\Data\tablecloth-boot.log`에 append.
   staging이 아니라 영속 마운트라 세션 종료 후 호스트에서 회수 가능(§2-1).
2. **실행 실패 가시화** — `TableCloth.exe` 호출 뒤 `errorlevel`을 검사해 실패 시 System32의 서명 바이너리(`msg *`)로 안내하고
   콘솔을 유지. 현재는 실패가 100% 무음이다.
3. **Serilog sink 구성** — 최소한 파일 sink 하나. 지금은 로거를 등록해 놓고 아무 데도 쓰지 않는다.

### 7-2. H1이 맞을 때

4. **SAC 우회 사후 검증** — `citool --refresh` 후 상태를 다시 읽어 여전히 1/2면 로그에 남기고 사용자에게 안내
   (우회가 더 이상 통하지 않는 빌드를 조기 감지).
5. 정책 적용 타이밍 완충 — #256의 "샌드박스 안에서 재부팅하면 정상 동작" 관찰은 적용 지연을 시사. 짧은 대기 + 1회 재시도.
6. 근본 해결은 여전히 #256의 장기 과제(서명 + reputation 축적).

### 7-3. H2가 맞을 때

7. **마운트 레이스 방어** — LogonCommand를 `cmd.exe /c "…대기 루프…"`로 감싸 `StartupScript.cmd` 존재를 최대 N초 폴링한 뒤 호출.
   LogonCommand가 System32 바이너리만 참조하게 되는 부수 효과도 있다.

## 8. 참고

- [How do you turn off Smart App Control in Windows Sandbox? — Microsoft Q&A](https://learn.microsoft.com/en-us/answers/questions/5641557/how-do-you-turn-off-smart-app-control-in-windows-s)
  — `reg + CiTool.exe -r` 우회와 "최근 빌드에서 듣지 않는다"는 후속 보고.
- [Smart App Control — text/plain (2026-04-28)](https://textslashplain.com/2026/04/28/smart-app-control/)
  — 평가 모드/차단 표면화 방식, 2026년 토글 개편.
- [Codex Sandbox Is Silently Dead on Windows](https://terminalblog.com/blog/codex-smart-app-control-sandbox-fail/)
  — 미서명 자식 프로세스가 커널 단에서 차단되어 부모가 눈치채지 못하는 동일 패턴 사례.
