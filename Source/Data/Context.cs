using System;
using EchoImGui.Texture;

namespace EchoImGui
{
	internal sealed class Context
	{
		public IntPtr ImGuiContext;
		public IntPtr ImNodesContext;
		public IntPtr ImPlotContext;
		public TextureManager TextureManager;
	}
}