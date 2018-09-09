using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using System;
using System.Text;
using System.IO.Compression;

using MetaSprite.Internal;

namespace MetaSprite {

public enum BlendMode {
    Normal         = 0,
    Multiply       = 1,
    Screen         = 2,
    Overlay        = 3,
    Darken         = 4,
    Lighten        = 5,
    ColorDodge     = 6,
    ColorBurn      = 7,
    HardLight      = 8,
    SoftLight      = 9,
    Difference     = 10,
    Exclusion      = 11,
    Hue            = 12,
    Saturation     = 13,
    Color          = 14,
    Luminosity     = 15,
    Addition       = 16,
    Subtract       = 17,
    Divide         = 18,
}

public class ASEFile {
    public int width, height;
    public List<Frame> frames = new List<Frame>();
    public List<Layer> layers = new List<Layer>();
    public List<FrameTag> frameTags = new List<FrameTag>();

    public Layer FindLayer(int index) {
        for (int i = 0; i < layers.Count; ++i) {
            var layer = layers[i];
            if (layer.index == index)
                return layer;
        }
        return null;
    }
}

public class Frame {
    public int duration;
    public int frameID;
    public Dictionary<int, Cel> cels = new Dictionary<int, Cel>();
}

public class Layer: UserDataAcceptor {
    public int index;
    public int parentIndex; // =1 if level==0 (have no parent), otherwise the index of direct parent
    public bool visible;
    public BlendMode blendMode;
    public float opacity;
    public string layerName;
    public string userData { get; set; }
    public LayerType type;

    // --- META

    /// If is metadata, the action name of the layer
    public string actionName { get; internal set; }

    internal readonly List<LayerParam> parameters = new List<LayerParam>();

    public int ParamCount {
        get {
            return parameters.Count;
        }
    }

    public int GetParamInt(int index) {
        return (int) CheckParamType(index, LayerParamType.Number).numberValue;
    }

    public float GetParamFloat(int index) {
        return (float) CheckParamType(index, LayerParamType.Number).numberValue;
    }

    public string GetParamString(int index) {
        return CheckParamType(index, LayerParamType.String).stringValue;
    }

    public bool GetParamBool(int index) {
        return CheckParamType(index, LayerParamType.Bool).boolValue;
    }

    public LayerParamType GetParamType(int index) {
        if (parameters.Count <= index) {
            return LayerParamType.None;
        }
        return parameters[index].type;
    }

    LayerParam CheckParamType(int index, LayerParamType type) {
        if (parameters.Count <= index) {
            throw new Exception("No parameter #" + index);
        }
        var par = parameters[index];
        if (par.type != type) {
            throw new Exception(string.Format("Type mismatch at parameter #{0}, expected {1}, got {2}",
                index, type, par.type));
        }
        return par;
    }

}

internal enum CelType {
    Raw, Linked, Compressed
}

public enum LayerType {
    Content, Meta
}

public enum LayerParamType {
    None, String, Number, Bool
}

public class LayerParam {
    public LayerParamType type;
    public double numberValue;
    public string stringValue;
    public bool boolValue;
}

public class Cel: UserDataAcceptor {
    static readonly Color Opaque = new Color(0, 0, 0, 0);

    public int layerIndex;

    public float opacity;
    public int x, y, width, height;

    public string userData { get; set; }

    internal CelType type;
    internal int linkedCel; // -1 if is raw cel, otherwise linked cel

    internal Color[] colorBuffer;

    // Get the color of the cel in cel space
    public Color GetPixelRaw(int x, int y) {
        return colorBuffer[y * width + x];
    }

    // Get the color of the cel in sprite image space
    public Color GetPixel(int x, int y) {
        var relx = x - this.x;
        var rely = y - this.y;
        if (0 <= relx && relx < width &&
            0 <= rely && rely < height) {
            return GetPixelRaw(relx, rely);
        } else {
            return Opaque;
        }
    }
}

internal interface UserDataAcceptor {
    string userData { get; set; }
}

public class FrameTag {
    public int from, to;
    public string name;
    public readonly HashSet<string> properties = new HashSet<string>();
}

public static class ASEParser {

