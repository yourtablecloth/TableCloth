# 릴리스 채널 분리 설계 — Retail / Preview (이슈 #296)

> 상태: **설계 (구현 전, 승인 대기)** · 2026-07-25
> 배경: WPF→Avalonia+Native AOT(이슈 [#296](https://github.com/yourtablecloth/TableCloth/issues/296)) 전환은 변화 폭이 커서,
> 안정 사용자를 보호한 채 조기 검증을 받기 위해 **Retail(안정)** 과 **Preview(선행)** 두 릴리스 링을 분리한다.
> 선행 참고: [RELEASING.md](RELEASING.md)(현행 릴리스 런북), [AVALONIA_AOT_MIGRATION.md](AVALONIA_AOT_MIGRATION.md)(마이그레이션).

## 1. 확정 결정 (2026-07-25)

| 항목 | 결정 | 근거 |
| ---- | ---- | ---- |
| 채널 매핑 | **Retail = 현행 WPF 유지 / Preview = AOT 신규 레인** | 기존 사용자는 안정 WPF 를 계속 받고, AOT 는 opt-in 으로 조기 검증. AOT 안정화 후 Retail 로 승격(§9). 리스크 최소·되돌리기 용이. |
| Preview 배포 | **별도 프리릴리스 설치본 + Velopack `preview` 채널** | 프리뷰 설치본을 한 번 받으면 이후 프리뷰 채널로 자동 업데이트. Retail·winget·무설치 웹앱은 GitHub 프리릴리스 특성상 자동으로 무영향. Velopack 네이티브 방식이라 가장 단순·견고. |
| 진행 방식 | **설계 문서 우선, 구현은 승인 후 단계별** | 변화 폭이 커 외부 계약(winget/웹앱)·CI·버전 체계가 얽힘. 문서로 확정 후 안전하게 이관. |

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

Preview 빌드는 **컴파일 타임 상수**로 자신이 Preview 임을 안다(설치본 각인과 일치). Retail 빌드는 무플래그 → 현행 동작.

- **정의**: Preview 게시 시 `-p:DefineConstants=PREVIEW_CHANNEL` (또는 `<IsPreviewChannel>` MSBuild 속성 → DefineConstants 매핑). Retail 은 미정의.
- **채널 상수**: `Helpers.ReleaseChannel`(enum `Retail`/`Preview`) 를 `#if PREVIEW_CHANNEL` 로 분기. UI·업데이트가 이를 참조.
- **업데이트(`AppUpdateManager`)**:
  - Velopack: **양쪽 링 모두 `ExplicitChannel` 을 명시**한다(Retail = `<arch>`, Preview = `preview-<arch>` — `vpk pack --channel` 값과 같은 이름). 비워 두면 Velopack 기본값이 "설치 시점에 구워진 채널"이라, Preview 설치본에서 Retail 을 골라도 전환이 걸리지 않는다. 소스는 Preview 만 `prerelease:true`.
  - GitHub API 폴백: Preview 는 `/releases/latest` 대신 `/releases`(프리릴리스 포함)에서 최신 prerelease 를 골라 `TableCloth-Preview_…_<arch>.exe` 매칭. Retail 은 현행(`/releases/latest` + `_Release_<arch>`).
  - **되돌리기(Preview → Retail)**: `AllowVersionDowngrade` 를 켠다. 대상 버전이 현재보다 낮기 때문(예: `1.21.0-preview.1` → `1.20.9`)이며, 이 옵션이 없으면 Velopack 이 "업데이트 없음"으로 판단한다. 안정 링에 머무는 평상시에는 켜지 않는다(의도치 않은 하향 방지).
- **UI 표시**: About/타이틀/스플래시에 **"Preview" 배지**(버전 옆). 사용자가 자신이 선행 채널임을 항상 인지. (Retail 은 배지 없음.)
- **앱 밖 링 변경 대조**: 설정 파일이 재설치를 살아남기 때문에, 안정 버전을 수동으로 재설치해도 설정이 계속 Preview 를 가리켜 다시 끌려 올라가는 함정이 있다. `PreferenceSettings.LastKnownInstalledChannel` 에 마지막으로 관측한 **설치본의 링**을 기록해 두고, 설치본의 링이 그 관측과 달라졌을 때만 설정을 설치본에 맞춘다(= 수동 다운그레이드 시 자동 옵트아웃). 무조건 설치본을 따르면 "Preview 로 토글 → 업데이트 전 재시작" 이 토글을 되돌려버리므로 이 조건이 필요하다. 상태 전이는 `ReleaseChannelReconciler` + 동명의 테스트가 고정한다.
- **되돌아가기**: 옵션 창에서 Retail 을 고르고 업데이트를 확인하면 그대로 내려간다(재설치 불필요). 이미 함정에 빠진 사용자의 수동 복구 절차는 [TROUBLESHOOTING_UPDATE_CHANNEL.md](TROUBLESHOOTING_UPDATE_CHANNEL.md).

## 9. 승격(Graduation) 경로 — AOT → Retail

AOT 가 Preview 에서 충분히 검증되면 Retail 로 승격한다. 절차(향후 별도 문서화):

1. `feature/avalonia-aot` → `main` 병합(WPF 코드 제거 또는 보존 브랜치 분리는 §의 롤백 정책에 따름).
2. `build.yml` Retail 레인을 **AOT 게시로 전환**(WPF 게시 플래그 제거 — §12 의 충돌 해소를 Retail 에도 적용).
3. **하위호환(중요)**: 기존 Preview 설치 사용자는 채널 `preview-<arch>` 에 묶여 있다. 승격 시 **최소 한 번은 승격 버전을 `preview-<arch>` 채널에도 게시**해 프리뷰 사용자를 Retail 로 유도하거나, 앱이 승격 감지 후 채널을 Retail 로 전환하는 마이그레이션을 둔다. (Velopack 채널 전환은 자동이 아니므로 브리지 필요.)
4. winget/무설치 웹앱은 Retail 이 AOT 로 바뀌어도 자산명·고정 URL 계약이 동일하면 무영향.

> 이 단계는 승격 시점에 확정한다. 본 설계의 §3~§8 은 그 전까지의 **병행 운영**을 다룬다.

## 10. 외부 계약 영향 요약

| 대상 | 영향 | 대응 |
| ---- | ---- | ---- |
| **winget** | 없음 | 프리릴리스라 `released` 미발생 → 자동 제외. Retail 만 계속 제출. |
| **무설치 웹앱**(yourtablecloth.app) | 없음 | `latest/download` 는 최신 정식 릴리스만 → Preview(프리릴리스) 미노출. Preview 레인은 고정 URL 별칭을 만들지 않음(§6). |
| **후원자/기여자 파이프라인** | 없음 | 릴리스와 무관. |
| **SBOM/attestation** | Preview 도 생성 권장 | CI Preview 레인에 동일 스텝(선택). |

## 11. CI 설계 (`build.yml`)

Preview 레인을 **추가**(Retail 스텝은 무변경). 트리거로 링을 판별:

- **트리거**: Preview 태그 규칙 `v*-preview.*`(예: `v1.21.0-preview.3`). Retail 은 현행 `v*`(프리릴리스 접미사 없음).
  - `validate-version`: 태그에서 3-part 코어 추출 후 Props 와 비교(§5). Preview 접미사 허용.
- **빌드/게시**: Preview 잡은 **AOT 게시**(`dotnet publish -r win-<arch>` → csproj 가 `PublishAot` 자동 활성화; WPF 전용 `PublishSingleFile`/`ReadyToRun` 플래그 **미전달**). MSVC 링커는 GitHub windows 러너 기본 포함(부트스트래퍼 선례). arm64 는 별도 검토(사용자 요청으로 본 문서 범위 제외 — §14).
- **패키징**: `vpk pack --channel preview-<arch>`(TableCloth) / `spork-preview-<arch>`(Spork). pdb 는 심볼 자산으로 분리(현행과 동일).
- **자산명**: §6 규칙(`TableCloth-Preview_…`).
- **릴리스**: `create-release` 를 **prerelease=true** 로(Preview 태그일 때). 무설치 고정 URL 별칭 스텝은 Preview 에서 건너뜀.
- **winget/discord**: `released` 트리거라 Preview(프리릴리스)에서는 발생하지 않음(무변경).

## 12. AOT/WPF 게시 플래그 충돌 해소

- 현행 `build.yml` 은 `-p:PublishSingleFile=true -p:PublishReadyToRun=true` 를 명시 → `PublishAot` 과 배타.
- **Retail 레인(WPF)**: 현행 유지(WPF 는 AOT 아님).
- **Preview 레인(AOT)**: 위 플래그를 **전달하지 않는다**. csproj 의 RID-조건부 그룹이 `PublishAot=true` + 크기 세트를 자동 적용(M5). 즉 Preview 잡은 `dotnet publish -r win-<arch> -c Release` 만으로 AOT 산출.
- (승격 시) Retail 레인도 이 방식으로 전환(§9-2).

## 13. build.cs (로컬 서명 빌드)

- `--preview` 플래그 추가: 설정 시 채널(`preview-<arch>`/`spork-preview-<arch>`), 자산명(`-Preview`), 버전(SemVer 프리릴리스), `DefineConstants=PREVIEW_CHANNEL`, 무설치 별칭 생략을 일괄 적용.
- 서명 경로(`--sign`)는 Retail/Preview 공통(SimplySign). AOT 네이티브 exe 서명은 signtool 로 동일 적용(별도 검증 필요 — §14).
- `EnsureVsWhereOnPath`/`PruneSymbols`(M5)는 Preview(AOT)에서 그대로 활용.

## 14. 열린 질문 / 미결

- **arm64 Preview(AOT)**: 로컬은 VS "C++ ARM64 build tools" 필요. CI 는 부트스트래퍼가 이미 arm64 AOT 게시 중이라 가능성 높음(다음 릴리스 확인). **본 채널 설계의 arm64 CI 세부는 사용자 요청으로 범위 제외** — x64 Preview 를 우선 확립하고 arm64 는 후속.
- **Preview 서명**: 프리뷰도 전체 서명할지, 설치 관리자 외피만 서명(`tools/sign-release.ps1`)할지. AOT 네이티브 exe + Velopack 자산 서명 실측 필요.
- **Preview 앱 내 채널 전환 토글**: 이번엔 별도 프리릴리스 설치본 방식으로 결정. 향후 "앱 내 opt-in 토글"을 추가할지는 별도 결정(현재 범위 아님).
- **승격 시 Velopack 채널 브리지**(§9-3)의 구체 방법: 승격 시점에 확정.
- **Preview 버전 `N` 부여 주체**: 태그 수기 vs CI 자동 카운터. 초기엔 태그 수기(`-preview.N`) 권장.

## 15. 구현 단계 (승인 후, 제안 순서)

1. **앱 채널 인식**: `Helpers.ReleaseChannel`(+`#if PREVIEW_CHANNEL`), About/스플래시 "Preview" 배지, `AppUpdateManager` 채널 분기(Velopack `ExplicitChannel` + GitHub 프리릴리스 폴백).
2. **build.cs `--preview`**: 로컬에서 프리뷰 산출물(채널/자산명/버전/DefineConstants/별칭 생략) 생성·검증.
3. **CI Preview 레인**: `build.yml` 에 `v*-preview.*` 트리거 + AOT 게시 + `preview` 채널 + prerelease 생성.
4. **문서**: `RELEASING.md` 에 Preview 릴리스 런북 추가, 본 문서 갱신.
5. (후속) arm64 Preview, 승격 절차 문서화.

## 16. 영향 받는 파일 (예상)

- `src/TableCloth.Core` 또는 `src/Shared`: `ReleaseChannel` 상수/헬퍼.
- `src/TableCloth.App/Components/Implementations/AppUpdateManager.cs`: 채널 분기 + 프리릴리스 폴백.
- `src/TableCloth.App` About/Splash 뷰: Preview 배지.
- `build.cs`: `--preview` 모드.
- `.github/workflows/build.yml`: Preview 레인 + `validate-version` 프리릴리스 허용.
- `docs/RELEASING.md`: Preview 런북. 본 문서.
