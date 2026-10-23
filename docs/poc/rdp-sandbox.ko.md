# Windows Sandbox RDP 호스팅 실험과 검증 기록

2026년 10월 23일에 `poc/rdp-activex-host` 브랜치의 리서치를 문서로 보존하고 구현 브랜치를 폐기하기로 결정했습니다. 이 기록은 마지막 커밋 `dcf8cef35d1a8708e1dc8251ae4a08ada7458afd`와 그 이후 미커밋 실험을 대상으로 합니다. `poc/managed-ai-runtime`에는 이 문서와 검증 요약만 남깁니다.

이 문서는 Avalonia와 Native AOT의 ActiveX 호스팅, Windows Sandbox 내부 브로커, 세션과 공유 자산의 수명, 화면 및 키보드 실험, 검증 한계를 다룹니다. 보관한 테스트 결과는 과거 실행의 증거이며 이 문서를 추가하면서 테스트나 VM 실험을 다시 실행하지 않았습니다.

먼저 보존 범위와 내부 통신 계약을 정리하겠습니다. 이어서 세션 제어와 HWND 관리에서 얻은 지식을 설명하고 화면 조정 및 실제 검증 결과를 구분합니다. [영문 기록](rdp-sandbox.md)과 [검증 요약](rdp-sandbox-evidence.json)도 함께 보존합니다.

실험 대상의 버전과 잠정성을 다음과 같이 한정합니다.

> 기준일: 2026년 10월 23일. 내부 API 실험은 Windows 11 x64의 Windows Sandbox 0.8.107.0을 대상으로 했습니다. 비공개 브로커와 RDP 파이프 계약은 패키지 업데이트에 따라 달라질 수 있으며 공개 지원 계약으로 확정하지 않습니다.

## 구현 폐기와 문서 보존 범위

보존할 이력부터 살펴보겠습니다. `poc/managed-ai-runtime`의 기준 커밋은 `b403591ccebc3d6e27ef4bc937592e3539e5bc17`입니다. RDP 브랜치는 그 이후의 `develop` 변경도 포함하므로 브랜치 전체를 병합하면 리서치 외의 변경까지 유입됩니다. 따라서 RDP 소스와 테스트, 옵션 UI, 패키지 참조, 리소스 및 진단용 `.wsb` 파일을 대상 브랜치로 옮기지 않았습니다.

마지막 커밋은 CLI 기반 세션 목록과 시작을 사용했습니다. 이후 미커밋 구현은 내부 브로커의 직접 RPC로 수명 관리 경로를 바꾸고 제품 실행 경로, 단일 세션 UI, 해상도 및 키보드 실험을 추가했습니다. 아래에서 설명하는 메뉴와 진입점, 클래스 이름은 이 폐기한 실험을 식별하며 대상 브랜치에서 제공하는 기능을 뜻하지 않습니다. 기존 AI 기능의 범위는 [Managed AI 보고서](managed-ai-runtime.md)에 남깁니다.

로컬 `Artifacts/`에는 기존 보고서와 화면, 실행 파일이 남아 있습니다. Git은 해당 디렉터리를 무시하므로 원격 저장소나 새 체크아웃에서는 원본 자료를 얻을 수 없습니다. [검증 요약](rdp-sandbox-evidence.json)은 주요 파일의 상대 경로와 SHA-256, 선별한 결과를 보존합니다. 해시만으로 원본 실행을 재현할 수는 없습니다. 원본 로그와 화면, 실행 파일은 문서 커밋에 포함하지 않았습니다.

## 설치 패키지에 종속된 내부 브로커

브로커 연동에서 관찰한 계약을 짚어보겠습니다. 후속 어댑터는 사용자 SID의 UTF-8 MD5를 GUID로 표현해 만든 `wsandbox\{GUID}` 명명된 파이프 위에서 HTTP/2와 gRPC 프레이밍을 사용했습니다. `EnumerateSandboxVMs`, `StartSandbox`, `ShutdownSandbox`, `GetRdpClientConfig`를 호출했으며 브로커가 없을 때에는 설치된 `WindowsSandboxServer.exe` 실행 별칭을 활성화했습니다. 이 후속 경로는 `wsb.exe`를 호출하지 않았습니다.

