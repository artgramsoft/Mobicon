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

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern IntPtr WindowFromPoint(POINT Point);

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

    // Caching for performance
    private static IntPtr _lastHWnd = IntPtr.Zero;
    private static string _lastProcessName = "";
    private static uint _lastPid = 0;
    private static DateTime _lastCacheTime = DateTime.MinValue;

    // Key tracking for safety release
    private static readonly HashSet<int> _sentKeys = new();

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

        // Background thread to refresh status (periodically checks active state)
        Task.Run(async () =>
        {
            while (true)
            {
                IsTargetActive(); // Keep _lastTargetActive updated
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
        bool active = _lastTargetActive;
        
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
        catch { }
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
            UpdateStatus();
        }
        catch { }
    }

    private static bool IsTargetActive()
    {
        IntPtr fgWnd = GetForegroundWindow();
        if (fgWnd == IntPtr.Zero) return SetActiveState(false);

        // Performance: Cache process name by HWnd
        string procName;
        if (fgWnd == _lastHWnd && (DateTime.Now - _lastCacheTime).TotalSeconds < 2)
        {
            procName = _lastProcessName;
        }
        else
        {
            GetWindowThreadProcessId(fgWnd, out uint pid);
            _lastHWnd = fgWnd;
            _lastPid = pid;
            try
            {
                using var p = Process.GetProcessById((int)pid);
                procName = p.ProcessName;
            }
            catch { procName = ""; }
            _lastProcessName = procName;
            _lastCacheTime = DateTime.Now;
        }

        bool isTargetProcess = procName.Equals("MabinogiMobile", StringComparison.OrdinalIgnoreCase);
        if (!isTargetProcess) return SetActiveState(false);

        // Click-through prevention: Verify window under cursor belongs to target process
        bool isMouseOver = false;
        if (GetCursorPos(out POINT pt))
        {
            IntPtr wndAtPt = WindowFromPoint(pt);
            if (wndAtPt != IntPtr.Zero)
            {
                GetWindowThreadProcessId(wndAtPt, out uint pidAtPt);
                if (pidAtPt == _lastPid)
                {
                    isMouseOver = true;
                }
            }
        }

        return SetActiveState(isTargetProcess && isMouseOver);
    }

    private static bool SetActiveState(bool active)
    {
        if (active != _lastTargetActive)
        {
            _lastTargetActive = active;
            // Log($"Active State: {(active ? "MATCH" : "NO MATCH")}");
            
            if (!active)
            {
                _triggerActive = false;
                ReleaseAllKeys(); // Fix Key Sticking
            }
            UpdateStatus();
        }
        return active;
    }

    private static void ReleaseAllKeys()
    {
        if (_sentKeys.Count == 0) return;
        
        var keys = _sentKeys.ToArray();
        foreach (var vk in keys)
        {
            SendKeyInternal((short)vk, true);
        }
        _sentKeys.Clear();
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
            
            if (vkCode == VK_TAB && isKeyDown)
            {
                if (IsTargetActive())
                {
                    _combatMode = !_combatMode;
                    // Log($"Combat Mode: {(_combatMode ? "ON" : "OFF")}");
                    if (!_combatMode) ReleaseAllKeys();
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
                if (oldCtrl != _ctrlPressed) 
                {
                    if (_ctrlPressed) ReleaseAllKeys();
                    UpdateStatus();
                }
            }

            bool effectivelyCombat = _combatMode && !_ctrlPressed;

            if (effectivelyCombat && _triggerActive && KeyMap.ContainsKey(vkCode))
            {
                if (isKeyDown) SendKeyInternal((short)KeyMap[vkCode], false);
                else if (isKeyUp) SendKeyInternal((short)KeyMap[vkCode], true);
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
                    ReleaseAllKeys();
                    UpdateStatus();
                    // If we intercepted the down, we must intercept the up
                    return wasActive ? new IntPtr(1) : CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
                }
            }
        }

        return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }

    private static void SendKeyInternal(short vk, bool up)
    {
        if (up) _sentKeys.Remove(vk);
        else _sentKeys.Add(vk);

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
