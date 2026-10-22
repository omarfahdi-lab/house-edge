using HouseEdge.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace HouseEdge.Game
{
    // a Vice drawn as a card: rarity band, name, archetype and effect text
    public static class ViceCardView
    {
        public static Color RarityColor(Rarity rarity) => rarity switch
        {
            Rarity.Common => new Color(0.6f, 0.7f, 0.8f),
            Rarity.Uncommon => new Color(0.36f, 0.8f, 0.48f),
            Rarity.Rare => new Color(0.92f, 0.36f, 0.4f),
            Rarity.Cursed => new Color(0.72f, 0.45f, 1f),
            _ => new Color(0.98f, 0.79f, 0.32f),
        };

        public static Color ArchetypeColor(Archetype archetype) => archetype switch
        {
            Archetype.Lucky => new Color(0.45f, 0.9f, 0.5f),
            Archetype.Risky => new Color(1f, 0.5f, 0.35f),
            Archetype.Cheater => new Color(0.75f, 0.55f, 1f),
            Archetype.Greedy => new Color(0.98f, 0.79f, 0.32f),
            Archetype.Steady => new Color(0.45f, 0.75f, 1f),
            _ => new Color(1f, 0.45f, 0.75f),
        };

        public static VisualElement Create(ViceDefinition vice, float width, string status = null)
        {
            var rarity = RarityColor(vice.Rarity);
            var card = Ui.Column(null, 7);
            card.style.width = width;
            card.style.backgroundColor = new Color(0.07f, 0.08f, 0.09f, 0.97f);
            card.style.backgroundImage = Ui.Gradient(new Color(0.14f, 0.15f, 0.17f), new Color(0.05f, 0.06f, 0.07f));
            card.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
            Ui.Rounded(card, 12);
            Ui.Border(card, 2, rarity);
            card.style.borderTopWidth = 5;
            Ui.Padding(card, 11, 13);

            var name = Ui.Text(card, vice.Name, 18, Ui.Cream, Weight.Heavy);
            name.style.whiteSpace = WhiteSpace.Normal;
            var tags = Ui.Row(card, 5);
            Ui.Pill(tags, vice.Rarity.ToString().ToUpperInvariant(), rarity, size: 10);
            Ui.Pill(tags, vice.Archetype.ToString().ToUpperInvariant(), ArchetypeColor(vice.Archetype), size: 10);

            var text = Ui.Body(card, vice.Description, 14, new Color(0.9f, 0.92f, 0.88f));
            text.style.flexGrow = 1;
            if (!string.IsNullOrEmpty(status)) Ui.Text(card, status, 13, Ui.Gold, Weight.Bold);
            Ui.Tip(card, $"{vice.Archetype} set bonus", $"Hold 3 {vice.Archetype} Vices: {ArchetypeBonuses.Describe(vice.Archetype)}");
            return card;
        }
    }
}
