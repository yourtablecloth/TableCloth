# Windows Sandbox 내부 Codex CLI와 Computer Use 탐색 기록

2026년 10월 24일에 `poc/codex-sandbox-computer-use` 브랜치에서 탐색한 내용을 `develop` 문서로 보존했습니다. 과거 기록에는 Codex CLI `0.156.1`과 `0.164.0`을 사용한 Windows Sandbox 실행 두 건을 담았습니다. 두 게스트는 CLI 로컬 점검과 폐기 가능한 Service Control Manager(SCM) 서비스 검사를 통과했습니다. Computer Use는 구성하거나 시연하지 않았습니다.

이 문서에서는 런타임 준비, 게스트 격리, 버전별 관측, 플러그인 파서 수정과 Computer Use의 미검증 범위를 다룹니다. [영문 문서](codex-sandbox-computer-use.md)를 기본으로 제공하며 [증거 요약](codex-sandbox-computer-use-evidence.json)에 관측 결과와 출처 해시를 보존합니다.

원본 자료와 이번 문서 검토의 범위부터 살펴보겠습니다. 이어서 프로토타입의 격리 방식과 실제 관측을 설명하고 후속 탐색의 판정 기준을 정리합니다. 호스트에서 동작하는 식탁보 AI 구현은 [별도 보고서](managed-ai-runtime.md)에서 다룹니다.

다음 기준일 고지는 과거 실행 결과와 공개 문서 확인 시점을 한정합니다.

> 원본 자료는 2026년 10월 23일과 2026년 10월 24일의 실험을 기록했습니다. 이번 아카이브 작업에서는 2026년 10월 24일에 로컬 README와 스크립트 및 `validation.json`을 검토했습니다. Sandbox, Codex, 인증, 모델 요청과 서비스 검사를 재실행하지 않았습니다. 공식 문서도 같은 날 확인했으며 이후 런타임과 제품의 동작은 달라질 수 있습니다.

## 1. 미추적 연구 자료의 출처와 증거 범위

원본 자료의 출처를 정리하겠습니다. 탐색 브랜치는 커밋 `bf973738d423e69df39de0534d791a228677c6f0`을 가리켰지만 해당 Git 트리에는 `tools/codex-sandbox-poc`가 없었습니다. 로컬 연구 파일 8개는 Git이 추적하지 않았습니다. [증거 요약](codex-sandbox-computer-use-evidence.json)은 이 차이와 원본 파일의 SHA-256을 기록합니다. 브랜치 커밋은 주변 저장소의 기준점을 식별하며 실험 파일 자체를 보존하지 않습니다.

원본 `validation.json`은 자동 수집 결과와 사용자 보고를 구별합니다. 이번 작업에서는 해당 기록과 스크립트를 읽었습니다. 원본 게스트 증거를 독립적으로 재현하거나 패키지 아카이브의 진위를 다시 검증하지 않았습니다. 기록한 해시로 파일의 바이트를 비교할 수 있지만 해시만으로 출처나 실행 결과까지 입증할 수는 없습니다.

관측 결과는 다음 세 출처에서 가져왔습니다.

- 과거 게스트 bootstrap과 CLI 및 SCM 자동 수집 결과
- `0.156.1` TUI 진입과 로그인 및 인증 후 목록에 관한 과거 사용자 보고
- 별도 호스트 조사와 합성 fixture 검사 결과

## 2. 준비한 입력과 폐기 가능한 게스트 프로필

