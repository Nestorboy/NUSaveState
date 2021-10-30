#if UNITY_EDITOR

using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using VRC.Udon;
using Nessie.Udon.Extensions;

namespace Nessie.Udon.SaveState
{
    [AddComponentMenu("")]
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
            };

            [SerializeField] private UdonBehaviour udon;
            public UdonBehaviour Udon
            {
                get
                {
                    return udon;
                }
                set
                {
                    udon = value;

                    Variables = value != null ? value.GetFilteredVariables(allowedTypes, ~NUExtensions.VariableType.Internal).ToArray() : new NUExtensions.Variable[0];
                    VariableLabels = PrepareLabels(Variables);

                    int newVariableIndex = Array.FindIndex(Variables, var => var.Name == Variable.Name);
                    if (newVariableIndex >= 0)
                        VariableIndex = Variables[newVariableIndex].Type == Variable.Type ? newVariableIndex : -1;
                    else
                        VariableIndex = newVariableIndex;
                }
            }

            public NUExtensions.Variable[] Variables;
            public NUExtensions.Variable Variable;

            [SerializeField] private int variableIndex = -1;
            public int VariableIndex
            {
                get
                {
                    return variableIndex;
                }
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
            NUSaveStateData data = null;
            Transform parent = behaviour.transform;

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform dataTransform = parent.GetChild(i);
                NUSaveStateData dataBehaviour = dataTransform.GetComponent<NUSaveStateData>();

                if (dataBehaviour != null)
                {
                    if (data == null)
                        data = dataBehaviour;
                    else
                    {
                        DestroyImmediate(dataBehaviour.gameObject);

                        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                    }
                }
                else if (dataTransform.gameObject.hideFlags != HideFlags.None && (dataTransform.name == "NUSS_DATA" || dataTransform.name == "NUSS_PREF"))
                {
                    DestroyImmediate(dataTransform.gameObject);

                    EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                }
            }

            return data;
        }

        static public NUSaveStateData CreatePreferences(NUSaveState behaviour)
        {
            GameObject dataGameObject = new GameObject("NUSS_DATA");
            NUSaveStateData data = dataGameObject.AddComponent<NUSaveStateData>();
            dataGameObject.transform.SetParent(behaviour.transform, false);

            dataGameObject.tag = "EditorOnly";
            dataGameObject.hideFlags = HideFlags.HideInHierarchy;

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            return data;
        }

        public void SetVisibility(bool show)
        {
            if (show)
            {
                if (gameObject.hideFlags != HideFlags.None)
                {
                    gameObject.hideFlags = HideFlags.None;

                    EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                }
            }
            else
            {
                if (gameObject.hideFlags != HideFlags.HideInHierarchy)
                {
                    gameObject.hideFlags = HideFlags.HideInHierarchy;
                    EditorApplication.DirtyHierarchyWindowSorting();

                    EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                }
            }
        }

        #endregion Public Methods
    }
}

#endif