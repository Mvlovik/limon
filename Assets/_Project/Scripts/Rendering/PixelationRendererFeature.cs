using GameLimon.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace GameLimon.Rendering
{
    /// <summary>
    /// Full-screen pixelation post-process. Reads its settings from
    /// <see cref="PixelationSettings"/>, which lives on the persistent GameRoot.
    /// </summary>
    public sealed class PixelationRendererFeature : ScriptableRendererFeature
    {
        private const string ShaderName = "Hidden/GameLimon/Pixelation";

        [SerializeField] private Shader pixelationShader;

        private Material _material;
        private PixelationPass _pass;

        public override void Create()
        {
            EnsureMaterial();

            _pass ??= new PixelationPass();
            _pass.renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
            _pass.SetMaterial(_material);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            EnsureMaterial();

            if (_material == null || _pass == null)
                return;

            if (!PixelationSettings.Enabled)
                return;

            float pixelSize = PixelationSettings.PixelSize;
            if (pixelSize < 2f)
                return;

            CameraType cameraType = renderingData.cameraData.cameraType;
            if (cameraType == CameraType.Preview || cameraType == CameraType.Reflection)
                return;

            _pass.Setup(pixelSize);
            renderer.EnqueuePass(_pass);
        }

        private void EnsureMaterial()
        {
            if (_material != null)
                return;

            Shader shader = pixelationShader != null ? pixelationShader : Shader.Find(ShaderName);
            if (shader == null)
                return;

            _material = CoreUtils.CreateEngineMaterial(shader);
            _material.hideFlags = HideFlags.HideAndDontSave;

            _pass?.SetMaterial(_material);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(_material);
            _material = null;
        }

        private sealed class PixelationPass : ScriptableRenderPass
        {
            private const int ShaderPassIndex = 0;

            private static readonly int PixelSizeId = Shader.PropertyToID("_PixelationSize");

            private readonly MaterialPropertyBlock _propertyBlock = new MaterialPropertyBlock();

            private Material _material;
            private float _pixelSize = 4f;

            public PixelationPass()
            {
                profilingSampler = new ProfilingSampler("Pixelation");
            }

            public void SetMaterial(Material material)
            {
                _material = material;
            }

            public void Setup(float pixelSize)
            {
                _pixelSize = pixelSize;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (_material == null)
                    return;

                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                if (!resourceData.cameraColor.IsValid())
                    return;

                TextureHandle source = resourceData.cameraColor;
                TextureHandle destination = resourceData.activeColorTexture;

                // Copy the camera color first: the pixelation pass cannot read and
                // write the same attachment inside a single RenderGraph pass.
                TextureDesc desc = renderGraph.GetTextureDesc(source);
                desc.name = "_PixelationSource";
                desc.clearBuffer = false;
                TextureHandle copy = renderGraph.CreateTexture(desc);

                renderGraph.AddBlitPass(source, copy, Vector2.one, Vector2.zero, passName: "Pixelation Copy");

                _propertyBlock.Clear();
                _propertyBlock.SetFloat(PixelSizeId, Mathf.Max(1f, _pixelSize));

                var parameters = new RenderGraphUtils.BlitMaterialParameters(
                    copy, destination, _material, ShaderPassIndex, _propertyBlock)
                {
                    // Use the procedural full-screen triangle that the Blit.hlsl
                    // "Vert" vertex shader expects. The default Mesh geometry needs
                    // Blitter's internal quad mesh, which is not always initialized.
                    geometry = RenderGraphUtils.FullScreenGeometryType.ProceduralTriangle
                };

                renderGraph.AddBlitPass(parameters, passName: "Pixelation");
            }
        }
    }
}
