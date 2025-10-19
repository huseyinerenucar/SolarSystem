using System;
using Unified.UniversalBlur.Runtime.CommandBuffer;
using Unified.UniversalBlur.Runtime.PassData;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

#if UNITY_6000_0_OR_NEWER
using UnityEngine.Rendering.RenderGraphModule;
#endif

namespace Unified.UniversalBlur.Runtime
{
    internal class UniversalBlurPass : ScriptableRenderPass, IDisposable
    {
        private const string k_BlitPassName = "Copy Blur To Texture";
        private const string k_PassName = "Universal Blur";
        private const string k_BlurTextureSourceName = k_PassName + " - Blur Source";
        private const string k_BlurTextureDestinationName = k_PassName + " - Blur Destination";

        private readonly ProfilingSampler _profilingSampler;
        private readonly MaterialPropertyBlock _propertyBlock;

        private BlurConfig _blurConfig;

        private RTHandle m_TargetRTH;
        private RenderTexture m_CachedTarget;

        public UniversalBlurPass()
        {
            _profilingSampler = new(k_PassName);
            _propertyBlock = new();
        }

        public void Setup(BlurConfig blurConfig)
        {
            _blurConfig = blurConfig;

            if (_blurConfig.TargetRenderTexture != null)
            {
                // If the user assigns a new texture or if we haven't created the handle yet
                if (m_CachedTarget != _blurConfig.TargetRenderTexture || m_TargetRTH == null)
                {
                    m_TargetRTH?.Release(); // Release the old one if it exists
                    m_TargetRTH = RTHandles.Alloc(_blurConfig.TargetRenderTexture);
                    m_CachedTarget = _blurConfig.TargetRenderTexture;
                }
            }
            else // If the user un-assigns the texture
            {
                m_TargetRTH?.Release();
                m_TargetRTH = null;
                m_CachedTarget = null;
            }
        }

        public void Dispose()
        {
            m_TargetRTH?.Release();
        }

        public void DrawDefaultTexture()
        {
            // For better preview experience in editor, we just use a gray texture
            Shader.SetGlobalTexture(Constants.GlobalFullScreenBlurTextureId, Texture2D.linearGrayTexture);
        }

        private RenderTextureDescriptor GetDescriptor() =>
            new(_blurConfig.Width, _blurConfig.Height, GraphicsFormat.B10G11R11_UFloatPack32, 0)
            {
                useMipMap = _blurConfig.EnableMipMaps,
                autoGenerateMips = _blurConfig.EnableMipMaps
            };

        // ----------------------------------------------
        // RenderGraph Path (Unity 6000+/URP 16+)
        // ----------------------------------------------
#if UNITY_6000_0_OR_NEWER

        private class BlitPassData
        {
            public TextureHandle source;
            public Material blitMaterial;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resourceData = frameData.Get<UniversalResourceData>();
            if (resourceData.isActiveTargetBackBuffer) return;

            TextureHandle targetRenderTextureHandle = default;
            if (m_TargetRTH != null)
            {
                targetRenderTextureHandle = renderGraph.ImportTexture(m_TargetRTH);
            }

            var descriptor = new TextureDesc(GetDescriptor()) { name = k_BlurTextureSourceName };
            TextureHandle source = renderGraph.CreateTexture(descriptor);
            descriptor.name = k_BlurTextureDestinationName;
            TextureHandle destination = renderGraph.CreateTexture(descriptor);

            TextureHandle finalResultHandle;
            using (var builder = renderGraph.AddUnsafePass<RenderGraphPassData>(k_PassName, out var passData, _profilingSampler))
            {
                passData.ColorSource = resourceData.activeColorTexture;
                passData.Source = source;
                passData.Destination = destination;
                passData.MaterialPropertyBlock = _propertyBlock;
                passData.BlurConfig = _blurConfig;

                builder.AllowPassCulling(false);
                builder.UseTexture(source, AccessFlags.ReadWrite);
                builder.UseTexture(destination, AccessFlags.ReadWrite);

                finalResultHandle = destination;
                builder.SetGlobalTextureAfterPass(finalResultHandle, Constants.GlobalFullScreenBlurTextureId);

                builder.SetRenderFunc<RenderGraphPassData>((data, ctx) =>
                {
                    BlurPasses.KawaseExecutePass(data, new WrappedUnsafeCommandBuffer(ctx.cmd));
                });
            }

            if (targetRenderTextureHandle.IsValid())
            {
                using (var builder = renderGraph.AddRasterRenderPass<BlitPassData>(k_BlitPassName, out var passData))
                {
                    passData.source = finalResultHandle;
                    builder.UseTexture(finalResultHandle, AccessFlags.Read);

                    passData.blitMaterial = _blurConfig.BlitMaterial;

                    // --- FIX: Use SetRenderAttachment to set the render target ---
                    builder.SetRenderAttachment(targetRenderTextureHandle, 0, AccessFlags.Write);

                    builder.SetRenderFunc((BlitPassData data, RasterGraphContext ctx) =>
                    {
                        var properties = ctx.renderGraphPool.GetTempMaterialPropertyBlock();
                        properties.SetTexture(Constants.BlitTextureId, data.source);

                        // --- FIX: Use DrawProcedural, the fundamental drawing command ---
                        ctx.cmd.DrawProcedural(Matrix4x4.identity, data.blitMaterial, 0, MeshTopology.Triangles, 3, 1, properties);
                    });
                }
            }
        }
#endif
    }
}
