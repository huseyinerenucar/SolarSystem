using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class StarfieldRenderFeature : ScriptableRendererFeature
{
    public static readonly List<StarFieldRenderer> Instances = new();
    private StarfieldRenderPass starfieldPass;

    public override void Create()
    {
        starfieldPass = new StarfieldRenderPass
        {
            // We want to draw the stars after opaque objects and the skybox,
            // but before transparents and post-processing.
            renderPassEvent = RenderPassEvent.AfterRenderingSkybox
        };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        // Don't add the pass if there are no stars to render
        if (StarFieldRenderer.Instances.Count == 0)
        {
            return;
        }

        // We need the camera color texture to fade stars against the bright sky
        starfieldPass.ConfigureInput(ScriptableRenderPassInput.Color);
        renderer.EnqueuePass(starfieldPass);
    }

    class StarfieldRenderPass : ScriptableRenderPass
    {
        // This method is called by the renderer before executing the pass.
        // It can be used to configure render targets and cleaning behaviour.
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            // No special setup needed here, we're drawing to the camera's active target.
        }

        // The main rendering method.
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get("Starfield");

            // Get the camera's position for this frame
            Vector3 cameraPosition = renderingData.cameraData.camera.transform.position;

            // Iterate over all active StarFieldRenderer instances
            foreach (var starfield in StarFieldRenderer.Instances)
            {
                if (starfield.StarMesh != null && starfield.StarMaterial != null)
                {
                    // Create a matrix to draw the star mesh centered on the camera
                    var matrix = Matrix4x4.TRS(cameraPosition, Quaternion.identity, Vector3.one);

                    // Issue the draw call
                    cmd.DrawMesh(starfield.StarMesh, matrix, starfield.StarMaterial, 0, 0);
                }
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }
}