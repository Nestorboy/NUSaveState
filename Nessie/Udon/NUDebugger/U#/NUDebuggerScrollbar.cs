
using UdonSharp;
using UnityEngine;

namespace UdonSharp.Nessie.Debugger
{
    [AddComponentMenu("Udon Sharp/Nessie/Debugger/NUDebugger Scrollbar")]
    public class NUDebuggerScrollbar : UdonSharpBehaviour
    {
        // Bodged scrollbar patch.

        #region SerializedFields

        [Tooltip("Minimum size used to limit the slider.")]
        [SerializeField] private float _minSize;

        [Tooltip("Scrollbar that should be limited.")]
        [SerializeField] private UnityEngine.UI.Scrollbar _scrollbar;

        #endregion SerializedFields

        private void OnEnable()
        {
            _CheckSize();
        }

        public void _CheckSize()
        {
            if (_scrollbar.size < _minSize)
                _scrollbar.size = _minSize;
        }
    }
}
