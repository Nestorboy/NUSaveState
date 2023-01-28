#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using UnityEngine;
using Nessie.Udon.Extensions;

namespace Nessie.Udon.SaveState.Data
{
    public static class BitUtilities
    {
        private static readonly IReadOnlyDictionary<TypeEnum, Type> TypeEnumToTypeDict = new Dictionary<TypeEnum, Type>()
        {
            { TypeEnum.Int, typeof(int) },
            { TypeEnum.UInt, typeof(uint) },
            { TypeEnum.Long, typeof(long) },
            { TypeEnum.ULong, typeof(ulong) },
            { TypeEnum.Short, typeof(short) },
            { TypeEnum.UShort, typeof(ushort) },
            { TypeEnum.Byte, typeof(byte) },
            { TypeEnum.SByte, typeof(sbyte) },
            { TypeEnum.Char, typeof(char) },
            { TypeEnum.Float, typeof(float) },
            { TypeEnum.Double, typeof(double) },
            { TypeEnum.Decimal, typeof(decimal) },
            { TypeEnum.Bool, typeof(bool) }, // Special Case
            { TypeEnum.Vector2, typeof(Vector2) },
            { TypeEnum.Vector3, typeof(Vector3) },
            { TypeEnum.Vector4, typeof(Vector4) },
            { TypeEnum.Vector2Int, typeof(Vector2Int) },
            { TypeEnum.Vector3Int, typeof(Vector3Int) },
            { TypeEnum.Quaternion, typeof(Quaternion) },
            { TypeEnum.Color, typeof(Color) },
            { TypeEnum.Color32, typeof(Color32) },
        };
        
        private static readonly IReadOnlyDictionary<Type, TypeEnum> TypeToEnumTypeDict = new Dictionary<Type, TypeEnum>()
        {
            { typeof(int), TypeEnum.Int },
            { typeof(uint), TypeEnum.UInt },
            { typeof(long), TypeEnum.Long },
            { typeof(ulong), TypeEnum.ULong },
            { typeof(short), TypeEnum.Short },
            { typeof(ushort), TypeEnum.UShort },
            { typeof(byte), TypeEnum.Byte },
            { typeof(sbyte), TypeEnum.SByte },
            { typeof(char), TypeEnum.Char },
            { typeof(float), TypeEnum.Float },
            { typeof(double), TypeEnum.Double },
            { typeof(decimal), TypeEnum.Decimal },
            { typeof(bool), TypeEnum.Bool }, // Special Case
            { typeof(Vector2), TypeEnum.Vector2 },
            { typeof(Vector3), TypeEnum.Vector3 },
            { typeof(Vector4), TypeEnum.Vector4 },
            { typeof(Vector2Int), TypeEnum.Vector2Int },
            { typeof(Vector3Int), TypeEnum.Vector3Int },
            { typeof(Quaternion), TypeEnum.Quaternion },
            { typeof(Color), TypeEnum.Color },
            { typeof(Color32), TypeEnum.Color32 },
        };
        
        private static readonly IReadOnlyDictionary<TypeEnum, int> TypeEnumBitsDict = new Dictionary<TypeEnum, int>()
        {
            { TypeEnum.Int, 32 },
            { TypeEnum.UInt, 32 },
            { TypeEnum.Long, 64 },
            { TypeEnum.ULong, 64 },
            { TypeEnum.Short, 16 },
            { TypeEnum.UShort, 16 },
            { TypeEnum.Byte, 8 },
            { TypeEnum.SByte, 8 },
            { TypeEnum.Char, 16 },
            { TypeEnum.Float, 32 },
            { TypeEnum.Double, 64 },
            { TypeEnum.Decimal, 128 },
            { TypeEnum.Bool, 1 }, // Special Case
            { TypeEnum.Vector2, 64 },
            { TypeEnum.Vector3, 96 },
            { TypeEnum.Vector4, 128 },
            { TypeEnum.Vector2Int, 64 },
            { TypeEnum.Vector3Int, 96 },
            { TypeEnum.Quaternion, 128 },
            { TypeEnum.Color, 128 },
            { TypeEnum.Color32, 32 },
        };
        
        public static readonly string[] ValidTypesLabels = new string[]
        {
            "Int (32)",
            "UInt (32)",
            "Long (64)",
            "ULong (64)",
            "Short (16)",
            "UShort (16)",
            "Byte (8)",
            "SByte (8)",
            "Char (16)",
            "Float (32)",
            "Double (64)",
            "Decimal (128)",
            "Bool (1)", // Special Case
            "Vector2 (64)",
            "Vector3 (96)",
            "Vector4 (128)",
            "Vector2Int (64)",
            "Vector3Int (96)",
            "Quaternion (128)",
            "Color (128)",
            "Color32 (32)",
        };

        public static string[] PrepareLabels(NUExtensions.Variable[] variables)
        {
            string[] variableLabels = new string[variables.Length];

            for (int i = 0; i < variableLabels.Length; i++)
            {
                variableLabels[i] = FormatLabel(variables[i]);
            }

            return variableLabels;
        }

        private static string FormatLabel(NUExtensions.Variable variable)
        {
            TypeEnum typeEnum = GetTypeEnum(variable.Type);
            return $"{variable.Name} ({typeEnum})";
        }
        
        public static int GetBitCount(TypeEnum typeEnum)
        {
            return TypeEnumBitsDict.ContainsKey(typeEnum) ? TypeEnumBitsDict[typeEnum] : 0;
        }

        public static Type GetType(TypeEnum typeEnum) => TypeEnumToTypeDict.ContainsKey(typeEnum) ? TypeEnumToTypeDict[typeEnum] : null;
        
        public static TypeEnum GetTypeEnum(Type type) => type != null && TypeToEnumTypeDict.ContainsKey(type) ? TypeToEnumTypeDict[type] : TypeEnum.None;

        public static int GetBitCount(this VariableSlot slot)
        {
            return GetBitCount(slot.TypeEnum);
        }

        public static int GetBitSum(this VariableSlot[] slots)
        {
            int bitCount = 0;

            foreach (VariableSlot slot in slots)
                bitCount += slot.GetBitCount();

            return bitCount;
        }
    }
}

#endif