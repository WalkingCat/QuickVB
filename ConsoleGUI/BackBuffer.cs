// Copyright (c) Microsoft Corporation.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ConsoleGUI
{
    public struct BackBuffer
    {
        private char[] _frontbufferChars;
        private ConsoleColor[] _frontbufferForegroundColors;
        private ConsoleColor[] _frontbufferBackgroundColors;
        private char[] _backbufferChars;
        private ConsoleColor[] _backbufferForegroundColors;
        private ConsoleColor[] _backbufferBackgroundColors;
        private Dimensions _bufferDimensions;

        internal void ResetBufferSize(Dimensions dim, ConsoleColor fgColor, ConsoleColor bgColor)
        {
            _bufferDimensions = dim;
            int bufferSize = dim.Width * dim.Height;
            _frontbufferChars = new char[bufferSize];
            _frontbufferForegroundColors = new ConsoleColor[bufferSize];
            _frontbufferBackgroundColors = new ConsoleColor[bufferSize];
            _backbufferChars = new char[bufferSize];
            _backbufferForegroundColors = new ConsoleColor[bufferSize];
            _backbufferBackgroundColors = new ConsoleColor[bufferSize];

            ClearBuffer(ConsoleColor.Black, ConsoleColor.Black);
        }

        public void ClearBuffer(ConsoleColor fgColor, ConsoleColor bgColor)
        {
            for (int i = 0; i < _backbufferChars.Length; i += 1)
            {
                _frontbufferChars[i] = ' ';
                _frontbufferForegroundColors[i] = fgColor;
                _frontbufferBackgroundColors[i] = bgColor;
                _backbufferChars[i] = ' ';
                _backbufferForegroundColors[i] = fgColor;
                _backbufferBackgroundColors[i] = bgColor;
            }
        }

        internal void SetChar(int column, int row, char ch, ConsoleColor foreground, ConsoleColor background)
        {
            int index = column + row * _bufferDimensions.Width;
            _backbufferChars[index] = ch;
            SetColor(column, row, foreground, background);
        }

        internal void SetColor(int column, int row, ConsoleColor foreground, ConsoleColor background)
        {
            int index = column + row * _bufferDimensions.Width;
            _backbufferForegroundColors[index] = foreground;
            _backbufferBackgroundColors[index] = background;
        }

        internal void ShadowChar(int column, int row)
        {
            int index = column + row * _bufferDimensions.Width;
            ConsoleColor[] foreground;
            ConsoleColor[] background;

            // Some chars are drawn inverted, but we don't know at this point.
            // Use black foreground color as a heuristic for this case.
            if (_backbufferForegroundColors[index] == ConsoleColor.Black)
            {
                foreground = _backbufferBackgroundColors;
                background = _backbufferForegroundColors;
            }
            else
            {
                foreground = _backbufferForegroundColors;
                background = _backbufferBackgroundColors;
            }

            int fg = (int)foreground[index];
            if (fg == 7)
                fg = 8;
            else
                fg &= ~0x8;

            foreground[index] = (ConsoleColor)fg;
            background[index] = ConsoleColor.Black;
        }

        public int Width { get { return _bufferDimensions.Width; } }
        public int Height { get { return _bufferDimensions.Height; } }

        internal void CopyToConsole(Win32Console console)
        {
            bool[] dirtyRows = new bool[_bufferDimensions.Height];

            for (int row = 0; row < _bufferDimensions.Height; row += 1)
            {
                for (int column = 0; column < _bufferDimensions.Width; column += 1)
                {
                    if (CellChanged(row, column))
                    {
                        dirtyRows[row] = true;
                        break;
                    }
                }
            }

            var spans = new List<RowSpan>();
            for (int row = 0; row < _bufferDimensions.Height; row += 1)
            {
                for (int column = 0; column < _bufferDimensions.Width; column += 1)
                {
                    if (!CellChanged(row, column))
                        continue;

                    int start = column;
                    while (column < _bufferDimensions.Width &&
                           CellChanged(row, column))
                    {
                        column += 1;
                    }

                    spans.Add(new RowSpan(row, start, column));
                }
            }

            console.WriteOutputBuffer(_backbufferChars, _backbufferForegroundColors, _backbufferBackgroundColors, _bufferDimensions.Width, _bufferDimensions.Height, spans);

            var tempbufferChars = _frontbufferChars;
            var tempbufferForegroundColors = _frontbufferForegroundColors;
            var tempbufferBackgroundColors = _frontbufferBackgroundColors;

            _frontbufferChars = _backbufferChars;
            _frontbufferForegroundColors = _backbufferForegroundColors;
            _frontbufferBackgroundColors = _backbufferBackgroundColors;

            _backbufferChars = tempbufferChars;
            _backbufferForegroundColors = tempbufferForegroundColors;
            _backbufferBackgroundColors = tempbufferBackgroundColors;
        }

        private bool CellChanged(int row, int column)
        {
            int index = column + row * _bufferDimensions.Width;
            return (_frontbufferChars[index] != _backbufferChars[index] ||
                    _frontbufferForegroundColors[index] != _backbufferForegroundColors[index] ||
                    _frontbufferBackgroundColors[index] != _backbufferBackgroundColors[index]);
        }

        internal void RestoreBufferToScreen(Win32Console console)
        {
            console.WriteOutputBuffer(_frontbufferChars, _frontbufferForegroundColors, _frontbufferBackgroundColors, _bufferDimensions.Width, _bufferDimensions.Height, null);
        }
    }

    class RowSpan
    {
        public RowSpan(int row, int start, int end)
        {
            this.Row = row;
            this.Start = start;
            this.End = end;
        }

        public int Row { get; private set; }
        public int Start { get; private set; }
        public int End { get; private set; }
    }
}
