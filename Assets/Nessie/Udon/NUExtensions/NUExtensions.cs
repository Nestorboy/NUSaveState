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
                set => typeAssemblyName = value.AssemblyQualifiedName;
            }

            public Variable(string name, VariableType variableType, Type type)
            {
                Name = name;
                VariableType = variableType;

                typeAssemblyName = type.AssemblyQualifiedName;
            }

            public Variable(Variable source)
            {
                Name = source.Name;
                VariableType = source.VariableType;

                typeAssemblyName = source.typeAssemblyName;
            }
        }

        #endregion Public Structs

        #region Public Extensions

        public static List<Event> GetEvents(this UdonBehaviour udon, EventType eventType = EventType.Any)
        {
            List<Event> events = new List<Event>();

            AbstractUdonProgramSource program = udon.programSource;
            if (!program) return events;
            IUdonSymbolTable entryTable = program.SerializedProgramAsset.RetrieveProgram().EntryPoints;

            string[] entries = entryTable.GetSymbols().ToArray();
            foreach (string entry in entries)
            {
                if (eventType.HasFlag(EventType.Exposed) && !entry.StartsWith("_"))
                {
                    events.Add(new Event(entry, EventType.Exposed));
                }
                else if (eventType.HasFlag(EventType.Protected) && entry.StartsWith("_"))
                {
                    events.Add(new Event(entry, EventType.Protected));
                }
            }

            return events;
        }

        public static List<Variable> GetVariables(this UdonBehaviour udon, VariableType variableType = VariableType.Any)
        {
            List<Variable> variables = new List<Variable>();

            AbstractUdonProgramSource program = udon.programSource;
            if (!program) return variables;
            IUdonSymbolTable symbolTable = program.SerializedProgramAsset.RetrieveProgram().SymbolTable;

            string[] symbols = symbolTable.GetSymbols().ToArray();
            Array.Sort(symbols);

            foreach (string symbol in symbols)
            {
                if (symbol.StartsWith("__"))
                {
                    if (variableType.HasFlag(VariableType.Internal))
                        variables.Add(new Variable(symbol, VariableType.Internal, symbolTable.GetSymbolType(symbol)));
                }
                else
                {
                    if (symbolTable.HasExportedSymbol(symbol))
                    {
                        if (variableType.HasFlag(VariableType.Public))
                            variables.Add(new Variable(symbol, VariableType.Public, symbolTable.GetSymbolType(symbol)));
                    }
                    else
                    {
                        if (variableType.HasFlag(VariableType.Private))
                            variables.Add(new Variable(symbol, VariableType.Private, symbolTable.GetSymbolType(symbol)));
                    }
                }
            }

            return variables;
        }

        public static List<Variable> GetFilteredVariables(this UdonBehaviour udon, Type[] filter, VariableType variableType = VariableType.Any)
        {
            List<Variable> variables = new List<Variable>();
            if (!udon.programSource) return variables;
            IUdonSymbolTable symbolTable = udon.programSource.SerializedProgramAsset.RetrieveProgram().SymbolTable;

            string[] symbols = symbolTable.GetSymbols().ToArray();
            Array.Sort(symbols);

            foreach (string symbol in symbols)
            {
                Type type = symbolTable.GetSymbolType(symbol);

                if (filter.Contains(type))
                {
                    if (symbol.StartsWith("__"))
                    {
                        if (variableType.HasFlag(VariableType.Internal))
                            variables.Add(new Variable(symbol, VariableType.Internal, type));
                    }
                    else
                    {
                        if (symbolTable.HasExportedSymbol(symbol))
                        {
                            if (variableType.HasFlag(VariableType.Public))
                                variables.Add(new Variable(symbol, VariableType.Public, type));
                        }
                        else
                        {
                            if (variableType.HasFlag(VariableType.Private))
                                variables.Add(new Variable(symbol, VariableType.Private, type));
                        }
                    }
                }
            }

            return variables;
        }

        #endregion Public Extensions
    }
}

#endif
