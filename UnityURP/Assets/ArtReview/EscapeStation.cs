using UnityEngine;

public sealed class EscapeStation : MonoBehaviour
{
    public int index;
    public string transferLabel;
    public string maintenanceLabel;
    public TextMesh label;
    public Vector3 maintenancePosition;
    public Quaternion maintenanceRotation;
    public bool alternateLocation;
    public void Present(EscapePlan.Route route)
    {
        if (label != null) label.text = route == EscapePlan.Route.Transfer ? transferLabel : maintenanceLabel;
        if (route == EscapePlan.Route.Maintenance && alternateLocation)
            transform.SetPositionAndRotation(maintenancePosition, maintenanceRotation);
    }
}
