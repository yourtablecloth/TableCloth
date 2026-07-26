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
| `repro-boot-chain.ps1` + `startup-script.cmd.template` | **메인테이너** | 부팅 체인(LogonCommand → StartupScript → Spork)을 식탁보 UI 없이 재현. 단계별 브레드크럼으로 **어디까지 갔는지**를 본다 |
| `local-repro.cmd` + `run-local-repro.ps1` | 메인테이너 | (1차 조사용) 게스트의 SAC 기본 상태 측정. **SAC 가설이 기각되어 역할은 끝났다** |

세 스크립트의 질문이 다릅니다. 첫째는 "제보자 환경에서 무슨 일이 벌어졌나",
둘째는 "우리 부팅 체인이 어느 줄에서 멈추나", 셋째는 "SAC가 범인인가"(→ 아니었음)입니다.

## 부팅 체인 재현 (현재 주력)

```powershell
cd tools/issue304
./repro-boot-chain.ps1 -Mode repro   # 수정 전: citool 에 stdin 리다이렉트 없음
./repro-boot-chain.ps1 -Mode fixed   # 수정 후: citool --refresh <nul
```

`%TEMP%\tablecloth-issue304\App\` 을 staging 으로 만들고(설치 폴더 복사 + StartupScript 생성),
그 폴더를 읽기·쓰기로 마운트한 wsb 를 실행합니다. 게스트가 진행하는 동안 브레드크럼이
같은 폴더의 `boot.log` 에 쌓이므로 **호스트에서 실시간으로 읽을 수 있습니다.**

| boot.log 마지막 줄 | 의미 |
| --- | --- |
| 파일 자체가 없음 | **LogonCommand 가 실행되지 않았다** (§3 H2) |
| `[07] calling citool` 에서 끊김 | **citool 이 stdin 을 기다리며 멈췄다** — 확정된 원인 |
| `[10] launching TableCloth.exe` 까지 도달 | 부팅 체인 정상 — 수정이 유효 |

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

- **2026-07-26 1차** — 조사 착수. 부팅 체인 분석, 가설 H1(SAC 커널 차단)/H2(LogonCommand 미실행) 수립.
  제보자에게 진단 요청 댓글 게시([전문](reporter-comment-2026-07-26.md)).
- **2026-07-26 2차** — 제보자 진단 결과 수신. **H1 기각**(게스트 SAC는 평가 모드이고 미서명 exe가 정상 생존).
  citool 대기 발견.
- **2026-07-26 3차** — `repro-boot-chain.ps1` 로 로컬 재현 시도. **제보자 증상은 재현되지 않음.**
  LogonCommand는 정상 동작하고 citool도 대기하지 않는다. 대신 확인된 실패 경로를 막고
  다음 재현에서 증거가 남도록 PR [#305](https://github.com/yourtablecloth/TableCloth/pull/305) 반영
  (citool `<nul`, 부팅 브레드크럼, 실행 실패 안내, 회귀 테스트 5종).
- **2026-07-26 4차** — 제보자에게 테스트 빌드 전달([댓글 전문](reporter-comment-2026-07-26-b.md)).
  CI 아티팩트 `TableCloth-Portable-x64`(약 101MB). **`tablecloth-boot.log` 회신 대기 중.**

### 테스트 빌드를 다시 전달해야 할 때

CI 아티팩트는 **5일 후 만료**되므로 위 댓글의 링크는 곧 죽습니다. 브랜치를 다시 푸시해 CI 를
돌린 뒤 `TableCloth-Portable-<arch>` 아티팩트 링크를 새로 잡아 주세요. 이 경량 아티팩트는
`build.yml` 에 별도 스텝으로 추가돼 있습니다(Velopack-* 는 651MB 라 제보자에게 부담).

```bash
gh api "repos/yourtablecloth/TableCloth/actions/runs/<RUN_ID>/artifacts" \
  --jq '.artifacts[] | select(.name|startswith("TableCloth-Portable")) | "\(.name) id=\(.id)"'
# 링크 형식: https://github.com/yourtablecloth/TableCloth/actions/runs/<RUN_ID>/artifacts/<ARTIFACT_ID>
```

### 하니스를 쓸 때 반드시 지킬 것

**게스트에서 실행할 스크립트는 순수 ASCII로 작성한다.** 초기 템플릿에 한글 주석을 넣었더니
UTF-8로 기록된 바이트를 cmd가 OEM 코드페이지로 읽으면서 스크립트가 통째로 무동작이 되었고,
"LogonCommand가 실행되지 않는다"는 **잘못된 결론을 한 번 냈습니다.** 제품 쪽은
`SandboxStartupScriptTests`가 이 조건을 테스트로 고정해 두었습니다.

메인테이너 PC는 제보자와 동일 호스트 빌드(25H2 26200.8875)이고 Windows Sandbox는 MSIX 앱
`MicrosoftWindows.WindowsSandbox 0.8.107.0`입니다. **게스트 OS는 호스트와 달리 26100.8875** —
샌드박스 앱이 자체 베이스 이미지를 들고 다닙니다. 자세한 환경 사실은 분석 본문 §0, §6 참조.
