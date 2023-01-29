
using System;
using UnityEngine;
using UnityEngine.Serialization;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Serialization.OdinSerializer;
using UdonSharp;
using Debug = Nessie.Udon.SaveState.DebugUtilities;

namespace Nessie.Udon.SaveState
{
    [AddComponentMenu("Nessie/NUSaveState")]
    public class NUSaveState : UdonSharpBehaviour
    {
        #region Serialized Public Fields
        
        [FormerlySerializedAs("CallbackReciever")]
        public UdonBehaviour CallbackReceiver;
        public string FallbackAvatarID = "avtr_c38a1615-5bf5-42b4-84eb-a8b6c37cbd11";

        #endregion Serialized Public Fields

        #region Serialized Private Fields

        [Header("Prefabs")]
        [SerializeField] private GameObject stationPrefab;
        [SerializeField] private Transform pedestalContainer;
        [SerializeField] private GameObject pedestalPrefab;

        [Header("Controllers")]
        [SerializeField] private RuntimeAnimatorController[] parameterWriters;

        [Header("Avatars")]
        [SerializeField] private string[] dataAvatarIDs;
        [SerializeField] private Vector3[] dataKeyCoords;

        [Header("Instructions")]
        [SerializeField] private int[] bufferBitCounts;
        [OdinSerialize] private Component[][] bufferUdonBehaviours;
        [OdinSerialize] private string[][] bufferVariables;
        [OdinSerialize] private TypeEnum[][] bufferTypes;

        #endregion Serialized Private Fields

        #region Private Fields

        private VRCPlayerApi localPlayer;

        private BoxCollider keyDetector;
        private VRCStation dataWriter;

        private VRC_AvatarPedestal[] dataAvatarPedestals;
        private VRC_AvatarPedestal fallbackAvatarPedestal;

        private byte[][] bufferBytes;

        private object[] dataBones = new object[]
        {
            HumanBodyBones.LeftIndexDistal,
            HumanBodyBones.LeftMiddleDistal,
            HumanBodyBones.LeftRingDistal,
            HumanBodyBones.LeftLittleDistal,
            HumanBodyBones.RightIndexDistal,
            HumanBodyBones.RightMiddleDistal,
            HumanBodyBones.RightRingDistal,
            HumanBodyBones.RightLittleDistal,

            HumanBodyBones.LeftIndexIntermediate,
            HumanBodyBones.LeftMiddleIntermediate,
            HumanBodyBones.LeftRingIntermediate,
            HumanBodyBones.LeftLittleIntermediate,
            HumanBodyBones.RightIndexIntermediate,
            HumanBodyBones.RightMiddleIntermediate,
            HumanBodyBones.RightRingIntermediate,
            HumanBodyBones.RightLittleIntermediate,

            HumanBodyBones.LeftIndexProximal,
            HumanBodyBones.LeftMiddleProximal,
            HumanBodyBones.LeftRingProximal,
            HumanBodyBones.LeftLittleProximal,
            HumanBodyBones.RightIndexProximal,
            HumanBodyBones.RightMiddleProximal,
            HumanBodyBones.RightRingProximal,
            HumanBodyBones.RightLittleProximal,
        };
        private float dataMinRange = 0.0009971f;
        private float dataMaxRange = 0.4999345f;

        private string[] callbackEvents = new string[]
        {
            "_SSSaved",
            "_SSLoaded",
            "_SSSaveFailed",
            "_SSLoadFailed",
            "_SSPostSave",
            "_SSPostLoad",
            "_SSProgress"
        };
        private bool avatarIsLoading;
        private ModeEnum mode;
        private StatusEnum status;

        private float avatarCurrentDuration;
        private float avatarTimeoutDuration = 10f;
        private float avatarUnloadDuration = 2f;
		
        private int dataAvatarIndex;
        private int dataByteIndex;

        #endregion Private Fields

        #region Public Fields

        [NonSerialized] public bool UseFallbackAvatar = true;
        
        [NonSerialized] public string FailReason;

        [NonSerialized] public float Progress;
        
        #endregion Public Fields

        #region private Properties
        
        private float dataProgress
        {
            set
            {
                Progress = value;

                if (CallbackReceiver)
                    CallbackReceiver.SendCustomEvent(callbackEvents[6]);

                // Debug.Log(String.Format("[NUSS] Progress: {0:P2}%", value));
            }
            get
            {
                return Progress;
            }
        }
        
        #endregion Private Properties
        
