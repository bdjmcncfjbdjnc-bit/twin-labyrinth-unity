using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

internal static class ActorMotionBuilder
{
    public static void Stage()
    {
        foreach(var actor in Object.FindObjectsByType<Animator>(FindObjectsSortMode.None))
        {
            if(actor.GetComponent<PrisonInmate>()==null && actor.GetComponent<MazeGuard>()==null)continue;
            string model=actor.name=="Prison guard"?"marcus_formal":actor.name=="Records clerk"?"kenji_formal":"hannah_casual";
            var controller=actor.runtimeAnimatorController as AnimatorController;
            var source=AssetDatabase.LoadAllAssetsAtPath("Assets/ArtReview/Source/MazeCast/"+model+".glb").OfType<AnimationClip>().ToArray();
            foreach(string name in new[]{"Talk","Argue","Think","Wave","Shrug","Phone","Drink"})
            {
                if(controller.layers[0].stateMachine.states.Any(s=>s.state.name==name))continue;
                string path="Assets/ArtReview/HauntCoop/"+model+" "+name+".anim";
                var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if(clip==null)
                {clip=Object.Instantiate(source.Single(c=>c.name==name));AssetDatabase.CreateAsset(clip,path);}
                var state=controller.layers[0].stateMachine.AddState(name);state.motion=clip;
            }
            if(actor.GetComponent<PrisonActorMotion>()==null)actor.gameObject.AddComponent<PrisonActorMotion>();
            EditorUtility.SetDirty(controller);
        }
    }
}
