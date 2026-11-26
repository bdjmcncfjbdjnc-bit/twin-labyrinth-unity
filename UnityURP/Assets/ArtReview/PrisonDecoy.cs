using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public sealed class PrisonDecoy : MonoBehaviour
{
    [SerializeField] private AudioClip knock;
    private float ready;

    public string Prompt => Time.time >= ready ? "F  Knock on pipe" : "Wait for the sound to settle";

    public void Configure(AudioClip clip) => knock = clip;

    public void Strike(MazeSession session)
    {
        if (Time.time < ready)
            return;
        ready = Time.time + 5f;
        AudioSource source = GetComponent<AudioSource>();
        source.spatialBlend = 1f;
        source.dopplerLevel = 0f;
        source.PlayOneShot(knock, .72f);
        session.ReportNoise(transform.position, 19f, true);
    }
}
