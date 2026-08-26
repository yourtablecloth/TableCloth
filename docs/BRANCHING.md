# TableCloth 브랜치와 버전 관리 정책

TableCloth는 2026년 8월 23일에 게시한 v1.21.0부터 정식 버전과 다음 Minor 버전을 분리하여 운영합니다. 현재 정식 버전의 호환성 패치는 `main`에서 `X.Y.Z`로 게시하고 다음 기능 개발을 시작하면 `develop`에서 `X.(Y+1).0-preview.N`으로 검증합니다.

이 문서는 브랜치의 역할, 버전 증가 기준, 핫픽스 전파 방식과 정식 승격 조건을 다룹니다. 채널별 배포 계약은 [RELEASE_CHANNELS.md](RELEASE_CHANNELS.md), 실제 게시 명령은 [RELEASING.md](RELEASING.md)에서 이어집니다.

> 기준일: 2026년 8월 26일. 현재 Retail은 v1.21.1입니다. 아직 다음 Minor 개발 주기를 시작하지 않아 원격 `develop` 브랜치는 만들지 않았습니다. 다음 버전을 시작할 때 이 문서와 `tablecloth-start-next-version` 스킬에 따라 생성합니다.

## 정식 버전과 다음 버전의 병행 개발

브랜치별 책임은 다음과 같이 고정합니다.

| 브랜치 | 책임 | 버전 예시 | 배포 대상 |
| --- | --- | --- | --- |
| `main` | 최신 정식 버전과 현재 지원 패치 | `1.21.0`, `1.21.1` | Retail |
| `develop` | 다음 Minor 버전의 기능 개발과 Preview | `1.22.0-preview.1` | Preview |
| `hotfix/X.Y.Z` | 현재 정식 버전의 긴급 수정 | `1.21.1` | 검증 후 `main`으로 병합 |
| `release/X.Y` | 이전 Minor 버전의 추가 유지보수 | `1.21.x` | 장기 지원이 필요할 때만 생성 |

`main`에는 언제든 배포할 수 있는 상태만 둡니다. `develop`은 다음 Minor 버전의 통합 지점으로 사용합니다. 기능 브랜치는 `develop`을 기준으로 만들고 검증을 마치면 `develop`으로 병합합니다.

`release/X.Y`는 `main`이 다음 Minor 버전으로 이동한 뒤에도 이전 버전을 지원할 때만 만듭니다. 최신 정식 버전만 지원한다면 장기 유지 브랜치를 만들지 않습니다.

## 버전 번호 증가 기준

