using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Mobicon;

// ─────────────────────────────────────────────
//  Win32 interop layer (P/Invoke declarations)
// ─────────────────────────────────────────────
internal static class Win32
{
    // Hook type identifiers
    public const int WH_KEYBOARD_LL = 13;
    public const int WH_MOUSE_LL    = 14;

    // Window messages
    public const int WM_KEYDOWN    = 0x0100;
    public const int WM_KEYUP      = 0x0101;
    public const int WM_SYSKEYDOWN = 0x0104;
    public const int WM_SYSKEYUP   = 0x0105;
    public const int WM_LBUTTONDOWN = 0x0201;
    public const int WM_LBUTTONUP   = 0x0202;

    // Input / hook flags
    public const int  INPUT_KEYBOARD  = 1;
    public const uint KEYEVENTF_KEYUP = 0x0002;
    public const int  LLKHF_INJECTED  = 0x10;

    // Virtual key codes
    public const int VK_TAB      = 0x09;
    public const int VK_CONTROL  = 0x11;
    public const int VK_LCONTROL = 0xA2;
    public const int VK_RCONTROL = 0xA3;
    public const int VK_A = 0x41; public const int VK_S = 0x53; public const int VK_D = 0x44;
    public const int VK_Q = 0x51; public const int VK_W = 0x57; public const int VK_E = 0x45;
    public const int VK_Z = 0x5A; public const int VK_X = 0x58; public const int VK_C = 0x43;
    public const int VK_F = 0x46;
    public const int VK_0 = 0x30; public const int VK_1 = 0x31; public const int VK_2 = 0x32;
    public const int VK_3 = 0x33; public const int VK_4 = 0x34; public const int VK_5 = 0x35;
    public const int VK_6 = 0x36; public const int VK_7 = 0x37; public const int VK_8 = 0x38;
    public const int VK_9 = 0x39;

    // ── Delegates ──
    public delegate IntPtr LowLevelProc(int nCode, IntPtr wParam, IntPtr lParam);

    // ── Structs ──
    [StructLayout(LayoutKind.Sequential)]
    public struct KBDLLHOOKSTRUCT
    {
        public int vkCode, scanCode, flags, time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MSG
    {
        public IntPtr hwnd;
        public uint   message;
        public IntPtr wParam, lParam;
        public uint   time;
        public POINT  pt;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int x, y; }

    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT { public int type; public InputUnion u; }