시작 메시지는 구성 XML을 필드 1에, 요청 GUID를 필드 2에 담았습니다. 필드 100의 `OwnerProcessId`를 생략해 뷰어 종료와 VM 종료를 분리했습니다. 시작 및 RDP 설정 응답에는 자격 증명이 포함되므로 응답 원문과 구성, 자격 증명을 로그나 명령 인자, 예외 메시지에 남기지 않는 경계를 적용했습니다. 파서는 메시지를 1 MiB로 제한하고 엄격한 UTF-8과 HRESULT, GUID 및 응답 프레임을 검사했습니다. 시작 RPC에는 120초, 나머지 RPC에는 30초 제한을 적용했습니다.

호환성 검사는 OS와 프로세스가 모두 x64이며 `MicrosoftWindows.WindowsSandbox_0.8.107.0_x64__cw5n1h2txyewy` 패키지 하나만 등록된 경우로 한정했습니다. 실제 창의 ActiveX 인터페이스와 네이티브 파이프 연결기를 준비하고 읽기 전용 목록 호출을 수행해도 이후 시작 성공까지 보장하지는 못했습니다. RDP 연결은 브로커에서 얻은 설정과 `RdpBase.dll`의 비공개 파이프 연결기에 의존했습니다. 게스트의 TCP RDP나 방화벽 설정은 변경하지 않았습니다.

