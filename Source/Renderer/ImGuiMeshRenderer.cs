using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using ImGuiNET;
using System.Runtime.InteropServices;
using System;
using UImGui.Renderer;
using UImGui.Texture;
using System.Collections.Generic;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using UnityEngine.Assertions;
using UImGui.Assets;

namespace UImGui
{
    public class ImGuiMeshRenderer : ScriptableRendererFeature
    {
        class ImGuiMeshRenderPass : ScriptableRenderPass
        {
            public Mesh mesh;
            public Material material;
            public MaterialPropertyBlock materialPropertyBlock;
            public ImDrawDataPtr drawData;
            public int textureId;

            // This class stores the data needed by the RenderGraph pass.
            // It is passed as a parameter to the delegate function that executes the RenderGraph pass.
            private class PassData
            {
                public Mesh mesh;
                public Material material;
                public MaterialPropertyBlock materialPropertyBlock;
                public ImDrawDataPtr drawData;
                public int textureId;
            }

            // This static method is passed as the RenderFunc delegate to the RenderGraph render pass.
            // It is used to execute draw commands.
            static void ExecutePass(PassData data, RasterGraphContext context)
            {
                ImDrawDataPtr drawData = data.drawData;
                var commandBuffer = context.cmd;
                Vector2 fbSize = drawData.DisplaySize * drawData.FramebufferScale;
                TextureManager textureManager = ImGuiController.Instance.TextureManager;

                IntPtr prevTextureId = IntPtr.Zero;
                Vector4 clipOffset = new Vector4(drawData.DisplayPos.x, drawData.DisplayPos.y,
                    drawData.DisplayPos.x, drawData.DisplayPos.y);
                Vector4 clipScale = new Vector4(drawData.FramebufferScale.x, drawData.FramebufferScale.y,
                    drawData.FramebufferScale.x, drawData.FramebufferScale.y);

                commandBuffer.SetViewport(new Rect(0f, 0f, fbSize.x, fbSize.y));
                commandBuffer.SetViewProjectionMatrices(
                    Matrix4x4.Translate(new Vector3(0.5f / fbSize.x, 0.5f / fbSize.y, 0f)), // Small adjustment to improve text.
                    Matrix4x4.Ortho(0f, fbSize.x, fbSize.y, 0f, 0f, 1f));

                int subOf = 0;
                for (int n = 0, nMax = drawData.CmdListsCount; n < nMax; ++n)
                {
                    ImDrawListPtr drawList = drawData.CmdLists[n];
                    for (int i = 0, iMax = drawList.CmdBuffer.Size; i < iMax; ++i, ++subOf)
                    {
                        ImDrawCmdPtr drawCmd = drawList.CmdBuffer[i];
                        if (drawCmd.UserCallback != IntPtr.Zero)
                        {
                            UserDrawCallback userDrawCallback = Marshal.GetDelegateForFunctionPointer<UserDrawCallback>(drawCmd.UserCallback);
                            userDrawCallback(drawList, drawCmd);
                        }
                        else
                        {
                            // Project scissor rectangle into framebuffer space and skip if fully outside.
                            Vector4 clipSize = drawCmd.ClipRect - clipOffset;
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

                            commandBuffer.EnableScissorRect(new Rect(clip.x, fbSize.y - clip.w, clip.z - clip.x, clip.w - clip.y)); // Invert y.
                            commandBuffer.DrawMesh(data.mesh, Matrix4x4.identity, data.material, subOf, -1, data.materialPropertyBlock);
                        }
                    }
                }
                commandBuffer.DisableScissorRect();
            }

            // RecordRenderGraph is where the RenderGraph handle can be accessed, through which render passes can be added to the graph.
            // FrameData is a context container through which URP resources can be accessed and managed.
            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                const string passName = "ImGui Render Pass";