    const UInt16
        CHUNK_LAYER = 0x2004,
        CHUNK_CEL = 0x2005,
        CHUNK_CELEXTRA = 0x2006,
        CHUNK_FRAME_TAGS = 0x2018,
        CHUNK_PALETTE = 0x2019,
        CHUNK_USERDATA = 0x2020;

    public static ASEFile Parse(byte[] bytes) {
        var stream = new MemoryStream(bytes);
        using (var reader = new BinaryReader(stream)) {
            var file = new ASEFile();

            reader.ReadDWord(); // File size
            _CheckMagicNumber(reader.ReadWord(), 0xA5E0);

            var frameCount = reader.ReadWord();

            file.width = reader.ReadWord();
            file.height = reader.ReadWord();

            var colorDepth = reader.ReadWord(); 

            if (colorDepth != 32) {
                _Error("Non RGBA color mode isn't supported yet");
            }

            reader.ReadDWord(); // Flags
            reader.ReadWord(); // Deprecated speed
            _CheckMagicNumber(reader.ReadDWord(), 0);
            _CheckMagicNumber(reader.ReadDWord(), 0);

            reader.ReadBytes(4);
            reader.ReadWord();
            reader.ReadBytes(2);
            reader.ReadBytes(92);
            int readLayerIndex = 0;

            UserDataAcceptor lastUserdataAcceptor = null;

            var levelToIndex = new Dictionary<int, int>();
            var enabledLayerIdxs = new List<int>();

            for (int i = 0; i < frameCount; ++i) {
                var frame = new Frame();
                frame.frameID = i;

                reader.ReadDWord(); // frameBytes
                _CheckMagicNumber(reader.ReadWord(), 0xF1FA);

                var chunkCount = reader.ReadWord();
                
                frame.duration = reader.ReadWord();

                reader.ReadBytes(6);

                for (int j = 0; j < chunkCount; ++j) {
                    var chunkBytes = reader.ReadDWord(); // 4
                    var chunkType = reader.ReadWord(); // 2

                    switch (chunkType) {
                    case CHUNK_LAYER: {
                        var layer = new Layer();
                        var flags = reader.ReadWord();

                        layer.visible = (flags & 0x1) != 0;
                        
                        var layerType = reader.ReadWord();
                        var childLevel = reader.ReadWord(); // childLevel
                        if (childLevel == 0) {
                            layer.parentIndex = -1;
                        } else {
                            layer.parentIndex = levelToIndex[childLevel - 1];
                        }

                        reader.ReadWord();
                        reader.ReadWord();

                        layer.blendMode = (BlendMode) reader.ReadWord();
                        layer.opacity = reader.ReadByte() / 255.0f;
                        reader.ReadBytes(3);

                        layer.layerName = reader.ReadUTF8();

                        var parentEnable = layer.parentIndex == -1 || enabledLayerIdxs.Contains(layer.parentIndex);
                        var thisEnable = layer.visible && !layer.layerName.StartsWith("//");
                        if (parentEnable && thisEnable) {
                            if (layerType == 0) {
                                layer.index = readLayerIndex;
                                layer.type = layer.layerName.StartsWith("@") ? LayerType.Meta : LayerType.Content;
                                if (layer.type == LayerType.Meta) {
                                    MetaLayerParser.Parse(layer);
                                }

                                file.layers.Add(layer);
                            }

                            enabledLayerIdxs.Add(readLayerIndex);
                        }

                        if (levelToIndex.ContainsKey(childLevel))
                            levelToIndex[childLevel] = readLayerIndex;
                        else
                            levelToIndex.Add(childLevel, readLayerIndex);

                        ++readLayerIndex;
                        
                        lastUserdataAcceptor = layer;

                    } break;

                    case CHUNK_CEL: {
                        var cel = new Cel();

                        cel.layerIndex = reader.ReadWord(); // 2
                        cel.x = reader.ReadInt16(); // 2
                        cel.y = reader.ReadInt16(); // 2
                        cel.opacity = reader.ReadByte() / 255.0f; // 1
                        cel.type = (CelType) reader.ReadWord(); // 2
                        reader.ReadBytes(7); // 7

                        switch (cel.type) {
                            case CelType.Raw: {
                                cel.width = reader.ReadWord(); // 2
                                cel.height = reader.ReadWord(); // 2
                                cel.colorBuffer = ToColorBufferRGBA(reader.ReadBytes(chunkBytes - 6 - 16 - 4)); 

                                _Assert(cel.width * cel.height == cel.colorBuffer.Length, "Color buffer size incorrect");
                            } break;
                            case CelType.Linked: {
                                cel.linkedCel = reader.ReadWord();
                            } break;
                            case CelType.Compressed: {
                                cel.width = reader.ReadWord();
                                cel.height = reader.ReadWord();
                                cel.colorBuffer = ToColorBufferRGBA(
                                    reader.ReadCompressedBytes(chunkBytes - 6 - 16 - 4));
                                _Assert(cel.width * cel.height == cel.colorBuffer.Length, "Color buffer size incorrect");                                
                            } break;
                        }

                        if (file.FindLayer(cel.layerIndex) != null)
                            frame.cels.Add(cel.layerIndex, cel);

                        lastUserdataAcceptor = cel;

                    } break;

                    case CHUNK_FRAME_TAGS: {
                        var count = reader.ReadWord();
                        reader.ReadBytes(8);

                        for (int c = 0; c < count; ++c) {
                            var frameTag = new FrameTag();

                            frameTag.from = reader.ReadWord();
                            frameTag.to = reader.ReadWord();
                            reader.ReadByte();
                            reader.ReadBytes(8);
                            reader.ReadBytes(3);
                            reader.ReadByte();

                            frameTag.name = reader.ReadUTF8();

                            if (frameTag.name.StartsWith("//")) { // Commented tags are ignored
                                continue;
                            }

                            var originalName = frameTag.name;

                            var tagIdx = frameTag.name.IndexOf('#');
                            var nameInvalid = false;
                            if (tagIdx != -1) {
                                frameTag.name = frameTag.name.Substring(0, tagIdx).Trim();
                                var possibleProperties = originalName.Substring(tagIdx).Split(' ');
                                foreach (var possibleProperty in possibleProperties) {
                                    if (possibleProperty.Length > 1 && possibleProperty[0] == '#') {
                                        frameTag.properties.Add(possibleProperty.Substring(1));
                                    } else {
                                        nameInvalid = true;
                                    }
                                }
                            }

                            if (nameInvalid) {
                                Debug.LogWarning("Invalid name: " + originalName);
                            }

                            file.frameTags.Add(frameTag);
                        }

                    } break;

                    case CHUNK_USERDATA: {
                        var flags = reader.ReadDWord();
                        var hasText = (flags & 0x01) != 0;
                        var hasColor = (flags & 0x02) != 0;

                        if (hasText) {
                            lastUserdataAcceptor.userData = reader.ReadUTF8();
                        }

                        if (hasColor) {
                            reader.ReadBytes(4);
                        }

                    } break;

                    default: {
                        reader.ReadBytes(chunkBytes - 6);
                    } break;

                    }
                }

                file.frames.Add(frame);
            }

            // Post process: calculate pixel alpha
            for (int f = 0; f < file.frames.Count; ++f) {
                var frame = file.frames[f];
                foreach (var cel in frame.cels.Values) {
                    if (cel.type != CelType.Linked) {
                        for(int i = 0; i < cel.colorBuffer.Length; ++i) {
                            cel.colorBuffer[i].a *= cel.opacity * file.FindLayer(cel.layerIndex).opacity;
                        }
                    }
                }
            }

            // Post process: eliminate reference cels
            for (int f = 0; f < file.frames.Count; ++f) {
                var frame = file.frames[f];
                foreach (var pair in frame.cels) {
                    var layerID = pair.Key;
                    var cel = pair.Value;
                    if (cel.type == CelType.Linked) {
                        cel.type = CelType.Raw;

                        var src = file.frames[cel.linkedCel].cels[layerID];

                        cel.x = src.x;
                        cel.y = src.y;
                        cel.width = src.width;
                        cel.height = src.height;
                        cel.colorBuffer = src.colorBuffer;
                        cel.opacity = src.opacity;
                        cel.userData = src.userData;
                    }
                }
            }

            return file;
        }
    }

