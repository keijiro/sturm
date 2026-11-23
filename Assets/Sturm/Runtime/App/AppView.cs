using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Sturm
{

// Operator overlay: input source and effect selectors plus a status line.
// Kept in a corner and hideable, since the output goes straight to the
// stage screen.
[RequireComponent(typeof(PanelRenderer))]
public sealed class AppView : MonoBehaviour
{
    // Public members

    public event Action<int> SourceSelected;
    public event Action<int> EffectSelected;

    public void Initialize()
    {
        _uiVersion = -1;
        _panelRenderer = GetComponent<PanelRenderer>();
        _panelRenderer.RegisterUIReloadCallback(OnUIReload);
    }

    public void Release()
    {
        if (_panelRenderer != null)
            _panelRenderer.UnregisterUIReloadCallback(OnUIReload);
        _panelRenderer = null;
        UnbindUI();
    }

    public void SetSources(IReadOnlyList<string> names, int selected)
    {
        _sourceChoices.Clear();
        if (names != null) _sourceChoices.AddRange(names);
        _sourceIndex = selected;
        BindDropdown(_sourceDropdown, _sourceChoices, _sourceIndex, OnSourceChanged);
    }

    public void SetEffects(IReadOnlyList<string> names)
    {
        _effectChoices.Clear();
        if (names != null) _effectChoices.AddRange(names);
        BindDropdown(_effectDropdown, _effectChoices, _effectIndex, OnEffectChanged);
    }

    public void SetSelectedSource(int index)
    {
        _sourceIndex = index;
        SetDropdownValue(_sourceDropdown, _sourceChoices, index);
    }

    public void SetSelectedEffect(int index)
    {
        _effectIndex = index;
        SetDropdownValue(_effectDropdown, _effectChoices, index);
    }

    public void SetStatus(string status, string error, double inferenceTime, float frameTime)
    {
        (_status, _error, _inferenceTime, _frameTime) = (status, error, inferenceTime, frameTime);
        RefreshStatus();
    }

    public void ToggleVisible()
    {
        _visible = !_visible;
        ApplyVisibility();
    }

    // Private members

    readonly List<string> _sourceChoices = new();
    readonly List<string> _effectChoices = new();
    PanelRenderer _panelRenderer;
    VisualElement _controls;
    DropdownField _sourceDropdown;
    DropdownField _effectDropdown;
    Label _statusLabel;
    int _uiVersion = -1;
    int _sourceIndex = -1;
    int _effectIndex = -1;
    bool _visible = true;
    string _status;
    string _error;
    double _inferenceTime;
    float _frameTime;

    void OnUIReload(PanelRenderer renderer, VisualElement root, int version)
    {
        if (root == null || version == _uiVersion) return;
        _uiVersion = version;

        UnbindUI();
        _controls = root.Q<VisualElement>("controls");
        _sourceDropdown = root.Q<DropdownField>("sourceDropdown");
        _effectDropdown = root.Q<DropdownField>("effectDropdown");
        _statusLabel = root.Q<Label>("statusLabel");

        BindDropdown(_sourceDropdown, _sourceChoices, _sourceIndex, OnSourceChanged);
        BindDropdown(_effectDropdown, _effectChoices, _effectIndex, OnEffectChanged);
        ApplyVisibility();
        RefreshStatus();
    }

    void UnbindUI()
    {
        _sourceDropdown?.UnregisterValueChangedCallback(OnSourceChanged);
        _effectDropdown?.UnregisterValueChangedCallback(OnEffectChanged);
        _controls = null;
        _sourceDropdown = null;
        _effectDropdown = null;
        _statusLabel = null;
    }

    static void BindDropdown(DropdownField dropdown, List<string> choices, int index,
                             EventCallback<ChangeEvent<string>> callback)
    {
        if (dropdown == null) return;
        dropdown.UnregisterValueChangedCallback(callback);
        dropdown.choices = new List<string>(choices);
        dropdown.SetEnabled(choices.Count > 0);
        SetDropdownValue(dropdown, choices, index);
        dropdown.RegisterValueChangedCallback(callback);
    }

    static void SetDropdownValue(DropdownField dropdown, List<string> choices, int index)
    {
        if (dropdown == null) return;
        var valid = index >= 0 && index < choices.Count;
        dropdown.SetValueWithoutNotify(valid ? choices[index] : string.Empty);
    }

    void OnSourceChanged(ChangeEvent<string> change)
    {
        _sourceIndex = _sourceDropdown.index;
        SourceSelected?.Invoke(_sourceIndex);
    }

    void OnEffectChanged(ChangeEvent<string> change)
    {
        _effectIndex = _effectDropdown.index;
        EffectSelected?.Invoke(_effectIndex);
    }

    void ApplyVisibility()
    {
        if (_controls == null) return;
        _controls.style.display = _visible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    void RefreshStatus()
    {
        if (_statusLabel == null) return;
        var hasError = !string.IsNullOrEmpty(_error);
        _statusLabel.tooltip = hasError ? _error : _status;
        var inference = !hasError && _inferenceTime > 0 ? $"{_inferenceTime:F1} ms" : "—";
        _statusLabel.text = $"RVM {inference}   Frame {_frameTime:F1} ms";
    }
}

} // namespace Sturm
