using System.Collections;
using UnityEngine;

namespace BobosJourney
{
    /// <summary>Small one-off effects: dust puffs, confetti and shock rings.</summary>
    public static class FxFactory
    {
        public static ParticleSystem Dust(Transform owner)
        {
            var go = new GameObject("Dust");
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
            main.startColor = new Color(1f, 0.97f, 0.95f, 0.85f);
            main.gravityModifier = -0.05f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 128;

            var emission = ps.emission;
            emission.enabled = false;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.35f;
            shape.rotation = new Vector3(-90, 0, 0);

            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0, 0.6f, 1, 1.5f));

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(new Color(0.85f, 0.82f, 0.95f), 1) },
                new[] { new GradientAlphaKey(0.9f, 0), new GradientAlphaKey(0f, 1) });
            colorOverLifetime.color = grad;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = Mats.Fx("dust", Color.white, BoboArt.Instance.softDot, false);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return ps;
        }

        public static ParticleSystem Confetti(Transform parent, Vector3 localPos)
        {
            var go = new GameObject("Confetti");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.5f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 10f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.55f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
            main.gravityModifier = 1.1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0, 40) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 30;
            shape.radius = 0.3f;
            shape.rotation = new Vector3(-90, 0, 0);

            var rotation = ps.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-4f, 4f);

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, 0.2f));

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = Mats.Fx("starfx", Color.white, BoboArt.Instance.star, false);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return ps;
        }

        /// <summary>A flat expanding ring that fades out.</summary>
        public static void Ring(Vector3 pos, float size, Color color)
        {
            var mat = Mats.Fx("ring", color, null, true, false);
            var go = ProceduralMeshes.Spawn("Ring", ProceduralMeshes.Ring(0.82f, 1f), mat, null, pos + Vector3.up * 0.05f, Vector3.one * 0.2f, null, false);
            Runner.Run(RingRoutine(go, mat, size, color));
        }

        static IEnumerator RingRoutine(GameObject go, Material mat, float size, Color color)
        {
            float t = 0;
            while (t < 0.45f && go)
            {
                t += Time.deltaTime;
                float k = t / 0.45f;
                go.transform.localScale = Vector3.one * Mathf.Lerp(0.3f, size, 1f - (1f - k) * (1f - k));
                mat.SetColor("_Color", new Color(color.r, color.g, color.b, (1f - k) * 0.9f));
                yield return null;
            }
            if (go) Object.Destroy(go);
            Object.Destroy(mat);
        }
    }

    /// <summary>Runs coroutines for static helpers.</summary>
    public class Runner : MonoBehaviour
    {
        static Runner instance;

        public static Coroutine Run(IEnumerator routine)
        {
            if (instance == null) instance = new GameObject("Runner").AddComponent<Runner>();
            return instance.StartCoroutine(routine);
        }
    }
}
