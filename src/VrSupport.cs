using UnityEngine;
using UnityEngine.XR;

namespace VividFrame
{
    /// <summary>XR state and tracked right-controller input used by the world-space VR interface.</summary>
    internal static class VrSupport
    {
        public static bool Active { get; private set; }
        public static bool HasPointer { get; private set; }
        public static Ray RightControllerRay { get; private set; }
        public static Quaternion RightControllerRotation { get; private set; } = Quaternion.identity;
        public static bool TriggerHeld { get; private set; }
        /// <summary>True only on the frame the trigger went down.</summary>
        public static bool TriggerDown { get; private set; }
        /// <summary>Right stick Y after the dead zone, -1 to 1 (up is positive).</summary>
        public static float StickY { get; private set; }
        public static bool MenuToggleDown { get; private set; }

        // Hysteresis on the analog trigger so a half pull cannot chatter between press and release.
        private const float PressAt = 0.55f;
        private const float ReleaseAt = 0.35f;
        private const float StickDeadZone = 0.2f;

        private static bool _wasMenuChordHeld;
        private static int _frame = -1;

        /// <summary>Call once per frame from OnUpdate. Edges last exactly one frame.</summary>
        public static void Tick()
        {
            int frame = Time.frameCount;
            if (frame == _frame)
                return;
            _frame = frame;

            TriggerDown = false;
            MenuToggleDown = false;
            StickY = 0f;
            HasPointer = false;
            try
            {
                Camera camera = Camera.main;
                Active = XRSettings.enabled && XRSettings.isDeviceActive && camera != null && camera.stereoEnabled;
                if (!Active)
                {
                    ResetHeld();
                    return;
                }

                InputDevice head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
                InputDevice hand = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
                if (!head.isValid || !hand.isValid
                    || !head.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 headPosition)
                    || !head.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion headRotation)
                    || !hand.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 handPosition)
                    || !hand.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion handRotation))
                {
                    ResetHeld();
                    return;
                }

                // Device poses are in tracking space; the camera tells us where tracking space sits in the world.
                Quaternion originRotation = camera.transform.rotation * Quaternion.Inverse(headRotation);
                Vector3 originPosition = camera.transform.position - originRotation * headPosition;
                Quaternion worldHand = originRotation * handRotation;
                RightControllerRotation = worldHand;
                RightControllerRay = new Ray(originPosition + originRotation * handPosition, worldHand * Vector3.forward);
                HasPointer = true;

                bool held = TriggerHeld;
                if (hand.TryGetFeatureValue(CommonUsages.trigger, out float pull))
                    held = held ? pull > ReleaseAt : pull > PressAt;
                else if (hand.TryGetFeatureValue(CommonUsages.triggerButton, out bool pressed))
                    held = pressed;
                TriggerDown = held && !TriggerHeld;
                TriggerHeld = held;

                if (hand.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 stick) && Mathf.Abs(stick.y) > StickDeadZone)
                    StickY = Mathf.Sign(stick.y) * (Mathf.Abs(stick.y) - StickDeadZone) / (1f - StickDeadZone);

                InputDevice left = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
                bool leftSecondary = left.isValid && left.TryGetFeatureValue(CommonUsages.secondaryButton, out bool lb) && lb;
                bool rightSecondary = hand.TryGetFeatureValue(CommonUsages.secondaryButton, out bool rb) && rb;
                bool chord = leftSecondary && rightSecondary;
                MenuToggleDown = chord && !_wasMenuChordHeld;
                _wasMenuChordHeld = chord;
            }
            catch
            {
                Active = false;
                ResetHeld();
            }
        }

        private static void ResetHeld()
        {
            HasPointer = false;
            TriggerHeld = false;
            TriggerDown = false;
            MenuToggleDown = false;
            StickY = 0f;
            _wasMenuChordHeld = false;
        }

        public static void ConsumeMenuToggleDown()
        {
            MenuToggleDown = false;
        }
    }
}
