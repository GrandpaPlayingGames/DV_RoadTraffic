using DV_RoadTraffic;
using System.IO;
using UnityEngine;
using System.Collections.Generic;

public class TrafficVehicleAudio : MonoBehaviour
{
    private AudioSource engineSource;
    private AudioSource oneshotSource;

    private float basePitch = 0.8f;
    private float maxPitch = 1.6f;

    private TrafficVehicleController controller;

    public void Initialize(TrafficVehicleController vehicle, AudioClip engineClip)
    {
        controller = vehicle;

        engineSource = gameObject.AddComponent<AudioSource>();
        oneshotSource = gameObject.AddComponent<AudioSource>();

        engineSource.clip = engineClip;
        engineSource.loop = true;
        engineSource.spatialBlend = 1f;
        engineSource.dopplerLevel = 1f;
        engineSource.rolloffMode = AudioRolloffMode.Linear;
        engineSource.maxDistance = 30f;
        engineSource.minDistance = 3f;

        engineSource.Play();
    }

    void Update()
    {
        if (controller == null || engineSource == null)
            return;

        float speed = controller.CurrentSpeed;
        float max = controller.MaxSpeed;

        if (max <= 0f)
            return;

        float t = Mathf.Clamp01(speed / max);

        engineSource.pitch = Mathf.Lerp(basePitch, maxPitch, t);
    }

    public void PlayHorn(AudioClip clip)
    {
        if (clip == null || oneshotSource == null)
            return;

        oneshotSource.PlayOneShot(clip);
    }

    public void PlayBrake(AudioClip clip)
    {
        oneshotSource.PlayOneShot(clip);
    }
}


namespace DV_RoadTraffic
{
    public static class DVRT_SoundLoader
    {
        public static AudioClip LoadMP3(string path)
        {
            if (!File.Exists(path))
            {
                Main.Log($"[DVRT] Sound file not found: {path}");
                return null;
            }

            string url = "file://" + path.Replace("\\", "/");

            WWW www = new WWW(url);

            while (!www.isDone) { }

            if (!string.IsNullOrEmpty(www.error))
            {
                Main.Log($"[DVRT] Failed loading sound: {www.error}");
                return null;
            }

            return www.GetAudioClip(false, false);
        }
    }
}

namespace DV_RoadTraffic
{
    public static class DVRT_SoundLibrary
    {
        private static Dictionary<string, List<AudioClip>> engineClips =
            new Dictionary<string, List<AudioClip>>();

        private static Dictionary<string, List<AudioClip>> hornClips =
            new Dictionary<string, List<AudioClip>>();

        private static Dictionary<string, List<AudioClip>> explosionClips =
    new Dictionary<string, List<AudioClip>>();

        private static Dictionary<string, List<AudioClip>> gunshotClips =
    new Dictionary<string, List<AudioClip>>();

        private static bool initialized = false;

        public static void Initialize()
        {
            if (initialized)
                return;

            LoadCategory("Engine", engineClips);
            LoadCategory("Horn", hornClips);
            LoadCategory("Other/Explosion", explosionClips);
            LoadCategory("Other", gunshotClips);

            initialized = true;

            Main.Log("[DVRT] Sound library initialized.");
        }

        private static void LoadCategory(
            string category,
            Dictionary<string, List<AudioClip>> target)
        {
            string basePath = Path.Combine(
                Main.Mod.Path,
                "Assets",
                "Sounds",
                category);

            if (!Directory.Exists(basePath))
            {
                Main.Log($"[DVRT] Sound folder missing: {basePath}");
                return;
            }

            foreach (var dir in Directory.GetDirectories(basePath))
            {
                string group = Path.GetFileName(dir);

                if (!target.ContainsKey(group))
                    target[group] = new List<AudioClip>();

                var files = Directory.GetFiles(dir, "*.mp3");

                foreach (var file in files)
                {
                    var clip = DVRT_SoundLoader.LoadMP3(file);

                    if (clip != null)
                    {
                        target[group].Add(clip);

                        Main.Log(
                            $"[DVRT] Loaded {category}/{group}/{Path.GetFileName(file)}",
                            true);
                    }
                }
            }
        }

        public static AudioClip GetRandomEngine(string group)
        {
            return GetRandom(engineClips, group);
        }

        public static string DetermineVehicleGroup(string cleanName)
        {
            string name = cleanName.ToLower();

            if (name.Contains("bus"))
                return "Bus";

            if (name.Contains("car"))
                return "Car";

            return "Truck";
        }

        public static AudioClip GetRandomHorn(string group)
        {
            // buses use truck horns
            if (group == "Bus")
                group = "Truck";
            
            return GetRandom(hornClips, group);
        }

        public static AudioClip GetRandomExplosion()
        {
            return GetRandom(explosionClips, "Default");
        }

        public static AudioClip GetRandomGunshot()
        {
            return GetRandom(gunshotClips, "Gunshot");
        }

        private static AudioClip GetRandom(
            Dictionary<string, List<AudioClip>> source,
            string group)
        {
            Main.Log($"[Random] source = {source}, group = {group}");
            if (!source.ContainsKey(group))
                return null;

            var list = source[group];
            Main.Log($"[Random] list.Count = {list.Count}");
            if (list.Count == 0)
                return null;

            return list[UnityEngine.Random.Range(0, list.Count)];
        }
    }
}

public static class DVRT_ParticleLibrary
{
    public static Texture2D ExplosionTexture;
    public static Texture2D FireTexture;

    public static Material ExplosionMaterial;
    public static Material FireMaterial;

    public static void Initialize()
    {
        string basePath = Path.Combine(Main.Mod.Path, "Assets", "Particles");

        ExplosionTexture = LoadTexture(Path.Combine(basePath, "explosion.png"));
        FireTexture = LoadTexture(Path.Combine(basePath, "fire.png"));

        // STEP 3 GOES HERE
        ExplosionMaterial = new Material(Shader.Find("Legacy Shaders/Particles/Additive"));
        ExplosionMaterial.mainTexture = ExplosionTexture;

        FireMaterial = new Material(Shader.Find("Legacy Shaders/Particles/Additive"));
        FireMaterial.mainTexture = FireTexture;
    }

    static Texture2D LoadTexture(string path)
    {
        if (!File.Exists(path))
        {
            Main.Log($"[DVRT] Texture missing: {path}");
            return null;
        }

        string url = "file://" + path.Replace("\\", "/");

        WWW www = new WWW(url);

        while (!www.isDone) { }

        if (!string.IsNullOrEmpty(www.error))
        {
            Main.Log($"[DVRT] Failed loading texture: {www.error}");
            return null;
        }

        return www.texture;
    }
}