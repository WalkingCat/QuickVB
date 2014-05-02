// Copyright (c) Microsoft Corporation.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace ConsoleGUI
{
    public class Menu : Control
    {
        public bool DropDownMenu { get; set; }
        public bool AllowNoSelection { get; set; }

        private List<MenuItem> _menuItems = new List<MenuItem>();
        private bool _active;
        private Control _activator;

        public MenuItem ActiveMenuItem { get; private set; }

        public IEnumerable<MenuItem> VisibleMenuItems
        {
            get
            {
                return _menuItems.Where(mi => mi.Visible);
            }
        }

        public void SetMenuItems(params MenuItem[] menuItems)
        {
            _menuItems = menuItems.ToList();
            foreach (var menuItem in menuItems)
            {
                menuItem.ParentMenu = this;
            }
            ActiveMenuItem = null;
            Layout();
        }

        public bool IsActive
        {
            get
            {
                return _active;
            }
        }

        public void Activate(Screen screen, Control activator)
        {
            this._activator = activator;
            if (this.DropDownMenu && !screen.Controls.Contains(this))
                screen.Controls.Add(this);
            if (activator != null)
                screen.ActiveControl = this;
            else
                screen.PopupControl = this;
            if (this.DropDownMenu && !this.AllowNoSelection && _menuItems.Count > 0)
                ActivateMenuItem(screen, _menuItems.First());
            screen.Invalidate();
            if (activator != null)
                screen.HideCursor();
        }

        public void ActivateMenuItem(Screen screen, MenuItem menuItem)
        {
            if (ActiveMenuItem != null)
                ActiveMenuItem.Deactivate(screen);
            ActiveMenuItem = menuItem;
            if (this._activator != null)
                menuItem.Activate(screen, this);
            else
                menuItem.Popup(screen);
        }

        public void Deactivate(Screen screen)
        {
            this._active = false;
            if (this.DropDownMenu)
                screen.Controls.Remove(this);
            if (this._activator != null)
                screen.ActiveControl = this._activator;
            else
                screen.PopupControl = null;
            screen.Invalidate();
            if (this._activator != null)
                screen.ShowCursor();
        }

        internal void TerminateMenuMode()
        {
            Deactivate(Screen.GetScreen());
            MenuItem menuItem = Screen.GetScreen().ActiveControl as MenuItem;
            if (menuItem != null)
            {
                menuItem.TerminateMenuMode();
            }
        }

        private bool HasFocus()
        {
            if (Screen.GetScreen().ActiveControl == this || Screen.GetScreen().PopupControl == this)
                return true;

            foreach (var menuItem in VisibleMenuItems)
            {
                if (Screen.GetScreen().ActiveControl == menuItem)
                    return true;
            }

            return false;
        }

        public void Layout()
        {
            if (DropDownMenu)
                LayoutAsDropDownMenu();
            else
                LayoutAsMenuBar();
        }

        private void LayoutAsMenuBar()
        {
            int runningLeft = this.Left + 2;
            foreach (var menuItem in VisibleMenuItems)
            {
                if (menuItem.AnchorRight)
                    continue;

                menuItem.Width = menuItem.Name.Length + 2;
                menuItem.Height = 1;
                menuItem.Left = runningLeft;
                menuItem.Top = this.Top;

                runningLeft += menuItem.Width;
            }

            int runningRight = this.Left + this.Width - 1;
            foreach (var menuItem in Enumerable.Reverse(VisibleMenuItems))
            {
                if (!menuItem.AnchorRight)
                    continue;

                menuItem.Width = menuItem.Name.Length + 2;
                menuItem.Height = 1;
                menuItem.Left = runningRight - menuItem.Width;
                menuItem.Top = this.Top;

                runningRight -= menuItem.Width;
            }
        }

        private void LayoutAsDropDownMenu()
        {
            var screen = Screen.GetScreen();

            int runningTop = this.Top + 1;
            int maxWidth = 0;

            foreach (var menuItem in VisibleMenuItems)
            {
                if (!menuItem.Separator)
                {
                    menuItem.Width = menuItem.Name.Length + 2;

                    if (maxWidth < menuItem.Width)
                    {
                        maxWidth = menuItem.Width;
                    }
                }

                menuItem.Height = 1;
                menuItem.Left = this.Left + 1;
                menuItem.Top = runningTop;

                runningTop += 1;
            }

            this.Width = maxWidth + 2;
            this.Height = runningTop - this.Top + 1;

            int dx = 0, dy = 0;
            if (screen != null)
            {
                if (this.ScreenBottom > (screen.Bottom - 2))
                {
                    dy -= (this.ScreenBottom - (screen.Bottom - 2));
                }
                if (this.ScreenRight > (screen.Right - 2))
                {
                    dx -= (this.ScreenRight - (screen.Right - 2));
                }
            }
            this.Top += dy;
            this.Left += dx;

            foreach (var menuItem in VisibleMenuItems)
            {
                menuItem.Left += dx;
                menuItem.Top += dy;
                if (menuItem.Separator)
                {
                    menuItem.Left -= 1;
                    menuItem.Width = maxWidth + 2;
                }
                else
                {
                    menuItem.Width = maxWidth;
                }
            }
        }

        public void SelectMenuItem()
        {
            if (ActiveMenuItem != null)
                ActiveMenuItem.Select();
        }

        private MenuItem MenuItemFromAccelerator(char accelerator)
        {
            foreach (var menuItem in VisibleMenuItems)
            {
                if (!menuItem.Separator && menuItem.IsAcceleratorMatch(accelerator))
                {
                    return menuItem;
                }
            }

            return null;
        }

        private MenuItem MenuItemFromCursorKey(VirtualKey virtualKey)
        {
            const int NextMenuItem = 0;
            const int PrevMenuItem = 1;
            const int Nothing = 2;
            int action = Nothing;

            if (DropDownMenu)
            {
                if (virtualKey == VirtualKey.VK_UP)
                    action = PrevMenuItem;
                else if (virtualKey == VirtualKey.VK_DOWN)
                    action = NextMenuItem;
            }
            else
            {
                if (virtualKey == VirtualKey.VK_LEFT)
                    action = PrevMenuItem;
                else if (virtualKey == VirtualKey.VK_RIGHT)
                    action = NextMenuItem;
            }

            if (action == Nothing || !VisibleMenuItems.Any())
                return null;

            var currentMenuItem = ActiveMenuItem ?? VisibleMenuItems.First();

            var index = _menuItems.IndexOf(currentMenuItem);
            var count = VisibleMenuItems.Count();

            do
            {
                if (action == NextMenuItem)
                    index = (index + 1) % count;
                else if (action == PrevMenuItem)
                    index = (index - 1 + count) % count;
            }
            while (_menuItems[index].Separator);

            return _menuItems[index];
        }

        public override void OnBeforeKeyDown(object sender, KeyEvent e)
        {
            if (!this._active && e.VirtualKey != VirtualKey.VK_MENU)
            {
                // Note we only do this when the VK is not VK_MENU so that we don't
                // become active if alt key is held down and producing key repeats.
                this._active = true;
            }

            if (e.VirtualKey == VirtualKey.VK_ESCAPE)
            {
                TerminateMenuMode();
            }

            var menuItem = MenuItemFromCursorKey(e.VirtualKey);
            bool sendKeyDownToMenuItem = false;

            if (menuItem == null)
            {
                menuItem = MenuItemFromAccelerator(e.Character);
                if (menuItem != null)
                {
                    sendKeyDownToMenuItem = true;
                    e.Handled = true;
                }
            }

            if (menuItem != null)
            {
                ActivateMenuItem((Screen)sender, menuItem);
                if (sendKeyDownToMenuItem)
                {
                    menuItem.OnBeforeKeyDown(sender, e);
                    menuItem.OnAfterKeyDown(sender, e);
                }
                e.Handled = true;
            }

            if (!e.Handled)
            {
                if (_activator == null)
                {
                    if (ActiveMenuItem != null)
                    {
                        ActiveMenuItem.OnBeforeKeyDown(sender, e);
                        ActiveMenuItem.OnAfterKeyDown(sender, e);
                    }
                }
                else
                {
                    if (DropDownMenu)
                    {
                        if (e.VirtualKey == VirtualKey.VK_LEFT ||
                            e.VirtualKey == VirtualKey.VK_RIGHT)
                        {
                            Deactivate(Screen.GetScreen());
                            var eDown = new KeyEvent(true, ' ', 1, VirtualKey.VK_DOWN, 0);
                            _activator.OnBeforeKeyDown(sender, e);
                            Screen.GetScreen().ActiveControl.OnBeforeKeyDown(sender, eDown);
                            _activator.OnAfterKeyDown(sender, e);
                            Screen.GetScreen().ActiveControl.OnAfterKeyDown(sender, eDown);
                        }
                    }
                }
            }
        }

        public override void OnKeyUp(object sender, KeyEvent e)
        {
            if (!this._active)
            {
                this._active = true;
                if (VisibleMenuItems.Any() && !this.AllowNoSelection)
                    ActivateMenuItem((Screen)sender, VisibleMenuItems.First());
            }
            else
            {
                if (e.VirtualKey == VirtualKey.VK_MENU)
                {
                    Deactivate((Screen)sender);
                }
            }
        }

        public override Control HitTest(int x, int y)
        {
            foreach (var control in this.VisibleMenuItems)
            {
                var hit = control.HitTest(x, y);
                if (hit != null) return hit;
            }
            return null;
        }

        public override void Render(Screen screen)
        {
            if (!this.DropDownMenu)
            {
                screen.DrawHorizontalLine(this.ScreenLeft, this.ScreenRight, this.ScreenTop, ' ', ConsoleColor.Black, ConsoleColor.Gray);
            }
            else
            {
                screen.DrawShadow(this.ScreenLeft + 2, this.ScreenTop + 1, this.ScreenRight + 2, this.ScreenBottom + 1);
                screen.DrawBoxFill(this.ScreenLeft, this.ScreenTop, this.ScreenRight, this.ScreenBottom, ConsoleColor.Black, ConsoleColor.Gray);
            }

            foreach (var menuItem in VisibleMenuItems)
            {
                menuItem.HighlightAccelerator = HasFocus();
                menuItem.Render(screen);
            }
        }
    }
}
