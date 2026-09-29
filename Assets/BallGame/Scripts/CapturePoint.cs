using UnityEngine;

namespace BallGame
{
    [RequireComponent(typeof(CircleCollider2D))]
    public sealed class CapturePoint : MonoBehaviour
    {
        public const float Radius = 0.55f;
        public CaptureLauncher launcher;
        public LineRenderer ring;

        public void SetRingRadius(float radius)
        {
            ring.positionCount = 48;
            for (int i = 0; i < ring.positionCount; i++)
            {
                float angle = i * Mathf.PI * 2 / ring.positionCount;
                ring.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * radius);
            }
        }

        void OnTriggerEnter2D(Collider2D other) { Request(other); }
        void OnTriggerStay2D(Collider2D other) { Request(other); }
        void Request(Collider2D other)
        {
            if (launcher != null && other.TryGetComponent<ElasticBall>(out var ball) && ball.Moving)
                launcher.RequestCapture(this);
        }
    }
}
