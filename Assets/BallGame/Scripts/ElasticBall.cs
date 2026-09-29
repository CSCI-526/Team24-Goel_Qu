using UnityEngine;

namespace BallGame
{
    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
    public sealed class ElasticBall : MonoBehaviour
    {
        public const float BaseDiameter = 0.5f;
        public const float MinDiameter = 0.25f;
        public const float MaxDiameter = 6;
        public BallGameController game;
        public float Speed { get; private set; } = 7;
        public float Diameter { get; private set; } = BaseDiameter;
        public float Radius => Diameter * 0.5f;
        public bool Moving { get; private set; }
        public Rigidbody2D Body => body != null ? body : body = GetComponent<Rigidbody2D>();
        Rigidbody2D body;
        float baseSpeed = 7;
        Vector2 direction = Vector2.right;

        public void ResetSize(float speed, float diameter = BaseDiameter)
        {
            Stop();
            baseSpeed = speed;
            SetDiameter(diameter);
            GetComponent<SpriteRenderer>().color = Color.white;
            GetComponent<CircleCollider2D>().enabled = true;
        }

        public void Launch(Vector2 heading, float speed)
        {
            if (heading.sqrMagnitude < 0.001f) return;
            baseSpeed = speed;
            direction = heading.normalized;
            Speed = baseSpeed * BaseDiameter / Diameter;
            Moving = true;
            Body.bodyType = RigidbodyType2D.Dynamic;
            Body.linearVelocity = direction * Speed;
            Body.WakeUp();
        }

        public void SetDiameter(float diameter)
        {
            Diameter = Mathf.Clamp(diameter, MinDiameter, MaxDiameter);
            transform.localScale = Vector3.one * Diameter;
            // Inverse size/speed relation: half the diameter means twice the speed.
            Speed = baseSpeed * BaseDiameter / Diameter;
            if (Moving)
            {
                if (Body.linearVelocity.sqrMagnitude > 0.001f) direction = Body.linearVelocity.normalized;
                Body.linearVelocity = direction * Speed;
            }
        }

        public void Stop()
        {
            Moving = false;
            Body.linearVelocity = Vector2.zero;
            Body.bodyType = RigidbodyType2D.Kinematic;
        }

        void FixedUpdate()
        {
            if (!Moving) return;
            if (game != null && game.captureLauncher != null
                && game.captureLauncher.TryCaptureAlongStep(Body.position, direction * Speed * Time.fixedDeltaTime)) return;
            if (!Moving) return;
            Body.linearVelocity = direction * Speed;
        }

        void OnCollisionEnter2D(Collision2D collision)
        {
            if (game == null || !Moving || game.Ended) return;
            if (collision.collider.gameObject.layer == 8)
            {
                game.BurnBall();
                return;
            }
            if (collision.collider.gameObject.layer == ShrinkingPocket.BumperLayer)
            {
                BounceOffExit(collision);
                return;
            }
            if (collision.collider.gameObject.layer == 9)
            {
                Vector2 outgoing = Body.linearVelocity;
                Vector2 normal = collision.GetContact(0).normal;
                if (!game.deflectionLines.Consume(collision.collider.gameObject)) return;
                direction = outgoing.sqrMagnitude > 0.001f ? outgoing.normalized
                    : Vector2.Reflect(direction, normal);
                Body.linearVelocity = direction * Speed;
                game.RegisterDeflection();
            }
        }

        void OnCollisionStay2D(Collision2D collision)
        {
            if (game != null && Moving && !game.Ended
                && collision.collider.gameObject.layer == ShrinkingPocket.BumperLayer)
                BounceOffExit(collision);
        }

        void BounceOffExit(Collision2D collision)
        {
            // Gray wall contact still wins at the seam between safe and lethal sections.
            game.CheckWallContact();
            if (game.Ended) return;
            Vector2 normal = collision.GetContact(0).normal;
            Vector2 outgoing = Body.linearVelocity;
            // Prefer the solver's response, including when both exit corners are hit together.
            if (outgoing.sqrMagnitude > 0.001f && Vector2.Dot(outgoing, normal) > 0)
                direction = outgoing.normalized;
            else if (Vector2.Dot(direction, normal) < 0)
                direction = Vector2.Reflect(direction, normal).normalized;
            Body.linearVelocity = direction * Speed;
        }
    }
}
