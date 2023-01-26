using System;
using UnityEditor;
using UnityEngine;

namespace Nessie.Udon.SaveState.Internal
{
    public static class EditorStyles
    {
        #region Styles
        
        // Why padding lr = 3??
        public static GUIStyle BoldLabel;
        
        public static GUIStyle RTLabel;
        public static GUIStyle RTButton;

        public static GUIStyle HelpBox;
        public static GUIStyle Box;

        #endregion Styles
        
        #region Assets
        
        private static Texture2D iconVRChat;
        private static Texture2D iconGitHub;

        #endregion Assets
        
        #region Contents
        
        public static GUIContent ContentVRChat;
        public static GUIContent ContentGitHub;
        
        public static readonly GUIContent ContentWorldAssets = new GUIContent("Generate World Animators", "Creates world-side animator controllers into the selected folder.");
        public static readonly GUIContent ContentAvatarAssets = new GUIContent("Generate Avatar Packages", "Creates avatar-side assets and leaves an exported package in the selected folder.");
        public static readonly GUIContent ContentMigrateData = new GUIContent("Migrate Legacy Data", "Takes the legacy data of the behaviour and uses it to create new AvatarDatas.");
        
        public static readonly GUIContent ContentEventReceiver = new GUIContent("Callback Receiver", "UdonBehaviour which receives the following callback events:\n_SSSaved _SSSaveFailed _SSPostSave\n_SSLoaded _SSLoadFailed _SSPostLoad\n_SSProgress");
        public static readonly GUIContent ContentFallbackAvatar = new GUIContent("Fallback Avatar", "Blueprint ID of the avatar which is switched to when the data processing is done.");

        public static readonly GUIContent ContentAvatarList = new GUIContent("Avatar", "List of AvatarDatas that define avatar information such as Blueprint ID and variable types.");
        public static readonly GUIContent ContentInstructionList = new GUIContent("Instructions", "List of UdonBehaviours variables used when saving or loading data.");

        public static readonly GUIContent ContentDefault = new GUIContent("Default Inspector", "Foldout for default UdonSharpBehaviour inspector.");
        public static readonly GUIContent ContentData = new GUIContent("NUSS Data", "EditorOnly MonoBehaviour containing the NUSaveState data.");
        public static readonly GUIContent ContentDataToggle = new GUIContent("Show NUSS Data object", "Toggle the visibility of the NUSaveState data object.");
        
        #endregion Contents

        public struct BlockScope : IDisposable
        {
            private bool disposed;

            public BlockScope(GUIContent content)
            {
                disposed = false;

                GUILayout.BeginVertical(HelpBox);
                GUILayout.Label(content, RTLabel);
                GUILayout.BeginVertical(Box);
            }
            
            public BlockScope(string label) : this(new GUIContent(label)) {}

            public void Dispose()
            {
                if (disposed) return;
                disposed = true;
                
                GUILayout.EndVertical();
                GUILayout.EndVertical();
            }
        }
        
        static EditorStyles()
        {
            InitializeStyles();
            InitializeUIAssets();
            InitializeContents();
        }

        private static void InitializeStyles()
        {
            BoldLabel = UnityEditor.EditorStyles.boldLabel;
            
            HelpBox = new GUIStyle(UnityEditor.EditorStyles.helpBox)
            {
                margin = new RectOffset(0, 0, 2, 2),
                padding = new RectOffset(0, 0, 0, UnityEditor.EditorStyles.helpBox.padding.bottom * 2),
            };
            
            Box = new GUIStyle(GUI.skin.box)
            {
                margin = new RectOffset(0, 0, 0, 0),
                padding = new RectOffset(GUI.skin.box.padding.left * 2, GUI.skin.box.padding.right * 2, GUI.skin.box.padding.top * 2, GUI.skin.box.padding.bottom * 2),
            };
            
            RTLabel = new GUIStyle(GUI.skin.label) { richText = true, };
            RTButton = new GUIStyle(GUI.skin.button) { richText = true, };
        }
        
        private static void InitializeUIAssets()
        {
            iconVRChat = Resources.Load<Texture2D>("Icons/VRChat-Emblem-32px");
            iconGitHub = Resources.Load<Texture2D>("Icons/GitHub-Mark-32px");
        }

        private static void InitializeContents()
        {
            ContentVRChat = new GUIContent(iconVRChat, "VRChat");
            ContentGitHub = new GUIContent(iconGitHub, "Github");
        }
    }
}
