namespace ACAI2.Rynth.Scrolls.Core;

// Runtime queues never resume after logout/restart. Only the pre-action checkpoint
// persists; an interrupted action must be reviewed before another batch can begin.
public sealed class CraftBatch
{
    private readonly ScrollRecipe[] recipes;
    private int recipeIndex,stepIndex;
    public int Completed=>recipeIndex;
    public int Total=>recipes.Length;
    public bool Done=>recipeIndex>=recipes.Length;
    public ScrollRecipe Current=>recipes[recipeIndex];
    public CraftBatch(IEnumerable<ScrollRecipe> selected)
    {recipes=selected.ToArray();if(recipes.Length is <1 or >250||recipes.Select(r=>r.Key).Distinct().Count()!=recipes.Length)throw new InvalidOperationException("Select one or more distinct scrolls.");}
    public PendingAction Prepare(IReadOnlyList<InventoryItem> inventory)
    {
        if(Done)throw new InvalidOperationException("Batch complete");
        var step=Current.Steps[stepIndex];
        uint Actual(uint template)
        {
            var ingredient=Current.Ingredients.FirstOrDefault(i=>i.Template==template);
            return ingredient==null?template:inventory.Where(i=>ingredient.Accepts(i.Template)&&i.Count>0).OrderBy(i=>i.Template==template?0:1).ThenBy(i=>i.Id).FirstOrDefault()?.Template??template;
        }
        step=step with {Source=Actual(step.Source),Target=Actual(step.Target)};
        var source=Planner.Pick(inventory,step.Source);var target=Planner.Pick(inventory,step.Target);
        return new(Current.Key,stepIndex,step,source.Id,target.Id,Planner.Count(inventory,step.Source),Planner.Count(inventory,step.Target),Planner.Count(inventory,step.Result));
    }
    public void Confirm(PendingAction action,IReadOnlyList<InventoryItem> inventory)
    {
        if(Done||action.RecipeKey!=Current.Key||action.StepIndex!=stepIndex||!Planner.Acknowledged(action,inventory))throw new InvalidOperationException("Craft not confirmed by inventory changes.");
        if(++stepIndex==Current.Steps.Length){recipeIndex++;stepIndex=0;}
    }
}
