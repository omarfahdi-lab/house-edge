namespace HouseEdge.Core
{
    // base for Vices, enhancements, stamps, curses and bosses. override only the hooks you need.
    // the table calls every effect in list order, so a new effect never touches the core loop
    public abstract class TableEffect
    {
        public virtual void OnCardDrawn(Table table, Card card, bool toDealer) { }
        public virtual void OnHit(Table table, PlayerHand hand) { }
        public virtual void OnStand(Table table, PlayerHand hand) { }
        public virtual void OnDealerBust(Table table) { }
        public virtual void OnHandScored(Table table, HandScore score) { }
        public virtual void OnTableEnd(Table table, bool won) { }
    }
}
