using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace BallGame
{
    public sealed class BallGameController : MonoBehaviour
    {
        public ElasticBall ball;
        public Camera boardCamera;
        public LineRenderer aimLine;
        public TrailRenderer trail;
        public Transform pocketCenter;
        public PowerUpSpawner powerUps;
        public DeflectionLines deflectionLines;
        public ShrinkingPocket shrinkingPocket;
        public CaptureLauncher captureLauncher;
        [Tooltip("Speed at the 0.5-unit reference diameter. Actual speed is this value times 0.5 divided by ball diameter.")]
        [Range(2, 60)] public float launchSpeed = 30.24f;
        [Range(ElasticBall.MinDiameter, ElasticBall.MaxDiameter)] public float initialBallDiameter = 3.2f;
        public Vector3 startPosition = Vector3.zero;
        [Header("Arena dimensions (half extents)")]
        public Vector2 arenaHalfSize = new Vector2(18, 9.9f);
        [HideInInspector] public int arenaVersion;
        public float pocketWidth = 2.76f;
        public float pocketEntryX = 18;
        [HideInInspector] public int rulesVersion;
        public const int FinalLevel = 2;
        public int CurrentLevel { get; private set; } = 1;
        public string LevelTitle => CurrentLevel == 1 ? "LEVEL ONE" : "LEVEL TWO";
        public int DeathCount { get; private set; }
        public string DeathCountLabel => "Deaths: " + DeathCount;
        public bool Won { get; private set; }
        public bool Lost { get; private set; }
        public bool ExitComplete { get; private set; }
        public string RestartPrompt => Lost ? "Press R to play again" : "";
        public bool Ended => Won || Lost;
        public bool RoundStarted { get; private set; }
        public bool IsPaused { get; private set; }
        public bool InStartMenu { get; private set; }
        public bool InPauseMenu { get; private set; }
        public bool InputEnabled { get; set; } = true;
        public int PickupsCollected { get; private set; }
        public int Deflections { get; private set; }
        public bool FitsPocket => ball.Diameter < pocketWidth;
        bool pocketRequested;
        GUIStyle restartStyle;
        GUIStyle levelStyle;
        GUIStyle deathCountStyle;
        GUIStyle menuTitleStyle;
        GUIStyle menuCreditStyle;
        GUIStyle menuButtonStyle;
        bool pausedBeforeMenu;
        float timeScaleBeforeMenu = 1;

        void Start()
        {
            CurrentLevel = 1;
            ShowStartMenu();
        }

        public void ShowStartMenu()
        {
            StopAllCoroutines();
            Time.timeScale = 1;
            InStartMenu = true;
            ClearPauseMenu();
            IsPaused = Won = Lost = RoundStarted = pocketRequested = ExitComplete = false;
            PickupsCollected = Deflections = 0;
            if (captureLauncher != null) captureLauncher.ResetLauncher();
            ball.Stop();
            ball.gameObject.SetActive(false);
            trail.Clear(); trail.emitting = false;
            deflectionLines.Clear();
            powerUps.ResetSpawner();
            shrinkingPocket.ResetOpening();
            aimLine.enabled = false;
        }

        public void StartLevel(int level, bool launchImmediately = true)
        {
            CurrentLevel = Mathf.Clamp(level, 1, FinalLevel);
            InStartMenu = false;
            ResetBall(launchImmediately);
        }

        public void ResetBall(bool launchImmediately = true)
        {
            StopAllCoroutines();
            Time.timeScale = 1;
            InStartMenu = false;
            ClearPauseMenu();
            IsPaused = Won = Lost = RoundStarted = pocketRequested = ExitComplete = false;
            PickupsCollected = Deflections = 0;
            if (captureLauncher != null) captureLauncher.ResetLauncher();
            ball.gameObject.SetActive(true);
            ball.ResetSize(launchSpeed, initialBallDiameter);
            ball.transform.position = startPosition;
            ball.Body.position = startPosition;
            trail.Clear(); trail.emitting = false;
            deflectionLines.Clear();
            powerUps.ResetSpawner();
            shrinkingPocket.ResetOpening();
            Physics2D.SyncTransforms();
            aimLine.enabled = false;
            if (launchImmediately)
            {
                float angle = Random.Range(0f, Mathf.PI * 2);
                Shoot(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)));
            }
        }

        public void Shoot(Vector3 direction)
        {
            if (ball.Moving || Ended || IsPaused || (captureLauncher != null && captureLauncher.IsHoldingBall)) return;
            ball.Launch(direction, launchSpeed);
            RoundStarted = true;
            aimLine.enabled = false;
            trail.Clear(); trail.emitting = true;
            CheckWallContact();
        }

        void Update()
        {
            if (!InputEnabled || InStartMenu) return;
            bool down = false, up = false, held = false, reset = false, pause = false, clear = false, escape = false;
            Vector2 mouse = Vector2.zero;
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                mouse = Mouse.current.position.ReadValue();
                down = Mouse.current.leftButton.wasPressedThisFrame;
                up = Mouse.current.leftButton.wasReleasedThisFrame;
                held = Mouse.current.leftButton.isPressed;
            }
            if (Keyboard.current != null)
            {
                reset = Keyboard.current.rKey.wasPressedThisFrame;
                pause = Keyboard.current.pKey.wasPressedThisFrame;
                clear = Keyboard.current.cKey.wasPressedThisFrame;
                escape = Keyboard.current.escapeKey.wasPressedThisFrame;
            }
