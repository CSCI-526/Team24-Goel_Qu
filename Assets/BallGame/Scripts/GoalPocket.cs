using UnityEngine;

namespace BallGame
{
    public sealed class GoalPocket : MonoBehaviour
    {
        public BallGameController game;
        void OnTriggerEnter2D(Collider2D other) { TryCapture(other); }
        void OnTriggerStay2D(Collider2D other) { TryCapture(other); }

        void TryCapture(Collider2D other)
        {
            if (!other.TryGetComponent<ElasticBall>(out var ball) || !ball.Moving || game.Ended) return;
            // The whole ball must fit between the two wall edges and pass the entry plane.
            float halfOpening = game.pocketWidth * 0.5f;
            if (ball.Diameter >= game.pocketWidth || Mathf.Abs(ball.Body.position.y - transform.position.y) + ball.Radius >= halfOpening) return;
            if (ball.Body.position.x < game.pocketEntryX + ball.Radius) return;
            game.RequestPocket();
        }
    }
}
