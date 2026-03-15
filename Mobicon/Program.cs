using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Mobicon;

class Program
{
    // Win32 Constants
    private const int WH_KEYBOARD_LL = 13;
    private const int WH_MOUSE_LL = 14;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_SYSKEYUP = 0x0105;
    private const int WM_LBUTTONDOWN = 0x0201;
    private const int WM_LBUTTONUP = 0x0202;

    private const int INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    // Virtual Keys
    private const int VK_TAB = 0x09;
    private const int VK_CONTROL = 0x11;
    private const int VK_LCONTROL = 0xA2;
    private const int VK_RCONTROL = 0xA3;
    private const int VK_A = 0x41;
    private const int VK_S = 0x53;
    private const int VK_D = 0x44;
    private const int VK_Q = 0x51;
    private const int VK_W = 0x57;
    private const int VK_E = 0x45;
    private const int VK_Z = 0x5A;
    private const int VK_X = 0x58;
    private const int VK_C = 0x43;
    private const int VK_F = 0x46;
    private const int VK_1 = 0x31;
    private const int VK_2 = 0x32;
    private const int VK_3 = 0x33;
    private const int VK_4 = 0x34;
    private const int VK_5 = 0x35;
    private const int VK_6 = 0x36;
    private const int VK_7 = 0x37;
    private const int VK_8 = 0x38;
    private const int VK_9 = 0x39;
    private const int VK_0 = 0x30;

    // Delegates
    private delegate IntPtr LowLevelProc(int nCode, IntPtr wParam, IntPtr lParam);

