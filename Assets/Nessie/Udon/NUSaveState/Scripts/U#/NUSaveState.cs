
using System;
using UnityEngine;
using UnityEngine.Serialization;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Serialization.OdinSerializer;
using UdonSharp;
using Nessie.Udon.SaveState.Data;
using Debug = Nessie.Udon.SaveState.DebugUtilities;

namespace Nessie.Udon.SaveState
{
    [AddComponentMenu("Nessie/NUSaveState")]
    public class NUSaveState : UdonSharpBehaviour
    {
        public const string PACKAGE_VERSION = "1.5.1";
        
        #region Serialized Public Fields

        public string Version = PACKAGE_VERSION;
        
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

        private float dataMinRange = 0.0009971f;
        private float dataMaxRange = 0.4999345f;
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
        
        private bool avatarIsLoading;
        private ModeEnum mode;
        private StatusEnum status;

        private byte[][] bufferBytes;
        private int currentPageIndex;
        private int currentByteIndex;
        
        private int totalAvatarCount;
        private int currentAvatarindex;

        private float avatarCurrentDuration;
        private float avatarTimeoutDuration = 10f;
        private float avatarUnloadDuration = 2f;
        
        private float progress;
        private ProgressState progressStatus = ProgressState.Complete;
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
        
        private string failReason;

        #endregion Private Fields

        #region Public Fields

        [NonSerialized] public VRC_AvatarPedestal FallbackAvatarPedestal;
        
        [NonSerialized] public bool UseFallbackAvatar = true;

        #endregion Public Fields

        #region Public Properties
        
        public int TotalAvatarCount => totalAvatarCount;
        public int CurrentAvatarIndex => currentAvatarindex;

        public ModeEnum Mode => mode;
        public StatusEnum Status => status;

        public float Progress => progress;
        public ProgressState ProgressStatus => progressStatus;

        public string FailReason => failReason;
        
        #endregion Public Properties
        
        #region Unity Events

        private void Start()
        {
            localPlayer = Networking.LocalPlayer;
            
            LogVersion();
            Validate();

            bufferBytes = PrepareBuffers();
            PrepareDataAvatarPedestals();
            PrepareWritingStation();
        }