프로토타입의 준비와 격리 방식을 짚어보겠습니다. `Prepare-CodexSandbox.ps1`은 식탁보가 관리하는 런타임 디렉터리와 새 출력 디렉터리를 입력받았습니다. 스크립트는 receipt와 런타임 레이아웃, 로컬 경로, 파일 크기 제한 및 reparse point를 검사했습니다. 패키지와 게스트 스크립트를 복사한 뒤 입력 해시 manifest와 `Sandbox.wsb`를 생성했습니다. 준비 단계에서는 호스트에서 런타임을 실행하지 않았습니다. 생성한 `.wsb` 파일을 직접 여는 방식은 [Microsoft의 구성 파일 실행 절차](https://learn.microsoft.com/en-us/windows/security/application-security/application-isolation/windows-sandbox/windows-sandbox-configure-using-wsb-file)와 같습니다.

구성 파일은 준비한 `input`을 읽기 전용으로 매핑하고 `evidence`에만 쓰기를 허용했습니다. 게스트는 매핑한 파일의 해시를 검사한 뒤 런타임을 게스트 내부로 복사하여 다시 검사했습니다. 이후 전용 `CODEX_HOME`과 작업 디렉터리를 생성했습니다. 호스트 Codex 프로필과 인증 파일, 식탁보 `Data` 및 NPKI 디렉터리는 매핑하지 않았습니다. 게스트 설정은 대화 이력 저장과 telemetry exporter를 비활성화했습니다. 스크립트는 자식 프로세스 환경에서 지정한 API 키와 세션 환경 변수를 제거했습니다.

네트워크는 활성화했습니다. `ProtectedClient`, vGPU, 클립보드, 마이크, 카메라와 프린터 리디렉션은 비활성화했습니다. `ProtectedClient = Disable`은 해당 프로토타입의 설정을 기록하며 Computer Use의 필수 조건이나 제품 배포 기준을 입증하지 않습니다. 네트워크와 쓰기 가능한 증거 매핑에는 격리 경계를 넘는 접근이 남습니다. Microsoft는 [내부 네트워크 노출과 매핑 폴더 쓰기의 영속성](https://learn.microsoft.com/en-us/windows/security/application-security/application-isolation/windows-sandbox/windows-sandbox-configure-using-wsb-file#mapped-folders)을 설명합니다.

게스트 셸은 `Login-Codex`, `Start-Codex`, 플러그인 목록과 marketplace 목록 조회 명령을 제공했습니다. 로그인은 사용자가 `codex login --device-auth`로 시작했습니다. bootstrap은 인증하거나 모델을 호출하지 않았습니다. Sandbox를 닫으면 게스트 내부 프로필과 서비스를 폐기하지만 매핑 폴더에 기록한 증거는 호스트에 남습니다.

## 3. 두 게스트 실행의 버전별 관측 결과

[증거 요약](codex-sandbox-computer-use-evidence.json)은 두 x64 게스트가 Windows 버전 `10.0.26100.0`을 보고했다고 기록합니다. 두 실행은 입력과 게스트 복사본의 파일 기록 49개를 비교하여 해시 불일치 0개를 남겼습니다. 상태는 `ReadyForManualLogin`, 단계는 `Complete`에 도달했습니다. `--version`, `--help`와 `plugin --help`는 모두 종료 코드 `0`을 반환했으며 예상 CLI 버전도 일치했습니다.

| 관측 | 2026년 10월 23일: CLI `0.156.1` | 2026년 10월 24일: CLI `0.164.0` |
| --- | --- | --- |
| bootstrap과 CLI 로컬 점검 | 통과 | 통과 |
| 플러그인 조사 | 종료 코드 `0`, JSON 파싱 성공, 이전 파서에서 `pluginCount: null` | 종료 코드 `0`, 형식 인식 성공, 설치 0개와 제공 0개 |
| 폐기 가능한 SCM 서비스 검사 | 컴파일과 등록, Session 0 시작, 중지 및 삭제 성공 | 동일한 결과 기록 |
| 대화형 CLI 진입 | 사용자가 환영 화면 또는 로그인 화면 보고 | 사용자 미확인 |
| 게스트 로그인 | 사용자가 bootstrap 이후 성공 보고 | 미검증, bootstrap은 미인증 상태 유지 |
| 인증 후 Computer Use 목록 | 사용자가 `/plugins`에서 미표시 보고 | 인증 후 목록 관측 없음 |
| 모델 요청과 Computer Use UI 조작 | 시험하지 않음 | 시험하지 않음 |

일반 서비스 검사는 작은 .NET Framework 서비스를 컴파일하여 Session 0 시작 표식을 확인하고 중지 및 삭제 결과를 기록했습니다. OpenAI 서비스를 설치하거나 화면을 캡처하거나 입력을 주입하지 않았습니다. 서비스 등록 권한만으로 대화형 데스크톱 접근을 입증할 수는 없습니다.

다만 bootstrap의 `authenticated: false`는 자동 수집 당시의 상태를 나타냅니다. 이후 `0.156.1`의 로그인 성공 보고로 해당 필드값을 대체하거나 새 `0.164.0` 게스트에 인증을 승계하지 않습니다. 미인증 목록의 빈 배열과 특정 계정의 인증 후 목록만으로 전체 Computer Use 제공 여부를 판단하지 않습니다.

## 4. 플러그인 파서 수정과 별도 호스트 조사

플러그인 응답 처리의 수정 범위를 검토하겠습니다. 최초 수집기는 JSON을 파싱했지만 관측한 `installed`/`available` 형식을 인식하지 못했습니다. 당시 `pluginCount: null`은 알 수 없는 값으로 보존합니다. 원본 게스트 응답을 보관하지 않았으므로 후속 호스트 응답으로 복원할 수 없습니다. OpenAI의 [CLI 참조 문서](https://learn.chatgpt.com/docs/cli/reference#codex-plugin)도 두 배열을 설명합니다.

수정한 파서는 고정한 파싱 메타데이터와 항목 수만 보존합니다. `pluginCount`는 설치와 제공 항목의 합계가 아닌 `available` 배열의 항목 수를 따릅니다. 실제 `0.164.0` 게스트는 수정한 파서를 실행하여 두 배열에 각각 0개를 기록했습니다. 과거 Windows PowerShell `5.1.26100.9711` fixture 실행은 준비 과정, XML 구성, 경로 처리, 해시와 파서 회귀 검사 64개를 통과했다고 기록합니다. 해당 fixture는 Sandbox나 호스트 서비스를 시작하지 않았으며 이번 문서 작업에서도 재실행하지 않았습니다.

별도 호스트 조사는 새 미인증 프로필을 사용했습니다. `0.156.1` 조사에서는 인증 파일 생성 없이 설치 및 제공 배열이 모두 비어 있었습니다. `0.164.0` 패키지 조사에서는 저장소 설치기를 재사용하여 체크섬, 제한을 적용한 추출, 레이아웃, 버전과 receipt 검사를 통과했다고 기록했습니다. workspace 프로필 어댑터를 사용했으므로 `WindowsManagedAiProfile`의 ACL 처리는 검증하지 않았습니다. 표준 app-server 스키마는 메서드 202개를 제공했고 실험 옵션 스키마는 272개를 제공했습니다. 조사에서는 Computer Use 설치나 구성 메서드를 식별하지 못했습니다. `computer_use` 기능의 `stable`/`true` 보고로 런타임 준비 완료를 판정하지 않았습니다.

## 5. Computer Use 구성과 후속 검증의 판정 기준

아카이브 검토일의 [OpenAI 공식 Computer Use 문서](https://learn.chatgpt.com/docs/computer-use)는 ChatGPT 데스크톱 앱과 Computer Use 플러그인을 통한 구성을 설명합니다. 조사한 독립 CLI 도움말과 스키마만으로는 지원되는 게스트 구성 절차를 확인하지 못했습니다. 일반 CLI 플러그인 관리, 기능 플래그, 구성 타입과 SCM 검사 성공도 Computer Use 도구의 설치를 입증하지 않습니다.

원본 조사에서는 번들 Computer Use 지침이 `node_repl`과 `@oai/sky`를 참조한다고 기록했습니다. 지침만 복사해도 해당 런타임 의존성을 제공할 수 있는지는 확인되지 않았습니다. 프로토타입은 게스트에 ChatGPT 데스크톱 앱을 설치하거나 Computer Use 런타임과 서비스를 구성하지 않았습니다. 도구 세션, 스크린샷과 UI 조작도 시연하지 않았습니다. 따라서 독립 CLI의 Computer Use 지원을 판정하지 않습니다.

후속 검증에서는 문서로 확인한 구성 경로, 게스트 도구 제공 여부와 실제 게스트 UI 조작을 완료 기준으로 삼을 수 있습니다. 인증과 계정 목록 접근, 모델 추론, 데스크톱 접근 및 ARM64 실행은 각각 별도 증거로 판단할 수 있습니다. 기록한 x64 실행만으로 이들 항목의 결과를 확정하지 않습니다.

## 6. 저장소 문서와 로컬 원본의 보존 범위

저장소는 이 문서와 [영문 문서](codex-sandbox-computer-use.md) 및 [증거 JSON](codex-sandbox-computer-use-evidence.json)을 유지합니다. 로컬 연구 원본은 Git이 무시하는 `Artifacts/codex-sandbox-research/tools/codex-sandbox-poc/`에 별도로 보존합니다. 새 checkout에는 해당 원본이 없습니다. 따라서 이 아카이브에는 저장소가 스크립트를 배포한다고 가정하는 재현 명령을 넣지 않았습니다. 증거 JSON은 원본 파일 이름과 해시를 기록하며 파서 수정 전 실제 실행의 수집기 해시도 구별합니다.

공개 자료 검토에서는 고정한 기술 결과와 해시를 보존했습니다. 로컬 계정 이름과 홈 경로, 실행 및 세션 식별자, 인증 자료, 플러그인 원본 응답, 대화 내용과 스크린샷은 제외했습니다. 원본 bootstrap도 CLI 원본 응답이나 예외 메시지 대신 크기를 제한한 상태와 항목 수를 내보내도록 작성했습니다. 이번 검토는 공개 아카이브를 대상으로 하며 Git이 무시하는 디렉터리의 모든 파일을 인증하지 않습니다.

이로써 [식탁보 AI 구현 이력](managed-ai-runtime.md)에서 CLI 격리 탐색 결과를 참조할 수 있습니다. 런타임 기능을 추가하거나 릴리스 검증을 확장하지는 않았습니다. 이번 문서 검토에서는 JSON 구문과 로컬 링크 및 원본 연구 기록과 요약의 일치를 확인했습니다.

## 탐색 결론과 후속 진행 기준

여기까지 정리하면 이 탐색은 x64 Sandbox 게스트 두 개에서 독립 CLI 시작과 폐기 가능한 SCM 서비스 동작의 과거 증거를 남겼습니다. 이전 `0.156.1` 게스트의 TUI 진입과 로그인에는 사용자 보고도 있습니다. 보존한 기록으로 Computer Use 동작을 입증하지는 못했습니다.

향후 Computer Use 작업에서는 지원되는 구성 경로와 실제 게스트 도구 세션을 검증할 수 있습니다. 기존 호스트 식탁보 AI 통합을 활용하는 작업은 별도 구현 보고서의 범위를 기준으로 판단할 수 있습니다. 새 게스트 로그인, 추론이나 UI 조작 결과를 추가할 경우에는 해당 실행의 날짜와 증거를 구별하여 남길 수 있습니다.
