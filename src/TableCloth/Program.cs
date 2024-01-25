using System;
using System.Runtime.CompilerServices;

namespace TableCloth;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        RunApp();
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    private static void RunApp()
    {
        var app = new App();
        app.Run();
    }
}
