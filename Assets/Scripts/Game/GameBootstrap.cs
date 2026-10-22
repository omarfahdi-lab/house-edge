using System.Collections.Generic;
using HouseEdge.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

namespace HouseEdge.Game
{
    // builds the whole game in code after the scene loads, nothing set up in the editor
    public sealed class GameBootstrap : MonoBehaviour
    {
        private const float ReplayStepSeconds = 0.35f;
        // after the hand that ends a table, the table stays up this long so the result can play out
        private const float ResultHoldSeconds = 2.3f;
        private float _holdUntil;

        private Hud _hud;
        private TableView _table;
        private ShopView _shop;
        private RunOverView _runOver;
        private DeckView _deck;
        private RoomView _room;
        private MenuView _menu;
        private Feedback _feedback;
        private Music _music;
        private Profile _profile;
        private List<string> _unlocks = new List<string>();
        private int _dailyPlace;
        // false while the placeholder run behind the first menu is showing
        private bool _playing;

        // a finished run being played back from its inputs
        private Queue<RunInput> _replay;
        private float _replayTimer;

        public Run Run { get; private set; }
        public bool Replaying => _replay != null;

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
            _profile = Saves.LoadProfile();
            var root = Ui.CreateDocument(transform, "UI", 0).rootVisualElement;
            var felt = new VisualElement { pickingMode = PickingMode.Ignore };
            Ui.Fill(felt);
            felt.style.backgroundColor = Ui.Felt;
            Ui.Stretch(felt, Ui.FeltTexture());
            root.Add(felt);
            _table = new TableView(root);
            _shop = new ShopView(root);
            _room = new RoomView(root);
            _hud = new Hud(root);
            _deck = new DeckView(root);
            _runOver = new RunOverView(root);
            _menu = new MenuView(root);
            _feedback = Feedback.Create(transform, root);
            _feedback.GoldAnchor = () => _hud.GoldAnchor.worldBound.center;
            _feedback.DealerAnchor = () => _table.DealerCenter;
            _feedback.PlayerAnchor = () => _table.PlayerCenter;
            _music = Music.Create(transform);
            _menu.MusicOn = () => _music.Enabled;
            _menu.SoundOn = () => !_feedback.Muted;
            _menu.ToggleMusic = () => _music.Enabled = !_music.Enabled;
            _menu.ToggleSound = () => _feedback.Muted = !_feedback.Muted;
            Tooltip.Create(root);

            _table.ActionChosen += action => TryApply(RunInput.Table(action));
            _shop.InputChosen += TryApply;
            _hud.InputChosen += TryApply;
            _hud.CocktailClicked += UseCocktail;
            _hud.DeckClicked += ShowDeck;
            _deck.InputChosen += TryApply;
            _room.InputChosen += TryApply;
            _room.CardOptionClicked += (option, label) => _deck.Pick(Run, label, "Pick a card from your deck.", 1,
                card => Run.IsLegal(RunInput.RoomOption(option, Run.Gambler.Deck.IndexOf(card))),
                picked => RunInput.RoomOption(option, picked.Count > 0 ? picked[0] : -1));
            _runOver.NewRunClicked += () => StartRun(System.Environment.TickCount, Run.Options);
            _runOver.MenuClicked += OpenMenu;
            _runOver.ReplayClicked += StartReplay;
            _menu.StartClicked += StartRun;
            _menu.DailyClicked += () => StartRun(Saves.DailySeed(), new RunOptions { Daily = true });
            _menu.ResumeClicked += () => _menu.Close();
            _menu.AbandonClicked += OpenMenuAfterAbandon;

            // a run sits behind the menu so the table isn't empty
            Run = new Run(DevCapture.SeedOverride ?? System.Environment.TickCount);
            _feedback.Reset(Run);
            Refresh();
            if (DevCapture.Requested)
            {
                _playing = true;
                _menu.Close();
            }
            else OpenMenu();
            DevCapture.AttachIfRequested(this);
        }

        public void ShowDeck() => _deck.ShowDeck(Run);

        public void ShowHowTo(int page)
        {
            OpenMenu();
            _menu.OpenHowTo(page);
        }

        public void StartRun(int seed, RunOptions options)
        {
            _replay = null;
            _playing = true;
            _unlocks = new List<string>();
            Run = new Run(seed, options);
            _menu.Close();
            _deck.Close();
            _feedback.Reset(Run);
            Refresh();
        }

        public void OpenMenu() => _menu.Open(_profile, _playing && !Run.IsOver && !Replaying, Saves.DailyText());

