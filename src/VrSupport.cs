using UnityEngine;
using UnityEngine.XR;

namespace VividFrame
{
    /// <summary>XR state and tracked right-controller input used by the world-space VR interface.</summary>
    internal static class VrSupport
    {
        public static bool Active { get; private set; }
        public static Ray RightControllerRay { get; private set; }
        public static bool TriggerHeld { get; private set; }
        public static bool TriggerDown { get; private set; }
        public static float ScrollDelta { get; private set; }
        public static bool MenuToggleDown { get; private set; }

        private static bool _wasTriggerHeld;
        private static bool _wasMenuChordHeld;
        private static float _nextScroll;
        private static float _nextProbe;

        public static void Tick()
        {
            if (Time.unscaledTime < _nextProbe)
                return;
            _nextProbe = Time.unscaledTime + 0.02f;
            TriggerDown = false;
            MenuToggleDown = false;
            TriggerHeld = false;
            ScrollDelta = 0f;
            try
            {
                Camera camera = Camera.main;
                Active = XRSettings.enabled && XRSettings.isDeviceActive && camera != null && camera.stereoEnabled;
                if (!Active)
                {
                    _wasTriggerHeld = false;
                    _wasMenuChordHeld = false;
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
                    _wasTriggerHeld = false;
                    _wasMenuChordHeld = false;
                    return;
                }

                Quaternion originRotation = camera.transform.rotation * Quaternion.Inverse(headRotation);
                Vector3 originPosition = camera.transform.position - originRotation * headPosition;
                Vector3 rayOrigin = originPosition + originRotation * handPosition;
                Vector3 rayDirection = originRotation * handRotation * Vector3.forward;
                RightControllerRay = new Ray(rayOrigin, rayDirection);

                if (hand.TryGetFeatureValue(CommonUsages.triggerButton, out bool pressed))
                    TriggerHeld = pressed;
                TriggerDown = TriggerHeld && !_wasTriggerHeld;
                _wasTriggerHeld = TriggerHeld;

                if (hand.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 stick)
                    && Mathf.Abs(stick.y) > 0.55f && Time.unscaledTime >= _nextScroll)
                {
                    ScrollDelta = stick.y > 0f ? -1f : 1f;
                    _nextScroll = Time.unscaledTime + 0.16f;
                }

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
                TriggerHeld = false;
                TriggerDown = false;
                MenuToggleDown = false;
                ScrollDelta = 0f;
                _wasTriggerHeld = false;
                _wasMenuChordHeld = false;
            }
        }

        public static void ConsumeTriggerDown()
        {
            TriggerDown = false;
        }

        public static void ConsumeMenuToggleDown()
        {
            MenuToggleDown = false;
        }
    }
}
