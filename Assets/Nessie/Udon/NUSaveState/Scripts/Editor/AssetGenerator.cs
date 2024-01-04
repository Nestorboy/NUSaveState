﻿
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDKBase;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using Nessie.Udon.Extensions;
using Nessie.Udon.SaveState.Data;

using ParameterType = UnityEngine.AnimatorControllerParameterType;

namespace Nessie.Udon.SaveState
{
    public static class AssetGenerator
    {
        public static readonly string PathNUSaveState = "Assets/Nessie/Udon/NUSaveState";

        public static readonly string PathWorld = $"{PathNUSaveState}/World";

        public static readonly string PathWorldAnimators = $"{PathWorld}/Animators";
        
        public static readonly string PathAvatar = $"{PathNUSaveState}/Avatar";
        
        public static readonly string PathAvatarAnimators = $"{PathAvatar}/Animators";
        private static readonly string PathAvatarExpressions = $"{PathAvatar}/Expressions";
        private static readonly string PathAvatarPackages = $"{PathAvatar}/Packages";
        private static readonly string PathAvatarPrefabs = $"{PathAvatar}/Prefabs";
        private static readonly string PathAvatarSOs = $"{PathAvatar}/SOs";

        private static readonly string[] MuscleNames = new string[]
        {
            "LeftHand.Index.",
            "LeftHand.Middle.",
            "LeftHand.Ring.",
            "LeftHand.Little.",
            "RightHand.Index.",
            "RightHand.Middle.",
            "RightHand.Ring.",
            "RightHand.Little.",
        };

        public static AnimatorController[] CreateWorldAnimators(AvatarData[] avatars, string folderPath)
        {
            AnimatorController[] controllers = new AnimatorController[avatars.Length];
            Dictionary<string, AnimatorController> controllerDict = new Dictionary<string, AnimatorController>();
            foreach (AvatarData avatar in avatars)
            {
                string parameterName = avatar.GetParameterName();
                AnimatorController writer = avatar.ParameterWriter;
                if (writer == null || writer.name != $"SaveState-{parameterName}")
                {
                    continue;
                }
                
                if (!controllerDict.ContainsKey(parameterName))
                    controllerDict.Add(parameterName, writer);
            }
            
            for (int avatarIndex = 0; avatarIndex < avatars.Length; avatarIndex++)
            {
                AvatarData avatar = avatars[avatarIndex];
                
                string parameterName = avatar.GetParameterName();
                if (!controllerDict.ContainsKey(parameterName))
                {
                    AnimatorController controller = CreateWorldAnimator(avatar, folderPath);
                    controllerDict.Add(parameterName, controller);
                }
                
                controllers[avatarIndex] = controllerDict[parameterName];
            }

            return controllers;
        }

