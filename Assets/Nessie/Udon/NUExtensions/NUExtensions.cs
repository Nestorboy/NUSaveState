#if UNITY_EDITOR

using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;

namespace Nessie.Udon.Extensions
{
    public static class NUExtensions
    {
        #region Public Enums

        [Flags]
        public enum EventType
        {
            Any = ~0,
            Exposed = 1 << 0,
            Protected = 1 << 1
        }

        [Flags]
        public enum VariableType
        {
            Any = ~0,
            Public = 1 << 0,
            Private = 1 << 1,
            Internal = 1 << 2
        }

        #endregion Public Enums

        #region Public Structs

        [Serializable]
        public struct Event
        {
            public string Name;
            public EventType EventType;

            public Event (string name, EventType eventType)
            {
                Name = name;
                EventType = eventType;
            }

            public Event(Event source)
            {
                Name = source.Name;
                EventType = source.EventType;
            }
        }

        [Serializable]
        public struct Variable
        {
            public string Name;
            public VariableType VariableType;

            [SerializeField] private string typeAssemblyName;
            public Type Type
            {
                get => typeAssemblyName != null ? Type.GetType(typeAssemblyName) : null;
                set => typeAssemblyName = value?.AssemblyQualifiedName;
            }

            public Variable(string name, VariableType variableType, Type type)
            {
                Name = name;
                VariableType = variableType;

                typeAssemblyName = type.AssemblyQualifiedName;
            }

            public Variable(Variable source, Type type) : this (source.Name, source.VariableType, type) { }
            
            public Variable(Variable source) : this (source.Name, source.VariableType, source.Type) { }
        }

        #endregion Public Structs

        #region Public Extensions

        public static VariableType GetSymbolVariableType(this IUdonSymbolTable symbolTable, string symbol)
        {
            if (symbol.StartsWith("__"))
            {
                return VariableType.Internal;
            }
            
            if (symbolTable.HasExportedSymbol(symbol))
            {
                return VariableType.Public;
            }

            return VariableType.Private;
        }

        public static List<Event> GetEvents(this UdonBehaviour udon, EventType eventTypeFilter = EventType.Any)
        {
            List<Event> events = new List<Event>();

            AbstractUdonProgramSource program = udon.programSource;
            if (!program) return events;
            IUdonSymbolTable entryTable = program.SerializedProgramAsset.RetrieveProgram().EntryPoints;

            string[] entries = entryTable.GetSymbols().ToArray();
            foreach (string entry in entries)
            {
                if (eventTypeFilter.HasFlag(EventType.Exposed) && !entry.StartsWith("_"))
                {
                    events.Add(new Event(entry, EventType.Exposed));
                }
                else if (eventTypeFilter.HasFlag(EventType.Protected) && entry.StartsWith("_"))
                {
                    events.Add(new Event(entry, EventType.Protected));
                }
            }

            return events;
        }

        public static List<Variable> GetVariables(this UdonBehaviour udon, VariableType variableTypeFilter = VariableType.Any)
        {
            List<Variable> variables = new List<Variable>();

            AbstractUdonProgramSource program = udon.programSource;
            if (!program) return variables;
            IUdonSymbolTable symbolTable = program.SerializedProgramAsset.RetrieveProgram().SymbolTable;

            string[] symbols = symbolTable.GetSymbols().ToArray();
            Array.Sort(symbols);

            foreach (string symbol in symbols)
            {
                VariableType varType = symbolTable.GetSymbolVariableType(symbol);
                if (!variableTypeFilter.HasFlag(varType))
                    continue;
                
                Type type = symbolTable.GetSymbolType(symbol);
                variables.Add(new Variable(symbol, varType, type));
            }

            return variables;
        }

        public static List<Variable> GetFilteredVariables(this UdonBehaviour udon, Type[] typeFilter, VariableType variableTypeFilter = VariableType.Any)
        {
            List<Variable> variables = new List<Variable>();
            if (!udon.programSource) return variables;
            IUdonSymbolTable symbolTable = udon.programSource.SerializedProgramAsset.RetrieveProgram().SymbolTable;

            string[] symbols = symbolTable.GetSymbols().ToArray();
            Array.Sort(symbols);

            foreach (string symbol in symbols)
            {
                VariableType varType = symbolTable.GetSymbolVariableType(symbol);
                if (!variableTypeFilter.HasFlag(varType))
                    continue;
                
                Type type = symbolTable.GetSymbolType(symbol);
                if (!typeFilter.Contains(type))
                    continue;
                
                variables.Add(new Variable(symbol, varType, type));
            }

            return variables;
        }

        public static List<Variable> GetFilteredVariables(this UdonBehaviour udon, Type typeFilter, VariableType variableTypeFilter = VariableType.Any) => GetFilteredVariables(udon, new Type[]{typeFilter}, variableTypeFilter);

        #endregion Public Extensions
    }
}

#endif
