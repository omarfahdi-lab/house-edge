using System;
using System.Collections.Generic;
using HouseEdge.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace HouseEdge.Game
{
    // the start menu: deck, stake, Endless, the Daily Run and run codes. also the pause menu
    public sealed class MenuView
    {
        private readonly VisualElement _root, _logo, _decks, _stakes;
        private readonly Label _deckName, _deckText, _deckLock, _stakeName, _stakeText, _summary, _stats, _daily, _codeError;
        private readonly Switch _endless;
        private readonly TextField _code;
        private readonly Button _resume, _newRun, _abandon;
        private readonly HowToView _howTo;
        private bool _endlessOn;
        private Profile _profile;
        private StartingDeck _deck = StartingDeck.Standard, _deckShown = StartingDeck.Standard;
        private Stake _stake = Stake.White, _stakeShown = Stake.White;

        // the audio settings live elsewhere; the menu just shows and flips them
        public Func<bool> MusicOn, SoundOn;
        public Action ToggleMusic, ToggleSound;
        private readonly Switch _musicSwitch, _soundSwitch;

        public event Action<int, RunOptions> StartClicked;
        public event Action DailyClicked, ResumeClicked, AbandonClicked;

        public VisualElement Root => _root;
        public bool IsOpen => _root.style.display == DisplayStyle.Flex;
        public bool HowToOpen => _howTo.IsOpen;

        // typing a run code, so Space is a space
        public bool Typing => _root.focusController?.focusedElement is VisualElement focused && (focused == _code || _code.Contains(focused));

        public MenuView(VisualElement parent)
        {
            _root = new VisualElement();
            Ui.Fill(_root);
            _root.style.backgroundColor = Ui.FeltDark;
            Ui.Stretch(_root, Ui.FeltTexture());
            parent.Add(_root);
            _root.Add(new Backdrop());
            var shade = new VisualElement { pickingMode = PickingMode.Ignore };
            Ui.Fill(shade);
            shade.style.backgroundColor = new Color(0, 0, 0, 0.3f);
            _root.Add(shade);

            // left: the logo and the big buttons
            var left = Ui.Column(_root, 12);
            left.style.position = Position.Absolute;
            left.style.left = 70;
            left.style.top = 0;
            left.style.bottom = 0;
            left.style.width = 440;
            left.style.justifyContent = Justify.Center;

            _logo = Ui.Column(left, 0);
            var suits = Ui.Row(_logo, 8);
            foreach (var shape in new[] { SuitIcon.Shape.Spade, SuitIcon.Shape.Heart, SuitIcon.Shape.Club, SuitIcon.Shape.Diamond })
                Ui.Add(suits, new SuitIcon(shape, shape == SuitIcon.Shape.Heart || shape == SuitIcon.Shape.Diamond ? Ui.Mult : Ui.Cream, 18));
            var house = Ui.Title(_logo, "HOUSE", 86, Ui.Gold);
            house.style.marginTop = 4;
            var edge = Ui.Title(_logo, "EDGE", 86, Ui.Gold);
            edge.style.marginTop = -18;
            foreach (var line in new[] { house, edge })
                line.style.textShadow = new TextShadow { offset = new Vector2(0, 5), blurRadius = 20, color = new Color(0, 0, 0, 0.9f) };
            var tagline = Ui.Body(left, "A blackjack roguelike. Climb 8 floors of a cursed casino, bend the rules with Vices, and beat The House.", 16, Ui.Cream);
            tagline.style.maxWidth = 400;
            tagline.style.marginBottom = 10;

            _resume = MenuButton(left, "Resume", () => ResumeClicked?.Invoke(), ButtonKind.Gold, "Esc");
            _newRun = MenuButton(left, "New run", NewRun, ButtonKind.Gold, "Space");
            var daily = MenuButton(left, "Daily Run", () => DailyClicked?.Invoke(), ButtonKind.Primary);
            Ui.Tip(daily, "Daily Run", "Everyone gets the same seed today: same shops, same cards, same bosses. Standard deck, White Chip.");
            MenuButton(left, "How to play", () => _howTo.Open(), ButtonKind.Normal);
            _abandon = MenuButton(left, "Abandon run", () => AbandonClicked?.Invoke(), ButtonKind.Ghost);
            _daily = Ui.Text(left, "", 13, Ui.Gold, Weight.Medium);
            _daily.style.whiteSpace = WhiteSpace.Normal;
            _daily.style.maxWidth = 380;
            _daily.style.marginTop = 6;
            _stats = Ui.Text(left, "", 12, Ui.Dim);

            var audio = Ui.Row(left, 18);
            audio.style.marginTop = 10;
            _musicSwitch = AudioSwitch(audio, "Music", () => { ToggleMusic?.Invoke(); Rebuild(); });
            _soundSwitch = AudioSwitch(audio, "Sounds", () => { ToggleSound?.Invoke(); Rebuild(); });
            Ui.Text(audio, "M mutes both", 11, Ui.Dim);

            // right: what kind of run to start
            var setup = Ui.Glass(_root, 9, radius: 18);
            setup.style.position = Position.Absolute;
            setup.style.right = 56;
            setup.style.top = 30;
            setup.style.bottom = 30;
            setup.style.width = 600;
            setup.style.backgroundColor = new Color(0.01f, 0.03f, 0.025f, 0.9f);
            Ui.Padding(setup, 18, 24);
            setup.style.justifyContent = Justify.Center;
            var setupHead = Ui.Row(setup, 10);
            Ui.Title(setupHead, "RUN SETUP", 24, Ui.Gold).style.flexGrow = 1;
            Ui.Text(setupHead, "pick, then start a new run", 12, Ui.Dim);

            Section(setup, "STARTING DECK", "The cards you begin with. Locked decks unlock as you play.");
            _decks = Ui.Row(setup);
            _decks.style.flexWrap = Wrap.Wrap;
            _decks.style.justifyContent = Justify.SpaceBetween;
            var deckInfo = InfoBox(setup);
            _deckName = Ui.Text(deckInfo, "", 15, Ui.Cream, Weight.Bold);
            _deckText = Ui.Body(deckInfo, "", 13, Ui.Dim);
            _deckLock = Ui.Text(deckInfo, "", 12, Ui.Gold, Weight.Bold);

            Section(setup, "STAKE", "Difficulty. Each chip adds a rule on top of the ones before it. Win to unlock the next.");
            _stakes = Ui.Row(setup);
            _stakes.style.justifyContent = Justify.SpaceBetween;
            _stakes.style.paddingLeft = _stakes.style.paddingRight = 12;
            var stakeInfo = InfoBox(setup);
            _stakeName = Ui.Text(stakeInfo, "", 15, Ui.Cream, Weight.Bold);
            _stakeText = Ui.Body(stakeInfo, "", 13, Ui.Dim);

            var endlessRow = Ui.Row(setup, 14);
            endlessRow.style.marginTop = 4;
            _endless = new Switch(() => { _endlessOn = !_endlessOn; Rebuild(); });
            Ui.Add(endlessRow, _endless);
            var endlessText = Ui.Column(endlessRow, 1);
            Ui.Text(endlessText, "Endless mode", 15, Ui.Cream, Weight.Bold);
            Ui.Text(endlessText, "Keep climbing after you beat The House. Targets double every floor.", 12, Ui.Dim);

            var codeRow = Ui.Row(setup, 8);
            codeRow.style.marginTop = 4;
            Ui.Caption(codeRow, "RUN CODE");
            _code = new TextField { maxLength = 16 };
            _code.style.width = 170;
            _code.style.marginLeft = _code.style.marginRight = 0;
            var input = _code.Q(className: TextField.inputUssClassName);
            if (input != null)
            {
                input.style.backgroundColor = new Color(0, 0, 0, 0.55f);
                input.style.color = Ui.Cream;
                input.style.minHeight = 32;
                input.style.fontSize = 15;
                input.style.unityFontDefinition = Ui.FontOf(Weight.Bold);
                Ui.Border(input, 1, new Color(1, 1, 1, 0.25f));
                Ui.Rounded(input, 8);
                Ui.Padding(input, 4, 10);
            }
            Ui.Add(codeRow, _code);
            var play = Ui.Button(codeRow, "Play code", PlayCode, ButtonKind.Normal);
            play.style.height = 34;
            Ui.Tip(play, "Run codes", "Every finished run shows a code like 3F7K2-21. Enter one to play the exact same run: same seed, deck and stake.");
            _codeError = Ui.Text(setup, "", 12, Ui.Bad);

            var summaryBox = Ui.Row(setup, 8);
            summaryBox.style.marginTop = 2;
            summaryBox.style.backgroundColor = new Color(0.98f, 0.79f, 0.32f, 0.1f);
            Ui.Border(summaryBox, 1, new Color(0.98f, 0.79f, 0.32f, 0.4f));
            Ui.Rounded(summaryBox, 10);
            Ui.Padding(summaryBox, 8, 12);
            Ui.Add(summaryBox, new SuitIcon(SuitIcon.Shape.Check, Ui.Gold, 16));
            _summary = Ui.Text(summaryBox, "", 14, Ui.Cream, Weight.Medium);

            _howTo = new HowToView(_root);

            // the logo breathes
            float t = 0;
            _logo.schedule.Execute(() =>
            {
                if (!IsOpen) return;
                t += 0.016f;
                _logo.style.translate = new Translate(0, Mathf.Sin(t * 1.6f) * 4);
            }).Every(16);
        }

        private static Switch AudioSwitch(VisualElement parent, string label, Action clicked)
        {
            var row = Ui.Row(parent, 8);
            var toggle = new Switch(clicked);
            toggle.style.scale = new Scale(new Vector2(0.85f, 0.85f));
            Ui.Add(row, toggle);
            Ui.Text(row, label, 13, Ui.Cream, Weight.Bold);
            return toggle;
        }

        private static Button MenuButton(VisualElement parent, string text, Action onClick, ButtonKind kind, string key = null)
        {
            var button = Ui.Button(parent, text, onClick, kind, key);
            button.style.width = 300;
            button.style.height = 50;
            button.style.justifyContent = Justify.SpaceBetween;
            Ui.Padding(button, 0, 20);
            button.Q<Label>().style.fontSize = 18;
            return button;
        }

        private static void Section(VisualElement parent, string title, string text)
        {
            var head = Ui.Column(parent, 1);
            head.style.marginTop = 6;
            Ui.Caption(head, title, Ui.Gold);
            Ui.Text(head, text, 12, Ui.Dim);
        }

        private static VisualElement InfoBox(VisualElement parent)
        {
            var box = Ui.Column(parent, 2);
            box.style.minHeight = 58;
            box.style.backgroundColor = new Color(0, 0, 0, 0.35f);
            Ui.Rounded(box, 10);
            Ui.Padding(box, 7, 12);
            return box;
        }

        public void Open(Profile profile, bool runInProgress, string dailyText)
        {
            _profile = profile;
            if (!profile.IsUnlocked(_deck)) _deck = StartingDeck.Standard;
            if (!profile.IsUnlocked(_stake)) _stake = Stake.White;
            _deckShown = _deck;
            _stakeShown = _stake;
            Ui.Show(_resume, runInProgress);
            Ui.Show(_abandon, runInProgress);
            Ui.Paint(_newRun, runInProgress ? ButtonKind.Normal : ButtonKind.Gold);
            _daily.text = dailyText;
            _stats.text = $"Runs {profile.Runs}   ·   Wins {profile.Wins}   ·   Best score {profile.BestScore:N0}   ·   Best floor {profile.BestFloor}";
            _codeError.text = "";
            Rebuild();
            bool wasOpen = IsOpen;
            Ui.Show(_root, true);
            if (!wasOpen) Ui.Enter(_root, 0, 0, 0.35f);
        }

        public void Close()
        {
            _howTo.Close();
            Ui.Show(_root, false);
        }

        public void CloseHowTo() => _howTo.Close();

        public void OpenHowTo(int page) => _howTo.Open(page);

        private void Rebuild()
        {
            _decks.Clear();
            foreach (var deck in Decks.All) _decks.Add(DeckTile(deck));
            bool deckLocked = !_profile.IsUnlocked(_deckShown);
            _deckName.text = Decks.Name(_deckShown) + (_deckShown == _deck ? "  (selected)" : "");
            _deckText.text = Ui.Rich(Decks.Describe(_deckShown));
            _deckLock.text = deckLocked ? $"LOCKED  ·  Unlock: {Decks.Unlock(_deckShown)}" : "";
            Ui.Show(_deckLock, deckLocked);

            _stakes.Clear();
            foreach (var stake in Stakes.All) _stakes.Add(StakeChip(stake));
            bool stakeLocked = !_profile.IsUnlocked(_stakeShown);
            _stakeName.text = Stakes.Name(_stakeShown) + (_stakeShown == _stake ? "  (selected)" : stakeLocked ? "  (locked)" : "");
            _stakeText.text = Ui.Rich(Stakes.Describe(_stakeShown) +
                              (_stakeShown > Stake.White ? " Plus every rule from the chips before it." : "") +
                              (stakeLocked ? " Win a run on the chip before it to unlock." : ""));

            _endless.Set(_endlessOn);
            _musicSwitch.Set(MusicOn?.Invoke() ?? true);
            _soundSwitch.Set(SoundOn?.Invoke() ?? true);
            _summary.text = $"You'll play:  {Decks.Name(_deck)} deck  ·  {Stakes.Name(_stake)}  ·  Endless {(_endlessOn ? "on" : "off")}";
        }

        private VisualElement DeckTile(StartingDeck deck)
        {
            bool unlocked = _profile.IsUnlocked(deck);
            bool selected = deck == _deck;
            var tile = Ui.Row(null, 10);
            tile.style.width = 178;
            tile.style.height = 52;
            tile.style.marginBottom = 8;
            Ui.Padding(tile, 0, 10);
            Ui.Rounded(tile, 10);
            Ui.Border(tile, selected ? 2 : 1, selected ? Ui.Gold : new Color(1, 1, 1, 0.14f));
            tile.style.backgroundColor = selected ? new Color(0.98f, 0.79f, 0.32f, 0.16f) : new Color(0, 0, 0, 0.35f);
            if (!unlocked) tile.style.opacity = 0.55f;

            var icon = Ui.Column(tile);
            icon.style.width = icon.style.height = 32;
            Ui.Rounded(icon, 8);
            icon.style.alignItems = Align.Center;
            icon.style.justifyContent = Justify.Center;
            icon.style.backgroundColor = new Color(1, 1, 1, 0.06f);
            Ui.Add(icon, new SuitIcon(unlocked ? DeckIcon(deck) : SuitIcon.Shape.Lock, unlocked ? (selected ? Ui.Gold : Ui.Cream) : Ui.Dim, 18));
            var text = Ui.Column(tile);
            text.style.flexGrow = 1;
            Ui.Text(text, Decks.Name(deck), 14, selected ? Ui.Gold : Ui.Cream, Weight.Bold);
            Ui.Text(text, selected ? "SELECTED" : unlocked ? "click to pick" : "locked", 10, selected ? Ui.Gold : Ui.Dim, selected ? Weight.Heavy : Weight.Regular);
            if (selected) Ui.Add(tile, new SuitIcon(SuitIcon.Shape.Check, Ui.Gold, 16));

            foreach (var child in tile.Query<VisualElement>().ToList())
                if (child != tile) child.pickingMode = PickingMode.Ignore;
            tile.RegisterCallback<ClickEvent>(_ =>
            {
                _deckShown = deck;
                if (unlocked) _deck = deck;
                Rebuild();
            });
            tile.RegisterCallback<PointerEnterEvent>(_ => { if (!selected) tile.style.backgroundColor = new Color(1, 1, 1, 0.08f); });
            tile.RegisterCallback<PointerLeaveEvent>(_ => { if (!selected) tile.style.backgroundColor = new Color(0, 0, 0, 0.35f); });
            return tile;
        }

        private static SuitIcon.Shape DeckIcon(StartingDeck deck) => deck switch
        {
            StartingDeck.ShortDeck => SuitIcon.Shape.Diamond,
            StartingDeck.AceHigh => SuitIcon.Shape.Crown,
            StartingDeck.Lowball => SuitIcon.Shape.Club,
            StartingDeck.MarkedDeck => SuitIcon.Shape.Eye,
            StartingDeck.DealersDeck => SuitIcon.Shape.Cards,
            _ => SuitIcon.Shape.Spade,
        };

        private VisualElement StakeChip(Stake stake)
        {
            bool unlocked = _profile.IsUnlocked(stake);
            bool selected = stake == _stake;
            var column = Ui.Column(null, 4);
            column.style.alignItems = Align.Center;
            column.style.width = 70;
            var holder = new VisualElement();
            holder.style.width = holder.style.height = 56;
            holder.style.alignItems = Align.Center;
            holder.style.justifyContent = Justify.Center;
            Ui.Rounded(holder, 28);
            if (selected) Ui.Border(holder, 3, Ui.Gold);
            column.Add(holder);
            var chip = new PokerChip(PokerChip.ColorOf(stake), PokerChip.InsertOf(stake), selected ? 46 : 42);
            holder.Add(chip);
            if (!unlocked)
            {
                chip.style.opacity = 0.35f;
                var lockIcon = new SuitIcon(SuitIcon.Shape.Lock, Ui.Cream, 18);
                lockIcon.style.position = Position.Absolute;
                holder.Add(lockIcon);
            }
            Ui.Text(column, stake.ToString(), 12, selected ? Ui.Gold : unlocked ? Ui.Cream : Ui.Dim, selected ? Weight.Heavy : Weight.Medium);

            Ui.Transition(holder, 0.15f, "scale");
            if (selected) holder.style.scale = new Scale(new Vector2(1.08f, 1.08f));
            foreach (var child in column.Query<VisualElement>().ToList())
                if (child != column) child.pickingMode = PickingMode.Ignore;
            column.RegisterCallback<ClickEvent>(_ =>
            {
                _stakeShown = stake;
                if (unlocked) _stake = stake;
                Rebuild();
            });
            column.RegisterCallback<PointerEnterEvent>(_ => holder.style.scale = new Scale(new Vector2(1.12f, 1.12f)));
            column.RegisterCallback<PointerLeaveEvent>(_ => holder.style.scale = new Scale(selected ? new Vector2(1.08f, 1.08f) : Vector2.one));
            return column;
        }

        public void NewRun()
        {
            var options = new RunOptions { Deck = _deck, Stake = _stake, Endless = _endlessOn };
            StartClicked?.Invoke(System.Environment.TickCount, options);
        }

        private void PlayCode()
        {
            if (RunCodes.TryDecode(_code.value, out int seed, out var options))
            {
                StartClicked?.Invoke(seed, options);
                return;
            }
            _codeError.text = "That code doesn't look right. It should look like 3F7K2-00.";
        }
    }
}
