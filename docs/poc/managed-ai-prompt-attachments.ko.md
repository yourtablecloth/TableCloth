# Managed AI 프롬프트 파일 첨부와 클립보드 붙여넣기 구현

2026년 10월 28일 작업 브랜치에서 TableCloth Managed AI의 파일 선택과 클립보드 첨부를 구현했습니다. PNG, JPEG와 허용한 텍스트 파일을 초안에 추가하고 현재 메시지와 함께 OpenAI에 전달합니다. 기존 인증 프로필의 관리 런타임 Codex 0.156.1에서 합성 PNG와 UTF-8 텍스트의 내용 해석을 확인했습니다. JPEG의 실제 전송과 Windows 원본 앱별 클립보드 호환성은 아직 검증하지 않았습니다.

이 문서는 파일 선택과 붙여넣기 분기, 허용 형식과 한도, 전송 전 검토, 요청별 첨부 수명, 자동 검증과 실기기 검증의 범위를 기록합니다. 일반 텍스트 붙여넣기는 입력란 편집 동작을 유지하며 파일이나 이미지 데이터는 첨부 목록으로 전달합니다.

입력 경로부터 살펴보겠습니다. 이어서 형식 검사와 전송 정책을 설명한 뒤 초안 보존과 검증 근거를 정리합니다.

외부 런타임의 옵션과 모델 동작에는 다음 확인 범위를 적용합니다.

> 확인 기준일: 2026년 10월 28일. Avalonia 11.3.18의 공개 API와 소스, Codex 공식 문서, 활성 관리 런타임 Codex 0.156.1의 `--version`과 `exec --help`를 대조했습니다. 실제 제공자 경로에서 합성 PNG와 UTF-8 텍스트의 인증 전송을 확인했습니다. 별도 Codex 0.164.0 사본에서는 도움말의 이미지 옵션만 확인했습니다. 런타임과 선택 모델을 변경하면 실제 전달 동작을 다시 검증할 수 있습니다.

## 파일 선택과 클립보드 형식별 처리

