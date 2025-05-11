using ImGuiNET;
using UnityEngine.Events;


namespace EchoImGui.Events
{
	[System.Serializable]
	public class FontInitializerEvent : UnityEvent<ImGuiIOPtr> { }
}