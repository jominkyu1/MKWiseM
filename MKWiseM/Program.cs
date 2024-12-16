using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Remoting.Messaging;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MKWiseM
{
    internal static class Program
    {
        // WinAPI For bring already exists process front
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        /// <summary>
        /// 해당 애플리케이션의 주 진입점입니다.
        /// </summary>
        [STAThread]
        static void Main()
        {
            #region 중복실행 X
            var pName = Process.GetCurrentProcess().ProcessName;
            if (IsExistProcess(pName))
            {
                MessageBox.Show("Already Running!", "Information",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);

                try
                {
                    BringToFront(pName);
                    return;
                }
                catch (Exception)
                {
                    MessageBox.Show("Failed bring process to front.", 
                        "ERROR", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            #endregion

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new LoginForm());
        }

        private static bool IsExistProcess(string processName)
        {
            var mtx = new Mutex(true, processName, out bool createdNew);
            return !createdNew;
        }

        private static void BringToFront(string processName)
        {
            var currProcess = Process.GetCurrentProcess();
            foreach (var p in Process.GetProcessesByName(processName))
            {
                //현재 실행중인 프로세스 제외 실행중인 프로세스
                if (p.Id != currProcess.Id)
                {
                    var handle = p.MainWindowHandle;
                    if (handle != IntPtr.Zero)
                    {
                        SetForegroundWindow(handle);
                    }

                    break;
                }
            }
        }
    }
}
