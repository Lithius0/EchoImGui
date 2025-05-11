# EchoImGui
EchoImGui is a Unity package that provides a wrapper for the [Dear ImGui](https://github.com/ocornut/imgui) [ImPlot](https://github.com/epezent/implot) libraries. This project is built on [ImGui.NET](https://github.com/ImGuiNET/ImGui.NET) and was originally a fork of [UImGui](https://github.com/psydack/uimgui). 

This package is intended for use as a library for quickly writing debug menus for developer tools. It is not intended as a general purpose UI library.

## Compatibility
This package should work with Unity 6000.0+ using the Universal Rendering Pipeline. **Currently, the project will not work for built-in or HDRP**. 
Not likely to work on Mac or Linux, I only have a Windows machine to test with. If you have a Mac or Linux and would like to help with testing, feel free to reach out.

This package is currently tailored for my own uses. I cannot guarantee support for anything beyond what I need the package for. 

## Differences from UImGui
There are some pretty major changes from UImGui that resulted in this being split off as an separate project. Here's a quick list of the user-facing changes:
- Incorporation of ImPlot as a core dependency rather than being optional.
- Updated DearImGui and Implot dlls.
- Rewritten controller script (UImGui.cs -> ImGuiController.cs).
- Migration to the RenderGraph API for URP.
- No support for ImNodes or ImGuizmo (at least for now).

----

## What is Dear ImGui?

> Dear ImGui is a **bloat-free graphical user interface library for C++**. It outputs optimized vertex buffers that you can render anytime in your 3D-pipeline enabled application. It is fast, portable, renderer agnostic and self-contained (no external dependencies).
> 
> Dear ImGui is designed to **enable fast iterations** and to **empower programmers** to create **content creation tools and visualization / debug tools** (as opposed to UI for the average end-user). It favors simplicity and productivity toward this goal, and lacks certain features normally found in more high-level libraries.

Setup
-------
### URP:
1. Add either the `ImGuiMeshRenderer` or `ImGuiProceduralRenderer` Renderer Feature to your Renderer (there are functionally equivalent). If you've just created a URP project, it should be called "PC_Renderer" or something to that effect.
1. Make sure the Material field in the Renderer Feature isn't empty. If you're using a custom shader assign a custom material. Otherwise, there are default materials in the EchoImGui/Resources tab. 

Usage
-------
- [Add package](https://docs.unity3d.com/Manual/upm-ui-giturl.html) from git URL: https://github.com/Lithius0/EchoImGui.git or save the source into a folder and use "Install package from disk" in the Package Manager.
- Add `ImGuiController` component to all scenes where you want the GUI to be visible.
- (Optional) Add `DemoWindow` component for a demo of what Dear ImGui has to offer.
- (Optional) Set `Platform Type` to `Input System` if you're using the new [input system](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.0/manual/index.html)

From here you can add your own components by subscribing to `ImGuiController.OnLayout` event like so:

```cs
public class Example : MonoBehaviour
{
	[SerializeField]
	private float _sliderFloatValue = 1;

	[SerializeField]
	private string _inputText;

	private void OnEnable()
	{
		ImGuiController.OnLayout += OnLayout;
	}

	private void OnDisable()
	{
		ImGuiController.OnLayout -= OnLayout;
	}

	private void OnLayout()
	{
		ImGui.Text($"Hello, world {123}");
		if (ImGui.Button("Save"))
		{
			Debug.Log("Save");
		}

		ImGui.InputText("string", ref _inputText, 100);
		ImGui.SliderFloat("float", ref _sliderFloatValue, 0.0f, 1.0f);
	}
}
```

## Using EchoImGui
EchoImGui is basically just a way to use Dear ImGui in Unity at the end of the day, so here's some resources for using Dear ImGui in general:

- **[Dear ImGui](https://github.com/ocornut/imgui)**:
If it feels like there's essentially no documentation on any of the methods, that's because there isn't. ImGui.NET is primarily auto-generated and the method documentation is all in the C++ version.
If you're unsure about what a parameter is or what a method does, it can help to open the files in that repo and find the method you're looking for. 
There's a good chance there's documentation describing what you need.

- **[ImGui Manual](https://pthom.github.io/imgui_manual_online/manual/imgui_manual.html)**:
This is very useful for seeing what all ImGui has to offer and seeing the accompanying sample code. The quickest way to learn Dear ImGui is copying someone's example.

- **[ImPlot Demo](https://traineq.org/implot_demo/src/implot_demo.html)**:
This is the ImPlot equivalent to the ImGui manual.