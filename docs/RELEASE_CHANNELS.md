# Retail / Preview 릴리스 채널 분리와 승격 기록 (이슈 #296)

> 상태: **Preview 구현과 검증 완료, Retail 승격 반영** · 2026-08-23
> 배경: WPF→Avalonia+Native AOT(이슈 [#296](https://github.com/yourtablecloth/TableCloth/issues/296)) 전환은 변화 폭이 커서,
> 안정 사용자를 보호한 채 조기 검증을 받기 위해 **Retail(안정)** 과 **Preview(선행)** 두 릴리스 링을 분리했다.
> `v1.21.0-preview.1`부터 `.3`까지 별도 Preview 파이프라인으로 검증했으며, `main` 통합부터 Retail도
> Avalonia+Native AOT로 게시한다. 다음 내용은 초기 설계와 승격 결정을 함께 보존한다.
> 운영 절차: [RELEASING.md](RELEASING.md), 마이그레이션 기록: [AVALONIA_AOT_MIGRATION.md](AVALONIA_AOT_MIGRATION.md).

## 1. 초기 확정 결정 (2026-07-25)

| 항목 | 결정 | 근거 |
| ---- | ---- | ---- |
| 채널 매핑 | **Retail = 현행 WPF 유지 / Preview = AOT 신규 레인** | 기존 사용자는 안정 WPF 를 계속 받고, AOT 는 opt-in 으로 조기 검증. AOT 안정화 후 Retail 로 승격(§9). 리스크 최소·되돌리기 용이. |
| Preview 배포 | **별도 프리릴리스 설치본 + Velopack `preview` 채널** | 프리뷰 설치본을 한 번 받으면 이후 프리뷰 채널로 자동 업데이트. Retail·winget·무설치 웹앱은 GitHub 프리릴리스 특성상 자동으로 무영향. Velopack 네이티브 방식이라 가장 단순·견고. |
| 진행 방식 | **설계 문서 우선, 구현은 승인 후 단계별** | 외부 계약(winget/웹앱), CI, 버전 체계를 문서로 확정한 뒤 이관. |

## 2. 현행 구조 요약 (분리 전)

- **버전 단일 출처**: `Directory.Build.Props`(4-part 숫자). CI `validate-version` 이 태그의 3-part 와 비교.
- **릴리스**: 태그 `v*` push → `build.yml`(x64/arm64 빌드 + **미서명 draft**) → 로컬 `build.cmd --sign` → `gh release upload --clobber` → UNSIGNED 마커 제거 후 Publish → `released` → winget PR.
- **Velopack 채널**: TableCloth = `<arch>`(x64/arm64), Spork = `spork-<arch>`. **링 개념 없음**.
- **자산명**: `TableCloth_<4part>_Release_<arch>.exe` / `_Portable.zip`, Spork 동일 패턴.
- **업데이트**(`AppUpdateManager`): ① Velopack(채널 미지정=기본) → ② 실패 시 GitHub API `/releases/latest` 에서 `_Release_<arch>.exe` 매칭. `/releases/latest` 는 **프리릴리스를 제외**한 최신을 반환.
- **외부 고정 URL 계약**: winget(설치 관리자 Setup.exe), 무설치 웹앱(`releases/latest/download/{no-install-spork.wsb, SporkBootstrap_<arch>.exe, Spork_<arch>_Portable.zip}`). 모두 `/releases/latest`(=Retail) 에 의존.

> **핵심 제약(분리의 실질적 계기):** `build.yml` 은 아직 WPF 게시 플래그(`PublishSingleFile`+`PublishReadyToRun`)를 명시한다. 이는 M5 의
> `PublishAot` 과 **상호배타**다. 따라서 AOT 는 현행 Retail CI 로 내보낼 수 없고 **전용 게시 경로**가 필요하다 → Preview 레인이 이를 담당.

## 3. 채널 모델

```
Retail  (안정, 대다수)        Preview (선행, opt-in)
├─ 소스: main (WPF v1.20.x)   ├─ 소스: feature/avalonia-aot (AOT)
├─ GitHub: 정식 릴리스        ├─ GitHub: 프리릴리스(prerelease=true)
│   (/releases/latest 대상)   │   (/releases/latest 에서 제외됨)
├─ Velopack 채널: <arch>      ├─ Velopack 채널: preview-<arch>
├─ 자산: TableCloth_…_x64.exe ├─ 자산: TableCloth-Preview_…_x64.exe
├─ winget: ✅ 제출            ├─ winget: ❌ (프리릴리스라 released 미발생)
└─ 무설치 웹앱: ✅ (latest)   └─ 무설치 웹앱: ❌ (latest 아님)
```

- **Retail 은 현행과 100% 동일**하게 유지한다(채널명·자산명·CI·winget·웹앱 무변경). 기존 설치 사용자의 자동 업데이트가 끊기지 않는다.
- **Preview 는 순수 추가(additive)** 레인. Retail 을 건드리지 않는다.
- GitHub 의 **prerelease 플래그**가 분리의 중심축: 프리릴리스는 `/releases/latest` 에서 빠지므로 winget·무설치 웹앱·Retail 업데이트 폴백이 **자동으로 Preview 를 무시**한다.

## 4. Velopack 채널 매핑

| 앱 | Retail 채널 | Preview 채널 |
| ---- | ---- | ---- |
| TableCloth | `x64` / `arm64` (현행) | `preview-x64` / `preview-arm64` |
| Spork | `spork-x64` / `spork-arm64` (현행) | `spork-preview-x64` / `spork-preview-arm64` |

- Velopack 메타데이터(`releases.<channel>.json` / `RELEASES-<channel>` / `assets.<channel>.json`)가 채널별로 분리되어 같은 릴리스 자산 폴더에 공존해도 충돌하지 않는다(현행 arch 분리와 동일 원리).
- Preview 설치본은 설치 시 자신의 채널(`preview-<arch>`)을 각인하고, 앱의 `UpdateManager` 가 그 채널 메타데이터로 업데이트를 확인한다(§8).

## 5. 버전 체계

- **Directory.Build.Props 는 4-part 숫자 유지**(AssemblyVersion/FileVersion 용). 여기에 `-preview` 접미사를 넣지 않는다.
- **Preview 표시 버전은 SemVer2 프리릴리스**: `X.Y.Z-preview.N`.
  - `X.Y.Z` = 다음 목표 Retail 버전(예: 현재 Retail 1.20.6 → Preview 는 `1.21.0-preview.*`).
  - `N` = 프리뷰 반복 번호(태그에서 부여, 예: `v1.21.0-preview.3`).
- **Velopack `--packVersion`** 은 SemVer2 프리릴리스를 그대로 받는다(`1.21.0-preview.3`). Velopack 이 프리릴리스를 정상 순서 비교하므로 프리뷰 간 자동 업데이트가 동작한다.
- **태그 검증(`validate-version`) 확장**: `vX.Y.Z-preview.N` 태그는 3-part 코어(`X.Y.Z`)만 Props 와 비교하고 `-preview.N` 접미사는 검증에서 제외(정규식으로 코어 추출). Retail 태그(`vX.Y.Z`)는 현행 그대로.

## 6. 자산명 규칙 (Preview)

Retail 과 이름이 겹치지 않도록 `-Preview` 를 접두 삽입한다(웹/자동화가 링을 이름으로 구분 가능):

| 종류 | Retail | Preview |
| ---- | ---- | ---- |
| 설치 관리자 | `TableCloth_<ver>_Release_<arch>.exe` | `TableCloth-Preview_<ver>_Release_<arch>.exe` |
| 포터블 | `TableCloth_<ver>_Release_<arch>_Portable.zip` | `TableCloth-Preview_<ver>_Release_<arch>_Portable.zip` |
| Spork | `Spork_<ver>…` | `Spork-Preview_<ver>…` |

- `<ver>` 는 Preview 에서도 4-part 파일 버전 문자열을 쓰되(파일명 안정), 릴리스/Velopack 표시는 §5 의 SemVer 프리릴리스.
- **무설치 고정 URL 별칭(`SporkBootstrap_<arch>.exe`, `Spork_<arch>_Portable.zip`, `no-install-spork.wsb`)은 Preview 레인에서 생성하지 않는다.** 이들은 `latest/download`(=Retail) 계약이므로 프리릴리스에 얹으면 혼동만 준다.

## 7. GitHub 릴리스 전략

- **Retail**: 현행 그대로 — draft 로 만들고 서명 후 **정식 릴리스로 Publish**(prerelease=false) → `released` → winget.
- **Preview**: **prerelease=true 로 생성·게시**.
  - `/releases/latest` 에서 제외 → winget·무설치 웹앱·Retail 폴백이 자동으로 무시.
  - 서명: Preview 도 동일 서명 절차 권장(사용자 실행 신뢰). 미서명 배포를 허용할지는 §14 참조.
  - 릴리스 노트: "미리 보기 — 실사용 검증용, 문제 보고 환영" 배너 + AOT 변경 요약.

## 8. 앱 측 채널 인식

사용자는 앱 옵션에서 업데이트 채널을 선택한다. 기본값은 Retail이며 선택값을 preferences 파일에 보존한다.

- **채널 모델**: `ReleaseChannel` enum의 `Retail`과 `Preview` 값을 옵션 UI와 업데이트 관리자가 함께 참조한다.
- **업데이트(`AppUpdateManager`)**:
  - Velopack: **양쪽 링 모두 `ExplicitChannel` 을 명시**한다(Retail = `<arch>`, Preview = `preview-<arch>` — `vpk pack --channel` 값과 같은 이름). 비워 두면 Velopack 기본값이 "설치 시점에 구워진 채널"이라, Preview 설치본에서 Retail 을 골라도 전환이 걸리지 않는다. 소스는 Preview 만 `prerelease:true`.
  - GitHub API 폴백: Preview 는 `/releases/latest` 대신 `/releases`(프리릴리스 포함)에서 최신 prerelease 를 골라 `TableCloth-Preview_…_<arch>.exe` 매칭. Retail 은 현행(`/releases/latest` + `_Release_<arch>`).
  - **되돌리기(Preview → Retail)**: `AllowVersionDowngrade` 를 켠다. 대상 버전이 현재보다 낮기 때문(예: `1.21.0-preview.1` → `1.20.9`)이며, 이 옵션이 없으면 Velopack 이 "업데이트 없음"으로 판단한다. 안정 링에 머무는 평상시에는 켜지 않는다(의도치 않은 하향 방지).
  - 설치 정보(IsInstalled/CurrentVersion)는 채널 무관하므로 기본 매니저 유지.
- **설정 영속성**: 채널 선택은 preferences 파일(사용자 데이터 위치)에 저장되어 업데이트 후에도 유지되어야 한다(§14 검증 항목).
- **되돌리기는 수동 절차로 안내한다(자동화하지 않음)**: 설정 파일이 재설치를 살아남기 때문에, 안정 버전을 먼저 설치해도 설정이 계속 Preview 를 가리켜 다시 끌려 올라간다. 이를 앱이 자동으로 판정하는 방안(설치본의 링을 관측해 설정을 따라가게 하는 대조 로직)과 인앱 다운그레이드(`AllowVersionDowngrade`)를 함께 시도했으나 **의도대로 동작하지 않아 들어냈다**. 자동 판정이 어려운 근본 이유는 *미리 보기로 막 전환한 사용자*(설정=Preview, 설치본=Retail)와 *되돌리려는 사용자*(설정=Preview, 설치본=Retail)가 **겉보기에 같은 상태**라서다. 잘못 구분하면 방금 켠 미리 보기 설정이 저절로 꺼지는 쪽이 되어 더 나쁘다.
  - 대신 **순서를 사용자가 정하도록** 안내한다: ① 채널을 Retail 로 바꿔 Preview 링에서 빠져나온 뒤 ② 안정 버전 설치 파일을 직접 받아 설치. 절차는 [TROUBLESHOOTING_UPDATE_CHANNEL.md](TROUBLESHOOTING_UPDATE_CHANNEL.md), 같은 취지의 문구가 옵션 창의 미리 보기 경고에도 들어간다.
  - ①이 실제로 효력을 가지려면 위 `ExplicitChannel` 명시가 필요하다. 그것이 없으면 Retail 을 골라도 조회는 계속 `preview-<arch>` 로 가서 옵트아웃 자체가 무의미해진다.
- **UI 표시**: 옵션의 미리 보기 탭에서 Retail과 Preview를 선택하고 Preview 채널의 특성과 되돌리기 순서를 안내한다.

## 9. AOT의 Retail 승격 결과

AOT는 세 차례 Preview 릴리스에서 x64와 arm64 빌드 및 릴리스 생성을 검증한 뒤 2026년 8월 23일 `main` 통합을 시작했다.
승격 작업은 다음 순서로 반영했다.

1. `v1.21.x`를 `main` 통합 브랜치에 병합하고 WPF 파일을 제거한다.
2. `build.yml` Retail 레인을 AOT 게시로 전환하고 WPF 게시 플래그를 제거한다.
3. **하위호환(중요)**: 기존 Preview 설치 사용자는 채널 `preview-<arch>` 에 묶여 있다. 승격 시 **최소 한 번은 승격 버전을 `preview-<arch>` 채널에도 게시**해 프리뷰 사용자를 Retail 로 유도하거나, 앱이 승격 감지 후 채널을 Retail 로 전환하는 마이그레이션을 둔다. (Velopack 채널 전환은 자동이 아니므로 브리지 필요.)
4. winget/무설치 웹앱은 Retail 이 AOT 로 바뀌어도 자산명·고정 URL 계약이 동일하면 무영향.

> Preview 채널 브리지는 v1.21.0 정식 게시 과정에서 최종 적용한다. §3부터 §8까지는 승격 전 병행 운영 기록이다.

## 10. 외부 계약 영향 요약

| 대상 | 영향 | 대응 |
| ---- | ---- | ---- |
| **winget** | 없음 | 프리릴리스라 `released` 미발생 → 자동 제외. Retail 만 계속 제출. |
| **무설치 웹앱**(yourtablecloth.app) | 없음 | `latest/download` 는 최신 정식 릴리스만 → Preview(프리릴리스) 미노출. Preview 레인은 고정 URL 별칭을 만들지 않음(§6). |
| **후원자/기여자 파이프라인** | 없음 | 릴리스와 무관. |
| **SBOM/attestation** | Preview 도 생성 권장 | CI Preview 레인에 동일 스텝(선택). |

## 11. CI 구현

Preview는 `.github/workflows/preview.yml`이 담당하고 Retail은 `.github/workflows/build.yml`이 담당한다.

- **트리거**: Preview 태그 규칙 `v*-preview.*`(예: `v1.21.0-preview.3`). Retail 은 현행 `v*`(프리릴리스 접미사 없음).
  - `validate-version`: 태그에서 3-part 코어 추출 후 Props 와 비교(§5). Preview 접미사 허용.
- **빌드/게시**: 두 레인 모두 **AOT 게시**(`dotnet publish -r win-<arch>` → csproj가 `PublishAot` 자동 활성화). WPF 전용 `PublishSingleFile`과 `ReadyToRun` 플래그는 전달하지 않는다. x64는 `windows-latest`, arm64는 `windows-11-arm` 네이티브 러너에서 빌드한다.
- **패키징**: `vpk pack --channel preview-<arch>`(TableCloth) / `spork-preview-<arch>`(Spork). pdb 는 심볼 자산으로 분리(현행과 동일).
- **자산명**: §6 규칙(`TableCloth-Preview_…`).
- **릴리스**: `create-release` 를 **prerelease=true** 로(Preview 태그일 때). 무설치 고정 URL 별칭 스텝은 Preview 에서 건너뜀.
- **winget/discord**: `released` 트리거라 Preview(프리릴리스)에서는 발생하지 않음(무변경).

## 12. AOT 게시 플래그

- `PublishSingleFile`과 `PublishReadyToRun`은 `PublishAot`과 배타이므로 Retail 및 Preview 워크플로에서 전달하지 않는다.
- 각 진입점 csproj의 RID 조건부 그룹이 `PublishAot=true`와 크기 최적화 속성을 적용한다.
- 두 워크플로는 `dotnet publish -r win-<arch> -c <구성>` 형식으로 AOT 산출물을 생성한다.

## 13. build.cs (로컬 서명 빌드)

- `--preview` 플래그 추가: 설정 시 채널(`preview-<arch>`/`spork-preview-<arch>`), 자산명(`-Preview`), 버전(SemVer 프리릴리스), 무설치 별칭 생략을 일괄 적용.
- 서명 경로(`--sign`)는 Retail/Preview 공통(SimplySign). AOT 네이티브 exe 서명은 signtool 로 동일 적용(별도 검증 필요 — §14).
- `EnsureVsWhereOnPath`/`PruneSymbols`(M5)는 Preview(AOT)에서 그대로 활용.

## 14. 잔여 검토 항목

- **arm64 Preview(AOT)**: `windows-11-arm` 네이티브 러너에서 빌드하고 x64 서명 호스트에서 패키징 및 서명하는 경로를 검증했다.
- **Preview 서명**: CI 게시 산출물을 x64 서명 호스트로 인계해 TableCloth와 Spork의 x64 및 arm64 패키지를 서명한다.
- **Preview 앱 내 채널 전환**: 옵션의 업데이트 채널 설정으로 Retail과 Preview를 선택한다.
- **승격 시 Velopack 채널 브리지**(§9-3)의 구체 방법: 승격 시점에 확정.
- **Preview 버전 `N` 부여 주체**: 태그 수기 vs CI 자동 카운터. 초기엔 태그 수기(`-preview.N`) 권장.

## 15. 구현 결과

1. **앱 채널 인식**: `ReleaseChannel` 설정과 `AppUpdateManager`의 Velopack 및 GitHub 프리릴리스 분기 구현.
2. **build.cs `--preview`**: 프리뷰 채널, 자산명, 버전, 고정 URL 별칭 생략 처리 구현.
3. **CI Preview 레인**: `preview.yml`에 프리뷰 태그 검증, AOT 게시, prerelease 생성, 서명용 게시 산출물 인계 구현.
4. **문서**: `RELEASING.md`에 Preview 릴리스 런북과 서명 절차 반영.
5. **arm64 Preview**: `windows-11-arm` 네이티브 빌드와 x64 호스트 서명 경로 검증.
6. **Retail 승격**: `v1.21.x` 통합과 Retail AOT 워크플로 전환.

## 16. 주요 반영 파일

- `src/TableCloth.Core` 또는 `src/Shared`: `ReleaseChannel` 상수/헬퍼.
- `src/TableCloth.App/Components/Implementations/AppUpdateManager.cs`: 채널 분기 + 프리릴리스 폴백.
- `src/TableCloth.App` About/Splash 뷰: Preview 배지.
- `build.cs`: `--preview` 모드.
- `.github/workflows/build.yml`: Preview 레인 + `validate-version` 프리릴리스 허용.
- `docs/RELEASING.md`: Preview 런북. 본 문서.