                // Create a RenderGraph pass
                using (var builder = renderGraph.AddRasterRenderPass<PassData>(passName, out var passData))
                {
                    passData.drawData = drawData;
                    passData.mesh = mesh;
                    passData.material = material;
                    passData.materialPropertyBlock = materialPropertyBlock;
                    passData.drawData = drawData;
                    passData.textureId = textureId;

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

        private ImGuiMeshRenderPass renderPass;
        private Mesh mesh;

        private int _prevSubMeshCount = 1;  // number of sub meshes used previously

        // Skip all checks and validation when updating the mesh.
        private const MeshUpdateFlags NoMeshChecks = MeshUpdateFlags.DontNotifyMeshUsers |
            MeshUpdateFlags.DontRecalculateBounds |
            MeshUpdateFlags.DontResetBoneBounds |
            MeshUpdateFlags.DontValidateIndices;

        // Color sent with TexCoord1 semantics because otherwise Color attribute would be reordered to come before UVs.
        private static readonly VertexAttributeDescriptor[] _vertexAttributes = new[]
        {
        new VertexAttributeDescriptor(VertexAttribute.Position , VertexAttributeFormat.Float32, 2), // Position.
		new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2), // UV.
		new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.UInt32 , 1), // Color.
	};

        /// <inheritdoc/>
        public override void Create()
        {
            if (mesh == null)
            {
                mesh = new Mesh()
                {
                    name = "DearImGui Mesh"
                };
                mesh.MarkDynamic();
            }

            renderPass = new()
            {
                mesh = mesh,
                material = material,
                materialPropertyBlock = new MaterialPropertyBlock(),
                renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing,
                textureId = Shader.PropertyToID("_Texture"), // TODO: Have this be set somewhere else
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

            UpdateMesh(drawData);
            renderPass.drawData = drawData;
            renderer.EnqueuePass(renderPass);
        }

        private void UpdateMesh(ImDrawDataPtr drawData)
        {
            // Number of submeshes is the same as the nr of ImDrawCmd.
            int subMeshCount = 0;
            for (int n = 0, nMax = drawData.CmdListsCount; n < nMax; ++n)
            {
                subMeshCount += drawData.CmdLists[n].CmdBuffer.Size;
            }

            if (_prevSubMeshCount != subMeshCount)
            {
                // Occasionally crashes when changing subMeshCount without clearing first.
                mesh.Clear(true);
                mesh.subMeshCount = _prevSubMeshCount = subMeshCount;
            }
            mesh.SetVertexBufferParams(drawData.TotalVtxCount, _vertexAttributes);
            mesh.SetIndexBufferParams(drawData.TotalIdxCount, IndexFormat.UInt16);

            //  Upload data into mesh.
            int vtxOf = 0;
            int idxOf = 0;
            List<SubMeshDescriptor> descriptors = new List<SubMeshDescriptor>();

            for (int n = 0, nMax = drawData.CmdListsCount; n < nMax; ++n)
            {
                ImDrawListPtr drawList = drawData.CmdLists[n];

                unsafe
                {
                    // TODO: Convert NativeArray to C# array or list (remove collections).
                    NativeArray<ImDrawVert> vtxArray = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<ImDrawVert>(
                        (void*)drawList.VtxBuffer.Data, drawList.VtxBuffer.Size, Allocator.None);
                    NativeArray<ushort> idxArray = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<ushort>(
                        (void*)drawList.IdxBuffer.Data, drawList.IdxBuffer.Size, Allocator.None);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                    NativeArrayUnsafeUtility
                        .SetAtomicSafetyHandle(ref vtxArray, AtomicSafetyHandle.GetTempMemoryHandle());
                    NativeArrayUnsafeUtility
                        .SetAtomicSafetyHandle(ref idxArray, AtomicSafetyHandle.GetTempMemoryHandle());
#endif
                    // Upload vertex/index data.
                    mesh.SetVertexBufferData(vtxArray, 0, vtxOf, vtxArray.Length, 0, NoMeshChecks);
                    mesh.SetIndexBufferData(idxArray, 0, idxOf, idxArray.Length, NoMeshChecks);

                    // Define subMeshes.
                    for (int i = 0, iMax = drawList.CmdBuffer.Size; i < iMax; ++i)
                    {
                        ImDrawCmdPtr cmd = drawList.CmdBuffer[i];
                        SubMeshDescriptor descriptor = new SubMeshDescriptor
                        {
                            topology = MeshTopology.Triangles,
                            indexStart = idxOf + (int)cmd.IdxOffset,
                            indexCount = (int)cmd.ElemCount,
                            baseVertex = vtxOf + (int)cmd.VtxOffset,
                        };
                        descriptors.Add(descriptor);
                    }

                    vtxOf += vtxArray.Length;
                    idxOf += idxArray.Length;
                }
            }

            mesh.SetSubMeshes(descriptors, NoMeshChecks);
            mesh.UploadMeshData(false);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(mesh);
        }
    }
}