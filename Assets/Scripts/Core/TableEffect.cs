namespace HouseEdge.Core
{
    // base for Vices, enhancements, stamps, curses and bosses. override only the hooks you need.
    // the table calls every active effect in list order, so a new effect never touches the core loop
    public abstract class TableEffect
    {
        public virtual bool IsActive => true;

        // the table is set up but nothing is dealt yet. change Hands, Burns or the rules here
        public virtual void OnTableStart(Table table) { }
        public virtual void OnCardDrawn(Table table, Card card, bool toDealer) { }
        public virtual void OnHit(Table table, PlayerHand hand) { }
        public virtual void OnStand(Table table, PlayerHand hand) { }
        // a hand busted for good (no Burn used). an effect can still rescue it
        public virtual void OnBust(Table table, PlayerHand hand) { }
        public virtual void OnDealerBust(Table table) { }
        public virtual void OnHandScored(Table table, HandScore score) { }
        public virtual void OnTableEnd(Table table, bool won) { }
    }
}
