using UnityEngine;
using UnityEngine.Rendering;

namespace Sturm
{

// Runs one particle effect on the GPU. The simulation and render code is
// common to every effect; the attached ParticleProfile supplies all the
// parameters that make one effect look different from another. Particle
// state never leaves the GPU: the CPU only pushes uniforms and dispatches.
public sealed class ParticleEffect : MonoBehaviour
{
    // Public properties

    [field:SerializeField]
    public ParticleProfile Profile { get; set; }

    [field:SerializeField]
    public Silhouette Source { get; set; }

    // Clearing it stops new particles while existing ones live out their
    // lifetime, so switching between effects cross-fades instead of cutting.
    [field:SerializeField]
    public bool Emitting { get; set; } = true;

    // True once emission has stopped and every particle has expired. The
    // effect then skips both simulation and drawing.
    public bool IsIdle => Profile == null || _capacity == 0 ||
        (!Emitting && _idleTime > Profile.Lifetime);

    public int Capacity => _capacity;

    // Must match the Particle struct in ParticleCommon.hlsl.
    public const int ParticleStride = 12 * sizeof(float);

    // MonoBehaviour implementation

    void LateUpdate()
    {
        if (Profile == null || _compute == null || _shader == null) return;

        EnsureResources();

        // A long hitch would otherwise fling particles out of the flow.
        var dt = Mathf.Min(Time.deltaTime, 1.0f / 20);
        _idleTime = Emitting ? 0 : _idleTime + dt;
        if (IsIdle) return;

        Simulate(dt);
        Draw();
    }

    void OnDisable() => ReleaseResources();

    void OnDestroy() => ReleaseResources();

    // Private members

    const int GradientWidth = 128;
    const int EmitThreads = 64;
    const int UpdateThreads = 256;
    const int EmitTries = 16;

    // Period of the noise field in Noise.hlsl. Wrapping the flow time there
    // is seamless.
    const float NoisePeriod = 289;

    [SerializeField, HideInInspector] ComputeShader _compute = null;
    [SerializeField, HideInInspector] Shader _shader = null;

    GraphicsBuffer _particles;
    Material _material;
    Texture2D _gradient;
    readonly Color[] _gradientPixels = new Color[GradientWidth];
    int _capacity;
    uint _head;
    uint _frame;
    float _emitRemainder;
    float _idleTime;
    float _noiseTime;

