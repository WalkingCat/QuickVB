// Copyright (c) Microsoft Corporation.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;

namespace ConsoleGUI
{
    public class MenuItem : Control
    {
        public bool AnchorRight { get; set; }
        public bool Separator { get; set; }
        public Menu DropDownMenu { get; set; }
        public bool HighlightAccelerator { get; set; }
        public Action Action { get; set; }
        public Menu ParentMenu { get; set; }

        private string _name;
        private char? _accelerator;
        private int _acceleratorCharIndex;
        private bool _active;
        private Menu _activator;

        public MenuItem(string Name = null, Action Action = null, Menu DropDownMenu = null, bool Visible = true, bool Separator = false, bool AnchorRight = false)
        {
            this.Name = Name;
            this.Action = Action;
            this.DropDownMenu = DropDownMenu;
            this.Visible = Visible;
            this.Separator = Separator;
            this.AnchorRight = AnchorRight;
        }

        public string Name
        {
            get { return _name; }
            set
            {
                _accelerator = null;
                if (value != null)
                {
                    _acceleratorCharIndex = value.IndexOf('&');
                    _name = value;
                    if (_acceleratorCharIndex != -1)
                    {
                        _name = _name.Remove(_acceleratorCharIndex, 1);
                        _accelerator = _name[_acceleratorCharIndex];
                    }
                }
                else
                {
                    _name = null;
                }
            }
        }

        public char? Accelerator
        {
            get { return _accelerator; }
        }

        public void Activate(Screen screen, Menu activator)
        {
            _active = true;
            screen.ActiveControl = this;
            _activator = activator;
            screen.Invalidate();
        }

        public void Popup(Screen screen)
        {
            _active = true;
            _activator = null;
            screen.Invalidate();
        }

        public void Deactivate(Screen screen)
        {
            _active = false;
            if (_activator != null)
            {
                screen.ActiveControl = _activator;
                _activator = null;
            }
            screen.Invalidate();
        }

        private void ActivateDropDownMenu(object sender)
        {
            DropDownMenu.Left = this.Left;
            DropDownMenu.Top = this.Top + 1;
            DropDownMenu.Layout();
            if (AnchorRight)
            {
                DropDownMenu.Left = this.Right - DropDownMenu.Width;
                DropDownMenu.Layout();
            }
            DropDownMenu.Activate((Screen)sender, this);
        }

        internal void TerminateMenuMode()
        {
            Deactivate(Screen.GetScreen());
            Menu parent = Screen.GetScreen().ActiveControl as Menu;
            if (parent != null)
            {
                parent.TerminateMenuMode();
            }
        }
        
        public override void OnBeforeKeyDown(object sender, KeyEvent e)
        {
            // Really should just check for enter, down & dropdown, and accelerator,
            // then execute action delegate.
            if (this.DropDownMenu != null)
            {
                if (e.VirtualKey == VirtualKey.VK_DOWN || IsAcceleratorMatch(e.Character))
                {
                    ActivateDropDownMenu(sender);
                    e.Handled = true;
                    return;
                }
            }
            else if (this.Action != null)
            {
                if (e.VirtualKey == VirtualKey.VK_RETURN || IsAcceleratorMatch(e.Character))
                {
                    Select();
                    e.Handled = true;
                    return;
                }
            }

            if (e.VirtualKey == VirtualKey.VK_ESCAPE)
            {
                TerminateMenuMode();
                e.Handled = true;
                return;
            }

            if (_activator != null && !IsAcceleratorMatch(e.Character))
            {
                var activator = _activator;
                activator.OnBeforeKeyDown(sender, e);
                if (Screen.GetScreen().ActiveControl != this)
                    _active = false;
                activator.OnAfterKeyDown(sender, e);
            }
        }

        public override void OnMouseDown(object sender, MouseEvent e)
        {
            var screen = (Screen)sender;
            var activator = screen.ActiveControl;
            var parentMenu = this.ParentMenu;
            if (parentMenu == null) return;

            if (this.DropDownMenu != null)
            {
                var previousMenuItem = activator as MenuItem;
                if (previousMenuItem == null)
                {
                    parentMenu.Activate(screen, activator);
                }
                else
                {
                    previousMenuItem.ParentMenu.Deactivate(screen);
                }
                parentMenu.ActivateMenuItem(screen, this);
                this.ActivateDropDownMenu(screen);
            }
            else if (this.Action != null)
            {
                parentMenu.ActivateMenuItem(screen, this);
                this.Select();
            }
        }

        public void Select()
        {
            if (this.Action != null)
            {
                TerminateMenuMode();
                Action();
            }
        }

        public override void OnKeyUp(object sender, KeyEvent e)
        {
            if (e.VirtualKey == VirtualKey.VK_MENU)
            {
                Deactivate((Screen)sender);
                ((Screen)sender).ActiveControl.OnKeyUp(sender, e);
            }
        }

        internal bool IsAcceleratorMatch(char character)
        {
            if (this.Accelerator == null)
                return false;

            return (char.ToLowerInvariant((char)this.Accelerator) == char.ToLowerInvariant(character));
        }

        public override void Render(Screen screen)
        {
            if (!Visible) return;

            ConsoleColor fg, bg, acceleratorColor;
            if (screen.Theme == ControlTheme.CSharp)
            {
                fg = _active ? ConsoleColor.Black : ConsoleColor.Black;
                bg = _active ? ConsoleColor.DarkGreen : ConsoleColor.Gray;
                acceleratorColor = ConsoleColor.DarkRed;
            }
            else
            {
                fg = _active ? ConsoleColor.Gray : ConsoleColor.Black;
                bg = _active ? ConsoleColor.Black : ConsoleColor.Gray;
                acceleratorColor = ConsoleColor.White;
            }

            bool grayedOut = DropDownMenu == null && Action == null && !Separator;
            if (grayedOut)
                fg = ConsoleColor.DarkGray;

            if (Separator)
            {
                screen.DrawLeftTee(this.ScreenLeft, this.ScreenTop, fg, bg);
                screen.DrawRightTee(this.ScreenRight, this.ScreenTop, fg, bg);
                screen.DrawHorizontalLine(this.ScreenLeft + 1, this.ScreenRight - 1, this.ScreenTop, fg, bg);
            }
            else if ((!HighlightAccelerator && screen.Theme != ControlTheme.CSharp) || grayedOut)
            {
                screen.DrawHorizontalLine(this.ScreenLeft, this.ScreenRight, this.ScreenTop, ' ', fg, bg);
                screen.DrawString(this.ScreenLeft + 1, this.ScreenTop, this.Name, fg, bg);
            }
            else
            {
                screen.DrawHorizontalLine(this.ScreenLeft, this.ScreenRight, this.ScreenTop, ' ', fg, bg);
                
                screen.DrawString(this.ScreenLeft + 1, this.ScreenTop, this.Name, fg, bg);
                if (_acceleratorCharIndex != -1) { screen.DrawString(this.ScreenLeft + 1 + _acceleratorCharIndex, this.ScreenTop, this.Name.Substring(_acceleratorCharIndex, 1), acceleratorColor, bg); }
            }
        }
    }

    public class MenuItem<TValue> : MenuItem
    {
        public TValue Value { get; set; }

        public MenuItem(string Name = null, TValue Value = default(TValue), Action Action = null, Menu DropDownMenu = null, bool Visible = true, bool Separator = false, bool AnchorRight = false) : base(Name, Action, DropDownMenu, Visible, Separator, AnchorRight)
        {
            this.Value = Value;
        }
    }
}
