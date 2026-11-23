using System.Linq;
using Rvm.CoreML;
using Sturm;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;
using UnityEngine.Video;

// Scene and asset builder for the prototype, run through the Pipeline
// package (see README.md).
//
// Profiles are only initialized when their asset does not exist yet, so the
// values below are starting points and tuning done in the Inspector survives
// a rerun. The scene, on the other hand, is rebuilt from scratch except for
// the camera and the UI object: manual edits to Main.unity are lost.
public static class Build
{
    const string Root = "Assets/Sturm";
    const string ProfileDir = Root + "/Profiles";
    const string ScenePath = "Assets/Main.unity";

    // Step 1: script default references (triggers a script reimport)
    public static string Defaults()
    {
        SetDefaults(Root + "/Runtime/Particles/ParticleEffect.cs",
            ("_compute", Load<ComputeShader>(Root + "/Runtime/Particles/ParticleSimulation.compute")),
            ("_shader", Load<Shader>(Root + "/Runtime/Particles/ParticleRender.shader")));
        SetDefaults(Root + "/Runtime/Silhouette/Silhouette.cs",
            ("_filterShader", Load<Shader>(Root + "/Runtime/Silhouette/SilhouetteFilter.shader")),
            ("_displayShader", Load<Shader>(Root + "/Runtime/Silhouette/SilhouetteDisplay.shader")));
        return "defaults set";
    }

    // Step 2: profiles, post-processing and the scene
    public static string All()
    {
        // Opening a scene unloads unreferenced assets, which would invalidate
        // freshly loaded profiles, so the scene is opened first.
        EditorSceneManager.OpenScene(ScenePath);
        var profiles = CreateProfiles();
        var volume = CreateVolumeProfile();
        BuildScene(profiles, volume);
        PlayerSettings.macOS.cameraUsageDescription = "Camera input for the live visuals";
        AssetDatabase.SaveAssets();
        return "built";
    }

    // Profiles

