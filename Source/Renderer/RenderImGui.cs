using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.UIElements;
using ImGuiNET;
using static Codice.CM.WorkspaceServer.WorkspaceTreeDataStore;
using UImGui.Assets;

#if HAS_URP
using UnityEngine.Rendering.Universal;
using UnityEngine;
#endif

namespace UImGui.Renderer
{
#if HAS_URP
	public class RenderImGui : ScriptableRendererFeature
	{
		private class ImGuiRenderPass : ScriptableRenderPass
		{
			public IRenderer renderer;

			// This class stores the data needed by the RenderGraph pass.
			// It is passed as a parameter to the delegate function that executes the RenderGraph pass.
			private class PassData
			{
				public IRenderer renderer;
				public ImDrawDataPtr drawData;
			}

			static void ExecutePass(PassData data, RasterGraphContext context)
			{
				data.renderer.RenderDrawLists(context.cmd, data.drawData);
			}

			public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
			{ 
				// Create a RenderGraph pass
				using (var builder = renderGraph.AddRasterRenderPass<PassData>("Draw ImGui", out var passData))
				{
					passData.drawData = ImGui.GetDrawData();
					passData.renderer = renderer;

					UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
					builder.SetRenderAttachment(resourceData.activeColorTexture, 0);
					builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.Write);

					// Assigns the ExecutePass function to the render pass delegate. This will be called by the render graph when executing the pass.
					builder.SetRenderFunc((PassData data, RasterGraphContext context) => ExecutePass(data, context));
				}
			}
		}

		public RenderType RendererType = RenderType.Mesh;
		internal IRenderer Renderer;
		public RenderPassEvent RenderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;

		private ImGuiRenderPass imguiRenderPass;

		public override void Create()
		{
			imguiRenderPass = new ImGuiRenderPass()
			{
				renderPassEvent = RenderPassEvent,
				renderer = Renderer
			};
		}

		internal void SetRenderer(IRenderer renderer)
		{
			Renderer = renderer;
			if (imguiRenderPass != null)
			{
				imguiRenderPass.renderer = renderer;
			}
		}

		public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
		{
			if (imguiRenderPass.renderer == null)
			{
				return;
			}

			renderer.EnqueuePass(imguiRenderPass);
		}
	}
#else
	public class RenderImGui : UnityEngine.ScriptableObject
	{
		public CommandBuffer CommandBuffer;
	}
#endif
}
