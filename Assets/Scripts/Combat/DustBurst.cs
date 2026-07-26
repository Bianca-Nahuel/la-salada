using UnityEngine;

namespace Salada.Combat
{
    /// <summary>
    /// Puff de polvo cuando se destruye un puesto (al demoler en construccion o al romperse en
    /// una guerra). Se arma por codigo (sin prefab) y se autodestruye. Las particulas usan una
    /// textura radial suave generada al vuelo asi el polvo se ve redondeado y no cuadrado.
    /// </summary>
    public class DustBurst : MonoBehaviour
    {
        static Material _mat;

        public static void Spawn(Vector3 pos)
        {
            var go = new GameObject("DustBurst");
            go.transform.position = pos;

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); // configurar antes de reproducir

            var main = ps.main;
            main.duration = 0.6f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.7f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.42f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.80f, 0.73f, 0.58f, 0.95f), new Color(0.62f, 0.56f, 0.45f, 0.95f));
            main.gravityModifier = -0.04f; // apenas se eleva, como polvo
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 80;
            main.playOnAwake = false;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 30) });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.28f;

            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f,
                new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(1f, 1.4f)));

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.5f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(grad);

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.material = PuffMaterial();
            r.sortingOrder = 45; // por encima del piso y los puestos

            ps.Play();
            Destroy(go, 1.2f);
        }

        static Material PuffMaterial()
        {
            if (_mat != null) return _mat;
            var shader = Shader.Find("Sprites/Default");
            _mat = new Material(shader) { mainTexture = PuffTexture() };
            return _mat;
        }

        static Texture2D _tex;
        static Texture2D PuffTexture()
        {
            if (_tex != null) return _tex;
            const int s = 32;
            _tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
            var center = new Vector2(s * 0.5f, s * 0.5f);
            var px = new Color32[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center) / (s * 0.5f);
                    float a = Mathf.Clamp01(1f - d);
                    a *= a; // borde mas suave
                    px[y * s + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            _tex.SetPixels32(px);
            _tex.Apply();
            return _tex;
        }
    }
}
