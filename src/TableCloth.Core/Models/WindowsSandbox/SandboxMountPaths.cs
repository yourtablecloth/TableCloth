namespace TableCloth.Models.WindowsSandbox
{
    /// <summary>
    /// 식탁보가 항상 마운트하는 표준 디렉터리의 샌드박스 내부 경로 상수입니다.
    /// 호스트(TableCloth)와 샌드박스 내부(Spork) 양쪽에서 동일한 경로를 사용하기 위해 공유됩니다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 식탁보가 생성하는 wsb 파일은 구 버전 Windows Sandbox 호환성을 위해
    /// <c>&lt;SandboxFolder&gt;</c> XML 요소를 의도적으로 사용하지 않습니다.
    /// 그 결과 매핑된 호스트 폴더는 항상 샌드박스 사용자의 바탕 화면 하위에
    /// 해당 호스트 폴더의 leaf 이름으로 노출됩니다(예: 호스트의 <c>...\TableCloth\Data</c>는
    /// 샌드박스의 <c>C:\Users\WDAGUtilityAccount\Desktop\Data</c>에 마운트됨).
    /// </para>
    /// <para>
    /// 따라서 본 클래스의 상수들은 "마운트 destination을 명시하기 위한 값"이 아니라,
    /// "마운트가 끝난 뒤 샌드박스 내부에서 자료를 찾을 위치"를 가리키는 상수입니다.
    /// 호스트 측 폴더의 leaf 이름이 곧 이 경로의 마지막 segment여야 합니다.
    /// </para>
    /// </remarks>
    public static class SandboxMountPaths
    {
        /// <summary>
        /// 샌드박스 사용자의 바탕 화면 경로입니다. 모든 매핑은 이 폴더 하위로 노출됩니다.
        /// </summary>
        public const string SandboxDesktop = @"C:\Users\WDAGUtilityAccount\Desktop";

        /// <summary>
        /// 식탁보 App 디렉터리의 샌드박스 노출 경로입니다.
        /// Spork 실행 파일과 카탈로그 스냅샷이 이 위치에서 보입니다(매 실행마다 호스트가 새로 채움).
        /// 호스트 측 폴더의 leaf 이름은 <c>App</c>이어야 합니다.
        /// </summary>
        public const string AppDirectory = SandboxDesktop + @"\App";

        /// <summary>
        /// 식탁보 Data 디렉터리의 샌드박스 노출 경로입니다.
        /// 즐겨찾기/사용 기록/사용자 백업이 누적되며 세션 간에 영속됩니다.
        /// 호스트 측 폴더의 leaf 이름은 <c>Data</c>여야 합니다.
        /// </summary>
        public const string DataDirectory = SandboxDesktop + @"\Data";

        /// <summary>
        /// App 디렉터리 안의 Spork 실행 파일 경로입니다.
        /// </summary>
        public const string SporkExecutable = AppDirectory + @"\Spork.exe";

        /// <summary>
        /// App 디렉터리 안의 카탈로그 스냅샷(폴백 데이터) 위치입니다.
        /// 샌드박스 내부 네트워크 실패 시 Spork가 이 스냅샷으로 폴백합니다.
        /// </summary>
        public const string CatalogSnapshotDirectory = AppDirectory + @"\Catalog";

        /// <summary>
        /// 호스트의 NPKI 인증서 폴더가 RO로 마운트되는 샌드박스 데스크톱 위치입니다.
        /// 은행/금융 소프트웨어는 <c>%USERPROFILE%\AppData\LocalLow\NPKI</c>에서 인증서를 찾기 때문에,
        /// startup 스크립트가 이 폴더의 내용을 <see cref="NpkiCanonicalPath"/>로 <c>xcopy</c> 하여
        /// 독립된 쓰기 가능 사본을 만들어 둡니다.
        /// </summary>
        /// <remarks>
        /// junction 링크 대신 사본을 만드는 이유는, junction은 결국 RO 마운트를 가리켜
        /// 호스트 인증서를 그대로 노출하고 은행 SW가 NPKI에 쓰기 작업을 할 때 실패하기 때문입니다.
        /// </remarks>
        public const string NpkiDesktopMount = SandboxDesktop + @"\NPKI";

        /// <summary>
        /// 은행/금융 소프트웨어가 NPKI 인증서를 기대하는 샌드박스 내부 표준 경로입니다.
        /// startup 스크립트가 <see cref="NpkiDesktopMount"/>의 내용을 이 위치로 xcopy 합니다.
        /// </summary>
        public const string NpkiCanonicalPath = @"C:\Users\WDAGUtilityAccount\AppData\LocalLow\NPKI";
    }
}
