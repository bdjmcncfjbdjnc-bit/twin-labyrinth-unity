using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public static class MazeVerification
{
    public static void Verify()
    {
        var scene = EditorSceneManager.OpenScene(MazeReviewBuilder.ScenePath);
        var layout = MazeLayout.Create();
        int[] distances = layout.Trace(out _);
        var player = scene.GetRootGameObjects().Single(root => root.GetComponent<MazeSession>() != null);
        VerifyHaunt(scene.GetRootGameObjects(), player);
        VerifyProtocols();
        var session = player.GetComponent<MazeSession>();
        var companion = session.PartnerTransform.GetComponent<MazeCompanion>();
        if (companion == null || !player.GetComponent<MazeSession>().HasPartner)
            throw new InvalidOperationException("Missing AI partner.");
        companion.Initialize();
        var officer = UnityEngine.Object.FindFirstObjectByType<MazeGuard>();
        if (officer == null)
            throw new InvalidOperationException("Missing security guard.");
        officer.GetComponent<MazeCompanion>().Initialize();
        officer.IgnorePartnerCollision();
        var exit = new SerializedObject(player.GetComponent<MazeSession>()).FindProperty("exit").vector3Value;
        float metres = distances[layout.Exit] * MazeLayout.Pitch;
        if (Vector3.Distance(player.transform.position, MazeLayout.Center(MazeLayout.Start)) > .1f ||
            exit != MazeLayout.Center(layout.Exit) || distances.Any(distance => distance < 0) ||
            Mathf.Abs(metres / CorridorReview.WalkSpeed - 300f) > 15f || layout.Loops < 10)
            throw new InvalidOperationException("Maze endpoint, connectivity, or walk time target failed.");

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.TryGetComponent(out ReviewDoor door))
            {
                door.Initialize();
                door.Unlock();
                door.RequestPassage();
                door.Step(1f);
            }
        }
        Physics.SyncTransforms();
        int tested = 0;
        for (int cell = 0; cell < distances.Length; cell++)
        {
            for (int direction = 0; direction < 2; direction++)
            {
                if (MazeLayout.Neighbor(cell, direction) < 0)
                    continue;
                Vector3 center = MazeLayout.Center(cell);
                bool blocked = Physics.CapsuleCast(center + Vector3.up * .29f, center + Vector3.up * 1.51f,
                    .27f, MazeReviewBuilder.Direction(direction), out RaycastHit hit, MazeLayout.Pitch, Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore);
                if (blocked == layout.IsOpen(cell, direction))
                    throw new InvalidOperationException($"Topology/collision mismatch at cell {cell}, direction {direction}, " +
                        $"hit {(hit.collider == null ? "(clear)" : hit.collider.name)}.");
                tested++;
            }
        }
        Debug.Log($"PASS maze: {distances.Length} reachable cells, {tested} edges, {layout.Loops} loops. " +
            $"Shortest route: {metres:F0} m at {CorridorReview.WalkSpeed:F1} m/s = {metres / CorridorReview.WalkSpeed:F1} s.");

        VerifyWalk(player.GetComponent<CharacterController>(), layout, exit, companion);
        VerifyRig("Intake orderly", 80);
        VerifyRig("Infirmary worker", 80);
        VerifyRig("Records clerk", 80);
        VerifyRig("The Imitator", 53);
        VerifyRig("Partner", 53);
        VerifyRig("Prison guard", 80);
        if (!session.HasGuard || UnityEngine.Object.FindObjectsByType<PrisonDecoy>(FindObjectsSortMode.None).Length != 5)
            throw new InvalidOperationException("Missing guard or staged sound distractions.");
        var hands = player.GetComponentInChildren<PrisonHands>();
        if (hands == null)
            throw new InvalidOperationException("Missing first-person hands.");
        var rig = new SerializedObject(hands);
        foreach (string side in new[] { "left", "right" })
        {
            var hand = (Transform)rig.FindProperty(side).objectReferenceValue;
            if (hand == null || !hand.GetComponentsInChildren<SkinnedMeshRenderer>().Any(skin => skin.sharedMesh.triangles.Length > 0))
                throw new InvalidOperationException("Missing skinned mesh for " + side + " first-person hand.");
        }
        var camera = player.GetComponentInChildren<Camera>();
        Camera overlay = camera.GetUniversalAdditionalCameraData().cameraStack.Single();
        if (!overlay.GetUniversalAdditionalCameraData().clearDepth || (camera.cullingMask & overlay.cullingMask) != 0)
            throw new InvalidOperationException("Hands must render over scene depth without sharing the world culling mask.");
        VerifyEscort(session, officer, companion);
        VerifyIncidents(player);
        VerifyGuidance(session);
        VerifyInmates(session);
        Debug.Log("PASS Marcus guard, five decoys and two skinned first-person arms.");
    }

    private static void VerifyInmates(MazeSession session)
    {
        var roster = session.GetComponent<PrisonInmateRoster>();
        var actors = UnityEngine.Object.FindObjectsByType<PrisonInmate>(FindObjectsSortMode.None);
        if (roster == null || actors.Length != 3)
            throw new InvalidOperationException("Missing hidden-role inmate cast.");
        foreach (PrisonInmate actor in actors)
        {
            var knife = new SerializedObject(actor).FindProperty("knife").objectReferenceValue as GameObject;
            if (knife == null || knife.activeSelf || knife.GetComponentsInChildren<MeshRenderer>(true).Length == 0)
                throw new InvalidOperationException("All inmate models must have a hidden, renderable knife attachment.");
        }
        var random = new System.Random(43);
        if (Enumerable.Range(0, 30).Select(_ => PrisonInmateRoster.SelectArmed(3, random)).Distinct().Count() != 3)
            throw new InvalidOperationException("Every inmate must be eligible for the hidden-knife role.");
        var aggressor = new InmateThreat(InmateThreat.Temperament.Aggressive);
        if (aggressor.Step(2f, 3f, false, true) || aggressor.KnifeVisible ||
            aggressor.Step(.7f, 3f, true, true) || !aggressor.KnifeVisible ||
            aggressor.Step(.1f, 5.1f, true, true) || aggressor.KnifeVisible)
            throw new InvalidOperationException("Warning cannot hit, and retreat must hide the knife.");
        var defensive = new InmateThreat(InmateThreat.Temperament.Defensive);
        defensive.Step(5f, 3f, true, true);
        if (defensive.KnifeVisible)
            throw new InvalidOperationException("Defensive role should tolerate space.");
        var feigning = new InmateThreat(InmateThreat.Temperament.Feigning);
        feigning.Step(2.2f, 1f, true, true);
        feigning.Step(1.5f, 1f, true, true);
        if (!feigning.Step(.1f, 1f, true, true) || feigning.Step(.1f, 1f, true, true))
            throw new InvalidOperationException("Contact must trigger one penalty before cooldown.");
        InmateThreat Pursuing()
        {
            var threat = new InmateThreat(InmateThreat.Temperament.Aggressive);
            threat.Step(.7f, 3f, true, true);
            threat.Step(1.1f, 3f, true, true);
            return threat;
        }
        var corner = Pursuing();
        if (corner.Step(1f, 4f, false, true) || corner.Current != InmateThreat.State.Pursuit ||
            corner.Step(.55f, 4f, false, true) || corner.Current != InmateThreat.State.Recovering)
            throw new InvalidOperationException("The inmate must briefly chase after sight loss, then give up.");
        var boundary = Pursuing();
        if (boundary.Step(.02f, 1f, true, true, false) || boundary.Current != InmateThreat.State.Recovering)
            throw new InvalidOperationException("Leaving the chase territory must end pursuit before contact.");
        var limit = Pursuing();
        if (limit.Step(InmateThreat.PursuitDuration, 1f, true, true) || limit.Current != InmateThreat.State.Recovering)
            throw new InvalidOperationException("Timed-out pursuit must end before contact.");
        Debug.Log("PASS local pursuit has corner memory, territory boundary and a five-second timeout.");
        Debug.Log("PASS shareable inmate models, randomized hidden knife, warning retreat and single contact penalty.");
    }

    private static void VerifyGuidance(MazeSession session)
    {
        var rules = new HauntRules();
        if (rules.AdvanceRelayB(1.1f, true) || rules.AdvanceRelayB(.02f, false) ||
            rules.RelayBProgress != 0f || rules.AdvanceRelayB(1.2f, true) || !rules.AdvanceRelayB(1.1f, true))
            throw new InvalidOperationException("Relay B must accumulate uninterrupted interaction.");
        var guidance = session.GetComponent<PrisonGuidance>();
        var boards = UnityEngine.Object.FindObjectsByType<PrisonRouteBoard>(FindObjectsSortMode.None);
        if (guidance == null || boards.Length != 4)
            throw new InvalidOperationException("Missing route notes or their guidance controller.");
        const BindingFlags hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        boards[0].Read(guidance);
        float end = (float)typeof(PrisonGuidance).GetField("revealUntil", hidden).GetValue(guidance);
        boards[0].Read(guidance);
        if (!boards[0].Used || end <= Time.time || (float)typeof(PrisonGuidance).GetField("revealUntil", hidden).GetValue(guidance) != end)
            throw new InvalidOperationException("Route notes must provide one time-limited hint each.");
        var relays = UnityEngine.Object.FindObjectsByType<HauntRelay>(FindObjectsSortMode.None).OrderBy(relay => relay.Index).ToArray();
        Transform partner = session.PartnerTransform;
        Vector3 formerPosition = partner.position;
        partner.position = relays[1].transform.position - relays[1].transform.forward * .25f;
        Physics.SyncTransforms();
        session.UpdateRelayHold(relays[1], true, 2.4f);
        if (session.RelayOn(relays[1]))
            throw new InvalidOperationException("Relay B was activated without Relay A.");
        session.Activate(relays[0]);
        partner.position = formerPosition;
        Physics.SyncTransforms();
        session.UpdateRelayHold(relays[1], true, 2.4f);
        if (session.RelayOn(relays[1]))
            throw new InvalidOperationException("Relay B was activated without the partner.");
        partner.position = relays[1].transform.position + relays[1].transform.forward * .25f;
        Physics.SyncTransforms();
        if (!session.PartnerReady(relays[1]))
            partner.position = relays[1].transform.position;
        Physics.SyncTransforms();
        session.UpdateRelayHold(relays[1], true, 2.4f);
        if (!session.ExitUnlocked)
            throw new InvalidOperationException("Both completed relays should unlock the exit.");
        Debug.Log("PASS four single-use route notes and uninterrupted Relay B interaction.");
    }

    private static void VerifyIncidents(GameObject player)
    {
        const BindingFlags hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        var incident = player.GetComponent<PrisonIncidents>();
        var controls = player.GetComponent<CorridorReview>();
        typeof(CorridorReview).GetMethod("Awake", hidden).Invoke(controls, null);
        typeof(PrisonIncidents).GetMethod("Awake", hidden).Invoke(incident, null);
        var gates = UnityEngine.Object.FindObjectsByType<ReviewDoor>(FindObjectsSortMode.None)
            .Where(door => door.name.StartsWith("CHECKPOINT ")).ToArray();
        var panels = UnityEngine.Object.FindObjectsByType<PrisonBreaker>(FindObjectsSortMode.None);
        if (panels.Length != 3 || !incident.TryStartSeal(gates[0], 10f) || !gates[0].IsLocked || gates[0].RequestPassage())
            throw new InvalidOperationException("A temporary seal did not stop checkpoint passage.");
        incident.ResolveExpired(27.99f);
        if (!gates[0].IsLocked)
            throw new InvalidOperationException("Temporary checkpoint seal ended early.");
        incident.ResolveExpired(28f);
        if (gates[0].IsLocked || incident.TryStartSeal(gates[1], 30f))
            throw new InvalidOperationException("Checkpoint seal failed to reopen or repeated in one round.");
        if (!incident.TryStartFault(panels[0], 30f) || !panels[0].Faulted || incident.Repair(panels[1]))
            throw new InvalidOperationException("Local outage did not fault the selected breaker only.");
        typeof(CorridorReview).GetMethod("SetPower", hidden).Invoke(controls, new object[] { false });
        if (!incident.Repair(panels[0]) || controls.HasFacilityPower || panels[0].Faulted)
            throw new InvalidOperationException("Repair should fix the local fault without cancelling facility blackout.");
        var nextRound = player.AddComponent<PrisonIncidents>();
        nextRound.Configure(player.GetComponent<MazeSession>(), gates, panels);
        typeof(PrisonIncidents).GetMethod("Awake", hidden).Invoke(nextRound, null);
        nextRound.TryStartFault(panels[2], 5f);
        nextRound.ResolveExpired(35f);
        if (panels[2].Faulted)
            throw new InvalidOperationException("Breaker fault must also clear automatically.");
        Debug.Log("PASS temporary seals and local power faults recover automatically, manual repair respects facility blackout.");
    }

    private static void VerifyEscort(MazeSession session, MazeGuard guard, MazeCompanion partner)
    {
        const BindingFlags hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(MazeSession).GetMethod("Awake", hidden).Invoke(session, null);
        typeof(HauntExperience).GetMethod("Awake", hidden).Invoke(session.GetComponent<HauntExperience>(), null);
        typeof(MazeGuard).GetMethod("Awake", hidden).Invoke(guard, null);
        var holding = new SerializedObject(guard).FindProperty("checkpoints");
        if (holding.arraySize != 5 || !session.TryDetain(partner.transform))
            throw new InvalidOperationException("Escort checkpoints or initial detention were unavailable.");
        Vector3 before = partner.transform.position;
        session.ArriveAtHolding();
        if (!session.HasDetainee || Vector3.Distance(before, partner.transform.position) > .001f)
            throw new InvalidOperationException("Premature holding arrival moved the captive.");
        session.MoveDetainee(guard.transform.position, .12f);
        if (Vector3.Distance(before, partner.transform.position) > .65f)
            throw new InvalidOperationException("Escort movement must stay within the walking step.");
        Vector3 beforeRelease = partner.transform.position;
        session.ReleaseStalledEscort();
        if (session.HasDetainee || partner.NeedsHelp || Vector3.Distance(beforeRelease, partner.transform.position) > .001f)
            throw new InvalidOperationException("A stalled escort must release the captive in place.");
        Debug.Log("PASS escort does not teleport on incomplete arrival or timeout, and hands use a clear-depth overlay.");
    }

    private static void VerifyHaunt(GameObject[] roots, GameObject player)
    {
        var items = roots.Select(root => root.GetComponent<HauntPickup>()).Where(item => item != null).ToArray();
        if (items.Count(item => item.Type == HauntPickup.Kind.Torch) != 1 ||
            items.Count(item => item.Type == HauntPickup.Kind.Batteries) != 4 ||
            items.Count(item => item.Type == HauntPickup.Kind.Water) != 4 ||
            roots.Count(root => root.name.StartsWith("CELL A", StringComparison.Ordinal) && root.GetComponent<ReviewDoor>()) != 16 ||
            player.GetComponentsInChildren<AudioListener>().Length != 1)
            throw new InvalidOperationException("Haunt items, cell doors, or listener were not staged.");

        var audio = new SerializedObject(player.GetComponent<HauntExperience>());
        foreach (string field in new[] { "ambience", "breath", "switchClick", "pickup", "latch", "encounter",
            "anomalyCue", "intakeMusic", "explorationMusic", "blackoutMusic", "anomalyMusic", "encounterSting",
            "exhaustedBreathing", "heartbeatClip" })
        {
            if (!(audio.FindProperty(field).objectReferenceValue is AudioClip clip) || clip.samples == 0)
                throw new InvalidOperationException("Missing haunt audio: " + field);
        }
        if (audio.FindProperty("figures").arraySize != 4)
            throw new InvalidOperationException("Encounter audio targets are incomplete.");
        foreach (var (field, count) in new[] { ("steps", 6), ("sprintSteps", 8) })
        {
            var bank = audio.FindProperty(field);
            if (bank.arraySize != count)
                throw new InvalidOperationException("Footstep bank is incomplete: " + field);
            for (int i = 0; i < bank.arraySize; i++)
            {
                if (bank.GetArrayElementAtIndex(i).objectReferenceValue is not AudioClip step ||
                    step.length is < .20f or > .29f || !AssetDatabase.GetAssetPath(step).Contains("/PartnerAudio/"))
                    throw new InvalidOperationException("Footsteps must use trimmed, recorded individual steps.");
            }
        }
        Debug.Log("PASS partner audio: 6 walk steps, 8 sprint steps, 4 music tracks and a shared encounter sting.");

        var rules = new HauntRules();
        var session = player.GetComponent<MazeSession>();
        var relays = roots.Select(root => root.GetComponent<HauntRelay>()).Where(relay => relay != null)
            .OrderBy(relay => relay.Index).ToArray();
        var doors = roots.Where(root => root.name == "EXIT GATE gate").Select(root => root.GetComponent<ReviewDoor>()).ToArray();
        if (!session.HasRelayObjectives || doors.Length == 0 ||
            doors.Any(door => !new SerializedObject(door).FindProperty("locked").boolValue) ||
            relays.Length != 2 || relays[0].Index != 0 || relays[1].Index != 1 || rules.ExitUnlocked ||
            !rules.Activate(1) || rules.ExitUnlocked || rules.Activate(1) || !rules.Activate(0) || !rules.ExitUnlocked)
            throw new InvalidOperationException("Exit should unlock only after two distinct relays.");
        Physics.SyncTransforms();
        foreach (HauntRelay relay in relays)
        {
            Vector3 center = relay.GetComponentInChildren<BoxCollider>().bounds.center;
            int x = Mathf.FloorToInt(center.x / MazeLayout.Pitch);
            int z = Mathf.FloorToInt(center.z / MazeLayout.Pitch);
            Vector3 eye = MazeLayout.Center(z * MazeLayout.Size + x) + Vector3.up * 1.63f;
            if (!Physics.Raycast(eye, center - eye, out RaycastHit hit, 2.8f, ~0, QueryTriggerInteraction.Collide) ||
                hit.collider.GetComponentInParent<HauntRelay>() != relay)
                throw new InvalidOperationException(relay.Label + " cannot be activated from its corridor.");
            if (relay.Index == 1)
            {
                var partner = GameObject.Find("Partner").transform;
                Vector3 original = partner.position;
                if (session.PartnerReady(relay))
                    throw new InvalidOperationException("Relay B allows activation without the partner.");
                try
                {
                    partner.position = eye - Vector3.up * 1.63f;
                    Physics.SyncTransforms();
                    if (!session.PartnerReady(relay))
                        throw new InvalidOperationException("Relay B did not accept a nearby partner.");
                }
                finally
                {
                    partner.position = original;
                    Physics.SyncTransforms();
                }
            }
        }
        if (HauntRules.Phase(55.9f) != HauntRules.PowerPhase.Normal ||
            HauntRules.Phase(56f) != HauntRules.PowerPhase.Warning ||
            HauntRules.Phase(62f) != HauntRules.PowerPhase.Blackout ||
            HauntRules.Countdown(62f, HauntRules.PowerPhase.Blackout) != 18 ||
            HauntRules.Phase(80f) != HauntRules.PowerPhase.Normal)
            throw new InvalidOperationException("Blackout phase timing is incorrect.");

        var vitals = new HauntVitals();
        if (vitals.ToggleTorch() || !vitals.Collect(HauntPickup.Kind.Torch) || !vitals.TorchOn ||
            vitals.Collect(HauntPickup.Kind.Torch) || vitals.Collect(HauntPickup.Kind.Batteries))
            throw new InvalidOperationException("Flashlight pickup did not update the inventory.");
        vitals.Step(7f, true, true);
        if (vitals.Stamina != 0f || vitals.Sprinting || !vitals.OutOfBreath)
            throw new InvalidOperationException("Sprint did not exhaust stamina.");
        vitals.Step(1f, true, false);
        if (vitals.Stamina <= 0f || !vitals.OutOfBreath)
            throw new InvalidOperationException("Panting must persist during early stamina recovery.");
        vitals.Step(2f, false, false);
        if (vitals.OutOfBreath || !vitals.Collect(HauntPickup.Kind.Water) || vitals.Stamina < .7f)
            throw new InvalidOperationException("Stamina recovery or water refill failed.");
        vitals.Step(425f, false, false);
        if (vitals.Charge != 0f || vitals.TorchOn || !vitals.Collect(HauntPickup.Kind.Batteries) ||
            !vitals.ToggleTorch() || !vitals.TorchOn)
            throw new InvalidOperationException("Battery depletion or replacement failed.");
        Debug.Log("PASS haunted maze: 16 cells, blackout cycle, two distinct relays, locked exit, supplies and flashlight.");
    }

    private static void VerifyProtocols()
    {
        var rules = new PartnerProtocols();
        void Advance(float dt, bool torch = false, bool eye = false, bool split = false, bool help = false,
            bool exit = false, bool partner = false) => rules.Step(dt, torch, false, eye, split, help, exit, partner);
        if (rules.Active != PartnerProtocols.Mode.Free || rules.Remaining != PartnerProtocols.Deadline)
            throw new InvalidOperationException("Lockdown clock starts in the wrong protocol.");
        Advance(75f);
        Advance(1f, torch: true);
        Advance(1f);
        if (rules.Active != PartnerProtocols.Mode.Sweep || rules.ExposureProgress != 0f || rules.Remaining != 523f)
            throw new InvalidOperationException("Sweep does not clear exposure with the light off.");
        Advance(3f, torch: true);
        if (rules.Active != PartnerProtocols.Mode.Sweep || rules.Remaining != 512f)
            throw new InvalidOperationException("Sweep detection did not advance lockdown.");
        Advance(45f);
        Advance(2f, eye: true);
        if (rules.Active != PartnerProtocols.Mode.Identity || rules.Remaining != 457f)
            throw new InvalidOperationException("Identity protocol did not penalize sustained eye contact.");
        Advance(78f);
        Advance(1f, help: true);
        Advance(.5f);
        if (rules.Active != PartnerProtocols.Mode.Life || !rules.NeedsHelp || rules.AssistProgress != 0f)
            throw new InvalidOperationException("Interrupted aid should reset progress.");
        Advance(2f, help: true);
        if (rules.NeedsHelp || rules.Alert == null)
            throw new InvalidOperationException("Completed aid did not release the companion.");
        Advance(271.5f);
        Advance(8f, split: true);
        if (rules.Active != PartnerProtocols.Mode.Reckoning || rules.Remaining != 88f)
            throw new InvalidOperationException("Separated players did not raise the final protocol alarm.");
        Advance(1f, exit: true);
        if (rules.Outcome != PartnerProtocols.Result.Running)
            throw new InvalidOperationException("Escaped without the companion.");
        Advance(1f, exit: true, partner: true);
        if (rules.Outcome != PartnerProtocols.Result.Escaped)
            throw new InvalidOperationException("Both players reached the unlocked exit without winning.");

        rules = new PartnerProtocols();
        Advance(205f);
        Advance(50f);
        if (rules.NeedsHelp || rules.Remaining != 320f)
            throw new InvalidOperationException("Missed rescue must cost time and release the companion.");
        Advance(320f, exit: true, partner: true);
        if (rules.Outcome != PartnerProtocols.Result.LockedDown)
            throw new InvalidOperationException("An expired lockdown clock must prevent escape.");
        Debug.Log("PASS five protocols, sweep and separation penalties, interrupted/missed rescue, joint escape and lockdown.");
    }

    private static void VerifyWalk(CharacterController player, MazeLayout layout, Vector3 exit, MazeCompanion companion)
    {
        const float frame = 1f / 30f;
        float simulatedSeconds = 0;
        foreach (int cell in layout.Route.Skip(1))
        {
            Vector3 target = MazeLayout.Center(cell);
            int steps = 0;
            Vector3 origin = player.transform.position;
            CollisionFlags flags = CollisionFlags.None;
            while (steps < 65)
            {
                Vector3 delta = target - player.transform.position;
                delta.y = 0;
                if (delta.sqrMagnitude < .01f)
                    break;
                Vector3 input = Vector3.ClampMagnitude(delta, CorridorReview.WalkSpeed * frame);
                flags |= player.Move(input + Vector3.down * .02f);
                simulatedSeconds += frame;
                companion.Step(frame, simulatedSeconds);
                steps++;
            }
            Vector3 reached = player.transform.position;
            reached.y = 0;
            if (Vector3.Distance(reached, target) > .10f)
                throw new InvalidOperationException($"The player capsule could not walk to route cell {cell} " +
                    $"from {origin} to {target}; stopped at {player.transform.position}, flags {flags}.");
        }
        Vector3 end = player.transform.position;
        end.y = 0;
        if (Vector3.Distance(end, exit) >= .9f)
            throw new InvalidOperationException("The walk did not reach the exit marker.");
        Debug.Log($"PASS full CharacterController walk at 30 simulated updates/s: {simulatedSeconds:F1} s to the exit center.");
        for (int step = 0; step < 360; step++)
            companion.Step(frame, simulatedSeconds + (step + 1) * frame);
        Vector3 followed = companion.transform.position;
        followed.y = 0f;
        if (Vector3.Distance(followed, exit) > 2.1f)
            throw new InvalidOperationException("AI partner did not reach the exit; stopped at " + followed);
        Debug.Log("PASS AI partner followed the full route and reached the exit.");
    }

    private static void VerifyRig(string name, int expected)
    {
        var actor = GameObject.Find(name);
        var animator = actor.GetComponent<Animator>();
        var skins = actor.GetComponentsInChildren<SkinnedMeshRenderer>();
        if (skins.Length == 0 || skins.Any(skin => skin.bones.Length != expected || skin.bones.Any(bone => bone == null) ||
                skin.sharedMesh.bindposes.Length != expected))
            throw new InvalidOperationException(name + " has invalid skin bindings.");
        AnimationClip idle = animator.runtimeAnimatorController.animationClips[0];
        var bones = skins[0].bones;
        idle.SampleAnimation(actor, 0);
        var rotations = bones.Select(bone => bone.localRotation).ToArray();
        idle.SampleAnimation(actor, idle.length * .43f);
        if (!bones.Where((bone, index) => Quaternion.Angle(rotations[index], bone.localRotation) > .1f).Any())
            throw new InvalidOperationException(name + " clip does not update its bones.");
        Debug.Log($"PASS {name}: {expected} bound bones, {skins.Length} skinned renderers, animation changes bone rotations.");
    }
}
