# AppContainer 스킬 실행 PoC

[English](README.md)

2026년 10월 25일 기준으로 이 브랜치에서 일반 Windows AppContainer 안의 Python과 Node.js 스크립트 실행을 평가합니다. 식탁보 제품 코드는 기존 `shell_tool=false`를 유지하며 이 중계기나 제거했던 전용 런타임 기능을 자동으로 활성화하지 않습니다.

이번 실험은 네이티브 작업 프로세스, 고정 스크립트 MCP 중계기, 고정 원본 스킬과 평가용 카탈로그를 포함합니다. 중계기 입력 검증, 실제 격리 실행, 외부 서비스 호출과 Codex 도구 연결을 구분해 기록합니다.

실행 경계를 먼저 살펴보겠습니다. 이어서 재현 명령, 스킬별 어댑터, 측정 결과와 제품 통합 범위를 정리합니다.

## 호스트 중계기와 AppContainer 작업 프로세스

신뢰하는 호스트 중계기는 등록한 스킬 ID와 JSON 입력만 MCP 도구로 받습니다. 모델의 도구 인수로 실행 파일 경로, 셸 명령이나 스크립트 코드를 받지 않습니다. 중계기는 호출 직전에 실행 파일과 스크립트의 해시를 검사합니다. Python은 `-I -S -B -X utf8`로 실행합니다. Node는 `--preserve-symlinks-main`으로 고정 `.cjs` 파일을 실행하며 reparse 경로 거부를 유지합니다. 두 경로 모두 cmd, PowerShell, pip, npm이나 npx를 호출하지 않습니다.