    static (ParticleProfile stardust, ParticleProfile geometry, ParticleProfile ash,
            ParticleProfile nebula, ParticleProfile ember) CreateProfiles()
    {
        // Fine white dust peeling off the outline and rising
        var stardust = Profile("Stardust", p =>
        {
            p.Capacity = 1 << 18;
            p.EmissionRate = 40000;
            p.RimBias = 1;
            p.Thickness = 0.15f;
            p.Lifetime = 4;
            p.LifetimeRandomness = 0.6f;
            p.InitialVelocity = new Vector3(0, 0.03f, 0);
            p.InitialSpread = 0.02f;
            p.NormalSpeed = 0.1f;
            p.Gravity = new Vector3(0, 0.08f, 0);
            p.Drag = 1.5f;
            p.NoiseFrequency = 1.5f;
            p.NoiseAmplitude = 0.15f;
            p.NoiseSpeed = 0.1f;
            p.NoiseOctaves = 2;
            p.BaseColor = Color.white;
            p.SourceLuminance = 0.7f;
            p.ColorOverLife = Gradient((0, Color.white), (1, Color.white),
                                       (0, 0), (0.03f, 1), (0.5f, 0.8f), (1, 0));
            p.Intensity = 2;
            p.IntensityRandomness = 0.8f;
            p.Twinkle = 0.6f;
            p.TwinkleRate = 8;
            p.SparkleDensity = 0.03f;
            p.SparkleGain = 20;
            p.SparkleRate = 1.5f;
            p.SparkleRays = 12;
            p.Shape = ParticleShape.Point;
            p.PointRadius = 1.0f;
            p.SizeRandomness = 0.6f;
            p.Aperture = 120;
            p.MaxCoc = 24;
            p.FocusOffset = 0;
            p.StrayRatio = 0.002f;
            p.StraySpeed = 0.5f;
            p.StrayMaxCoc = 120;
            p.StrayBrightness = 0.04f;
        });

        // Sparse polygon outlines drifting upward, layered over Stardust
        var geometry = Profile("Geometry", p =>
        {
            p.Capacity = 1 << 12;
            p.EmissionRate = 60;
            p.RimBias = 0.9f;
            p.Thickness = 0.3f;
            p.Lifetime = 8;
            p.LifetimeRandomness = 0.5f;
            p.InitialVelocity = new Vector3(0, 0.05f, 0);
            p.InitialSpread = 0.03f;
            p.NormalSpeed = 0.1f;
            p.Gravity = new Vector3(0, 0.08f, 0);
            p.Drag = 0.8f;
            p.NoiseFrequency = 0.8f;
            p.NoiseAmplitude = 0.12f;
            p.NoiseSpeed = 0.05f;
            p.NoiseOctaves = 1;
            p.BaseColor = Color.white;
            p.ColorOverLife = Gradient((0, Color.white), (1, Color.white),
                                       (0, 0), (0.1f, 1), (0.6f, 1), (1, 0));
            p.Intensity = 3;
            p.IntensityRandomness = 0.5f;
            p.Twinkle = 0.3f;
            p.TwinkleRate = 4;
            p.SparkleDensity = 0.05f;
            p.SparkleGain = 4;
            p.SparkleRate = 1;
            p.Shape = ParticleShape.Polygon;
            p.ShapeSize = 0.012f;
            p.SizeRandomness = 0.7f;
            p.ShapeSpin = 1.5f;
            p.ShapeLineWidth = 0.14f;
        });

        // The whole body disintegrating into grains blown sideways. The grains
        // carry the camera image as added light, so the performer's own
        // colors blow away with them.
        var ash = Profile("Ash", p =>
        {
            p.Capacity = 1 << 19;
            p.EmissionRate = 80000;
            p.RimBias = 0.3f;
            p.Thickness = 0.2f;
            p.Lifetime = 3.5f;
            p.LifetimeRandomness = 0.6f;
            p.InitialVelocity = new Vector3(0.05f, 0.03f, 0);
            p.InitialSpread = 0.01f;
            p.NormalSpeed = 0.03f;
            p.Gravity = new Vector3(0.15f, 0.03f, -0.02f);
            p.Drag = 1.0f;
            p.NoiseFrequency = 2.0f;
            p.NoiseAmplitude = 0.2f;
            p.NoiseSpeed = 0.15f;
            p.NoiseOctaves = 3;
            p.BaseColor = new Color(0.35f, 0.36f, 0.4f);
            p.SourceAdd = 3;
            p.ColorOverLife = Gradient((0, Color.white), (1, Color.white),
                                       (0, 0), (0.02f, 1), (0.4f, 0.6f), (1, 0));
            p.Intensity = 1.0f;
            p.IntensityRandomness = 0.8f;
            p.Twinkle = 0;
            p.SparkleDensity = 0.01f;
            p.SparkleGain = 12;
            p.SparkleRate = 1;
            p.Shape = ParticleShape.Point;
            p.PointRadius = 1.4f;
            p.SizeRandomness = 0.7f;
            p.Aperture = 150;
            p.MaxCoc = 28;
            p.StrayRatio = 0.0015f;
            p.StraySpeed = 0.4f;
            p.StrayMaxCoc = 120;
            p.StrayBrightness = 0.04f;
        });

        // A dense body of colored grains tinted by the camera image
        var nebula = Profile("Nebula", p =>
        {
            p.Capacity = 1 << 19;
            p.EmissionRate = 120000;
            p.RimBias = 0.1f;
            p.Thickness = 0.2f;
            p.Lifetime = 3;
            p.LifetimeRandomness = 0.8f;
            p.InitialVelocity = Vector3.zero;
            p.InitialSpread = 0.015f;
            p.NormalSpeed = 0.02f;
            p.Gravity = new Vector3(0, 0.02f, 0);
            p.Drag = 1.5f;
            p.NoiseFrequency = 2.5f;
            p.NoiseAmplitude = 0.2f;
            p.NoiseSpeed = 0.1f;
            p.NoiseOctaves = 2;
            p.BaseColor = Color.white;
            p.SourceTint = 0.6f;
            p.SourceAdd = 0.5f;
            p.ColorOverLife = Gradient((0, new Color(0.35f, 0.55f, 1)),
                                       (0.5f, new Color(1, 0.65f, 0.3f)),
                                       (1, new Color(1, 0.35f, 0.45f)),
                                       (0, 0), (0.05f, 1), (0.6f, 0.8f), (1, 0));
            p.Intensity = 1.0f;
            p.IntensityRandomness = 0.9f;
            p.Twinkle = 0.3f;
            p.TwinkleRate = 5;
            p.SparkleDensity = 0.02f;
            p.SparkleGain = 12;
            p.SparkleRate = 2;
            p.SparkleRays = 10;
            p.Shape = ParticleShape.Point;
            p.PointRadius = 1.1f;
            p.SizeRandomness = 0.5f;
            p.Aperture = 200;
            p.MaxCoc = 32;
            p.StrayRatio = 0.0007f;
            p.StraySpeed = 0.5f;
            p.StrayMaxCoc = 120;
            p.StrayBrightness = 0.025f;
        });

        // Flame-like aura rising off the outline, cooling from gold to violet
        var ember = Profile("Ember", p =>
        {
            p.Capacity = 1 << 18;
            p.EmissionRate = 60000;
            p.RimBias = 0.9f;
            p.Thickness = 0.15f;
            p.Lifetime = 2.5f;
            p.LifetimeRandomness = 0.5f;
            p.InitialVelocity = new Vector3(0, 0.1f, 0);
            p.InitialSpread = 0.03f;
            p.NormalSpeed = 0.08f;
            p.Gravity = new Vector3(0, 0.3f, 0);
            p.Drag = 2;
            p.NoiseFrequency = 1.8f;
            p.NoiseAmplitude = 0.5f;
            p.NoiseSpeed = 0.3f;
            p.NoiseOctaves = 3;
            p.BaseColor = Color.white;
            p.SourceLuminance = 0.5f;
            p.ColorOverLife = Gradient((0, new Color(1, 0.95f, 0.7f)),
                                       (0.25f, new Color(1, 0.55f, 0.15f)),
                                       (0.6f, new Color(0.95f, 0.2f, 0.35f)),
                                       (1, new Color(0.45f, 0.15f, 0.9f)),
                                       (0, 0), (0.03f, 1), (0.5f, 0.7f), (1, 0));
            p.Intensity = 1.2f;
            p.IntensityRandomness = 0.7f;
            p.Twinkle = 0.2f;
            p.TwinkleRate = 6;
            p.SparkleDensity = 0.02f;
            p.SparkleGain = 14;
            p.SparkleRate = 2.5f;
            p.SparkleRays = 8;
            p.Shape = ParticleShape.Point;
            p.PointRadius = 1.0f;
            p.SizeRandomness = 0.5f;
            p.Aperture = 120;
            p.MaxCoc = 24;
            p.StrayRatio = 0.002f;
            p.StraySpeed = 0.6f;
            p.StrayMaxCoc = 120;
            p.StrayBrightness = 0.04f;
        });

        AssetDatabase.SaveAssets();
        return (stardust, geometry, ash, nebula, ember);
    }

