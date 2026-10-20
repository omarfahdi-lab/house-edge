using HouseEdge.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

namespace HouseEdge.Game
{
    // builds the whole game in code after the scene loads, nothing set up in the editor
    public sealed class GameBootstrap : MonoBehaviour
    {
        private Hud _hud;
        private TableView _table;
        private ShopView _shop;
        private RunOverView _runOver;

        public Run Run { get; private set; }

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
            var root = Ui.CreateDocument(transform, "UI", 0).rootVisualElement;
            _table = new TableView(root);
            _shop = new ShopView(root);
            _hud = new Hud(root);
            _runOver = new RunOverView(root);

            _table.ActionChosen += action => TryApply(RunInput.Table(action));
            _shop.InputChosen += TryApply;
            _hud.InputChosen += TryApply;
            _runOver.NewRunClicked += NewRun;

            NewRun();
            DevCapture.AttachIfRequested(this);
        }

        public void NewRun()
        {
            Run = new Run(DevCapture.SeedOverride ?? System.Environment.TickCount);
            Refresh();
        }

        public void TryApply(RunInput input)
        {
            if (!Run.IsLegal(input)) return;
            Run.Apply(input);
            Refresh();
        }

        private void Refresh()
        {
            _hud.Refresh(Run);
            _table.Refresh(Run.Table);
            bool shop = Run.Phase == RunPhase.Shop;
            Ui.Show(_table.Root, !shop);
            Ui.Show(_shop.Root, shop);
            if (shop) _shop.Refresh(Run);
            Ui.Show(_runOver.Root, Run.IsOver);
            if (Run.IsOver) _runOver.Refresh(Run);
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.spaceKey.wasPressedThisFrame)
            {
                if (Run.IsOver) NewRun();
                else if (Run.Phase == RunPhase.Shop) TryApply(RunInput.LeaveShop());
                else TryApply(RunInput.Table(PlayerAction.Deal));
            }
            if (keyboard.hKey.wasPressedThisFrame) TryTable(PlayerAction.Hit);
            if (keyboard.sKey.wasPressedThisFrame) TryTable(PlayerAction.Stand);
            if (keyboard.dKey.wasPressedThisFrame) TryTable(PlayerAction.DoubleDown);
            if (keyboard.pKey.wasPressedThisFrame) TryTable(PlayerAction.Split);
            if (keyboard.bKey.wasPressedThisFrame) TryTable(PlayerAction.Burn);
            if (keyboard.rKey.wasPressedThisFrame) TryTable(PlayerAction.Surrender);
            if (keyboard.iKey.wasPressedThisFrame) TryTable(PlayerAction.TakeInsurance);
            if (keyboard.nKey.wasPressedThisFrame) TryTable(PlayerAction.DeclineInsurance);
        }

        private void TryTable(PlayerAction action) => TryApply(RunInput.Table(action));

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
