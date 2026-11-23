using Unity.Mathematics;
using UnityEngine;

namespace Sturm
{

// Slow, small orbit around a target point. The camera stays roughly facing
// the emitter plane so the silhouette keeps reading as a figure, while the
// parallax reveals that the particles live in 3D.
public sealed class CameraDrift : MonoBehaviour
{
    // Public properties

    [field:SerializeField]
    public Transform Target { get; set; }

    [field:SerializeField, Min(0.1f)]
    public float Distance { get; set; } = 2.2f;

    // Peak yaw and pitch, in degrees
    [field:SerializeField]
    public Vector2 Swing { get; set; } = new Vector2(14, 6);

    // Peak roll, in degrees
    [field:SerializeField, Range(0, 10)]
    public float Roll { get; set; } = 1.5f;

    // Relative change of distance (0.1 = ±10%)
    [field:SerializeField, Range(0, 0.5f)]
    public float DistanceSwing { get; set; } = 0.08f;

    // Noise frequency in hertz
    [field:SerializeField, Min(0)]
    public float Frequency { get; set; } = 0.04f;

    [field:SerializeField]
    public uint Seed { get; set; } = 1;

    // MonoBehaviour implementation

    void LateUpdate()
    {
        var t = Time.time * Frequency;
        var target = Target != null ? (float3)Target.position : float3.zero;

        var yaw = math.radians(Swing.x) * Fbm(t, 0);
        var pitch = math.radians(Swing.y) * Fbm(t, 1);
        var roll = math.radians(Roll) * Fbm(t, 2);
        var dist = Distance * (1 + DistanceSwing * Fbm(t, 3));

        var orbit = quaternion.EulerYXZ(pitch, yaw, 0);
        var position = target + math.mul(orbit, new float3(0, 0, -dist));
        var look = quaternion.LookRotation(target - position, math.up());
        var rotation = math.mul(look, quaternion.RotateZ(roll));

        transform.SetPositionAndRotation(position, rotation);
    }

    // Private members

    // Two octaves of simplex noise, roughly in [-1, 1]. Each channel samples
    // its own row of the 2D field.
    float Fbm(float t, int channel)
    {
        var y = Seed * 13.7f + channel * 7.1f;
        return noise.snoise(new float2(t, y)) * 0.75f +
               noise.snoise(new float2(t * 2.1f, y + 3.3f)) * 0.25f;
    }
}

} // namespace Sturm
