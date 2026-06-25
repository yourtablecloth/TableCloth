# 모드 2 (단독 Spork · 무설치 코어) 진척 관리

> 시작일: 2026-06-25
> 관련 선행 작업: [UNIFIED_BINARY_TODO.md](UNIFIED_BINARY_TODO.md) (Spork.App 추출 / 단일 바이너리 / 단독 패키징)

## 배경과 목표

식탁보의 NPKI 공동인증서 처리에 대한 보안 검토에서 출발했다. 핵심 통찰은 **VM이 항상 신뢰
경계이고, 호스트는 절대 인증서 보관자가 되지 않는다**는 것이다. 호스트 전체를 통제하는 별도
"인증서 수문장"은 한국 금융 보안모듈이 NPKI를 고정 경로에서 직접 읽는 폐쇄형 생태계 특성상
필터 드라이버급 투자가 필요해 1인 운영 프로젝트엔 부적합하다고 결론냈다. 대신 그 욕구를
**사용자가 직접 만든 VM에서 도는 단독 Spork**(모드 2)로 받아낸다.

```text
모드 1  식탁보 + Windows Sandbox      일회용 VHD. 인증서 일시 주입, 세션 종료 시 소멸 (최강).
모드 2  단독 Spork + 사용자 구축 VM   사용자가 신뢰·관리하는 환경. 인증서 at-rest는 사용자 책임.
```

| | 모드 1 — 식탁보 + WSB | 모드 2 — 단독 Spork + 사용자 VM |
|---|---|---|
| VM 성격 | 일회용(세션 종료 시 VHD 파괴) | 영속(사용자가 직접 구축·관리) |
| 인증서 주입 | 호스트가 staging→마운트→[SandboxBootstrap](../src/Spork.Sandbox/SandboxBootstrap.cs)이 NPKI 배치 | 없음 — 사용자가 VM에 직접 넣음, Spork는 스캔만 |
| 인증서 잔존 | 세션 후 어디에도 안 남음 | VM 디스크에 영속 — **사용자 책임** |
| 보안 강도 | 최강(격리+비영속) | 사용자가 선택한 trade-off |

## 모드 2의 두 사용 레인

"마운트 없음"(아래 패턴 B의 보안 결정) 때문에 두 레인이 깨끗하게 갈린다. WSB는 USB
passthrough를 지원하지 않으므로(파일 공유는 오직 `MappedFolders`로만), 마운트를 빼면 파일 기반
NPKI가 무설치 WSB에 들어올 길이 없다 → 그쪽의 자연스러운, 사실상 유일한 깨끗한 경로는 **모바일
인증**이고, 그건 파일이 아예 없으니 원래 우려를 근본적으로 해소한다.

| | 영속 사용자 VM (Hyper-V/VMware…) | 무설치 WSB (마운트 없음) |
|---|---|---|
| 인증서 파일 반입 | USB/공유폴더 자유 | 불가(마운트·USB passthrough 없음) |
| 모바일 인증 | 가능 | **자연 기본값** |
| 인증서 잔존 | VM 디스크 — 사용자 책임 | **없음**(파일 자체가 없음) |
| Spork 공급 | 사용자가 직접 설치 | 부팅 시 self-contained zip 다운로드 |

- **패턴 A — 영속 사용자 VM**: USB/공유폴더로 인증서를 직접 반입하거나 모바일 인증 사용.
  인증서가 VM 디스크에 상주하지만, 이는 사용자가 비영속성을 포기하고 영속성·커스터마이징을
  택한 대가로 **본인이 떠안는 명시적 opt-in 위험**이다(VM 디스크 BitLocker, 스냅샷에 인증서
  포함 금지 등 권고). Spork는 자신이 만들지도 파괴하지도 않는 VM의 at-rest 보안을 책임지지 않는다.
- **패턴 B — 무설치 WSB 코어**: `.wsb`가 XML이라는 점을 이용해 TableCloth 호스트 앱 없이
  브라우저 + WSB만으로 단독 Spork를 코어로 돌린다. `.wsb`에 **폴더 마운트를 처음부터 넣지
  않아** 호스트 유출 벡터를 제거한다. 다운로드한 `.wsb`의 실행 여부 판단은 일반 SW 설치와 동일한
  가치중립적 결정으로, 사용자·조직 정책(도메인 제한 등) 영역이며 제작자 스코프 밖이다.

## 콜드부트 준비성 감사 결과

단독 Spork가 `SporkAnswers.json` · `App\certs` · 카탈로그 스냅샷 마운트가 **전혀 없는**
네트워크-only 바 WSB에서 끝까지 뜨는지 점검했다. 결과: **유일한 차단 요인은 인증서 스캐너의
하드코딩 경로뿐**이었고, 나머지는 이미 콜드부트 안전하다.

