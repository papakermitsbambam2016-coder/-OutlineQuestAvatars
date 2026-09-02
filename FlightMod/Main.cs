using System.Collections.Generic;
using MelonLoader;
using UnityEngine;
using UnityEngine.XR;

namespace OutlineQuestFlight
{
    public sealed class Main : MelonMod
    {
        private const float FlySpeed = 5f;
        private const float VerticalSpeed = 4f;
        private const float ToggleCooldown = 0.8f;
        private const string GlassesRootName = "OutlineFusionGlasses";

        private readonly Color[] tintColors =
        {
            new Color(0f, 0f, 0f, 0f),
            new Color(1f, 0.08f, 0.08f, 0.13f),
            new Color(0.08f, 0.3f, 1f, 0.13f),
            new Color(0.1f, 1f, 0.25f, 0.11f),
            new Color(0.65f, 0.2f, 1f, 0.12f),
            new Color(0.05f, 0.05f, 0.05f, 0.24f)
        };

        private readonly string[] tintNames =
        {
            "Normal", "Red", "Blue", "Green", "Purple", "Dark"
        };

        private InputDevice leftController;
        private InputDevice rightController;
        private Rigidbody playerBody;
        private GameObject tintOverlay;
        private Material tintMaterial;

        private bool flying;
        private bool flightComboWasDown;
        private bool tintComboWasDown;
        private bool originalGravity;
        private int tintIndex;
        private float nextToggleTime;
        private float nextBodySearch;
        private float nextGlassesScan;

        public override void OnInitializeMelon()
        {
            LoggerInstance.Msg("Outline Fusion Glasses loaded.");
            LoggerInstance.Msg("A+B+X+Y: flight. Press both thumbsticks: change tint.");
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            DisableFlight();
            playerBody = null;
            tintOverlay = null;
            tintMaterial = null;
            nextBodySearch = Time.unscaledTime + 2f;
            nextGlassesScan = Time.unscaledTime + 2f;
        }

        public override void OnUpdate()
        {
            RefreshControllers();
            UpdateFlightToggle();
            UpdateTintToggle();

            if (Time.unscaledTime >= nextGlassesScan)
            {
                nextGlassesScan = Time.unscaledTime + 2f;
                AddGlassesToOutlineAvatars();
                UpdateLocalTintVisibility();
            }
        }

        public override void OnFixedUpdate()
        {
            if (!flying)
                return;

            if (playerBody == null)
            {
                FindPlayerBody();
                if (playerBody == null)
                    return;
            }

            ReadAxis(leftController, CommonUsages.primary2DAxis, out Vector2 move);
            ReadAxis(rightController, CommonUsages.primary2DAxis, out Vector2 height);

            Transform view = Camera.main != null ? Camera.main.transform : null;
            Vector3 forward = view != null
                ? Vector3.ProjectOnPlane(view.forward, Vector3.up).normalized
                : Vector3.forward;
            Vector3 right = view != null
                ? Vector3.ProjectOnPlane(view.right, Vector3.up).normalized
                : Vector3.right;

            Vector3 velocity = (forward * move.y + right * move.x) * FlySpeed;
            velocity.y = height.y * VerticalSpeed;
            playerBody.useGravity = false;
            playerBody.velocity = velocity;
        }

        private void UpdateFlightToggle()
        {
            bool combo = Button(rightController, CommonUsages.primaryButton)
                      && Button(rightController, CommonUsages.secondaryButton)
                      && Button(leftController, CommonUsages.primaryButton)
                      && Button(leftController, CommonUsages.secondaryButton);

            if (combo && !flightComboWasDown && Time.unscaledTime >= nextToggleTime)
            {
                nextToggleTime = Time.unscaledTime + ToggleCooldown;
                if (flying) DisableFlight(); else EnableFlight();
            }
            flightComboWasDown = combo;
        }

