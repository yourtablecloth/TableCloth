# 고정 스킬 평가 원본

[English](README.md)

2026년 10월 25일 기준으로 이 파일들은 `poc/appcontainer-skill-evaluation` 실험을 위한 원본을 제공합니다. 운영 식탁보 대화의 스크립트 실행 지원을 입증하지 않습니다. [ORIGINS.json](ORIGINS.json)에 기록한 공개 커밋과 원본 파일의 바이트를 일치시켜 보존했습니다.

출처, 명령 계약, 네트워크 의존성과 미검증 실행 범위를 설명합니다. 제안한 Python 직접 호출 어댑터와 원본 CLI 절차를 구분합니다.

원본 기록부터 살펴보겠습니다. 이후 명령 입력, 서비스 접근, 자격 증명, 라이선스와 결과 표기 범위를 정리하겠습니다.

## 고정 커밋의 원본 보존

BOK와 KOSIS 원본은 [NomaDamas/k-skill의 0102e5133865f00886e37ac8cf39a49e2c7a3d39](https://github.com/NomaDamas/k-skill/tree/0102e5133865f00886e37ac8cf39a49e2c7a3d39)에서 가져왔습니다. 계산기는 [877b58cf0741db21342445f2b3fcb54ad1ada85c](https://github.com/yourtablecloth/tablecloth-calculators/tree/877b58cf0741db21342445f2b3fcb54ad1ada85c)를 사용합니다. 출처 기록은 각 원본 경로, 바이트 수와 Git blob SHA-1을 포함합니다. 원본 파일 안에서 어댑터를 수정하지 않습니다.

## Python 명령의 입력 계약

NomaDamas 보조 스크립트 두 개는 Python 3.9 이상과 표준 라이브러리를 사용하며 계산기는 Python 3.11 이상을 사용합니다. 같은 폴더의 별도 모듈을 import하지 않습니다. BOK와 KOSIS는 명령행 인자를 받고 계산기는 stdin에서 JSON 객체 하나를 읽습니다. 상세 계약은 [BOK 지침](bok-ecos-stats/instruction.md), [KOSIS 지침](kosis-stats/instruction.md)과 계산기의 [연산 참고 문서](tablecloth-calculators/references/operations.md)에 보존했습니다. [영문 안내의 인자 예시](README.md#python-command-contracts)는 제안한 어댑터 입력을 나타내며 서비스 호출 성공을 뜻하지 않습니다.

## npx 로더의 어댑터 경계

원본 [BOK SKILL.md](bok-ecos-stats/SKILL.md)와 [KOSIS SKILL.md](kosis-stats/SKILL.md)는 `npx -y @nomadamas/k-skill@0 instruct ...`를 필수 단계로 안내하며 보조 스크립트도 CLI로 실행합니다. 이번 실험에서는 고정한 `instruction.md`를 읽고 독립 Python 파일을 직접 호출하는 경로를 평가할 수 있습니다. 이 방식은 원본 CLI의 실행 환경별 지침 조립을 재현하지 않으므로 변경한 실행 경로로 구분합니다. 원본 지침과 안전 규칙은 함께 유지합니다. npm의 이동 가능한 `@0` 선택자는 Git 고정 커밋과 다르며 임의 패키지 다운로드, 설치 스크립트나 갱신을 허용하는 근거로 사용하지 않습니다.

## 네트워크와 자격 증명의 평가 제한

[BOK 보조 스크립트](bok-ecos-stats/scripts/bok_ecos.py)는 `https://ecos.bok.or.kr/api/`에 직접 연결합니다. 사용자 키가 없으면 공개 `sample` 키를 사용하며 호출당 최대 10행으로 제한합니다. Dry-run 옵션은 없습니다. Sample로 전환하기 전에 사용자 키를 찾으면서 기본 `~/.config/k-skill/secrets.env` 읽기도 시도합니다. 따라서 평가에서는 전용 홈과 비운 자격 증명 환경을 사용하고 `--secrets-path`로 소유한 빈 파일을 지정합니다.

[KOSIS 보조 스크립트](kosis-stats/scripts/run_kosis_stats.py)의 일반 조회는 `https://k-skill-proxy.nomadamas.org/v1/kosis/...`에 연결하며 프록시 서버가 자체 KOSIS 키를 제공합니다. `--dry-run`은 URL과 인자만 출력하고 HTTP를 호출하지 않습니다. 프록시의 설정, 접근 가능 여부와 원본 응답은 외부 서비스에 의존합니다. 사용자 자격 증명을 요구하는 `--direct`와 `bigdata`는 평가에서 제외합니다. [계산기](tablecloth-calculators/scripts/calculate.py)는 네트워크와 API 키를 사용하지 않습니다. 원본 준비 과정에서는 실제 사용자 키를 읽거나 요구하거나 전달하지 않았습니다.

## 원본에 계속 적용하는 라이선스

BOK와 KOSIS 사본은 같은 커밋의 MIT 라이선스를 [bok-ecos-stats/LICENSE](bok-ecos-stats/LICENSE)와 [kosis-stats/LICENSE](kosis-stats/LICENSE)에 원문 그대로 보존합니다. 원래의 저작권 표기도 계속 적용합니다. 계산기는 [LICENSE-AGPL](tablecloth-calculators/LICENSE-AGPL), [LICENSE-COMMERCIAL](tablecloth-calculators/LICENSE-COMMERCIAL)과 [저작권 준수 기준](tablecloth-calculators/docs/COPYRIGHT.ko.md)을 보존합니다. 식탁보 저장소에 복사해도 원본 라이선스를 대체하지 않습니다.

## 실제 결과와 남은 평가

여기까지 정리하면 원본 준비에서 실행 없이 공개 고정 Git blob과 파일 31개의 일치를 확인했습니다. 이후 [평가](../RESULTS.ko.md)에서 격리 실행과 고정 공개 API 요청을 포함한 검사 46개가 통과했습니다. 실제 Codex, MCP와 AppContainer의 계산기 호출도 로컬 결정적 Responses 모의 서버로 확인했습니다. 실제 모델 추론과 인증은 각각 0회입니다. 설치, 모델의 스킬 발견, 격리 실행, 실제 추론과 제품 통합은 별도 결과로 기록합니다.

운영 대화의 기존 실행 정책은 유지합니다. 서비스가 막히거나 응답하지 않으면 해당 실제 호출을 실패 또는 미검증으로 기록하며 가공한 데이터를 성공 결과로 대신 제시하지 않습니다.
