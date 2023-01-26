/*
 * MIT License
 * 
 * Copyright (c) 2021 NGenesis
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE. 
 */

// Thank you Genesis for the bit converter code!
// https://gist.github.com/NGenesis/4dbc68228f8abff292812dfff0638c47

using System;
using UdonSharp;
using UnityEngine;

namespace Nessie.Udon.SaveState
{
    public class BufferUtilities
    {
        public static byte[] PrepareBuffer(int bitCount) => new byte[Mathf.CeilToInt(bitCount / 8f)];

        public static void WriteBufferTypedObject(ref int bitIndex, byte[] buffer, TypeEnum type, object value)
        {
            switch (type)
            {
                case TypeEnum.Int: WriteBufferInteger(ref bitIndex, buffer, (int)(value ?? 0)); break;
                case TypeEnum.UInt: WriteBufferUnsignedInteger(ref bitIndex, buffer, (uint)(value ?? 0U)); break;
                case TypeEnum.Long: WriteBufferLong(ref bitIndex, buffer, (long)(value ?? 0L)); break;
                case TypeEnum.ULong: WriteBufferUnsignedLong(ref bitIndex, buffer, (ulong)(value ?? 0UL)); break;
                case TypeEnum.Short: WriteBufferShort(ref bitIndex, buffer, (short)(value ?? 0)); break;
                case TypeEnum.UShort: WriteBufferUnsignedShort(ref bitIndex, buffer, (ushort)(value ?? 0U)); break;
                case TypeEnum.Byte: WriteBufferByte(ref bitIndex, buffer, (byte)(value ?? 0)); break;
                case TypeEnum.SByte: WriteBufferSignedByte(ref bitIndex, buffer, (sbyte)(value ?? 0)); break;
                case TypeEnum.Char: WriteBufferChar(ref bitIndex, buffer, (char)(value ?? 0)); break;
                case TypeEnum.Float: WriteBufferFloat(ref bitIndex, buffer, (float)(value ?? 0.0f)); break;
                case TypeEnum.Double: WriteBufferDouble(ref bitIndex, buffer, (double)(value ?? 0.0)); break;
                case TypeEnum.Decimal: WriteBufferDecimal(ref bitIndex, buffer, (decimal)(value ?? 0m)); break;
                case TypeEnum.Bool: WriteBufferBoolean(ref bitIndex, buffer, (bool)(value ?? false));  break; // Special case
                case TypeEnum.Vector2: WriteBufferVector2(ref bitIndex, buffer, (Vector2)(value ?? Vector2.zero)); break;
                case TypeEnum.Vector3: WriteBufferVector3(ref bitIndex, buffer, (Vector3)(value ?? Vector3.zero)); break;
                case TypeEnum.Vector4: WriteBufferVector4(ref bitIndex, buffer, (Vector4)(value ?? Vector4.zero)); break;
                case TypeEnum.Vector2Int: WriteBufferVector2Int(ref bitIndex, buffer, (Vector2Int)(value ?? Vector2Int.zero)); break;
                case TypeEnum.Vector3Int: WriteBufferVector3Int(ref bitIndex, buffer, (Vector3Int)(value ?? Vector3Int.zero)); break;
                case TypeEnum.Quaternion: WriteBufferQuaternion(ref bitIndex, buffer, (value != null ? (Quaternion)value : Quaternion.identity)); break;
                case TypeEnum.Color: WriteBufferColor(ref bitIndex, buffer, (value != null ? (Color)value : Color.clear)); break;
                case TypeEnum.Color32: WriteBufferColor32(ref bitIndex, buffer, (value != null ? (Color32)value : (Color32)Color.clear)); break;
            }
        }

