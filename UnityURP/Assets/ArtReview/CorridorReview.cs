using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(CharacterController))]
public sealed class CorridorReview : MonoBehaviour
{
    public const float WalkSpeed = 2.1f;
    private static readonly int EmissiveFactor = Shader.PropertyToID("emissiveFactor");

    [SerializeField] private Transform eyes;
    [SerializeField] private Light flashlight;
    [SerializeField] private Light[] corridorLights;
    [SerializeField] private Renderer[] lampMeshes;
    [SerializeField] private Light emergencyLight;
    [SerializeField] private Animator prisoner;

    private CharacterController controller;
    private float pitch;
    private float fallSpeed;
    private ReviewDoor focusedDoor;
    private HauntPickup focusedItem;
    private HauntRelay focusedRelay;
    private PrisonDecoy focusedDecoy;
    private PrisonBreaker focusedBreaker;
    private PrisonIncidents incidents;
    private PrisonGuidance guidance;
    private PrisonInmateRoster inmateRoster;
    private PrisonRouteBoard focusedBoard;
    private Vector3? localFault;
    public bool HasFacilityPower => powered;
    private PrisonHands hands;
    private HauntExperience haunt;
    private MazeSession maze;
    private bool powered;
    private float nextLightUpdate;
    private readonly Light[] closest = new Light[3];
    private readonly float[] lightDistances = new float[3];