        public static AnimatorController CreateWorldAnimator(AvatarData avatar, string folderPath)
        {
            ReadyPath(folderPath);

            string controllerPath = $"{folderPath}/SaveState-Write_{avatar.GetParameterName()}.controller";
            
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            
            string parameterName = avatar.GetParameterName();

            #region Parameters
            
            string[] velocityNames = new string[] { "VelocityX", "VelocityY", "VelocityZ" }; // Used when preparing the Parameters and Byte Layers.
            foreach (string velocityName in velocityNames)
                controller.AddParameterNoUndo(new AnimatorControllerParameter() { name = velocityName, type = ParameterType.Float });
            
            controller.AddParameterNoUndo(new AnimatorControllerParameter() { name = "IgnoreTransition", type = ParameterType.Bool, defaultBool = true });
            controller.AddParameterNoUndo(new AnimatorControllerParameter() { name = "Batch", type = ParameterType.Int });

            int avatarByteCount = Mathf.CeilToInt(avatar.BitCount / 8f);
            for (int byteIndex = 0; byteIndex < avatarByteCount; byteIndex++) // Prepare dummy parameters used to transfer the velocity parameters.
            {
                controller.AddParameterNoUndo(new AnimatorControllerParameter() { name = $"b{byteIndex}", type = ParameterType.Float });
            }
            
            #endregion Parameters
            
            #region Clips

            AnimationClip[][] byteClips = new AnimationClip[avatarByteCount][];
            for (int layerIndex = 0; layerIndex < byteClips.Length; layerIndex++)
            {
                byteClips[layerIndex] = new AnimationClip[8];

                for (int clipIndex = 0; clipIndex < 8; clipIndex++)
                {
                    float subtractionValue = 1f / Mathf.Pow(2, clipIndex + 1);

                    AnimationClip byteClip = new AnimationClip() { name = $"b{layerIndex}-{subtractionValue}".Replace(",", ".") };

                    byteClip.SetCurve("", typeof(Animator), $"b{layerIndex}", AnimationCurve.Linear(0, 0 - subtractionValue, 1, 1 - subtractionValue));

                    byteClips[layerIndex][clipIndex] = byteClip;
                    AssetDatabase.AddObjectToAsset(byteClip, controller);
                }
            }

            AnimationClip[] transferClips = new AnimationClip[avatarByteCount];
            for (int byteIndex = 0; byteIndex < transferClips.Length; byteIndex++)
            {
                AnimationClip transferClip = new AnimationClip() { name = $"b{byteIndex}-transfer" };

                // Subtract the control bit (1/32th) and multiply by 32. Here's the max range for example: (1 - 0.03125) * 32 = 1 * 32 - 0.03125 * 32 = 32 - 1 = 31
                transferClip.SetCurve("", typeof(Animator), $"b{byteIndex}", byteIndex % 6 == 0 ? AnimationCurve.Linear(0, -1, 1, 31) : AnimationCurve.Linear(0, 0, 1, 32));

                transferClips[byteIndex] = transferClip;
                AssetDatabase.AddObjectToAsset(transferClip, controller);
            }
            
            AnimationClip[] identityClips = new AnimationClip[avatarByteCount];
            for (int byteIndex = 0; byteIndex < identityClips.Length; byteIndex++)
            {
                AnimationClip identityClip = new AnimationClip() { name = $"b{byteIndex}-identity" };

                identityClip.SetCurve("", typeof(Animator), $"b{byteIndex}", AnimationCurve.Linear(0, 0, 1, 1)); // Create animations used to prevent animated floats from resetting when not animated.

                identityClips[byteIndex] = identityClip;
                AssetDatabase.AddObjectToAsset(identityClip, controller);
            }
            
            #endregion Clips

            #region Byte Layers
            
            for (int layerIndex = 0; layerIndex < avatarByteCount; layerIndex++)
            {
                int parameterIndex = layerIndex;

                string layerName = $"byte {layerIndex}";
                AnimatorControllerLayer byteLayer = new AnimatorControllerLayer()
                {
                    name = layerName,
                    defaultWeight = 1,
                    stateMachine = new AnimatorStateMachine()
                    {
                        name = layerName,
                        hideFlags = HideFlags.HideInHierarchy,
                    }
                };
                SetMachineDefaultPositions(byteLayer.stateMachine);

                AssetDatabase.AddObjectToAsset(byteLayer.stateMachine, controller);
                controller.AddLayerNoUndo(byteLayer);

                AnimatorStateMachine byteMachine = byteLayer.stateMachine;

                AnimatorState transferState = byteMachine.AddStateNoUndo("Transfer", new Vector3(200, 0));
                transferState.writeDefaultValues = false;
                transferState.timeParameterActive = true;
                transferState.timeParameter = velocityNames[layerIndex % 3];
                transferState.motion = transferClips[parameterIndex];

                AnimatorState finalState = byteMachine.AddStateNoUndo("Finished", new Vector3(200, 1700));
                finalState.writeDefaultValues = false;

                AnimatorState[] byteStates = new AnimatorState[16];
                for (int stepIndex = 0; stepIndex < 8; stepIndex++)
                {
                    float bitDenominator = Mathf.Pow(2, stepIndex + 1);
                    
                    var ignoreState = byteMachine.AddStateNoUndo($"Ignore {stepIndex}", new Vector3(300, 200 + stepIndex * 200));
                    ignoreState.writeDefaultValues = false;
                    ignoreState.timeParameterActive = true;
                    ignoreState.timeParameter = $"b{parameterIndex}";
                    ignoreState.motion = identityClips[parameterIndex];

                    var writeState = byteMachine.AddStateNoUndo($"b{parameterIndex}-(1/{bitDenominator})", new Vector3(100, 200 + stepIndex * 200));
                    writeState.writeDefaultValues = false;
                    writeState.timeParameterActive = true;
                    writeState.timeParameter = $"b{parameterIndex}";
                    writeState.motion = byteClips[parameterIndex][stepIndex];

                    if (stepIndex > 0)
                    {
                        for (int i = 0; i < 2; i++)
                        {
                            var ignoreTransition = byteStates[(stepIndex - 1) * 2 + i].AddTransitionNoUndo(ignoreState);
                            SetNoTransitionTimes(ignoreTransition);
                            ignoreTransition.AddConditionNoUndo(AnimatorConditionMode.Less, 1 / bitDenominator, $"b{parameterIndex}");

                            var writeTransition = byteStates[(stepIndex - 1) * 2 + i].AddTransitionNoUndo(writeState);
                            SetNoTransitionTimes(writeTransition);
                            writeTransition.AddConditionNoUndo(AnimatorConditionMode.If, 0, "IgnoreTransition");
                        }
                    }
                    else
                    {
                        var ignoreTransition = transferState.AddTransitionNoUndo(ignoreState);
                        SetNoTransitionTimes(ignoreTransition);
                        ignoreTransition.AddConditionNoUndo(AnimatorConditionMode.Less, 1 / bitDenominator, $"b{parameterIndex}");
                        ignoreTransition.AddConditionNoUndo(AnimatorConditionMode.Equals, layerIndex / 3 + 1, "Batch");

                        var writeTransition = transferState.AddTransitionNoUndo(writeState);
                        SetNoTransitionTimes(writeTransition);
                        writeTransition.AddConditionNoUndo(AnimatorConditionMode.Equals, layerIndex / 3 + 1, "Batch");
                    }

                    var byteDriver = writeState.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
                    // byte debugByte = (byte)(1 << (7 - stepIndex));
                    // byteDriver.debugString = $"[NUSS] b{layerIndex} += {Convert.ToString(debugByte, 2).PadLeft(8, '0')}";

                    byteDriver.parameters = new List<VRC_AvatarParameterDriver.Parameter>()
                    {
                        new VRC_AvatarParameterDriver.Parameter()
                        {
                            type = VRC_AvatarParameterDriver.ChangeType.Add,
                            name = $"{parameterName}_{layerIndex / 2}",
                            value = 1 / Mathf.Pow(2, stepIndex + (layerIndex & 1 ^ 1) * 8 + 1),
                        }
                    };
                    
                    byteStates[stepIndex * 2 + 1] = ignoreState;
                    byteStates[stepIndex * 2] = writeState;
                }

                var finalTransitionL = byteStates[14].AddTransitionNoUndo(finalState);
                SetNoTransitionTimes(finalTransitionL);
                finalTransitionL.AddConditionNoUndo(AnimatorConditionMode.If, 0, "IgnoreTransition");

                var finalTransitionR = byteStates[15].AddTransitionNoUndo(finalState);
                SetNoTransitionTimes(finalTransitionR);
                finalTransitionR.AddConditionNoUndo(AnimatorConditionMode.If, 0, "IgnoreTransition");
            }
            
            #endregion Byte Layers
            
            AssetDatabase.ImportAsset(controllerPath);
            
            return controller;
        }

