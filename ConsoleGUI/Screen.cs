// Copyright (c) Microsoft Corporation.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ConsoleGUI
{
    public class Screen : ContainerControl, IDisposable
    {
        public Control ActiveControl { get; set; }

        public Control PopupControl { get; set; }

        private Win32Console _console;
        private bool _disposed = false;
        private int _cursorHideCount;

        private BackBuffer _backbuffer = new BackBuffer();
        private bool _invalid = false;


        public event EventHandler BeforeRender;

        public event EventHandler<KeyEvent> BeforeKeyDown;
        public event EventHandler<KeyEvent> AfterKeyDown;
        public event EventHandler<KeyEvent> KeyUp;
        public event EventHandler<MouseEvent> MouseDown;
        public event EventHandler<BufferSizeEvent> Resize;

        public ControlTheme Theme { get; set; }


        private static Screen _screen = null;

        public static void NavigateTo(Screen screen)
        {
            _screen = screen;
            screen.DoIOLoop();
        }

        public static Screen GetScreen()
        {
            return _screen;
        }

        protected Screen()
        {
            ActiveControl = this;
            // TODO: Save existing console buffer before resizing to fit window,
            // TODO: then restore it in Dispose()
            Console.CursorSize = 8;
            Console.SetBufferSize(Console.WindowWidth, Console.WindowHeight);
            _console = new Win32Console();
            _console.EnableInputEvents();
            var dim = _console.GetBufferDimensions();
            this.Left = 0;
            this.Top = 0;
            this.Width = dim.Width;
            this.Height = dim.Height;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _console.Dispose();
                _disposed = true;
            }
        }

        protected Thread UIThread { get; set; }

        public void AssertOnUIThread()
        {
            if (this.UIThread != null && Thread.CurrentThread != this.UIThread)
                throw new Exception("Not on UI thread");
        }

        public void DoIOLoop()
        {
            UIThread = Thread.CurrentThread;

            var dim = _console.GetBufferDimensions();
            _backbuffer.ResetBufferSize(dim, ForegroundColor, BackgroundColor);

            this.Left = 0;
            this.Top = 0;
            this.Width = dim.Width;
            this.Height = dim.Height;

            _invalid = true;

            while (true)
            {
                if (BeforeRender != null) BeforeRender(this, EventArgs.Empty);
                if (_invalid)
                {
                    this.Render(this);
                    _backbuffer.CopyToConsole(_console);

                    _invalid = false;
                }

                var popupControl = this.PopupControl;
                var activeControl = this.ActiveControl;
                Control focusControl;
                if (popupControl != null)
                    focusControl = popupControl;
                else
                    focusControl = activeControl;

                activeControl.PlaceCursor(this);

                // Get event
                // Route event for handling
                var consoleEvent = _console.GetNextEvent();

                if (consoleEvent is KeyEvent)
                {
                    var keyEvent = (KeyEvent)consoleEvent;

                    if (keyEvent.KeyDown)
                    {
                        if (!keyEvent.Handled && popupControl != null) popupControl.OnBeforeKeyDown(this, keyEvent);
                        if (!keyEvent.Handled && BeforeKeyDown != null) BeforeKeyDown(this, keyEvent);
                        if (!keyEvent.Handled && activeControl != null) activeControl.OnBeforeKeyDown(this, keyEvent);
                        if (!keyEvent.Handled && popupControl != null) popupControl.OnAfterKeyDown(this, keyEvent);
                        if (!keyEvent.Handled && AfterKeyDown != null) AfterKeyDown(this, keyEvent);
                        if (!keyEvent.Handled && activeControl != null) activeControl.OnAfterKeyDown(this, keyEvent);
                    }
                    else
                    {
                        if (!keyEvent.Handled && KeyUp != null) KeyUp(this, keyEvent);
                        if (!keyEvent.Handled && popupControl != null) popupControl.OnKeyUp(this, keyEvent);
                        if (!keyEvent.Handled && activeControl != null) activeControl.OnKeyUp(this, keyEvent);
                    }
                }
                else if (consoleEvent is MouseEvent)
                {
                    var mouseEvent = (MouseEvent)consoleEvent;

                    if (mouseEvent.MouseEventFlags == 0 && mouseEvent.ButtonState != 0) // pressed
                    {
                        if (MouseDown != null) MouseDown(this, mouseEvent);
                        var hit = this.HitTest(mouseEvent.X, mouseEvent.Y);
                        if (hit != null) hit.OnMouseDown(this, mouseEvent);
                    }
                }
                else if (consoleEvent is BufferSizeEvent)
                {
                    var bufferSizeEvent = consoleEvent as BufferSizeEvent;
                    _backbuffer.ResetBufferSize(bufferSizeEvent.Dimensions, ForegroundColor, BackgroundColor);

                    this.Width = bufferSizeEvent.Dimensions.Width;
                    this.Height = bufferSizeEvent.Dimensions.Height;

                    // The buffer size event is for the actual size of the output
                    // buffer and not the visible window into the buffer. So
                    // we shouldn't resize to the entire buffer size.  Ideally
                    // we'd resize with the visible window portion of the buffer
                    // with the top-left corner anchored to the left most column
                    // and the row where the cursor was when the process started.
                    // Complicated.

                    if (Resize != null) Resize(this, bufferSizeEvent);
                }
                else if (consoleEvent is DispatchEvent)
                {
                    var dispatchEvent = consoleEvent as DispatchEvent;

                    dispatchEvent.Dispatch();
                }
            }
        }

        private class DispatchEvent : ConsoleEvent
        {
            Action _action;

            public DispatchEvent(Action action)
            {
                _action = action;
            }

            public void Dispatch()
            {
                _action();
            }
        }

        public void Post(Action a)
        {
            var dispatchEvent = new DispatchEvent(a);
            _console.PostEvent(dispatchEvent);
        }

        private class InvalidateEvent : ConsoleEvent { }
        public void Invalidate()
        {
            if (!_invalid)
            {
                lock (this)
                {
                    if (!_invalid)
                    {
                        _invalid = true;
                        _console.PostEvent(new InvalidateEvent());
                    }
                }
            }
        }

        public void RestoreScreen()
        {
            _backbuffer.RestoreBufferToScreen(_console);
        }

        public void DrawString(int column, int row, string str, ConsoleColor fg, ConsoleColor bg)
        {
            // Clipping is expected to occur at a higher level
            if (column + str.Length > _backbuffer.Width ||
                column < 0 ||
                row >= _backbuffer.Height ||
                row < 0)
            {
                throw new ArgumentOutOfRangeException();
            }

            for (int i = 0; i < str.Length; i += 1)
            {
                _backbuffer.SetChar(column + i, row, str[i], fg, bg);
            }
        }

        public void RecolorLine(int column, int row, int count, ConsoleColor fg, ConsoleColor bg)
        {
            // Clipping is expected to occur at a higher level
            if (column + count >= _backbuffer.Width ||
                column < 0 ||
                row >= _backbuffer.Height ||
                row < 0)
            {
                throw new ArgumentOutOfRangeException();
            }

            for (int i = 0; i < count; i += 1)
            {
                _backbuffer.SetColor(column + i, row, fg, bg);
            }
        }

        internal void DrawTopLeftCorner(int column, int row, ConsoleColor fg, ConsoleColor bg)
        {
            if (Theme == ControlTheme.Basic)
                _backbuffer.SetChar(column, row, '┌', fg, bg);
            else
                _backbuffer.SetChar(column, row, '╔', fg, bg);
        }

        internal void DrawTopRightCorner(int column, int row, ConsoleColor fg, ConsoleColor bg)
        {
            if (Theme == ControlTheme.Basic)
                _backbuffer.SetChar(column, row, '┐', fg, bg);
            else
                _backbuffer.SetChar(column, row, '╗', fg, bg);
        }

        internal void DrawBottomLeftCorner(int column, int row, ConsoleColor fg, ConsoleColor bg)
        {
            if (Theme == ControlTheme.Basic)
                _backbuffer.SetChar(column, row, '└', fg, bg);
            else
                _backbuffer.SetChar(column, row, '╚', fg, bg);
        }

        internal void DrawBottomRightCorner(int column, int row, ConsoleColor fg, ConsoleColor bg)
        {
            if (Theme == ControlTheme.Basic)
                _backbuffer.SetChar(column, row, '┘', fg, bg);
            else
                _backbuffer.SetChar(column, row, '╝', fg, bg);
        }

        internal void DrawLeftTee(int column, int row, ConsoleColor fg, ConsoleColor bg)
        {
            if (Theme == ControlTheme.Basic)
                _backbuffer.SetChar(column, row, '├', fg, bg);
            else
                _backbuffer.SetChar(column, row, '╠', fg, bg);
        }

        internal void DrawRightTee(int column, int row, ConsoleColor fg, ConsoleColor bg)
        {
            if (Theme == ControlTheme.Basic)
                _backbuffer.SetChar(column, row, '┤', fg, bg);
            else
                _backbuffer.SetChar(column, row, '╣', fg, bg);
        }

        internal void DrawHorizontalLine(int left, int right, int row, ConsoleColor fg, ConsoleColor bg)
        {
            if (Theme == ControlTheme.Basic)
                DrawHorizontalLine(left, right, row, '─', fg, bg);
            else
                DrawHorizontalLine(left, right, row, '═', fg, bg);
        }

        internal void DrawHorizontalLine(int left, int right, int row, char ch, ConsoleColor fg, ConsoleColor bg)
        {
            for (int column = left; column <= right; column += 1)
                _backbuffer.SetChar(column, row, ch, fg, bg);
        }

        internal void DrawVerticalLine(int column, int top, int bottom, char ch, ConsoleColor fg, ConsoleColor bg)
        {
            for (int row = top; row <= bottom; row += 1)
                _backbuffer.SetChar(column, row, ch, fg, bg);
        }

        internal void DrawVerticalLine(int column, int top, int bottom, ConsoleColor fg, ConsoleColor bg)
        {
            if (Theme == ControlTheme.Basic)
                DrawVerticalLine(column, top, bottom, '│', fg, bg);
            else
                DrawVerticalLine(column, top, bottom, '║', fg, bg);
        }

        internal void DrawBoxFill(int left, int top, int right, int bottom, ConsoleColor fg, ConsoleColor bg)
        {
            DrawBox(left, top, right, bottom, fg, bg);

            if (right - left > 1)
            {
                for (int row = top + 1; row < bottom; row += 1)
                {
                    DrawHorizontalLine(left + 1, right - 1, row, ' ', fg, bg);
                }
            }
        }

        internal void DrawShadow(int left, int top, int right, int bottom)
        {
            for (int row = top; row <= bottom; row += 1)
                for (int column = left; column <= right; column += 1)
                    _backbuffer.ShadowChar(column, row);
        }

        internal void DrawBox(int left, int top, int right, int bottom, ConsoleColor fg, ConsoleColor bg)
        {
            if (right < left)
            {
                int temp = left;
                left = right;
                right = temp;
            }

            if (bottom < top)
            {
                int temp = top;
                top = bottom;
                bottom = top;
            }

            if (left < 0 || right >= Width || top < 0 || bottom >= Height)
                return;

            if (left == right && top == bottom)
            {
                _backbuffer.SetChar(left, top, ' ', fg, bg);
            }
            else if (left == right)
            {
                DrawVerticalLine(left, top, bottom, fg, bg);
            }
            else if (top == bottom)
            {
                DrawHorizontalLine(left, right, top, fg, bg);
            }
            else
            {
                DrawTopLeftCorner(left, top, fg, bg);
                DrawTopRightCorner(right, top, fg, bg);
                DrawBottomLeftCorner(left, bottom, fg, bg);
                DrawBottomRightCorner(right, bottom, fg, bg);

                if (left + 1 <= right - 1)
                {
                    DrawHorizontalLine(left + 1, right - 1, top, fg, bg);
                    DrawHorizontalLine(left + 1, right - 1, bottom, fg, bg);
                }

                if (top + 1 <= bottom - 1)
                {
                    DrawVerticalLine(left, top + 1, bottom - 1, fg, bg);
                    DrawVerticalLine(right, top + 1, bottom - 1, fg, bg);
                }
            }
        }

        internal void DrawLeftArrow(int column, int row, ConsoleColor fg, ConsoleColor bg)
        {
            if (Theme == ControlTheme.Basic)
                _backbuffer.SetChar(column, row, '←', fg, bg);
            else
                _backbuffer.SetChar(column, row, (char)0x11, fg, bg);
        }

        internal void DrawRightArrow(int column, int row, ConsoleColor fg, ConsoleColor bg)
        {
            if (Theme == ControlTheme.Basic)
                _backbuffer.SetChar(column, row, '→', fg, bg);
            else
                _backbuffer.SetChar(column, row, (char)0x10, fg, bg);
        }

        internal void DrawUpArrow(int column, int row, ConsoleColor fg, ConsoleColor bg)
        {
            if (Theme == ControlTheme.Basic)
                _backbuffer.SetChar(column, row, '↑', fg, bg);
            else
                _backbuffer.SetChar(column, row, (char)0x1e, fg, bg);
        }

        internal void DrawDownArrow(int column, int row, ConsoleColor fg, ConsoleColor bg)
        {
            if (Theme == ControlTheme.Basic)
                _backbuffer.SetChar(column, row, '↓', fg, bg);
            else
                _backbuffer.SetChar(column, row, (char)0x1f, fg, bg);
        }

        internal void DrawLightShade(int column, int row, ConsoleColor fg, ConsoleColor bg)
        {
            _backbuffer.SetChar(column, row, '░', fg, bg);
        }

        internal void DrawBlock(int column, int row, ConsoleColor fg, ConsoleColor bg)
        {
            _backbuffer.SetChar(column, row, '█', fg, bg);
        }

        internal void DrawMiniBlock(int column, int row, ConsoleColor fg, ConsoleColor bg)
        {
            _backbuffer.SetChar(column, row, '■', fg, bg);
        }

        internal void SetCursorPosition(int x, int y)
        {
            Console.SetCursorPosition(x, y);
        }

        internal void HideCursor()
        {
            if (_cursorHideCount == 0)
                Console.CursorVisible = false;

            _cursorHideCount += 1;
        }

        internal void ShowCursor()
        {
            if (_cursorHideCount > 0)
                _cursorHideCount -= 1;

            if (_cursorHideCount == 0)
                Console.CursorVisible = true;
        }
    }

    public enum ControlTheme
    {
        Basic,
        CSharp
    }
}
