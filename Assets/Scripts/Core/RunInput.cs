using System.Collections.Generic;

namespace HouseEdge.Core
{
    public enum RunInputKind
    {
        // a blackjack action at the current table
        Table,
        // buy shop offer Index
        Buy,
        SellVice,
        // move the Vice at Index to position Index2
        MoveVice,
        Reroll,
        LeaveShop,
        // drink the Cocktail in slot Index, on deck cards Index2 and Index3 if it needs them
        UseCocktail,
        // take boss reward Index, or skip it
        PickReward,
        SkipReward,
        // floor map: pick room Index of the two offered
        ChooseRoom,
        // take room option Index, on deck card Index2 if it needs one
        RoomOption,
        LeaveRoom,
        // pay the Loan Shark as much as you can
        RepayDebt,
        // keep card Index from the pack you just opened, or none
        PickPackCard,
        SkipPack,
        // play the trick card in slot Index at the current hand
        PlayTrick,
    }

    // one player input in a run. a run is its seed plus a list of these
    public readonly struct RunInput
    {
        public RunInputKind Kind { get; }
        public PlayerAction Action { get; }
        public int Index { get; }
        public int Index2 { get; }
        public int Index3 { get; }

        private RunInput(RunInputKind kind, PlayerAction action = default, int index = -1, int index2 = -1, int index3 = -1)
        {
            Kind = kind;
            Action = action;
            Index = index;
            Index2 = index2;
            Index3 = index3;
        }

        public static RunInput Table(PlayerAction action) => new RunInput(RunInputKind.Table, action);
        public static RunInput Buy(int offer) => new RunInput(RunInputKind.Buy, index: offer);
        public static RunInput SellVice(int slot) => new RunInput(RunInputKind.SellVice, index: slot);
        public static RunInput MoveVice(int from, int to) => new RunInput(RunInputKind.MoveVice, index: from, index2: to);
        public static RunInput Reroll() => new RunInput(RunInputKind.Reroll);
        public static RunInput LeaveShop() => new RunInput(RunInputKind.LeaveShop);
        public static RunInput UseCocktail(int slot, int card = -1, int card2 = -1) =>
            new RunInput(RunInputKind.UseCocktail, index: slot, index2: card, index3: card2);
        public static RunInput PickReward(int index) => new RunInput(RunInputKind.PickReward, index: index);
        public static RunInput SkipReward() => new RunInput(RunInputKind.SkipReward);
        public static RunInput ChooseRoom(int index) => new RunInput(RunInputKind.ChooseRoom, index: index);
        public static RunInput RoomOption(int option, int card = -1) => new RunInput(RunInputKind.RoomOption, index: option, index2: card);
        public static RunInput LeaveRoom() => new RunInput(RunInputKind.LeaveRoom);
        public static RunInput RepayDebt() => new RunInput(RunInputKind.RepayDebt);
        public static RunInput PickPackCard(int index) => new RunInput(RunInputKind.PickPackCard, index: index);
        public static RunInput SkipPack() => new RunInput(RunInputKind.SkipPack);
        // target: which of your cards it works on (Steal, Nudge); -1 for none or your last card
        public static RunInput PlayTrick(int slot, int target = -1) => new RunInput(RunInputKind.PlayTrick, index: slot, index2: target);

        // deck cards picked for a Cocktail, in order
        public IEnumerable<int> CardTargets()
        {
            if (Index2 >= 0) yield return Index2;
            if (Index3 >= 0) yield return Index3;
        }

        public override string ToString() => Kind == RunInputKind.Table ? Action.ToString()
            : Index3 >= 0 ? $"{Kind}({Index},{Index2},{Index3})"
            : Index2 >= 0 ? $"{Kind}({Index},{Index2})"
            : Index >= 0 ? $"{Kind}({Index})"
            : Kind.ToString();
    }
}
