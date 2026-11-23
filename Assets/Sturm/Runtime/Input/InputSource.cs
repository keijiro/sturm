using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Video;

namespace Sturm
{

// Camera or video file input. Cameras come from WebCamTexture; MP4 files
// placed under StreamingAssets are listed after them for rehearsal without a
// performer. Adapted from RvmInputSource in unity-rvm-coreml.
[RequireComponent(typeof(VideoPlayer))]
public sealed class InputSource : MonoBehaviour
{
    // Public members

    // Raised when downstream state must be discarded: on a source switch and
    // when a video loops back to its first frame.
    public event Action ResetRequested;

    public IReadOnlyList<string> SourceNames => _sourceNames;
    public int SelectedIndex => _selectedIndex;
    public string StatusMessage { get; private set; }

    public void Initialize()
    {
        _videoPlayer = GetComponent<VideoPlayer>();
        ConfigureVideoPlayer();
        if (_sources != null) return;

        _sources = EnumerateSources();
        _sourceNames = _sources.Select(s => s.DisplayName).ToList();
        var saved = PlayerPrefs.GetString(PreferenceKey);
        _selectedIndex = _sources.FindIndex(s => s.DisplayName == saved);
        if (_selectedIndex < 0 && _sources.Count > 0) _selectedIndex = 0;
    }

    public void Release()
    {
        _generation++;
        StopAllCoroutines();
        _switchCoroutine = null;
        StopCurrentSource();
        _videoPlayer = null;
    }

    public void Select(int index)
    {
        if (index < 0 || index >= _sources.Count)
        {
            _selectedIndex = -1;
            StatusMessage = "No camera or MP4 input was found.";
            return;
        }
        if (index == _currentIndex && _switchCoroutine == null) return;

        _selectedIndex = index;
        PlayerPrefs.SetString(PreferenceKey, _sources[index].DisplayName);
        PlayerPrefs.Save();
        if (_switchCoroutine != null) StopCoroutine(_switchCoroutine);
        _switchCoroutine = StartCoroutine(SwitchSource(index));
    }

    // Returns a frame only when the source has produced a new one.
    public bool TryGetFrame(out Texture texture)
    {
        texture = null;
        if (_currentIndex < 0 || _currentIndex >= _sources.Count) return false;

        if (_sources[_currentIndex].Kind == SourceKind.Camera)
        {
            if (_webcam == null || !_webcam.isPlaying || !_webcam.didUpdateThisFrame ||
                _webcam.width <= 16 || _webcam.height <= 16)
                return false;
            texture = _webcam.videoVerticallyMirrored ? GetOrientedInput(_webcam) : _webcam;
        }
        else
        {
            texture = _videoPlayer.texture;
            if (!_videoFrameReady || texture == null ||
                texture.width <= 0 || texture.height <= 0)
                return false;
            _videoFrameReady = false;
        }

        return true;
    }

    // Private members

    const float CameraStartTimeout = 5;
    const int RequestedCameraWidth = 1280;
    const int RequestedCameraHeight = 720;
    const int RequestedCameraFrameRate = 30;
    const string PreferenceKey = "Sturm.SelectedSource";

    enum SourceKind { Camera, Video }

    readonly struct SourceEntry
    {
        public SourceKind Kind { get; }
        public string Location { get; }
        public string DisplayName { get; }

        public SourceEntry(SourceKind kind, string location, string displayName)
          => (Kind, Location, DisplayName) = (kind, location, displayName);
    }

    List<SourceEntry> _sources;
    List<string> _sourceNames;
    VideoPlayer _videoPlayer;
    WebCamTexture _webcam;
    RenderTexture _orientedInput;
    Coroutine _switchCoroutine;
    Coroutine _loopCoroutine;
    int _selectedIndex = -1;
    int _currentIndex = -1;
    int _generation;
    int _videoGeneration = -1;
    bool _videoFrameReady;

    static List<SourceEntry> EnumerateSources()
    {
        var sources = new List<SourceEntry>();
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var device in WebCamTexture.devices)
        {
            var baseName = $"Camera · {device.name}";
            counts.TryGetValue(baseName, out var count);
            counts[baseName] = ++count;
            var name = count == 1 ? baseName : $"{baseName} ({count})";
            sources.Add(new SourceEntry(SourceKind.Camera, device.name, name));
        }

        foreach (var path in EnumerateVideoPaths(Application.streamingAssetsPath))
            sources.Add(new SourceEntry(SourceKind.Video, path, $"Video · {path}"));

        return sources;
    }

    static string[] EnumerateVideoPaths(string root)
    {
        if (!Directory.Exists(root)) return Array.Empty<string>();
        return Directory
            .EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(path => string.Equals(Path.GetExtension(path), ".mp4",
                                         StringComparison.OrdinalIgnoreCase))
            .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
    }

    void ConfigureVideoPlayer()
    {
        _videoPlayer.playOnAwake = false;
        _videoPlayer.source = VideoSource.Url;
        _videoPlayer.renderMode = VideoRenderMode.APIOnly;
        _videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
        _videoPlayer.isLooping = false;
        _videoPlayer.skipOnDrop = true;
        _videoPlayer.sendFrameReadyEvents = true;
    }

    IEnumerator SwitchSource(int index)
    {
        var generation = ++_generation;
        StopCurrentSource();
        StatusMessage = $"Switching to {_sources[index].DisplayName}…";

        ResetRequested?.Invoke();
        _currentIndex = index;

        var source = _sources[index];
        if (source.Kind == SourceKind.Camera)
            yield return StartCamera(source, generation);
        else
            StartVideo(source, generation);

        if (generation == _generation) _switchCoroutine = null;
    }

