using System.Collections.Generic;
using UnityEngine;

namespace VividFrame
{
    /// <summary>
    /// Per-control animation state. Dictionaries grow once per id.
    /// Integration is limited to once per Unity frame so OnGUI's Layout/Repaint/input
    /// events cannot multiply the cost or the motion.
    /// </summary>
    internal sealed class Anim
    {
        public bool Enabled = true;
        public float Speed = 1f;

        private readonly Dictionary<int, float> _value = new Dictionary<int, float>(128);
        private readonly Dictionary<int, float> _vel = new Dictionary<int, float>(128);
        private readonly Dictionary<int, int> _step = new Dictionary<int, int>(128);
        private int _dtFrame = -1;
        private float _dt;

        public float Dt
        {
            get
            {
                int frame = Time.frameCount;
                if (_dtFrame != frame)
                {
                    _dtFrame = frame;
                    float speed = Mathf.Clamp(Speed, 0.25f, 3f);
                    _dt = Mathf.Max(0f, Time.unscaledDeltaTime) * speed;
                }
                return _dt;
            }
        }

        public float Get(int id, float fallback)
        {
            return _value.TryGetValue(id, out float value) ? value : fallback;
        }

        public void Set(int id, float value)
        {
            _value[id] = value;
            _vel[id] = 0f;
        }

        public bool Has(int id)
        {
            return _value.ContainsKey(id);
        }

        public float Move(int id, float target, float duration, float defaultIfNew)
        {
            if (!Enabled || duration <= 0.0001f)
            {
                _value[id] = target;
                _vel[id] = 0f;
                return target;
            }

            if (AlreadyStepped(id))
                return Get(id, defaultIfNew);

            if (!_value.TryGetValue(id, out float current))
                current = defaultIfNew;

            current = Mathf.MoveTowards(current, target, Dt / duration);
            _value[id] = current;
            return current;
        }

        public float Spring(int id, float target, float defaultIfNew, float omega = 16f, float zeta = 0.82f)
        {
            if (!Enabled)
            {
                _value[id] = target;
                _vel[id] = 0f;
                return target;
            }

            if (AlreadyStepped(id))
                return Get(id, defaultIfNew);

            if (!_value.TryGetValue(id, out float x))
                x = defaultIfNew;
            if (!_vel.TryGetValue(id, out float v))
                v = 0f;

            float dt = Mathf.Min(Dt, 0.05f);
            float accel = omega * omega * (target - x) - 2f * zeta * omega * v;
            v += accel * dt;
            x += v * dt;
            _value[id] = x;
            _vel[id] = v;
            return x;
        }

        public void Fire(int id)
        {
            _value[id] = 1f;
        }

        public float Decay(int id, float duration)
        {
            if (!_value.TryGetValue(id, out float value) || value <= 0f)
            {
                _value[id] = 0f;
                return 0f;
            }
            if (!Enabled)
            {
                _value[id] = 0f;
                return 0f;
            }
            if (AlreadyStepped(id))
                return value;
            value = Mathf.Max(0f, value - Dt / Mathf.Max(0.01f, duration));
            _value[id] = value;
            return value;
        }

        private bool AlreadyStepped(int id)
        {
            int frame = Time.frameCount;
            if (_step.TryGetValue(id, out int last) && last == frame)
                return true;
            _step[id] = frame;
            return false;
        }

        public static float EaseOutCubic(float t)
        {
            t = Mathf.Clamp01(t);
            float inv = 1f - t;
            return 1f - inv * inv * inv;
        }

        public static float EaseOutBack(float t)
        {
            t = Mathf.Clamp01(t);
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float p = t - 1f;
            return 1f + c3 * p * p * p + c1 * p * p;
        }
    }
}
