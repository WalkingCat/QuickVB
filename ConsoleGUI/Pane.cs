// Copyright (c) Microsoft Corporation.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleGUI
{
    public class Pane : ContainerControl
    {
        public Pane()
        {
            this.Fill = true;
            this.PaddingLeft = 1;
            this.PaddingTop = 1;
            this.PaddingRight = 1;
            this.PaddingBottom = 1;
        }

        public string Title { get; set; }

        public override void Render(Screen screen)
        {
            screen.DrawTopLeftCorner(ScreenLeft, ScreenTop, ForegroundColor, BackgroundColor);
            screen.DrawTopRightCorner(ScreenRight, ScreenTop, ForegroundColor, BackgroundColor);

            if (ScreenLeft + 1 <= ScreenRight - 1)
            {
                screen.DrawHorizontalLine(ScreenLeft + 1, ScreenRight - 1, ScreenTop, ForegroundColor, BackgroundColor);
            }

            if (ScreenTop + 1 <= ScreenBottom - 1)
            {
                screen.DrawVerticalLine(ScreenLeft, ScreenTop + 1, ScreenBottom - 1, ForegroundColor, BackgroundColor);
                screen.DrawVerticalLine(ScreenRight, ScreenTop + 1, ScreenBottom - 1, ForegroundColor, BackgroundColor);
            }

            screen.DrawLeftTee(ScreenLeft, ScreenBottom, ForegroundColor, BackgroundColor);
            screen.DrawRightTee(ScreenRight, ScreenBottom, ForegroundColor, BackgroundColor);

            var titleString = " " + Title + " ";
            var titleLeft = (ScreenLeft + (ScreenRight - ScreenLeft) / 2) - (titleString.Length / 2);
            ConsoleColor fg, bg;
            if (screen.ActiveControl.Parent == this && screen.Theme == ControlTheme.Basic)
            {
                fg = BackgroundColor;
                bg = ForegroundColor;
            }
            else
            {
                fg = ForegroundColor;
                bg = BackgroundColor;
            }
            screen.DrawString(titleLeft, ScreenTop, titleString, fg, bg);

            base.Render(screen);
        }
    }
}
