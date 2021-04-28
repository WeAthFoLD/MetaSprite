using System;
using UnityEngine;
using UnityEditor;
using System.IO;
using UnityEngine.Serialization;

namespace MetaSprite {

[Serializable]
public class ASEImportSettings {

    [Tooltip("Controls the child object animations target. Leave empty for root object.")]
    public string targetChildObject;
    
    public int pixelPerUnit = 48;

    public SpriteAlignment alignment;

    [Tooltip("Only applied if alignment is Custom.")]
    public Vector2 customPivot;

    public bool densePack = true;

    public int border = 3;

    public Vector2 PivotRelativePos {
        get {
            return alignment.GetRelativePos(customPivot);
        }
    }

}

}