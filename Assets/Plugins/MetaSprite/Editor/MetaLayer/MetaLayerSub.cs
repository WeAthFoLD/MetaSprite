using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MetaSprite {

public class MetaLayerSub : MetaLayerProcessor {

    // syntax: @sub(string subImageName)

    public override string actionName {
        get { return "sub"; }
    }

    public override void Process(ImportContext context, Layer layer) {
        string subImageName = layer.GetParamString(0);
        
        List<Layer> layers;
        context.subImageLayers.TryGetValue(subImageName, out layers);
        if (layers == null) {
            context.subImageLayers.Add(subImageName, layers = new List<Layer>());
        }

        layers.Add(layer);
    }

}

}