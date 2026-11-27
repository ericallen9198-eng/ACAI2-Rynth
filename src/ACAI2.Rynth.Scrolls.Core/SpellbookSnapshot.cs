namespace ACAI2.Rynth.Scrolls.Core;

// ReadKnownSpells serves Rynth's main-thread snapshot to the plugin tick thread.
// Never substitute the live CACQualities IsSpellKnown API here: that call rejects
// off-main-thread callers. A cold/wrong-owner cache is unknown, not an empty book.
public sealed class SpellbookSnapshot
{
    private readonly HashSet<uint> known;
    private SpellbookSnapshot(IEnumerable<uint> ids){known=ids.ToHashSet();}
    public bool Contains(uint spellId)=>known.Contains(spellId);
    public static SpellbookSnapshot Read(Func<uint[],int,int> read,int capacity=16384)
    {
        if(capacity is <2 or >32768)throw new ArgumentOutOfRangeException(nameof(capacity));
        uint[] ids=new uint[capacity];int count=read(ids,ids.Length);
        if(count<=0)throw new InvalidOperationException("Waiting for Rynth's learned-spell snapshot. Stay logged in and retry Rescan.");
        // The native API returns the number copied, so a full buffer may be truncated.
        if(count>=ids.Length)throw new InvalidOperationException("Learned-spell snapshot reached its bound; no missing-spell decisions were made.");
        if(ids.Take(count).Any(id=>id==0))throw new InvalidOperationException("Learned-spell snapshot is incomplete; retry Rescan.");
        return new(ids.Take(count));
    }
    public Checklist[] Checklist(Catalog catalog,IReadOnlyList<InventoryItem> inventory)=>catalog.Recipes.Select(r=>new Checklist(r,Contains(r.SpellId),Planner.Count(inventory,r.ScrollTemplate))).ToArray();
}
