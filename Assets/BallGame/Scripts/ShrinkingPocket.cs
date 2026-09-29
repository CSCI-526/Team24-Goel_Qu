using UnityEngine;

namespace BallGame
{
    public sealed class ShrinkingPocket : MonoBehaviour
    {
        public const int BumperLayer = 10;
        public BallGameController game;
        public Transform upperWall, lowerWall, upperJaw, lowerJaw, rim, well, mouth;
        public Transform upperBumper, lowerBumper;
        [Min(0)] public float bumperLength = 1.2f;
        public BoxCollider2D goalTrigger;
        public bool rectangularExit;
        public float initialWidth = 2.76f;
        [Min(1)] public float shrinkInterval = 100;
        [Range(0.1f, 0.99f)] public float shrinkFactor = 0.85f;
        [Min(0.3f)] public float minimumWidth = 0.38f;
        public float Width { get; private set; } = 2.76f;
        public float SecondsUntilShrink { get; private set; } = 100;
        public int ShrinkCount { get; private set; }
        public bool AtMinimum => Width <= minimumWidth + 0.001f;
        public string CountdownText => game == null || !game.RoundStarted || game.Ended ? ""
            : AtMinimum ? "Exit at minimum size" : $"Shrinks in {Mathf.CeilToInt(SecondsUntilShrink)}s";
        GUIStyle countdownStyle;

        public void ResetOpening()
        {
            SecondsUntilShrink = shrinkInterval;
            ShrinkCount = 0;
            SetWidth(initialWidth);
        }

        void Update()
        {
            if (game == null || !game.RoundStarted || game.Ended || game.IsPaused || AtMinimum) return;
            SecondsUntilShrink -= Time.deltaTime;
            while (SecondsUntilShrink <= 0 && !AtMinimum)
            {
                SecondsUntilShrink += shrinkInterval;
                ShrinkCount++;
                SetWidth(Mathf.Max(minimumWidth, Width * shrinkFactor));
            }
        }

        void OnGUI()
        {
            if (game == null || game.InStartMenu || game.InPauseMenu) return;
            string text = CountdownText;
            if (text.Length == 0 || game.boardCamera == null) return;
            if (countdownStyle == null)
            {
                countdownStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleRight };
                countdownStyle.normal.textColor = Color.white;
            }
            countdownStyle.fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height * 0.024f), 14, 20);
            // Keep the countdown just above the opening, beside its inside edge.
            Vector3 anchor = game.boardCamera.WorldToScreenPoint(new Vector3(game.pocketEntryX, Width * 0.5f + 0.35f, 0));
            if (anchor.z <= 0) return;
            Vector2 size = countdownStyle.CalcSize(new GUIContent(text));
            float x = Mathf.Clamp(anchor.x - size.x - 10, 8, Mathf.Max(8, Screen.width - size.x - 8));
            float y = Mathf.Clamp(Screen.height - anchor.y - size.y, 8, Mathf.Max(8, Screen.height - size.y - 8));
            GUI.Label(new Rect(x, y, size.x, size.y), text, countdownStyle);
        }

        public void SetWidth(float width)
        {
            Width = Mathf.Clamp(width, minimumWidth, initialWidth);
            game.pocketWidth = Width;
            float half = Width * 0.5f;
            SetWall(upperWall, half + (upperBumper != null ? bumperLength : 0), 1);
            SetWall(lowerWall, half + (lowerBumper != null ? bumperLength : 0), -1);
            SetBumper(upperBumper, half, 1);
            SetBumper(lowerBumper, half, -1);
            if (upperJaw != null) SetY(upperJaw, half + upperJaw.localScale.y * 0.5f);
            if (lowerJaw != null) SetY(lowerJaw, -half - lowerJaw.localScale.y * 0.5f);
            if (rim != null) rim.localScale = new Vector3(rectangularExit ? 1.4f : Width + 0.15f, Width + 0.15f, 1);
            if (well != null) well.localScale = new Vector3(rectangularExit ? 1.22f : Width - 0.02f, Width - 0.02f, 1);
            if (mouth != null) mouth.localScale = new Vector3(mouth.localScale.x, Width, 1);
            if (goalTrigger != null)
            {
                // Keep even the fastest fitting ball inside the trigger for a physics step
                // after it fully crosses the wall. GoalPocket still checks the actual opening.
                float maxStep = game.launchSpeed * ElasticBall.BaseDiameter / ElasticBall.MinDiameter * Time.fixedDeltaTime;
                float triggerWidth = Mathf.Max(goalTrigger.size.x, 2 * (maxStep + initialWidth * 0.5f));
                goalTrigger.size = new Vector2(triggerWidth, Mathf.Max(0.1f, Width - 0.22f));
            }
            Physics2D.SyncTransforms();
            game.CheckWallContact();
        }

        void SetBumper(Transform bumper, float half, float sign)
        {
            if (bumper == null) return;
            var scale = bumper.localScale;
            scale.y = bumperLength;
            bumper.localScale = scale;
            SetY(bumper, sign * (half + bumperLength * 0.5f));
        }

        void SetWall(Transform wall, float half, float sign)
        {
            if (wall == null) return;
            var scale = wall.localScale;
            scale.y = game.arenaHalfSize.y - half;
            wall.localScale = scale;
            SetY(wall, sign * (game.arenaHalfSize.y + half) * 0.5f);
        }
        static void SetY(Transform part, float y)
        {
            if (part == null) return;
            var p = part.position; p.y = y; part.position = p;
        }
    }
}
