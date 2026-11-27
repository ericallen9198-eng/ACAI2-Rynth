using System.Text.Json;
namespace ACAI2.Rynth.Scrolls.Core;

public sealed record Ingredient(uint Template,string Name,int Amount=1,uint[]? Alternatives=null)
{public bool Accepts(uint id)=>id==Template||(Alternatives?.Contains(id)??false);}
public sealed record CraftStep(uint Source,uint Target,uint Result,string ResultName);
public sealed record ScrollRecipe(string Key,string School,string Name,uint SpellId,uint ScrollTemplate,Ingredient[] Ingredients,CraftStep[] Steps,string WikiSource,string AceSource);
public sealed record InventoryItem(uint Id,uint Template,string Name,int Count);
public sealed record Supply(uint Template,string Name,int Required,int Owned)
{public int Missing=>Math.Max(0,Required-Owned);}
public sealed record Checklist(ScrollRecipe Recipe,bool Known,int InPack,SkillMatch? Skill=null)
{
    public SpellList List=>SpellRecommendations.List(Recipe);
    public bool Essential=>!Known&&List==SpellList.Self&&Skill?.Relevant==true;
}
public sealed record Catalog(string AceCommit,ScrollRecipe[] Recipes,string[] Unresolved)
{
    public static Catalog Load()
    {using var s=typeof(Catalog).Assembly.GetManifestResourceStream("Scrolls.catalog.json")!;return JsonSerializer.Deserialize<Catalog>(s,Json.Options)??throw new InvalidDataException("Missing recipe catalog");}
    public void Validate()
    {
        if(Recipes.Length is <1 or >300||Recipes.Select(r=>r.Key).Distinct().Count()!=Recipes.Length)throw new InvalidDataException("Recipe catalog keys/bounds");
        foreach(var r in Recipes)
        {
            if(r.SpellId==0||r.ScrollTemplate==0||r.Ingredients.Length!=4||r.Steps.Length!=3||r.Steps[^1].Result!=r.ScrollTemplate)throw new InvalidDataException("Recipe identity/steps: "+r.Name);
            var pool=r.Ingredients.ToDictionary(i=>i.Template,i=>i.Amount);
            foreach(var step in r.Steps)
            {
                foreach(uint id in new[]{step.Source,step.Target}){if(pool.GetValueOrDefault(id)<1)throw new InvalidDataException("Broken recipe chain: "+r.Name);pool[id]--;}
                pool[step.Result]=pool.GetValueOrDefault(step.Result)+1;
            }
            if(pool.Where(k=>k.Value>0).Any(k=>k.Key!=r.ScrollTemplate||k.Value!=1))throw new InvalidDataException("Recipe has leftovers");
        }
    }
}
public static class Json {public static readonly JsonSerializerOptions Options=new(){PropertyNameCaseInsensitive=true,WriteIndented=true};}
public static class Planner
{
    public static int Count(IEnumerable<InventoryItem> items,uint id)=>items.Where(i=>i.Template==id).Sum(i=>i.Count);
    public static Supply[] Shopping(IEnumerable<ScrollRecipe> recipes,IReadOnlyList<InventoryItem> inventory)=>recipes.SelectMany(r=>r.Ingredients).GroupBy(i=>i.Template)
        .Select(g=>new Supply(g.Key,g.First().Name,g.Sum(i=>i.Amount),inventory.Where(i=>g.First().Accepts(i.Template)).Sum(i=>i.Count))).OrderBy(s=>s.Name,StringComparer.Ordinal).ToArray();
    public static bool Acknowledged(PendingAction pending,IReadOnlyList<InventoryItem> items)=>
        Count(items,pending.Step.Source)==pending.SourceBefore-1 && Count(items,pending.Step.Target)==pending.TargetBefore-1 && Count(items,pending.Step.Result)==pending.ResultBefore+1;
    public static InventoryItem Pick(IReadOnlyList<InventoryItem> items,uint template)=>items.Where(i=>i.Template==template&&i.Count>0).OrderBy(i=>i.Id).FirstOrDefault()??throw new InvalidOperationException("A required component is missing; rescan supplies.");
}
public sealed record PendingAction(string RecipeKey,int StepIndex,CraftStep Step,uint SourceId,uint TargetId,int SourceBefore,int TargetBefore,int ResultBefore);
public sealed class ScrollProfile
{
    public string Server{get;set;}="";public string Character{get;set;}="";
    public string School{get;set;}="All";public string Search{get;set;}="";public bool MissingOnly{get;set;}=true;
    public bool EssentialsOnly{get;set;}=false;public string Tab{get;set;}="Self";
    public float X{get;set;}=90;public float Y{get;set;}=90;public bool Visible{get;set;}=true;
    public HashSet<string> Selected{get;set;}=[];public PendingAction? Pending{get;set;}
    public string[] Recent{get;set;}=[];
    public static ScrollProfile Load(string path,string server,string character)
    {
        if(!File.Exists(path))return new(){Server=server,Character=character};
        if(new FileInfo(path).Length>131072)throw new InvalidDataException("Saved scroll profile exceeds bounds.");
        var p=JsonSerializer.Deserialize<ScrollProfile>(File.ReadAllText(path),Json.Options)??throw new InvalidDataException("Empty scroll profile");
        if(p.Server!=server||p.Character!=character||p.Selected.Count>300||p.Recent.Length>20||p.Search.Length>96)throw new InvalidDataException("Saved profile identity/bounds mismatch");return p;
    }
    public void Save(string path){Directory.CreateDirectory(Path.GetDirectoryName(path)!);using(var stream=new FileStream(path+".tmp",FileMode.Create,FileAccess.Write,FileShare.None,4096,FileOptions.WriteThrough)){JsonSerializer.Serialize(stream,this,Json.Options);stream.Flush(true);}File.Move(path+".tmp",path,true);}
    public void Record(string text){Recent=Recent.Append(text).TakeLast(20).ToArray();}
}
