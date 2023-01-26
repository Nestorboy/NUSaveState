#if UNITY_EDITOR

using System;

namespace Nessie.Udon.SaveState.Data
{
    [Serializable]
    public class AvatarSlot
    {
        public AvatarData Data;
        public Instruction[] Instructions;
    }
}

#endif