[Semantic Versioning 2.0.0](https://semver.org/)을 기준으로 버전을 결정합니다.

- `PATCH`: 기존 호환성을 유지하는 결함 수정과 긴급 패치
- `MINOR`: 기존 호환성을 유지하는 기능 추가와 동작 개선
- `MAJOR`: 호환되지 않는 공개 계약 변경
- `preview.N`: 다음 정식 버전을 앞서 검증하는 순차 Preview

버전의 단일 출처는 [`Directory.Build.Props`](../Directory.Build.Props)입니다. 정식 버전과 Preview 모두 `Major.Minor.Patch`를 이 파일에 기록하고 `Revision`은 `0`으로 유지합니다. Preview 식별자는 파일에 넣지 않고 태그와 패키지 버전에만 추가합니다.

태그는 다음 형식을 사용합니다.

```text
v1.21.1
v1.22.0-preview.1
v1.22.0-preview.2
```

Preview 번호는 1부터 증가하며 이미 게시했거나 삭제한 번호를 재사용하지 않습니다. `preview.10`처럼 숫자 식별자를 점으로 구분하면 SemVer가 번호를 숫자로 비교합니다.

## 다음 Minor 버전의 시작

v1.21.0을 게시한 뒤 v1.22.0 개발을 시작하는 흐름은 다음 순서를 따릅니다.

1. `main`과 원격 태그가 최신 상태인지 확인합니다.
2. 최신 `main`에서 `develop`을 만들거나 기존 `develop`의 이력을 검토합니다.
3. `Directory.Build.Props`를 `1.22.0.0`으로 갱신합니다.
4. 전체 빌드와 단위 테스트를 실행합니다.
5. 기능 브랜치를 `develop`에서 분기합니다.
6. 검증할 시점마다 `v1.22.0-preview.N` 태그를 생성합니다.

다음 Minor 버전의 버전 변경 커밋에는 기능 변경을 섞지 않습니다. 버전 변경 이력을 분리하면 현재 정식 버전의 패치를 `develop`으로 옮길 때 충돌 범위를 줄일 수 있습니다.

## 현재 정식 버전의 핫픽스

긴급 패치는 최신 `main`에서 `hotfix/X.Y.Z`를 만들어 처리합니다. 패치 코드와 버전 변경을 별도 커밋으로 나누면 다음 버전 브랜치에 코드만 전파할 수 있습니다.

```text
main 1.21.0
  └─ hotfix/1.21.1
       ├─ 수정과 테스트
       └─ main 병합 및 v1.21.1 태그
              └─ 수정 커밋을 develop 1.22.0으로 순방향 전파
```

`main`에서 Retail 릴리스를 마친 뒤 `develop`이 존재하면 수정 커밋을 즉시 체리픽하거나 병합합니다. `develop`의 버전은 목표 Minor 버전을 유지합니다. 다음 개발 주기를 아직 시작하지 않았다면 최신 `main`에서 `develop`을 만들기 때문에 핫픽스가 새 브랜치 이력에 포함됩니다. 전파 과정에서 `Directory.Build.Props`가 이전 Retail 버전으로 되돌아가지 않는지 확인합니다.

동일한 문제를 두 브랜치에서 따로 수정하지 않습니다. Retail 패치 커밋을 다음 버전으로 전파하고 동일한 테스트로 회귀를 막습니다.

## Preview의 정식 승격

`develop`을 정식 버전으로 승격하는 흐름은 다음 순서를 따릅니다.

1. 새 기능 병합을 중지하고 승격 범위를 고정합니다.
2. 최신 `main`의 Retail 패치를 `develop`에 반영합니다.
3. 필요하면 마지막 Preview 또는 Release Candidate를 게시합니다.
4. 전체 테스트를 완료하고 TableCloth 호스트 시나리오는 호스트 Windows에서 스모크 테스트합니다. Spork 게스트 시나리오만 Windows Sandbox 안에서 확인합니다.
5. `develop`을 `main`으로 병합합니다.
6. 새 `main` HEAD에 `vX.Y.0` 정식 태그를 생성합니다.
7. CI Draft, 로컬 서명, 자산 검증과 정식 게시를 완료합니다.
8. 다음 Minor 버전 개발을 시작할 때 `develop`의 버전을 다시 올립니다.

정식 태그는 병합을 마친 `main` HEAD만 가리킵니다. Preview 태그는 `develop` 이력에 포함된 커밋만 가리킵니다.

## 보호 규칙과 검증 경계

GitHub의 [보호된 브랜치](https://docs.github.com/repositories/configuring-branches-and-merges-in-your-repository/managing-protected-branches/about-protected-branches)는 `main`과 `develop`에 다음 조건을 적용하는 데 사용할 수 있습니다.

- Pull Request를 통한 병합
- x64와 arm64 빌드 및 테스트 성공
- 병합 전 최신 대상 브랜치 반영
- 강제 Push와 태그 이동 차단

현재 릴리스 워크플로는 태그와 `Directory.Build.Props`의 버전 일치 여부를 확인하지만 태그의 브랜치 소속까지 강제하지 않습니다. 자동 검증을 추가하기 전까지 릴리스 담당자와 저장소 스킬이 다음 조건을 검사합니다.

- Retail 태그 커밋과 `origin/main` HEAD의 일치
- Preview 태그 커밋의 `origin/develop` 포함 여부
- `^v[0-9]+\.[0-9]+\.[0-9]+-preview\.[1-9][0-9]*$` 형식

## 저장소 릴리스 스킬

`.agents/skills`에는 이 정책을 실행하는 스킬을 버전 관리합니다.

- [`tablecloth-start-next-version`](../.agents/skills/tablecloth-start-next-version/SKILL.md): 다음 Minor 버전 개발 시작
- [`tablecloth-release-preview`](../.agents/skills/tablecloth-release-preview/SKILL.md): Preview 태그, Draft, 서명과 게시
- [`tablecloth-release-hotfix`](../.agents/skills/tablecloth-release-hotfix/SKILL.md): Retail 핫픽스와 `develop` 순방향 전파
- [`tablecloth-promote-stable`](../.agents/skills/tablecloth-promote-stable/SKILL.md): `develop`의 정식 승격
- [`tablecloth-sign-release`](../.agents/skills/tablecloth-sign-release/SKILL.md): SimplySign 기반 로컬 전체 서명
- [`tablecloth-verify-release`](../.agents/skills/tablecloth-verify-release/SKILL.md): 게시 전후 자산과 후속 자동화 검증
- [`tablecloth-release`](../.agents/skills/tablecloth-release/SKILL.md): 릴리스 유형 판단과 하위 스킬 조율

Claude용 `.claude/skills`는 `.agents/skills`를 가리키는 심볼릭 링크로 유지합니다. 두 도구의 스킬 내용을 따로 복제하지 않습니다.