        public static object ReadBufferTypedObject(ref int bitIndex, byte[] buffer, TypeEnum type)
        {
            switch (type)
            {
                case TypeEnum.Int: return ReadBufferInteger(ref bitIndex, buffer);
                case TypeEnum.UInt: return ReadBufferUnsignedInteger(ref bitIndex, buffer);
                case TypeEnum.Long: return ReadBufferLong(ref bitIndex, buffer);
                case TypeEnum.ULong: return ReadBufferUnsignedLong(ref bitIndex, buffer);
                case TypeEnum.Short: return ReadBufferShort(ref bitIndex, buffer);
                case TypeEnum.UShort: return ReadBufferUnsignedShort(ref bitIndex, buffer);
                case TypeEnum.Byte: return ReadBufferByte(ref bitIndex, buffer);
                case TypeEnum.SByte: return ReadBufferSignedByte(ref bitIndex, buffer);
                case TypeEnum.Char: return ReadBufferChar(ref bitIndex, buffer);
                case TypeEnum.Float: return ReadBufferFloat(ref bitIndex, buffer);
                case TypeEnum.Double: return ReadBufferDouble(ref bitIndex, buffer);
                case TypeEnum.Decimal: return ReadBufferDecimal(ref bitIndex, buffer);
                case TypeEnum.Bool: return ReadBufferBoolean(ref bitIndex, buffer); // Special case
                case TypeEnum.Vector2: return ReadBufferVector2(ref bitIndex, buffer);
                case TypeEnum.Vector3: return ReadBufferVector3(ref bitIndex, buffer);
                case TypeEnum.Vector4: return ReadBufferVector4(ref bitIndex, buffer);
                case TypeEnum.Vector2Int: return ReadBufferVector2Int(ref bitIndex, buffer);
                case TypeEnum.Vector3Int: return ReadBufferVector3Int(ref bitIndex, buffer);
                case TypeEnum.Quaternion: return ReadBufferQuaternion(ref bitIndex, buffer);
                case TypeEnum.Color: return ReadBufferColor(ref bitIndex, buffer);
                case TypeEnum.Color32: return ReadBufferColor32(ref bitIndex, buffer);
            }
            
            return null;
        }

        #region Converters
        
        private static bool ReadBufferBoolean(ref int bitIndex, byte[] buffer) // Special case
        {
            int byteIndex = bitIndex / 8;
            int bitOffset = bitIndex % 8;
            bitIndex++;

            byte mask = (byte)(1 << (7 - bitOffset));
            return (buffer[byteIndex] & mask) > 0;
        }
        private static void WriteBufferBoolean(ref int bitIndex, byte[] buffer, bool value) // Special case
        {
            int byteIndex = bitIndex / 8;
            int bitOffset = bitIndex % 8;
            bitIndex++;

            byte mask = (byte)(1 << (7 - bitOffset));
            if (value)
            {
                buffer[byteIndex] |= mask;
            }
            else
            {
                buffer[byteIndex] &= (byte)(~mask & 255);
            }
        }

        private static char ReadBufferChar(ref int bitIndex, byte[] buffer) => (char)ReadBufferShort(ref bitIndex, buffer);
        private static void WriteBufferChar(ref int bitIndex, byte[] buffer, char value) => WriteBufferShort(ref bitIndex, buffer, (short)value);

        private static byte ReadBufferByte(ref int bitIndex, byte[] buffer)
        {
            int byteIndex = bitIndex / 8;
            int bitOffset = bitIndex % 8;
            bitIndex += 8;

            if (bitOffset == 0)
            {
                return buffer[byteIndex];
            }

            byte value = 0;
            value |= (byte)((buffer[byteIndex] << bitOffset) & Byte.MaxValue);
            value |= (byte)(buffer[byteIndex + 1] >> (8 - bitOffset));
            
            return value;
        }
        private static void WriteBufferByte(ref int bitIndex, byte[] buffer, byte value)
        {
            int byteIndex = bitIndex / 8;
            int bitOffset = bitIndex % 8;
            bitIndex += 8;
            
            if (bitOffset == 0)
            {
                buffer[byteIndex] = value;
                return;
            }

            buffer[byteIndex] |= (byte)(value >> bitOffset);
            buffer[byteIndex + 1] |= (byte)((value << (8 - bitOffset)) & Byte.MaxValue);
        }

        private static sbyte ReadBufferSignedByte(ref int bitIndex, byte[] buffer)
        {
            int value = ReadBufferByte(ref bitIndex, buffer);
            if (value > 0x80) value -= 0xFF;
            
            return Convert.ToSByte(value);
        }
        private static void WriteBufferSignedByte(ref int bitIndex, byte[] buffer, sbyte value)
        {
            WriteBufferByte(ref bitIndex, buffer, (byte)(value < 0 ? value + 0xFF : value));
        }

