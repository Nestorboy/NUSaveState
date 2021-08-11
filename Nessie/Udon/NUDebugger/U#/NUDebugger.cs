
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;
using System;
using TMPro;

namespace UdonSharp.Nessie.Debugger
{
    [AddComponentMenu("Udon Sharp/Nessie/Debugger/NUDebugger")]
    public class NUDebugger : UdonSharpBehaviour
    {
        #region DebugFields

        [HideInInspector] public Component[] DataUdons = new Component[0];
        [HideInInspector] public int[] ProgramIndecies = new int[0];
        [HideInInspector] public string[] ProgramNames = new string[0];
        [HideInInspector] public string[][] GraphSolutions = new string[0][];
        [HideInInspector] public bool[][] GraphConditions = new bool[0][];
        [HideInInspector] public long[] SharpIDs = new long[0];
        [HideInInspector] public string[] DataType = new string[] { "Array", "Variable", "Event" };
        [HideInInspector] public string[][] DataArrays = new string[0][];
        [HideInInspector] public string[][] DataVariables = new string[0][];
        [HideInInspector] public string[][] DataEvents = new string[0][];

        #endregion DebugFields

        #region SerializedFields

        [Header("Debug Button")]

        [Tooltip("Transform used as parent for all DebugButton objects.")]
        [SerializeField] private Transform _buttonContainer = null;

        [Tooltip("GameObject used to instantiate new DebugButton objects.")]
        [SerializeField] private GameObject _buttonGameObject = null;



        [Header("Debug Text")]

        [Tooltip("Transform used as a parent for all the DebugText objects.")]
        [SerializeField] private Transform _textContainer = null;

        [Tooltip("Transform used as pivot for _poolDebugText prefab placement.")]
        [SerializeField] private Transform _textTarget = null;

        [Tooltip("Prefab used to instantiate new DebugText objects.")]
        [SerializeField] private GameObject _textPrefab = null;



        [Header("Menu Buttons")]

        [Tooltip("Animator used to animate button transitions.")]
        [SerializeField] private Animator _targetAnimator = null;

        [Tooltip("Text field used for displaying selected udon.")]
        [SerializeField] private TextMeshProUGUI _udonField = null;

        [Tooltip("Text field used for displaying selected variable.")]
        [SerializeField] private TextMeshProUGUI _typeField = null;

        [Tooltip("Text field used for displaying variable name.")]
        [SerializeField] private InputField _textField = null;



        [Header("Extra Buttons")]

        [Tooltip("Pointer used to get values for the UpdateRate setting.")]
        [SerializeField] private Slider _settingUpdateRate = null;

        [Tooltip("Text field used for displaying UpdateRate status.")]
        [SerializeField] private TextMeshProUGUI _updateRateField = null;

        [Tooltip("Pointer used to get values for the Networked setting.")]
        [SerializeField] private Toggle _settingNetworked = null;

        [Tooltip("Text field used for displaying Networking status.")]
        [SerializeField] private TextMeshProUGUI _networkedField = null;

        #endregion SerializedFields

        #region PublicFields

        [HideInInspector] public bool CustomUdon = false;
        [HideInInspector] public int UdonID = -1;
        [HideInInspector] public int ProgramID = -1;
        [HideInInspector] public int TypeID = -1;
        [HideInInspector] public int MenuID = 0;
        [HideInInspector] public int ButtonID = 0;

        // Settings. (Yes, I was too lazy to make the settings modular.)
        [HideInInspector] public Color MainColor = new Color(0f, 1f, 0.6235294f, 1f);
        [HideInInspector] public Color CrashColor = new Color(1f, 0.2705882f, 0.5294118f, 1f);
        [HideInInspector] public float UpdateRate = 0.2f;
        [HideInInspector] public bool Networked = false;

        #endregion PublicFields

        #region PrivateFields

        private UdonBehaviour _currentUdon;

        private bool _crashed = false;

        private object[] _currentArray;

        private int _buttonCount = 0;

        private NUDebuggerText[] _poolDebugText;

        private GameObject[] _poolSettingButton;

        private GameObject[] _poolButtonGOs;
        private Button[] _poolButtonButtons;
        private TextMeshProUGUI[] _poolButtonTMPs;

        #endregion PrivateFields

