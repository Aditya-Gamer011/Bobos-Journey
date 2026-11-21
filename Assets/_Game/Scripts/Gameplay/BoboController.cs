using UnityEngine;

namespace BobosJourney
{
    /// <summary>Rigidbody platformer controller for Bobo, with an "elastic" combo for jumping right after a big landing.</summary>
    [RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
    public class BoboController : MonoBehaviour
    {
        [Header("Shape")]
        public float radius = 0.5f;

        [Header("Run")]
        public float maxSpeed = 8.2f;
        public float groundAccel = 70f;
        public float groundDecel = 60f;
        public float airAccel = 30f;

        [Header("Jump")]
        public float jumpHeight = 3.2f;
        public float gravity = 32f;
        public float fallMultiplier = 1.5f;
        public float lowJumpMultiplier = 2.1f;
        public float maxFallSpeed = 40f;
        public float coyoteTime = 0.12f;
        public float jumpBuffer = 0.15f;

        [Header("Elastic bounce")]
        public float elasticWindow = 0.16f;
        public float elasticMinImpact = 11f;
        public float elasticBoostPerCombo = 0.2f;
        public int elasticMaxCombo = 3;

        [Header("Refs")]
        public Transform cameraTransform;
        public BallCharacter visual;
        public Transform blobShadow;

        public bool Grounded { get; private set; }
        public int ElasticCombo { get; private set; }
        public Vector3 Velocity => rb ? rb.linearVelocity : Vector3.zero;
        public bool InputEnabled = true;

        public System.Action<float> OnLanded;
        public System.Action<int> OnElastic;

        Rigidbody rb;
        Vector3 platformVelocity;
        Vector3 knock;
        float lastGroundedTime = -10f;
        float lastJumpPressTime = -10f;
        float lastLandTime = -10f;
        float lastLandImpact;
        bool jumping;
        Collider groundCollider;
        Vector2 moveInput;
        Vector3 facing = Vector3.forward;
        Material shadowMat;
        ParticleSystem dust;
        float stepBob;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.useGravity = false;
            rb.freezeRotation = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            var col = GetComponent<SphereCollider>();
            col.radius = radius;
            col.sharedMaterial = new PhysicsMaterial("Bobo")
            {
                dynamicFriction = 0,
                staticFriction = 0,
                bounciness = 0,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounceCombine = PhysicsMaterialCombine.Minimum
            };
            gameObject.layer = 2; // Ignore Raycast, so our own casts never hit us
        }

        void Start()
        {
            if (blobShadow == null) BuildShadow();
            dust = FxFactory.Dust(transform);
        }

        void BuildShadow()
        {
            shadowMat = Mats.Fx("blobshadow_inst", new Color(0.12f, 0.06f, 0.2f, 0.5f), BoboArt.Instance.blob, false, false, -2f);
            var go = ProceduralMeshes.Spawn("BlobShadow", ProceduralMeshes.FlatQuad(), shadowMat, null, Vector3.zero, Vector3.one, null, false);
            go.GetComponent<MeshRenderer>().receiveShadows = false;
            blobShadow = go.transform;
        }

        public void Teleport(Vector3 position)
        {
            rb.position = position;
            transform.position = position;
            rb.linearVelocity = Vector3.zero;
            knock = Vector3.zero;
            jumping = false;
            ElasticCombo = 0;
        }

        /// <summary>Snap Bobo's facing to a flat direction immediately.</summary>
        public void Face(Vector3 dir)
        {
            dir.y = 0;
            if (dir.sqrMagnitude < 0.001f) return;
            facing = dir.normalized;
            if (visual) visual.transform.rotation = Quaternion.LookRotation(facing, Vector3.up);
        }

        public void Knock(Vector3 impulseVelocity)
        {
            knock = impulseVelocity;
            jumping = false;
            Grounded = false;
            lastGroundedTime = -10f;
        }

        void Update()
        {
            moveInput = InputEnabled ? GameInput.Move : Vector2.zero;
            if (InputEnabled && GameInput.JumpDown) lastJumpPressTime = Time.time;
            UpdateVisual();
        }

        void FixedUpdate()
        {
            if (rb.isKinematic) return; // held in place (portal drop, cutscenes)
            float dt = Time.fixedDeltaTime;
            Vector3 v = rb.linearVelocity;
            bool wasGrounded = Grounded;
            float preVy = v.y - platformVelocity.y;
            GroundCheck(v);

            if (Grounded && !wasGrounded) Land(-preVy);
            if (Grounded)
            {
                lastGroundedTime = Time.time;
                jumping = false;
            }

            Vector3 camForward = cameraTransform ? Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized : Vector3.forward;
            if (camForward.sqrMagnitude < 0.01f) camForward = Vector3.forward;
            Vector3 camRight = Vector3.Cross(Vector3.up, camForward);
            Vector3 wish = (camForward * moveInput.y + camRight * moveInput.x) * maxSpeed;
            if (wish.sqrMagnitude > 0.01f) facing = wish.normalized;

            Vector3 basis = Grounded ? new Vector3(platformVelocity.x, 0, platformVelocity.z) : Vector3.zero;
            Vector3 horizontalVel = new Vector3(v.x, 0, v.z) - basis;
            float accel = Grounded ? (wish.sqrMagnitude > 0.01f ? groundAccel : groundDecel) : airAccel;
            // accelerate harder when reversing so turns feel snappy
            if (Vector3.Dot(horizontalVel, wish) < 0) accel *= 1.6f;
            horizontalVel = Vector3.MoveTowards(horizontalVel, wish, accel * dt);

            float vy = v.y;
            bool canCoyote = Time.time - lastGroundedTime <= coyoteTime && !jumping;
            bool buffered = Time.time - lastJumpPressTime <= jumpBuffer;
            if (buffered && canCoyote && InputEnabled)
            {
                vy = DoJump();
            }
            else if (Grounded)
            {
                vy = platformVelocity.y - 1.5f; // keep glued to slopes and descending lifts
                if (groundCollider != null)
                {
                    var pad = groundCollider.GetComponentInParent<BouncePad>();
                    if (pad != null)
                    {
                        vy = Mathf.Sqrt(2f * gravity * pad.launchHeight);
                        pad.OnBounce();
                        Launch();
                        visual?.Punch(-6f);
                    }
                    groundCollider.GetComponentInParent<CrumblePlatform>()?.Touch();
                }
            }
            else
            {
                float g = gravity;
                if (vy < 0) g *= fallMultiplier;
                else if (jumping && !GameInput.JumpHeld) g *= lowJumpMultiplier;
                vy -= g * dt;
                vy = Mathf.Max(vy, -maxFallSpeed);
            }

            Vector3 newVelocity = horizontalVel + basis + Vector3.up * vy;
            if (knock.sqrMagnitude > 0.01f)
            {
                newVelocity += knock;
                knock = Vector3.MoveTowards(knock, Vector3.zero, 25f * dt);
                if (knock.y > 0) knock.y = 0;
            }
            rb.linearVelocity = newVelocity;
        }

        float DoJump()
        {
            float height = jumpHeight;
            bool elastic = Time.time - lastLandTime <= elasticWindow && lastLandImpact >= elasticMinImpact;
            if (elastic)
            {
                ElasticCombo = Mathf.Min(ElasticCombo + 1, elasticMaxCombo);
                height *= 1f + elasticBoostPerCombo * ElasticCombo;
                OnElastic?.Invoke(ElasticCombo);
                Sfx.Play(Sfx.Id.Elastic, 0.75f, 1f + ElasticCombo * 0.08f);
                FxFactory.Ring(transform.position - Vector3.up * radius * 0.9f, 1.2f + ElasticCombo * 0.4f, Mats.Hex("#9fe3ff"));
            }
            else
            {
                ElasticCombo = 0;
                Sfx.Play(Sfx.Id.Jump, 0.55f, Random.Range(0.96f, 1.06f));
            }
            lastJumpPressTime = -10f;
            Launch();
            jumping = true;
            visual?.Punch(-5f - ElasticCombo * 1.5f);
            if (dust)
            {
                dust.transform.position = transform.position - Vector3.up * radius;
                dust.Emit(6);
            }
            return Mathf.Sqrt(2f * gravity * height) + Mathf.Max(0f, platformVelocity.y);
        }

        void Launch()
        {
            Grounded = false;
            lastGroundedTime = -10f;
        }

        void Land(float impact)
        {
            lastLandTime = Time.time;
            lastLandImpact = impact;
            if (impact < elasticMinImpact) ElasticCombo = 0;
            float strength = Mathf.InverseLerp(2f, 30f, impact);
            visual?.Punch(2f + strength * 9f);
            if (impact > 3f)
            {
                Sfx.Play(Sfx.Id.Land, Mathf.Lerp(0.25f, 0.8f, strength), Random.Range(0.92f, 1.08f));
                if (dust)
                {
                    dust.transform.position = transform.position - Vector3.up * radius;
                    dust.Emit(Mathf.RoundToInt(Mathf.Lerp(4, 16, strength)));
                }
            }
            OnLanded?.Invoke(impact);
        }

        void GroundCheck(Vector3 v)
        {
            Vector3 origin = rb.position + Vector3.up * 0.1f;
            bool hit = Physics.SphereCast(origin, radius * 0.9f, Vector3.down, out var info, 0.1f + radius * 0.1f + 0.08f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            bool good = hit && info.normal.y > 0.55f;
            if (good)
            {
                var motion = info.collider.GetComponentInParent<IPlatformMotion>();
                Vector3 surfaceVel = motion != null ? motion.VelocityAt(info.point) : Vector3.zero;
                // rising away from the surface (just jumped) does not count as grounded
                if (v.y - surfaceVel.y > 1.0f)
                {
                    good = false;
                }
                else
                {
                    platformVelocity = surfaceVel;
                    groundCollider = info.collider;
                }
            }
            if (!good)
            {
                groundCollider = null;
                if (!Grounded) platformVelocity = Vector3.Lerp(platformVelocity, Vector3.zero, 0.02f);
            }
            Grounded = good;
        }

        void UpdateVisual()
        {
            if (visual == null) return;
            var target = Quaternion.LookRotation(facing, Vector3.up);
            visual.transform.rotation = Quaternion.Slerp(visual.transform.rotation, target, 1f - Mathf.Exp(-12f * Time.deltaTime));

            Vector3 v = rb.linearVelocity - platformVelocity;
            float speed = new Vector2(v.x, v.z).magnitude;
            // springy hop while running, stretch while airborne
            if (Grounded)
            {
                stepBob += Time.deltaTime * speed * 1.6f;
                visual.motionSquash = speed > 0.5f ? Mathf.Sin(stepBob) * 0.06f * Mathf.Clamp01(speed / maxSpeed) : 0f;
            }
            else
            {
                visual.motionSquash = -Mathf.Clamp(Mathf.Abs(v.y) * 0.009f, 0f, 0.2f);
            }

            float lean = Mathf.Clamp(speed / maxSpeed, 0, 1) * 8f;
            visual.squashPivot.localRotation = Quaternion.Slerp(visual.squashPivot.localRotation, Quaternion.Euler(lean, 0, 0), 1f - Mathf.Exp(-10f * Time.deltaTime));

            // blob shadow makes landing spots readable
            if (blobShadow)
            {
                if (Physics.Raycast(transform.position, Vector3.down, out var hit, 60f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    blobShadow.gameObject.SetActive(true);
                    float heightAboveGround = hit.distance - radius;
                    blobShadow.position = hit.point + hit.normal * 0.03f;
                    blobShadow.rotation = Quaternion.FromToRotation(Vector3.up, hit.normal);
                    float shadowSize = radius * 2.1f * (1f + heightAboveGround * 0.04f);
                    blobShadow.localScale = new Vector3(shadowSize, 1, shadowSize);
                    if (shadowMat) shadowMat.SetColor("_Color", new Color(0.12f, 0.05f, 0.22f, Mathf.Lerp(0.6f, 0.18f, heightAboveGround / 25f)));
                }
                else
                {
                    blobShadow.gameObject.SetActive(false);
                }
            }
        }

        void OnDestroy() { if (blobShadow) Destroy(blobShadow.gameObject); }
    }
}
