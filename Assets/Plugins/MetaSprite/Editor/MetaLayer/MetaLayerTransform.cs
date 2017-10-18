using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using TangentMode = UnityEditor.AnimationUtility.TangentMode;

namespace MetaSprite {

public class MetaLayerTransform : MetaLayerProcessor {
    
    public override string actionName {
        get { return "transform"; }
    }

    public override void Process(ImportContext ctx, Layer layer) {
        var childName = layer.GetParamString(0);

        EditorCurveBinding
            bindingX = new EditorCurveBinding { path = childName, type = typeof(Transform), propertyName = "m_LocalPosition.x" },
            bindingY = new EditorCurveBinding { path = childName, type = typeof(Transform), propertyName = "m_LocalPosition.y" };

        var frames = new Dictionary<int, Vector2>();
        var file = ctx.file;

        for (int i = 0; i < file.frames.Count; ++i) {
            Vector2 center = Vector2.zero;
            int pixelCount = 0;

            Cel cel;
            file.frames[i].cels.TryGetValue(layer.index, out cel);

            if (cel == null)
                continue;

            for (int y = 0; y < cel.height; ++y) {
                for (int x = 0; x < cel.width; ++x) {
                    int texX = cel.x + x;
                    int texY = -(cel.y + y) + file.height - 1;
                    var col = cel.GetPixelRaw(x, y);
                    if (col.a > 0.1f) {
                        center += new Vector2(texX, texY);
                        pixelCount++;
                    }
                }
            }

            if (pixelCount > 0) {
                center /= pixelCount;
                var pivot = Vector2.Scale(ctx.settings.PivotRelativePos, new Vector2(file.width, file.height));
                var posWorld = (center - pivot) / ctx.settings.ppu;

                frames.Add(i, posWorld);
            }
        }

        foreach (var frameTag in file.frameTags) {
            var clip = ctx.generatedClips[frameTag];

            AnimationCurve curveX = new AnimationCurve(), 
                           curveY = new AnimationCurve();

            float t = 0;
            for (int f = frameTag.from; f <= frameTag.to; ++f) {
                if (frames.ContainsKey(f)) {
                    var pos = frames[f];
                    curveX.AddKey(t, pos.x);
                    curveY.AddKey(t, pos.y);
                }

                t += file.frames[f].duration * 1e-3f;
            }

            if (curveX.length > 0) {
                MakeConstant(curveX);
                MakeConstant(curveY);

                AnimationUtility.SetEditorCurve(clip, bindingX, curveX);
                AnimationUtility.SetEditorCurve(clip, bindingY, curveY);

                EditorUtility.SetDirty(clip);
            }
        }
    }


    static void MakeConstant(AnimationCurve curve) {
        for (int i = 0; i < curve.length; ++i) {
            AnimationUtility.SetKeyLeftTangentMode(curve, i, TangentMode.Constant);
        }
    }

}

}