        private void Start()
        {
            _poolDebugText = new NUDebuggerText[0];

            _poolButtonGOs = new GameObject[] { _buttonGameObject };
            _poolButtonButtons = new Button[] { _buttonGameObject.GetComponent<Button>() };
            _poolButtonTMPs = new TextMeshProUGUI[] { _buttonGameObject.GetComponentInChildren<TextMeshProUGUI>() };

            _poolSettingButton = new GameObject[_buttonContainer.childCount - _poolButtonButtons.Length];

            int settingButtonIndex = 0;
            for (int i = 0; settingButtonIndex < _poolSettingButton.Length; i++)
            {
                GameObject childGameObject = _buttonContainer.GetChild(i).gameObject;
                if (!childGameObject.activeSelf)
                    _poolSettingButton[settingButtonIndex++] = childGameObject;
            }

            _SetColor(gameObject);
            _SetColor(_buttonGameObject);
            _SetColor(_textPrefab);

            _UpdateButtons();

            _UpdateLoop();
        }

        public void _UpdateLoop()
        {
            if (UdonID >= 0)
            {
                if (Utilities.IsValid(_currentUdon))
                {
                    if (UdonID >= 0 && !_crashed)
                    {
                        if (_crashed = !_currentUdon.enabled)
                        {
                            _udonField.color = CrashColor;
                        }
                    }
                }
                else // UdonBehaviour removed.
                {
                    if (CustomUdon)
                    {
                        UdonID = -1;
                        MenuID = 0;

                        _udonField.text = "[Removed]";
                        _udonField.color = CrashColor;

                        _UpdateButtons();
                    }
                    else
                    {
                        _RemoveUdon(UdonID);
                    }
                }
            }

            // Update debug windows.
            for (int i = 0; i < _poolDebugText.Length;)
            {
                if (!Utilities.IsValid(_poolDebugText[i]))
                {
                    Debug.Log("[<color=#00FF9F>NUDebugger</color>] Removing missing pool object from pool array.");

                    _poolDebugText = (NUDebuggerText[])ArrayRemove(_poolDebugText, i);

                    continue;
                }

                if (_poolDebugText[i].gameObject.activeSelf)
                {
                    _poolDebugText[i]._UpdateText();
                }

                i++;
            }

            SendCustomEventDelayedSeconds(nameof(_UpdateLoop), UpdateRate);
        }

        #region Menu Methods

