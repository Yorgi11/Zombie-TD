using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace QF_Tools.QF_Utilities
{
    #region Coroutines
    public static class QF_Coroutines
    {
        public static IEnumerator DelayRunFunction(float delay, Action function)
        {
            yield return new WaitForSeconds(delay);
            function?.Invoke();
        }
        public static IEnumerator DelayRunFunction(bool startState, bool endState, float delay, (Action, Action<bool>) functionState)
        {
            functionState.Item2?.Invoke(startState);
            yield return new WaitForSeconds(delay);
            functionState.Item1?.Invoke();
            functionState.Item2?.Invoke(endState);
        }
        public static IEnumerator DelayRunFunctionUntilTrue(Func<bool> condition, Action function)
        {
            while (!condition())
                yield return null;
            function?.Invoke();
        }
        public static IEnumerator DelayBoolChange(bool startState, bool endState, float delay, Action<bool> onUpdate)
        {
            onUpdate?.Invoke(startState);
            yield return new WaitForSeconds(delay);
            onUpdate?.Invoke(endState);
        }
        #region Lerp
        #region Reusable
        // ---------- Single Type ---------- //
        public static IEnumerator LerpOverTime<T>(
        T start, T end, float duration,
        Func<T, T, float, T> lerp,
        Action<T> onUpdate, bool unscaledTime = false)
        {
            if (duration <= 0f) { onUpdate?.Invoke(end); yield break; }

            float t = 0f;
            while (t < duration)
            {
                t += unscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                float a = Mathf.Clamp01(t / duration);
                onUpdate?.Invoke(lerp(start, end, a));
                yield return null;
            }
            onUpdate?.Invoke(end);
        } // ---------- //
        public static IEnumerator LerpPingPongOverTime<T>(
            T start, T end, float duration,
            Func<T, T, float, T> lerp,
            Action<T> onUpdate, bool unscaledTime = false)
        {
            float half = Mathf.Max(0f, duration * 0.5f);
            // forward
            yield return LerpOverTime(start, end, half, lerp, onUpdate, unscaledTime);
            // back
            yield return LerpOverTime(end, start, half, lerp, onUpdate, unscaledTime);
            onUpdate?.Invoke(start);
        } // ---------- //
        /// <summary>
        /// Moves along a sequence at a constant "speed" measured in your units.
        /// Provide a distance function consistent with your type.
        /// </summary>
        public static IEnumerator LerpOverSequence<T>(
            IReadOnlyList<T> sequence, float speed,
            Func<T, T, float, T> lerp, Func<T, T, float> distance,
            Action<T> onUpdate, bool reverse = false, bool unscaledTime = false)
        {
            if (sequence == null || sequence.Count < 2 || speed <= 0f) yield break;

            int count = sequence.Count;
            int startIndex = reverse ? count - 1 : 0;
            int endIndex = reverse ? 0 : count - 1;
            int step = reverse ? -1 : 1;

            for (int i = startIndex; i != endIndex; i += step)
            {
                T a = sequence[i];
                T b = sequence[i + step];

                float segDist = Mathf.Max(0f, distance(a, b));
                float segDur = segDist / speed;
                if (segDur <= 0f)
                {
                    onUpdate?.Invoke(b);
                    continue;
                }
                float t = 0f;
                while (t < segDur)
                {
                    t += unscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                    float alpha = Mathf.Clamp01(t / segDur);
                    onUpdate?.Invoke(lerp(a, b, alpha));
                    yield return null;
                }
                onUpdate?.Invoke(b);
            }
            onUpdate?.Invoke(sequence[endIndex]);
        } // ---------- //
        // ---------- Dual Type ---------- //
        /// <summary>
        /// Lerp two values (T1, T2) in parallel over a fixed duration.
        /// Supply the lerp functions for each type.
        /// </summary>
        public static IEnumerator LerpOverTime<T1, T2>(
            T1 startT1, T1 endT1, T2 startT2, T2 endT2, float duration,
            Func<T1, T1, float, T1> lerpT1, Func<T2, T2, float, T2> lerpT2,
            Action<(T1, T2)> onUpdate, bool unscaledTime = false)
        {
            if (lerpT1 == null) throw new ArgumentNullException(nameof(lerpT1));
            if (lerpT2 == null) throw new ArgumentNullException(nameof(lerpT2));
            if (onUpdate == null) yield break;

            if (duration <= 0f)
            {
                onUpdate((endT1, endT2));
                yield break;
            }
            float t = 0f;
            while (t < duration)
            {
                t += unscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                float a = Mathf.Clamp01(t / duration);

                var v1 = lerpT1(startT1, endT1, a);
                var v2 = lerpT2(startT2, endT2, a);
                onUpdate((v1, v2));

                yield return null;
            }
            onUpdate((endT1, endT2));
        } // ---------- //
        public static IEnumerator LerpPingPongOverTime<T1, T2>(
            T1 startT1, T1 endT1, T2 startT2, T2 endT2, float duration,
            Func<T1, T1, float, T1> lerpT1, Func<T2, T2, float, T2> lerpT2,
            Action<(T1, T2)> onUpdate, bool unscaledTime = false)
        {
            float half = Mathf.Max(0f, duration * 0.5f);
            // forward
            yield return LerpOverTime(startT1, endT1, startT2, endT2, half, lerpT1, lerpT2, onUpdate, unscaledTime);
            // back
            yield return LerpOverTime(endT1, startT1, endT2, startT2, half, lerpT1, lerpT2, onUpdate, unscaledTime);
        } // ---------- //
        public static IEnumerator LerpOverSequence<T1, T2>(
            IReadOnlyList<T1> sequenceT1, IReadOnlyList<T2> sequenceT2, float speed,
            Func<T1, T1, float, T1> lerpT1, Func<T2, T2, float, T2> lerpT2,
            Func<T1, T1, float> distanceT1, Func<T2, T2, float> distanceT2,
            Action<(T1, T2)> onUpdate, bool reverse = false, bool unscaledTime = false)
        {
            if (sequenceT1 == null || sequenceT2 == null || sequenceT1.Count != sequenceT2.Count || sequenceT1.Count < 2 || speed <= 0f)
                yield break;

            int n = sequenceT1.Count;
            int step = reverse ? -1 : 1;
            int i = reverse ? n - 1 : 0;
            int end = reverse ? 0 : n - 1;

            for (; i != end; i += step)
            {
                int j = i + step;
                var a1 = sequenceT1[i];
                var b1 = sequenceT1[j];
                var a2 = sequenceT2[i];
                var b2 = sequenceT2[j];

                float d1 = Mathf.Max(0f, distanceT1(a1, b1));
                float d2 = Mathf.Max(0f, distanceT2(a2, b2));

                // Duration governed by the slower/longer of the two tracks
                float dur = Mathf.Max(d1, d2) / speed;

                if (dur <= 0f)
                {
                    onUpdate?.Invoke((b1, b2));
                    continue;
                }

                float t = 0f;
                while (t < dur)
                {
                    t += unscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                    float a = Mathf.Clamp01(t / dur);

                    var v1 = lerpT1(a1, b1, a);
                    var v2 = lerpT2(a2, b2, a);
                    onUpdate?.Invoke((v1, v2));
                    yield return null;
                }
                onUpdate?.Invoke((b1, b2));
            }
            int finalIdx = end;
            onUpdate?.Invoke((sequenceT1[finalIdx], sequenceT2[finalIdx]));
        } // ---------- //
        #endregion
        #region Adapters
        // int
        public static readonly Func<int, int, float, int> LerpInt = (a, b, t) => (int)Mathf.Lerp(a, b, t);
        public static readonly Func<int, int, float> DistInt = (a, b) => Math.Abs(a - b);

        // float
        public static readonly Func<float, float, float, float> LerpFloat = (a, b, t) => Mathf.Lerp(a, b, t);
        public static readonly Func<float, float, float> DistFloat = (a, b) => Mathf.Abs(a - b);

        // Vector2
        public static readonly Func<Vector2, Vector2, float, Vector2> LerpVector2 = (a, b, t) => Vector2.Lerp(a, b, t);
        public static readonly Func<Vector2, Vector2, float> DistVector2 = (a, b) => Vector2.Distance(a, b);

        // Vector3
        public static readonly Func<Vector3, Vector3, float, Vector3> LerpVector3 = (a, b, t) => Vector3.Lerp(a, b, t);
        public static readonly Func<Vector3, Vector3, float, Vector3> SlerpVector3 = (a, b, t) => Vector3.Slerp(a, b, t);
        public static readonly Func<Vector3, Vector3, float> DistVector3 = (a, b) => Vector3.Distance(a, b);

        // Quaternion
        public static readonly Func<Quaternion, Quaternion, float, Quaternion> LerpQuaternion = (a, b, t) => Quaternion.Lerp(a, b, t);
        public static readonly Func<Quaternion, Quaternion, float, Quaternion> SlerpQuaternion = (a, b, t) => Quaternion.Slerp(a, b, t);
        public static readonly Func<Quaternion, Quaternion, float> DistQuaternion = (a, b) => Quaternion.Angle(a, b);

        // Color
        public static readonly Func<Color, Color, float, Color> LerpColor = (a, b, t) => Color.Lerp(a, b, t);
        public static readonly Func<Color, Color, float> DistColor = (a, b) => Vector4.Distance(a, b);
        #endregion
        #endregion
    }
    #endregion
    public static class QF_Functions
    {
        public static float MapRangeto01(float value, float min, float max)
        {
            return (value - min) / (max - min);

        }
        public static Quaternion[] Vector3ArrayToQuaternionArray(Vector3[] vectors)
        {
            if (vectors == null) return null;
            Quaternion[] quaternions = new Quaternion[vectors.Length];
            for (int i = 0; i < vectors.Length; i++)
                quaternions[i] = Quaternion.Euler(vectors[i]);
            return quaternions;
        }
        public static bool TryGetComponentInParent<T>(this Component start, out T component) where T : Component
        {
            Transform t = start.transform;
            while (t != null)
            {
                if (t.TryGetComponent(out component)) return true;
                t = t.parent;
            }
            component = null;
            return false;
        }
        public static bool TryGetComponentInParent<T>(this GameObject start, out T component) where T : Component
        {
            Transform t = start.transform;
            while (t != null)
            {
                if (t.TryGetComponent(out component)) return true;
                t = t.parent;
            }
            component = null;
            return false;
        }
        public static bool TryGetComponentInChildren<T>(this GameObject start, out T component) where T : Component
        {
            Transform root = start.transform;
            if (root.TryGetComponent(out component)) return true;
            Stack<Transform> stack = new();
            for (int i = 0; i < root.childCount; i++)
                stack.Push(root.GetChild(i));
            while (stack.Count > 0)
            {
                Transform t = stack.Pop();
                if (t.TryGetComponent(out component)) return true;
                for (int i = 0; i < t.childCount; i++)
                    stack.Push(t.GetChild(i));
            }
            component = null;
            return false;
        }
        public static void ClearArray<T>(this T[] array)
        {
            if (array == null) return;
            Array.Clear(array, 0, array.Length);
        }
    }
    public class QF_Input
    {
        public KeyCode _key;
        public bool Up => Input.GetKeyUp(_key);
        public bool Down => Input.GetKeyDown(_key);
        public bool Hold => Input.GetKey(_key);
    }
}