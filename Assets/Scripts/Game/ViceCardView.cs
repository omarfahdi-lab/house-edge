using HouseEdge.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace HouseEdge.Game
{
    // a Vice drawn as a card: rarity stripe, name, archetype and effect text
    public static class ViceCardView
    {
        public static Color RarityColor(Rarity rarity) => rarity switch
        {
            Rarity.Common => new Color(0.55f, 0.66f, 0.78f),
            Rarity.Uncommon => new Color(0.36f, 0.78f, 0.47f),
            Rarity.Rare => new Color(0.86f, 0.33f, 0.36f),
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
            var card = Ui.Column(null, 6);
            card.style.width = width;
            card.style.backgroundColor = new Color(0.1f, 0.1f, 0.12f, 0.95f);
            Ui.Rounded(card, 10);
            Ui.Border(card, 2, RarityColor(vice.Rarity));
            Ui.Padding(card, 10, 12);

            var name = Ui.Text(card, vice.Name, 18, Ui.Cream, bold: true);
            name.style.whiteSpace = WhiteSpace.Normal;
            var tags = Ui.Row(card, 8);
            Ui.Text(tags, vice.Rarity.ToString().ToUpperInvariant(), 11, RarityColor(vice.Rarity), bold: true);
            Ui.Text(tags, vice.Archetype.ToString().ToUpperInvariant(), 11, ArchetypeColor(vice.Archetype), bold: true);

            var text = Ui.Text(card, vice.Description, 14, Ui.Dim);
            text.style.whiteSpace = WhiteSpace.Normal;
            if (!string.IsNullOrEmpty(status)) Ui.Text(card, status, 13, Ui.Gold, bold: true);
            return card;
        }
    }
}