        private void _UpdateButtons()
        {
            // Avoid turning on buttons for custom Udon target, or settings menu.
            if (MenuID > 2 && ProgramID < 0 || MenuID == 2)
            {
                for (int i = 0; i < _buttonCount; i++)
                {
                    _poolButtonGOs[i].SetActive(false);
                }
                _buttonCount = 0;

                if (MenuID == 2)
                {
                    for (int i = 0; i < _poolSettingButton.Length; i++)
                    {
                        _poolSettingButton[i].SetActive(true);
                    }

                    _updateRateField.text = $"Update Rate: {UpdateRate} s";
                    _networkedField.text = $"Networked: {Networked}";
                }
                else if (_targetAnimator.GetInteger("MenuID") == 2)
                {
                    for (int i = 0; i < _poolSettingButton.Length; i++)
                    {
                        _poolSettingButton[i].SetActive(false);
                    }
                }

                _targetAnimator.SetInteger("MenuID", MenuID);

                return;
            }

            // Turn off settings buttons if user exits settings.
            if (_targetAnimator.GetInteger("MenuID") == 2)
            {
                for (int i = 0; i < _poolSettingButton.Length; i++)
                {
                    _poolSettingButton[i].SetActive(false);
                }
            }

            switch (MenuID)
            {
                case 0: // Udon selection.

                    for (int i = 0; i < DataUdons.Length; i++)
                        if (!Utilities.IsValid(DataUdons[i]))
                            _RemoveUdon(i);

                    _currentArray = DataUdons;

                    break;

                case 1: // Type selection.

                    _currentArray = DataType;

                    break;

                case 3: // Array selection.

                    _currentArray = DataArrays[ProgramID];

                    break;

                case 4: // Variable selection

                    _currentArray = DataVariables[ProgramID];

                    break;

                case 5: // Event selection

                    _currentArray = DataEvents[ProgramID];

                    break;

                default:

                    Debug.LogWarning($"[<color=#00FF9F>NUDebugger</color>] No debug type specified.");

                    _currentArray = new object[0];

                    break;
            }


            int buttonCountNew = _currentArray == null ? 0 : _currentArray.Length;

            // Prepare buttons.
            if (_poolButtonGOs.Length < buttonCountNew)
            {
                GameObject[] newPoolGOs = new GameObject[buttonCountNew];
                Button[] newPoolButtons = new Button[buttonCountNew];
                TextMeshProUGUI[] newPoolTMPS = new TextMeshProUGUI[buttonCountNew];

                _poolButtonGOs.CopyTo(newPoolGOs, 0);
                _poolButtonButtons.CopyTo(newPoolButtons, 0);
                _poolButtonTMPs.CopyTo(newPoolTMPS, 0);

                for (int i = _poolButtonGOs.Length; i < buttonCountNew; i++)
                {
                    GameObject newObject = VRCInstantiate(_buttonGameObject);

                    newObject.transform.SetParent(_buttonContainer, false);

                    newPoolGOs[i] = newObject;
                    newPoolButtons[i] = newObject.GetComponent<Button>();
                    newPoolTMPS[i] = newObject.GetComponentInChildren<TextMeshProUGUI>();
                }

                _poolButtonGOs = newPoolGOs;
                _poolButtonButtons = newPoolButtons;
                _poolButtonTMPs = newPoolTMPS;
            }

            for (int i = 0; i < (buttonCountNew > _buttonCount ? buttonCountNew : _buttonCount); i++)
            {
                if (i < buttonCountNew)
                {
                    Color textColor;
                    string textName;

                    if (MenuID == 0)
                    {
                        // Crashed UdonBehaviour check.
                        textColor = ((UdonBehaviour)_currentArray[i]).enabled ? MainColor : CrashColor;

                        textName = ((UdonBehaviour)_currentArray[i]).name;
                        string typeName = ((UdonSharpBehaviour)_currentArray[i]).GetUdonTypeName();
                        if (typeName != "UnknownType")
                            textName += $" (U# {typeName})";
                        else
                            textName += $" ({ProgramNames[ProgramIndecies[i]]})";
                    }
                    else
                    {
                        textColor = MainColor;

                        textName = _currentArray[i].ToString();

                        if (MenuID == 3 || MenuID == 4)
                        {
                            object value = _currentUdon.GetProgramVariable((string)_currentArray[i]);
                            Type iconType;

                            if (Utilities.IsValid(value))
                                iconType = value.GetType();
                            else
                                iconType = _currentUdon.GetProgramVariableType((string)_currentArray[i]);

                            textName = $"{_CheckType(iconType)} {textName}";
                        }
                    }

                    _poolButtonTMPs[i].color = textColor;
                    _poolButtonTMPs[i].text = textName;
                }

                // Only toggle buttons if necessary.
                if (i >= (buttonCountNew < _buttonCount ? buttonCountNew : _buttonCount))
                    _poolButtonGOs[i].SetActive(i < buttonCountNew);
            }

            _buttonCount = buttonCountNew;

            _targetAnimator.SetInteger("MenuID", MenuID);
        }

        private string _CheckType(Type type)
        {
            // Debug.Log($"[<color=#00FF9F>NUDebugger</color>] Type: {type}\nName: {type.Name}\nFullName: {type.FullName}\nNamespace: {type.Namespace}\nAssembly: {type.AssemblyQualifiedName}\nGUID: {type.GUID}\nHash: {type.GetHashCode()}");

            if (type == null) return "<sprite name=Object\" tint>";

            string name = type.Name;
            string space = type.Namespace;
            string spriteName = "Object";
            
            if (space == "System")
            {
                if (name.Contains("Boolean")) spriteName = "Bool";
                else if (name.Contains("Int")) spriteName = "Int";
                else if (name.Contains("Single") || name.Contains("Double")) spriteName = "Float";
                else if (name.Contains("String")) spriteName = "String";
            }
            else
            { 
                if (name.Contains("Transform")) spriteName = "Transform";
                else if (name.Contains("Texture")) spriteName = "Texture";
                else if (name.Contains("Material")) spriteName = "Material";
                else if (name.Contains("Light")) spriteName = "Light";
                else if (name.Contains("Audio")) spriteName = "Audio";
                else if (name.Contains("Animat")) spriteName = "Animation";
                else if (name.Contains("Camera")) spriteName = "Camera";
                else if (name.Contains("Particle")) spriteName = "Particle";
                else if (name.Contains("Mesh") && space != "TextMeshPro") spriteName = "Mesh";
                else if (name.Contains("Udon")) spriteName = "Udon";
                else if (name.Contains("PlayerApi")) spriteName = "Player";
            }

            return $"<sprite name={spriteName}\" tint>";
        }

