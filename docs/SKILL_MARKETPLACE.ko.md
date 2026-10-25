# 스킬 마켓플레이스와 원격 카탈로그

2026년 10월 25일 기준으로 이번 구현은 식탁보 `develop` 계열을 대상으로 합니다. 마켓플레이스는 TableClothCatalog의 별도 XML 스킬 카탈로그를 읽고 식탁보 AI 전용 Codex 프로필에 설치한 스킬을 관리합니다. 현재 공개한 Preview 릴리스에는 이번 변경을 포함하지 않습니다.

마켓플레이스에서 스킬 검색, 출처와 라이선스 열람, 설치와 업데이트, 로컬 스킬 관리, 전용 Node.js와 Python 설치를 제공합니다. 카탈로그에는 공개 상태나 검토 상태를 두지 않습니다. 주석 밖에 있는 스킬 항목을 설치 대상으로 표시합니다.

이 문서에서는 창의 사용 경로부터 카탈로그 계약, 설치와 업데이트, 로컬 저장 범위, 검증 범위 순서로 살펴보겠습니다. [English](SKILL_MARKETPLACE.md)

## AI 설정에서 여는 마켓플레이스

마켓플레이스의 진입 경로를 정리하겠습니다. **옵션 > AI 스킬 > 스킬 마켓플레이스 열기**를 선택하면 별도 창을 엽니다. 창에서 **카탈로그**, **설치된 스킬**, **실행 환경** 탭을 제공합니다. 검색으로 스킬 목록을 좁히고 카탈로그의 분야 필터로 스킬을 선택할 수 있습니다. 스킬을 선택하면 출처, 저자, 라이선스, 원본 버전과 사용 조건을 표시합니다.

옵션 탭에는 짧은 사용 안내와 마켓플레이스 열기 버튼만 표시합니다. AI 창의 **OpenAI 계정 / 설정 > 전용 스킬 > 설치된 AI 스킬 관리**를 선택하면 설치된 스킬 탭을 바로 엽니다. 대화 상자를 닫으면 AI 창이 활성 스킬 수와 스킬 사용 상태를 다시 조회합니다.

이어서 카탈로그 조회와 로컬 작업의 관계를 짚어보겠습니다. 두 작업은 진행 상태를 각각 관리합니다. 카탈로그 요청에 실패해도 설치된 스킬을 관리할 수 있습니다. [창](../src/TableCloth.App/Dialogs/SkillMarketplaceWindow.axaml)과 [뷰 모델](../src/TableCloth.App/ViewModels/SkillMarketplaceWindowViewModel.cs)이 이 동작을 구현합니다.

## 버전을 지정하는 별도 XML 카탈로그

