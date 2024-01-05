#language: ko-KR
기능: ResourceResolver

기능에 대한 간단한 설명을 이곳에 남깁니다.
한국어 문법 참고 - https://velog.io/@clarekang/cucumber-kr-introduce

시나리오: 카탈로그 문서를 불러온다.
만일 a.a. 카탈로그 문서를 불러오는 함수를 호출하면
그러면 a.b. 카탈로그 문서에 1개 이상의 사이트 정보가 들어있다.
그리고 a.c. 마지막으로 카탈로그를 불러온 날짜와 시간 정보를 확인할 수 있다.

시나리오: 프로그램의 최신 정보를 가져온다.
먼저 b.a. 다음의 리포지터리에서 정보를 가져오려 한다.
  | 소유자 이름    | 리포지터리 이름   |
  | yourtablecloth | TableCloth        |
만일 b.b. 버전 정보를 가져오는 함수를 호출하면
그러면 b.c. GitHub에 출시한 최신 버전 정보를 반환한다.
