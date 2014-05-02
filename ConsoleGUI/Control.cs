// Copyright (c) Microsoft Corporation.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;

namespace ConsoleGUI
{
    public abstract class Control
    {
        public Control()
        {
            ForegroundColor = ConsoleColor.Gray;
            BackgroundColor = ConsoleColor.Black;
            Visible = true;
        }

        public int Left { get; set; }
        public int Top { get; set; }

        public int Width { get; set; }
        public int Height { get; set; }

        public int Right { get { return Left + Width - 1; } }
        public int Bottom { get { return Top + Height - 1; } }

        public bool Visible { get; set; }

        public Control Parent { get; internal set; }

        public int ScreenLeft { get { return ((Parent != null) ? Parent.ScreenLeft : 0) + Left; } }
        public int ScreenTop { get { return ((Parent != null) ? Parent.ScreenTop : 0) + Top; } }

        public int ScreenRight { get { return ScreenLeft + Width - 1; } }
        public int ScreenBottom { get { return ScreenTop + Height - 1; } }

        public int PaddingLeft { get; set; }
        public int PaddingTop { get; set; }

        public int PaddingRight { get; set; }
        public int PaddingBottom { get; set; }

        public ConsoleColor BackgroundColor { get; set; }
        public ConsoleColor ForegroundColor { get; set; }

        public virtual void UpdateCursor(Screen screen) { }
        public abstract void Render(Screen screen);
        public virtual void PlaceCursor(Screen screen) { }

        public virtual void OnBeforeKeyDown(object sender, KeyEvent e) { }
        public virtual void OnAfterKeyDown(object sender, KeyEvent e) { }
        
        public virtual void OnKeyUp(object sender, KeyEvent e) { }

        public virtual void OnMouseDown(object sender, MouseEvent e) { }

        public virtual Control HitTest(int x, int y)
        {
            if (x >= Left && x <= Right && y >= Top && y <= Bottom) { return this; }
            else { return null; }
        }
    }
}
