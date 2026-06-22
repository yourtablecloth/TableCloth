# winget-pkgs 자동 제출

TableCloth 의 정식 릴리스가 게시되면 [microsoft/winget-pkgs](https://github.com/microsoft/winget-pkgs/tree/master/manifests/t/TableClothProject/TableCloth) 에 `TableClothProject.TableCloth` 매니페스트 업데이트 PR 을 자동으로 제출하는 파이프라인입니다.

## 구성

| 파일 | 역할 |
| --- | --- |
| [`../../.github/workflows/winget_publish.yml`](../../.github/workflows/winget_publish.yml) | 트리거 + 런타임 준비 + 스크립트 호출 |
| [`submit-winget.cs`](submit-winget.cs) | .NET 10 file-based app ([Cadenza](https://github.com/rkttu/cadenza) SDK). 릴리스 조회 → 자산 선택 → `wingetcreate` 로 PR 제출 |
| [`../Directory.Packages.props`](../Directory.Packages.props) | `tools/` 하위 file-based app 이 repo 루트 CPM 을 상속하지 않도록 막는 walk-up 차단막 |

## 트리거

- **자동**: GitHub 릴리스가 `released`(정식, 비-draft·비-prerelease) 상태로 게시될 때.
- **수동**: Actions 탭에서 *Submit to winget-pkgs repo* → **Run workflow** → `release_tag` 에 `v1.20.0` 형식 입력.

## 동작

1. `RELEASE_TAG` 의 릴리스를 GitHub API 로 조회합니다.
2. draft 면 실패, prerelease 면 건너뜁니다.
3. 해당 winget 버전이 이미 등록돼 있으면 멱등적으로 건너뜁니다.
4. 릴리스 자산에서 `x64` / `arm64` 설치 관리자(`.exe`)를 찾습니다. (현재 매니페스트는 두 아키텍처 노드를 모두 요구하므로 둘 다 필요)
5. [`wingetcreate`](https://github.com/microsoft/winget-create) 단독 exe 를 내려받아 `update ... --submit` 으로 PR 을 제출합니다.

## 필요한 비밀값

`TABLECLOTH_GITHUB_PAT` 시크릿을 저장소에 등록해야 합니다.

- **classic** Personal Access Token 이어야 합니다. (`wingetcreate` 는 fine-grained PAT 을 지원하지 않습니다.)
- **`public_repo`** 스코프가 필요합니다. (winget-pkgs 포크 + PR)
- 선택: `delete_repo` 스코프가 있으면 제출 실패 시 `wingetcreate` 가 자신의 포크를 정리합니다.
- 저장소 기본 `GITHUB_TOKEN` 으로는 `microsoft/winget-pkgs` 에 포크/PR 을 할 수 없으므로 별도 PAT 이 반드시 필요합니다.

## 로컬에서 검증

```bash
# (1) 컴파일 + Cadenza 복원만 확인 — 자격 증명 불필요.
#     네트워크 호출 전에 태그 형식 검증에서 종료한다.
RELEASE_TAG=zzz dotnet run --file tools/winget/submit-winget.cs

# (2) 읽기 경로 전체 확인 — read 권한이 있는 *실제* PAT 필요.
#     이미 등록된 버전이라 wingetcreate 까지 가지 않고 멱등 건너뜀(::notice:: + exit 0).
#     GitHub API 읽기를 PAT 로 인증하므로, 'dummy' 같은 잘못된 토큰은 릴리스 조회에서
#     401 로 실패한다(=건너뜀 메시지에 도달하지 못함).
GITHUB_PAT=<your-real-pat> RELEASE_TAG=v1.16.3 dotnet run --file tools/winget/submit-winget.cs
```

## 왜 .NET 6 까지 설치하나요?

`wingetcreate.exe` 는 .NET 6 프레임워크 종속 앱인데, `windows-latest` 러너 이미지에서 .NET 6 런타임이 제거(2025-08)되었습니다. 워크플로는 스크립트 실행용 .NET 10 SDK 와 `wingetcreate` 실행용 .NET 6 런타임을 함께 설치합니다.
