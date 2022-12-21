#if UNITY_EDITOR

using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using VRC.Udon;
using Nessie.Udon.Extensions;

namespace Nessie.Udon.SaveState
{
    [AddComponentMenu(""), DisallowMultipleComponent]
    public class NUSaveStateData : MonoBehaviour
    {
        #region Public Classes

        [Serializable]
        public class Preferences
        {
            [SerializeField] private DefaultAsset folderAsset;
            [SerializeField] private string folderPath;
            public DefaultAsset Folder
            {
                get
                {
                    if (folderAsset == null)
                    {
                        if (folderPath != null)
                        {
                            folderAsset = AssetDatabase.LoadAssetAtPath<DefaultAsset>(folderPath);
                        }
                    }
                    else if (AssetDatabase.GetAssetPath(folderAsset) != folderPath)
                    {
                        folderPath = AssetDatabase.GetAssetPath(folderAsset);
                    }

                    return folderAsset;
                }
                set
                {
                    string newPath = AssetDatabase.GetAssetPath(value);
                    if (value == null || System.IO.Directory.Exists(newPath)) // Simple fix to prevent non-folders from being assigned.
                    {
                        folderAsset = value;
                        folderPath = newPath;
                    }
                }
            }

            public int FolderIndex = -1;

            public string Seed = "";
            public string Parameter = "";
        }

        [Serializable]
        public class Instruction
        {
            private static readonly Type[] allowedTypes = new Type[]
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
                typeof(bool), // Special Case
                typeof(Vector2),
                typeof(Vector3),
                typeof(Vector4),
                typeof(Vector2Int),
                typeof(Vector3Int),
                typeof(Quaternion),
                typeof(Color),
                typeof(Color32),
            };
            private static readonly int[] allowedTypesBits = new int[]
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
                64,
                128,
                1, // Special Case
                64,
                96,
                128,
                64,
                96,
                128,
                128,
                32,
            };

            [SerializeField] private UdonBehaviour udon;
            public UdonBehaviour Udon
            {
                get => udon;
                set
                {
                    udon = value;

                    Variables = value != null ? value.GetFilteredVariables(allowedTypes, ~NUExtensions.VariableType.Internal).ToArray() : new NUExtensions.Variable[0];
                    VariableLabels = PrepareLabels(Variables);
                    VariableIndex = Array.IndexOf(Variables, Variable);
                }
            }

            public NUExtensions.Variable[] Variables;
            public NUExtensions.Variable Variable;

            [SerializeField] private int variableIndex = -1;
            public int VariableIndex
            {
                get => variableIndex;
                set
                {
                    variableIndex = value;

                    if (value >= 0)
                    {
                        Variable = Variables[value];
                        BitCount = allowedTypesBits[Array.IndexOf(allowedTypes, Variable.Type)];
                    }
                    else
                    {
                        Variable = new NUExtensions.Variable();
                        BitCount = 0;
                    }
                }
            }

            public int BitCount;

            public string[] VariableLabels = new string[0];
            private string[] PrepareLabels(NUExtensions.Variable[] variables)
            {
                string[] variableLabels = new string[variables.Length];

                for (int i = 0; i < variableLabels.Length; i++)
                {
                    int bitCount = allowedTypesBits[Array.IndexOf(allowedTypes, variables[i].Type)];
                    variableLabels[i] = $"{variables[i].Name} ({bitCount})";
                }

                return variableLabels;
            }

            public Instruction ShallowCopy()
            {
                return (Instruction)MemberwiseClone();
            }
        }

        #endregion Public Classes

        #region Public Fields

        public Preferences DataPreferences = new Preferences();
        public Instruction[] DataInstructions = new Instruction[0];

        #endregion Public Fields

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
        
        #region Public Methods

        static public int BitSum(Instruction[] instructions)
        {
            int bitCount = 0;

            foreach (Instruction instruction in instructions)
                bitCount += instruction.BitCount;

            return bitCount;
        }

        static public NUSaveStateData GetPreferences(NUSaveState behaviour)
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

        static public NUSaveStateData CreatePreferences(NUSaveState behaviour)
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