using UnityEngine;

namespace BobosJourney
{
    /// <summary>Ball-shaped character with a painted face, springy squash and stretch, and blinking.</summary>
    public class BallCharacter : MonoBehaviour
    {
        public float radius = 0.5f;
        public Transform squashPivot;
        public Transform body;
        public Expression expression = Expression.Happy;
        public bool autoBlink = true;

        /// <summary>Squash target from motion: positive squashes flat, negative stretches tall.</summary>
        [HideInInspector] public float motionSquash;

        Material faceMat;
        Texture2D[] faces;
        float squash;
        float squashVel;
        float blinkTimer = 2f;
        float blinkHold;

        public Material FaceMaterial => faceMat;

        public void Init(float r, Material mat, Texture2D[] faceSet)
        {
            radius = r;
            faceMat = mat;
            faces = faceSet;
            SetExpression(expression);
        }

        public void SetExpression(Expression e)
        {
            expression = e;
            ApplyFace(e);
        }

        void ApplyFace(Expression e)
        {
            if (faceMat == null || faces == null || faces.Length == 0) return;
            int i = (int)e;
            var tex = i < faces.Length ? faces[i] : null;
            if (tex == null) tex = faces[0];
            faceMat.SetTexture("_MainTex", tex);
        }

        /// <summary>Kick the squash spring. Positive squashes (landing), negative stretches (jumping).</summary>
        public void Punch(float amount) => squashVel += amount;

        void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            // damped spring toward the motion-driven target
            float target = Mathf.Clamp(motionSquash, -0.35f, 0.4f);
            float acc = (target - squash) * 320f - squashVel * 17f;
            squashVel += acc * dt;
            squash += squashVel * dt;
            squash = Mathf.Clamp(squash, -0.45f, 0.55f);

            float y = 1f - squash;
            float xz = 1f / Mathf.Sqrt(Mathf.Max(0.2f, y)); // keep the volume roughly constant
            if (squashPivot) squashPivot.localScale = new Vector3(xz, y, xz);

            if (autoBlink && faces != null && (int)Expression.Blink < faces.Length && faces[(int)Expression.Blink] != null
                && (expression == Expression.Determined))
            {
                if (blinkHold > 0f)
                {
                    blinkHold -= dt;
                    if (blinkHold <= 0f) ApplyFace(expression);
                }
                else
                {
                    blinkTimer -= dt;
                    if (blinkTimer <= 0f)
                    {
                        ApplyFace(Expression.Blink);
                        blinkHold = 0.11f;
                        blinkTimer = Random.Range(1.8f, 4.5f);
                    }
                }
            }
        }

        public static BallCharacter Create(float radius, Transform parent = null, Expression expr = Expression.Happy)
        {
            var art = BoboArt.Instance;
            var root = new GameObject("Bobo");
            if (parent) root.transform.SetParent(parent, false);

            // the pivot sits at the bottom of the ball so squashing keeps it planted on the ground
            var pivot = new GameObject("Squash").transform;
            pivot.SetParent(root.transform, false);

            var mat = Mats.ToonInstance(Color.white, null, 2.6f);
            mat.SetColor("_ShadowColor", new Color(0.72f, 0.6f, 0.86f));
            mat.SetFloat("_SpecSize", 0.012f);
            mat.SetColor("_SpecColor2", new Color(1, 1, 1, 0.45f));
            mat.SetColor("_RimColor", new Color(1f, 0.96f, 1f, 0.28f));

            var bodyGo = ProceduralMeshes.Spawn("Body", ProceduralMeshes.Sphere(), mat, pivot, new Vector3(0, radius, 0), Vector3.one * radius);
            var character = root.AddComponent<BallCharacter>();
            character.squashPivot = pivot;
            character.body = bodyGo.transform;
            character.expression = expr;
            character.Init(radius, mat, art.boboFaces);

            return character;
        }
    }
}
