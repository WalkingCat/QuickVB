// Copyright (c) Microsoft Corporation.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleGUI
{
    public class TextBuffer
    {
        private ObservableCollection<TextBufferLine> _lines;

        public event EventHandler BufferChanged;

        public TextBuffer()
        {
            InitLines(Enumerable.Empty<TextBufferLine>());
        }

        private void InitLines(IEnumerable<TextBufferLine> lines)
        {
            _lines = new ObservableCollection<TextBufferLine>(lines);
            _lines.CollectionChanged += Lines_CollectionChanged;
        }

        void Lines_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (BufferChanged != null) BufferChanged(this, null);
        }

        public string Text
        {
            get
            {
                return string.Join("\r\n", _lines.Select(l => l.Text));
            }
            set
            {
                var textLines = value.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None).Select(s => new TextBufferLine { Text = s });
                InitLines(textLines);

                if (BufferChanged != null) BufferChanged(this, null);
                //Screen.GetScreen().Invalidate();
            }
        }

        public IReadOnlyList<TextBufferLine> Lines
        {
            get
            {
                return new ReadOnlyObservableCollection<TextBufferLine>(_lines);
            }
        }


        public void InsertText(int row, int col, string text)
        {
            if (row < this.Lines.Count)
            {
                var line = _lines[row];

                var oldText = line.Text;
                var newText = oldText.SubstringPadRight(0, col, col) + text + oldText.SubstringPadRight(col, 0);
                line.Text = newText;

                var spans = line.ColorSpans;
                for (int s = 0; s < spans.Count; s++)
                {
                    var span = spans[s];

                    if (span.Contains(col))
                    {
                        line.ColorSpans.RemoveAt(s--);
                    }
                    else if (col < span.Start)
                    {
                        span.Start += text.Length;
                    }
                }

                if (BufferChanged != null) BufferChanged(this, null);
            }
            else
            {
                InsertLine(row, new String(' ', col) + text);
            }
        }

        public void AppendText(int row, string text)
        {
            var oldText = _lines[row].Text;
            InsertText(row, oldText.Length, text);
        }

        public void RemoveText(int row, int col, int count)
        {
            var line = _lines[row];

            var oldText = line.Text;
            var newText = oldText.SubstringPadRight(0, col, col) + oldText.SubstringPadRight(col + count, 0);
            line.Text = newText;

            var spans = line.ColorSpans;
            for (int s = 0; s < spans.Count; s++)
            {
                var span = spans[s];

                if (span.Contains(col))
                {
                    line.ColorSpans.RemoveAt(s--);
                }
                else if (col < span.Start)
                {
                    span.Start -= count;
                }
            }

            if (BufferChanged != null) BufferChanged(this, null);
        }

        public string TruncateText(int row, int length)
        {
            var oldText = _lines[row].Text;
            RemoveText(row, length, oldText.Length - length);
            return oldText.SubstringPadRight(length, 0);
        }


        public void InsertLine(int row, string text)
        {
            var line = new TextBufferLine { Text = text };
            _lines.Insert(row, line);
        }

        public void RemoveLine(int row)
        {
            _lines.RemoveAt(row);
        }


        public TextBufferLine GetLineOrDefault(int l)
        {
            TextBufferLine line;
            if (l < _lines.Count)
            {
                line = _lines[l];
            }
            else
            {
                line = new TextBufferLine();
            }
            return line;
        }
    }

    public class TextBufferLine
    {
        // TODO: Text should only really be set from the TextBuffer methods
        public string Text { get; internal set; }
        public List<ColorSpan> ColorSpans { get; private set; }

        internal TextBufferLine()
        {
            Text = "";
            ColorSpans = new List<ColorSpan>();
        }
    }

    public class ColorSpan
    {
        public int Start;
        public int Length;
        public ConsoleColor ForegroundColor;
        public ConsoleColor BackgroundColor;
        public bool IsSticky;

        public int End
        {
            get { return Start + Length - 1; }
        }

        public bool Contains(int position)
        {
            return (position >= Start && position <= End);
        }

        public bool IntersectsWith(int start, int end)
        {
            return (this.Contains(start) || this.Contains(end) || (start < this.Start && end > this.End));
        }
    }
}
