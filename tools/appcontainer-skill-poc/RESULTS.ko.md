# AppContainer 스킬 실행 평가 결과

[English](RESULTS.md)

2026년 10월 25일에 Windows x64 환경에서 검사 46개와 실제 Codex 0.156.1의 MCP 연결 시험이 모두 통과했습니다. 호스트는 Windows 빌드 26300을 보고했습니다. 식탁보 제품의 채팅 경로와 셸 실행 정책은 유지했습니다.

이번 결과는 Python과 Node 격리 실행, 고정 스킬 어댑터, 공개 API 요청, 접근 및 자원 제한과 로컬 결정적 응답을 사용한 도구 연결을 다룹니다. 실제 모델 추론과 제품 통합은 검증하지 않았습니다.

측정 환경부터 검증 단계, 호환성 수정, 스킬별 결과와 후속 검증 범위를 살펴보겠습니다. [evaluation-summary.json](evaluation-summary.json)에 정제한 증거와 로컬 원본 보고서의 해시를 보존했습니다.

## 고정 환경과 결과 추적 경로

Python 3.14.9 embeddable x64, Node.js 24.22.1 x64, Codex 0.156.1과 SDK 11.0.100-rc.1.26425.128을 사용했습니다. [PoC 안내](README.ko.md)에 런타임 ZIP 해시와 재현 명령을 기록했으며 [ORIGINS.json](skills/ORIGINS.json)에 원본 파일과 라이선스를 연결했습니다.

