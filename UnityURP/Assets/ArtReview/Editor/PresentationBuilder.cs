using System.Linq;
using UnityEditor;
using UnityEngine;

internal static class PresentationBuilder
{
    public static void Stage(MazeSession session)
    {
        PlayerSettings.productName="双生迷城 · 最后一次点名";
        if(session.GetComponent<PrisonPresentation>()==null)session.gameObject.AddComponent<PrisonPresentation>();
        Font font=PrisonChinese.Font;
        if(font==null)throw new System.InvalidOperationException("Missing redistributable Chinese font.");
        var world=AssetDatabase.LoadAssetAtPath<Material>("Assets/ArtReview/Resources/ChineseWorldSign.mat");
        if(world==null){world=new Material(Shader.Find("Prison/WorldSign"));AssetDatabase.CreateAsset(world,"Assets/ArtReview/Resources/ChineseWorldSign.mat");}
        var importer=(TrueTypeFontImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(font));
        if(!importer.includeFontData){importer.includeFontData=true;importer.SaveAndReimport();}
        foreach(var label in Object.FindObjectsByType<TextMesh>(FindObjectsSortMode.None))
        {
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(label.gameObject);
            if(label.GetComponent<ChineseWorldLabel>()==null)label.gameObject.AddComponent<ChineseWorldLabel>();
            label.font=font;label.GetComponent<Renderer>().sharedMaterial=world;
            label.fontSize=64;label.text=PrisonChinese.T(label.text);
            label.characterSize=Mathf.Min(label.characterSize,.027f);
            if(label.name=="Printed Chinese form")label.color=new Color(.10f,.13f,.11f);
        }
        font.RequestCharactersInTexture(string.Concat(Object.FindObjectsByType<TextMesh>(FindObjectsSortMode.None).Select(t=>t.text)),64);
        world.mainTexture=font.material.mainTexture;EditorUtility.SetDirty(world);
        // Make the starting station a readable place, instead of a floating oversized pickup label.
        foreach(var label in Object.FindObjectsByType<TextMesh>(FindObjectsSortMode.None))
            if(label.text.StartsWith("手电筒")) {label.characterSize=.006f;label.text="手电筒 · F 拾取";}
        if(GameObject.Find("Presentation dressing v1")!=null)return;
        var root=new GameObject("Presentation dressing v1").transform;
        foreach(var station in session.GetComponent<LastRollCall>().stations)
        {
            if(station.index==0 || station.index==1) DocumentProp(station, root);
            // Differing physical stations: keep original colliders and objectives intact.
            if(station.index==0)
            {
                var light=CorridorReviewBuilder.AddLight("Intake reading light",station.transform.position+station.transform.forward*.5f+Vector3.up*.8f,LightType.Point,new Color(.8f,.84f,.8f),.4f,3f);
                light.shadows=LightShadows.None;light.transform.SetParent(root);
            }
        }
        Debug.Log("PASS Chinese font, localized world signs, unified HUD and paused narrative briefing staged.");
    }
    private static void DocumentProp(EscapeStation station,Transform root)
    {
        foreach(Transform child in station.transform.Cast<Transform>().ToArray())
            if(child.name.Contains("power_box"))child.gameObject.SetActive(false);
        var board=new GameObject("Readable prison document").transform;board.SetParent(station.transform,false);
        var wood=new Material(Shader.Find("Universal Render Pipeline/Lit"));wood.color=new Color(.16f,.19f,.17f);
        var paper=new Material(wood);paper.color=new Color(.65f,.64f,.54f);
        string folder="Assets/ArtReview/HauntCoop/";
        string a=folder+"Document backing.mat",b=folder+"Document paper.mat";
        if(AssetDatabase.LoadAssetAtPath<Material>(a)==null)AssetDatabase.CreateAsset(wood,a);else {Object.DestroyImmediate(wood);wood=AssetDatabase.LoadAssetAtPath<Material>(a);}
        if(AssetDatabase.LoadAssetAtPath<Material>(b)==null)AssetDatabase.CreateAsset(paper,b);else {Object.DestroyImmediate(paper);paper=AssetDatabase.LoadAssetAtPath<Material>(b);}
        void Slab(string name,Vector3 size,Vector3 at,Material material)
        {var item=GameObject.CreatePrimitive(PrimitiveType.Cube);item.name=name;Object.DestroyImmediate(item.GetComponent<Collider>());item.transform.SetParent(board,false);item.transform.localPosition=at;item.transform.localScale=size;item.GetComponent<Renderer>().sharedMaterial=material;}
        Slab("Steel document board",new Vector3(.65f,.85f,.03f),Vector3.zero,wood);
        Slab("Transfer form",new Vector3(.54f,.72f,.003f),new Vector3(0,0,.02f),paper);
        Slab("Upper retaining clip",new Vector3(.17f,.035f,.02f),new Vector3(0,.36f,.035f),wood);
        var ink=new GameObject("Printed Chinese form").AddComponent<TextMesh>();ink.transform.SetParent(board,false);
        ink.transform.localPosition=new Vector3(0,.27f,.025f);ink.transform.localRotation=Quaternion.Euler(0,180,0);
        ink.anchor=TextAnchor.UpperCenter;ink.alignment=TextAlignment.Center;ink.font=PrisonChinese.Font;ink.fontSize=64;ink.characterSize=.009f;
        ink.color=new Color(.12f,.14f,.12f);ink.GetComponent<Renderer>().sharedMaterial=ink.font.material;
        ink.text=station.index==0?"地下监区\n最终转运通知\n────────\n封锁前完成查验\n两人同行\n禁止单独离区":"监区物品领取单\n────────\n转运证 / 维修工具\n登记后领取\n按住 F";
    }
}
