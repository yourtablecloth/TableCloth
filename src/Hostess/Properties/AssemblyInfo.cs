using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;

[assembly: AssemblyTitle("Table Cloth Hostess")]
[assembly: AssemblyDescription("This program is a tool that installs the requested installers at once in the sandbox.")]
[assembly: AssemblyCompany("rkttu.com")]
[assembly: AssemblyProduct("Table Cloth")]
[assembly: AssemblyCopyright("(c) rkttu.com, 2021")]
[assembly: AssemblyTrademark("Table Cloth")]
[assembly: AssemblyVersion("0.5.3.0")]
[assembly: AssemblyFileVersion("0.5.3.0")]
[assembly: Guid("36e7b617-ca94-4ecb-ad65-39fe94ce265b")]
[assembly: ComVisible(true)]

#if DEBUG
[assembly: AssemblyConfiguration("Debug")]
#else // DEBUG
[assembly: AssemblyConfiguration("Release")]
#endif // DEBUG

[assembly: ThemeInfo(
    // where theme specific resource dictionaries are located (used if a resource is not found in the page, or application resource dictionaries)
    ResourceDictionaryLocation.None,

    // where the generic resource dictionary is located (used if a resource is not found in the page, app, or any theme specific resource dictionaries)
    ResourceDictionaryLocation.SourceAssembly)]
