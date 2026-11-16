using ACAI2.Protocol;
using ACAI2.Rynth.Core.Keyfer;
namespace ACAI2.Bridge;

internal static class OwnLootContext
{
    internal static object For(KeyferAsk ask,DateTime now)
    {
        var x=ask.LootObservation;
        if(x==null)return new{status="No own-loot observations supplied; do not invent findings."};
        bool Number(double? v)=>!v.HasValue||double.IsFinite(v.Value)&&v.Value is >=-100 and <=2000;
        bool Item(KeptLoot i)=>OwnLootLedger.ValidItem(i)&&i.Utc<=now.AddSeconds(5)&&now-i.Utc<=TimeSpan.FromDays(30)&&Number(i.Attack)&&Number(i.MeleeDefense)&&Number(i.ProfileAttackEstimate)&&Number(i.ProfileMeleeDefenseEstimate)&&(i.Chest==null||i.Chest.Length<=120&&!i.Chest.Any(char.IsControl))&&(!i.ChestConfirmed||!string.IsNullOrEmpty(i.Chest));
        bool valid=ask.Channel!="Discord"&&x.InstanceId==ask.InstanceId&&string.Equals(x.Server,ask.Server,StringComparison.OrdinalIgnoreCase)&&string.Equals(x.Character,ask.Character,StringComparison.OrdinalIgnoreCase)&&
            x.CapturedUtc<=now.AddSeconds(5)&&now-x.CapturedUtc<=TimeSpan.FromSeconds(75)&&x.SessionStartedUtc<=x.CapturedUtc&&x.Chest is{Length:<=120}&&!x.Chest.Any(char.IsControl)&&
            x.Evidence is{Length:<=1000}&&x.RecentKeeps is{Length:<=12}&&x.NotableKeeps is{Length:<=24}&&x.RecentKeeps.All(Item)&&x.NotableKeeps.All(Item)&&
            x.ObservedCompletedCycles>=0&&x.ObservedNoMatchCycles>=0&&x.ConfirmedKeepPickups>=0;
        return valid?x with{Running=x.Running&&x.ActivityUtc.HasValue&&x.ActivityUtc.Value<=now.AddSeconds(5)&&now-x.ActivityUtc.Value<=TimeSpan.FromSeconds(5)}:new{status="Own-loot observations unavailable: stale, malformed or different client identity. Do not guess findings."};
    }
}
