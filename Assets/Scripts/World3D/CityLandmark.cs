// File responsibility: Marks a clickable building in the 3D city. landmarkId uses the same IDs as the 2D map:
// store IDs from Stores.csv (S001, S002, S003), NEWS for the news tower and BISTRO for the player's bistro.
// The future 3D view layer raycasts against the collider and reports this ID through the existing click path.
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CityLandmark : MonoBehaviour
{
    public string landmarkId;
}
