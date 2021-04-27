using UnityEngine;
using UnityEditor;
using System.IO;
using UnityEngine.Serialization;

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

    public string spriteTarget;

    public string atlasOutputDirectory = "Assets/";

    public string clipOutputDirectory = "Assets/";

    public AnimControllerOutputPolicy controllerPolicy;

    [FormerlySerializedAs("animControllerOutputPath")]
    public string animControllerOutputDirectory = "Assets/";

    public bool CheckIsValid()
    {
        return !string.IsNullOrEmpty(baseName);
    }

    public Vector2 PivotRelativePos {
        get {
            return alignment.GetRelativePos(customPivot);
        }
    }

}

[CustomEditor(typeof(ImportSettings))]
public class ImportSettingsEditor : Editor {
    private static GUIContent 
        _textBaseName = new GUIContent("Base Name", "Mainly controls prefix name of generated files."),
        _textSpriteTarget = new GUIContent("Target Child Object", "Which child object should the animation target (leave empty for root)");

    public override void OnInspectorGUI() {
        var settings = (ImportSettings) target;
        EditorGUI.BeginChangeCheck();

        using (new GUILayout.HorizontalScope(EditorStyles.toolbar)) {
            GUILayout.Label("Options");
        }

        settings.baseName = EditorGUILayout.TextField(_textBaseName, settings.baseName);
        if (string.IsNullOrEmpty(settings.baseName)) {
            EditorGUILayout.HelpBox("Base name must be specified.", MessageType.Error);
        }
        
        settings.spriteTarget = EditorGUILayout.TextField(_textSpriteTarget, settings.spriteTarget);
        EditorGUILayout.Space();

        settings.ppu = EditorGUILayout.IntField("Pixel Per Unit", settings.ppu);
        settings.alignment = (SpriteAlignment) EditorGUILayout.EnumPopup("Default Align", settings.alignment);
        if (settings.alignment == SpriteAlignment.Custom) {
            settings.customPivot = EditorGUILayout.Vector2Field("Custom Pivot", settings.customPivot);
        }

        settings.densePacked = EditorGUILayout.Toggle("Dense Pack", settings.densePacked);
        settings.border = EditorGUILayout.IntField("Border", settings.border);

        EditorGUILayout.Space();
        using (new GUILayout.HorizontalScope(EditorStyles.toolbar)) {
            GUILayout.Label("Output");
        }
        
        settings.atlasOutputDirectory = PathSelection("Atlas Directory", settings.atlasOutputDirectory);
        settings.clipOutputDirectory = PathSelection("Anim Clip Directory", settings.clipOutputDirectory);

        settings.controllerPolicy = (AnimControllerOutputPolicy) EditorGUILayout.EnumPopup("Anim Controller Policy", settings.controllerPolicy);
        if (settings.controllerPolicy == AnimControllerOutputPolicy.CreateOrOverride) {
            settings.animControllerOutputDirectory = PathSelection("Anim Controller Directory", settings.animControllerOutputDirectory);
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