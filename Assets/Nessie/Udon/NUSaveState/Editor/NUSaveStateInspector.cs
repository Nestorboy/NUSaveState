
using UnityEngine;
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using VRC.SDKBase;
using VRC.Udon;
using UdonSharpEditor;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace UdonSharp.Nessie.SaveState.Internal
{
    [CustomEditor(typeof(NUSaveState))]
    internal class NUSaveStateInspector : Editor
    {
        private NUSaveState _behaviour;

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
        private string[] animatorSubFolderPaths;
        private string[] animatorFolderNames;
        private DefaultAsset[] animatorFolders;
        private DefaultAsset animatorFolderSelected;
        private int animatorFolderIndex = -1;

        private string saveStateParameterName = "byte";
        private string saveStateSeed = "";
        private int saveStateSeedHash;

        // Save State data.
        private UdonBehaviour dataEventReciever;
        private string dataFallbackAvatarID;
        private int dataVariableCount;
        private int dataBitCount;
        private string[] dataAvatarIDs;

        private UdonBehaviour[] dataUdonBehaviours;
        private string[] dataVariableNames;
        private Type[] dataVariableTypes;
        private int[] dataVariableIndecies;

        // Validity checking.
        private Type[] convertableTypes = new Type[]
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
            typeof(Quaternion),
            typeof(Color),
            typeof(Color32),
            typeof(VRCPlayerApi)
        };
        private int[] convertableBitCount = new int[]
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
            128,
            32,
            32,
            32
        };
        private string[][] validVariableNames;
        private string[][] validVariableAndBits;
        private Type[][] validVariableTypes;

        // UI.
        private GUIStyle styleHelpBox;
        private GUIStyle styleBox;
        private GUIStyle styleScrollView;
        private GUIStyle styleRichTextLabel;
        private GUIStyle styleRichTextButton;

        private GUIContent contentRefresh;

        private GUIContent contentAnimatorFolder = new GUIContent("Animator Folder", "Folder used when applying or generating animators/assets.");
        private GUIContent contentEncryptionSeed = new GUIContent("Encryption Seed", "Seed used to generate key coordinates.");
        private GUIContent contentParameterName = new GUIContent("Parameter Name", "Name used as a parameter prefix.");

        private GUIContent contentWorldAnimators = new GUIContent("Generate World Assets", "Generate world-side assets into the selected folder.");
        private GUIContent contentAvatarAnimators = new GUIContent("Generate Avatar Assets", "Generate avatar-side assets into the selected folder.");
        private GUIContent contentApplyAnimators = new GUIContent("Apply Save State Animators", "Apply animator controllers from the selected folder.");
        private GUIContent contentApplyKeys = new GUIContent("Apply Save State Keys", "Generate keys used as protection against unwanted data reading.");
        private GUIContent contentApplyData = new GUIContent("Apply Save State Data", "Apply changes done to the SavteState data.");

        private GUIContent contentEventReciever = new GUIContent("Event Reciever", "UdonBehaviour which recieves the following events when data processing is done:\n_SSSaved _SSSaveFailed _SSPostSave\n_SSLoaded _SSLoadFailed _SSPostLoad");
        private GUIContent contentFallbackAvatar = new GUIContent("Fallback Avatar", "Blueprint ID used to switch to a default avatar once the data processing is done.");
        private GUIContent contentVariableCount = new GUIContent("Variable Count", "Amount of variables the SaveStates should store. (Try to store as little as possible.)");

        private Texture2D _iconGitHub;
        private Texture2D _iconVRChat;

        private bool foldoutDefaultFields;
        private bool foldoutUdonInstructions;
        private bool foldoutDataAvatars;

        private Vector2 dataVariablesScrollPos;
        private Vector2 dataAvatarScrollPos;

        private void OnEnable()
        {
            InitializeStyles();

            GetUIAssets();

            GetAnimatorFolders();

            _behaviour = (NUSaveState)target;

            GetSaveStateData();
        }

        public override void OnInspectorGUI()
        {
            // Draws the default convert to UdonBehaviour button, program asset field, sync settings, etc.
            if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target) || target == null) return;

            if (!UdonSharpEditorUtility.IsProxyBehaviour(_behaviour)) return;

            if (styleHelpBox == null)
                OnEnable();

            // Initialize GUI styles.
            styleBox = new GUIStyle(GUI.skin.box);
            styleBox.padding = new RectOffset(GUI.skin.box.padding.left * 2, GUI.skin.box.padding.right * 2, GUI.skin.box.padding.top * 2, GUI.skin.box.padding.bottom * 2);
            styleBox.margin = new RectOffset(0, 0, 4, 4);

            styleRichTextLabel = new GUIStyle(GUI.skin.label);
            styleRichTextLabel.richText = true;

            styleRichTextButton = new GUIStyle(GUI.skin.button);
            styleRichTextButton.richText = true;

            DrawBanner();

            DrawSaveStateUtilities();

            DrawSaveStateData();

            EditorGUI.indentLevel++;
            DrawDefaultFields();
            EditorGUI.indentLevel--;
        }

        #region Drawers

        private void DrawDefaultFields()
        {
            foldoutDefaultFields = EditorGUILayout.Foldout(foldoutDefaultFields, new GUIContent("Default Inspector", "Foldout for default UdonSharpBehaviour inspector."));
            if (foldoutDefaultFields)
                base.OnInspectorGUI();
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
            string asteriskFolder;
            string asteriskSeed;
            string asteriskName;
            if (EditorGUIUtility.isProSkin)
            {
                asteriskFolder = animatorFolderSelected == null ? "<color=#FC6D3F>*</color>" : "";
                asteriskSeed = saveStateSeed.Length < 1 ? "<color=#B0FC58>*</color>" : "";
                asteriskName = saveStateParameterName.Length < 1 ? "<color=#7ED5FC>*</color>" : "";
            }
            else
            {
                asteriskFolder = animatorFolderSelected == null ? "<color=#AF0C0C>*</color>" : "";
                asteriskSeed = saveStateSeed.Length < 1 ? "<color=#2D7C31>*</color>" : "";
                asteriskName = saveStateParameterName.Length < 1 ? "<color=#0C6BC9>*</color>" : "";
            }

            GUILayout.BeginVertical(styleHelpBox);

            EditorGUILayout.LabelField("Save State Utilities", EditorStyles.boldLabel);

            GUILayout.BeginVertical(styleBox);

            GUILayout.BeginHorizontal();

            GUILayout.Label(new GUIContent(contentAnimatorFolder.text + asteriskFolder, contentAnimatorFolder.tooltip), styleRichTextLabel, GUILayout.Width(EditorGUIUtility.labelWidth));

            EditorGUI.BeginChangeCheck();
            DefaultAsset newFolder = (DefaultAsset)EditorGUILayout.ObjectField(animatorFolderSelected, typeof(DefaultAsset), true);
            if (EditorGUI.EndChangeCheck())
            {
                if (newFolder == null || System.IO.Directory.Exists(AssetDatabase.GetAssetPath(newFolder))) // Simple fix to prevent non-folders from being selected.
                {
                    animatorFolderSelected = newFolder;
                    animatorFolderIndex = ArrayUtility.IndexOf(animatorFolders, animatorFolderSelected);
                }
            }

            EditorGUI.BeginChangeCheck();
            animatorFolderIndex = EditorGUILayout.Popup(animatorFolderIndex, animatorFolderNames);
            if (EditorGUI.EndChangeCheck())
            {
                animatorFolderSelected = animatorFolders[animatorFolderIndex];
            }

            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();

            GUILayout.Label(new GUIContent(contentEncryptionSeed.text + asteriskSeed, contentEncryptionSeed.tooltip), styleRichTextLabel, GUILayout.Width(EditorGUIUtility.labelWidth));

            EditorGUI.BeginChangeCheck();
            saveStateSeed = EditorGUILayout.TextField(saveStateSeed);
            if (EditorGUI.EndChangeCheck())
            {
                saveStateSeedHash = GetStableHashCode(saveStateSeed);
            }

            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();

            GUILayout.Label(new GUIContent(contentParameterName.text + asteriskName, contentParameterName.tooltip), styleRichTextLabel, GUILayout.Width(EditorGUIUtility.labelWidth));

            EditorGUI.BeginChangeCheck();
            saveStateParameterName = EditorGUILayout.TextField(saveStateParameterName);
            if (EditorGUI.EndChangeCheck())
            {

            }

            GUILayout.EndHorizontal();

            EditorGUILayout.Space();

            EditorGUI.BeginDisabledGroup(animatorFolderSelected == null);
            EditorGUI.BeginDisabledGroup(saveStateParameterName.Length < 1);
            if (GUILayout.Button(new GUIContent(contentWorldAnimators.text + asteriskFolder + asteriskName, contentWorldAnimators.tooltip), styleRichTextButton))
            {
                if (EditorUtility.DisplayDialog("SaveState", $"Are you sure you want to generate and replace world assets in {animatorFolderSelected.name}?", "Yes", "No"))
                    PrepareWorldAnimators();
            }

            EditorGUI.BeginDisabledGroup(saveStateSeed.Length < 1);
            if (GUILayout.Button(new GUIContent(contentAvatarAnimators.text + asteriskFolder + asteriskSeed + asteriskName, contentAvatarAnimators.tooltip), styleRichTextButton))
            {
                if (EditorUtility.DisplayDialog("SaveState", $"Are you sure you want to generate and replace avatar assets in {animatorFolderSelected.name}?", "Yes", "No"))
                {
                    PrepareAvatarAnimators();
                    GenerateAvatarMenu();
                }
            }
            EditorGUI.EndDisabledGroup();
            EditorGUI.EndDisabledGroup();

            if (GUILayout.Button(new GUIContent(contentApplyAnimators.text + asteriskFolder, contentApplyAnimators.tooltip), styleRichTextButton))
            {
                if (EditorUtility.DisplayDialog("SaveState", $"Are you sure you want to apply the animator controllers from {animatorFolderSelected.name}?", "Yes", "No"))
                    SetSaveStateAnimators();
            }
            EditorGUI.EndDisabledGroup();

            EditorGUI.BeginDisabledGroup(saveStateSeed.Length < 1);
            if (GUILayout.Button(new GUIContent(contentApplyKeys.text + asteriskSeed, contentApplyKeys.tooltip), styleRichTextButton))
            {
                if (EditorUtility.DisplayDialog("SaveState", $"Are you sure you want to apply keys generated from \"{saveStateSeed}\"?", "Yes", "No"))
                    ApplyEncryptionKeys();
            }
            EditorGUI.EndDisabledGroup();

            if (GUILayout.Button(contentApplyData))
            {
                if (EditorUtility.DisplayDialog("SaveState", $"Are you sure you want to apply the changes?", "Yes", "No"))
                    SetSaveStateData();
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.EndVertical();
        }

        private void DrawSaveStateData()
        {
            // Fallback avatar, Variable count, Instructions (Udon & Variable) Scroll, Data Avatar IDs Scroll

            GUILayout.BeginVertical(styleHelpBox);

            GUILayout.BeginHorizontal();

            EditorGUILayout.LabelField("Save State Data", EditorStyles.boldLabel);

            if (GUILayout.Button(contentRefresh, GUILayout.ExpandWidth(false), GUILayout.Height(EditorGUIUtility.singleLineHeight)))
            {
                GetSaveStateData();
            }

            GUILayout.EndHorizontal();

            GUILayout.BeginVertical(styleBox);

            EditorGUI.BeginChangeCheck();
            dataEventReciever = (UdonBehaviour)EditorGUILayout.ObjectField(contentEventReciever, dataEventReciever, typeof(UdonBehaviour), true);
            if (EditorGUI.EndChangeCheck())
            {

            }

            EditorGUI.BeginChangeCheck();
            dataFallbackAvatarID = EditorGUILayout.TextField(contentFallbackAvatar, dataFallbackAvatarID);
            if (EditorGUI.EndChangeCheck())
            {
                
            }

            EditorGUI.BeginChangeCheck();
            dataVariableCount = EditorGUILayout.IntField(contentVariableCount, dataVariableCount);
            if (EditorGUI.EndChangeCheck())
            {
                dataVariableCount = Mathf.Max(dataVariableCount, 0);

                UdonBehaviour[] newUdonBehaviours = new UdonBehaviour[dataVariableCount];
                string[] newVariableNames = new string[dataVariableCount];
                Type[] newVariableTypes = new Type[dataVariableCount];
                int[] newVariableIndecies = new int[dataVariableCount];

                string[][] validVarNames = new string[dataVariableCount][];
                string[][] validVarNamesAndBits = new string[dataVariableCount][];
                Type[][] validVarTypes = new Type[dataVariableCount][];

                Array.Copy(dataUdonBehaviours, 0, newUdonBehaviours, 0, Math.Min(dataUdonBehaviours.Length, dataVariableCount));
                Array.Copy(dataVariableNames, 0, newVariableNames, 0, Math.Min(dataVariableNames.Length, dataVariableCount));
                Array.Copy(dataVariableTypes, 0, newVariableTypes, 0, Math.Min(dataVariableTypes.Length, dataVariableCount));
                Array.Copy(dataVariableIndecies, 0, newVariableIndecies, 0, Math.Min(dataVariableIndecies.Length, dataVariableCount));

                Array.Copy(validVariableNames, 0, validVarNames, 0, Math.Min(validVariableNames.Length, dataVariableCount));
                Array.Copy(validVariableAndBits, 0, validVarNamesAndBits, 0, Math.Min(validVariableAndBits.Length, dataVariableCount));
                Array.Copy(validVariableTypes, 0, validVarTypes, 0, Math.Min(validVariableTypes.Length, dataVariableCount));

                if (dataVariableCount > validVariableNames.Length)
                    for (int i = validVariableNames.Length; i < dataVariableCount; i++)
                    {
                        validVarNames[i] = new string[0];
                        validVarNamesAndBits[i] = new string[0];
                        validVarTypes[i] = new Type[0];
                        newVariableIndecies[i] = -1;
                    }

                dataUdonBehaviours = newUdonBehaviours;
                dataVariableNames = newVariableNames;
                dataVariableTypes = newVariableTypes;
                dataVariableIndecies = newVariableIndecies;

                validVariableNames = validVarNames;
                validVariableAndBits = validVarNamesAndBits;
                validVariableTypes = validVarTypes;

                dataBitCount = CalculateBitCount(dataVariableTypes);
                if (dataBitCount != dataAvatarIDs.Length)
                {
                    string[] newDataAvatarIDs = new string[Mathf.CeilToInt(dataBitCount / 128f)];
                    Array.Copy(dataAvatarIDs, 0, newDataAvatarIDs, 0, Math.Min(newDataAvatarIDs.Length, dataAvatarIDs.Length));
                    dataAvatarIDs = newDataAvatarIDs;
                }
            }

            EditorGUILayout.Space();

            GUILayout.BeginVertical(styleHelpBox);

            GUIStyle labelStyle = new GUIStyle(styleBox);
            labelStyle.margin = new RectOffset(0, 0, 0, 0);
            labelStyle.padding = new RectOffset(0, 1, 0, 0);

            GUILayout.BeginHorizontal();

            EditorGUI.indentLevel++;

            foldoutUdonInstructions = EditorGUILayout.Foldout(foldoutUdonInstructions, $"Bits: {dataBitCount}/{Mathf.CeilToInt(dataBitCount / 128f) * 128}  Bytes: {Mathf.Ceil(dataBitCount / 8f)} / {Mathf.CeilToInt(dataBitCount / 128f) * 16}");

            EditorGUI.indentLevel--;

            GUILayout.EndHorizontal();

            GUILayout.BeginVertical(labelStyle);

            dataVariablesScrollPos = GUILayout.BeginScrollView(dataVariablesScrollPos, false, true, GUILayout.MaxHeight(foldoutUdonInstructions ? 202 : 62));
            for (int i = 0; i < dataVariableCount; i++)
            {
                GUILayout.BeginHorizontal();

                EditorGUI.BeginChangeCheck();
                dataUdonBehaviours[i] = (UdonBehaviour)EditorGUILayout.ObjectField(dataUdonBehaviours[i], typeof(UdonBehaviour), true);
                if (EditorGUI.EndChangeCheck())
                {
                    GetValidVariables(dataUdonBehaviours[i], out List<string> vars, out List<Type> types);

                    string[] newValidVars = vars.ToArray();
                    Type[] newValidTypes = types.ToArray();

                    dataVariableIndecies[i] = dataVariableIndecies[i] < 0 ? -1 : Array.IndexOf(newValidVars, validVariableNames[i][dataVariableIndecies[i]]);
                    validVariableNames[i] = newValidVars;
                    validVariableTypes[i] = newValidTypes;

                    string[] newValidVarsAndBits = new string[newValidVars.Length];
                    for (int j = 0; j < newValidVars.Length; j++)
                    {
                        int typeIndex = Array.IndexOf(convertableTypes, validVariableTypes[i][j]);
                        string bitSuffix = typeIndex < 0 ? "invalid type" : $"{convertableBitCount[typeIndex]}";
                        newValidVarsAndBits[j] = $"{validVariableNames[i][j]} ({bitSuffix})";
                    }
                    validVariableAndBits[i] = newValidVarsAndBits;

                    if (dataVariableIndecies[i] < 0)
                    {
                        dataVariableNames[i] = null;
                        dataVariableTypes[i] = null;
                    }
                    else
                    {
                        dataVariableNames[i] = validVariableNames[i][dataVariableIndecies[i]];
                        dataVariableTypes[i] = validVariableTypes[i][dataVariableIndecies[i]];
                    }

                    dataBitCount = CalculateBitCount(dataVariableTypes);
                    if (dataBitCount != dataAvatarIDs.Length)
                    {
                        string[] newDataAvatarIDs = new string[Mathf.CeilToInt(dataBitCount / 128f)];
                        Array.Copy(dataAvatarIDs, 0, newDataAvatarIDs, 0, Math.Min(newDataAvatarIDs.Length, dataAvatarIDs.Length));
                        dataAvatarIDs = newDataAvatarIDs;
                    }
                }

                EditorGUI.BeginChangeCheck();
                dataVariableIndecies[i] = EditorGUILayout.Popup(dataVariableIndecies[i], validVariableAndBits[i]);
                if (EditorGUI.EndChangeCheck())
                {
                    dataVariableNames[i] = validVariableNames[i][dataVariableIndecies[i]];
                    dataVariableTypes[i] = validVariableTypes[i][dataVariableIndecies[i]];

                    dataBitCount = CalculateBitCount(dataVariableTypes);
                    if (dataBitCount != dataAvatarIDs.Length)
                    {
                        string[] newDataAvatarIDs = new string[Mathf.CeilToInt(dataBitCount / 128f)];
                        Array.Copy(dataAvatarIDs, 0, newDataAvatarIDs, 0, Math.Min(newDataAvatarIDs.Length, dataAvatarIDs.Length));
                        dataAvatarIDs = newDataAvatarIDs;
                    }
                }

                GUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();

            GUILayout.EndVertical();

            GUILayout.EndVertical();

            EditorGUILayout.Space();

            GUILayout.BeginVertical(styleHelpBox);

            GUILayout.BeginHorizontal();

            EditorGUI.indentLevel++;

            foldoutDataAvatars = EditorGUILayout.Foldout(foldoutDataAvatars, $"Avatars: {Mathf.CeilToInt(dataBitCount / 128f)}");

            EditorGUI.indentLevel--;

            GUILayout.EndHorizontal();

            GUILayout.BeginVertical(labelStyle);

            dataAvatarScrollPos = GUILayout.BeginScrollView(dataAvatarScrollPos, false, true, GUILayout.MaxHeight(foldoutDataAvatars ? 202 : 62));
            for (int i = 0; i < dataAvatarIDs.Length; i++)
            {
                GUILayout.BeginHorizontal();

                EditorGUI.BeginChangeCheck();
                dataAvatarIDs[i] = EditorGUILayout.TextField(dataAvatarIDs[i]);
                if (EditorGUI.EndChangeCheck())
                {

                }

                GUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();

            GUILayout.EndVertical();

            GUILayout.EndVertical();

            GUILayout.EndVertical();

            GUILayout.EndVertical();
        }

        #endregion Drawers

        #region SaveState Methods

        private void GetSaveStateData()
        {
            UdonBehaviour[] newUdonBehaviours = _behaviour.BufferUdonBehaviours == null ? new UdonBehaviour[0] : new UdonBehaviour[_behaviour.BufferUdonBehaviours.Length];
            string[] newVariableNames = _behaviour.BufferVariables == null ? new string[0] : new string[_behaviour.BufferUdonBehaviours.Length];
            Type[] newVariableTypes = _behaviour.BufferTypes == null ? new Type[0] : new Type[_behaviour.BufferUdonBehaviours.Length];
            int[] newVariableIndecies = _behaviour.BufferVariables == null ? new int[0] : new int[_behaviour.BufferUdonBehaviours.Length];

            if (newUdonBehaviours.Length > 0)
                _behaviour.BufferUdonBehaviours.CopyTo(newUdonBehaviours, 0);

            if (newVariableNames.Length > 0)
                _behaviour.BufferVariables.CopyTo(newVariableNames, 0);

            if (newVariableTypes.Length > 0)
                _behaviour.BufferTypes.CopyTo(newVariableTypes, 0);

            string[][] validVars = new string[newUdonBehaviours.Length][];
            string[][] validVarsAndBits = new string[newUdonBehaviours.Length][];
            Type[][] validTypes = new Type[newUdonBehaviours.Length][];
            for (int i = 0; i < newUdonBehaviours.Length; i++)
            {
                GetValidVariables(newUdonBehaviours[i], out List<string> vars, out List<Type> types);
                validVars[i] = vars.ToArray();
                validTypes[i] = types.ToArray();
                newVariableIndecies[i] = Array.IndexOf(validVars[i], newVariableNames[i]);

                validVarsAndBits[i] = new string[validVars[i].Length];
                for (int j = 0; j < validVars[i].Length; j++)
                {
                    int typeIndex = Array.IndexOf(convertableTypes, validTypes[i][j]);
                    string bitSuffix = typeIndex < 0 ? "invalid type" : $"{convertableBitCount[typeIndex]}";
                    validVarsAndBits[i][j] = $"{validVars[i][j]} ({bitSuffix})";
                }
            }

            string[] newDataAvatarIDs = _behaviour.DataAvatarIDs == null ? new string[0] : new string[_behaviour.DataAvatarIDs.Length];
            if (newDataAvatarIDs.Length > 0)
                _behaviour.DataAvatarIDs.CopyTo(newDataAvatarIDs, 0);

            dataUdonBehaviours = newUdonBehaviours;
            dataVariableNames = newVariableNames;
            dataVariableTypes = newVariableTypes;
            dataVariableIndecies = newVariableIndecies;

            dataEventReciever = _behaviour.HookEventReciever;
            dataFallbackAvatarID = _behaviour.FallbackAvatarID;
            dataVariableCount = newUdonBehaviours.Length;
            dataBitCount = CalculateBitCount(newVariableTypes);
            dataAvatarIDs = newDataAvatarIDs;

            validVariableNames = validVars;
            validVariableAndBits = validVarsAndBits;
            validVariableTypes = validTypes;
        }

        private void SetSaveStateData()
        {
            string[] newVarNames = new string[dataVariableNames.Length];
            for (int i = 0; i < newVarNames.Length; i++)
                newVarNames[i] = dataVariableIndecies[i] < 0 ? null : validVariableNames[i][dataVariableIndecies[i]];

            Undo.RecordObject(_behaviour, "Apply SaveState data");

            _behaviour.HookEventReciever = dataEventReciever;
            _behaviour.FallbackAvatarID = dataFallbackAvatarID;
            _behaviour.MaxByteCount = Mathf.CeilToInt(CalculateBitCount(dataVariableTypes) / 8);
            _behaviour.DataAvatarIDs = dataAvatarIDs;

            _behaviour.BufferUdonBehaviours = dataUdonBehaviours;
            _behaviour.BufferVariables = newVarNames;
            _behaviour.BufferTypes = dataVariableTypes;
        }

        private int CalculateBitCount(Type[] types)
        {
            int bits = 0;

            foreach (Type type in types)
                bits += Array.IndexOf(convertableTypes, type) < 0 ? 0 : convertableBitCount[Array.IndexOf(convertableTypes, type)];

            return bits;
        }

        private void GetValidVariables(UdonBehaviour udon, out List<string> vars, out List<Type> types)
        {
            vars = new List<string>();
            types = new List<Type>();
            if (udon == null) return;

            VRC.Udon.Common.Interfaces.IUdonSymbolTable symbolTable = udon.programSource.SerializedProgramAsset.RetrieveProgram().SymbolTable;
            List<string> programVariablesNames = symbolTable.GetSymbols().ToList();
            foreach (string variableName in programVariablesNames)
                if (!variableName.StartsWith("__"))
                {
                    Type variableType = symbolTable.GetSymbolType(variableName);
                    int typeIndex = Array.IndexOf(convertableTypes, variableType);
                    if (typeIndex > -1)
                    {
                        vars.Add(variableName);
                        types.Add(variableType);
                    }
                }
        }

        private void SetSaveStateAnimators()
        {
            int byteCount = Mathf.CeilToInt(dataBitCount / 8f);
            int minBitCount = Math.Min(dataBitCount, 128);
            int minByteCount = Math.Min(byteCount, 16);

            string[] writerControllerGUIDs = AssetDatabase.FindAssets("t:AnimatorController l:SaveState-Writer", new string[] { AssetDatabase.GetAssetPath(animatorFolderSelected) });
            string[] clearerControllerGUIDs = AssetDatabase.FindAssets("t:AnimatorController l:SaveState-Clearer", new string[] { AssetDatabase.GetAssetPath(animatorFolderSelected) });
            if (writerControllerGUIDs.Length < minBitCount || clearerControllerGUIDs.Length < minByteCount)
            {
                Debug.LogWarning("[<color=#00FF9F>SaveState</color>] Couldn't find enough Animator Controllers.");
                return;
            }

            AnimatorController[] writingControllers = new AnimatorController[minBitCount];
            AnimatorController[] clearingControllers = new AnimatorController[minByteCount];

            for (int controllerIndex = 0; controllerIndex < writingControllers.Length; controllerIndex++)
            {
                writingControllers[controllerIndex] = (AnimatorController)AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(writerControllerGUIDs[controllerIndex]));
            }

            for (int controllerIndex = 0; controllerIndex < clearingControllers.Length; controllerIndex++)
            {
                clearingControllers[controllerIndex] = (AnimatorController)AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(clearerControllerGUIDs[controllerIndex]));
            }

            Undo.RecordObject(_behaviour, "Apply animator controllers");
            _behaviour.ByteWriters = writingControllers;
            _behaviour.ByteClearers = clearingControllers;
        }

        private void ApplyEncryptionKeys()
        {
            int avatarCount = Mathf.CeilToInt(dataBitCount / 128f);

            Vector3[] keyCoordinates = new Vector3[avatarCount];

            UnityEngine.Random.InitState(saveStateSeedHash);
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

            Undo.RecordObject(_behaviour, "Apply encryption keys");
            _behaviour.KeyCoordinates = keyCoordinates;
        }

        private void PrepareWorldAnimators()
        {
            System.Diagnostics.Stopwatch timer = new System.Diagnostics.Stopwatch();
            timer.Start();

            try
            {
                AssetDatabase.StartAssetEditing();

                for (int controllerIndex = 0; controllerIndex < 144; controllerIndex++)
                {
                    EditorUtility.DisplayProgressBar("NUSaveState", $"Preparing Animator Controllers... ({controllerIndex}/{144})", (float)controllerIndex / 144);

                    bool isClearer = controllerIndex % 9 == 8;

                    // Prepare AnimatorController.
                    AnimatorController newController = AnimatorController.CreateAnimatorControllerAtPath(AssetDatabase.GetAssetPath(animatorFolderSelected) + (isClearer ? $"/SaveState-{saveStateParameterName}_{controllerIndex / 9}-clear.controller" : $"/SaveState-{saveStateParameterName}_{controllerIndex / 9}-bit_{controllerIndex % 9}.controller"));
                    AnimatorStateMachine newStateMachine = newController.layers[0].stateMachine;
                    newStateMachine.entryPosition = new Vector2(-30, 0);
                    newStateMachine.anyStatePosition = new Vector2(-30, 50);
                    newStateMachine.exitPosition = new Vector2(-30, 100);

                    // Prepate AnimatorState.
                    AnimatorState newState = newStateMachine.AddState("Write");
                    newStateMachine.states[0].position = new Vector2(-30, 150);
                    newState.writeDefaultValues = false;

                    // Prepare VRC Behaviour.
                    var VRCParameterDriver = newState.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
                    VRCParameterDriver.parameters = new List<VRC_AvatarParameterDriver.Parameter>()
                    {
                        new VRC_AvatarParameterDriver.Parameter()
                        {
                            name = $"{saveStateParameterName}_{controllerIndex / 9}",
                            value = isClearer ? 0 : 1 / Mathf.Pow(2, 7 - controllerIndex % 9 + 1),
                            type = isClearer ? VRC_AvatarParameterDriver.ChangeType.Set : VRC_AvatarParameterDriver.ChangeType.Add
                        }
                    };

                    AssetDatabase.SetLabels(newController, new string[] { isClearer ? "SaveState-Clearer" : "SaveState-Writer" });
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            AssetDatabase.SaveAssets();

            timer.Stop();
            Debug.Log($"[<color=#00FF9F>NUSaveState</color>] World asset creation took: {timer.Elapsed:mm\\:ss\\.fff}");

            EditorUtility.ClearProgressBar();
        }

        private void PrepareAvatarAnimators()
        {
            int avatarCount = Mathf.CeilToInt(dataBitCount / 128f);
            int byteCount = Mathf.CeilToInt(dataBitCount / 8f);

            // Prepare keys.
            Vector3[] keyCoordinates = new Vector3[avatarCount];

            UnityEngine.Random.InitState(saveStateSeedHash);

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

            System.Diagnostics.Stopwatch timer = new System.Diagnostics.Stopwatch();
            timer.Start();

            try
            {
                AssetDatabase.StartAssetEditing();

                // Create animator for each avatar.
                for (int avatarIndex = 0; avatarIndex < avatarCount; avatarIndex++)
                {
                    EditorUtility.DisplayProgressBar("NUSaveState", $"Preparing Animator Controllers... ({avatarIndex}/{avatarCount})", (float)avatarIndex / avatarCount);

                    // Prepare AnimatorController.
                    AnimatorController newController = AnimatorController.CreateAnimatorControllerAtPath(AssetDatabase.GetAssetPath(animatorFolderSelected) + $"/SaveState-Avatar_{avatarIndex}-{saveStateParameterName}.controller");
                    AnimatorStateMachine newStateMachine = newController.layers[0].stateMachine;
                    newStateMachine.entryPosition = new Vector2(-30, 0);
                    newStateMachine.anyStatePosition = new Vector2(-30, 50);
                    newStateMachine.exitPosition = new Vector2(-30, 100);

                    // Prepare data BlendTree state.
                    AnimatorState newState = newController.CreateBlendTreeInController("Data Blend", out BlendTree newTree, 0);
                    newStateMachine.states[0].position = new Vector2(-30, 150);
                    newController.RemoveParameter(0);

                    newState.writeDefaultValues = false;

                    newTree.blendType = BlendTreeType.Direct;

                    // Prepare VRC Behaviours.
                    var VRCLayerControl = newState.AddStateMachineBehaviour<VRCPlayableLayerControl>();
                    var VRCTrackingControl = newState.AddStateMachineBehaviour<VRCAnimatorTrackingControl>();

                    VRCLayerControl.goalWeight = 1;

                    VRCTrackingControl.trackingLeftFingers = VRC_AnimatorTrackingControl.TrackingType.Animation;
                    VRCTrackingControl.trackingRightFingers = VRC_AnimatorTrackingControl.TrackingType.Animation;

                    // Prepare base BlendTree animation.
                    AnimationClip newBaseClip = new AnimationClip() { name = "Straighten Bones" };

                    newBaseClip.SetCurve("", typeof(Animator), "RootT.y", AnimationCurve.Constant(0, 0, 1));

                    newBaseClip.SetCurve("SaveState-Key", typeof(Transform), "m_LocalPosition.x", AnimationCurve.Constant(0, 0, keyCoordinates[avatarIndex].x));
                    newBaseClip.SetCurve("SaveState-Key", typeof(Transform), "m_LocalPosition.y", AnimationCurve.Constant(0, 0, keyCoordinates[avatarIndex].y));
                    newBaseClip.SetCurve("SaveState-Key", typeof(Transform), "m_LocalPosition.z", AnimationCurve.Constant(0, 0, keyCoordinates[avatarIndex].z));
                    for (int i = 0; i < muscleNames.Length; i++)
                    {
                        newBaseClip.SetCurve("", typeof(Animator), $"{muscleNames[i]}2 Stretched", AnimationCurve.Constant(0, 0, 0.81002f));
                        newBaseClip.SetCurve("", typeof(Animator), $"{muscleNames[i]}3 Stretched", AnimationCurve.Constant(0, 0, 0.81002f));
                    }
                    newTree.AddChild(newBaseClip);

                    AssetDatabase.AddObjectToAsset(newBaseClip, newController);

                    // Prepare data BlendTree animations.
                    for (int byteIndex = 0; byteIndex < Math.Min(byteCount, 16); byteIndex++)
                    {
                        AnimationClip newClip = new AnimationClip() { name = $"SaveState-{saveStateParameterName}_{byteIndex}.anim" };

                        newClip.SetCurve("", typeof(Animator), $"{muscleNames[byteIndex % muscleNames.Length]}{3 - byteIndex / muscleNames.Length} Stretched", AnimationCurve.Constant(0, 0, 1));
                        newTree.AddChild(newClip);

                        AssetDatabase.AddObjectToAsset(newClip, newController);
                    }

                    // Prepare BlendTree parameters.
                    ChildMotion[] newChildren = newTree.children;

                    newController.AddParameter(new AnimatorControllerParameter() { name = "Base", type = AnimatorControllerParameterType.Float, defaultFloat = 1 });
                    newChildren[0].directBlendParameter = "Base";

                    for (int childIndex = 1; childIndex < newChildren.Length; childIndex++)
                    {
                        string newParameter = $"{saveStateParameterName}_{childIndex - 1}";

                        newController.AddParameter(newParameter, AnimatorControllerParameterType.Float);
                        newChildren[childIndex].directBlendParameter = newParameter;
                    }

                    newTree.children = newChildren;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            AssetDatabase.SaveAssets();

            timer.Stop();
            Debug.Log($"[<color=#00FF9F>NUSaveState</color>] Avatar asset creation took: {timer.Elapsed:mm\\:ss\\.fff}");

            EditorUtility.ClearProgressBar();
        }

        private void GenerateAvatarMenu()
        {
            int byteCount = Mathf.CeilToInt(dataBitCount / 8f);

            // Prepare ExpressionMenu.
            VRCExpressionsMenu expressionMenu = CreateInstance<VRCExpressionsMenu>();
            expressionMenu.controls.Add(new VRCExpressionsMenu.Control()
            {
                name = "<font=LiberationMono SDF><color=#00FF9F><size=140%><b>Nessie's Udon Save <voffset=15em>State"
            });

            AssetDatabase.CreateAsset(expressionMenu, AssetDatabase.GetAssetPath(animatorFolderSelected) + $"/SaveState-Menu-{saveStateParameterName}.asset");

            // Prepare ExpressionParameter.
            VRCExpressionParameters expressionParameters = CreateInstance<VRCExpressionParameters>();

            VRCExpressionParameters.Parameter[] expressionControls = new VRCExpressionParameters.Parameter[Math.Min(byteCount, 16)];
            for (int i = 0; i < expressionControls.Length; i++)
            {
                expressionControls[i] = new VRCExpressionParameters.Parameter()
                {
                    name = $"{saveStateParameterName}_{i}",
                    valueType = VRCExpressionParameters.ValueType.Float
                };
            }

            expressionParameters.parameters = expressionControls;

            AssetDatabase.CreateAsset(expressionParameters, AssetDatabase.GetAssetPath(animatorFolderSelected) + $"/SaveState-Expression-{saveStateParameterName}.asset");

            AssetDatabase.SaveAssets();
        }

        #endregion SaveState Methods

        #region Resources

        private void InitializeStyles()
        {
            styleHelpBox = new GUIStyle(EditorStyles.helpBox);
            styleHelpBox.padding = new RectOffset(0, 0, styleHelpBox.padding.top, styleHelpBox.padding.bottom + 3);
            styleScrollView = new GUIStyle(EditorStyles.helpBox) { padding = new RectOffset(0, 1, 0, 0) };
            styleScrollView.padding = new RectOffset(0, 1, 0, 0);

            if (EditorGUIUtility.isProSkin)
                contentRefresh = new GUIContent(EditorGUIUtility.IconContent("d_Refresh"));
            else
                contentRefresh = new GUIContent(EditorGUIUtility.IconContent("Refresh"));
            contentRefresh.tooltip = "Retrieve data from the selected Save State.";
        }

        private void GetUIAssets()
        {
            _iconVRChat = Resources.Load<Texture2D>("Icons/VRChat-Emblem-32px");
            _iconGitHub = Resources.Load<Texture2D>("Icons/GitHub-Mark-32px");
        }

        private void GetAnimatorFolders()
        {
            string[] guids = AssetDatabase.FindAssets("t:Folder AnimatorFolders", new string[] { "Assets" });

            string animatorFolderPath = null;
            foreach (string guid in guids)
                if (AssetDatabase.GUIDToAssetPath(guid).EndsWith("NUSaveState/AnimatorFolders"))
                {
                    animatorFolderPath = AssetDatabase.GUIDToAssetPath(guid);
                    break;
                }

            if (animatorFolderPath == null)
                return;

            animatorSubFolderPaths = AssetDatabase.GetSubFolders(animatorFolderPath);
            animatorFolderNames = new string[animatorSubFolderPaths.Length];
            animatorFolders = new DefaultAsset[animatorSubFolderPaths.Length];
            for (int i = 0; i < animatorFolders.Length; i++)
            {
                animatorFolders[i] = (DefaultAsset)AssetDatabase.LoadMainAssetAtPath(animatorSubFolderPaths[i]);
                animatorFolderNames[i] = animatorFolders[i].name;
            }
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