        private static short ReadBufferShort(ref int bitIndex, byte[] buffer)
        {
            int value = ReadBufferByte(ref bitIndex, buffer) << 8 | ReadBufferByte(ref bitIndex, buffer);
            if (value > 0x8000) value = value - 0xFFFF;
            return Convert.ToInt16(value);
        }
        private static void WriteBufferShort(ref int bitIndex, byte[] buffer, short value)
        {
            int tmp = value < 0 ? (value + 0xFFFF) : value;
            WriteBufferByte(ref bitIndex, buffer, (byte)(tmp >> 8));
            WriteBufferByte(ref bitIndex, buffer, (byte)(tmp & 0xFF));
        }

        private static ushort ReadBufferUnsignedShort(ref int bitIndex, byte[] buffer) => Convert.ToUInt16(ReadBufferByte(ref bitIndex, buffer) << 8 | ReadBufferByte(ref bitIndex, buffer));
        private static void WriteBufferUnsignedShort(ref int bitIndex, byte[] buffer, ushort value)
        {
            int tmp = Convert.ToInt32(value);
            WriteBufferByte(ref bitIndex, buffer, (byte)(tmp >> 8));
            WriteBufferByte(ref bitIndex, buffer, (byte)(tmp & 0xFF));
        }

        private static int ReadBufferInteger(ref int bitIndex, byte[] buffer) => ((int)ReadBufferByte(ref bitIndex, buffer) << 24) | (int)(ReadBufferByte(ref bitIndex, buffer) << 16) | (int)(ReadBufferByte(ref bitIndex, buffer) << 8) | (int)(ReadBufferByte(ref bitIndex, buffer));
        private static void WriteBufferInteger(ref int bitIndex, byte[] buffer, int value)
        {
            WriteBufferByte(ref bitIndex, buffer, (byte)((value >> 24) & 0xFF));
            WriteBufferByte(ref bitIndex, buffer, (byte)((value >> 16) & 0xFF));
            WriteBufferByte(ref bitIndex, buffer, (byte)((value >> 8) & 0xFF));
            WriteBufferByte(ref bitIndex, buffer, (byte)(value & 0xFF));
        }

        private static uint ReadBufferUnsignedInteger(ref int bitIndex, byte[] buffer) => ((uint)ReadBufferByte(ref bitIndex, buffer) << 24) | ((uint)ReadBufferByte(ref bitIndex, buffer) << 16) | ((uint)ReadBufferByte(ref bitIndex, buffer) << 8) | ((uint)ReadBufferByte(ref bitIndex, buffer));
        private static void WriteBufferUnsignedInteger(ref int bitIndex, byte[] buffer, uint value)
        {
            WriteBufferByte(ref bitIndex, buffer, (byte)((value >> 24) & 255u));
            WriteBufferByte(ref bitIndex, buffer, (byte)((value >> 16) & 255u));
            WriteBufferByte(ref bitIndex, buffer, (byte)((value >> 8) & 255u));
            WriteBufferByte(ref bitIndex, buffer, (byte)(value & 255u));
        }

        private static long ReadBufferLong(ref int bitIndex, byte[] buffer) => ((long)ReadBufferByte(ref bitIndex, buffer) << 56) | ((long)ReadBufferByte(ref bitIndex, buffer) << 48) | ((long)ReadBufferByte(ref bitIndex, buffer) << 40) | ((long)ReadBufferByte(ref bitIndex, buffer) << 32) | ((long)ReadBufferByte(ref bitIndex, buffer) << 24) | ((long)ReadBufferByte(ref bitIndex, buffer) << 16) | ((long)ReadBufferByte(ref bitIndex, buffer) << 8) | ((long)ReadBufferByte(ref bitIndex, buffer));
        private static void WriteBufferLong(ref int bitIndex, byte[] buffer, long value)
        {
            WriteBufferByte(ref bitIndex, buffer, (byte)((value >> 56) & 0xFF));
            WriteBufferByte(ref bitIndex, buffer, (byte)((value >> 48) & 0xFF));
            WriteBufferByte(ref bitIndex, buffer, (byte)((value >> 40) & 0xFF));
            WriteBufferByte(ref bitIndex, buffer, (byte)((value >> 32) & 0xFF));
            WriteBufferByte(ref bitIndex, buffer, (byte)((value >> 24) & 0xFF));
            WriteBufferByte(ref bitIndex, buffer, (byte)((value >> 16) & 0xFF));
            WriteBufferByte(ref bitIndex, buffer, (byte)((value >> 8) & 0xFF));
            WriteBufferByte(ref bitIndex, buffer, (byte)(value & 0xFF));
        }

