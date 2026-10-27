# 스킬 마켓플레이스와 격리 스크립트 실행

2026년 10월 27일 기준으로 이 문서는 식탁보 개발 구현을 설명합니다. 마켓플레이스는 TableClothCatalog의 XML 카탈로그를 읽고 식탁보 AI 전용 Codex 프로필의 스킬을 관리합니다. Preview 8은 계산기 스크립트만 AppContainer에 연결했습니다. 당시 계산기 실행 검증으로 다른 설치 스크립트의 실행까지 입증하지는 않았습니다.

현재 개발 구현은 활성화한 설치 스킬에서 Python과 Node.js 진입점을 찾아 앱이 관리하는 MCP 중계기에 등록합니다. 지침만 사용하는 스킬과 기존 인증서 및 Windows Sandbox 호스트 브리지는 각각의 처리 경로를 유지합니다. 추가 프롬프트 참고 자료는 기록한 커밋의 공개 GitHub 파일에서 인증 없이 조회하며 이 경로로 로컬 문서 본문을 전달하지 않습니다. 스킬을 설치하거나 활성화한 사실만으로 모델이 해당 응답에서 스킬을 사용했다고 판단할 수는 없습니다.

이 문서에서는 마켓플레이스 진입 경로, 카탈로그 계약, 로컬 설치, 스크립트 격리, 실행 환경 진단, 검증과 카탈로그 관리 순서로 살펴보겠습니다. [English](SKILL_MARKETPLACE.md)

## 마켓플레이스 진입 경로와 작업 진행 표시

마켓플레이스의 사용 경로를 정리하겠습니다. **옵션 > AI 스킬 > 스킬 마켓플레이스 열기**를 선택하면 **카탈로그 탐색**, **설치된 스킬**, **실행 환경** 탭을 제공합니다. 검색으로 두 스킬 목록을 좁히고 카탈로그의 분야 필터로 항목을 선택할 수 있습니다. 스킬을 선택하면 출처, 저자, 라이선스, 원본 버전과 사용 조건을 표시합니다. [창](../src/TableCloth.App/Dialogs/SkillMarketplaceWindow.axaml)과 [뷰 모델](../src/TableCloth.App/ViewModels/SkillMarketplaceWindowViewModel.cs)이 이 동작을 구현합니다.

옵션 페이지에는 짧은 사용 안내와 마켓플레이스 열기 버튼을 표시합니다. AI 창의 **OpenAI 계정 / 설정 > 전용 스킬 > 설치된 AI 스킬 관리**를 선택하면 설치된 스킬 탭을 바로 엽니다. 대화 상자를 닫으면 AI 창이 활성 스킬 수를 새로 조회합니다. 활성 스킬 수는 다음 대화에 제공할 지침 수를 나타내며 완료한 스크립트 실행 횟수를 뜻하지 않습니다.

첫 메시지 화면에는 서비스 찾기, 공공서비스 찾기와 세금 신고 사이트 찾기 카드 세 개를 표시합니다. 추가 기능 안내 링크는 카탈로그 탭을 엽니다. 새 프로필에는 스킬을 기본 설치하지 않으며 기존 설치본은 계속 관리할 수 있습니다.

카탈로그 조회, 설치된 스킬 작업과 실행 환경 검사는 진행 상태를 각각 관리합니다. 작업 표시 영역은 설치, 업데이트와 제거 중인 항목을 안내합니다. 카탈로그 요청에 실패해도 설치된 스킬을 관리할 수 있으며 조회 중에는 결과 없음 안내를 먼저 표시하지 않습니다.

## 버전과 처리 경로를 구분하는 스킬 카탈로그

