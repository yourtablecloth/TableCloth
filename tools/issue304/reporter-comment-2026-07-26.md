<!--
  이슈 #304 제보자에게 실제로 게시한 댓글 전문 (기록용).
  게시: 2026-07-26
  URL : https://github.com/yourtablecloth/TableCloth/issues/304#issuecomment-5082595754
  목적 : 다음 세션이 "무엇을 이미 요청했는지" 를 다시 묻지 않도록 원문을 보존한다.
        아래 방법 B 의 스크립트 본문은 tools/diagnose_sandbox_boot.cmd 와 동일 내용이다.
-->
안녕하세요. 상세한 제보와 영상까지 첨부해주셔서 감사합니다. 적극적으로 대응해주시겠다고 하셔서, 원인을 좁힐 수 있는 진단 방법을 정리해 드립니다.

## 먼저, "아무 메시지도 없는" 이유

식탁보는 샌드박스가 부팅되면 바탕 화면의 `App` 폴더에 있는 `StartupScript.cmd`를 실행하고, 그 배치가 마지막에 `TableCloth.exe spork`를 띄우는 구조입니다. 그런데 지금 코드는 **이 실행이 실패해도 결과를 확인하지 않고 배치가 그대로 끝나도록** 되어 있습니다. 콘솔 창이 즉시 닫히기 때문에 오류 문구조차 화면에 남지 않습니다.

즉 "메시지가 하나도 없다"는 것은 특수한 증상이 아니라, **Spork 프로세스가 뜨기도 전에 실패했다**는 신호입니다. (로그가 안 남는 것도 같은 이유입니다. 이 부분은 제 쪽에서 개선하겠습니다.)

바탕 화면에 `App` / `Data` / `NPKI` 폴더가 보이는 것은 폴더 공유가 붙었다는 뜻일 뿐이라, 위 단계가 실행됐는지와는 무관합니다.

