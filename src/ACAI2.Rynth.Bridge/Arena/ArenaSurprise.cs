namespace ACAI2.Bridge;

// Arena scenery is separate from character abilities and combat RNG. It never
// creates damage, equipment, class powers or a different winner.
internal sealed record ArenaSurprise(int Round, string Text)
{
    internal static ArenaSurprise? For(int seed)
    {
        var dice = new Random(unchecked(seed ^ 0x54A71B93));
        if (dice.Next(100) >= 25) return null;
        string[] who = { "A drudge", "A tiny Lugian", "The referee", "A portal chicken", "A unionized golem", "The concession vendor", "A floating turnip" };
        string[] what = { "demands a pants inspection", "eats the bell", "declares the floor haunted", "audits the referee", "steals the applause", "proposes to a chair", "confiscates gravity", "starts a conga tribunal" };
        string[] breaches = { "Reality buffers. Everyone's trousers load first.", "A portal drops a bathtub. Keyfer sells it ringside seats.", "The arena becomes a bakery. Nobody accepts the apron.", "A cow falls through a portal and requests the replay.", "The floor turns to custard. Keyfer declares it premium seating." };
        string text=dice.Next(3)==0?breaches[dice.Next(breaches.Length)]:who[dice.Next(who.Length)]+" "+what[dice.Next(what.Length)]+".";
        return new(dice.Next(1,4), text);
    }
}
