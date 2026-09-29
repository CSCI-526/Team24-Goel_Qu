using UnityEngine;

namespace BallGame
{
    public sealed class CaptureLauncher : MonoBehaviour
    {
        public BallGameController game;
        [Min(0)] public float firstSpawnDelay = 1.2f;
        [Min(1)] public float respawnDelay = 12;
        [Min(1)] public float itemLifetime = 15;
        public bool AutoSpawn { get; set; } = true;
        public bool IsHoldingBall { get; private set; }
        public bool IsAiming { get; private set; }
        public bool HasPoint => point != null && point.gameObject.activeSelf;
        public Vector2 AimDirection { get; private set; }
        public float NextSpawnIn => Mathf.Max(0, remaining);
        const int SolidMask = (1 << 8) | (1 << 9) | (1 << ShrinkingPocket.BumperLayer);
        static readonly Color Cyan = new Color(0.25f, 0.85f, 1);
        CapturePoint point, pending;
        Vector2 aimStart;
        float remaining, age;
        GUIStyle hintStyle;

        public void ResetLauncher()
        {
            Clear();
            remaining = firstSpawnDelay;
        }

        public void Clear()
        {
            CancelAim();
            IsHoldingBall = false;
            pending = null;
            RemovePoint();
        }

        void RemovePoint()
        {
            if (point != null) { point.gameObject.SetActive(false); Destroy(point.gameObject); }
            point = null;
            age = 0;
            remaining = respawnDelay;
        }

        void Update()
        {
            if (game == null || !game.RoundStarted || game.Ended || game.IsPaused || IsHoldingBall) return;
            if (HasPoint)
            {
                age += Time.deltaTime;
                if (pending == null && age >= itemLifetime) RemovePoint();
                return;
            }
            if (!AutoSpawn) return;
            remaining -= Time.deltaTime;
            if (remaining > 0) return;
            remaining = 1;
            // Even a maximum-size ball can safely snap to the center of the item.
            Vector2 limit = game.arenaHalfSize - Vector2.one * (ElasticBall.MaxDiameter * 0.5f + 0.5f);
            for (int attempt = 0; attempt < 30; attempt++)
            {
                Vector2 candidate = new Vector2(Random.Range(-limit.x, limit.x), Random.Range(-limit.y, limit.y));
                if (Vector2.Distance(candidate, game.ball.Body.position) < game.ball.Radius + 1) continue;
                if (Physics2D.OverlapCircle(candidate, ElasticBall.MaxDiameter * 0.5f + 0.1f, SolidMask) != null) continue;
                bool occupied = false;
                foreach (var pickup in game.powerUps.GetComponentsInChildren<SizePowerUp>())
                    if (Vector2.Distance(candidate, pickup.transform.position) < 1.3f) { occupied = true; break; }
                if (occupied) continue;
                SpawnAt(candidate);
                break;
            }
        }

        public CapturePoint SpawnAt(Vector2 position)
        {
            if (game == null || game.Ended || IsHoldingBall) return null;
            float margin = ElasticBall.MaxDiameter * 0.5f + 0.35f;
            if (Mathf.Abs(position.x) > game.arenaHalfSize.x - margin
                || Mathf.Abs(position.y) > game.arenaHalfSize.y - margin
                || Physics2D.OverlapCircle(position, margin, SolidMask) != null) return null;
            RemovePoint();
            var go = new GameObject("CAPTURE POINT • aim and shoot");
            go.transform.SetParent(transform);
            go.transform.position = position;
            var trigger = go.AddComponent<CircleCollider2D>();
            trigger.isTrigger = true; trigger.radius = CapturePoint.Radius;
            point = go.AddComponent<CapturePoint>();
            point.launcher = this;
            var ring = go.AddComponent<LineRenderer>();
            ring.sharedMaterial = game.deflectionLines.lineMaterial;
            ring.useWorldSpace = false; ring.loop = true;
            ring.startWidth = ring.endWidth = 0.065f;
            ring.startColor = ring.endColor = Cyan; ring.sortingOrder = 23;
            point.ring = ring; point.SetRingRadius(CapturePoint.Radius);
            var dot = new GameObject("Center dot");
            dot.transform.SetParent(go.transform, false);
            dot.transform.localScale = Vector3.one * 0.16f;
            var sprite = dot.AddComponent<SpriteRenderer>();
            sprite.sprite = game.powerUps.circleSprite;
            sprite.sharedMaterial = game.powerUps.spriteMaterial;
            sprite.color = Cyan; sprite.sortingOrder = 24;
            Physics2D.SyncTransforms();
            return point;
        }

        public void RequestCapture(CapturePoint candidate)
        {
            if (candidate == point && !IsHoldingBall && game.ball.Moving && !game.Ended && !game.IsPaused)
                pending = candidate;
        }

        public void ResolveCapture()
        {
            var candidate = pending;
            pending = null;
            if (candidate != null) TryHold(candidate);
        }

