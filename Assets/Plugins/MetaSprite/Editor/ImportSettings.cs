using UnityEngine;
using UnityEditor;
using System.IO;

namespace MetaSprite {

public enum AnimControllerOutputPolicy {
    Skip, CreateOrOverride
}

[CreateAssetMenu(menuName = "ASE Import Settings")]
public class ImportSettings : ScriptableObject {

    public int ppu = 48;

    public SpriteAlignment alignment;

    public Vector2 customPivot;

    public bool densePacked = true;

    public int border = 3;

    public string baseName = ""; // If left empty, use .ase file name

    public string spriteTarget = "";

    public string atlasOutputDirectory = "";

    public string clipOutputDirectory = "";

    public AnimControllerOutputPolicy controllerPolicy;

    public string animControllerOutputPath;

    public bool automaticReimport;

    public Vector2 PivotRelativePos {
        get {
            return alignment.GetRelativePos(customPivot);
        }
    }

}

[CustomEditor(typeof(ImportSettings))]
public class ImportSettingsEditor : Editor {

    public override void OnInspectorGUI() {
        var settings = (ImportSettings) target;
        EditorGUI.BeginChangeCheck();

        using (new GUILayout.HorizontalScope(EditorStyles.toolbar)) {
            GUILayout.Label("Options");
        }

        settings.baseName = EditorGUILayout.TextField("Base Name", settings.baseName);
        settings.spriteTarget = EditorGUILayout.TextField("Target Child Object", settings.spriteTarget);
        EditorGUILayout.Space();

        settings.ppu = EditorGUILayout.IntField("Pixel Per Unit", settings.ppu);
        settings.alignment = (SpriteAlignment) EditorGUILayout.EnumPopup("Default Align", settings.alignment);
        if (settings.alignment == SpriteAlignment.Custom) {
            settings.customPivot = EditorGUILayout.Vector2Field("Custom Pivot", settings.customPivot);
        }

        settings.densePacked = EditorGUILayout.Toggle("Dense Pack", settings.densePacked);
        settings.border = EditorGUILayout.IntField("Border", settings.border);
        settings.automaticReimport = EditorGUILayout.Toggle("Automatic Reimport", settings.automaticReimport);

        EditorGUILayout.Space();
        using (new GUILayout.HorizontalScope(EditorStyles.toolbar)) {
            GUILayout.Label("Output");
        }
        
        settings.atlasOutputDirectory = PathSelection("Atlas Directory", settings.atlasOutputDirectory);
        settings.clipOutputDirectory = PathSelection("Anim Clip Directory", settings.clipOutputDirectory);

        settings.controllerPolicy = (AnimControllerOutputPolicy) EditorGUILayout.EnumPopup("Anim Controller Policy", settings.controllerPolicy);
        if (settings.controllerPolicy == AnimControllerOutputPolicy.CreateOrOverride) {
            settings.animControllerOutputPath = PathSelection("Anim Controller Directory", settings.animControllerOutputPath);
        }

        if (EditorGUI.EndChangeCheck()) {
            EditorUtility.SetDirty(settings);
        }
    }

    string PathSelection(string id, string path) {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel(id);
        path = EditorGUILayout.TextField(path);
        if (GUILayout.Button("...", GUILayout.Width(30))) {
            path = GetAssetPath(EditorUtility.OpenFolderPanel("Select path", path, ""));
        }

        EditorGUILayout.EndHorizontal();
        return path;
    }

    static string GetAssetPath(string path) {
        if (path == null) {
            return null;
        }

        var projectPath = Application.dataPath;
        projectPath = projectPath.Substring(0, projectPath.Length - "/Assets".Length);
        path = Remove(path, projectPath);

        if (path.StartsWith("\\") || path.StartsWith("/")) {
            path = path.Remove(0, 1);
        }

        if (!path.StartsWith("Assets") && !path.StartsWith("/Assets")) {
            path = Path.Combine("Assets", path);
        }

        path.Replace('\\', '/');

        return path;
    }

    static string Remove(string s, string exactExpression) {
        return s.Replace(exactExpression, "");
    }

}

}