    static int ReadDWord(this BinaryReader reader) {
        return (int) reader.ReadUInt32();
    }

    static UInt16 ReadWord(this BinaryReader reader) {
        return reader.ReadUInt16();
    }

    static string ReadUTF8(this BinaryReader reader) {
        var length = reader.ReadWord();
        var chars = reader.ReadBytes(length);
        return Encoding.UTF8.GetString(chars);
    }

    static Color[] ToColorBufferRGBA(byte[] bytes) {
        if (bytes.Length % 4 != 0) {
            _Error("Invalid color data");
        }

        var arr = new Color[bytes.Length / 4];
        for (int i = 0; i < arr.Length; ++i) {
            var offset = i << 2;

            Color color = Color.white;
            color.r = bytes[offset] / 255.0f;
            color.g = bytes[offset + 1] / 255.0f;
            color.b = bytes[offset + 2] / 255.0f;
            color.a = bytes[offset + 3] / 255.0f;

            arr[i] = color;
        }

        return arr;
    }

    static byte[] ReadCompressedBytes(this BinaryReader reader, int count) {
        reader.ReadByte();
        reader.ReadByte();
        using (var deflateStream = new DeflateStream(
                new MemoryStream(reader.ReadBytes(count - 2 - 4)), CompressionMode.Decompress)) {
            var bytes = ReadFully(deflateStream);
            reader.ReadDWord(); // Skip the ADLER32 checksum
            return bytes;
        }
    }

