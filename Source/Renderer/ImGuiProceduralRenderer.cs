using ImGuiNET;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System;
using EchoImGui.Renderer;
using EchoImGui.Texture;
using EchoImGui;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;
using UnityEngine.Assertions;

namespace EchoImGui.Renderer
{
    public class ImGuiProceduralRenderer : ScriptableRendererFeature
    {
        class ImGuiProceduralRenderPass : ScriptableRenderPass
        {
            public Material material;
            public MaterialPropertyBlock materialPropertyBlock;
            public ImDrawDataPtr drawData;
            public int textureId;
            public int verticesId;
            public int baseVertexId;

            public ComputeBuffer vertexBuffer; // GPU buffer for vertex data.
            public GraphicsBuffer indexBuffer; // GPU buffer for indexes.
            public ComputeBuffer argumentsBuffer; // GPU buffer for draw arguments.

            // This class stores the data needed by the RenderGraph pass.
            // It is passed as a parameter to the delegate function that executes the RenderGraph pass.
            private class PassData
            {
                public Material material;
                public MaterialPropertyBlock materialPropertyBlock;
                public ImDrawDataPtr drawData;
                public int textureId;
                public int verticesId;
                public int baseVertexId;

                public ComputeBuffer vertexBuffer; // GPU buffer for vertex data.
                public GraphicsBuffer indexBuffer; // GPU buffer for indexes.
                public ComputeBuffer argumentsBuffer; // GPU buffer for draw arguments.
            }

            // This static method is passed as the RenderFunc delegate to the RenderGraph render pass.
            // It is used to execute draw commands.
            static void ExecutePass(PassData data, RasterGraphContext context)
            {
                var drawData = data.drawData;
                var cmd = context.cmd;
                var material = data.material;
                Vector2 fbSize = drawData.DisplaySize * drawData.FramebufferScale;
                TextureManager textureManager = ImGuiController.Instance.TextureManager;

                IntPtr prevTextureId = IntPtr.Zero;
                Vector4 clipOffst = new Vector4(drawData.DisplayPos.x, drawData.DisplayPos.y,
                    drawData.DisplayPos.x, drawData.DisplayPos.y);
                Vector4 clipScale = new Vector4(drawData.FramebufferScale.x, drawData.FramebufferScale.y,
                    drawData.FramebufferScale.x, drawData.FramebufferScale.y);

                material.SetBuffer(data.verticesId, data.vertexBuffer); // Bind vertex buffer.

                cmd.SetViewport(new Rect(0f, 0f, fbSize.x, fbSize.y));
                cmd.SetViewProjectionMatrices(
                    Matrix4x4.Translate(new Vector3(0.5f / fbSize.x, 0.5f / fbSize.y, 0f)), // Small adjustment to improve text.
                    Matrix4x4.Ortho(0f, fbSize.x, fbSize.y, 0f, 0f, 1f));

                int vtxOf = 0;
                int argOf = 0;
                for (int commandListIndex = 0, nMax = drawData.CmdListsCount; commandListIndex < nMax; ++commandListIndex)
                {
                    ImDrawListPtr drawList = drawData.CmdLists[commandListIndex];
                    for (int commandIndex = 0, iMax = drawList.CmdBuffer.Size; commandIndex < iMax; ++commandIndex, argOf += 5 * 4)
                    {
                        ImDrawCmdPtr drawCmd = drawList.CmdBuffer[commandIndex];
                        if (drawCmd.UserCallback != IntPtr.Zero)
                        {
                            UserDrawCallback userDrawCallback = Marshal.GetDelegateForFunctionPointer<UserDrawCallback>(drawCmd.UserCallback);
                            userDrawCallback(drawList, drawCmd);
                        }
                        else
                        {
                            // Project scissor rectangle into framebuffer space and skip if fully outside.
                            Vector4 clipSize = drawCmd.ClipRect - clipOffst;
                            Vector4 clip = Vector4.Scale(clipSize, clipScale);

                            if (clip.x >= fbSize.x || clip.y >= fbSize.y || clip.z < 0f || clip.w < 0f) continue;

                            if (prevTextureId != drawCmd.TextureId)
                            {
                                prevTextureId = drawCmd.TextureId;

                                // TODO: Implement ImDrawCmdPtr.GetTexID().
                                bool hasTexture = textureManager.TryGetTexture(prevTextureId, out UnityEngine.Texture texture);
                                Assert.IsTrue(hasTexture, $"Texture {prevTextureId} does not exist. Try to use UImGuiUtility.GetTextureID().");

                                data.materialPropertyBlock.SetTexture(data.textureId, texture);
                            }

                            // Base vertex location not automatically added to SV_VertexID.
                            data.materialPropertyBlock.SetInt(data.baseVertexId, vtxOf + (int)drawCmd.VtxOffset);

                            cmd.EnableScissorRect(new Rect(clip.x, fbSize.y - clip.w, clip.z - clip.x, clip.w - clip.y)); // Invert y.
                            cmd.DrawProceduralIndirect(data.indexBuffer, Matrix4x4.identity, data.material, -1,
                                MeshTopology.Triangles, data.argumentsBuffer, argOf, data.materialPropertyBlock);
                        }
                    }
                    vtxOf += drawList.VtxBuffer.Size;
                }
                cmd.DisableScissorRect();
            }

