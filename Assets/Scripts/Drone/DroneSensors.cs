using UnityEngine;

public class DroneSensors : MonoBehaviour
{
    public int radialRays = 16;
    public float rayRange = 30f;
    public float groundRayRange = 50f;
    public LayerMask obstacleMask = ~0;
    public bool drawGizmos = true;

    private float[] distances;

    private void Awake()
    {
        distances = new float[radialRays + 1]; // last index = ground distance
    }

    private void Update()
    {
        if (distances == null || distances.Length != radialRays + 1)
            distances = new float[radialRays + 1];

        Vector3 origin = transform.position;

        // Horizontal radial rays
        for (int i = 0; i < radialRays; i++)
        {
            float ang = i * Mathf.PI * 2f / radialRays;
            Vector3 dirLocal = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
            Vector3 dir = transform.TransformDirection(dirLocal);

            if (Physics.Raycast(origin, dir, out RaycastHit hit, rayRange, obstacleMask, QueryTriggerInteraction.Ignore))
                distances[i] = hit.distance / rayRange;
            else
                distances[i] = 1f;
        }

        // Ground ray
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit gHit, groundRayRange, obstacleMask, QueryTriggerInteraction.Ignore))
            distances[radialRays] = gHit.distance / groundRayRange;
        else
            distances[radialRays] = 1f;
    }

    public float[] GetObservations()
    {
        return distances;
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;

        Vector3 origin = transform.position;
        Gizmos.color = Color.cyan;

        for (int i = 0; i < radialRays; i++)
        {
            float ang = i * Mathf.PI * 2f / radialRays;
            Vector3 dir = transform.TransformDirection(new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)));
            Gizmos.DrawLine(origin, origin + dir * rayRange);
        }

        Gizmos.color = Color.green;
        Gizmos.DrawLine(origin, origin + Vector3.down * groundRayRange);
    }
}
