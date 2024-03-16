# 식탁보 (TableCloth) - 컴퓨터를 안전하게 사용하세요!

> 알립니다: 물가 상승과 Authenticode 수령 정책 변경으로 Authenticode 인증서 갱신 비용이 너무 높아져서 Codesign 인증서를 애플리케이션 바이너리에 적용이 불가한 상황입니다. SmartScreen 경고가 나타나거나, 실행 중인 안티바이러스 소프트웨어에 의해 식탁보가 자동으로 종료되거나 제거될 수 있으니 사용 시 참고 부탁드립니다.

> 알립니다: Microsoft Store 정책 변경으로 인하여 식탁보 최신 버전 업데이트가 어렵게 되었습니다. Microsoft Store 버전 식탁보 앱은 삭제하고, GitHub에서 릴리즈하는 버전으로 재설치하면 최신 기능을 안정적으로 이용하실 수 있습니다.

* [한국어 소개](README.md)
* [영어 (English) 소개](README.EN.md)

[![식탁보 프로젝트 빌드 상황](https://github.com/dotnetdev-kr/TableCloth/actions/workflows/build.yml/badge.svg)](https://github.com/yourtablecloth/TableCloth/actions)
[![식탁보 최신 버전 다운로드](https://img.shields.io/github/v/release/yourtablecloth/tablecloth)](https://github.com/yourtablecloth/TableCloth/releases)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE.txt)
[![Chocolatey](https://img.shields.io/badge/chocolatey-install-orange)](https://community.chocolatey.org/packages/tablecloth)
[![Winget](https://img.shields.io/badge/winget-install-purple)](https://winstall.app/apps/TableClothProject.TableCloth)

![식탁보 실행 화면](docs/images/TableCloth.png)

## 개요

이 프로젝트는 윈도우 10 버전 1909부터 추가된 윈도우 샌드박스를 활용하여, 컴퓨터에서 인터넷 뱅킹을 사용하거나, 전자정부 인터넷 서비스를 사용할 때 설치되는 여러가지 클라이언트 보안 프로그램을 실제 컴퓨터 환경에 영향을 주지 않고 사용할 수 있도록 도와주는 프로그램입니다.

보안을 명목으로 설치되는 여러가지 에이전트, 가상 키보드, 중간 암호화 프로그램들은 그 나름대로의 의미가 있습니다. 하지만 계속해서 변화하는 웹 생태계, 윈도우 운영 체제의 요구 사항을 제대로 반영하지 못하는 웹 사이트가 여전히 많습니다. 그로 인해 보안과 안정성을 추구해야 할 보조 소프트웨어들이 오히려 시스템의 성능을 저하시키거나 때로는 윈도우 운영 체제를 파괴하는 일도 발생합니다.

이런 문제를 완화하고, 컴퓨터를 항상 안정적인 상태로 유지할 수 있도록 도와주기 위하여 이 프로젝트를 시작하게 되었습니다.

## 일반적인 설치와 사용 방법 안내

일반적인 설치와 사용 방법 안내는 [식탁보 홈페이지](https://yourtablecloth.github.io)에서 소개하고 있습니다.

## 빌드 환경

* Visual Studio 2022 이상
* .NET 8.0 SDK
* .NET Framework 4.8 SDK

## 테스트 환경

* Windows 10 1909 이상
* 지원되는 SKU: Pro, Edu and Enterprise
* 반드시 Windows Sandbox를 실행할 수 있는 환경이어야 합니다.

## [개발자 가이드](./DEVREADME.md)

## 스폰서

GitHub Sponsorship을 통하여 후원해주시면 지속적으로 프로젝트를 진행하는데에 큰 도움이 됩니다. [프로젝트 후원하러 가기](https://github.com/sponsors/yourtablecloth)

## 저작권 정보

<img width="100" alt="Tablecloth Icon by Icons8" src="docs/images/TableCloth_NewLogo.png" /> by [Icons8](https://img.icons8.com/color/96/000000/tablecloth.png)

<img width="100" alt="Spork Icon by Freepik Flaticon" src="docs/images/Spork_NewLogo.png" /> by [Freepik Flaticon](https://www.flaticon.com/free-icon/spork_5625701)
