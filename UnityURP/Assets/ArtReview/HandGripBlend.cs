using UnityEngine;

[DefaultExecutionOrder(250)]
public sealed class HandGripBlend : MonoBehaviour
{
    [SerializeField] private Transform[] fingers;
    [SerializeField] private Quaternion[] relaxed;
    [SerializeField] private Quaternion[] holding;
    private HauntExperience equipment; private float weight;
    public void Configure(Transform[] joints,Quaternion[] open,Quaternion[] closed)
    {fingers=joints;relaxed=open;holding=closed;}
    private void Awake(){equipment=GetComponentInParent<HauntExperience>();}
    private void LateUpdate()
    {
        weight=Mathf.MoveTowards(weight,equipment!=null&&equipment.HasTorch?1f:0f,Time.deltaTime*4f);
        bool knife=GetComponentInParent<PrisonHands>()?.KnifeEquipped==true;
        // A flashlight handle is wider than a knife tang: a full fist intersects the lamp.
        for(int i=0;i<fingers.Length;i++)fingers[i].localRotation=Quaternion.Slerp(relaxed[i],holding[i],Mathf.SmoothStep(0,1,weight)*(knife?1f:.87f));
    }
}