    static ParticleProfile Profile(string name, System.Action<ParticleProfile> setup)
    {
        var path = $"{ProfileDir}/{name}.asset";
        var asset = AssetDatabase.LoadAssetAtPath<ParticleProfile>(path);
        if (asset != null) return asset;
        asset = ScriptableObject.CreateInstance<ParticleProfile>();
        setup(asset);
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    // Color keys are (time, color); alpha keys are (time, alpha) floats.
    static Gradient Gradient(params object[] keys)
    {
        var colors = keys.OfType<(int, Color)>()
            .Select(k => new GradientColorKey(k.Item2, k.Item1))
            .Concat(keys.OfType<(float, Color)>()
            .Select(k => new GradientColorKey(k.Item2, k.Item1)))
            .OrderBy(k => k.time).ToArray();
        var alphas = keys.OfType<(int, int)>()
            .Select(k => new GradientAlphaKey(k.Item2, k.Item1))
            .Concat(keys.OfType<(float, int)>()
            .Select(k => new GradientAlphaKey(k.Item2, k.Item1)))
            .Concat(keys.OfType<(int, float)>()
            .Select(k => new GradientAlphaKey(k.Item2, k.Item1)))
            .Concat(keys.OfType<(float, float)>()
            .Select(k => new GradientAlphaKey(k.Item2, k.Item1)))
            .OrderBy(k => k.time).ToArray();
        var g = new Gradient();
        g.SetKeys(colors, alphas);
        return g;
    }

    // Post-processing

    static VolumeProfile CreateVolumeProfile()
    {
        var path = Root + "/PostProcess.asset";
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);
        }

