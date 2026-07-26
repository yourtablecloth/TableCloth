<!--
  이슈 #304 제보자에게 게시한 2차 댓글 전문 (기록용).
  게시: 2026-07-26
  URL : https://github.com/yourtablecloth/TableCloth/issues/304#issuecomment-5083995509
  내용: SAC 가설 기각 정정 + citool 수정 안내 + 테스트 빌드(CI 아티팩트) 전달.
        아티팩트는 5일 후 만료되므로 링크는 곧 죽는다. 재전달이 필요하면
        브랜치를 다시 푸시해 CI 를 돌리고 TableCloth-Portable-<arch> 링크를 새로 잡을 것.
-->
보내주신 결과 덕분에 원인 후보가 많이 좁혀졌습니다. 특히 세 번째 댓글의 `citool` 관찰은 저 혼자서는 못 찾았을 내용입니다. 감사합니다.

먼저 정정드릴 것이 있습니다. 제가 처음에 가장 유력하게 봤던 **Smart App Control은 원인이 아니었습니다.** 보내주신 자료를 보면 `VerifiedAndReputablePolicyState` 값이 `0x2`, 즉 "평가" 모드라서 프로그램을 차단하지 않는 상태였습니다. 실제로 `TableCloth.exe spork`를 직접 실행하셨을 때 정상적으로 떴고, 진단 로그의 CodeIntegrity 이벤트에도 차단 기록이 전혀 없었습니다. 이슈 #256과는 다른 문제입니다. 엉뚱한 방향으로 안내드렸던 점 죄송합니다.

대신 찾아주신 `citool` 쪽이 훨씬 유력합니다. `citool.exe --refresh`가 "계속하려면 Enter 키를 누르세요."에서 멈춘다는 게 왜 치명적이냐면, `StartupScript.cmd`에서 **멈출 수 있는 줄이 저 한 줄뿐이고 바로 다음 줄이 Spork를 실행하는 줄**이기 때문입니다. 게다가 원래 스크립트는 저 줄의 출력을 감춰두기 때문에 프롬프트조차 화면에 나타나지 않습니다. 표준 입력을 비워서 프롬프트가 떠도 그냥 지나가도록 수정했습니다.

다만 솔직하게 말씀드리면, **제 PC에서는 이 대기 현상이 재현되지 않았습니다.** 같은 Windows Sandbox 앱 버전(0.8.107.0)으로 여러 번 돌려봤지만 `citool`이 0.5초 안에 끝나고 Spork까지 정상 실행됐습니다. 그래서 이 수정으로 해결될 수도 있지만 확신할 수는 없는 상태입니다.

그래서 이번에 드리는 테스트 빌드에는 수정과 함께 **부팅 과정을 파일로 기록하는 기능**을 넣었습니다. 지금까지는 Spork가 뜨기 전에 실패하면 아무 흔적도 남지 않았는데, 이제는 이런 식으로 남습니다.

```
[00] startup script begin ...
[01] sac policy rc=0
[02] browser policies applied rc=0
[03] citool refresh begin ...
[04] citool refresh end rc=0 ...
[05] launching spork ...
[06] spork exited rc=...
```

이 기록이 어디서 끊겼는지가 곧 실패 지점이라, 한 번만 실행해 주시면 원인이 확정됩니다.

빌드는 아래 링크에서 받으실 수 있습니다. GitHub에 로그인된 상태여야 내려받아지고, CI 산출물이라 5일 뒤 자동으로 삭제됩니다.

**[TableCloth-Portable-x64 내려받기](https://github.com/yourtablecloth/TableCloth/actions/runs/30206595695/artifacts/8633321708)** (약 101MB)

1. 링크에서 `TableCloth-Portable-x64` 를 내려받아 압축을 풉니다.
2. 그 안의 `TableCloth_1.20.8.0_Release_x64_Portable.zip` 을 다시 풀어주세요. 지금 쓰시는 식탁보 설치본은 건드리지 않고 폴더에서 바로 실행되는 형태입니다.
3. 풀린 폴더의 `TableCloth.exe` 를 실행합니다. 코드 서명이 없는 빌드라 "Windows의 PC 보호" 경고가 뜨는데, **추가 정보 → 실행**을 눌러주세요.
4. 평소처럼 샌드박스를 실행합니다.

샌드박스를 실행하신 뒤에는 Spork가 뜨든 안 뜨든, 실제 PC의 `문서\TableCloth\Data\tablecloth-boot.log` 파일을 이 이슈에 첨부해 주시면 됩니다. 식탁보 옵션에서 데이터 폴더를 따로 지정하셨다면 그 폴더 안에 있습니다.

그리고 **Spork가 정상적으로 떴다면 그 사실도 꼭 알려주세요.** 그러면 위 `citool` 수정만으로 해결된 것이 되므로 그대로 정식 릴리스에 반영하겠습니다.

번거로운 부탁을 계속 드리게 되어 죄송합니다. 보내주시는 자료마다 조사가 한 단계씩 진전되고 있어서 정말 큰 도움이 되고 있습니다. 🙇

<sub>관련 PR: #305</sub>


