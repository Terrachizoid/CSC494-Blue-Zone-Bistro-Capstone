using UnityEngine;
using UnityEngine.UI;

// A presentation-only blur on the menu's city image. Never affects the playable map.
[RequireComponent(typeof(Image))]
[ExecuteAlways]
public sealed class BistroMenuBlur : MonoBehaviour
{
    private Material ownedMaterial;
    private void Awake()
    {
        var shader=Resources.Load<Shader>("BistroMenuBlur");
        if(shader==null||!shader.isSupported)return;
        ownedMaterial=new Material(shader){hideFlags=HideFlags.HideAndDontSave};
        GetComponent<Image>().material=ownedMaterial;
    }
    private void OnDestroy()
    {
        if(ownedMaterial==null)return;
        if(Application.isPlaying)Destroy(ownedMaterial);else DestroyImmediate(ownedMaterial);
    }
}
