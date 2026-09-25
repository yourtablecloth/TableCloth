# TableCloth Retail과 Preview 릴리스 채널

TableCloth는 Retail과 Preview 두 릴리스 링을 운영합니다. Retail은 기본 업데이트 채널로 최신 정식 버전을 제공하고 Preview는 사용자가 선택한 경우에만 다음 Minor 버전의 선행 빌드를 제공합니다.

이 문서는 각 채널의 버전, GitHub Release, Velopack 메타데이터와 외부 배포 계약을 정리합니다. 브랜치와 버전 관리 규칙은 [BRANCHING.md](BRANCHING.md), 게시 절차는 [RELEASING.md](RELEASING.md)에서 다룹니다.

> 기준일: 2026년 9월 26일. 현재 Retail은 v1.21.1이며 `develop`의 v1.22.0 Preview는 .NET 11 RC1 SDK를 사용합니다. v1.21.0부터 Retail과 Preview 모두 Avalonia와 Native AOT 빌드를 사용합니다.

## 두 릴리스 링의 역할

| 항목 | Retail | Preview |
| --- | --- | --- |
| 소스 | `main` | `develop` |
| 버전 | `X.Y.Z` | `X.(Y+1).0-preview.N` |
| GitHub 상태 | 정식 Release | Prerelease |
| 앱 기본값 | 기본 선택 | 사용자 옵트인 |
| WinGet | 정식 게시 후 자동 제출 | 제출하지 않음 |
| Discord | 정식 게시 후 자동 공지 | 공지하지 않음 |
| `/releases/latest` | 최신 Retail을 가리킴 | 대상에서 제외 |

GitHub는 Prerelease를 최신 정식 Release와 구분합니다. 저장소의 WinGet과 Discord 워크플로도 `release: released` 이벤트만 처리하므로 Preview 게시에서는 실행되지 않습니다.

## Velopack 채널 매핑

[Velopack 릴리스 채널](https://docs.velopack.io/packaging/channels)은 채널별 업데이트 메타데이터를 분리합니다. TableCloth는 제품과 CPU 아키텍처를 다음과 같이 매핑합니다.

| 애플리케이션 | Retail | Preview |
| --- | --- | --- |
| TableCloth x64 | `x64` | `preview-x64` |
| TableCloth arm64 | `arm64` | `preview-arm64` |
| Spork x64 | `spork-x64` | `spork-preview-x64` |
| Spork arm64 | `spork-arm64` | `spork-preview-arm64` |

앱의 `AppUpdateManager`는 사용자가 선택한 릴리스 링과 현재 아키텍처를 조합하여 명시적인 채널을 조회합니다. Retail은 GitHub `/releases/latest`를 폴백으로 사용하고 Preview는 Prerelease 목록에서 Preview 자산을 찾습니다.

## 버전과 자산 이름

Preview는 [Semantic Versioning 2.0.0](https://semver.org/)의 점으로 구분한 숫자 식별자를 사용합니다.

```text
Retail:  v1.21.1
Preview: v1.22.0-preview.1
         v1.22.0-preview.2
```

`Directory.Build.Props`에는 `1.22.0` 코어만 기록합니다. `preview.N`은 태그와 Velopack 패키지 버전에 추가합니다.

Retail 자산은 기존 고정 URL 계약을 유지합니다. Preview 자산은 이름에 `-Preview`를 넣어 정식 자산과 구분합니다.

- Retail: `TableCloth_<file-version>_Release_<arch>.exe`
- Preview: `TableCloth-Preview_<file-version>_Release_<arch>.exe`
- Retail: `Spork_<file-version>_Release_<arch>.exe`
- Preview: `Spork-Preview_<file-version>_Release_<arch>.exe`

Preview는 `SporkBootstrap_<arch>.exe`, `Spork_<arch>_Portable.zip`, `no-install-spork.wsb`와 같은 `/releases/latest/download` 고정 URL 별칭을 만들지 않습니다.

## Preview 사용자의 정식 버전 전환

Preview와 Retail은 서로 다른 Velopack 메타데이터를 사용합니다. 따라서 Preview 사용자가 같은 버전의 Retail 패키지를 자동으로 받는다고 가정하지 않습니다.

현재 정책은 Preview를 계속 사용하는 옵트인 링으로 유지합니다. Preview 사용자가 Retail로 돌아가려면 앱 설정에서 업데이트 채널을 Retail로 바꾼 뒤 정식 설치 관리자를 직접 설치합니다. 앱이 더 낮은 Retail 버전으로 자동 다운그레이드하지 않습니다. 실제 사용자 절차는 [TROUBLESHOOTING_UPDATE_CHANNEL.md](TROUBLESHOOTING_UPDATE_CHANNEL.md)에 기록합니다.

Preview 사용자를 정식 버전으로 자동 이동하는 기능을 도입하려면 정식 패키지를 Preview 채널 메타데이터에도 노출하는 승격 브리지를 별도로 구현하고 검증합니다. 해당 브리지를 구현하기 전에는 자동 승격을 릴리스 조건으로 기록하지 않습니다.

## 정식 승격과 다음 Preview

`v1.22.0-preview.N`을 검증한 뒤 `develop`을 `main`으로 병합하고 `v1.22.0`을 Retail로 게시합니다. 정식 게시 자산은 Retail 채널에만 들어갑니다.

정식 게시 후 다음 기능 개발을 시작할 때 `develop`의 버전을 `1.23.0`으로 올리고 Preview 번호를 다시 1부터 시작합니다. 이전 Preview 번호와 패키지를 새 버전 코어에서 이어서 사용하지 않습니다.

## 외부 배포 계약

Retail 정식 게시가 발생하면 다음 자동화가 이어집니다.

- [WinGet 제출 워크플로](../.github/workflows/winget_publish.yml)의 `microsoft/winget-pkgs` Pull Request 생성
- [Discord 공지 워크플로](../.github/workflows/discord_release.yml)의 정식 출시 알림
- `/releases/latest/download`를 사용하는 무설치 실행 자산 갱신
- Retail 앱의 자동 업데이트 메타데이터 갱신

Preview는 위 자동화에서 제외됩니다. Preview를 정식 Release로 잘못 게시하거나 `latest`로 지정하지 않습니다.

## Avalonia와 Native AOT 전환 기록

v1.20에서 v1.21로 이어진 WPF 제거, Avalonia 이관과 Native AOT 검증은 [AVALONIA_AOT_MIGRATION.md](AVALONIA_AOT_MIGRATION.md)에 보존합니다. v1.21.1은 같은 배포 기반에서 첫 실행 데이터 디렉터리 문제인 [#308](https://github.com/yourtablecloth/TableCloth/issues/308)을 수정한 Retail 핫픽스입니다. 현재 채널 정책은 두 링 모두 Avalonia와 Native AOT를 사용한다는 전제에서 운영합니다.