    static byte[] ReadFully(Stream input) {
        byte[] buffer = new byte[16*1024];
        using (MemoryStream ms = new MemoryStream()) {
            int read;
            while ((read = input.Read(buffer, 0, buffer.Length)) > 0) {
                ms.Write(buffer, 0, read);
            }
            return ms.ToArray();
        }
    }

    static void _CheckMagicNumber<T>(T number, T expected)
        where T: IEquatable<T> {
        if (!(number.Equals(expected))) {
            _Error("File validation failed");
        }
    }

    static Exception _Error(string msg) {
        throw new Exception(msg);
    }

    static void _Assert(bool expression, string msg) {
        if (!expression)
            _Error(msg);
    }

}

public static class MetaLayerParser {
    class TokenType {
        readonly string id;

        public TokenType(string _id) { id = _id; }

        public override string ToString() {
            return id;
        }
    }

    static readonly TokenType
        TKN_STRING = new TokenType("string"),
        TKN_NUMBER = new TokenType("number"),
        TKN_ID = new TokenType("id"),
        TKN_LEFT = new TokenType("left_bracket"),
        TKN_RIGHT = new TokenType("right_bracket"),
        TKN_COMMA = new TokenType("comma"),
        TKN_SPACE = new TokenType("space"),
        TKN_BOOL = new TokenType("bool");

    static readonly TokenDefinition[] defs;

    static MetaLayerParser() {
        defs = new TokenDefinition[] {
            new TokenDefinition(@"""[^""]*""", TKN_STRING),
            new TokenDefinition(@"([-+]?\d+\.\d+([eE][-+]?\d+)?)|([-+]?\d+)", TKN_NUMBER),
            new TokenDefinition(@"(true)|(false)", TKN_BOOL),
            new TokenDefinition(@"[a-zA-Z0-9_\-]+", TKN_ID),
            new TokenDefinition(@"\,", TKN_COMMA),
            new TokenDefinition(@"\(", TKN_LEFT),
            new TokenDefinition(@"\)", TKN_RIGHT),
            new TokenDefinition(@"\s*", TKN_SPACE),
        };
    }