    [StructLayout(LayoutKind.Explicit)]
    public struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT    mi;
        [FieldOffset(0)] public KEYBDINPUT    ki;
        [FieldOffset(0)] public HARDWAREINPUT hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KEYBDINPUT
    {
        public short wVk, wScan;
        public uint  dwFlags, time2;  // 'time' renamed to avoid clash
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSEINPUT
    {
        public int dx, dy;
        public uint mouseData, dwFlags, time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct HARDWAREINPUT { public uint uMsg; public short wParamL, wParamH; }

    // ── P/Invoke ──
    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    public static extern bool GetMessage(out MSG lpMsg, IntPtr hWnd, uint min, uint max);

    [DllImport("user32.dll")]
    public static extern bool TranslateMessage(in MSG lpMsg);

    [DllImport("user32.dll")]
    public static extern IntPtr DispatchMessage(in MSG lpMsg);

    [DllImport("user32.dll")]
    public static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    public static extern IntPtr WindowFromPoint(POINT pt);
}

// ─────────────────────────────────────────────
//  Console UI helper
// ─────────────────────────────────────────────
internal static class ConsoleUI
{
    private const int StatusLine  = 14;
    private const int LogHeader   = 15;
    public  const int LogStart    = 16;

    private static readonly bool IsKorean =
        System.Globalization.CultureInfo.CurrentUICulture.Name.StartsWith("ko");

    // Cached width to avoid repeated P/Invoke inside tight draw loops
    private static int _consoleWidth = 80;

    public static void DrawStaticUI()
    {
        _consoleWidth = Console.WindowWidth;
        Console.Clear();
        Console.SetCursorPosition(0, 0);

        if (IsKorean)
        {
            Console.WriteLine("=================================================="); // 0
            Console.WriteLine($" Mobicon {Program.Version} - 전투 입력 매퍼");       // 1
            Console.WriteLine("=================================================="); // 2
            Console.WriteLine(" 대상 프로세스: MabinogiMobile.exe");                 // 3
            Console.WriteLine(" TAB: 전투 모드(ON/OFF) 전환");                       // 4
            Console.WriteLine(" CTRL: 전투 중 일시 중지 (클릭/드래그 시 사용)");     // 5
            Console.WriteLine(" 마우스 왼쪽(Hold): 누르고 있는 동안 키 매핑 활성"); // 6
            Console.WriteLine(" [Ctrl + C]를 눌러 프로그램 종료");                  // 7
            Console.WriteLine("--------------------------------------------------"); // 8
            Console.WriteLine(" [키 매핑 정보]");                                   // 9
            Console.WriteLine(" Q->4, W->5, E->6");                                  // 10
            Console.WriteLine(" A->1, S->2, D->3");                                  // 11
            Console.WriteLine(" Z->7, X->8, C->9, F->0");                            // 12
            Console.WriteLine("=================================================="); // 13
        }
        else
        {
            Console.WriteLine("=================================================="); // 0
            Console.WriteLine($" Mobicon {Program.Version} - Combat Input Mapper");  // 1
            Console.WriteLine("=================================================="); // 2
            Console.WriteLine(" Target: MabinogiMobile.exe");                        // 3
            Console.WriteLine(" TAB: Toggle Combat Mode (ON/OFF)");                  // 4
            Console.WriteLine(" CTRL: Temp Disable (Use for clicking/dragging)");    // 5
            Console.WriteLine(" Mouse Left(Hold): Key mapping active while held");   // 6
            Console.WriteLine(" Press [Ctrl + C] to Exit Safely");                   // 7
            Console.WriteLine("--------------------------------------------------"); // 8
            Console.WriteLine(" [Key Mappings]");                                    // 9
            Console.WriteLine(" Q->4, W->5, E->6");                                  // 10
            Console.WriteLine(" A->1, S->2, D->3");                                  // 11
            Console.WriteLine(" Z->7, X->8, C->9, F->0");                            // 12
            Console.WriteLine("=================================================="); // 13
        }

        // ① 상태 줄을 먼저 그린 뒤 ② 로그 헤더를 출력한다.
        //   (순서가 반대이면 UpdateStatus의 PadRight가 로그 헤더를 덮어씀 — 버그 수정)
        UpdateStatus(Program.State);
        Console.SetCursorPosition(0, LogHeader);
        Console.WriteLine(IsKorean ? " [로그]" : " [LOGS]");
    }

    /// <summary>
    /// 커서 위치를 보존하면서 line 14 상태 표시줄만 갱신한다.
    /// </summary>
    public static void UpdateStatus(in AppState s)
    {
        string status = IsKorean
            ? $" 상태: [프로세스: {(s.TargetActive ? "활성  " : "비활성")}] " +
              $"[전투: {(s.CombatMode ? "켜짐" : "꺼짐")}] " +
              $"[트리거: {(s.TriggerActive ? "켜짐" : "꺼짐")}] " +
              $"[Ctrl: {(s.CtrlPressed ? "켜짐" : "꺼짐")}]"
            : $" STATUS: [Process: {(s.TargetActive ? "ACTIVE  " : "INACTIVE")}] " +
              $"[Combat: {(s.CombatMode ? "ON " : "OFF")}] " +
              $"[Trigger: {(s.TriggerActive ? "ON " : "OFF")}] " +
              $"[Ctrl: {(s.CtrlPressed ? "ON " : "OFF")}]";

        try
        {
            (int savedLeft, int savedTop) = (Console.CursorLeft, Console.CursorTop);

            Console.SetCursorPosition(0, StatusLine);
            Console.ForegroundColor = PickStatusColor(s);
            Console.Write(status.PadRight(_consoleWidth - 1));
            Console.ResetColor();

            // 커서를 로그 영역으로 복원 (정적 UI 영역을 절대 침범하지 않음)
            Console.SetCursorPosition(
                savedLeft,
                savedTop >= LogStart ? savedTop : LogStart);
        }
        catch { /* 콘솔 리사이즈 등 예외 무시 */ }
    }

    public static void Log(string message, in AppState s)
    {
        try
        {
            if (Console.CursorTop < LogStart)
                Console.SetCursorPosition(0, LogStart);

            Console.WriteLine($" [{DateTime.Now:HH:mm:ss}] {message}");
            UpdateStatus(s);
        }
        catch { }
    }

    private static ConsoleColor PickStatusColor(in AppState s)
    {
        if (!s.TargetActive)              return ConsoleColor.Gray;
        if (s.CombatMode && s.CtrlPressed) return ConsoleColor.Yellow;
        if (s.CombatMode)                  return ConsoleColor.Cyan;
        return ConsoleColor.Green;
    }
}

// ─────────────────────────────────────────────
//  Immutable snapshot of runtime state
//  (passed by-ref to avoid heap allocations)
// ─────────────────────────────────────────────
internal readonly struct AppState
{
    public bool TargetActive  { get; init; }
    public bool CombatMode    { get; init; }
    public bool TriggerActive { get; init; }
    public bool CtrlPressed   { get; init; }
}

// ─────────────────────────────────────────────
//  Main program
// ─────────────────────────────────────────────
class Program
{
    public const string Version = "v1.0.3";

    // ── Key mapping ──
    private static readonly IReadOnlyDictionary<int, int> KeyMap =
        new Dictionary<int, int>
        {
            { Win32.VK_A, Win32.VK_1 }, { Win32.VK_S, Win32.VK_2 }, { Win32.VK_D, Win32.VK_3 },
            { Win32.VK_Q, Win32.VK_4 }, { Win32.VK_W, Win32.VK_5 }, { Win32.VK_E, Win32.VK_6 },
            { Win32.VK_Z, Win32.VK_7 }, { Win32.VK_X, Win32.VK_8 }, { Win32.VK_C, Win32.VK_9 },
            { Win32.VK_F, Win32.VK_0 },
        };

    // ── Hook handles ──
    private static Win32.LowLevelProc _kbHookProc  = KeyboardHookCallback;
    private static Win32.LowLevelProc _mHookProc   = MouseHookCallback;
    private static IntPtr _kbHook = IntPtr.Zero;
    private static IntPtr _mHook  = IntPtr.Zero;

    // ── Mutable runtime state (all access under _lock) ──
    private static readonly object _lock = new();
    private static bool _combatMode    = false;
    private static bool _triggerActive = false;
    private static bool _ctrlPressed   = false;
    private static bool _targetActive  = false;

    private static readonly HashSet<int> _sentMappedKeys  = new();
    private static readonly HashSet<int> _physicalKeysDown = new();

    // ── Public snapshot for UI ──
    public static AppState State => new()
    {
        TargetActive  = _targetActive,
        CombatMode    = _combatMode,
        TriggerActive = _triggerActive,
        CtrlPressed   = _ctrlPressed,
    };

    // ── Process detection cache ──
    private static IntPtr  _cachedHWnd    = IntPtr.Zero;
    private static string  _cachedProcName = "";
    private static uint    _cachedPid     = 0;
    private static DateTime _cacheExpiry  = DateTime.MinValue;
    private const double   CacheTtlSec   = 2.0;

    // ─────────────────────────────────────────
    static void Main()
    {
        using var curProcess = Process.GetCurrentProcess();
        IntPtr hMod = Win32.GetModuleHandle(curProcess.MainModule?.ModuleName);

        _kbHook = Win32.SetWindowsHookEx(Win32.WH_KEYBOARD_LL, _kbHookProc, hMod, 0);
        _mHook  = Win32.SetWindowsHookEx(Win32.WH_MOUSE_LL,    _mHookProc,  hMod, 0);

        ConsoleUI.DrawStaticUI();

        // バックグラウンドでターゲット監視 (background target polling)
        _ = Task.Run(async () =>
        {
            while (true)
            {
                RefreshTargetState();
                await Task.Delay(500);
            }
        });

        // Win32 message pump
        while (Win32.GetMessage(out Win32.MSG msg, IntPtr.Zero, 0, 0))
        {
            Win32.TranslateMessage(in msg);
            Win32.DispatchMessage(in msg);
        }

        if (_kbHook != IntPtr.Zero) Win32.UnhookWindowsHookEx(_kbHook);
        if (_mHook  != IntPtr.Zero) Win32.UnhookWindowsHookEx(_mHook);
    }

    // ─────────────────────────────────────────
    //  Target process detection
    // ─────────────────────────────────────────

    /// <summary>
    /// 포그라운드 창이 MabinogiMobile.exe이고 마우스가 그 위에 있는지 확인한다.
    /// 결과가 바뀌었을 때만 상태를 갱신하여 불필요한 UI 재그리기를 막는다.
    /// </summary>
    private static bool RefreshTargetState()
    {
        bool active = ComputeTargetActive();

        lock (_lock)
        {
            if (active == _targetActive) return active;
            _targetActive = active;
            if (!active) ReleaseAllKeys_Locked();
        }

        ConsoleUI.UpdateStatus(State);
        return active;
    }

    private static bool ComputeTargetActive()
    {
        IntPtr fgWnd = Win32.GetForegroundWindow();
        if (fgWnd == IntPtr.Zero) return false;

        // ── 프로세스 이름 캐시 ──
        string procName;
        uint   pid;
        if (fgWnd == _cachedHWnd && DateTime.Now < _cacheExpiry)
        {
            procName = _cachedProcName;
            pid      = _cachedPid;
        }
        else
        {
            Win32.GetWindowThreadProcessId(fgWnd, out pid);
            try   { using var p = Process.GetProcessById((int)pid); procName = p.ProcessName; }
            catch { procName = string.Empty; }

            _cachedHWnd     = fgWnd;
            _cachedPid      = pid;
            _cachedProcName = procName;
            _cacheExpiry    = DateTime.Now.AddSeconds(CacheTtlSec);
        }

        if (!procName.Equals("MabinogiMobile", StringComparison.OrdinalIgnoreCase))
            return false;

        // ── 마우스가 해당 창 위에 있는지 확인 ──
        if (!Win32.GetCursorPos(out Win32.POINT pt)) return false;
        IntPtr wndAtPt = Win32.WindowFromPoint(pt);
        if (wndAtPt == IntPtr.Zero) return false;
        Win32.GetWindowThreadProcessId(wndAtPt, out uint pidAtCursor);
        return pidAtCursor == pid;
    }

    // ─────────────────────────────────────────
    //  Key / Input helpers
    // ─────────────────────────────────────────

    /// <summary>Lock 내부에서만 호출할 것.</summary>
    private static void ReleaseAllKeys_Locked()
    {
        _triggerActive = false;
        foreach (int vk in _sentMappedKeys) SendKey((short)vk, keyUp: true);
        _sentMappedKeys.Clear();
    }

    private static void SendKey(short vk, bool keyUp)
    {
        var inputs = new Win32.INPUT[]
        {
            new()
            {
                type = Win32.INPUT_KEYBOARD,
                u    = new Win32.InputUnion
                {
                    ki = new Win32.KEYBDINPUT
                    {
                        wVk      = vk,
                        dwFlags  = keyUp ? Win32.KEYEVENTF_KEYUP : 0,
                    }
                }
            }
        };
        Win32.SendInput(1, inputs, Marshal.SizeOf<Win32.INPUT>());
    }

    // ─────────────────────────────────────────
    //  Keyboard hook
    // ─────────────────────────────────────────
    private static IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode < 0) return Win32.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);