        private static ulong ReadBufferUnsignedLong(ref int bitIndex, byte[] buffer) => ((ulong)ReadBufferByte(ref bitIndex, buffer) << 56) | ((ulong)ReadBufferByte(ref bitIndex, buffer) << 48) | ((ulong)ReadBufferByte(ref bitIndex, buffer) << 40) | ((ulong)ReadBufferByte(ref bitIndex, buffer) << 32) | ((ulong)ReadBufferByte(ref bitIndex, buffer) << 24) | ((ulong)ReadBufferByte(ref bitIndex, buffer) << 16) | ((ulong)ReadBufferByte(ref bitIndex, buffer) << 8) | ((ulong)ReadBufferByte(ref bitIndex, buffer));
        private static void WriteBufferUnsignedLong(ref int bitIndex, byte[] buffer, ulong value)
        {
            WriteBufferByte(ref bitIndex, buffer, (byte)((value >> 56) & 255ul));
            WriteBufferByte(ref bitIndex, buffer, (byte)((value >> 48) & 255ul));
            WriteBufferByte(ref bitIndex, buffer, (byte)((value >> 40) & 255ul));
            WriteBufferByte(ref bitIndex, buffer, (byte)((value >> 32) & 255ul));
            WriteBufferByte(ref bitIndex, buffer, (byte)((value >> 24) & 255ul));
            WriteBufferByte(ref bitIndex, buffer, (byte)((value >> 16) & 255ul));
            WriteBufferByte(ref bitIndex, buffer, (byte)((value >> 8) & 255ul));
            WriteBufferByte(ref bitIndex, buffer, (byte)(value & 255ul));
        }

        private static decimal ReadBufferDecimal(ref int bitIndex, byte[] buffer)
        {
            int signScaleBits = ReadBufferInteger(ref bitIndex, buffer);
            return new Decimal(ReadBufferInteger(ref bitIndex, buffer), ReadBufferInteger(ref bitIndex, buffer), ReadBufferInteger(ref bitIndex, buffer), (signScaleBits & 0x80000000) != 0, (byte)((signScaleBits >> 16) & 127));
        }
        private static void WriteBufferDecimal(ref int bitIndex, byte[] buffer, decimal value)
        {
            int[] bits = Decimal.GetBits(value);
            WriteBufferInteger(ref bitIndex, buffer, bits[3]); // Sign & scale bits
            WriteBufferInteger(ref bitIndex, buffer, bits[0]);
            WriteBufferInteger(ref bitIndex, buffer, bits[1]);
            WriteBufferInteger(ref bitIndex, buffer, bits[2]);
        }

        private static Vector2 ReadBufferVector2(ref int bitIndex, byte[] buffer) => new Vector2(ReadBufferFloat(ref bitIndex, buffer), ReadBufferFloat(ref bitIndex, buffer));
        private static void WriteBufferVector2(ref int bitIndex, byte[] buffer, Vector2 value)
        {
            WriteBufferFloat(ref bitIndex, buffer, value.x);
            WriteBufferFloat(ref bitIndex, buffer, value.y);
        }

        private static Vector3 ReadBufferVector3(ref int bitIndex, byte[] buffer) => new Vector3(ReadBufferFloat(ref bitIndex, buffer), ReadBufferFloat(ref bitIndex, buffer), ReadBufferFloat(ref bitIndex, buffer));
        private static void WriteBufferVector3(ref int bitIndex, byte[] buffer, Vector3 value)
        {
            WriteBufferFloat(ref bitIndex, buffer, value.x);
            WriteBufferFloat(ref bitIndex, buffer, value.y);
            WriteBufferFloat(ref bitIndex, buffer, value.z);
        }

        private static Vector4 ReadBufferVector4(ref int bitIndex, byte[] buffer) => new Vector4(ReadBufferFloat(ref bitIndex, buffer), ReadBufferFloat(ref bitIndex, buffer), ReadBufferFloat(ref bitIndex, buffer), ReadBufferFloat(ref bitIndex, buffer));
        private static void WriteBufferVector4(ref int bitIndex, byte[] buffer, Vector4 value)
        {
            WriteBufferFloat(ref bitIndex, buffer, value.x);
            WriteBufferFloat(ref bitIndex, buffer, value.y);
            WriteBufferFloat(ref bitIndex, buffer, value.z);
            WriteBufferFloat(ref bitIndex, buffer, value.w);
        }