    public static void Parse(Layer layer) {
        var reader = new StringReader(layer.layerName.Substring(1));
        var lexer = new Lexer(reader, defs);
        _Parse(lexer, layer);
    }

    static void _Parse(Lexer lexer, Layer layer) {
        layer.actionName = _Expect(lexer, TKN_ID);

        if (!_SkipSpaces(lexer)) {
            return;
        }

        if (lexer.Token != TKN_LEFT) {
            _ErrorUnexpected(lexer, TKN_LEFT);
        }

        while (true) {
            if (!_SkipSpaces(lexer)) {
                _ErrorEOF(lexer, TKN_RIGHT, TKN_NUMBER, TKN_STRING);
            }

            bool isParam = false;
            if (lexer.Token == TKN_STRING) {
                var param = new LayerParam();
                param.type = LayerParamType.String;
                param.stringValue = lexer.TokenContents.Substring(1, lexer.TokenContents.Length - 2);
                layer.parameters.Add(param);
                isParam = true;
            } else if (lexer.Token == TKN_NUMBER) {
                var param = new LayerParam();
                param.type = LayerParamType.Number;
                param.numberValue = double.Parse(lexer.TokenContents);
                layer.parameters.Add(param);
                isParam = true;
            } else if (lexer.Token == TKN_BOOL) {
                var param = new LayerParam();
                param.type = LayerParamType.Bool;
                param.boolValue = bool.Parse(lexer.TokenContents);
                layer.parameters.Add(param);
                isParam = true;
            } else if (lexer.Token == TKN_RIGHT) {
                break;
            } else {
                _ErrorUnexpected(lexer, TKN_RIGHT, TKN_NUMBER, TKN_STRING, TKN_BOOL);
            }

            if (isParam) {
                if (!_SkipSpaces(lexer)) {
                    _ErrorEOF(lexer, TKN_COMMA, TKN_RIGHT);
                }
                if (lexer.Token == TKN_RIGHT) {
                    break;
                }
                if (lexer.Token != TKN_COMMA) {
                    _ErrorUnexpected(lexer, TKN_COMMA, TKN_RIGHT);
                }
            }
        }
        
        if (_SkipSpaces(lexer)) {
            Debug.LogWarning("Invalid content after layer definition finished: " + lexer.Token + "/" + lexer.TokenContents);
        }

    }

    static bool _SkipSpaces(Lexer lexer) {
        while (true) {
            var hasMore = lexer.Next();
            if (!hasMore || lexer.Token != TKN_SPACE) {
                return hasMore;
            }
        }
    }

    static string _Expect(Lexer lexer, TokenType tokenType) {
        var hasMore = _SkipSpaces(lexer);

        if (!hasMore) {
            throw _ErrorEOF(lexer, tokenType);
        } else {
            if (lexer.Token != tokenType) {
                _ErrorUnexpected(lexer, tokenType);
            }
            return lexer.TokenContents;
        }
    }

    static Exception _ErrorEOF(Lexer lexer, params TokenType[] expected) {
        throw _Error(lexer, string.Format("Expected {0}, found EOF", _TokenTypeStr(expected)));
    }

    static Exception _ErrorUnexpected(Lexer lexer, params TokenType[] expected) {
        throw _Error(lexer, string.Format("Expected {0}, found {1}:{2}", _TokenTypeStr(expected), lexer.Token, lexer.TokenContents));
    }

    static string _TokenTypeStr(TokenType[] expected) {
        string typeStr = "";
        for (int i = 0; i < expected.Length; ++i) {
            typeStr += expected[i];
            if (i != expected.Length - 1) {
                typeStr += " or ";
            } else {
                typeStr += " ";
            }
        }
        return typeStr;
    }

    static Exception _Error(Lexer lexer, string msg) {
        throw new Exception(msg);
    }

}

}