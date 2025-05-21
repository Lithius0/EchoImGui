using ImGuiNET;
using System;
using EchoImGui.Assets;
using EchoImGui.Events;
using EchoImGui.Platform;
using EchoImGui.Texture;
using UnityEngine;
using ImPlotNET;

namespace EchoImGui
{
    /// <summary>
    /// This class handles the context and runs the main update loop for Dear ImGui.
    /// It is independent of the rendering loop.
    /// </summary>
    public class ImGuiController : MonoBehaviour
    {
        public static ImGuiController Instance => _instance;
        private static ImGuiController _instance;

        /// <summary>
        /// If true, Dear ImGui has been initialized and is ready to go.
        /// </summary>
        public static bool Active => _instance != null && ImGui.GetCurrentContext() != IntPtr.Zero;

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

        private IntPtr imGuiContext;
        private IntPtr imPlotContext;

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
            }
            else
            {
                Debug.LogWarning("Duplicate ImGuiController objects!");
                Destroy(this);
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void Update()
        {
            ImGui.SetCurrentContext(imGuiContext);
            ImPlot.SetCurrentContext(imPlotContext);

            ImGuiIOPtr io = ImGui.GetIO();

            Constants.PrepareFrameMarker.Begin(this);
            textureManager.PrepareFrame(io);
            _platform.PrepareFrame(io);

            // Time.unscaledDeltaTime can be 0 in rare occasions. For example, when using the Frame Debugger.
            io.DeltaTime = Mathf.Max(Time.unscaledDeltaTime, 0.001f);
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
                // C++ asserts can't be caught in C# and will crash the editor. 
                io.ConfigErrorRecoveryEnableAssert = false;
                ImGui.Render();
                // Turn off asserts only for user code.
                // For EchoImGui's code, fail fast so it can be fixed.
                io.ConfigErrorRecoveryEnableAssert = true;
                Constants.LayoutMarker.End();
            }
        }

        private void OnEnable()
        {
            imGuiContext = ImGui.CreateContext();
            imPlotContext = ImPlot.CreateContext();
            // This is needed because Dear ImGui and ImPlot are in separate dlls and do not share globals.
            // Might be worth having a single dll at some point the dll boundary causes a lot of issues.
            ImPlot.SetImGuiContext(imGuiContext);

            ImGuiIOPtr io = ImGui.GetIO();

            // Supports ImDrawCmd::VtxOffset to output large meshes while still using 16-bits indices.
            io.BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset;

            // C++ exceptions will crash the editor and may cause user to lose unsaved changes.
            // Try to have some elegant recovery so that things don't just break.
            io.ConfigErrorRecovery = true;
            
            textureManager = new TextureManager();
            textureManager.BuildFontAtlas(io, fontAtlasConfiguration, fontCustomInitializer);
            textureManager.Initialize(io);

            _initialConfiguration.ApplyTo(io);
            _style?.ApplyTo(ImGui.GetStyle());

            IPlatform platform = PlatformUtility.Create(_platformType, _cursorShapes, _iniSettings);
            SetPlatform(platform, io);
        }

        private void OnDisable()
        {
            // It's not guaranteed that OnDisable will be called after OnEnable e.g. if the object is destroyed before it is even enabled.
            // The IntPtr.Zero checks prevent a hard crash from ImGui destroying a null pointer.
            if (imGuiContext != IntPtr.Zero)
            {
                ImGui.DestroyContext(imGuiContext);
                textureManager.Shutdown();
            }
            if (imPlotContext != IntPtr.Zero)
            {
                ImPlot.DestroyContext(imPlotContext);
            }
        }

        private void SetPlatform(IPlatform platform, ImGuiIOPtr io)
        {
            _platform?.Shutdown(io);
            _platform = platform;
            _platform?.Initialize(io, _initialConfiguration, "Unity " + _platformType.ToString());
        }
    }
}