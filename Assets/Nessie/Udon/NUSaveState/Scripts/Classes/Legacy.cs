#if UNITY_EDITOR

using System;
using UnityEditor;
using UnityEngine;
using VRC.Udon;
using Nessie.Udon.Extensions;

namespace Nessie.Udon.SaveState.Legacy
{
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
                if (value == null ||
                    System.IO.Directory.Exists(newPath)) // Simple fix to prevent non-folders from being assigned.
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

                Variables = value != null
                    ? value.GetFilteredVariables(allowedTypes, ~NUExtensions.VariableType.Internal).ToArray()
                    : new NUExtensions.Variable[0];
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
}

#endif