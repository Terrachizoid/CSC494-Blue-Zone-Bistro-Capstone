// File responsibility: One store in the 3D city: its shell (the building as seen from the street: roof, walls,
// sign) and its interior (a cutaway room with the shelves, hidden in the city view). SetInside swaps them; the
// store view (camera move, focus fade) calls it. Built by the city generator; the meshes come from citygen.py
// groups store_<id>_shell and store_<id>_interior (plus their "_detail" parts).
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CityStore : MonoBehaviour
{
    public string storeId;
    public GameObject[] shell = new GameObject[0];
    public GameObject[] interior = new GameObject[0];
    public bool Inside { get; private set; }

    /// <summary>Show the interior (true) or the street view of the building (false).</summary>
    public void SetInside(bool inside)
    {
        Inside = inside;
        foreach (var go in shell)
            if (go != null) go.SetActive(!inside && (!go.name.EndsWith("_detail") || BistroGraphics.DetailLayer));
        foreach (var go in interior)
            if (go != null) go.SetActive(inside && (!go.name.EndsWith("_detail") || BistroGraphics.DetailLayer));
    }

    /// <summary>The store with this ID in the loaded city, or null.</summary>
    public static CityStore Find(string id)
    {
        foreach (var s in Object.FindObjectsByType<CityStore>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (s.storeId == id) return s;
        return null;
    }
}
