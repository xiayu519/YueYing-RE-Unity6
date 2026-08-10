using System;
using System.Collections.Generic;
using System.Linq;
using Jxqy.Domain.Content;
using Jxqy.Ports;
using UnityEngine;

namespace Jxqy.UnityAdapters
{
    public sealed class JxqySpriteFontDrawCommandBuilder
    {
        private readonly JxqySpriteFontMetadata _font;
        private readonly Dictionary<int, JxqySpriteFontGlyphMetadata>
            _glyphs;

        public JxqySpriteFontDrawCommandBuilder(
            JxqySpriteFontMetadata font)
        {
            _font = font ??
                    throw new ArgumentNullException(nameof(font));
            _glyphs = font.Glyphs.ToDictionary(
                glyph => glyph.Character);
        }

        public List<JxqyDrawCommand> Build(
            string text,
            Vector2 position,
            Color color,
            int depth,
            string materialKey = "default")
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));
            var result = new List<JxqyDrawCommand>();
            float x = position.x;
            float y = position.y;
            foreach (char character in text)
            {
                if (character == '\n')
                {
                    x = position.x;
                    y += _font.LineSpacing;
                    continue;
                }
                if (!_glyphs.TryGetValue(
                        character,
                        out JxqySpriteFontGlyphMetadata glyph))
                {
                    if (_font.DefaultCharacter < 0 ||
                        !_glyphs.TryGetValue(
                            _font.DefaultCharacter,
                            out glyph))
                        continue;
                }
                x += glyph.KerningLeft;
                result.Add(new JxqyDrawCommand(
                    _font.TextureAddress,
                    new Rect(
                        glyph.Glyph.X,
                        _font.TextureHeight -
                        glyph.Glyph.Y -
                        glyph.Glyph.Height,
                        glyph.Glyph.Width,
                        glyph.Glyph.Height),
                    new Vector2(
                        x + glyph.Cropping.X,
                        y + glyph.Cropping.Y),
                    Vector2.zero,
                    color,
                    depth,
                    materialKey));
                x += glyph.KerningWidth +
                     glyph.KerningRight +
                     _font.Spacing;
            }
            return result;
        }
    }
}
