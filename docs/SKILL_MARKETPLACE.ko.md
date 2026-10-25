# 스킬 마켓플레이스와 원격 카탈로그

2026년 10월 25일 기준으로 이번 구현은 식탁보 `develop` 계열을 대상으로 합니다. 마켓플레이스는 TableClothCatalog의 별도 XML 스킬 카탈로그를 읽고 식탁보 AI 전용 Codex 프로필에 설치한 스킬을 관리합니다. 현재 공개한 Preview 릴리스에는 이번 변경을 포함하지 않습니다.

별도 `poc/appcontainer-skill-runner` 평가 분기는 활성 항목 6개의 실험용 카탈로그와 독립 [AppContainer 실행 PoC](../tools/appcontainer-skill-poc/README.ko.md)를 포함합니다. 아래 제품 설명은 기존 3개 항목의 등재 정책을 다룹니다. PoC의 중계기와 런타임은 채팅 창에 연결하지 않습니다.

마켓플레이스에서 스킬 검색, 출처와 라이선스 열람, 설치와 업데이트, 로컬 스킬 관리를 제공합니다. 카탈로그에는 공개 상태나 검토 상태를 두지 않습니다. 주석 밖에 있는 스킬 항목을 설치 대상으로 표시합니다.

이 문서에서는 창의 사용 경로부터 카탈로그 계약, 설치와 업데이트, 로컬 저장 범위, 검증 범위 순서로 살펴보겠습니다. [English](SKILL_MARKETPLACE.md)

## AI 설정에서 여는 마켓플레이스

마켓플레이스의 진입 경로를 정리하겠습니다. **옵션 > AI 스킬 > 스킬 마켓플레이스 열기**를 선택하면 별도 창을 엽니다. 창에서 **카탈로그 탐색**과 **설치된 스킬** 두 탭을 제공합니다. 검색으로 스킬 목록을 좁히고 카탈로그의 분야 필터로 스킬을 선택할 수 있습니다. 스킬을 선택하면 출처, 저자, 라이선스, 원본 버전과 사용 조건을 표시합니다.

옵션 탭에는 짧은 사용 안내와 마켓플레이스 열기 버튼만 표시합니다. AI 창의 **OpenAI 계정 / 설정 > 전용 스킬 > 설치된 AI 스킬 관리**를 선택하면 설치된 스킬 탭을 바로 엽니다. 대화 상자를 닫으면 AI 창이 활성 스킬 수와 스킬 사용 상태를 다시 조회합니다.

첫 메시지 화면에는 서비스 찾기, 공공서비스 찾기와 세금 신고 사이트 찾기 카드 세 개를 표시합니다. 더 많은 기능을 안내하는 링크는 마켓플레이스의 카탈로그 탭을 열어 새 스킬을 선택하고 설치할 수 있도록 연결합니다. 기본 구성에는 스킬을 포함하지 않으며 기존 설치본은 계속 관리할 수 있습니다.

이어서 카탈로그 조회와 로컬 작업의 관계를 짚어보겠습니다. 두 작업은 진행 상태를 각각 관리합니다. 카탈로그 요청에 실패해도 설치된 스킬을 관리할 수 있습니다. [창](../src/TableCloth.App/Dialogs/SkillMarketplaceWindow.axaml)과 [뷰 모델](../src/TableCloth.App/ViewModels/SkillMarketplaceWindowViewModel.cs)이 이 동작을 구현합니다.

## 버전을 지정하는 별도 XML 카탈로그

