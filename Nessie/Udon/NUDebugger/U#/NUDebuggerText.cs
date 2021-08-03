
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using TMPro;

namespace UdonSharp.Nessie.Debugger
{
    [AddComponentMenu("Udon Sharp/Nessie/Debugger/NUDebugger Text")]
    public class NUDebuggerText : UdonSharpBehaviour
    {
        #region SerializedFields

        [SerializeField] private TextMeshProUGUI _targetHeader = null;
        [SerializeField] private TextMeshProUGUI _targetBody = null;

        [SerializeField] private RectTransform _handleCanvas = null;
        [SerializeField] private BoxCollider _handleCollider = null;

        [SerializeField] private RectTransform _bodyCanvas = null;
        [SerializeField] private BoxCollider _bodyCollider = null;

        #endregion SerializedFields

        #region PublicFields

        [HideInInspector] public UdonBehaviour TargetUdon = null;
        [HideInInspector] public string TargetName = "";
        [HideInInspector] public int TargetType = 0;

        #endregion PublicFields

        public void _Initialize()
        {
            if (TargetUdon == null)
            {
                Debug.Log($"[<color=#00FF9F>NUDebugger</color>] Missing UdonBehaviour reference on: {gameObject}");

                gameObject.SetActive(false);
            }

            CanvasCollider(_handleCollider, _handleCanvas);

            _UpdateText();
        }

        public void _UpdateText()
        {
            string header;
            string body;

            switch (TargetType)
            {
                case 0: // Array.

                    object[] bufferArray = (object[])TargetUdon.GetProgramVariable(TargetName);

                    if (Utilities.IsValid(bufferArray))
                    {
                        object[] newArray = new object[bufferArray.Length];
                        System.Array.Copy(bufferArray, newArray, newArray.Length);

                        header = $"{TargetName}[{newArray.Length}]";
                        body = ArrayToString(newArray);
                    }
                    else
                    {
                        header = $"{TargetName}[]";
                        body = "null";
                    }

                    _targetHeader.text = header;
                    _targetBody.text = body;

                    break;

                case 1: // Variable.

                    object bufferVariable = TargetUdon.GetProgramVariable(TargetName);

                    body = Utilities.IsValid(bufferVariable) ? VariableToString(bufferVariable) : "null";

                    header = TargetName;

                    _targetHeader.text = header;
                    _targetBody.text = body;

                    break;

                default:

                    Debug.LogWarning($"[<color=#00FF9F>NUDebugger</color>] No debug type specified.");

                    break;
            }

            CanvasCollider(_bodyCollider, _bodyCanvas);
        }

        public void _ExitText()
        {
            gameObject.SetActive(false);
        }



        // Custom functions.

        private string ArrayToString(object[] array)
        {
            string newString = null;

            // Convert array contents into single string.
            if (array.Length > 0)
            {
                int length = array.Length - 1;

                // Get VRC displayName if possible.
                for (int i = 0; i < length; i++)
                {
                    newString += $"[{i}]: {VariableToString(array[i])}\n";
                }

                newString += $"[{length}]: {VariableToString(array[length])}";
            }

            return newString;
        }

        private string VariableToString(object value)
        {
            if (value.GetType() == typeof(VRCPlayerApi))
                value = ((VRCPlayerApi)value).displayName;

            return value.ToString();
        }

        private void CanvasCollider(BoxCollider collider, RectTransform canvas)
        {
            Vector3[] corners = new Vector3[4];
            canvas.GetWorldCorners(corners);

            Vector3 newCenter = collider.transform.InverseTransformPoint((corners[0] + corners[2]) / 2);
            Vector2 newSize = canvas.rect.size * canvas.lossyScale / collider.transform.lossyScale;

            collider.center = newCenter;
            collider.size = new Vector3(newSize.x, newSize.y, 0);
        }
    }
}
