# 식탁보 (TableCloth) - 컴퓨터를 안전하게 사용하세요

> 알립니다: 물가 상승과 Authenticode 수령 정책 변경으로 Authenticode 인증서 갱신 비용이 너무 높아져서 Codesign 인증서를 애플리케이션 바이너리에 적용이 불가한 상황입니다. SmartScreen 경고가 나타나거나, 실행 중인 안티바이러스 소프트웨어에 의해 식탁보가 자동으로 종료되거나 제거될 수 있으니 사용 시 참고 부탁드립니다.

> 알립니다: Microsoft Store 정책 변경으로 인하여 식탁보 최신 버전 업데이트가 어렵게 되었습니다. Microsoft Store 버전 식탁보 앱은 삭제하고, GitHub에서 릴리즈하는 버전으로 재설치하면 최신 기능을 안정적으로 이용하실 수 있습니다.

> 알립니다: 1.13.0 버전 출시와 함께 식탁보 프로젝트는 듀얼 라이선스 모델로 변경하였습니다. AGPL 3.0 또는 상용 라이선스 중 택할 수 있습니다. 상용 라이선스 사용 시, 라이선스 파일에 기재된 연락처로 연락 부탁드립니다.

* [한국어 소개](README.md)
* [영어 (English) 소개](README.EN.md)

