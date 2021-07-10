using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using static ScrollReverser.User32dll;

namespace ScrollReverser
{
    public class Reverser
    {
        private readonly HookProc _reverseHookCallback;
        private IntPtr _reverseHookPtr;
        
        private const int MOUSEEVENTF_WHEEL = 0x800;
        private const int WH_MOUSE_LL = 14;
        private const int WM_MOUSEWHEEL = 0x020A;
        
        private const int OUR_REVERSE_EXTRA_INFO = 0x0fefefe0;

        public Reverser()
        {
            _reverseHookCallback = ReverseScrollWheel;
        }

        public void SetHook()
        {
            Log("SetWindowsHookEx call");

            var module = Marshal.GetHINSTANCE(Assembly.GetExecutingAssembly().GetModules()[0]);
            _reverseHookPtr = SetWindowsHookEx(WH_MOUSE_LL, _reverseHookCallback, module, 0);

            if (_reverseHookPtr == IntPtr.Zero)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "SetWindowsHookEx call failed");

            Log("SetWindowsHookEx done");

            // pump
            while (!GetMessage(out var msg, IntPtr.Zero, 0, 0))
            {
                TranslateMessage(ref msg);
                DispatchMessage(ref msg);
            }
        }

        public void Unhook()
        {
            if (_reverseHookPtr == IntPtr.Zero) return;
            
            Log("UnhookWindowsHookEx call");
            if (!UnhookWindowsHookEx(_reverseHookPtr))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "UnhookWindowsHookEx call failed");

            _reverseHookPtr = IntPtr.Zero;
            
            Log("UnhookWindowsHookEx done");
        }
        
        private unsafe int ReverseScrollWheel(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode < 0)
                // pass event further
                return CallNextHookEx(_reverseHookPtr, nCode, wParam, lParam);

            var wmMouse = wParam.ToInt64();
            if (wmMouse != WM_MOUSEWHEEL)
                return CallNextHookEx(_reverseHookPtr, nCode, wParam, lParam);
            
            var data = *(MSLLHOOKSTRUCT*)lParam.ToPointer();

            var isReversedByUs = data.dwExtraInfo.ToInt64() == OUR_REVERSE_EXTRA_INFO;
            Log($"WM_MOUSEWHEEL: x: {data.pt.x} y: {data.pt.y} {data.flags:x8} {data.mouseData:x8} {data.dwExtraInfo.ToInt64():x16}, ours: {isReversedByUs}");

            if (isReversedByUs)
            {
                Log("Our scroll, sending further");
                return CallNextHookEx(_reverseHookPtr, nCode, wParam, lParam);
            }
            
            Log("External scroll, reversing");

            var wheelDelta = data.mouseData >> 16;
            
            // do not send events from the same thread that handles the hook :)
            new Thread(() => mouse_event(MOUSEEVENTF_WHEEL, 0, 0, -wheelDelta, new IntPtr(OUR_REVERSE_EXTRA_INFO)))
            {
                Priority = ThreadPriority.Highest,
                IsBackground = false
            }.Start();
            
            // mark event as handled
            return 1;
        }

        private static void Log(string value)
        {
            Console.WriteLine($"[{Stopwatch.GetTimestamp()}] [{Thread.CurrentThread.ManagedThreadId}] {value}");
        }

        // ReSharper disable FieldCanBeMadeReadOnly.Local
        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT
        {
            public POINT pt; 
            public int mouseData;
            public int flags;
            // ReSharper disable once MemberCanBePrivate.Local
            public int time; 
            public IntPtr dwExtraInfo;
        }
        // ReSharper restore FieldCanBeMadeReadOnly.Local
    }
}