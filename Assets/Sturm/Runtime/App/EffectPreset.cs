using UnityEngine;

namespace Sturm
{

// A selectable look: the ParticleEffects under this GameObject run together
// as layers (fine dust plus outlined shapes, for example), and the silhouette
// they share takes this preset's rim light.
public sealed class EffectPreset : MonoBehaviour
{
    // Public properties

    // Targets for Silhouette.Color and Silhouette.Brightness. AppController
    // glides the silhouette toward them while this preset is selected.
    [field:SerializeField, ColorUsage(false)]
    public Color RimColor { get; set; } = new Color32(0x8a, 0xb2, 0xcc, 0xff);

    [field:SerializeField, Range(0, 8)]
    public float RimBrightness { get; set; } = 1.5f;

    // Public members

    public bool Emitting
    {
        set
        {
            foreach (var effect in GetComponentsInChildren<ParticleEffect>())
                effect.Emitting = value;
        }
    }
}

} // namespace Sturm