        private void UpdateTintToggle()
        {
            bool combo = Button(leftController, CommonUsages.primary2DAxisClick)
                      && Button(rightController, CommonUsages.primary2DAxisClick);

            if (combo && !tintComboWasDown)
            {
                tintIndex = (tintIndex + 1) % tintColors.Length;
                ApplyTint();
                LoggerInstance.Msg("Glasses tint: " + tintNames[tintIndex]);
            }
            tintComboWasDown = combo;
        }

        private void AddGlassesToOutlineAvatars()
        {
            Transform[] transforms = Object.FindObjectsOfType<Transform>();
            foreach (Transform candidate in transforms)
            {
                if (candidate == null || !IsHeadName(candidate.name))
                    continue;

                string hierarchy = Hierarchy(candidate, 12);
                if (!hierarchy.ToLowerInvariant().Contains("outline"))
                    continue;

                if (candidate.Find(GlassesRootName) != null)
                    continue;

                bool whiteAvatar = hierarchy.Contains("OutlineNew (1)")
                                || hierarchy.ToLowerInvariant().Contains("outlinea");
                CreateGlasses(candidate, whiteAvatar ? Color.black : Color.white);
            }
        }

        private static bool IsHeadName(string name)
        {
            string n = name.ToLowerInvariant();
            return n == "head" || n.EndsWith(":head") || n.Contains("head bone");
        }

        private void CreateGlasses(Transform head, Color frameColor)
        {
            GameObject root = new GameObject(GlassesRootName);
            root.transform.SetParent(head, false);
            root.transform.localPosition = new Vector3(0f, 0.055f, 0.105f);
            root.transform.localRotation = Quaternion.identity;

            Material frame = MakeMaterial(frameColor, false);
            Material lens = MakeMaterial(new Color(0.12f, 0.2f, 0.28f, 0.38f), true);

            AddCube(root.transform, "LeftFrame", new Vector3(-0.035f, 0f, 0f), new Vector3(0.062f, 0.043f, 0.006f), frame);
            AddCube(root.transform, "RightFrame", new Vector3(0.035f, 0f, 0f), new Vector3(0.062f, 0.043f, 0.006f), frame);
            AddCube(root.transform, "Bridge", Vector3.zero, new Vector3(0.018f, 0.008f, 0.008f), frame);
            AddCube(root.transform, "LeftLens", new Vector3(-0.035f, 0f, -0.004f), new Vector3(0.052f, 0.033f, 0.003f), lens);
            AddCube(root.transform, "RightLens", new Vector3(0.035f, 0f, -0.004f), new Vector3(0.052f, 0.033f, 0.003f), lens);
            AddCube(root.transform, "LeftArm", new Vector3(-0.073f, 0f, -0.025f), new Vector3(0.006f, 0.007f, 0.065f), frame);
            AddCube(root.transform, "RightArm", new Vector3(0.073f, 0f, -0.025f), new Vector3(0.006f, 0.007f, 0.065f), frame);
        }

        private static void AddCube(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localRotation = Quaternion.identity;
            part.transform.localScale = scale;
            Renderer renderer = part.GetComponent<Renderer>();
            if (renderer != null) renderer.material = material;
            Collider collider = part.GetComponent<Collider>();
            if (collider != null) Object.Destroy(collider);
        }

        private static Material MakeMaterial(Color color, bool transparent)
        {
            Shader shader = Shader.Find(transparent ? "Unlit/Transparent" : "Unlit/Color");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Standard");
            Material material = new Material(shader);
            material.color = color;
            if (transparent) material.renderQueue = 3000;
            return material;
        }

        private void UpdateLocalTintVisibility()
        {
            Camera camera = Camera.main;
            if (camera == null)
                return;

            bool wearingOutline = false;
            Transform[] transforms = Object.FindObjectsOfType<Transform>();
            foreach (Transform t in transforms)
            {
                if (t == null || t.name != GlassesRootName)
                    continue;
                if (Vector3.Distance(t.position, camera.transform.position) < 0.45f)
                {
                    wearingOutline = true;
                    break;
                }
            }

            EnsureTintOverlay(camera.transform);
            if (tintOverlay != null)
                tintOverlay.SetActive(wearingOutline && tintIndex != 0);
        }

