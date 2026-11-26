using UnityEngine;

public sealed class HauntExperience : MonoBehaviour
{
    [SerializeField] private Transform eyes;
    [SerializeField] private Light torch;
    [SerializeField] private GameObject heldModel;
    [SerializeField] private AudioClip ambience;
    [SerializeField] private AudioClip breath;
    [SerializeField] private AudioClip[] steps;
    [SerializeField] private AudioClip[] sprintSteps;
    [SerializeField] private AudioClip intakeMusic;
    [SerializeField] private AudioClip explorationMusic;
    [SerializeField] private AudioClip blackoutMusic;
    [SerializeField] private AudioClip anomalyMusic;
    [SerializeField] private AudioClip encounterSting;
    [SerializeField] private AudioClip exhaustedBreathing;
    [SerializeField] private AudioClip heartbeatClip;
    [SerializeField] private AudioClip switchClick;
    [SerializeField] private AudioClip pickup;
    [SerializeField] private AudioClip latch;
    [SerializeField] private AudioClip encounter;
    [SerializeField] private AudioClip anomalyCue;
    [SerializeField] private Transform[] figures;

    private readonly HauntVitals vitals = new();
    private AudioSource background;
    private AudioSource breathing;
    private AudioSource feedback;
    private AudioSource footsteps;
    private AudioSource music;
    private AudioSource heartbeat;
    private MazeSession session;
    private AudioClip requestedMusic;
    private bool underway;
    private bool blackout;
    private float anomalyMusicUntil;
    private float encounterCooldownUntil;
    private float heartbeatUntil;
    private float heartStrength;
    private float nextBreathNoise;
    private bool[] heard;
    private bool audioReady;
    private float stride;
    private int stepIndex;
    private int sprintStepIndex;
    private float sightPoll;
    private string message = "Take the flashlight at intake.";
    private float messageUntil = 12f;
    public bool TorchOn => vitals.TorchOn;
    public bool HasTorch => vitals.HasTorch;
    public void RechargeOnlineTorch() => vitals.Collect(HauntPickup.Kind.Batteries);
    public void GrantCache()
    {
        vitals.Collect(HauntPickup.Kind.Batteries);
        vitals.Collect(HauntPickup.Kind.Water);
    }
    public bool Sprinting => vitals.Sprinting;
    public void Stagger() => vitals.Stagger();
    public float RequestedSpeed(bool sprintHeld) => sprintHeld && vitals.CanSprint ? 3.55f : CorridorReview.WalkSpeed;
    public bool LookingAtImitator { get; private set; }
    public void ReplaceFigure(Transform previous, Transform replacement)
    {
        int index = System.Array.IndexOf(figures, previous);
        if (index >= 0)
            figures[index] = replacement;
    }

    public void Configure(Transform view, Light light, GameObject model, AudioClip hum, AudioClip pant,
        AudioClip[] footfalls, AudioClip click, AudioClip collect, AudioClip door, AudioClip sight,
        AudioClip strangeSight, Transform[] actors)
    {
        eyes = view;
        torch = light;
        heldModel = model;
        ambience = hum;
        breath = pant;
        steps = footfalls;
        switchClick = click;
        pickup = collect;
        latch = door;
        encounter = sight;
        anomalyCue = strangeSight;
        figures = actors;
    }

    public void ConfigurePartnerAudio(AudioClip[] walk, AudioClip[] run, AudioClip intake, AudioClip exploration,
        AudioClip dark, AudioClip anomaly, AudioClip sting, AudioClip exhaustedVoice, AudioClip pulse)
    {
        steps = walk;
        sprintSteps = run;
        intakeMusic = intake;
        explorationMusic = exploration;
        blackoutMusic = dark;
        anomalyMusic = anomaly;
        encounterSting = sting;
        exhaustedBreathing = exhaustedVoice;
        heartbeatClip = pulse;
    }

    private void Awake()
    {
        feedback = Source(gameObject, null, false);
        footsteps = Source(gameObject, null, false);
        background = Source(gameObject, ambience, true);
        breathing = Source(gameObject, exhaustedBreathing != null ? exhaustedBreathing : breath, true);
        session = GetComponent<MazeSession>();
        if (heartbeatClip != null)
        {
            heartbeat = Source(gameObject, heartbeatClip, true);
            heartbeat.volume = 0f;
        }
        if (explorationMusic != null)
        {
            music = Source(gameObject, null, true);
            music.volume = 0f;
        }
        background.volume = .4f;
        breathing.volume = 0f;
        heard = new bool[figures.Length];
        heldModel.SetActive(false);
        torch.enabled = false;
        if (GetComponent<MazeSession>().HasRelayObjectives)
            message = GetComponent<MazeSession>().HasPartner
                ? "Take the flashlight. Read the roll-call board. TAB shows the rules."
                : "Take the flashlight. Activate A and B to unlock the exit.";
        messageUntil = Time.time + 12f;
    }

