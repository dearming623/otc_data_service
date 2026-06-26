using System.Runtime.InteropServices;

namespace OtcDataService.Native;

internal static class Win32User32
{
    [DllImport("user32.dll")]
    internal static extern uint GetDoubleClickTime();
}
