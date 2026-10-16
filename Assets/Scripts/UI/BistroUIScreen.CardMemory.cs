// Flipped faces are temporary browsing state, never part of the checkpoint or food data.
using System.Collections.Generic;
public partial class BistroUIScreen
{
    private readonly Dictionary<string,bool> cardFaces=new Dictionary<string,bool>();
    private string CardFaceKey(BistroFoodCard card)=>renderedContext+"/"+card.Kind+"/"+card.Key;
    private void RememberCardFaces()
    {
        if(screen==null || renderedContext==null)return;
        foreach(var card in screen.GetComponentsInChildren<BistroFoodCard>())cardFaces[CardFaceKey(card)]=card.IsFlipped;
        if(cardFaces.Count>1024)cardFaces.Clear();
    }
    private void RestoreCardFaces()
    {
        foreach(var card in screen.GetComponentsInChildren<BistroFoodCard>())
            if(cardFaces.TryGetValue(CardFaceKey(card),out bool back))card.RestoreFace(back);
    }
}