        public static AnimatorController CreateAvatarAnimator(AvatarData avatar, string folderPath)
        {
            ReadyPath(folderPath);

            // Prepare animator controller.
            string controllerPath = $"{folderPath}/SaveState-{avatar.name}_Controller.controller";

            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            AnimatorStateMachine newStateMachine = controller.layers[0].stateMachine;
            SetMachineDefaultPositions(newStateMachine);
            
            // Prepare default animation.
            AnimationClip newDefaultClip = new AnimationClip() { name = "Default" };
            newDefaultClip.SetCurve("", typeof(Animator), "RootT.y", AnimationCurve.Constant(0, 0, 1));

            AssetDatabase.AddObjectToAsset(newDefaultClip, controller);

            // Prepare default state animation.
            controller.AddParameterNoUndo(new AnimatorControllerParameter() { name = "IsLocal", type = ParameterType.Bool, defaultBool = false });
            AnimatorState newDefaultState = newStateMachine.AddStateNoUndo("Default", new Vector3(200, 0));
            newDefaultState.motion = newDefaultClip;

            // Prepare flow control layer.
            AddFlowControlLayer(avatar, controller);
            
            // Prepare base BlendTree animation.
            AnimationClip newBaseClip = new AnimationClip() { name = "SaveState-Base" };

            newBaseClip.SetCurve("", typeof(Animator), "RootT.y", AnimationCurve.Constant(0, 0, 1));
            newBaseClip.SetCurve("SaveState-Avatar/hips/SaveState-Key", typeof(GameObject), "m_IsActive", AnimationCurve.Constant(0, 0, 1));
            
            Vector3 keyCoordinate = avatar.GetKeyCoordinate() / 100f * 50f; // Account for the scale of the armature and bounds.

            newBaseClip.SetCurve("SaveState-Avatar/hips/SaveState-Key", typeof(Transform), "m_LocalPosition.x", AnimationCurve.Constant(0, 0, keyCoordinate.x));
            newBaseClip.SetCurve("SaveState-Avatar/hips/SaveState-Key", typeof(Transform), "m_LocalPosition.y", AnimationCurve.Constant(0, 0, keyCoordinate.y));
            newBaseClip.SetCurve("SaveState-Avatar/hips/SaveState-Key", typeof(Transform), "m_LocalPosition.z", AnimationCurve.Constant(0, 0, keyCoordinate.z));
            for (int i = 0; i < MuscleNames.Length; i++)
            {
                newBaseClip.SetCurve("", typeof(Animator), $"{MuscleNames[i]}2 Stretched", AnimationCurve.Constant(0, 0, 0.81002f));
                newBaseClip.SetCurve("", typeof(Animator), $"{MuscleNames[i]}3 Stretched", AnimationCurve.Constant(0, 0, 0.81002f));
            }

            AssetDatabase.AddObjectToAsset(newBaseClip, controller);
            
            // Prepare page animations.
            string parameterName = avatar.GetParameterName();

            int maxPageClipCount = Mathf.CeilToInt(Mathf.Min(avatar.BitCount, DataConstants.BITS_PER_PAGE) / 16f); // Outputs two bytes per finger animation.
            AnimationClip[] pageClips = new AnimationClip[maxPageClipCount]; // Outputs two bytes per finger animation.
            for (int clipIndex = 0; clipIndex < pageClips.Length; clipIndex++)
            {
                AnimationClip newClip = new AnimationClip() { name = $"SaveState-{parameterName}_{clipIndex}.anim" };
                newClip.SetCurve("", typeof(Animator), $"{MuscleNames[clipIndex % MuscleNames.Length]}{3 - clipIndex / MuscleNames.Length} Stretched", AnimationCurve.Constant(0, 0, 1));
                pageClips[clipIndex] = newClip;
                
                AssetDatabase.AddObjectToAsset(newClip, controller);
            }
            
            // Prepare page BlendTrees.
            controller.AddParameterNoUndo(new AnimatorControllerParameter() { name = "Base", type = ParameterType.Float, defaultFloat = 1 });
            
            int avatarPageCount = Mathf.CeilToInt(avatar.BitCount / (float)DataConstants.BITS_PER_PAGE);

            for (int pageIndex = 0; pageIndex < avatarPageCount; pageIndex++)
            {
                AnimatorState newBlendState = controller.CreateBlendTreeInController($"Page Blend {pageIndex}", out BlendTree newTree, 0);
                ChildAnimatorState[] newChildStates = newStateMachine.states;
                newChildStates[1 + pageIndex].position = new Vector2(200, 50 + 50 * pageIndex);
                newStateMachine.states = newChildStates;
                
                AnimatorStateTransition newBlendTransition = newStateMachine.AddAnyStateTransitionNoUndo(newBlendState);
                SetNoTransitionTimes(newBlendTransition);

                newBlendTransition.AddConditionNoUndo(AnimatorConditionMode.If, 1, "IsLocal");
                float lowerBoundary = -(pageIndex + 0.5f) / 256f;
                newBlendTransition.AddConditionNoUndo(AnimatorConditionMode.Greater, lowerBoundary, "VelocityY");
                if (pageIndex != 0)
                {
                    float upperBoundary = -(pageIndex - 0.5f) / 256f;
                    newBlendTransition.AddConditionNoUndo(AnimatorConditionMode.Less, upperBoundary, "VelocityY");
                }
                
                newTree.blendType = BlendTreeType.Direct;
                newTree.AddChildNoUndo(newBaseClip);
                int pageClipCount = Mathf.CeilToInt(Mathf.Min(avatar.BitCount - pageIndex * DataConstants.BITS_PER_PAGE, DataConstants.BITS_PER_PAGE) / 16f);
                for (int clipIndex = 0; clipIndex < pageClipCount; clipIndex++)
                {
                    newTree.AddChildNoUndo(pageClips[clipIndex]);
                }
                
                // Prepare BlendTree parameters.
                ChildMotion[] newChildren = newTree.children;

                newChildren[0].directBlendParameter = "Base";
                for (int childIndex = 1; childIndex < newChildren.Length; childIndex++)
                {
                    string newParameter = $"{parameterName}_{childIndex - 1 + pageIndex * DataConstants.BITS_PER_PAGE / 16}";
                    controller.AddParameterNoUndo(new AnimatorControllerParameter() { name = newParameter, type = ParameterType.Float });
                    newChildren[childIndex].directBlendParameter = newParameter;
                }

                newTree.children = newChildren;
                
                // Prepare VRC Behaviours.
                VRCPlayableLayerControl layerControl = newBlendState.AddStateMachineBehaviour<VRCPlayableLayerControl>();
                VRCAnimatorTrackingControl trackingControl = newBlendState.AddStateMachineBehaviour<VRCAnimatorTrackingControl>();

                layerControl.goalWeight = 1;

                trackingControl.trackingLeftFingers = VRC_AnimatorTrackingControl.TrackingType.Animation;
                trackingControl.trackingRightFingers = VRC_AnimatorTrackingControl.TrackingType.Animation;
            }

            AssetDatabase.ImportAsset(controllerPath);
            
            return controller;
        }

