# 식탁보 (Table Cloth) 프로젝트

![식탁보 프로젝트 빌드 상황](https://github.com/dotnetdev-kr/TableCloth/actions/workflows/dotnet-desktop.yml/badge.svg)

[(Prerelease) 식탁보 0.5.0 출시했습니다! 🥳](https://github.com/dotnetdev-kr/TableCloth/releases/tag/v0.5.0)

[![식탁보 프로젝트 소개 영상](http://img.youtube.com/vi/HgHQB0Wp4Go/0.jpg)](https://youtu.be/HgHQB0Wp4Go?t=0s)

## 개요

이 프로젝트는 윈도우 10 버전 1909부터 추가된 윈도우 샌드박스를 활용하여, 컴퓨터에서 인터넷 뱅킹을 사용하거나, 전자정부 인터넷 서비스를 사용할 때 설치되는 여러가지 클라이언트 보안 프로그램을 실제 컴퓨터 환경에 영향을 주지 않고 사용할 수 있도록 도와주는 프로그램입니다.

보안을 명목으로 설치되는 여러가지 에이전트, 가상 키보드, 중간 암호화 프로그램들은 그 나름대로의 의미가 있습니다. 하지만 계속해서 변화하는 웹 생태계, 윈도우 운영 체제의 요구 사항을 제대로 반영하지 못하는 웹 사이트가 여전히 많습니다. 그로 인해 보안과 안정성을 추구해야 할 보조 소프트웨어들이 오히려 시스템의 성능을 저하시키거나 때로는 윈도우 운영 체제를 파괴하는 일도 발생합니다.

이런 문제를 완화하고, 컴퓨터를 항상 안정적인 상태로 유지할 수 있도록 도와주기 위하여 이 프로젝트를 시작하게 되었습니다.

## 사용 방법

이 프로그램은 아직 개발 단계에 있습니다. 이 리포지터리의 소스 코드를 체크아웃하고, 닷넷 5 SDK를 다운로드하고 설치하여 프로그램을 실행하실 수 있습니다.

### Windows Sandbox 설정
 - "Windows 기능 켜기/끄기" 어플리케이션을 실행합니다.
 - "Windows 샌드박스" 기능을 겹니다.

![Windows 기능 켜기/끄기](https://user-images.githubusercontent.com/979297/130183566-dca3bd81-a76e-42bb-bfb3-37b7e8a7370c.png)
![windows sandbox 켬](https://user-images.githubusercontent.com/979297/130183620-1b3376c6-a887-42fe-b925-1991a7b00434.png)


## 기여 방법

이 프로그램은 카탈로그 파일을 통해 각 인터넷 뱅킹 및 전자 정부 사이트에 접속할 때 설치해야 할 소프트웨어들을 자동으로 찾아 다운로드받습니다.

이 때 프로그램을 직접 다운로드받을 수 있는 URL이 바뀌거나, 구 버전으로 다운로드되는 경우가 있을 수 있는데, 이 부분에 대한 컨트리뷰터 여러분의 기여가 필요합니다.

만약 주소가 바뀌었다면 [docs](docs) 폴더의 [Catalog.xml](docs/Catalog.xml) 파일에 정확한 URL을 기재하여 풀 리퀘스트를 보내주세요.

## 스크린 샷

![스크린 샷](TableCloth.png)

## 라이선스

본 프로젝트는 MIT 라이선스를 따릅니다.

Project emblem made by [Eucalyp](https://www.flaticon.com/authors/eucalyp) from [Flaticon](https://www.flaticon.com/).

Photo by [Brooke Lark](https://unsplash.com/@brookelark?utm_source=unsplash&utm_medium=referral&utm_content=creditCopyText) on [Unsplash](https://unsplash.com/s/photos/tablecloth?utm_source=unsplash&utm_medium=referral&utm_content=creditCopyText).

## 참고사항

### VirtualBox Windows Guest
 - VirtualBox 를 통한 Windows 사용시에는 SLAT 기능이 활성화가 필요합니다.
 - macOS 에서 VirtualBox 를 통해 windows 를 사용하시는 분은 사용하실 수 없습니다.

