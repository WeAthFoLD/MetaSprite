using MetaSprite.Internal;
using UnityEditor;
using UnityEngine;

namespace MetaSprite {
	[CreateAssetMenu(menuName = "Shared ASE Import Settings")]
	public class SharedASEImportSettings : ScriptableObject {
		public ASEImportSettings settings;
	}

	[CustomEditor(typeof(SharedASEImportSettings))]
	public class SharedASEImportSettingsEditor : Editor {
		public override void OnInspectorGUI() {
			EditorGUILayout.LabelField("Shared ASE Import Settings", EditorStyles.boldLabel);
			ImportSettingsEditor.Inspect(serializedObject.FindProperty("settings"));

			serializedObject.ApplyModifiedProperties();
		}
	}
}