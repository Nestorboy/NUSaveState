#if UNITY_EDITOR

using System;

namespace Nessie.Udon.SaveState.Data
{
    [Serializable]
    public struct VariableSlot
    {
        public string Name;
        public TypeEnum TypeEnum;
        
        public VariableSlot(string name = null, TypeEnum typeEnum = TypeEnum.None)
        {
            Name = name;
            TypeEnum = typeEnum;
        }
        
        public VariableSlot(string name = null, Type type = null) : this(name, BitUtilities.GetTypeEnum(type)) { }
        
        public VariableSlot(VariableSlot slot) : this(slot.Name, slot.TypeEnum) { }
    }
}

#endif