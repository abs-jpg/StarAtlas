using UnityEngine;
using UnityEngine.Rendering;

namespace AZ.Exhibition
{
    [DisallowMultipleComponent]
    public sealed class ExhibitionStarField : MonoBehaviour
    {
        private const float LongLifetime = 999999f;

        [Header("Target")]
        [SerializeField] private Transform followTarget;
        [SerializeField] private bool autoFindMainCamera = true;

        [Header("Camera Background")]
        [SerializeField] private bool forceSolidBlackCameraBackground = true;
        [SerializeField] private Color cameraBackgroundColor = Color.black;

        [Header("Far Star Layer")]
        [SerializeField, Min(0)] private int farStarCount = 1400;
        [SerializeField, Min(0.1f)] private float farRadiusMin = 22f;
        [SerializeField, Min(0.1f)] private float farRadiusMax = 60f;
        [SerializeField, Min(0.0001f)] private float farSizeMin = 0.012f;
        [SerializeField, Min(0.0001f)] private float farSizeMax = 0.05f;
        [SerializeField, Min(0f)] private float farBrightnessMin = 0.35f;
        [SerializeField, Min(0f)] private float farBrightnessMax = 1f;

        [Header("Near Star Layer")]
        [SerializeField, Min(0)] private int nearStarCount = 160;
        [SerializeField, Min(0.1f)] private float nearRadiusMin = 5f;
        [SerializeField, Min(0.1f)] private float nearRadiusMax = 16f;
        [SerializeField, Min(0.0001f)] private float nearSizeMin = 0.018f;
        [SerializeField, Min(0.0001f)] private float nearSizeMax = 0.075f;
        [SerializeField, Min(0f)] private float nearBrightnessMin = 0.65f;
        [SerializeField, Min(0f)] private float nearBrightnessMax = 1.25f;

        [Header("Look")]
        [SerializeField] private Color coolTint = new Color(0.78f, 0.88f, 1f, 1f);
        [SerializeField, Range(0f, 1f)] private float warmStarChance = 0.18f;
        [SerializeField] private Color warmTint = new Color(1f, 0.78f, 0.48f, 1f);
        [SerializeField] private Material starMaterial;
        [SerializeField] private bool twinkle = true;
        [SerializeField, Range(0f, 1f)] private float twinkleStrength = 0.22f;
        [SerializeField, Min(0f)] private float twinkleSpeed = 1.2f;
        [SerializeField] private int randomSeed = 31415;

        private readonly StarLayer farLayer = new StarLayer();
        private readonly StarLayer nearLayer = new StarLayer();

        private Material runtimeMaterial;
        private Texture2D runtimeStarTexture;
        private Vector3 currentCenter;

        private void OnEnable()
        {
            Rebuild();
        }

        private void OnDestroy()
        {
            DestroyRuntimeObject(runtimeMaterial);
            DestroyRuntimeObject(runtimeStarTexture);
        }

        private void Update()
        {
            if (twinkle)
            {
                float time = Time.time * twinkleSpeed;
                farLayer.UpdateTwinkle(time, twinkleStrength);
                nearLayer.UpdateTwinkle(time, twinkleStrength);
            }
        }

        [ContextMenu("Rebuild Star Field")]
        public void Rebuild()
        {
            ResolveTarget();
            ApplyCameraBackground();
            currentCenter = GetCenter();
            Random.State previousState = Random.state;
            Random.InitState(randomSeed);

            BuildLayer(
                farLayer,
                "Far Stars",
                farStarCount,
                farRadiusMin,
                farRadiusMax,
                farSizeMin,
                farSizeMax,
                farBrightnessMin,
                farBrightnessMax);

            BuildLayer(
                nearLayer,
                "Near Stars",
                nearStarCount,
                nearRadiusMin,
                nearRadiusMax,
                nearSizeMin,
                nearSizeMax,
                nearBrightnessMin,
                nearBrightnessMax);

            Random.state = previousState;
        }

        private void ResolveTarget()
        {
            if (followTarget != null || !autoFindMainCamera)
            {
                return;
            }

            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                followTarget = mainCamera.transform;
                return;
            }

            Camera[] cameras = Camera.allCameras;
            for (int i = 0; i < cameras.Length; i++)
            {
                if (cameras[i] != null && cameras[i].isActiveAndEnabled)
                {
                    followTarget = cameras[i].transform;
                    return;
                }
            }
        }

        private Vector3 GetCenter()
        {
            return followTarget != null ? followTarget.position : transform.position;
        }

        private void ApplyCameraBackground()
        {
            if (!forceSolidBlackCameraBackground)
            {
                return;
            }

            Camera camera = followTarget != null ? followTarget.GetComponent<Camera>() : null;
            if (camera == null)
            {
                camera = Camera.main;
            }

            if (camera == null)
            {
                return;
            }

            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = cameraBackgroundColor;
        }