        var kbd = Marshal.PtrToStructure<Win32.KBDLLHOOKSTRUCT>(lParam);

        // 자체 주입 이벤트는 무시 (무한 루프 방지)
        if ((kbd.flags & Win32.LLKHF_INJECTED) != 0)
            return Win32.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);

        int msg       = (int)wParam;
        int vk        = kbd.vkCode;
        bool isDown   = msg is Win32.WM_KEYDOWN or Win32.WM_SYSKEYDOWN;
        bool isUp     = msg is Win32.WM_KEYUP   or Win32.WM_SYSKEYUP;

        // 매핑 대상 키의 물리적 누름 상태 추적
        if (KeyMap.ContainsKey(vk))
        {
            lock (_lock)
            {
                if (isDown) _physicalKeysDown.Add(vk);
                else if (isUp) _physicalKeysDown.Remove(vk);
            }
        }

        // TAB: 전투 모드 토글 (대상 창이 활성일 때만)
        if (vk == Win32.VK_TAB && isDown && ComputeTargetActive())
        {
            lock (_lock)
            {
                _combatMode = !_combatMode;
                ReleaseAllKeys_Locked();
            }
            ConsoleUI.UpdateStatus(State);
            return new IntPtr(1); // 원본 TAB 이벤트 소비
        }

