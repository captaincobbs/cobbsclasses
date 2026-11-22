using System;
using System.Collections.Generic;
using System.Text;

using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace CobbsClasses
{
    internal sealed class DarkvisionRenderer(ICoreClientAPI clientAPI) : IRenderer
    {
        private const string STRENGTH_UNIFORM = "darkvisionStrength";
        private float currentStrength;

        public double RenderOrder => 0.5;
        public int RenderRange => int.MaxValue;

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            var player = clientAPI.World.Player?.Entity;
            float targetStrength = 0.0f;

            if (player is not null)
            {
                targetStrength = Math.Clamp(
                    player.Stats.GetBlended("darkvisionStrength") - 1.0f, 0.0f, 1.0f);
            }

            float fadeSeconds = targetStrength > currentStrength ? 2.0f : 0.6f;
            float blend = 1.0f - MathF.Exp(-deltaTime / fadeSeconds);
            currentStrength += (targetStrength - currentStrength) * blend;

            var shader = clientAPI.Shader.GetProgram((int)EnumShaderProgram.Chunkopaque);
            if (shader is null || shader.LoadError || !shader.HasUniform(STRENGTH_UNIFORM))
            {
                return;
            }

            var activeShader = clientAPI.Render.CurrentActiveShader;
            if (ReferenceEquals(activeShader, shader))
            {
                shader.Uniform(STRENGTH_UNIFORM, currentStrength);
            }
            else if (activeShader is null)
            {
                shader.Use();
                try
                {
                    shader.Uniform(STRENGTH_UNIFORM, currentStrength);
                }
                finally
                {
                    shader.Stop();
                }
            }
        }

        public void Dispose()
        {
            clientAPI.Event.UnregisterRenderer(this, EnumRenderStage.Before);
        }
    }
}
