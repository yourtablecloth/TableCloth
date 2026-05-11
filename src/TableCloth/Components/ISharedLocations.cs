namespace TableCloth.Components;

public interface ISharedLocations
{
    string AppDataDirectoryPath { get; }
    string ApplicationLogPath { get; }
    string ExecutableDirectoryPath { get; }
    string ExecutableFilePath { get; }
    string SporkZipFilePath { get; }
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
    string GetCertificateStagingDirectoryPath();

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
}