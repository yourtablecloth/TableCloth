---
name: tablecloth-release-preview
description: Prepare and publish a TableCloth Preview from develop with a strict preview.N tag, native x64 and arm64 CI artifacts, local signing, and prerelease verification. Use for Preview releases, not Retail patches or stable promotion.
---

# TableCloth Preview 릴리스

`develop`의 다음 Minor 버전을 `vX.Y.0-preview.N` Prerelease로 게시합니다.

## 준비 상태

[`docs/BRANCHING.md`](../../../docs/BRANCHING.md), [`docs/RELEASE_CHANNELS.md`](../../../docs/RELEASE_CHANNELS.md), [`docs/RELEASING.md`](../../../docs/RELEASING.md)를 읽습니다. 다음 조건을 확인합니다.

- 대상 커밋이 `origin/develop` 이력에 포함됨
- 최신 `main` 핫픽스가 `develop`에 반영됨
- `Directory.Build.Props`가 목표 버전 코어와 일치함
- 로컬 빌드와 관련 테스트가 성공함
- 기존 Preview 태그에서 다음 번호를 계산함

태그는 `^v[0-9]+\.[0-9]+\.[0-9]+-preview\.[1-9][0-9]*$` 형식만 허용합니다. 게시하거나 삭제한 Preview 번호를 재사용하지 않습니다.

## CI Draft 생성

사용자가 Preview 릴리스를 실행하도록 요청했다면 태그를 생성하고 Push합니다. [`preview.yml`](../../../.github/workflows/preview.yml)의 x64와 arm64 Job, Draft Prerelease 생성, 두 `PublishPayload` 아티팩트를 확인합니다.

CI가 실패하면 태그를 이동하지 않습니다. 원인을 새 커밋에서 수정하고 다음 Preview 번호를 사용합니다.

## 서명과 게시

CI Draft가 성공하면 [`tablecloth-sign-release`](../tablecloth-sign-release/SKILL.md)를 읽고 Preview 모드로 수행합니다. `--preview-number`에는 태그의 `N`을 전달합니다.

서명 뒤 [`tablecloth-verify-release`](../tablecloth-verify-release/SKILL.md)를 읽고 Preview 자산, Preview 채널 메타데이터와 Prerelease 상태를 검증합니다. 검증을 통과하고 사용자가 게시까지 요청했다면 Draft를 Prerelease로 게시합니다.

Preview에는 WinGet 제출, Discord 정식 공지, 무설치 고정 URL 별칭을 만들지 않습니다. Preview를 최신 정식 Release로 지정하지 않습니다.

완료 보고에는 태그, Release URL, CI 실행, 서명 검증과 알려진 제한을 포함합니다.
