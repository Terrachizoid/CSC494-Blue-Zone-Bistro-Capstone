using UnityEngine;
using UnityEngine.UI;

public partial class BistroUIScreen
{
    private void RecipeBook()
    {
        var shade=BistroWidgets.Box(screen,"Recipe book shade",new Color(0,0,0,.6f),Vector2.zero,new Vector2(1,.91f));
        var panel=BistroShelfStyle.PaperPanel(shade,"Recipe book",theme,new Vector2(.035f,.025f),new Vector2(.965f,.98f));
        BistroShelfStyle.Text(panel,"Your recipe book",theme,new Vector2(.04f,.89f),new Vector2(.78f,.99f),34,BistroShelfStyle.Ink,true);
        PlanHeader(panel);
        BistroShelfStyle.Text(panel,"Choose a card to see what it needs. Use + to plan today's dishes.",theme,new Vector2(.04f,.862f),new Vector2(.84f,.892f),16,BistroShelfStyle.Ink);
        BistroShelfStyle.Button(panel,"Close",theme,new Vector2(.85f,.89f),new Vector2(.97f,.97f),new Color(.77f,.82f,.69f),BistroShelfStyle.Ink,()=>Change(()=>{
            // Day-1 lesson: closing is refused (with the reason) until a dish is planned, and reported after.
            if(ShopLesson()&&!game.Loop.AskTutorial(TutorialAction.CloseRecipes)){Refused(game.Loop.Tutorials.LastError);return;}
            modal=null;if(ShopLesson())game.Loop.TellTutorial(TutorialAction.CloseRecipes);}));
        var grid=BistroWidgets.Grid(panel,theme,new Vector2(.025f,.035f),new Vector2(.975f,.82f));
        ((RectTransform)grid.parent).GetComponent<Image>().color=BistroShelfStyle.Cream;
        grid.GetComponent<BistroResponsiveGrid>().SetPreferred(new Vector2(340,480));
        grid.GetComponent<GridLayoutGroup>().spacing=new Vector2(24,24);
        foreach(var recipe in game.Queries.GetUnlockedRecipes())PlanControl(BistroRecipeCard.Create(grid,recipe,theme,game.Queries.Foods),recipe);
        BistroWidgets.EmptyShelf(grid,"No recipes are available yet.",theme);
        AddShelfScrollbar((RectTransform)grid.parent);
    }
}
