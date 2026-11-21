using System.Collections;
using UnityEngine;

namespace BobosJourney
{
    /// <summary>Anything Bobo can stand on that moves; he inherits its velocity.</summary>
    public interface IPlatformMotion
    {
        Vector3 VelocityAt(Vector3 worldPoint);
    }

    /// <summary>Ping-pongs smoothly between two world points.</summary>
    [RequireComponent(typeof(Rigidbody))]
    public class MovingPlatform : MonoBehaviour, IPlatformMotion
    {
        public Vector3 pointA, pointB;
        public float period = 4f;
        public float phase;
        Rigidbody rb;
        Vector3 velocity;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
        }

        void FixedUpdate()
        {
            float t = Time.fixedTime / period + phase;
            float blend = 0.5f - 0.5f * Mathf.Cos(t * Mathf.PI * 2f);
            Vector3 p = Vector3.Lerp(pointA, pointB, blend);
            velocity = (p - rb.position) / Time.fixedDeltaTime;
            rb.MovePosition(p);
        }

        public Vector3 VelocityAt(Vector3 worldPoint) => velocity;
    }

    /// <summary>Rotates around its up axis, carrying Bobo with it.</summary>
    [RequireComponent(typeof(Rigidbody))]
    public class SpinningPlatform : MonoBehaviour, IPlatformMotion
    {
        public float degreesPerSecond = 40f;
        Rigidbody rb;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
        }

        void FixedUpdate() => rb.MoveRotation(rb.rotation * Quaternion.Euler(0, degreesPerSecond * Time.fixedDeltaTime, 0));

        public Vector3 VelocityAt(Vector3 worldPoint)
        {
            Vector3 angularVel = Vector3.up * degreesPerSecond * Mathf.Deg2Rad;
            Vector3 offset = worldPoint - rb.position;
            offset.y = 0;
            return Vector3.Cross(angularVel, offset);
        }
    }

    /// <summary>A rotating bar that shoves Bobo off whatever he is standing on.</summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Sweeper : MonoBehaviour
    {
        public float degreesPerSecond = 90f;
        public float shove = 9f;
        Rigidbody rb;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
        }

        void FixedUpdate() => rb.MoveRotation(rb.rotation * Quaternion.Euler(0, degreesPerSecond * Time.fixedDeltaTime, 0));

        void OnCollisionStay(Collision collision)
        {
            var bobo = collision.collider.GetComponentInParent<BoboController>();
            if (bobo == null) return;
            Vector3 offset = bobo.transform.position - rb.position;
            offset.y = 0;
            Vector3 tangent = Vector3.Cross(Vector3.up * Mathf.Sign(degreesPerSecond), offset).normalized;
            bobo.Knock(tangent * shove + offset.normalized * shove * 0.35f + Vector3.up * 4f);
        }
    }

    /// <summary>Jelly pad that launches Bobo high into the air.</summary>
    public class BouncePad : MonoBehaviour
    {
        public float launchHeight = 11f;
        public Transform visual;
        float squash;
        Vector3 baseScale;

        void Start() { if (visual) baseScale = visual.localScale; }

        public void OnBounce()
        {
            squash = 1f;
            Sfx.Play(Sfx.Id.Bounce, 0.9f, Random.Range(0.95f, 1.08f));
        }

        void Update()
        {
            if (visual == null) return;
            squash = Mathf.MoveTowards(squash, 0, Time.deltaTime * 3f);
            float wobble = Mathf.Sin(squash * 18f) * squash * 0.35f;
            visual.localScale = new Vector3(baseScale.x * (1 + wobble * 0.5f), baseScale.y * (1 - wobble), baseScale.z * (1 + wobble * 0.5f));
        }
    }

    /// <summary>Shakes when stood on, drops away, then grows back.</summary>
    public class CrumblePlatform : MonoBehaviour
    {
        public Transform visual;
        public Collider solid;
        public float shakeTime = 0.6f;
        public float respawnTime = 3.2f;
        bool busy;
        Vector3 basePos;
        Vector3 baseScale;

        void Start()
        {
            basePos = visual.localPosition;
            baseScale = visual.localScale;
        }

        public void Touch()
        {
            if (!busy) StartCoroutine(Crumble());
        }

        IEnumerator Crumble()
        {
            busy = true;
            Sfx.Play(Sfx.Id.Crumble, 0.6f, Random.Range(0.9f, 1.1f));
            float t = 0;
            while (t < shakeTime)
            {
                t += Time.deltaTime;
                float progress = t / shakeTime;
                visual.localPosition = basePos + new Vector3(Random.Range(-1f, 1f), Random.Range(-0.5f, 0.5f), Random.Range(-1f, 1f)) * 0.08f * (0.4f + progress);
                yield return null;
            }
            solid.enabled = false;
            t = 0;
            while (t < 0.6f)
            {
                t += Time.deltaTime;
                float progress = t / 0.6f;
                visual.localPosition = basePos + Vector3.down * (progress * progress * 6f);
                visual.localScale = baseScale * (1f - progress * 0.7f);
                yield return null;
            }
            visual.gameObject.SetActive(false);
            yield return new WaitForSeconds(respawnTime);
            visual.gameObject.SetActive(true);
            visual.localPosition = basePos;
            solid.enabled = true;
            t = 0;
            while (t < 0.35f)
            {
                t += Time.deltaTime;
                float progress = t / 0.35f;
                float overshoot = 1f + Mathf.Sin(progress * Mathf.PI) * 0.12f;
                visual.localScale = baseScale * Mathf.Lerp(0.2f, 1f, progress) * overshoot;
                yield return null;
            }
            visual.localScale = baseScale;
            busy = false;
        }
    }

    /// <summary>Banner pole that saves Bobo's progress.</summary>
    public class Checkpoint : MonoBehaviour
    {
        public int index;
        public Renderer banner;
        public Light glow;
        public ParticleSystem burst;
        public Material lantern;
        public bool reached;

        public Vector3 RespawnPoint => transform.position + Vector3.up * 0.6f;

        /// <summary>Mark as reached and light it up; also used when resuming a saved climb.</summary>
        public void Ignite(bool celebrate)
        {
            reached = true;
            if (banner) banner.material.SetColor("_Color", new Color(0.85f, 0.6f, 0.2f));
            if (glow) glow.enabled = true;
            if (lantern) lantern.SetColor("_EmissionColor", new Color(1f, 0.62f, 0.32f) * 1.4f);
            if (celebrate && burst) burst.Play();
        }

        void OnTriggerEnter(Collider other)
        {
            if (reached) return;
            if (other.GetComponentInParent<BoboController>() == null) return;
            Ignite(true);
            GameManager.Instance?.ReachCheckpoint(this);
        }
    }

    /// <summary>Gentle idle float for decorative props.</summary>
    public class Bob : MonoBehaviour
    {
        public float amplitude = 0.15f;
        public float speed = 1.2f;
        public float spin;
        Vector3 start;
        float offset;

        void Start()
        {
            start = transform.localPosition;
            offset = Random.value * 10f;
        }

        void Update()
        {
            transform.localPosition = start + Vector3.up * Mathf.Sin((Time.time + offset) * speed) * amplitude;
            if (spin != 0) transform.Rotate(0, spin * Time.deltaTime, 0, Space.World);
        }
    }
}
