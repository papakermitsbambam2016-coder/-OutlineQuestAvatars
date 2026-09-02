using MelonLoader;
using UnityEngine;
using UnityEngine.XR;

namespace OutlineQuestFlight
{
    public class Main : MelonMod
    {
        private InputDevice leftController;
        private InputDevice rightController;

        private Rigidbody playerBody;

        private bool flying;
        private bool comboWasPressed;
        private bool originalGravity;

        private float nextToggleTime;
        private float nextPlayerSearch;

        private const float FlySpeed = 5f;
        private const float VerticalSpeed = 4f;
        private const float ToggleCooldown = 0.8f;

        public override void OnInitializeMelon()
        {
            LoggerInstance.Msg(
                "Outline Quest Flight loaded. Press A+B+X+Y to toggle flight."
            );
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            DisableFlight();

            playerBody = null;
            nextPlayerSearch = Time.unscaledTime + 2f;
        }

        public override void OnUpdate()
        {
            UpdateControllers();

            bool aPressed = GetButton(
                rightController,
                CommonUsages.primaryButton
            );

            bool bPressed = GetButton(
                rightController,
                CommonUsages.secondaryButton
            );

            bool xPressed = GetButton(
                leftController,
                CommonUsages.primaryButton
            );

            bool yPressed = GetButton(
                leftController,
                CommonUsages.secondaryButton
            );

            bool comboPressed =
                aPressed &&
                bPressed &&
                xPressed &&
                yPressed;

            if (
                comboPressed &&
                !comboWasPressed &&
                Time.unscaledTime >= nextToggleTime
            )
            {
                nextToggleTime =
                    Time.unscaledTime + ToggleCooldown;

                if (flying)
                {
                    DisableFlight();
                }
                else
                {
                    EnableFlight();
                }
            }

            comboWasPressed = comboPressed;
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

            Vector2 movement;
            Vector2 altitude;

            GetJoystick(
                leftController,
                CommonUsages.primary2DAxis,
                out movement
            );

            GetJoystick(
                rightController,
                CommonUsages.primary2DAxis,
                out altitude
            );

            Transform headset =
                Camera.main != null
                    ? Camera.main.transform
                    : null;

            Vector3 forward = Vector3.forward;
            Vector3 right = Vector3.right;

            if (headset != null)
            {
                forward = Vector3.ProjectOnPlane(
                    headset.forward,
                    Vector3.up
                ).normalized;

                right = Vector3.ProjectOnPlane(
                    headset.right,
                    Vector3.up
                ).normalized;
            }

            Vector3 velocity =
                forward * movement.y * FlySpeed +
                right * movement.x * FlySpeed;

            velocity.y =
                altitude.y * VerticalSpeed;

            playerBody.useGravity = false;
            playerBody.velocity = velocity;
        }

        private void EnableFlight()
        {
            FindPlayerBody();

            if (playerBody == null)
            {
                LoggerInstance.Warning(
                    "Player body not found. Enter a level and try again."
                );

                return;
            }

            originalGravity = playerBody.useGravity;
            playerBody.useGravity = false;
            flying = true;

            LoggerInstance.Msg("Flight enabled");
        }

        private void DisableFlight()
        {
            if (flying && playerBody != null)
            {
                playerBody.useGravity = originalGravity;
                playerBody.velocity = Vector3.zero;
            }

            if (flying)
                LoggerInstance.Msg("Flight disabled");

            flying = false;
        }

        private void FindPlayerBody()
        {
            if (Time.unscaledTime < nextPlayerSearch)
                return;

            nextPlayerSearch =
                Time.unscaledTime + 1f;

            Rigidbody[] bodies =
                Object.FindObjectsOfType<Rigidbody>();

            Rigidbody bestBody = null;
            int bestScore = -999;

            foreach (Rigidbody body in bodies)
            {
                if (body == null || body.isKinematic)
                    continue;

                string objectName =
                    body.name.ToLowerInvariant();

                string hierarchy =
                    GetHierarchy(body.transform)
                    .ToLowerInvariant();

                int score = 0;

                if (objectName.Contains("pelvis"))
                    score += 100;

                if (objectName.Contains("hip"))
                    score += 80;

                if (hierarchy.Contains("physicsrig"))
                    score += 60;

                if (hierarchy.Contains("player"))
                    score += 30;

                if (hierarchy.Contains("npc"))
                    score -= 100;

                if (body.mass >= 5f && body.mass <= 100f)
                    score += 10;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestBody = body;
                }
            }

            if (bestScore >= 50)
                playerBody = bestBody;
        }

        private static string GetHierarchy(Transform current)
        {
            string result = current.name;

            for (
                int level = 0;
                level < 7 && current.parent != null;
                level++
            )
            {
                current = current.parent;
                result += "/" + current.name;
            }

            return result;
        }

        private void UpdateControllers()
        {
            if (!leftController.isValid)
            {
                leftController =
                    InputDevices.GetDeviceAtXRNode(
                        XRNode.LeftHand
                    );
            }

            if (!rightController.isValid)
            {
                rightController =
                    InputDevices.GetDeviceAtXRNode(
                        XRNode.RightHand
                    );
            }
        }

        private static bool GetButton(
            InputDevice controller,
            InputFeatureUsage<bool> button
        )
        {
            bool pressed;

            return
                controller.isValid &&
                controller.TryGetFeatureValue(
                    button,
                    out pressed
                ) &&
                pressed;
        }

        private static void GetJoystick(
            InputDevice controller,
            InputFeatureUsage<Vector2> joystick,
            out Vector2 value
        )
        {
            if (
                !controller.isValid ||
                !controller.TryGetFeatureValue(
                    joystick,
                    out value
                )
            )
            {
                value = Vector2.zero;
            }
        }
    }
}
