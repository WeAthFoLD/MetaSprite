#if UNITY_2020_2_OR_NEWER
using UnityEditor.AssetImporters;
#elif UNITY_2019_4_OR_NEWER
using UnityEditor.Experimental.AssetImporters;
#endif
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

using MetaSprite.Internal;
using System.Linq;

namespace MetaSprite {

public class ImportContext {

    // Input
    public ASEFile file;
    public string mainName;
    public ASEImportSettings settings;

    public readonly ImportOutput output = new ImportOutput();
    
    public Dictionary<string, List<Layer>> subImageLayers = new Dictionary<string, List<Layer>>();

}

public class GeneratedAtlas {
    public const string MainAtlasName = "@main";
    
    public string name; // Atlas name. '@main' for main atlas.
    public Texture2D texture; // Packed texture.
    public List<Sprite> sprites; // All sprites associated with the texture.
    
    // The local texture coordinate for bottom-left point of each frame's crop rect, in Unity texture space.
    public List<Vector2> spriteCropPositions = new List<Vector2>();
}

public class ImportOutput {
    public readonly List<GeneratedAtlas> generatedAtlasList = new List<GeneratedAtlas>();
    public readonly Dictionary<FrameTag, AnimationClip> generatedClips = new Dictionary<FrameTag, AnimationClip>();

    public GeneratedAtlas mainAtlas {
        get {
            var ret = generatedAtlasList[0];
            Debug.Assert(ret.name == GeneratedAtlas.MainAtlasName);
            return ret;
        }
    }
}

public enum ImportType {
    Direct,
    SharedSettings,
    Ignored
}

[ScriptedImporter(1, new[] { "ase", "aseprite" })]
public class ASEImporter : ScriptedImporter {

    public ImportType importType = ImportType.Direct;
    public ASEImportSettings settings = new ASEImportSettings();
    public SharedASEImportSettings sharedSettings;

    public override void OnImportAsset(AssetImportContext ctx) {
        if (importType == ImportType.Ignored)
            return;

        ASEImportSettings realSettings;
        if (importType == ImportType.Direct)
            realSettings = settings;
        else
            realSettings = sharedSettings ? sharedSettings.settings : null;

        if (realSettings == null)
            return;
        
        var output = ASEImportProcess.Import(ctx.assetPath, realSettings);

        var metadata = ScriptableObject.CreateInstance<ASEAssetMetadata>();
        metadata.name = Path.GetFileName(ctx.assetPath) + " Info";
        metadata.clips = output.generatedClips
            .Select(entry => new ClipMetadata {
                clipName = entry.Key.name,
                clip = entry.Value
            })
            .ToArray();

        ctx.AddObjectToAsset("_metadata", metadata);

        foreach (var atlas in output.generatedAtlasList) {
            bool isMain = atlas.name == GeneratedAtlas.MainAtlasName;
            
            ctx.AddObjectToAsset("atlas_" + atlas.name, atlas.texture);

            for (var i = 0; i < atlas.sprites.Count; i++) {
                var sprite = atlas.sprites[i];
                string spriteId;
                if (isMain) {
                    spriteId = "spr_" + i;
                } else {
                    spriteId = $"spr_{atlas.name}_{i}";
                }
                ctx.AddObjectToAsset(spriteId, sprite);
            }
        }
        foreach (var entry in output.generatedClips) {
            ctx.AddObjectToAsset(entry.Key.name, entry.Value);
        }

        if (realSettings.outputController) {
            ASEImportProcess.QueueAnimControllerImport(new ASEImportProcess.AnimControllerImportInfo {
                controller = realSettings.outputController,
                assetPath = ctx.assetPath
            });
        }
    }
}

public static class ASEImportProcess {

    static readonly Dictionary<string, MetaLayerProcessor> layerProcessors = new Dictionary<string, MetaLayerProcessor>();

    enum Stage {
        LoadFile,
        GenerateAtlas,
        GenerateClips,
        InvokeMetaLayerProcessor
    }

    static float GetProgress(this Stage stage) {
        return (float) (int) stage / Enum.GetValues(typeof(Stage)).Length;
    }

    static string GetDisplayString(this Stage stage) {
        return stage.ToString();
    }

    private static void _CheckStartup() {
        if (layerProcessors.Count > 0)
            return;
        
        var processorTypes = FindAllTypes(typeof(MetaLayerProcessor));
        // Debug.Log("Found " + processorTypes.Length + " layer processor(s).");
        foreach (var type in processorTypes) {
            if (type.IsAbstract) continue;
            try {
                var instance = (MetaLayerProcessor) type.GetConstructor(new Type[0]).Invoke(new object[0]);
                if (layerProcessors.ContainsKey(instance.actionName)) {
                    Debug.LogError(string.Format("Duplicate processor with name {0}: {1}", instance.actionName, instance));
                } else {
                    layerProcessors.Add(instance.actionName, instance);
                }
            } catch (Exception ex) {
                Debug.LogError("Can't instantiate meta processor " + type);
                Debug.LogException(ex);
            }
        }
    }

    static Type[] FindAllTypes(Type interfaceType) {
        var types = System.Reflection.Assembly.GetExecutingAssembly()
            .GetTypes();
        return types.Where(type => type.IsClass && interfaceType.IsAssignableFrom(type))
                    .ToArray();
    }

    struct LayerAndProcessor {
        public Layer layer;
        public MetaLayerProcessor processor;
    }

    internal static void QueueAnimControllerImport(AnimControllerImportInfo info) {
        _animControllerProcessQueue.Enqueue(info);
        if (_animControllerProcessQueue.Count == 1) {
            EditorApplication.update += _delegateProcessAnimController;
        }
    }

