
using UnityEngine;
using VRC.Udon;
using System;
using UnityEditor;
using UdonSharpEditor;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;

namespace UdonSharp.Nessie.Debugger.Internal
{
    [CustomEditor(typeof(NUDebugger))]
    internal class NUDebuggerInspector : Editor
    {
        // Assets.
        private Texture2D _iconGitHub;
        private Texture2D _iconVRChat;

        private bool _foldoutRefs;
        private bool _foldoutData;

        public FilterType _filterType = FilterType.Smart;

        public List<UdonData> _listData = new List<UdonData>();

        private NUDebugger _debugUdon;

        [Flags]
        public enum FilterType
        {
            None,
            Public,
            Smart
        }

        public class UdonData : IComparable<UdonData>
        {
            public UdonBehaviour Udon;
            public List<string> Arrays;
            public List<string> Variables;
            public List<string> Events;
            public bool[] Expanded;

            public int CompareTo(UdonData comparePart)
            {
                return comparePart == null ? 1 : Udon.name.CompareTo(comparePart.Udon.name);
            }
        }

        private void OnEnable()
        {
            // Stupid workaround for whacky U# related errors.
            if (target == null) return;
            
            _debugUdon = (NUDebugger)target;

            GetAssets();

            GetData();

            _foldoutData = _listData.Count < 20;
        }

        public override void OnInspectorGUI()
        {
            // Draws the default convert to UdonBehaviour button, program asset field, sync settings, etc.
            if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target)) return;

            GUILayout.BeginVertical(EditorStyles.helpBox);

            DrawBanner();

            EditorGUILayout.Space();

            DrawSettings();

            EditorGUILayout.Space();

            DrawUtilities();

            EditorGUILayout.Space();

            DrawBehaviours();