            // RecordRenderGraph is where the RenderGraph handle can be accessed, through which render passes can be added to the graph.
            // FrameData is a context container through which URP resources can be accessed and managed.
            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                const string passName = "ImGui Render Pass";

                // Create a RenderGraph pass
                using (var builder = renderGraph.AddRasterRenderPass<PassData>(passName, out var passData))
                {
                    passData.material = material;
                    passData.materialPropertyBlock = materialPropertyBlock;
                    passData.drawData = drawData;
                    passData.textureId = textureId;
                    passData.verticesId = verticesId;
                    passData.baseVertexId = baseVertexId;

                    passData.vertexBuffer = vertexBuffer;
                    passData.indexBuffer = indexBuffer;
                    passData.argumentsBuffer = argumentsBuffer;

                    UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                    builder.SetRenderAttachment(resourceData.activeColorTexture, 0);
                    builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.Write);

                    // Assigns the ExecutePass function to the render pass delegate. This will be called by the render graph when executing the pass.
                    builder.SetRenderFunc((PassData data, RasterGraphContext context) => ExecutePass(data, context));
                }
            }
        }

        [SerializeField]
        private Material material;
        [SerializeField]
        private string textureProperty = "_Texture";
        [SerializeField]
        private string verticesProperty = "_Vertices";
        [SerializeField]
        private string baseVertexProperty = "_BaseVertex";

        private ImGuiProceduralRenderPass renderPass;

        private ComputeBuffer vertexBuffer; // GPU buffer for vertex data.
        private GraphicsBuffer indexBuffer; // GPU buffer for indexes.
        private ComputeBuffer argumentsBuffer; // GPU buffer for draw arguments.

        /// <inheritdoc/>
        public override void Create()
        {
            renderPass = new()
            {
                material = material,
                materialPropertyBlock = new MaterialPropertyBlock(),
                renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing,
                textureId = Shader.PropertyToID(textureProperty),
                verticesId = Shader.PropertyToID(verticesProperty),
                baseVertexId = Shader.PropertyToID(baseVertexProperty),

                vertexBuffer = vertexBuffer,
                indexBuffer = indexBuffer,
                argumentsBuffer = argumentsBuffer,
            };
        }

        // Here you can inject one or multiple render passes in the renderer.
        // This method is called when setting up the renderer once per-camera.
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (ImGuiController.Instance == null)
                return;

            var drawData = ImGui.GetDrawData();
            Vector2 fbOSize = drawData.DisplaySize * drawData.FramebufferScale;
            // Avoid rendering when minimized.
            if (fbOSize.x <= 0f || fbOSize.y <= 0f || drawData.TotalVtxCount == 0)
                return;

            UpdateBuffers(drawData);
            // TODO: Probably don't need to be passing the buffers every frame
            renderPass.vertexBuffer = vertexBuffer;
            renderPass.indexBuffer = indexBuffer;
            renderPass.argumentsBuffer = argumentsBuffer;
            renderPass.drawData = drawData;
            renderer.EnqueuePass(renderPass);
        }

        private unsafe void UpdateBuffers(ImDrawDataPtr drawData)
        {
            int drawArgCount = 0; // nr of drawArgs is the same as the nr of ImDrawCmd
            for (int n = 0, nMax = drawData.CmdListsCount; n < nMax; ++n)
            {
                drawArgCount += drawData.CmdLists[n].CmdBuffer.Size;
            }

            // create or resize vertex/index buffers
            if (vertexBuffer == null || vertexBuffer.count < drawData.TotalVtxCount)
            {
                CreateOrResizeVtxBuffer(ref vertexBuffer, drawData.TotalVtxCount);
            }

            if (indexBuffer == null || indexBuffer.count < drawData.TotalIdxCount)
            {
                CreateOrResizeIdxBuffer(ref indexBuffer, drawData.TotalIdxCount);
            }

            if (argumentsBuffer == null || argumentsBuffer.count < drawArgCount * 5)
            {
                CreateOrResizeArgBuffer(ref argumentsBuffer, drawArgCount * 5);
            }

            // upload vertex/index data into buffers
            int vtxOf = 0;
            int idxOf = 0;
            int argOf = 0;
            for (int n = 0, nMax = drawData.CmdListsCount; n < nMax; ++n)
            {
                ImDrawListPtr drawList = drawData.CmdLists[n];
                NativeArray<ImDrawVert> vtxArray = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<ImDrawVert>(
                    (void*)drawList.VtxBuffer.Data, drawList.VtxBuffer.Size, Allocator.None);
                NativeArray<ushort> idxArray = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<ushort>(
                    (void*)drawList.IdxBuffer.Data, drawList.IdxBuffer.Size, Allocator.None);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref vtxArray, AtomicSafetyHandle.GetTempMemoryHandle());
                NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref idxArray, AtomicSafetyHandle.GetTempMemoryHandle());
