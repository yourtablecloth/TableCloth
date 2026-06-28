namespace TableCloth.Components;

public interface ISharedLocations
{
    string AppDataDirectoryPath { get; }
    string ApplicationLogPath { get; }
    string ExecutableDirectoryPath { get; }
    string ExecutableFilePath { get; }
    string ImagesZipFilePath { get; }
    string PreferencesFilePath { get; }
    string CatalogCacheFilePath { get; }

    /// <summary>
    /// 사용자가 별도 지정하지 않았을 때 사용하는 Data 디렉터리의 기본 호스트 경로입니다.
    /// 샌드박스 시작 시 Data 디렉터리로 마운트되어 즐겨찾기/사용 기록/사용자 백업이 누적됩니다.
    /// </summary>
    string DefaultDataDirectoryPath { get; }

    string GetImageDirectoryPath();
    string GetImageFilePath(string serviceId);
    string GetIconFilePath(string serviceId);
    string GetTempPath();

    /// <summary>
    /// 샌드박스 시작 시 App 디렉터리로 마운트할 호스트 측 스테이징 경로를 반환합니다.
    /// 매 실행마다 Spork 실행 파일과 카탈로그 스냅샷이 이 위치에 새로 채워집니다.
    /// </summary>
    /// <param name="sandboxStagingDirectory">현재 샌드박스 실행을 위해 생성한 출력 디렉터리.</param>
    string GetSandboxAppStagingPath(string sandboxStagingDirectory);

    /// <summary>
    /// <see cref="DefaultDataDirectoryPath"/>를 포함하여, 환경 설정의 Data 경로를 반영한 실제 호스트 Data 디렉터리 경로를 반환합니다.
    /// 디렉터리가 존재하지 않을 경우 호출 측에서 필요 시 생성해야 합니다.
    /// </summary>
    /// <param name="configuredPath">환경 설정에 저장된 호스트 경로(빈 값이면 기본 경로 사용).</param>
    string GetEffectiveDataDirectoryPath(string? configuredPath);

    /// <summary>
    /// 주어진 경로가 Windows 샌드박스로 마운트 가능한 위치(로컬 고정 디스크)에 있는지 판별합니다.
    /// 네트워크/이동식 드라이브나 UNC 경로는 샌드박스가 마운트하지 못하므로 false를 반환합니다.
    /// 클라우드 가상 드라이브처럼 판별이 불확실한 경우에는 거짓 차단/경고를 피하기 위해 true를 반환합니다.
    /// </summary>
    /// <param name="path">검사할 호스트 경로.</param>
    bool IsMountableDataDirectory(string? path);
}