        private static Vector2Int ReadBufferVector2Int(ref int bitIndex, byte[] buffer) => new Vector2Int(ReadBufferInteger(ref bitIndex, buffer), ReadBufferInteger(ref bitIndex, buffer));
        private static void WriteBufferVector2Int(ref int bitIndex, byte[] buffer, Vector2Int value)
        {
            WriteBufferInteger(ref bitIndex, buffer, value.x);
            WriteBufferInteger(ref bitIndex, buffer, value.y);
        }

        private static Vector3Int ReadBufferVector3Int(ref int bitIndex, byte[] buffer) => new Vector3Int(ReadBufferInteger(ref bitIndex, buffer), ReadBufferInteger(ref bitIndex, buffer), ReadBufferInteger(ref bitIndex, buffer));
        private static void WriteBufferVector3Int(ref int bitIndex, byte[] buffer, Vector3Int value)
        {
            WriteBufferInteger(ref bitIndex, buffer, value.x);
            WriteBufferInteger(ref bitIndex, buffer, value.y);
            WriteBufferInteger(ref bitIndex, buffer, value.z);
        }

        private static Quaternion ReadBufferQuaternion(ref int bitIndex, byte[] buffer) => new Quaternion(ReadBufferFloat(ref bitIndex, buffer), ReadBufferFloat(ref bitIndex, buffer), ReadBufferFloat(ref bitIndex, buffer), ReadBufferFloat(ref bitIndex, buffer));
        private static void WriteBufferQuaternion(ref int bitIndex, byte[] buffer, Quaternion value)
        {
            WriteBufferFloat(ref bitIndex, buffer, value.x);
            WriteBufferFloat(ref bitIndex, buffer, value.y);
            WriteBufferFloat(ref bitIndex, buffer, value.z);
            WriteBufferFloat(ref bitIndex, buffer, value.w);
        }

        private static Color ReadBufferColor(ref int bitIndex, byte[] buffer) => new Color(ReadBufferFloat(ref bitIndex, buffer), ReadBufferFloat(ref bitIndex, buffer), ReadBufferFloat(ref bitIndex, buffer), ReadBufferFloat(ref bitIndex, buffer));
        private static void WriteBufferColor(ref int bitIndex, byte[] buffer, Color value)
        {
            WriteBufferFloat(ref bitIndex, buffer, value.r);
            WriteBufferFloat(ref bitIndex, buffer, value.g);
            WriteBufferFloat(ref bitIndex, buffer, value.b);
            WriteBufferFloat(ref bitIndex, buffer, value.a);
        }

        private static Color32 ReadBufferColor32(ref int bitIndex, byte[] buffer) => new Color32(ReadBufferByte(ref bitIndex, buffer), ReadBufferByte(ref bitIndex, buffer), ReadBufferByte(ref bitIndex, buffer), ReadBufferByte(ref bitIndex, buffer));
        private static void WriteBufferColor32(ref int bitIndex, byte[] buffer, Color32 value)
        {
            WriteBufferByte(ref bitIndex, buffer, value.r);
            WriteBufferByte(ref bitIndex, buffer, value.g);
            WriteBufferByte(ref bitIndex, buffer, value.b);
            WriteBufferByte(ref bitIndex, buffer, value.a);
        }