        private static void AddFlowControlLayer(AvatarData avatar, AnimatorController controller)
        {
            string layerName = "Flow Control";
            AnimatorStateMachine batchMachine = new AnimatorStateMachine()
            {
                name = layerName,
                hideFlags = HideFlags.HideInHierarchy,
            };
            SetMachineDefaultPositions(batchMachine);
            AssetDatabase.AddObjectToAsset(batchMachine, controller);
            
            AnimatorControllerLayer flowLayer = new AnimatorControllerLayer()
            {
                name = layerName,
                defaultWeight = 1f,
                stateMachine = batchMachine,
            };
            controller.AddLayerNoUndo(flowLayer);
            
            controller.AddParameterNoUndo(new AnimatorControllerParameter() { name = "InStation", type = ParameterType.Bool });
            controller.AddParameterNoUndo(new AnimatorControllerParameter() { name = "Seated", type = ParameterType.Bool });
            controller.AddParameterNoUndo(new AnimatorControllerParameter() { name = "VelocityX", type = ParameterType.Float });
            controller.AddParameterNoUndo(new AnimatorControllerParameter() { name = "VelocityY", type = ParameterType.Float });
            controller.AddParameterNoUndo(new AnimatorControllerParameter() { name = "Batch", type = ParameterType.Int });
            
            // We're able to write three bytes per batch since we're packing one byte into each Velocity parameter.
            // Since each parameter contains two bytes, we can only clear them in sets of two, so we alternative between clearing 2 and 1 parameters.
            int avatarByteCount = Mathf.CeilToInt(avatar.BitCount / 8f);
            int avatarParameterCount = Mathf.CeilToInt(avatarByteCount / 2f);
            int avatarBatchCount = Mathf.CeilToInt(avatarByteCount / 3f);
            AnimatorState[] batchStates = new AnimatorState[avatarBatchCount + 1];

            // Empty default state to avoid having the animator controller get stuck.
            batchStates[0] = batchMachine.AddStateNoUndo("Default", new Vector3(200, 0));

            int parameterDriverIndex = 0;
            for (int stateIndex = 1; stateIndex < batchStates.Length; stateIndex++)
            {
                batchStates[stateIndex] = batchMachine.AddStateNoUndo($"Batch {stateIndex}", new Vector3(200, 100 * stateIndex));

                var batchTransition = batchStates[stateIndex - 1].AddTransitionNoUndo(batchStates[stateIndex]);
                SetNoTransitionTimes(batchTransition);

                if (stateIndex == 1)
                {
                    batchTransition.AddConditionNoUndo(AnimatorConditionMode.If, 0f, "IsLocal");
                    batchTransition.AddConditionNoUndo(AnimatorConditionMode.If, 0f, "InStation");
                    batchTransition.AddConditionNoUndo(AnimatorConditionMode.IfNot, 0f, "Seated");
                }
                batchTransition.AddConditionNoUndo(stateIndex % 2 == 0 ? AnimatorConditionMode.Less : AnimatorConditionMode.Greater, 0.03125f, "VelocityX");
                
                var batchDriver = batchStates[stateIndex].AddStateMachineBehaviour<VRCAvatarParameterDriver>();
                //batchDriver.debugString = $"[NUSS] Batch: {stateIndex}";

                var batchParameters = new List<VRC_AvatarParameterDriver.Parameter>
                {
                    new VRC_AvatarParameterDriver.Parameter()
                    {
                        type = VRC_AvatarParameterDriver.ChangeType.Set,
                        name = "Batch",
                        value = stateIndex,
                    },
                };

                string parameterName = avatar.GetParameterName();
                int parameterDriverCount = Mathf.Min(1 + (stateIndex % 2), avatarParameterCount - parameterDriverIndex);
                for (int i = 0; i < parameterDriverCount; i++)
                {
                    batchParameters.Add(new VRC_AvatarParameterDriver.Parameter()
                    {
                        type = VRC_AvatarParameterDriver.ChangeType.Set,
                        name = $"{parameterName}_{parameterDriverIndex++}",
                    });
                }

                batchDriver.parameters = batchParameters;
            }
        }