공개 [Remote Desktop ActiveX 문서](https://learn.microsoft.com/en-us/windows/win32/termserv/using-remote-desktop-web-connection)는 컨트롤 호스팅을 설명합니다. 해당 문서가 이 비공개 브로커나 파이프 계약을 보장하지는 않습니다. 설치 패키지별 관찰 근거는 [검증 요약](rdp-sandbox-evidence.json)에 구분했습니다.

## 세션 식별과 공유 자산의 수명

세션 제어에서는 요청 결과의 불확실성을 검토하겠습니다. 실험 코드는 기존 세션을 종료하기 전에 `.wsb`를 불변 스냅샷으로 읽고 XML, 중복 및 미지원 설정, 값과 공유 폴더를 검사했습니다. DTD를 거부하고 전달용 사본에 누락된 `WorkingDirectory`를 원본 파일의 상위 경로로 보완했습니다. 공개 구성 형식은 [Microsoft WSB 문서](https://learn.microsoft.com/en-us/windows/security/application-security/application-isolation/windows-sandbox/windows-sandbox-configure-using-wsb-file)에서 확인할 수 있으며 내부 로더의 관찰 결과와 구분합니다.

일반 실행은 `.wsb` 파일 연결을 기본으로 삼았습니다. 선택형 내장 경로는 사전 검사를 통과했을 때만 내부 API를 사용했습니다. 새 세션 시작에서는 `ProtectedClient`, 비활성화를 명시하지 않은 `VideoInput`, TCP 전송 및 미검증 표시 기능 등을 파일 연결로 보냈습니다. 내부 구성 로더는 `ProtectedClient` 활성화를 거부했습니다. 구성을 읽는 데 성공한 뒤 기존 세션을 발견한 경우에는 일부 미지원 뷰어 구성에도 기존 연결과 취소를 허용하고 종료 후 새 시작을 비활성화했습니다. 검사한 패키지 내부 로더의 비디오 입력 기본값에 의존한 제한이며 모든 버전의 공개 기본값으로 일반화하지 않습니다. 파일 연결 경로는 `UseShellExecute=true`와 `Verb="open"`으로 생성한 파일 자체를 열었습니다.

다만 내부 시작을 한 번 시도한 뒤에는 파일 연결이나 자동 재시작으로 전환하지 않았습니다. 시간 초과와 취소, 응답 유실, 파싱 및 연결 실패가 VM 부재를 뜻하지 않기 때문입니다. 시작 전에 게시한 요청 GUID를 복구 기준으로 유지했고 해당 GUID의 실행을 관찰할 때까지 후속 시작을 차단했습니다. 브로커의 명시적 `CO_E_APPSINGLEUSE` 거부는 해당 요청의 보류 상태를 해제했지만 자동 파일 연결 실행으로 이어지지 않았습니다. 종료도 같은 GUID의 부재를 목록에서 확인한 뒤에만 완료로 처리했습니다. 이 보류 상태는 뷰어를 닫아도 유지했지만 TableCloth 프로세스 재시작을 넘어 저장하지 않았습니다.

제품 통합 실험은 세션이 없거나 하나인 상황만 처리했습니다. 여러 항목을 발견하면 시작과 연결, 종료를 차단했습니다. 남은 단일 세션에는 기존 연결, 데이터 삭제를 확인한 종료 후 새 시작, 취소를 제공했습니다. 대체는 기존 GUID 종료와 부재 확인 후 새 GUID로 시작하는 순서이며 원자적 교체나 롤백을 제공하지 않았습니다. 브로커에는 설정만 검증하는 RPC가 없어 기존 세션을 종료한 다음 새 시작이 실패할 수 있었습니다. 읽기 전용 복구는 새 구성 제출과 생성, 종료를 차단했습니다. 실제 새 시작은 기존 세션이 있을 때 `0x800401F6`인 `CO_E_APPSINGLEUSE`를 반환했으므로 ID 배열을 동시 실행 지원의 증거로 해석하지 않았습니다.

창 닫기는 Sandbox 종료, VM을 유지한 뷰어 종료, 취소를 구분했습니다. 실행 직전에는 세션 staging을 보존 대상으로 표시해 살아 있는 VM의 매핑 자산을 삭제하지 않게 했습니다. 시작 결과가 불확실한 경우도 같은 정책을 적용했습니다. staging에는 사용자가 선택한 인증서 사본이 포함될 수 있었으며 자동 회수는 구현하지 않았습니다. 관련 상태 전이와 실기 증거는 [검증 요약](rdp-sandbox-evidence.json)에 남깁니다.

## Avalonia HWND와 COM 해제 순서

네이티브 호스트의 소유권을 확인하겠습니다. Avalonia `NativeControlHost.CreateNativeControlCore`가 전달한 내부 holder HWND 아래에 ATL ActiveX 자식 창을 만들고 `PlatformHandle(hwnd, "HWND")`를 반환했습니다. 최상위 Avalonia 창을 직접 부모로 가정하지 않았습니다. `OleInitialize`, ActiveX 생성과 COM 호출, `DestroyWindow`, `OleUninitialize`를 UI 스레드에서 처리했습니다.

직접 COM vtable 호출을 사용해 동적 COM이나 리플렉션 기반 래퍼에 의존하지 않았습니다. `NativeControlHost`가 임의 HWND를 자동 파괴한다고 가정하지 않고 `DestroyNativeControlCore`에서 해제를 수행했습니다. ActiveX `Disconnect`가 관찰된 `0x80020009`를 반환해도 예외 형식과 HRESULT만 기록하고 HWND 파괴와 OLE 해제를 계속하도록 보완했습니다.

초기 x64 Native AOT 기록은 CoreCLR 미로딩, `atl.dll`과 `mstscax.dll` 로딩, `IMsRdpClient10` 조회, 부모 및 루트 HWND 관계, 네 가지 크기의 View 일치와 종료 후 HWND 소멸을 확인했습니다. 후속 연결 실험은 `RdpBase.dll` 로딩도 확인했습니다. 호스팅 결과를 투명도와 복잡한 clipping, transform, 모든 중첩 레이아웃이나 ARM64 검증으로 확대하지 않았습니다. [ActiveX 공식 설명](https://learn.microsoft.com/en-us/windows/win32/termserv/using-remote-desktop-web-connection)과 [로컬 검증 요약](rdp-sandbox-evidence.json)을 함께 참고할 수 있습니다.

## 게스트 해상도와 키보드 전달의 관찰 범위

화면 실험은 [IMsRdpClient9.UpdateSessionDisplaySettings](https://learn.microsoft.com/en-us/previous-versions/windows/desktop/legacy/mt703457%28v%3Dvs.85%29)로 게스트 해상도를 바꾸었습니다. ActiveX HWND의 실제 클라이언트 픽셀을 우선 사용하고 논리 크기 대체 경로에서는 렌더 배율을 한 번 적용했습니다. 조정 코드는 250ms 동안 변경을 모으고 성공한 동일 요청을 생략했으며 최소화와 숨김 상태에서는 요청을 중단했습니다.

일시적 실패는 크기별 최대 8회 시도했습니다. 재시도 간격은 500ms, 1,000ms, 이후 최대 2,000ms였으며 예약 대기는 합계 11.5초였습니다. 미지원이나 실패 후에는 [SmartSizing](https://learn.microsoft.com/en-us/windows/win32/termserv/imsrdpclientadvancedsettings-smartsizing)으로 기존 영상을 맞추었습니다. 해상도 변경에 재연결이나 VM 재시작을 사용하지 않았습니다.

단일 모니터와 배율 100%의 x64 실험에서 게스트 PerMonitorV2 프로세스의 `GetSystemMetrics` 값은 `1120×751`, `1470×803`, `1120×751`, `920×751`, `920×571` 순서였습니다. 연결은 한 번이었으며 초기 `0x80020009` 이후 제한된 재시도가 성공했습니다. 원본 로그의 마지막 `640×480` 표본은 뷰어 종료 중에 나왔고 연결 상태의 성공한 조정 다섯 번에 포함하지 않았습니다. DPI 변경과 최소화, 재진입, API 미지원은 자동 테스트 범위에 머물렀습니다.

키보드 실험은 연결 전에 `KeyboardHookMode=1`, `AcceleratorPassthrough=1`, `EnableWindowsKey=1`을 설정하고 실제 ActiveX에서 다시 읽었습니다. [KeyboardHookMode 공식 설명](https://learn.microsoft.com/en-us/windows/win32/termserv/imsrdpclientsecuredsettings-keyboardhookmode)은 원격 단축키 전달 설정을 설명하며 설정값 재조회만으로 게스트 수신을 확정할 수는 없습니다.

데스크톱 접근을 복구한 뒤 연결 토글의 분리 및 결합 표시와 같은 세션 재연결, 게스트 검색창의 `a` 입력을 관찰했습니다. 연결 해제 상태의 `Alt_L+F4`는 호스트 닫기 확인창을 열었고 취소는 뷰어를 유지했습니다. 일반 `Alt` 별칭의 자동 `Alt+Tab`은 게스트 검증 창 A에서 B로 활성 상태를 바꾸었으나 `Alt_L+Tab`에서는 변화를 관찰하지 못했습니다. 두 별칭의 `Alt+F4`에서는 게스트 닫기나 호스트 확인창을 관찰하지 못했습니다. 물리 키보드 Alt+F4와 일반 단축키 전달은 확인되지 않았습니다. 재개 로그의 배율 150%와 `1680×1144`는 클라이언트 기록이며 독립적인 게스트 해상도 측정을 뜻하지 않습니다.

## 과거 테스트 결과와 남은 검증 한계

보관된 최종 TRX의 `Counters`를 다시 읽어 다음 수치를 확인했습니다. 서로 다른 실험 단계의 테스트 수를 합산하지 않았으며 선행 단계의 성공을 후속 구현의 검증으로 확대하지 않았습니다. 보고서 경로와 SHA-256은 [검증 요약](rdp-sandbox-evidence.json)에 기록했습니다.

| 실험 단계 | 최종 성공 테스트 수 | 실기 증거와 범위 |
| --- | ---: | --- |
| 초기 RDP 호스팅 | 47 | 빈 목록에서 생성과 연결, 마우스와 재연결, 뷰어 종료 뒤 세션 생존 기록 |
| 내부 수명 관리 API | 107 | 집중 테스트, 목록 조회와 단일 인스턴스 거부, 별도 승인 후 Winver 구성 대체 로그 |
| ActiveX 해제 보완 | 447 | Disconnect 오류 후 HWND 파괴와 종료 코드 `0`, 같은 VM 생존 기록 |
| 옵션과 축소 배치 | 459 | 영문 및 한국어 headless 배치, 기존 세션 연결과 연결 토글 기록 |
| 아이콘 명령과 닫기 | 527 | 취소 시 유지, 계속 실행 시 뷰어 종료와 같은 세션 조회 기록 |
| 단일 세션과 시작 확인 | 605 | 가짜 서비스의 상태 전이, Native AOT 빈 목록 화면과 해제, 실제 생성 및 종료 미시험 |
| 동적 해상도 | 671 | 조정 테스트 62개와 COM 검사 4개 포함, 배율 100%의 게스트 실측 |
| 축소 도구 모음과 키보드 | 685 | 네 가지 테마 및 연결 표시 렌더링, COM 설정 재조회와 별도 재개 입력 기록 |

실제 구성 대체 로그는 기존 세션 종료 확인과 부재, 새 GUID 시작 응답 및 RDP 설정, 최종 새 세션 목록을 기록했습니다. Winver 자동 실행과 뷰어 연결도 당시 문서와 화면 기록에 남아 있습니다. 이 결과는 당시 검사한 패키지의 단일 세션 대체를 다루며 복수 세션 동시 실행이나 모든 제품 경로를 검증하지 않습니다.

후속 키보드 실험은 서로 다른 바이너리를 구분합니다. 최초 실관찰 파일의 SHA-256은 `5B13EB0D6AAFC45105353E95704A3B1511D93B61A51602192917AF0171AE845A`였고 이후 소스 두 개를 수정했습니다. 최종 보관 파일과 재개 기록이 가리키는 SHA-256은 `44818723C4D584AE56DEB91BCD2481D9BD332AB9552DF8B8BAF2FFADA8D07B2F`입니다. 정리 시 최종 소스 목록 497개의 해시가 작업 파일과 일치했습니다. 재개 기록은 `sourceChanged=false`를 보고하지만 실행 중 프로세스 이미지의 독립 해시는 제공하지 않습니다.

최초 키보드 실험은 데스크톱 접근 실패로 입력 검증을 수행하지 못했고 뷰어 프로세스를 강제 종료했습니다. 그 시도의 VM 정리 기록을 정상 창 닫기 검증으로 확대하지 않았습니다. 재개 기록은 `cleanupComplete=false`를 유지합니다. 별도 파일의 뷰어 종료 코드 `0`만으로 마지막 VM 정리 완료를 확정하지 않았으며 문서 보존 작업에서는 VM을 조작하지 않았습니다.

ARM64, 다른 Sandbox 패키지, 브로커가 실행 중이지 않은 상태의 최초 활성화, 혼합 DPI 모니터 이동, 실제 미지원 서버의 대체 표시와 Shell 폴백, 제품 경로의 모든 시작 및 종료 선택, 물리 키보드와 일반 단축키, staging 자동 회수와 프로세스 재시작 후 미확정 상태 복구는 검증 또는 구현이 남았습니다. 초기 headless 정지와 이름 범위 조회 실패 보고서도 로컬에 남아 있으며 최종 성공 보고서와 구분합니다.

## 후속 판단을 위한 보존 기록

여기까지 정리하면 이 실험은 x64 Native AOT의 Avalonia ActiveX 호스팅과 특정 Sandbox 패키지의 내부 연결 가능성을 확인했습니다. `poc/managed-ai-runtime`은 리서치 문서만 보존하며 폐기한 내장 뷰어의 실행 기능을 포함하지 않습니다. 검증 범위는 [증거 요약](rdp-sandbox-evidence.json)과 각 단계의 원본 자료가 한정합니다.

장기 검토에는 공개 지원 계약과 패키지별 유지 비용, ARM64 및 DPI 검증, 세션과 staging의 수명 설계가 남습니다. 현재 브랜치에서 개발을 이어갈 때에는 [기존 Managed AI 구현](managed-ai-runtime.md)을 기준으로 판단할 수 있습니다. RDP 연동을 다시 구현할 경우 이 기록의 설계 선택을 참고하되 비공개 계약과 미확정 결과를 새 환경에서 다시 검증하는 범위로 계획할 수 있습니다.
