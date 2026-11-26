using UnityEngine;

public sealed class HauntRelay : MonoBehaviour
{
    [SerializeField] private int index;
    [SerializeField] private Light indicator;
    public int Index => index;
    public string Label => index == 0 ? "RELAY A" : "RELAY B";

    public void Configure(int number, Light light)
    {
        index = number;
        indicator = light;
    }

    public void MarkActive() => indicator.enabled = true;
}
