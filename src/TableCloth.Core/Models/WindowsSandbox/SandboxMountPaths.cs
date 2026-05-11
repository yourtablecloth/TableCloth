namespace TableCloth.Models.WindowsSandbox
{
    /// <summary>
    /// 식탁보가 항상 마운트하는 표준 디렉터리의 샌드박스 내부 경로 상수입니다.
    /// 호스트(TableCloth)와 샌드박스 내부(Spork) 양쪽에서 동일한 경로를 사용하기 위해 공유됩니다.
    /// </summary>
    public static class SandboxMountPaths
    {
        // Windows Sandbox는 SandboxFolder가 쓰기 가능한 위치에 있을 것을 요구한다.
        // 사용자 프로필 하위(C:\Users\WDAGUtilityAccount\...)가 가장 자연스러우며,
        // 다른 후보인 C:\ProgramData나 C:\Windows\Temp보다 위치가 명확하다.
        private const string SandboxUserProfile = @"C:\Users\WDAGUtilityAccount";

        /// <summary>
        /// 식탁보 마운트의 샌드박스 내부 루트 경로입니다.
        /// </summary>
        public const string Root = SandboxUserProfile + @"\TableCloth";

        /// <summary>
        /// 읽기 전용으로 마운트되는 App 디렉터리의 샌드박스 내부 경로입니다.
        /// Spork 실행 파일, 카탈로그 스냅샷, 부속 리소스가 위치합니다.
        /// 매 실행마다 호스트가 새로 채워 넣습니다.
        /// </summary>
        public const string AppDirectory = Root + @"\App";

        /// <summary>
        /// 읽기-쓰기로 마운트되는 Data 디렉터리의 샌드박스 내부 경로입니다.
        /// 즐겨찾기, 사용 기록, 사용자 백업 등 영속 상태가 누적됩니다.
        /// 호스트의 사용자 지정 폴더를 그대로 마운트합니다.
        /// </summary>
        public const string DataDirectory = Root + @"\Data";

        /// <summary>
        /// App 디렉터리 안에서 Spork 실행 파일이 위치하는 샌드박스 내부 경로입니다.
        /// </summary>
        public const string SporkExecutable = AppDirectory + @"\Spork.exe";

        /// <summary>
        /// App 디렉터리 안에서 카탈로그 스냅샷(폴백 데이터)이 위치하는 샌드박스 내부 경로입니다.
        /// 샌드박스 내부 네트워크 실패 시 Spork가 이 스냅샷으로 폴백합니다.
        /// </summary>
        public const string CatalogSnapshotDirectory = AppDirectory + @"\Catalog";
    }
}
