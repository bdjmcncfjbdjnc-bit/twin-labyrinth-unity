using UnityEngine;

public sealed class HauntPickup : MonoBehaviour
{
    public enum Kind { Torch, Batteries, Water }

    [SerializeField] private Kind type;
    public Kind Type => type;
    public string Description => type switch
    {
        Kind.Torch => "flashlight",
        Kind.Batteries => "batteries",
        _ => "water"
    };

    public void Configure(Kind item) => type = item;
}