    public void UpdateMovement(bool moving, bool sprintHeld)
    {
        underway |= moving;
        if (!audioReady && (moving || Input.anyKeyDown || Input.GetMouseButtonDown(0)))
        {
            audioReady = true;
            background.Play();
            if (exhaustedBreathing == null)
                breathing.Play();
        }

        vitals.Step(Time.deltaTime, moving, sprintHeld);
        torch.enabled = vitals.TorchOn;
        float target = exhaustedBreathing != null
            ? (vitals.OutOfBreath ? Mathf.Lerp(.10f, .22f, 1f - vitals.Stamina / .32f) : 0f)
            : vitals.Sprinting ? .62f : (1f - vitals.Stamina) * .5f;
        breathing.volume = Mathf.MoveTowards(breathing.volume, target, Time.deltaTime * .4f);
        if (audioReady && exhaustedBreathing != null)
            PlayWhileAudible(breathing);
        if (audioReady && heartbeat != null)
        {
            float pulse = heartStrength * Mathf.Clamp01((heartbeatUntil - Time.time) / 2f);
            heartbeat.pitch = session.GuardChasing ? 1.39f : 1f;
            heartbeat.volume = Mathf.MoveTowards(heartbeat.volume, pulse, Time.deltaTime * .9f);
            PlayWhileAudible(heartbeat);
        }
        if (audioReady && music != null)
            UpdateMusic();
        if (vitals.OutOfBreath && Time.time >= nextBreathNoise)
        {
            nextBreathNoise = Time.time + 1.35f;
            session?.ReportNoise(transform.position, 5f);
        }
    }

    public void MovementFeedback(Vector3 displacement)
    {
        displacement.y = 0;
        stride += displacement.magnitude;
        float stepLength = vitals.Sprinting ? 1.12f : .78f;
        if (stride >= stepLength)
        {
            stride %= stepLength;
            bool dedicatedSprint = vitals.Sprinting && sprintSteps != null && sprintSteps.Length > 0;
            AudioClip[] clips = dedicatedSprint ? sprintSteps : steps;
            if (clips != null && clips.Length > 0)
            {
                int index = dedicatedSprint ? sprintStepIndex++ : stepIndex++;
                footsteps.pitch = music != null ? 1f : .97f + .015f * (index % 5);
                float volume = music != null ? (dedicatedSprint ? .65f : .58f) : (vitals.Sprinting ? .34f : .22f);
                footsteps.PlayOneShot(clips[index % clips.Length], volume);
            }
            if (vitals.Sprinting)
                session?.ReportNoise(transform.position, 10f);
        }
        if (audioReady && Time.time >= sightPoll)
        {
            sightPoll = Time.time + .12f;
            CheckEncounters();
        }
    }

    public void SetBlackout(bool active) => blackout = active;

    public void EndRound(bool escaped)
    {
        breathing.Stop();
        heartbeat?.Stop();
        music?.Stop();
        background.Stop();
        RuleCue(!escaped);
    }

    private static void PlayWhileAudible(AudioSource source)
    {
        if (source.volume > 0f && !source.isPlaying)
            source.Play();
        else if (source.volume == 0f && source.isPlaying)
            source.Stop();
    }

    private void UpdateMusic()
    {
        requestedMusic = Time.time < anomalyMusicUntil ? anomalyMusic :
            blackout ? blackoutMusic : underway ? explorationMusic : intakeMusic;
        float target = music.clip == requestedMusic
            ? (Time.time < encounterCooldownUntil ? .035f : blackout ? .12f : .10f)
            : 0f;
        music.volume = Mathf.MoveTowards(music.volume, target, Time.deltaTime * .18f);
        if (music.clip == requestedMusic || music.volume > 0f)
            return;
        music.clip = requestedMusic;
        if (music.clip != null)
            music.Play();
    }

    public void ToggleTorch()
    {
        if (vitals.ToggleTorch())
            feedback.PlayOneShot(switchClick, .52f);
        else
            Notify(vitals.HasTorch ? "Flashlight needs batteries." : "Find the flashlight at intake.");
    }