로컬 원본 증거는 `Artifacts/appcontainer-skill-poc/evaluation-public-api.json`, `codex-probe-evidence.json`과 `test-results/appcontainer-poc-managed-ai.trx`에 보존했습니다. 커밋한 요약에서는 장비별 경로, 프로필 SID, 인증정보와 서비스 응답 전체를 제외했습니다. 빌드 26300의 API 실행 성공만으로 Windows 10 22H2나 ARM64의 지원을 판정하지 않습니다. Microsoft의 [프로필 API 지원 조건](https://learn.microsoft.com/en-us/windows/win32/api/userenv/nf-userenv-createappcontainerprofile)은 API 가용성을 설명합니다.

## 검증 단계별 통과 범위

각 검사 수치는 원래 범위를 유지합니다. 검사기 합계에는 원본 단위 테스트 실행 두 번을 각각 한 항목으로 포함하며 내부 단위 테스트 수를 다시 더하지 않습니다.

| 검증 단계 | 결과 | 확인한 범위 |
| --- | --- | --- |
| 중계기 오프라인 검사 | 20/20 | 가짜 실행기로 입력, 경로, 해시와 프로토콜 검사 |
| 나머지 검사기 항목 | 26/26 | 실제 작업 프로세스, 정책 거부, 대조 검사, 공개 요청과 정리 |
| 격리한 계산기 원본 테스트 | 28/28 | 원본 산술 및 입력 검증 |
| 격리한 BOK 원본 테스트 | 19/19 | 실제 요청을 제외한 원본 도우미 단위 검사 |
| 기존 Managed AI 회귀 검사 | 197/197 | 카탈로그 파서, 설치와 기존 백엔드 동작 |
| 실제 CLI 연결 시험 | 통과 | 로컬 응답 모의 서버를 사용한 Codex, MCP와 AppContainer 연결 |

Python과 Node 모두 비공개 가짜 파일의 읽기 및 쓰기를 거부하면서 작업 디렉터리 쓰기를 허용했습니다. 호스트에 주입한 가짜 인증정보 환경 변수도 상속하지 않았습니다. 네트워크 권한이 없는 Python은 Winsock 10013을, Node는 `EACCES`를 반환했습니다. 자식 프로세스 생성은 Python에서 Win32 1816으로 실패했습니다. Node는 프로세스 한도 1개에서 `UNKNOWN/-4094`를 반환했으며 한도를 2개로 바꾼 대조 검사에서는 같은 고정 자식 프로세스를 실행했습니다. 중계기의 한도는 1개를 유지합니다.

시간 초과, 취소, 출력 크기 제한과 메모리 64 MiB 제한 검사도 통과했습니다. DACL과 실제 무결성 레이블 ACE가 실행 전후 일치했으며 최종 네이티브 호출별 작업 디렉터리와 ACL 복구 기록이 남지 않았습니다. 이번 검사는 구현한 제한 동작을 다루며 일반적인 샌드박스 탈출 감사를 대신하지 않습니다. Microsoft의 [AppContainer 구현 안내](https://learn.microsoft.com/en-us/windows/win32/secauthz/implementing-an-appcontainer)에서 접근 통제 구조를 설명합니다.

## 최종 실행에서 확인한 호환성 수정

실행기는 변경한 DACL과 무결성 레이블을 분리해 복원합니다. 원래 레이블이 없으면 non-null 빈 ACL로 임시 Low 레이블을 제거합니다. Windows가 빈 SACL 제어 플래그를 남길 수 있어 DACL과 실제 무결성 ACE의 바이트를 비교합니다. 소유 파일의 배타적 잠금으로 여러 프로세스의 변경을 직렬화하고 복구 기록이 남으면 새 실행을 차단합니다. 복구 기록 재적용은 실제로 확인했으며 강제 종료와 전원 차단 후 복구는 검증하지 않았습니다.

Node는 `--preserve-symlinks-main`으로 고정 시작 파일을 엽니다. 경로 정규화 과정의 드라이브 루트 접근을 피하면서 reparse와 하드 링크 거부를 유지합니다. 추가 외부 모듈과 npm 로더는 검증 범위에 포함하지 않았습니다. [Node CLI 옵션](https://nodejs.org/docs/latest-v24.x/api/cli.html#--preserve-symlinks-main)에서 동작을 확인할 수 있습니다.

Python은 `-I -S -B -X utf8`로 실행합니다. `-I`가 `PYTHONUTF8` 환경 변수를 무시하므로 고정 인수로 UTF-8 모드를 지정해 CP949 출력을 UTF-8로 해석하는 문제를 해결했습니다. 한글 표준 입출력과 최종 BOK 및 KOSIS 데이터 검사가 통과했습니다. [Python 명령행 문서](https://docs.python.org/3.14/using/cmdline.html)에서 격리와 UTF-8 옵션을 설명합니다.

## 스킬 3개의 실행 결과와 Codex 도구 연결

`tablecloth-calculators`는 중계기를 통해 고정 원본 스크립트를 실행했습니다. 할인, 퍼센트, 윤일 덧셈, UTC 오프셋 변환의 날짜 변경, BMI 숫자 계산과 무이자 대출 상환 사례를 통과했습니다. BOK는 원본 Python 도우미로 공개 sample 데이터 한 행을 반환했습니다. KOSIS도 공용 프록시에서 인구 검색 결과 한 행을 반환했습니다. 두 스킬의 원본 npm 지침 로더는 실행하지 않았습니다. [원본 평가 안내](skills/README.ko.md)에서 별도 Python 어댑터를 구분합니다.

중계기는 네트워크가 필요한 등록을 차단합니다. BOK와 KOSIS의 실제 요청만 호스트가 고정한 평가 경로에서 `internetClient`를 사용합니다. 이 권한은 도메인 허용 목록보다 넓으므로 임의의 네트워크 스킬을 운영 등재할 근거로 사용하지 않습니다. Codex 계산기 연결 시험의 작업 프로세스는 네트워크를 허용하지 않았습니다.

연결 시험은 새 `CODEX_HOME`과 로컬 Responses 모의 서버를 사용했습니다. Codex는 계산기 도구를 노출하고 namespace를 포함한 호출을 받아 실제 중계기를 실행했습니다. 반환 결과의 `wasAppContainer=true`, `status=completed`와 최종 계산값 `80.00`을 확인했습니다. 셸 도구는 노출하지 않았습니다. 계산기 MCP 도구 하나만 `approve`로 지정했으며 전역 승인 정책은 `never`, 서버 기본 승인은 `prompt`를 유지했습니다. 인증 파일을 복사하거나 생성하지 않았으며 모의 서버에 인증 헤더를 보내지 않았습니다. 실제 모델 추론은 0회입니다. [공식 MCP 설정 안내](https://learn.chatgpt.com/docs/extend/mcp)에서 도구별 설정을 설명합니다.

## 평가용 카탈로그와 제품 검증 경계

카탈로그 평가 분기는 스크립트 스킬 3개를 재활성화하고 기존 3개를 유지해 활성 6개와 주석 9개를 포함합니다. [카탈로그 평가 보고서](../../external/TableClothCatalog/docs/APP_CONTAINER_POC.ko.md)에 평가 예외를 기록했습니다. API 키가 필요한 후보와 연결하지 않은 MCP 후보는 계속 보류합니다. 이번 초안 실험은 부모 PR이나 운영 등재 정책을 변경하지 않습니다.

제품 통합 범위에는 실행 등록 규격, 의존 파일 전체의 무결성, 중계기와 런타임 배포 및 관리, 동의와 업데이트 동작, 제품 프로필 연결, 도메인 제한 네트워크, 비정상 종료 복구와 Windows 10 및 ARM64 검증이 남아 있습니다. 일반 AppContainer는 기존 ACL이 `ALL APPLICATION PACKAGES`에 허용한 리소스에 접근할 수 있으며 VM 수준의 분리를 주장하지 않습니다.

여기까지 정리하면 이번 구현은 범용 셸 도구를 끈 상태에서 고정 Python과 Node 스크립트를 가벼운 프로세스 격리 경로로 실행했습니다.

계산기는 실제 도구 연결까지 검증했습니다. 두 통계 도우미는 고정 공개 서비스 접근도 확인했습니다. 제품 채택과 운영 등재는 이번 로컬 PoC 결과와 구분해 판단할 수 있습니다.
