# WinGet 자동 제출과 인증 검증

TableCloth 정식 릴리스를 게시하면 `TableClothProject.TableCloth`의 매니페스트 업데이트 PR을 [microsoft/winget-pkgs](https://github.com/microsoft/winget-pkgs/tree/master/manifests/t/TableClothProject/TableCloth)에 제출합니다. 이 문서는 제출 자격 증명, 중복 제출 방지, 변경 없는 검증과 재시도 절차를 설명합니다.

## 제출 워크플로와 실행 환경

실행 구성과 각 파일의 역할부터 정리하겠습니다.

| 파일 | 역할 |
| --- | --- |
| [winget_publish.yml](../../.github/workflows/winget_publish.yml) | 릴리스 및 수동 실행, 오프라인 회귀 검사, 태그별 실행 직렬화 |
| [submit-winget.cs](submit-winget.cs) | .NET 11 file-based app과 Cadenza 1.0.15로 인증, 매니페스트 검증과 제출 수행 |
| [Directory.Packages.props](../Directory.Packages.props) | 도구가 제품의 중앙 패키지 버전 관리를 상속하지 않도록 차단 |

워크플로는 `11.0.100-rc.1.26425.128` SDK와 `wingetcreate`용 .NET 9 런타임을 설치합니다. Microsoft가 배포한 `wingetcreate 1.12.13.0` 단독 실행 파일은 [net9.0을 대상으로 하며](https://github.com/microsoft/winget-create/blob/v1.12.13.0/src/WingetCreateCLI/WingetCreateCLI.csproj) [framework-dependent 방식으로 게시합니다](https://github.com/microsoft/winget-create/blob/v1.12.13.0/pipelines/azure-pipelines.release.yml). 기존 .NET 6 런타임 설명은 현재 도구에 적용하지 않습니다.

## 릴리스 읽기와 외부 제출의 인증 분리

인증 경로에서는 저장소 릴리스 읽기와 외부 PR 제출을 구분하겠습니다.

- `GITHUB_TOKEN`: 현재 저장소의 릴리스 조회에 사용하는 `contents: read` 권한의 Actions 토큰
- `TABLECLOTH_GITHUB_PAT`: 외부 포크와 PR 제출에 사용하는 저장소 Actions Secret, 스크립트에 `GITHUB_PAT`으로 전달
- `WINGET_CREATE_GITHUB_TOKEN`: 스크립트가 `wingetcreate` 자식 프로세스에 제출 자격 증명을 전달하는 환경 변수

Microsoft는 WinGet 제출용 classic PAT에 `public_repo` 권한을 안내합니다. `repo` 권한도 제출을 허용하지만 전용 자격 증명을 만들 때는 공개 저장소 권한으로 범위를 제한할 수 있습니다. `wingetcreate`는 fine-grained PAT을 지원하지 않습니다. CI에서는 명령줄의 `--token` 대신 환경 변수를 사용합니다. [Microsoft 토큰 문서](https://github.com/microsoft/winget-create/blob/main/doc/token.md)

스크립트는 `/user`로 제출 인증을 먼저 검증합니다. `X-OAuth-Scopes` 헤더가 있으면 `public_repo` 또는 `repo` 권한도 확인합니다. 헤더가 없으면 scope 미확인 사실을 출력합니다. HTTP 401에서는 자격 증명의 만료, 폐기 또는 등록값을 구분할 수 있도록 진단합니다. HTTP 403에서는 권한, 조직 정책 또는 API 사용 한도를 확인할 수 있도록 안내합니다. 이미 제출한 버전이라도 이 검사를 생략하지 않으므로 정상적인 릴리스 읽기가 잘못된 제출 인증을 가리지 않습니다.

로컬 실행에서 `GITHUB_TOKEN`을 생략하면 동일한 제출 자격 증명으로 릴리스를 읽습니다. 저장소 Secret은 GitHub의 저장소 설정에서 갱신할 수 있습니다. 토큰 값은 파일, 명령줄 인자 또는 로그에 남기지 않고 해당 프로세스의 환경 변수로만 전달합니다. 기본 Actions 토큰은 워크플로 저장소로 권한을 제한하므로 외부 PR 제출용 자격 증명을 대체하지 않습니다. [GitHub 인증 범위 문서](https://docs.github.com/en/actions/concepts/security/github_token)

## 중복 PR과 Preview 제출의 차단

제출 여부는 다음 순서로 판단합니다.

1. 제출 사용자 인증과 제공된 scope를 검증합니다.
2. `vX.Y.Z` 태그의 공개 정식 릴리스와 고유한 x64 및 ARM64 설치 관리자 URL을 확인합니다.
3. upstream에 같은 버전이 있으면 불변 blob에서 패키지 ID, 버전, 두 설치 관리자 URL과 제공된 SHA-256 digest를 검증한 뒤 해당 링크를 출력하고 종료합니다.
4. 기존 Open PR을 검색하고 제출 사용자의 Open PR 목록도 REST API로 별도 조회합니다. 동일 패키지와 정확히 같은 버전의 후보는 파일과 설치 관리자 정보를 검증하고 head SHA를 다시 확인한 뒤 해당 PR 링크를 출력하고 종료합니다.
5. 기존 제출이 없고 `verify_only=false`이면 포크를 동기화하고 Microsoft 공식 `wingetcreate`로 PR을 제출합니다.

Search API의 인덱싱 지연은 제출 사용자별 REST 목록으로 보완합니다. 스크립트는 REST 목록을 마지막 페이지까지 조회하며 10페이지를 넘으면 중단합니다. 검색 오류, 불완전한 응답, 파일 불일치 또는 검증 중 head SHA 변경을 발견하면 중복 제출을 방지하기 위해 실패로 종료합니다. [GitHub 저장소 이슈 목록 API](https://docs.github.com/en/rest/issues/issues#list-repository-issues)

같은 태그의 실행은 `concurrency`로 직렬화하며 진행 중인 실행을 취소하지 않습니다. Preview 태그는 워크플로와 스크립트에서 제외합니다. 제품 릴리스, 태그 또는 이미 제출한 PR은 재시도 과정에서 수정하지 않습니다. [워크플로 설정](../../.github/workflows/winget_publish.yml)

## 변경 없는 검증과 재시도

수동 실행에서는 Actions의 **Submit to winget-pkgs repo**에서 `release_tag`를 지정합니다. `verify_only=true`이면 인증, 릴리스, 두 자산과 중복 제출 여부를 읽기만 하며 포크 동기화와 PR 제출을 수행하지 않습니다. 기본값 `false`는 검증 후 실제 제출을 허용합니다. 이미 유효한 PR을 확인한 경우에는 두 모드 모두 제출을 건너뜁니다. [워크플로 입력](../../.github/workflows/winget_publish.yml)

오프라인 회귀 검사는 실제 자격 증명을 읽거나 GitHub에 접속하지 않고 가짜 HTTP 응답으로 인증 실패, scope, 검색 실패, 검색 지연, 기존 PR 검증과 버전 경계를 검사합니다. 다음 명령으로 실행합니다.

```powershell
dotnet run --file tools/winget/submit-winget.cs -- --self-test
```

이 검사는 최초 실행 시 Cadenza SDK 복원에 네트워크를 사용할 수 있습니다. 검사 코드 자체는 인메모리 HTTP 처리기만 사용합니다. PR 워크플로도 같은 검사를 실행하며 제출 job과 Secret에 접근하지 않습니다.

읽기 검증의 성공은 현재 인증과 중복 방지 경로의 동작을 확인합니다. 새 버전의 PR 생성, WinGet 커뮤니티 승인 또는 클라이언트 카탈로그 등재까지 입증하지는 않습니다. 실제 제출 후에는 반환한 PR의 파일과 설치 관리자 URL을 별도로 확인할 수 있습니다. [WinGet 매니페스트 제출 안내](https://github.com/microsoft/winget-pkgs/blob/master/doc/Authoring.md)
