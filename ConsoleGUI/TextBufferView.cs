// Copyright (c) Microsoft Corporation.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleGUI
{
    public class RowChangedEventArgs : EventArgs
    {
        public int OldRow { get; private set; }
        public int NewRow { get; private set; }

        public RowChangedEventArgs(int oldRow, int newRow)
        {
            OldRow = oldRow;
            NewRow = newRow;
        }
    }

    public class SelectionChangedEventArgs : EventArgs
    {
        public ViewSpan OldSelection { get; private set; }
        public ViewSpan NewSelection { get; private set; }

        public SelectionChangedEventArgs(ViewSpan oldSelection, ViewSpan newSelection)
        {
            OldSelection = oldSelection;
            NewSelection = newSelection;
        }
    }


    public class TextBufferView : Control
    {
        private TextBuffer _buffer;

        public TextBuffer Buffer {
            get
            {
                return _buffer;
            }
            set
            {
                if (_buffer != null)
                {
                    _buffer.BufferChanged -= _buffer_BufferChanged;
                }
                _buffer = value;
                _buffer.BufferChanged += _buffer_BufferChanged;
            }
        }

        void _buffer_BufferChanged(object sender, EventArgs e)
        {
            var screen = Screen.GetScreen();
            if (screen != null) screen.Invalidate();
        }

        public int WindowLeft { get; set; }
        public int WindowTop { get; set; }

        public int WindowRight { get { return WindowLeft + Width - 1; } }
        public int WindowBottom { get { return WindowTop + Height - 1; } }

        public int CursorRow
        {
            get { return Selection.StartRow; }
            private set { Selection = new ViewSpan(value, CursorColumn); }
        }
        public int CursorColumn
        {
            get { return Selection.StartColumn; }
            private set { Selection = new ViewSpan(CursorRow, value); }
        }

        private ViewSpan _selection;
        public ViewSpan Selection
        {
            get { return _selection; }
            private set
            {
                _selection = value;
                NotifyCursorMoved(Screen.GetScreen());
            }
        }

        public void MoveCursor(int row, int col)
        {
            CursorRow = row;
            CursorColumn = col;

            NotifyCursorMoved(Screen.GetScreen());
        }

        public event EventHandler<RowChangedEventArgs> RowChanged;
        public event EventHandler<SelectionChangedEventArgs> SelectionChanged;

        private bool _scrollBars = true;
        public bool ScrollBars {
            get { return _scrollBars; }
            set { _scrollBars = value; }
        }
        private bool _scrollBarsVisible = true;
        public bool ScrollBarsVisible {
            get { return _scrollBarsVisible; }
            set { _scrollBarsVisible = value; }
        }

        public override void Render(Screen screen)
        {
            for (int cr = ScreenTop, wr = WindowTop; cr <= ScreenBottom; cr++, wr++)
            {
                var line = Buffer.GetLineOrDefault(wr);
                var windowLineWidth = Math.Min(line.Text.Length - WindowLeft, Width);
                var screenLine = line.Text.SubstringPadRight(WindowLeft, windowLineWidth, Width);
                screen.DrawString(ScreenLeft, cr, screenLine, ForegroundColor, BackgroundColor);

                foreach (var span in line.ColorSpans)
                {
                    if (span.IntersectsWith(WindowLeft, WindowRight))
                    {
                        var left = Left - WindowLeft + span.Start;
                        var right = Left - WindowLeft + span.End;

                        if (left < ScreenLeft) left = ScreenLeft;
                        if (right > ScreenRight) right = ScreenRight;

                        screen.RecolorLine(left, cr, right - left + 1, span.ForegroundColor, span.BackgroundColor);
                    }
                }
            }

            RenderScrollBars(screen);
        }

        private void RenderScrollBars(Screen screen)
        {
            if (_scrollBars && ScrollBarsVisible)
            {
                ConsoleColor fg, bg;
                if (screen.Theme == ControlTheme.Basic)
                {
                    fg = ConsoleColor.Black;
                    bg = ConsoleColor.Gray;
                }
                else
                {
                    fg = ConsoleColor.Blue;
                    bg = ConsoleColor.DarkCyan;
                }

                screen.DrawLeftArrow(ScreenLeft, ScreenBottom + 1, fg, bg);
                for (int x = ScreenLeft + 1; x < ScreenRight; x++)
                {
                    screen.DrawLightShade(x, ScreenBottom + 1, fg, bg);
                }
                screen.DrawRightArrow(ScreenRight, ScreenBottom + 1, fg, bg);
                var blockColumn = Utility.GetRangeValue(ScreenLeft + 1, ScreenRight - 1, ((double)WindowLeft / 255));
                if (screen.Theme == ControlTheme.Basic)
                    screen.DrawBlock(blockColumn, ScreenBottom + 1, fg, bg);
                else
                    screen.DrawMiniBlock(blockColumn, ScreenBottom + 1, fg, bg);


                screen.DrawUpArrow(ScreenRight + 1, ScreenTop, fg, bg);
                for (int y = ScreenTop + 1; y < ScreenBottom; y++)
                {
                    screen.DrawLightShade(ScreenRight + 1, y, fg, bg);
                }
                screen.DrawDownArrow(ScreenRight + 1, ScreenBottom, fg, bg);
                var blockRow = Utility.GetRangeValue(ScreenTop + 1, ScreenBottom - 1, ((double)CursorRow / (Buffer.Lines.Count - 1)));
                if (screen.Theme == ControlTheme.Basic)
                    screen.DrawBlock(ScreenRight + 1, blockRow, fg, bg);
                else
                    screen.DrawMiniBlock(ScreenRight + 1, blockRow, fg, bg);
            }
        }

        public override void OnBeforeKeyDown(object sender, KeyEvent e)
        {
            var screen = (Screen)sender;

            var lineText = Buffer.GetLineOrDefault(CursorRow).Text;

            if (e.ControlKeyState.IsControlPressed())
            {
                switch (e.VirtualKey)
                {
                    case VirtualKey.VK_HOME:
                        MoveCursor(0, 0);
                        break;
                    case VirtualKey.VK_END:
                        MoveCursor(this.Buffer.Lines.Count, 0);
                        break;
                }
            }
            else
            {
                switch (e.VirtualKey)
                {
                    case VirtualKey.VK_DOWN:
                        CursorRow++;
                        break;
                    case VirtualKey.VK_UP:
                        CursorRow--;
                        break;
                    case VirtualKey.VK_RIGHT:
                        CursorColumn++;
                        break;
                    case VirtualKey.VK_LEFT:
                        CursorColumn--;
                        break;
                    case VirtualKey.VK_HOME:
                        var preSpace = lineText.Length - lineText.TrimStart().Length;
                        if (CursorColumn != preSpace) { CursorColumn = preSpace; }
                        else { CursorColumn = 0; }
                        break;
                    case VirtualKey.VK_END:
                        CursorColumn = lineText.Length;
                        break;
                    case VirtualKey.VK_PRIOR:
                        CursorRow -= Height;
                        break;
                    case VirtualKey.VK_NEXT:
                        CursorRow += Height;
                        break;
                    case VirtualKey.VK_ESCAPE:
                        break;
                    case VirtualKey.VK_TAB:
                    case VirtualKey.VK_BACK:
                    case VirtualKey.VK_DELETE:
                        CharacterEdit(e, screen);
                        break;
                    default:
                        if (e.Character != '\0') CharacterEdit(e, screen);
                        break;
                }
            }

            NotifyCursorMoved(screen);
        }

        ViewSpan lastNotifySpan = new ViewSpan(-1, -1);

        private void NotifyCursorMoved(Screen screen)
        {
            UpdateWindow(screen);

            if (CursorRow != lastNotifySpan.StartRow)
            {
                if (RowChanged != null) RowChanged(this, new RowChangedEventArgs(lastNotifySpan.StartRow, CursorRow));
            }
            if (Selection != lastNotifySpan)
            {
                if (SelectionChanged != null) SelectionChanged(this, new SelectionChangedEventArgs(lastNotifySpan, Selection));
            }

            lastNotifySpan = Selection;
        }

        private void CharacterEdit(char ch, Screen screen)
        {
            if (CursorRow < 0 || CursorRow > Buffer.Lines.Count || CursorColumn < 0)
                return;

            if (ch == '\0') return;

            var lineText = Buffer.GetLineOrDefault(CursorRow).Text;
            var preSpace = lineText.Length - lineText.TrimStart().Length;

            switch (ch)
            {
                case (char)ConsoleKey.Tab:
                    var newColumn = (CursorColumn + 4) - (CursorColumn % 4);
                    if (preSpace < lineText.Length)
                    {
                        Buffer.InsertText(CursorRow, CursorColumn, new string(' ', newColumn - CursorColumn));
                    }
                    
                    CursorColumn = newColumn;
                    break;

                case (char)ConsoleKey.Backspace:
                    if (CursorColumn > 0)
                    {
                        int count;

                        if (CursorColumn <= preSpace || lineText.Trim().Length == 0) { count = ((CursorColumn - 1) % 4) + 1; }
                        else { count = 1; }

                        Buffer.RemoveText(CursorRow, CursorColumn - count, count);
                        CursorColumn -= count;
                    }
                    else
                    {
                        if (CursorRow > 0)
                        {
                            MoveCursor(CursorRow - 1, Buffer.Lines[CursorRow - 1].Text.Length);
                            if (CursorRow < Buffer.Lines.Count - 1)
                            {
                                Buffer.AppendText(CursorRow, Buffer.Lines[CursorRow + 1].Text);
                                Buffer.RemoveLine(CursorRow + 1);
                            }
                        }
                    }
                    break;

                case (char)ConsoleKey.Enter:
                    if (CursorRow >= Buffer.Lines.Count)
                    {
                        Buffer.InsertLine(CursorRow, "");
                    }
                    var padding = Buffer.Lines[CursorRow].Text.Length - Buffer.Lines[CursorRow].Text.TrimStart(' ').Length;
                    var truncatedText = Buffer.TruncateText(CursorRow, CursorColumn);
                    Buffer.InsertLine(CursorRow + 1, new string(' ', padding) + truncatedText);
                    MoveCursor(CursorRow + 1, padding);
                    break;

                default:
                    Buffer.InsertText(CursorRow, CursorColumn, ch.ToString());
                    CursorColumn++;
                    break;
            }
        }

        public void CharacterEdit(KeyEvent e, Screen screen)
        {
            _windowUpdatesSuspended = true;

            try
            {
                if (CursorRow < 0 || CursorRow > Buffer.Lines.Count || CursorColumn < 0)
                    return;

                switch (e.VirtualKey)
                {
                    case VirtualKey.VK_DELETE:
                        if (CursorColumn < Buffer.GetLineOrDefault(CursorRow).Text.Length)
                        {
                            Buffer.RemoveText(CursorRow, CursorColumn, 1);
                        }
                        else
                        {
                            if (CursorRow < Buffer.Lines.Count - 1)
                            {
                                Buffer.AppendText(CursorRow, Buffer.Lines[CursorRow + 1].Text);
                                Buffer.RemoveLine(CursorRow + 1);
                            }
                        }
                        break;
                    case VirtualKey.VK_TAB:
                    case VirtualKey.VK_BACK:
                    case VirtualKey.VK_RETURN:
                    default:
                        CharacterEdit(e.Character, screen);
                        break;
                }
            }
            finally
            {
                _windowUpdatesSuspended = false;

                UpdateWindow(screen);
            }
        }

        private bool _windowUpdatesSuspended = false;

        private void UpdateWindow(Screen screen)
        {
            if (_windowUpdatesSuspended) return;

            _windowUpdatesSuspended = true;

            try
            {
                bool scrolled = false;

                int WindowBottom = WindowTop + Height - 1;
                int WindowRight = WindowLeft + Width - 1;

                if (CursorRow < 0)
                {
                    CursorRow = 0;
                }
                if (CursorRow > Buffer.Lines.Count)
                {
                    CursorRow = Buffer.Lines.Count;
                }
                if (CursorColumn < 0)
                {
                    CursorColumn = 0;
                }

                if (CursorRow < WindowTop)
                {
                    WindowTop = CursorRow;
                    scrolled = true;
                }
                else if (CursorRow > WindowBottom)
                {
                    WindowTop = CursorRow - Height + 1;
                    scrolled = true;
                }

                if (CursorColumn < WindowLeft)
                {
                    WindowLeft = CursorColumn;
                    scrolled = true;
                }
                else if (CursorColumn > WindowRight)
                {
                    WindowLeft = CursorColumn - Width + 1;
                    scrolled = true;
                }

                if (scrolled)
                {
                    screen.Invalidate();
                }
            }
            finally
            {
                _windowUpdatesSuspended = false;
            }
        }

        public override void OnMouseDown(object sender, MouseEvent e)
        {
            MoveCursor(e.Y - ScreenTop, e.X - ScreenLeft);
        }

        public override void PlaceCursor(Screen screen)
        {
            UpdateWindow(screen);

            screen.SetCursorPosition(ScreenLeft + (CursorColumn - WindowLeft), ScreenTop + (CursorRow - WindowTop));
        }
    }

    public struct ViewSpan
    {
        public ViewSpan(int row, int col) : this()
        {
            this.StartRow = row;
            this.StartColumn = col;
            this.EndRow = row;
            this.EndColumn = col;
        }

        public ViewSpan(int startRow, int startCol, int endRow, int endCol) : this()
        {
            this.StartRow = startRow;
            this.StartColumn = startCol;
            this.EndRow = endRow;
            this.EndColumn = endCol;
        }

        public int StartRow { get; private set; }
        public int StartColumn { get; private set; }
        public int EndRow { get; private set; }
        public int EndColumn { get; private set; }

        public override bool Equals(object other)
        {
            return (other is ViewSpan && this == (ViewSpan)other);
        }

        public static bool operator ==(ViewSpan x, ViewSpan y)
        {
            return (x.StartRow == y.StartRow && x.StartColumn == y.StartColumn &&
                    x.EndRow == y.EndRow && x.EndColumn == y.EndColumn);
        }

        public static bool operator !=(ViewSpan x, ViewSpan y)
        {
            return !(x == y);
        }

        public override int GetHashCode()
        {
            return StartRow ^ StartColumn ^ EndRow ^ EndColumn;
        }
    }
}
