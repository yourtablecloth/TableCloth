namespace TableCloth.Models
{
    /// <summary>
    /// UI 프레임워크 중립적인 메시지 박스 버튼 구성. WPF <c>System.Windows.MessageBoxButton</c>에 대응하나
    /// ViewModel이 특정 UI 프레임워크(WPF)에 결합되지 않도록 Core에 정의한다. 실제 표시 구현
    /// (WPF/Avalonia)이 자신에 맞는 타입으로 매핑한다. (이슈 #296 UI 이음새 디커플링)
    /// </summary>
    public enum AppMessageBoxButton
    {
        OK,
        OKCancel,
        YesNo,
        YesNoCancel,
    }

    /// <summary>UI 프레임워크 중립적인 메시지 박스 결과. WPF <c>MessageBoxResult</c>에 대응.</summary>
    public enum AppMessageBoxResult
    {
        None,
        OK,
        Cancel,
        Yes,
        No,
    }

    /// <summary>UI 프레임워크 중립적인 메시지 박스 아이콘. WPF <c>MessageBoxImage</c>에 대응(Error→Stop 등은 구현이 매핑).</summary>
    public enum AppMessageBoxImage
    {
        None,
        Information,
        Warning,
        Error,
        Question,
    }
}
