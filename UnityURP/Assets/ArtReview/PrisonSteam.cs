using UnityEngine;
public sealed class PrisonSteam : MonoBehaviour
{
    private ParticleSystem mist;
    private LastRollCall mission;
    private bool stopped;
    private void Start() { mist=GetComponent<ParticleSystem>();mission=FindFirstObjectByType<LastRollCall>(); }
    private void Update()
    {
        if(!stopped && mission.Plan.Prepared && mission.Plan.Selected == EscapePlan.Route.Maintenance) { stopped=true;mist.Stop(true,ParticleSystemStopBehavior.StopEmitting); }
    }
}
