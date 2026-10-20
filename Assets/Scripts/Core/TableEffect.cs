namespace HouseEdge.Core
{
    // base for Vices, enhancements, stamps, curses and bosses. override only the hooks you need.
    // the table calls every active effect in list order, so a new effect never touches the core loop
    public abstract class TableEffect
    {
        public virtual bool IsActive => true;

        // the table is set up but nothing is dealt yet. change Hands, Burns or the rules here
        public virtual void OnTableStart(Table table) { }
        // a deal is about to start (DealsPlayed hasn't counted it yet)
        public virtual void OnBeforeDeal(Table table) { }
        public virtual void OnCardDrawn(Table table, Card card, bool toDealer) { }
        // the opening four cards are out, before insurance and the player's turn
        public virtual void OnDeal(Table table) { }
        public virtual void OnHit(Table table, PlayerHand hand) { }
        public virtual void OnStand(Table table, PlayerHand hand) { }
        public virtual void OnBurn(Table table, PlayerHand hand) { }
        // a hand busted for good (no Burn used). an effect can still rescue it
        public virtual void OnBust(Table table, PlayerHand hand) { }
        public virtual void OnDealerBust(Table table) { }
        public virtual void OnHandScored(Table table, HandScore score) { }
        // every hand of the deal is scored
        public virtual void OnDealEnd(Table table) { }
        public virtual void OnTableEnd(Table table, bool won) { }
    }
}