        // an abandoned run isn't recorded
        public void OpenMenuAfterAbandon()
        {
            _replay = null;
            _playing = false;
            Run = new Run(System.Environment.TickCount);
            Refresh();
            OpenMenu();
        }

        public bool Holding => Time.time < _holdUntil;

        public void TryApply(RunInput input)
        {
            if (Replaying || Holding || !Run.IsLegal(input)) return;
            Apply(input);
        }

        private void Apply(RunInput input)
        {
            var before = Snapshot.Of(Run);
            Run.Apply(input);
            // the hand that clears (or loses) a table gets a moment on screen before the shop or the end screen
            if (before.Phase == RunPhase.AtTable && Run.Phase != RunPhase.AtTable && !Replaying && !DevCapture.Requested)
                _holdUntil = Time.time + ResultHoldSeconds;
            _feedback.After(before, Run);
            if (Run.IsOver && !Replaying) RecordRun();
            Refresh();
        }

        private void RecordRun()
        {
            _unlocks = _profile.Record(Run);
            Saves.SaveProfile(_profile);
            _dailyPlace = Run.Options.Daily ? Saves.AddDailyScore(Run.TotalScore) : 0;
        }

        private void StartReplay()
        {
            var inputs = new Queue<RunInput>(Run.Inputs);
            var options = Run.Options;
            int seed = Run.Seed;
            Run = new Run(seed, options);
            _replay = inputs;
            _replayTimer = 0;
            _feedback.Reset(Run);
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
            _table.SetReplay(Replaying);
            bool holding = Holding;
            bool shop = !holding && (Run.Phase == RunPhase.Shop || Run.Phase == RunPhase.BossReward);
            bool backRoom = Run.ActiveTable != Run.Table;
            bool room = !holding && !backRoom && (Run.Phase == RunPhase.RoomChoice || Run.Phase == RunPhase.InRoom);
            ShowScreen(_table.Root, !shop && !room);
            ShowScreen(_shop.Root, shop);
            ShowScreen(_room.Root, room);
            if (room) _room.Refresh(Run);
            if (shop) _shop.Refresh(Run);
            bool over = Run.IsOver && !Replaying && !holding;
            Ui.Show(_runOver.Root, over);
            if (over) _runOver.Refresh(Run, _unlocks, _dailyPlace);
        }

        // a screen that's just appearing slides in
        private static void ShowScreen(VisualElement screen, bool visible)
        {
            bool was = Ui.IsShown(screen);
            Ui.Show(screen, visible);
            if (visible && !was)
            {
                Tooltip.HideAll();
                Ui.Enter(screen, 24, 0, 0.3f);
            }
        }

        // lounge jazz in the casino, something tense when a boss or security is at the table, quiet when it's over
        private Track ChooseTrack()
        {
            if (_menu.IsOpen && !_playing) return Track.Lounge;
            if (Run.IsOver) return Track.None;
            bool tense = Run.Phase == RunPhase.AtTable &&
                         (Run.Stage == TableKind.Boss || Run.AtEyeInTheSky || Run.AtEnforcer || Run.ActiveTable != Run.Table);
            return tense ? Track.Boss : Track.Lounge;
        }

        private bool _wasHolding;

        private void Update()
        {
            _music.Play(ChooseTrack());
            // the hold just ran out: show the shop or the end screen
            if (_wasHolding && !Holding) Refresh();
            _wasHolding = Holding;
            if (Replaying)
            {
                _replayTimer += Time.deltaTime;
                if (_replayTimer < ReplayStepSeconds) return;
                _replayTimer = 0;
                if (_replay.Count > 0) Apply(_replay.Dequeue());
                if (_replay.Count == 0)
                {
                    _replay = null;
                    Refresh();
                }
                return;
            }

            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                if (_deck.IsOpen) _deck.Close();
                else if (_menu.HowToOpen) _menu.CloseHowTo();
                else if (_menu.IsOpen) { if (_playing && !Run.IsOver) _menu.Close(); }
                else OpenMenu();
                return;
            }
            if (keyboard.mKey.wasPressedThisFrame)
            {
                // M mutes everything, or brings everything back
                bool anyOn = _music.Enabled || !_feedback.Muted;
                _music.Enabled = !anyOn;
                _feedback.Muted = anyOn;
                if (_menu.IsOpen) OpenMenu();
            }
            if (_deck.IsOpen || Holding) return;
            if (_menu.IsOpen)
            {
                if (keyboard.spaceKey.wasPressedThisFrame && !_menu.HowToOpen && !_menu.Typing) _menu.NewRun();
                return;
            }

            if (keyboard.spaceKey.wasPressedThisFrame)
            {
                if (Run.IsOver) StartRun(System.Environment.TickCount, Run.Options);
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