        #region Unity Events

        private void Start()
        {
            localPlayer = Networking.LocalPlayer;

            int avatarCount = dataAvatarIDs.Length;

            // Validation.
            if (parameterWriters.Length < avatarCount)
                Debug.LogError("NUSaveState is missing animator controllers.");

            if (dataAvatarIDs.Length < avatarCount)
                Debug.LogError("NUSaveState is missing avatar blueprints.");

            if (dataKeyCoords.Length < avatarCount)
                Debug.LogError("NUSaveState is missing key coordinates.");

            if (gameObject.layer != 5)
                Debug.LogError("NUSaveState behaviour is not situated on the UI layer.");

            keyDetector = GetComponent<BoxCollider>();
            if (!keyDetector)
                Debug.LogError("NUSaveState is missing the BoxCollider.");
                

            bufferBytes = PrepareBuffers();

            // Prepare data avatar pedestals.
            dataAvatarPedestals = new VRC_AvatarPedestal[avatarCount];
            for (int i = 0; i < dataAvatarPedestals.Length; i++)
            {
                dataAvatarPedestals[i] = (VRC_AvatarPedestal)Instantiate(pedestalPrefab).GetComponent(typeof(VRC_AvatarPedestal));
                dataAvatarPedestals[i].transform.SetParent(pedestalContainer, false);
                dataAvatarPedestals[i].transform.localPosition = new Vector3(0, i + 1, 0);
                dataAvatarPedestals[i].blueprintId = dataAvatarIDs[i];
            }

            // Prepare fallback avatar pedestal.
            fallbackAvatarPedestal = (VRC_AvatarPedestal)pedestalPrefab.GetComponent(typeof(VRC_AvatarPedestal));
            fallbackAvatarPedestal.transform.SetParent(pedestalContainer, false);
            fallbackAvatarPedestal.transform.localPosition = new Vector3(1, 1, 0);
            fallbackAvatarPedestal.blueprintId = FallbackAvatarID;

            // Prepare VRCStation, aka the "data writer".
            GameObject newStation = Instantiate(stationPrefab); // Instantiate a new station to make it use a relative object path remotely.
            newStation.transform.SetParent(stationPrefab.transform.parent, false);
            
            dataWriter = (VRCStation)newStation.GetComponent(typeof(VRCStation));
            if (localPlayer != null) // Prevent an error from being throw in the editor.
                dataWriter.name = $"{localPlayer.displayName} {Guid.NewGuid()}"; // Rename the station to make the path different for each user so others can't occupy it.
            dataWriter.PlayerMobility = VRCStation.Mobility.Immobilize;
            dataWriter.canUseStationFromStation = false;
        }

        private void OnParticleCollision(GameObject other)
        {
            if (avatarIsLoading)
            {
                Debug.Log($"Detected buffer avatar: {dataAvatarIndex}");

                avatarCurrentDuration = 0f;
                avatarIsLoading = false;
                keyDetector.enabled = false;

                if (mode == ModeEnum.Saving)
                {
                    SendCustomEventDelayedFrames(nameof(_ClearData), 2);
                }
                else
                    SendCustomEventDelayedFrames(nameof(_GetData), 1);
            }
            else if (status == StatusEnum.Finished)
            {
                avatarCurrentDuration = avatarUnloadDuration;
            }
        }

        #endregion Unity Events

        #region SaveState API

        public void _SSSave()
        {
            if (status != StatusEnum.Idle)
            {
                Debug.LogWarning($"Cannot save until the NUSaveState is idle. Status: {status}");
                return;
            }

            dataProgress = 0;
            
            mode = ModeEnum.Saving;
            status = StatusEnum.Processing;

            PackData(bufferBytes);
            
            dataWriter.transform.SetPositionAndRotation(localPlayer.GetPosition(), localPlayer.GetRotation()); // Put user in station to prevent movement and set the velocity parameters to 0.
            dataWriter.animatorController = null;
            dataWriter.UseStation(localPlayer);

            dataAvatarIndex = 0;
            _ChangeAvatar();
        }

        public void _SSLoad()
        {
            if (status != StatusEnum.Idle)
            {
                Debug.LogWarning($"Cannot save until the NUSaveState is idle. Status: {status}");
                return;
            }

            dataProgress = 0;
            
            mode = ModeEnum.Loading;
            status = StatusEnum.Processing;

            dataAvatarIndex = 0;
            _ChangeAvatar();
        }

