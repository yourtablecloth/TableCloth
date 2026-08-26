---
name: tablecloth-release
description: Orchestrate the TableCloth branch, version, Preview, hotfix, signing, stable promotion, and post-release lifecycle. Use for end-to-end release management requests; use a narrower TableCloth child skill when the request covers only one phase.
---

# TableCloth 릴리스 오케스트레이션

TableCloth의 다음 버전 개발 시작부터 정식 게시까지 단계별 상태를 관리합니다. 이 스킬은 작업을 직접 포괄하기보다 필요한 하위 스킬을 선택하고 완료 조건을 연결합니다.

## 기준 문서

작업을 시작할 때 다음 문서를 읽습니다.

- [`docs/BRANCHING.md`](../../../docs/BRANCHING.md): 브랜치 역할과 버전 증가 기준
- [`docs/RELEASE_CHANNELS.md`](../../../docs/RELEASE_CHANNELS.md): Retail과 Preview 채널 계약
- [`docs/RELEASING.md`](../../../docs/RELEASING.md): CI Draft, SimplySign 서명과 게시 절차

문서와 실제 워크플로가 다르면 `.github/workflows`, `build.cs`, `Directory.Build.Props`의 현재 동작을 확인하고 차이를 보고합니다. 확인하지 않은 문서 설명을 현재 구현으로 단정하지 않습니다.

## 상태 확인

다음 항목으로 현재 릴리스 단계를 판별합니다.

- 현재 브랜치와 작업 트리
- `origin/main`과 `origin/develop`의 존재 및 선후 관계
- 최신 정식 태그와 Preview 태그
- `Directory.Build.Props`의 버전 코어
- GitHub Release의 Draft, Prerelease와 게시 상태
- 관련 GitHub Actions 실행 결과

검토나 계획만 요청받았다면 외부 상태를 변경하지 않습니다. 릴리스 실행을 요청받았다면 태그 Push, Draft 생성, 서명 자산 업로드와 게시를 요청 범위에 맞추어 이어갑니다.

## 하위 스킬 선택

선택한 하위 스킬의 `SKILL.md`를 작업 전에 모두 읽습니다.

| 요청 | 하위 스킬 |
| --- | --- |
| 다음 Minor 버전 개발 시작 | [`tablecloth-start-next-version`](../tablecloth-start-next-version/SKILL.md) |
| Preview 생성과 게시 | [`tablecloth-release-preview`](../tablecloth-release-preview/SKILL.md) |
| 현재 Retail 긴급 패치 | [`tablecloth-release-hotfix`](../tablecloth-release-hotfix/SKILL.md) |
| Preview의 정식 승격 | [`tablecloth-promote-stable`](../tablecloth-promote-stable/SKILL.md) |
| CI 산출물의 로컬 서명 | [`tablecloth-sign-release`](../tablecloth-sign-release/SKILL.md) |
| 게시 전후 검증 | [`tablecloth-verify-release`](../tablecloth-verify-release/SKILL.md) |

Preview 릴리스는 Preview 스킬, 서명 스킬, 검증 스킬 순서로 진행합니다. 핫픽스와 정식 승격도 각 준비 스킬 뒤에 서명과 검증 스킬을 연결합니다.

## 공통 불변 조건

- Retail 태그는 `origin/main` HEAD와 정확히 일치합니다.
- Preview 태그는 `origin/develop` 이력에 포함됩니다.
- Preview 태그는 `vX.Y.Z-preview.N` 형식을 사용합니다.
- `Directory.Build.Props`의 코어 버전과 태그 코어를 일치시킵니다.
- x64와 arm64 산출물을 모두 확보하고 모든 서명 검증을 통과한 뒤 게시합니다.
- Retail 핫픽스를 `develop`으로 전파하면서 다음 Minor 버전을 유지합니다.
- Preview 게시에서는 WinGet과 Discord 자동화를 실행하지 않습니다.
- Retail 게시에서는 WinGet Pull Request와 Discord 공지의 실제 결과를 각각 확인합니다.

## 중단 조건

서명 실패, 아키텍처 누락, 태그와 브랜치 불일치, 버전 불일치, 부분 업로드가 발견되면 Draft를 유지하고 원인을 보고합니다. 이미 외부에 Push한 태그를 다른 커밋으로 이동하지 않습니다. 게시 권한이 요청에 포함되지 않았다면 서명과 검증 결과까지만 제공합니다.