        private const uint FLOAT_SIGN_BIT = 0x80000000;
        private const uint FLOAT_EXP_MASK = 0x7F800000;
        private const uint FLOAT_FRAC_MASK = 0x007FFFFF;
        private static float ReadBufferFloat(ref int bitIndex, byte[] buffer)
        {
            uint value = ReadBufferUnsignedInteger(ref bitIndex, buffer);
            if (value == 0 || value == FLOAT_SIGN_BIT) return 0.0f;

            int exp = (int)((value & FLOAT_EXP_MASK) >> 23);
            int frac = (int)(value & FLOAT_FRAC_MASK);
            bool negate = (value & FLOAT_SIGN_BIT) == FLOAT_SIGN_BIT;

            if (exp == 0xFF)
            {
                if (frac == 0) return negate ? float.NegativeInfinity : float.PositiveInfinity;
                return float.NaN;
            }

            bool normal = exp != 0x00;
            if (normal) exp -= 127;
            else exp = -126;

            float result = frac / (float)(2 << 22);
            if (normal) result += 1.0f;

            result *= Mathf.Pow(2, exp);
            if (negate) result = -result;

            return result;
        }
        private static void WriteBufferFloat(ref int bitIndex, byte[] buffer, float value)
        {
            uint tmp = 0;
            if (float.IsNaN(value))
            {
                tmp = FLOAT_EXP_MASK | FLOAT_FRAC_MASK;
            }
            else if (float.IsInfinity(value))
            {
                tmp = FLOAT_EXP_MASK;
                if (float.IsNegativeInfinity(value)) tmp |= FLOAT_SIGN_BIT;
            }
            else if (value != 0.0f)
            {
                if (value < 0.0f)
                {
                    value = -value;
                    tmp |= FLOAT_SIGN_BIT;
                }

                int exp = 0;
                while (value >= 2.0f)
                {
                    value *= 0.5f;
                    ++exp;
                }

                bool normal = true;
                while (value < 1.0f)
                {
                    if (exp == -126)
                    {
                        normal = false;
                        break;
                    }

                    value *= 2.0f;
                    --exp;
                }

                if (normal)
                {
                    value -= 1.0f;
                    exp += 127;
                }
                else exp = 0;

                tmp |= Convert.ToUInt32(exp << 23) & FLOAT_EXP_MASK;
                tmp |= Convert.ToUInt32(value * (2 << 22)) & FLOAT_FRAC_MASK;
            }

            WriteBufferUnsignedInteger(ref bitIndex, buffer, tmp);
        }

        private const ulong DOUBLE_SIGN_BIT = 0x8000000000000000;
        private const ulong DOUBLE_EXP_MASK = 0x7FF0000000000000;
        private const ulong DOUBLE_FRAC_MASK = 0x000FFFFFFFFFFFFF;
        private static double ReadBufferDouble(ref int bitIndex, byte[] buffer)
        {
            ulong value = ReadBufferUnsignedLong(ref bitIndex, buffer);
            if (value == 0.0 || value == DOUBLE_SIGN_BIT) return 0.0;

            long exp = (long)((value & DOUBLE_EXP_MASK) >> 52);
            long frac = (long)(value & DOUBLE_FRAC_MASK);
            bool negate = (value & DOUBLE_SIGN_BIT) == DOUBLE_SIGN_BIT;

            if (exp == 0x7FF)
            {
                if (frac == 0) return negate ? double.NegativeInfinity : double.PositiveInfinity;
                return double.NaN;
            }

            bool normal = exp != 0x000;
            if (normal) exp -= 1023;
            else exp = -1022;

            double result = frac / (double)(2UL << 51);
            if (normal) result += 1.0;

            result *= Math.Pow(2, exp);
            if (negate) result = -result;

            return result;
        }
        private static void WriteBufferDouble(ref int bitIndex, byte[] buffer, double value)
        {
            ulong tmp = 0;
            if (double.IsNaN(value))
            {
                tmp = DOUBLE_EXP_MASK | DOUBLE_FRAC_MASK;
            }
            else if (double.IsInfinity(value))
            {
                tmp = DOUBLE_EXP_MASK;
                if (double.IsNegativeInfinity(value)) tmp |= DOUBLE_SIGN_BIT;
            }
            else if (value != 0.0)
            {
                if (value < 0.0)
                {
                    value = -value;
                    tmp |= DOUBLE_SIGN_BIT;
                }

                long exp = 0;
                while (value >= 2.0)
                {
                    value *= 0.5;
                    ++exp;
                }

                bool normal = true;
                while (value < 1.0)
                {
                    if (exp == -1022)
                    {
                        normal = false;
                        break;
                    }
                    value *= 2.0;
                    --exp;
                }

                if (normal)
                {
                    value -= 1.0;
                    exp += 1023;
                }
                else exp = 0;

                tmp |= Convert.ToUInt64(exp << 52) & DOUBLE_EXP_MASK;
                tmp |= Convert.ToUInt64(value * (2UL << 51)) & DOUBLE_FRAC_MASK;
            }

            WriteBufferUnsignedLong(ref bitIndex, buffer, tmp);
        }
        
        #endregion Converters
    }
}
