namespace HouseEdge.Core
{
    // what an enhancement, stamp or curse does. one small class each, looked up by CardEffects.For
    public abstract class CardEffect
    {
        // toPlayer is false when the dealer drew it
        public virtual void OnDrawn(Table table, Card card, bool toPlayer) { }
        public virtual void OnScored(Table table, HandScore score, Card card) { }
    }

    public static class CardEffects
    {
        public static CardEffect For(Enhancement enhancement) => enhancement switch
        {
            Enhancement.Gilded => Gilded.Instance,
            Enhancement.Fragile => Fragile.Instance,
            Enhancement.Marked => Marked.Instance,
            Enhancement.Ghost => Ghost.Instance,
            Enhancement.Hot => Hot.Instance,
            Enhancement.Heavy => Heavy.Instance,
            // Flex only changes the card's value, see HandValue
            _ => null,
        };

        public static CardEffect For(Stamp stamp) => stamp switch
        {
            Stamp.Blue => BlueStamp.Instance,
            Stamp.Gold => GoldStamp.Instance,
            // Red is handled by the scorer (score twice), Black by the dealer's draw
            _ => null,
        };

        public static CardEffect For(Curse curse) => curse switch
        {
            Curse.DeadCard => DeadCard.Instance,
            Curse.SnakeEyes => SnakeEyes.Instance,
            Curse.TaxCard => TaxCard.Instance,
            Curse.Jinx => Jinx.Instance,
            _ => null,
        };

        public static string Describe(Enhancement enhancement) => enhancement switch
        {
            Enhancement.Gilded => "+3 gold when it's part of a winning hand.",
            Enhancement.Flex => "Counts as any value from 1 to 11, picked to get you closest to 21.",
            Enhancement.Fragile => "x2 Mult. Breaks after it's been in 3 winning hands.",
            Enhancement.Marked => "You see it when it's the next card. +2 Heat when drawn.",
            Enhancement.Ghost => "Worth 0 but gives +15 Chips. A free hit.",
            Enhancement.Hot => "+1 Mult for every time it's been drawn this table.",
            Enhancement.Heavy => "Counts as 1 higher and gives +20 Chips.",
            _ => "",
        };

        public static string Describe(Stamp stamp) => stamp switch
        {
            Stamp.Red => "The card is scored twice.",
            Stamp.Blue => "Drawing it gives you +1 Burn.",
            Stamp.Gold => "+2 gold when you draw it.",
            Stamp.Black => "The dealer can never draw it.",
            _ => "",
        };

        public static string Describe(Curse curse) => curse switch
        {
            Curse.DeadCard => "Counts as 10 and gives -2 Mult.",
            Curse.SnakeEyes => "Counts as 1 and ends your turn immediately.",
            Curse.TaxCard => "Counts as 2. Lose 2 gold when drawn.",
            Curse.Jinx => "Counts as 2. Disables a random Vice for this hand.",
            _ => "",
        };

        // enhancements

        private sealed class Gilded : CardEffect
        {
            public static readonly Gilded Instance = new Gilded();
            public override void OnScored(Table table, HandScore score, Card card)
            {
                if (score.Outcome.IsWin()) table.Gambler.GainGold(Balance.GildedGold);
            }
        }

        private sealed class Fragile : CardEffect
        {
            public static readonly Fragile Instance = new Fragile();
            public override void OnScored(Table table, HandScore score, Card card)
            {
                score.MultiplyMult(2);
                if (!score.Outcome.IsWin()) return;
                card.FragileWins++;
                if (card.FragileWins >= Balance.FragileBreaksAfter) table.DestroyCard(card);
            }
        }

        private sealed class Marked : CardEffect
        {
            public static readonly Marked Instance = new Marked();
            public override void OnDrawn(Table table, Card card, bool toPlayer) => table.AddHeat(Balance.MarkedHeat);
        }

        private sealed class Ghost : CardEffect
        {
            public static readonly Ghost Instance = new Ghost();
            public override void OnScored(Table table, HandScore score, Card card) => score.AddChips(15);
        }

        private sealed class Hot : CardEffect
        {
            public static readonly Hot Instance = new Hot();
            public override void OnScored(Table table, HandScore score, Card card) => score.AddMult(card.TimesDrawnThisTable);
        }

        private sealed class Heavy : CardEffect
        {
            public static readonly Heavy Instance = new Heavy();
            public override void OnScored(Table table, HandScore score, Card card) => score.AddChips(20);
        }

        // stamps

        private sealed class BlueStamp : CardEffect
        {
            public static readonly BlueStamp Instance = new BlueStamp();
            public override void OnDrawn(Table table, Card card, bool toPlayer)
            {
                if (toPlayer) table.AddBurns(1);
            }
        }

        private sealed class GoldStamp : CardEffect
        {
            public static readonly GoldStamp Instance = new GoldStamp();
            public override void OnDrawn(Table table, Card card, bool toPlayer)
            {
                if (toPlayer) table.Gambler.GainGold(2);
            }
        }

        // curses

        private sealed class DeadCard : CardEffect
        {
            public static readonly DeadCard Instance = new DeadCard();
            public override void OnScored(Table table, HandScore score, Card card)
            {
                if (score.ScoresCards) score.AddMult(-2);
            }
        }

        private sealed class SnakeEyes : CardEffect
        {
            public static readonly SnakeEyes Instance = new SnakeEyes();
            public override void OnDrawn(Table table, Card card, bool toPlayer)
            {
                if (toPlayer) table.EndTurnAfterThisCard();
            }
        }

        private sealed class TaxCard : CardEffect
        {
            public static readonly TaxCard Instance = new TaxCard();
            public override void OnDrawn(Table table, Card card, bool toPlayer)
            {
                if (toPlayer) table.Gambler.LoseGold(2);
            }
        }

        private sealed class Jinx : CardEffect
        {
            public static readonly Jinx Instance = new Jinx();
            public override void OnDrawn(Table table, Card card, bool toPlayer)
            {
                if (toPlayer) table.JinxRandomVice();
            }
        }
    }

    // runs every card's enhancement, stamp and curse. it sits in front of the Vices so cards score first
    internal sealed class CardRules : TableEffect
    {
        public override void OnCardDrawn(Table table, Card card, bool toDealer)
        {
            card.TimesDrawnThisTable++;
            CardEffects.For(card.Enhancement)?.OnDrawn(table, card, !toDealer);
            CardEffects.For(card.Stamp)?.OnDrawn(table, card, !toDealer);
            CardEffects.For(card.Curse)?.OnDrawn(table, card, !toDealer);
        }

        public override void OnHandScored(Table table, HandScore score)
        {
            // left to right through the hand
            var cards = score.Hand.Cards;
            for (int i = 0; i < cards.Count; i++)
            {
                var card = cards[i];
                ScoreCard(table, score, card);
                score.Mark(ScoreSource.Card, card.IsCursed ? card.CurseName : card.Enhancement.ToString(), card, i);
                if (card.Stamp == Stamp.Red)
                {
                    ScoreCardAgain(table, score, card);
                    score.Mark(ScoreSource.Card, "Red Stamp", card, i);
                }
            }
        }

        private static void ScoreCard(Table table, HandScore score, Card card)
        {
            CardEffects.For(card.Enhancement)?.OnScored(table, score, card);
            CardEffects.For(card.Curse)?.OnScored(table, score, card);
        }

        // Red Stamp and Echo: the card's value counts again and its enhancement triggers again
        public static void ScoreCardAgain(Table table, HandScore score, Card card)
        {
            if (score.ScoresCards) score.AddChips(card.Value);
            ScoreCard(table, score, card);
        }
    }
}
