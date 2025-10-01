using System.Collections.Generic;
using UnityEngine;

public class RandomObstacleSpawner : MonoBehaviour
{
    [Header("Spawn Counts")]
    public int minObjects = 10;
    public int maxObjects = 30;

    [Header("Arena")]
    public Vector2 arenaSize = new Vector2(200f, 200f);
    public float groundY = 0f;

    [Header("Sizes")]
    public float minSize = 0.5f;
    public float maxSize = 5f;

    [Header("Randomization")]
    public int seed = 0;

    [Header("Parenting")]
    public Transform parentForSpawned;

    private readonly List<GameObject> spawned = new List<GameObject>();
    private System.Random rng;

    private void Awake()
    {
        rng = seed == 0 ? new System.Random() : new System.Random(seed);
        if (parentForSpawned == null)
        {
            parentForSpawned = transform;
        }
    }

    public void ResetSpawn()
    {
        ClearSpawned();
        Spawn();
    }

    public void ClearSpawned()
    {
        for (int i = spawned.Count - 1; i >= 0; i--)
        {
            var go = spawned[i];
            if (go == null) continue;
#if UNITY_EDITOR
            if (!Application.isPlaying)
                GameObject.DestroyImmediate(go);
            else
#endif
                GameObject.Destroy(go);
        }
        spawned.Clear();
    }

    public void Spawn()
    {
        int count = Mathf.Clamp(RandomRangeInt(minObjects, maxObjects + 1), minObjects, maxObjects);

        for (int i = 0; i < count; i++)
        {
            var typePick = rng.NextDouble();
            GameObject go;
            if (typePick < 0.33)
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = $"RndCube_{i}";
            }
            else if (typePick < 0.66)
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = $"RndSphere_{i}";
            }
            else
            {
                // ramp: a thin cube tilted
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = $"RndRamp_{i}";
                float tilt = Mathf.Lerp(10f, 30f, (float)rng.NextDouble());
                go.transform.rotation = Quaternion.Euler(tilt, (float)rng.NextDouble() * 360f, 0f);
            }

            go.transform.SetParent(parentForSpawned, true);

            float sx = Mathf.Lerp(minSize, maxSize, (float)rng.NextDouble());
            float sy = Mathf.Lerp(minSize, maxSize, (float)rng.NextDouble());
            float sz = Mathf.Lerp(minSize, maxSize, (float)rng.NextDouble());

            // Ramps are flatter
            if (go.name.StartsWith("RndRamp"))
            {
                sy = Mathf.Clamp(sy * 0.2f, 0.2f, 1.0f);
                sx = Mathf.Max(1.5f, sx);
                sz = Mathf.Max(2.5f, sz);
            }

            go.transform.localScale = new Vector3(sx, sy, sz);

            var pos = RandomXZ((arenaSize * 0.5f) - new Vector2(2f, 2f));
            float y = groundY + sy * 0.5f;
            go.transform.position = new Vector3(pos.x, y, pos.y);

            // Collider setup
            var col = go.GetComponent<Collider>();
            if (col != null)
            {
                col.isTrigger = false;
            }

            // Bright random color
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("HDRP/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                if (shader == null) shader = Shader.Find("Legacy Shaders/Diffuse");
                if (shader == null) shader = Shader.Find("Unlit/Color"); // ultimate fallback

                var mat = new Material(shader);
                mat.color = RandomBrightColor();
                mat.enableInstancing = true;
                mr.sharedMaterial = mat;
            }

            spawned.Add(go);
        }
    }

    public void SetDifficulty(float t)
    {
        // t in [0,1]
        t = Mathf.Clamp01(t);
        int baseMin = 10, baseMax = 30;
        minObjects = Mathf.RoundToInt(Mathf.Lerp(baseMin, 40, t));
        maxObjects = Mathf.RoundToInt(Mathf.Lerp(baseMax, 80, t));
        minSize = Mathf.Lerp(0.5f, 0.3f, t);
        maxSize = Mathf.Lerp(5f, 6f, t);
    }

    public void SetSeed(int newSeed, bool respawn = true)
    {
        seed = newSeed;
        rng = seed == 0 ? new System.Random() : new System.Random(seed);
        if (respawn)
        {
            ResetSpawn();
        }
    }

    private int RandomRangeInt(int minInclusive, int maxExclusive)
    {
        if (rng == null) rng = new System.Random();
        return rng.Next(minInclusive, maxExclusive);
    }

    private Vector2 RandomXZ(Vector2 halfExtents)
    {
        float x = (float)(rng.NextDouble() * 2 - 1) * halfExtents.x;
        float z = (float)(rng.NextDouble() * 2 - 1) * halfExtents.y;
        return new Vector2(x, z);
    }

    private Color RandomBrightColor()
    {
        float h = (float)rng.NextDouble();
        float s = 0.8f + 0.2f * (float)rng.NextDouble();
        float v = 0.8f + 0.2f * (float)rng.NextDouble();
        return Color.HSVToRGB(h, s, v);
    }
}
