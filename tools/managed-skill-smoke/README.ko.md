# 제품 스킬 실행 재현 도구

[English](README.md)

2026년 10월 27일 기준으로 이 도구는 제품 DI, 설치 스킬 등록, AppContainer 스크립트 실행과 실제 Codex 대화를 재현합니다. 도구 소스를 추가한 사실만으로 테스트 통과나 릴리스 검증을 선언하지 않습니다. [마켓플레이스 안내](../../docs/SKILL_MARKETPLACE.ko.md)에서 구현 경로와 외부 서비스의 제한을 설명합니다.

도구는 스크립트 직접 실행 결과, 실제 모델과 MCP 이벤트, 지침에 맞는 응답과 합성 호스트 보고서를 구분합니다. 식탁보 전용 Codex 프로필을 사용하고 기존 스킬의 활성화 설정을 유지합니다. 아래에서는 준비, 격리 실행, 모델 요청, 네이티브 제품 파일의 출처와 보존할 증거를 순서대로 살펴보겠습니다.

## 기존 앱 전용 프로필을 사용하는 제품 DI

`Program.cs`는 실제 `UseTableCloth()` 구성을 호출하고 `ISharedLocations`, 런타임, 스킬, 마켓플레이스와 대화 서비스를 유지합니다. 관찰용 실행기는 실제 프로세스 호출을 전달하며 `codex exec --json`의 stdout만 수집합니다. 인증 상태 stdout과 인증정보는 캡처하지 않습니다. 인증정보를 복사하거나 전역 Codex 설정, 시스템 PATH와 레지스트리를 변경하지 않습니다. 전용 프로필은 `shell_tool=false`와 `unified_exec=false`를 유지합니다. [서비스 구성](../../src/TableCloth.App/DependencyInjection/ManagedAiWindowsServiceCollectionExtensions.cs)에서 주입 계약을 확인할 수 있습니다.

실제 대화에는 기존 식탁보 런타임과 로그인 상태를 사용합니다. `--discover`는 정상 Codex 스킬 검색과 제품 bridge 준비를 수행합니다. 준비 과정에서 전용 스크립트 런타임을 설치하거나 검증할 수 있으므로 로컬 런타임을 전혀 변경하지 않는 검사로 표현하지 않습니다. BOK와 KOSIS의 직접 검사는 제한한 공개 서비스에 요청하며 외부 서비스 장애와 격리 실행 실패를 구분합니다.

현재 실행은 전용 런타임과 프로필, 설치 스킬, 작업자 파일과 진단 로그를 `%LOCALAPPDATA%\TableCloth.Data\ManagedAi`에 보존합니다. Codex 프로필은 이 데이터 루트 아래의 `profiles\openai-codex\codex-home`을 사용합니다. 설치 프로그램이 관리하는 `%LOCALAPPDATA%\TableCloth`와 데이터를 분리하며 제품 실행 파일은 `TableCloth\current`에 유지합니다. 인증정보는 전용 프로필에 보존하고 sidecar나 수집 증거에는 포함하지 않습니다. 라이브 네이티브 fixture 두 개는 이전 `TableCloth\ManagedAi` 위치를 가리키는 seed를 거절합니다. 설치 패키지 검사 도구도 실제 기록한 데이터 루트가 새 위치인지 확인합니다.

`--install-catalog`를 지정하면 정상 마켓플레이스 서비스로 누락된 활성 항목을 설치합니다. 새 설치본은 검사 후에도 유지합니다. 기존 패키지, 원본 버전과 활성화 설정은 변경하지 않으며 필요한 스킬이 비활성 상태이거나 유효하지 않으면 실패를 기록합니다. 이 옵션을 생략하면 누락된 카탈로그 스킬을 설치하지 않습니다.

`--install-runtime <version>`은 검색 전에 정상 설치 서비스로 지정한 식탁보 전용 Codex 버전을 설치합니다. 복원 준비에는 `--discover --install-runtime 0.156.1 --install-catalog`와 같이 사용할 수 있습니다. 로그인이나 인증정보 가져오기는 수행하지 않으며 전역 Codex도 변경하지 않습니다. 실제 대화 전에는 전용 프로필의 로그인을 사용자가 직접 완료합니다. 설치 패키지 검증에서는 이 옵션을 생략하여 사전 설치로 누락된 런타임을 감추지 않습니다.

