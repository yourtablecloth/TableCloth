# 스킬 스크립트의 AppContainer 제품 연결

[English](appcontainer-product-integration.md)

**현재 개발 구현은 [스킬 마켓플레이스와 격리 실행 안내](../SKILL_MARKETPLACE.ko.md)에서 설명합니다.** 이 문서는 이전 Preview 7 준비 기록을 보존합니다. 당시 계산기 전용 어댑터와 검사 결과를 현재의 범용 등록 경로와 구분합니다.

2026년 10월 27일의 개발 구현은 활성화한 유효한 설치 스킬에서 Python과 Node.js 진입점을 찾아 앱 소유 MCP 중계기에 등록합니다. 활성 카탈로그는 BOK, KOSIS와 계산기의 스크립트 스킬 3개, 보고서 작성 지침 스킬 1개, 인증서 만료와 Windows Sandbox의 호스트 보고서 스킬 2개를 제공합니다. 지침 스킬과 호스트 보고서 스킬은 기존 처리 계약을 유지하며 스크립트 작업 프로세스를 추가하지 않습니다. [현재 실행 경로와 제한](../SKILL_MARKETPLACE.ko.md#mcp-연결과-appcontainer-실행-경계)을 함께 확인할 수 있습니다.

작업 프로세스에는 네트워크 capability를 부여하지 않습니다. 허용 대상 BOK와 KOSIS 요청은 공개 HTTPS GET 경로를 제한하는 호스트 전송을 사용하며 다른 목적지, 사용자 자격 증명과 임의 패키지 관리자 실행은 허용하지 않습니다. 전용 프로필은 `shell_tool=false`와 `unified_exec=false`를 유지하고 원본의 `npx` 로더 대신 고정한 공개 지침을 제공합니다. 이 설명은 구현 경로를 다루며 새로운 테스트 통과나 릴리스 검증을 선언하지 않습니다. [현재 안내](../SKILL_MARKETPLACE.ko.md)는 실행 경로, 실행 증거와 외부 서비스 가용성을 구분합니다.

## Preview 7 준비 기록의 과거 검증 범위

Preview 7 준비 브랜치는 검토한 계산기를 당시 식탁보 실행 파일을 통해 채팅에 연결했고 고정 버전의 전용 Python과 Node.js를 자동으로 설치했습니다. 2026년 10월 26일 Windows x64 새 설치 검사에서는 두 런타임의 버전과 AppContainer 안에서 320자를 넘는 경로의 파일 읽기 및 쓰기를 확인했습니다.

이하에서는 당시 제품 호출 경로, 계산기 연산 6개의 제한, 런타임 진단, 스킬 관리와 보존한 실행 증거를 살펴보겠습니다. 당시 계산기 해시 제한을 이후 범용 스크립트 등록 구현에 적용하지 않습니다.

> 아래의 모든 실측과 계산기 전용 동작은 2026년 10월 26일 Preview 7 준비 단계를 기록합니다. 이후 범용 스크립트 지원, Windows 10 22H2와 ARM64 실행, 최종 설치 패키지, 서명과 게시의 검증 결과를 뜻하지 않습니다. 현재 구현은 [마켓플레이스 안내](../SKILL_MARKETPLACE.ko.md)에서 설명합니다.

## 당시 제품 실행 파일을 거친 스킬 호출

Preview 7 준비 단계의 채팅 공급자는 활성화한 로컬 스킬을 조회하고 `WindowsManagedAiSkillExecution`에 연결 정보를 요청했습니다. 유효한 계산기를 활성화했고 스크립트 해시가 검토한 값과 일치하면 전용 런타임을 준비하고 계산기 MCP 도구를 등록했습니다. 수동 활성화 기록이나 별도로 복사한 제품 호스트는 사용하지 않았습니다. 이후 [현재 소스](../../src/TableCloth.ManagedAi.Windows/WindowsManagedAiSkillExecution.cs)는 계산기 전용 등록을 설치 진입점의 범용 검색으로 교체했습니다.

호출은 `식탁보 채팅 -> 전용 Codex CLI -> tablecloth_skills/tablecloth-calculators -> 현재 TableCloth.exe --skill-mcp -> AppContainer Python 작업 프로세스` 순서로 진행합니다. [제품 진입점](../../src/TableCloth/Program.cs)은 GUI, 업데이트 검사와 앱 로그를 초기화하기 전에 중계기를 실행합니다. 앱 전용 프로필은 `shell_tool=false`, `unified_exec=false`, 읽기 전용 Codex 샌드박스와 승인 정책 `never`를 유지합니다. [호출 구성기](../../src/TableCloth.ManagedAi.OpenAi/Execution/OpenAiCodexInvocationBuilder.cs)는 현재 호출에 등록한 계산기만 승인합니다. [Codex MCP 설정](https://learn.chatgpt.com/docs/extend/mcp)은 도구 전송을 담당하며 [네이티브 실행기](../../src/TableCloth.ManagedAi.AppContainer/Native/AppContainerProcessRunner.cs)는 작업 프로세스를 재개하기 전에 AppContainer 토큰을 검사합니다.

## 당시 계산기 전용 어댑터와 연산 6개의 제한

Preview 7 어댑터는 `tablecloth-calculators/scripts/calculate.py`의 SHA-256이 `279b627d4350b8895717a6ac3597a6e87cab7a9882004d639251ebbee3e7c947`과 일치할 때만 스크립트를 허용했습니다. 스크립트를 변경하거나 삭제하면 실행을 차단했습니다. 등록한 연산은 `percent.part`, `discount.single`, `loan`, `date.add`, `time.convert`와 `bmi`로 제한했습니다. 이 제한은 당시 어댑터에 적용했으며 [현재 구현](../../src/TableCloth.ManagedAi.Windows/WindowsManagedAiSkillExecution.cs)의 범용 등록과 구분합니다.

당시 작업 프로세스는 검증한 JSON을 입력으로 받고 네트워크 capability 없이 고정 Python 진입점을 실행했습니다. 모델의 도구 인수에서 셸 명령, 실행 파일 경로나 스크립트 소스를 받지 않았습니다. 중계기는 pip, npm과 npx 실행 경로를 제공하지 않았습니다. Node.js는 설치와 네이티브 진단에 사용했고 Node.js 마켓플레이스 어댑터, BOK 어댑터와 KOSIS 어댑터는 등록하지 않았습니다. 당시 계산기 실측으로 다른 카탈로그 스크립트의 지원까지 입증하지는 않았습니다.

## 당시 런타임 압축 파일과 의존 파일의 무결성 검사

[WindowsManagedAiScriptRuntimeManager](../../src/TableCloth.ManagedAi.Windows/ScriptRuntimeManager.cs)는 Windows x64 또는 ARM64에 맞는 Python 3.14.9 embeddable과 Node.js 24.22.1 패키지를 선택합니다. [ScriptRuntimePackages](../../src/TableCloth.ManagedAi.Windows/ScriptRuntimePackages.cs)에 공식 URL과 SHA-256을 고정했습니다. Python 해시는 [공식 릴리스 메타데이터](https://www.python.org/ftp/python/3.14.9/windows-3.14.9.json), Node 해시는 [공식 체크섬 파일](https://nodejs.org/dist/v24.22.1/SHASUMS256.txt)을 근거로 사용합니다.

지원하는 계산기를 처음 호출하면 앱 전용 `ManagedAi/skill-execution` 디렉터리에 런타임을 설치하거나 복구합니다. 설치기는 압축 파일의 해시와 항목 경로를 검사하고 임시 디렉터리에 압축을 풉니다. 이후 설치한 모든 파일을 고정한 공식 압축 파일의 해당 항목과 대조합니다. Node 설치 범위는 `node.exe`와 `LICENSE`로 제한하며 npm과 npx는 설치하지 않습니다. 전역 PATH, 시스템 런타임, 레지스트리와 전역 Codex 설정은 변경하지 않습니다.

호출마다 호스트가 고유 실행 등록 파일을 만들고 검토한 스크립트와 `runtimeFiles` 해시를 기록합니다. [중계기](../../src/TableCloth.ManagedAi.AppContainer/Broker/SkillExecutionRegistry.cs)는 스크립트와 검증한 의존 파일을 읽기 공유 모드로 열고 작업 프로세스가 끝날 때까지 파일 핸들을 유지합니다. 설치기는 복구 설치 전에 네이티브 실행기의 프로세스 간 잠금 안에서 남은 ACL 기록의 복구를 시도합니다. 복구에 실패하거나 설치 잠금을 얻은 뒤에도 ACL 기록이 남아 있으면 복구 설치를 차단합니다. 설치 기록은 검사 결과를 보관하며 공식 압축 파일과의 대조를 대신하지 않습니다.

## 당시 마켓플레이스의 버전 표시와 격리 환경 긴 경로 검사

마켓플레이스의 세 번째 탭인 **실행 환경**은 전용 Python과 Node.js의 설치 버전과 런타임별 긴 경로 결과를 표시합니다. **실행 환경 설치/복구**는 전용 패키지를 준비합니다. **설치 및 환경 검사**는 기존 설치 파일과 격리 환경의 동작을 검사하며 없는 패키지를 내려받지는 않습니다. [마켓플레이스 UI](../../src/TableCloth.App/Dialogs/SkillMarketplaceWindow.axaml)에서 두 기능을 제공합니다.

각 [검사](../../src/TableCloth.ManagedAi.Windows/ScriptRuntimeManager.cs)는 네트워크를 차단한 AppContainer 안에서 실제 전용 인터프리터를 실행합니다. 320자를 넘는 임시 경로를 만들고 파일을 쓴 뒤 다시 읽습니다. 결과는 성공이면 `true`, 경로 사용 실패를 확인하면 `false`, 결과를 확정하지 못하면 `null`로 반환합니다. UI는 제한과 미확인 상태 모두에 기능 제한 가능성을 표시합니다. 두 런타임이 모두 통과해야 경고를 숨기며 새 검사에 실패하면 이전의 성공 결과를 폐기합니다.

다만 검사 결과는 현재 인터프리터와 격리한 임시 디렉터리에 적용합니다. 추가 라이브러리나 도구에는 다른 제한이 있을 수 있습니다. 제품 자체의 설치 및 중계기 경로 길이 검사는 유지하며 Windows 긴 경로 정책을 변경하지 않습니다. [런타임 UI 상태](../../src/TableCloth.App/ViewModels/SkillMarketplaceWindowViewModel.Runtimes.cs)에서 경고 조건을 확인할 수 있습니다.

## 당시 계산기의 설치와 업데이트 및 제거 절차

계산기와 전용 런타임은 마켓플레이스에서 관리합니다. [마켓플레이스 창](../../src/TableCloth.App/Dialogs/SkillMarketplaceWindow.axaml)의 사용 절차를 정리하겠습니다.

1. 카탈로그에서 `tablecloth-calculators`를 설치하고 활성화합니다.
2. **실행 환경**에서 버전과 긴 경로 검사 결과를 확인합니다. 채팅 전에 런타임을 준비하려면 **실행 환경 설치/복구**를 실행합니다.
3. 새 대화에서 지원하는 계산을 요청합니다. 성공한 계산기 MCP 호출 결과를 기준으로 스크립트 실행을 확인합니다. 모델이 스킬 이름을 언급한 문구만으로 실행을 판정하지 않습니다.
4. **설치된 스킬**에서 스킬을 업데이트하거나 비활성화 또는 제거합니다. 이후 호출은 비활성화하거나 제거한 스킬에 실행 도구를 등록하지 않습니다.

당시 어댑터는 스크립트 해시가 바뀌면 새 어댑터의 검토와 제품 반영을 요구했습니다. 설치한 원본 스크립트를 삭제하거나 변경하면 과거에 준비한 복사본으로 실행을 대신하지 않았습니다. 스킬을 비활성화해도 공유 런타임은 유지했습니다. 이후 범용 구현은 현재 설치한 패키지의 실행별 사본과 새 해시를 기록합니다. [현재 스킬 관리](../SKILL_MARKETPLACE.ko.md#로컬-설치와-업데이트-및-활성화-설정)와 [등록 테스트](../../src/TableCloth.ManagedAi.Test/WindowsSkillRegistrationTests.cs)에서 해당 동작을 설명합니다.

과거 `Enable-ProductSkillExecution.ps1` 절차는 로컬 수동 활성화 실험에 해당합니다. Preview 7 준비 브랜치는 현재 제품 실행 파일과 자동 런타임 준비를 사용하며 해당 스크립트의 활성화 기록을 읽지 않습니다.

## 보존한 Preview 7 런타임 검사와 이전 모델 실험의 증거

저장소의 [제품 런타임 및 공급자 검증 기록](appcontainer-production-runtime-evidence.json)은 `Artifacts/runtime-preview7-check/evidence.json`, 설치 기록과 `Artifacts/release-v1.22.0-preview.7/product-routing`을 요약합니다. 새 x64 설치에서 AppContainer 안의 Python 3.14.9와 Node.js 24.22.1을 확인했고 두 런타임 모두 320자를 넘는 경로의 읽기 및 쓰기 검사에 성공했습니다. 런타임 구현의 단위 테스트 19개도 통과했습니다.

최종 로컬 공급자 검사에서는 의존성 주입으로 실제 `OpenAiCodexProvider.ChatAsync`를 조회하고 앱 전용 Codex CLI 0.156.1, 전용 프로필과 실제 GPT-6-Luna 모델을 사용했습니다. 완료된 MCP 호출은 가격 1,000,000과 할인율 10으로 `discount.single`을 실행했습니다. 작업 프로세스는 `wasAppContainer=true`, 종료 코드 `0`, 할인액 `100000.00`과 최종 금액 `900000.00`을 기록했고 공급자도 같은 금액을 반환했습니다. JSONL은 완료된 MCP 호출 1회, 완료된 명령 실행 0회와 대화 완료를 기록합니다. `shell_tool=false`, `unified_exec=false`, 읽기 전용 Codex 샌드박스와 승인 정책 `never`도 유지했습니다. 저장소의 요약은 결과 파일의 해시를 보존하며 인증정보, 대화 ID와 절대 사용자 경로를 포함하지 않습니다.

당시 준비 단계의 최종 로컬 Release 검사에서는 Managed AI 테스트 219개, TableCloth 테스트 334개와 Spork 테스트 43개가 통과했습니다. [검증 기록](appcontainer-production-runtime-evidence.json)은 각 테스트 모음을 런타임 단위 검사와 이전 PoC 수치로부터 구분합니다. 공급자 검사는 당시 프레임워크 종속 제품 실행 파일을 사용했습니다. 이 검사로 서명한 릴리스 파일과 GUI 자동 조작을 검증하지는 않았습니다.

이어서 과거의 [제품 연결 검증 기록](appcontainer-product-integration-evidence.json)은 2026년 10월 25일의 수동 활성화 실험을 설명합니다. 실제 GPT-6-Luna 모델을 사용한 호출 구성기 세션 3개와 `OpenAiCodexProvider.ChatAsync` 세션 1개에서 계산기 MCP 호출 완료, `wasAppContainer=true`와 종료 코드 `0`을 기록했습니다. 할인 계산은 100만 원의 할인액 10만 원과 최종 금액 90만 원을 산출했고 BMI는 27.36을 산출했습니다. 해당 기록은 당시 작업 트리의 소스와 로그 해시를 보존합니다. 최종 Preview 7 런타임 설치기나 제품 패키지의 검증 결과로 사용하지 않습니다.

기존 [독립 PoC 보고서](../../tools/appcontainer-skill-poc/RESULTS.ko.md)는 결정적 모델 모의 응답 연결을 포함한 별도 검증 단계를 다룹니다. 당시 검사 수와 Managed AI 테스트 수는 과거 수치로 유지하며 현재 제품 검사와 합산하지 않습니다. 실제 작업 프로세스 실행, 실제 모델의 도구 호출, UI 검증과 릴리스 파일 검증은 각각 입증하는 범위가 다릅니다.

## 과거 검증 기록의 대상 플랫폼과 격리 경계

당시 패키지 구성은 x64와 ARM64의 공식 URL과 해시를 포함했습니다. 보존한 Preview 7 새 설치 검증 기록은 개발 장비의 실제 x64 실행을 다룹니다. Windows 10 22H2, ARM64 실행, GUI 자동 조작과 강제 전원 차단 후 복구는 이 기록의 범위에 포함하지 않습니다. 빌드 산출물, 서명과 게시는 저장소의 [릴리스 절차](../RELEASING.md)를 따릅니다.

일반 AppContainer는 호스트 커널을 공유하며 기존 ACL이 토큰에 허용한 리소스는 읽을 수 있습니다. 토큰 검사, 필요한 범위의 ACL 권한, 네트워크 차단과 Job Object만으로 모든 호스트 경로의 차단을 입증하지는 않습니다. Microsoft의 [AppContainer 구현 안내](https://learn.microsoft.com/en-us/windows/win32/secauthz/implementing-an-appcontainer)에서 해당 경계를 설명합니다.

여기까지 정리하면 Preview 7 준비 기록은 당시 계산기 실행 경로와 전용 런타임 진단을 입증합니다. 이후 범용 Python과 Node.js 등록 및 공개 GET 호스트 전송은 [현재 마켓플레이스 안내](../SKILL_MARKETPLACE.ko.md)에서 구현 경로와 검증 범위를 구분합니다. 이 문서의 계산기 전용 실측을 이후 경로나 추가 플랫폼의 검증 결과로 확대하지 않습니다.

성공한 MCP 결과와 네이티브 격리 기록을 기준으로 해당 호출의 실행 여부를 판단할 수 있습니다. 지원 플랫폼을 넓힐 때에는 해당 운영체제와 아키텍처의 실제 실행 기록 및 릴리스 파일 검증 결과를 함께 적용할 수 있습니다.
