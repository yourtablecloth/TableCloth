# 보안 정책

## 식탁보 보안에 대하여

식탁보는 보안 프로그램이 필요한 웹사이트(주로 한국의 인터넷 뱅킹 및 전자정부 서비스)를 Windows Sandbox 내에서 실행하여 보안을 강화하는 Windows 데스크톱 애플리케이션입니다. 이러한 격리 환경 접근 방식은 보안 소프트웨어가 호스트 시스템에 영향을 주는 것을 방지하여 근본적으로 보안을 향상시킵니다.

### 보안 아키텍처

- **Windows Sandbox 격리**: 보안에 민감한 작업을 일회용 격리된 Windows Sandbox 환경에서 실행
- **빌드 출처 증명**: 모든 릴리스에 GitHub Attestations와 SBOM(소프트웨어 자재 명세서) 포함
- **공급망 보안**: SBOM으로 모든 의존성을 추적하여 투명성과 취약점 관리 지원
- **듀얼 라이선스**: AGPL 3.0 또는 상용 라이선스로 코드 투명성 보장

## 지원되는 버전

식탁보는 롤링 릴리스 모델을 따릅니다. 다음 버전에 대해 보안 업데이트를 제공합니다:

| 버전 | 지원 여부 | 상태 |
| ------- | ------------------ | ------ |
| 1.16.x | :white_check_mark: | 현재 안정 버전 |
| 1.15.x | :white_check_mark: | 보안 업데이트만 제공 |
| < 1.15 | :x: | 더 이상 지원하지 않음 |

**권장 사항**: 항상 [GitHub Releases](https://github.com/yourtablecloth/TableCloth/releases)에서 최신 버전을 사용하거나 WinGet을 통해 설치하세요:

```powershell
winget install TableClothProject.TableCloth
```

### Windows 운영체제 지원 정책

**중요**: 식탁보는 Windows Sandbox 기능에 의존합니다. Microsoft는 2025년 10월 14일부로 Windows 10의 지원을 종료했습니다. 이에 따라:

- ✅ **Windows 11**: 전체 보안 지원 및 업데이트 제공
- ⚠️ **Windows 10**: 더 이상 보안 고려 대상이 아니며, 보안 이슈에 대한 수정 및 지원 제공하지 않음

Windows 10 사용자는 보안을 위해 Windows 11로 업그레이드할 것을 강력히 권장합니다. Windows 10에서 발생하는 보안 취약점은 Microsoft의 공식 지원 종료로 인해 해결되지 않을 수 있습니다.

## 보안 기능

### 빌드 보안

- **SBOM 생성**: 모든 릴리스에 전체 의존성 추적을 위한 SPDX 형식 SBOM 포함
- **출처 증명**: Sigstore를 통한 GitHub Actions 빌드 출처 증명
- **자동 취약점 스캔**: 알려진 취약점에 대한 의존성 모니터링

### 런타임 보안

- **샌드박스 격리**: 잠재적으로 위험한 모든 작업이 Windows Sandbox 내에서 실행됨
- **영구적 변경 없음**: 각 세션 후 샌드박스 환경 파괴
- **최소한의 호스트 영향**: 보안 소프트웨어는 격리된 샌드박스 환경에만 영향을 미침

## 취약점 보고

우리는 보안을 매우 중요하게 생각합니다. 식탁보에서 보안 취약점을 발견하신 경우, 책임감 있는 공개 관행을 따라 도움을 주시기 바랍니다.

### 보고 방법

**심각/높음 수준의 이슈:**

1. 공개 GitHub 이슈를 생성하지 **마세요**
2. GitHub Security Advisories 사용: [취약점 보고](https://github.com/yourtablecloth/TableCloth/security/advisories/new)
3. 또는 직접 이메일: [LICENSE-COMMERCIAL 파일의 연락처 정보 참조]

**중간/낮음 수준의 이슈:**

- GitHub 이슈 생성: [새 이슈](https://github.com/yourtablecloth/TableCloth/issues/new)
- `security` 라벨 태그
- 상세 정보 제공 (아래 참조)

### 필요한 정보

보고서에 다음 내용을 포함해 주세요:

1. **취약점 설명**
   - 보안 이슈에 대한 명확한 설명
   - 잠재적 영향 및 심각도 평가
   - 영향받는 버전 (알고 있는 경우)

2. **재현 단계**
   - 상세한 단계별 지침
   - 개념 증명 코드 또는 스크린샷
   - 환경 세부 정보 (Windows 버전, 식탁보 버전 등)

3. **완화 방안 (선택 사항)**
   - 제안하는 수정 사항 또는 임시 해결책
   - 관련 CVE 또는 보안 권고

### 응답 타임라인

다음 타임라인에 따라 보안 보고에 응답하는 것을 목표로 합니다:

| 심각도 | 초기 응답 | 상태 업데이트 | 수정 목표 |
| -------- | ---------------- | ------------- | ---------- |
| 심각 | 24시간 | 48시간 | 7일 |
| 높음 | 48시간 | 5일 | 14일 |
| 중간 | 5일 | 14일 | 30일 |
| 낮음 | 7일 | 30일 | 다음 릴리스 |

**참고**: 이는 목표 타임라인입니다. 실제 응답은 이슈의 복잡성과 관리자의 가용성에 따라 달라질 수 있습니다.

## 사용자를 위한 보안 모범 사례

식탁보 사용 시:

1. **최신 버전 유지**: 항상 최신 버전 사용
2. **다운로드 확인**:
   - 공식 소스(GitHub Releases 또는 WinGet)에서만 다운로드
   - 공급망 보안을 위해 SBOM 및 출처 증명 확인
3. **Windows Sandbox 요구 사항**:
   - **Windows 11 Pro/Education/Enterprise 사용 권장** (필수)
   - Windows 10은 Microsoft 지원 종료로 인해 보안 고려 대상에서 제외됨
   - 최신 샌드박스 보안 패치를 위해 Windows 업데이트 유지
4. **SmartScreen 경고**:
   - 코드 서명 인증서 비용 문제로 식탁보 바이너리는 서명되지 않음
   - 주의를 기울이고 공식 소스에서 다운로드 확인
5. **안티바이러스 소프트웨어**: 일부 안티바이러스가 서명되지 않은 바이너리를 차단할 수 있음 - 필요시 예외 추가

## 보안 감사

식탁보는 오픈소스(AGPL 3.0)이며 보안 감사를 환영합니다:

- 소스 코드: [GitHub 리포지터리](https://github.com/yourtablecloth/TableCloth)
- 빌드 프로세스: [GitHub Actions 워크플로](.github/workflows/)
- 의존성: 각 릴리스의 SBOM 파일 확인

## 보안 업데이트

보안 업데이트는 다음을 통해 공지됩니다:

- GitHub Security Advisories
- GitHub Releases (`security` 태그 포함)
- 프로젝트 README
- Discord 서버: [Discord 참여](https://discord.gg/eT2UnUXyTV)

## 공로 인정

책임감 있게 취약점을 공개한 보안 연구자를 인정하고 공로를 표시합니다. 허락하시면:

- 릴리스 노트에 감사 표시
- 보안 명예의 전당에 추가
- 프로필/웹사이트 링크 (제공된 경우)

## 궁금한 점이 있으신가요?

일반적인 보안 질문(취약점 보고가 아닌):

- [GitHub Discussion](https://github.com/yourtablecloth/TableCloth/discussions) 열기
- [Discord 서버](https://discord.gg/eT2UnUXyTV) 참여
- 문서 및 기존 이슈 확인

---

**최종 업데이트**: 2026년 1월 15일  
**문서 버전**: 2.0
