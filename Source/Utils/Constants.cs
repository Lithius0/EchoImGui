using Unity.Profiling;

namespace EchoImGui
{
	internal static class Constants
	{
		public static readonly uint Version = (3 << 16) | (2 << 8) | (4);

		// TODO: Test all profile markers.
		internal static readonly ProfilerMarker PrepareFrameMarker = new ProfilerMarker("EchoImGui.PrepareFrame");
		internal static readonly ProfilerMarker LayoutMarker = new ProfilerMarker("EchoImGui.Layout");
		internal static readonly ProfilerMarker DrawListMarker = new ProfilerMarker("EchoImGui.RenderDrawLists");

		internal static readonly ProfilerMarker UpdateMeshMarker = new ProfilerMarker("EchoImGui.RendererMesh.UpdateMesh");
		internal static readonly ProfilerMarker CreateDrawCommandsMarker = new ProfilerMarker("EchoImGui.RendererMesh.CreateDrawCommands");

		internal static readonly ProfilerMarker UpdateBuffersMarker = new ProfilerMarker("EchoImGui.RendererProcedural.UpdateBuffers");
		internal static readonly ProfilerMarker CreateDrawComandsMarker = new ProfilerMarker("EchoImGui.RendererProcedural.CreateDrawCommands");

		internal static readonly string ExecuteDrawCommandsMarker = "EchoImGui.ExecuteDrawCommands";
	}
}