파일 선택과 붙여넣기의 처리 순서를 짚어보겠습니다. 클립 모양의 파일 첨부 버튼은 입력창 위쪽에서 `+` 스킬 버튼과 같은 행에 표시합니다. 클립 버튼은 여러 파일을 선택하는 대화 상자를 열고 읽기와 검증을 모두 마친 배치만 초안에 추가합니다. 파일 선택 취소나 배치 중 일부 파일의 실패는 기존 첨부와 작성 중인 문장을 유지합니다. [첨부 UI](../../src/TableCloth.App/ManagedAi/ManagedAiWindow.Attachments.cs), [파일 읽기와 미리보기](../../src/TableCloth.App/ManagedAi/ManagedAiPromptAttachmentReader.cs), [Avalonia 파일 대화 상자 문서](https://docs.avaloniaui.net/docs/services/file-dialogs)

클립보드 처리는 같은 프로세스가 제공한 데이터가 있으면 해당 객체를 빌려 씁니다. 그 밖의 경우에는 한 번의 `TryGetDataAsync()` 호출로 네이티브 스냅샷을 확보합니다. 명령마다 선택한 데이터 객체 하나에서 형식을 판별하며 네이티브 경로에서 새로 만든 Bitmap과 파일 항목 및 스냅샷을 처리 후 해제합니다. Ctrl+V, Shift+Insert와 TextBox 붙여넣기 명령은 같은 경로를 사용합니다. 여러 표현을 제공하는 데이터에는 다음 우선순위를 적용합니다. [붙여넣기 분기](../../src/TableCloth.App/ManagedAi/ManagedAiWindow.Attachments.cs), [Avalonia 11.3.18 클립보드 계약](https://github.com/AvaloniaUI/Avalonia/blob/11.3.18/src/Avalonia.Base/Input/Platform/IClipboard.cs)

| 순서 | 클립보드 데이터 | 처리 결과 |
| --- | --- | --- |
| 1 | 탐색기 등에서 복사한 실제 파일 | 파일 선택과 같은 읽기, 검증, 첨부 경로 |
| 2 | `FileGroupDescriptor` 또는 `FileContents` 가상 파일 형식 | 지원하지 않는 형식 안내와 기존 초안 유지 |
| 3 | Bitmap 이미지 | 크기 검사 후 제한한 메모리 스트림에서 PNG로 변환 |
| 4 | 일반 텍스트 | 기존 선택 영역 교체와 실행 취소를 지원하는 입력란 붙여넣기 |
| 5 | 그 밖의 데이터 | 지원하지 않는 형식 안내 |

Windows의 실제 파일 목록과 가상 파일은 서로 다른 전달 계약을 사용합니다. 현재 코드는 가상 파일의 스트림을 추출하지 않습니다. HTML이나 RTF와 일반 텍스트를 함께 제공하면 일반 텍스트 표현을 입력란에 넣습니다. 긴 일반 텍스트를 자동으로 파일 첨부로 바꾸지 않습니다. 비동기 읽기 중 초안이나 선택 영역이 달라지면 이전 텍스트를 덮어쓰지 않습니다. [Microsoft Shell Clipboard Formats](https://learn.microsoft.com/en-us/windows/win32/shell/clipboard), [입력란 붙여넣기 코드](../../src/TableCloth.App/ManagedAi/ManagedAiWindow.Attachments.cs)

## 허용 형식과 첨부 한도

첨부 검사에서는 확장자, 실제 내용과 크기 예산을 함께 확인합니다. 이미지는 PNG와 JPEG만 허용합니다. 텍스트는 UTF-8을 엄격하게 디코딩하며 UTF-8 BOM을 허용합니다. UTF-16은 Little Endian 또는 Big Endian BOM이 있을 때만 허용합니다. 잘못된 인코딩과 허용하지 않은 제어 문자를 포함한 데이터는 거절합니다. [Core 첨부 계약과 허용 목록](../../src/TableCloth.ManagedAi.Core/ChatAttachments.cs)

| 제한 대상 | 한도 | 적용 시점 |
| --- | --- | --- |
| 첨부 개수 | 요청당 4개 | 파일 추가와 요청 전달 전 |
| 이미지 파일 크기 | 파일당 8 MiB | 파일 읽기, PNG 변환과 Core 생성 |
| 텍스트 파일 크기 | 파일당 64 KiB | 원본 읽기와 가림 처리 후 UTF-8 저장 |
| 첨부 총크기 | 요청당 16 MiB | 초안 추가와 요청 전달 전 |
| 텍스트 길이 | 파일당 12,000자, 요청당 24,000자 | 디코딩과 가림 처리 후 |
| 이미지 한 변 | 최대 8,192픽셀 | 헤더 검사와 이미지 디코딩 후 |
| 이미지 전체 픽셀 | 최대 16,777,216픽셀 | 헤더 검사와 이미지 디코딩 후 |
| 파일 표시 이름 | 경로 없는 이름으로 최대 128자 | Core 생성 전과 이름 가림 처리 후 |

텍스트 허용 목록에는 `.txt`, `.md`, `.csv`, `.json`, `.log`, `.xml`, `.yaml`, `.yml`, `.cs`, `.py`, `.js`, `.ts`, `.html`, `.css`, `.ini` 등 문서와 소스 파일 확장자가 포함됩니다. 전체 목록은 [Core의 `TextExtensions`](../../src/TableCloth.ManagedAi.Core/ChatAttachments.cs)에서 관리합니다. PDF, Office 문서, HWP, ZIP, 임의 바이너리와 지원하지 않는 이미지 형식은 첨부하지 않습니다.

PNG 검사는 서명, IHDR, 청크 크기와 CRC, 이미지 데이터와 IEND 구조를 확인합니다. JPEG 검사는 프레임과 스캔 헤더, 크기, 비어 있지 않은 압축 데이터와 종료 표식을 확인합니다. Core 검사는 전체 이미지 디코더를 대체하지 않습니다. UI는 헤더 검사를 마친 바이트를 Bitmap으로 디코딩해 미리보기를 만들고 픽셀 한도를 다시 적용합니다. 클립보드 API가 이미 디코딩한 Bitmap을 반환하는 경로에서는 원본 앱과 플랫폼 디코더의 초기 메모리 사용까지 제한했다고 주장하지 않습니다. [Core 이미지 검사](../../src/TableCloth.ManagedAi.Core/ChatAttachments.cs), [UI 이미지 읽기](../../src/TableCloth.App/ManagedAi/ManagedAiPromptAttachmentReader.cs), [PNG 명세](https://www.w3.org/TR/png-3/#11IHDR), [JPEG T.81 명세](https://www.w3.org/Graphics/JPEG/itu-t81.pdf)

## 전송 전 미리보기와 민감정보 처리

전송 전 검토 수단을 정리하겠습니다. 첨부 카드는 파일 이름, 형식, 크기와 제거 버튼을 표시합니다. 파일을 추가할 때마다 한 행 안에서 같은 너비의 열을 나누며 최대 4개까지 줄바꿈 없이 나란히 표시합니다. 긴 파일 이름은 말줄임표로 줄이고 도움말에서 전체 이름을 제공합니다. 이미지는 축소 미리보기를 제공하고 텍스트는 미리보기 버튼으로 펼쳐 전송할 본문을 읽을 수 있습니다. 미리보기는 처음에는 접힌 상태로 표시하며 마우스나 키보드로 열고 닫을 수 있습니다. 파일 추가와 붙여넣기만으로 요청을 전송하지 않습니다. 일반 메시지가 비어 있어도 첨부가 있으면 보내기 버튼을 사용할 수 있습니다. [첨부 카드와 미리보기](../../src/TableCloth.App/ManagedAi/ManagedAiWindow.Attachments.cs), [메시지 전송](../../src/TableCloth.App/ManagedAi/ManagedAiWindow.cs)

텍스트 파일에는 기존 개인정보 필터를 적용합니다. 첨부 객체는 가림 처리한 텍스트와 UTF-8 바이트만 보존하며 파일 이름에도 같은 필터를 적용하고 확장자를 유지합니다. 인증서와 개인 키의 알려진 확장자, 파일 이름과 PEM 등의 본문 표식은 Core에서 차단합니다. UI와 제공자는 기존 인증서 입력 정책도 적용합니다. 제한 초과나 차단은 고정 오류 코드로 전달하며 첨부 내용이나 원본 경로를 예외 메시지에 넣지 않습니다. [불변 첨부 객체와 차단 검사](../../src/TableCloth.ManagedAi.Core/ChatAttachments.cs), [개인정보 필터](../../src/TableCloth.ManagedAi.Core/SensitiveDataSanitizer.cs), [UI 인증서 검사](../../src/TableCloth.App/ManagedAi/ManagedAiPromptAttachmentReader.cs), [제공자 입력 정책](../../src/TableCloth.ManagedAi.OpenAi/Execution/OpenAiChatPromptFactory.cs)

다만 문자열 필터는 모든 개인정보를 식별하지 못하며 이미지에 OCR이나 자동 가림을 적용하지 않습니다. UI는 첨부를 메시지와 함께 OpenAI에 전송한다는 안내와 이미지에 자동 가림을 적용하지 않는다는 안내를 표시합니다. 인증서, 개인 키, 비밀번호와 인증서 상세 화면을 제출하지 않는 기존 정책도 유지합니다. [전송 안내 문구](../../src/TableCloth.App/ManagedAi/ManagedAiWindow.Attachments.cs), [대화의 인증서 정책](../../src/TableCloth.ManagedAi.OpenAi/Execution/OpenAiChatPromptFactory.cs)

## 요청별 전달과 초안의 보존 및 정리

초안과 실행 요청의 첨부 수명을 구분하겠습니다. 초안은 원본 파일 경로나 열린 클립보드 스트림을 계속 참조하지 않고 크기를 제한한 메모리 사본을 소유합니다. `AiChatAttachment`는 생성 시 바이트를 복사하고 `GetContent()`도 복사본을 반환합니다. 세션과 제공자는 첨부 목록을 고정한 뒤 요청을 시작하므로 원본 파일 변경이 재시도 내용을 바꾸지 않습니다. 초안용 임시 파일은 만들지 않습니다. [첨부 객체](../../src/TableCloth.ManagedAi.Core/ChatAttachments.cs), [세션 요청 스냅샷](../../src/TableCloth.ManagedAi.Core/ManagedAiChatSession.cs), [제공자 요청 스냅샷](../../src/TableCloth.ManagedAi.OpenAi/OpenAiCodexProvider.cs)

텍스트 첨부는 가림 처리한 본문을 별도 JSON 데이터 영역으로 표준 입력 프롬프트에 넣습니다. 제공자는 파일 이름과 첨부 본문을 신뢰하지 않는 사용자 데이터로 표시합니다. 이미지는 앱이 소유한 요청 실행 디렉터리에 실행용 이름으로 기록하고 각 경로를 `ArgumentList`의 `--image` 인자로 전달합니다. 활성 관리 런타임 Codex 0.156.1과 별도 Codex 0.164.0 사본의 `exec --help`에서 해당 옵션을 확인했으며 공식 문서도 이미지 첨부 옵션을 설명합니다. [프롬프트 생성기](../../src/TableCloth.ManagedAi.OpenAi/Execution/OpenAiChatPromptFactory.cs), [실행 사본 생성](../../src/TableCloth.ManagedAi.OpenAi/OpenAiCodexProvider.cs), [CLI 인자 생성기](../../src/TableCloth.ManagedAi.OpenAi/Execution/OpenAiCodexInvocationBuilder.cs), [Codex CLI 공식 문서](https://learn.chatgpt.com/docs/developer-commands?surface=cli#codex-exec)

프롬프트 생성기는 메시지 4,000자, 이력 12개와 20,000자, 생성 프롬프트 50,000자 제한도 적용합니다. 첨부별 제한을 통과해도 이력과 현재 입력을 합친 프롬프트가 한도를 넘으면 전송을 거절합니다. 실행용 이미지 사본은 프로세스 처리가 끝난 뒤 제공자의 `finally`에서 소유한 실행 디렉터리와 함께 삭제합니다. 원본 파일은 정리 대상에 포함하지 않습니다. [프롬프트 예산](../../src/TableCloth.ManagedAi.OpenAi/Execution/OpenAiChatPromptFactory.cs), [소유 디렉터리 정리](../../src/TableCloth.ManagedAi.OpenAi/OpenAiCodexProvider.cs)

메시지 전송을 시작하면 입력 영역에서 첨부 카드를 즉시 제거합니다. 요청은 이미 고정한 첨부 스냅샷을 사용하며 응답을 마칠 때까지 카드를 입력 영역에 남기지 않습니다. 성공 후에는 초안과 미리보기를 해제하고 오류나 취소 후에는 작성 중인 메시지와 첨부 카드를 복원해 다시 보낼 수 있습니다. 전송 전 확인 대화 상자에서 취소하면 기존 첨부를 그대로 표시합니다. 첨부 제거는 해당 항목만 해제하며 새 대화는 초안이나 첨부가 있을 때 확인을 거칩니다. 창을 닫으면 남은 첨부와 미리보기를 해제합니다. 대화 이력에는 파일 이름을 담은 표시 문구만 남기며 이미지 바이트와 텍스트 본문을 후속 요청에 자동으로 재전송하지 않습니다. [전송 성공과 실패 처리](../../src/TableCloth.App/ManagedAi/ManagedAiWindow.cs), [초안 정리](../../src/TableCloth.App/ManagedAi/ManagedAiWindow.Attachments.cs), [파일 이름만 보관하는 이력](../../src/TableCloth.ManagedAi.Core/ManagedAiChatSession.cs)

## 자동 검증과 실제 전송 증거의 범위

검증 근거에서는 자동화한 계약 검사와 실제 환경에서 남은 항목을 검토하겠습니다. 호스트 테스트 424개와 Managed AI 테스트 521개를 모두 통과했습니다. Core 테스트는 인코딩, 개인정보 가림, 인증서 차단, 이미지 구조와 크기 제한, 요청 목록 고정, 실패 후 재시도와 이력의 원문 제외를 다룹니다. 제공자 테스트는 가짜 프로세스 실행기로 텍스트 전달과 이미지 인자, 요청별 사본과 정리, 오류와 취소 경로를 확인합니다. Headless UI 테스트는 실제 파일과 Bitmap 데이터의 붙여넣기, 일반 텍스트 선택 영역과 실행 취소, IME 조합 보존, 미리보기, 초안 보존과 레이아웃을 다룹니다. 클립 버튼과 스킬 버튼의 수평 정렬, 방향키 탐색과 현재 후보의 테마 강조색도 확인했습니다. [Core 첨부 테스트](../../src/TableCloth.ManagedAi.Test/ChatAttachmentTests.cs), [제공자 첨부 테스트](../../src/TableCloth.ManagedAi.Test/AttachmentProviderTests.cs), [Headless 첨부 UI 테스트](../../src/TableCloth.Test/ManagedAiWindowTests.Attachments.cs), [가로 첨부 배치 테스트](../../src/TableCloth.Test/ManagedAiWindowTests.AttachmentLayout.cs), [스킬 탐색 테스트](../../src/TableCloth.Test/ManagedAiWindowTests.SkillNavigation.cs), [검증 기록](managed-ai-prompt-attachments-evidence.json)

실제 전송 검증은 활성 관리 런타임 Codex 0.156.1 x64와 기존 인증 프로필을 사용했습니다. 애플리케이션의 DI 구성을 통해 `OpenAiCodexProvider`, `WindowsManagedAiProfile`, `JsonlProcessRunner`를 실행했습니다. 입력에는 32×32픽셀 파란색 합성 PNG와 `The sample attachment token is ATTACHMENT-4721.` 문장의 UTF-8 텍스트를 함께 제공했습니다. 모델을 별도로 지정하지 않고 토큰과 색상을 요청했으며 응답은 정확히 `ATTACHMENT-4721/blue`를 반환했습니다. 프로세스 종료 코드는 0이었고 내용 검증 결과도 성공했습니다. 개인 파일을 전송하거나 새 로그인을 수행하지 않았으며 기존 구독의 인증 상태로 실행했습니다. [실제 제공자 경로](../../src/TableCloth.ManagedAi.OpenAi/OpenAiCodexProvider.cs), [프롬프트와 첨부 직렬화](../../src/TableCloth.ManagedAi.OpenAi/Execution/OpenAiChatPromptFactory.cs), [전달과 정리의 회귀 테스트](../../src/TableCloth.ManagedAi.Test/AttachmentProviderTests.cs)

이와는 별개로 JPEG의 실제 내용 해석, 모델별 비교, Windows 원본 앱의 OLE 조합, 네이티브 파일 선택 대화 상자와 한글 전환키는 아직 검증하지 않았습니다. Headless와 가짜 실행기 검사는 이 경로의 실제 동작을 입증하지 않습니다. 남은 항목은 다음 절차로 확인할 수 있습니다. [Avalonia Windows 클립보드 구현](https://github.com/AvaloniaUI/Avalonia/blob/11.3.18/src/Windows/Avalonia.Win32/ClipboardImpl.cs), [Codex CLI 이미지 옵션](https://learn.chatgpt.com/docs/developer-commands?surface=cli#codex-exec)

1. 클립 버튼에서 PNG, JPEG와 텍스트 파일을 선택합니다. 같은 파일을 탐색기에서 복사해 Ctrl+V로 붙여넣고 첨부 목록과 미리보기를 확인합니다.
2. 캡처 도구와 그림판의 이미지를 붙여넣고 메모장과 브라우저의 일반 텍스트에서 선택 영역 교체, 실행 취소, 첫 포커스의 한글 전환키와 한글 조합을 확인합니다.
3. Outlook 등의 가상 파일에서 지원하지 않는 형식 안내와 초안 보존을 확인합니다.
4. 비민감 JPEG를 인증한 관리 런타임으로 전송하고 다른 모델에서도 합성 첨부를 시험합니다. 런타임 버전, 선택 모델, CLI 옵션 수락과 내용 해석 결과를 각각 기록합니다.
5. 전송 시작 직후 첨부 카드 제거, 오류와 취소 후 초안 복원, 성공 후 첨부 해제, 요청 실행 사본 삭제와 후속 메시지의 원문 미재전송을 확인합니다.

## 후속 메시지와 파일 처리 확장의 판단 기준

여기까지 정리하면 현재 구현은 허용한 이미지와 텍스트를 초안에서 검토하고 해당 요청에 전달하는 경로를 제공합니다. Codex 0.156.1의 실제 제공자 실행에서 합성 PNG와 UTF-8 텍스트의 내용 해석을 확인했으며 JPEG, 다른 모델과 네이티브 Windows 입력 경로는 추가 검증 범위로 남습니다. 후속 메시지에 파일 원문을 자동으로 다시 보내지 않으므로 같은 내용을 다시 참조하려면 첨부를 다시 추가할 수 있습니다. [첨부 이력 계약](../../src/TableCloth.ManagedAi.Core/ManagedAiChatSession.cs)

PDF, Office 문서, HWP와 가상 파일의 본문 추출은 후속 기능으로 남습니다. 첨부 기능은 일반 셸 실행이나 설치된 스킬의 호스트 파일 접근을 허용하지 않습니다. 해당 형식의 분석이나 스킬의 파일 처리를 추가하는 경우에는 제공자 입력 계약과 AppContainer 접근 범위를 별도로 확장할 수 있습니다. 현재 지원 여부는 허용한 파일 형식과 요청별 전송 경로를 기준으로 판단합니다. [Windows 전용 프로필](../../src/TableCloth.ManagedAi.Windows/WindowsManagedAiProfile.cs), [스킬 실행 레지스트리](../../src/TableCloth.ManagedAi.AppContainer/Broker/SkillExecutionRegistry.cs), [AppContainer 제품 통합 경계](appcontainer-product-integration.ko.md)
