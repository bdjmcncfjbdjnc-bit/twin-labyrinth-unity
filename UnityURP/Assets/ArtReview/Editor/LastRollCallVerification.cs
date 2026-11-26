using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class LastRollCallVerification
{
    public static void Verify()
    {
        MazeVerification.Verify();
        foreach (int seed in Enumerable.Range(0, 50))
        {
            var plan = new EscapePlan(seed);
            if (plan.Advance(3, 3f, true, true, true) || plan.Advance(2, 3f, true, true, true))
                throw new Exception("Escape stages can be skipped.");
            if (!plan.Advance(0, 1f, true, false, false) || !plan.Advance(1, 1f, true, false, false))
                throw new Exception("Briefing and item collection failed.");
            plan.Advance(2, 1f, true, false, false);
            plan.Advance(2, .1f, false, false, false);
            if (plan.Progress != 0f || plan.Prepared) throw new Exception("Interrupted preparation persisted.");
            if (!plan.Advance(2, 3f, true, false, false)) throw new Exception("Preparation did not complete.");
            if (plan.Advance(3, 3f, true, true, false) || plan.Advance(3, 3f, true, false, true))
                throw new Exception("Final release ignored partner or power.");
            if (!plan.Advance(3, 3f, true, true, true) || !plan.Released) throw new Exception("Joint release failed.");
            if (!plan.Advance(4, 2f, true, true, true) || plan.Advance(4, 2f, true, true, true))
                throw new Exception("Cache is not single use.");
        }
        var rules = new PartnerProtocols(1080f);
        rules.Step(204f, false, false, false, false, false, false, false);
        rules.Step(10f, false, false, false, false, false, false, false, false, true);
        if (rules.NeedsHelp) throw new Exception("Life emergency overlaps detention.");
        rules.Step(2f, false, false, false, false, false, false, false);
        if (!rules.NeedsHelp) throw new Exception("Deferred emergency did not resume.");
        var scene = EditorSceneManager.OpenScene(MazeReviewBuilder.ScenePath);
        var mission = UnityEngine.Object.FindFirstObjectByType<LastRollCall>();
        var rooms=UnityEngine.Object.FindObjectsByType<PrisonRoom>(FindObjectsSortMode.None);
        if(rooms.Length!=6||rooms.Any(r=>r.entrance==null||r.cells.Length!=4||r.GetComponentsInChildren<BoxCollider>().Length<3))
            throw new Exception("Six furnished, enterable rooms required.");
        foreach(var label in UnityEngine.Object.FindObjectsByType<TextMesh>(FindObjectsSortMode.None))
            if(label.GetComponent<Renderer>().enabled&&label.name!="Printed Chinese form"&&label.name!="Installed sign lettering")
                throw new Exception("Obsolete floating label remains visible: "+label.name);
        Debug.Log("PASS six physical rooms with furnishings and gates; only installed signs and paper documents remain visible.");
        if (mission == null || mission.stations.Length != 5 || mission.stations.Any(s => s == null || s.GetComponent<Collider>() == null))
            throw new Exception("Missing mission interactions.");
        VerifyGateIntegration(mission);
        var hands = UnityEngine.Object.FindFirstObjectByType<PrisonHands>();
        if (hands.GetComponentsInChildren<Animator>(true).Length != 0)
            throw new Exception("A full-body Animator is overriding the first-person hand pose.");
        foreach (var skin in hands.GetComponentsInChildren<SkinnedMeshRenderer>())
            if (skin.quality != SkinQuality.Bone4) throw new Exception("First-person skin quality mismatch.");
        if(mission.GetComponent<PrisonPresentation>()==null || hands.GetComponentInChildren<HandGripBlend>()==null)
            throw new Exception("Missing Chinese presentation or imported grip transition.");
        if(PrisonChinese.Font==null || PrisonChinese.T("HOLD F AT ENTRY PANEL - SEND PARTNER THROUGH INNER GATE").Contains("HOLD"))
            throw new Exception("Chinese font or critical gate instruction missing.");
        foreach(var text in UnityEngine.Object.FindObjectsByType<TextMesh>(FindObjectsSortMode.None))
            if(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(text.gameObject)>0 || text.font!=PrisonChinese.Font)
                throw new Exception("Invalid localized world sign: "+text.name);
        Debug.Log("PASS Chinese font and gate instructions, world sign script references, imported finger grip transition.");
        const System.Reflection.BindingFlags privateFlags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
        var presentation=mission.GetComponent<PrisonPresentation>();
        typeof(PrisonPresentation).GetMethod("Awake",privateFlags).Invoke(presentation,null);
        typeof(PrisonPresentation).GetField("introduction",privateFlags).SetValue(presentation,false);
        presentation.ToggleNotebook();
        if(!PrisonPresentation.Blocked||Time.timeScale!=0)throw new Exception("Notebook did not pause the game.");
        presentation.ToggleNotebook();
        if(PrisonPresentation.Blocked||Time.timeScale!=1)throw new Exception("Notebook did not resume the game.");
        Debug.Log("PASS direct notebook bridge pauses and resumes the game without keyboard focus dependency.");
        Debug.Log("PASS Last Roll Call: 50 route seeds, task order, interrupted holds, joint release, one-use cache, deferred emergency, stable hand rigs.");
    }

    private static void VerifyGateIntegration(LastRollCall mission)
    {
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var session = mission.GetComponent<MazeSession>();
        var rules = new HauntRules();
        var plan = new EscapePlan(1);
        typeof(MazeSession).GetField("rules", flags).SetValue(session, rules);
        typeof(MazeSession).GetField("escape", flags).SetValue(session, mission);
        typeof(MazeSession).GetField("haunt", flags).SetValue(session, mission.GetComponent<HauntExperience>());
        typeof(LastRollCall).GetField("plan", flags).SetValue(mission, plan);
        var doors = (ReviewDoor[])typeof(MazeSession).GetField("exitDoors", flags).GetValue(session);
        rules.Activate(0); rules.Activate(1);
        session.OpenPreparedExit();
        if (doors.Any(door => !door.IsLocked)) throw new Exception("Power alone released the exit.");
        plan.Advance(0, 1f, true, true, true);
        plan.Advance(1, 1f, true, true, true);
        plan.Advance(2, 3f, true, true, true);
        plan.Advance(3, 3f, true, true, true);
        // The cue uses an AudioSource created by the runtime Awake.
        typeof(HauntExperience).GetMethod("Awake", flags).Invoke(mission.GetComponent<HauntExperience>(), null);
        var interlock = session.GetComponent<TransferInterlock>();
        if (interlock != null) typeof(TransferInterlock).GetMethod("Awake", flags).Invoke(interlock, null);
        session.OpenPreparedExit();
        if (interlock == null && doors.Any(door => door.IsLocked)) throw new Exception("Prepared exit did not unlock actual scene gates.");
        if (interlock != null && (interlock.innerGate.IsLocked || doors.Any(door => !door.IsLocked)))
            throw new Exception("Interlock must open only the inner gate before the partner handoff.");
        foreach (var station in mission.stations)
        {
            foreach (Vector3 position in new[] { station.transform.position, station.alternateLocation ? station.maintenancePosition : station.transform.position })
            {
                int x = Mathf.FloorToInt(position.x / MazeLayout.Pitch);
                int z = Mathf.FloorToInt(position.z / MazeLayout.Pitch);
                if (x < 0 || z < 0 || x >= MazeLayout.Size || z >= MazeLayout.Size)
                    throw new Exception("Task position is outside the connected maze.");
            }
        }
        Debug.Log("PASS integrated gate prerequisites and both route station locations.");
    }
}