        private void OnParticleCollision(GameObject other)
        {
            if (avatarIsLoading)
            {
                Debug.Log($"Detected buffer avatar: {currentAvatarindex}");

                avatarCurrentDuration = 0f;
                avatarIsLoading = false;
                keyDetector.enabled = false;

                if (mode == ModeEnum.Saving)
                {
                    progressStatus = ProgressState.Writing;
                    _SSProgressCallback();
                    SendCustomEventDelayedFrames(nameof(_ClearData), 2);
                }
                else
                {
                    progressStatus = ProgressState.Reading;
                    _SSProgressCallback();
                    SendCustomEventDelayedFrames(nameof(_GetData), 1);
                }
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
            
            mode = ModeEnum.Saving;
            status = StatusEnum.Processing;

            PackData(bufferBytes);
            
            dataWriter.transform.SetPositionAndRotation(localPlayer.GetPosition(), localPlayer.GetRotation()); // Put user in station to prevent movement and set the velocity parameters to 0.
            dataWriter.animatorController = null;
            dataWriter.UseStation(localPlayer);

            currentAvatarindex = 0;
            progress = 0f;
            _ChangeAvatar();
        }

        public void _SSLoad()
        {
            if (status != StatusEnum.Idle)
            {
                Debug.LogWarning($"Cannot save until the NUSaveState is idle. Status: {status}");
                return;
            }
            
            mode = ModeEnum.Loading;
            status = StatusEnum.Processing;

            currentAvatarindex = 0;
            progress = 0f;
            _ChangeAvatar();
        }

        private void _SSProgressCallback()
        {
            if (CallbackReceiver)
            {
                CallbackReceiver.SendCustomEvent(callbackEvents[6]);
            }
        }
        
        private void _SSCallback()
        {
            if (CallbackReceiver)
            {
                string callback = _GetCallback(mode, status);
                CallbackReceiver.SendCustomEvent(callback);
            }
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

        private void LogVersion()
        {
            Debug.Log($"Loaded Nessie's Udon Save State {PACKAGE_VERSION}");
        }
        
        private void Validate()
        {
            totalAvatarCount = dataAvatarIDs.Length;

            if (Version != PACKAGE_VERSION)
            {
                Debug.LogWarning($"NUSaveState version mismatch. Behaviour: {Version} Release: {PACKAGE_VERSION}");
            }
            
            if (parameterWriters.Length != totalAvatarCount)
            {
                Debug.LogError("NUSaveState is missing animator controllers.");
            }

            if (dataAvatarIDs.Length != totalAvatarCount)
            {
                Debug.LogError("NUSaveState is missing avatar blueprints.");
            }

            if (dataKeyCoords.Length != totalAvatarCount)
            {
                Debug.LogError("NUSaveState is missing key coordinates.");
            }

            if (gameObject.layer != 5)
            {
                Debug.LogError("NUSaveState behaviour is not situated on the UI layer.");
            }
            
            keyDetector = GetComponent<BoxCollider>();
            if (!keyDetector)
            {
                Debug.LogError("NUSaveState is missing the BoxCollider.");
            }

            foreach (RuntimeAnimatorController controller in parameterWriters)
            {
                if (!controller)
                {
                    Debug.LogError("NUSaveState is missing one or more Parameter Writers.");
                    break;
                }
            }
        }

        private void PrepareDataAvatarPedestals()
        {
            // Prepare data avatar pedestals.
            dataAvatarPedestals = new VRC_AvatarPedestal[totalAvatarCount];
            for (int i = 0; i < dataAvatarPedestals.Length; i++)
            {
                dataAvatarPedestals[i] = (VRC_AvatarPedestal)Instantiate(pedestalPrefab).GetComponent(typeof(VRC_AvatarPedestal));
                dataAvatarPedestals[i].transform.SetParent(pedestalContainer, false);
                dataAvatarPedestals[i].transform.localPosition = new Vector3(0, i + 1, 0);
                dataAvatarPedestals[i].blueprintId = dataAvatarIDs[i];
            }

            // Prepare fallback avatar pedestal.
            FallbackAvatarPedestal = (VRC_AvatarPedestal)pedestalPrefab.GetComponent(typeof(VRC_AvatarPedestal));
            FallbackAvatarPedestal.transform.SetParent(pedestalContainer, false);
            FallbackAvatarPedestal.transform.localPosition = new Vector3(1, 1, 0);
            FallbackAvatarPedestal.blueprintId = FallbackAvatarID;
        }
        
        private void PrepareWritingStation()
        {
            // Prepare VRCStation, aka the "data writer".
            GameObject newStation = Instantiate(stationPrefab); // Instantiate a new station to make it use a relative object path remotely.
            newStation.transform.SetParent(stationPrefab.transform.parent, false);
            
            dataWriter = (VRCStation)newStation.GetComponent(typeof(VRCStation));
            if (localPlayer != null) // Prevent an error from being throw in the editor.
                dataWriter.name = $"{localPlayer.displayName} {Guid.NewGuid()}"; // Rename the station to make the path different for each user so others can't occupy it.
            dataWriter.PlayerMobility = VRCStation.Mobility.Immobilize;
            dataWriter.canUseStationFromStation = false;
        }
        
        #endregion SaveState API

        #region SaveState Data

        private void _ChangeAvatar()
        {
            currentPageIndex = 0;
            currentByteIndex = 0;

            Debug.Log($"Switching avatar to buffer avatar: {currentAvatarindex} ({dataAvatarPedestals[currentAvatarindex].blueprintId})");
            dataAvatarPedestals[currentAvatarindex].SetAvatarUse(localPlayer);

            avatarCurrentDuration = avatarTimeoutDuration;
            avatarIsLoading = true;
            keyDetector.enabled = true;
            
            progressStatus = ProgressState.WaitingForAvatar;
            _LookForAvatar();
        }

        public void _LookForAvatar()
        {
            keyDetector.center = transform.InverseTransformPoint(localPlayer.GetBonePosition(HumanBodyBones.Hips) + localPlayer.GetBoneRotation(HumanBodyBones.Hips) * dataKeyCoords[currentAvatarindex]);

            if (avatarIsLoading)
            {
                if (avatarCurrentDuration > 0)
                {
                    //current avatar contributes to progress in 1/avatarcount increments
                    float avatarProgress = currentAvatarindex / (float)totalAvatarCount;
                    //avatar waiting time contributes a 1/avatarcount / 2 increment sized chunk, slowly increasing as avatarCurrentDuration decreases
                    float avatarTimeoutProgress = (1 - (avatarCurrentDuration / avatarTimeoutDuration)) / 2;
                    
                    progress = avatarProgress + avatarTimeoutProgress;
                    _SSProgressCallback();

                    avatarCurrentDuration -= Time.deltaTime;
                    SendCustomEventDelayedFrames(nameof(_LookForAvatar), 1);
                }
                else
                {
                    Debug.LogError("Data avatar took too long to load or avatar ID is mismatched.");
                    failReason = "Data avatar took too long to load or avatar ID is mismatched.";
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
            dataWriter.animatorController = parameterWriters[currentAvatarindex];
            dataWriter.UseStation(localPlayer);

            _SetData();
        }

        public void _SetData() // Write data by doing float additions.
        {
            //Debug.Log($"Writing data for avatar {currentAvatarindex}: data byte index {currentByteIndex}");
            
            int avatarByteCount = bufferBytes[currentAvatarindex].Length;

            bool controlBit = currentByteIndex % 6 == 0; // Mod the 9th bit in order to control the animator steps.
            byte[] avatarBytes = bufferBytes[currentAvatarindex];
            int byte1 = currentByteIndex < avatarByteCount ? avatarBytes[currentByteIndex++] : 0;
            int byte2 = currentByteIndex < avatarByteCount ? avatarBytes[currentByteIndex++] : 0;
            int byte3 = currentByteIndex < avatarByteCount ? avatarBytes[currentByteIndex++] : 0;
            byte1 |= controlBit ? 1 << 8 : 0;

            // Add 1/512th to avoid precision issues as this wont affect the conditionals in the animator.
            // Divide by 256 to normalize the range of a byte.
            // Lastly divide by 16 to account for the avatar's velocity parameter transition speed, this is then in turn multiplied by 16 in the animator controller so that it's normalized again.
            Vector3 newVelocity = (new Vector3(byte1, byte2, byte3) + (Vector3.one / 8f)) / 256f / 32f; // 8 data bits and 1 control bit (0-511)
            localPlayer.SetVelocity(localPlayer.GetRotation() * newVelocity);

            //string debugBits = $"{Convert.ToString(byte1, 2).PadLeft(8, '0')}, {Convert.ToString(byte2, 2).PadLeft(8, '0')}, {Convert.ToString(byte3, 2).PadLeft(8, '0')}";
            //string debugVels = $"{newVelocity.x}, {newVelocity.y}, {newVelocity.z}";
            //Debug.Log($"Batch {Mathf.CeilToInt(currentByteIndex / 3f)}: {debugBits} : {debugVels}");

            if (currentByteIndex < avatarByteCount)
            {
                float avatarProgress = currentAvatarindex / (float)totalAvatarCount + (1f / totalAvatarCount / 2f);
                float writeProgress = currentByteIndex / (float)avatarByteCount;
                
                //current avatar index contributes to progress in 1/avatarcount increments
                //writing data for a single avatar contributes to 1/4th an avatar worth of progress,
                //so the total avatar index amount is split between the avatar waiting, the write and the verify processes in 1/2, 1/4, 1/4 increments
                progress = avatarProgress + (writeProgress / totalAvatarCount / 4f);
                _SSProgressCallback();

                SendCustomEventDelayedFrames(nameof(_SetData), 1);
            }
            else
            {
                progressStatus = ProgressState.Verifying;
                _SSProgressCallback();
                
                currentPageIndex = 0;
                currentByteIndex = 0;
                
                SendCustomEventDelayedFrames(nameof(_VerifyData), 10); // Wait for last byte to be copied over + some padding for good measure.
            }
        }

        /// <summary>
        /// Verifies if the written data matches the input data. If successful, queues the next write or finishes write operation.
        /// </summary>
        public void _VerifyData()
        {
            if (currentPageIndex == 0)
                Debug.Log("Starting data verification...");

            int bufferOffset = currentPageIndex * DataConstants.BYTES_PER_PAGE;
            int avatarByteCount = bufferBytes[currentAvatarindex].Length;
            int pageByteCount = Mathf.Min(avatarByteCount - bufferOffset, DataConstants.BYTES_PER_PAGE);

            // Verify that the write was successful.
            byte[] inputData = bufferBytes[currentAvatarindex]; // Input bytes for the current avatar.
            byte[] writtenData = _GetPageBytes(pageByteCount);

            // Check for corrupt bytes.
            for (int byteIndex = 0; byteIndex < pageByteCount; byteIndex++)
            {
                int bufferIndex = bufferOffset + byteIndex;
                byte inputByte = inputData[bufferIndex];
                byte writtenByte = writtenData[byteIndex];
                //Debug.Log($"Byte {bufferIndex} input: {inputByte:X2} written: {writtenByte:X2}");
                if (inputByte != writtenByte)
                {
                    Debug.LogError($"Data verification failed at index {bufferIndex}: {inputByte:X2} doesn't match {writtenByte:X2}! Write should be restarted!");
                    failReason = $"Data verification failed at index {bufferIndex}: {inputByte:X2} doesn't match {writtenByte:X2}";
                    _FailedData();
                    return;
                }
            }

            int avatarPageCount = Mathf.CeilToInt(avatarByteCount / (float)DataConstants.BYTES_PER_PAGE);
            int newPageIndex = currentPageIndex + 1;
            if (newPageIndex < avatarPageCount) // Load next data page.
            {
                currentPageIndex = newPageIndex;
                Vector3 newVel = -new Vector3(0, currentPageIndex / 256f, 0);
                localPlayer.SetVelocity(localPlayer.GetRotation() * newVel);
                SendCustomEventDelayedFrames(nameof(_VerifyData), 1); // Takes a frame to switch.
                return;
            }

            localPlayer.SetVelocity(Vector3.zero); // Reset velocity before finishing or changing avatar.
            
            // Continue if write was successful.
            int newAvatarIndex = currentAvatarindex + 1;
            if (newAvatarIndex < totalAvatarCount)
            {
                currentAvatarindex = newAvatarIndex;
                
                _ChangeAvatar();
            }
            else
            {
                _FinishedData();
            }
        }

        public void _GetData() // Read data using finger rotations.
        {
            int bufferOffset = currentPageIndex * DataConstants.BYTES_PER_PAGE;
            int avatarByteCount = bufferBytes[currentAvatarindex].Length;
            int pageByteCount = Mathf.Min(avatarByteCount - bufferOffset, DataConstants.BYTES_PER_PAGE);

            byte[] pageBytes = _GetPageBytes(pageByteCount);
            Array.Copy(pageBytes, 0, bufferBytes[CurrentAvatarIndex], bufferOffset, pageByteCount);
            //for (int byteIndex = 0; byteIndex < pageByteCount; byteIndex++) Debugging.
            //{
            //    int bufferIndex = bufferOffset + byteIndex;
            //    byte pageByte = pageBytes[byteIndex];
            //    Debug.Log($"Byte {bufferIndex} read: {pageByte:X2}");
            //}
            
            int avatarPageCount = Mathf.CeilToInt(avatarByteCount / (float)DataConstants.BYTES_PER_PAGE);
            int newPageIndex = currentPageIndex + 1;
            if (newPageIndex < avatarPageCount) // Load next data page.
            {
                currentPageIndex = newPageIndex;
                Vector3 newVel = -new Vector3(0, currentPageIndex / 256f, 0);
                localPlayer.SetVelocity(localPlayer.GetRotation() * newVel);
                SendCustomEventDelayedFrames(nameof(_GetData), 1); // Takes a frame to switch.
                return;
            }
            
            int newAvatarIndex = currentAvatarindex + 1;
            if (newAvatarIndex < totalAvatarCount)
            {
                currentAvatarindex = newAvatarIndex;
                
                _ChangeAvatar();
            }
            else
            {
                _FinishedData();
            }
        }

        private void _FinishedData()
        {
            progress = 1f;
            progressStatus = ProgressState.Complete;
            _SSProgressCallback();

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
                FallbackAvatarPedestal.SetAvatarUse(localPlayer);

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

            status = StatusEnum.Failed;
            _SSCallback();
            status = StatusEnum.Idle;
        }
        
        private void DeconstructInstruction(int avatarIndex, int instructionIndex, out UdonBehaviour udon, out string name, out TypeEnum type)
        {
            udon = (UdonBehaviour)bufferUdonBehaviours[avatarIndex][instructionIndex];
            name = bufferVariables[avatarIndex][instructionIndex];
            type = bufferTypes[avatarIndex][instructionIndex];
        }

        private byte[][] PrepareBuffers()
        {
            byte[][] buffer = new byte[totalAvatarCount][];
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
            for (int avatarIndex = 0; avatarIndex < totalAvatarCount; avatarIndex++)
            {
                int bitIndex = 0;
                byte[] buffer = buffers[avatarIndex];
                for (int instructionIndex = 0; instructionIndex < bufferUdonBehaviours[avatarIndex].Length; instructionIndex++)
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
            for (int avatarIndex = 0; avatarIndex < totalAvatarCount; avatarIndex++)
            {
                int bitIndex = 0;
                byte[] buffer = buffers[avatarIndex];
                for (int instructionIndex = 0; instructionIndex < bufferUdonBehaviours[avatarIndex].Length; instructionIndex++)
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
        /// Fills a byte array with the current avatars current page data.
        /// </summary>
        public void _GetPageBytes(byte[] buffer)
        {
            int pageByteCount = Mathf.Min(buffer.Length, DataConstants.BYTES_PER_PAGE);

            int byteIndex = 0;
            for (int boneIndex = 0; byteIndex < pageByteCount; boneIndex++)
            {
                ushort bytes = ReadParameter(boneIndex);
                buffer[byteIndex++] = (byte)(bytes & 0xFF);
                if (byteIndex < pageByteCount)
                    buffer[byteIndex++] = (byte)(bytes >> (ushort)8);
            }
        }
        
        /// <summary>
        /// Returns a byte array containing the current avatars current page data.
        /// </summary>
        public byte[] _GetPageBytes(int byteCount)
        {
            int pageByteCount = Mathf.Min(byteCount, DataConstants.BYTES_PER_PAGE);
            byte[] output = new byte[pageByteCount];

            _GetPageBytes(output);
            
            return output;
        }

        #endregion SaveState Data
    }
}