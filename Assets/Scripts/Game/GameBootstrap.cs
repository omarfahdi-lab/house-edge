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
        private DeckView _deck;
        private RoomView _room;

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
            _room = new RoomView(root);
            _hud = new Hud(root);
            _deck = new DeckView(root);
            _runOver = new RunOverView(root);

            _table.ActionChosen += action => TryApply(RunInput.Table(action));
            _shop.InputChosen += TryApply;
            _hud.InputChosen += TryApply;
            _hud.CocktailClicked += UseCocktail;
            _hud.DeckClicked += () => _deck.ShowDeck(Run);
            _deck.InputChosen += TryApply;
            _room.InputChosen += TryApply;
            _room.CardOptionClicked += (option, label) => _deck.Pick(Run, label, "Pick a card from your deck.", 1,
                card => Run.IsLegal(RunInput.RoomOption(option, Run.Gambler.Deck.IndexOf(card))),
                picked => RunInput.RoomOption(option, picked.Count > 0 ? picked[0] : -1));
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

        // Cocktails that work on cards open the deck to pick them
        private void UseCocktail(int slot)
        {
            var cocktail = Run.Gambler.Cocktails[slot];
            if (cocktail.Targets.max > 0) _deck.PickFor(Run, slot);
            else TryApply(RunInput.UseCocktail(slot));
        }

        private void Refresh()
        {
            _hud.Refresh(Run);
            _table.Refresh(Run);
            bool shop = Run.Phase == RunPhase.Shop || Run.Phase == RunPhase.BossReward;
            bool backRoom = Run.ActiveTable != Run.Table;
            bool room = !backRoom && (Run.Phase == RunPhase.RoomChoice || Run.Phase == RunPhase.InRoom);
            Ui.Show(_table.Root, !shop && !room);
            Ui.Show(_shop.Root, shop);
            Ui.Show(_room.Root, room);
            if (room) _room.Refresh(Run);
            if (shop) _shop.Refresh(Run);
            Ui.Show(_runOver.Root, Run.IsOver);
            if (Run.IsOver) _runOver.Refresh(Run);
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.escapeKey.wasPressedThisFrame) _deck.Close();
            if (_deck.IsOpen) return;

            if (keyboard.spaceKey.wasPressedThisFrame)
            {
                if (Run.IsOver) NewRun();
                else if (Run.Phase == RunPhase.Shop) TryApply(RunInput.LeaveShop());
                else if (Run.Phase == RunPhase.InRoom && Run.IsLegal(RunInput.LeaveRoom())) TryApply(RunInput.LeaveRoom());
                else TryApply(RunInput.Table(PlayerAction.Deal));
            }
            bool deciding = Run.ActiveTable.Phase == TablePhase.DealerDecision;
            if (keyboard.hKey.wasPressedThisFrame) TryTable(deciding ? PlayerAction.LetDealerDraw : PlayerAction.Hit);
            if (keyboard.sKey.wasPressedThisFrame) TryTable(deciding ? PlayerAction.StopDealer : PlayerAction.Stand);
            if (keyboard.wKey.wasPressedThisFrame) TryTable(PlayerAction.SwapWithDealer);
            if (Run.Phase == RunPhase.RoomChoice)
            {
                if (keyboard.digit1Key.wasPressedThisFrame) TryApply(RunInput.ChooseRoom(0));
                if (keyboard.digit2Key.wasPressedThisFrame) TryApply(RunInput.ChooseRoom(1));
            }
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
