using System;
using System.Collections;
using System.Runtime.InteropServices;
using System.Text;

namespace ConsoleApp2
{
    internal class WindowStation
    {
        public delegate bool EnumDesktopsDelegate(string desktop, IntPtr lParam);

        public delegate bool EnumDesktopWindowsDelegate(IntPtr hWnd, int lParam);

        public delegate bool EnumWindowStationsDelegate(string windowsStation, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern bool EnumWindowStations(
            EnumWindowStationsDelegate lpEnumFunc,
            IntPtr lParam
        );

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr OpenWindowStation(
            [MarshalAs(UnmanagedType.LPTStr)] string WinStationName,
            [MarshalAs(UnmanagedType.Bool)] bool Inherit,
            uint Access
        );

        [DllImport("user32.dll")]
        public static extern bool CloseWindowStation(
            IntPtr winStation
        );

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool EnumDesktops(
            IntPtr winStation,
            EnumDesktopsDelegate EnumFunc,
            IntPtr lParam
        );

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr OpenDesktop(
            [MarshalAs(UnmanagedType.LPTStr)] string DesktopName,
            uint Flags,
            bool Inherit,
            uint Access
        );

        [DllImport("user32.dll")]
        public static extern bool CloseDesktop(
            IntPtr hDesktop
        );

        [DllImport("user32.dll")]
        public static extern bool EnumDesktopWindows(
            IntPtr hDesktop,
            EnumDesktopWindowsDelegate EnumFunc,
            IntPtr lParam
        );

        [DllImport("user32", SetLastError = true)]
        public static extern IntPtr GetProcessWindowStation();

        [DllImport("user32.dll", SetLastError = true)]
        public static extern int GetWindowText(
            IntPtr hWnd,
            StringBuilder lpWindowText,
            int nMaxCount
        );

        [DllImport("user32.dll")]
        public static extern bool IsWindowVisible(
            IntPtr hwnd
        );

        [DllImport("user32.dll")]
        public static extern IntPtr GetWindowThreadProcessId(
            IntPtr hWnd,
            out IntPtr ProcessId
        );

        private static bool HandleDesktopEntry(string desktop, IntPtr lParam)
        {
            Console.WriteLine(desktop);
            return true;
        }

        public static string[] EnumerateDesktops(string winStationName)
        {
            var winStation = OpenWindowStation(winStationName, true, 0x00020000);
            if (winStation == IntPtr.Zero)
                throw new Exception("Winstation could not be opened : " + Marshal.GetLastWin32Error());

            var tArrayList = new ArrayList();
            var desktopDelegate = new EnumDesktopsDelegate(HandleDesktopEntry);
            EnumDesktops(winStation, desktopDelegate, IntPtr.Zero);

            var desktops = new string[tArrayList.Count];
            tArrayList.CopyTo(desktops);
            tArrayList.Clear();

            CloseWindowStation(winStation);

            return desktops;
        }

        // Function call made is 
    }
}