        private static void SetMachineDefaultPositions(AnimatorStateMachine machine)
        {
            machine.entryPosition = new Vector2(-30, 0);
            machine.anyStatePosition = new Vector2(-30, 50);
            machine.exitPosition = new Vector2(-30, 100);
        }
        
        private static void SetNoTransitionTimes(AnimatorStateTransition transition)
        {
            transition.duration = 0f;
            transition.exitTime = 0f;
            transition.hasExitTime = false;
        }
        
        public static VRCExpressionsMenu CreateAvatarMenu(AvatarData avatar, string folderPath)
        {
            ReadyPath(folderPath);

            VRCExpressionsMenu menu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
            menu.controls.Add(new VRCExpressionsMenu.Control()
            {
                name = "<voffset=15em><size=110%><color=#00FF9F><b>NUSaveState"
            });
            
            string menuPath = $"{folderPath}/SaveState-{avatar.name}_Menu.asset";
            AssetDatabase.CreateAsset(menu, menuPath);

            AssetDatabase.ImportAsset(menuPath);
            
            return menu;
        }
        
        public static VRCExpressionParameters CreateAvatarParameters(AvatarData avatar, string folderPath)
        {
            ReadyPath(folderPath);

            VRCExpressionParameters parameters = ScriptableObject.CreateInstance<VRCExpressionParameters>();

            string parameterName = avatar.GetParameterName();
            int paramCount = Mathf.CeilToInt(avatar.BitCount / 16f);

            VRCExpressionParameters.Parameter[] expressionControls = new VRCExpressionParameters.Parameter[paramCount];
            for (int i = 0; i < expressionControls.Length; i++)
            {
                expressionControls[i] = new VRCExpressionParameters.Parameter()
                {
                    name = $"{parameterName}_{i}",
                    valueType = VRCExpressionParameters.ValueType.Float,
                    networkSynced = false
                };
            }

            parameters.parameters = expressionControls;

            string parametersPath = $"{folderPath}/SaveState-{avatar.name}_Parameters.asset";
            AssetDatabase.CreateAsset(parameters, parametersPath);

            AssetDatabase.ImportAsset(parametersPath);
            
            return parameters;
        }
        
