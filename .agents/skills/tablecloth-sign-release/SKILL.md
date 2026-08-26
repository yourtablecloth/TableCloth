---
name: tablecloth-sign-release
description: Repackage and Authenticode-sign TableCloth and Spork x64 and arm64 CI payloads with the local SimplySign certificate, then replace and verify draft release assets. Use only after a release CI draft succeeds.
---

# TableCloth 릴리스 산출물 서명

CI가 만든 x64와 arm64 게시 산출물을 로컬 SimplySign 인증서로 전체 서명하고 GitHub Draft 자산을 교체합니다.

## 입력 검증

[`docs/RELEASING.md`](../../../docs/RELEASING.md)의 로컬 서명 절차를 읽습니다. 다음 입력을 확인합니다.

- 대상 태그와 Retail 또는 Preview 구분
- 성공한 CI 실행과 Draft Release
- `PublishPayload-x64`와 `PublishPayload-arm64`
- 태그 코어와 `Directory.Build.Props`의 일치
- Preview라면 태그에서 추출한 Preview 번호
- SimplySign 세션과 개인 키를 포함한 로컬 인증서

인증서 개인 키나 PFX를 복사하거나 저장소와 CI에 업로드하지 않습니다. 인증서 주체는 현재 로컬 인증서에서 확인하며 문서에 고정된 이름을 가정하지 않습니다.

## 안전한 작업 디렉터리

`git rev-parse --show-toplevel`로 저장소 루트를 확인합니다. 정리 대상은 해당 루트 아래의 생성물인 `publish`와 `Releases`, 그리고 작업별 임시 아티팩트 디렉터리로 제한합니다. 계산한 경로가 저장소 루트 또는 임시 디렉터리 안에 있는지 확인한 뒤 제거합니다.

두 `PublishPayload`를 내려받아 `publish` 계약에 맞게 합칩니다. TableCloth와 Spork의 x64 및 arm64 폴더가 모두 존재하지 않으면 중단합니다.

## 패키징과 서명

Retail은 `build.cmd --skip-build --sign`을 사용합니다. Preview는 `--preview --preview-number N`을 추가하며 `N`을 태그에서 추출합니다. 수동 기본값에 의존하지 않습니다.

패키징 로그에서 TableCloth와 Spork의 앱 바이너리, `Update.exe`, `Setup.exe` 서명을 확인합니다. 로컬에서 빌드할 수 없는 arm64 Native AOT 코드는 CI 페이로드를 사용하고 x64 호스트에서는 패키징과 서명만 수행합니다.

## 업로드와 검증

자산을 파일별로 `gh release upload --clobber`하여 부분 실패를 식별합니다. 각 파일을 최대 세 번 재시도하고 계속 실패하면 Draft를 유지합니다.

원격 자산 이름과 크기를 로컬 결과와 비교합니다. 모든 `.exe` 자산과 Portable ZIP 내부 앱 바이너리의 Authenticode 상태가 `Valid`인지 확인합니다. 한 항목이라도 누락되거나 서명이 유효하지 않으면 `UNSIGNED` 경고를 제거하거나 Release를 게시하지 않습니다.

완료 보고에는 사용한 CI 실행, 두 아키텍처의 산출물 수, 서명 검증 결과와 업로드 대조 결과를 포함합니다. 인증서의 민감한 정보는 출력하지 않습니다.