    public static ImportOutput Import(string path, ASEImportSettings settings) {
        _CheckStartup();

        var fileName = Path.GetFileNameWithoutExtension(path);
        var context = new ImportContext {
            mainName = fileName,
            settings = settings,
        };

        try {
            ImportStage(context, Stage.LoadFile);
            context.file = ASEParser.Parse(File.ReadAllBytes(path));        
            
            ImportStage(context, Stage.GenerateAtlas);
            context.output.generatedAtlasList.Add(AtlasGenerator.GenerateAtlas(
                context, 
                context.file.layers.Where(it => it.type == LayerType.Content).ToList(),
                GeneratedAtlas.MainAtlasName
            ));

            ImportStage(context, Stage.GenerateClips);
            GenerateAnimClips(context);

            ImportStage(context, Stage.InvokeMetaLayerProcessor);
            context.file.layers
                .Where(layer => layer.type == LayerType.Meta)
                .Select(layer => {
                    MetaLayerProcessor processor;
                    layerProcessors.TryGetValue(layer.actionName, out processor);
                    return new LayerAndProcessor { layer = layer, processor = processor };                     
                })
                .OrderBy(it => it.processor != null ? it.processor.executionOrder : 0)
                .ToList()
                .ForEach(it => {
                    var layer = it.layer;
                    var processor = it.processor;
                    if (processor != null) {
                        processor.Process(context, layer);
                    } else {
                        Debug.LogWarning(string.Format("No processor for meta layer {0}", layer.layerName));                        
                    }
                });
        } catch (Exception e) {
            Debug.LogException(e);
        }

        ImportEnd(context);

        return context.output;
    }

    static void ImportStage(ImportContext ctx, Stage stage) {
        EditorUtility.DisplayProgressBar("Importing " + ctx.mainName, stage.GetDisplayString(), stage.GetProgress());
    }

    static void ImportEnd(ImportContext ctx) {
        EditorUtility.ClearProgressBar();
    }

    public static void GenerateClipImageLayer(ImportContext ctx, string childPath, List<Sprite> frameSprites) {
        foreach (var tag in ctx.file.frameTags) {
            AnimationClip clip = ctx.output.generatedClips[tag];

            int time = 0;
            var keyFrames = new ObjectReferenceKeyframe[tag.to - tag.from + 2];
            for (int i = tag.from; i <= tag.to; ++i) {
                var aseFrame = ctx.file.frames[i];
                keyFrames[i - tag.from] = new ObjectReferenceKeyframe {
                    time = time * 1e-3f,
                    value = frameSprites[aseFrame.frameID]
                };

                time += aseFrame.duration;
            }

            keyFrames[keyFrames.Length - 1] = new ObjectReferenceKeyframe {
                time = time * 1e-3f - 1.0f / clip.frameRate,
                value = frameSprites[tag.to]
            };

            var binding = new EditorCurveBinding {
                path = childPath,
                type = typeof(SpriteRenderer),
                propertyName = "m_Sprite"
            };

            AnimationUtility.SetObjectReferenceCurve(clip, binding, keyFrames);
        }
    }

    static void GenerateAnimClips(ImportContext ctx) {
        string childPath = ctx.settings.targetChildObject;

        // Generate one animation for each tag
        foreach (var tag in ctx.file.frameTags) {
            var clip = new AnimationClip();
            clip.name = ctx.mainName + "_clip_" + tag.name;

            // Set loop property
            var loop = tag.properties.Contains("loop");
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            if (loop) {
                clip.wrapMode = WrapMode.Loop;
                settings.loopBlend = true;
                settings.loopTime = true;
            } else {
                clip.wrapMode = WrapMode.Clamp;
                settings.loopBlend = false;
                settings.loopTime = false;
            }
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            ctx.output.generatedClips.Add(tag, clip);
        }

        // Generate main image
        GenerateClipImageLayer(ctx, childPath, ctx.output.mainAtlas.sprites);
    }

    static void GenerateAnimController(AnimControllerImportInfo info) {
        var controller = info.controller;
        var layer = controller.layers[0];

        var stateMap = new Dictionary<string, AnimatorState>();
        PopulateStateTable(stateMap, layer.stateMachine);

        var metadata = AssetDatabase.LoadAssetAtPath<ASEAssetMetadata>(info.assetPath);
        foreach (var clipInfo in metadata.clips) {
            var clipName = clipInfo.clipName;
            var clip = clipInfo.clip;

            AnimatorState st;
            stateMap.TryGetValue(clipName, out st);
            if (!st) {
                st = layer.stateMachine.AddState(clipName);
            }

            st.motion = clip;
        }

        EditorUtility.SetDirty(controller);
    }

    static void PopulateStateTable(Dictionary<string, AnimatorState> table, AnimatorStateMachine machine) {
        foreach (var state in machine.states) {
            var name = state.state.name;
            if (table.ContainsKey(name)) {
                Debug.LogWarning("Duplicate state with name " + name + " in animator controller. Behaviour is undefined.");
            } else {
                table.Add(name, state.state);
            }
        }

        foreach (var subMachine in machine.stateMachines) {
            PopulateStateTable(table, subMachine.stateMachine);
        }
    }

    private static void _DelayProcessAnimController() {
        while (_animControllerProcessQueue.Count > 0) {
            var item = _animControllerProcessQueue.Dequeue();
            GenerateAnimController(item);
        }

        AssetDatabase.SaveAssets();
        EditorApplication.update -= _delegateProcessAnimController;
    }

    internal class AnimControllerImportInfo {
        public string assetPath;
        public AnimatorController controller;
    }

    private static readonly EditorApplication.CallbackFunction _delegateProcessAnimController = _DelayProcessAnimController;
    private static readonly Queue<AnimControllerImportInfo> _animControllerProcessQueue = new Queue<AnimControllerImportInfo>();

}

}