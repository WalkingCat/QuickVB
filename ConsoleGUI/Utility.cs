// Copyright (c) Microsoft Corporation.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleGUI
{
    static class Utility
    {
        public static bool IsControlPressed(this ControlKeyState state)
        {
            return ((state & (ControlKeyState.LEFT_CTRL_PRESSED | ControlKeyState.RIGHT_CTRL_PRESSED)) != 0);
        }

        public static string SubstringPadRight(this string s, int startIndex, int length, int totalWidth, bool trim = false)
        {
            string sub;
            var l = Math.Min(length, s.Length - startIndex);
            if (startIndex > s.Length)
            {
                sub = "";
            }
            else
            {
                sub = s.Substring(startIndex, l);
            }
            sub = sub.PadRight(totalWidth);
            if (trim && sub.Length > totalWidth)
            {
                sub = sub.Substring(0, totalWidth);
            }
            return sub;
        }

        public static string SubstringPadRight(this string s, int startIndex, int totalWidth, bool trim = false)
        {
            return s.SubstringPadRight(startIndex, s.Length, totalWidth, trim);
        }

        public static int GetRangeValue(int from, int to, double progress)
        {
            var value = (int)Math.Floor(from + (progress * (to - from)));
            if (value < from) value = from;
            else if (value > to) value = to;

            return value;
        }
    }
}
