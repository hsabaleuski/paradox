// Copyright (c) 2014 Silicon Studio Corp. (http://siliconstudio.co.jp)
// This file is distributed under GPL v3. See LICENSE.md for details.

using SiliconStudio.Core;
using SiliconStudio.Core.Mathematics;
using SiliconStudio.Xenko.Engine;

namespace SiliconStudio.Xenko.Rendering.Lights
{
    /// <summary>
    /// Base implementation of <see cref="IDirectLight"/>.
    /// </summary>
    [DataContract()]
    public abstract class DirectLightBase : ColorLightBase, IDirectLight
    {
        /// <summary>
        /// Gets or sets the shadow.
        /// </summary>
        /// <value>The shadow.</value>
        /// <userdoc>The settings of the light shadow</userdoc>
        [DataMember(200)]
        public LightShadowMap Shadow { get; protected set; }

        public abstract bool HasBoundingBox { get; }

        public abstract BoundingBox ComputeBounds(Vector3 position, Vector3 direction);

        public float ComputeScreenCoverage(RenderContext context, Vector3 position, Vector3 direction)
        {
            var viewport = context.GraphicsDevice.Viewport;
            var camera = context.GetCurrentCamera();
            if (camera == null)
            {
                return 0.0f;
            }
            return ComputeScreenCoverage(camera, position, direction, viewport.Width, viewport.Height);
        }

        protected abstract float ComputeScreenCoverage(CameraComponent camera, Vector3 position, Vector3 direction, float width, float height);
    }
}