        public static string CreateAvatarPrefab(AvatarData avatar, AnimatorController controller, VRCExpressionsMenu menu, VRCExpressionParameters parameters, string folderPath)
        {
            ReadyPath(folderPath);
            
            string prefabPath = $"{folderPath}/SaveState-Avatar_{avatar.name}.prefab";
            
            GameObject templatePrefab = PrefabUtility.LoadPrefabContents($"{PathAvatar}/Template/SaveState-Avatar-Template.prefab");
            
            VRCAvatarDescriptor newAvatarDescriptor = templatePrefab.GetComponent<VRCAvatarDescriptor>();
            newAvatarDescriptor.expressionsMenu = menu;
            newAvatarDescriptor.expressionParameters = parameters;
            
            VRCAvatarDescriptor.CustomAnimLayer[] baseLayers = newAvatarDescriptor.baseAnimationLayers;
            baseLayers[3].animatorController = controller;
            baseLayers[4].animatorController = controller;
            
            VRCAvatarDescriptor.CustomAnimLayer[] specialLayers = newAvatarDescriptor.specialAnimationLayers;
            specialLayers[1].animatorController = controller;
            
            PrefabUtility.SaveAsPrefabAsset(templatePrefab, prefabPath);
            
            PrefabUtility.UnloadPrefabContents(templatePrefab);

            // TODO: Find a way to load prefab asset inside of AssetEditing after saving using SaveAsPrefabAsset.
            //return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            return prefabPath;
        }
        
