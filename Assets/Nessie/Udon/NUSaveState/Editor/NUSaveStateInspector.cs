
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using VRC.SDKBase;
using VRC.Udon;
using UdonSharpEditor;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace Nessie.Udon.SaveState.Internal
{
    [CustomEditor(typeof(NUSaveState))]
    internal class NUSaveStateInspector : Editor
    {
        #region Private Fields

        private NUSaveState _behaviourProxy;

        // Assets.
        private string pathSaveState = "Assets/Nessie/Udon/NUSaveState";

        private string[] assetFolderPaths;
        private string[] assetFolderNames;
        private DefaultAsset[] assetFolders;
        private DefaultAsset assetFolderSelected;
        private string assetFolderPath;
        private int assetFolderIndex = -1;

        private string[] muscleNames = new string[]
        {
            "LeftHand.Index.",
            "LeftHand.Middle.",
            "LeftHand.Ring.",
            "LeftHand.Little.",
            "RightHand.Index.",
            "RightHand.Middle.",
            "RightHand.Ring.",
            "RightHand.Little.",
        };

        // Save State Utilities.
        private string saveStateParameterName = "parameter";
        private string saveStateSeed = "";
        private int saveStateHash;

        // Save State data.
        private DataInstruction[] dataInstructions;
        private int dataBitCount;

        // Validity checking.
        private static Type[] convertableTypes = new Type[]
        {
            typeof(int),
            typeof(uint),
            typeof(long),
            typeof(ulong),
            typeof(short),
            typeof(ushort),
            typeof(byte),
            typeof(sbyte),
            typeof(char),
            typeof(float),
            typeof(double),
            typeof(decimal),
            typeof(bool),
            typeof(Vector2),
            typeof(Vector3),
            typeof(Vector4),
            /* UI 1.5 Update
            typeof(Vector2Int),
            typeof(Vector3Int),
            */
            typeof(Quaternion),
            typeof(Color),
            typeof(Color32),
            typeof(VRCPlayerApi)
        };
        private static int[] convertableBitCount = new int[]
        {
            32,
            32,
            64,
            64,
            16,
            16,
            8,
            8,
            16,
            32,
            16,
            128,
            1,
            64,
            96,
            128,
            /* UI 1.5 Update
            128,
            128,
            */
            128,
            128,
            32,
            32
        };

        // UI.
        private bool foldoutDefaultFields;

        private UnityEditorInternal.ReorderableList instructionList;
        private int selectedInstructionIndex;
        private UnityEditorInternal.ReorderableList avatarList;

        private SerializedProperty propertyEventReciever;
        private SerializedProperty propertyFallbackAvatar;

        private SerializedProperty propertyBufferByteCount;
        private SerializedProperty propertyBufferUdonBehaviours;
        private SerializedProperty propertyBufferVariables;
        private SerializedProperty propertyBufferTypes;

        private SerializedProperty propertyAvatarIDs;
        private SerializedProperty propertyKeyCoords;

        private SerializedProperty propertyParameterClearer;
        private SerializedProperty propertyParameterWriters;

        private Texture2D _iconGitHub;
        private Texture2D _iconVRChat;

        private GUIStyle styleHelpBox;
        private GUIStyle styleBox;
        private GUIStyle styleRichTextLabel;
        private GUIStyle styleRichTextButton;

        private string labelAsteriskFolder;
        private string labelAsteriskSeed;
        private string labelAsteriskName;

        private GUIContent contentAssetFolder = new GUIContent("Asset Folder", "Folder used when applying or generating world/avatar assets.");
        private GUIContent contentEncryptionSeed = new GUIContent("Encryption Seed", "Seed used to generate key coordinates.");
        private GUIContent contentParameterName = new GUIContent("Parameter Name", "Name used as a parameter prefix.");

        private GUIContent contentWorldAssets = new GUIContent("Generate World Assets", "Creates world-side assets into the selected folder.");
        private GUIContent contentAvatarAssets = new GUIContent("Generate Avatar Assets", "Creates avatar-side assets and leaves an exported package in the selected folder.");
        private GUIContent contentApplyAnimators = new GUIContent("Apply Save State Animators", "Applies animator controllers from the selected folder.");
        private GUIContent contentApplyKeys = new GUIContent("Apply Save State Keys", "Generates keys used to identify the specified data avatars.");

        private GUIContent contentEventReciever = new GUIContent("Callback Reciever", "UdonBehaviour which recieves the following callback events:\n_SSSaved _SSSaveFailed _SSPostSave\n_SSLoaded _SSLoadFailed _SSPostLoad");
        private GUIContent contentFallbackAvatar = new GUIContent("Fallback Avatar", "Blueprint ID of the avatar which is switched to when the data processing is done.");

        private GUIContent contentInstructionList = new GUIContent("Data Instructions", "List of UdonBehaviours variables used when saving or loading data.");
        private GUIContent contentAvatarList = new GUIContent("Avatar IDs", "List of avatars used as data buffers. (Unused avatars are drawn with disabled fields.)");

        #endregion Private Fields

        #region Private Classes

        private class DataInstruction
        {
            public UdonBehaviour Udon;

            public string[] VariableNames;
            public Type[] VariableTypes;

            private int _variableIndex = -1;
            public int VariableIndex
            {
                get => _variableIndex;
                set
                {
                    _variableIndex = value;

                    if (value < 0)
                        VariableBits = 0;
                    else
                    {
                        int typeIndex = Array.IndexOf(convertableTypes, VariableTypes[value]);
                        VariableBits = typeIndex < 0 ? 0 : convertableBitCount[typeIndex];
                    }
                }
            }

            public int VariableBits = 0;

            public void PrepareLabels()
            {
                string[] newLabels = new string[Math.Min(VariableNames.Length, VariableTypes.Length)];
                for (int i = 0; i < newLabels.Length; i++)
                    newLabels[i] = $"{VariableNames[i]} ({convertableBitCount[Array.IndexOf(convertableTypes, VariableTypes[i])]})";
                VariableLabels = newLabels;
            }

            public string[] VariableLabels = new string[0];
        }

        #endregion Private Classes

        #region Editor Events

        private void OnEnable()
        {
            if (target == null) return; // Prevents some iffy errors in the console.

            _behaviourProxy = (NUSaveState)target;
            _behaviourProxy.UpdateProxy();
            serializedObject.Update();

            GetUIAssets();

            GetAssetFolders();

            InitializeProperties();

            GetSaveStateData();

            #region Reorderable Lists

            instructionList = new UnityEditorInternal.ReorderableList(serializedObject, propertyBufferUdonBehaviours, true, true, true, true);
            instructionList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
            {
                if (!instructionList.serializedProperty.isExpanded) return;

                Rect udonFieldRect = new Rect(rect.x, rect.y + 1.5f, (rect.width - 2) / 2, EditorGUIUtility.singleLineHeight);
                Rect variableFieldRect = new Rect(rect.x + udonFieldRect.width + 4, rect.y + 1.5f, (rect.width - 2) / 2, EditorGUIUtility.singleLineHeight);

                EditorGUI.BeginChangeCheck();
                dataInstructions[index].Udon = (UdonBehaviour)EditorGUI.ObjectField(udonFieldRect, dataInstructions[index].Udon, typeof(UdonBehaviour), true);
                if (EditorGUI.EndChangeCheck())
                {
                    GetValidVariables(dataInstructions[index].Udon, out List<string> vars, out List<Type> types);

                    string[] newValidNames = vars.ToArray();
                    Type[] newValidTypes = types.ToArray();

                    int newVariableIndex = dataInstructions[index].VariableIndex < 0 ? -1 : Array.IndexOf(newValidNames, dataInstructions[index].VariableNames[dataInstructions[index].VariableIndex]);
                    if (newVariableIndex < 0)
                        dataInstructions[index].VariableIndex = -1;
                    else
                        dataInstructions[index].VariableIndex = newValidTypes[newVariableIndex] == dataInstructions[index].VariableTypes[dataInstructions[index].VariableIndex] ? newVariableIndex : -1;

                    dataInstructions[index].VariableNames = newValidNames;
                    dataInstructions[index].VariableTypes = newValidTypes;
                    dataInstructions[index].PrepareLabels();

                    dataBitCount = CalculateBitCount(dataInstructions);

                    int avatarCount = Mathf.CeilToInt(dataBitCount / 256f);
                    if (avatarCount > propertyAvatarIDs.arraySize)
                    {
                        propertyAvatarIDs.arraySize = avatarCount;
                    }

                    SetSaveStateData(index);
                }

                EditorGUI.BeginChangeCheck();
                dataInstructions[index].VariableIndex = EditorGUI.Popup(variableFieldRect, dataInstructions[index].VariableIndex, dataInstructions[index].VariableLabels);
                if (EditorGUI.EndChangeCheck())
                {
                    dataBitCount = CalculateBitCount(dataInstructions);

                    int avatarCount = Mathf.CeilToInt(dataBitCount / 256f);
                    if (avatarCount > propertyAvatarIDs.arraySize)
                    {
                        propertyAvatarIDs.arraySize = avatarCount;
                    }

                    SetSaveStateData(index);
                }
            };
            instructionList.drawHeaderCallback = (Rect rect) =>
            {
                instructionList.serializedProperty.isExpanded = EditorGUI.Foldout(new Rect(rect.x + 12, rect.y, 16, EditorGUIUtility.singleLineHeight), instructionList.serializedProperty.isExpanded, "");
                EditorGUI.LabelField(new Rect(rect.x + 16, rect.y, (rect.width - 16) / 2, EditorGUIUtility.singleLineHeight), contentInstructionList);
                EditorGUI.LabelField(new Rect(rect.x + 16 + (rect.width - 16) / 2, rect.y, (rect.width - 16) / 2, EditorGUIUtility.singleLineHeight), $"Bits: {dataBitCount} / {Mathf.CeilToInt(dataBitCount / 256f) * 256} Bytes: {Mathf.Ceil(dataBitCount / 8f)} / {Mathf.CeilToInt(dataBitCount / 256f) * 32}");
            };
            instructionList.elementHeightCallback = (int index) =>
            {
                return instructionList.serializedProperty.isExpanded ? instructionList.elementHeight : 0;
            };
            instructionList.onSelectCallback = (UnityEditorInternal.ReorderableList list) =>
            {
                selectedInstructionIndex = list.index;
            };
            instructionList.onReorderCallback = (UnityEditorInternal.ReorderableList list) =>
            {
                DataInstruction selectedInstruction = dataInstructions[selectedInstructionIndex];
                if (list.index < selectedInstructionIndex)
                    Array.Copy(dataInstructions, list.index, dataInstructions, list.index + 1, selectedInstructionIndex - list.index);
                else
                    Array.Copy(dataInstructions, selectedInstructionIndex + 1, dataInstructions, selectedInstructionIndex, list.index - selectedInstructionIndex);
                dataInstructions[list.index] = selectedInstruction;

                SetSaveStateData();
            };
            instructionList.onAddCallback = (UnityEditorInternal.ReorderableList list) =>
            {
                DataInstruction newData = new DataInstruction();
                if (list.count > 0)
                {
                    int copyIndex = list.count - 1;

                    GetValidVariables(dataInstructions[copyIndex].Udon, out List<string> vars, out List<Type> types);

                    newData.Udon = dataInstructions[copyIndex].Udon;

                    newData.VariableNames = vars.ToArray();
                    newData.VariableTypes = types.ToArray();
                    newData.PrepareLabels();

                    int newVariableIndex = Array.IndexOf(newData.VariableNames, dataInstructions[copyIndex]);
                    if (newVariableIndex >= 0)
                        newData.VariableIndex = newData.VariableTypes[newVariableIndex] == dataInstructions[copyIndex].VariableTypes[newVariableIndex] ? newVariableIndex : -1;
                }
                ArrayUtility.Add(ref dataInstructions, newData);

                propertyBufferUdonBehaviours.arraySize++;
                propertyBufferVariables.arraySize++;
                propertyBufferTypes.arraySize++;

                SetSaveStateData();
            };
            instructionList.onRemoveCallback = (UnityEditorInternal.ReorderableList list) =>
            {
                dataBitCount -= dataInstructions[list.index].VariableBits;

                ArrayUtility.RemoveAt(ref dataInstructions, list.index);

                propertyBufferUdonBehaviours.DeleteArrayElementAtIndex(list.index);
                propertyBufferVariables.DeleteArrayElementAtIndex(list.index);
                propertyBufferTypes.DeleteArrayElementAtIndex(list.index);
                if (propertyBufferUdonBehaviours.GetArrayElementAtIndex(list.index) != null)
                    propertyBufferUdonBehaviours.DeleteArrayElementAtIndex(list.index);

                if (list.index >= dataInstructions.Length)
                    list.index--;

                int avatarCount = Mathf.CeilToInt(dataBitCount / 256f);
                if (avatarCount > propertyAvatarIDs.arraySize)
                    propertyAvatarIDs.arraySize = avatarCount;

                SetSaveStateData();
            };

            instructionList.footerHeight = instructionList.serializedProperty.isExpanded ? 20 : 0;

            avatarList = new UnityEditorInternal.ReorderableList(serializedObject, propertyAvatarIDs, true, true, false, true);
            avatarList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
            {
                if (!avatarList.serializedProperty.isExpanded) return;

                Rect avatarFieldRect = new Rect(rect) { y = rect.y + 1.5f, height = EditorGUIUtility.singleLineHeight };

                SerializedProperty avatarID = propertyAvatarIDs.GetArrayElementAtIndex(index);

                EditorGUI.BeginDisabledGroup(index >= Mathf.CeilToInt(dataBitCount / 256f));

                EditorGUI.BeginChangeCheck();
                avatarID.stringValue = EditorGUI.TextField(avatarFieldRect, avatarID.stringValue);
                if (EditorGUI.EndChangeCheck())
                {
                    serializedObject.ApplyModifiedProperties();
                    _behaviourProxy.ApplyProxyModifications();
                }

                EditorGUI.EndDisabledGroup();
            };
            avatarList.drawHeaderCallback = (Rect rect) =>
            {
                avatarList.serializedProperty.isExpanded = EditorGUI.Foldout(new Rect(rect.x + 12, rect.y, 16, EditorGUIUtility.singleLineHeight), avatarList.serializedProperty.isExpanded, "");
                EditorGUI.LabelField(new Rect(rect.x + 16, rect.y, (rect.width - 16) / 2, EditorGUIUtility.singleLineHeight), contentAvatarList);
                EditorGUI.LabelField(new Rect(rect.x + 16 + (rect.width - 16) / 2, rect.y, (rect.width - 16) / 2, EditorGUIUtility.singleLineHeight), $"Avatars: {Mathf.CeilToInt(dataBitCount / 256f)} / {propertyAvatarIDs.arraySize}");
            };
            avatarList.elementHeightCallback = (int index) =>
            {
                return avatarList.serializedProperty.isExpanded ? avatarList.elementHeight : 0;
            };
            avatarList.onRemoveCallback = (UnityEditorInternal.ReorderableList list) =>
            {
                if (propertyAvatarIDs.arraySize <= Mathf.CeilToInt(dataBitCount / 256f)) return;

                propertyAvatarIDs.DeleteArrayElementAtIndex(list.index);

                serializedObject.ApplyModifiedProperties();
                _behaviourProxy.ApplyProxyModifications();
            };

            avatarList.footerHeight = avatarList.serializedProperty.isExpanded ? 20 : 0;

            #endregion;
        }

        public override void OnInspectorGUI()
        {
            if (styleBox == null) InitializeStyles();

            _behaviourProxy = (NUSaveState)target;

            // Draws the default convert to UdonBehaviour button, program asset field, sync settings, etc.
            if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target) || target == null) return;

            if (!UdonSharpEditorUtility.IsProxyBehaviour(_behaviourProxy)) return;

            _behaviourProxy.UpdateProxy();
            serializedObject.Update();

            DrawBanner();

            DrawMessages();

            DrawSaveStateUtilities();

            DrawSaveStateData();

            EditorGUI.indentLevel++;
            DrawDefaultFields();
            EditorGUI.indentLevel--;
        }

        #endregion Editor Events

        #region Drawers

        private void DrawDefaultFields()
        {
            foldoutDefaultFields = EditorGUILayout.Foldout(foldoutDefaultFields, new GUIContent("Default Inspector", "Foldout for default UdonSharpBehaviour inspector."));
            if (foldoutDefaultFields)
                base.OnInspectorGUI();
        }

        private void DrawMessages()
        {
            if (propertyParameterWriters.arraySize < Math.Min(dataBitCount, 256))
                EditorGUILayout.HelpBox("There are not enough animators controllers on the behaviour.\nPlease select an asset folder and apply the animator controllers again.", MessageType.Error);
            if (propertyKeyCoords.arraySize < Mathf.CeilToInt(dataBitCount / 256f))
                EditorGUILayout.HelpBox("There are not enough key coordinates on the behaviour.\nPlease enter the seed and apply the keys again.", MessageType.Error);
        }

        private void DrawBanner()
        {
            GUILayout.BeginHorizontal(EditorStyles.helpBox);

            GUILayout.Label("<b>Nessie's Udon Save State</b>", styleRichTextLabel);

            float iconSize = EditorGUIUtility.singleLineHeight;

            GUIContent buttonVRChat = new GUIContent("", "VRChat");
            GUIStyle styleVRChat = new GUIStyle(GUI.skin.box);
            if (_iconVRChat != null)
            {
                buttonVRChat = new GUIContent(_iconVRChat, "VRChat");
                styleVRChat = GUIStyle.none;
            }

            if (GUILayout.Button(buttonVRChat, styleVRChat, GUILayout.Width(iconSize), GUILayout.Height(iconSize)))
            {
                Application.OpenURL("https://vrchat.com/home/user/usr_95c31e1e-15c3-4bf4-b8dd-00373124d67a");
            }

            GUILayout.Space(iconSize / 4);

            GUIContent buttonGitHub = new GUIContent("", "Github");
            GUIStyle styleGitHub = new GUIStyle(GUI.skin.box);
            if (_iconGitHub != null)
            {
                buttonGitHub = new GUIContent(_iconGitHub, "Github");
                styleGitHub = GUIStyle.none;
            }

            if (GUILayout.Button(buttonGitHub, styleGitHub, GUILayout.Width(iconSize), GUILayout.Height(iconSize)))
            {
                Application.OpenURL("https://github.com/Nestorboy?tab=repositories");
            }

            GUILayout.EndHorizontal();
        }

        private void DrawSaveStateUtilities()
        {
            GUILayout.BeginVertical(styleHelpBox);

            EditorGUILayout.LabelField("Save State Utilities", EditorStyles.boldLabel);

            GUILayout.BeginVertical(styleBox);

            GUILayout.BeginHorizontal();

            GUILayout.Label(new GUIContent(contentAssetFolder.text + labelAsteriskFolder, contentAssetFolder.tooltip), styleRichTextLabel, GUILayout.Width(EditorGUIUtility.labelWidth));

            EditorGUI.BeginChangeCheck();
            DefaultAsset newFolder = (DefaultAsset)EditorGUILayout.ObjectField(assetFolderSelected, typeof(DefaultAsset), true);
            if (EditorGUI.EndChangeCheck())
            {
                if (newFolder == null || System.IO.Directory.Exists(AssetDatabase.GetAssetPath(newFolder))) // Simple fix to prevent non-folders from being selected.
                {
                    assetFolderSelected = newFolder;
                    assetFolderPath = AssetDatabase.GetAssetPath(assetFolderSelected);
                    assetFolderIndex = ArrayUtility.IndexOf(assetFolders, assetFolderSelected);
                }
            }

            EditorGUI.BeginChangeCheck();
            assetFolderIndex = EditorGUILayout.Popup(assetFolderIndex, assetFolderNames);
            if (EditorGUI.EndChangeCheck())
            {
                assetFolderSelected = assetFolders[assetFolderIndex];
                assetFolderPath = AssetDatabase.GetAssetPath(assetFolders[assetFolderIndex]);
            }

            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();

            GUILayout.Label(new GUIContent(contentEncryptionSeed.text + labelAsteriskSeed, contentEncryptionSeed.tooltip), styleRichTextLabel, GUILayout.Width(EditorGUIUtility.labelWidth));

            EditorGUI.BeginChangeCheck();
            saveStateSeed = EditorGUILayout.TextField(saveStateSeed);
            if (EditorGUI.EndChangeCheck())
            {
                saveStateHash = GetStableHashCode(saveStateSeed);
            }

            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();

            GUILayout.Label(new GUIContent(contentParameterName.text + labelAsteriskName, contentParameterName.tooltip), styleRichTextLabel, GUILayout.Width(EditorGUIUtility.labelWidth));

            EditorGUI.BeginChangeCheck();
            saveStateParameterName = EditorGUILayout.TextField(saveStateParameterName);
            if (EditorGUI.EndChangeCheck())
            {

            }

            GUILayout.EndHorizontal();

            EditorGUILayout.Space();

            EditorGUI.BeginDisabledGroup(assetFolderSelected == null);
            EditorGUI.BeginDisabledGroup(saveStateParameterName.Length < 1);
            if (GUILayout.Button(new GUIContent(contentWorldAssets.text + labelAsteriskFolder + labelAsteriskName, contentWorldAssets.tooltip), styleRichTextButton))
            {
                if (EditorUtility.DisplayDialog("SaveState", $"Are you sure you want to generate and replace world assets in {assetFolderSelected.name}?", "Yes", "No"))
                {
                    System.Diagnostics.Stopwatch timer = new System.Diagnostics.Stopwatch();
                    timer.Start();

                    try
                    {
                        AssetDatabase.StartAssetEditing();

                        PrepareWorldAnimators();

                        Debug.Log($"[<color=#00FF9F>NUSaveState</color>] World asset creation took: {timer.Elapsed:mm\\:ss\\.fff}");
                    }
                    finally
                    {
                        timer.Stop();

                        AssetDatabase.StopAssetEditing();

                        AssetDatabase.SaveAssets();
                    }
                }
            }

            EditorGUI.BeginDisabledGroup(saveStateSeed.Length < 1);
            if (GUILayout.Button(new GUIContent(contentAvatarAssets.text + labelAsteriskFolder + labelAsteriskSeed + labelAsteriskName, contentAvatarAssets.tooltip), styleRichTextButton))
            {
                if (EditorUtility.DisplayDialog("SaveState", $"Are you sure you want to generate and replace avatar assets in {assetFolderSelected.name}?", "Yes", "No"))
                {
                    string pathTemplatePrefab = $"{pathSaveState}/Avatar/Template/SaveState-Avatar-Template";
                    if (System.IO.File.Exists(pathTemplatePrefab))
                    {
                        Debug.LogError($"[<color=#00FF9F>NUSaveState</color>] Could not find SaveState-Avatar-Template at {pathTemplatePrefab}");

                        return;
                    }

                    System.Diagnostics.Stopwatch timer = new System.Diagnostics.Stopwatch();
                    timer.Start();

                    try
                    {
                        AssetDatabase.StartAssetEditing();

                        // Prepare the assets.
                        List<string> assetPaths = new List<string>();
                        assetPaths.AddRange(PrepareAvatarAnimators(out AnimatorController[] controllers));
                        assetPaths.AddRange(PrepareAvatarMenus(out VRCExpressionsMenu menu, out VRCExpressionParameters parameters));
                        assetPaths.AddRange(PrepareAvatarPrefabs(menu, parameters, controllers));

                        // Filter out dependencies outside of the NUSaveState folder.
                        List<string> packageAssetPaths = new List<string>();
                        foreach (string assetPath in assetPaths)
                            foreach (string pathDependency in AssetDatabase.GetDependencies(assetPath, true))
                                if (pathDependency.StartsWith(pathSaveState))
                                    packageAssetPaths.Add(pathDependency);

                        // Create UnityPackage.
                        string pathUnityPackage = $"{assetFolderPath}/SaveState-Avatar_{saveStateParameterName}.unitypackage";
                        AssetDatabase.ExportPackage(packageAssetPaths.ToArray(), pathUnityPackage, ExportPackageOptions.Default);
                        AssetDatabase.ImportAsset(pathUnityPackage);

                        Debug.Log($"[<color=#00FF9F>NUSaveState</color>] Avatar asset creation took: {timer.Elapsed:mm\\:ss\\.fff}");
                    }
                    finally
                    {
                        timer.Stop();

                        AssetDatabase.StopAssetEditing();

                        AssetDatabase.SaveAssets();
                    }
                }
            }
            EditorGUI.EndDisabledGroup();
            EditorGUI.EndDisabledGroup();

            if (GUILayout.Button(new GUIContent(contentApplyAnimators.text + labelAsteriskFolder, contentApplyAnimators.tooltip), styleRichTextButton))
            {
                if (EditorUtility.DisplayDialog("SaveState", $"Are you sure you want to apply the animator controllers from {assetFolderSelected.name}?", "Yes", "No"))
                    SetSaveStateAnimators();
            }
            EditorGUI.EndDisabledGroup();

            EditorGUI.BeginDisabledGroup(saveStateSeed.Length < 1);
            if (GUILayout.Button(new GUIContent(contentApplyKeys.text + labelAsteriskSeed, contentApplyKeys.tooltip), styleRichTextButton))
            {
                if (EditorUtility.DisplayDialog("SaveState", $"Are you sure you want to apply keys generated from \"{saveStateSeed}\"?", "Yes", "No"))
                    ApplyEncryptionKeys();
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.EndVertical();

            EditorGUILayout.EndVertical();
        }

        private void DrawSaveStateData()
        {
            // Fallback avatar, Variable count, Instructions (Udon & Variable) Scroll, Data Avatar IDs Scroll

            GUILayout.BeginVertical(styleHelpBox);

            EditorGUILayout.LabelField("Save State Data", EditorStyles.boldLabel);

            GUILayout.BeginVertical(styleBox);

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(propertyEventReciever, contentEventReciever);
            EditorGUILayout.PropertyField(propertyFallbackAvatar, contentFallbackAvatar);
            if (EditorGUI.EndChangeCheck())
                serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();

            if (instructionList.serializedProperty.isExpanded != instructionList.draggable)
            {
                instructionList.draggable = instructionList.serializedProperty.isExpanded;
                instructionList.footerHeight = instructionList.serializedProperty.isExpanded ? 20 : 0;
                instructionList.displayAdd = instructionList.serializedProperty.isExpanded;
                instructionList.displayRemove = instructionList.serializedProperty.isExpanded;
            }
            instructionList.DoLayoutList();

            EditorGUILayout.Space();

            if (avatarList.serializedProperty.isExpanded != avatarList.draggable)
            {
                avatarList.draggable = avatarList.serializedProperty.isExpanded;
                avatarList.footerHeight = avatarList.serializedProperty.isExpanded ? 20 : 0;
                avatarList.displayRemove = avatarList.serializedProperty.isExpanded;
            }
            avatarList.DoLayoutList();

            GUILayout.EndVertical();

            GUILayout.EndVertical();
        }

        #endregion Drawers

        #region SaveState Methods

        private void GetSaveStateData()
        {
            int udonSize = propertyBufferUdonBehaviours.arraySize;
            int nameSize = propertyBufferVariables.arraySize;
            int typeSize = propertyBufferTypes.arraySize;
            int instructionSize = Math.Max(Math.Max(udonSize, nameSize), Math.Max(typeSize, 0));

            DataInstruction[] newDataInstructions = new DataInstruction[instructionSize];

            if (udonSize < instructionSize)
                propertyBufferUdonBehaviours.arraySize = instructionSize;
            if (nameSize < instructionSize)
                propertyBufferVariables.arraySize = instructionSize;
            if (typeSize < instructionSize)
                propertyBufferTypes.arraySize = instructionSize;

            serializedObject.ApplyModifiedProperties();
            _behaviourProxy.ApplyProxyModifications();

            for (int i = 0; i < instructionSize; i++)
            {
                UdonBehaviour oldUdonBehaviour = (UdonBehaviour)propertyBufferUdonBehaviours.GetArrayElementAtIndex(i).objectReferenceValue;
                string oldVariableName = propertyBufferVariables.GetArrayElementAtIndex(i).stringValue;
                string oldTypeName = propertyBufferTypes.GetArrayElementAtIndex(i).stringValue;

                GetValidVariables(oldUdonBehaviour, out List<string> vars, out List<Type> types);

                newDataInstructions[i] = new DataInstruction();

                newDataInstructions[i].Udon = oldUdonBehaviour;

                newDataInstructions[i].VariableNames = vars.ToArray();
                newDataInstructions[i].VariableTypes = types.ToArray();
                newDataInstructions[i].PrepareLabels();

                int newVariableIndex = Array.IndexOf(newDataInstructions[i].VariableNames, oldVariableName);
                if (newVariableIndex >= 0)
                    newDataInstructions[i].VariableIndex = newDataInstructions[i].VariableTypes[newVariableIndex].FullName == oldTypeName ? newVariableIndex : -1;
            }

            dataInstructions = newDataInstructions;

            dataBitCount = CalculateBitCount(dataInstructions);
        }

        private void SetSaveStateData()
        {
            Undo.RecordObject(_behaviourProxy, "Apply SaveState data");

            propertyBufferByteCount.intValue = Mathf.CeilToInt(CalculateBitCount(dataInstructions) / 8);

            for (int i = 0; i < dataInstructions.Length; i++)
            {
                int variableIndex = dataInstructions[i].VariableIndex;

                UdonBehaviour newUdonBehaviour = dataInstructions[i].Udon;
                string newVariableName = variableIndex < 0 ? "" : dataInstructions[i].VariableNames[dataInstructions[i].VariableIndex];
                string newTypeName = variableIndex < 0 ? null : dataInstructions[i].VariableTypes[dataInstructions[i].VariableIndex].FullName;

                propertyBufferUdonBehaviours.GetArrayElementAtIndex(i).objectReferenceValue = newUdonBehaviour;
                propertyBufferVariables.GetArrayElementAtIndex(i).stringValue = newVariableName;
                propertyBufferTypes.GetArrayElementAtIndex(i).stringValue = newTypeName;
            }

            serializedObject.ApplyModifiedProperties();
            _behaviourProxy.ApplyProxyModifications();
        }
        private void SetSaveStateData(int index)
        {
            Undo.RecordObject(_behaviourProxy, "Apply SaveState data");

            propertyBufferByteCount.intValue = Mathf.CeilToInt(CalculateBitCount(dataInstructions) / 8);

            int variableIndex = dataInstructions[index].VariableIndex;

            UdonBehaviour newUdonBehaviour = dataInstructions[index].Udon;
            string newVariableName = variableIndex < 0 ? "" : dataInstructions[index].VariableNames[dataInstructions[index].VariableIndex];
            string newTypeName = variableIndex < 0 ? null : dataInstructions[index].VariableTypes[dataInstructions[index].VariableIndex].FullName;

            propertyBufferUdonBehaviours.GetArrayElementAtIndex(index).objectReferenceValue = newUdonBehaviour;
            propertyBufferVariables.GetArrayElementAtIndex(index).stringValue = newVariableName;
            propertyBufferTypes.GetArrayElementAtIndex(index).stringValue = newTypeName;

            serializedObject.ApplyModifiedProperties();
            _behaviourProxy.ApplyProxyModifications();
        }

        private int CalculateBitCount(DataInstruction[] instructions)
        {
            int bits = 0;

            foreach (DataInstruction instruction in instructions)
                bits += instruction.VariableBits;

            return bits;
        }

        private void GetValidVariables(UdonBehaviour udon, out List<string> vars, out List<Type> types)
        {
            vars = new List<string>();
            types = new List<Type>();
            if (udon == null) return;

            VRC.Udon.Common.Interfaces.IUdonSymbolTable symbolTable = udon.programSource.SerializedProgramAsset.RetrieveProgram().SymbolTable;

            List<string> programVariablesNames = symbolTable.GetSymbols().ToList();
            List<KeyValuePair<string, Type>> toSort = new List<KeyValuePair<string, Type>>();

            foreach (string variableName in programVariablesNames)
            {
                if (variableName.StartsWith("__")) continue;

                Type variableType = symbolTable.GetSymbolType(variableName);
                int typeIndex = Array.IndexOf(convertableTypes, variableType);
                if (typeIndex > -1)
                {
                    toSort.Add(new KeyValuePair<string, Type>(variableName, variableType));
                }
            }

            List<KeyValuePair<string, Type>> sorted = toSort.OrderBy(kvp => kvp.Key).ToList();

            foreach (KeyValuePair<string, Type> item in sorted)
            {
                vars.Add(item.Key);
                types.Add(item.Value);
            }
        }

        private void SetSaveStateAnimators()
        {
            int minBitCount = Math.Min(dataBitCount, 256);

            string[] clearerControllerGUIDs = AssetDatabase.FindAssets("t:AnimatorController l:SaveState-Clearer", new string[] { assetFolderPath });
            string[] writerControllerGUIDs = AssetDatabase.FindAssets("t:AnimatorController l:SaveState-Writer", new string[] { assetFolderPath });

            if (clearerControllerGUIDs.Length < 1 || writerControllerGUIDs.Length < minBitCount)
            {
                if (clearerControllerGUIDs.Length < 1 && writerControllerGUIDs.Length < minBitCount)
                    Debug.LogWarning("[<color=#00FF9F>SaveState</color>] Couldn't find parameter Clearer or enough parameter Writers.", assetFolderSelected);
                else if (clearerControllerGUIDs.Length < 1)
                    Debug.LogWarning("[<color=#00FF9F>SaveState</color>] Couldn't find parameter Clearer.", assetFolderSelected);
                else if (writerControllerGUIDs.Length < minBitCount)
                    Debug.LogWarning("[<color=#00FF9F>SaveState</color>] Couldn't find enough parameter Writers.", assetFolderSelected);
                return;
            }

            AnimatorController[] clearingController = new AnimatorController[] { (AnimatorController)AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(clearerControllerGUIDs[0])) };
            AnimatorController[] writingControllers = new AnimatorController[minBitCount];

            for (int controllerIndex = 0; controllerIndex < writingControllers.Length; controllerIndex++)
            {
                writingControllers[controllerIndex] = (AnimatorController)AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(writerControllerGUIDs[controllerIndex]));
            }

            Undo.RecordObject(_behaviourProxy, "Apply animator controllers");

            propertyParameterClearer.arraySize = 1;
            propertyParameterClearer.GetArrayElementAtIndex(0).objectReferenceValue = clearingController[0];

            propertyParameterWriters.arraySize = minBitCount;
            for (int i = 0; i < minBitCount; i++)
                propertyParameterWriters.GetArrayElementAtIndex(i).objectReferenceValue = writingControllers[i];

            serializedObject.ApplyModifiedProperties();
            _behaviourProxy.ApplyProxyModifications();
        }

        private void ApplyEncryptionKeys()
        {
            int avatarCount = Mathf.CeilToInt(dataBitCount / 256f);

            Vector3[] keyCoordinates = new Vector3[avatarCount];

            UnityEngine.Random.InitState(saveStateHash);
            for (int i = 0; i < keyCoordinates.Length; i++)
            {
                keyCoordinates[i] = RandomInsideUnitCube() * 50;

                for (int j = 0; j < i; j++)
                {
                    Vector3 vec = keyCoordinates[j] - keyCoordinates[i];
                    if (Mathf.Abs(vec.x) < 1 && Mathf.Abs(vec.y) < 2 && Mathf.Abs(vec.z) < 1)
                    {
                        i--;
                        break;
                    }
                }
            }
            UnityEngine.Random.InitState((int)DateTime.Now.Ticks);

            Undo.RecordObject(_behaviourProxy, "Apply encryption keys");

            propertyKeyCoords.arraySize = avatarCount;
            for (int i = 0; i < avatarCount; i++)
                propertyKeyCoords.GetArrayElementAtIndex(i).vector3Value = keyCoordinates[i];

            serializedObject.ApplyModifiedProperties();
            _behaviourProxy.ApplyProxyModifications();
        }

        private void PrepareWorldAnimators()
        {
            // Prepare AnimatorController used for clearing data.
            AnimatorController newClearerController = AnimatorController.CreateAnimatorControllerAtPath($"{assetFolderPath}/SaveState-{saveStateParameterName}-clear.controller");
            AnimatorStateMachine newClearerStateMachine = newClearerController.layers[0].stateMachine;
            newClearerStateMachine.entryPosition = new Vector2(-30, 0);
            newClearerStateMachine.anyStatePosition = new Vector2(-30, 50);
            newClearerStateMachine.exitPosition = new Vector2(-30, 100);

            // Prepare AnimatorState used for clearing data.
            AnimatorState newClearerState = newClearerStateMachine.AddState("Write", new Vector3(200, 0));

            // Prepare VRC Behaviour used for clearing data.
            var newClearerVRCParameterDriver = newClearerState.AddStateMachineBehaviour<VRCAvatarParameterDriver>();

            var newClearerParameters = new List<VRC_AvatarParameterDriver.Parameter>();
            for (int parameterIndex = 0; parameterIndex < 16; parameterIndex++)
            {
                var newParameter = new VRC_AvatarParameterDriver.Parameter()
                {
                    name = $"{saveStateParameterName}_{parameterIndex}",
                    value = 0,
                    type = VRC_AvatarParameterDriver.ChangeType.Set
                };
                newClearerParameters.Add(newParameter);
            }

            newClearerVRCParameterDriver.parameters = newClearerParameters;

            AssetDatabase.SetLabels(newClearerController, new string[] { "SaveState-Clearer" });

            for (int controllerIndex = 0; controllerIndex < 256; controllerIndex++)
            {
                EditorUtility.DisplayProgressBar("NUSaveState", $"Preparing Animator Controllers... ({controllerIndex}/{256})", (float)controllerIndex / 256);

                // Prepare AnimatorController.
                AnimatorController newWriterController = AnimatorController.CreateAnimatorControllerAtPath($"{assetFolderPath}/SaveState-{saveStateParameterName}_{controllerIndex / 16}-bit_{controllerIndex % 16}.controller");
                AnimatorStateMachine newWriterStateMachine = newWriterController.layers[0].stateMachine;
                newWriterStateMachine.entryPosition = new Vector2(-30, 0);
                newWriterStateMachine.anyStatePosition = new Vector2(-30, 50);
                newWriterStateMachine.exitPosition = new Vector2(-30, 100);

                // Prepare AnimatorState.
                AnimatorState newWriterState = newWriterStateMachine.AddState("Write", new Vector3(200, 0));

                // Prepare VRC Behaviour.
                var VRCParameterDriver = newWriterState.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
                VRCParameterDriver.parameters = new List<VRC_AvatarParameterDriver.Parameter>()
                {
                    new VRC_AvatarParameterDriver.Parameter()
                    {
                        name = $"{saveStateParameterName}_{controllerIndex / 16}",
                        value = 1 / Mathf.Pow(2, 16 - controllerIndex % 16),
                        type = VRC_AvatarParameterDriver.ChangeType.Add
                    }
                };

                AssetDatabase.SetLabels(newWriterController, new string[] { "SaveState-Writer" });
            }

            EditorUtility.ClearProgressBar();
        }

        private List<string> PrepareAvatarAnimators(out AnimatorController[] controllers)
        {
            string pathAnimatorsFolder = $"{pathSaveState}/Avatar/Animators";
            ReadyPath(pathAnimatorsFolder);

            List<string> assetPaths = new List<string>();

            int avatarCount = Mathf.CeilToInt(dataBitCount / 256f);
            int byteCount = Mathf.CeilToInt(dataBitCount / 8f);

            controllers = new AnimatorController[avatarCount];

            // Prepare keys.
            Vector3[] keyCoordinates = new Vector3[avatarCount];

            UnityEngine.Random.InitState(saveStateHash);

            for (int i = 0; i < keyCoordinates.Length; i++)
            {
                keyCoordinates[i] = RandomInsideUnitCube() * 50;

                for (int j = 0; j < i; j++)
                {
                    Vector3 vec = keyCoordinates[j] - keyCoordinates[i];
                    if (Mathf.Abs(vec.x) < 1 && Mathf.Abs(vec.y) < 2 && Mathf.Abs(vec.z) < 1)
                    {
                        i--;
                        break;
                    }
                }
            }

            // Create animator for each avatar.
            for (int avatarIndex = 0; avatarIndex < avatarCount; avatarIndex++)
            {
                EditorUtility.DisplayProgressBar("NUSaveState", $"Preparing Animator Controllers... ({avatarIndex}/{avatarCount})", (float)avatarIndex / avatarCount);

                // Prepare AnimatorController.
                string newControllerPath = $"{pathAnimatorsFolder}/SaveState-Avatar_{avatarIndex}-{saveStateParameterName}.controller";
                assetPaths.Add(newControllerPath);

                controllers[avatarIndex] = AnimatorController.CreateAnimatorControllerAtPath(newControllerPath);
                AnimatorStateMachine newStateMachine = controllers[avatarIndex].layers[0].stateMachine;
                newStateMachine.entryPosition = new Vector2(-30, 0);
                newStateMachine.anyStatePosition = new Vector2(-30, 50);
                newStateMachine.exitPosition = new Vector2(-30, 100);

                // Prepare default animation.
                AnimationClip newDefaultClip = new AnimationClip() { name = "Default" };
                newDefaultClip.SetCurve("", typeof(Animator), "RootT.y", AnimationCurve.Constant(0, 0, 1));

                AssetDatabase.AddObjectToAsset(newDefaultClip, controllers[avatarIndex]);

                // Prepare default state an animation.
                controllers[avatarIndex].AddParameter(new AnimatorControllerParameter() { name = "IsLocal", type = AnimatorControllerParameterType.Bool, defaultBool = false });
                AnimatorState newDefaultState = newStateMachine.AddState("Default", new Vector3(200, 0));
                newDefaultState.motion = newDefaultClip;

                // Prepare data BlendTree state.
                AnimatorState newBlendState = controllers[avatarIndex].CreateBlendTreeInController("Data Blend", out BlendTree newTree, 0);
                ChildAnimatorState[] newChildStates = newStateMachine.states;
                newChildStates[1].position = new Vector2(200, 50);
                newStateMachine.states = newChildStates;

                controllers[avatarIndex].RemoveParameter(1); // Get rid of 'Blend' parameter.

                AnimatorStateTransition newBlendTransition = newStateMachine.AddAnyStateTransition(newBlendState);
                newBlendTransition.exitTime = 1;
                newBlendTransition.duration = 0;
                newBlendTransition.AddCondition(AnimatorConditionMode.If, 1, "IsLocal");

                newTree.blendType = BlendTreeType.Direct;

                // Prepare VRC Behaviours.
                var VRCLayerControl = newBlendState.AddStateMachineBehaviour<VRCPlayableLayerControl>();
                var VRCTrackingControl = newBlendState.AddStateMachineBehaviour<VRCAnimatorTrackingControl>();

                VRCLayerControl.goalWeight = 1;

                VRCTrackingControl.trackingLeftFingers = VRC_AnimatorTrackingControl.TrackingType.Animation;
                VRCTrackingControl.trackingRightFingers = VRC_AnimatorTrackingControl.TrackingType.Animation;

                // Prepare base BlendTree animation.
                AnimationClip newBaseClip = new AnimationClip() { name = "SaveState-Base" };

                newBaseClip.SetCurve("", typeof(Animator), "RootT.y", AnimationCurve.Constant(0, 0, 1));
                newBaseClip.SetCurve("SaveState-Key", typeof(GameObject), "m_IsActive", AnimationCurve.Constant(0, 0, 1));

                newBaseClip.SetCurve("SaveState-Key", typeof(Transform), "m_LocalPosition.x", AnimationCurve.Constant(0, 0, keyCoordinates[avatarIndex].x));
                newBaseClip.SetCurve("SaveState-Key", typeof(Transform), "m_LocalPosition.y", AnimationCurve.Constant(0, 0, keyCoordinates[avatarIndex].y));
                newBaseClip.SetCurve("SaveState-Key", typeof(Transform), "m_LocalPosition.z", AnimationCurve.Constant(0, 0, keyCoordinates[avatarIndex].z));
                for (int i = 0; i < muscleNames.Length; i++)
                {
                    newBaseClip.SetCurve("", typeof(Animator), $"{muscleNames[i]}2 Stretched", AnimationCurve.Constant(0, 0, 0.81002f));
                    newBaseClip.SetCurve("", typeof(Animator), $"{muscleNames[i]}3 Stretched", AnimationCurve.Constant(0, 0, 0.81002f));
                }
                newTree.AddChild(newBaseClip);

                AssetDatabase.AddObjectToAsset(newBaseClip, controllers[avatarIndex]);

                // Prepare data BlendTree animations.
                for (int byteIndex = 0; byteIndex < Math.Min(byteCount, 16); byteIndex++)
                {
                    AnimationClip newClip = new AnimationClip() { name = $"SaveState-{saveStateParameterName}_{byteIndex}.anim" };

                    newClip.SetCurve("", typeof(Animator), $"{muscleNames[byteIndex % muscleNames.Length]}{3 - byteIndex / muscleNames.Length} Stretched", AnimationCurve.Constant(0, 0, 1));
                    newTree.AddChild(newClip);

                    AssetDatabase.AddObjectToAsset(newClip, controllers[avatarIndex]);
                }

                // Prepare BlendTree parameters.
                ChildMotion[] newChildren = newTree.children;

                controllers[avatarIndex].AddParameter(new AnimatorControllerParameter() { name = "Base", type = AnimatorControllerParameterType.Float, defaultFloat = 1 });
                newChildren[0].directBlendParameter = "Base";

                for (int childIndex = 1; childIndex < newChildren.Length; childIndex++)
                {
                    string newParameter = $"{saveStateParameterName}_{childIndex - 1}";

                    controllers[avatarIndex].AddParameter(newParameter, AnimatorControllerParameterType.Float);
                    newChildren[childIndex].directBlendParameter = newParameter;
                }

                newTree.children = newChildren;
            }

            EditorUtility.ClearProgressBar();

            return assetPaths;
        }

        private List<string> PrepareAvatarMenus(out VRCExpressionsMenu menu, out VRCExpressionParameters parameters)
        {
            string pathExpressionsFolder = $"{pathSaveState}/Avatar/Expressions";
            ReadyPath(pathExpressionsFolder);

            List<string> assetPaths = new List<string>();

            int byteCount = Mathf.CeilToInt(dataBitCount / 8f);

            // Prepare ExpressionMenu.
            menu = CreateInstance<VRCExpressionsMenu>();
            menu.controls.Add(new VRCExpressionsMenu.Control()
            {
                name = "<font=LiberationMono SDF><color=#00FF9F><size=140%><b>Nessie's Udon Save <voffset=15em>State"
            });

            string newMenuPath = $"{pathExpressionsFolder}/SaveState-Menu-{saveStateParameterName}.asset";
            assetPaths.Add(newMenuPath);
            AssetDatabase.CreateAsset(menu, newMenuPath);

            // Prepare ExpressionParameter.
            parameters = CreateInstance<VRCExpressionParameters>();

            VRCExpressionParameters.Parameter[] expressionControls = new VRCExpressionParameters.Parameter[Math.Min(byteCount, 16)];
            for (int i = 0; i < expressionControls.Length; i++)
            {
                expressionControls[i] = new VRCExpressionParameters.Parameter()
                {
                    name = $"{saveStateParameterName}_{i}",
                    valueType = VRCExpressionParameters.ValueType.Float
                };
            }
            parameters.parameters = expressionControls;

            string newParametersPath = $"{pathExpressionsFolder}/SaveState-Expression-{saveStateParameterName}.asset";
            assetPaths.Add(newParametersPath);
            AssetDatabase.CreateAsset(parameters, newParametersPath);

            return assetPaths;
        }

        private List<string> PrepareAvatarPrefabs(VRCExpressionsMenu menu, VRCExpressionParameters parameters, AnimatorController[] controllers)
        {
            string pathPrefabsFolder = $"{pathSaveState}/Avatar/Prefabs";
            ReadyPath(pathPrefabsFolder);

            List<string> assetPaths = new List<string>();

            int avatarCount = Mathf.CeilToInt(dataBitCount / 256f);

            GameObject templatePrefab = PrefabUtility.LoadPrefabContents($"{pathSaveState}/Avatar/Template/SaveState-Avatar-Template.prefab");
            for (int avatarIndex = 0; avatarIndex < avatarCount; avatarIndex++)
            {
                string newPrefabPath = $"{pathPrefabsFolder}/SaveState-Avatar_{avatarIndex}-{saveStateParameterName}.prefab";
                assetPaths.Add(newPrefabPath);

                VRCAvatarDescriptor newAvatarDescriptor = templatePrefab.GetComponent<VRCAvatarDescriptor>();

                newAvatarDescriptor.expressionsMenu = menu;
                newAvatarDescriptor.expressionParameters = parameters;

                VRCAvatarDescriptor.CustomAnimLayer[] baseLayers = newAvatarDescriptor.baseAnimationLayers;
                baseLayers[3].animatorController = controllers[avatarIndex];
                baseLayers[4].animatorController = controllers[avatarIndex];

                VRCAvatarDescriptor.CustomAnimLayer[] specialLayers = newAvatarDescriptor.specialAnimationLayers;
                specialLayers[1].animatorController = controllers[avatarIndex];

                PrefabUtility.SaveAsPrefabAsset(templatePrefab, newPrefabPath);
            }
            PrefabUtility.UnloadPrefabContents(templatePrefab);

            return assetPaths;
        }

        #endregion SaveState Methods

        #region Resources

        private void InitializeStyles()
        {
            // EditorGUI
            styleHelpBox = new GUIStyle(EditorStyles.helpBox);
            styleHelpBox.padding = new RectOffset(0, 0, styleHelpBox.padding.top, styleHelpBox.padding.bottom + 3);

            // GUI
            styleBox = new GUIStyle(GUI.skin.box);
            styleBox.padding = new RectOffset(GUI.skin.box.padding.left * 2, GUI.skin.box.padding.right * 2, GUI.skin.box.padding.top * 2, GUI.skin.box.padding.bottom * 2);
            styleBox.margin = new RectOffset(0, 0, 4, 4);

            styleRichTextLabel = new GUIStyle(GUI.skin.label);
            styleRichTextLabel.richText = true;

            styleRichTextButton = new GUIStyle(GUI.skin.button);
            styleRichTextButton.richText = true;

            // Content
            if (EditorGUIUtility.isProSkin)
            {
                labelAsteriskFolder = assetFolderSelected == null ? "<color=#FC6D3F>*</color>" : "";
                labelAsteriskSeed = saveStateSeed.Length < 1 ? "<color=#B0FC58>*</color>" : "";
                labelAsteriskName = saveStateParameterName.Length < 1 ? "<color=#7ED5FC>*</color>" : "";
            }
            else
            {
                labelAsteriskFolder = assetFolderSelected == null ? "<color=#AF0C0C>*</color>" : "";
                labelAsteriskSeed = saveStateSeed.Length < 1 ? "<color=#2D7C31>*</color>" : "";
                labelAsteriskName = saveStateParameterName.Length < 1 ? "<color=#0C6BC9>*</color>" : "";
            }
        }

        private void InitializeProperties()
        {
            propertyEventReciever = serializedObject.FindProperty(nameof(NUSaveState.CallbackReciever));
            propertyFallbackAvatar = serializedObject.FindProperty(nameof(NUSaveState.FallbackAvatarID));

            propertyBufferByteCount = serializedObject.FindProperty("bufferByteCount");
            propertyBufferUdonBehaviours = serializedObject.FindProperty("bufferUdonBehaviours");
            propertyBufferVariables = serializedObject.FindProperty("bufferVariables");
            propertyBufferTypes = serializedObject.FindProperty("bufferTypes");

            propertyAvatarIDs = serializedObject.FindProperty("dataAvatarIDs");
            propertyKeyCoords = serializedObject.FindProperty("dataKeyCoords");

            propertyParameterClearer = serializedObject.FindProperty("parameterClearer");
            propertyParameterWriters = serializedObject.FindProperty("parameterWriters");
        }

        private void GetUIAssets()
        {
            _iconVRChat = Resources.Load<Texture2D>("Icons/VRChat-Emblem-32px");
            _iconGitHub = Resources.Load<Texture2D>("Icons/GitHub-Mark-32px");
        }

        private void GetAssetFolders()
        {
            string pathAssetsFolder = $"{pathSaveState}/AssetFolders";
            ReadyPath(pathAssetsFolder);

            assetFolderPaths = AssetDatabase.GetSubFolders(pathAssetsFolder);
            assetFolderNames = new string[assetFolderPaths.Length];
            assetFolders = new DefaultAsset[assetFolderPaths.Length];
            for (int i = 0; i < assetFolders.Length; i++)
            {
                assetFolders[i] = (DefaultAsset)AssetDatabase.LoadMainAssetAtPath(assetFolderPaths[i]);
                assetFolderNames[i] = assetFolders[i].name;
            }
        }

        private static void ReadyPath(string folderPath)
        {
            if (!System.IO.Directory.Exists(folderPath))
                System.IO.Directory.CreateDirectory(folderPath);
        }

        #endregion Resources

        #region Hashing

        // Lazily implemented hash function from: https://stackoverflow.com/a/36845864
        private int GetStableHashCode(string str)
        {
            unchecked
            {
                int hash1 = 5381;
                int hash2 = hash1;

                for (int i = 0; i < str.Length && str[i] != '\0'; i += 2)
                {
                    hash1 = ((hash1 << 5) + hash1) ^ str[i];
                    if (i == str.Length - 1 || str[i + 1] == '\0')
                        break;
                    hash2 = ((hash2 << 5) + hash2) ^ str[i + 1];
                }

                return hash1 + (hash2 * 1566083941);
            }
        }

        private Vector3 RandomInsideUnitCube()
        {
            return new Vector3(UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-1f, 1f));
        }

        #endregion Hashing
    }
}