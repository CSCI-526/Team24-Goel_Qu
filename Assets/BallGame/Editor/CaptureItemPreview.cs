using UnityEditor;
using UnityEngine;

namespace BallGame.Editor
{
    [InitializeOnLoad]
    public static class CaptureItemPreview
    {
        const string Pending = "BallGame.CapturePreview.Pending";

        static CaptureItemPreview()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
                SessionState.SetBool(Pending, false);
                EditorApplication.delayCall += Prepare;
            };
        }

        [MenuItem("Ball Game/Preview Capture Controls")]
        public static void Run()
        {
            if (EditorApplication.isPlaying) { Prepare(); return; }
            SessionState.SetBool(Pending, true);
            EditorApplication.EnterPlaymode();
        }

        static void Prepare()
        {
            var game = Object.FindFirstObjectByType<BallGameController>();
            if (!EditorApplication.isPlaying || game == null || game.captureLauncher == null) return;
            game.InputEnabled = true;
            game.ResetBall(false);
            game.captureLauncher.SpawnAt(new Vector2(6, 0));
            game.Shoot(Vector2.right);
        }
    }
}
