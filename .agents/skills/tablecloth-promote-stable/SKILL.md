---
name: tablecloth-promote-stable
description: Promote a tested TableCloth develop line to a stable X.Y.0 Retail release on main, including freeze, synchronization, merge, tagging, signing, publishing, and transition checks. Use when ending a Preview cycle.
---

# TableCloth 정식 버전 승격

검증한 Preview 개발선을 `main`에 병합하고 `vX.Y.0` Retail로 게시합니다.

## 승격 준비

[`docs/BRANCHING.md`](../../../docs/BRANCHING.md), [`docs/RELEASE_CHANNELS.md`](../../../docs/RELEASE_CHANNELS.md), [`docs/RELEASING.md`](../../../docs/RELEASING.md)를 읽습니다. 목표 버전, 마지막 Preview, 미해결 차단 이슈와 릴리스 노트 범위를 확인합니다.

새 기능 병합을 중지하고 최신 `main` 핫픽스를 `develop`에 반영합니다. `Directory.Build.Props`가 목표 `X.Y.0.0`을 유지하는지 확인합니다.

## 최종 검증과 병합

x64와 arm64 CI와 두 테스트 프로젝트를 완료합니다. TableCloth 본체와 그 밖의 호스트 시나리오는 호스트 Windows에서 스모크 테스트하고, Spork 게스트 시나리오만 Windows Sandbox 안에서 확인합니다. TableCloth와 Spork의 주요 실행 경로 및 업데이트 채널 동작을 확인합니다.

검증을 통과하면 Pull Request로 `develop`을 `main`에 병합합니다. 정식 태그는 병합된 `origin/main` HEAD와 정확히 일치해야 합니다. Preview 태그가 가리키던 병합 전 커밋이나 로컬 전용 커밋에 태그하지 않습니다.

## Retail 게시

Retail 태그 Push 뒤 [`build.yml`](../../../.github/workflows/build.yml)의 Draft와 두 아키텍처 산출물을 확인합니다. [`tablecloth-sign-release`](../tablecloth-sign-release/SKILL.md)를 Retail 모드로 수행하고 [`tablecloth-verify-release`](../tablecloth-verify-release/SKILL.md)로 게시 전후 결과를 확인합니다.

릴리스 노트에는 Preview 기간의 주요 기능, 호환성 영향, 알려진 문제와 마이그레이션 사항을 사용자 관점에서 정리합니다. Avalonia와 Native AOT처럼 이전 버전 대비 기반 기술이 바뀌었다면 실행 성능과 이후 플랫폼 확장에 미치는 범위를 함께 설명합니다.

## 승격 이후 상태

Preview 사용자가 Retail로 자동 전환된다고 가정하지 않습니다. 현재 수동 채널 전환 정책을 릴리스 노트와 지원 문서에 반영합니다.

정식 게시와 후속 자동화를 확인한 뒤 다음 Minor 버전 개발 요청이 있으면 [`tablecloth-start-next-version`](../tablecloth-start-next-version/SKILL.md)을 수행합니다. `develop` 삭제나 재생성은 미병합 이력을 확인한 뒤 결정합니다.
