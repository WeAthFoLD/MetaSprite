#if UNITY_2020_2_OR_NEWER
#define SCRIPTABLE_IMPORTERS
using UnityEditor.AssetImporters;
#elif UNITY_2019_4_OR_NEWER
#define SCRIPTABLE_IMPORTERS
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

    public ASEFile file;
    public ImportSettings settings;

    public string fileDirectory;
    public string fileName;
    public string fileNameNoExt;
    
    public string atlasPath;
    public string animControllerPath;
    public string animClipDirectory;

    public List<Sprite> generatedSprites = new List<Sprite>();

    // The local texture coordinate for bottom-left point of each frame's crop rect, in Unity texture space.
    public List<Vector2> spriteCropPositions = new List<Vector2>();

    public Dictionary<FrameTag, AnimationClip> generatedClips = new Dictionary<FrameTag, AnimationClip>();

    public Dictionary<string, List<Layer>> subImageLayers = new Dictionary<string, List<Layer>>();

}

#if SCRIPTABLE_IMPORTERS
[ScriptedImporter(1, new[] { "ase", "aseprite" })]
public class ASEImporter : ScriptedImporter {

    public ImportSettings settings;

    public override void OnImportAsset(AssetImportContext ctx) {
    }
}
#endif

public static class ASEImportProcess {

    static readonly Dictionary<string, MetaLayerProcessor> layerProcessors = new Dictionary<string, MetaLayerProcessor>();

    enum Stage {
        LoadFile,
        GenerateAtlas,
        GenerateClips,
        GenerateController,
        InvokeMetaLayerProcessor
    }

    static float GetProgress(this Stage stage) {
        return (float) (int) stage / Enum.GetValues(typeof(Stage)).Length;
    }

    static string GetDisplayString(this Stage stage) {
        return stage.ToString();
    }

    public static void Startup() {
        layerProcessors.Clear();
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


    public static void Import(string path, ImportSettings settings) {
        if (!settings.CheckIsValid()) {
            var settingsPath = AssetDatabase.GetAssetPath(settings);
            Debug.LogError($"Import settings {settingsPath} is invalid, please fix it before importing.");
            return;
        }
        
        var context = new ImportContext {
            // file = file,
            settings = settings,
            fileDirectory = Path.GetDirectoryName(path),
            fileName = Path.GetFileName(path),
            fileNameNoExt = Path.GetFileNameWithoutExtension(path)
        };

        try {
            ImportStage(context, Stage.LoadFile);
            context.file = ASEParser.Parse(File.ReadAllBytes(path));        

            context.atlasPath = Path.Combine(settings.atlasOutputDirectory, context.fileNameNoExt + ".png");

            if (settings.controllerPolicy == AnimControllerOutputPolicy.CreateOrOverride)
                context.animControllerPath = settings.animControllerOutputDirectory + "/" + settings.baseName + ".controller";
            context.animClipDirectory = settings.clipOutputDirectory;

            // Create paths in advance
            Directory.CreateDirectory(settings.atlasOutputDirectory);
            Directory.CreateDirectory(context.animClipDirectory);
            if (context.animControllerPath != null)
                Directory.CreateDirectory(Path.GetDirectoryName(context.animControllerPath));
            //

            ImportStage(context, Stage.GenerateAtlas);
            context.generatedSprites = AtlasGenerator.GenerateAtlas(context, 
                context.file.layers.Where(it => it.type == LayerType.Content).ToList(),
                context.atlasPath);

            ImportStage(context, Stage.GenerateClips);
            GenerateAnimClips(context);

            ImportStage(context, Stage.GenerateController);
            GenerateAnimController(context);

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
    }

    static void ImportStage(ImportContext ctx, Stage stage) {
        EditorUtility.DisplayProgressBar("Importing " + ctx.fileName, stage.GetDisplayString(), stage.GetProgress());
    }

    static void ImportEnd(ImportContext ctx) {
        EditorUtility.ClearProgressBar();
    }

    public static void GenerateClipImageLayer(ImportContext ctx, string childPath, List<Sprite> frameSprites) {
        foreach (var tag in ctx.file.frameTags) {
            AnimationClip clip = ctx.generatedClips[tag];

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
        Directory.CreateDirectory(ctx.animClipDirectory);       
        var fileNamePrefix = ctx.animClipDirectory + '/' + ctx.settings.baseName; 

        string childPath = ctx.settings.spriteTarget;

        // Generate one animation for each tag
        foreach (var tag in ctx.file.frameTags) {
            var clipPath = fileNamePrefix + '_' + tag.name + ".anim";
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);

            // Create clip
            if (!clip) {
                clip = new AnimationClip();
                AssetDatabase.CreateAsset(clip, clipPath);
            } else {
                AnimationUtility.SetAnimationEvents(clip, new AnimationEvent[0]);
            }

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

            EditorUtility.SetDirty(clip);
            ctx.generatedClips.Add(tag, clip);
        }

        // Generate main image
        GenerateClipImageLayer(ctx, childPath, ctx.generatedSprites);
    }

    static void GenerateAnimController(ImportContext ctx) {
        if (string.IsNullOrEmpty(ctx.animControllerPath)) {
            // Debug.LogWarning("No animator controller specified. Controller generation will be ignored");
            return;
        }

        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ctx.animControllerPath);
        if (!controller) {
            controller = AnimatorController.CreateAnimatorControllerAtPath(ctx.animControllerPath);
        }

        var layer = controller.layers[0];
        var stateMap = new Dictionary<string, AnimatorState>();
        PopulateStateTable(stateMap, layer.stateMachine);
        
        foreach (var pair in ctx.generatedClips) {
            var frameTag = pair.Key;
            var clip = pair.Value;

            AnimatorState st;
            stateMap.TryGetValue(frameTag.name, out st);
            if (!st) {
                st = layer.stateMachine.AddState(frameTag.name);
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

}

}