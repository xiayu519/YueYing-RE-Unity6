using System;
using System.Collections.Generic;

namespace Jxqy.Domain.Content
{
    [Serializable]
    public sealed class JxqySpriteFontMetadata
    {
        public int SchemaVersion = 1;
        public string ConverterVersion = string.Empty;
        public string SourceStableId = string.Empty;
        public string SourceRelativePath = string.Empty;
        public string SourceAddress = string.Empty;
        public string SourceSha256 = string.Empty;
        public string TextureAddress = string.Empty;
        public int TextureWidth;
        public int TextureHeight;
        public int LineSpacing;
        public float Spacing;
        public int DefaultCharacter = -1;
        public List<JxqySpriteFontGlyphMetadata> Glyphs = new();
    }

    [Serializable]
    public sealed class JxqySpriteFontGlyphMetadata
    {
        public int Character;
        public JxqySpriteFontRectangle Glyph;
        public JxqySpriteFontRectangle Cropping;
        public float KerningLeft;
        public float KerningWidth;
        public float KerningRight;
    }

    [Serializable]
    public struct JxqySpriteFontRectangle
    {
        public int X;
        public int Y;
        public int Width;
        public int Height;
    }
}
