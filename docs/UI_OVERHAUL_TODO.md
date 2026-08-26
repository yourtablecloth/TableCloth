# UI Overhaul 진척 관리 (Quick-Start + Catalog-in-Spork)

> 작업 브랜치: `feature/ui-overhaul-quickstart` (main 반영됨)
> 시작일: 2026-05-11
>
> **상태 최신화 (2026-08-26):** 핵심 흐름을 v1.21.1 Retail에 반영했습니다. 호스트 **QuickStart 진입점**
> (`Pages/QuickStartPage`), **데이터 디렉터리 설정**(#282, `SharedLocations.GetEffectiveDataDirectoryPath`),
> **Spork 측 카탈로그**(Phase 3 완료, 사이트 그리드와 검색, 즐겨찾기, 설치 흐름). 이후 Spork 단독 실행 시
> Windows Sandbox 밖에서 실행 중임을 알리는 대화상자도 v1.20.8에 추가했습니다.
> **잔여 작업:** 호스트의 `CatalogPage`와 `DetailPage`가 레거시 진입 폴백으로 남아 있습니다. 리소스 문자열, 부가 UI 배치와 공동인증서 미사용 UX도 후속 작업에 포함합니다. 스냅샷과 자산명 계약은
> [PARAMETERIZED_WSB_SPEC](PARAMETERIZED_WSB_SPEC.md) §7에서 확인할 수 있습니다.
> **참고:** 이슈 [#296](https://github.com/yourtablecloth/TableCloth/issues/296)의 Avalonia와 Native AOT 전환은 v1.21.0에서 완료했습니다. 이 문서는 남아 있는 호스트 카탈로그 폴백과 정리 작업을 추적하는 이력 문서로 유지합니다.

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

## 구현한 아키텍처 변경

| 영역 | 현재 | 변경 후 |
| ------ | ------ | --------- |
| 시작 화면 | `CatalogPage` → `DetailPage` → 샌드박스 실행 | "퀵 스타트" 단일 화면(폴더 마운트 위주) → 즉시 샌드박스 실행 |
| 사이트 선택 | TableCloth 호스트에서 1개만 선택 | Spork(샌드박스 내부)에서 다수 사이트를 자유롭게 선택/실행 |
| 카탈로그 데이터 흐름 | 호스트가 카탈로그 로드 후 선택값을 sandbox config에 반영 | 호스트는 카탈로그를 직접 사용하지 않음. Spork가 샌드박스 내부에서 카탈로그 로드 및 처리 |
| 공동인증서 매핑 | DetailPage 옵션으로 노출 | 퀵 스타트 화면 1차 옵션으로 노출 |
| 데이터 백업 폴더 | (없음) | 퀵 스타트 화면 1차 옵션으로 신설 |
| 매핑 폴더 | DetailPage 하단 옵션 | 퀵 스타트 화면 1차 옵션으로 승격 |
| 표준 마운트 | (없음) | **App** 디렉터리(읽기 전용, Spork+카탈로그 스냅샷) + **Data** 디렉터리(읽기-쓰기, 즐겨찾기/사용 기록/사용자 백업) 항상 마운트 |

## 작업 항목

### Phase 0: 준비
- [x] 작업 브랜치 `feature/ui-overhaul-quickstart` 생성
- [x] 본 진척 관리 문서 작성
- [ ] 새 UX 와이어프레임/스케치 (필요 시 `docs/images/`에 추가)
- [ ] 기존 코드 진입점 정리 (`MainWindowViewModel.MainWindowLoaded` → 카탈로그 진입 경로 파악 완료)

### Phase 1: TableCloth 퀵 스타트 화면 도입

- [x] `QuickStartPage` XAML/코드비하인드 신설
- [x] `QuickStartPageViewModel` 신설 (사용자 폴더 + 옵션 진입)
- [x] Data 디렉터리 지정 UX (`PreferenceSettings.DataDirectoryHostPath` 신설, 기본값 `Documents\TableCloth\Data`)
- [x] 사용자 정의 매핑 폴더 UX (`MappedFolderSetting` 재사용, DetailPage 코드 패턴 답습)
- [x] `INavigationService.NavigateToQuickStart()` 추가
- [x] `IAppUserInterface.CreateQuickStartPage()` 추가, DI 등록(`Program.cs`)
- [x] `MainWindowViewModel`: 일반 진입은 QuickStart, `--select <SiteId>`가 있을 때는 종전대로 DetailPage 유지
- [x] `SandboxMountPaths` 상수(`C:\TableCloth\App` / `C:\TableCloth\Data`) 도입, `ISharedLocations`에 App 스테이징/Data 기본 경로 추가

#### Phase 1.5: 퀵 스타트 UI 단순화 (2026-05-11)

- [x] 인증서 섹션 제거: `%userprofile%\AppData\LocalLow\NPKI`가 호스트에 존재하면 매 시작 시 자동 RO 마운트되도록 `LaunchSandbox`에서 처리
- [x] Data 디렉터리 표시 제거: 사용자에게 노출하지 않고 내부에서 계산된 경로 사용. 디렉터리가 없으면 시작 시 Yes/No 생성 유도 흐름
- [x] 옵션(장치 공유/보조 프로그램/진단)은 별도 `OptionsWindow` 다이얼로그로 이전, 퀵 스타트에는 "옵션..." 진입 버튼만 노출
- [x] 빌드 통과 확인 (에러 0)
- [x] wsb 생성 시 App/Data 표준 마운트 적용

#### Phase 1.6: Data 디렉터리 사용자 지정 재도입 (이슈 #282)

- [x] 문서 폴더가 네트워크/클라우드 드라이브로 강제 리디렉션된 환경에서 Data 경로를 바꿀 수 없던 문제 해결
- [x] `OptionsWindow`에 "데이터 디렉터리" 탭 신설(현재 경로 표시 + 폴더 선택/기본값으로/열기, `DataDirectoryHostPath` 저장). 기존 고아 리소스 문자열 재사용
- [x] 비로컬(네트워크/클라우드/이동식, UNC) 경로 경고 문구 노출(휴리스틱, 비차단)
- [x] 시작 시 자동 경로에 폴더 생성을 거부하거나 생성에 실패하면 데이터 디렉터리 탭으로 안내(`OpenOptions(DataDirectory)`)
- [x] 기본 경로(`Documents\TableCloth\Data`)는 유지: 기존 사용자 데이터 마이그레이션 불필요

### Phase 2: TableCloth에서 카탈로그/디테일 화면 제거 또는 격리
- [ ] `CatalogPage` / `DetailPage` / 관련 ViewModel을 Spork 측으로 이관 또는 Deprecated 처리
- [ ] `MainWindowViewModel`의 카탈로그 의존성 정리
- [ ] 카탈로그 로더 (`IResourceCacheManager`) 의 호스트 측 사용 범위 축소
- [ ] 호스트에서 더 이상 필요 없는 카탈로그 관련 리소스/이미지 정리

### Phase 3: Spork 내부 카탈로그 UI 도입

- [x] Spork `MainWindow`에 카탈로그 뷰 추가 (사이트 그리드 + 카테고리 그루핑 + 검색)
- [x] 명령줄로 사이트 ID가 들어오지 않은 경우 카탈로그 뷰로 진입, 들어온 경우 기존 설치 흐름 유지
- [x] `AppStartup`의 "no targets → 홈 URL 열고 종료" 조기 종료 제거 (카탈로그 뷰가 그 자리를 대체)
- [x] `IStepsComposer.ComposeStepsForSites(IEnumerable<string>)` 오버로드 추가, 사용자가 카탈로그에서 선택한 사이트만으로 설치 단계 구성
- [x] 카탈로그에서 사이트 선택 → 설치 모드(Steps 뷰)로 자동 전환 + 설치 자동 시작
- [x] 즐겨찾기/사용 기록 (Data 디렉터리 영속) 도입: `SporkUserData` 공유 모델 + `IUserDataStore`로 `user-data.json` 읽고 쓰기. 호스트는 기존 `PreferenceSettings.Favorites`를 첫 실행 시 Data 디렉터리로 1회성 마이그레이션
- [x] 사이트 아이콘 표시: 호스트가 `Images.zip`을 staging의 `App/images/`로 풀고, Spork는 `ServiceLogoConverter`로 사이트 ID를 PNG 이미지로 해석. 카탈로그 카드에 아이콘과 즐겨찾기 별 토글을 표시하고 카탈로그 상단에 "즐겨찾기만 보기" 체크박스를 배치
- [x] 카탈로그 데이터 폴백 로직 (네트워크 실패 시 호스트가 주입한 스냅샷 사용: 하이브리드 방식): 호스트가 wsb 생성 시점에 `CatalogCacheFilePath`를 staging의 `catalog/catalog.xml`로 복사, Spork의 `ResourceCacheManager`가 네트워크 실패 시 같은 디렉터리에서 읽어 들이도록 폴백
- [x] 여러 사이트 순차 사용 UX: 카탈로그 진입 사용자는 설치 완료 후 자동 종료 대신 "카탈로그로 돌아가기" 버튼이 노출되어 다음 사이트를 이어서 선택 가능. 명령줄(--select) 진입은 종전대로 자동 종료(외부 호출 호환).
- [x] Windows Sandbox 안에서 Spork 재실행용 데스크톱 바로가기 자동 생성. 통합 진입점은 `TableCloth.exe spork`를 사용하고 단독 진입점은 `Spork.exe`를 사용

### Phase 4: 데이터 / 설정 / 호환성
- [x] `PreferenceSettings`에 Data 디렉터리, 사용자 매핑 폴더와 Sandbox 옵션 추가
- [x] 사용자 기존 설정 마이그레이션 정책과 즐겨찾기 이전
- [x] 명령줄 인자 `--select` 하위 호환 유지
- [x] Spork 재실행 바로가기 생성 기능 정리
- [x] TableCloth 진입점의 `spork` verb와 Spork 인자 전달 통합

### Phase 5: 마무리
- [ ] 리소스 문자열(`UIStringResources`) 정리/추가/번역
- [x] 테스트 (`TableCloth.Test`, `Spork.Test`) 업데이트
- [x] 스크린샷과 README 업데이트 (2026년 8월 26일)
- [ ] Disclaimer/UpdateCheck/SponsorBanner 등 기존 부가 UI의 새 흐름 내 위치 결정
- [ ] 수동 테스트 시나리오 작성 및 통과 확인

## 설계 제약 (must-follow)

- **wsb에 `<SandboxFolder>` 절대 출력 금지**. 구 버전 Windows Sandbox에서 wsb 로딩이 실패하기 때문이며, 모든 호스트 매핑 폴더는 샌드박스 사용자의 데스크톱 하위에 호스트 폴더의 leaf 이름으로 노출된다 (`C:\Users\WDAGUtilityAccount\Desktop\<leaf>`). 이 동작은 by-design이며, 향후 모든 마운트/스크립트 설계는 이를 전제로 한다.
  - 표준 위치가 필요한 자료(예: NPKI를 `AppData\LocalLow\NPKI`로)는 wsb 마운트 옵션이 아니라 startup 스크립트의 `mklink /j`로 처리한다.
  - 호스트 측 폴더의 leaf 이름이 곧 샌드박스 측 식별자다. 새 마운트 추가 시 leaf 충돌 가능성을 항상 검토.

## 결정 사항

- [x] **카탈로그 데이터 전달 방식** (2026-05-11 결정): **하이브리드**. Spork가 카탈로그 로직을 소유하되, 호스트가 시작 시점에 카탈로그 스냅샷(약속된 경로에 읽기 전용 마운트)을 폴백으로 주입한다. Spork는 네트워크 우선, 실패 시 스냅샷으로 폴백.
  - 근거: 샌드박스 내부 네트워크 실패 사례가 경험적으로 존재 → 폴백 필수. 동시에 카탈로그 UI/로직은 Spork에 일원화되어야 함.
  - 비용 (호스트-Spork 계약 분산)은 향후 Spork→TableCloth 병합 Phase에서 자연스럽게 해소됨.
  - 호스트는 빌드 시 이미 생성하는 `Images.zip` 파이프라인을 재활용한다.
- [x] **`--select <SiteId>` 명령줄 인자 호환성** (2026-05-11 결정): **하위 호환 유지: 퀵 스타트를 거친 뒤 SiteId를 Spork에 그대로 전달**. 기존 사용자/바로가기가 깨지지 않도록 호스트는 인자를 보존하여 샌드박스 안 Spork에 넘기고, Spork가 그 SiteId의 사이트를 자동 선택/실행한다.
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
- [x] **Data 디렉터리 호스트 기본 경로**: #282에서 확정. `SharedLocations.DefaultDataDirectoryPath` +
  사용자 지정 경로(`GetEffectiveDataDirectoryPath`), 옵션에 "데이터 디렉터리" 탭으로 노출.
- [x] **App / Data 디렉터리의 샌드박스 내부 경로 컨벤션**: `SandboxMountPaths`(예: `SandboxMountPaths.DataDirectory`)
  상수로 확정. Spork(`UserDataStore`)와 wsb 생성 로직이 공유.
- [x] **카탈로그 스냅샷 포맷**: staging의 `App/catalog/catalog.xml`에 압축하지 않은 XML 스냅샷을 배치

## 향후 Phase (본 작업 이후)

- **Phase 6 완료:** TableCloth 설치판은 `TableCloth.exe spork` verb로 Spork 게스트를 실행합니다. 무설치 Express와 단독 배포를 위해 `Spork.exe` 진입점은 유지합니다. 두 진입점은 `Spork.App`을 공유합니다.

## 진행 방식

- 본 문서를 작업하면서 단계별로 체크박스를 업데이트한다.
- 결정 필요 항목은 결정 시점/근거를 문서에 함께 남긴다.
- 큰 결정(카탈로그 데이터 전달 방식 등)은 코드 변경 이전에 별도로 합의한다.
