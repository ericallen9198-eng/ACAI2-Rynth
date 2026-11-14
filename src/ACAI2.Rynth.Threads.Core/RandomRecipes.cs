namespace ACAI2.Rynth.Threads.Core;

/// <summary>Role facts only, not claims about a component's outcome. Native learned
/// roles extend the unadorned herb/reagent names. Other families identify by name.</summary>
public static class ThreadComponentCatalog
{
    public const uint SpellComponentType=0x1000;
    private static readonly HashSet<string> Herbs=new(StringComparer.OrdinalIgnoreCase){"Mugwort","Hyssop","Saffron","Amaranth","Damiana"};
    private static readonly HashSet<string> Reagents=new(StringComparer.OrdinalIgnoreCase){"Colcothar","Cinnabar","Cadmia","Brimstone","Realgar","Cobalt"};
    public static IReadOnlyList<string> Scarabs {get;}=Array.AsReadOnly(new[]{"Lead Scarab","Iron Scarab","Copper Scarab","Silver Scarab","Gold Scarab","Pyreal Scarab","Diamond Scarab","Platinum Scarab","Mana Scarab"});
    public static string Role(string name)=>name.EndsWith(" Scarab",StringComparison.OrdinalIgnoreCase)?"Scarab":
        name.EndsWith(" Taper",StringComparison.OrdinalIgnoreCase)?"Taper":name.EndsWith(" Talisman",StringComparison.OrdinalIgnoreCase)?"Talisman":
        name.StartsWith("Powdered ",StringComparison.OrdinalIgnoreCase)?"Powder":
        Herbs.Contains(name)?"Herb":
        Reagents.Contains(name)?"Reagent":"Unknown";
}

public static class RandomRecipes
{
    /// <summary>Freeze all recipes before buying or dosing. Each name is equally likely within
    /// its family among available spell components; finite supplies are reserved across the batch.</summary>
    public static List<string[]> Build(PrepPlan plan,PrepWorld world,int count,Random? random=null)
    {
        if(count is <0 or >50)throw new InvalidDataException("Random recipe count must be 0..50.");
        if(count==0)return [];
        random??=Random.Shared;
        static bool Plain(string name)=>!string.IsNullOrWhiteSpace(name)&&name.Length<=100&&name==name.Trim()&&!name.Any(char.IsControl)&&!name.Contains(',')&&!name.Contains('`');
        var available=world.Inventory.Where(i=>(i.ItemType&ThreadComponentCatalog.SpellComponentType)!=0&&i.Count>0&&Plain(i.Name))
            .GroupBy(i=>i.Name,StringComparer.Ordinal).ToDictionary(g=>g.Key,g=>Math.Min(int.MaxValue,g.Sum(i=>(long)i.Count)),StringComparer.Ordinal);
        if(plan.BuyMissing)
        {
            if(world.ReagentVendor==0||world.OpenVendor!=world.ReagentVendor)throw new InvalidOperationException("Open the bound reagent vendor before generating random recipes.");
            foreach(var rows in world.Stock.Where(s=>(s.ItemType&ThreadComponentCatalog.SpellComponentType)!=0&&s.Amount!=0&&s.UnitPrice>0&&Plain(s.Name)).GroupBy(s=>s.Name,StringComparer.Ordinal))
            {
                if(rows.Count()!=1)continue;var row=rows.Single();
                long stock=row.Amount<0?int.MaxValue:row.Amount;
                available[row.Name]=Math.Min(int.MaxValue,available.GetValueOrDefault(row.Name)+stock);
            }
        }
        string Role(string name)=>world.ComponentRoles?.TryGetValue(name,out var role)==true?role:ThreadComponentCatalog.Role(name);
        void Reserve(string name,int amount)
        {
            if(available.GetValueOrDefault(name)<amount)throw new InvalidOperationException("Random batch needs "+amount+" available "+name+". No gem action issued.");
            available[name]-=amount;
        }
        string Pick(string role,int dose)
        {
            var pool=available.Where(p=>p.Value>=dose&&Role(p.Key)==role).Select(p=>p.Key).Order(StringComparer.Ordinal).ToArray();
            if(pool.Length==0)throw new InvalidOperationException("No recognized "+role+" supply remains for the random batch. "+(plan.BuyMissing?"Check the reagent vendor's stock; unknown families are excluded.":"Supply spell components in your pack or enable Buy missing supplies."));
            string chosen=pool[random.Next(pool.Length)];Reserve(chosen,dose);return chosen;
        }
        Reserve(plan.Scarab,count);
        List<string[]> result=[];
        for(int i=0;i<count;i++)
        {
            string herb=Pick("Herb",1),powder=Pick("Powder",1),reagent=Pick("Reagent",1),talisman=Pick("Talisman",1),taper=Pick("Taper",4);
            result.Add([plan.Scarab,herb,powder,reagent,talisman,taper,taper,taper,taper]);
        }
        return result;
    }
}
