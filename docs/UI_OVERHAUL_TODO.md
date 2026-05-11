# UI Overhaul 진척 관리 (Quick-Start + Catalog-in-Spork)

> 작업 브랜치: `feature/ui-overhaul-quickstart`
> 시작일: 2026-05-11

## 배경과 목표

기존 식탁보(TableCloth) UX는 "카탈로그에서 사이트 하나를 골라 → 상세 화면에서 옵션 지정 → 샌드박스 실행" 흐름을 따른다.
실사용 시나리오에서 사용자는 한 번 띄운 샌드박스 안에서 여러 사이트를 오가며 작업하는 경우가 많고,
사이트 단위로 샌드박스를 새로 띄우는 방식은 오히려 불편하다.

따라서 다음과 같이 UX를 재구성한다.

- **TableCloth (호스트 런처)**: 카탈로그/사이트 선택 단계를 제거한 "퀵 스타트" 진입점으로 단순화.
  - 공동인증서 폴더 마운트
  - 데이터 백업용 폴더 마운트
  - 사용자가 직접 추가한 사용자 정의 폴더 마운트
  - 위 설정만으로 즉시 샌드박스 시작
- **Spork (샌드박스 내부 에이전트)**: 샌드박스 안에서 카탈로그 UI를 노출하여, 사용자가 그 안에서 자유롭게 사이트를 골라 보안 모듈 설치/접속할 수 있게 한다.

## 아키텍처 변경 요약 (초안 — 구현 진행 중 갱신)

| 영역 | 현재 | 변경 후 |
|------|------|---------|
| 시작 화면 | `CatalogPage` → `DetailPage` → 샌드박스 실행 | "퀵 스타트" 단일 화면(폴더 마운트 위주) → 즉시 샌드박스 실행 |
| 사이트 선택 | TableCloth 호스트에서 1개만 선택 | Spork(샌드박스 내부)에서 다수 사이트를 자유롭게 선택/실행 |
| 카탈로그 데이터 흐름 | 호스트가 카탈로그 로드 후 선택값을 sandbox config에 반영 | 호스트는 카탈로그를 직접 사용하지 않음. Spork가 샌드박스 내부에서 카탈로그 로드 및 처리 |
| 공동인증서 매핑 | DetailPage 옵션으로 노출 | 퀵 스타트 화면 1차 옵션으로 노출 |
| 데이터 백업 폴더 | (없음) | 퀵 스타트 화면 1차 옵션으로 신설 |
| 매핑 폴더 | DetailPage 하단 옵션 | 퀵 스타트 화면 1차 옵션으로 승격 |
| 표준 마운트 | (없음) | **App** 디렉터리(읽기 전용, Spork+카탈로그 스냅샷) + **Data** 디렉터리(읽기-쓰기, 즐겨찾기/사용 기록/사용자 백업) 항상 마운트 |

## 작업 항목

### Phase 0 — 준비
- [x] 작업 브랜치 `feature/ui-overhaul-quickstart` 생성
- [x] 본 진척 관리 문서 작성
- [ ] 새 UX 와이어프레임/스케치 (필요 시 `docs/images/`에 추가)
- [ ] 기존 코드 진입점 정리 (`MainWindowViewModel.MainWindowLoaded` → 카탈로그 진입 경로 파악 완료)

### Phase 1 — TableCloth 퀵 스타트 화면 도입

- [x] `QuickStartPage` XAML/코드비하인드 신설
- [x] `QuickStartPageViewModel` 신설 (사용자 폴더 + 옵션 진입)
- [x] Data 디렉터리 지정 UX (`PreferenceSettings.DataDirectoryHostPath` 신설, 기본값 `Documents\TableCloth\Data`)
- [x] 사용자 정의 매핑 폴더 UX (`MappedFolderSetting` 재사용, DetailPage 코드 패턴 답습)
- [x] `INavigationService.NavigateToQuickStart()` 추가
- [x] `IAppUserInterface.CreateQuickStartPage()` 추가, DI 등록(`Program.cs`)
- [x] `MainWindowViewModel`: 일반 진입은 QuickStart, `--select <SiteId>`가 있을 때는 종전대로 DetailPage 유지
- [x] `SandboxMountPaths` 상수(`C:\TableCloth\App` / `C:\TableCloth\Data`) 도입, `ISharedLocations`에 App 스테이징/Data 기본 경로 추가

