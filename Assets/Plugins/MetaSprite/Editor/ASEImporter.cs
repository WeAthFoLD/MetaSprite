using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

using MetaSprite.Internal;

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
    public Dictionary<FrameTag, AnimationClip> generatedClips = new Dictionary<FrameTag, AnimationClip>();

}

public static class ASEImporter {

    public static void Refresh() {

    }

    public static void Import(DefaultAsset defaultAsset, ImportSettings settings) {
        var path = AssetDatabase.GetAssetPath(defaultAsset);

        var file = ASEParser.Parse(File.ReadAllBytes(path));

        var context = new ImportContext {
            file = file,
            settings = settings,
            fileDirectory = Path.GetDirectoryName(path),
            fileName = Path.GetFileName(path),
            fileNameNoExt = Path.GetFileNameWithoutExtension(path)
        };

        context.atlasPath = Path.Combine(settings.atlasOutputDirectory, context.fileNameNoExt + ".png");

        if (settings.controllerPolicy == AnimControllerOutputPolicy.CreateOrOverride)
            context.animControllerPath = settings.animControllerOutputPath + "/" + settings.baseName + ".controller";
        context.animClipDirectory = settings.clipOutputDirectory;

        // Create paths in advance
        Directory.CreateDirectory(settings.atlasOutputDirectory);
        Directory.CreateDirectory(context.animClipDirectory);
        if (context.animControllerPath != null)
            Directory.CreateDirectory(Path.GetDirectoryName(context.animControllerPath));
        //

        AtlasGenerator.GenerateAtlas(context);

        GenerateAnimClips(context);

        GenerateAnimController(context);
    }

    static void GenerateAnimClips(ImportContext ctx) {
        Directory.CreateDirectory(ctx.animClipDirectory);       
        var fileNamePrefix = ctx.animClipDirectory + '/' + ctx.settings.baseName; 

        string childPath = ctx.settings.spriteTarget;

        // Generate one animation for each tag
        foreach (var tag in ctx.file.frameTags) {
            var clipPath = fileNamePrefix + '_' + tag.name + ".anim";
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);

            if (!clip) {
                clip = new AnimationClip();
                AssetDatabase.CreateAsset(clip, clipPath);
            } else {
                AnimationUtility.SetAnimationEvents(clip, new AnimationEvent[0]);
            }

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

            int time = 0;
            var keyFrames = new ObjectReferenceKeyframe[tag.to - tag.from + 2];
            for (int i = tag.from; i <= tag.to; ++i) {
                var aseFrame = ctx.file.frames[i];
                keyFrames[i - tag.from] = new ObjectReferenceKeyframe {
                    time = time * 1e-3f,
                    value = ctx.generatedSprites[aseFrame.frameID]
                };

                time += aseFrame.duration;
            }

            keyFrames[keyFrames.Length - 1] = new ObjectReferenceKeyframe {
                time = time * 1e-3f - 1.0f / clip.frameRate,
                value = ctx.generatedSprites[tag.to]
            };

            var binding = new EditorCurveBinding {
                path = childPath,
                type = typeof(SpriteRenderer),
                propertyName = "m_Sprite"
            };

            AnimationUtility.SetObjectReferenceCurve(clip, binding, keyFrames);
            EditorUtility.SetDirty(clip);
            ctx.generatedClips.Add(tag, clip);
        }

        
    }

    static void GenerateAnimController(ImportContext ctx) {
        if (ctx.animControllerPath == null) {
            Debug.LogWarning("No animator controller specified. Controller generation will be ignored");
            return;
        }

        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ctx.animControllerPath);
        if (!controller) {
            controller = AnimatorController.CreateAnimatorControllerAtPath(ctx.animControllerPath);
        }

        var layer = controller.layers[0];
        var stateMap = new Dictionary<string, AnimatorState>();
        foreach (var state in layer.stateMachine.states) {
            stateMap.Add(state.state.name, state.state);
        }
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

}

}