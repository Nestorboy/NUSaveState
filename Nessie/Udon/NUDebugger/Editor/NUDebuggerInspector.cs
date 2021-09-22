
using UnityEngine;
using VRC.Udon;
using VRC.Udon.Editor.ProgramSources.UdonGraphProgram;
using System;
using UnityEditor;
using UnityEditorInternal;
using UdonSharpEditor;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;

namespace UdonSharp.Nessie.Debugger.Internal
{
    [CustomEditor(typeof(NUDebugger))]
    internal class NUDebuggerInspector : Editor
    {
        private NUDebugger _udonDebugger;
        private List<ProgramData> _listPrograms = new List<ProgramData>();
        private List<UdonData> _listData = new List<UdonData>();

        private bool _foldoutRefs;
        private bool _foldoutData;

        private FilterType _filterType = FilterType.Smart;

        private Texture2D _iconGitHub;
        private Texture2D _iconVRChat;

        private SerializedObject so;
        private SerializedProperty settingMainColor;
        private SerializedProperty settingCrashColor;
        private SerializedProperty settingUpdateRate;
        private SerializedProperty settingNetworked;

        [Flags]
        public enum FilterType
        {
            None,
            Public,
            Smart
        }

        [Serializable]
        public class ProgramData
        {
            public string Name;
            public List<string> Arrays;
            public List<string> Variables;
            public List<string> Events;
            public bool[] Expanded;
        }

        public class UdonData : IComparable<UdonData>
        {
            public UdonBehaviour Udon;
            public int ProgramIndex;

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

            _udonDebugger = (NUDebugger)target;

            GetAssets();

            GetData();

            so = new SerializedObject(target);
            settingMainColor = so.FindProperty(nameof(NUDebugger.MainColor));
            settingCrashColor = so.FindProperty(nameof(NUDebugger.CrashColor));
            settingUpdateRate = so.FindProperty(nameof(NUDebugger.UpdateRate));
            settingNetworked = so.FindProperty(nameof(NUDebugger.Networked));
        }

        public override void OnInspectorGUI()
        {
            // Draws the default convert to UdonBehaviour button, program asset field, sync settings, etc.
            if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target)) return;

            GetData();

            so.Update();

            GUILayout.BeginVertical(EditorStyles.helpBox);

            DrawBanner();

            EditorGUILayout.Space();

            DrawSettings();

            EditorGUILayout.Space();

            DrawUtilities();

            EditorGUILayout.Space();

            DrawBehaviours();

            GUILayout.EndVertical();

            so.ApplyModifiedProperties();
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

            EditorGUILayout.PropertyField(settingMainColor);
            EditorGUILayout.PropertyField(settingCrashColor);
            EditorGUILayout.PropertyField(settingUpdateRate);
            EditorGUILayout.PropertyField(settingNetworked);

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

            if (GUILayout.Button(new GUIContent("Cache Udon Programs", "Cache all Udon Program dependencies of the scene.")))
            {
                CacheUdonPrograms();
            }

            _filterType = (FilterType)EditorGUILayout.EnumPopup(new GUIContent("Filter Type", "Filter used when storing Program data.\nNone = Store everything.\nPublic = Store data set to public.\nSmart = Try to store user-defined data."), _filterType);

