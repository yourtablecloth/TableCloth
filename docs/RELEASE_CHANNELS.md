# 릴리스 채널 분리 설계 — Retail / Preview (이슈 #296)

> 상태: **설계 확정 · Retail 측(1.20.7) 구현 완료 · Preview 게시 배선 진행 예정** · 2026-07-25
> 배경: WPF→Avalonia+Native AOT(이슈 [#296](https://github.com/yourtablecloth/TableCloth/issues/296)) 전환은 변화 폭이 커서,
> 안정 사용자를 보호한 채 조기 검증을 받기 위해 **Retail(안정)** 과 **Preview(선행)** 두 릴리스 링을 분리한다.
> 선행 참고: [RELEASING.md](RELEASING.md)(릴리스 런북), [AVALONIA_AOT_MIGRATION.md](AVALONIA_AOT_MIGRATION.md)(마이그레이션).

## 1. 확정 결정

| 항목 | 결정 | 근거 |
| ---- | ---- | ---- |
| 채널 매핑 | **Retail = 현행 WPF 유지 / Preview = AOT 신규 레인** | 기존 사용자는 안정 WPF 를 계속 받고, AOT 는 opt-in 으로 조기 검증. AOT 안정화 후 Retail 로 승격(§9). |
| Preview 선택 방식 | **앱 내 채널 토글**(옵션 → 안정/미리 보기) — Retail 1.20.7 에 탑재 | 기존 1.20.6 사용자가 별도 설치본을 따로 받지 않고도 **앱 안에서** 미리 보기로 전환해 1.21.x(AOT)를 받을 수 있어야 롤아웃이 성립한다. (초기 설계의 "별도 프리릴리스 설치본"을 이 in-app 토글로 대체·확정. 신규 사용자를 위한 프리릴리스 설치본은 부가 경로로 남길 수 있음.) |
| Preview 배포 구조 | **GitHub 프리릴리스 + Velopack `preview` 채널** | 프리릴리스는 `/releases/latest` 에서 빠지므로 winget·무설치 웹앱·Retail 업데이트가 자동으로 Preview 를 무시한다. |
| 진행 방식 | **설계 문서 우선, 구현은 승인 후 단계별** | 외부 계약(winget/웹앱)·CI·버전 체계가 얽혀 문서로 확정 후 이관. |

## 2. 현행 구조 요약 (분리 전)

- **버전 단일 출처**: `Directory.Build.Props`(4-part 숫자). CI `validate-version` 이 태그의 3-part 와 비교.
- **릴리스**: 태그 `v*` push → `build.yml`(x64/arm64 빌드 + **미서명 draft**) → 로컬 `build.cmd --sign` → `gh release upload --clobber` → UNSIGNED 마커 제거 후 Publish → `released` → winget PR.
- **Velopack 채널**: TableCloth = `<arch>`(x64/arm64), Spork = `spork-<arch>`. **링 개념 없음**.
- **자산명**: `TableCloth_<4part>_Release_<arch>.exe` / `_Portable.zip`.
- **업데이트**(`AppUpdateManager`): ① Velopack → ② 실패 시 GitHub API `/releases/latest` 에서 `_Release_<arch>.exe` 매칭. `/releases/latest` 는 프리릴리스를 제외한 최신을 반환.
- **외부 고정 URL 계약**: winget(Setup.exe), 무설치 웹앱(`releases/latest/download/…`). 모두 `/releases/latest`(=Retail) 의존.

> **핵심 제약(분리의 실질적 계기):** `build.yml` 은 WPF 게시 플래그(`PublishSingleFile`+`ReadyToRun`)를 명시하는데, 이는 M5 의
> `PublishAot` 과 상호배타다. AOT 는 현행 Retail CI 로 못 내보내므로 전용 게시 경로가 필요 → Preview 레인이 담당.

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

전환 방법: 앱 옵션 → 미리 보기 탭 → "업데이트 채널" 라디오(안정/미리 보기).
           선택은 다음 업데이트 확인부터 적용(재시작 불필요).
```

- **Retail 은 현행과 100% 동일**하게 유지(채널명·자산명·CI·winget·웹앱 무변경). 기존 설치 사용자의 자동 업데이트가 끊기지 않는다.
- **Preview 는 순수 추가(additive)** 레인. GitHub 의 **prerelease 플래그**가 분리의 중심축이다.

## 4. Velopack 채널 매핑

| 앱 | Retail 채널 | Preview 채널 |
| ---- | ---- | ---- |
| TableCloth | `x64` / `arm64` (현행) | `preview-x64` / `preview-arm64` |
| Spork | `spork-x64` / `spork-arm64` (현행) | `spork-preview-x64` / `spork-preview-arm64` |

## 5. 버전 체계

- **Directory.Build.Props 는 4-part 숫자 유지**(AssemblyVersion/FileVersion). `-preview` 접미사를 넣지 않는다.
- **Preview 표시 버전은 SemVer2 프리릴리스**: `X.Y.Z-preview.N`(예: `1.21.0-preview.3`). Velopack `--packVersion` 이 그대로 수용.
- **태그 검증(`validate-version`) 확장**: `vX.Y.Z-preview.N` 은 3-part 코어만 Props 와 비교하고 접미사는 제외. Retail 태그(`vX.Y.Z`)는 현행 그대로.

## 6. 자산명 규칙 (Preview)

| 종류 | Retail | Preview |
| ---- | ---- | ---- |
| 설치 관리자 | `TableCloth_<ver>_Release_<arch>.exe` | `TableCloth-Preview_<ver>_Release_<arch>.exe` |
| 포터블 | `TableCloth_<ver>_Release_<arch>_Portable.zip` | `TableCloth-Preview_<ver>_Release_<arch>_Portable.zip` |
| Spork | `Spork_<ver>…` | `Spork-Preview_<ver>…` |

- **무설치 고정 URL 별칭(`SporkBootstrap_<arch>.exe`, `Spork_<arch>_Portable.zip`, `no-install-spork.wsb`)은 Preview 레인에서 생성하지 않는다.** `latest/download`(=Retail) 계약이므로.

## 7. GitHub 릴리스 전략

- **Retail**: 현행 그대로 — draft → 서명 → 정식 릴리스 Publish(prerelease=false) → `released` → winget.
- **Preview**: **prerelease=true 로 생성·게시** → `/releases/latest` 제외 → winget·웹앱·Retail 폴백 자동 무시. 서명은 동일 절차 권장(§14).

## 8. 앱 측 채널 인식

**Retail 1.20.7 에 in-app 채널 토글을 탑재**(구현 완료 — §11). 사용자가 옵션에서 링을 고르면 앱이 해당 링으로 업데이트를 확인한다.

- **설정**: `PreferenceSettings.UpdateChannel`(`ReleaseChannel` = Retail/Preview, 기본 Retail, JSON 문자열 저장).
- **UI**: 옵션 창 "미리 보기" 탭 상단 라디오 버튼(안정/미리 보기) + 설명 + 프리뷰 경고. 변경 시 저장만(재시작 불필요).
- **업데이트(`AppUpdateManager`)**:
  - Velopack: **양쪽 링 모두 `ExplicitChannel` 을 명시**한다(Retail = `<arch>`, Preview = `preview-<arch>` — `vpk pack --channel` 값과 같은 이름). 소스는 Preview 만 `prerelease:true`.
  - GitHub API 폴백: Retail 은 `/releases/latest` + `_Release_<arch>`(현행), Preview 는 `/releases` 최신 프리릴리스의 `-Preview_…_<arch>` 매칭.
  - **되돌리기(Preview → Retail)**: `AllowVersionDowngrade` 를 켠다. 대상 버전이 현재보다 낮기 때문(예: `1.21.0-preview.1` → `1.20.9`)이며, 이 옵션이 없으면 Velopack 이 "업데이트 없음"으로 판단한다. 안정 링에 머무는 평상시에는 켜지 않는다(의도치 않은 하향 방지).
  - 설치 정보(IsInstalled/CurrentVersion)는 채널 무관하므로 기본 매니저 유지.
- **설정 영속성**: 채널 선택은 preferences 파일(사용자 데이터 위치)에 저장되어 업데이트 후에도 유지되어야 한다(§14 검증 항목).
- **앱 밖 링 변경 대조**: 설정 파일이 재설치를 살아남기 때문에, 안정 버전을 수동으로 재설치해도 설정이 계속 Preview 를 가리켜 다시 끌려 올라가는 함정이 있다. `PreferenceSettings.LastKnownInstalledChannel` 에 마지막으로 관측한 **설치본의 링**을 기록해 두고, 설치본의 링이 그 관측과 달라졌을 때만 설정을 설치본에 맞춘다(= 수동 다운그레이드 시 자동 옵트아웃). 무조건 설치본을 따르면 "Preview 로 토글 → 업데이트 전 재시작" 이 토글을 되돌려버리므로 이 조건이 필요하다. 상태 전이는 `ReleaseChannelReconciler` + 동명의 테스트가 고정한다.
- **사용자 안내**: 이미 함정에 빠진 사용자의 수동 복구 절차는 [TROUBLESHOOTING_UPDATE_CHANNEL.md](TROUBLESHOOTING_UPDATE_CHANNEL.md).

## 9. 승격(Graduation) 경로 — AOT → Retail

AOT 가 Preview 에서 충분히 검증되면 Retail 로 승격한다.

1. `feature/avalonia-aot` → `main` 병합. `build.yml` Retail 레인을 AOT 게시로 전환(§12).
2. **하위호환(중요)**: 기존 Preview 설치 사용자는 채널 `preview-<arch>` 에 묶여 있다. 승격 시 최소 한 번 승격 버전을 `preview-<arch>` 채널에도 게시하거나, 앱이 승격 감지 후 채널을 Retail 로 전환하는 브리지를 둔다(Velopack 채널 전환은 자동이 아님).
3. winget/무설치 웹앱은 자산명·고정 URL 계약이 동일하면 무영향.

## 10. 외부 계약 영향 요약

| 대상 | 영향 | 대응 |
| ---- | ---- | ---- |
| winget | 없음 | 프리릴리스라 `released` 미발생 → 자동 제외. |
| 무설치 웹앱 | 없음 | `latest/download` 는 정식 릴리스만. Preview 레인은 고정 URL 별칭 미생성. |
| 후원자/기여자 | 없음 | 릴리스와 무관. |

## 11. 구현 현황

- ✅ **1.20.7 (Retail) — 앱 내 업데이트 채널 옵션 완료** (브랜치 `feature/update-channel-option`, 커밋 `59d154e`):
  `ReleaseChannel` + `PreferenceSettings.UpdateChannel`, 채널 인식 `AppUpdateManager`, 옵션 라디오 UI + UI 문자열,
  `EnumBooleanConverter` 양방향 수정, 버전 1.20.7. 빌드 0경고 + 테스트 66/42 통과.
- ⏳ **Preview 게시 배선**(`feature/avalonia-aot`): build.cs/build.yml 에 `--channel preview-<arch>` + prerelease +
  `-Preview` 자산명 + AOT 게시 추가 → **1.21.x-preview** 출시.
- ⏳ **크로스채널 업데이트 실측**: Preview 전환한 WPF(1.20.7) 설치본이 Velopack 으로 1.21.x(AOT) 패키지로 교체
  업데이트되는 경로 검증(1.21.x-preview 게시 후).

## 12. AOT/WPF 게시 플래그 충돌 해소

- **Retail 레인(WPF)**: 현행 유지(`PublishSingleFile`+`ReadyToRun`).
- **Preview 레인(AOT)**: 위 플래그를 전달하지 않는다. csproj 의 RID-조건부 그룹이 `PublishAot=true` + 크기 세트를 자동 적용(M5).
- (승격 시) Retail 레인도 이 방식으로 전환.

## 13. build.cs / CI (Preview 레인)

- `build.yml`: Preview 태그 규칙 `v*-preview.*` 트리거 추가. Preview 잡은 AOT 게시 + `vpk pack --channel preview-<arch>` +
  `-Preview` 자산명 + `create-release` prerelease=true. winget/discord 는 `released` 트리거라 미발생(무변경).
- `build.cs`: `--preview` 플래그(채널/자산명/버전/무설치 별칭 생략) 추가. 서명은 Retail/Preview 공통.

## 14. 열린 질문 / 미결

- **arm64 Preview(AOT)**: VS "C++ ARM64 build tools" 필요. CI 는 부트스트래퍼 선례로 가능성 높음(다음 릴리스 확인). arm64 CI 세부는 범위 제외 — x64 우선.
- **Preview 서명**: 전체 서명 vs 설치 관리자 외피만 서명. AOT 네이티브 exe + Velopack 자산 서명 실측 필요.
- **채널 선택 영속성**: preferences 파일이 Velopack 업데이트 후에도 유지되는지 실측(§8).
- **승격 시 Velopack 채널 브리지**(§9-2) 구체안: 승격 시점에 확정.