        private void BuildLayer(
            StarLayer layer,
            string layerName,
            int count,
            float radiusMin,
            float radiusMax,
            float sizeMin,
            float sizeMax,
            float brightnessMin,
            float brightnessMax)
        {
            layer.Ensure(transform, layerName, GetStarMaterial());

            count = Mathf.Max(0, count);
            radiusMax = Mathf.Max(radiusMin, radiusMax);
            sizeMax = Mathf.Max(sizeMin, sizeMax);
            brightnessMax = Mathf.Max(brightnessMin, brightnessMax);

            layer.Resize(count);

            for (int i = 0; i < count; i++)
            {
                Vector3 direction = Random.onUnitSphere;
                float radius = Random.Range(radiusMin, radiusMax);
                float size = Random.Range(sizeMin, sizeMax);
                float brightness = Random.Range(brightnessMin, brightnessMax);
                Color tint = Random.value < warmStarChance ? warmTint : coolTint;
                Color color = tint * brightness;
                color.a = Mathf.Clamp01(brightness);

                ParticleSystem.Particle particle = new ParticleSystem.Particle
                {
                    position = currentCenter + direction * radius,
                    startLifetime = LongLifetime,
                    remainingLifetime = LongLifetime,
                    startSize = size,
                    startColor = color,
                    rotation3D = Vector3.zero,
                    velocity = Vector3.zero
                };

                layer.Particles[i] = particle;
                layer.BaseColors[i] = color;
                layer.TwinklePhases[i] = Random.Range(0f, Mathf.PI * 2f);
                layer.TwinkleAmounts[i] = Random.Range(0.35f, 1f);
            }

            layer.Apply();
        }

        private Material GetStarMaterial()
        {
            if (starMaterial != null)
            {
                return starMaterial;
            }

            if (runtimeMaterial != null)
            {
                return runtimeMaterial;
            }

            Shader shader = Shader.Find("Legacy Shaders/Particles/Additive");
            if (shader == null)
            {
                shader = Shader.Find("Particles/Standard Unlit");
            }

            if (shader == null)
            {
                shader = Shader.Find("Unlit/Transparent");
            }

            runtimeMaterial = new Material(shader)
            {
                name = "Runtime Procedural Star Material",
                mainTexture = GetStarTexture()
            };

            if (runtimeMaterial.HasProperty("_SrcBlend"))
            {
                runtimeMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            }

            if (runtimeMaterial.HasProperty("_DstBlend"))
            {
                runtimeMaterial.SetInt("_DstBlend", (int)BlendMode.One);
            }

            if (runtimeMaterial.HasProperty("_ZWrite"))
            {
                runtimeMaterial.SetInt("_ZWrite", 0);
            }

            runtimeMaterial.renderQueue = (int)RenderQueue.Transparent;
            return runtimeMaterial;
        }

        private Texture2D GetStarTexture()
        {
            if (runtimeStarTexture != null)
            {
                return runtimeStarTexture;
            }

            const int size = 32;
            runtimeStarTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Runtime Soft Star Texture",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float maxDistance = center.x;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center) / maxDistance;
                    float alpha = Mathf.Clamp01(1f - distance);
                    alpha = Mathf.Pow(alpha, 2.6f);
                    runtimeStarTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            runtimeStarTexture.Apply(false, true);
            return runtimeStarTexture;
        }

        private static void DestroyRuntimeObject(Object target)
        {
            if (target == null)
            {
                return;
            }

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                DestroyImmediate(target);
                return;
            }
#endif

            Destroy(target);
        }

        private sealed class StarLayer
        {
            public ParticleSystem.Particle[] Particles { get; private set; } =
                new ParticleSystem.Particle[0];

            public Color[] BaseColors { get; private set; } = new Color[0];
            public float[] TwinklePhases { get; private set; } = new float[0];
            public float[] TwinkleAmounts { get; private set; } = new float[0];

            private ParticleSystem particleSystem;

            public void Ensure(Transform parent, string name, Material material)
            {
                if (particleSystem == null)
                {
                    Transform existing = parent.Find(name);
                    GameObject layerObject = existing != null
                        ? existing.gameObject
                        : new GameObject(name);

                    layerObject.transform.SetParent(parent, false);
                    layerObject.transform.localPosition = Vector3.zero;
                    layerObject.transform.localRotation = Quaternion.identity;
                    layerObject.transform.localScale = Vector3.one;
                    particleSystem = layerObject.GetComponent<ParticleSystem>();

                    if (particleSystem == null)
                    {
                        particleSystem = layerObject.AddComponent<ParticleSystem>();
                    }
                }

                ParticleSystem.MainModule main = particleSystem.main;
                if (particleSystem.isPlaying || particleSystem.particleCount > 0)
                {
                    particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }

                main.duration = 1f;
                main.loop = false;
                main.playOnAwake = false;
                main.startSpeed = 0f;
                main.startLifetime = LongLifetime;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                main.maxParticles = Mathf.Max(1, Particles.Length);

                ParticleSystem.EmissionModule emission = particleSystem.emission;
                emission.enabled = false;

                ParticleSystem.ShapeModule shape = particleSystem.shape;
                shape.enabled = false;

                ParticleSystemRenderer renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.material = material;
                renderer.sortMode = ParticleSystemSortMode.None;
            }

            public void Resize(int count)
            {
                if (Particles.Length == count)
                {
                    return;
                }

                Particles = new ParticleSystem.Particle[count];
                BaseColors = new Color[count];
                TwinklePhases = new float[count];
                TwinkleAmounts = new float[count];
            }

            public void Apply()
            {
                if (particleSystem == null)
                {
                    return;
                }

                ParticleSystem.MainModule main = particleSystem.main;
                main.maxParticles = Mathf.Max(1, Particles.Length);
                particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                particleSystem.SetParticles(Particles, Particles.Length);
                particleSystem.Play(true);
            }

            public void UpdateTwinkle(float time, float strength)
            {
                if (particleSystem == null || Particles.Length == 0)
                {
                    return;
                }

                strength = Mathf.Clamp01(strength);
                for (int i = 0; i < Particles.Length; i++)
                {
                    float pulse = 1f + Mathf.Sin(time + TwinklePhases[i]) * strength * TwinkleAmounts[i];
                    Color color = BaseColors[i] * pulse;
                    color.a = BaseColors[i].a;
                    Particles[i].startColor = color;
                }

                particleSystem.SetParticles(Particles, Particles.Length);
            }
        }
    }
}