        private void EnsureTintOverlay(Transform camera)
        {
            if (tintOverlay != null)
                return;

            tintOverlay = GameObject.CreatePrimitive(PrimitiveType.Quad);
            tintOverlay.name = "OutlineGlassesTintOverlay";
            tintOverlay.transform.SetParent(camera, false);
            tintOverlay.transform.localPosition = new Vector3(0f, 0f, 0.31f);
            tintOverlay.transform.localRotation = Quaternion.identity;
            tintOverlay.transform.localScale = new Vector3(0.62f, 0.42f, 1f);
            Collider collider = tintOverlay.GetComponent<Collider>();
            if (collider != null) Object.Destroy(collider);
            tintMaterial = MakeMaterial(tintColors[tintIndex], true);
            Renderer renderer = tintOverlay.GetComponent<Renderer>();
            if (renderer != null) renderer.material = tintMaterial;
            tintOverlay.SetActive(false);
        }

        private void ApplyTint()
        {
            if (tintMaterial != null)
                tintMaterial.color = tintColors[tintIndex];
            if (tintOverlay != null)
                tintOverlay.SetActive(tintIndex != 0);
        }

        private void EnableFlight()
        {
            FindPlayerBody();
            if (playerBody == null)
            {
                LoggerInstance.Warning("Local player body not found yet.");
                return;
            }
            originalGravity = playerBody.useGravity;
            playerBody.useGravity = false;
            flying = true;
            LoggerInstance.Msg("Flight ON");
        }

        private void DisableFlight()
        {
            if (flying && playerBody != null)
            {
                playerBody.useGravity = originalGravity;
                playerBody.velocity = Vector3.zero;
            }
            if (flying) LoggerInstance.Msg("Flight OFF");
            flying = false;
        }

        private void FindPlayerBody()
        {
            if (Time.unscaledTime < nextBodySearch)
                return;
            nextBodySearch = Time.unscaledTime + 1f;

            Rigidbody[] bodies = Object.FindObjectsOfType<Rigidbody>();
            Rigidbody best = null;
            int bestScore = int.MinValue;
            foreach (Rigidbody body in bodies)
            {
                if (body == null || body.isKinematic)
                    continue;
                string n = body.name.ToLowerInvariant();
                string hierarchy = Hierarchy(body.transform, 8).ToLowerInvariant();
                int score = 0;
                if (n.Contains("pelvis")) score += 100;
                if (n.Contains("hip")) score += 80;
                if (hierarchy.Contains("physicsrig")) score += 60;
                if (hierarchy.Contains("local")) score += 50;
                if (hierarchy.Contains("player")) score += 25;
                if (hierarchy.Contains("remote")) score -= 200;
                if (hierarchy.Contains("npc")) score -= 200;
                if (score > bestScore) { bestScore = score; best = body; }
            }
            if (bestScore >= 50) playerBody = best;
        }

        private static string Hierarchy(Transform transform, int levels)
        {
            string result = transform.name;
            for (int i = 0; i < levels && transform.parent != null; i++)
            {
                transform = transform.parent;
                result += "/" + transform.name;
            }
            return result;
        }

        private void RefreshControllers()
        {
            if (!leftController.isValid)
                leftController = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            if (!rightController.isValid)
                rightController = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        }

        private static bool Button(InputDevice device, InputFeatureUsage<bool> usage)
        {
            return device.isValid && device.TryGetFeatureValue(usage, out bool value) && value;
        }

        private static void ReadAxis(InputDevice device, InputFeatureUsage<Vector2> usage, out Vector2 value)
        {
            if (!device.isValid || !device.TryGetFeatureValue(usage, out value))
                value = Vector2.zero;
        }
    }
}
