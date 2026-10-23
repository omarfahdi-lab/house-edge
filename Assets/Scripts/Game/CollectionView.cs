using System.Linq;
using HouseEdge.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace HouseEdge.Game
{
    // everything you've found across runs: Vices, bosses and combos, with what's still locked
    public sealed class CollectionView
    {
        private static readonly string[] Tabs = { "Vices", "Bosses", "Combos", "Feats" };

        private readonly VisualElement _root, _tabs, _page;
        private readonly Label _progress;
        private Profile _profile;
        private int _tab;

        public bool IsOpen => _root.style.display == DisplayStyle.Flex;

        public CollectionView(VisualElement parent)
        {
            _root = Ui.Column(parent, 10);
            Ui.Fill(_root);
            _root.style.backgroundColor = Ui.FeltDark;
            Ui.Stretch(_root, Ui.FeltTexture());
            _root.style.alignItems = Align.Center;
            Ui.Padding(_root, 18, 24);

            var head = Ui.Row(_root, 16);
            Ui.Title(head, "COLLECTION", 34, Ui.Gold);
            _progress = Ui.Text(head, "", 14, Ui.Cream, Weight.Medium);
            _tabs = Ui.Row(_root, 8);
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            scroll.style.alignSelf = Align.Stretch;
            scroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.contentContainer.style.flexShrink = 0;
            _root.Add(scroll);
            _page = Ui.Row(null);
            _page.style.flexWrap = Wrap.Wrap;
            _page.style.justifyContent = Justify.Center;
            scroll.Add(_page);
            Ui.Button(_root, "Close", Close, ButtonKind.Normal, "Esc");
            // no theme stylesheet: only the scrolling page may shrink
            foreach (var child in _root.Children()) if (child != scroll) child.style.flexShrink = 0;
            Ui.Show(_root, false);
        }

        public void Open(Profile profile)
        {
            _profile = profile;
            Show(_tab);
            Ui.Show(_root, true);
            Ui.Enter(_root, 16, 0, 0.25f);
        }

        public void Close() => Ui.Show(_root, false);

        private void Show(int tab)
        {
            _tab = tab;
            _tabs.Clear();
            for (int i = 0; i < Tabs.Length; i++)
            {
                int index = i;
                Ui.Button(_tabs, Tabs[i], () => Show(index), i == tab ? ButtonKind.Gold : ButtonKind.Ghost).style.height = 36;
            }
            int seen = ViceCatalog.All.Count(v => _profile.HasSeen(v));
            var bosses = Bosses.PitBosses.Concat(new[] { Bosses.TheHouse, Bosses.EyeInTheSky, Bosses.Enforcer }).ToList();
            int faced = bosses.Count(b => _profile.HasFaced(b.Name));
            int feats = Feats.All.Count(f => _profile.FeatsHit(f) > 0);
            _progress.text = $"Vices found {seen}/{ViceCatalog.All.Count}   ·   Bosses faced {faced}/{bosses.Count}   ·   Combos won {Combos.All.Sum(c => _profile.CombosWon(c)):N0}   ·   Feats {feats}/{Feats.All.Count}";

            _page.Clear();
            if (tab == 0) foreach (var vice in ViceCatalog.All) _page.Add(ViceTile(vice));
            else if (tab == 1) foreach (var boss in bosses) _page.Add(BossTile(boss));
            else if (tab == 2) foreach (var combo in Combos.All) _page.Add(ComboTile(combo));
            else foreach (var feat in Feats.All) _page.Add(FeatTile(feat));
            int n = 0;
            foreach (var child in _page.Children()) Ui.Enter(child, 14, 12 * n++, 0.25f);
        }

        private VisualElement ViceTile(ViceDefinition vice)
        {
            bool unlocked = _profile.IsUnlocked(vice);
            bool seen = _profile.HasSeen(vice);
            VisualElement tile;
            if (unlocked && seen)
            {
                tile = ViceCardView.Create(vice, 176);
                tile.style.minHeight = 130;
            }
            else
            {
                // a dark card: locked ones say how to unlock, unseen ones keep their secret
                tile = Ui.Column(null, 6);
                tile.style.width = 176;
                tile.style.minHeight = 130;
                tile.style.backgroundColor = new Color(0.05f, 0.05f, 0.06f, 0.95f);
                Ui.Rounded(tile, 12);
                Ui.Border(tile, 2, new Color(1, 1, 1, 0.12f));
                Ui.Padding(tile, 12, 12);
                tile.style.alignItems = Align.Center;
                tile.style.justifyContent = Justify.Center;
                Ui.Add(tile, new SuitIcon(unlocked ? SuitIcon.Shape.Star : SuitIcon.Shape.Lock, Ui.Dim, 26));
                Ui.Text(tile, unlocked ? "???" : vice.Name, 15, Ui.Cream, Weight.Heavy);
                var hint = Ui.Body(tile, unlocked ? "Not found yet. Keep playing." : "Locked: " + Profile.UnlockHint(vice), 12, unlocked ? Ui.Dim : Ui.Gold, center: true);
                hint.style.unityTextAlign = TextAnchor.MiddleCenter;
            }
            Ui.Margin(tile, 5);
            return tile;
        }

        private VisualElement BossTile(BossDefinition boss)
        {
            bool faced = _profile.HasFaced(boss.Name);
            var tile = Ui.Column(null, 6);
            tile.style.width = 190;
            tile.style.alignItems = Align.Center;
            Ui.Padding(tile, 10, 10);
            Ui.Margin(tile, 6);
            tile.style.backgroundColor = Ui.Panel;
            Ui.Rounded(tile, 12);
            Ui.Border(tile, 1, faced ? Ui.Hairline : new Color(1, 1, 1, 0.08f));
            var face = new BossPortrait(boss.Name, 110);
            if (!faced)
            {
                // a silhouette until you've met them
                face.style.unityBackgroundImageTintColor = Color.black;
                face.style.opacity = 0.18f;
            }
            tile.Add(face);
            Ui.Text(tile, faced ? boss.Name : "???", 15, faced ? Ui.Gold : Ui.Dim, Weight.Heavy);
            if (faced)
            {
                Ui.Pill(tile, boss.Ability.ToUpperInvariant(), Ui.Bad, size: 10);
                Ui.Body(tile, boss.Twist, 12, Ui.Cream, center: true);
                if (boss.Rage != null) Ui.Body(tile, "Phase two: " + boss.Rage, 11, new Color(1f, 0.7f, 0.65f), center: true);
            }
            else Ui.Text(tile, "Not faced yet", 12, Ui.Dim);
            return tile;
        }

        // rare named hands: hidden until you've made one
        private VisualElement FeatTile(Feat feat)
        {
            long hit = _profile.FeatsHit(feat);
            var tile = Ui.Column(null, 4);
            tile.style.width = 220;
            Ui.Padding(tile, 12, 14);
            Ui.Margin(tile, 6);
            tile.style.backgroundColor = Ui.Panel;
            Ui.Rounded(tile, 12);
            Ui.Border(tile, 1, hit > 0 ? Ui.Gold : new Color(1, 1, 1, 0.08f));
            var head = Ui.Row(tile, 6);
            Ui.Add(head, new SuitIcon(hit > 0 ? SuitIcon.Shape.Crown : SuitIcon.Shape.Lock, hit > 0 ? Ui.Gold : Ui.Dim, 16));
            Ui.Text(head, Feats.Name(feat), 15, hit > 0 ? Ui.Gold : Ui.Cream, Weight.Heavy);
            Ui.Body(tile, Feats.Describe(feat), 13, Ui.Cream);
            Ui.Text(tile, $"x{Feats.Factor(feat):0.#} Mult on a winning hand", 12, Ui.Mult, Weight.Bold);
            Ui.Text(tile, hit > 0 ? $"Made it {hit:N0} times" : "Not made yet", 12, hit > 0 ? Ui.Good : Ui.Dim, Weight.Medium);
            return tile;
        }

        private VisualElement ComboTile(Combo combo)
        {
            long won = _profile.CombosWon(combo);
            var (chips, mult) = Combos.Base(combo);
            var tile = Ui.Column(null, 4);
            tile.style.width = 220;
            Ui.Padding(tile, 12, 14);
            Ui.Margin(tile, 6);
            tile.style.backgroundColor = Ui.Panel;
            Ui.Rounded(tile, 12);
            Ui.Border(tile, 1, won > 0 ? new Color(0.78f, 0.5f, 1f, 0.6f) : new Color(1, 1, 1, 0.08f));
            Ui.Pill(tile, Combos.Name(combo).ToUpperInvariant(), Ui.Cocktail, filled: won > 0, size: 12).style.alignSelf = Align.FlexStart;
            Ui.Body(tile, Combos.Describe(combo), 13, Ui.Cream);
            Ui.Text(tile, $"+{chips} Chips  +{mult} Mult", 12, Ui.Gold, Weight.Bold);
            Ui.Text(tile, won > 0 ? $"Won with it {won:N0} times" : "Never won with it yet", 12, won > 0 ? Ui.Good : Ui.Dim, Weight.Medium);
            return tile;
        }
    }
}