        public static string[] CreateAvatarPackages(AvatarData[] avatars, string folderPath)
        {
            string[] paths = new string[avatars.Length];
            for (int i = 0; i < avatars.Length; i++)
            {
                paths[i] = CreateAvatarPackage(avatars[i], folderPath);
            }

            return paths;
        }
        
        public static string CreateAvatarPackage(AvatarData avatar, string folderPath)
        {
            ReadyPath(folderPath);
            
            var controller = CreateAvatarAnimator(avatar, $"{PathAvatar}/Animators");
            var menu = CreateAvatarMenu(avatar, $"{PathAvatar}/Expressions");
            var parameters = CreateAvatarParameters(avatar, $"{PathAvatar}/Expressions");
            
            string[] assetPaths = new string[]
            {
                AssetDatabase.GetAssetPath(controller),
                AssetDatabase.GetAssetPath(menu),
                AssetDatabase.GetAssetPath(parameters),
                CreateAvatarPrefab(avatar, controller, menu, parameters, $"{PathAvatar}/Prefabs"),
                    
                PathAvatar + "/Materials/Surface.mat",
                PathAvatar + "/Template/SaveState-Avatar.fbx",
            };
            
            // TODO: Get dependencies instead of being hard-coded.
            //List<string> packageAssetPaths = new List<string>();
            //foreach (string dependencyPath in AssetDatabase.GetDependencies(assetPaths, true))
            //{
            //    Debug.Log("dependency: " + dependencyPath + " " + dependencyPath.StartsWith(AssetGenerator.PathNUSaveState));
            //    if (!dependencyPath.StartsWith(AssetGenerator.PathNUSaveState))
            //        continue;
            //        
            //    packageAssetPaths.Add(dependencyPath);
            //}

            string pathUnityPackage = $"{folderPath}/SaveState-{avatar.name}_Package.unitypackage";
            AssetDatabase.ExportPackage(assetPaths, pathUnityPackage, ExportPackageOptions.Default);
            AssetDatabase.ImportAsset(pathUnityPackage);

            return pathUnityPackage;
        }
        
        public static string[] MigrateSaveStateData(NUSaveState saveState, NUSaveStateData data, string folderPath) // Does not account for overlapping data. E.g. Float split between two avatars.
        {
            if (!(data.Instructions?.Length > 0))
            {
                return null;
            }

            ReadyPath(folderPath);

            Legacy.Preferences preferences = data.Preferences;
            SerializedObject so = new SerializedObject(saveState);
            SerializedProperty propertyAvatarIDs = so.FindProperty("dataAvatarIDs");
            SerializedProperty propAvatarCoordinates = so.FindProperty("dataKeyCoords");

            bool isLegacyParameter = preferences.Parameter != DataConstants.DEFAULT_PARAMETER_NAME;

            Legacy.Instruction[][] avatarInstructions = SplitAvatarInstructions(data.Instructions);
            string[] avatarDataPaths = new string[avatarInstructions.Length];
            for (int avatarIndex = 0; avatarIndex < avatarInstructions.Length; avatarIndex++)
            {
                AvatarData avatarData = ScriptableObject.CreateInstance<AvatarData>();
                
                avatarData.AvatarBlueprint = avatarIndex < propertyAvatarIDs.arraySize ? propertyAvatarIDs.GetArrayElementAtIndex(avatarIndex).stringValue : "";
                
                bool isLegacyCoordinate = avatarIndex > 0; // First avatar was always getting the initial random values from the key, the rest depended on previous ones.
                avatarData.IsLegacy = isLegacyParameter || isLegacyCoordinate;
                if (isLegacyParameter)
                {
                    avatarData.ParameterName = preferences.Parameter;
                }

                if (!isLegacyCoordinate)
                {
                    avatarData.EncryptionKey = preferences.Seed;
                }

                if (avatarData.IsLegacy && avatarIndex < propAvatarCoordinates.arraySize)
                {
                    avatarData.KeyCoordinate = propAvatarCoordinates.GetArrayElementAtIndex(avatarIndex).vector3Value / 50f;
                }
                
                avatarData.VariableSlots = InstructionsToVariableSlots(avatarInstructions[avatarIndex]);
                avatarData.BitCount = avatarData.VariableSlots.GetBitSum();
                
                string newAvatarDataPath = AssetDatabase.GenerateUniqueAssetPath($"{folderPath}/Avatar_Data.asset");
                avatarDataPaths[avatarIndex] = newAvatarDataPath;
                AssetDatabase.CreateAsset(avatarData, newAvatarDataPath);
            }

            return avatarDataPaths;
        }

