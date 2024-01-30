
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using VRC.Udon;
using UdonSharpEditor;
using Nessie.Udon.Extensions;
using Nessie.Udon.SaveState.Data;

namespace Nessie.Udon.SaveState.Internal
{
    [CustomEditor(typeof(NUSaveState))]
    internal class NUSaveStateInspector : Editor
    {
        #region Private Fields

        private NUSaveState saveState;
        private SerializedObject saveStateSO;
        private NUSaveStateData data;
        private SerializedObject dataSO;
        
        private bool foldoutDefaultFields;

        private ReorderableList instructionsRList;
        private ReorderableList avatarSlotsRList;
        private Dictionary<string, ReorderableList> instructionRListDict = new Dictionary<string, ReorderableList>();

        private SerializedProperty propEventReceiver;
        private SerializedProperty propFallbackAvatar;

        private SerializedProperty propAvatarSlots;
        private SerializedProperty propDataVisible;

        #endregion Private Fields

        #region Unity Events

        private void OnEnable()
        {
            saveState = (NUSaveState)target;
            saveStateSO = serializedObject;
            if (!UdonSharpEditorUtility.IsProxyBehaviour(saveState)) return;
            
            data = NUSaveStateData.GetData(saveState);

            NUExtensions.ClearTableCache();
            data.UpdateInstructions();
            dataSO = new SerializedObject(data);
            dataSO.Update();

            InitializeProperties();

            #region Reorderable Lists

            ReorderableList GetInstructionRList(int index)
            {
                SerializedProperty propertyAvatarSlot = propAvatarSlots.GetArrayElementAtIndex(index);
                SerializedProperty propertyAvatarData = propertyAvatarSlot.FindPropertyRelative(nameof(AvatarSlot.Data));
                SerializedProperty propertyInstructions = propertyAvatarSlot.FindPropertyRelative(nameof(AvatarSlot.Instructions));

                string instructionKey = propertyInstructions.propertyPath;

                ReorderableList instructionRList;
                if (instructionRListDict.ContainsKey(instructionKey))
                {
                    instructionRList = instructionRListDict[instructionKey];
                }
                else
                {
                    instructionRList = new ReorderableList(propertyAvatarSlot.serializedObject, propertyInstructions)
                    {
                        displayAdd = false,
                        displayRemove = false,
                        draggable = false,
                        
                        elementHeight = propertyInstructions.isExpanded ? 20f : 0f,
                        footerHeight = 2f,

                        drawHeaderCallback = (Rect rect) =>
                        {
                            rect.width = rect.width / 2f - 2f;

                            Rect foldoutRect = new Rect(rect) { x = rect.x + 12, width = rect.width - 12 };
                            Rect avatarDataRect = new Rect(rect) { x = rect.x + rect.width };
                            
                            EditorGUI.BeginChangeCheck();
                            bool isExpanded = EditorGUI.Foldout(foldoutRect, propertyInstructions.isExpanded, EditorStyles.ContentInstructionList);
                            if (EditorGUI.EndChangeCheck())
                            {
                                propertyInstructions.isExpanded = isExpanded;
                                instructionRListDict[instructionKey].elementHeight = isExpanded ? 20f : 0f;
                            }
                            
                            EditorGUI.BeginChangeCheck();
                            EditorGUI.PropertyField(avatarDataRect, propertyAvatarData, new GUIContent(""));
                            if (EditorGUI.EndChangeCheck())
                            {
                                // Update instruction slots.
                                AvatarData data = SerializationUtilities.GetPropertyValue<AvatarData>(propertyAvatarData);
                                int newSize = data && (data.VariableSlots != null) ? data.VariableSlots.Length : 0;
                                propertyInstructions.arraySize = newSize;
                                for (int i = 0; i < newSize; i++)
                                {
                                    SerializedProperty propInstruction = propertyInstructions.GetArrayElementAtIndex(i);
                                    Instruction instruction = SerializationUtilities.GetPropertyValue<Instruction>(propInstruction);
                                    instruction.Slot = data.VariableSlots[i];
                                    SerializationUtilities.SetPropertyValue(propInstruction, instruction);
                                }
                            }
                        },

                        drawElementCallback = (Rect rect, int elementIndex, bool isActive, bool isFocused) =>
                        {
                            if (!propertyInstructions.isExpanded)
                            {
                                return;
                            }
                            
                            rect.width = (rect.width - 4f) / 3f;
                            rect.height = EditorGUIUtility.singleLineHeight;
                            rect.y += (instructionRListDict[instructionKey].elementHeight - rect.height) / 2f;
                            
                            Rect labelRect = new Rect(rect);
                            Rect udonRect = new Rect(rect) { x = rect.x + rect.width + 2f };
                            Rect variableRect = new Rect(rect) { x = rect.x + rect.width * 2f + 4f };
                            
                            SerializedProperty propInstruction = propertyInstructions.GetArrayElementAtIndex(elementIndex);
                            SerializedProperty propUdon = propInstruction.FindPropertyRelative("udon");
                            SerializedProperty propSlot = propInstruction.FindPropertyRelative("slot");
                            SerializedProperty propVar = propInstruction.FindPropertyRelative("variable");
                            SerializedProperty propVars = propInstruction.FindPropertyRelative("variables");
                            SerializedProperty propVarIndex = propInstruction.FindPropertyRelative("variableIndex");

                            VariableSlot varSlot = SerializationUtilities.GetPropertyValue<VariableSlot>(propSlot);
                            EditorGUI.LabelField(labelRect, $"{varSlot.Name} ({varSlot.TypeEnum})");
                            
                            EditorGUI.BeginChangeCheck();
                            EditorGUI.PropertyField(udonRect, propUdon, new GUIContent(""));
                            if (EditorGUI.EndChangeCheck())
                            {
                                AvatarSlot slot = SerializationUtilities.GetPropertyValue<AvatarSlot>(propertyAvatarSlot);
                                slot.UpdateInstruction(elementIndex);
                                SerializationUtilities.SetPropertyValue(propertyAvatarSlot, slot);
                            }
                            
                            SerializedProperty propVariableLabels = propInstruction.FindPropertyRelative("variableLabels");
                            string[] variableLabels = SerializationUtilities.GetPropertyValue<string[]>(propVariableLabels);
                            EditorGUI.BeginChangeCheck();
                            int newVariableIndex = EditorGUI.Popup(variableRect, propVarIndex.intValue, variableLabels);
                            if (EditorGUI.EndChangeCheck())
                            {
                                propVarIndex.intValue = newVariableIndex;

                                if (newVariableIndex < 0 || newVariableIndex >= propVars.arraySize)
                                {
                                    return;
                                }

                                SerializedProperty propNewVar = propVars.GetArrayElementAtIndex(newVariableIndex);
                                
                                NUExtensions.Variable variable = SerializationUtilities.GetPropertyValue<NUExtensions.Variable>(propNewVar);
                                SerializationUtilities.SetPropertyValue(propVar, variable);
                            }
                        },
                    };
                    
                    instructionRListDict[instructionKey] = instructionRList;
                }
                
                return instructionRList;
            };

            avatarSlotsRList = new ReorderableList(dataSO, propAvatarSlots)
            {
                draggable = propAvatarSlots.isExpanded,
                displayAdd = propAvatarSlots.isExpanded,
                displayRemove = propAvatarSlots.isExpanded,
                
                elementHeight = propAvatarSlots.isExpanded ? 20f : 0f, // Why is the default 21f???
                footerHeight = propAvatarSlots.isExpanded ? 20f : 0f,
                
                drawHeaderCallback = (Rect rect) =>
                {
                    Rect foldoutRect = new Rect(rect){ x = rect.x + 12, width = rect.width - 12 };
                    //EditorGUI.TextField(foldoutRect, "Avatars");
                    EditorGUI.BeginChangeCheck();
                    
                    bool isExpanded = EditorGUI.Foldout(foldoutRect, propAvatarSlots.isExpanded, EditorStyles.ContentAvatarList);

                    if (!EditorGUI.EndChangeCheck())
                    {
                        return;
                    }

                    propAvatarSlots.isExpanded = isExpanded;
                    avatarSlotsRList.draggable = isExpanded;
                    avatarSlotsRList.displayAdd = isExpanded;
                    avatarSlotsRList.displayRemove = isExpanded;

                    avatarSlotsRList.elementHeight = isExpanded ? 20f : 0f;
                    avatarSlotsRList.footerHeight = isExpanded ? 20f : 0f;
                },
                
                drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
                {
                    if (!propAvatarSlots.isExpanded)
                    {
                        return;
                    }

                    ReorderableList instructionRList = GetInstructionRList(index);
                    
                    //float listHeight = Math.Min(propertyInstructions.arraySize, 1) * instructionRList.elementHeight + 4f + 4f + instructionRList.headerHeight + 4f;
                    //instructionRList.DoList(new Rect(rect.x, rect.y + 2f, rect.width, listHeight));
                    instructionRList.DoList(new Rect(rect){ y = rect.y + 2f }); // Add half of padding to center.
                },
                
                elementHeightCallback = (int index) =>
                {
                    if (!propAvatarSlots.isExpanded)
                    {
                        return 0f;
                    }

                    ReorderableList instructionRList = GetInstructionRList(index);
                    
                    float headerHeight = instructionRList.headerHeight;

                    int elementCount = instructionRList.count;
                    float elementHeight = instructionRList.elementHeight;
                    bool isExpanded = instructionRList.elementHeight > 0;
                    bool hasElements = elementCount > 0;
                    if (isExpanded && hasElements) elementHeight += 2f;
                    float elementsHeight = 0f;
                    elementsHeight += 4f; // listElementTopPadding.
                    elementsHeight += Math.Max(elementCount * elementHeight, elementHeight); // Sum of element heights.
                    elementsHeight += 4f; // kListElementBottomPadding.
                    
                    float footerHeight = instructionRList.footerHeight;

                    return headerHeight + elementsHeight + footerHeight;
                },
            };

            #endregion
        }

