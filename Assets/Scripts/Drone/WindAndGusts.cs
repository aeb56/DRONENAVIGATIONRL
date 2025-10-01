using UnityEngine;

/// <summary> Adds wind and gusts to make flying more realistic </summary>
[RequireComponent(typeof(Rigidbody))]
public class WindAndGusts : MonoBehaviour
{
    public DroneTuning tuning;
    public float turbulence = 0.3f;
    private Rigidbody rb;
    private float seed;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        seed = Random.value * 1000f;
    }

    private void FixedUpdate()
    {
        if (tuning == null || !tuning.windEnabled) return;
        float t = Time.time;
        Vector3 gust = new Vector3(
            (Mathf.PerlinNoise(seed, t * tuning.gustFrequency) - 0.5f),
            (Mathf.PerlinNoise(seed + 33.1f, t * tuning.gustFrequency * 0.7f) - 0.5f) * 0.3f,
            (Mathf.PerlinNoise(seed + 72.7f, t * tuning.gustFrequency) - 0.5f)
        );
        gust *= tuning.gustStrength;

        Vector3 wind = tuning.baseWind + gust;
        // Simple drag force relative to air mass
        Vector3 relVel = rb.velocity - wind;
        Vector3 force = -relVel * 0.4f; // coarse coefficient
        rb.AddForce(force, ForceMode.Force);

        // Small random torque for turbulence
        rb.AddTorque(new Vector3(gust.x, gust.y, gust.z) * turbulence, ForceMode.Force);
    }
}
