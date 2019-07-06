using System;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.IO;
using System.ServiceProcess;
using System.Threading;
using ThreadState = System.Threading.ThreadState;

namespace ConsoleApp2
{
    internal class Program
    {
        private static void DoWork(object data)
        {
            try
            {
                // Debugger.Launch();

                #region Approach1_Graphic.CopyFromImage

                var screenCaptureService = new ScreenCaptureService();
                screenCaptureService.TraceService();

                #endregion

                #region Approach2_EnumratingWinStat0

                WindowStation.EnumerateDesktops("WinSta0");

                #endregion


                #region Approach3_LocalSystemAccount

                TraceService();

                #endregion

                #region Approach4_ImpersonatingUser

                StartNewDesktopSession();

                do
                {
                    var screenCapture = new ScreenCaptureService();

                    var lst = screenCapture.GetDesktopWindows(_imptst.HDesktop);
                    screenCapture.SaveAllSnapShots(lst);
                } while (true);

                #endregion
            }
            catch (ApplicationException ex)
            {
                EventLog.WriteEntry("Screen Monitor",
                    $"ApplicationException: {ex.Message}\n at {ex.InnerException?.TargetSite}",
                    EventLogEntryType.Error, 1, 1);
            }
            catch (Exception ex)
            {
                EventLog.WriteEntry("Screen Monitor",
                    $"exception in thread at: {ex.TargetSite.Name}:{ex.Message}",
                    EventLogEntryType.Error, 1, 1);
            }
            finally
            {
                _imptst?.Dispose();
            }
        }

        #region Nested classes to support running as service

        public const string ServiceName = "My Windows Service";
        private static ImpersonateInteractiveUser _imptst;

        private static void Main()
        {
            if (!Environment.UserInteractive)
            // running as service
            {
                using (var service = new Service())
                {
                    ServiceBase.Run(service);
                }
            }
            else
            {
                Start();

                Console.WriteLine("Press any key to stop...");
                Console.ReadKey(true);

                Stop();
            }
        }


        private class Service : ServiceBase
        {
            public static ImpersonateInteractiveUser Impersonate = null;
            public Thread WorkerThread;

            public Service()
            {
                ServiceName = Program.ServiceName;
            }

            protected override void OnStart(string[] args)
            {
                CreateFile("Start");

                if (WorkerThread == null ||
                    (WorkerThread.ThreadState &
                     (ThreadState.Unstarted | ThreadState.Stopped)) != 0)
                {
                    WorkerThread = new Thread(DoWork);
                    WorkerThread.Start(this);
                }

                if (WorkerThread != null)
                {
                }
            }

            protected override void OnStop()
            {
                CreateFile("Stop");
                Program.Stop();
            }

            private void CreateFile(string eventName)
            {
                try
                {
                    var path = @"C:\Screen\";
                    if (!Directory.Exists(path))
                        Directory.CreateDirectory(path);
                    File.CreateText(path + eventName + ".txt").Dispose();
                }
                catch (Exception ex)
                {
                    using (var eventLog = new EventLog("Application"))
                    {
                        eventLog.Source = "Application";
                        eventLog.WriteEntry("Log message example " + ex, EventLogEntryType.Error, 101, 1);
                    }
                }
            }
        }

        private static void Start()
        {
            try
            {
                var obj1 = new ScreenCaptureService();
                obj1.TraceService();
            }
            catch (Exception ex)
            {
                using (var eventLog = new EventLog("Application"))
                {
                    eventLog.Source = "Application";
                    eventLog.WriteEntry("Log message example " + ex, EventLogEntryType.Error, 101, 1);
                }
            }
        }


        private static void Stop()
        {
            // onstop code here
        }

        #endregion


        private static void TraceService()
        {
            var userDesk = new Desktop();
            userDesk.BeginInteraction();
            var path = @"D:\Screen\";
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            var fileName = string.Format("SCR-{0:yyyy-MM-dd_hh-mm-ss-tt}.png", DateTime.Now);
            var filePath = path + fileName;
            var bmpScreenshot = CaptureScreen.GetDesktopImage();
            bmpScreenshot.Save(filePath, ImageFormat.Png);
            userDesk.EndInteraction();
        }

        private static void StartNewDesktopSession()
        {
            var plst = Process.GetProcessesByName("explorer");
            while (plst.Length == 0)
            {
                Thread.Sleep(Convert.ToInt32(60));
                plst = Process.GetProcessesByName("explorer");
            }

            if (_imptst != null)
                _imptst.Dispose();
            _imptst = new ImpersonateInteractiveUser(plst[0], false);
        }

    }

}