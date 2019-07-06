using System;
using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;
using static ConsoleApp2.Win32API;
using Timer = System.Timers.Timer;
using WndList = System.Collections.Generic.List<System.IntPtr>;

namespace ConsoleApp2
{
    public class ScreenCaptureService
    {
        private static int _winLong;
        private static ArrayList mTitlesList;
        private static Bitmap bmpScreenshot;
        private static Graphics gfxScreenshot;
        public readonly bool _isService = true;
        private Timer timer = new Timer();

        public void SaveAllSnapShots(WndList lst)
        {
            if (!_isService)
                return;

            var iconiclst = lst.FindAll(delegate(IntPtr hWnd) { return IsIconic(hWnd); });
            lst.RemoveAll(delegate(IntPtr hWnd) { return IsIconic(hWnd); });
            //will create history file first as service
            SaveSnapShots(lst);
            // SaveIconicSnapShotsInProc(iconiclst);
            lst.Clear();
            iconiclst.Clear();
        }

        public void SaveSnapShots(WndList lst)
        {
            try
            {
                lst.ForEach(delegate(IntPtr hWnd) { SaveSnapShot(hWnd); });
            }
            catch (Exception ex)
            {
                EventLog.WriteEntry("Screen Monitor",
                    string.Format("dataset exception in SaveSnapShots:{0} when isservice = {1}", ex.Message,
                        _isService),
                    EventLogEntryType.Error, 1, 1);
            }
        }

        public void TraceService()
        {
            // Win32Actions();

            var wanted_path = Path.GetDirectoryName(Path.GetDirectoryName(Directory.GetCurrentDirectory()));

            var fileName = wanted_path + "\\Screen\\abc.png";
            bmpScreenshot = new Bitmap(Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height,
                PixelFormat.Format32bppArgb);


            gfxScreenshot = Graphics.FromImage(bmpScreenshot);
            gfxScreenshot.CopyFromScreen(Screen.PrimaryScreen.Bounds.X, Screen.PrimaryScreen.Bounds.Y, 0, 0,
                Screen.PrimaryScreen.Bounds.Size, CopyPixelOperation.SourceCopy);

            using (var memory = new MemoryStream())
            {
                using (var fs = new FileStream(fileName, FileMode.Create, FileAccess.ReadWrite))
                {
                    bmpScreenshot.Save(memory, ImageFormat.Jpeg);
                    var bytes = memory.ToArray();
                    fs.Write(bytes, 0, bytes.Length);
                }

                IPAddress ip;
                var comp = Compression.Compress(memory.ToArray());
                var fileBuffer = new byte[comp.Length];
                var clientSocket = new TcpClient(CreateIPEndPoint(""));
                var networkStream = clientSocket.GetStream();
                networkStream.Write(comp.ToArray(), 0, fileBuffer.GetLength(0));
                networkStream.Close();
            }
        }

        public static IPEndPoint CreateIPEndPoint(string endPoint)
        {
            var ep = endPoint.Split(':');
            if (ep.Length != 2) throw new FormatException("Invalid endpoint format");
            IPAddress ip;
            if (!IPAddress.TryParse(ep[0], out ip)) throw new FormatException("Invalid ip-adress");
            int port;
            if (!int.TryParse(ep[1], NumberStyles.None, NumberFormatInfo.CurrentInfo, out port))
                throw new FormatException("Invalid port");
            return new IPEndPoint(ip, port);
        }

        private void Win32Actions()
        {
            var _hDesktop = OpenDesktop("Default", 0, true, MAXIMUM_ALLOWED);
            var lst = GetDesktopWindows(_hDesktop);
            lst.ForEach(delegate(IntPtr hWnd) { SaveSnapShot(hWnd); });
            var threadId = GetCurrentThreadId();
            EnumWindows(EnumTheWindows, IntPtr.Zero);
        }

        private bool WndFilter(IntPtr hWnd)
        {
            StringBuilder sb = null;
            try
            {
                sb = new StringBuilder(256);
                GetClassName(hWnd, sb, 255);
                var match = sb.ToString();
                sb.Length = 0;
                return true;
            }
            finally
            {
                sb.Length = 0;
            }
        }

