﻿// Copyright (c) 2014-2015 Silicon Studio Corp. (http://siliconstudio.co.jp)
// This file is distributed under GPL v3. See LICENSE.md for details.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using SiliconStudio.Core;
using SiliconStudio.Core.Mathematics;
using SiliconStudio.Core.Serialization;
using SiliconStudio.Core.Serialization.Contents;
using SiliconStudio.Xenko.Engine.Design;

namespace SiliconStudio.Xenko.Physics
{
    [ContentSerializer(typeof(DataContentSerializer<ColliderShapeAssetDesc>))]
    [DataContract("ColliderShapeAssetDesc")]
    [Display(50, "Asset")]
    public class ColliderShapeAssetDesc : IInlineColliderShapeDesc
    {
        /// <userdoc>
        /// The reference to the collider Shape asset.
        /// </userdoc>
        [DataMember(10)]
        public PhysicsColliderShape Shape { get; set; }

        public int CompareTo(object obj)
        {
            var other = obj as ColliderShapeAssetDesc;
            if (other == null) return -1;

            if (other.Shape == null || Shape == null) return other.Shape == Shape ? 0 : 1;
            if (other.Shape.Descriptions.Count != Shape.Descriptions.Count) return 1;
            if (other.Shape.Descriptions.Where((t, i) => t.CompareTo(Shape.Descriptions[i]) != 0).Any())
            {
                return 1;
            }

            return other.Shape == Shape ? 0 : 1;
        }
    }
}