# 이슈 #304 트리아지 작업 폴더

> 상태: **제보자 진단 결과 대기** · 2026-07-26
> 분석 본문: [docs/ISSUE_304_TRIAGE.md](../../docs/ISSUE_304_TRIAGE.md)
> 이슈: [#304](https://github.com/yourtablecloth/TableCloth/issues/304) — Windows 11 25H2에서 샌드박스는 뜨지만 Spork가 실행되지 않고 아무 메시지도 없음

이 폴더는 #304를 재현·판별하기 위한 도구 모음입니다. 원인이 확정되면 폴더째 정리하거나
재사용 가능한 부분만 `tools/` 상위로 승격시키면 됩니다.

## 어떤 스크립트를 언제 쓰나

| 파일 | 실행 주체 | 목적 |
| --- | --- | --- |
| [`../diagnose_sandbox_boot.cmd`](../diagnose_sandbox_boot.cmd) | **제보자** | 식탁보가 띄운 **실제 세션** 안에서 실행. 증거를 `tablecloth-diag.txt`로 모아 호스트에서 회수 |
| `local-repro.cmd` + `run-local-repro.ps1` | **메인테이너** | 식탁보와 무관한 **맨 샌드박스**를 띄워, 게스트의 SAC 기본 상태와 우회 성공 여부를 직접 측정 |

두 스크립트는 목적이 다릅니다. 전자는 "제보자 환경에서 무슨 일이 벌어졌나", 후자는
"이 빌드에서 우회책이 아직 유효한가"를 봅니다. 헷갈리지 않게 분리해 두었습니다.

## 로컬 재현 실행

```powershell
cd tools/issue304
./run-local-repro.ps1
```

`local-repro.wsb.template`의 플레이스홀더를 실제 경로로 치환해 `local-repro.wsb`를 만들고 실행합니다
(wsb의 `HostFolder`는 절대 경로만 받으므로 템플릿 구조를 씁니다). 게스트에서 `local-repro.cmd`가
LogonCommand로 돌면서 이 폴더에 `result.txt`를 남기고, 콘솔을 열어 둔 채 대기합니다.

기본적으로 Velopack 설치 위치(`%LocalAppData%\TableCloth\current`)를 [04] 프로브 대상으로 붙입니다.
게시 산출물로 바꾸려면:

```powershell
./run-local-repro.ps1 -TableClothDirectory "D:\Projects\TableCloth\publish\win-x64"
```

생성물(`local-repro.wsb`, `result.txt`)은 `.gitignore` 처리되어 있습니다.

## 확인할 것 (result.txt 읽는 법)

| 섹션 | 보는 것 | 의미 |
| --- | --- | --- |
| `[00]` | 이 줄이 있는가 | 없으면 **LogonCommand 자체가 실행되지 않았다**(가설 H2 성립) |
| `[01]` | `VerifiedAndReputablePolicyState`, `SmartAppControlState` | 게스트 SAC 기본값. `1`/`2`면 H1의 전제가 성립 |
| `[03]` | 우회 적용 **후** 같은 값 | 여전히 `1`/`2`면 **reg + citool 우회가 이 빌드에서 더 이상 듣지 않는다**는 직접 증거 |
| `[04]` | 20초 뒤 `tasklist` | 비어 있으면 프로세스가 생성 즉시 죽었다는 뜻 |
| `[05]` | CodeIntegrity 이벤트 | `TableCloth.exe`가 나오면 커널 차단 확정 |

## 이력

- 2026-07-26 — 조사 착수. 부팅 체인 분석, 가설 H1(SAC 커널 차단)/H2(LogonCommand 미실행·마운트 레이스) 수립.
  제보자에게 진단 요청 댓글 게시([전문](reporter-comment-2026-07-26.md)). 로컬 재현은 **미실행**.

메인테이너 PC가 제보자와 동일 빌드(25H2 26200.8875)이고 Windows Sandbox는 MSIX 앱
`MicrosoftWindows.WindowsSandbox 0.8.107.0`입니다. 자세한 환경 사실과 대응 후보는 분석 본문 §6, §7 참조.