## 직렬 빌드와 제품 bridge 검색

저장소 루트에서 `global.json`이 선택한 SDK로 빌드합니다. 다른 솔루션 빌드와 동시에 실행하지 않습니다. 다음 예시는 관리형 진단 프로세스를 빌드하고 모델 요청 없이 실제 설치 항목과 등록 도구를 조회합니다.

```powershell
dotnet build tools/managed-skill-smoke/AllSkillsProbe.csproj -c Release
dotnet tools/managed-skill-smoke/bin/Release/net11.0-windows10.0.18362.0/AllSkillsProbe.dll --discover
```

프로젝트는 DI에 사용할 앱 라이브러리와 `ReferenceOutputAssembly=false`를 지정한 실제 제품 호스트를 참조합니다. 빌드 후 대상은 `GetTargetPath`로 제품 호스트의 실제 출력 디렉터리를 얻고 `TableCloth.exe`, `TableCloth.dll`, `TableCloth.deps.json`, `TableCloth.runtimeconfig.json` 네 파일만 probe 옆에 복사합니다. 제품의 `ISharedLocations`가 해당 실행 파일을 MCP 자식 프로세스로 선택합니다. 관리형 probe의 빌드 성공만으로 자식 프로세스가 NativeAOT라고 판단하지 않습니다. 검색 결과는 실제 호스트 경로, 해시, 버전과 MCP 도구 목록을 기록합니다.

## 직접 스크립트 실행과 계산기 전체 22개 연산

`--direct`는 실제 제품 registry를 통해 등록한 BOK, KOSIS와 계산기 구현을 호출합니다. `--node-fixture`는 임시 JSON stdin Node.js 패키지를 별도로 검사하고 `finally`에서 원본 fixture 디렉터리를 제거합니다. 이 합성 패키지는 마켓플레이스 스킬에 해당하지 않습니다. 다음 예시는 같은 bridge 수명 안에서 고정 계산기의 전체 사례도 실행합니다.

```powershell
$calculatorDataset = (Resolve-Path tools/managed-skill-smoke/calculator-cases.json).Path
dotnet tools/managed-skill-smoke/bin/Release/net11.0-windows10.0.18362.0/AllSkillsProbe.dll --direct --node-fixture --calculator-cases $calculatorDataset
```

