using System.Linq;
using UnityEditor;
using UnityEngine;

internal static class DistrictBuilder
{
    public static void Stage(MazeLayout layout, MazeSession session)
    {
        if(GameObject.Find("Prison architecture v3")!=null)return;
        if (GameObject.Find("Independent prison districts v2") != null) { Polish(layout); return; }
        var previous = GameObject.Find("Independent prison districts");
        if (previous != null) Object.DestroyImmediate(previous);
        foreach (string name in new[] { "Maze slab", "Maze concrete shell" })
            Object.DestroyImmediate(GameObject.Find(name));
        MazeReviewBuilder.MakeArchitecture(layout,
            AssetDatabase.LoadAssetAtPath<Material>("Assets/ArtReview/HauntCoop/LastCall-Floor.mat"),
            AssetDatabase.LoadAssetAtPath<Material>("Assets/ArtReview/HauntCoop/LastCall-Walls.mat"));
        var root = new GameObject("Independent prison districts v2").transform;
        string[] names = { "DUTY OFFICE", "LAUNDRY / SHOWERS", "REPAIR WORKSHOP" };
        string[][] props = { new[] { "metal_office_desk", "worn_metal_rack" },
            new[] { "industrial_storage_cart", "worn_metal_rack" }, new[] { "tool_cart", "power_box_01" } };
        var controls = new SerializedObject(session.GetComponent<CorridorReview>());
        var fixtures = controls.FindProperty("corridorLights");
        for (int n=fixtures.arraySize-1;n>=0;n--) if(fixtures.GetArrayElementAtIndex(n).objectReferenceValue==null) fixtures.DeleteArrayElementAtIndex(n);
        var mission = session.GetComponent<LastRollCall>();
        for (int district = 0; district < layout.Districts.Count; district++)
        {
            var cells = layout.Districts[district];
            var group = new GameObject(names[district]).transform; group.SetParent(root);
            int placed = 0;
            foreach (int cell in cells)
            {
                int side = Enumerable.Range(0, 4).FirstOrDefault(s => !layout.IsOpen(cell, s));
                if (layout.IsOpen(cell, side)) continue;
                Vector3 outward = MazeReviewBuilder.Direction(side), center = MazeLayout.Center(cell);
                var sign = CorridorReviewBuilder.PlaceRoomSign(names[district], center + outward * 1.85f + Vector3.up * 2.2f, Quaternion.LookRotation(outward));
                sign.characterSize = .038f; sign.transform.SetParent(group);
                var prop = Object.Instantiate(CorridorReviewBuilder.LoadEnvironment(props[district][placed % 2]), group);
                prop.transform.rotation = Quaternion.LookRotation(-outward);
                Bounds bounds = CorridorReviewBuilder.ModelBounds(prop);
                prop.transform.localScale *= Mathf.Min(1f, 1.6f / Mathf.Max(bounds.size.x, bounds.size.z));
                bounds = CorridorReviewBuilder.ModelBounds(prop);
                Vector3 goal = center + outward * 1.23f;
                prop.transform.position += new Vector3(goal.x - bounds.center.x, -bounds.min.y, goal.z - bounds.center.z);
                bounds = CorridorReviewBuilder.ModelBounds(prop);
                var obstacle = new GameObject("Furniture collision").AddComponent<BoxCollider>(); obstacle.transform.SetParent(group);
                obstacle.transform.position = bounds.center; obstacle.size = bounds.size;
                var lamp = Object.Instantiate(CorridorReviewBuilder.LoadEnvironment("industrial_wall_lamp"), group);
                lamp.transform.SetPositionAndRotation(center + outward*1.85f + Vector3.up*2.35f, Quaternion.LookRotation(-outward));
                var light = CorridorReviewBuilder.AddLight("District fixture", center + Vector3.up*2.3f, LightType.Point,
                    new Color(.72f,.78f,.8f), .9f, 6f);
                light.shadows = LightShadows.None; light.transform.SetParent(group);
                int slot = fixtures.arraySize++; fixtures.GetArrayElementAtIndex(slot).objectReferenceValue = light;
                if (placed == 0)
                {
                    EscapeStation station = mission.stations[district == 0 ? 1 : district == 1 ? 4 : 2];
                    station.transform.SetPositionAndRotation(center - outward*.8f + Vector3.up*1.2f, Quaternion.LookRotation(-outward));
                    station.alternateLocation = false;
                    // Stand-alone controls occupy the side of the room, leaving grid centres clear.
                    Vector3 tangent = Vector3.Cross(Vector3.up, outward);
                    station.transform.position = center + outward*1.65f + tangent*1.1f + Vector3.up*1.45f;
                    station.label.characterSize = .036f;
                }
                placed++;
            }
            if (placed == 0) throw new System.InvalidOperationException("District has no exterior wall for furnishings.");
            if (district == 1) Showers(group, cells, layout);
        }
        controls.ApplyModifiedPropertiesWithoutUndo();
        StageInterlock(layout, session);
        foreach (var nav in Object.FindObjectsByType<MazeCompanion>(FindObjectsSortMode.None)) nav.SetPassages(layout.CopyPassages());
        Polish(layout);
    }

