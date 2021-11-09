
using System;
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

        private NUSaveState behaviourProxy;

        private NUSaveStateData data;
        private SerializedObject dataSO;

        // Assets.
        private readonly string pathSaveState = "Assets/Nessie/Udon/NUSaveState";

        private string[] assetFolderPaths;
        private string[] assetFolderNames;
        private DefaultAsset[] assetFolders;

        private readonly string[] muscleNames = new string[]
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

        // Save State data.
        private int dataBitCount;

        // UI.
        private bool foldoutDefaultFields;

        private UnityEditorInternal.ReorderableList instructionList;
        private int selectedInstructionIndex;
        private UnityEditorInternal.ReorderableList avatarList;

        private SerializedProperty propertyEncryptionSeed;
        private SerializedProperty propertyParameterName;

        private SerializedProperty propertyEventReciever;
        private SerializedProperty propertyFallbackAvatar;

        private SerializedProperty propertyByteCount;
        private SerializedProperty propertyUdonBehaviours;
        private SerializedProperty propertyVariables;
        private SerializedProperty propertyTypes;

        private SerializedProperty propertyAvatarIDs;
        private SerializedProperty propertyKeyCoords;

        private SerializedProperty propertyParameterClearer;
        private SerializedProperty propertyParameterWriters;

        private Texture2D iconGitHub;
        private Texture2D iconVRChat;

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

        private GUIContent contentDefault = new GUIContent("Default Inspector", "Foldout for default UdonSharpBehaviour inspector.");
        private GUIContent contentData = new GUIContent("NUSS Data", "EditorOnly MonoBehaviour containing the NUSaveState data.");
        private GUIContent contentDataToggle = new GUIContent("Show NUSS Data object", "Toggle the visibility of the NUSaveState data object.");

        #endregion Private Fields

        #region Editor Events

        private void OnEnable()
        {
            if (target == null) return; // Prevents some iffy errors in the console.

            behaviourProxy = (NUSaveState)target;
            if (!UdonSharpEditorUtility.IsProxyBehaviour(behaviourProxy)) return;

            GetUIAssets();

            GetAssetFolders();

            data = NUSaveStateData.GetPreferences(behaviourProxy) ?? NUSaveStateData.CreatePreferences(behaviourProxy);
            if (data.DataPreferences.Folder == null || System.IO.Directory.Exists(AssetDatabase.GetAssetPath(data.DataPreferences.Folder)))
                data.DataPreferences.FolderIndex = ArrayUtility.IndexOf(assetFolders, data.DataPreferences.Folder);

            dataSO = new SerializedObject(data);
            dataSO.Update();

            InitializeProperties();

            GetSaveStateData();
            SetSaveStateData();

            #region Reorderable Lists

            instructionList = new UnityEditorInternal.ReorderableList(serializedObject, propertyUdonBehaviours, true, true, true, true);
            instructionList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
            {
                if (!instructionList.serializedProperty.isExpanded) return;

                Rect udonFieldRect = new Rect(rect.x, rect.y + 1.5f, (rect.width - 2) / 2, EditorGUIUtility.singleLineHeight);
                Rect variableFieldRect = new Rect(rect.x + udonFieldRect.width + 4, rect.y + 1.5f, (rect.width - 2) / 2, EditorGUIUtility.singleLineHeight);

                EditorGUI.BeginChangeCheck();
                UdonBehaviour newUdon = (UdonBehaviour)EditorGUI.ObjectField(udonFieldRect, data.DataInstructions[index].Udon, typeof(UdonBehaviour), true);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(data, "Changed Instructions Udon Behaviour");

                    data.DataInstructions[index].Udon = newUdon;

                    dataBitCount = NUSaveStateData.BitSum(data.DataInstructions);

                    int avatarCount = Mathf.CeilToInt(dataBitCount / 256f);
                    if (avatarCount > propertyAvatarIDs.arraySize)
                    {
                        propertyAvatarIDs.arraySize = avatarCount;
                    }

                    SetSaveStateData(index);
                }

                EditorGUI.BeginChangeCheck();
                int newVariableIndex = EditorGUI.Popup(variableFieldRect, data.DataInstructions[index].VariableIndex, data.DataInstructions[index].VariableLabels);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(data, "Changed Instructions Variable");

                    data.DataInstructions[index].VariableIndex = newVariableIndex;

                    dataBitCount = NUSaveStateData.BitSum(data.DataInstructions);

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
                Undo.RecordObject(data, "Changed Instruction Order");

                NUSaveStateData.Instruction selectedInstruction = data.DataInstructions[selectedInstructionIndex];
                if (list.index < selectedInstructionIndex)
                    Array.Copy(data.DataInstructions, list.index, data.DataInstructions, list.index + 1, selectedInstructionIndex - list.index);
                else
                    Array.Copy(data.DataInstructions, selectedInstructionIndex + 1, data.DataInstructions, selectedInstructionIndex, list.index - selectedInstructionIndex);

                data.DataInstructions[list.index] = selectedInstruction;

                SetSaveStateData();
            };
            instructionList.onAddCallback = (UnityEditorInternal.ReorderableList list) =>
            {
                Undo.RecordObject(data, "Added Instruction");

                NUSaveStateData.Instruction newData = list.count > 0 ? data.DataInstructions[list.count - 1].ShallowCopy() : new NUSaveStateData.Instruction();
                
                ArrayUtility.Add(ref data.DataInstructions, newData);
                dataBitCount += newData.BitCount;

                propertyUdonBehaviours.arraySize++;
                propertyVariables.arraySize++;
                propertyTypes.arraySize++;

                SetSaveStateData();
            };
            instructionList.onRemoveCallback = (UnityEditorInternal.ReorderableList list) =>
            {
                Undo.RecordObject(data, "Removed Instruction");

                dataBitCount -= data.DataInstructions[list.index].BitCount;
                ArrayUtility.RemoveAt(ref data.DataInstructions, list.index);

                propertyUdonBehaviours.arraySize--;
                propertyVariables.arraySize--;
                propertyTypes.arraySize--;

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
            };

            avatarList.footerHeight = avatarList.serializedProperty.isExpanded ? 20 : 0;

            #endregion;
        }

        public override void OnInspectorGUI()
        {
            // Draws the default convert to UdonBehaviour button, program asset field, sync settings, etc.
            if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target) || target == null) return;

            behaviourProxy = (NUSaveState)target;
            if (!UdonSharpEditorUtility.IsProxyBehaviour(behaviourProxy)) return;

            if (styleBox == null)
            {
                InitializeStyles();
            }

            if (data == null)
            {
                data = NUSaveStateData.CreatePreferences(behaviourProxy);

                dataSO = new SerializedObject(data);
                dataSO.Update();

                InitializeProperties();

                GetSaveStateData();
                SetSaveStateData();
            }

            dataSO.Update();

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
            foldoutDefaultFields = EditorGUILayout.Foldout(foldoutDefaultFields, contentDefault);
            if (foldoutDefaultFields)
            {
                EditorGUI.BeginDisabledGroup(true);

                EditorGUILayout.ObjectField(contentData, data, typeof(NUSaveStateData), true);
                
                EditorGUI.EndDisabledGroup();

                EditorGUI.BeginChangeCheck();
                bool toggleVisibility = EditorGUILayout.Toggle(contentDataToggle, data.hideFlags == HideFlags.None);
                if (EditorGUI.EndChangeCheck())
                    data.SetVisibility(toggleVisibility);

                base.OnInspectorGUI();
            }
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
            if (iconVRChat != null)
            {
                buttonVRChat = new GUIContent(iconVRChat, "VRChat");
                styleVRChat = GUIStyle.none;
            }

            if (GUILayout.Button(buttonVRChat, styleVRChat, GUILayout.Width(iconSize), GUILayout.Height(iconSize)))
            {
                Application.OpenURL("https://vrchat.com/home/user/usr_95c31e1e-15c3-4bf4-b8dd-00373124d67a");
            }

            GUILayout.Space(iconSize / 4);

            GUIContent buttonGitHub = new GUIContent("", "Github");
            GUIStyle styleGitHub = new GUIStyle(GUI.skin.box);
            if (iconGitHub != null)
            {
                buttonGitHub = new GUIContent(iconGitHub, "Github");
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
            // Asterisks!
            if (EditorGUIUtility.isProSkin)
            {
                labelAsteriskFolder = data.DataPreferences.Folder == null ? "<color=#FC6D3F>*</color>" : "";
                labelAsteriskSeed = data.DataPreferences.Seed.Length < 1 ? "<color=#B0FC58>*</color>" : "";
                labelAsteriskName = data.DataPreferences.Parameter.Length < 1 ? "<color=#7ED5FC>*</color>" : "";
            }
            else
            {
                labelAsteriskFolder = data.DataPreferences.Folder == null ? "<color=#AF0C0C>*</color>" : "";
                labelAsteriskSeed = data.DataPreferences.Seed.Length < 1 ? "<color=#2D7C31>*</color>" : "";
                labelAsteriskName = data.DataPreferences.Parameter.Length < 1 ? "<color=#0C6BC9>*</color>" : "";
            }

            GUILayout.BeginVertical(styleHelpBox);

            EditorGUILayout.LabelField("Save State Utilities", EditorStyles.boldLabel);

            GUILayout.BeginVertical(styleBox);

            EditorGUI.BeginChangeCheck();

            using (var horizontalGroup = new GUILayout.HorizontalScope())
            {
                GUILayout.Label(new GUIContent(contentAssetFolder.text + labelAsteriskFolder, contentAssetFolder.tooltip), styleRichTextLabel, GUILayout.Width(EditorGUIUtility.labelWidth));
                
                EditorGUI.BeginChangeCheck();
                DefaultAsset newFolder = (DefaultAsset)EditorGUILayout.ObjectField(data.DataPreferences.Folder, typeof(DefaultAsset), true);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(data, "Changed Asset Folder");

                    data.DataPreferences.Folder = newFolder;
                    data.DataPreferences.FolderIndex = ArrayUtility.IndexOf(assetFolders, data.DataPreferences.Folder);
                }

                EditorGUI.BeginChangeCheck();
                int newFolderIndex = EditorGUILayout.Popup(data.DataPreferences.FolderIndex, assetFolderNames);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(data, "Changed Asset Folder");

                    data.DataPreferences.FolderIndex = newFolderIndex;
                    data.DataPreferences.Folder = assetFolders[data.DataPreferences.FolderIndex];
                }
            }

            using (var horizontalGroup = new GUILayout.HorizontalScope())
                {
                    GUILayout.Label(new GUIContent(contentEncryptionSeed.text + labelAsteriskSeed, contentEncryptionSeed.tooltip), styleRichTextLabel, GUILayout.Width(EditorGUIUtility.labelWidth));
                    EditorGUILayout.PropertyField(propertyEncryptionSeed, GUIContent.none);
                }

            using (var horizontalGroup = new GUILayout.HorizontalScope())
                {
                    GUILayout.Label(new GUIContent(contentParameterName.text + labelAsteriskName, contentParameterName.tooltip), styleRichTextLabel, GUILayout.Width(EditorGUIUtility.labelWidth));
                    EditorGUILayout.PropertyField(propertyParameterName, GUIContent.none);
                }

            if (EditorGUI.EndChangeCheck())
                dataSO.ApplyModifiedProperties();

            EditorGUILayout.Space();

            EditorGUI.BeginDisabledGroup(data.DataPreferences.Folder == null);
            EditorGUI.BeginDisabledGroup(data.DataPreferences.Parameter.Length < 1);
            if (GUILayout.Button(new GUIContent(contentWorldAssets.text + labelAsteriskFolder + labelAsteriskName, contentWorldAssets.tooltip), styleRichTextButton))
            {
                if (EditorUtility.DisplayDialog("SaveState", $"Are you sure you want to generate and replace world assets in {data.DataPreferences.Folder.name}?", "Yes", "No"))
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

            EditorGUI.BeginDisabledGroup(data.DataPreferences.Seed.Length < 1);
            if (GUILayout.Button(new GUIContent(contentAvatarAssets.text + labelAsteriskFolder + labelAsteriskSeed + labelAsteriskName, contentAvatarAssets.tooltip), styleRichTextButton))
            {
                if (EditorUtility.DisplayDialog("SaveState", $"Are you sure you want to generate and replace avatar assets in {data.DataPreferences.Folder.name}?", "Yes", "No"))
                {
                    string pathTemplateArmature = $"{pathSaveState}/Avatar/Template/SaveState-Avatar.fbx";
                    string pathTemplatePrefab = $"{pathSaveState}/Avatar/Template/SaveState-Avatar-Template.prefab";
                    if (!System.IO.File.Exists(pathTemplateArmature) || !System.IO.File.Exists(pathTemplatePrefab))
                    {
                        Debug.LogError($"[<color=#00FF9F>NUSaveState</color>] Could not find all of the template assets at {pathSaveState}/Avatar/Template/");

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
                        string pathAssetFolder = AssetDatabase.GetAssetPath(data.DataPreferences.Folder);
                        string pathUnityPackage = $"{pathAssetFolder}/SaveState-Avatar_{data.DataPreferences.Parameter}.unitypackage";
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
                if (EditorUtility.DisplayDialog("SaveState", $"Are you sure you want to apply the animator controllers from {data.DataPreferences.Folder.name}?", "Yes", "No"))
                    SetSaveStateAnimators();
            }
            EditorGUI.EndDisabledGroup();

            EditorGUI.BeginDisabledGroup(data.DataPreferences.Seed.Length < 1);
            if (GUILayout.Button(new GUIContent(contentApplyKeys.text + labelAsteriskSeed, contentApplyKeys.tooltip), styleRichTextButton))
            {
                if (EditorUtility.DisplayDialog("SaveState", $"Are you sure you want to apply keys generated from \"{data.DataPreferences.Seed}\"?", "Yes", "No"))
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
            int udonSize = propertyUdonBehaviours.arraySize;
            int nameSize = propertyVariables.arraySize;
            int typeSize = propertyTypes.arraySize;
            int instructionSize = data.DataInstructions.Length;

            int newSize = Math.Max(udonSize, Math.Max(nameSize, Math.Max(typeSize, instructionSize)));

            if (udonSize < newSize)
                propertyUdonBehaviours.arraySize = newSize;
            if (nameSize < newSize)
                propertyVariables.arraySize = newSize;
            if (typeSize < newSize)
                propertyTypes.arraySize = newSize;

            serializedObject.ApplyModifiedPropertiesWithoutUndo();

            if (instructionSize != newSize)
                Array.Resize(ref data.DataInstructions, newSize);

            for (int i = 0; i < newSize; i++)
            {
                if (i >= instructionSize)
                    data.DataInstructions[i] = new NUSaveStateData.Instruction();

                UdonBehaviour oldUdonBehaviour = (UdonBehaviour)propertyUdonBehaviours.GetArrayElementAtIndex(i).objectReferenceValue;
                string oldVariableName = propertyVariables.GetArrayElementAtIndex(i).stringValue;
                string oldVariableType = propertyTypes.GetArrayElementAtIndex(i).stringValue;

                data.DataInstructions[i].Udon = oldUdonBehaviour;

                int newVariableIndex = Array.FindIndex(data.DataInstructions[i].Variables, var => var.Name == oldVariableName);
                if (newVariableIndex >= 0)
                {
                    if (data.DataInstructions[i].Variables[newVariableIndex].Type.FullName == oldVariableType)
                        data.DataInstructions[i].VariableIndex = newVariableIndex;
                }
            }

            dataBitCount = NUSaveStateData.BitSum(data.DataInstructions);
        }

        private void SetSaveStateData()
        {
            propertyByteCount.intValue = Mathf.CeilToInt(NUSaveStateData.BitSum(data.DataInstructions) / 8);

            for (int i = 0; i < data.DataInstructions.Length; i++)
            {
                SetSaveStateData(i);
            }

            serializedObject.ApplyModifiedProperties();
        }
        private void SetSaveStateData(int index)
        {
            propertyByteCount.intValue = Mathf.CeilToInt(NUSaveStateData.BitSum(data.DataInstructions) / 8);

            Extensions.NUExtensions.Variable variable = data.DataInstructions[index].Variable;

            UdonBehaviour newUdonBehaviour = data.DataInstructions[index].Udon;
            string newVariableName = variable.Name;
            string newTypeName = variable.Type?.FullName;

            propertyUdonBehaviours.GetArrayElementAtIndex(index).objectReferenceValue = newUdonBehaviour;
            propertyVariables.GetArrayElementAtIndex(index).stringValue = newVariableName;
            propertyTypes.GetArrayElementAtIndex(index).stringValue = newTypeName;

            serializedObject.ApplyModifiedProperties();
        }

        private void SetSaveStateAnimators()
        {
            string pathAssetFolder = AssetDatabase.GetAssetPath(data.DataPreferences.Folder);

            int minBitCount = Math.Min(dataBitCount, 256);

            string[] clearerControllerGUIDs = AssetDatabase.FindAssets("t:AnimatorController l:SaveState-Clearer", new string[] { pathAssetFolder });
            string[] writerControllerGUIDs = AssetDatabase.FindAssets("t:AnimatorController l:SaveState-Writer", new string[] { pathAssetFolder });

            if (clearerControllerGUIDs.Length < 1 || writerControllerGUIDs.Length < minBitCount)
            {
                if (clearerControllerGUIDs.Length < 1 && writerControllerGUIDs.Length < minBitCount)
                    Debug.LogWarning("[<color=#00FF9F>SaveState</color>] Couldn't find parameter Clearer or enough parameter Writers.", data.DataPreferences.Folder);
                else if (clearerControllerGUIDs.Length < 1)
                    Debug.LogWarning("[<color=#00FF9F>SaveState</color>] Couldn't find parameter Clearer.", data.DataPreferences.Folder);
                else if (writerControllerGUIDs.Length < minBitCount)
                    Debug.LogWarning("[<color=#00FF9F>SaveState</color>] Couldn't find enough parameter Writers.", data.DataPreferences.Folder);
                return;
            }

            AnimatorController[] clearingController = new AnimatorController[] { (AnimatorController)AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(clearerControllerGUIDs[0])) };
            AnimatorController[] writingControllers = new AnimatorController[minBitCount];

            for (int controllerIndex = 0; controllerIndex < writingControllers.Length; controllerIndex++)
            {
                writingControllers[controllerIndex] = (AnimatorController)AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(writerControllerGUIDs[controllerIndex]));
            }

            Undo.RecordObject(behaviourProxy, "Apply animator controllers");

            propertyParameterClearer.arraySize = 1;
            propertyParameterClearer.GetArrayElementAtIndex(0).objectReferenceValue = clearingController[0];

            propertyParameterWriters.arraySize = minBitCount;
            for (int i = 0; i < minBitCount; i++)
                propertyParameterWriters.GetArrayElementAtIndex(i).objectReferenceValue = writingControllers[i];

            serializedObject.ApplyModifiedProperties();
        }

        private void ApplyEncryptionKeys()
        {
            int avatarCount = Mathf.CeilToInt(dataBitCount / 256f);

            Vector3[] keyCoordinates = new Vector3[avatarCount];

            UnityEngine.Random.InitState(GetStableHashCode(data.DataPreferences.Seed));

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

            Undo.RecordObject(behaviourProxy, "Apply encryption keys");

            propertyKeyCoords.arraySize = avatarCount;
            for (int i = 0; i < avatarCount; i++)
                propertyKeyCoords.GetArrayElementAtIndex(i).vector3Value = keyCoordinates[i];

            serializedObject.ApplyModifiedProperties();
        }

        private void PrepareWorldAnimators()
        {
            string assetFolderPath = AssetDatabase.GetAssetPath(data.DataPreferences.Folder);

            // Prepare AnimatorController used for clearing data.
            AnimatorController newClearerController = AnimatorController.CreateAnimatorControllerAtPath($"{assetFolderPath}/SaveState-{data.DataPreferences.Parameter}-clear.controller");
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
                    name = $"{data.DataPreferences.Parameter}_{parameterIndex}",
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
                AnimatorController newWriterController = AnimatorController.CreateAnimatorControllerAtPath($"{assetFolderPath}/SaveState-{data.DataPreferences.Parameter}_{controllerIndex / 16}-bit_{controllerIndex % 16}.controller");
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
                        name = $"{data.DataPreferences.Parameter}_{controllerIndex / 16}",
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

            UnityEngine.Random.InitState(GetStableHashCode(data.DataPreferences.Seed));

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
                string newControllerPath = $"{pathAnimatorsFolder}/SaveState-Avatar_{avatarIndex}-{data.DataPreferences.Parameter}.controller";
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
                    AnimationClip newClip = new AnimationClip() { name = $"SaveState-{data.DataPreferences.Parameter}_{byteIndex}.anim" };

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
                    string newParameter = $"{data.DataPreferences.Parameter}_{childIndex - 1}";

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

            string newMenuPath = $"{pathExpressionsFolder}/SaveState-Menu-{data.DataPreferences.Parameter}.asset";
            assetPaths.Add(newMenuPath);
            AssetDatabase.CreateAsset(menu, newMenuPath);

            // Prepare ExpressionParameter.
            parameters = CreateInstance<VRCExpressionParameters>();

            VRCExpressionParameters.Parameter[] expressionControls = new VRCExpressionParameters.Parameter[Math.Min(byteCount, 16)];
            for (int i = 0; i < expressionControls.Length; i++)
            {
                expressionControls[i] = new VRCExpressionParameters.Parameter()
                {
                    name = $"{data.DataPreferences.Parameter}_{i}",
                    valueType = VRCExpressionParameters.ValueType.Float
                };
            }
            parameters.parameters = expressionControls;

            string newParametersPath = $"{pathExpressionsFolder}/SaveState-Expression-{data.DataPreferences.Parameter}.asset";
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
                string newPrefabPath = $"{pathPrefabsFolder}/SaveState-Avatar_{avatarIndex}-{data.DataPreferences.Parameter}.prefab";
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
        }

        private void InitializeProperties()
        {
            propertyEncryptionSeed = dataSO.FindProperty(nameof(NUSaveStateData.DataPreferences)).FindPropertyRelative(nameof(NUSaveStateData.Preferences.Seed));
            propertyParameterName = dataSO.FindProperty(nameof(NUSaveStateData.DataPreferences)).FindPropertyRelative(nameof(NUSaveStateData.Preferences.Parameter));

            propertyEventReciever = serializedObject.FindProperty(nameof(NUSaveState.CallbackReciever));
            propertyFallbackAvatar = serializedObject.FindProperty(nameof(NUSaveState.FallbackAvatarID));

            propertyByteCount = serializedObject.FindProperty("bufferByteCount");
            propertyUdonBehaviours = serializedObject.FindProperty("bufferUdonBehaviours");
            propertyVariables = serializedObject.FindProperty("bufferVariables");
            propertyTypes = serializedObject.FindProperty("bufferTypes");

            propertyAvatarIDs = serializedObject.FindProperty("dataAvatarIDs");
            propertyKeyCoords = serializedObject.FindProperty("dataKeyCoords");

            propertyParameterClearer = serializedObject.FindProperty("parameterClearer");
            propertyParameterWriters = serializedObject.FindProperty("parameterWriters");
        }

        private void GetUIAssets()
        {
            iconVRChat = Resources.Load<Texture2D>("Icons/VRChat-Emblem-32px");
            iconGitHub = Resources.Load<Texture2D>("Icons/GitHub-Mark-32px");
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