각 계산 사례는 격리 실행 성공, 종료 코드 0, 성공한 스크립트 응답, 비어 있지 않은 가정 목록과 `JsonElement.DeepEquals(result, expectedResult)` 일치를 확인합니다. 데이터셋은 금액 보존과 날짜 경계를 포함한 [고정 원본 연산 22개](https://github.com/yourtablecloth/tablecloth-calculators/blob/877b58cf0741db21342445f2b3fcb54ad1ada85c/references/operations.md)의 예상 입력과 출력을 제공합니다. 저장된 성공 결과로 사용하지 않습니다. 실제 결과와 사용한 데이터셋 원본 및 해시는 별도로 보존합니다. 일부 사례가 실패해도 나머지 계산 사례를 계속 검사합니다.

## 실제 Codex 대화와 자연어 BMI 요청

`--chat all`은 기존 앱 전용 로그인으로 활성 카탈로그 스킬마다 제품 대화 공급자를 한 번씩 호출합니다. `--chat <스킬 ID>`로 개별 사례를 선택할 수 있습니다. 기본 모델은 `gpt-6-luna`이며 다음 예시는 모델을 직접 지정합니다.

```powershell
dotnet tools/managed-skill-smoke/bin/Release/net11.0-windows10.0.18362.0/AllSkillsProbe.dll --chat all --model gpt-6-luna
```

계산기 사례는 `키 171cm, 몸무게 80kg인 사람의 BMI는 얼마인가요? tablecloth-calculators 스킬을 활용해서 계산해주세요.`라는 자연어 요청을 전송합니다. 요청에 연산 이름이나 Python 입력 필드를 제공하지 않습니다. 성공은 실제 MCP 완료 이벤트, 격리 작업 결과와 BMI `27.36`을 기준으로 판단합니다. 모델이 스킬을 사용했다고 말한 문구로 실행 증거를 대신하지 않습니다.

보고서 사례는 스크립트를 강제로 호출하지 않고 지침에 맞는 초안인지 확인합니다. 인증서 만료와 Windows Sandbox 사례는 합성 자료라고 표시한 `LocalCertificateReport` 및 `LocalWindowsSandboxReport`를 첨부합니다. 실제 인증서를 검색하거나 실제 Sandbox 세션을 조회하고 변경하지 않습니다. 이 응답 검사만으로 모델이 특정 스킬 파일을 읽었다고 판단하지 않습니다. 공개 고정 지침의 메타데이터와 실제 스크립트 이벤트도 각각의 증거 범위를 유지합니다. [대화 요청 계약](../../src/TableCloth.ManagedAi.Core/Contracts.cs)에서 보고서 입력을 확인할 수 있습니다.

## 네이티브 제품 호스트와 독립 중계기 fixture

관리형 probe를 빌드한 뒤 실제 NativeAOT 제품 산출물을 `AllSkillsProbe.dll` 옆에 복사합니다. 진단 프로세스에 필요한 관리형 의존 파일은 유지하고 제품 산출물의 빌드와 커밋 출처를 기록합니다. `--nativepath`는 실제 제품 `ISharedLocations`가 선택한 파일과 경로가 일치하는지 확인합니다. DI를 교체하거나 바이너리를 복사하지 않습니다. 먼저 검색을 실행해 현재 런타임 seed를 얻은 뒤 독립 중계기 fixture에 동일한 제품 실행 파일과 seed를 지정합니다.

```powershell
dotnet '<probe-output>/AllSkillsProbe.dll' --discover --nativepath '<probe-output>/TableCloth.exe'
& ./tools/managed-skill-smoke/native-fixtures/Invoke-NativeSkillSmoke.ps1 -HostExecutable '<probe-output>/TableCloth.exe' -SeedManifest '<probe-output>/evidence/<run>/runtime-seed-manifest.json' -EvidenceDirectory 'D:/Projects/TableCloth/Artifacts/managed-skill-smoke-native'
& ./tools/managed-skill-smoke/native-fixtures/python-concurrency/Invoke-PythonNetworkConcurrency.ps1 -HostExecutable '<probe-output>/TableCloth.exe' -SeedManifest '<probe-output>/evidence/<run>/runtime-seed-manifest.json' -EvidenceDirectory 'D:/Projects/TableCloth/Artifacts/python-concurrency-positive'
& ./tools/managed-skill-smoke/native-fixtures/python-concurrency/Invoke-PythonNetworkConcurrency.ps1 -HostExecutable '<probe-output>/TableCloth.exe' -SeedManifest '<probe-output>/evidence/<run>/runtime-seed-manifest.json' -EvidenceDirectory 'D:/Projects/TableCloth/Artifacts/python-concurrency-control' -LegacyControl
& ./tools/managed-skill-smoke/native-fixtures/Test-ServiceResultValidation.ps1
```

예시의 자리표시자는 로컬 절대 경로로 바꾸고 실행마다 별도 증거 디렉터리를 사용합니다. `--direct`, 전체 22개 연산과 `--chat all`도 `--nativepath`를 지정해 반복하면 선택한 제품 호스트와 결과를 연결할 수 있습니다. 독립 스크립트는 실제 `TableCloth.exe --skill-mcp`를 통해 Python 패키지 가져오기, Node CommonJS와 ESM 모듈, 지원하는 공개 GET, 합성 환경 변수 제외, 파일과 직접 네트워크 접근 거부를 확인합니다. 로컬 경로와 seed 런타임 해시를 검사하고 `-KeepFixtures`를 지정하지 않으면 고유하게 만든 fixture를 정리합니다. NativeAOT 실행이라고 설명할 때에는 실제 호스트의 출처와 성공한 실행 결과를 함께 적용합니다.

[Python 동시성 fixture](native-fixtures/python-concurrency/Invoke-PythonNetworkConcurrency.ps1)는 실제 격리 Python 작업자 안에서 동시 응답의 요청별 대응, URL 및 HTTP 오류, 시간 초과 후 처리를 검사합니다. 합성 응답기를 사용하므로 외부 서비스에 요청하지 않습니다. 기본 실행은 현재 전송 구현이 해당 계약을 유지하는지 확인합니다. `-LegacyControl`은 fixture 내부에 이전의 경합 가능 구현을 넣고 응답 혼선과 격리 작업자 실패를 기대하는 음성 대조군을 실행합니다. 대조군 스크립트는 기대한 실패를 재현할 때 성공으로 종료하며 이전 구현의 정상 동작을 뜻하지 않습니다. [Test-ServiceResultValidation.ps1](native-fixtures/Test-ServiceResultValidation.ps1)은 HTTP 200 API 오류를 포함한 합성 KOSIS 응답을 별도로 검사하며 실제 서비스에 접속하지 않습니다.

[독립 Win32 관찰기](ProcessEvidenceObserver.cs)는 실제 probe 대화와 네이티브 중계기 fixture가 실행되는 동안 중계기 외부에서 동작합니다. 예상한 제품과 런타임 경로에 해당하는 자손 프로세스만 관찰합니다. Windows에서 이미지 경로와 생성 시각을 조회하고 이미지 해시를 계산하며 실제 프로세스 토큰의 `TokenIsAppContainer`를 질의합니다. 조회 실패도 불완전한 증거로 남깁니다. 명령줄, 환경 변수와 인증 파일은 읽지 않습니다. 네이티브 fixture는 `processes.json`에 관측을 저장하며 probe 대화는 스킬별 관측 파일을 저장합니다.

설치 패키지를 검사할 때에는 [Invoke-InstalledSkillProbe.ps1](Invoke-InstalledSkillProbe.ps1)에 `-InstalledDirectory "$env:LOCALAPPDATA\TableCloth\current"`, `-ProbeDirectory '<진단 도구 빌드 출력>'`, `-EvidenceDirectory '<저장소>/Artifacts/<새 실행 폴더>'`와 `-ExpectedProductSha256 '<별도로 신뢰한 제품 해시>'`를 지정합니다. 먼저 해당 NativeAOT 패키지를 설치하고 관리형 진단 도구를 별도로 빌드합니다. 스크립트는 설치된 실행 파일의 해시와 CLR 헤더 부재를 확인하고 원본 설치 파일 전체의 상대 경로, 크기와 해시를 보존합니다. 진단용 최상위 DLL과 probe 실행에 필요한 JSON 파일 두 개만 임시로 추가합니다. 기존 파일을 덮어쓰지 않으며 같은 이름의 의존 파일이 있으면 해시가 같을 때만 진행합니다. 설치 디렉터리의 진단 DLL을 실행하므로 기존 제품 DI가 실제 설치된 `TableCloth.exe`를 선택합니다. 검색, 직접 스크립트 실행, 계산기 22개 사례와 전체 6개 대화를 검사합니다. 이 단계의 부모는 관리형 진단 프로세스로 실행하므로 NativeAOT GUI 부모의 동작까지 입증하지 않습니다.

이어서 스크립트는 원시 증거를 워크스페이스의 새 증거 디렉터리로 복사하고 원본과 해시를 대조한 후 지정한 제품 해시로 읽기 전용 검증기를 실행합니다. 프로세스 stdout, stderr와 종료 코드, 설치 전후 파일 목록과 정리 결과를 보존합니다. 정리 과정에서는 새로 추가한 파일 중 생성 시 해시와 일치하는 파일만 제거합니다. 독점적으로 만든 probe 증거 디렉터리도 보존한 사본과 일치할 때만 제거합니다. 예기치 않은 변경은 검사 실패로 기록하고 요약에 남깁니다. 설치 폴더에 기존 `evidence` 디렉터리가 있으면 이전 결과의 소유권을 가정하지 않고 시작을 거절합니다. 실제 GUI 요청과 외부 프로세스 관찰은 별도 단계로 검사할 수 있습니다. 사용자 내용을 담지 않는 앱 결과 집계만으로 MCP 호출을 입증하지 않습니다.

## 외부 신뢰 해시를 사용하는 읽기 전용 재검증

저장한 결과의 재검증 절차를 정리하겠습니다. [증거 검증기](EvidenceVerifier.cs)는 원시 provider MCP 이벤트, 런타임 정보와 독립 프로세스 관측으로 판정을 다시 구성합니다. 제품 SHA-256은 검증할 증거 묶음에서 가져오지 않고 별도로 신뢰할 수 있는 빌드나 릴리스 산출물에서 구합니다. 전체 스킬 대화의 증거 묶음을 검사할 때 다음과 같이 해시를 지정합니다.

```powershell
dotnet '<probe-output>/AllSkillsProbe.dll' --verify '<evidence-directory>' --expected-product-sha256 '<trusted-product-sha256>'
```

이 모드는 저장한 증거를 읽고 새 검증 보고서를 출력하며 판정을 수락하면 종료 코드 0, 거절하면 1을 반환합니다. 제품 실행, 모델 응답 요청, 런타임 설치나 증거 파일 변경은 수행하지 않습니다. 검증기는 MCP 시작과 완료 이벤트 및 인수, 스크립트 결과, 프로세스 계보와 관측 시각, 제품 및 런타임 이미지의 경로와 해시, AppContainer 작업자를 확인한 커널 토큰 질의를 대조합니다. 기존 요약의 성공 표시, 모델의 설명이나 중계기가 반환한 `wasAppContainer`만으로 수락하지 않습니다. 독립 OS 관측이 없는 과거 증거 묶음은 스크립트 실행 검증에 실패합니다. 사후 검사로 당시 수집하지 않은 측정값을 복원하지 않습니다.

`--expected-product-sha256`를 생략하면 묶음 내부의 일관성만 검사하며 `ProductHashExternallyAnchored=false`를 표시합니다. 신뢰 해시를 제공하면 선택한 제품의 식별 정보를 외부 기준과 연결하지만 악의적인 수집자가 만든 증거 파일을 암호학적으로 입증하지는 않습니다. 실제 작업자가 실행했더라도 관측을 누락하거나 질의에 실패하면 검증 실패로 남습니다. 공공문서, 인증서와 Sandbox의 응답 사례도 응답 범위에 한정합니다. 작성한 초안과 합성 보고서는 실제 호스트 동작을 입증하지 않습니다.

오프라인 검증기는 데이터 저장 위치를 별도로 표시합니다. 새 증거에는 `RuntimeStorageLayout=update-safe-data-root`를 기록합니다. 이전 `TableCloth\ManagedAi` 경로의 증거도 `RuntimeStorageLayout=legacy-installation-root`와 `HistoricalStorageLayout=true`로 구분하여 읽을 수 있습니다. 과거 증거의 통과 결과로 현재 저장 위치나 설치 프로그램의 데이터 보존 동작을 입증하지 않습니다. 다른 앱 디렉터리나 이름 끝부분만 비슷한 경로는 거절합니다. 라이브 fixture는 보관한 과거 seed를 실행 입력으로 수락하지 않습니다.

## 증거 파일과 검증의 적용 범위

여기까지 정리하면 도구는 실행마다 `bin/.../evidence/<UTC 시각>-<접미사>/`에 결과를 기록합니다. `probe.jsonl`은 처음과 마지막 설치 상태 및 선택한 런타임과 호스트 정보를 보존합니다. 스킬별 provider JSONL은 실제 `exec --json` 이벤트를 포함하고 `<skill-id>.processes.json`은 독립 Win32 관측을 보존합니다. 직접 실행과 대화 JSON은 결과 및 판정을 보존합니다. `calculator-cases/`는 사례별 결과, 합계와 입력 데이터셋을 보관합니다. `runtime-seed-manifest.json`은 독립 fixture가 사용할 검증한 런타임 좌표를 기록합니다. probe가 종료하면 호출용 snapshot을 제거할 수 있으므로 seed를 다시 사용할 스킬 등록 파일로 해석하지 않습니다.

독립 fixture는 지정한 증거 디렉터리에 요청과 응답 JSONL 및 요약을 기록합니다. 작업 프로세스의 실행 성공과 실제 외부 API의 데이터 성공을 구분합니다. 인증정보와 사용자 인증서 자료는 증거 파일에 포함하지 않습니다. 로그, 바이너리, 빌드 출력과 생성한 증거는 도구 소스의 추적 대상에서 제외합니다. 결과는 기록한 호스트와 입력에 적용하며 GUI, 모든 의존성, 호스트의 모든 ACL이나 Windows 아키텍처 전체를 검증하지 않습니다.