        private int _GetButtonIndex()
        {
            if (MenuID == 2)
            {
                if (!_settingUpdateRate.interactable)
                {
                    _settingUpdateRate.interactable = true;
                    return 0;
                }
                else if (!_settingNetworked.interactable)
                {
                    _settingNetworked.interactable = true;
                    return 1;
                }
            }
            else
            {
                for (int i = 0; i < _poolButtonButtons.Length; i++)
                {
                    if (!_poolButtonButtons[i].interactable)
                    {
                        _poolButtonButtons[i].interactable = true;
                        return i;
                    }
                }
            }

            return -1;
        }

        private void _CheckSelected()
        {
            if (UdonID < 0)
                MenuID = 0;
            else
                MenuID = TypeID < 0 ? 1 : TypeID + 3;
        }

        public void _SelectID()
        {
            ButtonID = _GetButtonIndex();

            if (ButtonID < 0) return;

            switch (MenuID)
            {
                case 0: // Udon selection.

                    UdonBehaviour newUdon = (UdonBehaviour)DataUdons[ButtonID];

                    if (!Utilities.IsValid(newUdon))
                    {
                        _RemoveUdon(ButtonID);

                        return;
                    }

                    CustomUdon = false;
                    _currentUdon = newUdon;

                    Color textColor = _crashed ? CrashColor : MainColor;
                    _crashed = !_currentUdon.enabled;

                    _udonField.color = textColor;

                    UdonID = ButtonID;
                    ProgramID = ProgramIndecies[UdonID];

                    // Update the UdonTarget label.
                    string textName = _currentUdon.name;
                    string typeName = ((UdonSharpBehaviour)(Component)_currentUdon).GetUdonTypeName();

                    if (typeName != "UnknownType")
                        textName += $" (U# {typeName})";
                    else
                        textName += $" ({ProgramNames[ProgramID]})";

                    _udonField.text = textName;

                    _CheckSelected();
                    _UpdateButtons();

                    break;

                case 1: // Type selection.

                    _typeField.text = DataType[ButtonID];

                    TypeID = ButtonID;

                    _CheckSelected();
                    _UpdateButtons();

                    break;

                case 2: // Setting selection.

                    if (ButtonID == 0)
                    {
                        UpdateRate = _settingUpdateRate.value / 20;
                        _updateRateField.text = $"Update Rate: {UpdateRate} s";
                    }
                    else if (ButtonID == 1)
                    {
                        Networked = _settingNetworked.isOn;
                        _networkedField.text = $"Networked: {Networked}";
                    }

                    break;

                case 3: // Array selection.

                    _textField.text = DataArrays[ProgramID][ButtonID];

                    _targetAnimator.SetTrigger("Button/Name");

                    _SelectName();

                    break;

                case 4: // Variable selection

                    _textField.text = DataVariables[ProgramID][ButtonID];

                    _targetAnimator.SetTrigger("Button/Name");

                    _SelectName();

                    break;

                case 5: // Event selection

                    _textField.text = DataEvents[ProgramID][ButtonID];

                    _targetAnimator.SetTrigger("Button/Name");

                    _SelectName();

                    break;

                default:

                    Debug.LogWarning($"[<color=#00FF9F>NUDebugger</color>] No debug type specified.");

                    _currentArray = new object[0];

                    break;
            }
        }

        public void _SelectUdon()
        {
            if (MenuID == 0)
                _CheckSelected();
            else
                MenuID = 0;

            _UpdateButtons();
        }

        public void _SelectType()
        {
            if (MenuID == 1)
                _CheckSelected();
            else
                MenuID = 1;

            _UpdateButtons();
        }

        public void _SelectSettings()
        {
            if (MenuID == 2)
                _CheckSelected();
            else
            { 
                MenuID = 2;

                _settingUpdateRate.value = UpdateRate * 20;
                _settingNetworked.isOn = Networked;
            }

            _UpdateButtons();
        }