    private static void Polish(MazeLayout layout)
    {
        Transform root=GameObject.Find("Independent prison districts v2").transform;
        if(root.Find("District polish")!=null)return;
        var marker=new GameObject("District polish").transform;marker.SetParent(root);
        foreach(var text in root.GetComponentsInChildren<TextMesh>())
        {text.characterSize=.022f;var p=text.transform.position;p.y=2.03f;text.transform.position=p;}
        foreach(var light in root.GetComponentsInChildren<Light>()) light.intensity=.75f;
        foreach(var mist in root.GetComponentsInChildren<ParticleSystem>())
        {mist.transform.rotation=Quaternion.Euler(-90,0,0);var shape=mist.shape;shape.scale=new Vector3(2.8f,2.8f,.1f);}
        var workshop=root.Find("REPAIR WORKSHOP");
        foreach(Transform child in workshop.Cast<Transform>().ToArray())
        {
            if(!child.name.Contains("power_box"))continue;
            var bounds=CorridorReviewBuilder.ModelBounds(child.gameObject);
            child.position+=Vector3.up*1.1f;
            foreach(var box in workshop.GetComponentsInChildren<BoxCollider>())
                if((box.bounds.center-bounds.center).sqrMagnitude<.01f)box.transform.position+=Vector3.up*1.1f;
        }
        int cell=layout.Districts[1].Last();Vector3 center=MazeLayout.Center(cell);
        int side=Enumerable.Range(0,4).FirstOrDefault(s=>!layout.IsOpen(cell,s));
        if(!layout.IsOpen(cell,side))
        {
            Vector3 outward=MazeReviewBuilder.Direction(side), tangent=Vector3.Cross(Vector3.up,outward);
            for(int i=0;i<2;i++)
            {
                var divider=GameObject.CreatePrimitive(PrimitiveType.Cube);divider.name="Concrete wash-stall divider";divider.transform.SetParent(marker);
                divider.transform.SetPositionAndRotation(center+outward*1.35f+tangent*(i==0?-.6f:.6f)+Vector3.up*1.05f,Quaternion.LookRotation(outward));
                divider.transform.localScale=new Vector3(.08f,2.1f,.9f);
                divider.GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/ArtReview/HauntCoop/LastCall-Walls.mat");
            }
            var drain=Object.Instantiate(CorridorReviewBuilder.LoadModel("Assets/ArtReview/Source/Environment/dungeon_cell_bar_front.glb"),marker);
            drain.name="Wash-stall floor grate";drain.transform.rotation=Quaternion.Euler(90,0,0);
            var bounds=CorridorReviewBuilder.ModelBounds(drain);
            drain.transform.localScale=Vector3.Scale(drain.transform.localScale,new Vector3(.45f/bounds.size.x,.45f/bounds.size.z,.012f/bounds.size.y));
            bounds=CorridorReviewBuilder.ModelBounds(drain);
            drain.transform.position+=center+outward*1.3f+Vector3.up*.01f-bounds.center;
        }
    }