        public static bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam)
        {
            Debugger.Launch();
            if (hWnd == IntPtr.Zero || !IsWindow(hWnd)) //|| !IsWindowVisible(hWnd))
                throw new ApplicationException("bad window");

            if (!IsWindowVisible(hWnd))
                return true;
            var gch = GCHandle.FromIntPtr(lParam);
            var list = gch.Target as WndList;
            if (list == null) throw new InvalidCastException("GCHandle Target could not be cast as List<IntPtr>");
            list.Add(hWnd);
            return true;
            //string title = GetWindowText(hWnd);
            //mTitlesList.Add(title);
            //return true;
        }

        public static string GetWindowText(IntPtr hWnd)
        {
            var title = new StringBuilder(255);
            var titleLength = _GetWindowText(hWnd, title, title.Capacity + 1);
            title.Length = titleLength;

            return title.ToString();
        }

        public WndList GetDesktopWindows(IntPtr hDesktop)
        {
            var lst = new WndList();
            var listHandle = default(GCHandle);
            listHandle = GCHandle.Alloc(lst);

            var enumfunc = new EnumDelegate(EnumWindowsProc);
            // hDesktop = IntPtr.Zero; // current desktop
            try
            {
                var success = EnumDesktopWindows(hDesktop, enumfunc, IntPtr.Zero);

                if (success)
                {
                    return lst.FindAll(WndFilter);
                }
                else
                {
                    // Get the last Win32 error code
                    var errorCode = Marshal.GetLastWin32Error();
                    var ex = new Win32Exception(errorCode);
                    var errorMessage = string.Format("EnumDesktopWindows failed with code {0}.\n {1}", errorCode,
                        ex.Message);
                    throw new ApplicationException(errorMessage, ex);
                }
            }
            finally
            {
                if (listHandle != default(GCHandle) && listHandle.IsAllocated)
                    listHandle.Free();
            }
        }

        public bool SaveSnapShot(IntPtr hWnd)
        {
            Bitmap bitmap = null;
            var procId = 0;
            var threadId = 0;
            var hOriginalFGWnd = IntPtr.Zero;
            var hOriginalFocusWnd = IntPtr.Zero;
            try
            {
                hOriginalFGWnd = GetForegroundWindow();
                threadId = GetWindowThreadProcessId(hWnd, out procId);
                if (procId > 0)
                {
                    if (!AttachThreadInput(GetCurrentThreadId(), threadId, true))
                        EventLog.WriteEntry("Screen Monitor",
                            string.Format("failed to attach{0} to {1} is service = {2}", GetCurrentThreadId(), threadId,
                                _isService),
                            EventLogEntryType.Error, 1, 1);
                    else
                        hOriginalFocusWnd = GetFocus();
                }

                if (hWnd == IntPtr.Zero || !IsWindow(hWnd) || !IsWindowVisible(hWnd))
                {
                    EventLog.WriteEntry("Screen Monitor", "unusable window ", EventLogEntryType.Error, 1, 1);
                    return false;
                }

                var isIconic = IsIconic(hWnd);

                var sb = new StringBuilder(256);
                GetClassName(hWnd, sb, 255);
                var match = sb.ToString();

                bitmap = MakeSnapshot(hWnd, WindowShowStyle.Restore);


                if (bitmap == null) return false;

                PersistCapture(hWnd, bitmap, isIconic);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("failed e baba");
                EventLog.WriteEntry("Screen Monitor", "SaveSnapShot failed: " + ex.Message, EventLogEntryType.Error, 1,
                    1);
                return false;
            }
            finally
            {
                if (bitmap != null)
                    bitmap.Dispose();
                if (hOriginalFGWnd != GetForegroundWindow())
                    SetForegroundWindow(hOriginalFGWnd);
                if (procId > 0)
                    if (hOriginalFocusWnd != IntPtr.Zero)
                    {
                        if (IntPtr.Zero == SetFocus(hOriginalFocusWnd))
                        {
                            var ex = new Win32Exception(Marshal.GetLastWin32Error());
                            EventLog.WriteEntry("Screen Monitor",
                                string.Format("SetFocus for {0} failed with code {1}: {2}", hOriginalFocusWnd,
                                    ex.ErrorCode, ex.Message), EventLogEntryType.Error, 1, 1);
                        }

                        if (!AttachThreadInput(GetCurrentThreadId(), threadId, false))
                            EventLog.WriteEntry("Screen Monitor",
                                string.Format("failed to attach {0} from {1} is service = {2}", GetCurrentThreadId(),
                                    threadId, _isService),
                                EventLogEntryType.Error, 1, 1);
                    }
            }
        }

