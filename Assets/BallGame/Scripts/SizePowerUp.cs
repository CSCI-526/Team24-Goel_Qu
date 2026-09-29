using UnityEngine;

namespace BallGame
{
    public enum SizeEffect { Shrink, Grow }

    [RequireComponent(typeof(CircleCollider2D))]
    public sealed class SizePowerUp : MonoBehaviour
    {
        public SizeEffect effect;
        public BallGameController game;
        public float lifetime = 8;
        public Transform visual;
        [Min(0)] public float moveSpeed;
        public Vector2 moveDirection;
        float age;
        bool collected;

        void Update()
        {
            if (game == null || game.Ended || game.IsPaused) return;
            age += Time.deltaTime;
            if (age >= lifetime) { gameObject.SetActive(false); Destroy(gameObject); return; }
            Move();
            if (visual != null)
            {
                float pulse = 1 + Mathf.Sin(age * 4) * 0.05f;
                if (lifetime - age < 1.5f) pulse *= 0.88f + 0.12f * Mathf.Sin(age * 18);
                visual.localScale = Vector3.one * pulse;
            }
        }

        void Move()
        {
            if (moveSpeed <= 0 || moveDirection.sqrMagnitude < 0.01f) return;
            Vector2 limit = game.arenaHalfSize - Vector2.one * 0.55f;
            Vector2 position = transform.position;
            position += moveDirection.normalized * moveSpeed * Time.deltaTime;
            if (Mathf.Abs(position.x) > limit.x)
            {
                position.x = Mathf.Clamp(position.x, -limit.x, limit.x);
                moveDirection.x *= -1;
            }
            if (Mathf.Abs(position.y) > limit.y)
            {
                position.y = Mathf.Clamp(position.y, -limit.y, limit.y);
                moveDirection.y *= -1;
            }
            transform.position = new Vector3(position.x, position.y, transform.position.z);
        }

        void OnTriggerEnter2D(Collider2D other) { TryCollect(other); }
        void OnTriggerStay2D(Collider2D other) { TryCollect(other); }

        void TryCollect(Collider2D other)
        {
            if (collected || game == null || game.Ended || game.IsPaused) return;
            if (!other.TryGetComponent<ElasticBall>(out var ball) || !ball.Moving) return;
            collected = true;
            GetComponent<Collider2D>().enabled = false;
            game.CollectPowerUp(effect);
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }
}
