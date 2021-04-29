using System;
using UnityEngine;
using UnityEditor;
using System.IO;
using UnityEditor.Animations;
using UnityEngine.Serialization;

namespace MetaSprite {

[Serializable]
public class ASEImportSettings {

    [Tooltip("Controls the child object animations target. Leave empty for root object.")]
    public string targetChildObject;
    
    public int pixelPerUnit = 48;

    public SpriteAlignment alignment;

    public Vector2 customPivot;

    public bool densePack = true;

    public int border = 3;

    public AnimatorController outputController;

    public Vector2 PivotRelativePos {
        get {
            return alignment.GetRelativePos(customPivot);
        }
    }

}

}