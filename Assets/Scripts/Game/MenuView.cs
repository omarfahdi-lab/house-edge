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
        private readonly VisualElement _root, _decks, _stakes, _howTo;
        private readonly Label _deckText, _stakeText, _stats, _daily, _codeError;
        private readonly Button _endless;
        private bool _endlessOn;
        private readonly TextField _code;
        private readonly Button _resume, _abandon;
        private Profile _profile;
        private StartingDeck _deck = StartingDeck.Standard;
        private Stake _stake = Stake.White;

        public event Action<int, RunOptions> StartClicked;
        public event Action DailyClicked, ResumeClicked, AbandonClicked;

        public VisualElement Root => _root;
        public bool IsOpen => _root.style.display == DisplayStyle.Flex;

        public MenuView(VisualElement parent)
        {
            _root = new VisualElement();
            Ui.Fill(_root);
            _root.style.backgroundColor = new Color(0.02f, 0.09f, 0.06f, 1f);
            _root.style.alignItems = Align.Center;
            _root.style.justifyContent = Justify.Center;
            parent.Add(_root);

            var column = Ui.Column(_root, 10);
            column.style.alignItems = Align.Center;
            column.style.width = 820;

            var title = Ui.Text(column, "HOUSE EDGE", 56, Ui.Gold, bold: true);
            title.style.letterSpacing = 8;
            Ui.Text(column, "A blackjack roguelike. Climb 8 floors of a cursed casino and beat The House.", 17, Ui.Cream);

            var resumeRow = Ui.Row(column, 12);
            _resume = Ui.Button(resumeRow, "Resume  <size=12><color=#FFFFFF88>Esc</color></size>", () => ResumeClicked?.Invoke(), accent: true);
            _resume.enableRichText = true;
            _abandon = Ui.Button(resumeRow, "Abandon run", () => AbandonClicked?.Invoke());

            Section(column, "DECK");
            _decks = Ui.Row(column, 6);
            _decks.style.flexWrap = Wrap.Wrap;
            _decks.style.justifyContent = Justify.Center;
            _deckText = Ui.Text(column, "", 14, Ui.Dim);

            Section(column, "STAKE");
            _stakes = Ui.Row(column, 6);
            _stakeText = Ui.Text(column, "", 14, Ui.Dim);

            // no UI theme here, so a button stands in for a checkbox
            _endless = Ui.Button(column, "", () => { _endlessOn = !_endlessOn; Rebuild(); });
            _endless.style.marginTop = 8;
            _endless.style.height = 32;
            _endless.style.fontSize = 13;

            var play = Ui.Row(column, 12);
            play.style.marginTop = 12;
            var newRun = Ui.Button(play, "New run  <size=12><color=#FFFFFF88>Space</color></size>", NewRun, accent: true);
            newRun.enableRichText = true;
            newRun.style.minWidth = 180;
            Ui.Button(play, "Daily Run", () => DailyClicked?.Invoke()).style.minWidth = 140;
            Ui.Button(play, "How to play", () => Ui.Show(_howTo, _howTo.style.display != DisplayStyle.Flex));

            var codeRow = Ui.Row(column, 8);
            Ui.Text(codeRow, "Run code", 14, Ui.Dim);
            _code = new TextField { maxLength = 16 };
            _code.style.width = 180;
            _code.style.marginLeft = _code.style.marginRight = 0;
            var input = _code.Q(className: TextField.inputUssClassName);
            if (input != null)
            {
                input.style.backgroundColor = new Color(0, 0, 0, 0.5f);
                input.style.color = Ui.Cream;
                input.style.minHeight = 34;
                input.style.fontSize = 16;
                Ui.Border(input, 1, Ui.Dim);
                Ui.Rounded(input, 6);
                Ui.Padding(input, 4, 8);
            }
            Ui.Add(codeRow, _code);
            Ui.Button(codeRow, "Play code", PlayCode).style.height = 34;
            _codeError = Ui.Text(column, "", 13, Ui.Bad);

            _daily = Ui.Text(column, "", 14, Ui.Gold);
            _daily.style.whiteSpace = WhiteSpace.Normal;
            _daily.style.unityTextAlign = TextAnchor.MiddleCenter;
            _stats = Ui.Text(column, "", 13, Ui.Dim);

            _howTo = Ui.Column(_root, 4);
            _howTo.style.position = Position.Absolute;
            _howTo.style.right = 24;
            _howTo.style.top = 24;
            _howTo.style.width = 340;
            _howTo.style.backgroundColor = new Color(0, 0, 0, 0.8f);
            Ui.Rounded(_howTo, 10);
            Ui.Padding(_howTo, 14, 16);
            foreach (var line in HowTo)
                Ui.Text(_howTo, line, 13, Ui.Cream).style.whiteSpace = WhiteSpace.Normal;
            Ui.Show(_howTo, false);
        }

        private static readonly string[] HowTo =
        {
            "Each table: reach the target score before your Hands run out.",
            "Score = Chips x Mult. Chips are your card total plus the outcome's base. Better outcomes (21, Blackjack, a 5-card Charlie) have bigger Mult.",
            "You and the dealer draw from YOUR deck. Shape it with Cocktails and the Card Room.",
            "Burn redraws the card you just hit (even a bust). Burns reset every table.",
            "Vices bend the rules and trigger left to right. Hold 3 of one archetype for a set bonus.",
            "Cheating builds Heat. At 100, security drags you to The Eye in the Sky.",
            "Keys: Space deal, H hit, S stand, D double, P split, B burn, R surrender, W swap, Esc menu.",
        };

        private static void Section(VisualElement parent, string text)
        {
            var label = Ui.Text(parent, text, 12, Ui.Dim, bold: true);
            label.style.marginTop = 8;
        }

        public void Open(Profile profile, bool runInProgress, string dailyText)
        {
            _profile = profile;
            if (!profile.IsUnlocked(_deck)) _deck = StartingDeck.Standard;
            if (!profile.IsUnlocked(_stake)) _stake = Stake.White;
            Ui.Show(_resume, runInProgress);
            Ui.Show(_abandon, runInProgress);
            _daily.text = dailyText;
            _stats.text = $"Runs {profile.Runs}  ·  Wins {profile.Wins}  ·  Best score {profile.BestScore:N0}  ·  Best floor {profile.BestFloor}";
            _codeError.text = "";
            Rebuild();
            Ui.Show(_root, true);
        }

        public void Close() => Ui.Show(_root, false);

        private void Rebuild()
        {
            _decks.Clear();
            foreach (var deck in Decks.All)
            {
                var d = deck;
                bool unlocked = _profile.IsUnlocked(deck);
                var button = Ui.Button(null, Decks.Name(deck), () => { _deck = d; Rebuild(); }, accent: deck == _deck);
                button.style.minWidth = 110;
                button.style.height = 36;
                button.style.fontSize = 14;
                Ui.SetEnabled(button, unlocked);
                Ui.Add(_decks, button);
            }
            _deckText.text = Decks.Describe(_deck);
            _endless.text = _endlessOn ? "Endless: ON (keep climbing after The House)" : "Endless: off";

            _stakes.Clear();
            foreach (var stake in Stakes.All)
            {
                var s = stake;
                var button = Ui.Button(null, stake.ToString(), () => { _stake = s; Rebuild(); }, accent: stake == _stake);
                button.style.minWidth = 76;
                button.style.height = 32;
                button.style.fontSize = 13;
                Ui.SetEnabled(button, _profile.IsUnlocked(stake));
                Ui.Add(_stakes, button);
            }
            var locked = new List<string>();
            foreach (var deck in Decks.All)
                if (!_profile.IsUnlocked(deck)) locked.Add($"{Decks.Name(deck)}: {Decks.Unlock(deck)}");
            _stakeText.text = $"{Stakes.Name(_stake)}: {Stakes.Describe(_stake)}" +
                              (_stake > Stake.White ? " (plus every stake before it)" : "") +
                              (locked.Count > 0 ? "\nLocked decks: " + string.Join(" · ", locked) : "");
            _stakeText.style.whiteSpace = WhiteSpace.Normal;
            _stakeText.style.unityTextAlign = TextAnchor.MiddleCenter;
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
            _codeError.text = "That code doesn't look right. It's like 3F7K2-00.";
        }
    }
}