        private void _SSCallback()
        {
            if (!CallbackReceiver)
                return;
            
            string callback = _GetCallback(mode, status);
            CallbackReceiver.SendCustomEvent(callback);
        }

        private string _GetCallback(ModeEnum mode, StatusEnum status)
        {
            switch (mode)
            {
                case ModeEnum.Saving:
                {
                    switch (status)
                    {
                        case StatusEnum.Processing:
                            return callbackEvents[0];
                        case StatusEnum.Failed:
                            return callbackEvents[2];
                        case StatusEnum.Finished:
                            return callbackEvents[4];
                    }
                    
                    break;
                }
                case ModeEnum.Loading:
                {
                    switch (status)
                    {
                        case StatusEnum.Processing:
                            return callbackEvents[1];
                        case StatusEnum.Failed:
                            return callbackEvents[3];
                        case StatusEnum.Finished:
                            return callbackEvents[5];
                    }

                    break;
                }
            }

            return null;
        }
        
        #endregion SaveState API

        #region SaveState Data

        private void _ChangeAvatar()
        {
            dataByteIndex = 0;

            Debug.Log($"Switching avatar to buffer avatar: {dataAvatarIndex}.");
            dataAvatarPedestals[dataAvatarIndex].SetAvatarUse(localPlayer);

            avatarCurrentDuration = avatarTimeoutDuration;
            avatarIsLoading = true;
            keyDetector.enabled = true;
            _LookForAvatar();
        }

        public void _LookForAvatar()
        {
            keyDetector.center = transform.InverseTransformPoint(localPlayer.GetBonePosition(HumanBodyBones.Hips) + localPlayer.GetBoneRotation(HumanBodyBones.Hips) * dataKeyCoords[dataAvatarIndex]);

            if (avatarIsLoading)
            {
                if (avatarCurrentDuration > 0)
                {
                    avatarCurrentDuration -= Time.deltaTime;
                    SendCustomEventDelayedFrames(nameof(_LookForAvatar), 1);
                }
                else
                {
                    Debug.LogError("Data avatar took too long to load or avatar ID is mismatched.");
                    FailReason = "Data avatar took too long to load or avatar ID is mismatched.";
                    _FailedData();
                }
            }
            else if (status == StatusEnum.Finished)
            {
                if (avatarCurrentDuration > 0)
                {
                    avatarCurrentDuration -= Time.deltaTime;
                    SendCustomEventDelayedFrames(nameof(_LookForAvatar), 1);
                }
                else
                {
                    _SSCallback();
                    status = StatusEnum.Idle;
                }
            }
        }

        public void _ClearData() // Initiate the data writing by changing the animator controller to one that stores the velocities.
        {
            dataWriter.ExitStation(localPlayer);
            dataWriter.animatorController = parameterWriters[dataAvatarIndex];
            dataWriter.UseStation(localPlayer);

            _SetData();
        }

        public void _SetData() // Write data by doing float additions.
        {
            //Log($"Writing data for avatar {dataAvatarIndex}: data byte index {dataByteIndex}");
            
            int avatarByteCount = bufferBytes[dataAvatarIndex].Length;

            bool controlBit = dataByteIndex % 6 == 0; // Mod the 9th bit in order to control the animator steps.
            byte[] avatarBytes = bufferBytes[dataAvatarIndex];
            int byte1 = dataByteIndex < avatarByteCount ? avatarBytes[dataByteIndex++] : 0;
            int byte2 = dataByteIndex < avatarByteCount ? avatarBytes[dataByteIndex++] : 0;
            int byte3 = dataByteIndex < avatarByteCount ? avatarBytes[dataByteIndex++] : 0;
            byte1 += controlBit ? 256 : 0;

            // Add 1/512th to avoid precision issues as this wont affect the conditionals in the animator.
            // Divide by 256 to normalize the range of a byte.
            // Lastly divide by 16 to account for the avatar's velocity parameter transition speed, this is then in turn multiplied by 16 in the animator controller so that it's normalized again.
            Vector3 newVelocity = (new Vector3(byte1, byte2, byte3) + (Vector3.one / 8f)) / 256f / 32f; // 8 data bits and 1 control bit (0-511)
            localPlayer.SetVelocity(localPlayer.GetRotation() * newVelocity);

            //string debugBits = $"{Convert.ToString(byte1, 2).PadLeft(8, '0')}, {Convert.ToString(byte2, 2).PadLeft(8, '0')}, {Convert.ToString(byte3, 2).PadLeft(8, '0')}";
            //string debugVels = $"{newVelocity.x}, {newVelocity.y}, {newVelocity.z}";
            //Debug.Log($"Batch {Mathf.CeilToInt(dataByteIndex / 3f)}: {debugBits} : {debugVels}");

            if (dataByteIndex < avatarByteCount)
            {
                dataProgress = (float)dataAvatarIndex / dataAvatarIDs.Length;

                SendCustomEventDelayedFrames(nameof(_SetData), 1);
            }
            else
            {
                SendCustomEventDelayedFrames(nameof(_VerifyData), 10);
            }
        }

