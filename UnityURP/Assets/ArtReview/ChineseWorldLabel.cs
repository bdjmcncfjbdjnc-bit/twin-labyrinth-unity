using UnityEngine;

// Keep this MonoBehaviour in its own file so Unity can serialize its script reference.
public sealed class ChineseWorldLabel : MonoBehaviour
{
    private TextMesh label; private string last; private Material material;
    private void Awake()
    {
        label=GetComponent<TextMesh>(); label.font=PrisonChinese.Font;
        material=Resources.Load<Material>("ChineseWorldSign");GetComponent<Renderer>().sharedMaterial=material;
        Font.textureRebuilt+=Atlas;Atlas(label.font);
        label.fontSize=64;label.characterSize=Mathf.Min(label.characterSize,.027f);
    }
    private void Atlas(Font font){if(material!=null&&font==label.font)material.mainTexture=font.material.mainTexture;}
    private void OnDestroy(){Font.textureRebuilt-=Atlas;}
    private void LateUpdate(){if(label.text==last)return;label.text=PrisonChinese.T(label.text);last=label.text;}
}
