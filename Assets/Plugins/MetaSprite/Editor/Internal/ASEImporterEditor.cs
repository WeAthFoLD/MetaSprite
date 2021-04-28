#if UNITY_2020_2_OR_NEWER
using UnityEditor.AssetImporters;
#elif UNITY_2019_4_OR_NEWER
using UnityEditor.Experimental.AssetImporters;
#endif
using UnityEditor;
using UnityEngine;

namespace MetaSprite.Internal {

    [CustomEditor(typeof(ASEImporter))]
    public class ASEImporterEditor : ScriptedImporterEditor {
        public override void OnInspectorGUI() {
            var importer = (ASEImporter) target;

            serializedObject.Update();
            
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("importDirectly"), 
                new GUIContent("Import Directly"));
            if (importer.importDirectly) {
                var settingsProperty = serializedObject.FindProperty("settings");
                EditorGUILayout.PropertyField(settingsProperty);
            }
            
            serializedObject.ApplyModifiedProperties();
            
            ApplyRevertGUI();
        }
    }
}