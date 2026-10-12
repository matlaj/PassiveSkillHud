using UnityEngine;

namespace PassiveSkillHud;

/// <summary>White, anti-aliased disc and ring sprites for the round icons, tinted by the images that use them.</summary>
internal static class RoundSprites
{
    private const int Size = 128;

    // Inner edge of the ring as a fraction of its radius. The ring is drawn over the clipped picture's edge, so it
    // has to reach inside the picture: on a 36-wide picture with a 1-unit margin, the ring spans 17.7 to 19.
    private const float RingInnerRadius = 0.93f;

    private static Sprite disc;
    private static Sprite ring;

    // Checked with Unity's null, so a sprite is made again if something ever destroys it.
    public static Sprite Disc => disc ? disc : disc = Create("PassiveSkillHudDisc", 0f);
    public static Sprite Ring => ring ? ring : ring = Create("PassiveSkillHudRing", RingInnerRadius);

    private static Sprite Create(string name, float innerRadius)
    {
        var outer = Size / 2f;
        var inner = outer * innerRadius;
        var pixels = new Color32[Size * Size];
        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                var distance = new Vector2(x + 0.5f - outer, y + 0.5f - outer).magnitude;
                var alpha = Mathf.Clamp01(outer - distance);
                if (innerRadius > 0f) alpha *= Mathf.Clamp01(distance - inner);
                pixels[y * Size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
            }
        }

        var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, true)
        {
            name = name,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontUnloadUnusedAsset,
        };
        texture.SetPixels32(pixels);
        texture.Apply(true, true);

        var sprite = Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f));
        sprite.name = name;
        sprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
        return sprite;
    }
}
