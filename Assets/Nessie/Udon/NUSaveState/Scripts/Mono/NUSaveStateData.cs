#if UNITY_EDITOR

using System;
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

        public AvatarSlot[] AvatarSlots;
        
        [FormerlySerializedAs("DataPreferences")]
        public Legacy.Preferences Preferences;
        
        //[Obsolete("This is a legacy field used for backwards compatibility, instructions are now stored in AvatarSlots instead.")]
        [FormerlySerializedAs("DataInstructions")]
        public Legacy.Instruction[] Instructions;

        #endregion Public Fields

        public int callbackOrder => 0;

        private bool visible;

        public bool Visible
        {
            get => visible;
            set
            {
                visible = value;
                SetVisibility(visible);
            }
        }

        private void OnValidate()
        {
            //Debug.Log("NUSaveStateData: OnValidate");
            
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
            SetFieldValue(saveState, "bufferBitCounts", bitCounts);
            
            SetFieldValue(saveState, "bufferUdonBehaviours", udonBehaviours);
            SetFieldValue(saveState, "bufferVariables", variableNames);
            SetFieldValue(saveState, "bufferTypes", variableTypes);
        }
        
        #region Public Methods
        
        public static NUSaveStateData GetPreferences(NUSaveState behaviour)
        {
            NUSaveStateData oldData = behaviour.GetComponent<NUSaveStateData>();
            if (oldData)
            {
                oldData.SetVisibility(oldData.Visible);
                return oldData;
            }

            NUSaveStateData newData = CreatePreferences(behaviour);
            Transform parent = behaviour.transform;

            // Look for legacy data, and if found, convert it.
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform dataTransform = parent.GetChild(i);
                NUSaveStateData dataBehaviour = dataTransform.GetComponent<NUSaveStateData>();

                if (dataBehaviour != null)
                {
                    if (oldData == null)
                    {
                        oldData = dataBehaviour;
                        EditorUtility.CopySerialized(oldData, newData);
                    }

                    DestroyImmediate(dataBehaviour.gameObject);

                    EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                }
                else if (dataTransform.name == "NUSS_DATA" || dataTransform.name == "NUSS_PREF")
                {
                    DestroyImmediate(dataTransform.gameObject);

                    EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                }
            }

            newData.SetVisibility(newData.Visible);
            
            return newData;
        }

        public static NUSaveStateData CreatePreferences(NUSaveState behaviour)
        {
            NUSaveStateData data = behaviour.gameObject.AddComponent<NUSaveStateData>();
            data.tag = "EditorOnly";
            data.hideFlags = HideFlags.HideInInspector; // Fix initialization

            EditorUtility.SetDirty(data);
            PrefabUtility.RecordPrefabInstancePropertyModifications(data);
            //EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            
            return data;
        }

        // TODO: Find proper HideFlag dirtying fix that works with prefabs.
        private void SetVisibility(bool show)
        {
            if (show)
            {
                hideFlags &= ~HideFlags.HideInInspector;
            }
            else
            {
                hideFlags |= HideFlags.HideInInspector;
            }

            EditorUtility.SetDirty(this);
            PrefabUtility.RecordPrefabInstancePropertyModifications(this);
            
            //PrefabUtility.SavePrefabAsset(gameObject);
            //EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            //EditorApplication.DirtyHierarchyWindowSorting();
            //EditorApplication.RepaintHierarchyWindow();
        }

        #endregion Public Methods
    }
}

#endif