        public override void OnInspectorGUI()
        {
            // Draws the default convert to UdonBehaviour button, program asset field, sync settings, etc.
            if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target) || target == null) return;

            dataSO.Update();

            DrawBanner();
            DrawMessages();
            DrawSaveStateUtilities();
            DrawSaveStateData();

            EditorGUI.indentLevel++;
            DrawDefaultFields();
            EditorGUI.indentLevel--;

            if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                GUI.FocusControl(null);
            }
        }

        #endregion Unity Events

        #region Drawers

        private void DrawDefaultFields()
        {
            foldoutDefaultFields = EditorGUILayout.Foldout(foldoutDefaultFields, EditorStyles.ContentDefault);
            if (foldoutDefaultFields)
            {
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.ObjectField(EditorStyles.ContentData, data, typeof(NUSaveStateData), true);

                EditorGUILayout.PropertyField(propDataVisible);
                dataSO.ApplyModifiedProperties();

                base.OnInspectorGUI();
            }
        }

        private void DrawMessages()
        {
            //void DrawInfoMessage(string s) => EditorGUILayout.HelpBox(s, MessageType.Info);
            //void DrawWarningMessage(string s) => EditorGUILayout.HelpBox(s, MessageType.Warning);
            //void DrawErrorMessage(string s) => EditorGUILayout.HelpBox(s, MessageType.Error);
        }

        private void DrawBanner()
        {
            GUILayout.BeginHorizontal(UnityEditor.EditorStyles.helpBox);
            GUILayout.Label("<b>Nessie's Udon Save State</b>", EditorStyles.RTLabel);

            float iconSize = EditorGUIUtility.singleLineHeight;
            
            if (GUILayout.Button(EditorStyles.ContentVRChat, GUIStyle.none, GUILayout.Width(iconSize), GUILayout.Height(iconSize)))
            {
                Application.OpenURL("https://vrchat.com/home/user/usr_95c31e1e-15c3-4bf4-b8dd-00373124d67a");
            }

            GUILayout.Space(iconSize / 4);
            
            if (GUILayout.Button(EditorStyles.ContentGitHub, GUIStyle.none, GUILayout.Width(iconSize), GUILayout.Height(iconSize)))
            {
                Application.OpenURL("https://github.com/Nestorboy?tab=repositories"); // :)
            }

            GUILayout.EndHorizontal();
        }

        private void DrawSaveStateUtilities()
        {
            using (new EditorStyles.BlockScope("<b>Save State Utilities</b>"))
            {
                using (new EditorGUI.DisabledScope(data.AvatarSlots == null || data.AvatarSlots.Length == 0))
                {
                    DrawWorldAssetsButton();
                    DrawAvatarAssetsButton();
                }

                bool isLegacy = data.Instructions?.Length > 0 && propAvatarSlots.arraySize <= 0;
                using (new EditorGUI.DisabledScope(!isLegacy))
                {
                    DrawMigrateButton();
                }
            }
        }

        private void DrawSaveStateData()
        {
            // Fallback avatar, Variable count, Instructions (Udon & Variable) Scroll, Data Avatar IDs Scroll
            using (new EditorStyles.BlockScope("<b>Save State Data</b>"))
            {
                EditorGUILayout.PropertyField(propEventReceiver, EditorStyles.ContentEventReceiver);
                EditorGUILayout.PropertyField(propFallbackAvatar, EditorStyles.ContentFallbackAvatar);
                saveStateSO.ApplyModifiedProperties();

                EditorGUILayout.Space();
                avatarSlotsRList.DoLayoutList();
                if (dataSO.ApplyModifiedProperties())
                {
                    dataSO.Update(); // Is this necessary?

                    if (propAvatarSlots.arraySize > 0)
                    {
                        EditorUtility.SetDirty(saveState);
                        data.ApplyAvatarSlots(saveState);
                    }
                }
            }
        }

        private void DrawWorldAssetsButton()
        {
            if (!GUILayout.Button(EditorStyles.ContentWorldAssets)) 
                return;
            
            if (!AssetGenerator.TrySaveFolderInProjectPanel("World Animator Folder", AssetGenerator.PathWorld, "Animators", out string animatorPath)) 
                return;

            System.Diagnostics.Stopwatch timer = new System.Diagnostics.Stopwatch();
            timer.Start();

            bool success = false;
            try
            {
                AssetDatabase.StartAssetEditing();

                var avatars = data.AvatarSlots.Select(slot => slot.Data).ToArray();
                var controllers = AssetGenerator.CreateWorldAnimators(avatars, animatorPath);

                for (int i = 0; i < avatars.Length; i++)
                {
                    SerializedObject avatarSO = new SerializedObject(avatars[i]);
                    SerializedProperty propWriter = avatarSO.FindProperty(nameof(AvatarData.ParameterWriter));
                    SerializationUtilities.SetPropertyValue(propWriter, controllers[i]);
                    avatarSO.ApplyModifiedPropertiesWithoutUndo();
                }

                EditorUtility.SetDirty(saveState);
                data.ApplyAvatarSlots(saveState);

                success = true;
            }
            finally
            {
                AssetDatabase.StopAssetEditing();

                timer.Stop();
                
                if (success)
                    DebugUtilities.Log($"World asset creation took: {timer.Elapsed:mm\\:ss\\.fff}");
            }
        }
        
        private void DrawAvatarAssetsButton()
        {
            if (!GUILayout.Button(EditorStyles.ContentAvatarAssets)) 
                return;

            if (!AssetGenerator.TrySaveFolderInProjectPanel("Avatar Package Folder", AssetGenerator.PathAvatar, "Packages", out string packagePath))
                return;
            
            string pathTemplateArmature = $"{AssetGenerator.PathAvatar}/Template/SaveState-Avatar.fbx";
            string pathTemplatePrefab = $"{AssetGenerator.PathAvatar}/Template/SaveState-Avatar-Template.prefab";
            if (!System.IO.File.Exists(pathTemplateArmature) || !System.IO.File.Exists(pathTemplatePrefab))
            {
                DebugUtilities.LogError($"Could not find all of the template assets at {AssetGenerator.PathAvatar}/Template/");

                return;
            }

            System.Diagnostics.Stopwatch timer = new System.Diagnostics.Stopwatch();
            timer.Start();

            bool success = false;
            try
            {
                AssetDatabase.StartAssetEditing();

                AssetGenerator.CreateAvatarPackages(data.AvatarSlots.Select(slot => slot.Data).ToArray(), packagePath);

                success = true;
            }
            finally
            {
                AssetDatabase.StopAssetEditing();

                AssetDatabase.SaveAssets();

                timer.Stop();
                
                if (success)
                    DebugUtilities.Log($"Avatar asset creation took: {timer.Elapsed:mm\\:ss\\.fff}");
            }
        }

        private void DrawMigrateButton()
        {
            if (!GUILayout.Button(EditorStyles.ContentMigrateData))
                return;
            
            if (!AssetGenerator.TrySaveFolderInProjectPanel("Avatar Data Folder", AssetGenerator.PathAvatar, "SOs", out string dataPath))
                return;
            
            try
            {
                AssetDatabase.StartAssetEditing();

                string[] avatarDataPaths = AssetGenerator.MigrateSaveStateData(saveState, data, dataPath);
                Legacy.Instruction[][] avatarInstructions = AssetGenerator.SplitAvatarInstructions(data.Instructions);
                propAvatarSlots.arraySize = avatarDataPaths.Length;
                for (int i = 0; i < avatarDataPaths.Length; i++)
                {
                    string path = avatarDataPaths[i];
                    AssetDatabase.ImportAsset(path);
                            
                    AvatarData newAvatarData = AssetDatabase.LoadAssetAtPath<AvatarData>(path);
                    Instruction[] newInstructions = new Instruction[avatarInstructions[i].Length];
                    for (int j = 0; j < newInstructions.Length; j++)
                    {
                        newInstructions[j] = new Instruction(avatarInstructions[i][j]);
                    }

                    AvatarSlot newSlot = new AvatarSlot() { Data = newAvatarData, Instructions = newInstructions };
                    SerializationUtilities.SetPropertyValue(propAvatarSlots.GetArrayElementAtIndex(i), newSlot);
                }
                        
                dataSO.ApplyModifiedProperties();

                EditorUtility.SetDirty(saveState);
                data.ApplyAvatarSlots(saveState);
                
                // TODO: Figure out a way to prevent "SerializedObject of SerializedProperty has been Disposed" exception.
                //Selection.activeObject = AssetDatabase.LoadAssetAtPath<AvatarData>(assetPath);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
        }
        
        #endregion Drawers

        private void InitializeProperties()
        {
            propEventReceiver = saveStateSO.FindProperty(nameof(NUSaveState.CallbackReceiver));
            propFallbackAvatar = saveStateSO.FindProperty(nameof(NUSaveState.FallbackAvatarID));
            
            propAvatarSlots = dataSO.FindProperty(nameof(NUSaveStateData.AvatarSlots));
            propDataVisible = dataSO.FindProperty(nameof(NUSaveStateData.Visible));
        }
    }
}