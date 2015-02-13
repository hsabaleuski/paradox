﻿// Copyright (c) 2014 Silicon Studio Corp. (http://siliconstudio.co.jp)
// This file is distributed under GPL v3. See LICENSE.md for details.

using SiliconStudio.Core;
using SiliconStudio.Paradox.EntityModel;

namespace SiliconStudio.Paradox.Engine
{
    /// <summary>
    /// A link to a scene that is rendered by a parent <see cref="Scene"/>.
    /// </summary>
    [DataContract("SceneChildComponent")]
    [DefaultEntityComponentProcessor(typeof(SceneChildProcessor))]
    public sealed class SceneChildComponent : EntityComponent
    {
        public readonly static PropertyKey<SceneChildComponent> Key = new PropertyKey<SceneChildComponent>("Key", typeof(SceneChildComponent));

        /// <summary>
        /// Initializes a new instance of the <see cref="SceneChildComponent"/> class.
        /// </summary>
        public SceneChildComponent()
        {
        }

        /// <summary>
        /// Gets or sets the child scene.
        /// </summary>
        /// <value>The scene.</value>
        [DataMember(10)]
        public Scene Scene { get; set; }

        public override PropertyKey DefaultKey
        {
            get
            {
                return Key;
            }
        }
    }
}