네이티브 실행기는 매번 고유 AppContainer 프로필을 생성합니다. 해당 SID에 준비한 런타임과 스크립트 디렉터리의 읽기 및 실행 권한과 호출별 작업, 홈, 임시 디렉터리의 쓰기 권한을 부여합니다. 프로세스를 일시 중단 상태로 생성한 뒤 `TokenIsAppContainer`를 검사하고 Job Object에 넣은 다음 실행을 재개합니다. 기본 작업 프로세스에는 네트워크 capability를 부여하지 않습니다. Microsoft의 [AppContainer 구현 안내](https://learn.microsoft.com/en-us/windows/win32/secauthz/implementing-an-appcontainer)와 [Job Object 문서](https://learn.microsoft.com/en-us/windows/win32/procthread/job-objects)에서 API 동작을 설명합니다.

다만 일반 AppContainer는 호스트 커널을 공유합니다. 기존 ACL이 `ALL APPLICATION PACKAGES`에 허용한 Windows 리소스는 읽을 수 있습니다. 가짜 비공개 파일의 접근 거부만으로 모든 호스트 경로의 차단을 입증하지는 않습니다. Job Object는 자원과 프로세스 수명을 제한하며 AppContainer 토큰과 ACL은 접근을 통제합니다. 이번 실험은 Windows Sandbox나 VM을 사용하지 않습니다. 시스템 방화벽, 전역 PATH와 전역 Codex 설정도 변경하지 않습니다.

## 전용 디렉터리에서의 준비와 평가

현재 실험은 저장소의 .NET 11 SDK와 Windows x64를 사용합니다. 평가 장비는 빌드 26300을 실행합니다. Windows 10 22H2와 ARM64의 실제 동작 검증은 남아 있습니다. API 지원 버전만으로 해당 운영체제에서 런타임과 제품까지 동작한다고 판정하지 않습니다. [CreateAppContainerProfile 지원 조건](https://learn.microsoft.com/en-us/windows/win32/api/userenv/nf-userenv-createappcontainerprofile)을 참고할 수 있습니다.

다음 명령은 호스트를 빌드하고 검증한 런타임을 준비한 뒤 격리한 작업 프로세스를 실행합니다. `prepare`는 고정 ZIP이 없으면 저장소의 아티팩트 캐시에 내려받고 해시를 검사합니다. 쓰기 범위는 `Artifacts`로 제한합니다. 선택한 공개 API 평가 명령은 로컬 검사도 다시 실행합니다.

```powershell
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
dotnet build tools/appcontainer-skill-poc/Poc.csproj -c Release
$poc = 'tools/appcontainer-skill-poc/bin/Release/net11.0-windows10.0.18362.0/TableCloth.AppContainerPoc.dll'
dotnet $poc prepare $PWD.Path
$ownedRoot = Join-Path $PWD.Path 'Artifacts/appcontainer-skill-poc'
dotnet $poc selftest $ownedRoot
dotnet $poc evaluate-public-api $ownedRoot
dotnet $poc codex-probe $ownedRoot
```

준비 단계는 [Python 3.14.9 embeddable x64](https://www.python.org/downloads/release/python-3149/)와 [Node.js 24.22.1 Windows x64](https://nodejs.org/dist/v24.22.1/)를 고정합니다. SHA-256 값은 [영문 안내](README.md)에 기록했습니다. 자동 업그레이드는 수행하지 않으며 공식 ZIP의 라이선스 고지도 보존합니다.

## 스킬 3개의 재활성화와 별도 어댑터

평가용 카탈로그에서 `tablecloth-calculators`, `bok-ecos-stats`와 `kosis-stats`를 다시 활성화했습니다. 기존 지침형 스킬 3개도 유지했습니다. 부모 카탈로그 PR과 운영 카탈로그는 이 실험과 분리합니다. [skills/ORIGINS.json](skills/ORIGINS.json)에 원본 커밋과 파일 목록을 기록했습니다. [원본 평가 안내](skills/README.ko.md)는 명령, 인증정보와 라이선스 조건을 설명합니다.

계산기는 고정 원본 Python 스크립트를 JSON 표준 입력으로 호출합니다. 중계기는 비율에 해당하는 값, 단일 할인, 대출 상환, 날짜 덧셈, 고정 UTC 오프셋 변환과 BMI 숫자 계산을 허용합니다. 세율, 의료 판정이나 실시간 금융 데이터는 제공하지 않습니다.

BOK와 KOSIS의 원본 지침은 npm 기반 지침 로더를 사용합니다. 이번 실험은 고정 원본의 독립 Python 조회 도우미를 별도 어댑터로 호출합니다. 원래의 `npx @nomadamas/k-skill@0` 경로는 재현하거나 지원 판정하지 않습니다. 별도 Node 예제는 Node 격리 실행을 검증하며 마켓플레이스 스킬로 등록하지 않습니다.

중계기는 `requiresNetwork` 등록을 모두 차단합니다. KOSIS 중계기 등록은 `--dry-run`을 사용합니다. 별도 공개 API 평가만 호스트가 고정한 BOK 공개 sample 요청 1건과 KOSIS 공용 프록시 요청 1건에 `internetClient`를 부여합니다. 이 권한은 도메인 허용 목록보다 넓으므로 마켓플레이스 도구에 네트워크를 허용했다는 뜻으로 해석하지 않습니다. 사용자 API 키, 인증 파일과 호스트의 인증정보 환경 변수는 작업 프로세스에 전달하지 않습니다.

## 실제 실행과 입력 검사 증거

검사기는 `Artifacts/appcontainer-skill-poc/evaluation.json`에 결과를 기록합니다. 공개 API 평가를 실행하면 `evaluation-public-api.json`도 생성합니다. 각 기록은 검사명, 성공 여부와 크기를 제한한 실행 증거를 포함합니다. 가짜 실행기를 주입하는 중계기 검사는 `broker-offline` 접두사로 구분하며 입력 검증 동작만 입증합니다.

실제 네이티브 검사는 계산 결과, Node JSON 실행, KOSIS dry-run, AppContainer 토큰, 비공개 가짜 파일의 읽기 및 쓰기 거부, 작업 파일 쓰기, 네트워크 접근 거부, 자식 프로세스 거부와 인증정보 환경 변수 누락을 다룹니다. 메모리, 출력 및 시간 제한, 취소, ACL 복원과 실제 표준 입출력 MCP 연결도 검사합니다. 네트워크 시간 초과만으로는 차단 성공을 판정하지 않습니다.

최종 검사 46개가 모두 통과했습니다. 이 중 20개는 가짜 실행기를 사용하는 중계기 검사로 구분합니다. 원본 계산기 테스트 28개와 BOK 테스트 19개도 AppContainer 안에서 통과했습니다. 기존 Managed AI 회귀 검사는 197개를 통과했습니다. 각 수치는 검증 단계를 나타내며 네이티브 보안 검사 수로 합산하지 않습니다.

`codex-probe`는 셸 도구를 끈 Codex 0.156.1에서 MCP, AppContainer와 고정 계산기를 거치는 실제 연결에 성공했습니다. 로컬 결정적 Responses 모의 서버가 도구 호출을 전달하며 실제 모델 추론과 인증은 수행하지 않습니다. 새 프로필은 등록한 계산기 도구 하나만 명시적으로 승인합니다. 서버의 기본 승인은 `prompt`로 두며 전역 승인 정책 `never`와 제품 프로필을 유지합니다.

[평가 보고서](RESULTS.ko.md)에 최종 측정값과 실패를 기록합니다. 원본 코드와 출처는 [원본 평가 안내](skills/README.ko.md)에서 추적할 수 있습니다. 실제 서비스 호출이 실패하면 가짜 응답을 성공 결과로 대체하지 않습니다.

## 제품 연결과 후속 검증 범위

현재 PoC는 식탁보 채팅 창이나 설치기에 연결하지 않습니다. 로컬 실행 등록 파일은 호스트가 신뢰하는 설정으로 취급하며 원격 카탈로그 XML만으로 실행 스크립트를 등록하지 않습니다. 제품 통합 범위에는 실행 등록 규격, 의존 파일 무결성, 업데이트 및 제거 정책, 비정상 종료 복구, 중계기 배포, 사용자 실행 동의와 대상 운영체제 검증이 남아 있습니다.

PoC는 권한 변경 전에 원래 DACL과 무결성 레이블을 기록합니다. 소유 파일의 배타적 잠금으로 여러 프로세스의 ACL 변경을 직렬화하며 복구 기록이 남아 있으면 실행을 차단합니다. `recover-acl <owned-root>`는 소유 파일의 식별자를 검사하고 권한을 복원한 뒤 기록을 지웁니다. 진단 과정에서 복구를 실제로 실행했으며 강제 전원 차단 후 복구는 검증하지 않았습니다. Windows가 Low 레이블 제거 후 빈 SACL 제어 플래그를 남길 수 있어 DACL과 실제 무결성 레이블 ACE를 비교합니다.

Codex의 [MCP 설정](https://learn.chatgpt.com/docs/extend/mcp)은 도구 전송 경로를 제공합니다. MCP 자체는 코드를 격리하지 않습니다. 셸 도구를 끈 상태에서 고정 CLI가 등록 도구를 호출하는지와 실제 ChatGPT 추론, 인증 및 제품의 엄격한 프로필 정책을 구분해 기록합니다.

여기까지 정리하면 이번 AppContainer PoC는 등록한 스크립트에 가벼운 프로세스 격리 경로를 적용합니다. 실제 로컬 실행 결과로 측정한 범위 안에서 구현 가능성을 평가합니다.

운영 마켓플레이스의 등재 기준은 기존 브랜치 정책을 따릅니다. 네트워크를 사용하는 스킬과 일반 npm 실행 경로는 더 넓은 접근 범위를 다루는 정책과 검증을 후속 작업으로 남깁니다.
