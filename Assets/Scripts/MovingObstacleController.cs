using System.Collections.Generic;
using UnityEngine;

public class MovingObstacleController : MonoBehaviour
{
    public enum Mode
    {
        Rotate,
        MoveBetweenPoints
    }

    [Header("General")]
    public Mode mode = Mode.Rotate;

    [Header("Rotation")]
    public float angularSpeed = 90f; // degrees per second
    public Vector3 rotationAxis = Vector3.up;

    [Header("Linear Movement")]
    public List<Transform> waypoints = new List<Transform>();
    public float speed = 2f;
    public bool loop = true;
    public bool pingPong = true;

    private int currentIndex = 0;
    private int dir = 1;

    private void Update()
    {
        if (mode == Mode.Rotate)
        {
            transform.Rotate(rotationAxis, angularSpeed * Time.deltaTime, Space.Self);
        }
        else if (mode == Mode.MoveBetweenPoints)
        {
            if (waypoints == null || waypoints.Count < 2) return;

            Transform target = waypoints[currentIndex];
            Vector3 to = target.position - transform.position;
            float step = speed * Time.deltaTime;

            if (to.magnitude <= step)
            {
                transform.position = target.position;
                currentIndex += dir;

                if (currentIndex >= waypoints.Count)
                {
                    if (loop)
                    {
                        if (pingPong)
                        {
                            dir = -1;
                            currentIndex = waypoints.Count - 2;
                        }
                        else
                        {
                            currentIndex = 0;
                        }
                    }
                    else
                    {
                        currentIndex = waypoints.Count - 1;
                    }
                }
                else if (currentIndex < 0)
                {
                    dir = 1;
                    currentIndex = 1;
                }
            }
            else
            {
                transform.position += to.normalized * step;
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (mode == Mode.MoveBetweenPoints && waypoints != null && waypoints.Count > 1)
        {
            Gizmos.color = Color.yellow;
            for (int i = 0; i < waypoints.Count - 1; i++)
            {
                var a = waypoints[i];
                var b = waypoints[i + 1];
                if (a && b)
                {
                    Gizmos.DrawLine(a.position, b.position);
                    Gizmos.DrawSphere(a.position, 0.2f);
                    Gizmos.DrawSphere(b.position, 0.2f);
                }
            }
        }
    }
}
