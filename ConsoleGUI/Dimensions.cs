// Copyright (c) Microsoft Corporation.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ConsoleGUI
{
    public struct Dimensions
    {
        internal int Width { get; set; }
        internal int Height { get; set; }

        internal Dimensions(int width, int height) : this()
        {
            this.Width = width;
            this.Height = height;
        }
    }
}