        public void _SelectName()
        {
            if (MenuID == 0)
            {
                GameObject newGameObject = GameObject.Find(_textField.text);

                if (Utilities.IsValid(newGameObject))
                {
                    UdonBehaviour newUdon = (UdonBehaviour)newGameObject.GetComponent(typeof(UdonBehaviour));

                    if (Utilities.IsValid(newUdon))
                        _SelectCustomUdon(newUdon);
                    else
                        Debug.LogWarning($"[<color=#00FF9F>NUDebugger</color>] No UdonBehaviour found on: {newGameObject}");
                }
                else
                    Debug.LogWarning($"[<color=#00FF9F>NUDebugger</color>] No active GameObject found by the name of: {_textField.text}");
            }
            else if (UdonID < 0)
            {
                MenuID = 0;

                _UpdateButtons();
            }
            else if (TypeID < 0)
            {
                MenuID = 1;

                _UpdateButtons();
            }
            else
            {
                switch (TypeID)
                {
                    case 0:

                        _DebugArray(_currentUdon, _textField.text);

                        break;

                    case 1:

                        _DebugVariable(_currentUdon, _textField.text);

                        break;

                    case 2:

                        _DebugEvent(_currentUdon, _textField.text);

                        break;
                }

                _targetAnimator.SetTrigger("Button/Enter");
            }
        }

        public void _SelectCustomUdon(UdonBehaviour udon)
        {
            CustomUdon = true;
            _currentUdon = udon;

            _crashed = !_currentUdon.enabled;
            _udonField.color = _crashed ? CrashColor : MainColor;

            UdonID = 0;
            ProgramID = _GetProgramIndex(_currentUdon);

            // Update the UdonTarget label.
            string textName = _currentUdon.name;
            string typeName = ((UdonSharpBehaviour)(Component)_currentUdon).GetUdonTypeName();

            if (typeName != "UnknownType")
                textName += $" (U# {typeName})";
            else
                textName += $" ({ProgramNames[ProgramID]})";

            _udonField.text = textName;

            _CheckSelected();
            _UpdateButtons();
        }

        #endregion Menu Methods

        #region Misc Methods

        private object[] ArrayAdd(object[] list, object value)
        {
            object[] newArr = new object[list.Length + 1];

            list.CopyTo(newArr, 0);
            newArr[list.Length] = value;

            return newArr;
        }

        private object[] ArrayRemove(object[] list, int index)
        {
            if (index < 0 || index >= list.Length)
            {
                Debug.LogError($"Attempted to remove item at index: {index}. Array length was: {list.Length}");
                return list;
            }

            object[] newArr = new object[list.Length - 1];

            Array.Copy(list, newArr, index);

            if (index < newArr.Length)
                Array.Copy(list, index + 1, newArr, index, list.Length - index + 1);

            return newArr;
        }

        private void _SetColor(GameObject target)
        {
            TextMeshProUGUI[] TMPs;
            Text[] Texts;
            Image[] Icons;

            TMPs = target.GetComponentsInChildren<TextMeshProUGUI>(true);
            Texts = target.GetComponentsInChildren<Text>(true);
            Icons = target.GetComponentsInChildren<Image>(true);

            for (int i = 0; i < TMPs.Length; i++)
                TMPs[i].color = MainColor;
            for (int i = 0; i < Texts.Length; i++)
                Texts[i].color = MainColor;
            for (int i = 0; i < Icons.Length; i++)
                if (Icons[i].name.StartsWith("Icon-"))
                    Icons[i].color = MainColor;
        }

        private int _GetProgramIndex(UdonBehaviour udon)
        {
            long typeID = ((UdonSharpBehaviour)(Component)udon).GetUdonTypeID();
            int programIndex = -1;

            if (typeID == 0)
            {
                for (int i = 0; i < GraphSolutions.Length; i++)
                {
                    for (int j = 0; j < GraphSolutions[i].Length; j++)
                    { 
                        bool hasVariable = udon.GetProgramVariableType(GraphSolutions[i][j]) != null;
                        if (hasVariable == GraphConditions[i][j])
                        {
                            if (i + 1 >= GraphSolutions[i].Length)
                            { 
                                programIndex = i;
                                break;
                            }
                        }
                        else
                            break;
                    }
                }
            }
            else
            {
                programIndex = Array.IndexOf(SharpIDs, (long)typeID);
                if (programIndex >= 0)
                    programIndex += GraphSolutions.Length;
            }

            return programIndex;
        }

