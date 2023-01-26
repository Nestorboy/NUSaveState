using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

namespace Nessie.Udon.SaveState
{
    public static class AnimatorExtensions
    {
        #region AnimatorController
        
        public static void AddLayerNoUndo(this AnimatorController controller, AnimatorControllerLayer layer)
        {
            AnimatorControllerLayer[] layers = controller.layers;
            ArrayUtility.Add(ref layers, layer);
            controller.layers = layers;
        }
        
        public static void AddParameterNoUndo(this AnimatorController controller, AnimatorControllerParameter paramater)
        {
            AnimatorControllerParameter[] parameters = controller.parameters;
            ArrayUtility.Add(ref parameters, paramater);
            controller.parameters = parameters;
        }
        
        public static void RemoveParameterNoUndo(this AnimatorController controller, int index)
        {
            AnimatorControllerParameter[] parameters = controller.parameters;
            ArrayUtility.Remove(ref parameters, parameters[index]);
            controller.parameters = parameters;
        }
        
        #endregion AnimatorController
        
        #region AnimatorStateMachine
        
        public static AnimatorState AddStateNoUndo(this AnimatorStateMachine machine, string name, Vector3 position)
        {
            AnimatorState animatorState = new AnimatorState();
            animatorState.hideFlags = HideFlags.HideInHierarchy;
            animatorState.name = machine.MakeUniqueStateName(name);
            if (AssetDatabase.GetAssetPath(machine) != "")
                AssetDatabase.AddObjectToAsset(animatorState, AssetDatabase.GetAssetPath(machine));
            machine.AddStateNoUndo(animatorState, position);
            return animatorState;
        }
        
        public static void AddStateNoUndo(this AnimatorStateMachine machine, AnimatorState state, Vector3 position)
        {
            ChildAnimatorState[] states = machine.states;
            if (System.Array.Exists(states, childState => childState.state == state))
            {
                Debug.LogWarning(string.Format("State '{0}' already exists in state machine '{1}', discarding new state.", state.name, machine.name));
            }
            else
            {
                ArrayUtility.Add(ref states, new ChildAnimatorState()
                {
                    state = state,
                    position = position
                });
                machine.states = states;
            }
        }
        
        private static AnimatorStateTransition AddAnyStateTransitionNoUndo(this AnimatorStateMachine machine)
        {
            AnimatorStateTransition[] stateTransitions = machine.anyStateTransitions;
            AnimatorStateTransition objectToAdd = new AnimatorStateTransition();
            objectToAdd.hasExitTime = false;
            objectToAdd.hasFixedDuration = true;
            objectToAdd.duration = 0.25f;
            objectToAdd.exitTime = 0.75f;
            
            if (AssetDatabase.GetAssetPath(machine) != "")
                AssetDatabase.AddObjectToAsset(objectToAdd, AssetDatabase.GetAssetPath(machine));
            
            objectToAdd.hideFlags = HideFlags.HideInHierarchy;
            ArrayUtility.Add(ref stateTransitions, objectToAdd);
            machine.anyStateTransitions = stateTransitions;
            return objectToAdd;
        }
        
        public static AnimatorStateTransition AddAnyStateTransitionNoUndo(this AnimatorStateMachine machine, AnimatorState destinationState)
        {
            AnimatorStateTransition animatorStateTransition = machine.AddAnyStateTransitionNoUndo();
            animatorStateTransition.destinationState = destinationState;
            return animatorStateTransition;
        }
        
        #endregion AnimatorStateMachine
        
        #region AnimatorState
        
        public static AnimatorStateTransition AddTransitionNoUndo(this AnimatorState sourceState, AnimatorState destinationState)
        {
            AnimatorStateTransition transition = sourceState.CreateTransition(false);
            transition.destinationState = destinationState;
            sourceState.AddTransitionNoUndo(transition);
            return transition;
        }
        
        public static void AddTransitionNoUndo(this AnimatorState sourceState, AnimatorStateTransition transition)
        {
            AnimatorStateTransition[] transitions = sourceState.transitions;
            ArrayUtility.Add(ref transitions, transition);
            sourceState.transitions = transitions;
        }

        private static AnimatorStateTransition CreateTransition(this AnimatorState sourceState, bool setDefaultExitTime)
        {
            AnimatorStateTransition newTransition = new AnimatorStateTransition();
            newTransition.hasExitTime = false;
            newTransition.hasFixedDuration = true;

            if (AssetDatabase.GetAssetPath(sourceState) != "")
                AssetDatabase.AddObjectToAsset(newTransition, AssetDatabase.GetAssetPath(sourceState));

            newTransition.hideFlags = HideFlags.HideInHierarchy;

            if (setDefaultExitTime)
                sourceState.SetDefaultTransitionExitTime(ref newTransition);

            return newTransition;
        }
        
        private static void SetDefaultTransitionExitTime(this AnimatorState sourceState, ref AnimatorStateTransition newTransition)
        {
            newTransition.hasExitTime = true;
            if (sourceState.motion != null && sourceState.motion.averageDuration > 0.0)
            {
                float num = 0.25f / sourceState.motion.averageDuration;
                newTransition.duration = 0.25f;
                newTransition.exitTime = 1f - num;
            }
            else
            {
                newTransition.duration = 0.25f;
                newTransition.exitTime = 0.75f;
            }
        }

        #endregion AnimatorState
        
        #region BlendTree
        
        public static void AddChildNoUndo(this BlendTree tree, Motion motion) => tree.AddChildNoUndo(motion, Vector2.zero, 0.0f);
        
        private static void AddChildNoUndo(this BlendTree tree, Motion motion, Vector2 position, float threshold)
        {
            ChildMotion[] children = tree.children;
            ArrayUtility.Add(ref children, new ChildMotion()
            {
                timeScale = 1f,
                motion = motion,
                position = position,
                threshold = threshold,
                directBlendParameter = "Blend",
            });
            
            tree.children = children;
        }
        
        #endregion BlendTree
        
        #region AnimatorTransitionBase
        
        public static void AddConditionNoUndo(this AnimatorTransitionBase transition, AnimatorConditionMode mode, float threshold, string parameter)
        {
            AnimatorCondition[] conditions = transition.conditions;
            ArrayUtility.Add(ref conditions, new AnimatorCondition()
            {
                mode = mode,
                parameter = parameter,
                threshold = threshold
            });
            
            transition.conditions = conditions;
        }
        
        #endregion AnimatorTransitionBase
    }
}
