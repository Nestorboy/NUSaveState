using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Nessie.Udon.SaveState.Data
{
    public static class SerializationUtilities
    {
        private static void SetValue(SerializedProperty sp, object value)
        {
            if (sp == null)
                return;
            switch (sp.propertyType)
            {
                case SerializedPropertyType.Integer:
                    sp.intValue = (int)value;
                    break;
                case SerializedPropertyType.Boolean:
                    sp.boolValue = (bool)value;
                    break;
                case SerializedPropertyType.Float:
                    sp.floatValue = (float)value;
                    break;
                case SerializedPropertyType.String:
                    sp.stringValue = (string)value;
                    break;
                case SerializedPropertyType.Color:
                    sp.colorValue = (Color)value;
                    break;
                case SerializedPropertyType.ObjectReference:
                    sp.objectReferenceValue = (UnityEngine.Object)value;
                    break;
                case SerializedPropertyType.LayerMask:
                    sp.intValue = (int)value;
                    break;
                case SerializedPropertyType.Enum:
                    sp.intValue = (int)value;
                    break;
                case SerializedPropertyType.Vector2:
                    sp.vector2Value = (Vector2)value;
                    break;
                case SerializedPropertyType.Vector3:
                    sp.vector3Value = (Vector3)value;
                    break;
                case SerializedPropertyType.Vector4:
                    sp.vector4Value = (Vector4)value;
                    break;
                case SerializedPropertyType.Rect:
                    sp.rectValue = (Rect)value;
                    break;
                case SerializedPropertyType.ArraySize:
                    sp.intValue = (int)value;
                    break;
                case SerializedPropertyType.Character:
                    sp.intValue = (int)value;
                    break;
                case SerializedPropertyType.AnimationCurve:
                    sp.animationCurveValue = (AnimationCurve)value;
                    break;
                case SerializedPropertyType.Bounds:
                    sp.boundsValue = (Bounds)value;
                    break;
                case SerializedPropertyType.ExposedReference:
                    sp.exposedReferenceValue = (UnityEngine.Object)value;
                    break;
                case SerializedPropertyType.Vector2Int:
                    sp.vector2IntValue = (Vector2Int)value;
                    break;
                case SerializedPropertyType.Vector3Int:
                    sp.vector3IntValue = (Vector3Int)value;
                    break;
                case SerializedPropertyType.RectInt:
                    sp.rectIntValue = (RectInt)value;
                    break;
                case SerializedPropertyType.BoundsInt:
                    sp.boundsIntValue = (BoundsInt)value;
                    break;
                default:
                    Debug.Log($"Unable to set value of PropertyType: {sp.propertyType}");
                    break;
            }
        }
        
        private static object GetValue(SerializedProperty sp)
        {
            if (sp == null)
                return null;
            switch (sp.propertyType)
            {
                case SerializedPropertyType.Integer:
                    return sp.intValue;

                case SerializedPropertyType.Boolean:
                    return sp.boolValue;

                case SerializedPropertyType.Float:
                    return sp.floatValue;

                case SerializedPropertyType.String:
                    return sp.stringValue;

                case SerializedPropertyType.Color:
                    return sp.colorValue;

                case SerializedPropertyType.ObjectReference:
                    return sp.objectReferenceValue;

                case SerializedPropertyType.LayerMask:
                    return sp.intValue;

                case SerializedPropertyType.Enum:
                    return sp.intValue;

                case SerializedPropertyType.Vector2:
                    return sp.vector2Value;

                case SerializedPropertyType.Vector3:
                    return sp.vector3Value;

                case SerializedPropertyType.Vector4:
                    return sp.vector4Value;

                case SerializedPropertyType.Rect:
                    return sp.rectValue;

                case SerializedPropertyType.ArraySize:
                    return sp.intValue;

                case SerializedPropertyType.Character:
                    return sp.intValue;

                case SerializedPropertyType.AnimationCurve:
                    return sp.animationCurveValue;

                case SerializedPropertyType.Bounds:
                    return sp.boundsValue;

                case SerializedPropertyType.ExposedReference:
                    return sp.exposedReferenceValue;

                case SerializedPropertyType.Vector2Int:
                    return sp.vector2IntValue;

                case SerializedPropertyType.Vector3Int:
                    return sp.vector3IntValue;

                case SerializedPropertyType.RectInt:
                    return sp.rectIntValue;

                case SerializedPropertyType.BoundsInt:
                    return sp.boundsIntValue;
                
                default:
                    Debug.Log($"Unable to get value of PropertyType: {sp.propertyType}");
                    return null;
            }
        }
        
        public static IEnumerator<(SerializedProperty, FieldInfo)> GetPropertyFieldEnumerator(SerializedProperty property, Type type)
        {
            FieldInfo[] fields =  type.GetFields(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
            foreach (FieldInfo field in fields)
            {
                // Check if field is private and not serialized or if its serialization is disabled.
                if (!(field.IsPublic || field.IsDefined(typeof(SerializeField))) || field.IsDefined(typeof(NonSerializedAttribute)))
                {
                    continue;
                }
                
                SerializedProperty childProperty = property.FindPropertyRelative(field.Name);
                if (childProperty == null)
                {
                    Debug.LogError($"Property path {property.name}.{field.Name} does not exist.");

                    continue;
                }

                yield return (childProperty, field);
            }
        }

        public static object GetPropertyValue(SerializedProperty property, Type type)
        {
            if (!property.hasVisibleChildren)
            {
                //Debug.Log($"{property.propertyPath} ({property.propertyType}): {GetValue(property)}");

                return GetValue(property);
            }

            if (property.isArray)
            {
                Type elementType = type.GetElementType();

                //Debug.Log($"Creating array of: {elementType}[{sp.arraySize}]");
                Array newArray = Array.CreateInstance(elementType, property.arraySize);
                for (var i = 0; i < property.arraySize; i++)
                {
                    SerializedProperty propElement = property.GetArrayElementAtIndex(i);
                    object elementValue = GetPropertyValue(propElement, elementType);

                    newArray.SetValue(elementValue, i);
                }

                return newArray;
            }
            
            object obj = Activator.CreateInstance(type);
            var enumerator = GetPropertyFieldEnumerator(property, type);
            while (enumerator.MoveNext())
            {
                (SerializedProperty sp, FieldInfo field) = enumerator.Current;

                Type fieldType = field.FieldType;
                object newValue = GetPropertyValue(sp, fieldType);

                field.SetValue(obj, newValue);
            }
            
            return obj;
        }

        public static T GetPropertyValue<T>(SerializedProperty property) => (T)GetPropertyValue(property, typeof(T));
        
        public static void SetPropertyValue(SerializedProperty property, Type type, object obj)
        {
            if (!property.hasVisibleChildren)
            {
                //Debug.Log($"{property.propertyPath} ({property.propertyType}): {GetValue(property)}");

                SetValue(property, obj);
                
                return;
            }

            if (property.isArray)
            {
                Type elementType = type.GetElementType();
                Array oldArray = (Array)obj;
                property.arraySize = oldArray?.Length ?? 0;
                for (int i = 0; i < property.arraySize; i++)
                {
                    SerializedProperty propElement = property.GetArrayElementAtIndex(i);
                    SetPropertyValue(propElement, elementType, oldArray.GetValue(i));
                }
                
                return;
            }
            
            var enumerator = GetPropertyFieldEnumerator(property, type);
            while (enumerator.MoveNext())
            {
                (SerializedProperty sp, FieldInfo field) = enumerator.Current;

                Type fieldType = field.FieldType;
                SetPropertyValue(sp, fieldType, obj == null ? null : field.GetValue(obj));
            }
        }

        public static void SetPropertyValue<T>(SerializedProperty property, T obj) => SetPropertyValue(property, typeof(T), obj);
    }
}
