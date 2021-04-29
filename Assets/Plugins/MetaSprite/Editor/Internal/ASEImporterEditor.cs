#if UNITY_2020_2_OR_NEWER
using UnityEditor.AssetImporters;
#elif UNITY_2019_4_OR_NEWER
using UnityEditor.Experimental.AssetImporters;
#endif
using UnityEditor;
using UnityEngine;

namespace MetaSprite.Internal {

    static class ImportSettingsEditor {

        public static void Inspect(SerializedProperty property) {
            EditorGUILayout.PropertyField(property.FindPropertyRelative("targetChildObject"));
            EditorGUILayout.PropertyField(property.FindPropertyRelative("pixelPerUnit"));

            var alignmentProp = property.FindPropertyRelative("alignment");
            EditorGUILayout.PropertyField(alignmentProp);

            var alignment = (SpriteAlignment) alignmentProp.enumValueIndex;
            if (alignment == SpriteAlignment.Custom) {
                EditorGUILayout.PropertyField(property.FindPropertyRelative("customPivot"));
            }

            EditorGUILayout.PropertyField(property.FindPropertyRelative("densePack"));
            EditorGUILayout.PropertyField(property.FindPropertyRelative("border"));
            EditorGUILayout.PropertyField(property.FindPropertyRelative("outputController"));
        }
        
    }

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
                ImportSettingsEditor.Inspect(settingsProperty);
            }
            
            serializedObject.ApplyModifiedProperties();
            
            ApplyRevertGUI();
        }
    }
}