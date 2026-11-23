using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Sturm
{

// The performer's silhouette on the emitter plane. It turns the RVM matte
// into a rim-lit image, draws that on a plane one unit high centered on this
// transform, and builds the emission guide that particle effects sample.
//
// The rim lighting follows the hologram prototype in unity-rvm-coreml: a
// pyramid of blurred mattes stands in for an inflated surface, and a light
// behind the subject grazes its edge.
[DefaultExecutionOrder(-100)]
public sealed class Silhouette : MonoBehaviour
{
    // Surface shape

    // Slope scale of the pseudo-surface. Higher values tilt more of the
    // interior away from the viewer and widen the lit band along the rim.
    [field:SerializeField, Range(1, 64)]
    public float Depth { get; set; } = 10;

    // Coarsest pyramid level (0-3) blended into the surface. Finer levels keep
    // the rim tight; coarser ones round off the body.
    [field:SerializeField, Range(0, 3)]
    public float RimWidth { get; set; } = 2;

    // Lighting

    // Hue of faint light. Bright light saturates toward white.
    [field:SerializeField, ColorUsage(false)]
    public Color Color { get; set; } = new Color32(0x8a, 0xb2, 0xcc, 0xff);

    // Image-plane direction toward the light: 0 is right, 90 is up.
    [field:SerializeField, Range(-180, 180)]
    public float LightAngle { get; set; } = 120;

    // Negative values put the light behind the subject.
    [field:SerializeField, Range(-90, 90)]
    public float LightElevation { get; set; } = -64;

    [field:SerializeField, Range(0, 16)]
    public float LightIntensity { get; set; } = 16;

    // Halo

    [field:SerializeField, Range(0, 4)]
    public float GlowIntensity { get; set; } = 0.02f;

    // Coarsest pyramid level (1-4, offset by one) blended into the halo
    [field:SerializeField, Range(0, 3)]
    public float GlowWidth { get; set; } = 2;

    // Display

    // HDR multiplier on the plane. Values above one feed the bloom.
    [field:SerializeField, Range(0, 8)]
    public float Brightness { get; set; } = 1.5f;

    // Camera image revealed on the side of the body facing the light. Its
    // luminance is tinted with Color.
    [field:SerializeField, Range(0, 1)]
    public float BodyColor { get; set; } = 0;

    // Coarsest pyramid level (1-4, offset by one) shaping the lit side of the
    // body. Higher values carry the light deeper inside from the outline.
    [field:SerializeField, Range(0, 3)]
    public float BodyLightWidth { get; set; } = 2;

    // Width of the fade toward the frame border, relative to the frame. It
    // applies to emission too.
    [field:SerializeField, Range(0, 0.5f)]
    public float EdgeFade { get; set; } = 0.08f;

    // Public members

    // RVM output: linear RGB of the camera image plus the matte in alpha.
    public Texture Matte { get; set; }

    public bool IsReady => Matte != null && _emission != null;

    public Texture ColorMap => Matte;

    public Texture EmissionMap => _emission;

    public float Aspect =>
        Matte != null ? (float)Matte.width / Matte.height : 16.0f / 9;

    // MonoBehaviour implementation

    void OnEnable()
    {
        if (_filterShader != null)
            _filter = new Material(_filterShader) { hideFlags = HideFlags.DontSave };
        if (_displayShader != null)
            _display = new Material(_displayShader) { hideFlags = HideFlags.DontSave };
    }

    void OnDisable()
    {
        for (var i = 0; i < LevelCount; i++) ReleaseTexture(ref _levels[i]);
        ReleaseTexture(ref _composite);
        ReleaseTexture(ref _emission);
        DestroyObject(ref _filter);
        DestroyObject(ref _display);
    }

    // Runs ahead of the particle effects so that they emit from this
    // frame's matte.
    void LateUpdate()
    {
        if (_filter == null || _display == null) return;
        if (Matte == null || Matte.width <= 0 || Matte.height <= 0) return;

        UpdatePyramid(Matte);
        SetFilterProperties();
        EnsureTexture(ref _composite, Matte.width, Matte.height,
                      GraphicsFormat.R16G16B16A16_SFloat, "Silhouette Composite");
        Graphics.Blit(Matte, _composite, _filter, CompositePass);
        EnsureTexture(ref _emission, _levels[0].width, _levels[0].height,
                      GraphicsFormat.R16G16B16A16_SFloat, "Silhouette Emission");
        Graphics.Blit(Matte, _emission, _filter, EmissionPass);

        Draw();
    }

    // Private members

    const int LevelCount = 5;
    const int PrefilterPass = 0;
    const int DownsamplePass = 1;
    const int BlurHPass = 2;
    const int BlurVPass = 3;
    const int CompositePass = 4;
    const int EmissionPass = 5;

    static readonly int[] LevelIDs =
    {
        Shader.PropertyToID("_Level0"),
        Shader.PropertyToID("_Level1"),
        Shader.PropertyToID("_Level2"),
        Shader.PropertyToID("_Level3"),
        Shader.PropertyToID("_Level4")
    };

    [SerializeField, HideInInspector] Shader _filterShader = null;
    [SerializeField, HideInInspector] Shader _displayShader = null;

    readonly RenderTexture[] _levels = new RenderTexture[LevelCount];
    Material _filter;
    Material _display;
    RenderTexture _composite;
    RenderTexture _emission;

    // The pyramid is rebuilt every frame instead of tracking matte updates.
    // Its levels are small enough that the redundant work is negligible.
    void UpdatePyramid(Texture source)
    {
        var (w, h) = (source.width, source.height);
        for (var i = 0; i < LevelCount; i++)
        {
            (w, h) = (Mathf.Max(1, (w + 1) / 2), Mathf.Max(1, (h + 1) / 2));
            EnsureTexture(ref _levels[i], w, h,
                          GraphicsFormat.R16_SFloat, $"Silhouette Level {i}");
        }

        Graphics.Blit(source, _levels[0], _filter, PrefilterPass);
        BlurLevel(_levels[0]);
        for (var i = 1; i < LevelCount; i++)
        {
            Graphics.Blit(_levels[i - 1], _levels[i], _filter, DownsamplePass);
            BlurLevel(_levels[i]);
        }
    }

    void BlurLevel(RenderTexture level)
    {
        var temp = RenderTexture.GetTemporary(level.descriptor);
        temp.filterMode = FilterMode.Bilinear;
        temp.wrapMode = TextureWrapMode.Clamp;
        Graphics.Blit(level, temp, _filter, BlurHPass);
        Graphics.Blit(temp, level, _filter, BlurVPass);
        RenderTexture.ReleaseTemporary(temp);
    }

    void SetFilterProperties()
    {
        for (var i = 0; i < LevelCount; i++)
            _filter.SetTexture(LevelIDs[i], _levels[i]);

        var angle = LightAngle * Mathf.Deg2Rad;
        var elevation = LightElevation * Mathf.Deg2Rad;
        var light = new Vector3(
            Mathf.Cos(elevation) * Mathf.Cos(angle),
            Mathf.Cos(elevation) * Mathf.Sin(angle),
            Mathf.Sin(elevation)
        );

        _filter.SetVector("_ShapeWeights", CumulativeWeights(RimWidth));
        _filter.SetVector("_HaloWeights", CumulativeWeights(GlowWidth));
        _filter.SetVector("_BodyWeights", CumulativeWeights(BodyLightWidth));
        _filter.SetVector("_LightDirection", light);
        _filter.SetVector("_LightAxis", new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)));
        _filter.SetVector("_Color", Color.linear);
        _filter.SetFloat("_Depth", Depth);
        _filter.SetFloat("_LightIntensity", LightIntensity);
        _filter.SetFloat("_GlowIntensity", GlowIntensity);
        _filter.SetFloat("_BodyColor", BodyColor);
        _filter.SetFloat("_EdgeFade", EdgeFade);
    }

    void Draw()
    {
        _display.SetTexture("_MainTex", _composite);
        _display.SetMatrix("_LocalToWorld", transform.localToWorldMatrix);
        _display.SetVector("_Size", new Vector2(Aspect, 1));
        _display.SetFloat("_Brightness", Brightness);

        var rparams = new RenderParams(_display)
        {
            worldBounds = new Bounds(transform.position, Vector3.one * 100),
            shadowCastingMode = ShadowCastingMode.Off,
            receiveShadows = false,
            layer = gameObject.layer
        };
        Graphics.RenderPrimitives(rparams, MeshTopology.Triangles, 6);
    }

    // Normalized weights over four consecutive levels. Levels up to the
    // position count fully and the next one fractionally, so each finer level
    // keeps contributing as the profile widens. Summed Gaussians fall off with
    // a long tail, close to how a bright edge blooms.
    static Vector4 CumulativeWeights(float position)
    {
        var w = new Vector4(
            1,
            Mathf.Clamp01(position),
            Mathf.Clamp01(position - 1),
            Mathf.Clamp01(position - 2)
        );
        return w / (w.x + w.y + w.z + w.w);
    }

    static void EnsureTexture(ref RenderTexture texture, int width, int height,
                              GraphicsFormat format, string name)
    {
        if (texture != null && texture.width == width && texture.height == height)
            return;
        ReleaseTexture(ref texture);
        texture = new RenderTexture(width, height, 0, format)
        {
            name = name,
            hideFlags = HideFlags.DontSave,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        texture.Create();
    }

    static void ReleaseTexture(ref RenderTexture texture)
    {
        if (texture == null) return;
        texture.Release();
        DestroyObject(ref texture);
    }

    static void DestroyObject<T>(ref T obj) where T : Object
    {
        if (obj == null) return;
        if (Application.isPlaying) Destroy(obj); else DestroyImmediate(obj);
        obj = null;
    }
}

} // namespace Sturm