        /// <summary>
        /// Verifies if the written data matches the input data. If successful, queues the next write or finishes write operation.
        /// </summary>
        public void _VerifyData()
        {
            int avatarByteCount = bufferBytes[dataAvatarIndex].Length;
            
            // Verify that the write was successful.
            byte[] inputData = PrepareBuffer(dataAvatarIndex);
            Array.Copy(bufferBytes[dataAvatarIndex], inputData, avatarByteCount);

            byte[] writtenData = _GetAvatarBytes(dataAvatarIndex);

            // Check for corrupt bytes.
            for (int i = 0; i < inputData.Length; i++)
            {
                if (inputData[i] != writtenData[i])
                {
                    Debug.LogError($"Data verification failed at index {i}: {inputData[i]:X2} doesn't match {writtenData[i]:X2}! Write should be restarted!");
                    FailReason = $"Data verification failed at index {i}: {inputData[i]:X2} doesn't match {writtenData[i]:X2}";
                    _FailedData();
                    return;
                }
            }

            // Continue if write was successful.
            localPlayer.SetVelocity(Vector3.zero); // Reset velocity before finishing or changing avatar.
            int newAvatarIndex = dataAvatarIndex + 1;
            if (newAvatarIndex < dataAvatarIDs.Length)
            {
                dataAvatarIndex = newAvatarIndex;
                dataProgress = (float)dataAvatarIndex / dataAvatarIDs.Length;

                _ChangeAvatar();
            }
            else
            {
                _FinishedData();
            }
        }

        public void _GetData() // Read data using finger rotations.
        {
            byte[] data = _GetAvatarBytes(dataAvatarIndex);
            
            // Append new avatar bytes to the end of the previous bytes.
            Array.Copy(data, bufferBytes[dataAvatarIndex], data.Length);

            int newAvatarIndex = dataAvatarIndex + 1;
            if (newAvatarIndex < dataAvatarIDs.Length)
            {
                dataAvatarIndex = newAvatarIndex;
                dataProgress = (float)dataAvatarIndex / dataAvatarIDs.Length;
                
                _ChangeAvatar();
            }
            else
            {
                _FinishedData();
            }
        }

        private void _FinishedData()
        {
            dataProgress = 1;

            if (mode == ModeEnum.Saving)
            {
                dataWriter.ExitStation(localPlayer); // Only exit the station once the last animator states have been reached.
                localPlayer.Immobilize(false);

                Debug.Log("Data has been saved.");
            }
            else
            {
                UnpackData(bufferBytes);

                Debug.Log("Data has been loaded.");
            }

            _SSCallback();

            if (UseFallbackAvatar)
                fallbackAvatarPedestal.SetAvatarUse(localPlayer);

            avatarCurrentDuration = avatarUnloadDuration;
            status = StatusEnum.Finished;
            keyDetector.enabled = true;
            _LookForAvatar();
        }

        private void _FailedData()
        {
            if (mode == ModeEnum.Saving)
            {
                dataWriter.ExitStation(localPlayer);
                localPlayer.Immobilize(false);
            }

            avatarIsLoading = false;
            keyDetector.enabled = false;

            status = status = StatusEnum.Failed;
            _SSCallback();
            status = StatusEnum.Idle;
        }

        private bool MoveNextAvatar(ref int avatarIndex)
        {
            if (++avatarIndex >= dataAvatarIDs.Length)
                return false;

            if (dataAvatarIDs[avatarIndex] == null)
                MoveNextAvatar(ref avatarIndex);
            
            return true;
        }
        
        private bool MoveNextInstruction(int avatarIndex, ref int instructionIndex)
        {
            if (++instructionIndex >= bufferUdonBehaviours[avatarIndex].Length)
                return false;

            DeconstructInstruction(avatarIndex, instructionIndex, out UdonBehaviour udon, out string variableName, out TypeEnum variableType);
            if (udon == null || variableName == null || variableType == TypeEnum.None)
            {
                return MoveNextInstruction(avatarIndex, ref instructionIndex);
            }
            
            return true;
        }