        if (!ComputeTargetActive())
            return Win32.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);

        // CTRL 추적
        if (vk is Win32.VK_CONTROL or Win32.VK_LCONTROL or Win32.VK_RCONTROL)
        {
            bool changed;
            lock (_lock)
            {
                bool prev = _ctrlPressed;
                _ctrlPressed = isDown || (isUp ? false : _ctrlPressed);
                changed = prev != _ctrlPressed;
                if (changed) ReleaseAllKeys_Locked();
            }
            if (changed) ConsoleUI.UpdateStatus(State);
            return Win32.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
        }

        // 전투 모드에서 매핑 키 처리
        lock (_lock)
        {
            if (_combatMode && !_ctrlPressed && _triggerActive && KeyMap.TryGetValue(vk, out int mapped))
            {
                if (isDown) { _sentMappedKeys.Add(mapped);    SendKey((short)mapped, keyUp: false); }
                else if (isUp) { _sentMappedKeys.Remove(mapped); SendKey((short)mapped, keyUp: true);  }
                return new IntPtr(1); // 원본 키 이벤트 소비
            }
        }

        return Win32.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }

    // ─────────────────────────────────────────
    //  Mouse hook
    // ─────────────────────────────────────────
    private static IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode < 0 || !ComputeTargetActive())
            return Win32.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);

        int  msg             = (int)wParam;
        bool effectiveCombat;
        lock (_lock) effectiveCombat = _combatMode && !_ctrlPressed;

        if (msg == Win32.WM_LBUTTONDOWN && effectiveCombat)
        {
            lock (_lock)
            {
                _triggerActive = true;

                // 마우스 DOWN 시점에 이미 눌려 있는 물리 키 처리:
                //   ① 게임에 전달되고 있던 물리 keyDown을 먼저 올려서(keyUp) 이동을 멈추고
                //   ② 대응하는 매핑 키를 keyDown으로 주입해 스킬로 전환한다.
                //
                // 주입한 물리 keyUp은 LLKHF_INJECTED라 훅을 통과하므로
                // _physicalKeysDown을 직접 비워서 상태를 일치시킨다.
                // (물리 keyUp이 나중에 다시 오면 훅이 Remove를 시도하지만 이미 없으므로 무해하다.)
                var keysToTransition = _physicalKeysDown.Where(k => KeyMap.ContainsKey(k)).ToList();
                foreach (int vk in keysToTransition)
                {
                    int mapped = KeyMap[vk];
                    SendKey((short)vk,     keyUp: true);   // ① 이동 키 해제 주입
                    SendKey((short)mapped, keyUp: false);  // ② 스킬 키 주입
                    _sentMappedKeys.Add(mapped);
                }
                // 주입된 keyUp과 상태를 동기화
                _physicalKeysDown.ExceptWith(keysToTransition);
            }
            ConsoleUI.UpdateStatus(State);
            return new IntPtr(1); // 클릭 이벤트 소비
        }

        if (msg == Win32.WM_LBUTTONUP)
        {
            bool wasTrigger;
            lock (_lock)
            {
                wasTrigger     = _triggerActive;
                _triggerActive = false;

                if (wasTrigger)
                {
                    // 매핑 키(스킬)를 모두 올린다.
                    // 물리 키 재주입은 하지 않는다 — 물리 키가 여전히 눌려 있다면
                    // 훅이 다음 이벤트에서 그대로 게임에 전달하므로 복원 불필요.
                    // 재주입하면 주입된 keyDown에 대응하는 keyUp 주체가 없어 키가 끼는 버그가 발생한다.
                    foreach (int vk in _sentMappedKeys) SendKey((short)vk, keyUp: true);
                    _sentMappedKeys.Clear();
                }
            }

            if (wasTrigger || effectiveCombat)
            {
                ConsoleUI.UpdateStatus(State);
                if (wasTrigger) return new IntPtr(1); // 버튼 업 이벤트도 소비
            }
        }

        return Win32.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }
}