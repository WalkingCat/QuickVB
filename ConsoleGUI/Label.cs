// Copyright (c) Microsoft Corporation.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleGUI
{
    public class Label : Control
    {
        public Label()
        {
            this.Height = 1;
        }

        public string Text { get; set; }

        public override void Render(Screen screen)
        {
            if (!Visible) return;

            screen.DrawHorizontalLine(Left, Right, Top, ' ', ForegroundColor, BackgroundColor);
            screen.DrawString(Left + PaddingLeft, Top + PaddingTop, Text.SubstringPadRight(0, Width - PaddingLeft - PaddingRight, trim: true), ForegroundColor, BackgroundColor);
        }
    }
}
