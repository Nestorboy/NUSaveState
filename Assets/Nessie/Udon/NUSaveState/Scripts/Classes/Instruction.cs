#if UNITY_EDITOR

using System;
using UnityEngine;
using VRC.Udon;
using Nessie.Udon.Extensions;

namespace Nessie.Udon.SaveState.Data
{
    [Serializable]
    public class Instruction
    {
        [SerializeField] private UdonBehaviour udon;
        [SerializeField] private VariableSlot slot;
        [SerializeField] private NUExtensions.Variable variable;

        [SerializeField] private NUExtensions.Variable[] variables;
        [SerializeField] private string[] variableLabels;
        [SerializeField] private int variableIndex = -1;
        
        public UdonBehaviour Udon
        {
            get => udon;
            set
            {
                udon = value;

                UpdateVariableOptions();
            }
        }

        public VariableSlot Slot
        {
            get => slot;
            set
            {
                slot = value;
                
                UpdateVariableOptions();
            }
        }
        
        public NUExtensions.Variable Variable => variable;

        public string[] VariableLabels => variableLabels;
        
        public int VariableIndex
        {
            get => variableIndex;
            set
            {
                variableIndex = value;

                if (value < 0 || value >= variables.Length)
                    return;
                
                variable = variables[value];
            }
        }

        public void UpdateVariableOptions(NUExtensions.VariableType variableTypeFilter = ~NUExtensions.VariableType.Internal)
        {
            if (udon == null || slot.TypeEnum <= TypeEnum.None)
            {
                variables = new NUExtensions.Variable[0];
            }
            else
            {
                variables = udon.GetFilteredVariables(BitUtilities.GetType(slot.TypeEnum), variableTypeFilter).ToArray();
            }

            variableLabels = BitUtilities.PrepareLabels(variables);
            VariableIndex = Array.IndexOf(variables, variable);
        }
        
        public Instruction()
        {

        }

        public Instruction(NUExtensions.Variable var)
        {
            variable = var;
        }
        
        public Instruction(Instruction source) : this (source.variable) { }

        public Instruction(Legacy.Instruction source)
        {
            udon = source.Udon;
            slot = new VariableSlot(source.Variable.Name, source.Variable.Type);
            variable = source.Variable;

            UpdateVariableOptions();
        }
    }
}

#endif