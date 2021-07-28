
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using TMPro;

namespace UdonSharp.Nessie.Debugger
{
    [AddComponentMenu("Udon Sharp/Nessie/Debugger/NUDebugger Button")]
    public class NUDebuggerButton : UdonSharpBehaviour
    {
        #region SerializedFields

        [Tooltip("Udon Behaviour which fetches this interactables value.")]
        public NUDebugger TargetUdon = null;

        [Tooltip("Field used to display this interactables value.")]
        public TextMeshProUGUI TargetText = null;

        [Tooltip("Integer used to identify the most recently selected button.")]
        public int ButtonID = 0;

        #endregion SerializedFields

        private void Start()
        {
            if (!Utilities.IsValid(TargetUdon))
            {
                Debug.Log("[<color=#00FF9F>NUDebugger</color>] Missing TargetUdon.");
                Destroy(gameObject);
            }
        }

        public void _Pressed()
        {
            TargetUdon.ButtonID = ButtonID;

            TargetUdon._SelectID();
        }
    }
}
