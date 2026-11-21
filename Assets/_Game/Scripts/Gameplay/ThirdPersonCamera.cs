using UnityEngine;

namespace BobosJourney
{
    /// <summary>Orbit camera with wall avoidance and a lazy vertical follow, tuned for vertical platforming.</summary>
    public class ThirdPersonCamera : MonoBehaviour
    {
        public Transform target;
        public float distance = 9f;
        public float minDistance = 3.5f;
        public float maxDistance = 16f;
        public float focusHeight = 1.1f;
        public float yaw;
        public float pitch = 20f;
        public float minPitch = -20f;
        public float maxPitch = 75f;
        public float collisionRadius = 0.35f;
        public bool inputEnabled = true;

        Vector3 focus;
        float currentDistance;
        float shake;

        void Start()
        {
            if (target) focus = target.position + Vector3.up * focusHeight;
            currentDistance = distance;
        }

        public void SnapBehind(Vector3 forward)
        {
            forward.y = 0;
            if (forward.sqrMagnitude > 0.01f) yaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
            if (target) focus = target.position + Vector3.up * focusHeight;
        }

        public void Shake(float amount) => shake = Mathf.Max(shake, amount);

        void LateUpdate()
        {
            if (!target) return;
            float dt = Time.unscaledDeltaTime;
            if (inputEnabled && Time.timeScale > 0)
            {
                var look = GameInput.Look;
                yaw += look.x;
                pitch = Mathf.Clamp(pitch - look.y, minPitch, maxPitch);
                distance = Mathf.Clamp(distance + GameInput.Zoom * 1.2f, minDistance, maxDistance);
            }

            // horizontal follow is tight, vertical is lazy so jumps don't jerk the frame
            Vector3 desiredFocus = target.position + Vector3.up * focusHeight;
            float focusX = Mathf.Lerp(focus.x, desiredFocus.x, 1f - Mathf.Exp(-18f * dt));
            float focusZ = Mathf.Lerp(focus.z, desiredFocus.z, 1f - Mathf.Exp(-18f * dt));
            float focusY = Mathf.Lerp(focus.y, desiredFocus.y, 1f - Mathf.Exp(-(desiredFocus.y < focus.y - 4f ? 12f : 6f) * dt));
            focus = new Vector3(focusX, focusY, focusZ);

            Quaternion rot = Quaternion.Euler(pitch, yaw, 0);
            Vector3 back = rot * Vector3.back;
            float targetDistance = distance;
            if (Physics.SphereCast(focus, collisionRadius, back, out var hit, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                targetDistance = Mathf.Max(0.6f, hit.distance - 0.1f);
            // pull in fast, ease back out
            currentDistance = targetDistance < currentDistance ? targetDistance : Mathf.Lerp(currentDistance, targetDistance, 1f - Mathf.Exp(-4f * dt));

            Vector3 pos = focus + back * currentDistance;
            if (shake > 0)
            {
                pos += Random.insideUnitSphere * shake * 0.25f;
                shake = Mathf.MoveTowards(shake, 0, dt * 3f);
            }
            transform.SetPositionAndRotation(pos, rot);
        }
    }
}