        private void DeconstructInstruction(int avatarIndex, int instructionIndex, out UdonBehaviour udon, out string name, out TypeEnum type)
        {
            udon = (UdonBehaviour)bufferUdonBehaviours[avatarIndex][instructionIndex];
            name = bufferVariables[avatarIndex][instructionIndex];
            type = bufferTypes[avatarIndex][instructionIndex];
        }

        private byte[][] PrepareBuffers()
        {
            byte[][] buffer = new byte[dataAvatarIDs.Length][];
            for (int avatarIndex = 0; avatarIndex < buffer.Length; avatarIndex++)
            {
                buffer[avatarIndex] = PrepareBuffer(avatarIndex);
            }

            return buffer;
        }
        
        private byte[] PrepareBuffer(int avatarIndex) => BufferUtilities.PrepareBuffer(bufferBitCounts[avatarIndex]);

        /// <summary>
        /// Returns variables from the data instructions packed into a jagged byte array.
        /// </summary>
        private void PackData(byte[][] buffers)
        {
            int avatarIndex = -1;
            while (MoveNextAvatar(ref avatarIndex))
            {
                int bitIndex = 0;
                byte[] buffer = buffers[avatarIndex];
                int instructionIndex = -1;
                while (MoveNextInstruction(avatarIndex, ref instructionIndex))
                {
                    DeconstructInstruction(avatarIndex, instructionIndex, out UdonBehaviour udon, out string variableName, out TypeEnum variableType);

                    object value = udon != null ? udon.GetProgramVariable(variableName) : null;
                    BufferUtilities.WriteBufferTypedObject(ref bitIndex, buffer, variableType, value);
                }
            }
        }

        /// <summary>
        /// Unpacks byte array into variables based on the data instructions.
        /// </summary>
        private void UnpackData(byte[][] buffers)
        {
            int avatarIndex = -1;
            while (MoveNextAvatar(ref avatarIndex))
            {
                int bitIndex = 0;
                byte[] buffer = buffers[avatarIndex];
                int instructionIndex = -1;
                while (MoveNextInstruction(avatarIndex, ref instructionIndex))
                {
                    DeconstructInstruction(avatarIndex, instructionIndex, out UdonBehaviour udon, out string variableName, out TypeEnum variableType);

                    object value = BufferUtilities.ReadBufferTypedObject(ref bitIndex, buffer, variableType);
                    if (udon != null) udon.SetProgramVariable(variableName, value);
                }
            }
        }

        private float InverseMuscle(Quaternion a, Quaternion b) // Funky numbers that make the world go round.
        {
            Quaternion deltaQ = Quaternion.Inverse(b) * a;
            float initialRange = Mathf.Abs(Mathf.Asin(deltaQ.x)) * 4 / Mathf.PI;
            float normalizedRange = (initialRange - dataMinRange) / dataMaxRange;

            return normalizedRange;
        }

        /// <summary>
        /// Returns two bytes representing an avatar parameter.
        /// </summary>
        private ushort ReadParameter(int index) // 2 bytes per parameter.
        {
            Quaternion muscleTarget = localPlayer.GetBoneRotation((HumanBodyBones)dataBones[index]);
            Quaternion muscleParent = localPlayer.GetBoneRotation((HumanBodyBones)dataBones[index + 8]);

            return (ushort)(Mathf.RoundToInt(InverseMuscle(muscleTarget, muscleParent) * 65536) & 0xFFFF);
        }

        /// <summary>
        /// Returns a byte array containing current avatars data.
        /// </summary>
        public byte[] _GetAvatarBytes(int avatarIndex)
        {
            int bitCount = bufferBitCounts[avatarIndex];
            int avatarByteCount = Mathf.CeilToInt(bitCount / 8f);

            byte[] output = new byte[avatarByteCount];

            int byteIndex = 0;

            for (int boneIndex = 0; byteIndex < avatarByteCount; boneIndex++)
            {
                ushort bytes = ReadParameter(boneIndex);
                output[byteIndex++] = (byte)(bytes & 0xFF);
                if (byteIndex < avatarByteCount)
                    output[byteIndex++] = (byte)(bytes >> (ushort)8);
            }

            return output;
        }

        #endregion SaveState Data
    }
}