- [x] **[NoopSandboxBootstrap](../src/Spork.App/Components/Implementations/NoopSandboxBootstrap.cs)** — no-op. staging 의존 없음.
- [x] **SporkAnswers.json** — [ApplySporkAnswersIfPresent](../src/Spork.App/DependencyInjection/UseSporkExtensions.cs#L160)는 완전 best-effort. 파일 없으면 `answer` null → VM 기본 컬처로 폴백, 크래시 없음. (UI locale 전용)
- [x] **카탈로그** — [LoadCatalogDocumentAsync](../src/Spork.App/Components/Implementations/ResourceCacheManager.cs#L29)는 네트워크 우선 → `AppContext.BaseDirectory\catalog\catalog.xml` 스냅샷 폴백. 호스트 주입 스냅샷 부재는 네트워크가 살아있으면 무해. (작업 6으로 오프라인까지 보강)
- [x] **단일 파일 경로 안전** — `AppContext.BaseDirectory` 사용(`Assembly.Location` 4건은 UNIFIED_BINARY 단계에서 이미 교체 완료).
- [ ] **인증서 스캐너** — `WDAGUtilityAccount` LocalLow 경로 하드코딩 → 사용자 VM(임의 계정명)에선 아무것도 못 찾음. **작업 2에서 해소.**

## 다운로드 자산명 계약 (공개 contract)

무설치 웹앱(yourtablecloth.app 등)이 의존하는 **공개 계약**. [build.cs](../build.cs)의 rename
규칙이 이 이름을 만든다. 변경 시 웹앱 다운로더가 조용히 깨지므로 신중히 다룬다.

- 포터블 산출물: `Spork_<4파트버전>_<config>_<platform>_Portable.zip`
  - 예: `Spork_1.20.1.0_Release_x64_Portable.zip`
  - `<platform>` ∈ { `x64`, `arm64` }, `<config>` = `Release`(배포용)
- 설치형 산출물(무설치 아님, 참고): `Spork_<4파트버전>_<config>_<platform>.exe`
- Velopack 채널: `spork-<platform>` (메타데이터 `releases.spork-<platform>.json` 등)
- 산출물은 **self-contained 단일 파일**이라 바 WSB(런타임/마운트 없음)에서 압축 해제 후 즉시 실행 가능.
- "최신 릴리스 태그 확인 → 위 자산 URL 해석"은 **웹앱 로직(본 repo 스코프 밖)**. 웹앱은 GitHub
  Releases API로 최신 릴리스를 찾아 `Spork_*_<platform>_Portable.zip` 자산을 매칭한다.

## 무설치 `.wsb` 템플릿 + 부트스트랩 규약

- [tools/no-install/no-install-spork.wsb](../tools/no-install/no-install-spork.wsb) — 마운트 0개,
  네트워킹 Enable, vGPU Disable. `LogonCommand`는 `__SPORK_BOOTSTRAP_URL__`에서 부트스트랩
  스크립트를 받아 실행. 웹앱이 이 자리표시자를 호스팅된 스크립트 URL로 치환해 서빙한다.
- [tools/no-install/spork-bootstrap.ps1](../tools/no-install/spork-bootstrap.ps1) — 참조 부트스트랩:
  (1) 다운로드 전 공용 DNS(8.8.8.8/1.1.1.1) 보정 — **닭-달걀 주의**: Spork 안의 DNS 보정은 Spork를
  받은 *뒤*에 도므로, 받기 *전에* 부트스트랩이 먼저 세팅한다. (2) `__SPORK_PORTABLE_ZIP_URL__`에서
  포터블 zip 다운로드 → 압축 해제 → 실행.
- 신뢰 모델: `.wsb`는 마운트가 없어 호스트 파일 접근 권한을 갖지 않는다. 다운로드 신뢰는
  통제된 HTTPS 오리진 + 체크섬으로 관리(웹앱 책임).

## 작업 항목

- [x] **1. 계획 문서** — 본 문서.
- [ ] **2. 스캐너 일반화 (keystone)** — [X509CertScanner](../src/Spork.App/Components/Implementations/X509CertScanner.cs)를
  후보 집합 스캔으로: 실제 LocalLow NPKI + WSB canonical + `Desktop\NPKI` 마운트 + 제거식
  드라이브(USB), CertHash dedup. WSB 안에선 LocalLow가 canonical과 동일 경로로 풀려 **모드 1
  비회귀**. 인터페이스 메서드명을 WSB 가정에서 일반화(`ScanSandboxNpkiCertificates` →
  `ScanLocalNpkiCertificates`).
- [ ] **3. 콜드부트 준비성** — 위 감사대로 작업 2 외 추가 코드 불필요. 본 문서에 결과 기록(완료).
- [ ] **4. 다운로드 자산명 계약 문서화** — 위 "다운로드 자산명 계약" 절 + [build.cs](../build.cs)
  rename 지점에 "공개 계약" 주석.
- [ ] **5. 무설치 `.wsb` 템플릿 + 부트스트랩** — 위 두 파일.
- [ ] **6. (선택) 카탈로그 스냅샷 동봉** — [build.cs](../build.cs)에서 Spork publish 후
  `external/TableClothCatalog/docs/Catalog.xml`을 `<publish>/catalog/catalog.xml`로 복사해 포터블
  산출물에 포함 → 무설치/오프라인 폴백.

## 별도 권장: 모바일 인증 우선 문서화

무설치 WSB 레인에선 모바일 인증이 자연 기본값이자 **가장 안전**(파일 자체가 없음)하다. 보안
민감 사용자 대상 안내에서 이를 기본 권장 경로로 포지셔닝한다(README/배포 페이지). 코드 변경 없음.

## 스코프 밖 (별도 웹 애플리케이션)

- GitHub 최신 릴리스 태그 확인 → 포터블 zip URL 해석.
- `.wsb` / 부트스트랩 스크립트의 자리표시자 치환 + HTTPS 호스팅 + 체크섬 공개.
- 다운로드한 `.wsb` 실행 허용 여부 정책(도메인 제한 등).

## 남은 열린 결정

- 패턴 A에서 단독 Spork에 **인증서 가져오기 UI**를 둘지, 스캔 전용으로 둘지(= 인증서 반입을
  사용자 셋업 단계로 규정).
- 모드 2에 **세션 종료 시 정리(wipe) 옵션**을 *선택적으로* 줄지(기본 권장: 두지 않음 — VM은 사용자 것).