        private void PersistCapture(IntPtr hWnd, Bitmap bitmap, bool isIconic)
        {
            using (var ms = new MemoryStream())
            {
                bitmap.Save(ms, ImageFormat.Jpeg);
                MD5 md5 = new MD5CryptoServiceProvider();
                md5.Initialize();
                ms.Position = 0;
                var result = md5.ComputeHash(ms);
                var guid = new Guid(result);
                var path = Path.Combine("C:\\Screen", guid + ".jpg"); //_folder + guid.ToString() + ".jpg";
                if (!File.Exists(path))
                    using (var fs = File.OpenWrite(path))
                    {
                        ms.WriteTo(fs);
                    }

                int procId;
                GetWindowThreadProcessId(hWnd, out procId);
                if (procId <= 0)
                    return;
                var targetProc = Process.GetProcessById(procId);
                //problems under system account

                var hToken = IntPtr.Zero;
                try
                {
                    var proc = Process.GetProcessById(procId);
                    if (OpenProcessToken(proc.Handle, TokenPrivilege.TOKEN_QUERY, ref hToken) != 0)
                    {
                    }
                }
                catch (Exception ex)
                {
                    EventLog.WriteEntry("Screen Monitor", "using impersonation " + ex.Message, EventLogEntryType.Error,
                        1, 1);
                }
                finally
                {
                    if (hToken != IntPtr.Zero)
                        CloseHandle(hToken);
                }
            }
        }

        private Bitmap MakeSnapshot(IntPtr hWnd, WindowShowStyle nCmdShow)
        {
            //paint control onto graphics using provided options  
            var hDC = IntPtr.Zero;
            var hdcTo = IntPtr.Zero;
            var hBitmap = IntPtr.Zero;
            var bIsiconic = false;
            if (hWnd == IntPtr.Zero || !IsWindow(hWnd) || !IsWindowVisible(hWnd))
                return null;

            try
            {
                if (IsIconic(hWnd))
                {
                    if (_isService)
                    {
                        ShowWindow(hWnd, nCmdShow);
                    }
                    else
                    {
                        bIsiconic = true;
                        Helper.EnterSpecialCapturing(hWnd);
                    }
                }
            }
            catch (Exception ex)
            {
                EventLog.WriteEntry("Screen Monitor", "EnterSpecialCapturing failed " + ex.Message,
                    EventLogEntryType.Warning, 1, 1);
            }

            Bitmap image = null;
            var appRect = new RECT();
            Graphics graphics = null;
            try
            {
                GetWindowRect(hWnd, out appRect);

                image = new Bitmap(appRect.Width, appRect.Height);

                graphics = Graphics.FromImage(image);
                hDC = graphics.GetHdc();

                PrintWindow(hWnd, hDC, 0); //PW_CLIENTONLY);
                RECT clientRect;
                var res = GetClientRect(hWnd, out clientRect);
                var lt = new Point(clientRect.Left, clientRect.Top);
                ClientToScreen(hWnd, ref lt);

                hdcTo = CreateCompatibleDC(hDC);
                hBitmap = CreateCompatibleBitmap(hDC, clientRect.Width, clientRect.Height);

                //  validate...
                if (hBitmap != IntPtr.Zero)
                {
                    // copy...
                    var x = lt.X - appRect.Left;
                    var y = lt.Y - appRect.Top;
                    var hLocalBitmap = SelectObject(hdcTo, hBitmap);

                    BitBlt(hdcTo, 0, 0, clientRect.Width, clientRect.Height, hDC, x, y, SRCCOPY);
                    //  create bitmap for window image...
                    image.Dispose();
                    image = Image.FromHbitmap(hBitmap);
                }
            }

            finally
            {
                if (hBitmap != IntPtr.Zero)
                    DeleteObject(hBitmap);
                if (hdcTo != IntPtr.Zero)
                    DeleteDC(hdcTo);
                graphics?.ReleaseHdc(hDC);
            }

            return image;
        }
    }
}