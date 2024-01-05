
using UnityEngine;
using UnityEditor;
using UnityEditor.Callbacks;
using VRC.SDKBase.Editor.BuildPipeline;

namespace Nessie.Udon.SaveState.Internal
{
    public class NUSaveStateBuildCallback : IVRCSDKBuildRequestedCallback
    {
        public int callbackOrder => 0;

        public bool OnBuildRequested(VRCSDKRequestedBuildType requestedBuildType)
        {
            if (requestedBuildType != VRCSDKRequestedBuildType.Scene)
                return true;

            NUSaveState[] saveStates = Object.FindObjectsOfType<NUSaveState>();
            foreach (NUSaveState saveState in saveStates)
            {
                if (saveState.TryGetComponent(out NUSaveStateData data))
                {
                    EditorUtility.SetDirty(saveState);
                    data.ApplyAvatarSlots(saveState);
                }
            }
            
            return true;
        }
        
        [PostProcessScene(0)]
        public static void OnPostprocessScene()
        {
            NUSaveStateData[] datas = Object.FindObjectsOfType<NUSaveStateData>();
            foreach (NUSaveStateData data in datas)
            {
                Object.DestroyImmediate(data);
            }
        }
    }
}
