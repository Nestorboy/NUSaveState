#if UNITY_EDITOR

using System;
using UnityEngine;

namespace Nessie.Udon.SaveState.Data
{
    [Serializable]
    public class AvatarSlot
    {
        public AvatarData Data;
        public Instruction[] Instructions;

        public void UpdateInstructions()
        {
            int varSlotCount = Data && (Data.VariableSlots != null) ? Data.VariableSlots.Length : 0;
            Instruction[] newInstructions = new Instruction[varSlotCount];
            for (int i = 0; i < varSlotCount; i++)
            {
                newInstructions[i] = i < Instructions.Length ? Instructions[i] : new Instruction();

                newInstructions[i].Slot = Data.VariableSlots[i];
            }
            
            Instructions = newInstructions;
        }
        
        public void UpdateInstruction(int index)
        {
            Instructions[index] = index < Instructions.Length ? Instructions[index] : new Instruction();
            Instructions[index].Slot = Data.VariableSlots[index];
        }
    }
}

#endif
