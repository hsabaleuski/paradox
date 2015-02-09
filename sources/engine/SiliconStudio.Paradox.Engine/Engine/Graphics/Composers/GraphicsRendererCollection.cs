// Copyright (c) 2014 Silicon Studio Corp. (http://siliconstudio.co.jp)
// This file is distributed under GPL v3. See LICENSE.md for details.

using System.Collections.Generic;

using SiliconStudio.Core;

namespace SiliconStudio.Paradox.Engine.Graphics.Composers
{
    /// <summary>
    /// A collection of <see cref="IGraphicsRenderer"/>.
    /// </summary>
    [DataContract("GraphicsRendererCollection")]
    public sealed class GraphicsRendererCollection : List<IGraphicsRenderer>
    {
    }
}