    public void Collect(HauntPickup item)
    {
        if (!vitals.Collect(item.Type))
        {
            Notify(item.Type == HauntPickup.Kind.Batteries && !vitals.HasTorch ? "You need a flashlight first." : "Already full.");
            return;
        }
        heldModel.SetActive(vitals.HasTorch);
        torch.enabled = vitals.TorchOn;
        feedback.PlayOneShot(pickup, .5f);
        Notify(item.Type switch
        {
            HauntPickup.Kind.Torch => "Flashlight collected. L to toggle.",
            HauntPickup.Kind.Batteries => "Battery charge restored.",
            _ => "Stamina restored."
        });
        item.gameObject.SetActive(false);
    }

    public void DoorSound(ReviewDoor door)
    {
        var source = door.GetComponent<AudioSource>() ?? Source(door.gameObject, null, false);
        source.spatialBlend = .72f;
        source.PlayOneShot(latch, .43f);
    }

    private void CheckEncounters()
    {
        LookingAtImitator = false;
        float pulse = heartbeat != null && CanSee(eyes, session.PartnerTransform, 5f) ? .10f : 0f;
        if (heartbeat != null && CanSee(eyes, session.GuardTransform, 11f))
            pulse = .26f;
        for (int index = 0; index < figures.Length; index++)
        {
            Transform figure = figures[index];
            if (!CanSee(eyes, figure, 7.1f))
                continue;
            bool abnormal = figure.GetComponent<MazeAnomaly>() != null;
            LookingAtImitator |= abnormal;
            pulse = Mathf.Max(pulse, abnormal ? .23f : .15f);
            if (heard[index] || (music != null && Time.time < encounterCooldownUntil))
                continue;
            heard[index] = true;
            var source = Source(figure.gameObject, null, false);
            source.spatialBlend = .7f;
            source.minDistance = 3.5f;
            source.maxDistance = 12f;
            if (music == null)
            {
                source.PlayOneShot(abnormal ? anomalyCue : encounter, abnormal ? .72f : .54f);
                continue;
            }
            source.PlayOneShot(encounterSting, abnormal ? .18f : .14f);
            encounterCooldownUntil = Time.time + 2.4f;
            if (abnormal)
                anomalyMusicUntil = Time.time + 14f;
        }
        if (pulse > 0f)
        {
            heartStrength = pulse;
            heartbeatUntil = Time.time + 3.2f;
        }
    }

    private static bool CanSee(Transform eyes, Transform figure, float range)
    {
        if (figure == null)
            return false;
        Vector3 target = figure.position + Vector3.up * 1.4f;
        Vector3 direction = target - eyes.position;
        return direction.sqrMagnitude < range * range && Vector3.Dot(eyes.forward, direction.normalized) >= .7f &&
            !Physics.Linecast(eyes.position, target, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
    }

    private static AudioSource Source(GameObject host, AudioClip clip, bool loop)
    {
        var source = host.AddComponent<AudioSource>();
        source.clip = clip;
        source.playOnAwake = false;
        source.loop = loop;
        source.dopplerLevel = 0f;
        return source;
    }

    private void Notify(string text)
    {
        message = text;
        messageUntil = Time.time + 3.2f;
    }

    public void RuleCue(bool urgent)
    {
        feedback.PlayOneShot(urgent ? latch : pickup, urgent ? .62f : .46f);
    }

    private void OnGUI()
    {
        if (GetComponent<PrisonPresentation>() != null) return;
        if (session != null && session.IsOver)
            return;
        float top = Screen.height - 69;
        GUI.Label(new Rect(14, top - 2, 60, 20), "STAMINA");
        Bar(83, top + 4, vitals.Stamina, new Color(.76f, .85f, .76f));
        if (vitals.OutOfBreath)
            GUI.Label(new Rect(196, top - 2, 160, 20), "OUT OF BREATH");
        if (vitals.HasTorch)
        {
            GUI.Label(new Rect(14, top + 15, 60, 20), "BATTERY");
            Bar(83, top + 21, vitals.Charge, new Color(.85f, .79f, .57f));
        }
        if (Time.time < messageUntil)
            GUI.Box(new Rect((Screen.width - 390) * .5f, Screen.height - 85, 390, 25), message);
    }

    public HauntVitals PresentationVitals => vitals;
    public string PresentationMessage => Time.time < messageUntil ? message : null;
    private static void Bar(float x, float y, float fraction, Color color)
    {
        Color previous = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, .7f);
        GUI.DrawTexture(new Rect(x, y, 105, 6), Texture2D.whiteTexture);
        GUI.color = color;
        GUI.DrawTexture(new Rect(x, y, 105 * fraction, 6), Texture2D.whiteTexture);
        GUI.color = previous;
    }
}
