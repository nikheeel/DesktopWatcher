using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using static ConsoleApp2.Win32API;
using WndList = System.Collections.Generic.List<System.IntPtr>;

namespace ConsoleApp2
{
    public static class Helper
    {
        private static int _winLong;

        public static bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam)
        {
            if (hWnd == IntPtr.Zero || !IsWindow(hWnd)) //|| !Win32API.IsWindowVisible(hWnd))
                throw new ApplicationException("bad window");

            if (!IsWindowVisible(hWnd))
                return true;
            var gch = GCHandle.FromIntPtr(lParam);
            var list = gch.Target as WndList;
            if (list == null) throw new InvalidCastException("GCHandle Target could not be cast as List<IntPtr>");
            list.Add(hWnd);
            return true;
        }

        public static void EnterSpecialCapturing(IntPtr hWnd)
        {
            try
            {
                SetMinimizeMaximizeAnimation(false);

                _winLong = GetWindowLong(hWnd, GWL_EXSTYLE);
                if (_winLong == 0)
                {
                    var ex = new Win32Exception(Marshal.GetLastWin32Error());
                    throw new ApplicationException("GetWindowLong failed: " + ex.Message, ex);
                }

                var res = SetWindowLong(hWnd, GWL_EXSTYLE, _winLong | WS_EX_LAYERED);
                if (res != _winLong)
                {
                    var ex = new Win32Exception(Marshal.GetLastWin32Error());
                    throw new ApplicationException("SetWindowLong failed: " + ex.Message, ex);
                }

                if (!SetLayeredWindowAttributes(hWnd, 0, 1, LWA_ALPHA))
                {
                    var ex = new Win32Exception(Marshal.GetLastWin32Error());
                    throw new ApplicationException("SetLayeredWindowAttributes failed: " + ex.Message, ex);
                }

                ShowWindow(hWnd, WindowShowStyle.Restore);
                SendMessage(hWnd, WM_PAINT, 0, 0);
            }
            catch (Exception ex)
            {
                EventLog.WriteEntry("Screen Monitor", ex.Message, EventLogEntryType.Error, 1, 1);
            }
        }
    }
}