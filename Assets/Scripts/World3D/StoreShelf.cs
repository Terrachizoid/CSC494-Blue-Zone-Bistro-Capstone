// File responsibility: Marks one clickable shelf zone inside a 3D store interior. storeId and shelfId use the same
// IDs as the store data (Blue_Zone_Bistro_Stores.csv, shelf_definitions) and the 2D store screens, so a click on
// this collider can open the existing purchasing page for that shelf. Created by the city generator from
// city_shelves.csv; the collider is generous (padded) so it is easy to tap on a phone.
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public sealed class StoreShelf : MonoBehaviour
{
    public string storeId;
    public string shelfId;
}