    IEnumerator StartCamera(SourceEntry source, int generation)
    {
        if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
            yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
        if (generation != _generation) yield break;
        if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
        {
            SetSourceError("Camera access was denied. Select a video to continue.");
            yield break;
        }

        try
        {
            _webcam = new WebCamTexture(source.Location, RequestedCameraWidth,
                                        RequestedCameraHeight, RequestedCameraFrameRate);
            _webcam.Play();
        }
        catch (Exception e)
        {
            _webcam = null;
            SetSourceError($"Camera could not start: {e.Message}");
            yield break;
        }

        var deadline = Time.realtimeSinceStartup + CameraStartTimeout;
        while (generation == _generation && _webcam.isPlaying &&
               (_webcam.width <= 16 || _webcam.height <= 16) &&
               Time.realtimeSinceStartup < deadline)
            yield return null;
        if (generation != _generation) yield break;
        if (!_webcam.isPlaying || _webcam.width <= 16 || _webcam.height <= 16)
        {
            _webcam.Stop();
            _webcam = null;
            SetSourceError($"Camera did not produce frames: {source.Location}.");
            yield break;
        }

        StatusMessage = $"Camera ready · {source.Location}";
    }

    void StartVideo(SourceEntry source, int generation)
    {
        var path = Path.Combine(Application.streamingAssetsPath, source.Location);
        if (!File.Exists(path))
        {
            SetSourceError($"Video file is missing: {source.Location}.");
            return;
        }

        _videoPlayer.prepareCompleted += OnVideoPrepared;
        _videoPlayer.errorReceived += OnVideoError;
        _videoPlayer.frameReady += OnVideoFrameReady;
        _videoPlayer.loopPointReached += OnVideoLoopPoint;
        _videoGeneration = generation;

        try
        {
            _videoPlayer.url = new Uri(path).AbsoluteUri;
            _videoPlayer.Prepare();
        }
        catch (Exception e)
        {
            DetachVideoEvents();
            SetSourceError($"Video could not start: {e.Message}");
        }
    }

    // VideoPlayer may deliver callbacks after a source switch, so each one
    // checks that it still belongs to the current video.
    bool IsCurrentVideoEvent(VideoPlayer player) =>
        _videoGeneration == _generation && player == _videoPlayer;

    void OnVideoPrepared(VideoPlayer player)
    {
        if (!IsCurrentVideoEvent(player)) return;
        _videoFrameReady = false;
        player.Play();
        StatusMessage = $"Video ready · {_sources[_currentIndex].Location}";
    }

    void OnVideoError(VideoPlayer player, string message)
    {
        if (!IsCurrentVideoEvent(player)) return;
        SetSourceError($"Video decode failed: {message}");
    }

    void OnVideoFrameReady(VideoPlayer player, long frame)
    {
        if (!IsCurrentVideoEvent(player)) return;
        _videoFrameReady = true;
    }

    void OnVideoLoopPoint(VideoPlayer player)
    {
        if (!IsCurrentVideoEvent(player)) return;
        if (_loopCoroutine != null) StopCoroutine(_loopCoroutine);
        _loopCoroutine = StartCoroutine(RestartVideoLoop(_generation));
    }

    // The matting model keeps recurrent state, so a loop restarts it along
    // with the video instead of letting it carry over the cut.
    IEnumerator RestartVideoLoop(int generation)
    {
        _videoPlayer.Pause();
        _videoFrameReady = false;
        ResetRequested?.Invoke();
        _videoPlayer.frame = 0;
        yield return null;
        if (generation != _generation) yield break;
        _videoPlayer.Play();
        _loopCoroutine = null;
    }

    void StopCurrentSource()
    {
        if (_loopCoroutine != null)
        {
            StopCoroutine(_loopCoroutine);
            _loopCoroutine = null;
        }
        if (_webcam != null)
        {
            _webcam.Stop();
            Destroy(_webcam);
            _webcam = null;
        }
        ReleaseOrientedInput();

        DetachVideoEvents();
        if (_videoPlayer != null) _videoPlayer.Stop();
        _videoFrameReady = false;
        _currentIndex = -1;
    }

    void DetachVideoEvents()
    {
        _videoGeneration = -1;
        if (_videoPlayer == null) return;
        _videoPlayer.prepareCompleted -= OnVideoPrepared;
        _videoPlayer.errorReceived -= OnVideoError;
        _videoPlayer.frameReady -= OnVideoFrameReady;
        _videoPlayer.loopPointReached -= OnVideoLoopPoint;
    }

    Texture GetOrientedInput(Texture source)
    {
        if (_orientedInput == null || _orientedInput.width != source.width ||
            _orientedInput.height != source.height)
        {
            ReleaseOrientedInput();
            _orientedInput = new RenderTexture(source.width, source.height, 0,
                                               RenderTextureFormat.ARGB32,
                                               RenderTextureReadWrite.sRGB)
            {
                name = "Oriented Camera Input",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            _orientedInput.Create();
        }

        Graphics.Blit(source, _orientedInput, new Vector2(1, -1), new Vector2(0, 1));
        return _orientedInput;
    }

    void ReleaseOrientedInput()
    {
        if (_orientedInput == null) return;
        _orientedInput.Release();
        Destroy(_orientedInput);
        _orientedInput = null;
    }

    void SetSourceError(string message)
    {
        ResetRequested?.Invoke();
        StatusMessage = message;
    }
}

} // namespace Sturm