        public bool TryCaptureAlongStep(Vector2 start, Vector2 displacement)
        {
            if (!HasPoint || IsHoldingBall || game.Ended || game.IsPaused || !game.ball.Moving) return false;
            float radius = CapturePoint.Radius + game.ball.Radius;
            Vector2 offset = start - (Vector2)point.transform.position;
            float c = offset.sqrMagnitude - radius * radius;
            float distance = 0;
            if (c > 0)
            {
                float a = displacement.sqrMagnitude;
                if (a < 0.000001f) return false;
                float b = Vector2.Dot(offset, displacement);
                float discriminant = b * b - a * c;
                if (discriminant < 0) return false;
                float fraction = (-b - Mathf.Sqrt(discriminant)) / a;
                if (fraction < 0 || fraction > 1) return false;
                distance = displacement.magnitude * fraction;
            }
            // A wall or drawn line met before the item must get the collision first.
            if (Physics2D.CircleCast(start, game.ball.Radius, displacement.normalized, distance + 0.001f, SolidMask).collider != null)
                return false;
            return TryHold(point);
        }

        bool TryHold(CapturePoint candidate)
        {
            if (candidate != point || !HasPoint || IsHoldingBall || game.Ended || game.IsPaused || !game.ball.Moving) return false;
            game.CheckWallContact();
            if (game.Ended) return false;
            Vector2 center = point.transform.position;
            if (Physics2D.OverlapCircle(center, game.ball.Radius + 0.04f, SolidMask) != null) return false;
            Vector2 toCenter = center - game.ball.Body.position;
            if (toCenter.sqrMagnitude > 0.000001f && Physics2D.CircleCast(game.ball.Body.position,
                game.ball.Radius, toCenter.normalized, toCenter.magnitude, SolidMask).collider != null) return false;
            game.deflectionLines.Cancel();
            CancelAim();
            game.ball.Stop();
            game.ball.Body.position = center;
            game.ball.transform.position = center;
            game.trail.Clear(); game.trail.emitting = false;
            point.GetComponent<CircleCollider2D>().enabled = false;
            point.SetRingRadius(game.ball.Radius + 0.2f);
            IsHoldingBall = true;
            pending = null;
            Physics2D.SyncTransforms();
            return true;
        }

        public bool BeginAim(Vector2 position)
        {
            if (!IsHoldingBall || game.Ended || game.IsPaused
                || Vector2.Distance(position, game.ball.Body.position) > game.ball.Radius + 0.25f) return false;
            IsAiming = true;
            aimStart = position;
            DragAim(position);
            return true;
        }

        public void DragAim(Vector2 position)
        {
            if (!IsAiming || !IsHoldingBall || game.IsPaused || game.Ended) return;
            Vector2 pull = aimStart - position;
            AimDirection = pull.magnitude >= 0.25f ? pull.normalized : Vector2.zero;
            var guide = game.aimLine;
            guide.enabled = AimDirection != Vector2.zero;
            if (!guide.enabled) return;
            Vector2 origin = game.ball.Body.position + AimDirection * (game.ball.Radius + 0.15f);
            Vector2 end = origin + AimDirection * 3;
            Vector2 side = new Vector2(-AimDirection.y, AimDirection.x);
            guide.positionCount = 5;
            guide.SetPositions(new[] { (Vector3)origin, (Vector3)end,
                (Vector3)(end - AimDirection * 0.4f + side * 0.25f), (Vector3)end,
                (Vector3)(end - AimDirection * 0.4f - side * 0.25f) });
            guide.startColor = guide.endColor = Cyan;
            guide.startWidth = guide.endWidth = 0.055f;
            guide.sortingOrder = 26;
        }

        public bool ReleaseAim(Vector2 position)
        {
            if (!IsAiming || !IsHoldingBall || game.IsPaused || game.Ended) return false;
            DragAim(position);
            Vector2 heading = AimDirection;
            CancelAim();
            if (heading == Vector2.zero) return false;
            IsHoldingBall = false;
            pending = null;
            RemovePoint();
            game.Shoot(heading);
            return game.ball.Moving;
        }

        public void CancelAim()
        {
            IsAiming = false; AimDirection = Vector2.zero;
            if (game != null && game.aimLine != null) game.aimLine.enabled = false;
        }

        void OnGUI()
        {
            if (!IsHoldingBall || game.Ended || game.IsPaused) return;
            if (hintStyle == null)
            {
                hintStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
                hintStyle.normal.textColor = Cyan;
            }
            hintStyle.fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height * 0.024f), 14, 20);
            Vector3 screen = game.boardCamera.WorldToScreenPoint(game.ball.transform.position + Vector3.up * (game.ball.Radius + 0.8f));
            const float width = 300;
            GUI.Label(new Rect(Mathf.Clamp(screen.x - width * 0.5f, 8, Mathf.Max(8, Screen.width - width - 8)),
                Mathf.Clamp(Screen.height - screen.y - 40, 8, Mathf.Max(8, Screen.height - 48)), width, 44),
                "Drag back to aim\nRelease to shoot", hintStyle);
        }
    }
}
