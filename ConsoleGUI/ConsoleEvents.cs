// Copyright (c) Microsoft Corporation.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ConsoleGUI
{
    public class ConsoleEvent
    {
    }

    public class NotImplementedEvent : ConsoleEvent
    {
        private string _message;
 
        public NotImplementedEvent(string message)
        {
            _message = message;
        }

        public override string ToString()
        {
            return "NotImplementedEvent: " + _message;
        }
    }

    public class KeyEvent : ConsoleEvent
    {
        public bool KeyDown { get; private set; }
        public char Character { get; private set; }
        public ushort RepeatCount { get; private set; }
        public VirtualKey VirtualKey { get; private set; }
        public ControlKeyState ControlKeyState { get; private set; }

        public bool Handled { get; set; }

        public KeyEvent(bool keyDown, char character, ushort repeatCount, VirtualKey virtualKey, ControlKeyState controlKeyState)
        {
            this.KeyDown = keyDown;
            this.Character = character;
            this.RepeatCount = repeatCount;
            this.VirtualKey = virtualKey;
            this.ControlKeyState = controlKeyState;

            this.Handled = false;
        }
    }

    public class MouseEvent : ConsoleEvent
    {
        public short X { get; private set; }
        public short Y { get; private set; }
        public ButtonState ButtonState { get; private set; }
        public ControlKeyState ControlKeyState { get; private set; }
        public MouseEventFlags MouseEventFlags { get; private set; }

        public MouseEvent(short x, short y, ButtonState buttonState, ControlKeyState controlKeyState, MouseEventFlags mouseEventFlags)
        {
            this.X = x;
            this.Y = y;
            this.ButtonState = buttonState;
            this.ControlKeyState = controlKeyState;
            this.MouseEventFlags = mouseEventFlags;
        }
    }

    public class BufferSizeEvent : ConsoleEvent
    {
        public BufferSizeEvent(int width, int height)
        {
            Dimensions = new Dimensions(width, height);
        }

        public Dimensions Dimensions { get; set; }
    }
}