        public static VariableSlot[] InstructionsToVariableSlots(Legacy.Instruction[] instructions)
        {
            VariableSlot[] slots = new VariableSlot[instructions.Length];
            for (int i = 0; i < instructions.Length; i++)
            {
                NUExtensions.Variable variable = instructions[i].Variable;
                slots[i] = new VariableSlot(variable.Name, variable.Type);
            }

            return slots;
        }
        
        /// <summary>
        /// Splits instructions into groups where their bit sum is 256 or less and ignores overlapping instructions.
        /// </summary>
        /// <param name="instructions"></param>
        /// <returns></returns>
        public static Legacy.Instruction[][] SplitAvatarInstructions(Legacy.Instruction[] instructions)
        {
            instructions = ReorderInstructions(instructions);

            List<List<Legacy.Instruction>> instructionLists = new List<List<Legacy.Instruction>>();

            int currentBits = 0;
            int avatarIndex = 0;
            for (int i = 0; i < instructions.Length; i++)
            {
                if (currentBits == 0)
                {
                    instructionLists.Add(new List<Legacy.Instruction>());
                }
                
                Legacy.Instruction instruction = instructions[i];
                currentBits += instruction.BitCount;
                
                if (currentBits > 256)
                {
                    currentBits = 0;
                    NUExtensions.Variable variable = instruction.Variable;
                    Debug.LogWarning($"Found overlapping variable {variable.Name} ({variable.Type}). This will prevent backwards compatibility.\nPlease edit the Avatar Data asset to determine which avatar it should be stored on.");
                    continue;
                }
                
                instructionLists[avatarIndex].Add(instruction);
                
                if (currentBits == 256)
                {
                    currentBits = 0;
                    avatarIndex++;
                }
            }

            return instructionLists.Select(l => l.ToArray()).ToArray();
        }
        
        /// <summary>
        /// Puts boolean instructions at the end of the instruction stack to reflect how they were stored using the legacy system.
        /// </summary>
        /// <param name="instructions"></param>
        /// <returns></returns>
        private static Legacy.Instruction[] ReorderInstructions(Legacy.Instruction[] instructions)
        {
            Legacy.Instruction[] newInstructions = new Legacy.Instruction[instructions.Length];
            List<Legacy.Instruction> boolInstructions = new List<Legacy.Instruction>();
            
            for (int i = 0; i < instructions.Length; i++)
            {
                if (instructions[i].Variable.Type == typeof(bool))
                {
                    boolInstructions.Add(instructions[i]);
                    continue;
                }
                
                newInstructions[i - boolInstructions.Count] = instructions[i];
            }

            int boolStartIndex = newInstructions.Length - boolInstructions.Count;
            for (int i = 0; i < boolInstructions.Count; i++)
            {
                newInstructions[boolStartIndex + i] = instructions[i];
            }
            
            return newInstructions;
        }

        public static bool TrySaveFolderInProjectPanel(string title, string folder, string defaultName, out string path)
        {
            ReadyPath($"{folder}/{defaultName}");
            
            string absPath = EditorUtility.SaveFolderPanel(title, folder, defaultName);
            if (absPath.Length == 0)
            {
                path = null;
                return false;
            }
            
            path = RelativePath(absPath);
            if (path == null)
                return false;
            
            return true;
        }
        
        public static bool PathInProject(string folderPath) => folderPath.StartsWith(Application.dataPath);

        public static string RelativePath(string folderPath)
        {
            if (!PathInProject(folderPath))
                return null;

            string projectPath = Application.dataPath;
            return folderPath.Remove(0, projectPath.Length - System.IO.Path.GetFileName(projectPath).Length);
        }
        
        public static void ReadyPath(string folderPath)
        {
            if (!System.IO.Directory.Exists(folderPath))
                System.IO.Directory.CreateDirectory(folderPath);
        }
    }
}
