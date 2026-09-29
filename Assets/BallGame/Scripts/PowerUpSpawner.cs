using System.Collections.Generic;
using UnityEngine;

namespace BallGame
{
    public sealed class PowerUpSpawner : MonoBehaviour
    {
        public BallGameController game;
        public Sprite circleSprite;
        public Sprite squareSprite;
        public Material spriteMaterial;
        [Min(0.1f)] public float spawnInterval = 2;
        [Min(0)] public float firstSpawnDelay = 0.6f;
        [Min(1)] public float shrinkPickupLifetime = 12;
        [Min(1)] public float growPickupLifetime = 5;
        [Range(1, 8)] public int maxPickups = 4;
        [Min(0)] public float levelTwoMoveSpeed = 2.5f;
        public bool AutoSpawn { get; set; } = true;
        public int ActiveCount { get { Prune(); return pickups.Count; } }
        public float NextSpawnIn => Mathf.Max(0, remaining);
        readonly List<SizePowerUp> pickups = new List<SizePowerUp>();
        float remaining;
        SizeEffect next = SizeEffect.Shrink;

        public void ResetSpawner()
        {
            Clear();
            remaining = firstSpawnDelay;
            next = SizeEffect.Shrink;
        }

        public void Clear()
        {
            foreach (var pickup in pickups)
                if (pickup != null) { pickup.gameObject.SetActive(false); Destroy(pickup.gameObject); }
            pickups.Clear();
        }

        void Prune() { pickups.RemoveAll(p => p == null || !p.gameObject.activeSelf); }

        void Update()
        {
            if (!AutoSpawn || game == null || !game.RoundStarted || game.Ended || game.IsPaused) return;
            Prune();
            remaining -= Time.deltaTime;
            if (remaining > 0) return;
            remaining = spawnInterval;
            if (pickups.Count >= maxPickups) return;
            for (int attempt = 0; attempt < 30; attempt++)
            {
                Vector2 limit = game.arenaHalfSize - Vector2.one * 1.2f;
                Vector2 point = new Vector2(Random.Range(-limit.x, limit.x), Random.Range(-limit.y, limit.y));
                if (Vector2.Distance(point, game.ball.transform.position) < game.ball.Radius + 0.7f) continue;
                if (Physics2D.OverlapCircle(point, 0.6f, 1 << 8) != null) continue;
                bool occupied = pickups.Exists(p => Vector2.Distance(point, p.transform.position) < 1.0f);
                if (occupied) continue;
                SpawnAt(point, next);
                next = next == SizeEffect.Shrink ? SizeEffect.Grow : SizeEffect.Shrink;
                break;
            }
        }

        public SizePowerUp SpawnAt(Vector2 position, SizeEffect effect)
        {
            if (game == null || game.Ended) return null;
            var go = new GameObject(effect == SizeEffect.Shrink ? "POWER UP • shrink / faster" : "POWER UP • grow / slower");
            go.transform.SetParent(transform);
            go.transform.position = position;
            var trigger = go.AddComponent<CircleCollider2D>();
            trigger.isTrigger = true;
            trigger.radius = 0.34f;
            var pickup = go.AddComponent<SizePowerUp>();
            pickup.effect = effect; pickup.game = game;
            pickup.lifetime = effect == SizeEffect.Shrink ? shrinkPickupLifetime : growPickupLifetime;
            if (game.CurrentLevel >= 2)
            {
                Vector2 direction = Random.insideUnitCircle.normalized;
                pickup.moveDirection = direction.sqrMagnitude > 0.01f ? direction : Vector2.right;
                pickup.moveSpeed = levelTwoMoveSpeed;
            }
            var visual = new GameObject("Icon").transform;
            visual.SetParent(go.transform, false);
            pickup.visual = visual;
            Draw("Ring", visual, circleSprite, Vector2.one * 0.67f, Color.white, 16);
            Draw("Center", visual, circleSprite, Vector2.one * 0.57f, new Color(0.08f, 0.08f, 0.08f), 17);
            Draw("Minus", visual, squareSprite, new Vector2(0.30f, 0.065f), Color.white, 18);
            if (effect == SizeEffect.Grow) Draw("Plus", visual, squareSprite, new Vector2(0.065f, 0.30f), Color.white, 18);
            pickups.Add(pickup);
            return pickup;
        }

        void Draw(string name, Transform parent, Sprite sprite, Vector2 size, Color color, int order)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            go.transform.localScale = new Vector3(size.x, size.y, 1);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite; renderer.sharedMaterial = spriteMaterial;
            renderer.color = color; renderer.sortingOrder = order;
        }
    }
}
