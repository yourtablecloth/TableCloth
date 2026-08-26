---
name: tablecloth-start-next-version
description: Start development of the next TableCloth minor version on develop, including version selection, branch preparation, version-source updates, and baseline verification. Use when beginning a new minor cycle, not for a patch or an existing Preview release.
---

# TableCloth 다음 버전 개발 시작

최신 Retail을 기준으로 다음 Minor 버전의 `develop` 브랜치와 버전 코어를 준비합니다.

## 기준 확인

[`docs/BRANCHING.md`](../../../docs/BRANCHING.md)를 읽고 다음 상태를 현재 저장소와 원격에서 확인합니다.

- 최신 게시 Retail 태그와 `origin/main` HEAD
- 기존 `origin/develop`의 존재와 미병합 커밋
- `Directory.Build.Props`의 현재 버전
- 작업 트리와 서브모듈 상태

최신 정식 버전이 `X.Y.Z`라면 기본 다음 버전은 `X.(Y+1).0`입니다. 사용자가 다른 목표 버전을 지정하면 SemVer 증가 방향과 현재 브랜치 정책의 충돌 여부를 먼저 검토합니다.

## 브랜치와 버전 준비

기존 `develop`이 없으면 최신 `origin/main`에서 만듭니다. 기존 브랜치가 있으면 덮어쓰지 않고 `main`과의 선후 관계 및 미병합 작업을 확인합니다.

`Directory.Build.Props`에서 Major, Minor, Patch와 Revision을 목표 버전에 맞춥니다. 다음 Minor 버전은 Patch와 Revision을 `0`으로 둡니다. Preview 접미사는 이 파일에 넣지 않습니다.

버전 변경은 기능 변경과 분리한 커밋으로 남깁니다. 원격 Push나 Pull Request 생성은 사용자가 개발 착수를 실행하도록 요청한 범위에서만 수행합니다.

## 기준선 검증

서브모듈을 초기화한 뒤 저장소의 전체 빌드와 두 테스트 프로젝트를 실행합니다. 기능 변경 전 실행 기준선이 필요하면 TableCloth 본체를 호스트 Windows에서 스모크 테스트합니다. Spork 게스트 시나리오를 검증할 때만 Windows Sandbox 안에서 실행합니다.

완료 보고에는 다음 내용을 포함합니다.

- 목표 버전과 브랜치
- 버전 변경 커밋
- 빌드와 테스트 결과
- 원격 Push 또는 Pull Request 상태
- 첫 Preview를 만들기 전에 남은 작업
