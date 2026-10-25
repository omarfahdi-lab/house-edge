using System;
using System.Collections.Generic;
using System.Linq;
using HouseEdge.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace HouseEdge.Game
{
    // what changed between two moments of a run, so Feedback knows what to celebrate
    public readonly struct Snapshot
    {
        public readonly Table Table;
        // the run's own table; Table differs from it during a Back Room hand
        public readonly Table MainTable;
        public readonly int Deals;
        public readonly int Scored;
        public readonly int Gold;
        public readonly int Heat;
        public readonly int Cards;
        public readonly RunPhase Phase;
        public readonly bool Eye;
        public readonly long Score;

        private Snapshot(Run run)
        {
            Table = run.ActiveTable;
            MainTable = run.Table;
            Deals = Table.DealsPlayed;
            Scored = Table.LastScores.Count;
            Gold = run.Gambler.Gold;
            Heat = run.Gambler.Heat;
            Cards = Table.PlayerHands.Sum(h => h.Cards.Count) + Table.DealerCards.Count + Table.SecondDealerCards.Count;
            Phase = run.Phase;
            Eye = run.AtEyeInTheSky;
            Score = run.Table.Score;
        }

        public static Snapshot Of(Run run) => new Snapshot(run);

        // a hand (or split hands) was just settled between this snapshot and now
        public bool ScoredBy(Run run)
        {
            var table = run.Shown();
            return table.LastScores.Count > 0 && (table != Table || table.DealsPlayed != Deals || Scored == 0);
        }
    }

    // sounds, the score popup, particles, screen shake and the security alarm. all made in code
    public sealed class Feedback : MonoBehaviour
    {
        private AudioSource _audio;
        private AudioClip _card, _chip, _win, _lose, _coin, _alarm, _bust, _blackjack, _cleared, _clink, _trombone, _fanfare, _boom;
        private VisualElement _root, _flash, _popup, _chipsBox, _multBox;
        private Label _chips, _mult, _times, _total;
        private Particles _particles;
        private Sunburst _rays;
        private bool _raysOn;
        private float _clinkCooldown;
        private float _shake, _flashTime, _flashStrength;
        private Color _flashColor;
        private VisualElement _callouts;
        private AudioClip _tick, _sting, _speak;

        // a boss walks in
        public void BossSting()
        {
            Play(_sting, steady: true);
            Play(_boom);
            Flash(new Color(0.6f, 0.05f, 0.08f), 0.6f, 0.2f);
        }

        public void BossSpeaks() => Play(_speak);

        // the scoring playback: actions at times in 1x seconds, run as the clock reaches them
        private readonly List<(float at, Action act)> _timeline = new List<(float, Action)>();
        private float _clock;
        private bool _skipping;
        // the big total counting up
        private long _totalTo;
        private float _totalT = 1;
        private bool _popupShown;

        public bool Playing => _timeline.Count > 0;
        // real seconds until the playback ends
        public float RemainingSeconds => Playing ? Mathf.Max(0, _timeline[_timeline.Count - 1].at - _clock) / TableView.Speed : 0;

        // hooks into the table and sidebar, set by the bootstrap
        public Action<int, int> PopCard;
        public Func<int, int, Vector2?> CardAnchor;
        public Func<PlayerHand, int> HandIndex;
        public Func<object, VisualElement> ViceRow;
        public Action CommitScore, ActOut;
        // the boss just went into their second phase
        public Action Raged;

        // where coins burst from when you're paid: the gold counter in the sidebar, in world space
        public Func<Vector2> GoldAnchor;
        // the dealer's cards and yours, in world space, for chips sliding across the table
        public Func<Vector2> DealerAnchor, PlayerAnchor;

        public static Feedback Create(Transform parent, VisualElement root)
        {
            var feedback = new GameObject("Feedback").AddComponent<Feedback>();
            feedback.transform.SetParent(parent, false);
            feedback.Build(root);
            return feedback;
        }

        private void Build(VisualElement root)
        {
            _root = root;
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _card = Synth.Noise(0.05f, 0.25f);
            _chip = Synth.Tones(new[] { 2200f, 3100f }, 0.07f, 0.25f);
            _coin = Synth.Tones(new[] { 1320f, 1760f }, 0.06f, 0.2f);
            _win = Synth.Tones(new[] { 523f, 659f, 784f }, 0.09f, 0.3f);
            _blackjack = Synth.Tones(new[] { 523f, 659f, 784f, 1047f }, 0.09f, 0.35f);
            _cleared = Synth.Tones(new[] { 392f, 523f, 659f, 784f, 1047f }, 0.1f, 0.35f);
            _lose = Synth.Tones(new[] { 196f, 147f }, 0.16f, 0.3f);
            _bust = Synth.Tones(new[] { 110f }, 0.25f, 0.3f);
            _alarm = Synth.Siren(1.2f, 0.25f);
            _clink = Synth.Tones(new[] { 3400f }, 0.05f, 0.12f);
            _trombone = Synth.Brass(new[] { 196f, 185f, 175f, 165f }, new[] { 0.42f, 0.42f, 0.42f, 1.3f }, 0.3f);
            _fanfare = Synth.Brass(new[] { 392f, 523f, 659f, 784f, 659f, 1047f }, new[] { 0.14f, 0.14f, 0.14f, 0.3f, 0.14f, 0.9f }, 0.26f);
            _boom = Synth.Boom(0.7f, 0.35f);
            _tick = Synth.Tones(new[] { 880f }, 0.07f, 0.22f);
            _sting = Synth.Brass(new[] { 110f, 104f, 98f }, new[] { 0.22f, 0.22f, 1.1f }, 0.34f);
            _speak = Synth.Tones(new[] { 330f, 262f }, 0.08f, 0.18f);

            _flash = new VisualElement();
            Ui.Fill(_flash);
            _flash.pickingMode = PickingMode.Ignore;
            _flash.style.backgroundColor = new Color(0, 0, 0, 0);
            root.Add(_flash);

            // light rays behind a big hand
            _rays = new Sunburst(new Color(0.98f, 0.79f, 0.32f, 0.16f), 620);
            _rays.style.top = PopupTop + 60 - 310;
            _rays.style.opacity = 0;
            root.Add(_rays);

            // Chips x Mult = total, on the open right side of the felt, clear of every card
            _popup = Ui.Column(root, 6);
            _popup.pickingMode = PickingMode.Ignore;
            _popup.style.position = Position.Absolute;
            _popup.style.right = 16;
            _popup.style.width = PopupWidth;
            _popup.style.top = PopupTop;
            _popup.style.alignItems = Align.Center;
            var equation = Ui.Row(_popup, 10);
            (_chipsBox, _chips) = ScoreBox(equation, Ui.Chips, "CHIPS");
            _times = Ui.Title(equation, "×", 34, Ui.Cream);
            (_multBox, _mult) = ScoreBox(equation, Ui.Mult, "MULT");
            _total = Ui.Title(_popup, "", 58, Ui.Gold);
            _total.style.textShadow = new TextShadow { offset = new Vector2(0, 4), blurRadius = 18, color = new Color(0, 0, 0, 0.9f) };
            foreach (var e in _popup.Query<VisualElement>().ToList()) e.pickingMode = PickingMode.Ignore;
            Ui.Show(_popup, false);

            _callouts = new VisualElement { pickingMode = PickingMode.Ignore };
            Ui.Fill(_callouts);
            root.Add(_callouts);

            _particles = new Particles();
            root.Add(_particles);
        }

        private static (VisualElement, Label) ScoreBox(VisualElement parent, Color color, string name)
        {
            var box = Ui.Column(parent, 0);
            box.style.alignItems = Align.Center;
            box.style.minWidth = 104;
            Ui.Padding(box, 6, 14);
            Ui.Rounded(box, 12);
            box.style.backgroundImage = Ui.Gradient(color, Color.Lerp(color, Color.black, 0.45f));
            box.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
            Ui.Border(box, 2, Color.Lerp(color, Color.white, 0.4f));
            box.style.borderBottomWidth = 4;
            box.style.borderBottomColor = new Color(0, 0, 0, 0.4f);
            var value = Ui.Text(box, "", 30, Color.white, Weight.Heavy);
            Ui.Text(box, name, 10, new Color(1, 1, 1, 0.75f), Weight.Heavy).style.letterSpacing = 2;
            return (box, value);
        }

        public void Reset(Run run)
        {
            Skip();
            HidePopup(true);
        }

        public void CardSound()
        {
            if (!_skipping) Play(_card);
        }

        // reacts to whatever the last input did. a settled hand plays back its scoring step by step,
        // starting once the cards have landed (settle seconds from now)
        public void After(Snapshot before, Run run, float settle)
        {
            Skip();
            _clock = 0;
            var table = run.Shown();
            if (run.Phase != RunPhase.AtTable && table == run.Table && before.Phase != RunPhase.AtTable) HidePopup(true);

            bool scored = before.ScoredBy(run) && table.LastScores.Any(s => s.Outcome != Outcome.Surrender);
            float end = scored ? Playback(run, table, settle * TableView.Speed) : 0;

            // everything below happens once the playback is over
            bool cleared = before.Phase == RunPhase.AtTable && run.Phase != RunPhase.AtTable && !run.IsOver && before.Table == run.Table;
            int goldGained = run.Gambler.Gold - before.Gold;
            Schedule(end, () =>
            {
                if (cleared)
                {
                    Play(_cleared);
                    _particles.Burst(new Vector2(TableCenterX, 120), 70, 640);
                    Flash(new Color(0.98f, 0.79f, 0.32f), 0.5f, 0.14f);
                }
                if (run.Phase == RunPhase.Won && before.Phase != RunPhase.Won) Fireworks();
                if (run.Phase == RunPhase.Lost && before.Phase != RunPhase.Lost)
                {
                    // the house wins: cards rain down and a sad trombone plays
                    _particles.CardRain(_root.layout.width, 12);
                    StartCoroutine(After(0.5f, () => Play(_trombone, steady: true)));
                    Flash(new Color(0.5f, 0, 0), 1.2f, 0.14f);
                }
                if (goldGained > 0)
                {
                    if (!scored && !cleared) Play(_coin);
                    if (GoldAnchor != null) _particles.Coins(_root.WorldToLocal(GoldAnchor()), Mathf.Clamp(goldGained, 3, 18), 260);
                }
                if (goldGained < 0 && before.Phase != RunPhase.AtTable) Play(_chip);
                // past half the target, the boss gets angry
                if (scored && table.RagedThisDeal && run.Boss != null)
                {
                    Banner("PHASE TWO", run.Boss.RageText, Ui.Bad);
                    BossSting();
                    _shake = 0.35f;
                    Flash(new Color(0.7f, 0.05f, 0.05f), 0.8f, 0.18f);
                    Raged?.Invoke();
                }
                if (run.AtEyeInTheSky && !before.Eye || run.Gambler.Heat >= Balance.MaxHeat && before.Heat < Balance.MaxHeat)
                {
                    Play(_alarm);
                    Flash(new Color(0.9f, 0.05f, 0.05f), 1.2f, 0.3f, pulse: true);
                }
            });
        }

        // Balatro-style: each card and each effect adds its part, the numbers tick up, then the total lands
        private float Playback(Run run, Table table, float t)
        {
            t += 0.15f;
            int beat = 0;
            long grand = table.LastScores.Sum(sc => sc.Total);
            float share = grand / (float)Math.Max(1, table.Target);
            foreach (var score in table.LastScores)
            {
                if (score.Steps.Count == 0 || score.Total <= 0) continue;
                int hand = HandIndex?.Invoke(score.Hand) ?? 0;
                bool big = score.Outcome == Outcome.Blackjack || score.Outcome == Outcome.Charlie;
                var shown = score;
                Schedule(t, () => ShowPopup(big || share >= 0.5f));
                t += 0.25f;
                foreach (var step in score.Steps)
                {
                    var s = step;
                    if (s.Kind == ScoreSource.Hand)
                    {
                        // your cards count in one at a time
                        int n = shown.Hand.Cards.Count;
                        for (int i = 0; i < n; i++)
                        {
                            int card = i, b = beat++;
                            Schedule(t + i * 0.16f, () =>
                            {
                                PopCard?.Invoke(hand, card);
                                // each card throws its chips into the Chips box
                                if (!_skipping && !Settings.ReducedMotion && CardAnchor?.Invoke(hand, card) is Vector2 spot)
                                    _particles.Sparks(_root.WorldToLocal(spot), Center(_chipsBox), Ui.Chips, 9);
                                SetChips((int)Math.Round(s.Chips * (card + 1) / (double)n));
                                Tick(b);
                            });
                        }
                        t += n * 0.16f + 0.14f;
                        continue;
                    }
                    int beatNow = beat++;
                    Schedule(t, () =>
                    {
                        if (s.ChipsAdded != 0) SetChips(s.Chips);
                        if (s.MultAdded != 0 || s.Multiplied) SetMult(s.Mult);
                        Callout(s, hand);
                        Tick(beatNow);
                    });
                    t += 0.45f;
                }
                // the total counts up, then slams into the score
                Schedule(t, () =>
                {
                    _totalTo = shown.Total;
                    _totalT = 0;
                    Play(big ? _blackjack : _win);
                });
                t += 0.75f;
                Schedule(t, () =>
                {
                    CommitScore?.Invoke();
                    Ui.Pop(_total, 1.35f);
                    var center = new Vector2(_root.layout.width - 16 - PopupWidth / 2, PopupTop + 90);
                    _particles.Coins(center, Mathf.Clamp((int)(share * 40), 6, 40));
                    if (big || share >= 0.5f) _particles.Burst(center, big ? 60 : 36);
                    if (big) Flash(new Color(0.98f, 0.79f, 0.32f), 0.4f, 0.12f);
                    if (share >= 0.5f) _shake = Mathf.Min(0.5f, 0.15f + share * 0.1f);
                });
                t += 0.55f;
                Schedule(t, () => HidePopup(false));
                t += 0.2f;
                // a Vice that just levelled up gets its moment
                foreach (var name in shown.LevelUps)
                {
                    var vice = run.Gambler.Vices.FirstOrDefault(v => v.Name == name);
                    int level = vice?.Level ?? 2;
                    Schedule(t, () =>
                    {
                        Banner($"{name.ToUpperInvariant()}  LEVEL {level}", $"Its effect is now {(level - 1) * Balance.ViceLevelBoost:P0} stronger", Ui.Gold);
                        Play(_cleared);
                        if (vice != null && ViceRow?.Invoke(vice) is VisualElement row && row.panel != null)
                            _particles.Burst(_root.WorldToLocal(row.worldBound).center, 30, 300);
                    });
                    t += 1.1f;
                }
            }

            // then the cards act out the result and the pot slides across
            Schedule(t, () =>
            {
                CommitScore?.Invoke();
                ActOut?.Invoke();
                var best = table.LastScores.OrderByDescending(sc => sc.Total).First();
                if (grand <= 0)
                {
                    Play(best.Outcome == Outcome.Bust ? _bust : best.Outcome == Outcome.Push ? _win : _lose);
                    if (best.Outcome != Outcome.Push)
                    {
                        _shake = 0.12f;
                        Flash(new Color(0.9f, 0.1f, 0.1f), 0.3f, 0.07f);
                    }
                }
                if (DealerAnchor == null || PlayerAnchor == null || run.Shown() != table) return;
                var dealer = _root.WorldToLocal(DealerAnchor());
                var player = _root.WorldToLocal(PlayerAnchor());
                var chipColor = new[] { new Color(0.8f, 0.14f, 0.16f), new Color(0.13f, 0.5f, 0.3f), new Color(0.13f, 0.13f, 0.15f) };
                var color = chipColor[table.DealsPlayed % chipColor.Length];
                bool won = table.LastScores.Any(sc => sc.Outcome.IsWin());
                bool lost = table.LastScores.All(sc => sc.Outcome == Outcome.Lose || sc.Outcome == Outcome.Bust);
                if (won) _particles.Chips(dealer, player, Mathf.Clamp(4 + (int)(share * 12), 4, 14), color, Clink, 0.1f);
                else if (lost) _particles.Chips(player, dealer, 4, color, Clink, 0.1f);
            });
            return t + 0.8f;
        }

        private void Schedule(float at, Action act)
        {
            int i = _timeline.Count;
            while (i > 0 && _timeline[i - 1].at > at) i--;
            _timeline.Insert(i, (at, act));
        }

        // finish the playback at once: click or Space while it runs
        public void Skip()
        {
            if (_timeline.Count == 0) return;
            _skipping = true;
            while (_timeline.Count > 0)
            {
                var (_, act) = _timeline[0];
                _timeline.RemoveAt(0);
                act();
            }
            _skipping = false;
            _totalT = 1;
            HidePopup(true);
        }

        private void ShowPopup(bool rays)
        {
            if (_skipping) return;
            _chips.text = "0";
            _mult.text = "0";
            _total.text = "";
            _totalT = 1;
            Ui.Show(_popup, true);
            Ui.Transition(_popup, 0, "opacity", "translate");
            _popup.style.opacity = 1;
            _popup.style.translate = new Translate(0, 0);
            _popupShown = true;
            _raysOn = rays;
            Ui.Pop(_chipsBox, 1.4f);
            Ui.Pop(_multBox, 1.4f, 80);
        }

        private void HidePopup(bool now)
        {
            _popupShown = false;
            _raysOn = false;
            if (now)
            {
                Ui.Show(_popup, false);
                return;
            }
            Ui.Transition(_popup, 0.3f, "opacity", "translate");
            _popup.style.opacity = 0;
            _popup.style.translate = new Translate(0, -30);
        }

        private void SetChips(int chips)
        {
            if (_skipping) return;
            _chips.text = chips.ToString("N0");
            Ui.Pop(_chipsBox, 1.18f);
            if (!Settings.ReducedMotion) _particles.Sparks(Center(_chipsBox), Center(_chipsBox), Ui.Chips, 7, 160);
        }

        private void SetMult(double mult)
        {
            if (_skipping) return;
            _mult.text = mult.ToString("0.##");
            Ui.Pop(_multBox, 1.25f);
            if (!Settings.ReducedMotion) _particles.Sparks(Center(_multBox), Center(_multBox), Ui.Mult, 8, 170);
        }

        private Vector2 Center(VisualElement e) => _root.WorldToLocal(e.worldBound.center);

        // pixels fly from whatever scored into the box it fed: blue into Chips, red into Mult, gold for x Mult
        private void Feed(Vector2 from, ScoreStep step)
        {
            if (_skipping || Settings.ReducedMotion) return;
            if (step.ChipsAdded > 0) _particles.Sparks(from, Center(_chipsBox), Ui.Chips, Mathf.Clamp(step.ChipsAdded / 3, 6, 18));
            if (step.Multiplied) _particles.Sparks(from, Center(_multBox), Ui.Gold, 22, 320);
            else if (step.MultAdded > 0) _particles.Sparks(from, Center(_multBox), Ui.Mult, Mathf.Clamp((int)(step.MultAdded * 3), 6, 18));
        }

        // each beat of scoring rings a little higher than the last
        private void Tick(int beat)
        {
            if (_skipping || Muted) return;
            _audio.pitch = Mathf.Min(2.2f, 1 + beat * 0.08f);
            _audio.PlayOneShot(_tick);
        }

        // "FLUSH!" in big letters, with what it added underneath
        private void Banner(string title, string detail, Color color)
        {
            var box = Ui.Column(_callouts, 2);
            box.style.position = Position.Absolute;
            // in the open middle of the felt, between the dealer's cards and yours
            box.style.left = TableCenterX;
            box.style.top = BannerTop;
            box.style.alignItems = Align.Center;
            box.style.translate = new Translate(Length.Percent(-50), 0);
            box.pickingMode = PickingMode.Ignore;
            var head = Ui.Title(box, title, 38, color);
            head.style.textShadow = new TextShadow { offset = new Vector2(0, 4), blurRadius = 16, color = new Color(0, 0, 0, 0.95f) };
            var line = Ui.Text(box, detail, 18, Ui.Cream, Weight.Heavy);
            line.style.backgroundColor = new Color(0.03f, 0.04f, 0.05f, 0.9f);
            Ui.Rounded(line, 10);
            Ui.Padding(line, 3, 10);
            foreach (var e in box.Query<VisualElement>().ToList()) e.pickingMode = PickingMode.Ignore;
            Ui.Pop(box, 1.8f);
            _particles.Burst(new Vector2(TableCenterX, BannerTop + 30), 18, 380);
            float life = 0.8f / TableView.Speed;
            box.schedule.Execute(() =>
            {
                Ui.Transition(box, 0.3f, "opacity", "top");
                box.style.opacity = 0;
                box.style.top = BannerTop - 25;
            }).StartingIn((long)(life * 1000));
            box.schedule.Execute(box.RemoveFromHierarchy).StartingIn((long)(life * 1000) + 350);
        }

        // a little "+4 Mult" that rises from whatever caused it
        private void Callout(ScoreStep step, int hand)
        {
            if (_skipping) return;
            var parts = new List<string>();
            if (step.ChipsAdded != 0) parts.Add(Ui.Colored($"{(step.ChipsAdded > 0 ? "+" : "")}{step.ChipsAdded} Chips", Ui.Chips));
            if (step.Multiplied) parts.Add(Ui.Colored($"×{step.Factor:0.##} Mult", Ui.Mult));
            else if (step.MultAdded != 0) parts.Add(Ui.Colored($"{(step.MultAdded > 0 ? "+" : "")}{step.MultAdded:0.##} Mult", Ui.Mult));
            if (parts.Count == 0) return;

            // rare hands get the fanfare
            if (step.Kind == ScoreSource.Feat)
            {
                Feed(new Vector2(TableCenterX, BannerTop + 30), step);
                Banner(step.Name.ToUpperInvariant() + "!", string.Join("  ", parts), Ui.Gold);
                Play(_fanfare, steady: true);
                _particles.Burst(new Vector2(TableCenterX, 250), 80, 700);
                Flash(new Color(0.98f, 0.79f, 0.32f), 0.5f, 0.16f);
                return;
            }
            if (step.Kind == ScoreSource.Raise)
            {
                Feed(new Vector2(TableCenterX, BannerTop + 30), step);
                Banner("THE RAISE PAYS!", string.Join("  ", parts), Ui.Gold);
                return;
            }

            // combos and streaks get a big banner in the middle of the table
            if (step.Kind == ScoreSource.Combo || step.Kind == ScoreSource.Streak)
            {
                Feed(new Vector2(TableCenterX, BannerTop + 30), step);
                Banner(step.Kind == ScoreSource.Combo ? step.Name.ToUpperInvariant() + "!" : "STREAK!", string.Join("  ", parts),
                    step.Kind == ScoreSource.Combo ? Ui.Cocktail : Ui.HeatColor);
                return;
            }

            Vector2 at;
            string name = step.Name;
            if (step.Kind == ScoreSource.Card && CardAnchor?.Invoke(hand, step.Card) is Vector2 card)
            {
                at = _root.WorldToLocal(card) + new Vector2(0, -78);
                name = null;
            }
            else if (step.Kind == ScoreSource.Vice && ViceRow?.Invoke(step.Source) is VisualElement row && row.panel != null)
            {
                var bounds = _root.WorldToLocal(row.worldBound);
                at = new Vector2(bounds.xMax + 110, Mathf.Clamp(bounds.center.y, 40, _root.layout.height - 40));
                Ui.Pop(row, 1.07f);
                // a quick wiggle, like a Joker firing
                float[] tilt = { -4, 4, -2, 0 };
                for (int i = 0; i < tilt.Length; i++)
                {
                    float angle = tilt[i];
                    row.schedule.Execute(() =>
                    {
                        Ui.Transition(row, 0.05f, "rotate");
                        row.style.rotate = new Rotate(angle);
                    }).StartingIn(60 * i);
                }
                var before = row.style.backgroundColor;
                row.style.backgroundColor = new Color(0.98f, 0.79f, 0.32f, 0.3f);
                row.schedule.Execute(() => row.style.backgroundColor = before).StartingIn(450);
            }
            // no particular source: beside the score popup, clear of the cards
            else
            {
                // several in a row stack down instead of landing on each other
                at = new Vector2(TableCenterX - 270, 320 + 44 * (_calloutRow++ % 3));
            }

            Feed(at, step);
            var label = Ui.Text(_callouts, (name != null ? name + "  " : "") + string.Join("  ", parts), 18, Ui.Cream, Weight.Heavy);
            label.style.position = Position.Absolute;
            label.style.backgroundColor = new Color(0.03f, 0.04f, 0.05f, 0.9f);
            Ui.Rounded(label, 10);
            Ui.Padding(label, 4, 10);
            Ui.Border(label, 1, new Color(1, 1, 1, 0.15f));
            label.pickingMode = PickingMode.Ignore;
            label.style.left = at.x;
            label.style.top = at.y;
            // centered on the point once it has a size
            label.style.translate = new Translate(Length.Percent(-50), Length.Percent(-50));
            Ui.Pop(label, 1.3f);
            float life = 0.9f / TableView.Speed;
            label.schedule.Execute(() =>
            {
                Ui.Transition(label, 0.35f, "opacity", "top");
                label.style.opacity = 0;
                label.style.top = at.y - 26;
            }).StartingIn((long)(life * 1000));
            label.schedule.Execute(label.RemoveFromHierarchy).StartingIn((long)(life * 1000) + 400);
        }

        private void Clink()
        {
            if (_clinkCooldown > 0) return;
            _clinkCooldown = 0.05f;
            Play(_clink);
        }

        // bursts all over the screen for a few seconds
        private void Fireworks()
        {
            Play(_fanfare, steady: true);
            var random = new System.Random();
            for (int i = 0; i < 9; i++)
            {
                float at = i * 0.45f;
                var spot = new Vector2((float)(0.15 + random.NextDouble() * 0.7) * _root.layout.width, (float)(0.15 + random.NextDouble() * 0.4) * _root.layout.height);
                StartCoroutine(After(at, () =>
                {
                    _particles.Burst(spot, 45, 560);
                    _particles.Coins(spot, 10, 380);
                    Play(_boom);
                }));
            }
        }

        private static System.Collections.IEnumerator After(float seconds, Action action)
        {
            yield return new WaitForSecondsRealtime(seconds);
            action();
        }

        private const float PopupWidth = 300, PopupTop = 290, BannerTop = 310;
        private int _calloutRow;

        private float TableCenterX => Hud.Width + (_root.layout.width - Hud.Width) / 2f;

        private void Flash(Color color, float seconds, float strength, bool pulse = false)
        {
            if (Settings.ReducedMotion) return;
            _flashColor = color;
            _flashTime = seconds;
            _flashStrength = strength;
            _flashPulse = pulse;
        }

        private bool _flashPulse;

        public bool Muted
        {
            get => PlayerPrefs.GetInt("muted", 0) == 1;
            set
            {
                PlayerPrefs.SetInt("muted", value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        // steady keeps the pitch, for melodies
        private void Play(AudioClip clip, bool steady = false)
        {
            if (clip == null || Muted) return;
            _audio.pitch = steady ? 1 : UnityEngine.Random.Range(0.94f, 1.06f);
            _audio.PlayOneShot(clip);
        }

        private void Update()
        {
            _clinkCooldown -= Time.deltaTime;
            if (_timeline.Count > 0)
            {
                _clock += Time.deltaTime * TableView.Speed;
                while (_timeline.Count > 0 && _timeline[0].at <= _clock)
                {
                    var (_, act) = _timeline[0];
                    _timeline.RemoveAt(0);
                    act();
                }
            }
            if (_totalT < 1)
            {
                _totalT = Mathf.Min(1, _totalT + Time.deltaTime * TableView.Speed / 0.55f);
                _total.text = "+" + ((long)(_totalTo * (1 - Mathf.Pow(1 - _totalT, 3)))).ToString("N0");
            }

            float raysTarget = _raysOn && _popupShown ? 1 : 0;
            float raysAlpha = Mathf.MoveTowards(_rays.resolvedStyle.opacity, raysTarget, Time.deltaTime * 3);
            _rays.style.opacity = raysAlpha;
            if (raysAlpha > 0)
            {
                _rays.style.left = _root.layout.width - 16 - PopupWidth / 2 - 310;
                _rays.style.rotate = new Rotate(Time.time * 25);
            }

            if (_shake > 0 && Settings.ReducedMotion) _shake = 0;
            if (_shake > 0)
            {
                _shake -= Time.deltaTime;
                float strength = Mathf.Max(0, _shake) * 30;
                _root.style.translate = new Translate(UnityEngine.Random.Range(-strength, strength), UnityEngine.Random.Range(-strength, strength));
                if (_shake <= 0) _root.style.translate = new Translate(0, 0);
            }

            if (_flashTime > 0)
            {
                _flashTime -= Time.deltaTime;
                // the security alarm pulses, everything else is a soft glow that fades
                float pulse = _flashPulse && _flashTime > 0.5f ? Mathf.Abs(Mathf.Sin(_flashTime * 10)) : 1;
                float alpha = _flashStrength * pulse * Mathf.Clamp01(_flashTime / 0.3f);
                _flash.style.backgroundColor = new Color(_flashColor.r, _flashColor.g, _flashColor.b, alpha);
                if (_flashTime <= 0) _flash.style.backgroundColor = new Color(0, 0, 0, 0);
            }
        }
    }

    // tiny procedural sounds, so the game ships without audio files
    public static class Synth
    {
        private const int Rate = 22050;

        public static AudioClip Tones(float[] notes, float noteLength, float volume)
        {
            int perNote = (int)(Rate * noteLength);
            var data = new float[perNote * notes.Length];
            for (int n = 0; n < notes.Length; n++)
                for (int i = 0; i < perNote; i++)
                {
                    float t = i / (float)Rate;
                    float envelope = Mathf.Exp(-6f * i / perNote);
                    data[n * perNote + i] = Mathf.Sin(2 * Mathf.PI * notes[n] * t) * envelope * volume;
                }
            return Clip("tones", data);
        }

        // a buzzy brass tone with vibrato: trombones and fanfares
        public static AudioClip Brass(float[] notes, float[] lengths, float volume)
        {
            int total = 0;
            foreach (float l in lengths) total += (int)(Rate * l);
            var data = new float[total];
            int offset = 0;
            for (int n = 0; n < notes.Length; n++)
            {
                int count = (int)(Rate * lengths[n]);
                float phase = 0;
                for (int i = 0; i < count; i++)
                {
                    float t = i / (float)Rate;
                    // vibrato grows on long notes
                    float vibrato = 1 + 0.012f * Mathf.Sin(2 * Mathf.PI * 5.5f * t) * Mathf.Clamp01(t / 0.3f);
                    phase += 2 * Mathf.PI * notes[n] * vibrato / Rate;
                    float tone = 0;
                    for (int h = 1; h <= 6; h++) tone += Mathf.Sin(phase * h) / h;
                    float env = Mathf.Min(1, t / 0.03f) * Mathf.Min(1, (lengths[n] - t) / 0.06f);
                    data[offset + i] = tone * env * volume * 0.6f;
                }
                offset += count;
            }
            return Clip("brass", data);
        }

        // a low thump with a tail of noise, for fireworks
        public static AudioClip Boom(float length, float volume)
        {
            int count = (int)(Rate * length);
            var data = new float[count];
            var random = new System.Random(3);
            float smooth = 0;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)Rate;
                smooth += ((float)(random.NextDouble() * 2 - 1) - smooth) * 0.08f;
                data[i] = (Mathf.Sin(2 * Mathf.PI * (60 + 80 * Mathf.Exp(-t / 0.05f)) * t) * Mathf.Exp(-t / 0.15f) + smooth * 2 * Mathf.Exp(-t / 0.3f)) * volume;
            }
            return Clip("boom", data);
        }

        public static AudioClip Noise(float length, float volume)
        {
            int count = (int)(Rate * length);
            var data = new float[count];
            var random = new System.Random(7);
            for (int i = 0; i < count; i++)
                data[i] = (float)(random.NextDouble() * 2 - 1) * volume * (1 - i / (float)count);
            return Clip("noise", data);
        }

        public static AudioClip Siren(float length, float volume)
        {
            int count = (int)(Rate * length);
            var data = new float[count];
            float phase = 0;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)Rate;
                float frequency = 600 + 300 * Mathf.Sin(2 * Mathf.PI * 2.5f * t);
                phase += 2 * Mathf.PI * frequency / Rate;
                data[i] = Mathf.Sign(Mathf.Sin(phase)) * volume * 0.5f * (1 - t / length);
            }
            return Clip("siren", data);
        }

        private static AudioClip Clip(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
