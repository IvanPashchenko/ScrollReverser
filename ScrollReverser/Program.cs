using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using static ScrollReverser.User32dll;

namespace ScrollReverser
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "test")
            {
                OpenNotepadWithText();
            }

            var reverser = new Reverser();
            try
            {
                AppDomain.CurrentDomain.ProcessExit += (_, _) => reverser.Unhook();
                Console.CancelKeyPress += (_, _) => reverser.Unhook();
                
                new Thread(() => reverser.SetHook())
                {
                    Priority = ThreadPriority.Highest,
                    IsBackground = false
                }.Start();
                
                Thread.Sleep(int.MaxValue);
            }
            finally
            {
                reverser.Unhook();
            }
        }

        private static void OpenNotepadWithText()
        {
            var lines = Enumerable.Range(1, 200).Select(i => i.ToString()).ToList();
            File.WriteAllLines("test.txt", lines);

            var process = Process.Start("notepad.exe", "test.txt");
            SpinWait.SpinUntil(() => IsWindowVisible(process.MainWindowHandle));

            var pos = new Rect();
            GetWindowRect(process.MainWindowHandle, ref pos);

            // shrink horizontally
            var x = pos.Left + pos.Right - 200;
            var y = pos.Top;
            MoveWindow(process.MainWindowHandle, x, y, 200, pos.Bottom - pos.Top, true);
            SetCursorPos(x + 100, y + 100);
        }
    }
}