
using UnityEditor;
using UnityEngine;
using Nessie.Udon.SaveState.Data;
using ReorderableList = UnityEditorInternal.ReorderableList;

namespace Nessie.Udon.SaveState.Internal
{
    [CustomEditor(typeof(AvatarData))]
    internal class AvatarDataInspector : Editor
    {
        private AvatarData avatarData;
        
        private SerializedProperty propBlueprint;
        private SerializedProperty propEncryption;
        private SerializedProperty propWriter;
        private SerializedProperty propParameter;
        private SerializedProperty propCoordinate;
        private SerializedProperty propIsLegacy;
        private SerializedProperty propVarSlots;
        private SerializedProperty propBitCount;

        private ReorderableList variableSlotRList;

        #region Unity Events
        
        private void OnEnable()
        {
            avatarData = (AvatarData)target;
            
            InitializeProperties();

            variableSlotRList = new ReorderableList(serializedObject, propVarSlots)
            {
                draggable = propVarSlots.isExpanded,
                displayAdd = propVarSlots.isExpanded,
                displayRemove = propVarSlots.isExpanded,
            
                elementHeight = propVarSlots.isExpanded ? 20f : 0f,
                footerHeight = propVarSlots.isExpanded ? 20f : 0f,
                
                drawHeaderCallback = (Rect rect) =>
                {
                    rect.width = rect.width / 2f - 2f;
                        
                    Rect foldoutRect = new Rect(rect) { x = rect.x + 12, width = rect.width - 12 };
                    Rect bitsRect = new Rect(rect) { x = rect.x + rect.width };
                    
                    int bitCount = propBitCount.intValue;
                    string slotsLabel = propVarSlots.displayName;
                    string bitsLabel = $"Bits: {bitCount} / {DataConstants.MAX_BIT_COUNT} Bytes: {Mathf.CeilToInt(bitCount / 8f)} / {DataConstants.MAX_BIT_COUNT / 8}";
                    
                    //EditorGUI.LabelField(new Rect(rect) { height = EditorGUIUtility.singleLineHeight }, slotsLabel, bitsLabel);
                    
                    EditorGUI.BeginChangeCheck();
                    bool isExpanded = EditorGUI.Foldout(foldoutRect, propVarSlots.isExpanded, slotsLabel);
                    
                    if (EditorGUI.EndChangeCheck())
                    {
                        propVarSlots.isExpanded = isExpanded;
                        
                        variableSlotRList.draggable = isExpanded;
                        variableSlotRList.displayAdd = isExpanded;
                        variableSlotRList.displayRemove = isExpanded;
                        
                        variableSlotRList.elementHeight = isExpanded ? 20f : 0f;
                        variableSlotRList.footerHeight = isExpanded ? 20f : 0f;
                    }
                    
                    EditorGUI.LabelField(bitsRect, bitsLabel);
                },
                
                drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
                {
                    if (!propVarSlots.isExpanded)
                    {
                        return;
                    }

                    rect.width = (rect.width - 2f) / 2f;
                    rect.height = EditorGUIUtility.singleLineHeight;
                    rect.y += (variableSlotRList.elementHeight - rect.height) / 2f;

                    Rect nameRect = new Rect(rect);
                    Rect typeRect = new Rect(rect) { x = rect.x + nameRect.width + 4f };

                    SerializedProperty propertySlot = propVarSlots.GetArrayElementAtIndex(index);
                    SerializedProperty propertyName = propertySlot.FindPropertyRelative(nameof(VariableSlot.Name));

                    VariableSlot slot = SerializationUtilities.GetPropertyValue<VariableSlot>(propertySlot);

                    // Draw Variable name.
                    propertyName.stringValue = EditorGUI.TextField(nameRect, propertyName.stringValue);

                    // Draw Variable type.
                    EditorGUI.BeginChangeCheck();
                    TypeEnum newTypeEnum = (TypeEnum)EditorGUI.Popup(typeRect, (int)slot.TypeEnum - 1, BitUtilities.ValidTypesLabels) + 1;
                    if (EditorGUI.EndChangeCheck())
                    {
                        VariableSlot newSlot = new VariableSlot(slot.Name, newTypeEnum);
                        ReplaceVariableSlot(index, newSlot);
                    }
                },
                
                onAddCallback = (ReorderableList list) =>
                {
                    ++list.serializedProperty.arraySize;
                    list.index = list.serializedProperty.arraySize - 1;
                    
                    SerializedProperty propertySlot = propVarSlots.GetArrayElementAtIndex(list.index);
                    VariableSlot slot = SerializationUtilities.GetPropertyValue<VariableSlot>(propertySlot);

                    if (slot.GetBitCount() + propBitCount.intValue <= DataConstants.MAX_BIT_COUNT)
                    {
                        return;
                    }

                    slot.TypeEnum = TypeEnum.Bool;

                    ReplaceVariableSlot(list.index, slot);
                },
                
                onCanAddCallback = (ReorderableList list) => propBitCount.intValue < DataConstants.MAX_BIT_COUNT,
            };
        }

