using UnityEngine;

namespace Nessie.Udon.SaveState
{
    public static class DebugUtilities
    {
        private const string LOG_PREFIX = "[<color=#00FF9F>SaveState</color>]";

        public static void Log(object log)
        {
            Debug.Log($"{LOG_PREFIX} {log}");
        }
        
        public static void LogWarning(object log)
        {
            Debug.LogWarning($"{LOG_PREFIX} {log}");
        }
        
        public static void LogError(object log)
        {
            Debug.LogError($"{LOG_PREFIX} {log}");
        }
    }
}