#### Phase 1.5 — 퀵 스타트 UI 단순화 (2026-05-11)

- [x] 인증서 섹션 제거: `%userprofile%\AppData\LocalLow\NPKI`가 호스트에 존재하면 매 시작 시 자동 RO 마운트되도록 `LaunchSandbox`에서 처리
- [x] Data 디렉터리 표시 제거: 사용자에게 노출하지 않고 내부에서 계산된 경로 사용. 디렉터리가 없으면 시작 시 Yes/No 생성 유도 흐름
- [x] 옵션(장치 공유/보조 프로그램/진단)은 별도 `OptionsWindow` 다이얼로그로 이전, 퀵 스타트에는 "옵션..." 진입 버튼만 노출
- [x] 빌드 통과 확인 (에러 0)
- [ ] wsb 생성 시 App/Data 표준 마운트 적용은 Phase 3 작업으로 분리

### Phase 2 — TableCloth에서 카탈로그/디테일 화면 제거 또는 격리
- [ ] `CatalogPage` / `DetailPage` / 관련 ViewModel을 Spork 측으로 이관 또는 Deprecated 처리
- [ ] `MainWindowViewModel`의 카탈로그 의존성 정리
- [ ] 카탈로그 로더 (`IResourceCacheManager`) 의 호스트 측 사용 범위 축소
- [ ] 호스트에서 더 이상 필요 없는 카탈로그 관련 리소스/이미지 정리

### Phase 3 — Spork 내부 카탈로그 UI 도입
- [ ] Spork에 카탈로그 화면 (사이트 그리드 + 카테고리 필터 + 즐겨찾기) 신설
- [ ] 샌드박스 안에서 카탈로그 데이터 로드 (호스트가 동봉해주는 zip vs Spork 자체 다운로드 — 방식 결정 필요)
- [ ] 사이트 선택 시 해당 사이트용 설치/구성 Step 실행 (기존 `Steps/Implementations/*` 재구성)
- [ ] 사이트 전환(여러 사이트 순차 사용) 시의 UX 동선 정의
- [ ] 카탈로그 → 브라우저 열기 흐름 (Spork의 `OpenWebSiteStep` 재사용)

### Phase 4 — 데이터 / 설정 / 호환성
- [ ] `PreferenceSettings`에 새 항목 추가 (`BackupFolder`, 퀵 스타트 기본값 등)
- [ ] 사용자 기존 설정 마이그레이션 정책 (즐겨찾기는 Spork로 이전?)
- [ ] 명령줄 인자(`--select`) 동작 변경 가이드
- [ ] 바로가기 생성 기능 (`IShortcutCreator`) 의미 재정의 — 퀵 스타트 단축키로?
- [ ] CommandLineComposer가 호스트에서 만들어주던 명령줄을 Spork 쪽으로 이전

### Phase 5 — 마무리
- [ ] 리소스 문자열(`UIStringResources`) 정리/추가/번역
- [ ] 테스트 (`TableCloth.Test`, `Spork.Test`) 업데이트
- [ ] 스크린샷/README 업데이트
- [ ] Disclaimer/UpdateCheck/SponsorBanner 등 기존 부가 UI의 새 흐름 내 위치 결정
- [ ] 수동 테스트 시나리오 작성 및 통과 확인

## 결정 사항

