using ACAI2.Rynth.Scrolls.Core;
var catalog=Catalog.Load();catalog.Validate();int checks=0;
void Check(bool value,string message){if(!value)throw new Exception(message);checks++;}
Check(catalog.Recipes.Length==197,"197 independently resolved recipes");
Check(catalog.Unresolved.Length==2&&catalog.Unresolved.All(s=>s.Contains("Light Weapon")),"ambiguous recipes cannot craft");
var aura=catalog.Recipes.Single(r=>r.SpellId==5183);Check(aura.Steps[^1].Result==37665,"Aura Blood Drinker spell identity");
var arc=catalog.Recipes.Single(r=>r.SpellId==4421);Check(arc.ScrollTemplate==37992,"Acid Arc spell identity");
// Regression: the learned-spell snapshot succeeds even when live per-spell queries
// are unavailable on the plugin pump. It must classify all recipes from that cache.
int reads=0;
var book=SpellbookSnapshot.Read((ids,size)=>{reads++;ids[0]=aura.SpellId;ids[1]=123;return 2;});
var checklist=book.Checklist(catalog,[new(99,arc.ScrollTemplate,"owned arc",1)]);
Check(reads==1&&checklist.Length==197,"one snapshot supplies the entire checklist");
Check(checklist.Single(r=>r.Recipe.Key==aura.Key).Known,"snapshot learned spell excluded from missing");
Check(!checklist.Single(r=>r.Recipe.Key==arc.Key).Known&&checklist.Single(r=>r.Recipe.Key==arc.Key).InPack==1,"unlearned owned scroll remains in-pack, not learned");
foreach(int nativeResult in new[]{-1,0,8,9})
{
    bool blocked=false;try{SpellbookSnapshot.Read((_,_)=>nativeResult,8);}catch(InvalidOperationException){blocked=true;}
    Check(blocked,"cold, empty or truncated snapshot must not mean all spells missing");
}
bool invalid=false;try{SpellbookSnapshot.Read((_,_)=>1,8);}catch(InvalidOperationException){invalid=true;}Check(invalid,"unfilled cached spell IDs rejected");
var nextBook=SpellbookSnapshot.Read((ids,_)=>{ids[0]=arc.SpellId;return 1;});
Check(nextBook.Contains(arc.SpellId)&&!nextBook.Contains(aura.SpellId),"new snapshot does not carry previous known set");
// Spell list partitioning and recommendations use recipe intent and native training,
// not whether a name happens to contain Other or a buffed skill has a positive value.
Check(catalog.Recipes.Count(r=>SpellRecommendations.List(r)==SpellList.Self)==62,"self list contains only introspective self recipes");
Check(catalog.Recipes.Count(r=>SpellRecommendations.List(r)==SpellList.Others)==110,"other list includes attacks, debuffs and other buffs");
Check(catalog.Recipes.Count(r=>SpellRecommendations.List(r)==SpellList.Equipment)==25,"non-aura item targets have a separate equipment list");
Check(SpellRecommendations.List(aura)==SpellList.Self&&SpellRecommendations.List(arc)==SpellList.Others,"aura self and offensive arc classify independently of school");
var armorBane=catalog.Recipes.Single(r=>r.Name=="Inscription of Acid Bane");Check(SpellRecommendations.List(armorBane)==SpellList.Equipment,"bane is equipment, not a self creature buff");
var jumpDebuff=catalog.Recipes.Single(r=>r.Name.Contains("Jumping Ineptitude"));Check(SpellRecommendations.List(jumpDebuff)==SpellList.Others,"historical misleading Self name cannot classify an inflictive debuff as self");
var missile=catalog.Recipes.Single(r=>r.SpellId==4522);var fletching=catalog.Recipes.Single(r=>r.SpellId==4552);
var specialized=new Dictionary<uint,int>{{47,3},{37,2},{31,1}};
var relevant=book.Checklist(catalog,[],specialized);
Check(relevant.Single(r=>r.Recipe.Key==missile.Key).Essential&&relevant.Single(r=>r.Recipe.Key==missile.Key).Skill!.TrainingName=="Specialized","missing missile buff matches specialized missile skill");
Check(relevant.Single(r=>r.Recipe.Key==fletching.Key).Essential,"trained fletching is also a relevant self buff");
Check(!relevant.Single(r=>r.Recipe.SpellId==4530).Essential,"untrained Creature Magic does not recommend its mastery");
Check(!book.Checklist(catalog,[]).Any(r=>r.Essential),"unavailable skill snapshot does not invent essentials or break spell scan");
Check(!book.Checklist(catalog,[],new Dictionary<uint,int>{{47,0}}).Any(r=>r.Essential),"inactive skill cannot be essential");
var learnedMissile=SpellbookSnapshot.Read((ids,_)=>{ids[0]=missile.SpellId;return 1;});
Check(!learnedMissile.Checklist(catalog,[],specialized).Single(r=>r.Recipe.Key==missile.Key).Essential,"learned buff is not a missing essential");
var ownEssential=book.Checklist(catalog,[new(90,missile.ScrollTemplate,"owned missile scroll",1)],specialized);
Check(ownEssential.Single(r=>r.Recipe.Key==missile.Key).Essential,"unlearned owned scroll remains relevant for manual learning");
var selfEssentials=SpellRecommendations.Filter(ownEssential,SpellList.Self,"All","",true,true);
Check(selfEssentials.Length==2&&selfEssentials.All(r=>r.Essential),"essentials filter keeps precisely trained/specialized direct self buffs");
var cart=SpellRecommendations.SelectMissing(selfEssentials,[]);
Check(cart.Length==1&&cart[0]==fletching.Key,"bulk essentials selection skips the copy already in inventory");
Check(SpellRecommendations.Filter(relevant,SpellList.Others,"All","",true,false).All(r=>!r.Essential),"outgoing debuffs are not essential just because their glyph matches a trained skill");
Check(!relevant.Any(r=>r.Recipe.Name.Contains("Nullify")&&r.Essential),"school glyph in a dispel does not make it a skill buff");
Check(SpellRecommendations.Filter(relevant,SpellList.Self,"War","",false,false).Length==0,"school filter remains applied within tabs");
Check(SpellRecommendations.Filter(relevant,SpellList.Self,"Creature","Fletching",true,true).Single().Recipe.Key==fletching.Key,"search and essentials filters compose");
Check(SpellRecommendations.Skills.Select(r=>r.Id).Distinct().Count()==SpellRecommendations.Skills.Length,"skill IDs are unique");
foreach(var recipe in catalog.Recipes)
{
    var items=recipe.Ingredients.Select((i,k)=>new InventoryItem((uint)(k+1),i.Template,i.Name,1)).ToList();
    var batch=new CraftBatch([recipe]);var p=batch.Prepare(items);
    Check(!Planner.Acknowledged(p,items),"submission alone does not confirm");
    for(int n=0;n<3;n++)
    {
        var a=batch.Prepare(items);var missing=items.Where(i=>i.Template!=a.Step.Source&&i.Template!=a.Step.Target).ToList();
        missing.Add(new((uint)(100+n),a.Step.Result,a.Step.ResultName,1));
        Check(Planner.Acknowledged(a,missing),"exact inventory consumption and result required");
        batch.Confirm(a,missing);items=missing;
    }
    Check(batch.Done&&items.Single().Template==recipe.ScrollTemplate,"full chain finishes only the selected scroll");
}
var supply=Planner.Shopping([aura,arc],[]);Check(supply.Sum(s=>s.Required)==8&&supply.Sum(s=>s.Missing)==8,"combined raw component quantities");
var scarab=supply.Single(s=>s.Name=="Mana Scarab");Check(scarab.Required==2,"scarabs shared across schools");
var fake=new PendingAction(aura.Key,0,aura.Steps[0],1,2,3,3,0);
Check(!Planner.Acknowledged(fake,[new(1,fake.Step.Source,"",2),new(2,fake.Step.Target,"",2)]),"consumption without output is uncertain");
Check(!Planner.Acknowledged(fake,[new(1,fake.Step.Source,"",3),new(2,fake.Step.Target,"",3),new(3,fake.Step.Result,"",1)]),"output without consumption is not confirmation");
var variants=arc with {Ingredients=arc.Ingredients.Select((i,k)=>k==3?i with {Alternatives=[999999u]}:i).ToArray()};
var altItems=variants.Ingredients.Select((i,k)=>new InventoryItem((uint)(k+1),i.Alternatives?.FirstOrDefault() is >0?i.Alternatives[0]:i.Template,i.Name,1)).ToList();
Check(Planner.Shopping([variants],altItems).All(s=>s.Missing==0),"explicitly allowed variant counted");
// A verified alternative glyph must remain usable through all three native pairs.
var altBatch=new CraftBatch([variants]);
for(int n=0;n<3;n++)
{
    var a=altBatch.Prepare(altItems);
    altItems=altItems.Where(i=>i.Template!=a.Step.Source&&i.Template!=a.Step.Target).Append(new InventoryItem((uint)(200+n),a.Step.Result,a.Step.ResultName,1)).ToList();
    altBatch.Confirm(a,altItems);
}
Check(altBatch.Done,"legacy glyph variant executes the same verified output chain");
bool rejected=false;try{new CraftBatch([arc,arc]);}catch(InvalidOperationException){rejected=true;}Check(rejected,"duplicate cart keys rejected");
var folder=Path.Combine(Path.GetTempPath(),"scroll-tests-"+Guid.NewGuid());var path=Path.Combine(folder,"profile.json");
try
{
    var profile=new ScrollProfile{Server="Test",Character="Mage",Pending=fake,Tab="Others",EssentialsOnly=true};profile.Save(path);
    var restored=ScrollProfile.Load(path,"Test","Mage");Check(restored.Tab=="Others"&&restored.EssentialsOnly,"list preferences persist per character");Check(restored.Pending==fake,"pre-action checkpoint survives restart; no runtime batch restored");
    bool refused=false;try{ScrollProfile.Load(path,"Test","Other");}catch(InvalidDataException){refused=true;}Check(refused,"checkpoint bound to character");
    for(int i=0;i<50;i++)profile.Record(i.ToString());Check(profile.Recent.Length==20,"activity remains bounded");
}
finally{Directory.Delete(folder,true);}
Console.WriteLine($"Scroll crafting checks passed: {checks}. No game or provider calls.");
