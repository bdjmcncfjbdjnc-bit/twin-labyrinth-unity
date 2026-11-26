using UnityEngine;

public sealed class PrisonBreaker : MonoBehaviour
{
    [SerializeField] private TextMesh status;
    public bool Faulted { get; private set; }
    public void Configure(TextMesh indicator) => status = indicator;
    public void SetFault(bool fault)
    {
        Faulted = fault;
        status.text = fault ? "FAULT  /  F" : "BREAKER";
        status.color = fault ? new Color(1f, .34f, .17f) : new Color(.60f, .87f, .76f);
    }
}
