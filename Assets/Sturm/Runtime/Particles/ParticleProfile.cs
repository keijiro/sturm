using UnityEngine;
using UnityEngine.Serialization;

namespace Sturm
{

public enum ParticleShape { Point, Polygon }

// Everything that distinguishes one particle effect from another. A new look
// is a new asset rather than new code (see ParticleEffect).
[CreateAssetMenu(menuName = "Sturm/Particle Profile")]
public sealed class ParticleProfile : ScriptableObject
{
    // Emission

    // Upper bound on live particles. Size it to EmissionRate * Lifetime;
    // when it is smaller, the oldest particles are recycled early.
    [field:SerializeField, Min(1024)]
    public int Capacity { get; set; } = 1 << 19;

    // Particles per second while the whole frame is body. Partial coverage
    // lowers the actual rate, since emission samples the matte.
    [field:SerializeField, Min(0)]
    public float EmissionRate { get; set; } = 200000;

    // 0 emits uniformly over the body; 1 emits only along the outline.
    [field:SerializeField, Range(0, 1)]
    public float RimBias { get; set; } = 0.6f;

    // Depth range of the emitter around the image plane
    [field:SerializeField, Min(0)]
    public float Thickness { get; set; } = 0.05f;

    // Lifetime

    [field:SerializeField, Min(0.01f)]
    public float Lifetime { get; set; } = 4;

    [field:SerializeField, Range(0, 1)]
    public float LifetimeRandomness { get; set; } = 0.6f;

    // Initial velocity

    // Constant velocity in the emitter's local space
    [field:SerializeField]
    public Vector3 InitialVelocity { get; set; } = Vector3.zero;

    // Random velocity magnitude in all directions
    [field:SerializeField, Min(0)]
    public float InitialSpread { get; set; } = 0.02f;

    // Speed along the outward silhouette normal, scaled by rimness
    [field:SerializeField]
    public float NormalSpeed { get; set; } = 0.05f;

    // Dynamics

    // Constant acceleration in world space. Upward values act as buoyancy.
    [field:SerializeField]
    public Vector3 Gravity { get; set; } = new Vector3(0, 0.05f, 0);

    // Exponential velocity damping per second
    [field:SerializeField, Min(0)]
    public float Drag { get; set; } = 1.5f;

    [field:SerializeField, Min(0)]
    public float NoiseFrequency { get; set; } = 1.2f;

    [field:SerializeField, Min(0)]
    public float NoiseAmplitude { get; set; } = 0.4f;

    // How fast the flow field evolves
    [field:SerializeField, Min(0)]
    public float NoiseSpeed { get; set; } = 0.1f;

    [field:SerializeField, Range(1, 4)]
    public int NoiseOctaves { get; set; } = 2;

    // Color

    [field:SerializeField, ColorUsage(false, true)]
    public Color BaseColor { get; set; } = Color.white;

    // Source color: how the camera image at the emission point shows through.
    // The three ways combine, so an effect can mix them.

    // Multiplies the base color by the camera color.
    [field:SerializeField, Range(0, 1)]
    [field:FormerlySerializedAs("<SourceColorMix>k__BackingField")]
    public float SourceTint { get; set; } = 0;

    // Scales brightness by the camera luminance while the hue stays the
    // base color's. Mid gray leaves it unchanged.
    [field:SerializeField, Range(0, 1)]
    public float SourceLuminance { get; set; } = 0;

    // Adds the camera color as light, untouched by the color over life
    // except for its fade envelope.
    [field:SerializeField, Min(0)]
    public float SourceAdd { get; set; } = 0;

    // Multiplies the base color over the normalized age. Alpha scales
    // brightness, so it doubles as the fade-in/out envelope.
    [field:SerializeField, GradientUsage(true)]
    public Gradient ColorOverLife { get; set; } = DefaultGradient();

    [field:SerializeField, Min(0)]
    public float Intensity { get; set; } = 2;

    [field:SerializeField, Range(0, 1)]
    public float IntensityRandomness { get; set; } = 0.7f;

    // Depth of brightness modulation over time
    [field:SerializeField, Range(0, 1)]
    public float Twinkle { get; set; } = 0;

    // Angular frequency of the twinkle, in radians per second
    [field:SerializeField, Min(0)]
    public float TwinkleRate { get; set; } = 10;

    // Sparkle: brief flares on a few particles at a time

    // Chance that a particle flares in each of its flare slots
    [field:SerializeField, Range(0, 1)]
    public float SparkleDensity { get; set; } = 0;

    // Peak brightness of a flare as a multiple of the particle's own
    [field:SerializeField, Min(0)]
    public float SparkleGain { get; set; } = 16;

    // Flare slots per second; a flare spans about a quarter of a slot.
    [field:SerializeField, Min(0.01f)]
    public float SparkleRate { get; set; } = 1.5f;

    // Length of the cross rays around a flare, in pixels at 1080 lines
    // (point shape only). The rays retract as the particle defocuses.
    [field:SerializeField, Min(0)]
    public float SparkleRays { get; set; } = 0;

    // Shape

    [field:SerializeField]
    public ParticleShape Shape { get; set; } = ParticleShape.Point;

    // Point radius in pixels at 1080 lines
    [field:SerializeField, Min(0.1f)]
    public float PointRadius { get; set; } = 1.2f;

    [field:SerializeField, Range(0, 1)]
    public float SizeRandomness { get; set; } = 0.5f;

    // Polygon half extent in world units
    [field:SerializeField, Min(0)]
    public float ShapeSize { get; set; } = 0.02f;

    // Maximum spin in radians per second; each particle picks a fraction.
    [field:SerializeField]
    public float ShapeSpin { get; set; } = 2;

    // Stroke width relative to the half extent
    [field:SerializeField, Range(0.01f, 1)]
    public float ShapeLineWidth { get; set; } = 0.12f;

    // Depth of field (point shape only)

    // Circle of confusion diameter at infinity, in pixels at 1080 lines
    [field:SerializeField, Min(0)]
    public float Aperture { get; set; } = 40;

    // Upper bound on the circle of confusion radius, which also bounds fill rate
    [field:SerializeField, Min(0)]
    public float MaxCoc { get; set; } = 24;

    // Focus distance offset from the emitter plane, in world units
    [field:SerializeField]
    public float FocusOffset { get; set; } = 0;

    // Strays: a rare few particles that leave the flow and drift far off the
    // focal plane, mostly toward the camera, so that the depth of field
    // shows as large bokeh. Keeping them rare keeps the effect coherent.

    // Fraction of particles that stray
    [field:SerializeField, Range(0, 0.05f)]
    public float StrayRatio { get; set; } = 0;

    // Mean drift speed along the view axis in world units per second
    [field:SerializeField, Min(0)]
    public float StraySpeed { get; set; } = 0.5f;

    // Upper bound on a stray's circle of confusion radius, in pixels at 1080
    // lines
    [field:SerializeField, Min(0)]
    public float StrayMaxCoc { get; set; } = 120;

    // Lowest brightness of a stray's bokeh relative to its in-focus point.
    // Above zero, strays stop conserving energy and read as lights.
    [field:SerializeField, Range(0, 1)]
    public float StrayBrightness { get; set; } = 0.05f;

    // Private members

    static Gradient DefaultGradient()
    {
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0),
                    new GradientColorKey(Color.white, 1) },
            new[] { new GradientAlphaKey(0, 0),
                    new GradientAlphaKey(1, 0.05f),
                    new GradientAlphaKey(1, 0.5f),
                    new GradientAlphaKey(0, 1) }
        );
        return g;
    }
}

} // namespace Sturm
