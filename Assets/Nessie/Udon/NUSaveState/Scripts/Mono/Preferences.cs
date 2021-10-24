#if UNITY_EDITOR

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Nessie.Udon.SaveState
{
    [System.Serializable]
    public class Preferences : MonoBehaviour
    {
        public DefaultAsset Folder;
        public string Seed = "";
        public string Parameter = "";

        static public Preferences GetPreferences(NUSaveState behaviour)
        {
            Preferences prefBehaviour = null;

            Preferences[] prefChildren = behaviour.GetComponentsInChildren<Preferences>();
            foreach (Preferences prefChild in prefChildren)
            {
                if (prefChild.transform.parent == behaviour.transform)
                {
                    if (prefBehaviour == null)
                        prefBehaviour = prefChild;
                    else
                    {
                        DestroyImmediate(prefChild.gameObject);

                        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                    }
                }
            }

            return prefBehaviour;
        }

        static public Preferences CreatePreferences(NUSaveState behaviour)
        {
            GameObject prefGameObject = new GameObject("NUSS_PREF");
            Preferences prefBehaviour = prefGameObject.AddComponent<Preferences>();
            prefGameObject.transform.SetParent(behaviour.transform, false);

            prefGameObject.tag = "EditorOnly";
            prefGameObject.hideFlags = HideFlags.HideInHierarchy;

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            return prefBehaviour;
        }

        public void SetVisibility(bool show)
        {
            if (show)
            {
                if (gameObject.hideFlags != HideFlags.None)
                {
                    gameObject.hideFlags = HideFlags.None;

                    EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                }
            }
            else
            {
                if (gameObject.hideFlags != HideFlags.HideInHierarchy)
                {
                    gameObject.hideFlags = HideFlags.HideInHierarchy;
                    EditorApplication.DirtyHierarchyWindowSorting();

                    EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                }
            }
        }
    }
}

#endif