        public override void OnInspectorGUI()
        {
            //base.OnInspectorGUI(); return;
            serializedObject.Update();

            DrawUtilities();
            DrawFields();

            if (serializedObject.ApplyModifiedProperties())
            {
                //Debug.Log("ApplyModifiedProperties");
            }
            
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                GUI.FocusControl(null);
            }
        }

        #endregion Unity Events

        private void InitializeProperties()
        {
            propBlueprint = serializedObject.FindProperty(nameof(AvatarData.AvatarBlueprint));
            propEncryption = serializedObject.FindProperty(nameof(AvatarData.EncryptionKey));
            propWriter = serializedObject.FindProperty(nameof(AvatarData.ParameterWriter));
            
            propIsLegacy = serializedObject.FindProperty(nameof(AvatarData.IsLegacy));
            propParameter = serializedObject.FindProperty(nameof(AvatarData.ParameterName));
            propCoordinate = serializedObject.FindProperty(nameof(AvatarData.KeyCoordinate));
            
            propVarSlots = serializedObject.FindProperty(nameof(AvatarData.VariableSlots));
            propBitCount = serializedObject.FindProperty(nameof(AvatarData.BitCount));
        }

        private void DrawUtilities()
        {
            using (new EditorStyles.BlockScope("<b>Utilities</b>"))
            {
                DrawWorldAnimatorButton();
                DrawAvatarPackageButton();
            }
        }

        private void DrawFields()
        {
            using (new EditorStyles.BlockScope("<b>Settings</b>"))
            {
                EditorGUILayout.PropertyField(propBlueprint);
                EditorGUILayout.PropertyField(propEncryption);
                EditorGUILayout.PropertyField(propWriter);

                EditorGUILayout.PropertyField(propIsLegacy);
                using (new EditorGUI.DisabledScope(!propIsLegacy.boolValue)) // < v1.3.0 would let you specify the prefix for parameter names.
                {
                    EditorGUILayout.PropertyField(propParameter);
                    EditorGUILayout.PropertyField(propCoordinate);
                }

                EditorGUILayout.Space();
                variableSlotRList.DoLayoutList();
            }
        }

        private void DrawWorldAnimatorButton()
        {
            if (!GUILayout.Button(EditorStyles.ContentWorldAssets))
                return;
            
            if (!AssetGenerator.TrySaveFolderInProjectPanel("World Animator Folder", AssetGenerator.PathWorld, "Animators", out string animatorPath))
                return;

            try
            {
                AssetDatabase.StartAssetEditing();

                var animator = AssetGenerator.CreateWorldAnimator(avatarData, animatorPath);
                SerializationUtilities.SetPropertyValue(propWriter, animator);

                serializedObject.ApplyModifiedPropertiesWithoutUndo();

                EditorGUIUtility.PingObject(animator);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                
                AssetDatabase.SaveAssets();
            }
        }

        private void DrawAvatarPackageButton()
        {
            if (!GUILayout.Button(EditorStyles.ContentAvatarAssets))
                return;

            if (!AssetGenerator.TrySaveFolderInProjectPanel("Avatar Package Folder", AssetGenerator.PathAvatar, "Packages", out string packageFolder))
                return;
            
            try
            {
                AssetDatabase.StartAssetEditing();

                string packagePath = AssetGenerator.CreateAvatarPackage(avatarData, packageFolder);
                
                EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Object>(packagePath));
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                
                AssetDatabase.SaveAssets();
            }
        }
        
        private void ReplaceVariableSlot(int index, VariableSlot slot)
        {
            SerializedProperty propertySlot = propVarSlots.GetArrayElementAtIndex(index);
            VariableSlot oldSlot = SerializationUtilities.GetPropertyValue<VariableSlot>(propertySlot);

            int oldBitCount = oldSlot.GetBitCount();
            int newBitCount = propBitCount.intValue - oldBitCount + slot.GetBitCount();
            if (newBitCount > DataConstants.MAX_BIT_COUNT) return;

            SerializationUtilities.SetPropertyValue(propertySlot, slot);
        }
    }
}
