using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace ConsoleApp2
{
    public class ImpersonateInteractiveUser : IDisposable
    {
        //IntPtr _hpiu = IntPtr.Zero;
        private readonly bool _bimpersonate;
        private IntPtr _hSaveDesktop;
        private IntPtr _hSaveWinSta;
        private IntPtr _hWinSta;


        private WindowsImpersonationContext _impersonatedUser;

        private IntPtr _userTokenHandle = IntPtr.Zero;

        public ImpersonateInteractiveUser(IntPtr hWnd, bool bimpersonate)
        {
            if (hWnd == IntPtr.Zero || !Win32API.IsWindow(hWnd) || !Win32API.IsWindowVisible(hWnd))
                throw new ApplicationException(string.Format("{0} is an Invalid window", hWnd));
            _bimpersonate = bimpersonate;
            int procId;
            Win32API.GetWindowThreadProcessId(hWnd, out procId);
            var proc = Process.GetProcessById(procId);
            ImpersonateUsingProcess(proc);
        }

        public ImpersonateInteractiveUser(Process proc, bool bimpersonate)
        {
            _bimpersonate = bimpersonate;
            ImpersonateUsingProcess(proc);
        }

        public IntPtr UserTokenHandle
        {
            [DebuggerStepThrough] get => _userTokenHandle;
        }

        public IntPtr HDesktop { [DebuggerStepThrough] get; private set; }

        #region IDisposable Members

        public void Dispose()
        {
            //if (!Win32API.UnloadUserProfile(WindowsIdentity.GetCurrent().Token, _hpiu))
            // throw new Win32Exception(Marshal.GetLastWin32Error());
            //Marshal.FreeHGlobal(_hpiu);
            UndoDesktop();


            if (_userTokenHandle != IntPtr.Zero)
                Win32API.CloseHandle(_userTokenHandle);
            _userTokenHandle = IntPtr.Zero;
            _impersonatedUser = null;
        }

        #endregion

        private void ImpersonateUsingProcess(Process proc)
        {
            var hToken = IntPtr.Zero;

            Win32API.RevertToSelf();

            if (Win32API.OpenProcessToken(proc.Handle, TokenPrivilege.TOKEN_ALL_ACCESS, ref hToken) != 0)
            {
                try
                {
                    var sa = new SECURITY_ATTRIBUTES();
                    sa.Length = Marshal.SizeOf(sa);
                    var result = Win32API.DuplicateTokenEx(hToken, Win32API.GENERIC_ALL_ACCESS, ref sa,
                        (int) SECURITY_IMPERSONATION_LEVEL.SecurityIdentification, (int) TOKEN_TYPE.TokenPrimary,
                        ref _userTokenHandle);
                    if (IntPtr.Zero == _userTokenHandle)
                    {
                        var ex = new Win32Exception(Marshal.GetLastWin32Error());
                        throw new ApplicationException(
                            string.Format("Can't duplicate the token for {0}:\n{1}", proc.ProcessName, ex.Message), ex);
                    }


                    //EventLog.WriteEntry("Screen Monitor", string.Format("Before impersonation: owner = {0} Windows ID Name = {1} token = {2}", WindowsIdentity.GetCurrent().Owner, WindowsIdentity.GetCurrent().Name, WindowsIdentity.GetCurrent().Token), EventLogEntryType.SuccessAudit, 1, 1);

                    if (!ImpersonateDesktop())
                    {
                        var ex = new Win32Exception(Marshal.GetLastWin32Error());
                        throw new ApplicationException(ex.Message, ex);
                    }
                }
                finally
                {
                    Win32API.CloseHandle(hToken);
                }
            }
            else
            {
                var s = string.Format("OpenProcess Failed {0}, privilege not held", Marshal.GetLastWin32Error());
                throw new Exception(s);
            }
        }

        private bool ImpersonateDesktop()
        {
            _hSaveWinSta = Win32API.GetProcessWindowStation();
            if (_hSaveWinSta == IntPtr.Zero)
                return false;
            _hSaveDesktop = Win32API.GetThreadDesktop(Win32API.GetCurrentThreadId());
            if (_hSaveDesktop == IntPtr.Zero)
                return false;
            if (_bimpersonate)
            {
                var newId = new WindowsIdentity(_userTokenHandle);
                _impersonatedUser = newId.Impersonate();
            }

            _hWinSta = Win32API.OpenWindowStation("WinSta0", false, Win32API.MAXIMUM_ALLOWED);
            if (_hWinSta == IntPtr.Zero)
                return false;
            if (!Win32API.SetProcessWindowStation(_hWinSta))
                return false;
            HDesktop = Win32API.OpenDesktop("Default", 0, true, Win32API.MAXIMUM_ALLOWED);

            if (HDesktop == IntPtr.Zero)
            {
                Win32API.SetProcessWindowStation(_hSaveWinSta);
                Win32API.CloseWindowStation(_hWinSta);
                return false;
            }

            if (!Win32API.SetThreadDesktop(HDesktop))
                return false;
            return true;
        }

        public int CreateProcessAsUser(string app, string cmd)
        {
            var pi = new PROCESS_INFORMATION();
            try
            {
                var sa = new SECURITY_ATTRIBUTES();
                sa.Length = Marshal.SizeOf(sa);
                var si = new STARTUPINFO();
                si.cb = Marshal.SizeOf(si);
                si.lpDesktop = string.Empty;
                if (app != null && app.Length == 0)
                    app = null;
                if (cmd != null && cmd.Length == 0)
                    cmd = null;
                if (!Win32API.CreateProcessAsUser(
                    _userTokenHandle,
                    app,
                    cmd,
                    ref sa, ref sa,
                    false, 0, IntPtr.Zero,
                    @"C:\", ref si, ref pi
                ))
                {
                    var error = Marshal.GetLastWin32Error();
                    var ex = new Win32Exception(error);
                    var message = string.Format("CreateProcessAsUser Error: {0}", ex.Message);
                    throw new ApplicationException(message, ex);
                }
            }
            catch (Exception ex)
            {
                EventLog.WriteEntry("Screen Monitor", ex.Message, EventLogEntryType.Error, 1, 1);
                throw;
            }
            finally
            {
                if (pi.hProcess != IntPtr.Zero)
                    Win32API.CloseHandle(pi.hProcess);
                if (pi.hThread != IntPtr.Zero)
                    Win32API.CloseHandle(pi.hThread);
            }

            return pi.dwProcessID;
        }

        private bool UndoDesktop()
        {
            if (_impersonatedUser != null)
            {
                _impersonatedUser.Undo();
                _impersonatedUser.Dispose();
            }

            if (_hSaveWinSta != IntPtr.Zero)
                Win32API.SetProcessWindowStation(_hSaveWinSta);
            if (_hSaveDesktop != IntPtr.Zero)
                Win32API.SetThreadDesktop(_hSaveDesktop);
            if (_hWinSta != IntPtr.Zero)
                Win32API.CloseWindowStation(_hWinSta);
            if (HDesktop != IntPtr.Zero)
                Win32API.CloseDesktop(HDesktop);
            return true;
        }
    }
}