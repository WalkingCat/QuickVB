// Copyright (c) Microsoft Corporation.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;

namespace ConsoleGUI
{
    public abstract class ContainerControl : Control
    {
        protected ContainerControl()
        {
            Controls = new ObservableCollection<Control>();
            Controls.CollectionChanged += Controls_CollectionChanged;
        }

        void Controls_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (var control in e.OldItems.Cast<Control>())
                {
                    control.Parent = null;
                }
            }

            if (e.NewItems != null)
            {
                foreach (var control in e.NewItems.Cast<Control>())
                {
                    control.Parent = this;

                    if (Fill)
                    {
                        control.Left = PaddingLeft;
                        control.Top = PaddingTop;
                        control.Width = Width - PaddingLeft - PaddingRight;
                        control.Height = Height - PaddingTop - PaddingBottom;
                    }
                }
            }
        }

        public ObservableCollection<Control> Controls { get; private set; }

        public bool Fill { get; set; }

        public override void Render(Screen screen)
        {
            foreach (var control in Controls)
            {
                control.Render(screen);
            }
        }

        public override Control HitTest(int x, int y)
        {
            foreach (var control in this.Controls.Reverse())
            {
                var hit = control.HitTest(x, y);
                if (hit != null) return hit;
            }
            return null;
        }
    }
}
