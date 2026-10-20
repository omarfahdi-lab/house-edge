using HouseEdge.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

namespace HouseEdge.Game
{
    // builds the whole game in code after the scene loads, nothing set up in the editor
    public sealed class GameBootstrap : MonoBehaviour
    {
        private TableScreen _screen;
        private Table _table;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (FindAnyObjectByType<GameBootstrap>() != null) return;
            EnsureCamera();
            EnsureGlobalLight();
            new GameObject("House Edge").AddComponent<GameBootstrap>();
        }

        private void Awake()
        {
            _screen = new TableScreen(transform);
            _screen.ActionChosen += TryApply;
            _screen.NewTableClicked += NewTable;
            NewTable();
            DevCapture.AttachIfRequested(this);
        }

        public Table Table => _table;

        public void NewTable()
        {
            _table = new Table(DevCapture.SeedOverride ?? System.Environment.TickCount, Balance.FirstTableTarget);
            _screen.Refresh(_table);
        }

        public void TryApply(PlayerAction action)
        {
            if (!_table.IsLegal(action)) return;
            _table.Apply(action);
            _screen.Refresh(_table);
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.spaceKey.wasPressedThisFrame)
            {
                if (_table.IsOver) NewTable();
                else TryApply(PlayerAction.Deal);
            }
            if (keyboard.hKey.wasPressedThisFrame) TryApply(PlayerAction.Hit);
            if (keyboard.sKey.wasPressedThisFrame) TryApply(PlayerAction.Stand);
            if (keyboard.dKey.wasPressedThisFrame) TryApply(PlayerAction.DoubleDown);
            if (keyboard.pKey.wasPressedThisFrame) TryApply(PlayerAction.Split);
            if (keyboard.bKey.wasPressedThisFrame) TryApply(PlayerAction.Burn);
            if (keyboard.rKey.wasPressedThisFrame) TryApply(PlayerAction.Surrender);
            if (keyboard.iKey.wasPressedThisFrame) TryApply(PlayerAction.TakeInsurance);
            if (keyboard.nKey.wasPressedThisFrame) TryApply(PlayerAction.DeclineInsurance);
        }

        private static void EnsureCamera()
        {
            var camera = Camera.main;
            if (camera == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                camera = go.AddComponent<Camera>();
            }
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Ui.Felt;
            // the build strips the post-processing shaders, so don't ask for them
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            if (FindAnyObjectByType<AudioListener>() == null) camera.gameObject.AddComponent<AudioListener>();
        }

        private static void EnsureGlobalLight()
        {
            foreach (var light in FindObjectsByType<Light2D>())
                if (light.lightType == Light2D.LightType.Global) return;

            var global = new GameObject("Global Light 2D").AddComponent<Light2D>();
            global.lightType = Light2D.LightType.Global;
        }
    }
}
