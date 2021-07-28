
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

        private bool m_refs;

        public FilterType m_filterType = FilterType.Smart;

        public List<UdonData> m_listData = new List<UdonData>();

        private NUDebugger m_debugUdon;

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
                if (comparePart == null)
                    return 1;
                else
                    return Udon.name.CompareTo(comparePart.Udon.name);
            }
        }

        private void OnEnable()
        {
            _iconVRChat = Resources.Load<Texture2D>("Nessie/Icons/VRChat-Emblem-32px");
            _iconGitHub = Resources.Load<Texture2D>("Nessie/Icons/GitHub-Mark-32px");

            m_debugUdon = (NUDebugger)target;

            GetData();
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
            Color mainColor = EditorGUILayout.ColorField(new GUIContent("Main Color", "Color used for UI text and icon elements."), m_debugUdon._mainColor);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(m_debugUdon, "Changed Main Color.");
                m_debugUdon._mainColor = mainColor;
            }

            EditorGUI.BeginChangeCheck();
            Color crashColor = EditorGUILayout.ColorField(new GUIContent("Crash Color", "Color used to indicate if an UdonBehaviour has crashed."), m_debugUdon._crashColor);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(m_debugUdon, "Changed Crash Color.");
                m_debugUdon._crashColor = crashColor;
            }

            EditorGUI.BeginChangeCheck();
            float updateRate = EditorGUILayout.FloatField(new GUIContent("Update Rate", "Delay in seconds between debugger updates."), m_debugUdon._updateRate);
            if (EditorGUI.EndChangeCheck())
            {
                if (updateRate <= 0)
                    updateRate = 0;

                Undo.RecordObject(m_debugUdon, "Changed Update Rate.");
                m_debugUdon._updateRate = updateRate;
            }

            EditorGUI.BeginChangeCheck();
            bool networked = EditorGUILayout.Toggle(new GUIContent("Networked", "Boolean used to decide if an event call should be networked or not."), m_debugUdon._networked);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(m_debugUdon, "Changed Update Rate.");
                m_debugUdon._networked = networked;
            }

            EditorGUILayout.Space();

            EditorGUI.indentLevel++;
            m_refs = EditorGUILayout.Foldout(m_refs, new GUIContent("References", "References used by the Udon Debugger. (Don't touch if you're not sure what you're doing.)"));
            EditorGUI.indentLevel--;
            if (m_refs)
                base.OnInspectorGUI();

            GUILayout.EndVertical();
        }

        private void DrawUtilities()
        {
            GUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.LabelField("Debugger Utilities", EditorStyles.boldLabel);

            if (GUILayout.Button(new GUIContent("Cache Udon Behaviours", "Cache all active Udon Behaviours in the scene.")))
            {
                Component[] udonBehaviours = (Component[])FindObjectsOfType(typeof(UdonBehaviour));
                List<UdonData> oldData = m_listData;
                List<UdonData> newData = new List<UdonData>();

                for (int i = 0; i < udonBehaviours.Length; i++)
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

                newData.Sort();

                for (int i = 0; i < oldData.Count && i < newData.Count; i++)
                    newData[i].Expanded = oldData[i].Expanded;

                Undo.RecordObject(m_debugUdon, "Cached all the active Udon Behaviours in the scene.");

                m_listData = newData;

                SetData();
            }

            m_filterType = (FilterType)EditorGUILayout.EnumPopup(new GUIContent("Filter Type", "Filter used when storing Udon data.\nNone = Store everything.\nPublic = Store data set to public.\nSmart = Try to store user-defined data."), m_filterType);

            GUILayout.EndVertical();
        }

        private void DrawBehaviours()
        {
            GUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.LabelField("Debugger Data", EditorStyles.boldLabel);

            EditorGUI.indentLevel++;

            DrawData();

            EditorGUI.indentLevel--;

            GUILayout.EndVertical();
        }

        private void DrawData()
        {
            for (int i = 0; i < m_listData.Count; i++)
            {
                GUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUILayout.BeginHorizontal();

                EditorGUI.BeginChangeCheck();
                m_listData[i].Expanded[0] = EditorGUILayout.Foldout(m_listData[i].Expanded[0], $"Udon Data:");
                if (EditorGUI.EndChangeCheck())
                {
                    if (m_listData[i].Expanded[0])
                        GetData(i);
                }

                EditorGUI.BeginChangeCheck();
                m_listData[i].Udon = (UdonBehaviour)EditorGUILayout.ObjectField(m_listData[i].Udon, typeof(UdonBehaviour), true);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(m_debugUdon, "Changed Udon Behaviour.");

                    SetData(i);
                }

                EditorGUILayout.EndHorizontal();

                if (m_listData[i].Expanded[0])
                {
                    EditorGUI.indentLevel++;

                    using (new EditorGUI.DisabledScope(m_listData[i].Arrays.Count < 1))
                    {
                        m_listData[i].Expanded[1] = EditorGUILayout.Foldout(m_listData[i].Expanded[1] && m_listData[i].Arrays.Count > 0, $"Array Names [{m_listData[i].Arrays.Count}]:");
                        if (m_listData[i].Expanded[1])
                        {
                            EditorGUI.indentLevel++;

                            DrawStringList(m_listData[i].Arrays, i, "Array:");

                            EditorGUI.indentLevel--;
                        }
                    }

                    using (new EditorGUI.DisabledScope(m_listData[i].Variables.Count < 1))
                    {
                        m_listData[i].Expanded[2] = EditorGUILayout.Foldout(m_listData[i].Expanded[2] && m_listData[i].Variables.Count > 0, $"Variable Names [{m_listData[i].Variables.Count}]:");
                        if (m_listData[i].Expanded[2])
                        {
                            EditorGUI.indentLevel++;

                            DrawStringList(m_listData[i].Variables, i, "Variable:");

                            EditorGUI.indentLevel--;
                        }
                    }

                    using (new EditorGUI.DisabledScope(m_listData[i].Events.Count < 1))
                    {
                        m_listData[i].Expanded[3] = EditorGUILayout.Foldout(m_listData[i].Expanded[3] && m_listData[i].Events.Count > 0, $"Event Names [{m_listData[i].Events.Count}]:");
                        if (m_listData[i].Expanded[3])
                        {
                            EditorGUI.indentLevel++;

                            DrawStringList(m_listData[i].Events, i, "Event:");

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
                    Undo.RecordObject(m_debugUdon, "Changed data name.");

                    SetData(index);
                }
            }
        }

        #endregion Drawers

        // Custom functions.
        private void GetAssets()
        {

        }

        private void GetData()
        {
            m_listData.Clear();

            for (int i = 0; i < m_debugUdon.ArrUdons.Length; i++)
            {
                UdonData data = new UdonData
                {
                    Udon = (UdonBehaviour)m_debugUdon.ArrUdons[i],
                    Arrays = m_debugUdon.ArrNames[i].ToList(),
                    Variables = m_debugUdon.VarNames[i].ToList(),
                    Events = m_debugUdon.EntNames[i].ToList(),
                    Expanded = new bool[4]
                };

                m_listData.Add(data);
            }
        }
        private void GetData(int index)
        {
            m_listData[index].Udon = (UdonBehaviour)m_debugUdon.ArrUdons[index];
            m_listData[index].Arrays = m_debugUdon.ArrNames[index].ToList();
            m_listData[index].Variables = m_debugUdon.VarNames[index].ToList();
            m_listData[index].Events = m_debugUdon.EntNames[index].ToList();
        }

        private void SetData()
        {
            Component[] newUdons = new Component[m_listData.Count];
            string[][] newArrs = new string[m_listData.Count][];
            string[][] newVars = new string[m_listData.Count][];
            string[][] newEnts = new string[m_listData.Count][];

            for (int i = 0; i < m_listData.Count; i++)
            {
                newUdons[i] = m_listData[i].Udon;
                newArrs[i] = m_listData[i].Arrays.ToArray();
                newVars[i] = m_listData[i].Variables.ToArray();
                newEnts[i] = m_listData[i].Events.ToArray();
            }

            m_debugUdon.ArrUdons = newUdons;
            m_debugUdon.ArrNames = newArrs;
            m_debugUdon.VarNames = newVars;
            m_debugUdon.EntNames = newEnts;
        }
        private void SetData(int index)
        {
            m_debugUdon.ArrUdons[index] = m_listData[index].Udon;
            m_debugUdon.ArrNames[index] = m_listData[index].Arrays.ToArray();
            m_debugUdon.VarNames[index] = m_listData[index].Variables.ToArray();
            m_debugUdon.EntNames[index] = m_listData[index].Events.ToArray();
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

                if (m_filterType == FilterType.Public)
                {
                    bool isProperty = symbolTable.HasExportedSymbol(name);
                    storeProperty = isProperty;
                }
                else if (m_filterType == FilterType.Smart) // Ignore property names that start with "__" or end with "_number/letter". Internal variables now start with "__", so eventually this can be switched out to String.StartsWith().
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
