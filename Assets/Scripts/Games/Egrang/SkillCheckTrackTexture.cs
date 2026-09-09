using UnityEngine;

namespace Museum.Games.Egrang
{
    /// <summary>The three band colours of a skill-check track, in one bundle so callers pass a palette rather than three loose fields.</summary>
    [System.Serializable]
    public struct SkillCheckTrackColors
    {
        [Tooltip("Colour of red bands.")] public Color Fail;
        [Tooltip("Colour of yellow bands.")] public Color Half;
        [Tooltip("Colour of green bands.")] public Color Full;

        public SkillCheckTrackColors(Color fail, Color half, Color full)
        {
            Fail = fail;
            Half = half;
            Full = full;
        }

        /// <summary>The shipped palette. Used as the field default so a fresh component looks right without authoring.</summary>
        public static SkillCheckTrackColors Default => new SkillCheckTrackColors(
            new Color(0.85f, 0.20f, 0.20f),
            new Color(0.95f, 0.80f, 0.20f),
            new Color(0.25f, 0.80f, 0.35f));

        /// <summary>The colour a band with this result is drawn in. Anything unrecognised reads as a fail, matching <see cref="SkillCheckZones.Evaluate"/>'s own fallback.</summary>
        public Color For(EgrangStepResult result)
        {
            switch (result)
            {
                case EgrangStepResult.Full: return Full;
                case EgrangStepResult.Half: return Half;
                default: return Fail;
            }
        }
    }

    /// <summary>
    /// Bakes a zone table into a sprite: a one-pixel-tall strip where every pixel carries the
    /// colour of the result a press at that position would score. Stretched across an
    /// <see cref="UnityEngine.UI.Image"/>, one strip fits any width — which matters on WebGL, where
    /// the canvas scales to the museum kiosk's screen or an arbitrary browser window.
    ///
    /// This lives apart from <see cref="SkillCheckBar"/> because the stick-selection cards preview
    /// the same tables. Sharing the bake is what stops a card from showing bands the bar would not
    /// actually score.
    /// </summary>
    public static class SkillCheckTrackTexture
    {
        /// <summary>Narrowest and widest baked strip. The floor keeps band edges from landing visibly off; the ceiling is plenty, since the strip is one pixel tall and costs nothing at runtime.</summary>
        public const int MinResolution = 64;
        public const int MaxResolution = 2048;

        /// <summary>
        /// Bakes <paramref name="zones"/> into a fresh sprite. The caller owns the result and must
        /// pass it to <see cref="Release"/> — these are made by hand rather than loaded as assets, so
        /// nothing else will collect them.
        ///
        /// Bands meet at hard edges, with no blending: the player must see exactly where green stops,
        /// because that is exactly where the scoring changes. Point filtering is what enforces that —
        /// bilinear would smear each seam into a soft ramp as the strip is stretched, promising a
        /// precision the bar does not grade on.
        /// </summary>
        public static Sprite Bake(SkillCheckZones zones, SkillCheckTrackColors colors, int resolution, string name = "SkillCheckTrack")
        {
            if (zones == null) return null;

            int width = Mathf.Clamp(resolution, MinResolution, MaxResolution);
            var pixels = new Color[width];
            for (int x = 0; x < width; x++)
            {
                // Sample pixel centres so the first and last pixel are not half a step off the ends.
                pixels[x] = colors.For(zones.Evaluate((x + 0.5f) / width));
            }

            var texture = new Texture2D(width, 1, TextureFormat.RGBA32, mipChain: false)
            {
                name = name,
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Point,
            };
            texture.SetPixels(pixels);
            texture.Apply();

            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, width, 1f), new Vector2(0.5f, 0.5f),
                                          100f, 0, SpriteMeshType.FullRect);
            sprite.name = name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        /// <summary>
        /// Throws away a baked sprite and the texture behind it. Callers rebake on every inspector
        /// change, which would otherwise leak one of each per keystroke.
        /// </summary>
        public static void Release(ref Sprite sprite)
        {
            if (sprite == null) return;

            Texture2D texture = sprite.texture;
            Destroy(sprite);
            if (texture != null) Destroy(texture);
            sprite = null;
        }

        static void Destroy(Object target)
        {
            if (Application.isPlaying) Object.Destroy(target);
            else Object.DestroyImmediate(target);
        }
    }
}
