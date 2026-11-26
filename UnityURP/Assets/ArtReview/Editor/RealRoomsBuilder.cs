using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

internal static class RealRoomsBuilder
{
    const string RootName="Prison architecture v3";
    static Material paint,trim,plate;
    public static void Stage(MazeLayout layout,MazeSession session)
    {
        if(GameObject.Find(RootName)!=null){RepairCells(layout,session);NavigationSigns(layout);RefineCharacters();CleanLabels();return;}
        var root=new GameObject(RootName).transform;
        paint=Mat("Lower wall paint",new Color(.24f,.29f,.26f),.24f);
        trim=Mat("Worn architectural steel",new Color(.11f,.13f,.12f),.28f);
        plate=Mat("Enamel signs",new Color(.065f,.075f,.07f),.2f);
        var old=GameObject.Find("Independent prison districts v2");if(old!=null)Object.DestroyImmediate(old);
        foreach(string name in new[]{"Maze slab","Maze concrete shell"})Object.DestroyImmediate(GameObject.Find(name));
        var walls=AssetDatabase.LoadAssetAtPath<Material>("Assets/ArtReview/HauntCoop/LastCall-Walls.mat");
        MazeReviewBuilder.MakeArchitecture(layout,AssetDatabase.LoadAssetAtPath<Material>("Assets/ArtReview/HauntCoop/LastCall-Floor.mat"),walls);
        // Remove obsolete internal gates from enlarged rooms, but never touch the final interlock.
        var critical=session.GetComponent<TransferInterlock>();
        foreach(var door in Object.FindObjectsByType<ReviewDoor>(FindObjectsSortMode.None))
        {
            if(door==critical.innerGate||critical.outerGates.Contains(door))continue;
            foreach(var cells in layout.Districts)
            {
                bool internalEdge=cells.Any(c=>Enumerable.Range(0,4).Any(s=>cells.Contains(MazeLayout.Neighbor(c,s))&&
                    Vector3.Distance(new Vector3(door.transform.position.x,0,door.transform.position.z),MazeLayout.Center(c)+MazeReviewBuilder.Direction(s)*2)<.2f));
                if(internalEdge){Object.DestroyImmediate(door.gameObject);break;}
            }
        }
        string[] names={"值班室","洗衣淋浴间","维修间","医务室","档案室","备品库"};
        string[][] props={new[]{"metal_office_desk","metal_stool_02","drawer_cabinet","worn_metal_rack"},
            new[]{"industrial_storage_cart","worn_metal_rack","metal_stool_02","industrial_storage_cart"},
            new[]{"tool_cart","metal_toolbox","worn_metal_rack","power_box_01"},
            new[]{"old_bed_frame","wheelchair_01","old_bed_frame","metal_stool_02"},
            new[]{"drawer_cabinet","worn_metal_rack","drawer_cabinet","metal_office_desk"},
            new[]{"industrial_storage_cart","worn_metal_rack","tool_cart","drawer_cabinet"}};
        var controller=new SerializedObject(session.GetComponent<CorridorReview>());var lights=controller.FindProperty("corridorLights");
        for(int i=lights.arraySize-1;i>=0;i--)if(lights.GetArrayElementAtIndex(i).objectReferenceValue==null)lights.DeleteArrayElementAtIndex(i);
        for(int r=0;r<layout.Districts.Count;r++)
        {
            int[] cells=layout.Districts[r];var room=new GameObject(names[r]).AddComponent<PrisonRoom>();room.transform.SetParent(root);
            room.roomName=names[r];room.cells=cells;
            var center=cells.Select(MazeLayout.Center).Aggregate(Vector3.zero,(a,b)=>a+b)/cells.Length;
            room.volume=new Bounds(center+Vector3.up*1.4f,new Vector3(7.8f,2.8f,7.8f));
            int p=0;
            foreach(int cell in cells)
            {
                int side=Enumerable.Range(0,4).FirstOrDefault(s=>!layout.IsOpen(cell,s));
                if(layout.IsOpen(cell,side))continue;
                Vector3 outward=MazeReviewBuilder.Direction(side),tangent=Vector3.Cross(Vector3.up,outward),c=MazeLayout.Center(cell);
                string asset=props[r][p%props[r].Length];
                // Beds run along the wall at adult scale, leaving the cell centre and door lanes clear.
                var furniture=Prop(asset,room.transform,c+outward*1.22f,Quaternion.LookRotation(-outward),asset=="old_bed_frame"?2.1f:1.55f);
                if(r==0&&p==0)
                {
                    var top=CorridorReviewBuilder.ModelBounds(furniture).max.y;
                    Prop("vintage_radio_transceiver",room.transform,c+outward*1.22f+Vector3.up*top,Quaternion.LookRotation(-outward),.42f,false);
                }
                if(p==0&&r<3)
                {
                    var station=session.GetComponent<LastRollCall>().stations[r==0?1:r==1?4:2];
                    station.transform.SetPositionAndRotation(c+outward*1.68f+tangent*1.15f+Vector3.up*1.35f,Quaternion.LookRotation(-outward));
                    station.alternateLocation=false;
                }
                var fixture=Object.Instantiate(CorridorReviewBuilder.LoadEnvironment("industrial_wall_lamp"),room.transform);
                fixture.transform.SetPositionAndRotation(c+outward*1.85f+Vector3.up*2.35f,Quaternion.LookRotation(-outward));
                var lamp=CorridorReviewBuilder.AddLight("Room practical",c+Vector3.up*2.4f,LightType.Point,new Color(.79f,.82f,.78f),.65f,5.5f);
                lamp.shadows=LightShadows.None;lamp.transform.SetParent(room.transform);int k=lights.arraySize++;lights.GetArrayElementAtIndex(k).objectReferenceValue=lamp;
                p++;
            }
            if(r==1)DistrictBuilder.Showers(room.transform,cells,layout);
            // Every connection into the room gets a physical doorway, rather than a floating room title.
            foreach(int cell in cells)for(int side=0;side<4;side++)
            {
                int next=MazeLayout.Neighbor(cell,side);if(!layout.IsOpen(cell,side)||cells.Contains(next))continue;
                Vector3 outward=MazeReviewBuilder.Direction(side),threshold=MazeLayout.Center(cell)+outward*2;
                var door=Object.FindObjectsByType<ReviewDoor>(FindObjectsSortMode.None).FirstOrDefault(d=>Vector3.Distance(d.transform.position,threshold)<.2f);
                if(door==null)door=CorridorReviewBuilder.AddEntrance(CorridorReviewBuilder.LoadModel("Assets/ArtReview/Source/Environment/dungeon_cell_bar_front.glb"),
                    AssetDatabase.LoadAssetAtPath<Material>("Assets/ArtReview/Expanded/Steel.mat"),walls,names[r]+"入口",threshold,Quaternion.LookRotation(outward).eulerAngles.y,4);
                if(room.entrance==null)
                {
                    room.entrance=door.transform;
                    Plaque(root,(r+1).ToString("00")+"  "+names[r],threshold+outward*.18f+Vector3.Cross(Vector3.up,outward)*1.55f+Vector3.up*1.65f,Quaternion.LookRotation(-outward),.7f);
                }
            }
        }
        controller.ApplyModifiedPropertiesWithoutUndo();
        var details=new GameObject("Architectural finishes").transform;details.SetParent(root);
        ArchitecturalDetail(layout,details);CombineDetails(details);
        var allDoors=Object.FindObjectsByType<ReviewDoor>(FindObjectsSortMode.None);
        foreach(var nav in Object.FindObjectsByType<MazeCompanion>(FindObjectsSortMode.None))
        {nav.SetPassages(layout.CopyPassages());SetDoors(nav,allDoors);}
        // Keep incident selection limited to its surviving original checkpoints.
        var incidents=session.GetComponent<PrisonIncidents>();var incidentData=new SerializedObject(incidents);var original=incidentData.FindProperty("gates");
        for(int i=original.arraySize-1;i>=0;i--)if(original.GetArrayElementAtIndex(i).objectReferenceValue==null)original.DeleteArrayElementAtIndex(i);
        incidentData.ApplyModifiedPropertiesWithoutUndo();
        foreach(var relay in Object.FindObjectsByType<HauntRelay>(FindObjectsSortMode.None))
            Plaque(root,relay.Index==0?"配电 A":"配电 B",relay.transform.position+Vector3.up*.7f,Quaternion.LookRotation(-relay.transform.forward),.55f);
        foreach(var door in allDoors.Where(d=>d.name.Contains("Cell")||d.name.Contains("cell")))
            Plaque(root,"监舍",door.transform.position+door.transform.right*1.55f+Vector3.up*1.65f,door.transform.rotation,.48f);
        RepairCells(layout,session);NavigationSigns(layout);CleanLabels();RefineCharacters();
        Debug.Log("PASS six furnished physical rooms staged; obsolete floating labels hidden; room portals and navigation updated.");
    }
    static void RepairCells(MazeLayout layout,MazeSession session)
    {
        var doors=Object.FindObjectsByType<ReviewDoor>(FindObjectsSortMode.None).ToList();
        int count=doors.Count(d=>d.name.StartsWith("CELL A"));
        var root=GameObject.Find(RootName).transform;
        plate=Mat("Enamel signs",new Color(.065f,.075f,.07f),.2f);
        for(int cell=0;cell<MazeLayout.Size*MazeLayout.Size&&count<16;cell++)
        {
            if(cell==MazeLayout.Start||cell==layout.Exit||layout.Districts.Any(r=>r.Contains(cell)))continue;
            var sides=Enumerable.Range(0,4).Where(s=>layout.IsOpen(cell,s)).ToArray();if(sides.Length!=1)continue;
            var outward=MazeReviewBuilder.Direction(sides[0]);var c=MazeLayout.Center(cell);var threshold=c+outward*2;
            if(doors.Any(d=>Vector3.Distance(d.transform.position,threshold)<.2f))continue;
            var door=CorridorReviewBuilder.AddEntrance(CorridorReviewBuilder.LoadModel("Assets/ArtReview/Source/Environment/dungeon_cell_bar_front.glb"),
                AssetDatabase.LoadAssetAtPath<Material>("Assets/ArtReview/Expanded/Steel.mat"),AssetDatabase.LoadAssetAtPath<Material>("Assets/ArtReview/HauntCoop/LastCall-Walls.mat"),
                "CELL A NEW"+(++count),threshold,Quaternion.LookRotation(-outward).eulerAngles.y,4);
            doors.Add(door);
            Prop("old_bed_frame",root,c-outward*1.25f,Quaternion.LookRotation(outward),2.1f);
            Plaque(root,"监舍 "+count,threshold+outward*.16f+Vector3.Cross(Vector3.up,outward)*1.55f+Vector3.up*1.65f,Quaternion.LookRotation(-outward),.6f);
        }
        if(count!=16)throw new System.Exception("Cannot retain sixteen prison cells after expansion: "+count);
        foreach(var nav in Object.FindObjectsByType<MazeCompanion>(FindObjectsSortMode.None))SetDoors(nav,doors.ToArray());
    }
    static void SetDoors(Object target,ReviewDoor[] doors)
    {var so=new SerializedObject(target);var array=so.FindProperty("gates");array.arraySize=doors.Length;for(int i=0;i<doors.Length;i++)array.GetArrayElementAtIndex(i).objectReferenceValue=doors[i];so.ApplyModifiedPropertiesWithoutUndo();}
    static void NavigationSigns(MazeLayout layout)
    {
        var root=GameObject.Find(RootName).transform;
        foreach(var marker in new[]{(MazeLayout.Start,"收押区 · 起点"),(layout.Exit,"地面通道 · 出口")})
        {
            if(GameObject.Find("Installed sign "+marker.Item2)!=null)continue;
            int side=MazeReviewBuilder.WallSide(layout,marker.Item1);if(layout.IsOpen(marker.Item1,side))continue;
            var outward=MazeReviewBuilder.Direction(side);
            Plaque(root,marker.Item2,MazeLayout.Center(marker.Item1)+outward*1.83f+Vector3.up*1.9f,Quaternion.LookRotation(outward),.9f);
        }
        foreach(var door in Object.FindObjectsByType<ReviewDoor>(FindObjectsSortMode.None).Where(d=>d.name.StartsWith("CELL A")))
        {
            string label="监舍 "+door.name.Replace("CELL A","").Replace(" gate","").Replace("NEW","").Trim();
            if(GameObject.Find("Installed sign "+label)!=null)continue;
            Plaque(root,label,door.transform.position+door.transform.right*1.8f+Vector3.up*1.65f-door.transform.forward*.16f,door.transform.rotation,.55f);
        }
    }
    static GameObject Prop(string asset,Transform parent,Vector3 floor,Quaternion rotation,float width,bool collider=true)
    {
        var obj=Object.Instantiate(CorridorReviewBuilder.LoadEnvironment(asset),parent);obj.transform.rotation=rotation;
        if(asset=="old_bed_frame")
        {
            var bounds=CorridorReviewBuilder.ModelBounds(obj);var normal=rotation*Vector3.forward;
            float depth=Mathf.Abs(normal.x)*bounds.size.x+Mathf.Abs(normal.z)*bounds.size.z;
            float length=Mathf.Abs(normal.z)*bounds.size.x+Mathf.Abs(normal.x)*bounds.size.z;
            if(depth>length)obj.transform.rotation*=Quaternion.Euler(0,90,0);
        }
        var b=CorridorReviewBuilder.ModelBounds(obj);obj.transform.localScale*=Mathf.Min(1,width/Mathf.Max(b.size.x,b.size.z));b=CorridorReviewBuilder.ModelBounds(obj);
        obj.transform.position+=new Vector3(floor.x-b.center.x,floor.y-b.min.y,floor.z-b.center.z);b=CorridorReviewBuilder.ModelBounds(obj);
        if(collider){var box=new GameObject("Room furnishing collision").AddComponent<BoxCollider>();box.transform.SetParent(parent);box.transform.position=b.center;box.size=b.size;}
        return obj;
    }
    static Material Mat(string name,Color color,float smooth)
    {string path="Assets/ArtReview/HauntCoop/"+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);if(mat==null){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,path);}mat.color=color;mat.SetFloat("_Smoothness",smooth);return mat;}
    static void Box(Transform root,string name,Vector3 position,Vector3 size,Material mat,Quaternion rotation)
    {var obj=GameObject.CreatePrimitive(PrimitiveType.Cube);obj.name=name;Object.DestroyImmediate(obj.GetComponent<Collider>());obj.transform.SetParent(root);obj.transform.SetPositionAndRotation(position,rotation);obj.transform.localScale=size;obj.GetComponent<Renderer>().sharedMaterial=mat;}
    static void Plaque(Transform root,string text,Vector3 position,Quaternion rotation,float width)
    {
        var panel=new GameObject("Installed sign "+text).transform;panel.SetParent(root);panel.SetPositionAndRotation(position,rotation);
        Box(panel,"Enamel backing",position,new Vector3(width,.21f,.026f),plate,rotation);
        var label=CorridorReviewBuilder.PlaceRoomSign(text,position-rotation*Vector3.forward*.017f,rotation);label.transform.SetParent(panel);label.characterSize=.005f;label.fontSize=64;label.name="Installed sign lettering";
    }
    static void ArchitecturalDetail(MazeLayout layout,Transform root)
    {
        for(int cell=0;cell<MazeLayout.Size*MazeLayout.Size;cell++)for(int side=0;side<4;side++)
        {
            if(layout.IsOpen(cell,side))continue;
            Vector3 outward=MazeReviewBuilder.Direction(side),center=MazeLayout.Center(cell)+outward*1.86f;
            var rotation=Quaternion.LookRotation(outward);
            Box(root,"Painted lower wall",center+Vector3.up*.62f,new Vector3(3.75f,1.24f,.012f),paint,rotation);
            Box(root,"Steel wall rail",center-outward*.016f+Vector3.up*1.25f,new Vector3(3.75f,.055f,.035f),trim,rotation);
            Box(root,"Concrete panel joint",center-outward*.02f+Vector3.up*2.02f,new Vector3(.016f,1.5f,.016f),trim,rotation);
        }
        for(int i=3;i<layout.Route.Count;i+=8)
        {
            Vector3 center=MazeLayout.Center(layout.Route[i]);Vector3 forward=(MazeLayout.Center(layout.Route[i+1])-center).normalized;
            Box(root,"Ceiling lintel",center+Vector3.up*2.68f,new Vector3(3.78f,.20f,.23f),trim,Quaternion.LookRotation(forward));
        }
    }
    static void CleanLabels()
    {foreach(var text in Object.FindObjectsByType<TextMesh>(FindObjectsSortMode.None))text.GetComponent<Renderer>().enabled=text.name=="Printed Chinese form"||text.name=="Installed sign lettering";}
    static void CombineDetails(Transform root)
    {
        var filters=root.GetComponentsInChildren<MeshFilter>();
        foreach(var group in filters.GroupBy(f=>f.GetComponent<Renderer>().sharedMaterial))
        {
            var mesh=new Mesh{indexFormat=IndexFormat.UInt32};
            mesh.CombineMeshes(group.Select(f=>new CombineInstance{mesh=f.sharedMesh,transform=f.transform.localToWorldMatrix}).ToArray());
            string path="Assets/ArtReview/HauntCoop/Architecture-"+group.Key.name+".asset";
            AssetDatabase.DeleteAsset(path);AssetDatabase.CreateAsset(mesh,path);
            var obj=new GameObject(group.Key.name);obj.transform.SetParent(root);obj.AddComponent<MeshFilter>().sharedMesh=mesh;obj.AddComponent<MeshRenderer>().sharedMaterial=group.Key;
        }
        foreach(var filter in filters)Object.DestroyImmediate(filter.gameObject);
    }
    static void RefineCharacters()
    {
        foreach(var skin in Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsSortMode.None))
        {
            skin.quality=SkinQuality.Bone4;
            if(skin.GetComponentInParent<PrisonHands>()!=null)continue;
            skin.sharedMaterials=skin.sharedMaterials.Select(source=>
            {
                var material=source;
                if(!AssetDatabase.GetAssetPath(source).Contains("/CharacterSurface-")&&AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source,out string guid,out long id))
                {
                    string path="Assets/ArtReview/HauntCoop/CharacterSurface-"+guid+"-"+id+".mat";
                    material=AssetDatabase.LoadAssetAtPath<Material>(path);
                    if(material==null){material=new Material(source);AssetDatabase.CreateAsset(material,path);}
                }
                bool skinSurface=source.name.ToLowerInvariant().Contains("skin")||source.name.ToLowerInvariant().Contains("body");
                if(material.HasProperty("roughnessFactor"))material.SetFloat("roughnessFactor",skinSurface?.64f:.86f);
                if(material.HasProperty("metallicFactor"))material.SetFloat("metallicFactor",0);
                EditorUtility.SetDirty(material);return material;
            }).ToArray();
        }
    }
}
