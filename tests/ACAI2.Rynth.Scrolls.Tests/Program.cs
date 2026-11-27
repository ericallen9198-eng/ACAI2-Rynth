using ACAI2.Rynth.Scrolls.Core;
var catalog=Catalog.Load();catalog.Validate();int checks=0;
void Check(bool value,string message){if(!value)throw new Exception(message);checks++;}
Check(catalog.Recipes.Length==197,"197 independently resolved recipes");
Check(catalog.Unresolved.Length==2&&catalog.Unresolved.All(s=>s.Contains("Light Weapon")),"ambiguous recipes cannot craft");
var aura=catalog.Recipes.Single(r=>r.SpellId==5183);Check(aura.Steps[^1].Result==37665,"Aura Blood Drinker spell identity");
var arc=catalog.Recipes.Single(r=>r.SpellId==4421);Check(arc.ScrollTemplate==37992,"Acid Arc spell identity");
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
    var profile=new ScrollProfile{Server="Test",Character="Mage",Pending=fake};profile.Save(path);
    var restored=ScrollProfile.Load(path,"Test","Mage");Check(restored.Pending==fake,"pre-action checkpoint survives restart; no runtime batch restored");
    bool refused=false;try{ScrollProfile.Load(path,"Test","Other");}catch(InvalidDataException){refused=true;}Check(refused,"checkpoint bound to character");
    for(int i=0;i<50;i++)profile.Record(i.ToString());Check(profile.Recent.Length==20,"activity remains bounded");
}
finally{Directory.Delete(folder,true);}
Console.WriteLine($"Scroll crafting checks passed: {checks}. No game or provider calls.");
