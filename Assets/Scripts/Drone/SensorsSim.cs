using System.Collections.Generic;
using UnityEngine;

/// <summary> Simulated onboard sensors with optional noise & latency buffers. </summary>
[RequireComponent(typeof(Rigidbody))]
public class SensorsSim : MonoBehaviour
{
    public struct ImuSample { public Vector3 angVel; public Vector3 linAcc; public Quaternion attitude; }
    public struct NavSample { public Vector3 position; public Vector3 velocity; }

    public bool enableNoise = false;
    public bool enableLatency = false;
    public float imuLatencySeconds = 0.02f; // 20 ms
    public float navLatencySeconds = 0.18f; // GNSS 180 ms

    private Rigidbody rb;
    private Queue<(float time, ImuSample sample)> imuQueue = new();
    private Queue<(float time, NavSample sample)> navQueue = new();

    private Vector3 lastVel;
    private DroneTuning tuning;

    public ImuSample CurrentImu { get; private set; }
    public NavSample CurrentNav { get; private set; }

    public void Init(DroneTuning t)
    {
        tuning = t;
        enableNoise = t.sensorNoiseEnabled;
        enableLatency = t.sensorLatencyEnabled;
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        Vector3 vel = rb.velocity;
        Vector3 accWorld = (vel - lastVel) / Mathf.Max(dt, 1e-5f) - Physics.gravity; // body linear accel (without gravity)
        lastVel = vel;
        Vector3 accBody = transform.InverseTransformVector(accWorld);
        Vector3 angVel = rb.angularVelocity; // rad/s in world; convert to body local
        Vector3 angVelBody = transform.InverseTransformVector(angVel);

        ImuSample imu = new ImuSample { angVel = NoiseVec(angVelBody, 0.01f), linAcc = NoiseVec(accBody, 0.1f), attitude = transform.rotation };
        NavSample nav = new NavSample { position = NoiseVec(transform.position, 0.08f), velocity = NoiseVec(vel, 0.05f) };

        if (enableLatency)
        {
            float now = Time.time;
            imuQueue.Enqueue((now, imu));
            navQueue.Enqueue((now, nav));
            // Pop until latency satisfied
            while (imuQueue.Count > 0 && now - imuQueue.Peek().time >= imuLatencySeconds) CurrentImu = imuQueue.Dequeue().sample;
            while (navQueue.Count > 0 && now - navQueue.Peek().time >= navLatencySeconds) CurrentNav = navQueue.Dequeue().sample;
        }
        else
        {
            CurrentImu = imu;
            CurrentNav = nav;
        }
    }

    private Vector3 NoiseVec(Vector3 v, float sigma)
    {
        if (!enableNoise) return v;
        return v + new Vector3(RandomGaussian() * sigma, RandomGaussian() * sigma, RandomGaussian() * sigma);
    }

    private float RandomGaussian()
    {
        // Box-Muller
        float u1 = Mathf.Clamp01(Random.value);
        float u2 = Mathf.Clamp01(Random.value);
        return Mathf.Sqrt(-2f * Mathf.Log(u1 + 1e-6f)) * Mathf.Cos(2 * Mathf.PI * u2);
    }
}
