using UnityEngine;

namespace Museum.Games.Dakon
{
    /// <summary>
    /// Bakes the screen-edge vignette the drop feedback tints: a square sprite that is clear in
    /// the middle and opaque at the border, with a soft ramp between. The pixels are white, so the
    /// single baked sprite serves both the green "correct" and the red "wrong" pulse — the colour
    /// is the <see cref="UnityEngine.UI.Image"/>'s, never the texture's.
    ///
    /// Baked rather than authored for the same reason as
    /// Egrang's <c>SkillCheckTrackTexture</c>: there is no import to get wrong,
    /// and a rebuilt project cannot lose it. The caller owns the result and must
    /// <see cref="Release"/> it.
    /// </summary>
    public static class VignetteTexture
    {
        /// <summary>Smallest and largest baked square. The ramp is broad and low-contrast, so even the floor stretches to a full screen without banding.</summary>
        public const int MinResolution = 32;
        public const int MaxResolution = 512;

        /// <summary>
        /// Distance from the centre (1 = half the sprite's width) where the tint starts.
        ///
        /// Well out toward the border on purpose. The ramp used to begin at 0.45, which put
        /// colour across most of the screen — over the board, over the hand, over the seed the
        /// player is watching. A vignette is meant to be read out of the corner of the eye while
        /// looking at something else, so the band is kept in the outer third.
        /// </summary>
        public const float DefaultInnerRadius = 0.72f;

        /// <summary>Distance where the tint reaches full strength. Held inside the corner so the edge midpoints — not just the corners — are fully tinted.</summary>
        public const float DefaultOuterRadius = 0.98f;

        /// <summary>
        /// Bakes a vignette sprite. Alpha ramps from 0 at <paramref name="innerRadius"/> to 1 at
        /// <paramref name="outerRadius"/>, measured radially with the sprite's half-width as 1.
        /// </summary>
        public static Sprite Bake(int resolution,
                                  float innerRadius = DefaultInnerRadius,
                                  float outerRadius = DefaultOuterRadius,
                                  string name = "DakonVignette")
        {
            int size = Mathf.Clamp(resolution, MinResolution, MaxResolution);

            // A zero-wide ramp would divide by zero in SmoothStep's normalisation; nudge the outer
            // edge out rather than refusing to bake, so a bad inspector value still renders.
            if (outerRadius <= innerRadius) outerRadius = innerRadius + 0.001f;

            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                // Pixel centres, mapped to -1..1 so the middle of the sprite is the origin.
                float ny = (y + 0.5f) / size * 2f - 1f;
                for (int x = 0; x < size; x++)
                {
                    float nx = (x + 0.5f) / size * 2f - 1f;
                    float r = Mathf.Sqrt(nx * nx + ny * ny);
                    float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(innerRadius, outerRadius, r));
                    pixels[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false)
            {
                name = name,
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            texture.SetPixels(pixels);
            texture.Apply();

            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f),
                                          100f, 0, SpriteMeshType.FullRect);
            sprite.name = name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        /// <summary>Throws away a baked sprite and the texture behind it, and clears the caller's reference.</summary>
        public static void Release(ref Sprite sprite)
        {
            if (sprite == null)
            {
                sprite = null;
                return;
            }

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