- [x] **카탈로그 데이터 전달 방식** (2026-05-11 결정): **하이브리드**. Spork가 카탈로그 로직을 소유하되, 호스트가 시작 시점에 카탈로그 스냅샷(약속된 경로에 읽기 전용 마운트)을 폴백으로 주입한다. Spork는 네트워크 우선, 실패 시 스냅샷으로 폴백.
  - 근거: 샌드박스 내부 네트워크 실패 사례가 경험적으로 존재 → 폴백 필수. 동시에 카탈로그 UI/로직은 Spork에 일원화되어야 함.
  - 비용 (호스트-Spork 계약 분산)은 향후 Spork→TableCloth 병합 Phase에서 자연스럽게 해소됨.
  - 호스트는 빌드 시 이미 생성하는 `Images.zip` 파이프라인을 재활용한다.
- [x] **`--select <SiteId>` 명령줄 인자 호환성** (2026-05-11 결정): **하위 호환 유지 — 퀵 스타트를 거친 뒤 SiteId를 Spork에 그대로 전달**. 기존 사용자/바로가기가 깨지지 않도록 호스트는 인자를 보존하여 샌드박스 안 Spork에 넘기고, Spork가 그 SiteId의 사이트를 자동 선택/실행한다.
- [x] **샌드박스 표준 마운트 컨벤션** (2026-05-11 결정): 모든 wsb는 **App 디렉터리(읽기 전용)**, **Data 디렉터리(읽기-쓰기)**를 항상 마운트한다.
  - **App**: Spork 실행 파일 + 카탈로그 스냅샷 + 보조 리소스. 호스트가 매 실행마다 최신 상태로 채워 넣는다.
  - **Data**: 영속 상태 저장소. 즐겨찾기, 사용 기록, 사용자 다운로드/백업 등 사용자의 자산이 모두 여기에 누적된다.
  - 그 외에 공동인증서 폴더와 사용자 정의 폴더는 선택적으로 추가 마운트한다.
- [x] **즐겨찾기/사용 기록 저장 위치** (2026-05-11 결정): **Data 디렉터리 안에 저장**.
  - 호스트는 즐겨찾기를 알 필요가 없으며 `PreferenceSettings.Favorites`/`ShowFavoritesOnly`는 호스트 측 모델에서 단계적으로 제거 또는 무시한다.
  - Spork가 Data 디렉터리에 즐겨찾기 JSON(가칭 `favorites.json`) 등을 직접 읽고 쓴다.
  - 세션 간 영속성은 Data 디렉터리의 호스트 매핑으로 자연스럽게 확보된다.

## 결정 필요 / 오픈 이슈

- [ ] **공동인증서 미사용 사용자 경험**: 기본 체크 해제? 퀵 스타트에서 "건너뛰기" 일급 동작 제공?
- [ ] **Data 디렉터리 호스트 기본 경로**: `%USERPROFILE%\Documents\식탁보\Data` vs `%APPDATA%\TableCloth\Data` 등. 사용자에게 노출되는 백업 자산이라는 점을 고려.
- [ ] **App / Data 디렉터리의 샌드박스 내부 경로 컨벤션**: 예) `C:\TableCloth\App`, `C:\TableCloth\Data`. Spork와 wsb 생성 로직이 공유하는 상수로 두어야 함.
- [ ] **카탈로그 스냅샷 포맷**: App 디렉터리 안에 zip으로 둘지 압축 해제 상태로 둘지, 버전 메타 파일 포함 여부.

## 향후 Phase (본 작업 이후)

- **Phase 6 — Spork → TableCloth 병합**: 별도 EXE/프로젝트로 분리되어 있는 Spork를 TableCloth 본체에 흡수. 본 작업에서 도입하는 "호스트-Spork 카탈로그 스냅샷 계약"은 이 시점에 내부 호출로 단순화된다. 따라서 본 작업의 계약 설계는 *임시 비용*임을 전제로 단순/얇게 유지한다.

## 진행 방식

- 본 문서를 작업하면서 단계별로 체크박스를 업데이트한다.
- 결정 필요 항목은 결정 시점/근거를 문서에 함께 남긴다.
- 큰 결정(카탈로그 데이터 전달 방식 등)은 코드 변경 이전에 별도로 합의한다.