카탈로그는 `https://catalog.yourtablecloth.app/SkillCatalog.xml`에서 제공합니다. 원본은 [TableClothCatalog의 docs 디렉터리](https://github.com/yourtablecloth/TableClothCatalog/tree/main/docs)에 두며 `SkillCatalog.xsd`와 기여 안내를 함께 관리합니다. 기존 Pages 워크플로가 `main`에 반영한 파일을 게시합니다.

`SkillCatalog Version="1"` 아래에 고정 ID를 가진 `Skill` 항목을 기록합니다. 각 항목은 GitHub 저장소, 경로, `Source.Ref`에 고정한 원본 커밋을 지정합니다. 설명, 저자, SPDX 식별자, 라이선스 근거, 런타임과 네트워크 요구 사항, 설치 방식, 사용 조건도 기록합니다. `Skill` 블록을 주석 처리하거나 삭제하면 원격 목록에서 제외합니다. 기존 설치본은 계속 관리할 수 있습니다. [공용 모델](../src/TableCloth.ManagedAi.Core/SkillCatalog.cs)에 애플리케이션이 읽는 필드를 정의했습니다.

활성 카탈로그에는 `official-report-skillset`, [tablecloth-certificate-expiry](https://github.com/yourtablecloth/tablecloth-certificate-expiry), [tablecloth-windows-sandbox](https://github.com/yourtablecloth/tablecloth-windows-sandbox) 세 스킬을 표시합니다. 보고서 스킬은 초안 작성을 위한 지침과 참고 문서를 제공합니다. 나머지 두 스킬은 설치와 활성화 후 API 키 없이 식탁보의 호스트 브리지가 제공한 로컬 보고서를 해석합니다. 새 프로필에 이 스킬들을 자동으로 설치하지는 않습니다.

`bok-ecos-stats`, `kosis-stats`, `tablecloth-calculators`는 스크립트 실행을 요구하므로 주석으로 보류합니다. `korean-jangbu-for`와 `korean-law-mcp`는 MCP 연결과 추가 설정을 요구하므로 주석으로 보류합니다. 이전에 보류한 다른 후보도 주석으로 보존합니다. 이 항목들은 이후 작업을 위해 원본 정보를 남기며 활성 카탈로그의 설치 대상으로 제공하지 않습니다. [스킬 제공 기준](https://github.com/yourtablecloth/TableClothCatalog/blob/main/docs/SKILL_ELIGIBILITY.ko.md)에서 현재 범위를 설명합니다. API 키 설정과 DPAPI 자격 증명 관리는 [다음 버전 이슈 #321](https://github.com/yourtablecloth/TableCloth/issues/321)에서 계속 추적합니다.

## 카탈로그 버전에 맞춘 설치와 업데이트

원격 설치와 버전 교체 동작을 확인하겠습니다. 미설치 스킬을 선택하면 검토 승인 없이 설치 버튼을 사용할 수 있습니다. 설치 시 클라이언트가 스킬 ID로 현재 카탈로그를 다시 조회합니다. 원본은 고정한 커밋의 upstream 스킬을 가리키며 라이선스 근거는 같은 저장소와 커밋의 파일을 가리킵니다. 클라이언트는 잘못된 메타데이터나 파일을 검증 단계에서 거절합니다. 이전 카탈로그의 상태 속성은 전환 기간에 읽을 수 있지만 설치를 제한하는 데 사용하지 않습니다.

클라이언트는 Git 체크아웃 없이 GitHub REST API로 선택한 파일을 조회합니다. 경로와 저장소 항목을 검사하고 요청 및 다운로드 크기를 제한합니다. 링크와 서브모듈을 거절하며 임시 디렉터리에 파일을 받은 뒤 설치를 확정합니다. 고정한 라이선스 파일도 `LICENSE.upstream`으로 보존합니다. XML 파서는 외부 엔터티와 DTD를 허용하지 않습니다. [OpenAI 백엔드](../src/TableCloth.ManagedAi.OpenAi/Skills)가 이 검사를 수행합니다.

마켓플레이스는 설치 시 카탈로그 ID, 저장소, 경로와 원본 커밋을 보관합니다. 같은 출처의 `Source.Ref`가 달라지면 카탈로그와 설치된 스킬 화면에서 해당 버전으로 업데이트할 수 있습니다. 커밋 해시는 버전을 구분하며 시간상 선후 관계를 뜻하지 않습니다. 업데이트는 교체 파일을 임시 폴더에서 검증한 뒤 설치본에 반영합니다. 검증에 실패하거나 작업을 취소하면 기존 파일, 버전 정보와 사용 설정을 보존합니다. 같은 ID의 로컬 스킬 가져오기 항목은 마켓플레이스 업데이트로 덮어쓰지 않습니다.

다만 후보에 따라 API 키, OAuth 자격 증명, MCP 연결, 추가 패키지 또는 스크립트 실행을 요구합니다. 카탈로그의 사용 조건에 이러한 의존성을 기록합니다. 스킬 파일을 설치해도 서비스 자격 증명을 제공하거나 MCP 서버를 구성하거나 명령 실행을 허용하지 않습니다. 필수 실행 경로를 제공하지 못하는 후보는 [스킬 제공 기준](https://github.com/yourtablecloth/TableClothCatalog/blob/main/docs/SKILL_ELIGIBILITY.ko.md)에 따라 주석으로 보관합니다.

## 설치본의 로컬 관리 범위

설치된 스킬 화면에서 폴더 가져오기, 카탈로그 버전으로 업데이트, 활성화와 비활성화, 확인 후 제거, 새로 고침, 스킬 폴더 열기를 제공합니다. 마켓플레이스에서 설치한 항목에는 설치 당시의 원본 버전을 표시합니다. 사용 설정은 이후 AI 작업에 적용합니다. 제거 시 해당 전용 폴더와 출처 정보를 함께 삭제합니다. 카탈로그에서 내린 스킬이나 원격 조회에 실패한 경우에도 제거할 수 있습니다.

잘못된 `SKILL.md`도 오류 상태로 목록에 표시합니다. 해당 항목을 비활성화하거나 폴더를 열어 확인하고 개별 제거할 수 있습니다. 다른 스킬의 관리도 계속할 수 있습니다. 오류 항목의 활성화는 차단합니다. AI 실행은 잘못된 관리 대상 manifest를 복구하거나 제거할 때까지 오류로 중단합니다.

스킬은 `%LOCALAPPDATA%\TableCloth\ManagedAi\profiles\openai-codex\codex-home\skills`에 보관합니다. 기존 가져오기 기능과 작업 잠금이 변경 작업을 제어합니다. 마켓플레이스는 이 프로필을 관리합니다. 사용자 전역 Codex 프로필과 스킬은 관리 범위에 포함하지 않습니다. [스킬 관리자](../src/TableCloth.ManagedAi.OpenAi/Skills/OpenAiCodexSkillManager.cs)와 [관리 경로](../src/TableCloth.ManagedAi.Core/ManagedAiPaths.cs)에서 저장 범위를 확인할 수 있습니다.

## 네이티브 Codex와 호스트 브리지의 실행 범위

AI 사용 준비 과정에서는 대화와 검색에 사용하는 네이티브 Codex CLI를 설치합니다. Node.js와 Python은 설치하지 않습니다. 마켓플레이스에도 언어 런타임 설치, 버전 표시와 진단 기능을 제공하지 않습니다. [Codex 설치기](../src/TableCloth.ManagedAi.OpenAi/Distribution/OpenAiCodexPackageInstaller.cs)는 스킬 파일 설치기와 별도로 유지합니다.

[전용 프로필](../src/TableCloth.ManagedAi.Windows/WindowsManagedAiProfile.cs)은 `shell_tool=false`와 `unified_exec=false`를 유지합니다. [호출 구성](../src/TableCloth.ManagedAi.OpenAi/Execution/OpenAiCodexInvocationBuilder.cs)은 `read-only` 샌드박스와 `never` 승인 정책을 사용합니다. [대화 지침](../src/TableCloth.ManagedAi.OpenAi/Execution/OpenAiChatPromptFactory.cs)도 명령 실행을 금지합니다. 스킬을 설치해도 이 설정을 변경하지 않습니다. 읽기 전용 실행으로 모든 읽기 접근을 격리하거나 네트워크 접근을 차단한다고 보장하지는 않습니다.

인증서 만료와 Windows Sandbox 연동에서는 앱이 소유한 호스트 브리지를 사용합니다. [AI 창](../src/TableCloth.App/ManagedAi/ManagedAiWindow.cs)이 지원하는 사용자 요청을 인식하면 모델에 보고서를 보내기 전에 브리지를 실행합니다. 인증서 브리지는 사용자 동의를 받고 만료 건수만 전달합니다. Sandbox 브리지는 `list`, `start`, `stop`, `connect`, `ip`를 지원하며 종료 전에 사용자에게 확인합니다. 모델은 전달받은 보고서를 설명합니다. 두 연동은 Node.js나 Python에 의존하지 않습니다. `Network="none"`은 스킬 자체의 데이터 접근 조건을 나타내며 Codex 대화는 온라인 연결을 사용합니다.

## 자동 검증과 게시 경로의 구분

자동 검증의 범위를 검토하겠습니다. 테스트는 검토 조건 없는 카탈로그 파싱, 다운로드 검증, 버전 추적, 교체 실패 시 복구, 제거와 뷰 모델 명령을 다룹니다. 백엔드 테스트는 합성 응답으로 실패와 복구 경로를 검사합니다. Avalonia 헤드리스 테스트는 언어, 테마와 창 크기를 달리해 마켓플레이스의 두 탭을 검사합니다. [스킬 테스트](../src/TableCloth.ManagedAi.Test/SkillMarketplaceTests.cs)와 [UI 테스트](../src/TableCloth.Test/SkillMarketplaceTests.cs)에 검증 항목을 기록했습니다.

테스트용 응답, 실제 저장소 다운로드와 네이티브 Codex 스킬 조회는 서로 다른 범위를 검증합니다. 테스트용 응답은 구현한 계약을 검사합니다. 실제 다운로드와 스킬 조회로는 설치 파일, 메타데이터와 활성 상태를 확인할 수 있습니다. 어느 결과도 모델의 스크립트 호출이나 외부 서비스, API 자격 증명, MCP 연결의 정상 동작을 입증하지 않습니다. 이 문서에서는 실제 서비스와 모델의 도구 실행을 검증한 것으로 안내하지 않습니다. 호스트 브리지도 로컬 CLI와 Windows 기능에 의존하므로 스킬 목록에 표시된 사실만으로 실제 호스트 작업의 성공을 판단하지 않습니다.

카탈로그와 애플리케이션은 서로 다른 경로로 배포합니다. TableClothCatalog가 XML을 게시하면 원격 후보를 조회할 수 있습니다. 사용자는 이후 식탁보 빌드에서 마켓플레이스 창을 사용할 수 있습니다. 카탈로그 게시를 기다리는 동안에도 애플리케이션에서 설치된 스킬을 관리할 수 있습니다.

## 카탈로그 변경과 기존 설치본 관리

여기까지 정리하면 카탈로그는 활성 `Skill` 블록으로 설치 대상을 제공합니다. 관리자가 원본 커밋과 해당 라이선스 경로를 갱신하면 마켓플레이스가 그 버전을 업데이트 대상으로 표시합니다. 블록을 주석 처리하면 원격 목록에서 내립니다. [카탈로그 기여 안내](https://github.com/yourtablecloth/TableClothCatalog/blob/main/docs/SkillCatalog.README.ko.md)에서 작성 규칙을 다룹니다.

원격 목록에서 내려도 사용자가 기존 설치본을 계속 관리합니다. 다른 카탈로그 버전이 있으면 업데이트하고 더 이상 사용하지 않을 때는 제거할 수 있습니다. 설치본을 보관해도 부족한 실행 권한, 서비스 자격 증명과 MCP 구성을 제공하지는 않습니다.
