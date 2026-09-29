using UnityEngine;

namespace BallGame
{
    [ExecuteAlways, RequireComponent(typeof(Camera))]
    public sealed class ArenaCamera : MonoBehaviour
    {
        public BallGameController game;
        Camera view;

        void OnEnable() { Fit(); }
        void LateUpdate() { Fit(); }

        public void Fit()
        {
            if (game == null) return;
            if (view == null) view = GetComponent<Camera>();
            view.orthographic = true;
            float halfHeight = game.arenaHalfSize.y + 0.6f;
            float halfWidth = game.arenaHalfSize.x + 2.5f;
            // Include space beyond the exit so a successful ball remains visible.
            view.orthographicSize = Mathf.Max(halfHeight / 0.94f, halfWidth / (Mathf.Max(0.2f, view.aspect) * 0.92f));
            transform.position = new Vector3(1.5f, 0, -10);
        }
    }
}
