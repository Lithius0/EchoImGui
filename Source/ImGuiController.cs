using ImGuiNET;
using System;
using EchoImGui.Assets;
using EchoImGui.Events;
using EchoImGui.Platform;
using EchoImGui.Texture;
using UnityEngine;

namespace EchoImGui
{
    public class ImGuiController : MonoBehaviour
    {
        public static ImGuiController Instance => _instance;
        private static ImGuiController _instance;
        public static event Action OnLayout;

        [SerializeField]
        private FontInitializerEvent fontCustomInitializer = new FontInitializerEvent();
        [SerializeField]
        private FontAtlasConfigAsset fontAtlasConfiguration = null;
        [SerializeField]
        private InputType _platformType = InputType.InputManager;

        [SerializeField]
        private UIOConfig _initialConfiguration = new UIOConfig
        {
            ImGuiConfig = ImGuiConfigFlags.NavEnableKeyboard | ImGuiConfigFlags.DockingEnable,

            DoubleClickTime = 0.30f,
            DoubleClickMaxDist = 6.0f,

            DragThreshold = 6.0f,

            KeyRepeatDelay = 0.250f,
            KeyRepeatRate = 0.050f,

            FontGlobalScale = 1.0f,
            FontAllowUserScaling = false,

            DisplayFramebufferScale = Vector2.one,

            MouseDrawCursor = false,
            TextCursorBlink = false,

            ResizeFromEdges = true,
            MoveFromTitleOnly = true,
            ConfigMemoryCompactTimer = 1f,
        };

        [Tooltip("Null value uses default imgui.ini file.")]
        [SerializeField]
        private IniSettingsAsset _iniSettings = null;

        private IPlatform _platform;

        [Header("Customization")]
        [SerializeField]
        private StyleAsset _style = null;

        [SerializeField]
        private CursorShapesAsset _cursorShapes = null;

        internal TextureManager TextureManager => textureManager;
        private TextureManager textureManager;

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                Setup();
            }
            else
            {
                Debug.LogWarning("Duplicate ImGuiController objects!");
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
                Shutdown();
            }
        }

        private void Update()
        {
            ImGuiIOPtr io = ImGui.GetIO();

            Constants.PrepareFrameMarker.Begin(this);
            textureManager.PrepareFrame(io);
            _platform.PrepareFrame(io);

            io.DisplaySize = new Vector2(Screen.width, Screen.height);

            ImGui.NewFrame();
            Constants.PrepareFrameMarker.End();

            Constants.LayoutMarker.Begin(this);
            try
            {
                OnLayout?.Invoke();
            }
            finally
            {
                ImGui.Render();
                Constants.LayoutMarker.End();
            }
        }

        private void Setup()
        {
            var imGuiContext = ImGui.CreateContext();
            var imPlotContext = ImPlotNET.ImPlot.CreateContext();
            ImPlotNET.ImPlot.SetImGuiContext(imGuiContext);

            ImGuiIOPtr io = ImGui.GetIO();

            textureManager = new TextureManager();
            textureManager.BuildFontAtlas(io, fontAtlasConfiguration, fontCustomInitializer);
            textureManager.Initialize(io);

            _initialConfiguration.ApplyTo(io);
            _style?.ApplyTo(ImGui.GetStyle());

            IPlatform platform = PlatformUtility.Create(_platformType, _cursorShapes, _iniSettings);
            SetPlatform(platform, io);
        }

        private void Shutdown()
        {
            ImGui.DestroyContext();
            ImPlotNET.ImPlot.DestroyContext();
        }

        private void SetPlatform(IPlatform platform, ImGuiIOPtr io)
        {
            _platform?.Shutdown(io);
            _platform = platform;
            _platform?.Initialize(io, _initialConfiguration, "Unity " + _platformType.ToString());
        }
    }
}