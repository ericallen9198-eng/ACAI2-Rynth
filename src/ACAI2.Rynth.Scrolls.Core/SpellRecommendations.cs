namespace ACAI2.Rynth.Scrolls.Core;

public enum SpellList { Self, Others, Equipment }
public sealed record SkillDescriptor(uint Id,string Name,string Glyph);
public sealed record SkillMatch(SkillDescriptor Skill,int Training)
{
    // AC SkillAdvancementClass: 0 inactive, 1 untrained, 2 trained, 3 specialized.
    // Buffed level alone never establishes training or specialization.
    public bool Relevant=>Training is 2 or 3;
    public string TrainingName=>Training switch{0=>"Inactive",1=>"Untrained",2=>"Trained",3=>"Specialized",_=>"Unavailable"};
}
public static class SpellRecommendations
{
    public static readonly SkillDescriptor[] Skills=
    [
        new(38,"Alchemy","Glyph of Alchemy"),new(14,"Arcane Lore","Glyph of Arcane Lore"),
        new(29,"Armor Tinkering","Glyph of Armor Tinkering"),new(39,"Cooking","Glyph of Cooking"),
        new(31,"Creature Enchantment","Glyph of Creature Enchantment"),new(20,"Deception","Glyph of Deception"),
        new(52,"Dirty Fighting","Glyph of Dirty Fighting"),new(49,"Dual Wield","Glyph of Dual Wield"),
        new(46,"Finesse Weapons","Glyph of Finesse Weapons"),new(37,"Fletching","Glyph of Fletching"),
        new(21,"Healing","Glyph of Healing"),new(44,"Heavy Weapons","Glyph of Heavy Weapons"),
        new(7,"Missile Defense","Glyph of Missile Defense"),new(6,"Melee Defense","Glyph of Melee Defense"),
        new(32,"Item Enchantment","Glyph of Item Enchantment"),new(18,"Item Tinkering","Glyph of Item Tinkering"),
        new(22,"Jump","Glyph of Jump"),new(35,"Leadership","Glyph of Leadership"),
        new(33,"Life Magic","Glyph of Life Magic"),new(23,"Lockpick","Glyph of Lockpick"),
        new(30,"Magic Item Tinkering","Glyph of Magic Item Tinkering"),new(15,"Magic Defense","Glyph of Magic Defense"),
        new(16,"Mana Conversion","Glyph of Mana Conversion"),new(47,"Missile Weapons","Glyph of Missile Weapons"),
        new(27,"Assess Creature","Glyph of Monster Appraisal"),new(19,"Assess Person","Glyph of Person Appraisal"),
        new(50,"Recklessness","Glyph of Recklessness"),new(48,"Shield","Glyph of Shield"),
        new(51,"Sneak Attack","Glyph of Sneak Attack"),new(24,"Run","Glyph of Run"),
        new(43,"Void Magic","Glyph of Void Magic"),new(34,"War Magic","Glyph of War Magic"),
        new(28,"Weapon Tinkering","Glyph of Weapon Tinkering"),new(36,"Loyalty","Glyph of Loyalty"),
        new(40,"Salvaging","Glyph of Salvaging")
    ];
    public static SpellList List(ScrollRecipe recipe)
    {
        // Recipe quills distinguish self/other intent even for misleading historical
        // names such as "Jumping Ineptitude Self". Non-aura item spells get their own list.
        if(recipe.School=="Item"&&!recipe.Name.EndsWith(" Self",StringComparison.Ordinal)&&!recipe.Name.EndsWith(" Other",StringComparison.Ordinal))return SpellList.Equipment;
        return recipe.Ingredients.Any(i=>i.Name=="Quill of Introspection")?SpellList.Self:SpellList.Others;
    }
    public static SkillMatch? Match(ScrollRecipe recipe,IReadOnlyDictionary<uint,int>? training)
    {
        // Direct self skill buffs only. Nullification shares school glyphs but is
        // a dispel; attributes, protections and equipment buffs remain available,
        // without being falsely promoted to a specific trained-skill match.
        if(recipe.School!="Creature"||List(recipe)!=SpellList.Self||!recipe.Ingredients.Any(i=>i.Name=="Ink of Formation"))return null;
        var descriptor=Skills.FirstOrDefault(s=>recipe.Ingredients.Any(i=>i.Name==s.Glyph));
        return descriptor==null?null:new(descriptor,training?.GetValueOrDefault(descriptor.Id,-1)??-1);
    }
    public static Checklist[] Filter(IEnumerable<Checklist> rows,SpellList list,string school,string search,bool missingOnly,bool essentialsOnly)=>rows
        .Where(r=>r.List==list&&(school=="All"||r.Recipe.School==school)&&(!missingOnly||!r.Known)&&(!essentialsOnly||r.Essential))
        .Where(r=>search.Length==0||r.Recipe.Name.Contains(search,StringComparison.OrdinalIgnoreCase)||(r.Skill?.Skill.Name.Contains(search,StringComparison.OrdinalIgnoreCase)??false)||r.Recipe.Ingredients.Any(i=>i.Name.Contains(search,StringComparison.OrdinalIgnoreCase)))
        .OrderByDescending(r=>r.Essential).ThenByDescending(r=>r.Skill?.Training==3).ThenBy(r=>r.Recipe.Name,StringComparer.Ordinal).ToArray();
    public static string[] SelectMissing(IEnumerable<Checklist> rows,IEnumerable<string> existing)=>existing.Union(rows.Where(r=>!r.Known&&r.InPack==0).Select(r=>r.Recipe.Key)).ToArray();
}
