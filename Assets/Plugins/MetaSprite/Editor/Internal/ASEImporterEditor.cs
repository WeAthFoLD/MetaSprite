#if UNITY_2020_2_OR_NEWER
#define SCRIPTABLE_IMPORTERS
using UnityEditor.AssetImporters;
#elif UNITY_2019_4_OR_NEWER
#define SCRIPTABLE_IMPORTERS
using UnityEditor.Experimental.AssetImporters;
#endif
using UnityEditor;
using UnityEngine;

#if SCRIPTABLE_IMPORTERS
namespace MetaSprite.Internal {

    [CustomEditor(typeof(ASEImporter))]
    public class ASEImporterEditor : ScriptedImporterEditor {
        public override void OnInspectorGUI() {
            // EditorGUILayout.LabelField("Hello?");
            base.OnInspectorGUI();

            var importer = (ASEImporter) target;
            GUI.enabled = importer.settings;
            EditorGUILayout.BeginHorizontal();

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Import", GUILayout.Width(50))) {
                ASEImportProcess.Startup();
                ASEImportProcess.Import(importer.assetPath, importer.settings);
            }

            EditorGUILayout.EndHorizontal();
        }
    }
}
#endif