#endif
                // Upload vertex/index data.
                vertexBuffer.SetData(vtxArray, 0, vtxOf, vtxArray.Length);
                indexBuffer.SetData(idxArray, 0, idxOf, idxArray.Length);

                // Arguments for indexed draw.
                for (int meshIndex = 0, iMax = drawList.CmdBuffer.Size; meshIndex < iMax; ++meshIndex)
                {
                    ImDrawCmdPtr cmd = drawList.CmdBuffer[meshIndex];
                    var drawArgs = new int[]
                    {
                        (int)cmd.ElemCount,
                        1,
                        idxOf + (int)cmd.IdxOffset,
                        vtxOf,
                        0
                    };
                    argumentsBuffer.SetData(drawArgs, 0, argOf, 5);
                    argOf += 5; // 5 int for each command.
                }
                vtxOf += vtxArray.Length;
                idxOf += idxArray.Length;
            }
        }
        private void CreateOrResizeVtxBuffer(ref ComputeBuffer buffer, int count)
        {
            buffer?.Release();

            unsafe
            {
                int num = (((count - 1) / 256) + 1) * 256;
                buffer = new ComputeBuffer(num, sizeof(ImDrawVert));
            }
        }

        private void CreateOrResizeIdxBuffer(ref GraphicsBuffer buffer, int count)
        {
            buffer?.Release();

            unsafe
            {
                int num = (((count - 1) / 256) + 1) * 256;
                buffer = new GraphicsBuffer(GraphicsBuffer.Target.Index, num, sizeof(ushort));
            }
        }

        private void CreateOrResizeArgBuffer(ref ComputeBuffer buffer, int count)
        {
            buffer?.Release();
            unsafe
            {
                int num = (((count - 1) / 256) + 1) * 256;
                buffer = new ComputeBuffer(num, sizeof(int), ComputeBufferType.IndirectArguments);
            }
        }

        protected override void Dispose(bool disposing)
        {
            vertexBuffer?.Release(); 
            vertexBuffer = null;
            indexBuffer?.Release(); 
            indexBuffer = null;
            argumentsBuffer?.Release(); 
            argumentsBuffer = null;
        }
    }
}