카탈로그의 데이터 계약을 짚어보겠습니다. 앱은 `https://catalog.yourtablecloth.app/SkillCatalog.xml`을 읽습니다. [TableClothCatalog의 카탈로그 디렉터리](https://github.com/yourtablecloth/TableClothCatalog/tree/main/docs)에서 원본, 스키마와 기여 안내를 관리하며 Pages 워크플로가 `main`의 변경을 게시합니다. `SkillCatalog Version="1"`은 고정 ID, GitHub 원본 커밋, 라이선스 근거, 런타임과 네트워크 요구 사항, 사용 조건을 기록합니다. 공개나 검토 상태를 별도로 두지 않으며 `Skill` 블록을 주석 처리하면 원격 목록에서 제외합니다. 앱이 읽는 필드는 [카탈로그 모델](../src/TableCloth.ManagedAi.Core/SkillCatalog.cs)에 정의했습니다.

현재 체크아웃한 카탈로그는 처리 경로가 다른 스킬 6개를 제공합니다.

| 스킬 | 제품 처리 경로 | 의존성과 제한 |
| --- | --- | --- |
| `bok-ecos-stats` | 설치 패키지의 Python 도우미를 MCP와 AppContainer로 실행 | 요청을 제한하는 호스트 HTTPS 프록시로 ECOS 공개 및 sample 조회 |
| `kosis-stats` | 설치 패키지의 Python 도우미를 MCP와 AppContainer로 실행 | 요청을 제한하는 호스트 HTTPS 프록시로 원본의 공개 프록시 접근 |
| `tablecloth-calculators` | 설치 패키지의 Python 스크립트를 MCP와 AppContainer로 실행 | 사용자 입력과 Python 표준 라이브러리 사용, 네트워크 차단 |
| `official-report-skillset` | 지침과 설치된 참고 문서 제공 | 제공한 자료로 초안 작성, 스크립트 실행 불필요 |
| `tablecloth-certificate-expiry` | 기존 앱 인증서 보고서 브리지 사용 | 사용자 동의 후 만료 건수 제공, Python과 Node.js 작업자 미사용 |
| `tablecloth-windows-sandbox` | 기존 앱 Windows Sandbox 브리지 사용 | 호환되는 Windows Sandbox 환경 필요, Python과 Node.js 작업자 미사용 |

이 표는 처리 경로와 의존성을 설명하며 테스트 결과나 외부 서비스 가용성을 보장하지 않습니다. 다른 후보는 주석으로 보류합니다. API 키 설정과 DPAPI 자격 증명 보호는 [이슈 #321](https://github.com/yourtablecloth/TableCloth/issues/321)에서 추적합니다. [스킬 제공 기준](https://github.com/yourtablecloth/TableClothCatalog/blob/main/docs/SKILL_ELIGIBILITY.ko.md)과 [기존 PoC 보고서](https://github.com/yourtablecloth/TableClothCatalog/blob/main/docs/APP_CONTAINER_POC.ko.md)는 카탈로그 판단과 당시 평가 범위를 각각 설명합니다.

## 로컬 설치와 업데이트 및 활성화 설정

미설치 카탈로그 항목은 검토 승인 없이 설치할 수 있습니다. 클라이언트는 ID로 항목을 다시 조회하고 고정한 원본 커밋과 일치하는 라이선스 근거를 확인한 뒤 파일을 검증합니다. Git 체크아웃 없이 GitHub REST API를 사용하며 요청과 파일 크기를 제한합니다. 링크와 서브모듈을 거절하고 임시 폴더에 파일을 받은 뒤 설치를 확정하며 원본 라이선스는 `LICENSE.upstream`으로 보존합니다. XML 파서는 외부 엔터티와 DTD를 허용하지 않습니다. [설치기](../src/TableCloth.ManagedAi.OpenAi/Skills/GithubSkillInstaller.cs)가 이 검사를 수행합니다.

마켓플레이스는 카탈로그 ID, 저장소, 경로와 원본 커밋을 보관합니다. 같은 출처의 커밋이 달라지면 두 목록에서 업데이트를 제공합니다. 업데이트는 교체 파일을 검증한 뒤 기존 활성화 설정을 유지하며 설치본을 변경합니다. 교체에 실패하거나 작업을 취소하면 이전 설치본을 보존합니다. 같은 ID로 가져온 로컬 스킬은 카탈로그 업데이트로 덮어쓰지 않습니다.

설치된 스킬 탭은 폴더 가져오기, 업데이트, 활성화와 비활성화, 확인 후 제거, 새로 고침과 폴더 열기를 제공합니다. 활성화 표시는 다음 대화에 지침을 제공할지 나타내며 실행 결과를 뜻하지 않습니다. 잘못된 `SKILL.md`도 검사, 비활성화나 제거를 위해 목록에 표시하며 활성화는 허용하지 않습니다. [스킬 관리자](../src/TableCloth.ManagedAi.OpenAi/Skills/OpenAiCodexSkillManager.cs)에서 이 동작을 구현합니다.

스킬은 `%LOCALAPPDATA%\TableCloth\ManagedAi\profiles\openai-codex\codex-home\skills`에 보관하고 대화는 별도 `%LOCALAPPDATA%\TableCloth\ManagedAi\runs\<GUID>` 폴더를 사용합니다. 두 폴더는 서로 다른 역할을 맡습니다. 앱은 대화 작업 폴더에 의존하지 않고 설치한 진입점을 찾아 중계기에 등록합니다. 사용자 전역 Codex 프로필은 마켓플레이스 관리 범위에 포함하지 않습니다.

## MCP 연결과 AppContainer 실행 경계

스크립트의 제품 실행 경로를 확인하겠습니다. [제품 브리지](../src/TableCloth.ManagedAi.Windows/WindowsManagedAiSkillExecution.cs)는 활성화한 유효 설치 스킬에서 `.py`, `.js`, `.mjs`, `.cjs` 진입점을 찾습니다. 앱은 런타임과 패키지 식별 정보를 포함한 대화별 manifest를 생성합니다. 앱이 관리하는 [중계기](../src/TableCloth.ManagedAi.AppContainer/Broker/SkillMcpBroker.cs)는 진입점을 최대 128개 등록하며 인수 배열과 선택적인 표준 입력을 Python이나 Node.js에 직접 전달합니다. 셸을 거쳐 명령을 실행하지 않습니다. [호출 구성](../src/TableCloth.ManagedAi.OpenAi/Execution/OpenAiCodexInvocationBuilder.cs)은 MCP 서버를 등록하고 원본의 `npx` 지침 로더 대신 설치한 스크립트 경로를 안내합니다.

각 작업자는 별도 임시 작업 폴더를 사용하는 네이티브 Windows AppContainer에서 실행합니다. 스킬 패키지와 검증한 런타임 파일은 읽기 전용으로 제공하며 해시를 확인한 파일의 핸들은 실행 중 잠금 상태를 유지합니다. 작업자에게 호스트 자격 증명, API 키 환경 변수나 임의의 호스트 경로를 전달하지 않습니다. [실행기와 레지스트리](../src/TableCloth.ManagedAi.AppContainer)가 무결성, 경로, 프로세스와 자원 제한을 검사합니다. 일반 AppContainer도 `ALL APPLICATION PACKAGES`에 이미 허용한 자원에는 접근할 수 있으며 가상 머신 수준의 경계를 제공하지는 않습니다.

모든 작업자는 `AllowNetwork=false`로 실행하며 `internetClient` 권한을 받지 않습니다. BOK와 KOSIS는 공개 서비스의 허용 목적지와 요청 범위를 검사하는 호스트 HTTPS 프록시를 사용합니다. 카탈로그의 `Network="required"` 선언만으로 임의의 네트워크 접근을 허용하거나 목적지를 추가하거나 자격 증명을 제공하지 않습니다. 로컬 가져오기 항목이나 다른 스크립트가 `SKILL.md`에 네트워크를 언급한 것만으로 인터넷에 접근할 수는 없습니다.

[전용 프로필](../src/TableCloth.ManagedAi.Windows/WindowsManagedAiProfile.cs)은 `shell_tool=false`와 `unified_exec=false`를 유지합니다. Codex는 `read-only` 샌드박스와 `never` 승인 정책을 유지하며 패키지 스크립트는 앱이 관리하는 MCP 경로로 실행합니다. 앱은 `pip`, `npm`, `npx`로 임의 의존성을 설치하거나 외부 MCP 서버를 자동 구성하거나 지원하지 않는 네이티브 의존성을 제공하지 않습니다.

카탈로그에서 설치한 스킬에는 [공개 문서 컨텍스트](../src/TableCloth.ManagedAi.OpenAi/Execution/InstalledSkillPromptContext.cs)를 제공합니다. 기록한 커밋의 `raw.githubusercontent.com`에서 `SKILL.md`, `instruction.md`와 링크한 `references/*.md` 파일을 최대 네 개 조회합니다. 인증 정보를 보내거나 리다이렉트를 따르지 않으며 공개 원본 URL을 기준으로 세션 메모리에 캐시합니다. 파일당 32 KiB와 전체 JSON 컨텍스트 128 KiB를 제한으로 적용합니다. 조회할 수 없거나 크기를 초과한 문서는 생략 상태로 알리고 컨텍스트를 잘랐을 때에도 표시합니다. 로컬 가져오기 항목은 ID만 제공하며 이 경로로 설치 문서의 본문을 읽거나 전달하지 않습니다. 공개 지침을 제공하므로 `npx` 로더를 실행하지 않고도 해당 지침을 사용할 수 있습니다.

인증서 만료와 Windows Sandbox 요청은 [기존 호스트 브리지](../src/TableCloth.App/ManagedAi/ManagedAiWindow.cs)가 처리합니다. 인증서 브리지는 사용자 동의를 받고 만료 건수만 전송합니다. Sandbox 브리지는 `list`, `start`, `stop`, `connect`, `ip`를 지원하며 종료 전에 사용자에게 확인합니다. 이 호스트 작업은 스킬 AppContainer에서 실행하지 않습니다. `Network="none"`은 스킬 자체의 데이터 요구 사항을 나타내며 Codex 대화는 온라인 연결을 사용합니다.

## 전용 런타임과 긴 경로 진단

**실행 환경** 탭은 설치한 Python과 Node.js 버전을 표시하며 **실행 환경 설치/복구**와 **설치 및 환경 검사**를 제공합니다. 두 런타임은 식탁보 전용으로 관리하며 대화에 사용하는 네이티브 Codex CLI와 구분합니다. 시스템 인터프리터, 시스템 PATH와 Windows 정책은 변경하지 않습니다. [런타임 관리자](../src/TableCloth.ManagedAi.Windows/ScriptRuntimeManager.cs)와 [실행 환경 뷰 모델](../src/TableCloth.App/ViewModels/SkillMarketplaceWindowViewModel.Runtimes.cs)이 설치와 상태 표시를 담당합니다.

긴 경로 검사는 각 인터프리터의 AppContainer 안에서 260자를 넘는 경로의 파일을 읽고 씁니다. UI는 Python과 Node.js 각각에 **검사 통과**, **사용 제한**, **확인 불가**를 표시합니다. 검사에 실패하거나 지원 여부를 확인하지 못하면 기능 제한 가능성을 안내합니다. 재검사에 실패하면 이전의 통과 결과를 지웁니다. 인터프리터 검사를 통과해도 추가 라이브러리가 별도의 경로 제한을 가질 수 있습니다.

런타임 설치와 스크립트 검색은 실행 조건 일부를 확인합니다. 코드 비호환, 패키지에 없는 의존성, 외부 서비스 장애, 인증 조건이나 스킬의 입력 검사 때문에 실행에 실패할 수 있습니다. 런타임 버전과 활성 스킬 수만으로 스크립트 실행을 입증하지는 않습니다.

## 검증 증거와 배포 범위의 구분

[제품 스킬 재현 도구](../tools/managed-skill-smoke/README.ko.md)로 실제 DI 구성, 계산기 22개 연산과 Codex MCP 이벤트를 검사할 수 있습니다. 인증서와 Sandbox 검사는 합성 호스트 보고서를 사용한 사례로 구분합니다.

자동 테스트는 카탈로그 파싱, 다운로드 검증, 버전 추적, 교체 실패 시 복구, 제거와 뷰 모델 명령을 다룹니다. Avalonia 헤드리스 검사는 언어, 테마, 창 크기, 진행 상태와 실행 환경 경고를 다룹니다. 테스트용 응답은 해당 계약을 검사하며 실제 서비스 가용성이나 모델의 도구 선택을 입증하지 않습니다. [백엔드 테스트](../src/TableCloth.ManagedAi.Test)와 [마켓플레이스 UI 테스트](../src/TableCloth.Test/SkillMarketplaceTests.cs)에서 검사 항목을 확인할 수 있습니다.

스크립트 실행 검증에서는 실제 MCP 호출과 결과, 선택한 설치 진입점, 런타임과 패키지 해시, 종료 상태와 `wasAppContainer` 기록을 수집합니다. 모델이 스킬을 사용했다고 답한 문구로 이 기록을 대신하지 않습니다. 스킬 6개의 검증 경로도 구분합니다. 보고서 스킬은 지침과 제공 자료를 확인하고 두 로컬 연동은 호스트 브리지 결과를 확인하며 BOK, KOSIS와 계산기는 격리한 스크립트의 결과를 확인합니다. 중계기 직접 검사와 실제 Codex 대화 검사는 각각 다른 계층을 다룹니다.

[기존 PoC 결과](../tools/appcontainer-skill-poc/RESULTS.ko.md)는 당시 측정 환경과 검증 제한을 보존합니다. 이후 배포 파일이 모든 스크립트를 등록했거나 Windows 10 22H2와 ARM64 실기기에서도 같은 동작을 보인다고 입증하지 않습니다. 개발 변경, 카탈로그 게시와 앱 릴리스는 서로 다른 경로로 진행합니다. 이 안내 문서만으로 빌드 배포나 테스트 통과를 선언하지 않습니다.

## 카탈로그 변경과 기존 설치본 관리

여기까지 정리하면 카탈로그는 활성 `Skill` 블록으로 설치 대상을 제공합니다. 관리자가 고정한 원본 커밋과 해당 라이선스 경로를 갱신하면 마켓플레이스가 그 버전을 업데이트 대상으로 표시합니다. 블록을 주석 처리하면 원격 목록에서 내리며 기존 설치본은 로컬 관리 범위에 남깁니다. [카탈로그 기여 안내](https://github.com/yourtablecloth/TableClothCatalog/blob/main/docs/SkillCatalog.README.ko.md)에서 작성 규칙을 다룹니다.

기존 설치본은 원격 목록에서 내리거나 카탈로그 조회에 실패해도 계속 관리할 수 있습니다. 기존 파일에는 기록한 의존성과 실행 제한이 적용됩니다. 설치본을 보관해도 새로운 자격 증명, 패키지나 외부 MCP 연결을 제공하지 않으며 앱이 지원하는 연동 경로에 따라 해당 기능을 사용할 수 있습니다.
