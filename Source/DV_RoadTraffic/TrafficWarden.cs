using UnityEngine;
using System.Collections;

namespace DV_RoadTraffic
{
    public class TrafficWarden : MonoBehaviour
    {
        public static TrafficWarden Instance;

        private bool enabledMode = false;

        public static int score = 0;

        AudioClip lockAndLoadClip;
        AudioClip lockAndLoadSayClip;

        enum WeaponMode
        {
            Raygun,
            Projectile
        }

        WeaponMode weaponMode = WeaponMode.Projectile;

        void Awake()
        {
            Instance = this;

            string basePath = System.IO.Path.Combine(
                Main.Mod.Path,
                "Assets",
                "Sounds",
                "Other",
                "Gun");

            lockAndLoadClip = DVRT_SoundLoader.LoadMP3(
                System.IO.Path.Combine(basePath, "lockAndLoad.mp3"));

            lockAndLoadSayClip = DVRT_SoundLoader.LoadMP3(
                System.IO.Path.Combine(basePath, "lockAndLoadSay.mp3"));
        }

        void Update()
        {
            if (!Main.IsGameLoaded)
            {
                if (enabledMode)
                {
                    enabledMode = false;

                    DVRT_RuntimeUI.Instance?.SetTrafficWardenVisible(false);

                    Main.Log("[DVRT] Traffic Warden disabled (game unloaded)");
                }

                return;
            }

            if (Input.GetKey(KeyCode.LeftAlt) && Input.GetKeyDown(KeyCode.G))
            {
                weaponMode = weaponMode == WeaponMode.Projectile
                    ? WeaponMode.Raygun
                    : WeaponMode.Projectile;

                DVRT_RuntimeUI.Instance?.SetWeaponText($"Weapon: {weaponMode}");

                Main.Log($"[DVRT] Weapon mode: {weaponMode}");
            }

            HandleToggle();
            HandleFire();
        }

        void HandleToggle()
        {
            if (Input.GetKey(KeyCode.LeftAlt) && Input.GetKeyDown(KeyCode.W))
            {
                enabledMode = !enabledMode;

                Main.Log($"[DVRT] Traffic Warden Mode: {enabledMode}");

                DVRT_RuntimeUI.Instance?.SetTrafficWardenVisible(enabledMode);

                if (enabledMode)
                {
                    DVRT_RuntimeUI.Instance?.SetWeaponText($"Weapon: {weaponMode}");
                    DVRT_RuntimeUI.Instance?.SetScore(score);

                    StartCoroutine(PlayLockAndLoadSequence());
                }
            }
        }

        IEnumerator PlayLockAndLoadSequence()
        {
            if (Camera.main == null)
                yield break;

            Vector3 pos = Camera.main.transform.position;

            if (lockAndLoadSayClip != null)
                AudioSource.PlayClipAtPoint(lockAndLoadSayClip, pos, 1f);

            yield return new WaitForSeconds(1.25f);

            if (lockAndLoadClip != null)
                AudioSource.PlayClipAtPoint(lockAndLoadClip, pos, 1f);
        }

        void HandleFire()
        {
            if (!enabledMode)
                return;

            if (!Input.GetMouseButtonDown(0))
                return;

            if (enabledMode && Input.GetMouseButtonDown(0))
            {
                if (weaponMode == WeaponMode.Projectile)
                    FireBullet();
                else
                    FireRay();
            }
        }

        void FireBullet()
        {
            if (Camera.main == null)
                return;

            Transform cam = Camera.main.transform;

            Vector3 spawnPos = cam.position + cam.forward * 1.5f;

            var clip = DVRT_SoundLibrary.GetRandomGunshot();

            if (clip != null)
            {
                AudioSource.PlayClipAtPoint(
                    clip,
                    Camera.main.transform.position,
                    0.9f);
            }

            GameObject bullet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bullet.name = "DVRT_TrafficBullet";

            var renderer = bullet.GetComponent<Renderer>();

            renderer.material = new Material(Shader.Find("Standard"));
            renderer.material.color = Color.yellow;

            renderer.material.EnableKeyword("_EMISSION");
            renderer.material.SetColor("_EmissionColor", Color.yellow * 2f);

            bullet.transform.position = spawnPos;
            bullet.transform.localScale = Vector3.one * 0.15f;

            var trail = bullet.AddComponent<TrailRenderer>();
            trail.time = 0.15f;
            trail.startWidth = 0.05f;
            trail.endWidth = 0f;
            trail.material = new Material(Shader.Find("Sprites/Default"));
            trail.startColor = Color.yellow;
            trail.endColor = new Color(1f, 1f, 0f, 0f);

            Destroy(bullet.GetComponent<Collider>()); 

            SphereCollider col = bullet.AddComponent<SphereCollider>();
            col.radius = 0.1f;

            Rigidbody rb = bullet.AddComponent<Rigidbody>();

            rb.mass = 0.1f;
            rb.useGravity = true;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            rb.velocity = cam.forward * 70f;

            bullet.AddComponent<TrafficBullet>();
        }

  

        public static void RegisterKill()
        {
            score++;

            DVRT_RuntimeUI.Instance?.SetScore(score);
        }

        void FireRay()
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

            if (Physics.Raycast(ray, out RaycastHit hit, 200f))
            {
                var vehicle = hit.collider.GetComponentInParent<TrafficVehicleController>();

                if (vehicle != null)
                {
                    vehicle.DestroyVehicle(Vector3.zero, true);

                    Main.Log("[DVRT] Traffic Warden destroyed vehicle");
                }
            }
        }
    }
}


namespace DV_RoadTraffic
{
    public class TrafficBullet : MonoBehaviour
    {
        private float life = 3f;

        void Update()
        {
            life -= Time.deltaTime;

            if (life <= 0f)
                Destroy(gameObject);
        }

        void OnCollisionEnter(Collision collision)
        {
            var vehicle = collision.collider.GetComponentInParent<TrafficVehicleController>();

            if (vehicle != null && !vehicle.destroyed)
            {
                vehicle.DestroyVehicle(Vector3.zero, true);

                TrafficWarden.RegisterKill();
            }

            Destroy(gameObject);
        }
    }
}