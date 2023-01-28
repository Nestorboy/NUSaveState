#if UNITY_EDITOR

using UnityEngine;
using UnityEngine.Serialization;
using UnityEditor;
using UnityEditor.SceneManagement;
using VRC.Udon;
using VRC.SDKBase.Editor.BuildPipeline;
using Nessie.Udon.Extensions;
using Nessie.Udon.SaveState.Data;
using UnityEditor.Animations;
using BindingFlags = System.Reflection.BindingFlags;

namespace Nessie.Udon.SaveState
{
    [AddComponentMenu(""), DisallowMultipleComponent]
    public class NUSaveStateData : MonoBehaviour, IVRCSDKBuildRequestedCallback
    {
        #region Public Fields

        public bool Visible;
        
        public AvatarSlot[] AvatarSlots;
        
        [FormerlySerializedAs("DataPreferences")]
        public Legacy.Preferences Preferences;
        
        //[Obsolete("This is a legacy field used for backwards compatibility, instructions are now stored in AvatarSlots instead.")]
        [FormerlySerializedAs("DataInstructions")]
        public Legacy.Instruction[] Instructions;

        #endregion Public Fields

        public int callbackOrder => 0;
        
        private void OnValidate()
        {
            //Debug.Log("NUSaveStateData: OnValidate");
            
            UpdateVisibility();
            
            UpdateInstructions();
        }
        
        public bool OnBuildRequested(VRCSDKRequestedBuildType requestedBuildType)
        {
            if (requestedBuildType != VRCSDKRequestedBuildType.Scene)
            {
                return true;
            }
            
            // TODO: Figure out why this causes an exception.
            //if (TryGetComponent(out NUSaveState saveState))
            //    ApplyAvatarSlots(saveState);
            
            return true;
        }
        
        public void UpdateInstructions()
        {
            if (AvatarSlots == null)
                return;

            foreach (AvatarSlot slot in AvatarSlots)
            {
                AvatarData data = slot.Data;
                int varSlotCount = data && (data.VariableSlots != null) ? data.VariableSlots.Length : 0;
                Instruction[] newInstructions = new Instruction[varSlotCount];

                for (int i = 0; i < varSlotCount; i++)
                {
                    newInstructions[i] = i < slot.Instructions.Length ? slot.Instructions[i] : new Instruction();

                    newInstructions[i].Slot = data.VariableSlots[i];
                }
                
                slot.Instructions = newInstructions;
            }
        }
        
        public void ApplyAvatarSlots(NUSaveState saveState)
        {
            if (!saveState)
                return;
            
            if (AvatarSlots == null)
                return;

            int avatarCount = AvatarSlots.Length;
            
            AnimatorController[] writers = new AnimatorController[avatarCount];
            string[] avatarIDs = new string[avatarCount];
            Vector3[] coordinates = new Vector3[avatarCount];
            int[] bitCounts = new int[avatarCount];
            Component[][] udonBehaviours = new Component[avatarCount][];
            string[][] variableNames = new string[avatarCount][];
            TypeEnum[][] variableTypes = new TypeEnum[avatarCount][];
            
            //(Component, string, TypeEnum)[][] avatarInstructions = new Tuple<Component, string, TypeEnum>[avatarCount][];
            for (int avatarIndex = 0; avatarIndex < avatarCount; avatarIndex++)
            {
                AvatarSlot slot = AvatarSlots[avatarIndex];
                int instructionCount = slot.Instructions.Length;
                AvatarData data = slot.Data;
                if (data)
                {
                    writers[avatarIndex] = data.ParameterWriter;
                    avatarIDs[avatarIndex] = data.AvatarBlueprint;
                    coordinates[avatarIndex] = data.GetKeyCoordinate() * 50f;
                    bitCounts[avatarIndex] = data.BitCount;
                }
                udonBehaviours[avatarIndex] = new Component[instructionCount];
                variableNames[avatarIndex] = new string[instructionCount];
                variableTypes[avatarIndex] = new TypeEnum[instructionCount];

                for (int instructionIndex = 0; instructionIndex < instructionCount; instructionIndex++)
                {
                    Instruction instruction = slot.Instructions[instructionIndex];
                    NUExtensions.Variable variable = instruction.Variable;

                    UdonBehaviour udon = instruction.Udon;
                    string name = variable.Name;
                    TypeEnum type = BitUtilities.GetTypeEnum(variable.Type);

                    udonBehaviours[avatarIndex][instructionIndex] = udon;
                    variableNames[avatarIndex][instructionIndex] = name;
                    variableTypes[avatarIndex][instructionIndex] = type;
                }
            }

            BindingFlags fieldFlags = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;
            void SetFieldValue<T>(T obj, string name, object value)
            {
                var field = typeof(T).GetField(name, fieldFlags);
                field?.SetValue(obj, value);
            }

            SetFieldValue(saveState, "parameterWriters", writers);
            
            SetFieldValue(saveState, "dataAvatarIDs", avatarIDs);
            SetFieldValue(saveState, "dataKeyCoords", coordinates);
            SetFieldValue(saveState, "bufferBitCounts", bitCounts);
            
            SetFieldValue(saveState, "bufferUdonBehaviours", udonBehaviours);
            SetFieldValue(saveState, "bufferVariables", variableNames);
            SetFieldValue(saveState, "bufferTypes", variableTypes);
        }
        
        #region Public Methods
        
        public static NUSaveStateData GetData(NUSaveState saveState)
        {
            if (!saveState.TryGetComponent(out NUSaveStateData data))
            {
                data = CreatePreferences(saveState);
            }

            if (TryGetLegacyData(saveState, out NUSaveStateData legacyData))
            {
                EditorUtility.CopySerialized(legacyData, data);
                DestroyImmediate(legacyData.gameObject);
            }

            data.UpdateVisibility();

            return data;
        }

        private static bool TryGetLegacyData(NUSaveState saveState, out NUSaveStateData legacyData)
        {
            legacyData = null;
            Transform parent = saveState.transform;
            bool foundData = false;
            foreach (Transform child in parent)
            {
                if (child.TryGetComponent(out NUSaveStateData data))
                {
                    if (!foundData)
                    {
                        foundData = true;
                        legacyData = data;
                        continue;
                    }
                }
                else if (child.name == "NUSS_DATA" || child.name == "NUSS_PREF")
                {

                }
                else
                {
                    continue;
                }
                
                EditorSceneManager.MarkSceneDirty(child.gameObject.scene);
                DestroyImmediate(child.gameObject);
            }
            
            return foundData;
        }

        private static NUSaveStateData CreatePreferences(NUSaveState behaviour)
        {
            NUSaveStateData data = behaviour.gameObject.AddComponent<NUSaveStateData>();
            EditorUtility.SetDirty(data);
            return data;
        }

        private void UpdateVisibility()
        {
            if (Visible)
            {
                hideFlags &= ~HideFlags.HideInInspector;
            }
            else
            {
                hideFlags |= HideFlags.HideInInspector;
            }
        }
        
        #endregion Public Methods
    }
}

#endif