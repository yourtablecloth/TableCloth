#nullable enable
using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace TableCloth.Interop;

/// <summary>
/// AOT 안전(source-generated COM)한 <c>.lnk</c> 바로가기 생성 헬퍼. 기존 <c>Shell.Application</c> late-bound
/// COM + <c>dynamic</c>(Native AOT 비호환: COM 활성화 + DLR)을 대체한다. <see cref="IShellLinkW"/> +
/// <see cref="IPersistFile"/>를 <c>[GeneratedComInterface]</c>로 선언하고 CoCreateInstance +
/// <see cref="StrategyBasedComWrappers"/>로 활성화한다.
///
/// netstandard2.0인 TableCloth.Core에는 GeneratedComInterface(net8+ 필요)를 둘 수 없으므로, 본 파일은
/// <c>src/Shared/Interop</c>에 두고 TableCloth.App / Spork.App 두 net10 모듈에 <c>&lt;Compile Link&gt;</c>로
/// 공유 링크한다. 각 어셈블리가 독립적으로 COM 스텁을 생성한다.
/// </summary>
internal static partial class NativeShellLink
{
    private static readonly Guid CLSID_ShellLink = new("00021401-0000-0000-C000-000000000046");
    private static readonly Guid IID_IShellLinkW = new("000214F9-0000-0000-C000-000000000046");
    private const uint CLSCTX_INPROC_SERVER = 1;
    private const uint COINIT_MULTITHREADED = 0x0;
    private const int RPC_E_CHANGED_MODE = unchecked((int)0x80010106);

    [LibraryImport("ole32.dll")]
    private static partial int CoCreateInstance(
        in Guid rclsid, IntPtr pUnkOuter, uint dwClsContext, in Guid riid, out IntPtr ppv);

    [LibraryImport("ole32.dll")]
    private static partial int CoInitializeEx(IntPtr pvReserved, uint dwCoInit);

    [LibraryImport("ole32.dll")]
    private static partial void CoUninitialize();

    /// <summary>지정 경로에 .lnk 바로가기를 생성한다. 실패 시 COM HRESULT 예외를 던진다.</summary>
    public static void Create(
        string linkFilePath,
        string targetPath,
        string? arguments = null,
        string? workingDirectory = null,
        string? description = null,
        string? iconPath = null,
        int iconIndex = 0)
    {
        // 호출 스레드에서 COM이 초기화되어 있지 않을 수 있다(예: 스레드풀). 초기화가 성공(S_OK/S_FALSE)하면
        // 뒤에서 정리한다. 이미 다른 아파트먼트로 초기화됨(RPC_E_CHANGED_MODE)이면 그대로 사용하고 정리하지 않는다.
        var initHr = CoInitializeEx(IntPtr.Zero, COINIT_MULTITHREADED);
        var shouldUninitialize = initHr >= 0; // SUCCEEDED(S_OK/S_FALSE) → 우리가 초기화했으니 짝을 맞춰 정리
        if (initHr < 0 && initHr != RPC_E_CHANGED_MODE)
            Marshal.ThrowExceptionForHR(initHr);

        try
        {
            CreateCore(linkFilePath, targetPath, arguments, workingDirectory, description, iconPath, iconIndex);
        }
        finally
        {
            if (shouldUninitialize)
                CoUninitialize();
        }
    }

    private static void CreateCore(
        string linkFilePath,
        string targetPath,
        string? arguments,
        string? workingDirectory,
        string? description,
        string? iconPath,
        int iconIndex)
    {
        Marshal.ThrowExceptionForHR(
            CoCreateInstance(in CLSID_ShellLink, IntPtr.Zero, CLSCTX_INPROC_SERVER, in IID_IShellLinkW, out var pUnk));

        try
        {
            var cw = new StrategyBasedComWrappers();
            var link = (IShellLinkW)cw.GetOrCreateObjectForComInstance(pUnk, CreateObjectFlags.None);

            link.SetPath(targetPath);
            if (!string.IsNullOrEmpty(arguments))
                link.SetArguments(arguments);
            if (!string.IsNullOrEmpty(workingDirectory))
                link.SetWorkingDirectory(workingDirectory);
            if (!string.IsNullOrEmpty(description))
                link.SetDescription(description);
            if (!string.IsNullOrEmpty(iconPath))
                link.SetIconLocation(iconPath, iconIndex);

            // IPersistFile.Save(fRemember=TRUE)
            ((IPersistFile)link).Save(linkFilePath, 1);
        }
        finally
        {
            Marshal.Release(pUnk);
        }
    }

    // IShellLinkW vtable 순서(ShObjIdl.h). 사용하지 않는 슬롯은 vtable 정렬만 맞추면 되므로 단순 시그니처로 둔다.
    [GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16)]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    internal partial interface IShellLinkW
    {
        void GetPath(IntPtr pszFile, int cch, IntPtr pfd, uint fFlags);     // unused
        void GetIDList(out IntPtr ppidl);                                   // unused
        void SetIDList(IntPtr pidl);                                        // unused
        void GetDescription(IntPtr pszName, int cch);                      // unused
        void SetDescription(string pszName);
        void GetWorkingDirectory(IntPtr pszDir, int cch);                  // unused
        void SetWorkingDirectory(string pszDir);
        void GetArguments(IntPtr pszArgs, int cch);                        // unused
        void SetArguments(string pszArgs);
        void GetHotkey(out ushort pwHotkey);                               // unused
        void SetHotkey(ushort wHotkey);                                    // unused
        void GetShowCmd(out int piShowCmd);                               // unused
        void SetShowCmd(int iShowCmd);                                     // unused
        void GetIconLocation(IntPtr pszIconPath, int cch, out int piIcon); // unused
        void SetIconLocation(string pszIconPath, int iIcon);
        void SetRelativePath(string pszPathRel, uint dwReserved);          // unused
        void Resolve(IntPtr hwnd, uint fFlags);                            // unused
        void SetPath(string pszFile);
    }

    // IPersistFile vtable 순서(IPersist::GetClassID 먼저).
    [GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16)]
    [Guid("0000010B-0000-0000-C000-000000000046")]
    internal partial interface IPersistFile
    {
        void GetClassID(out Guid pClassID);                               // unused
        [PreserveSig] int IsDirty();                                      // unused
        void Load(string pszFileName, int dwMode);                       // unused
        void Save(string pszFileName, int fRemember);
        void SaveCompleted(string pszFileName);                          // unused
        void GetCurFile(out IntPtr ppszFileName);                        // unused
    }
}
