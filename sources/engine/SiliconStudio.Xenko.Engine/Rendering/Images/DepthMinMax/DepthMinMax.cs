﻿// Copyright (c) 2014 Silicon Studio Corp. (http://siliconstudio.co.jp)
// This file is distributed under GPL v3. See LICENSE.md for details.

using System;

using SiliconStudio.Core.Mathematics;
using SiliconStudio.Xenko.Graphics;

namespace SiliconStudio.Xenko.Rendering.Images
{
    public class DepthMinMax : ImageEffect
    {
        internal static ParameterKey<bool> IsFirstPassKey = ParameterKeys.New<bool>();

        // TODO: Currently capturing two effects, because xkfx permutation triggers DynamicEffectCompiler
        private ImageEffectShader effectFirstPass, effectNotFirstPass;

        private ImageReadback<Vector2> readback;

        public DepthMinMax()
        {
        }

        protected override void InitializeCore()
        {
            base.InitializeCore();

            effectFirstPass = ToLoadAndUnload(new ImageEffectShader("DepthMinMaxEffect"));
            effectNotFirstPass = ToLoadAndUnload(new ImageEffectShader("DepthMinMaxEffect"));
            readback = ToLoadAndUnload(new ImageReadback<Vector2>());
        }

        public bool IsResultAvailable { get; private set; }

        public Vector2 Result { get; private set; }

        protected override void DrawCore(RenderContext context)
        {
            var input = GetSafeInput(0);

            Texture fromTexture = input;
            Texture downTexture = null;
            var nextSize = Math.Max(MathUtil.NextPowerOfTwo(input.Size.Width), MathUtil.NextPowerOfTwo(input.Size.Height));
            bool isFirstPass = true;
            while (nextSize > 1)
            {
                nextSize = nextSize / 2;
                downTexture = NewScopedRenderTarget2D(nextSize, nextSize, PixelFormat.R32G32_Float, 1);

                var effect = isFirstPass ? effectFirstPass : effectNotFirstPass;
                effect.Parameters.Set(DepthMinMaxShaderKeys.TextureMap, fromTexture);
                effect.Parameters.Set(DepthMinMaxShaderKeys.TextureReduction, fromTexture);

                effect.SetOutput(downTexture);
                effect.Parameters.Set(IsFirstPassKey, isFirstPass);
                effect.Draw(context);

                fromTexture = downTexture;

                isFirstPass = false;
            }

            readback.SetInput(downTexture);
            readback.Draw();
            IsResultAvailable = readback.IsResultAvailable;
            if (IsResultAvailable)
            {
                float min = float.MaxValue;
                float max = -float.MaxValue;
                var results = readback.Result;
                foreach (var result in results)
                {
                    min = Math.Min(result.X, min);
                    if (result.Y != 1.0f)
                    {
                        max = Math.Max(result.Y, max);
                    }
                }

                Result = new Vector2(min, max);
            }
        }
    }
}