    internal static void Showers(Transform parent, int[] cells, MazeLayout layout)
    {
        int cell = cells.Last(); Vector3 center = MazeLayout.Center(cell);
        for (int i=0;i<3;i++)
        {
            var pipe = Object.Instantiate(CorridorReviewBuilder.LoadEnvironment("modular_industrial_pipes_01"), parent);
            var bounds = CorridorReviewBuilder.ModelBounds(pipe);
            pipe.transform.localScale *= 1.3f / bounds.size.magnitude;
            pipe.transform.position += center + new Vector3(-.8f+i*.8f,2.45f,0) - CorridorReviewBuilder.ModelBounds(pipe).center;
        }
        var mist = new GameObject("Warm pipe steam").AddComponent<ParticleSystem>(); mist.transform.SetParent(parent);
        mist.transform.position = center + Vector3.up*.8f;mist.transform.rotation=Quaternion.Euler(-90,0,0);
        var main = mist.main; main.startLifetime=2.8f; main.startSpeed=.38f; main.startSize=1.1f; main.maxParticles=40;
        main.startColor=new Color(.5f,.57f,.6f,.15f); main.simulationSpace=ParticleSystemSimulationSpace.World;
        var emission=mist.emission; emission.rateOverTime=8;
        var shape=mist.shape; shape.shapeType=ParticleSystemShapeType.Box; shape.scale=new Vector3(2.8f,2.8f,.1f);
        string path="Assets/ArtReview/HauntCoop/Steam.mat";
        var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(mat==null)
        {
            var texture=new Texture2D(32,32,TextureFormat.RGBA32,false);
            for(int y=0;y<32;y++) for(int x=0;x<32;x++) {float a=Mathf.Clamp01(1f-new Vector2(x-15.5f,y-15.5f).magnitude/15.5f);texture.SetPixel(x,y,new Color(1,1,1,a*a*.25f));}
            texture.Apply();AssetDatabase.CreateAsset(texture,"Assets/ArtReview/HauntCoop/SteamTexture.asset");
            mat=new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));mat.SetTexture("_BaseMap",texture);
            mat.SetFloat("_Surface",1);mat.SetFloat("_Blend",0);mat.SetFloat("_SrcBlend",5);mat.SetFloat("_DstBlend",10);mat.SetFloat("_ZWrite",0);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");mat.renderQueue=3000;AssetDatabase.CreateAsset(mat,path);
        }
        mist.GetComponent<ParticleSystemRenderer>().sharedMaterial=mat;
        mist.gameObject.AddComponent<PrisonSteam>();
    }

    private static void StageInterlock(MazeLayout layout, MazeSession session)
    {
        if(session.GetComponent<TransferInterlock>() != null) return;
        var original = new SerializedObject(session).FindProperty("exitDoors");
        var doors = Enumerable.Range(0,original.arraySize).Select(i=>(ReviewDoor)original.GetArrayElementAtIndex(i).objectReferenceValue).ToArray();
        Vector3 from=MazeLayout.Center(layout.Route[155]),to=MazeLayout.Center(layout.Route[156]);
        var inner=CorridorReviewBuilder.AddEntrance(CorridorReviewBuilder.LoadModel("Assets/ArtReview/Source/Environment/dungeon_cell_bar_front.glb"),
            AssetDatabase.LoadAssetAtPath<Material>("Assets/ArtReview/Expanded/Steel.mat"),AssetDatabase.LoadAssetAtPath<Material>("Assets/ArtReview/Expanded/Walls.mat"),
            "TRANSFER INNER GATE",(from+to)/2,Quaternion.LookRotation(to-from).eulerAngles.y,MazeLayout.Pitch);
        inner.Lock();
        var interlock=session.gameObject.AddComponent<TransferInterlock>();interlock.innerGate=inner;interlock.outerGates=doors;
        interlock.entryConsole=session.GetComponent<LastRollCall>().stations[3].transform;
        interlock.assemblyPoint=new GameObject("Return control standing position").transform;
        interlock.assemblyPoint.position=MazeLayout.Center(layout.Route[157]);
        int wallSide=MazeReviewBuilder.WallSide(layout,layout.Route[157]);Vector3 wall=MazeReviewBuilder.Direction(wallSide);
        interlock.assemblyPoint.position += wall*.8f;
        interlock.returnConsole=Object.Instantiate(CorridorReviewBuilder.LoadEnvironment("power_box_01")).transform;
        interlock.returnConsole.position=interlock.assemblyPoint.position+wall*.65f+Vector3.up*1.2f;
        interlock.returnConsole.rotation=Quaternion.LookRotation(-wall);
        interlock.status=CorridorReviewBuilder.PlaceRoomSign("RETURN CONTROL",interlock.assemblyPoint.position+wall*1.85f+Vector3.up*2.05f,Quaternion.LookRotation(wall));
        interlock.status.characterSize=.025f;
        foreach(var nav in Object.FindObjectsByType<MazeCompanion>(FindObjectsSortMode.None))
        {
            var serialized=new SerializedObject(nav);var gates=serialized.FindProperty("gates");int n=gates.arraySize++;gates.GetArrayElementAtIndex(n).objectReferenceValue=inner;serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        session.PartnerTransform.gameObject.AddComponent<PartnerAction>();
    }
}