#else
            mouse = Input.mousePosition;
            down = Input.GetMouseButtonDown(0); up = Input.GetMouseButtonUp(0); held = Input.GetMouseButton(0);
            reset = Input.GetKeyDown(KeyCode.R);
            pause = Input.GetKeyDown(KeyCode.P); clear = Input.GetKeyDown(KeyCode.C);
            escape = Input.GetKeyDown(KeyCode.Escape);
#endif
            if (escape) { TogglePauseMenu(); return; }
            if (InPauseMenu) return;
            if (reset) { ResetBall(); return; }
            if (pause && RoundStarted && !Ended) TogglePause();
            if (Ended || IsPaused) { deflectionLines.Cancel(); return; }
            if (clear) deflectionLines.Clear();
            Vector2 world = boardCamera.ScreenToWorldPoint(new Vector3(mouse.x, mouse.y, -boardCamera.transform.position.z));
            if (captureLauncher != null && captureLauncher.IsHoldingBall)
            {
                if (down) captureLauncher.BeginAim(world);
                if (held) captureLauncher.DragAim(world);
                if (up) captureLauncher.ReleaseAim(world);
                return;
            }
            bool overBoard = deflectionLines.OnBoard(world);
            if (down && overBoard) deflectionLines.Begin(world);
            if (held && deflectionLines.Drawing) deflectionLines.Drag(world);
            if (up && deflectionLines.Drawing) deflectionLines.Release(world);
        }

        void LateUpdate()
        {
            if (InStartMenu || IsPaused) return;
            // Resolve the goal after physics callbacks, so wall contact always wins.
            if (!pocketRequested)
            {
                if (captureLauncher != null) captureLauncher.ResolveCapture();
                return;
            }
            pocketRequested = false;
            if (Ended) return;
            CheckWallContact();
            if (Ended) return;
            Won = true;
            StopRound();
            StartCoroutine(ExitBoard());
        }

        public void RequestPocket() { if (!Ended && ball.Moving && FitsPocket) pocketRequested = true; }
        public void RegisterDeflection() { if (!Ended) Deflections++; }

        public void CollectPowerUp(SizeEffect effect)
        {
            if (Ended || !ball.Moving) return;
            float factor = effect == SizeEffect.Shrink ? 0.75f : 1f / 0.75f;
            ball.SetDiameter(ball.Diameter * factor);
            PickupsCollected++;
            Physics2D.SyncTransforms();
            CheckWallContact();
        }

        public void CheckWallContact()
        {
            if (Ended || !ball.Moving) return;
            if (Physics2D.OverlapCircle(ball.Body.position, ball.Radius, 1 << 8) != null) BurnBall();
        }

        public void BurnBall()
        {
            if (Ended || IsPaused || InStartMenu || !ball.Moving) return;
            DeathCount++;
            Lost = true;
            StopRound();
            StartCoroutine(BurnAnimation());
        }

        void StopRound()
        {
            ball.Stop();
            ball.GetComponent<CircleCollider2D>().enabled = false;
            trail.emitting = false;
            aimLine.enabled = false;
            powerUps.Clear();
            deflectionLines.Cancel();
            if (captureLauncher != null) captureLauncher.Clear();
        }

        IEnumerator ExitBoard()
        {
            Vector3 from = ball.Body.position;
            // Continue beyond the open gate and leave the full-size ball visible outside.
            float outsideX = Mathf.Max(from.x + 1.5f, pocketEntryX + 1.2f + ball.Radius + 0.65f);
            Vector3 destination = new Vector3(outsideX, from.y, 0);
            for (float t = 0; t < 1; t += Time.deltaTime / 0.6f)
            {
                while (IsPaused) yield return null;
                Vector3 position = Vector3.Lerp(from, destination, t);
                ball.Body.position = position;
                ball.transform.position = position;
                yield return null;
            }
            while (IsPaused) yield return null;
            ball.Body.position = destination;
            ball.transform.position = destination;
            ExitComplete = true;
            if (CurrentLevel < FinalLevel)
            {
                yield return new WaitForSeconds(0.65f);
                while (IsPaused) yield return null;
                StartLevel(CurrentLevel + 1);
            }
        }

        IEnumerator BurnAnimation()
        {
            var ballRenderer = ball.GetComponent<SpriteRenderer>();
            float size = ball.Diameter;
            for (float t = 0; t < 1; t += Time.deltaTime * 1.5f)
            {
                while (IsPaused) yield return null;
                ballRenderer.color = new Color(1, 1, 1, 1 - t);
                ball.transform.localScale = Vector3.one * size * (1 - t);
                yield return null;
            }
            while (IsPaused) yield return null;
            ball.gameObject.SetActive(false);
        }

        public void TogglePause()
        {
            if (InPauseMenu || !RoundStarted || Ended) return;
            IsPaused = !IsPaused;
            if (IsPaused)
            {
                deflectionLines.Cancel();
                if (captureLauncher != null) captureLauncher.CancelAim();
            }
            Time.timeScale = IsPaused ? 0 : 1;
        }

        public void OpenPauseMenu()
        {
            if (InStartMenu || InPauseMenu) return;
            pausedBeforeMenu = IsPaused;
            timeScaleBeforeMenu = Time.timeScale;
            InPauseMenu = IsPaused = true;
            Time.timeScale = 0;
            deflectionLines.Cancel();
            if (captureLauncher != null) captureLauncher.CancelAim();
        }

        public void ClosePauseMenu()
        {
            if (!InPauseMenu) return;
            IsPaused = pausedBeforeMenu;
            Time.timeScale = timeScaleBeforeMenu;
            ClearPauseMenu();
        }

        public void TogglePauseMenu()
        {
            if (InPauseMenu) ClosePauseMenu();
            else OpenPauseMenu();
        }

        void ClearPauseMenu()
        {
            InPauseMenu = false;
            pausedBeforeMenu = false;
            timeScaleBeforeMenu = 1;
        }

        void OnGUI()
        {
            if (InStartMenu)
            {
                DrawStartMenu();
                return;
            }
            if (InPauseMenu)
            {
                DrawPauseMenu();
                return;
            }
            DrawHud();
            if (RestartPrompt.Length == 0) return;
            if (restartStyle == null)
            {
                restartStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
                restartStyle.normal.textColor = Color.white;
            }
            restartStyle.fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height * 0.035f), 18, 32);
            GUI.Label(new Rect(0, Screen.height * 0.5f - 32, Screen.width, 64), RestartPrompt, restartStyle);
        }

        void DrawHud()
        {
            if (levelStyle == null)
            {
                levelStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft, fontStyle = FontStyle.Bold };
                levelStyle.normal.textColor = Color.white;
                deathCountStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleRight };
                deathCountStyle.normal.textColor = Color.white;
            }
            float margin = Mathf.Max(16, Screen.width * 0.03f);
            float top = Mathf.Max(8, Screen.height * 0.015f);
            levelStyle.fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height * 0.045f), 24, 48);
            deathCountStyle.fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height * 0.03f), 18, 30);
            float height = levelStyle.fontSize * 1.3f;
            GUI.Label(new Rect(margin, top, Screen.width * 0.6f - margin, height), LevelTitle, levelStyle);
            GUI.Label(new Rect(Screen.width * 0.62f, top, Screen.width * 0.38f - margin, height), DeathCountLabel, deathCountStyle);
        }

        void EnsureMenuStyles()
        {
            if (menuTitleStyle == null)
            {
                menuTitleStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
                menuTitleStyle.normal.textColor = Color.white;
                menuCreditStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
                menuCreditStyle.normal.textColor = new Color(0.78f, 0.86f, 0.82f);
                menuButtonStyle = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            }
        }

        void DrawPauseMenu()
        {
            EnsureMenuStyles();
            Color previousColor = GUI.color;
            GUI.color = new Color(0, 0, 0, 0.88f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = previousColor;
            DrawHud();

            menuTitleStyle.fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height * 0.07f), 30, 64);
            menuButtonStyle.fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height * 0.032f), 17, 25);
            float buttonWidth = Mathf.Min(400, Screen.width - 40);
            float buttonX = (Screen.width - buttonWidth) * 0.5f;
            float buttonHeight = Mathf.Min(60, Screen.height * 0.09f);
            float gap = Mathf.Min(16, Screen.height * 0.02f);
            float blockHeight = buttonHeight * 4 + gap * 3;
            float buttonY = Mathf.Max(100, (Screen.height - blockHeight) * 0.5f + 35);
            GUI.Label(new Rect(0, buttonY - 85, Screen.width, 65), "PAUSED", menuTitleStyle);
            if (GUI.Button(new Rect(buttonX, buttonY, buttonWidth, buttonHeight), "RESUME", menuButtonStyle)) { ClosePauseMenu(); return; }
            buttonY += buttonHeight + gap;
            if (GUI.Button(new Rect(buttonX, buttonY, buttonWidth, buttonHeight), "LEVEL ONE", menuButtonStyle)) { StartLevel(1); return; }
            buttonY += buttonHeight + gap;
            if (GUI.Button(new Rect(buttonX, buttonY, buttonWidth, buttonHeight), "LEVEL TWO", menuButtonStyle)) { StartLevel(2); return; }
            buttonY += buttonHeight + gap;
            if (GUI.Button(new Rect(buttonX, buttonY, buttonWidth, buttonHeight), "MAIN MENU", menuButtonStyle)) ShowStartMenu();
        }

        void DrawStartMenu()
        {
            EnsureMenuStyles();

            Color previousColor = GUI.color;
            GUI.color = new Color(0.035f, 0.075f, 0.07f, 1);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = previousColor;

            menuTitleStyle.fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height * 0.09f), 38, 72);
            menuCreditStyle.fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height * 0.028f), 16, 23);
            menuButtonStyle.fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height * 0.032f), 17, 25);

            float titleY = Mathf.Max(54, Screen.height * 0.18f);
            GUI.Label(new Rect(20, titleY, Screen.width - 40, 90), "BALL GAME", menuTitleStyle);
            GUI.Label(new Rect(20, titleY + 86, Screen.width - 40, 42), "Created by Manu Goel and Yuan Qu", menuCreditStyle);

            float buttonWidth = Mathf.Min(300, Screen.width - 40);
            float buttonX = (Screen.width - buttonWidth) * 0.5f;
            float buttonY = Mathf.Min(Mathf.Max(titleY + 170, Screen.height * 0.53f), Screen.height - 148);
            if (GUI.Button(new Rect(buttonX, buttonY, buttonWidth, 56), "LEVEL ONE", menuButtonStyle)) StartLevel(1);
            if (GUI.Button(new Rect(buttonX, buttonY + 72, buttonWidth, 56), "LEVEL TWO", menuButtonStyle)) StartLevel(2);
        }

        void OnDestroy() { Time.timeScale = 1; }
    }
}