            GUILayout.EndVertical();
        }

        private void DrawBehaviours()
        {
            GUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.LabelField("Debugger Data", EditorStyles.boldLabel);

            EditorGUI.indentLevel++;

            GUILayout.BeginHorizontal();

            using (new EditorGUI.DisabledScope(_listPrograms.Count < 1))
            {
                _foldoutData = EditorGUILayout.Foldout(_foldoutData && _listPrograms.Count > 0, new GUIContent($"Cached Programs [{_listPrograms.Count}]", "Display cached data. (Might be laggy with too many Programs.)"));

                if (GUILayout.Button(new GUIContent("Clear Data", "Remove all the data stored in the Udon Debugger.")))
                {
                    Undo.RecordObject(_udonDebugger, "Cleared all the cached Program data.");

                    _listPrograms.Clear();
                    _listData.Clear();

                    SetData();

                    Debug.Log("[<color=#00FF9F>NUDebugger</color>] Cleared cached Program data.");
                }
            }

            GUILayout.EndHorizontal();

            if (_foldoutData)
                DrawData();

            EditorGUI.indentLevel--;

            GUILayout.EndVertical();
        }

        private void DrawData()
        {
            for (int i = 0; i < _listPrograms.Count; i++)
            {
                GUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUILayout.BeginHorizontal();

                EditorGUI.BeginChangeCheck();
                _listPrograms[i].Expanded[0] = EditorGUILayout.Foldout(_listPrograms[i].Expanded[0], $"Program:");
                if (EditorGUI.EndChangeCheck())
                {
                    if (_listPrograms[i].Expanded[0])
                        GetData(i);
                }

                EditorGUI.BeginChangeCheck();
                _listPrograms[i].Name = EditorGUILayout.TextField(_listPrograms[i].Name);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(_udonDebugger, "Changed Program Name.");

                    SetData(i);
                }

                EditorGUILayout.EndHorizontal();

                if (_listPrograms[i].Expanded[0])
                {
                    EditorGUI.indentLevel++;

                    using (new EditorGUI.DisabledScope(_listPrograms[i].Arrays.Count < 1))
                    {
                        _listPrograms[i].Expanded[1] = EditorGUILayout.Foldout(_listPrograms[i].Expanded[1] && _listPrograms[i].Arrays.Count > 0, $"Array Names [{_listPrograms[i].Arrays.Count}]:");
                        if (_listPrograms[i].Expanded[1])
                        {
                            EditorGUI.indentLevel++;

                            DrawStringList(_listPrograms[i].Arrays, i, "Array:");

                            EditorGUI.indentLevel--;
                        }
                    }

                    using (new EditorGUI.DisabledScope(_listPrograms[i].Variables.Count < 1))
                    {
                        _listPrograms[i].Expanded[2] = EditorGUILayout.Foldout(_listPrograms[i].Expanded[2] && _listPrograms[i].Variables.Count > 0, $"Variable Names [{_listPrograms[i].Variables.Count}]:");
                        if (_listPrograms[i].Expanded[2])
                        {
                            EditorGUI.indentLevel++;

                            DrawStringList(_listPrograms[i].Variables, i, "Variable:");

                            EditorGUI.indentLevel--;
                        }
                    }

                    using (new EditorGUI.DisabledScope(_listPrograms[i].Events.Count < 1))
                    {
                        _listPrograms[i].Expanded[3] = EditorGUILayout.Foldout(_listPrograms[i].Expanded[3] && _listPrograms[i].Events.Count > 0, $"Event Names [{_listPrograms[i].Events.Count}]:");
                        if (_listPrograms[i].Expanded[3])
                        {
                            EditorGUI.indentLevel++;

                            DrawStringList(_listPrograms[i].Events, i, "Event:");

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
                    Undo.RecordObject(_udonDebugger, "Changed data name.");

                    SetData(index);
                }
            }
        }

        #endregion Drawers

        #region Data Methods

        private void GetAssets()
        {
            _iconVRChat = Resources.Load<Texture2D>("Nessie/Icons/VRChat-Emblem-32px");
            _iconGitHub = Resources.Load<Texture2D>("Nessie/Icons/GitHub-Mark-32px");
        }

        private void GetData()
        {
            try
            {
                _listData.Clear();

                for (int i = 0; i < _udonDebugger.DataUdons.Length; i++)
                {
                    UdonData data = new UdonData
                    {
                        Udon = (UdonBehaviour)_udonDebugger.DataUdons[i],
                        ProgramIndex = _udonDebugger.ProgramIndecies[i]
                    };

                    _listData.Add(data);
                }

                List<ProgramData> oldPrograms = _listPrograms.ToList();

                _listPrograms.Clear();

                for (int i = 0; i < _udonDebugger.ProgramNames.Length; i++)
                {
                    ProgramData program = new ProgramData
                    {
                        Name = _udonDebugger.ProgramNames[i],
                        Arrays = _udonDebugger.DataArrays[i].ToList(),
                        Variables = _udonDebugger.DataVariables[i].ToList(),
                        Events = _udonDebugger.DataEvents[i].ToList(),
                    };

                    if (i < oldPrograms.Count)
                        program.Expanded = oldPrograms[i].Expanded;
                    else
                        program.Expanded = new bool[4];

                    _listPrograms.Add(program);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Unable to retrieve data.\n{e}");
            }
        }
        private void GetData(int index)
        {
            try
            {
                _listData[index].Udon = (UdonBehaviour)_udonDebugger.DataUdons[index];
                _listData[index].ProgramIndex = _udonDebugger.ProgramIndecies[index];

                _listPrograms[index].Name = _udonDebugger.ProgramNames[index];
                _listPrograms[index].Arrays = _udonDebugger.DataArrays[index].ToList();
                _listPrograms[index].Variables = _udonDebugger.DataVariables[index].ToList();
                _listPrograms[index].Events = _udonDebugger.DataEvents[index].ToList();
            }
            catch (Exception e)
            {
                Debug.LogError($"Unable to retrieve data.\n{e}");
            }
        }

        private void SetData()
        {
            Component[] newUdons = new Component[_listData.Count];
            int[] newProgramIDs = new int[_listData.Count];

            for (int i = 0; i < _listData.Count; i++)
            {
                newUdons[i] = _listData[i].Udon;
                newProgramIDs[i] = _listData[i].ProgramIndex;
            }

            _udonDebugger.DataUdons = newUdons;
            _udonDebugger.ProgramIndecies = newProgramIDs;

            _udonDebugger.ProgramNames = _listPrograms.Select(a => a.Name).ToArray();
            _udonDebugger.DataArrays = _listPrograms.Select(a => a.Arrays.ToArray()).ToArray();
            _udonDebugger.DataVariables = _listPrograms.Select(a => a.Variables.ToArray()).ToArray();
            _udonDebugger.DataEvents = _listPrograms.Select(a => a.Events.ToArray()).ToArray();
        }
        private void SetData(int index)
        {
            _udonDebugger.DataUdons[index] = _listData[index].Udon;
            _udonDebugger.ProgramIndecies[index] = _listData[index].ProgramIndex;

            _udonDebugger.ProgramNames[index] = _listPrograms[index].Name;
            _udonDebugger.DataArrays[index] = _listPrograms[index].Arrays.ToArray();
            _udonDebugger.DataVariables[index] = _listPrograms[index].Variables.ToArray();
            _udonDebugger.DataEvents[index] = _listPrograms[index].Events.ToArray();
        }

        #endregion Data Methods

        #region Udon Methods

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

                #pragma warning disable CS0162 // Account for early break.

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

                            string conditionStrings = exclude.Count > 0 ? exclude[0] : include[0];

                            solution.Add(conditionStrings);
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

                    break; // Early break since we only need the first/simplest solution.
                    Debug.Log(log);
                }

                #pragma warning restore CS0162
            }
            return solutions;
        }

        private void CacheUdonPrograms()
        {
            System.Diagnostics.Stopwatch timer = new System.Diagnostics.Stopwatch();
            timer.Start();

            List<UdonBehaviour> udonBehaviours = GetUdonBehavioursInScene();
            GetUdonBehaviourDependencies(udonBehaviours, out List<UdonGraphProgramAsset> graphAssets, out List<UdonSharpProgramAsset> sharpAssets);
            GetUdonAssetIDs(graphAssets, out List<UniqueSolution> graphIDs);
            GetUdonAssetIDs(sharpAssets, out List<long> sharpIDs);

            ProgramData[] newPrograms = new ProgramData[graphAssets.Count + sharpAssets.Count];

            for (int i = 0; i < newPrograms.Length; i++)
            {
                EditorUtility.DisplayProgressBar("NUDebugger", $"Caching Program Data... ({i}/{newPrograms.Length})", (float)i / newPrograms.Length);

                AbstractUdonProgramSource udonProgram;
                if (i < graphAssets.Count)
                    udonProgram = graphAssets[i];
                else
                    udonProgram = sharpAssets[i - graphAssets.Count];

                GetProperties(udonProgram, out List<string> arrayNames, out List<string> variableNames);
                GetMethods(udonProgram, out List<string> eventNames);

                arrayNames.Sort();
                variableNames.Sort();
                eventNames.Sort();

                ProgramData newProgram = new ProgramData
                {
                    Name = udonProgram.name,
                    Arrays = arrayNames,
                    Variables = variableNames,
                    Events = eventNames,
                    Expanded = new bool[4]
                };

                newPrograms[i] = newProgram;
            }

            string[][] graphSolutions = new string[graphIDs.Count][];
            bool[][] graphConditions = new bool[graphIDs.Count][];
            long[] sharpSolutions = sharpIDs.ToArray();

            for (int i = 0; i < graphIDs.Count; i++)
            {
                graphSolutions[i] = graphIDs[i].SolutionStrings.ToArray();
                graphConditions[i] = graphIDs[i].SolutionConditions.ToArray();
            }

            List<UdonData> newData = new List<UdonData>();

            for (int i = 0; i < udonBehaviours.Count; i++)
            {
                EditorUtility.DisplayProgressBar("NUDebugger", $"Setting up Program references... ({i}/{udonBehaviours.Count})", (float)i / udonBehaviours.Count);

                try
                {
                    int programIndex;
                    AbstractUdonProgramSource program = udonBehaviours[i].programSource;
                    if (program is UdonGraphProgramAsset)
                        programIndex = graphAssets.IndexOf((UdonGraphProgramAsset)program);
                    else if (program is UdonSharpProgramAsset)
                        programIndex = sharpAssets.IndexOf((UdonSharpProgramAsset)program) + graphAssets.Count;
                    else
                        continue;

                    UdonData data = new UdonData
                    {
                        Udon = udonBehaviours[i],
                        ProgramIndex = programIndex
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

            EditorUtility.DisplayProgressBar("NUDebugger", "Storing Data...", 1);

            newData.Sort();

            Undo.RecordObject(_udonDebugger, "Cached all the Udon program dependencies of the scene.");

            _listPrograms = newPrograms.ToList();
            _listData = newData;

            _udonDebugger.GraphSolutions = graphSolutions;
            _udonDebugger.GraphConditions = graphConditions;
            _udonDebugger.SharpIDs = sharpSolutions;

            SetData();

            timer.Stop();
            Debug.Log($"[<color=#00FF9F>NUDebugger</color>] Cached {_listPrograms.Count} Programs: {timer.Elapsed:mm\\:ss\\.fff}");

            EditorUtility.ClearProgressBar();
        }

        private void GetProperties(AbstractUdonProgramSource udonProgram, out List<string> arrayNames, out List<string> variableNames)
        {
            VRC.Udon.Common.Interfaces.IUdonSymbolTable symbolTable = udonProgram.SerializedProgramAsset.RetrieveProgram().SymbolTable;

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

        private void GetMethods(AbstractUdonProgramSource udonProgram, out List<string> methodNames)
        {
            // Thank you Vowgan and Varneon for pointing me in the right direction when I was trying to getting Udon methods. ^^
            methodNames = udonProgram.SerializedProgramAsset.RetrieveProgram().EntryPoints.GetExportedSymbols().ToList();
        }

        #endregion Udon Methods
    }
}