카탈로그는 `https://catalog.yourtablecloth.app/SkillCatalog.xml`에서 제공합니다. 원본은 [TableClothCatalog의 docs 디렉터리](https://github.com/yourtablecloth/TableClothCatalog/tree/main/docs)에 두며 `SkillCatalog.xsd`와 기여 안내를 함께 관리합니다. 기존 Pages 워크플로가 `main`에 반영한 파일을 게시합니다.

`SkillCatalog Version="1"` 아래에 고정 ID를 가진 `Skill` 항목을 기록합니다. 각 항목은 GitHub 저장소, 경로, `Source.Ref`에 고정한 원본 커밋을 지정합니다. 설명, 저자, SPDX 식별자, 라이선스 근거, 런타임과 네트워크 요구 사항, 설치 방식, 사용 조건도 기록합니다. `Skill` 블록을 주석 처리하거나 삭제하면 원격 목록에서 제외합니다. 기존 설치본은 계속 관리할 수 있습니다. [공용 모델](../src/TableCloth.ManagedAi.Core/SkillCatalog.cs)에 애플리케이션이 읽는 필드를 정의했습니다.

## 카탈로그 버전에 맞춘 설치와 업데이트

원격 설치와 버전 교체 동작을 확인하겠습니다. 미설치 스킬을 선택하면 검토 승인 없이 설치 버튼을 사용할 수 있습니다. 설치 시 클라이언트가 스킬 ID로 현재 카탈로그를 다시 조회합니다. 원본은 고정한 커밋의 upstream 스킬을 가리키며 라이선스 근거는 같은 저장소와 커밋의 파일을 가리킵니다. 클라이언트는 잘못된 메타데이터나 파일을 검증 단계에서 거절합니다. 이전 카탈로그의 상태 속성은 전환 기간에 읽을 수 있지만 설치를 제한하는 데 사용하지 않습니다.

클라이언트는 Git 체크아웃 없이 GitHub REST API로 선택한 파일을 조회합니다. 경로와 저장소 항목을 검사하고 요청 및 다운로드 크기를 제한합니다. 링크와 서브모듈을 거절하며 임시 디렉터리에 파일을 받은 뒤 설치를 확정합니다. 고정한 라이선스 파일도 `LICENSE.upstream`으로 보존합니다. XML 파서는 외부 엔터티와 DTD를 허용하지 않습니다. [OpenAI 백엔드](../src/TableCloth.ManagedAi.OpenAi/Skills)가 이 검사를 수행합니다.

마켓플레이스는 설치 시 카탈로그 ID, 저장소, 경로와 원본 커밋을 보관합니다. 같은 출처의 `Source.Ref`가 달라지면 카탈로그와 설치된 스킬 화면에서 해당 버전으로 업데이트할 수 있습니다. 커밋 해시는 버전을 구분하며 시간상 선후 관계를 뜻하지 않습니다. 업데이트는 교체 파일을 임시 폴더에서 검증한 뒤 설치본에 반영합니다. 검증에 실패하거나 작업을 취소하면 기존 파일, 버전 정보와 사용 설정을 보존합니다. 같은 ID의 로컬 스킬 가져오기 항목은 마켓플레이스 업데이트로 덮어쓰지 않습니다.

다만 스킬에 따라 API 키, OAuth 자격 증명, MCP 연결 또는 추가 패키지를 사용합니다. 카탈로그의 사용 조건에 이러한 의존성을 기록합니다. 스킬 파일을 설치해도 서비스 자격 증명을 제공하거나 명령 실행을 허용하지 않습니다. 실행 환경 탭에서 아래에 설명한 전용 Node.js와 Python을 관리합니다. 설치할 수 있는 `SKILL.md`가 없는 원본은 어댑터를 제공할 때까지 주석으로 보관합니다.

## 로컬 스킬과 전용 실행 환경 관리

설치된 스킬 화면에서 폴더 가져오기, 카탈로그 버전으로 업데이트, 활성화와 비활성화, 확인 후 제거, 새로 고침, 스킬 폴더 열기를 제공합니다. 마켓플레이스에서 설치한 항목에는 설치 당시의 원본 버전을 표시합니다. 사용 설정은 이후 AI 작업에 적용합니다. 제거 시 해당 전용 폴더와 출처 정보를 함께 삭제합니다. 카탈로그에서 내린 스킬이나 원격 조회에 실패한 경우에도 제거할 수 있습니다.

잘못된 `SKILL.md`도 오류 상태로 목록에 표시합니다. 해당 항목을 비활성화하거나 폴더를 열어 확인하고 개별 제거할 수 있습니다. 다른 스킬의 관리도 계속할 수 있습니다. 오류 항목의 활성화는 차단합니다. AI 실행은 잘못된 관리 대상 manifest를 복구하거나 제거할 때까지 오류로 중단합니다.

스킬은 `%LOCALAPPDATA%\TableCloth\ManagedAi\profiles\openai-codex\codex-home\skills`에 보관합니다. 기존 가져오기 기능과 작업 잠금이 변경 작업을 제어합니다. 마켓플레이스는 이 프로필을 관리합니다. 사용자 전역 Codex 프로필과 스킬은 관리 범위에 포함하지 않습니다. [스킬 관리자](../src/TableCloth.ManagedAi.OpenAi/Skills/OpenAiCodexSkillManager.cs)와 [관리 경로](../src/TableCloth.ManagedAi.Core/ManagedAiPaths.cs)에서 저장 범위를 확인할 수 있습니다.

실행 환경 탭은 설치한 Node.js와 Python 버전을 표시하고 **실행 환경 설치/업데이트**, **설치 및 환경 검사**를 제공합니다. Codex 설치 경로에서도 두 실행 환경을 함께 준비합니다. 애플리케이션은 Windows x64와 ARM64용 Node.js 24.22.1, npm과 npx 11.19.1, Python 3.14.9를 고정해 사용합니다. [Node.js](https://nodejs.org/en/download/archive/v24.22.1)와 [Python](https://www.python.org/downloads/release/python-3149/)의 공식 ZIP을 내려받으며 [실행 환경 manifest](../src/TableCloth.ManagedAi.OpenAi/Distribution/SkillRuntimeManifest.cs)에 버전과 SHA-256 해시를 지정합니다.

실행 파일은 `%LOCALAPPDATA%\TableCloth\ManagedAi\runtimes\skill-tools\releases\<설치 ID>` 아래의 `node`, `python` 디렉터리에 보관합니다. 설치 관리자는 압축 파일의 경로와 크기, 해시, 실행 버전과 설치 기록을 검사합니다. 검증을 마치면 활성 버전 정보를 원자적으로 교체해 두 실행 환경을 함께 전환합니다. 교체에 실패하거나 취소하면 이전 활성 버전을 보존합니다. npm 캐시, 설정, 임시 파일과 자식 프로세스의 홈 디렉터리는 `%LOCALAPPDATA%\TableCloth\ManagedAi\profiles\skill-tools` 아래에서 관리합니다. 전용 실행 경로는 자식 프로세스에만 적용합니다. 식탁보는 전역 `PATH`, 레지스트리와 기존 Node.js 또는 Python 설치를 변경하지 않습니다.

Python은 [임베디드 배포판](https://docs.python.org/3.14/using/windows.html#the-embeddable-package)의 기본 `python314._pth` 격리를 유지합니다. 이번 설치는 표준 라이브러리를 사용하는 스크립트를 지원하며 pip, ensurepip, venv와 외부 Python 패키지 관리를 제공하지 않습니다. 설치 관리자는 `python3` 명령을 사용하는 스크립트를 위해 원본과 바이트가 같은 `python3.exe` 사본을 제공합니다. 추가 Python 패키지를 사용하는 스킬에는 별도의 의존성 검증이 남습니다.

## Windows 정책을 변경하지 않는 긴 경로 검사

**설치 및 환경 검사**는 Windows 긴 경로 정책을 읽기 전용으로 조회하고 .NET 파일 I/O, Node.js와 Python을 각각 검사합니다. 짧은 경로에서 기본 동작을 확인한 뒤 애플리케이션의 전용 진단 폴더 아래에서 330자를 초과하는 경로로 임시 파일 생성, 읽기, 이름 변경, 다시 읽기와 삭제를 수행합니다. 화면은 **긴 경로 검사 통과**, **긴 경로 사용 제한**, **설치 안 함**, **확인 불가**를 구분합니다. 접근 권한 문제 등으로 검사를 마치지 못한 경우에는 긴 경로 제한으로 단정하지 않습니다. 제한이나 확인 불가 상태는 안내로 표시하며 설치, 카탈로그 조회와 로컬 스킬 관리를 차단하지 않습니다. [진단 계약](../src/TableCloth.ManagedAi.Core/SkillRuntimeDiagnostics.cs)에서 결과 범위를 확인할 수 있습니다.

Windows 정책 변경 권한은 관리자나 조직 정책의 관리 범위에 속합니다. 식탁보는 레지스트리나 정책을 수정하지 않습니다. 최신 .NET은 긴 경로를 자체 처리하지만 Python의 일반 경로가 260자를 초과하면 Windows 긴 경로 지원 설정의 영향을 받습니다. 따라서 .NET의 압축 해제나 실행 환경의 버전 검사를 통과해도 자식 프로세스의 긴 경로 지원을 보장하지 않습니다. 실제 I/O 검사를 통과한 경우에도 외부 도구나 패키지에 따라 긴 경로 사용이 제한될 수 있습니다. [Windows 정책 조건](https://learn.microsoft.com/en-us/windows/win32/fileio/maximum-file-path-limitation), [.NET 경로 처리](https://learn.microsoft.com/en-us/dotnet/standard/io/file-path-formats#skip-normalization), [Python 긴 경로 지원](https://docs.python.org/3.14/using/windows.html#removing-the-max-path-limitation)에서 각 실행 환경의 조건을 설명합니다.

## 자동 검증과 게시 경로의 구분

자동 검증의 범위를 검토하겠습니다. 테스트는 검토 조건 없는 카탈로그 파싱, 다운로드 검증, 버전 추적, 교체 실패 시 복구, 제거와 뷰 모델 명령을 다룹니다. 실행 환경 테스트는 합성 ZIP과 프로세스 응답으로 무결성, 압축 해제 제한, 격리 설정과 두 실행 환경의 일괄 교체를 검사합니다. Avalonia 헤드리스 테스트는 실제 창을 생성합니다. 서비스의 사용자 자격 증명 연동이나 실제 MCP 연결은 이 테스트로 확인하지 않습니다. [스킬 테스트](../src/TableCloth.ManagedAi.Test/SkillMarketplaceTests.cs), [실행 환경 테스트](../src/TableCloth.ManagedAi.Test/SkillRuntimeTests.cs), [UI 테스트](../src/TableCloth.Test/SkillMarketplaceTests.cs)에 검증 항목을 기록했습니다.

[CI 검사 도구](../tools/verify-skill-runtime-long-paths.ps1)는 일회용 GitHub 호스팅 Windows 러너에서 긴 경로 정책을 켠 경우와 끈 경우를 새 프로세스로 검사하고 `finally`에서 원래 정책을 복원합니다. 로컬과 자체 호스팅 러너에서는 실행을 거절합니다. 애플리케이션의 진단 기능은 정책을 읽기만 합니다. [진단 테스트](../src/TableCloth.ManagedAi.Test/SkillRuntimeDiagnosticsTests.cs)는 확인 불가, 접근 권한 오류, 취소와 임시 파일 정리도 검증합니다.

k-dart의 서비스 호출에서는 명령 실행 권한과 API 키 연동 범위를 구분해 다루겠습니다. Node.js와 Python을 설치해도 DART 연동 성공을 뜻하지 않습니다. 이번 변경은 채팅의 명령 실행 정책과 API 키 전달 방식을 유지합니다. [전용 프로필](../src/TableCloth.ManagedAi.Windows/WindowsManagedAiProfile.cs), [호출 구성](../src/TableCloth.ManagedAi.OpenAi/Execution/OpenAiCodexInvocationBuilder.cs), [대화 지침](../src/TableCloth.ManagedAi.OpenAi/Execution/OpenAiChatPromptFactory.cs)이 실행 범위를 제한합니다. 필요한 명령을 허용하고 `API_K_DART`를 별도 자격 증명 경로로 제공하는 기능은 추가 구현과 검증 범위로 남습니다.

카탈로그와 애플리케이션은 서로 다른 경로로 배포합니다. TableClothCatalog가 XML을 게시하면 원격 후보를 조회할 수 있습니다. 사용자는 이후 식탁보 빌드에서 마켓플레이스 창을 사용할 수 있습니다. 카탈로그 게시를 기다리는 동안에도 애플리케이션에서 설치된 스킬을 관리할 수 있습니다.

## 카탈로그 변경과 기존 설치본 관리

여기까지 정리하면 카탈로그는 활성 `Skill` 블록으로 설치 대상을 제공합니다. 관리자가 원본 커밋과 해당 라이선스 경로를 갱신하면 마켓플레이스가 그 버전을 업데이트 대상으로 표시합니다. 블록을 주석 처리하면 원격 목록에서 내립니다. [카탈로그 기여 안내](https://github.com/yourtablecloth/TableClothCatalog/blob/main/docs/SkillCatalog.README.ko.md)에서 작성 규칙을 다룹니다.

원격 목록에서 내려도 사용자가 기존 설치본을 계속 관리합니다. 다른 카탈로그 버전이 있으면 업데이트하고 더 이상 사용하지 않을 때는 제거할 수 있습니다. 서비스 자격 증명과 실행 환경 의존성은 스킬별 사용 조건에 따릅니다.