    public void Configure(Transform view, Light torch, Light[] fixtures, Renderer[] meshes, Light emergency, Animator character)
    {
        eyes = view;
        flashlight = torch;
        corridorLights = fixtures;
        lampMeshes = meshes;
        emergencyLight = emergency;
        prisoner = character;
    }

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        haunt = GetComponent<HauntExperience>();
        maze = GetComponent<MazeSession>();
        incidents = GetComponent<PrisonIncidents>();
        guidance = GetComponent<PrisonGuidance>();
        inmateRoster = GetComponent<PrisonInmateRoster>();
        hands = GetComponentInChildren<PrisonHands>();
        flashlight.renderMode = LightRenderMode.ForcePixel;
        SetPower(true);
        flashlight.enabled = false;
    }

    private void Update()
    {
        if (PrisonPresentation.Blocked) return;
        if (maze != null && maze.IsOver)
            return;
        if (Input.GetMouseButtonDown(0))
            Cursor.lockState = CursorLockMode.Locked;
        if (PrisonInput.Down(KeyCode.Escape))
            Cursor.lockState = CursorLockMode.None;

        if (Cursor.lockState == CursorLockMode.Locked)
        {
            transform.Rotate(0, Input.GetAxisRaw("Mouse X") * 2f, 0);
            pitch = Mathf.Clamp(pitch - Input.GetAxisRaw("Mouse Y") * 2f, -80f, 80f);
            eyes.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }
        if (maze != null && maze.PlayerDetained)
        {
            maze.UpdateRelayHold(null, false, Time.deltaTime);
            haunt?.UpdateMovement(false, false);
            haunt?.MovementFeedback(Vector3.zero);
            hands?.Step(0f);
            return;
        }

        if (haunt == null)
        {
            if (PrisonInput.Down(KeyCode.Alpha1)) SetPower(true);
            if (PrisonInput.Down(KeyCode.Alpha2)) SetPower(false);
            if (PrisonInput.Down(KeyCode.Alpha3)) prisoner.CrossFade("Base Layer.Prisoner_idle", .15f, 0);
            if (PrisonInput.Down(KeyCode.Alpha4)) prisoner.CrossFade("Base Layer.Prisoner_walk", .15f, 0);
            if (PrisonInput.Down(KeyCode.Alpha5)) prisoner.CrossFade("Base Layer.Prisoner_run", .15f, 0);
        }
        if (PrisonInput.Down(KeyCode.L))
        {
            if (haunt != null) haunt.ToggleTorch();
            else flashlight.enabled = !flashlight.enabled;
        }
        bool target = Physics.Raycast(eyes.position, eyes.forward, out RaycastHit hit, 2.8f, Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Collide);
        focusedDoor = target ? hit.collider.GetComponentInParent<ReviewDoor>() : null;
        focusedItem = target ? hit.collider.GetComponentInParent<HauntPickup>() : null;
        focusedRelay = target ? hit.collider.GetComponentInParent<HauntRelay>() : null;
        focusedDecoy = target ? hit.collider.GetComponentInParent<PrisonDecoy>() : null;
        focusedBreaker = target ? hit.collider.GetComponentInParent<PrisonBreaker>() : null;
        focusedBoard = target ? hit.collider.GetComponentInParent<PrisonRouteBoard>() : null;
        maze?.UpdateRelayHold(focusedRelay, PrisonInput.Held(KeyCode.F), Time.deltaTime);
        if (focusedRelay != null && PrisonInput.Held(KeyCode.F)) hands?.Hold(focusedRelay.transform.position);
        if (PrisonInput.Down(KeyCode.F) && (maze == null || !maze.CanAssist) &&
            (!target || hit.collider.GetComponentInParent<EscapeStation>() == null))
        {
            if (focusedItem != null || focusedRelay != null || focusedDoor != null || focusedDecoy != null ||
                (focusedBreaker != null && focusedBreaker.Faulted) || (focusedBoard != null && !focusedBoard.Used))
                hands?.Interact(hit.point);
            if (focusedItem != null && haunt != null)
                haunt.Collect(focusedItem);
            else if (focusedRelay != null && maze != null)
            {
                if (!maze.HasPartner || focusedRelay.Index == 0)
                    maze.Activate(focusedRelay);
            }
            else if (focusedDecoy != null && maze != null)
                focusedDecoy.Strike(maze);
            else if (focusedBreaker != null)
                incidents?.Repair(focusedBreaker);
            else if (focusedBoard != null)
                focusedBoard.Read(guidance);
            else if (focusedDoor != null)
            {
                focusedDoor.Toggle(controller);
                haunt?.DoorSound(focusedDoor);
            }
            else
                inmateRoster?.TrySpeak();
        }

        Vector3 motion = new Vector3(Axis(KeyCode.D, KeyCode.A), 0f, Axis(KeyCode.W, KeyCode.S));
        bool sprintHeld = PrisonInput.Held(KeyCode.LeftShift) || PrisonInput.Held(KeyCode.RightShift);
        float speed = haunt?.RequestedSpeed(sprintHeld) ?? WalkSpeed;
        motion = transform.TransformDirection(Vector3.ClampMagnitude(motion, 1f)) * speed;
        fallSpeed = controller.isGrounded && fallSpeed < 0f ? -1f : fallSpeed - 9.81f * Time.deltaTime;
        motion.y = fallSpeed;
        Vector3 start = transform.position;
        controller.Move(motion * Time.deltaTime);
        Vector3 travel = transform.position - start;
        float distance = new Vector2(travel.x, travel.z).magnitude;
        haunt?.UpdateMovement(distance > .08f * Time.deltaTime, sprintHeld);
        haunt?.MovementFeedback(travel);
        hands?.Step(distance);
        if (maze != null && maze.HasPartner && powered && Time.time >= nextLightUpdate)
        {
            nextLightUpdate = Time.time + .15f;
            SelectNearbyLights();
        }
    }

    private void OnGUI()
    {
        if (GetComponent<PrisonPresentation>() != null) return;
        if (maze != null && maze.IsOver)
            return;
        float x = Screen.width * .5f;
        float y = Screen.height * .5f;
        GUI.Label(new Rect(x - 3, y - 8, 12, 20), "+");
        if (focusedItem != null)
            GUI.Box(new Rect(x - 85, y + 24, 170, 26), "F  Take " + focusedItem.Description);
        else if (focusedRelay != null && maze != null)
            GUI.Box(new Rect(x - 86, y + 24, 172, 26),
                maze.RelayPrompt(focusedRelay));
        else if (focusedDoor != null)
            GUI.Box(new Rect(x - 90, y + 24, 180, 26), focusedDoor.Prompt);
        else if (focusedDecoy != null)
            GUI.Box(new Rect(x - 105, y + 24, 210, 26), focusedDecoy.Prompt);
        else if (focusedBreaker != null)
            GUI.Box(new Rect(x - 105, y + 24, 210, 26), focusedBreaker.Faulted ? "F  Reset local power" : "Breaker online");
        else if (focusedBoard != null)
            GUI.Box(new Rect(x - 105, y + 24, 210, 26), focusedBoard.Used ? "Route note already read" : "F  Read route note");
        else if (inmateRoster?.Prompt != null)
            GUI.Box(new Rect(x - 105, y + 24, 210, 26), inmateRoster.Prompt);
        GUI.Label(new Rect(12, Screen.height - 32, 470, 24), haunt == null
            ? "WASD Move    F Door    L Flashlight    1 / 2 Power"
            : "WASD Move    Shift Sprint    F Interact    L Flashlight    R Restart");
    }

    public string InteractionHint => focusedItem != null ? "F  Take " + focusedItem.Description :
        focusedRelay != null ? maze.RelayPrompt(focusedRelay) : focusedDoor != null ? focusedDoor.Prompt :
        focusedDecoy != null ? focusedDecoy.Prompt : focusedBreaker != null ? (focusedBreaker.Faulted ? "F  Reset local power" : "Breaker online") :
        focusedBoard != null ? (focusedBoard.Used ? "Route note already read" : "F  Read route note") : inmateRoster?.Prompt;
    private static int Axis(KeyCode positive, KeyCode negative)
    {
        return (PrisonInput.Held(positive) ? 1 : 0) - (PrisonInput.Held(negative) ? 1 : 0);
    }

    internal void SetPower(bool powered)
    {
        this.powered = powered;
        if (powered && maze != null && maze.HasPartner)
            SelectNearbyLights();
        else
            foreach (Light fixture in corridorLights)
                fixture.enabled = powered;

        var emission = new MaterialPropertyBlock();
        foreach (Renderer mesh in lampMeshes)
        {
            emission.SetColor(EmissiveFactor, powered && !InFault(mesh.bounds.center) ? new Color(2f, 2.15f, 2.3f) : Color.black);
            Material[] materials = mesh.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] != null && materials[i].HasColor(EmissiveFactor))
                    mesh.SetPropertyBlock(emission, i);
            }
        }

        emergencyLight.enabled = !powered;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = powered ? new Color(.19f, .20f, .21f) : new Color(.045f, .052f, .064f);
    }

    public void SetLocalFault(Vector3? center)
    {
        localFault = center;
        SetPower(powered);
    }

    private bool InFault(Vector3 point) => localFault.HasValue && (point - localFault.Value).sqrMagnitude <= 9f * 9f;

    private void SelectNearbyLights()
    {
        for (int i = 0; i < closest.Length; i++)
        {
            closest[i] = null;
            lightDistances[i] = 13f * 13f;
        }
        int slots = maze != null && maze.HasGuard ? 2 : closest.Length;
        foreach (Light fixture in corridorLights)
        {
            if (InFault(fixture.transform.position))
                continue;
            float distance = (fixture.transform.position - eyes.position).sqrMagnitude;
            for (int slot = 0; slot < slots; slot++)
            {
                if (distance >= lightDistances[slot])
                    continue;
                for (int previous = slots - 1; previous > slot; previous--)
                {
                    closest[previous] = closest[previous - 1];
                    lightDistances[previous] = lightDistances[previous - 1];
                }
                closest[slot] = fixture;
                lightDistances[slot] = distance;
                break;
            }
        }
        foreach (Light fixture in corridorLights)
        {
            bool selected = System.Array.IndexOf(closest, fixture) >= 0;
            if (fixture.enabled != selected)
                fixture.enabled = selected;
        }
    }
}