        var bloom = GetOrAdd<Bloom>(profile);
        bloom.threshold.Override(0.8f);
        bloom.intensity.Override(1.2f);
        bloom.scatter.Override(0.75f);
        bloom.highQualityFiltering.Override(true);

        var tonemap = GetOrAdd<Tonemapping>(profile);
        tonemap.mode.Override(TonemappingMode.Neutral);

        var vignette = GetOrAdd<Vignette>(profile);
        vignette.intensity.Override(0.25f);
        vignette.smoothness.Override(0.5f);

        EditorUtility.SetDirty(profile);
        return profile;
    }

    static T GetOrAdd<T>(VolumeProfile profile) where T : VolumeComponent
    {
        if (profile.TryGet<T>(out var c)) return c;
        c = profile.Add<T>(true);
        c.name = typeof(T).Name;
        AssetDatabase.AddObjectToAsset(c, profile);
        return c;
    }

    // Scene

    static void BuildScene
      ((ParticleProfile stardust, ParticleProfile geometry, ParticleProfile ash,
        ParticleProfile nebula, ParticleProfile ember) profiles,
       VolumeProfile volumeProfile)
    {
        var scene = EditorSceneManager.GetActiveScene();
        var roots = scene.GetRootGameObjects();

        // Keep the UI and the camera from the template; everything else
        // (and anything from a previous build) is rebuilt.
        var ui = roots.First(go => go.name == "UI");
        var camera = roots.SelectMany(go => go.GetComponentsInChildren<Camera>(true)).First();
        camera.transform.SetParent(null);
        foreach (var go in scene.GetRootGameObjects())
            if (go != ui && go != camera.gameObject) Object.DestroyImmediate(go);

        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = Color.black;

        // Emitter plane and silhouette
        var emitter = new GameObject("Emitter").AddComponent<Silhouette>();
        AssignShaders(emitter, ("_filterShader", Root + "/Runtime/Silhouette/SilhouetteFilter.shader"),
                               ("_displayShader", Root + "/Runtime/Silhouette/SilhouetteDisplay.shader"));

        // Camera
        camera.name = "Main Camera";
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.fieldOfView = 35;
        camera.nearClipPlane = 0.05f;
        camera.farClipPlane = 50;
        camera.allowHDR = true;
        camera.allowMSAA = false;
        camera.transform.SetPositionAndRotation(new Vector3(0, 0, -2.2f), Quaternion.identity);
        var camData = camera.GetComponent<UniversalAdditionalCameraData>();
        camData.renderPostProcessing = true;
        camData.antialiasing = AntialiasingMode.None;
        if (!camera.TryGetComponent<CameraDrift>(out var drift))
            drift = camera.gameObject.AddComponent<CameraDrift>();
        drift.Target = emitter.transform;
        drift.Distance = 2.2f;

        // Post-processing
        var volume = new GameObject("Post Process").AddComponent<Volume>();
        volume.isGlobal = true;
        volume.sharedProfile = volumeProfile;

        // Input and matting
        var input = new GameObject("Input");
        input.AddComponent<VideoPlayer>();
        var source = input.AddComponent<InputSource>();
        var generator = new GameObject("Matting").AddComponent<MatteGenerator>();

        // Effect presets
        var effects = new GameObject("Effects").transform;
        var presets = new[]
        {
            Preset("Stardust", effects, emitter, new Color32(0x8a, 0xb2, 0xcc, 0xff), 1.5f,
                   profiles.stardust),
            Preset("Constellation", effects, emitter, new Color32(0xa0, 0xa8, 0xd8, 0xff), 1.5f,
                   profiles.stardust, profiles.geometry),
            Preset("Ash", effects, emitter, new Color32(0xc8, 0xc8, 0xc8, 0xff), 1.2f,
                   profiles.ash),
            Preset("Nebula", effects, emitter, new Color32(0x70, 0x90, 0xff, 0xff), 1.5f,
                   profiles.nebula),
            Preset("Ember", effects, emitter, new Color32(0xff, 0x9a, 0x50, 0xff), 1.5f,
                   profiles.ember)
        };

        // Overlay UI and the app controller
        if (!ui.TryGetComponent<AppView>(out var view)) view = ui.AddComponent<AppView>();
        var app = new GameObject("App").AddComponent<AppController>();
        var so = new SerializedObject(app);
        so.FindProperty("_source").objectReferenceValue = source;
        so.FindProperty("_generator").objectReferenceValue = generator;
        so.FindProperty("_silhouette").objectReferenceValue = emitter;
        so.FindProperty("_view").objectReferenceValue = view;
        var list = so.FindProperty("_presets");
        list.arraySize = presets.Length;
        for (var i = 0; i < presets.Length; i++)
            list.GetArrayElementAtIndex(i).objectReferenceValue = presets[i];
        so.ApplyModifiedPropertiesWithoutUndo();

        // Hierarchy order for readability
        var order = new GameObject[] { app.gameObject, input, generator.gameObject,
                                       emitter.gameObject, effects.gameObject,
                                       camera.gameObject, volume.gameObject, ui };
        for (var i = 0; i < order.Length; i++) order[i].transform.SetSiblingIndex(i);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    static EffectPreset Preset(string name, Transform parent, Silhouette source,
                               Color rimColor, float rimBrightness,
                               params ParticleProfile[] profiles)
    {
        var preset = new GameObject(name).AddComponent<EffectPreset>();
        preset.RimColor = rimColor;
        preset.RimBrightness = rimBrightness;
        preset.transform.SetParent(parent, false);
        foreach (var profile in profiles)
        {
            var go = new GameObject(profile.name);
            go.transform.SetParent(preset.transform, false);
            var effect = go.AddComponent<ParticleEffect>();
            effect.Profile = profile;
            effect.Source = source;
            effect.Emitting = false;
            AssignShaders(effect, ("_compute", Root + "/Runtime/Particles/ParticleSimulation.compute"),
                                  ("_shader", Root + "/Runtime/Particles/ParticleRender.shader"));
        }
        return preset;
    }

    // Helpers

    static T Load<T>(string path) where T : Object
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null) throw new System.Exception($"Missing asset: {path}");
        return asset;
    }

    static void AssignShaders(Object target, params (string field, string path)[] refs)
    {
        var so = new SerializedObject(target);
        foreach (var (field, path) in refs)
            so.FindProperty(field).objectReferenceValue = Load<Object>(path);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetDefaults(string scriptPath, params (string name, Object obj)[] refs)
    {
        var importer = (MonoImporter)AssetImporter.GetAtPath(scriptPath);
        importer.SetDefaultReferences(refs.Select(r => r.name).ToArray(),
                                      refs.Select(r => r.obj).ToArray());
        importer.SaveAndReimport();
    }
}
