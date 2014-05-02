// Copyright (c) Microsoft Corporation.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace ConsoleGUI
{
    internal sealed class Win32Console : IDisposable
    {
        private bool _disposed = false;

        private int _handleIn;
        private int _handleOut;
        private AutoResetEvent consoleEvent = new AutoResetEvent(false);
        private AutoResetEvent clientEvent = new AutoResetEvent(false);
        private ConcurrentQueue<ConsoleEvent> clientEventQueue = new ConcurrentQueue<ConsoleEvent>();

        private bool _needRestoreConsoleMode = false;
        private ConsoleMode _originalConsoleMode;

        internal Win32Console()
        {
            // According to http://msdn.microsoft.com/en-us/library/windows/desktop/ms683231(v=vs.85).aspx
            // it sounds like GetStdHandle isn't going to do the right thing if the user redirects stdin
            // or stdout, e.g. by starting the app on the command line with "edit.exe > foo.txt < bar.txt".
            // Instead it sounds like we should use CreateFile("CONIN$") and CreateFile("CONOUT$")...
            _handleIn = GetStdHandle(STD_INPUT_HANDLE);
            _handleOut = GetStdHandle(STD_OUTPUT_HANDLE);

            consoleEvent.SafeWaitHandle = new SafeWaitHandle(new IntPtr(_handleIn), false);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                if (_needRestoreConsoleMode)
                {
                    SetConsoleMode(_handleIn, _originalConsoleMode);
                    _needRestoreConsoleMode = false;
                }

                _disposed = true;
            }
        }

        internal void PostEvent(ConsoleEvent @event)
        {
            clientEventQueue.Enqueue(@event);
            clientEvent.Set();
        }

        internal void EnableInputEvents()
        {
            if (!GetConsoleMode(_handleIn, out _originalConsoleMode))
            {
                throw new Exception("GetConsoleMode failed");
            }

            if (!SetConsoleMode(_handleIn, ConsoleMode.ENABLE_MOUSE_INPUT | ConsoleMode.ENABLE_WINDOW_INPUT))
            {
                throw new Exception("SetConsoleMode failed");
            }

            _needRestoreConsoleMode = true;
        }

        internal Dimensions GetBufferDimensions()
        {
            CONSOLE_SCREEN_BUFFER_INFO info;

            if (!GetConsoleScreenBufferInfo(_handleOut, out info))
            {
                throw new Exception("GetConsoleScreenBufferInfo failed");
            }

            return new Dimensions(info.dwSize.x, info.dwSize.y);
        }

        internal void WriteOutputBuffer(char[] bufferChars, ConsoleColor[] bufferFg, ConsoleColor[] bufferBg, int width, int height, List<RowSpan> invalidSpans)
        {
            // This copying of the backbuffer data into a local CHAR_INFO array is a lot of
            // copying that might be a perf issue.  If so consider storing a CHAR_INFO array
            // directly in the BackBuffer class.
            var charinfos = new CHAR_INFO[bufferChars.Length];
            for (int i = 0; i < bufferChars.Length; i += 1)
            {
                charinfos[i].UnicodeChar = bufferChars[i];
                charinfos[i].Attributes = CharInfoAttributeFromConsoleColors(bufferFg[i], bufferBg[i]);
            }

            COORD bufferSize;
            bufferSize.x = (short)width;
            bufferSize.y = (short)height;

            COORD bufferCoord;
            SMALL_RECT writeRegion;

            if (invalidSpans == null)
            {
                writeRegion.Top = 0;
                writeRegion.Left = 0;
                writeRegion.Bottom = (short)(height - 1);
                writeRegion.Right = (short)(width - 1);

                bufferCoord.x = 0;
                bufferCoord.y = 0;

                WriteConsoleOutput(_handleOut, charinfos, bufferSize, bufferCoord, ref writeRegion);
            }
            else
            {
                foreach (var span in invalidSpans)
                {
                    writeRegion.Top = (short)span.Row;
                    writeRegion.Bottom = (short)span.Row;
                    writeRegion.Left = (short)span.Start;
                    writeRegion.Right = (short)(span.End - 1);

                    bufferCoord.y = writeRegion.Top;
                    bufferCoord.x = writeRegion.Left;

                    WriteConsoleOutput(_handleOut, charinfos, bufferSize, bufferCoord, ref writeRegion);
                }
            }
        }

        private ushort CharInfoAttributeFromConsoleColors(ConsoleColor fg, ConsoleColor bg)
        {
            return (ushort)((ushort)fg | ((ushort)bg << 4));
        }

        internal ConsoleEvent GetNextEvent()
        {
            // TODO: How to handle timing events?
            ConsoleEvent ev = null;
            while (ev == null)
            {
                int whichEvent = WaitHandle.WaitAny(new[] { consoleEvent, clientEvent });

                switch (whichEvent)
                {
                    case 0: ev = GetConsoleEvent(); break;
                    case 1: ev = GetClientEvent(); break;
                }

                if (!clientEventQueue.IsEmpty)
                {
                    clientEvent.Set();
                }
            }

            return ev;
        }

        private ConsoleEvent GetClientEvent()
        {
            ConsoleEvent ev;
            if (clientEventQueue.TryDequeue(out ev))
            {
                return ev;
            }
            return null;
        }

        private ConsoleEvent GetConsoleEvent()
        {
            // CONSIDER: Any reason to buffer more than one record in our Win32Console class?
            var records = new INPUT_RECORD[1];
            uint count;

            if (!ReadConsoleInput(_handleIn, records, (uint)records.Length, out count))
            {
                throw new Exception("ReadConsoleInput failed");
            }

            var record = records[0];

            switch (record.EventType)
            {
                case EventType.KEY_EVENT:
                    {
                        var keyEvent = record.KeyEvent;
                        return new KeyEvent(keyEvent.bKeyDown, keyEvent.UnicodeChar, keyEvent.wRepeatCount, keyEvent.wVirtualKeyCode, keyEvent.dwControlKeyState);
                    }
                case EventType.MOUSE_EVENT:
                    {
                        var mouseEvent = record.MouseEvent;
                        return new MouseEvent(mouseEvent.dwMousePosition.x, mouseEvent.dwMousePosition.y, mouseEvent.dwButtonState, mouseEvent.dwControlKeyState, mouseEvent.dwEventFlags);
                    }
                case EventType.WINDOW_BUFFER_SIZE_EVENT:
                    {
                        COORD size = record.WindowBufferSizeEvent.dwSize;
                        return new BufferSizeEvent(size.x, size.y);
                    }
                case EventType.FOCUS_EVENT:
                case EventType.MENU_EVENT:
                    {
                        // Ignore.
                        return null;
                    }
                default:
                    throw new Exception("Invalid EventType enum value");
            }
        }

        private const int STD_INPUT_HANDLE = -10;
        private const int STD_OUTPUT_HANDLE = -11;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint GetConsoleCP();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetConsoleMode(int hConsoleHandle, out ConsoleMode lpMode);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetConsoleScreenBufferInfo(int hConsoleHandle, out CONSOLE_SCREEN_BUFFER_INFO lpConsoleScreenBufferInfo);

        [DllImport("kernel32.dll", EntryPoint = "GetStdHandle", SetLastError = true, CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
        private static extern int GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll", SetLastError = true, EntryPoint = "ReadConsoleInputW", CallingConvention = CallingConvention.StdCall)]
        private static extern bool ReadConsoleInput(int hConsoleHandle, [Out] INPUT_RECORD[] lpBuffer, uint nLength, out uint lpNumberOfEventsRead);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetConsoleCP(uint wCodePageID);

        [DllImport("kernel32.dll", EntryPoint = "SetConsoleCursorPosition", SetLastError = true, CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
        private static extern int SetConsoleCursorPosition(int hConsoleOutput, COORD dwCursorPosition);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetConsoleMode(int hConsoleHandle, ConsoleMode dwMode);

        [DllImport("kernel32.dll", SetLastError = true, EntryPoint = "WriteConsoleOutputW", CharSet = CharSet.Unicode)]
        private static extern bool WriteConsoleOutput(int hConsoleHandle, CHAR_INFO[] lpBuffer, COORD dwBufferSize, COORD dwBufferCoord, ref SMALL_RECT lpWriteRegion);

        [Flags]
        enum ConsoleMode : uint
        {
            ENABLE_PROCESSED_INPUT = 0x01,
            ENABLE_LINE_INPUT = 0x02,
            ENABLE_ECHO_INPUT = 0x04,
            ENABLE_WINDOW_INPUT = 0x08,
            ENABLE_MOUSE_INPUT = 0x10,
            ENABLE_INSERT_MODE = 0x20,
            ENABLE_QUICK_EDIT_MODE = 0x40,
            ENABLE_EXTENDED_FLAGS = 0x80,
            ENABLE_AUTO_POSITION = 0x100,

            ENABLE_PROCESSED_OUTPUT = 0x01,
            ENABLE_WRAP_AT_EOL_OUTPUT = 0x02
        }

        [StructLayout(LayoutKind.Explicit, CharSet = CharSet.Unicode)]
        internal struct CHAR_INFO
        {
            [FieldOffset(0)]
            internal char UnicodeChar;
            [FieldOffset(0)]
            internal byte AsciiChar;
            [FieldOffset(2)]
            internal ushort Attributes;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct COORD
        {
            public short x;
            public short y;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct SMALL_RECT
        {
            public short Left;
            public short Top;
            public short Right;
            public short Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct CONSOLE_SCREEN_BUFFER_INFO
        {
            public COORD dwSize;
            public COORD dwCursorPosition;
            public int wAttributes;
            public SMALL_RECT srWindow;
            public COORD dwMaximumWindowSize;
        }

        [StructLayout(LayoutKind.Explicit)]
        internal struct INPUT_RECORD
        {
            [FieldOffset(0)]
            public EventType EventType;
            [FieldOffset(4)]
            public KEY_EVENT_RECORD KeyEvent;
            [FieldOffset(4)]
            public MOUSE_EVENT_RECORD MouseEvent;
            [FieldOffset(4)]
            public WINDOW_BUFFER_SIZE_RECORD WindowBufferSizeEvent;
            [FieldOffset(4)]
            public MENU_EVENT_RECORD MenuEvent;
            [FieldOffset(4)]
            public FOCUS_EVENT_RECORD FocusEvent;
        }

        public enum EventType : ushort
        {
            KEY_EVENT = 0x01,
            MOUSE_EVENT = 0x02,
            WINDOW_BUFFER_SIZE_EVENT = 0x04,
            MENU_EVENT = 0x08,
            FOCUS_EVENT = 0x10,
        }

        [StructLayout(LayoutKind.Explicit, CharSet = CharSet.Unicode)]
        internal struct KEY_EVENT_RECORD
        {
            [FieldOffset(0), MarshalAs(UnmanagedType.Bool)]
            public bool bKeyDown;
            [FieldOffset(4), MarshalAs(UnmanagedType.U2)]
            public ushort wRepeatCount;
            [FieldOffset(6), MarshalAs(UnmanagedType.U2)]
            public VirtualKey wVirtualKeyCode;
            [FieldOffset(8), MarshalAs(UnmanagedType.U2)]
            public ushort wVirtualScanCode;
            [FieldOffset(10)]
            public char UnicodeChar;
            [FieldOffset(12), MarshalAs(UnmanagedType.U4)]
            public ControlKeyState dwControlKeyState;
        }

        [StructLayout(LayoutKind.Explicit)]
        internal struct MOUSE_EVENT_RECORD
        {
            [FieldOffset(0)]
            public COORD dwMousePosition;
            [FieldOffset(4), MarshalAs(UnmanagedType.U4)]
            public ButtonState dwButtonState;
            [FieldOffset(8), MarshalAs(UnmanagedType.U4)]
            public ControlKeyState dwControlKeyState;
            [FieldOffset(12), MarshalAs(UnmanagedType.U4)]
            public MouseEventFlags dwEventFlags;
        }

        [StructLayout(LayoutKind.Explicit)]
        internal struct WINDOW_BUFFER_SIZE_RECORD
        {
            [FieldOffset(0)]
            public COORD dwSize;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct MENU_EVENT_RECORD
        {
            public uint dwCommandId;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct FOCUS_EVENT_RECORD
        {
            public uint bSetFocus;
        }

        
    }

    public enum VirtualKey : ushort
    {
        VK_LBUTTON = 0x01,
        VK_RBUTTON = 0x02,
        VK_CANCEL = 0x03,
        VK_MBUTTON = 0x04,
        VK_XBUTTON1 = 0x05,
        VK_XBUTTON2 = 0x06,
        VK_BACK = 0x08,
        VK_TAB = 0x09,
        VK_CLEAR = 0x0C,
        VK_RETURN = 0x0D,
        VK_SHIFT = 0x10,
        VK_CONTROL = 0x11,
        VK_MENU = 0x12,
        VK_PAUSE = 0x13,
        VK_CAPITAL = 0x14,
        VK_KANA = 0x15,
        VK_HANGEUL = 0x15,
        VK_HANGUL = 0x15,
        VK_JUNJA = 0x17,
        VK_FINAL = 0x18,
        VK_HANJA = 0x19,
        VK_KANJI = 0x19,
        VK_ESCAPE = 0x1B,
        VK_CONVERT = 0x1C,
        VK_NONCONVERT = 0x1D,
        VK_ACCEPT = 0x1E,
        VK_MODECHANGE = 0x1F,
        VK_SPACE = 0x20,
        VK_PRIOR = 0x21,
        VK_NEXT = 0x22,
        VK_END = 0x23,
        VK_HOME = 0x24,
        VK_LEFT = 0x25,
        VK_UP = 0x26,
        VK_RIGHT = 0x27,
        VK_DOWN = 0x28,
        VK_SELECT = 0x29,
        VK_PRINT = 0x2A,
        VK_EXECUTE = 0x2B,
        VK_SNAPSHOT = 0x2C,
        VK_INSERT = 0x2D,
        VK_DELETE = 0x2E,
        VK_HELP = 0x2F,
        VK_0 = 0x30,
        VK_1 = 0x31,
        VK_2 = 0x32,
        VK_3 = 0x33,
        VK_4 = 0x34,
        VK_5 = 0x35,
        VK_6 = 0x36,
        VK_7 = 0x37,
        VK_8 = 0x38,
        VK_9 = 0x39,
        VK_A = 0x41,
        VK_B = 0x42,
        VK_C = 0x43,
        VK_D = 0x44,
        VK_E = 0x45,
        VK_F = 0x46,
        VK_G = 0x47,
        VK_H = 0x48,
        VK_I = 0x49,
        VK_J = 0x4A,
        VK_K = 0x4B,
        VK_L = 0x4C,
        VK_M = 0x4D,
        VK_N = 0x4E,
        VK_O = 0x4F,
        VK_P = 0x50,
        VK_Q = 0x51,
        VK_R = 0x52,
        VK_S = 0x53,
        VK_T = 0x54,
        VK_U = 0x55,
        VK_V = 0x56,
        VK_W = 0x57,
        VK_X = 0x58,
        VK_Y = 0x59,
        VK_Z = 0x5A,
        VK_LWIN = 0x5B,
        VK_RWIN = 0x5C,
        VK_APPS = 0x5D,
        VK_SLEEP = 0x5F,
        VK_NUMPAD0 = 0x60,
        VK_NUMPAD1 = 0x61,
        VK_NUMPAD2 = 0x62,
        VK_NUMPAD3 = 0x63,
        VK_NUMPAD4 = 0x64,
        VK_NUMPAD5 = 0x65,
        VK_NUMPAD6 = 0x66,
        VK_NUMPAD7 = 0x67,
        VK_NUMPAD8 = 0x68,
        VK_NUMPAD9 = 0x69,
        VK_MULTIPLY = 0x6A,
        VK_ADD = 0x6B,
        VK_SEPARATOR = 0x6C,
        VK_SUBTRACT = 0x6D,
        VK_DECIMAL = 0x6E,
        VK_DIVIDE = 0x6F,
        VK_F1 = 0x70,
        VK_F2 = 0x71,
        VK_F3 = 0x72,
        VK_F4 = 0x73,
        VK_F5 = 0x74,
        VK_F6 = 0x75,
        VK_F7 = 0x76,
        VK_F8 = 0x77,
        VK_F9 = 0x78,
        VK_F10 = 0x79,
        VK_F11 = 0x7A,
        VK_F12 = 0x7B,
        VK_F13 = 0x7C,
        VK_F14 = 0x7D,
        VK_F15 = 0x7E,
        VK_F16 = 0x7F,
        VK_F17 = 0x80,
        VK_F18 = 0x81,
        VK_F19 = 0x82,
        VK_F20 = 0x83,
        VK_F21 = 0x84,
        VK_F22 = 0x85,
        VK_F23 = 0x86,
        VK_F24 = 0x87,
        VK_NUMLOCK = 0x90,
        VK_SCROLL = 0x91,
        VK_OEM_NEC_EQUAL = 0x92,
        VK_OEM_FJ_JISHO = 0x92,
        VK_OEM_FJ_MASSHOU = 0x93,
        VK_OEM_FJ_TOUROKU = 0x94,
        VK_OEM_FJ_LOYA = 0x95,
        VK_OEM_FJ_ROYA = 0x96,
        VK_LSHIFT = 0xA0,
        VK_RSHIFT = 0xA1,
        VK_LCONTROL = 0xA2,
        VK_RCONTROL = 0xA3,
        VK_LMENU = 0xA4,
        VK_RMENU = 0xA5,
        VK_BROWSER_BACK = 0xA6,
        VK_BROWSER_FORWARD = 0xA7,
        VK_BROWSER_REFRESH = 0xA8,
        VK_BROWSER_STOP = 0xA9,
        VK_BROWSER_SEARCH = 0xAA,
        VK_BROWSER_FAVORITES = 0xAB,
        VK_BROWSER_HOME = 0xAC,
        VK_VOLUME_MUTE = 0xAD,
        VK_VOLUME_DOWN = 0xAE,
        VK_VOLUME_UP = 0xAF,
        VK_MEDIA_NEXT_TRACK = 0xB0,
        VK_MEDIA_PREV_TRACK = 0xB1,
        VK_MEDIA_STOP = 0xB2,
        VK_MEDIA_PLAY_PAUSE = 0xB3,
        VK_LAUNCH_MAIL = 0xB4,
        VK_LAUNCH_MEDIA_SELECT = 0xB5,
        VK_LAUNCH_APP1 = 0xB6,
        VK_LAUNCH_APP2 = 0xB7,
        VK_OEM_1 = 0xBA,
        VK_OEM_PLUS = 0xBB,
        VK_OEM_COMMA = 0xBC,
        VK_OEM_MINUS = 0xBD,
        VK_OEM_PERIOD = 0xBE,
        VK_OEM_2 = 0xBF,
        VK_OEM_3 = 0xC0,
        VK_OEM_4 = 0xDB,
        VK_OEM_5 = 0xDC,
        VK_OEM_6 = 0xDD,
        VK_OEM_7 = 0xDE,
        VK_OEM_8 = 0xDF,
        VK_OEM_AX = 0xE1,
        VK_OEM_102 = 0xE2,
        VK_ICO_HELP = 0xE3,
        VK_ICO_00 = 0xE4,
        VK_PROCESSKEY = 0xE5,
        VK_ICO_CLEAR = 0xE6,
        VK_PACKET = 0xE7,
        VK_OEM_RESET = 0xE9,
        VK_OEM_JUMP = 0xEA,
        VK_OEM_PA1 = 0xEB,
        VK_OEM_PA2 = 0xEC,
        VK_OEM_PA3 = 0xED,
        VK_OEM_WSCTRL = 0xEE,
        VK_OEM_CUSEL = 0xEF,
        VK_OEM_ATTN = 0xF0,
        VK_OEM_FINISH = 0xF1,
        VK_OEM_COPY = 0xF2,
        VK_OEM_AUTO = 0xF3,
        VK_OEM_ENLW = 0xF4,
        VK_OEM_BACKTAB = 0xF5,
        VK_ATTN = 0xF6,
        VK_CRSEL = 0xF7,
        VK_EXSEL = 0xF8,
        VK_EREOF = 0xF9,
        VK_PLAY = 0xFA,
        VK_ZOOM = 0xFB,
        VK_NONAME = 0xFC,
        VK_PA1 = 0xFD,
        VK_OEM_CLEAR = 0xFE
    }

    [Flags]
    public enum ControlKeyState : uint
    {
        // dwControlKeyState bitmask
        RIGHT_ALT_PRESSED = 0x01,
        LEFT_ALT_PRESSED = 0x02,
        RIGHT_CTRL_PRESSED = 0x04,
        LEFT_CTRL_PRESSED = 0x08,
        SHIFT_PRESSED = 0x10,
        NUMLOCK_ON = 0x20,
        SCROLLLOCK_ON = 0x40,
        CAPSLOCK_ON = 0x80,
        ENHANCED_KEY = 0x100,
    }

    [Flags]
    public enum ButtonState : uint
    {
        FROM_LEFT_1ST_BUTTON_PRESSED = 0x01,
        RIGHTMOST_BUTTON_PRESSED = 0x02,
        FROM_LEFT_2ND_BUTTON_PRESSED = 0x04,
        FROM_LEFT_3RD_BUTTON_PRESSED = 0x08,
        FROM_LEFT_4TH_BUTTON_PRESSED = 0x10
    }

    [Flags]
    public enum MouseEventFlags : uint
    {
        MOUSE_MOVED = 0x01,
        DOUBLE_CLICK = 0x02,
        MOUSE_WHEELED = 0x04,
        MOUSE_HWHEELED = 0x08
    }
}
