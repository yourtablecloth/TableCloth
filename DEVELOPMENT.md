# TableCloth 개발 환경 요구사항

TableCloth는 Windows 11 이상에서 개발하도록 최적화되어 있습니다. 프로젝트 빌드 및 실행을 포함한 전체 애플리케이션 개발에는 Windows 환경이 필요합니다. 다른 운영체제에서는 문서 편집 및 카탈로그 관리와 같은 특정 작업으로 개발이 제한됩니다.

> [!WARNING]
> Windows 10은 2025년 12월부로 지원이 중단된 OS이며 보안 업데이트가 더 이상 제공되지 않습니다. 개발이나 빌드가 가능하더라도 보안상 권장하지 않습니다.

## Windows 개발

애플리케이션 빌드 및 실행을 포함한 전체 개발을 위해서는 Windows 11 이상 환경이 필요합니다.

### Windows 단계

1. 저장소를 클론합니다:

    ```powershell
    git clone https://github.com/yourtablecloth/TableCloth.git
    ```

2. Visual Studio 2026 이상을 설치합니다.
3. Visual Studio에서 솔루션 파일 `TableCloth.slnx`를 엽니다.
4. 프로젝트를 빌드하고 실행합니다:
    - 디버깅 없이 실행하려면 `Ctrl + F5`를 누릅니다.

## Visual Studio Code

> C# Dev Kit은 유효한 Visual Studio 라이선스가 필요할 수 있습니다.

1. 저장소를 클론합니다:

    ```powershell
    git clone https://github.com/yourtablecloth/TableCloth.git
    ```

2. C# Dev Kit 확장을 설치합니다:
    - 확장 ID: `ms-dotnettools.csdevkit`
3. 프로젝트를 빌드하고 실행합니다:
    - `Ctrl + F5`를 누릅니다.

## Windows 이외 OS 개발

macOS 또는 Linux에서는 문서 편집과 카탈로그 관리 같은 작업으로 개발이 제한됩니다. 전체 애플리케이션 빌드 및 실행은 지원되지 않습니다.
