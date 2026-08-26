---
name: tablecloth-release-hotfix
description: Deliver an urgent TableCloth Retail patch from main and forward-port the fix to develop without changing its next-minor version. Use for backward-compatible X.Y.Z fixes, not feature releases.
---

# TableCloth Retail 핫픽스

현재 정식 버전의 호환성 문제를 `X.Y.Z` Retail 패치로 게시하고 수정 코드를 다음 Minor 버전에 전파합니다.

## 패치 범위

[`docs/BRANCHING.md`](../../../docs/BRANCHING.md)와 [`docs/RELEASING.md`](../../../docs/RELEASING.md)를 읽습니다. 최신 `origin/main`과 정식 Release를 확인하고 다음 Patch 번호를 선택합니다.

긴급 수정이 새 공개 기능이나 호환되지 않는 변경을 포함하면 핫픽스로 게시하지 않습니다. 다음 Minor 또는 Major 버전 경로로 전환합니다.

## 수정과 버전 커밋

최신 `main`에서 `hotfix/X.Y.Z`를 만듭니다. 실제 수정과 회귀 테스트를 먼저 커밋하고 `Directory.Build.Props`의 Patch 변경을 별도 커밋으로 남깁니다. Revision은 `0`을 유지합니다.

관련 단위 테스트를 실행합니다. TableCloth 본체와 그 밖의 호스트 시나리오는 호스트 Windows에서 스모크 테스트하고, Spork 게스트 시나리오만 Windows Sandbox 안에서 확인합니다. Pull Request를 통해 `main`에 병합하고 새 `origin/main` HEAD에만 Retail 태그를 생성합니다.

## Retail 게시

[`build.yml`](../../../.github/workflows/build.yml)이 생성한 Draft와 x64 및 arm64 `PublishPayload`를 확인합니다. [`tablecloth-sign-release`](../tablecloth-sign-release/SKILL.md)를 Retail 모드로 수행하고 [`tablecloth-verify-release`](../tablecloth-verify-release/SKILL.md)로 게시 전후 상태를 확인합니다.

정식 게시 뒤 WinGet Pull Request와 Discord 공지의 실제 생성 결과를 확인합니다.

## 다음 버전 순방향 전파

`develop`이 존재하면 패치 코드 커밋을 즉시 전파합니다. 버전 변경 커밋은 전파하지 않으며 `develop`의 `X.(Y+1).0`을 유지합니다. 같은 회귀 테스트를 `develop`에서도 실행합니다.

브랜치 정리는 병합, Retail 게시와 순방향 전파를 모두 확인한 뒤 수행합니다. 이전 버전 유지보수 계획이 남아 있으면 관련 브랜치를 보존합니다.