            GUILayout.EndVertical();
        }

        #region Drawers

        private void DrawBanner()
        {
            GUILayout.BeginHorizontal(EditorStyles.helpBox);

            GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.richText = true;

            GUILayout.Label("<b>Nessie's Udon Debugger</b>", labelStyle);

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

        private void DrawSettings()
        {
            GUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.LabelField("Debugger Settings", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            Color mainColor = EditorGUILayout.ColorField(new GUIContent("Main Color", "Color used for UI text and icon elements."), _debugUdon._mainColor);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_debugUdon, "Changed Main Color.");
                _debugUdon._mainColor = mainColor;
            }

            EditorGUI.BeginChangeCheck();
            Color crashColor = EditorGUILayout.ColorField(new GUIContent("Crash Color", "Color used to indicate if an UdonBehaviour has crashed."), _debugUdon._crashColor);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_debugUdon, "Changed Crash Color.");
                _debugUdon._crashColor = crashColor;
            }

            EditorGUI.BeginChangeCheck();
            float updateRate = EditorGUILayout.FloatField(new GUIContent("Update Rate", "Delay in seconds between debugger updates."), _debugUdon._updateRate);
            if (EditorGUI.EndChangeCheck())
            {
                if (updateRate <= 0)
                    updateRate = 0;

                Undo.RecordObject(_debugUdon, "Changed Update Rate.");
                _debugUdon._updateRate = updateRate;
            }

            EditorGUI.BeginChangeCheck();
            bool networked = EditorGUILayout.Toggle(new GUIContent("Networked", "Boolean used to decide if an event call should be networked or not."), _debugUdon._networked);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_debugUdon, "Changed Update Rate.");
                _debugUdon._networked = networked;
            }

            EditorGUILayout.Space();

            EditorGUI.indentLevel++;
            _foldoutRefs = EditorGUILayout.Foldout(_foldoutRefs, new GUIContent("References", "References used by the Udon Debugger. (Don't touch if you're not sure what you're doing.)"));
            EditorGUI.indentLevel--;
            if (_foldoutRefs)
                base.OnInspectorGUI();

            GUILayout.EndVertical();
        }

        private void DrawUtilities()
        {
            GUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.LabelField("Debugger Utilities", EditorStyles.boldLabel);

            if (GUILayout.Button(new GUIContent("Cache Udon Behaviours", "Cache all active Udon Behaviours in the scene.")))
            {
                System.Diagnostics.Stopwatch timer = new System.Diagnostics.Stopwatch();
                timer.Start();

                Component[] udonBehaviours = (Component[])FindObjectsOfType(typeof(UdonBehaviour));
                List<UdonData> oldData = _listData;
                List<UdonData> newData = new List<UdonData>();

                for (int i = 0; i < udonBehaviours.Length; i++)
                {
                    EditorUtility.DisplayProgressBar("NUDebugger", $"Getting Data... ({i}/{udonBehaviours.Length})", (float)i / udonBehaviours.Length);

                    try
                    {
                        List<string> arrayNames;
                        List<string> variableNames;
                        List<string> eventNames;

                        GetProperties((UdonBehaviour)udonBehaviours[i], out arrayNames, out variableNames);
                        GetMethods((UdonBehaviour)udonBehaviours[i], out eventNames);

                        arrayNames.Sort();
                        variableNames.Sort();
                        eventNames.Sort();

                        UdonData data = new UdonData
                        {
                            Udon = (UdonBehaviour)udonBehaviours[i],
                            Arrays = arrayNames,
                            Variables = variableNames,
                            Events = eventNames,
                            Expanded = new bool[4]
                        };

                        newData.Add(data);
                    }
                    catch (Exception e)
                    {
                        if (((UdonBehaviour)udonBehaviours[i]).programSource == null)
                            Debug.LogWarning($"[<color=#00FF9F>NUDebugger</color>] Missing program source on: {udonBehaviours[i].name}", udonBehaviours[i]);
                        else
                            Debug.LogWarning($"[<color=#00FF9F>NUDebugger</color>] Couldn't cache: {udonBehaviours[i].name}\n{e}", udonBehaviours[i]);
                    }
                }

                EditorUtility.DisplayProgressBar("NUDebugger", "Storing Data...", 1);

                newData.Sort();

                for (int i = 0; i < oldData.Count && i < newData.Count; i++)
                    newData[i].Expanded = oldData[i].Expanded;

                Undo.RecordObject(_debugUdon, "Cached all the active Udon Behaviours in the scene.");

                _listData = newData;
                _foldoutData = _listData.Count < 20;

                SetData();

                EditorUtility.ClearProgressBar();

                timer.Stop();
                Debug.Log($"[<color=#00FF9F>NUDebugger</color>] Cached {newData.Count} UdonBehaviours: {timer.Elapsed:mm\\:ss\\.fff}");
            }

            _filterType = (FilterType)EditorGUILayout.EnumPopup(new GUIContent("Filter Type", "Filter used when storing Udon data.\nNone = Store everything.\nPublic = Store data set to public.\nSmart = Try to store user-defined data."), _filterType);

            GUILayout.EndVertical();
        }

        private void DrawBehaviours()
        {
            GUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.LabelField("Debugger Data", EditorStyles.boldLabel);

            EditorGUI.indentLevel++;

            _foldoutData = EditorGUILayout.Foldout(_foldoutData, new GUIContent($"Cached Behaviours [{_listData.Count}]", "Display cached data. (Might be laggy with too many Udon Behaviours.)"));
            if (_foldoutData)
                DrawData();

            EditorGUI.indentLevel--;

            GUILayout.EndVertical();
        }

        private void DrawData()
        {
            for (int i = 0; i < _listData.Count; i++)
            {
                GUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUILayout.BeginHorizontal();

                EditorGUI.BeginChangeCheck();
                _listData[i].Expanded[0] = EditorGUILayout.Foldout(_listData[i].Expanded[0], $"Udon Data:");
                if (EditorGUI.EndChangeCheck())
                {
                    if (_listData[i].Expanded[0])
                        GetData(i);
                }

                EditorGUI.BeginChangeCheck();
                _listData[i].Udon = (UdonBehaviour)EditorGUILayout.ObjectField(_listData[i].Udon, typeof(UdonBehaviour), true);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(_debugUdon, "Changed Udon Behaviour.");

                    SetData(i);
                }

                EditorGUILayout.EndHorizontal();

                if (_listData[i].Expanded[0])
                {
                    EditorGUI.indentLevel++;

                    using (new EditorGUI.DisabledScope(_listData[i].Arrays.Count < 1))
                    {
                        _listData[i].Expanded[1] = EditorGUILayout.Foldout(_listData[i].Expanded[1] && _listData[i].Arrays.Count > 0, $"Array Names [{_listData[i].Arrays.Count}]:");
                        if (_listData[i].Expanded[1])
                        {
                            EditorGUI.indentLevel++;

                            DrawStringList(_listData[i].Arrays, i, "Array:");

                            EditorGUI.indentLevel--;
                        }
                    }

                    using (new EditorGUI.DisabledScope(_listData[i].Variables.Count < 1))
                    {
                        _listData[i].Expanded[2] = EditorGUILayout.Foldout(_listData[i].Expanded[2] && _listData[i].Variables.Count > 0, $"Variable Names [{_listData[i].Variables.Count}]:");
                        if (_listData[i].Expanded[2])
                        {
                            EditorGUI.indentLevel++;

                            DrawStringList(_listData[i].Variables, i, "Variable:");

                            EditorGUI.indentLevel--;
                        }
                    }

                    using (new EditorGUI.DisabledScope(_listData[i].Events.Count < 1))
                    {
                        _listData[i].Expanded[3] = EditorGUILayout.Foldout(_listData[i].Expanded[3] && _listData[i].Events.Count > 0, $"Event Names [{_listData[i].Events.Count}]:");
                        if (_listData[i].Expanded[3])
                        {
                            EditorGUI.indentLevel++;

                            DrawStringList(_listData[i].Events, i, "Event:");

                            EditorGUI.indentLevel--;
                        }
                    }

                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.EndVertical();
            }
        }

        private void DrawStringList(List<string> list, int index, string format)
        {
            for (int i = 0; i < list.Count; i++)
            {
                EditorGUI.BeginChangeCheck();
                list[i] = EditorGUILayout.TextField(string.Format(format, i), list[i]);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(_debugUdon, "Changed data name.");

                    SetData(index);
                }
            }
        }

        #endregion Drawers

        // Custom functions.
        private void GetAssets()
        {
            _iconVRChat = Resources.Load<Texture2D>("Nessie/Icons/VRChat-Emblem-32px");
            _iconGitHub = Resources.Load<Texture2D>("Nessie/Icons/GitHub-Mark-32px");
        }

        private void GetData()
        {
            _listData.Clear();

            for (int i = 0; i < _debugUdon.ArrUdons.Length; i++)
            {
                UdonData data = new UdonData
                {
                    Udon = (UdonBehaviour)_debugUdon.ArrUdons[i],
                    Arrays = _debugUdon.ArrNames[i].ToList(),
                    Variables = _debugUdon.VarNames[i].ToList(),
                    Events = _debugUdon.EntNames[i].ToList(),
                    Expanded = new bool[4]
                };

                _listData.Add(data);
            }
        }
        private void GetData(int index)
        {
            _listData[index].Udon = (UdonBehaviour)_debugUdon.ArrUdons[index];
            _listData[index].Arrays = _debugUdon.ArrNames[index].ToList();
            _listData[index].Variables = _debugUdon.VarNames[index].ToList();
            _listData[index].Events = _debugUdon.EntNames[index].ToList();
        }

        private void SetData()
        {
            Component[] newUdons = new Component[_listData.Count];
            string[][] newArrs = new string[_listData.Count][];
            string[][] newVars = new string[_listData.Count][];
            string[][] newEnts = new string[_listData.Count][];

            for (int i = 0; i < _listData.Count; i++)
            {
                newUdons[i] = _listData[i].Udon;
                newArrs[i] = _listData[i].Arrays.ToArray();
                newVars[i] = _listData[i].Variables.ToArray();
                newEnts[i] = _listData[i].Events.ToArray();
            }

            _debugUdon.ArrUdons = newUdons;
            _debugUdon.ArrNames = newArrs;
            _debugUdon.VarNames = newVars;
            _debugUdon.EntNames = newEnts;
        }
        private void SetData(int index)
        {
            _debugUdon.ArrUdons[index] = _listData[index].Udon;
            _debugUdon.ArrNames[index] = _listData[index].Arrays.ToArray();
            _debugUdon.VarNames[index] = _listData[index].Variables.ToArray();
            _debugUdon.EntNames[index] = _listData[index].Events.ToArray();
        }

        private void GetProperties(UdonBehaviour target, out List<string> arrayNames, out List<string> variableNames)
        {
            VRC.Udon.Common.Interfaces.IUdonSymbolTable symbolTable = target.programSource.SerializedProgramAsset.RetrieveProgram().SymbolTable;

            arrayNames = new List<string>();
            variableNames = new List<string>();

            string[] propertyNames = symbolTable.GetSymbols().ToArray();

            foreach (string name in propertyNames)
            {
                bool isArray = symbolTable.GetSymbolType(name).IsArray;

                // Check to see if the property is internal so it doesn't clog the debugger.
                bool storeProperty = true;

                if (_filterType == FilterType.Public)
                {
                    bool isProperty = symbolTable.HasExportedSymbol(name);
                    storeProperty = isProperty;
                }
                else if (_filterType == FilterType.Smart) // Ignore property names that start with "__" or end with "_number/letter". Internal variables now start with "__", so eventually this can be switched out to String.StartsWith().
                {
                    Group result = Regex.Match(name, @"(^__|_(\d+|\w)$)");
                    storeProperty = !result.Success;
                }

                if (storeProperty)
                {
                    if (isArray)
                        arrayNames.Add(name);
                    else
                        variableNames.Add(name);
                }
            }
        }

        private void GetMethods(UdonBehaviour target, out List<string> methodNames)
        {
            // Thank you Vowgan and Varneon for pointing me in the right direction when I was trying to getting Udon methods. ^^
            methodNames = target.programSource.SerializedProgramAsset.RetrieveProgram().EntryPoints.GetExportedSymbols().ToList();
        }
    }
}
