using System.Collections.Generic;
using System.Linq;
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
        private const float ResultHoldSeconds = 1.1f;
        private float _holdUntil;

        private Hud _hud;
        private TableView _table;
        private ShopView _shop;
        private RunOverView _runOver;
        private DeckView _deck;
        private RoomView _room;
        private MenuView _menu;
        private MapView _map;
        private PackView _pack;
        private CollectionView _collection;
        private BossIntro _bossIntro;
        private Coach _coach;
        private Table _introShownFor, _heardTable;
        private int _heardDeal, _heard;
        // each floor's boss, for the climb strip on the map
        private readonly Dictionary<int, string> _bosses = new Dictionary<int, string>();
        // the table the map was last shown before, so each table you walk up to opens with the map once
        private Table _mapShownTable;
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
        public TableView Table => _table;
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
            _pack = new PackView(root);
            _hud = new Hud(root);
            _deck = new DeckView(root);
            _map = new MapView(root);
            _map.InputChosen += TryApply;
            _map.Footstep += () => _feedback.CardSound();
            // a boss waits for you to leave the map before walking in
            _map.Closed += IntroduceBoss;
            _coach = new Coach(root, _table);
            _coach.Finished += () =>
            {
                PlayerPrefs.SetInt("tutorialDone", 1);
                PlayerPrefs.Save();
            };
            _runOver = new RunOverView(root);
            _menu = new MenuView(root);
            _bossIntro = new BossIntro(root);
            _collection = new CollectionView(root);
            _menu.CollectionClicked += () => _collection.Open(_profile);
            _pack.InputChosen += TryApply;
            _bossIntro.Closed += () => Listen(force: true);
            _feedback = Feedback.Create(transform, root);
            _feedback.GoldAnchor = () => _hud.GoldAnchor.worldBound.center;
            _feedback.DealerAnchor = () => _table.DealerCenter;
            _feedback.PlayerAnchor = () => _table.PlayerCenter;
            _feedback.PopCard = _table.PopCard;
            _feedback.CardAnchor = _table.CardCenter;
            _feedback.HandIndex = _table.HandIndex;
            _feedback.Raged = _table.Enrage;
            _feedback.ViceRow = _hud.FocusVice;
            _feedback.ActOut = () =>
            {
                _table.ActOut();
                if (Replaying) return;
                string quip = DealerQuips.For(Run);
                if (quip != null) _table.Say(quip);
            };
            _feedback.CommitScore = () =>
            {
                if (_hud.FrozenScore == null) return;
                _hud.FrozenScore = null;
                _hud.Refresh(Run);
            };
            _table.CardLanded += _feedback.CardSound;
            TableView.Speed = PlayerPrefs.GetFloat("speed", 1);
            _menu.SpeedOf = () => TableView.Speed;
            _menu.SetSpeed = speed =>
            {
                TableView.Speed = speed;
                PlayerPrefs.SetFloat("speed", speed);
                PlayerPrefs.Save();
            };
            _music = Music.Create(transform);
            _menu.MusicOn = () => _music.Enabled;
            _menu.SoundOn = () => !_feedback.Muted;
            _menu.ToggleMusic = () => _music.Enabled = !_music.Enabled;
            _menu.ToggleSound = () => _feedback.Muted = !_feedback.Muted;
            Tooltip.Create(root);

            _table.ActionChosen += action => TryApply(RunInput.Table(action));
            _table.TrickPlayed += (slot, target) => TryApply(RunInput.PlayTrick(slot, target));
            _shop.InputChosen += TryApply;
            _hud.InputChosen += TryApply;
            _hud.CocktailClicked += UseCocktail;
            _hud.DeckClicked += ShowDeck;
            _hud.MapClicked += ShowMap;
            _deck.InputChosen += TryApply;
            _room.InputChosen += TryApply;
            _room.CardOptionClicked += (option, label) => _deck.Pick(Run, label, "Pick a card from your deck.", 1,
                card => Run.IsLegal(RunInput.RoomOption(option, Run.Gambler.Deck.IndexOf(card))),
                picked => RunInput.RoomOption(option, picked.Count > 0 ? picked[0] : -1));
            _runOver.NewRunClicked += () => StartRun(System.Environment.TickCount, Run.Options);
            _runOver.MenuClicked += OpenMenu;
            _runOver.ReplayClicked += StartReplay;
            _menu.StartClicked += StartRun;
            // the Daily Run's twist comes from the date's seed, so everyone gets the same one
            _menu.DailyClicked += () => StartRun(Saves.DailySeed(), new RunOptions { Daily = true, Modifier = Modifiers.ForSeed(Saves.DailySeed()) });
            _menu.ResumeClicked += () => _menu.Close();
            _menu.AbandonClicked += OpenMenuAfterAbandon;

            // a run sits behind the menu so the table isn't empty
            Run = new Run(DevCapture.SeedOverride ?? System.Environment.TickCount);
            Gallery.Deal(Run.Seed);
            _feedback.Reset(Run);
            Refresh();
            if (DevCapture.Requested)
            {
                _playing = true;
                _menu.Close();
            }
            else OpenMenu();
            DevCapture.AttachIfRequested(this);
            StartCoroutine(Preloader.Run());
        }

        public void ShowDeck() => _deck.ShowDeck(Run);

        // a look at the floor's map. between tables it's open anyway, for picking the next stop
        public void ShowMap()
        {
            if (Run.Phase == RunPhase.RoomChoice) return;
            SkipPlayback();
            Remember();
            _map.Open(Run, _bosses, choosing: false);
        }

        // the boss walks in before their table's first deal
        private void IntroduceBoss()
        {
            if (!_playing || Replaying || DevCapture.Requested || Holding || _menu.IsOpen || _bossIntro.IsOpen || _map.Viewing) return;
            if (Run.Phase != RunPhase.AtTable || Run.Table.DealsPlayed > 0 || _introShownFor == Run.Table) return;
            if (Run.Boss == null && Run.Dealer is Rival rival)
            {
                // the rival gets an entrance too, one that remembers your last meeting
                _introShownFor = Run.Table;
                _map.Close();
                _bossIntro.Show(RivalLines.Intro(Run, rival), RivalLines.Kind(rival));
                _feedback.BossSting();
                return;
            }
            if (Run.Boss == null) return;
            _introShownFor = Run.Table;
            _map.Close();
            string kind = Run.AtEyeInTheSky ? "SECURITY"
                : Run.AtElite ? "ELITE TABLE  ·  WIN FOR A RARE VICE"
                : Run.AtEnforcer ? "COLLECTIONS"
                : Run.Floor == Balance.Floors ? $"FINAL BOSS  ·  FLOOR {Run.Floor}"
                : $"PIT BOSS  ·  FLOOR {Run.Floor}";
            _bossIntro.Show(Run.Boss.Definition, kind);
            _feedback.BossSting();
        }

        // for screenshots: a boss's entrance on demand
        public void PreviewBoss(int index)
        {
            var all = Bosses.PitBosses.Concat(new[] { Bosses.TheHouse, Bosses.EyeInTheSky, Bosses.Enforcer }).ToList();
            _bossIntro.Show(all[Mathf.Clamp(index, 0, all.Count - 1)], "PIT BOSS  ·  FLOOR 3");
        }

        // what the boss just said, as a speech bubble
        private void Listen(bool force = false)
        {
            var table = Run.ActiveTable;
            if (table != _heardTable || table.DealsPlayed != _heardDeal)
            {
                _heardTable = table;
                _heardDeal = table.DealsPlayed;
                _heard = 0;
            }
            if (_bossIntro.IsOpen || Run.Boss == null || table != Run.Table) return;
            if (table.Announcements.Count <= _heard && !force) return;
            var fresh = table.Announcements.Skip(_heard).ToList();
            _heard = table.Announcements.Count;
            if (fresh.Count == 0) return;
            _table.Say($"<b>{Run.Boss.Name}:</b> {string.Join(" ", fresh)}");
            _feedback.BossSpeaks();
        }

        private void Remember()
        {
            if (Run.FloorBoss != null) _bosses[Run.Floor] = Run.FloorBoss.Name;
        }

        public void ShowCollection() => _collection.Open(_profile);

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
            // Vices you haven't unlocked stay out of this run (the Daily Run is the same for everyone)
            options = options.Clone();
            // the guided first run is always the plain game
            if (options.Tutorial)
            {
                options.Deck = StartingDeck.Standard;
                options.Stake = Stake.White;
                options.Endless = false;
                _coach.Reset();
            }
            options.LockedVices = options.Daily ? new List<string>() : _profile.LockedVices;
            _bosses.Clear();
            _mapShownTable = null;
            // a fresh set of paintings to play the dealers, hosts and you
            Gallery.Deal(seed);
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
            // an input during the scoring playback skips to the end of it
            if (_feedback.Playing)
            {
                SkipPlayback();
                if (!DevCapture.Requested) return;
            }
            if (Replaying || Holding || !Run.IsLegal(input) || !_coach.Allows(Run, input)) return;
            Apply(input);
        }

        private void SkipPlayback()
        {
            _feedback.Skip();
            _holdUntil = Mathf.Min(_holdUntil, Time.time + 0.9f);
        }

        private void Apply(RunInput input)
        {
            // anything still playing back from the last hand finishes first
            _feedback.Skip();
            var before = Snapshot.Of(Run);
            Run.Apply(input);
            if (Run.IsOver && !Replaying) RecordRun();
            // the score waits for the playback to count it in
            _hud.FrozenScore = before.ScoredBy(Run) && Run.Table == before.Table ? before.Score : (long?)null;
            bool leftTable = before.Phase == RunPhase.AtTable && Run.Phase != RunPhase.AtTable;
            // a Back Room hand is played inside the room, so ending it doesn't leave the table phase. it holds too
            bool backRoomEnded = Run.Phase == RunPhase.InRoom && before.Table != before.MainTable && Run.ActiveTable != before.Table;
            if (backRoomEnded && !Replaying && !DevCapture.Requested) TableOnScreen.HeldBackRoom = before.Table;
            // the hand that clears (or loses) a table stays on screen before the shop, the room or the end screen
            if ((leftTable || backRoomEnded) && !Replaying && !DevCapture.Requested) _holdUntil = float.MaxValue;
            Refresh();
            _feedback.After(before, Run, _table.SettleSeconds);
            if (_holdUntil == float.MaxValue) _holdUntil = Time.time + _feedback.RemainingSeconds + ResultHoldSeconds;
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
            _bosses.Clear();
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
            // the held Back Room result has been seen: back to the room
            if (!Holding) TableOnScreen.HeldBackRoom = null;
            Remember();
            // each table you walk up to opens with the map, so leaving the last stop shows where you're headed
            // (an Elite was just picked on the map; security and the Enforcer are surprises)
            if (_playing && !Replaying && !DevCapture.Requested && !Holding && !_menu.IsOpen && Run.Phase == RunPhase.AtTable
                && Run.Table.DealsPlayed == 0 && Run.Table != _mapShownTable && !Run.AtElite && !Run.AtEyeInTheSky && !Run.AtEnforcer)
            {
                _mapShownTable = Run.Table;
                ShowMap();
            }
            _hud.Refresh(Run);
            _table.Refresh(Run);
            _table.SetReplay(Replaying);
            bool holding = Holding;
            bool shop = !holding && (Run.Phase == RunPhase.Shop || Run.Phase == RunPhase.BossReward || Run.Phase == RunPhase.OpeningPack);
            bool backRoom = Run.ActiveTable != Run.Table;
            bool room = !holding && !backRoom && Run.Phase == RunPhase.InRoom;
            // between tables the map is where you pick the next stop
            bool choosing = !holding && Run.Phase == RunPhase.RoomChoice;
            if (choosing) _map.Open(Run, _bosses, choosing: true);
            else if (_map.IsOpen && !_map.Viewing) _map.Close();
            ShowScreen(_table.Root, !shop && !room && !choosing);
            _pack.Refresh(Run, Packs.Name(Run.OpenPack));
            IntroduceBoss();
            Listen();
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

        // lounge jazz between tables, a livelier swing at the tables, something tense for bosses and security
        private Track ChooseTrack()
        {
            if (_menu.IsOpen && !_playing) return Track.Lounge;
            if (Run.IsOver) return Track.None;
            if (Run.Phase != RunPhase.AtTable || Holding) return Track.Lounge;
            bool tense = Run.Stage == TableKind.Boss || Run.AtElite || Run.AtEyeInTheSky || Run.AtEnforcer || Run.ActiveTable != Run.Table;
            return tense ? Track.Boss : Track.Table;
        }

        // the band leans in as the Hands run out
        private float Tension()
        {
            if (!_playing || Run.IsOver || Run.Phase != RunPhase.AtTable || _menu.IsOpen) return 0;
            var table = Run.ActiveTable;
            if (table.Score >= table.Target) return 0;
            return table.HandsLeft <= 1 ? 1 : table.HandsLeft == 2 ? 0.5f : 0;
        }

        private bool _wasHolding;

        private void Update()
        {
            _music.Play(ChooseTrack());
            _music.SetTension(Tension());
            _coach.Refresh(Run, !_playing || Replaying || Holding || _feedback.Playing || _menu.IsOpen || _bossIntro.IsOpen || _deck.IsOpen || _collection.IsOpen || _map.Viewing);
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
                if (_collection.IsOpen) _collection.Close();
                else if (_table.CancelTarget()) { }
                else if (_bossIntro.IsOpen) _bossIntro.Close();
                else if (_map.Viewing) _map.Close();
                else if (_deck.IsOpen) _deck.Close();
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
            // Space or a click during the scoring playback skips it
            if (_feedback.Playing && !_menu.IsOpen && (keyboard.spaceKey.wasPressedThisFrame || Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame))
            {
                SkipPlayback();
                return;
            }
            if (_collection.IsOpen) return;
            if (_bossIntro.IsOpen)
            {
                if (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame) _bossIntro.Close();
                return;
            }
            if (_map.Viewing)
            {
                if (keyboard.spaceKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame || keyboard.tabKey.wasPressedThisFrame) _map.Close();
                return;
            }
            if (keyboard.tabKey.wasPressedThisFrame && _playing && !_menu.IsOpen && !Run.IsOver)
            {
                ShowMap();
                return;
            }
            if (_deck.IsOpen || Holding) return;
            if (keyboard.enterKey.wasPressedThisFrame && !_menu.IsOpen && _coach.DismissKey()) return;
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
                if (keyboard.digit1Key.wasPressedThisFrame) _map.Choose(0);
                if (keyboard.digit2Key.wasPressedThisFrame) _map.Choose(1);
                if (keyboard.digit3Key.wasPressedThisFrame) _map.Choose(2);
            }
            if (keyboard.xKey.wasPressedThisFrame) TryTable(PlayerAction.Raise);
            if (Run.ActiveTable.Phase == TablePhase.Choosing)
            {
                if (keyboard.digit1Key.wasPressedThisFrame) TryTable(PlayerAction.GiveFirst);
                if (keyboard.digit2Key.wasPressedThisFrame) TryTable(PlayerAction.GiveSecond);
                if (keyboard.digit3Key.wasPressedThisFrame) TryTable(PlayerAction.GiveThird);
            }
            if (Run.Phase == RunPhase.AtTable && Run.Table.Phase == TablePhase.PlayerTurn)
            {
                if (keyboard.digit1Key.wasPressedThisFrame) _table.UseTrickSlot(0);
                if (keyboard.digit2Key.wasPressedThisFrame) _table.UseTrickSlot(1);
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
