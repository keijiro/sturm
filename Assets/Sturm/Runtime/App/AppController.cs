using System.Linq;
using Rvm.CoreML;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.InputSystem;

namespace Sturm
{

// Wires the input, matting, silhouette and particle effects together and
// handles the operator's controls.
//
// Keys: 1-9 select a preset, Left/Right step through them, Tab toggles the
// overlay UI.
public sealed class AppController : MonoBehaviour
{
    [SerializeField] InputSource _source = null;
    [SerializeField] MatteGenerator _generator = null;
    [SerializeField] Silhouette _silhouette = null;
    [SerializeField] EffectPreset[] _presets = null;
    [SerializeField] AppView _view = null;

    // MonoBehaviour implementation

    void OnEnable()
    {
        // The generator writes into a texture owned here so that it is
        // cleared before the first matte arrives and can be cleared again on
        // every reset. The sRGB format keeps precision in the dark RGB.
        _matte = new RenderTexture(MatteWidth, MatteHeight, 0,
                                   GraphicsFormat.R8G8B8A8_SRGB)
        {
            name = "Matte",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        _matte.Create();
        ClearTexture(_matte);
        _generator.Output = _matte;
        _silhouette.Matte = _matte;

        _source.ResetRequested += OnResetRequested;
        _source.Initialize();

        _view.SourceSelected += OnSourceSelected;
        _view.EffectSelected += SelectEffect;
        _view.SetSources(_source.SourceNames, _source.SelectedIndex);
        _view.SetEffects(_presets.Select(p => p.name).ToList());
        _view.Initialize();

        SelectEffect(PlayerPrefs.GetInt(EffectPreferenceKey, 0));
        _source.Select(_source.SelectedIndex);
    }

    void OnDisable()
    {
        if (_view != null)
        {
            _view.SourceSelected -= OnSourceSelected;
            _view.EffectSelected -= SelectEffect;
            _view.Release();
        }

        if (_source != null)
        {
            _source.ResetRequested -= OnResetRequested;
            _source.Release();
        }

        if (_generator != null && _generator.Output == _matte) _generator.Output = null;
        if (_silhouette != null) _silhouette.Matte = null;
        if (_matte != null)
        {
            _matte.Release();
            Destroy(_matte);
            _matte = null;
        }
        _pendingFrame = null;
    }

    void Update()
    {
        // Camera and video textures update in place, so a frame the generator
        // was too busy to accept is still current when retried.
        if (_source.TryGetFrame(out var frame)) _pendingFrame = frame;
        if (_pendingFrame != null && _generator.Process(_pendingFrame))
            _pendingFrame = null;

        HandleKeyboard();
        UpdateRimLight();

        _frameTime = Mathf.Lerp(_frameTime, Time.unscaledDeltaTime, 0.05f);
        _view.SetStatus(_source.StatusMessage, _generator.LastError,
                        _generator.InferenceTime, _frameTime * 1000);
    }

    // Private members

    const int MatteWidth = 1280;
    const int MatteHeight = 720;
    const string EffectPreferenceKey = "Sturm.SelectedEffect";

    RenderTexture _matte;
    Texture _pendingFrame;
    int _effectIndex = -1;
    float _frameTime = 1.0f / 60;

    // Rim light changes glide over about a second, in step with the particle
    // cross-fade.
    const float RimTransitionRate = 3;

    void HandleKeyboard()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.tabKey.wasPressedThisFrame) _view.ToggleVisible();
        if (kb.rightArrowKey.wasPressedThisFrame) SelectEffect(_effectIndex + 1);
        if (kb.leftArrowKey.wasPressedThisFrame) SelectEffect(_effectIndex - 1);

        for (var i = 0; i < 9; i++)
            if (kb[Key.Digit1 + i].wasPressedThisFrame) SelectEffect(i);
    }

    // Only the selected preset emits; the others fade out on their own (see
    // ParticleEffect.Emitting).
    void SelectEffect(int index)
    {
        var count = _presets.Length;
        if (count == 0) return;
        index = (index % count + count) % count;

        _effectIndex = index;
        for (var i = 0; i < count; i++) _presets[i].Emitting = i == index;

        PlayerPrefs.SetInt(EffectPreferenceKey, index);
        _view.SetSelectedEffect(index);
    }

    void UpdateRimLight()
    {
        if (_effectIndex < 0) return;
        var preset = _presets[_effectIndex];
        var t = 1 - Mathf.Exp(-RimTransitionRate * Time.deltaTime);
        _silhouette.Color = Color.Lerp(_silhouette.Color, preset.RimColor, t);
        _silhouette.Brightness = Mathf.Lerp(_silhouette.Brightness, preset.RimBrightness, t);
    }

    void OnSourceSelected(int index)
    {
        _source.Select(index);
        _view.SetSelectedSource(_source.SelectedIndex);
    }

    // Clearing the matte hides the previous source's silhouette and stops
    // emission until the first matte of the new stream arrives.
    void OnResetRequested()
    {
        _pendingFrame = null;
        _generator.Reset();
        ClearTexture(_matte);
    }

    static void ClearTexture(RenderTexture texture)
    {
        if (texture == null) return;
        var previous = RenderTexture.active;
        RenderTexture.active = texture;
        GL.Clear(false, true, Color.clear);
        RenderTexture.active = previous;
    }
}

} // namespace Sturm
