
using UnityEngine;
using VRC.Udon;
using VRC.Udon.Editor.ProgramSources.UdonGraphProgram;
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
            public int ProgramIndex;
            public string ProgramID;
            public List<string> Arrays;
            public List<string> Variables;
            public List<string> Events;
            public bool[] Expanded;

            public int CompareTo(UdonData comparePart)
            {
                return comparePart == null ? 1 : Udon.name.CompareTo(comparePart.Udon.name);
            }
        }

        public class UniqueSolution
        {
            public List<string> SolutionStrings;
            public List<bool> SolutionConditions;

            public List<string> GetIncludes()
            {
                List<string> includes = new List<string>();
                for (int i = 0; i < SolutionConditions.Count; i++)
                    if (SolutionConditions[i])
                        includes.Add(SolutionStrings[i]);
                return includes;
            }

            public List<string> GetExcludes()
            {
                List<string> excludes = new List<string>();
                for (int i = 0; i < SolutionConditions.Count; i++)
                    if (!SolutionConditions[i])
                        excludes.Add(SolutionStrings[i]);
                return excludes;
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

                List<UdonBehaviour> udonBehaviours = GetUdonBehavioursInScene();
                GetUdonBehaviourDependencies(udonBehaviours, out List <UdonGraphProgramAsset> graphAssets, out List<UdonSharpProgramAsset> sharpAssets);
                GetUdonAssetIDs(graphAssets, out List<UniqueSolution> graphIDs);
                GetUdonAssetIDs(sharpAssets, out List<long> sharpIDs);

                string[] programNames = new string[graphAssets.Count + sharpAssets.Count];
                string[][] graphSolutions = new string[graphIDs.Count][];
                bool[][] graphConditions = new bool[graphIDs.Count][];
                long[] sharpSolutions = sharpIDs.ToArray();

                for (int i = 0; i < programNames.Length; i++)
                    programNames[i] = i < graphAssets.Count ? graphAssets[i].name : sharpAssets[i - graphAssets.Count].name;

                for (int i = 0; i < graphIDs.Count; i++)
                {
                    graphSolutions[i] = graphIDs[i].SolutionStrings.ToArray();
                    graphConditions[i] = graphIDs[i].SolutionConditions.ToArray();

                    List<string> incs = graphIDs[i].GetIncludes();
                    List<string> excs = graphIDs[i].GetExcludes();

                    string str = string.Format("Asset {0} [{1}]:", i < graphAssets.Count ? graphAssets[i].name : sharpAssets[i - graphAssets.Count].name, incs.Count + excs.Count);
                    
                    str += $"\nIncludes: ";
                    foreach (string include in incs)
                        str += $" {include}";

                    str += "\n Excludes: ";
                    foreach (string exclude in excs)
                        str += $" {exclude}";

                    Debug.Log(str, graphAssets[i]);
                }

                _debugUdon.ProgramNames = programNames;
                _debugUdon.GraphSolutions = graphSolutions;
                _debugUdon.GraphConditions = graphConditions;
                _debugUdon.SharpIDs = sharpSolutions;

                List<UdonData> oldData = _listData;
                List<UdonData> newData = new List<UdonData>();

                int[] programIndecies = new int[udonBehaviours.Count];
                for (int i = 0; i < udonBehaviours.Count; i++)
                {
                    EditorUtility.DisplayProgressBar("NUDebugger", $"Getting Data... ({i}/{udonBehaviours.Count})", (float)i / udonBehaviours.Count);

                    try
                    {
                        List<string> arrayNames;
                        List<string> variableNames;
                        List<string> eventNames;

                        AbstractUdonProgramSource program = udonBehaviours[i].programSource;
                        if (program is UdonGraphProgramAsset)
                            programIndecies[i] = graphAssets.IndexOf((UdonGraphProgramAsset)program);
                        else if (program is UdonSharpProgramAsset)
                            programIndecies[i] = sharpAssets.IndexOf((UdonSharpProgramAsset)program) + graphAssets.Count;
                        else
                            programIndecies[i] = -1;

                        GetProperties(udonBehaviours[i], out arrayNames, out variableNames);
                        GetMethods(udonBehaviours[i], out eventNames);

                        arrayNames.Sort();
                        variableNames.Sort();
                        eventNames.Sort();

                        UdonData data = new UdonData
                        {
                            Udon = udonBehaviours[i],
                            Arrays = arrayNames,
                            Variables = variableNames,
                            Events = eventNames,
                            Expanded = new bool[4]
                        };

                        newData.Add(data);
                    }
                    catch (Exception e)
                    {
                        if (udonBehaviours[i].programSource == null)
                            Debug.LogWarning($"[<color=#00FF9F>NUDebugger</color>] Missing program source on: {udonBehaviours[i].name}", udonBehaviours[i]);
                        else
                            Debug.LogWarning($"[<color=#00FF9F>NUDebugger</color>] Couldn't cache: {udonBehaviours[i].name}\n{e}", udonBehaviours[i]);
                    }
                }

                _debugUdon.ProgramIndecies = programIndecies;

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

        private void GetProperties(UdonBehaviour udon, out List<string> arrayNames, out List<string> variableNames)
        {
            VRC.Udon.Common.Interfaces.IUdonSymbolTable symbolTable = udon.programSource.SerializedProgramAsset.RetrieveProgram().SymbolTable;

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





        private void GetUdonBehaviourDependencies(List<UdonBehaviour> udonBehaviours, out List<UdonGraphProgramAsset> graphAssets, out List<UdonSharpProgramAsset> sharpAssets)
        {
            graphAssets = new List<UdonGraphProgramAsset>();
            sharpAssets = new List<UdonSharpProgramAsset>();

            UnityEngine.Object[] dependencies = udonBehaviours.ToArray();
            dependencies = EditorUtility.CollectDependencies(dependencies);
            foreach (UnityEngine.Object dependency in dependencies)
            {
                if (dependency is UdonGraphProgramAsset graphAsset)
                    graphAssets.Add(graphAsset);
                else if (dependency is UdonSharpProgramAsset sharpAsset)
                    sharpAssets.Add(sharpAsset);
            }
        }

        private List<UdonBehaviour> GetUdonBehavioursInScene()
        {
            List<UdonBehaviour> udonBehavioursInScene = new List<UdonBehaviour>();
            int curentSceneHandle = UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle;

            foreach (UdonBehaviour ub in Resources.FindObjectsOfTypeAll<UdonBehaviour>())
            {
                if (ub.gameObject.scene.handle == curentSceneHandle)
                    if (!EditorUtility.IsPersistent(ub.transform.root.gameObject) && !(ub.hideFlags == HideFlags.NotEditable || ub.hideFlags == HideFlags.HideAndDontSave))
                        udonBehavioursInScene.Add(ub);
            }

            return udonBehavioursInScene;
        }

        private void GetUdonAssetIDs(List<UdonGraphProgramAsset> graphAssets, out List<UniqueSolution> udonGraphIDs)
        {
            udonGraphIDs = new List<UniqueSolution>();
            List<List<string>> udonGraphSymbols = new List<List<string>>();
            for (int i = 0; i < graphAssets.Count; i++)
            {
                List<string> symbols = graphAssets[i].SerializedProgramAsset.RetrieveProgram().SymbolTable.GetSymbols().ToList();
                udonGraphSymbols.Add(symbols);
            }

            udonGraphIDs = GetSolutions(udonGraphSymbols);
        }

        private void GetUdonAssetIDs(List<UdonSharpProgramAsset> sharpAssets, out List<long> udonSharpIDs)
        {
            udonSharpIDs = new List<long>();
            foreach (UdonSharpProgramAsset asset in sharpAssets)
            {
                long typeID = UdonSharp.Internal.UdonSharpInternalUtility.GetTypeID(asset.sourceCsScript.GetClass());
                udonSharpIDs.Add(typeID);
                Debug.Log($"{asset} type ID: {typeID}", asset);
            }
        }

        private List<UniqueSolution> GetSolutions(List<List<string>> data)
        {
            List<UniqueSolution> solutions = new List<UniqueSolution>();
            List<string> occurrences = new List<string>();

            foreach (List<string> list in data)
                occurrences = occurrences.Concat(list).ToList();

            occurrences = occurrences.GroupBy(x => x)
                  .OrderByDescending(g => g.Count())
                  .SelectMany(g => g).Distinct().ToList();

            for (int listIndex = 0; listIndex < data.Count; listIndex++)
            {
                List<string> occurrencesFiltered = occurrences.Intersect(data[listIndex]).Reverse().ToList();
                List<string> occurrencesOthersFiltered = occurrences.Except(data[listIndex]).ToList();

                for (int stringIndex = 0; stringIndex < occurrencesFiltered.Count; stringIndex++)
                {
                    List<string> solution = new List<string>() { occurrencesFiltered[stringIndex] };
                    for (int againstIndex = 0; againstIndex < data.Count; againstIndex++)
                    {
                        if (againstIndex == listIndex) continue;
                        if (data[listIndex].Count == data[againstIndex].Count && data[listIndex].All(data[againstIndex].Contains)) continue;
                        if (data[againstIndex].Contains(occurrencesFiltered[stringIndex]))
                        {
                            List<string> include = occurrencesFiltered.Except(data[againstIndex]).ToList();
                            List<string> exclude = occurrencesOthersFiltered.Intersect(data[againstIndex]).ToList();

                            string conditions = exclude.Count > 0 ? exclude[0] : include[0];

                            solution.Add(conditions);
                        }
                    }

                    List<string> finalResult = occurrences.Intersect(solution).ToList();
                    List<string> solutionStrings = new List<string>();
                    List<bool> solutionConditions = new List<bool>();

                    string log = $"List ({listIndex}): Solution ({stringIndex})";
                    foreach (string str in finalResult)
                    {
                        solutionStrings.Add(str);
                        if (data[listIndex].Contains(str))
                        {
                            solutionConditions.Add(true);
                            log += $"\nInclude: {str}";
                        }
                        else
                        {
                            solutionConditions.Add(false);
                            log += $"\nExclude: {str}";
                        }
                    }

                    UniqueSolution newSolution = new UniqueSolution();
                    newSolution.SolutionStrings = solutionStrings;
                    newSolution.SolutionConditions = solutionConditions;
                    solutions.Add(newSolution);

                    break;
                }
            }
            return solutions;
        }
    }
}
