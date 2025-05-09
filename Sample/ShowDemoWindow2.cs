using ImGuiNET;
#if !UIMGUI_REMOVE_IMNODES
using imnodesNET;
#endif
#if !UIMGUI_REMOVE_IMPLOT
using ImPlotNET;
using System.Linq;
#endif
#if !UIMGUI_REMOVE_IMGUIZMO
using ImGuizmoNET;
#endif
using UnityEngine;

namespace UImGui
{
    public class ShowDemoWindow2 : MonoBehaviour
    {
#if !UIMGUI_REMOVE_IMPLOT
        [SerializeField]
        float[] _barValues = Enumerable.Range(1, 10).Select(x => (x * x) * 1.0f).ToArray();
        [SerializeField]
        float[] _xValues = Enumerable.Range(1, 10).Select(x => (x * x) * 1.0f).ToArray();
        [SerializeField]
        float[] _yValues = Enumerable.Range(1, 10).Select(x => (x * x) * 1.0f).ToArray();
#endif

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
#if !UIMGUI_REMOVE_IMPLOT
            if (ImGui.Begin("Plot Window Sample"))
            {
                ImGui.SetNextWindowSize(Vector2.one * 200, ImGuiCond.Once);
                ImPlot.BeginPlot("Plot test");
                ImPlot.PlotBars("My Bar Plot", ref _barValues[0], _barValues.Length + 1);
                ImPlot.PlotLine("My Line Plot", ref _xValues[0], ref _yValues[0], _xValues.Length, 0, 0);
                ImPlot.EndPlot();

                ImGui.End();
            }
#endif

            ImGui.ShowDemoWindow();
        }
    }
}

