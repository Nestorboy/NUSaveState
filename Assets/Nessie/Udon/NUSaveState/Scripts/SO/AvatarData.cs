#if UNITY_EDITOR

using UnityEngine;
using AnimatorController = UnityEditor.Animations.AnimatorController;

namespace Nessie.Udon.SaveState.Data
{
    [CreateAssetMenu(fileName = "Avatar_Data", menuName = "ScriptableObjects/Nessie/Avatar Data")]
    public class AvatarData : ScriptableObject
    {
        [Tooltip("Blueprint ID of the avatar used to store this data.")]
        public string AvatarBlueprint;

        [Tooltip("Key used to generate random coordinates used when identifying the avatar.")]
        public string EncryptionKey = System.Guid.NewGuid().ToString();

        public AnimatorController ParameterWriter;
        
        [Tooltip("Was the avatar generated before NUSaveState v1.3.0? Adds backwards compatibility.")]
        public bool IsLegacy;
        
        [Tooltip("Name used as a prefix for the parameters in the generated assets. Format: {prefix}{index}")]
        public string ParameterName = DataConstants.DEFAULT_PARAMETER_NAME;
        
        [Tooltip("Coordinate used to identify the avatar once it has loaded in.")]
        public Vector3 KeyCoordinate;
        
        public VariableSlot[] VariableSlots;
        public int BitCount;

        public string GetParameterName() => IsLegacy ? ParameterName : DataConstants.DEFAULT_PARAMETER_NAME;

        public Vector3 GetKeyCoordinate() => IsLegacy ? KeyCoordinate : GetKeyCoordinate(EncryptionKey);

        public int GetByteCount() => Mathf.CeilToInt(BitCount / 8f);

        public int GetParameterCount() => Mathf.CeilToInt(BitCount / 16f);

        public int GetPageCount() => Mathf.CeilToInt(BitCount / (float)DataConstants.BITS_PER_PAGE);
        
        public static Vector3 GetKeyCoordinate(string id)
        {
            Random.InitState(GetStableHashCode(id));
            return RandomInsideUnitCube();
        }
        
        public static Vector3 RandomInsideUnitCube() => new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), Random.Range(-1f, 1f));
        
        // Lazily implemented hash function from: https://stackoverflow.com/a/36845864
        public static int GetStableHashCode(string str)
        {
            unchecked
            {
                int hash1 = 5381;
                int hash2 = hash1;

                for (int i = 0; i < str.Length && str[i] != '\0'; i += 2)
                {
                    hash1 = ((hash1 << 5) + hash1) ^ str[i];
                    if (i == str.Length - 1 || str[i + 1] == '\0')
                        break;
                    hash2 = ((hash2 << 5) + hash2) ^ str[i + 1];
                }

                return hash1 + (hash2 * 1566083941);
            }
        }
        
        private void OnValidate()
        {
            BitCount = VariableSlots.GetBitSum();
        }
    }
}

#endif