        private void _RemoveUdon(int index)
        {
            Debug.LogError("[<color=#00FF9F>NUDebugger</color>] Removing missing UdonBehaviour from UdonDebugger.");

            DataUdons = (Component[])ArrayRemove(DataUdons, index);

            int[] newIndecies = new int[ProgramIndecies.Length - 1];
            if (index < newIndecies.Length)
                Array.Copy(ProgramIndecies, index + 1, newIndecies, index, ProgramIndecies.Length - index + 1);
            ProgramIndecies = newIndecies;

            UdonID = -1;
            MenuID = 0;

            _udonField.text = "[Removed]";
            _udonField.color = CrashColor;

            _UpdateButtons();
        }

        #endregion Misc Methods

        #region Debug Methods

        private void _DebugArray(UdonBehaviour udon, string name)
        {
            NUDebuggerText udonTarget = null;
            bool usePool = false;

            // Check if there are unused objects in the pool.
            for (int i = 0; i < _poolDebugText.Length;)
            {
                if (!Utilities.IsValid(_poolDebugText[i]))
                {
                    Debug.Log("[<color=#00FF9F>NUDebugger</color>] Removing missing DebugText object from pool array.");

                    _poolDebugText = (NUDebuggerText[])ArrayRemove(_poolDebugText, i);

                    continue;
                }

                if (!_poolDebugText[i].gameObject.activeSelf)
                {
                    udonTarget = _poolDebugText[i];
                    usePool = true;

                    Debug.Log($"[<color=#00FF9F>NUDebugger</color>] Found available DebugText object at index: {i}");

                    break;
                }

                i++;
            }

            // If all objects in the pool are being used then instantiate a new one.
            if (usePool)
            {
                udonTarget.transform.SetPositionAndRotation(_textTarget.position, _textTarget.rotation);

                udonTarget.gameObject.SetActive(true);
            }
            else
            { 
                GameObject newObject = VRCInstantiate(_textPrefab);

                newObject.transform.SetPositionAndRotation(_textTarget.position, _textTarget.rotation);
                newObject.transform.parent = _textContainer;

                // Store new UdonBehaviour into pool.
                udonTarget = newObject.GetComponent<NUDebuggerText>();

                _poolDebugText = (NUDebuggerText[])ArrayAdd(_poolDebugText, udonTarget);
            }

            // Set up behaviour.
            udonTarget.TargetUdon = udon;
            udonTarget.TargetName = name;
            udonTarget.TargetType = 0; // Array.
            udonTarget.transform.localScale = _targetAnimator.transform.localScale;

            udonTarget._Initialize();
        }

        private void _DebugVariable(UdonBehaviour udon, string name)
        {
            NUDebuggerText udonTarget = null;
            bool usePool = false;

            // Check if there are unused objects in the pool.
            for (int i = 0; i < _poolDebugText.Length;)
            {
                if (!Utilities.IsValid(_poolDebugText[i]))
                {
                    Debug.Log("[<color=#00FF9F>NUDebugger</color>] Removing missing DebugText object from pool array.");

                    _poolDebugText = (NUDebuggerText[])ArrayRemove(_poolDebugText, i);

                    continue;
                }

                if (!_poolDebugText[i].gameObject.activeSelf)
                {
                    udonTarget = _poolDebugText[i];
                    usePool = true;

                    Debug.Log($"[<color=#00FF9F>NUDebugger</color>] Found available DebugText object at index: {i}");

                    break;
                }

                i++;
            }

            // If all objects in the pool are being used then instantiate a new one.
            if (usePool)
            {
                udonTarget.transform.SetPositionAndRotation(_textTarget.position, _textTarget.rotation);

                udonTarget.gameObject.SetActive(true);
            }
            else
            { 
                GameObject newObject = VRCInstantiate(_textPrefab);

                newObject.transform.SetPositionAndRotation(_textTarget.position, _textTarget.rotation);
                newObject.transform.parent = _textContainer;

                // Store new UdonBehaviour into pool.
                udonTarget = newObject.GetComponent<NUDebuggerText>();

                _poolDebugText = (NUDebuggerText[])ArrayAdd(_poolDebugText, udonTarget);
            }

            // Set up behaviour.
            udonTarget.TargetUdon = udon;
            udonTarget.TargetName = name;
            udonTarget.TargetType = 1; // Variable.
            udonTarget.transform.localScale = _targetAnimator.transform.localScale;

            udonTarget._Initialize();
        }

        private void _DebugEvent(UdonBehaviour udon, string name)
        {
            if (Networked)
                udon.SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, name);
            else
                udon.SendCustomEvent(name);
        }

        #endregion Debug Methods
    }
}