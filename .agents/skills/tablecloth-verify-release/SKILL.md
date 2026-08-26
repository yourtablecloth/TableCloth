---
name: tablecloth-verify-release
description: Verify a TableCloth draft or published release across tag provenance, versions, x64 and arm64 assets, signatures, Velopack channels, release flags, and Retail follow-up automation. Use before publishing and after release.
---

# TableCloth 릴리스 검증

Draft 게시 전과 Release 게시 후에 소스, 자산, 서명, 채널과 후속 자동화를 증거로 확인합니다.

## 소스와 버전

[`docs/BRANCHING.md`](../../../docs/BRANCHING.md), [`docs/RELEASE_CHANNELS.md`](../../../docs/RELEASE_CHANNELS.md), [`docs/RELEASING.md`](../../../docs/RELEASING.md)를 기준으로 다음 항목을 확인합니다.

- Retail 태그 커밋과 `origin/main` HEAD의 일치
- Preview 태그 커밋의 `origin/develop` 포함 여부
- 태그 코어와 `Directory.Build.Props`의 일치
- Preview 태그 형식과 번호의 미재사용
- GitHub Release의 Draft와 Prerelease 플래그

## 자산과 서명

x64와 arm64에서 TableCloth 및 Spork 설치 관리자, Portable ZIP, Velopack 패키지와 채널 메타데이터, 심볼과 SBOM을 확인합니다. Retail에서는 무설치 고정 URL 자산과 Bootstrapper도 확인하고 Preview에서는 해당 별칭이 없는지 확인합니다.

Release의 모든 `.exe`를 새 임시 디렉터리에 내려받아 Authenticode 상태를 확인합니다. Portable ZIP을 풀어 내부의 `TableCloth.exe`와 `Spork.exe`도 검사합니다. 파일 이름만으로 서명 완료를 판단하지 않습니다.

Velopack 메타데이터는 다음 채널과 일치해야 합니다.

- Retail TableCloth: `x64`, `arm64`
- Retail Spork: `spork-x64`, `spork-arm64`
- Preview TableCloth: `preview-x64`, `preview-arm64`
- Preview Spork: `spork-preview-x64`, `spork-preview-arm64`

## 게시 상태와 외부 결과

릴리스 노트에 `UNSIGNED` 경고가 남아 있으면 게시 실패로 처리합니다. Preview는 Prerelease이며 `/releases/latest`, WinGet과 Discord에서 제외되어야 합니다.

Retail은 정식 Release이며 `/releases/latest`가 해당 태그를 가리켜야 합니다. WinGet 워크플로 성공 뒤 실제 `microsoft/winget-pkgs` Pull Request를 확인하고 Discord 워크플로 성공 뒤 실제 공지 결과를 확인합니다. 자동화 실행 상태와 외부 결과를 구분하여 보고합니다.

## 판정

검증 결과를 통과, 실패, 확인되지 않음으로 구분합니다. 실패 또는 확인되지 않은 필수 항목이 있으면 Draft를 유지하거나 게시된 Release의 영향 범위를 보고합니다. 확인하지 못한 항목을 성공으로 간주하지 않습니다.
