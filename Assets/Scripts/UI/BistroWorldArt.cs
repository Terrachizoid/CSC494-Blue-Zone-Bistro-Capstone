// Click regions only (layout, not art): Resources/BistroArt/world.json lists, per view ID ("city" or a
// store ID), the polygons and label markers drawn over that view's picture. Coordinates are normalized
// from the top-left of the picture. The pictures themselves come from the UI theme (city) and Stores.csv.
using System;
using UnityEngine;

public static class BistroWorldArt
{
    [Serializable] public class Region { public string id="",label=""; public Vector2 marker; public Vector2[] polygon; }
    [Serializable] public class View { public string id=""; public Region[] regions; }
    [Serializable] private class Catalogue { public View[] views; }
    private static Catalogue catalogue;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache(){catalogue=null;}
    public static View Get(string id)
    {
        if(catalogue==null){var json=Resources.Load<TextAsset>("BistroArt/world");if(json==null)return null;
            try{catalogue=JsonUtility.FromJson<Catalogue>(json.text);}catch(ArgumentException){return null;}}
        return catalogue?.views==null?null:Array.Find(catalogue.views,v=>v.id==id);
    }
}