    void EnsureResources()
    {
        if (_material == null)
            _material = new Material(_shader) { hideFlags = HideFlags.DontSave };

        if (_gradient == null)
            _gradient = new Texture2D(GradientWidth, 1, TextureFormat.RGBAHalf, false, true)
            {
                name = "Particle Color Over Life",
                hideFlags = HideFlags.DontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

        if (_particles != null && _capacity == Profile.Capacity) return;

        _particles?.Dispose();
        _capacity = Profile.Capacity;
        _particles = new GraphicsBuffer
          (GraphicsBuffer.Target.Structured, _capacity, ParticleStride);
        _head = 0;
        _emitRemainder = 0;

        var kernel = _compute.FindKernel("Clear");
        _compute.SetInt(ShaderIDs.Capacity, _capacity);
        _compute.SetBuffer(kernel, ShaderIDs.Particles, _particles);
        _compute.Dispatch(kernel, DivUp(_capacity, UpdateThreads), 1, 1);
    }

    void ReleaseResources()
    {
        _particles?.Dispose();
        _particles = null;
        _capacity = 0;
        DestroyObject(ref _material);
        DestroyObject(ref _gradient);
    }

    void Simulate(float dt)
    {
        var p = Profile;
        _frame++;

        _noiseTime = (_noiseTime + dt * p.NoiseSpeed) % NoisePeriod;

        // The compute shader is shared by every effect, so every uniform is
        // set again before each dispatch.
        _compute.SetInt(ShaderIDs.Capacity, _capacity);
        _compute.SetFloat(ShaderIDs.DeltaTime, dt);
        _compute.SetFloat(ShaderIDs.DragFactor, Mathf.Exp(-p.Drag * dt));
        _compute.SetVector(ShaderIDs.Gravity, p.Gravity);
        _compute.SetFloat(ShaderIDs.NoiseFrequency, p.NoiseFrequency);
        _compute.SetFloat(ShaderIDs.NoiseAmplitude, p.NoiseAmplitude);
        _compute.SetInt(ShaderIDs.NoiseOctaves, p.NoiseOctaves);
        _compute.SetVector(ShaderIDs.NoiseOffset1,
                           new Vector3(_noiseTime, 31.4f, 15.9f));
        _compute.SetVector(ShaderIDs.NoiseOffset2,
                           new Vector3(-26.5f, 35.8f, _noiseTime));

        // The emitter plane faces the viewer along its negative z axis.
        var toward = Source != null ? -Source.transform.forward : Vector3.back;
        _compute.SetFloat(ShaderIDs.StrayRatio, p.StrayRatio);
        _compute.SetFloat(ShaderIDs.StraySpeed, p.StraySpeed);
        _compute.SetVector(ShaderIDs.StrayAxis, toward);

        // Update before emission so that new particles start at the emitter
        // in the frame they appear.
        var update = _compute.FindKernel("Update");
        _compute.SetBuffer(update, ShaderIDs.Particles, _particles);
        _compute.Dispatch(update, DivUp(_capacity, UpdateThreads), 1, 1);

        Emit(dt);
    }

    void Emit(float dt)
    {
        var p = Profile;
        var source = Source;
        if (!Emitting || source == null || !source.IsReady)
        {
            _emitRemainder = 0;
            return;
        }

        _emitRemainder += p.EmissionRate * dt;
        var count = Mathf.Min((int)_emitRemainder, _capacity);
        _emitRemainder -= (int)_emitRemainder;
        if (count == 0) return;

        var kernel = _compute.FindKernel("Emit");
        _compute.SetBuffer(kernel, ShaderIDs.Particles, _particles);
        _compute.SetTexture(kernel, ShaderIDs.EmissionMap, source.EmissionMap);
        _compute.SetTexture(kernel, ShaderIDs.ColorMap, source.ColorMap);
        _compute.SetInt(ShaderIDs.EmitHead, (int)_head);
        _compute.SetInt(ShaderIDs.EmitCount, count);
        _compute.SetInt(ShaderIDs.EmitTries, EmitTries);
        _compute.SetInt(ShaderIDs.Seed, (int)(_frame * 7919 + (uint)GetHashCode()));
        _compute.SetMatrix(ShaderIDs.EmitterMatrix, source.transform.localToWorldMatrix);
        _compute.SetVector(ShaderIDs.EmitterSize, new Vector2(source.Aspect, 1));
        _compute.SetFloat(ShaderIDs.EmitterThickness, p.Thickness);
        _compute.SetFloat(ShaderIDs.RimBias, p.RimBias);
        _compute.SetVector(ShaderIDs.Lifetime, new Vector2(p.Lifetime, p.LifetimeRandomness));
        _compute.SetVector(ShaderIDs.InitialVelocity, p.InitialVelocity);
        _compute.SetFloat(ShaderIDs.InitialSpread, p.InitialSpread);
        _compute.SetFloat(ShaderIDs.NormalSpeed, p.NormalSpeed);
        _compute.Dispatch(kernel, DivUp(count, EmitThreads), 1, 1);

        _head = (uint)((_head + count) % _capacity);
    }

    void Draw()
    {
        var p = Profile;

        BakeGradient(p.ColorOverLife);

        var focus = Source != null ? Source.transform.position : Vector3.zero;

        _material.SetBuffer(ShaderIDs.Particles, _particles);
        _material.SetTexture(ShaderIDs.ColorOverLife, _gradient);
        _material.SetVector(ShaderIDs.BaseColor, p.BaseColor.linear);
        _material.SetFloat(ShaderIDs.SourceTint, p.SourceTint);
        _material.SetFloat(ShaderIDs.SourceLuminance, p.SourceLuminance);
        _material.SetFloat(ShaderIDs.SourceAdd, p.SourceAdd);
        _material.SetVector(ShaderIDs.LightHue, LightHue());
        _material.SetFloat(ShaderIDs.LightTint, p.LightTint);
        _material.SetFloat(ShaderIDs.Intensity, p.Intensity);
        _material.SetFloat(ShaderIDs.IntensityRandomness, p.IntensityRandomness);
        _material.SetFloat(ShaderIDs.Twinkle, p.Twinkle);
        _material.SetFloat(ShaderIDs.TwinkleRate, p.TwinkleRate);
        _material.SetFloat(ShaderIDs.SparkleDensity, p.SparkleDensity);
        _material.SetFloat(ShaderIDs.SparkleGain, p.SparkleGain);
        _material.SetFloat(ShaderIDs.SparkleRate, p.SparkleRate);
        _material.SetFloat(ShaderIDs.SparkleRays, p.SparkleRays);
        _material.SetFloat(ShaderIDs.PointRadius, p.PointRadius);
        _material.SetFloat(ShaderIDs.SizeRandomness, p.SizeRandomness);
        _material.SetFloat(ShaderIDs.Aperture, p.Aperture);
        _material.SetFloat(ShaderIDs.MaxCoc, p.MaxCoc);
        _material.SetVector(ShaderIDs.FocusPoint, focus);
        _material.SetFloat(ShaderIDs.FocusOffset, p.FocusOffset);
        _material.SetFloat(ShaderIDs.StrayRatio, p.StrayRatio);
        _material.SetFloat(ShaderIDs.StrayMaxCoc, p.StrayMaxCoc);
        _material.SetFloat(ShaderIDs.StrayBrightness, p.StrayBrightness);
        _material.SetFloat(ShaderIDs.ShapeSize, p.ShapeSize);
        _material.SetFloat(ShaderIDs.ShapeSpin, p.ShapeSpin);
        _material.SetFloat(ShaderIDs.ShapeLineWidth, p.ShapeLineWidth);

        if (p.Shape == ParticleShape.Polygon)
            _material.EnableKeyword("_SHAPE_MODE");
        else
            _material.DisableKeyword("_SHAPE_MODE");

        var rparams = new RenderParams(_material)
        {
            worldBounds = new Bounds(Vector3.zero, Vector3.one * 1000),
            shadowCastingMode = ShadowCastingMode.Off,
            receiveShadows = false,
            layer = gameObject.layer
        };
        Graphics.RenderPrimitives(rparams, MeshTopology.Triangles, _capacity * 6);
    }

    // The silhouette's light color scaled to unit luminance, so that tinting
    // shifts the particles' hue without dimming them. Read every frame, it
    // follows the rim light as it glides between presets, and the particles
    // of a fading preset take on the new light along with the silhouette.
    Vector4 LightHue()
    {
        if (Source == null) return Vector4.one;
        var c = Source.Color.linear;
        var luma = 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
        return (Vector4)c / Mathf.Max(luma, 1e-4f);
    }

    // Rebaked every frame so that edits to the profile show up live. It is
    // 128 evaluations and a 1 KB upload.
    void BakeGradient(Gradient gradient)
    {
        for (var i = 0; i < GradientWidth; i++)
        {
            var c = gradient.Evaluate((float)i / (GradientWidth - 1));
            _gradientPixels[i] = c.linear;
        }
        _gradient.SetPixels(_gradientPixels);
        _gradient.Apply(false);
    }

    static int DivUp(int x, int y) => (x + y - 1) / y;

    static void DestroyObject<T>(ref T obj) where T : Object
    {
        if (obj == null) return;
        if (Application.isPlaying) Destroy(obj); else DestroyImmediate(obj);
        obj = null;
    }

    static class ShaderIDs
    {
        public static readonly int Particles = Shader.PropertyToID("_Particles");
        public static readonly int Capacity = Shader.PropertyToID("_Capacity");
        public static readonly int EmitHead = Shader.PropertyToID("_EmitHead");
        public static readonly int EmitCount = Shader.PropertyToID("_EmitCount");
        public static readonly int EmitTries = Shader.PropertyToID("_EmitTries");
        public static readonly int Seed = Shader.PropertyToID("_Seed");
        public static readonly int EmitterMatrix = Shader.PropertyToID("_EmitterMatrix");
        public static readonly int EmitterSize = Shader.PropertyToID("_EmitterSize");
        public static readonly int EmitterThickness = Shader.PropertyToID("_EmitterThickness");
        public static readonly int RimBias = Shader.PropertyToID("_RimBias");
        public static readonly int Lifetime = Shader.PropertyToID("_Lifetime");
        public static readonly int InitialVelocity = Shader.PropertyToID("_InitialVelocity");
        public static readonly int InitialSpread = Shader.PropertyToID("_InitialSpread");
        public static readonly int NormalSpeed = Shader.PropertyToID("_NormalSpeed");
        public static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        public static readonly int SourceTint = Shader.PropertyToID("_SourceTint");
        public static readonly int SourceLuminance = Shader.PropertyToID("_SourceLuminance");
        public static readonly int SourceAdd = Shader.PropertyToID("_SourceAdd");
        public static readonly int LightHue = Shader.PropertyToID("_LightHue");
        public static readonly int LightTint = Shader.PropertyToID("_LightTint");
        public static readonly int EmissionMap = Shader.PropertyToID("_EmissionMap");
        public static readonly int ColorMap = Shader.PropertyToID("_ColorMap");
        public static readonly int DeltaTime = Shader.PropertyToID("_DeltaTime");
        public static readonly int DragFactor = Shader.PropertyToID("_DragFactor");
        public static readonly int Gravity = Shader.PropertyToID("_Gravity");
        public static readonly int NoiseFrequency = Shader.PropertyToID("_NoiseFrequency");
        public static readonly int NoiseAmplitude = Shader.PropertyToID("_NoiseAmplitude");
        public static readonly int NoiseOctaves = Shader.PropertyToID("_NoiseOctaves");
        public static readonly int NoiseOffset1 = Shader.PropertyToID("_NoiseOffset1");
        public static readonly int NoiseOffset2 = Shader.PropertyToID("_NoiseOffset2");
        public static readonly int StrayRatio = Shader.PropertyToID("_StrayRatio");
        public static readonly int StraySpeed = Shader.PropertyToID("_StraySpeed");
        public static readonly int StrayAxis = Shader.PropertyToID("_StrayAxis");
        public static readonly int StrayMaxCoc = Shader.PropertyToID("_StrayMaxCoc");
        public static readonly int StrayBrightness = Shader.PropertyToID("_StrayBrightness");
        public static readonly int ColorOverLife = Shader.PropertyToID("_ColorOverLife");
        public static readonly int Intensity = Shader.PropertyToID("_Intensity");
        public static readonly int IntensityRandomness = Shader.PropertyToID("_IntensityRandomness");
        public static readonly int Twinkle = Shader.PropertyToID("_Twinkle");
        public static readonly int TwinkleRate = Shader.PropertyToID("_TwinkleRate");
        public static readonly int SparkleDensity = Shader.PropertyToID("_SparkleDensity");
        public static readonly int SparkleGain = Shader.PropertyToID("_SparkleGain");
        public static readonly int SparkleRate = Shader.PropertyToID("_SparkleRate");
        public static readonly int SparkleRays = Shader.PropertyToID("_SparkleRays");
        public static readonly int PointRadius = Shader.PropertyToID("_PointRadius");
        public static readonly int SizeRandomness = Shader.PropertyToID("_SizeRandomness");
        public static readonly int Aperture = Shader.PropertyToID("_Aperture");
        public static readonly int MaxCoc = Shader.PropertyToID("_MaxCoc");
        public static readonly int FocusPoint = Shader.PropertyToID("_FocusPoint");
        public static readonly int FocusOffset = Shader.PropertyToID("_FocusOffset");
        public static readonly int ShapeSize = Shader.PropertyToID("_ShapeSize");
        public static readonly int ShapeSpin = Shader.PropertyToID("_ShapeSpin");
        public static readonly int ShapeLineWidth = Shader.PropertyToID("_ShapeLineWidth");
    }
}

} // namespace Sturm
