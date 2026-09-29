using System.Collections.Generic;
using UnityEngine;

namespace BallGame
{
    public sealed class DeflectionLines : MonoBehaviour
    {
        public BallGameController game;
        public Material lineMaterial;
        public PhysicsMaterial2D bounceMaterial;
        [Min(0.1f)] public float lineLifetime = 2;
        public int Count => lines.Count;
        public bool Drawing { get; private set; }
        public Vector2 StartPoint { get; private set; }

        sealed class DrawnLine
        {
            public GameObject root;
            public LineRenderer visual;
            public EdgeCollider2D edge;
            public float expiresAt;
        }

        readonly List<DrawnLine> lines = new List<DrawnLine>();
        DrawnLine drawingLine;

        public Vector2 DrawingHalfSize => game.arenaHalfSize - Vector2.one * 0.2f;
        public bool OnBoard(Vector2 point) => Mathf.Abs(point.x) <= DrawingHalfSize.x && Mathf.Abs(point.y) <= DrawingHalfSize.y;
        Vector2 ClampToBoard(Vector2 p) => new Vector2(Mathf.Clamp(p.x, -DrawingHalfSize.x, DrawingHalfSize.x), Mathf.Clamp(p.y, -DrawingHalfSize.y, DrawingHalfSize.y));

        public void Begin(Vector2 point)
        {
            if (game.Ended || game.IsPaused || (game.captureLauncher != null && game.captureLauncher.IsHoldingBall) || !OnBoard(point)) return;
            Cancel();
            StartPoint = point;
            Drawing = true;
        }

        public void Drag(Vector2 point)
        {
            ExpireLines();
            if (!Drawing || game.Ended || game.IsPaused) return;
            point = ClampToBoard(point);
            // Retain the last safe geometry if the new segment would overlap the ball.
            if (!CanPlace(StartPoint, point)) return;
            if (drawingLine == null) drawingLine = CreateLine(StartPoint, point);
            else SetGeometry(drawingLine, StartPoint, point);
        }

        // Releasing ends editing; the collider is already active and its timer keeps running.
        public bool Release(Vector2 point)
        {
            if (!Drawing) return false;
            Drag(point);
            bool created = drawingLine != null;
            Cancel();
            return created;
        }

        public bool TryAdd(Vector2 a, Vector2 b)
        {
            if (!CanPlace(a, b)) return false;
            CreateLine(a, b);
            return true;
        }

        bool CanPlace(Vector2 a, Vector2 b)
        {
            if (game.Ended || game.IsPaused || (game.captureLauncher != null && game.captureLauncher.IsHoldingBall)
                || !OnBoard(a) || !OnBoard(b) || Vector2.Distance(a, b) < 0.3f) return false;
            Vector2 delta = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(game.ball.Body.position - a, delta) / delta.sqrMagnitude);
            return Vector2.Distance(game.ball.Body.position, a + delta * t) > game.ball.Radius + 0.09f;
        }

        DrawnLine CreateLine(Vector2 a, Vector2 b)
        {
            var go = new GameObject("DEFLECTOR • player drawn");
            go.layer = 9;
            go.transform.SetParent(transform);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = lineMaterial;
            line.positionCount = 2;
            line.useWorldSpace = true;
            line.startWidth = line.endWidth = 0.07f;
            line.numCapVertices = 8;
            line.sortingOrder = 21;
            line.startColor = line.endColor = Color.white;
            var edge = go.AddComponent<EdgeCollider2D>();
            edge.edgeRadius = 0.035f;
            edge.sharedMaterial = bounceMaterial;
            var drawn = new DrawnLine { root = go, visual = line, edge = edge, expiresAt = Time.time + lineLifetime };
            SetGeometry(drawn, a, b);
            lines.Add(drawn);
            return drawn;
        }

        static void SetGeometry(DrawnLine line, Vector2 a, Vector2 b)
        {
            line.visual.SetPosition(0, a);
            line.visual.SetPosition(1, b);
            line.edge.points = new[] { a, b };
            Physics2D.SyncTransforms();
        }

        void Update() { ExpireLines(); }
        void FixedUpdate() { ExpireLines(); }

        void ExpireLines()
        {
            for (int i = lines.Count - 1; i >= 0; i--)
                if (lines[i].root == null || Time.time >= lines[i].expiresAt) RemoveAt(i);
        }

        public bool Consume(GameObject line)
        {
            int index = lines.FindIndex(drawn => drawn.root == line);
            if (index < 0) return false;
            RemoveAt(index);
            return true;
        }

        void RemoveAt(int index)
        {
            var line = lines[index];
            // End a consumed/expired gesture so holding the mouse cannot recreate it.
            if (drawingLine == line) Cancel();
            lines.RemoveAt(index);
            if (line.root == null) return;
            line.root.SetActive(false);
            Destroy(line.root);
        }

        public void Cancel()
        {
            Drawing = false;
            drawingLine = null;
        }

        public void Clear()
        {
            Cancel();
            for (int i = lines.Count - 1; i >= 0; i--) RemoveAt(i);
        }
    }
}