    // Win32 API Imports
    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern bool GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage([In] ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage([In] ref MSG lpMsg);

    // Structs
    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public int vkCode;
        public int scanCode;
        public int flags;
        public int time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public POINT pt;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int x;
        public int y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public int type;
        public InputUnion u;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
        [FieldOffset(0)] public HARDWAREINPUT hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public short wVk;
        public short wScan;
        public uint dwFlags;
        public int time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HARDWAREINPUT
    {
        public uint uMsg;
        public short wParamL;
        public short wParamH;
    }

    // State Variables
    private static LowLevelProc _kbHookProc = KeyboardHookCallback;
    private static LowLevelProc _mHookProc = MouseHookCallback;
    private static IntPtr _kbHook = IntPtr.Zero;
    private static IntPtr _mHook = IntPtr.Zero;

    private static bool _combatMode = false;
    private static bool _triggerActive = false;
    private static bool _ctrlPressed = false;
    private static bool _lastTargetActive = false;

    private static readonly Dictionary<int, int> KeyMap = new()
    {
        { VK_A, VK_1 },
        { VK_S, VK_2 },
        { VK_D, VK_3 },
        { VK_Q, VK_4 },
        { VK_W, VK_5 },
        { VK_E, VK_6 },
        { VK_Z, VK_7 },
        { VK_X, VK_8 },
        { VK_C, VK_9 },
        { VK_F, VK_0 }
    };

    static void Main(string[] args)
    {
        using var curProcess = Process.GetCurrentProcess();
        string moduleName = curProcess.MainModule?.ModuleName ?? "Mobicon";
        IntPtr hMod = GetModuleHandle(moduleName);

        _kbHook = SetWindowsHookEx(WH_KEYBOARD_LL, _kbHookProc, hMod, 0);
        _mHook = SetWindowsHookEx(WH_MOUSE_LL, _mHookProc, hMod, 0);

        Console.Clear();
        Console.WriteLine("==================================================");
        Console.WriteLine(" Mobicon - Combat Input Mapper");
        Console.WriteLine("==================================================");
        Console.WriteLine(" Target: MabinogiMobile.exe");
        Console.WriteLine(" TAB: Toggle Combat Mode | CTRL: Temp Disable");
        Console.WriteLine(" Mouse Left: Trigger (In Combat Mode)");
        Console.WriteLine("==================================================");
        Console.WriteLine(); // Reserved for status line (Line 7)
        Console.WriteLine(" [LOGS]");

        UpdateStatus();

        // Background thread to refresh status (especially process active state)
        Task.Run(async () =>
        {
            while (true)
            {
                UpdateStatus();
                await Task.Delay(500);
            }
        });

        MSG msg;
        while (GetMessage(out msg, IntPtr.Zero, 0, 0))
        {
            TranslateMessage(ref msg);
            DispatchMessage(ref msg);
        }

        if (_kbHook != IntPtr.Zero) UnhookWindowsHookEx(_kbHook);
        if (_mHook != IntPtr.Zero) UnhookWindowsHookEx(_mHook);
    }

    private static void UpdateStatus()
    {
        bool active = IsTargetActive();
        
        // Only update if something changed to reduce flickering/CPU
        // But we refresh anyway to keep UI responsive
        
        string status = $" STATUS: [Process: {(active ? "ACTIVE  " : "INACTIVE")}] " +
                        $"[Combat: {(_combatMode ? "ON " : "OFF")}] " +
                        $"[Trigger: {(_triggerActive ? "ON " : "OFF")}] " +
                        $"[Ctrl: {(_ctrlPressed ? "ON " : "OFF")}]";

        try
        {
            int oldLeft = Console.CursorLeft;
            int oldTop = Console.CursorTop;

            Console.SetCursorPosition(0, 7);
            
            if (active) Console.ForegroundColor = ConsoleColor.Green;
            else Console.ForegroundColor = ConsoleColor.Gray;

            if (_combatMode && active)
            {
                if (_ctrlPressed) Console.ForegroundColor = ConsoleColor.Yellow;
                else Console.ForegroundColor = ConsoleColor.Cyan;
            }

            Console.Write(status.PadRight(Console.WindowWidth - 1));
            Console.ResetColor();

            // Restore cursor if it was in the log area
            if (oldTop > 7) Console.SetCursorPosition(oldLeft, oldTop);
            else Console.SetCursorPosition(0, 9);
        }
        catch { /* Console might be resized or redirected */ }
    }

    private static void Log(string message)
    {
        try
        {
            int oldLeft = Console.CursorLeft;
            int oldTop = Console.CursorTop;
            
            // Logs start from line 9
            if (oldTop < 9) Console.SetCursorPosition(0, 9);
            
            Console.WriteLine($" [{DateTime.Now:HH:mm:ss}] {message}");
            UpdateStatus(); // Refresh status line whenever we log
        }
        catch { }
    }

    private static bool IsTargetActive()
    {
        IntPtr hWnd = GetForegroundWindow();
        if (hWnd == IntPtr.Zero) return false;

        GetWindowThreadProcessId(hWnd, out uint pid);
        try
        {
            using var p = Process.GetProcessById((int)pid);
            string currentName = p.ProcessName;
            bool active = currentName.Equals("MabinogiMobile", StringComparison.OrdinalIgnoreCase);
            
            if (active != _lastTargetActive)
            {
                _lastTargetActive = active;
                Log($"Active Window Changed: {currentName} (Target: {(active ? "MATCH" : "NO MATCH")})");
                if (!active)
                {
                    _combatMode = false;
                    _triggerActive = false;
                }
                UpdateStatus();
            }
            return active;
        }
        catch
        {
            return false;
        }
    }

    private static IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var kbd = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            int vkCode = kbd.vkCode;
            int msg = wParam.ToInt32();
            bool isKeyDown = msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN;
            bool isKeyUp = msg == WM_KEYUP || msg == WM_SYSKEYUP;
            
            // DEBUG: Tab 키 누를 때 활성 프로세스 로그 출력
            if (vkCode == VK_TAB && isKeyDown)
            {
                IntPtr hWnd = GetForegroundWindow();
                GetWindowThreadProcessId(hWnd, out uint pid);
                string procName = "Unknown";
                try { using var p = Process.GetProcessById((int)pid); procName = p.ProcessName; } catch { }
                
                Log($"Tab Pressed. Active Process: {procName}");
                
                if (IsTargetActive())
                {
                    _combatMode = !_combatMode;
                    Log($"Combat Mode Toggle: {(_combatMode ? "ON" : "OFF")}");
                    UpdateStatus();
                    return new IntPtr(1);
                }
            }

            if (!IsTargetActive())
            {
                return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
            }

            if (vkCode == VK_CONTROL || vkCode == VK_LCONTROL || vkCode == VK_RCONTROL)
            {
                bool oldCtrl = _ctrlPressed;
                if (isKeyDown) _ctrlPressed = true;
                if (isKeyUp) _ctrlPressed = false;
                if (oldCtrl != _ctrlPressed) UpdateStatus();
            }

            bool effectivelyCombat = _combatMode && !_ctrlPressed;

            if (effectivelyCombat && _triggerActive && KeyMap.ContainsKey(vkCode))
            {
                if (isKeyDown) SendKey((short)KeyMap[vkCode], false);
                else if (isKeyUp) SendKey((short)KeyMap[vkCode], true);
                return new IntPtr(1);
            }
        }

        return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }

    private static IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int msg = wParam.ToInt32();

            if (!IsTargetActive())
            {
                return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
            }

            bool effectivelyCombat = _combatMode && !_ctrlPressed;

            if (msg == WM_LBUTTONDOWN)
            {
                if (effectivelyCombat)
                {
                    _triggerActive = true;
                    UpdateStatus();
                    return new IntPtr(1);
                }
            }
            else if (msg == WM_LBUTTONUP)
            {
                bool wasActive = _triggerActive;
                _triggerActive = false;
                if (effectivelyCombat || wasActive)
                {
                    UpdateStatus();
                    return new IntPtr(1);
                }
            }
        }

        return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }

    private static void SendKey(short vk, bool up)
    {
        var inputs = new INPUT[]
        {
            new INPUT
            {
                type = INPUT_KEYBOARD,
                u = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = vk,
                        dwFlags = up ? KEYEVENTF_KEYUP : 0
                    }
                }
            }
        };

        SendInput(1, inputs, Marshal.SizeOf(typeof(INPUT)));
    }
}