[![식탁보 프로젝트 빌드 상황](https://github.com/yourtablecloth/TableCloth/actions/workflows/build.yml/badge.svg)](https://github.com/yourtablecloth/TableCloth/actions)
[![식탁보 Discord](https://img.shields.io/discord/1443777680418930761?label=Discord&logo=discord&color=7289DA)](https://discord.gg/eT2UnUXyTV)
[![식탁보 최신 버전 다운로드](https://img.shields.io/github/v/release/yourtablecloth/tablecloth)](https://github.com/yourtablecloth/TableCloth/releases)
[![UniGetUI에서 다운로드](https://img.shields.io/badge/UniGetUI-TableCloth-blue)](https://marticliment.com/unigetui/share?name=TableCloth&id=TableClothProject.TableCloth&sourceName=winget&managerName=WinGet)

![식탁보 실행 화면](docs/images/TableCloth.png)

## 개요

이 프로젝트는 윈도우 샌드박스를 활용하여, 컴퓨터에서 인터넷 뱅킹을 사용하거나, 전자정부 인터넷 서비스를 사용할 때 설치되는 여러가지 클라이언트 보안 프로그램을 실제 컴퓨터 환경에 영향을 주지 않고 사용할 수 있도록 도와주는 프로그램입니다.

보안을 명목으로 설치되는 여러가지 에이전트, 가상 키보드, 중간 암호화 프로그램들은 그 나름대로의 의미가 있습니다. 하지만 계속해서 변화하는 웹 생태계, 윈도우 운영 체제의 요구 사항을 제대로 반영하지 못하는 웹 사이트가 여전히 많습니다. 그로 인해 보안과 안정성을 추구해야 할 보조 소프트웨어들이 오히려 시스템의 성능을 저하시키거나 때로는 윈도우 운영 체제를 파괴하는 일도 발생합니다.

이런 문제를 완화하고, 컴퓨터를 항상 안정적인 상태로 유지할 수 있도록 도와주기 위하여 이 프로젝트를 시작하게 되었습니다.

## 설치와 사용 방법 안내

식탁보를 손쉽게 설치하고 사용하기 위해서는 UniGetUI 또는 Winget을 통한 설치 방법을 권장합니다.

1. Windows 11 Pro, Education, Enterprise SKU 이상의 OS를 설치합니다.

2. Windows Sandbox 옵션을 활성화합니다.

3. <https://apps.microsoft.com/detail/XPFFTQ032PTPHF?hl=ko&gl=KR&ocid=pdpshare> 에서 최신 버전의 UniGetUI를 설치합니다.

4. <https://marticliment.com/unigetui/share/?name=TableCloth&id=TableClothProject.TableCloth&sourceName=winget&managerName=WinGet> 에서 최신 버전의 식탁보 패키지를 설치합니다.

만약 winget 명령줄을 사용하는 것이 익숙하다면 아래와 같이 설치하실 수 있습니다.

```powershell
winget install TableClothProject.TableCloth
```

## 웹 사이트 정보 수정 안내

식탁보에서 접속할 수 있는 특정 웹 사이트와 관련된 문제는 다음 중 한 가지 방법을 통하여 제보 또는 기여를 부탁드립니다.

* **권장**: [식탁보 카탈로그 리포지터리에 이슈 등록 또는 PR 제출](https://github.com/yourtablecloth/TableClothCatalog)
* [Google Forms를 통한 제보](https://forms.gle/Pw6pBKhqF1e5Nesw6)
* [Discord 채널을 통한 제보/토론](https://discord.gg/eT2UnUXyTV)

## 빌드 환경

* Visual Studio 2026 이상
* .NET 10.0 SDK
* .NET Framework 4.8 SDK

## 테스트 환경

* Windows 11 25H2 이상
* 지원되는 SKU: Pro, Edu and Enterprise
* 반드시 Windows Sandbox를 실행할 수 있는 환경이어야 합니다.

## [개발자 가이드](./DEVREADME.md)

## 스폰서

GitHub Sponsorship을 통하여 후원해주시면 지속적으로 프로젝트를 진행하는데에 큰 도움이 됩니다. [프로젝트 후원하러 가기](https://github.com/sponsors/yourtablecloth)

## 수상

### 2024년 9월

[![Product of the Week, Recognized by disquiet.io](docs/images/disquiet_product_of_the_week.jpeg)](https://disquiet.io/product/%EC%8B%9D%ED%83%81%EB%B3%B4)

## 법적 고지

### 저작권 (Copyright)

**식탁보 (TableCloth)** 소프트웨어는 대한민국 저작권법에 따라 보호받는 저작물입니다.

* **저작권 등록번호**: C-2025-051228
* **등록일**: 2025년 11월 21일
* **저작권자**: rkttu.com
* **조회**: [한국저작권위원회 CROS 포털](https://www.cros.or.kr)에서 등록번호로 검색

© 2021-2026 rkttu.com. All rights reserved.

### 상표권 (Trademark)

**'식탁보'** 명칭은 대한민국 특허청에 상표 출원된 등록 상표입니다.

* **출원번호**: 4020240205929
* **출원공고일**: 2025년 3월 17일
* **상표권자**: rkttu.com
* **조회**: [KIPRIS (특허정보검색서비스)](https://www.kipris.or.kr)에서 출원번호로 검색

'식탁보' 명칭의 상업적 사용은 상표권자의 허가가 필요합니다.

### 라이선스 (License)

본 프로젝트는 **듀얼 라이선스** 모델을 채택하고 있습니다:

1. **AGPL 3.0**: 오픈소스 프로젝트 및 비상업적 사용
2. **상용 라이선스**: 상업적 사용 시 별도 문의 필요

자세한 내용은 [LICENSE-AGPL](./LICENSE-AGPL) 파일을 참조하세요.

## 이미지 저작권 정보

<img width="100" alt="Tablecloth Icon by Icons8" src="docs/images/TableCloth_NewLogo.txt" /> by [Icons8](https://img.icons8.com/color/96/000000/tablecloth.png)

<img width="100" alt="Spork Icon by Freepik Flaticon" src="docs/images/Spork_NewLogo.txt" /> by [Freepik Flaticon](https://www.flaticon.com/free-icon/spork_5625701)

<img width="100" alt="Sponge Icon by Freepik Flaticon" src="docs/images/Sponge_NewLogo.txt"/> by [Freepik photo3idea_studio](https://www.flaticon.com/free-icons/sponge)