현재 가장 유력하게 보는 원인은 **Windows 11의 Smart App Control(SAC)이 서명되지 않은 식탁보 실행 파일을 커널 단에서 차단**하는 경우입니다. SAC는 Windows 11에만 있는 기능이라, "Windows 10에서는 잘 됐는데 업그레이드 후부터"라는 말씀과 시점이 정확히 맞습니다. (관련 선행 이슈: #256) 다만 이 경우 식탁보가 넣어둔 우회 코드가 동작해야 정상인데 그게 안 듣는 것인지, 아니면 **배치 자체가 실행되지 않은 것**인지에 따라 대응이 완전히 달라집니다. 그래서 이 둘을 가르는 확인이 필요합니다.

---

## 방법 A. 30초 확인 (이것만 해주셔도 큰 도움이 됩니다)

1. 평소처럼 식탁보로 샌드박스를 실행합니다.
2. Spork가 뜨지 않는 상태에서, **샌드박스 안에서** `Win` + `R` → `cmd` 입력 → 확인.
3. 열린 검은 창에 아래를 붙여넣고 Enter를 눌러주세요.

```cmd
reg query "HKLM\SOFTWARE\Policies\Microsoft\Edge\LocalNetworkAccessAllowedForUrls" /v 1
reg query "HKLM\SYSTEM\CurrentControlSet\Control\CI\Policy" /v VerifiedAndReputablePolicyState
powershell -c "(Get-MpComputerStatus).SmartAppControlState"
cd /d "%USERPROFILE%\Desktop\App" && TableCloth.exe spork
```

4. 화면에 나온 결과를 **캡처해서** 올려주시면 됩니다.

특히 마지막 줄을 실행했을 때 무엇이 나오는지가 중요합니다. `액세스가 거부되었습니다`, `이 앱은 차단되었습니다` 같은 문구나 대화상자가 뜨면 그것까지 캡처 부탁드립니다.

---

## 방법 B. 전체 진단 (더 정확합니다)

증거를 한 번에 파일로 모으는 스크립트입니다. 결과 파일이 호스트(실제 PC) 쪽에도 그대로 보이므로 첨부하기 편합니다.

1. **실제 PC**에서 `문서` → `TableCloth` → `Data` 폴더를 엽니다. (식탁보 옵션에서 데이터 폴더를 따로 지정하셨다면 그 폴더입니다.)
2. 메모장을 열고 아래 내용을 그대로 붙여넣은 뒤, 그 폴더에 **`diagnose_sandbox_boot.cmd`** 라는 이름으로 저장합니다. (저장할 때 "파일 형식"을 `모든 파일`로 바꿔주세요. `.txt`가 붙으면 실행되지 않습니다.)

<details>
<summary>스크립트 내용 (펼치기)</summary>

```bat
@echo off
setlocal enabledelayedexpansion
set LOG=%~dp0tablecloth-diag.txt
set APPDIR=C:\Users\WDAGUtilityAccount\Desktop\App

echo ==== TableCloth sandbox boot diagnostics (issue #304) ====> "%LOG%"
echo [00] collected at %DATE% %TIME%>> "%LOG%"
whoami>> "%LOG%" 2>&1
ver>> "%LOG%" 2>&1

echo.>> "%LOG%"
echo [01] did the LogonCommand batch run?>> "%LOG%"
reg query "HKLM\SOFTWARE\Policies\Microsoft\Edge\LocalNetworkAccessAllowedForUrls" /v 1>> "%LOG%" 2>&1
reg query "HKLM\SOFTWARE\Policies\Microsoft\Edge" /v HardwareAccelerationModeEnabled>> "%LOG%" 2>&1

echo.>> "%LOG%"
echo [02] Smart App Control state (0=off 1=enforce 2=evaluation)>> "%LOG%"
reg query "HKLM\SYSTEM\CurrentControlSet\Control\CI\Policy" /v VerifiedAndReputablePolicyState>> "%LOG%" 2>&1
powershell -NoProfile -Command "try { 'SmartAppControlState = ' + (Get-MpComputerStatus).SmartAppControlState } catch { 'ERR: ' + $_.Exception.Message }">> "%LOG%" 2>&1
if exist "%SystemRoot%\System32\citool.exe" (echo citool.exe present>> "%LOG%") else (echo citool.exe MISSING>> "%LOG%")

echo.>> "%LOG%"
echo [03] App mount contents>> "%LOG%"
if exist "%APPDIR%\TableCloth.exe" (
  dir "%APPDIR%">> "%LOG%" 2>&1
) else (
  echo   %APPDIR%\TableCloth.exe NOT FOUND>> "%LOG%"
  dir "C:\Users\WDAGUtilityAccount\Desktop">> "%LOG%" 2>&1
)

echo.>> "%LOG%"
echo [04] launch TableCloth.exe spork and check survival>> "%LOG%"
if exist "%APPDIR%\TableCloth.exe" (
  echo Launching TableCloth.exe - if a "blocked" dialog pops up, screenshot it.
  start "" "%APPDIR%\TableCloth.exe" spork
  echo   start errorlevel=!errorlevel!>> "%LOG%"
  ping -n 21 127.0.0.1 >nul
  echo   --- tasklist after 20s --->> "%LOG%"
  tasklist /fi "imagename eq TableCloth.exe">> "%LOG%" 2>&1
) else (
  echo   SKIP - binary not present>> "%LOG%"
)

echo.>> "%LOG%"
echo [05] CodeIntegrity operational log (most recent 30 events)>> "%LOG%"
wevtutil qe Microsoft-Windows-CodeIntegrity/Operational /c:30 /rd:true /f:text>> "%LOG%" 2>&1

echo.>> "%LOG%"
echo [06] Windows PowerShell availability>> "%LOG%"
if exist "%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe" (
  echo   powershell.exe present>> "%LOG%"
  powershell -NoProfile -Command "$PSVersionTable.PSVersion.ToString()">> "%LOG%" 2>&1
  powershell -NoProfile -Command "(Get-ExecutionPolicy -List | Out-String)">> "%LOG%" 2>&1
) else (
  echo   powershell.exe MISSING>> "%LOG%"
)

echo.>> "%LOG%"
echo [07] DNS and catalog reachability>> "%LOG%"
ipconfig /all>> "%LOG%" 2>&1
nslookup yourtablecloth.app>> "%LOG%" 2>&1

echo.>> "%LOG%"
echo ==== done ====>> "%LOG%"

echo.
echo Diagnostics finished. Result: %LOG%
echo.
pause
```

</details>

3. 식탁보로 평소처럼 샌드박스를 실행합니다.
4. Spork가 뜨지 않으면, 샌드박스 안에서 `Win` + `R` → 아래를 붙여넣고 실행합니다.

```
%USERPROFILE%\Desktop\Data\diagnose_sandbox_boot.cmd
```

5. 20초 정도 뒤 `Diagnostics finished` 가 나오면 끝입니다. 실제 PC의 `문서\TableCloth\Data\tablecloth-diag.txt` 파일을 이 이슈에 첨부해주세요.

> 결과 파일에는 샌드박스 안의 네트워크 설정(`ipconfig`)도 포함됩니다. 샌드박스 내부 정보라 민감한 내용은 아니지만, 올리시기 전에 한 번 훑어보시고 불편한 부분이 있으면 지우고 올리셔도 됩니다.

---

## 추가로 알려주시면 좋은 것

실제 PC(샌드박스 밖)에서 PowerShell을 열고 아래를 실행한 결과입니다.

```powershell
Get-AppxPackage -Name "*WindowsSandbox*" | Select-Object Name, Version
reg query "HKLM\SYSTEM\CurrentControlSet\Control\CI\Policy" /v VerifiedAndReputablePolicyState
```

25H2부터 Windows Sandbox가 스토어를 통해 배포되는 앱 형태로 바뀌어서, 버전에 따라 동작이 달라질 여지가 있어 확인하려는 것입니다.

---

번거로운 부탁을 드리게 되어 죄송합니다. 제 PC도 제보해주신 것과 동일한 빌드(25H2 26200.8875)라 재현을 시도해보고 있고, 결과를 주시면 그에 맞춰 대응하겠습니다.

원인과 무관하게, **이렇게 조용히 실패하면 아무 단서도 남지 않는 것 자체가 문제**이므로 다음 버전에서는 부팅 단계 로그를 남기고 실행 실패 시 안내가 뜨도록 개선하겠습니다. 좋은 제보 감사합니다. 🙇

