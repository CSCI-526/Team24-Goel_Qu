using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace BallGame.Editor
{
    [InitializeOnLoad]
    public static class BallGameChecks
    {
        const string Pending = "BallGame.Checks.Pending";
        static BallGameController game;
        static GameObject consumedLine, timedLine;
        static SizePowerUp timedShrinkPickup, timedGrowPickup;
        static SizePowerUp levelTwoPickup;
        static CapturePoint checkedCapturePoint;
        static int step, wallIndex, goalApproachIndex, bumperProbeIndex, deathsBeforeWalls, deathsBeforeWin;
        static int[] bumperIds;
        static Vector2 expectedBumperDirection, levelTwoPickupStart, pauseBallPosition;
        static float started, pauseHole, pauseSpawn, pauseCaptureSpawn, lineStarted, pausedGameTime, exitStartX, capturedHole;
        static double deadline, pauseAt;
        static StringBuilder report;
        static string pauseCountdown;
        static UnityEngine.Random.State randomState;
        static bool randomStateSaved;
        static Vector2[] WallPositions => new[] {
            new Vector2(0, game.arenaHalfSize.y - 0.7f), new Vector2(0, -game.arenaHalfSize.y + 0.7f),
            new Vector2(-game.arenaHalfSize.x + 0.7f, 0), new Vector2(game.arenaHalfSize.x - 0.7f, 4),
            new Vector2(game.arenaHalfSize.x - 0.7f, -4) };
        static readonly Vector2[] WallDirections = { Vector2.up, Vector2.down, Vector2.left, Vector2.right, Vector2.right };
        static readonly float[] GoalApproachDistances = { 3, 3.3f, 3.6f };

        static BallGameChecks()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Pending, false))
                {
                    game = null; step = wallIndex = 0;
                    report = new StringBuilder("BALL GAME — CURRENT RULE CHECKS\n" + DateTime.Now.ToString("u") + "\n\n");
                    deadline = EditorApplication.timeSinceStartup + 75;
                    EditorApplication.update += Tick;
                }
                if (state == PlayModeStateChange.ExitingPlayMode) EditorApplication.update -= Tick;
            };
        }

        [MenuItem("Ball Game/Run Game Rule Checks")]
        public static void Run()
        {
            if (EditorApplication.isPlaying) return;
            SessionState.SetBool(Pending, true);
            EditorApplication.EnterPlaymode();
        }

        public static void RunBatch()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(BallGameSetup.ScenePath);
            Run();
        }

        static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Timed out at step " + step);
                if (game == null) game = UnityEngine.Object.FindFirstObjectByType<BallGameController>();
                if (game == null) return;
                if (step == 0)
                {
                    randomState = UnityEngine.Random.state;
                    randomStateSaved = true;
                    UnityEngine.Random.InitState(526);
                    Require(game.InStartMenu && !game.RoundStarted && !game.ball.gameObject.activeSelf,
                        "The game opens on the level-select start page without running gameplay behind it");
                    game.BurnBall(); game.TogglePauseMenu();
                    Require(game.DeathCount == 0 && game.DeathCountLabel == "Deaths: 0" && !game.InPauseMenu,
                        "A new session starts at zero deaths; idle-menu burn and Escape requests have no effect");
                    Require(game.captureLauncher != null, "The scene connects the capture-and-aim item controller");
                    Require(UnityEngine.Object.FindObjectsByType<Rigidbody>(FindObjectsSortMode.None).Length == 0, "Scene uses 2D physics only");
                    Require(Near(game.arenaHalfSize.x * 2, 36) && Near(game.arenaHalfSize.y * 2, 19.8f), "Arena is 36 x 19.8: three times the original width and height");
                    Require(game.deflectionLines.OnBoard(new Vector2(17, 9)) && !game.deflectionLines.OnBoard(new Vector2(19, 0)), "Drawing bounds cover the expanded arena and stop at its new edge");
                    var fit = game.boardCamera.GetComponent<ArenaCamera>();
                    fit.Fit();
                    Vector3 lower = game.boardCamera.WorldToViewportPoint(new Vector3(-18, -9.9f, 0));
                    Vector3 upper = game.boardCamera.WorldToViewportPoint(new Vector3(18, 9.9f, 0));
                    Require(lower.x > 0 && upper.x < 1 && lower.y > 0.03f && upper.y < 0.97f, "Camera fits the whole prototype arena without reserving HUD space");
                    CheckAutomaticStarts();
                    game.StartLevel(1, false);
                    var levelOnePickup = game.powerUps.SpawnAt(Vector2.zero, SizeEffect.Shrink);
                    Require(levelOnePickup.moveSpeed == 0 && game.LevelTitle == "LEVEL ONE", "Level 1 has the LEVEL ONE title and stationary size pickups");
                    game.powerUps.Clear();
                    game.StartLevel(2, false);
                    levelTwoPickup = game.powerUps.SpawnAt(Vector2.zero, SizeEffect.Grow);
                    levelTwoPickupStart = levelTwoPickup.transform.position;
                    Require(game.CurrentLevel == 2 && game.LevelTitle == "LEVEL TWO" && levelTwoPickup.moveSpeed > 0 && levelTwoPickup.moveDirection.sqrMagnitude > 0.9f,
                        "The Level 2 selection starts with moving plus and minus pickups");
                    started = Time.time; step = 60;
                }
                else if (step == 60)
                {
                    if (Time.time - started < 0.15f) return;
                    Require(levelTwoPickup != null && Vector2.Distance(levelTwoPickupStart, levelTwoPickup.transform.position) > 0.1f,
                        "Level 2 pickups travel across the arena");
                    float edge = game.arenaHalfSize.x - 0.56f;
                    levelTwoPickup.transform.position = new Vector2(edge, 0);
                    levelTwoPickup.moveDirection = Vector2.right;
                    started = Time.time; step = 61;
                }
                else if (step == 61)
                {
                    if (Time.time - started < 0.05f) return;
                    Require(levelTwoPickup != null && levelTwoPickup.transform.position.x <= game.arenaHalfSize.x - 0.55f
                        && levelTwoPickup.moveDirection.x < 0, "Moving level 2 pickups bounce at the arena boundary");
                    game.powerUps.Clear();
                    game.StartLevel(1, false);
                    deathsBeforeWalls = game.DeathCount;
                    Begin(WallPositions[0], WallDirections[0]); step = 1;
                }
                else if (step == 1)
                {
                    if (!game.Lost) return;
                    Require(!game.Won && !game.ball.Moving && game.ball.Body.linearVelocity.sqrMagnitude < 0.001f && game.RestartPrompt == "Press R to play again" && game.shrinkingPocket.CountdownText == "", "Wall segment " + (wallIndex + 1) + " burns/stops the ball, hides the countdown and shows the exact retry prompt");
                    int expectedDeaths = deathsBeforeWalls + wallIndex + 1;
                    game.BurnBall(); game.BurnBall();
                    Require(game.DeathCount == expectedDeaths && game.DeathCountLabel == "Deaths: " + expectedDeaths,
                        "Wall loss " + (wallIndex + 1) + " adds exactly one session death, even with repeated burn requests");
                    wallIndex++;
                    if (wallIndex < WallPositions.Length)
                    {
                        Begin(WallPositions[wallIndex], WallDirections[wallIndex]);
                        Require(game.DeathCount == expectedDeaths, "Restart preserves the accumulated death count before wall probe " + (wallIndex + 1));
                    }
                    else { started = Time.time; step = 2; }
                }
                else if (step == 2)
                {
                    if (Time.time - started < 1) return;
                    Require(!game.ball.gameObject.activeSelf && game.RestartPrompt == "Press R to play again", "Burn animation completes while the retry prompt remains visible");
                    game.Shoot(Vector2.right);
                    Require(!game.ball.Moving, "Game over rejects another launch");
                    CheckLossMenuNavigation();
                    Ready(new Vector2(5, 4));
                    game.deflectionLines.Begin(new Vector2(8, 2));
                    game.deflectionLines.Drag(new Vector2(8, 2.2f));
                    Require(game.deflectionLines.Drawing && game.deflectionLines.Count == 0, "An anchor or short drag does not create a collider");
                    game.deflectionLines.Drag(new Vector2(8, 5));
                    consumedLine = game.deflectionLines.GetComponentsInChildren<EdgeCollider2D>().Single().gameObject;
                    Require(game.deflectionLines.Drawing && game.deflectionLines.Count == 1 && LineIsActive(consumedLine), "A valid held drag immediately creates a visible solid deflector before release");
                    game.deflectionLines.Drag(new Vector2(8, 6));
                    Require(game.deflectionLines.Count == 1 && LineEndsAt(consumedLine, new Vector2(8, 6)), "Dragging updates the same line's visible endpoint and collider");
                    game.deflectionLines.Drag(new Vector2(2, 6));
                    Require(game.deflectionLines.Count == 1 && LineEndsAt(consumedLine, new Vector2(8, 6)), "A live update through the ball keeps the last safe geometry");
                    Require(!game.deflectionLines.TryAdd(new Vector2(5, 3), new Vector2(5, 5)), "A line cannot be drawn through the ball");
                    Require(game.deflectionLines.TryAdd(new Vector2(10, -6), new Vector2(12, -6)), "A separate unused line can be drawn");
                    game.Shoot(Vector2.right); Time.timeScale = 1; step = 3;
                }
                else if (step == 3)
                {
                    if (game.Deflections == 0) return;
                    Require(!game.Lost && game.ball.Body.linearVelocity.x < 0 && Near(game.ball.Speed, ExpectedSpeed(ElasticBall.BaseDiameter)), "Ball reflects off a line while the mouse gesture is still held");
                    Require(!game.deflectionLines.Drawing && game.deflectionLines.Count == 1 && LineIsGone(consumedLine), "Contact ends the held gesture and immediately removes only the hit line's renderer and collider");
                    game.deflectionLines.Drag(new Vector2(8, 7));
                    Require(!game.deflectionLines.Release(new Vector2(8, 7)) && game.deflectionLines.Count == 1, "Dragging or releasing after a bounce cannot recreate the consumed line");
                    game.ball.Launch(Vector2.right, game.launchSpeed);
                    step = 13;
                }
                else if (step == 13)
                {
                    if (game.ball.Body.position.x < 8.8f) return;
                    Require(!game.Ended && game.Deflections == 1 && game.ball.Body.linearVelocity.x > 0, "Ball passes through the consumed line's former position without bouncing again");
                    Ready(Vector2.zero);
                    Require(Near(game.deflectionLines.lineLifetime, 2), "Drawn lines have a two-second active lifetime");
                    StartTimedLine(); step = 17;
                }
                else if (step == 17)
                {
                    if (Time.time - lineStarted < 0.8f) return;
                    game.deflectionLines.Drag(new Vector2(4, 6));
                    Require(game.deflectionLines.Release(new Vector2(4, 6)) && !game.deflectionLines.Drawing
                        && game.deflectionLines.Count == 1 && LineIsActive(timedLine) && LineEndsAt(timedLine, new Vector2(4, 6)), "Release finalizes the same live line without creating a duplicate");
                    step = 18;
                }
                else if (step == 18)
                {
                    if (Time.time - lineStarted < 1.6f) return;
                    Require(game.deflectionLines.Count == 1 && LineIsActive(timedLine), "Released line remains solid before its original two-second deadline");
                    step = 19;
                }
                else if (step == 19)
                {
                    if (Time.time - lineStarted < 2.15f) return;
                    Require(game.deflectionLines.Count == 0 && LineIsGone(timedLine), "Line renderer and collider expire two seconds after activation; dragging/releasing does not reset the clock");
                    Require(game.LevelTitle == "LEVEL ONE", "The current level title remains available beyond the former two-second announcement");
                    StartTimedLine();
                    game.Shoot(Vector2.right);
                    game.ball.Launch(Vector2.right, 0.05f);
                    step = 20;
                }
                else if (step == 20)
                {
                    if (Time.time - lineStarted < 1.6f) return;
                    Require(game.deflectionLines.Drawing && LineIsActive(timedLine), "A held line remains active before its deadline");
                    game.powerUps.AutoSpawn = true;
                    game.captureLauncher.AutoSpawn = true;
                    game.OpenPauseMenu(); game.OpenPauseMenu();
                    pausedGameTime = Time.time; pauseBallPosition = game.ball.Body.position;
                    pauseHole = game.shrinkingPocket.SecondsUntilShrink; pauseSpawn = game.powerUps.NextSpawnIn;
                    pauseCaptureSpawn = game.captureLauncher.NextSpawnIn;
                    game.deflectionLines.Begin(new Vector2(6, 2)); game.deflectionLines.Drag(new Vector2(6, 5));
                    Require(game.InPauseMenu && game.IsPaused && Near(Time.timeScale, 0) && !game.deflectionLines.Drawing
                        && !game.deflectionLines.TryAdd(new Vector2(6, 2), new Vector2(6, 5)),
                        "Opening Escape freezes play, cancels a held drawing gesture and blocks new deflectors");
                    pauseAt = EditorApplication.timeSinceStartup; step = 21;
                }
                else if (step == 21)
                {
                    if (EditorApplication.timeSinceStartup - pauseAt < 0.6) return;
                    Require(game.InPauseMenu && game.IsPaused && Near(Time.time, pausedGameTime)
                        && (game.ball.Body.position - pauseBallPosition).sqrMagnitude < 0.0001f
                        && Near(pauseHole, game.shrinkingPocket.SecondsUntilShrink)
                        && Near(pauseSpawn, game.powerUps.NextSpawnIn) && Near(pauseCaptureSpawn, game.captureLauncher.NextSpawnIn)
                        && game.deflectionLines.Count == 1 && LineIsActive(timedLine),
                        "Escape freezes ball movement, hole/pickup/capture timers and a nearly expired line");
                    game.ClosePauseMenu(); game.ClosePauseMenu();
                    Require(!game.InPauseMenu && !game.IsPaused && Near(Time.timeScale, 1) && game.ball.Moving,
                        "Closing Escape resumes a previously running round; repeated open/close calls preserve its prior state");
                    step = 22;
                }
                else if (step == 22)
                {
                    if (Time.time - lineStarted < 2.15f) return;
                    Require(!game.deflectionLines.Drawing && game.deflectionLines.Count == 0 && LineIsGone(timedLine), "Held line expires after two active seconds and ends the gesture");
                    Require(game.captureLauncher.NextSpawnIn < pauseCaptureSpawn - 0.1f,
                        "Capture spawning resumes when the Escape menu closes");
                    game.deflectionLines.Drag(new Vector2(4, 7));
                    Require(!game.deflectionLines.Release(new Vector2(4, 7)) && game.deflectionLines.Count == 0, "Further drag/release cannot resurrect a line that expired while held");
                    Ready(Vector2.zero);
                    StartTimedLine(); game.deflectionLines.Cancel();
                    Require(!game.deflectionLines.Drawing && game.deflectionLines.Count == 1 && LineIsActive(timedLine), "Cancel ends editing while the existing line remains active");
                    step = 23;
                }
                else if (step == 23)
                {
                    if (Time.time - lineStarted < 2.15f) return;
                    Require(game.deflectionLines.Count == 0 && LineIsGone(timedLine), "Cancelled editing preserves the original two-second expiry");
                    Ready(new Vector2(-3.5f, 0));
                    game.powerUps.SpawnAt(new Vector2(-2, 0), SizeEffect.Shrink);
                    game.Shoot(Vector2.right); Time.timeScale = 0.5f; step = 4;
                }
                else if (step == 4)
                {
                    if (game.PickupsCollected < 1) return;
                    Require(Near(game.ball.Diameter, 0.375f) && Near(game.ball.Speed, ExpectedSpeed(0.375f)), "Touching minus shrinks the ball and increases speed immediately");
                    Require(Near(game.ball.GetComponent<CircleCollider2D>().bounds.extents.x, game.ball.Radius), "The physical collider resizes with the sprite");
                    game.powerUps.SpawnAt(game.ball.Body.position + Vector2.right * 1.2f, SizeEffect.Grow);
                    step = 5;
                }
                else if (step == 5)
                {
                    if (game.PickupsCollected < 2) return;
                    Require(Near(game.ball.Diameter, ElasticBall.BaseDiameter) && Near(game.ball.Speed, ExpectedSpeed(ElasticBall.BaseDiameter)) && game.ball.Body.linearVelocity.x > 0, "Touching plus enlarges/slows the ball without changing heading");
                    for (int i = 0; i < 30; i++) game.CollectPowerUp(SizeEffect.Shrink);
                    Require(Near(game.ball.Diameter, ElasticBall.MinDiameter) && Near(game.ball.Speed, ExpectedSpeed(ElasticBall.MinDiameter)), "Repeated shrink pickups respect the minimum size");
                    for (int i = 0; i < 30; i++) game.CollectPowerUp(SizeEffect.Grow);
                    Require(Near(game.ball.Diameter, ElasticBall.MaxDiameter) && Near(game.ball.Diameter * game.ball.Speed, game.launchSpeed * ElasticBall.BaseDiameter), "Repeated growth respects the maximum and inverse speed formula");
                    Begin(new Vector2(game.pocketEntryX - 0.35f, 4), Vector2.up);
                    game.CollectPowerUp(SizeEffect.Grow); game.CollectPowerUp(SizeEffect.Grow);
                    Require(game.Lost, "Growing into a wall immediately burns the ball");
                    Ready(new Vector2(game.pocketEntryX - 3, 0));
                    game.ball.SetDiameter(3.2f);
                    game.Shoot(Vector2.right); Time.timeScale = 1;
                    Require(!game.FitsPocket, "A ball wider than the opening does not fit");
                    started = Time.time; step = 6;
                }
                else if (step == 6)
                {
                    if (game.Ended) throw new Exception("An oversized ball should bounce alive at the red exit edges");
                    if (game.ball.Body.linearVelocity.x >= 0)
                    {
                        if (Time.time - started > 2) throw new Exception("Oversized ball failed to bounce away from the red exit edges");
                        return;
                    }
                    Require(game.ball.Moving && !game.FitsPocket && Near(game.ball.Body.linearVelocity.magnitude, ExpectedSpeed(3.2f)), "Oversized ball bounces away from the exit alive without losing speed");
                    var bumpers = BumperColliders();
                    bumperIds = bumpers.Select(collider => collider.GetInstanceID()).ToArray();
                    Require(UnityEngine.Object.FindObjectsByType<Collider2D>(FindObjectsSortMode.None).Count(collider => collider.gameObject.layer == ShrinkingPocket.BumperLayer) == 4 && BumpersIntact(), "All four red exit colliders remain active after contact");
                    StartBumperProbe(0);
                }
                else if (step == 28)
                {
                    if (game.Ended) throw new Exception("Red exit bumper ended the round in probe " + bumperProbeIndex);
                    if (Vector2.Dot(game.ball.Body.linearVelocity, expectedBumperDirection) <= 0)
                    {
                        if (Time.time - started > 1) throw new Exception("Red exit bumper failed to reflect probe " + bumperProbeIndex);
                        return;
                    }
                    string part = new[] { "upper strip", "lower strip", "upper lip", "lower lip" }[bumperProbeIndex % 4];
                    Require(game.ball.Moving && Near(game.ball.Body.linearVelocity.magnitude, ExpectedSpeed(ElasticBall.BaseDiameter)) && BumpersIntact(),
                        "Red " + part + " reflects the ball alive at full speed and remains solid " + (bumperProbeIndex < 4 ? "before shrinking" : "after shrinking"));
                    if (bumperProbeIndex < 7) StartBumperProbe(bumperProbeIndex + 1);
                    else StartGoalApproach(0);
                }
                else if (step == 7)
                {
                    if (!game.Won && game.ball.Body.position.x > game.pocketEntryX + 2)
                        throw new Exception("Fast fitting ball skipped the goal from approach distance " + GoalApproachDistances[goalApproachIndex]);
                    if (!game.Won) return;
                    Require(!game.Lost && !game.ball.Moving && !game.ball.GetComponent<CircleCollider2D>().enabled && game.RestartPrompt == "", "Minimum-size ball enters and wins without a retry prompt from approach distance " + GoalApproachDistances[goalApproachIndex]);
                    if (goalApproachIndex == 0)
                    {
                        exitStartX = game.ball.transform.position.x;
                        started = Time.time; step = 24;
                        return;
                    }
                    if (goalApproachIndex + 1 < GoalApproachDistances.Length) { StartGoalApproach(goalApproachIndex + 1); return; }
                    game.OpenPauseMenu();
                    Require(game.InPauseMenu && game.IsPaused && game.Won && !game.ExitComplete && game.DeathCount == deathsBeforeWin,
                        "Escape is available during the winning exit animation without adding a death");
                    pauseBallPosition = game.ball.Body.position; pausedGameTime = Time.time;
                    pauseAt = EditorApplication.timeSinceStartup; step = 63;
                }
                else if (step == 63)
                {
                    if (EditorApplication.timeSinceStartup - pauseAt < 0.3) return;
                    Require(game.InPauseMenu && game.IsPaused && game.Won && !game.ExitComplete && game.CurrentLevel == 1
                        && Near(Time.time, pausedGameTime) && (game.ball.Body.position - pauseBallPosition).sqrMagnitude < 0.0001f
                        && ((Vector2)game.ball.transform.position - pauseBallPosition).sqrMagnitude < 0.0001f
                        && game.DeathCount == deathsBeforeWin,
                        "Escape freezes the winning exit animation and delays automatic level progression without adding deaths");
                    game.ClosePauseMenu();
                    Require(!game.InPauseMenu && !game.IsPaused && game.Won, "Resuming a won round preserves its success state");
                    started = Time.time; step = 8;
                }
                else if (step == 8)
                {
                    if (Time.time - started < 0.7f) return;
                    CheckVisibleExit(ElasticBall.MinDiameter, "Minimum-size ball remains visible at full size outside the arena after exiting");
                    started = Time.time; step = 62;
                }
                else if (step == 62)
                {
                    if (Time.time - started < 0.7f) return;
                    Require(game.CurrentLevel == 2 && game.LevelTitle == "LEVEL TWO" && !game.Ended && game.RoundStarted && game.ball.Moving
                        && game.ball.Body.position.sqrMagnitude < 4f && game.DeathCount == deathsBeforeWin,
                        "Clearing level 1 automatically starts LEVEL TWO from the center and preserves session deaths");
                    game.StartLevel(1, false);
                    Ready(new Vector2(game.pocketEntryX - 3, 0));
                    game.ball.SetDiameter(2.4f);
                    game.Shoot(Vector2.right); Time.timeScale = 1; step = 26;
                }
                else if (step == 24)
                {
                    if (Time.time - started < 0.15f) return;
                    Require(!game.ExitComplete && game.ball.gameObject.activeSelf && game.ball.transform.position.x > exitStartX + 0.005f, "Winning ball visibly travels through the exit before the animation completes");
                    game.ResetBall();
                    Require(!game.Ended && !game.ExitComplete && game.RestartPrompt == "" && game.ball.Moving && game.ball.Body.position.sqrMagnitude < 0.0001f, "Restart during the exit immediately clears success and returns the ball to the center");
                    started = Time.time; step = 25;
                }
                else if (step == 25)
                {
                    if (Time.time - started < 0.8f) return;
                    Require(!game.Ended && !game.ExitComplete && game.RestartPrompt == "" && game.ball.gameObject.activeSelf && game.ball.Moving && Near(game.ball.transform.localScale.x, 3.2f) && game.ball.Body.position.magnitude < 6, "Restart stops the old exit animation from moving, hiding or completing the new round");
                    StartGoalApproach(1);
                }
                else if (step == 26)
                {
                    if (!game.Won && game.ball.Body.position.x > game.pocketEntryX + 2)
                        throw new Exception("The larger fitting ball skipped the goal");
                    if (!game.Won) return;
                    Require(!game.Lost && game.RestartPrompt == "", "A larger fitting ball wins without showing the loss prompt");
                    started = Time.time; step = 27;
                }
                else if (step == 27)
                {
                    if (Time.time - started < 0.7f) return;
                    CheckVisibleExit(2.4f, "A ball reduced by one shrink pickup remains opaque and wholly visible outside the larger exit");
                    Ready(Vector2.zero);
                    var pickup = game.powerUps.SpawnAt(new Vector2(3, 2), SizeEffect.Shrink);
                    pickup.lifetime = 0.25f;
                    Time.timeScale = 4; started = Time.time; step = 9;
                }
                else if (step == 9)
                {
                    if (Time.time - started < 0.5f) return;
                    Require(game.powerUps.ActiveCount == 0, "Uncollected pickups expire");
                    Ready(Vector2.zero);
                    timedShrinkPickup = game.powerUps.SpawnAt(new Vector2(-3, 2), SizeEffect.Shrink);
                    timedGrowPickup = game.powerUps.SpawnAt(new Vector2(3, 2), SizeEffect.Grow);
                    Time.timeScale = 4; started = Time.time; step = 29;
                }
                else if (step == 29)
                {
                    if (Time.time - started < 4.6f) return;
                    Require(timedShrinkPickup != null && timedShrinkPickup.gameObject.activeInHierarchy
                        && timedGrowPickup != null && timedGrowPickup.gameObject.activeInHierarchy && game.powerUps.ActiveCount == 2,
                        "Both pickup effects remain available before the plus pickup's five-second deadline");
                    step = 30;
                }
                else if (step == 30)
                {
                    if (Time.time - started < 5.25f) return;
                    Require((timedGrowPickup == null || !timedGrowPickup.gameObject.activeInHierarchy)
                        && timedShrinkPickup != null && timedShrinkPickup.gameObject.activeInHierarchy && game.powerUps.ActiveCount == 1,
                        "Plus pickup expires after five seconds while minus remains available");
                    step = 31;
                }
                else if (step == 31)
                {
                    if (Time.time - started < 11.6f) return;
                    Require(timedShrinkPickup != null && timedShrinkPickup.gameObject.activeInHierarchy && game.powerUps.ActiveCount == 1,
                        "Minus pickup remains available until its twelve-second deadline");
                    step = 32;
                }
                else if (step == 32)
                {
                    if (Time.time - started < 12.25f) return;
                    Require((timedShrinkPickup == null || !timedShrinkPickup.gameObject.activeInHierarchy) && game.powerUps.ActiveCount == 0,
                        "Minus pickup expires after twelve seconds and releases its spawn slot");
                    Ready(new Vector2(-4, 0));
                    game.ball.SetDiameter(3.2f);
                    checkedCapturePoint = game.captureLauncher.SpawnAt(Vector2.zero);
                    Require(checkedCapturePoint != null && game.captureLauncher.SpawnAt(new Vector2(17, 0)) == null && game.captureLauncher.HasPoint,
                        "Capture points spawn safely inside the arena and reject placement near its walls");
                    game.Shoot(Vector2.right); Time.timeScale = 0.5f; started = Time.time; step = 33;
                }
                else if (step == 33)
                {
                    if (!game.captureLauncher.IsHoldingBall)
                    {
                        if (Time.time - started > 1) throw new Exception("Ball passed the capture point without being held");
                        return;
                    }
                    var capture = game.captureLauncher;
                    Require(!game.Ended && game.RoundStarted && !game.ball.Moving && game.ball.Body.position.sqrMagnitude < 0.0001f && game.ball.Body.linearVelocity.sqrMagnitude < 0.0001f && capture.HasPoint,
                        "Contact captures and centers the ball without ending the round");
                    Require(!capture.ReleaseAim(new Vector2(-2, 0)) && !capture.BeginAim(new Vector2(3, 3)) && capture.IsHoldingBall,
                        "Capture ignores an old release and a press away from the held ball");
                    Require(capture.BeginAim(new Vector2(0.4f, 0)) && !capture.ReleaseAim(new Vector2(0.4f, 0)) && capture.IsHoldingBall,
                        "An off-center click without dragging does not launch the held ball");
                    game.deflectionLines.Begin(new Vector2(4, 2)); game.deflectionLines.Drag(new Vector2(4, 5));
                    Require(!game.deflectionLines.Drawing && game.deflectionLines.Count == 0 && !game.deflectionLines.TryAdd(new Vector2(4, 2), new Vector2(4, 5)),
                        "Drawing input cannot create a deflector while the ball is captured");
                    Require(capture.BeginAim(Vector2.zero), "A fresh press near the captured ball starts aiming");
                    capture.DragAim(new Vector2(-0.1f, 0));
                    Require(!capture.ReleaseAim(new Vector2(-0.1f, 0)) && capture.IsHoldingBall && !capture.IsAiming && !game.aimLine.enabled,
                        "A tiny pull cancels aiming without releasing or consuming the capture point");
                    Require(capture.BeginAim(Vector2.zero), "A cancelled small pull can be retried with a fresh press");
                    capture.DragAim(new Vector2(-2, 0));
                    Require(capture.IsAiming && game.aimLine.enabled, "Pulling the captured ball shows the aim guide");
                    game.TogglePause(); pauseHole = game.shrinkingPocket.SecondsUntilShrink;
                    Require(game.IsPaused && capture.IsHoldingBall && !capture.IsAiming && !game.aimLine.enabled,
                        "Pause cancels the aim gesture while retaining the captured ball");
                    game.OpenPauseMenu(); game.TogglePause();
                    Require(game.InPauseMenu && game.IsPaused && capture.IsHoldingBall && !capture.BeginAim(Vector2.zero)
                        && !capture.ReleaseAim(new Vector2(-2, 0)),
                        "Escape over a P-paused capture keeps the ball held and blocks aim/release and P behind the menu");
                    pauseAt = EditorApplication.timeSinceStartup; step = 34;
                }
                else if (step == 34)
                {
                    if (EditorApplication.timeSinceStartup - pauseAt < 0.3) return;
                    Require(game.captureLauncher.IsHoldingBall && Near(pauseHole, game.shrinkingPocket.SecondsUntilShrink)
                        && !game.captureLauncher.ReleaseAim(new Vector2(-2, 0)), "Paused capture keeps the ball and ignores release");
                    game.ClosePauseMenu();
                    Require(!game.InPauseMenu && game.IsPaused && Near(Time.timeScale, 0) && game.captureLauncher.IsHoldingBall,
                        "Closing Escape preserves a pre-existing P pause and the captured ball");
                    game.TogglePause(); Time.timeScale = 4;
                    game.powerUps.SpawnAt(new Vector2(3, 2), SizeEffect.Shrink).lifetime = 0.25f;
                    capturedHole = game.shrinkingPocket.SecondsUntilShrink;
                    started = Time.time; step = 35;
                }
                else if (step == 35)
                {
                    if (Time.time - started < 16.2f) return;
                    var capture = game.captureLauncher;
                    Require(capture.IsHoldingBall && capture.HasPoint && !game.ball.Moving && !game.Ended
                        && game.shrinkingPocket.SecondsUntilShrink < capturedHole - 15 && game.powerUps.ActiveCount == 0,
                        "Holding persists beyond the uncollected item lifetime while the hole countdown and size-pickup expiry continue");
                    timedGrowPickup = game.powerUps.SpawnAt(game.ball.Body.position, SizeEffect.Grow);
                    started = Time.time; step = 44;
                }
                else if (step == 44)
                {
                    if (Time.time - started < 0.2f) return;
                    var capture = game.captureLauncher;
                    Require(capture.IsHoldingBall && game.PickupsCollected == 0 && timedGrowPickup != null && timedGrowPickup.gameObject.activeInHierarchy,
                        "A size pickup overlapping a captured ball waits until the ball is released");
                    Require(capture.BeginAim(game.ball.Body.position), "A new press after pause starts a fresh aim gesture");
                    Vector2 release = game.ball.Body.position - new Vector2(2, 1);
                    capture.DragAim(release);
                    capture.AutoSpawn = true;
                    Require(capture.ReleaseAim(release) && game.ball.Moving && !capture.IsHoldingBall && !capture.IsAiming && !capture.HasPoint
                        && (checkedCapturePoint == null || !checkedCapturePoint.gameObject.activeInHierarchy) && !game.aimLine.enabled
                        && Vector2.Dot(game.ball.Body.linearVelocity.normalized, new Vector2(2, 1).normalized) > 0.999f
                        && Near(game.ball.Body.linearVelocity.magnitude, ExpectedSpeed(3.2f)),
                        "Release launches opposite the pull at the usual size-based speed and consumes the capture point");
                    Vector2 releasedVelocity = game.ball.Body.linearVelocity;
                    Require(!capture.ReleaseAim(release) && (game.ball.Body.linearVelocity - releasedVelocity).sqrMagnitude < 0.0001f,
                        "A consumed capture point cannot launch the ball a second time");
                    Time.timeScale = 1; started = Time.time; step = 45;
                }
                else if (step == 45)
                {
                    if (game.PickupsCollected == 0)
                    {
                        if (Time.time - started > 1) throw new Exception("Pickup already overlapping the held ball was not collected after release");
                        return;
                    }
                    Require(game.PickupsCollected == 1 && Near(game.ball.Diameter, 3.2f / 0.75f)
                        && Near(game.ball.Speed, ExpectedSpeed(3.2f / 0.75f)) && (timedGrowPickup == null || !timedGrowPickup.gameObject.activeInHierarchy),
                        "The overlapping grow pickup collects once after release and updates size/speed");
                    pausedGameTime = Time.time; step = 46;
                }
                else if (step == 46)
                {
                    if (Time.time - pausedGameTime < 0.2f) return;
                    Require(game.PickupsCollected == 1 && Near(game.ball.Diameter, 3.2f / 0.75f), "The released ball cannot collect the same overlapping pickup twice");
                    game.ball.Launch(Vector2.right, 0.05f); Time.timeScale = 4; step = 36;
                }
                else if (step == 36)
                {
                    if (Time.time - started < 11.6f) return;
                    Require(!game.captureLauncher.HasPoint, "A used capture point does not respawn before twelve seconds");
                    step = 37;
                }
                else if (step == 37)
                {
                    if (Time.time - started < 12.3f) return;
                    Require(game.captureLauncher.HasPoint, "A new capture point appears after the twelve-second respawn delay");
                    Ready(new Vector2(-2, 0));
                    game.ball.SetDiameter(ElasticBall.MinDiameter);
                    game.captureLauncher.SpawnAt(Vector2.zero); game.Shoot(Vector2.right);
                    Require(game.captureLauncher.TryCaptureAlongStep(game.ball.Body.position, Vector2.right * 4)
                        && game.captureLauncher.IsHoldingBall && game.ball.Body.position.sqrMagnitude < 0.0001f,
                        "A swept step captures a minimum-size ball even when both step endpoints are outside the item");
                    game.captureLauncher.BeginAim(Vector2.zero); game.captureLauncher.DragAim(Vector2.left);
                    game.ResetBall();
                    Require(!game.captureLauncher.HasPoint && !game.captureLauncher.IsHoldingBall && !game.captureLauncher.IsAiming && !game.aimLine.enabled
                        && !game.Ended && game.ball.Moving && game.ball.Body.position.sqrMagnitude < 0.0001f && Near(game.ball.Diameter, 3.2f),
                        "Restart while captured clears the item and aim, then launches a new ball from the center");
                    Ready(new Vector2(-0.9f, 0));
                    game.ball.SetDiameter(ElasticBall.MinDiameter);
                    game.captureLauncher.SpawnAt(Vector2.zero); game.Shoot(Vector2.right);
                    Time.timeScale = 0.5f; started = Time.time; step = 38;
                }
                else if (step == 38)
                {
                    if (!game.captureLauncher.IsHoldingBall)
                    {
                        if (Time.time - started > 1) throw new Exception("Fast minimum-size ball missed capture during real physics");
                        return;
                    }
                    Require(!game.ball.Moving && Near(game.ball.Diameter, ElasticBall.MinDiameter) && game.ball.Body.position.sqrMagnitude < 0.0001f,
                        "Real physics captures the fastest minimum-size ball at the point center");
                    Ready(new Vector2(-1.4f, 0)); game.ball.SetDiameter(ElasticBall.MinDiameter);
                    game.captureLauncher.SpawnAt(Vector2.zero);
                    Require(game.deflectionLines.TryAdd(new Vector2(-1, -2), new Vector2(-1, 2)), "Capture blocker fixture creates a solid line between ball and item");
                    Require(game.captureLauncher.SpawnAt(Vector2.zero) == null && game.captureLauncher.HasPoint,
                        "Capture-point placement rejects an overlapping deflector without removing the existing point");
                    game.Shoot(Vector2.right);
                    Require(!game.captureLauncher.TryCaptureAlongStep(game.ball.Body.position, Vector2.right * 3) && !game.captureLauncher.IsHoldingBall,
                        "Swept capture respects a deflector met before the item");
                    Time.timeScale = 0.5f; started = Time.time; step = 39;
                }
                else if (step == 39)
                {
                    if (game.Deflections == 0)
                    {
                        if (Time.time - started > 1) throw new Exception("Ball did not bounce from the capture-path blocker");
                        return;
                    }
                    Require(!game.Ended && !game.captureLauncher.IsHoldingBall && game.captureLauncher.HasPoint && game.ball.Body.linearVelocity.x < 0,
                        "A ball blocked by a drawn line bounces instead of capturing through it");
                    Ready(Vector2.zero);
                    checkedCapturePoint = game.captureLauncher.SpawnAt(new Vector2(0, 4));
                    game.Shoot(Vector2.right); game.ball.Launch(Vector2.right, 0.05f);
                    Time.timeScale = 4; started = Time.time; step = 40;
                }
                else if (step == 40)
                {
                    if (Time.time - started < 14.6f) return;
                    Require(checkedCapturePoint != null && game.captureLauncher.HasPoint, "An uncollected capture point remains available before fifteen seconds");
                    step = 41;
                }
                else if (step == 41)
                {
                    if (Time.time - started < 15.25f) return;
                    Require(!game.captureLauncher.HasPoint && (checkedCapturePoint == null || !checkedCapturePoint.gameObject.activeInHierarchy),
                        "An uncollected capture point expires after fifteen seconds");
                    Ready(Vector2.zero); game.captureLauncher.AutoSpawn = true;
                    game.Shoot(Vector2.right); game.ball.Launch(Vector2.right, 0.05f);
                    Time.timeScale = 4; started = Time.time; step = 42;
                }
                else if (step == 42)
                {
                    if (Time.time - started < 0.7f) return;
                    Require(!game.captureLauncher.HasPoint, "A new round waits before creating its first capture point");
                    step = 43;
                }
                else if (step == 43)
                {
                    if (Time.time - started < 1.5f) return;
                    Require(game.captureLauncher.HasPoint, "The first capture point appears after the initial 1.2-second delay");
                    Ready(Vector2.zero);
                    game.powerUps.AutoSpawn = true;
                    game.Shoot(Vector2.right);
                    // Keep the ball inside the arena while testing timers independently of deflectors.
                    game.ball.Launch(Vector2.right, 0.05f);
                    Time.timeScale = 10;
                    started = Time.time; step = 10;
                }
                else if (step == 10)
                {
                    if (Time.time - started < 3) return;
                    var pickups = UnityEngine.Object.FindObjectsByType<SizePowerUp>(FindObjectsSortMode.None);
                    Require(pickups.Any(p => p.effect == SizeEffect.Shrink) && pickups.Any(p => p.effect == SizeEffect.Grow), "Timer spawns both shrink and grow pickups");
                    Require(pickups.All(p => Physics2D.OverlapCircle(p.transform.position, 0.35f, 1 << 8) == null), "Timed pickups spawn clear of the expanded arena walls");
                    float countdownElapsed = Time.time - started;
                    float countdownRemaining = game.shrinkingPocket.SecondsUntilShrink;
                    // Editor callbacks and the countdown's Update can observe adjacent player-loop phases.
                    float countdownTolerance = Mathf.Max(Time.deltaTime, Time.fixedDeltaTime) + 0.015f;
                    Require(Near(game.shrinkingPocket.shrinkInterval, 100)
                        && countdownRemaining > 90 && countdownRemaining < 100
                        && Mathf.Abs((100 - countdownRemaining) - countdownElapsed) <= countdownTolerance
                        && game.shrinkingPocket.CountdownText == $"Shrinks in {Mathf.CeilToInt(countdownRemaining)}s",
                        $"The exit countdown follows elapsed game time and rounds up (elapsed {countdownElapsed:F3}s, remaining {countdownRemaining:F3}s, text '{game.shrinkingPocket.CountdownText}')");
                    game.TogglePause(); pauseHole = game.shrinkingPocket.SecondsUntilShrink; pauseSpawn = game.powerUps.NextSpawnIn;
                    pauseCountdown = game.shrinkingPocket.CountdownText;
                    pauseAt = EditorApplication.timeSinceStartup; step = 11;
                }
                else if (step == 11)
                {
                    if (EditorApplication.timeSinceStartup - pauseAt < 0.3) return;
                    Require(Near(pauseHole, game.shrinkingPocket.SecondsUntilShrink) && Near(pauseSpawn, game.powerUps.NextSpawnIn) && game.shrinkingPocket.CountdownText == pauseCountdown, "Pause freezes the visible countdown and both the hole and pickup timers");
                    game.TogglePause(); Time.timeScale = 10;
                    game.powerUps.AutoSpawn = false; game.powerUps.Clear(); step = 12;
                }
                else if (step == 12)
                {
                    if (Time.time - started < 99.2f) return;
                    Require(game.shrinkingPocket.ShrinkCount == 0 && Near(game.pocketWidth, 2.76f) && game.shrinkingPocket.CountdownText == "Shrinks in 1s", "Hole keeps its initial width until 100 seconds while the countdown reaches its final second");
                    step = 14;
                }
                else if (step == 14)
                {
                    if (Time.time - started < 100.2f) return;
                    Require(!game.Ended && game.shrinkingPocket.ShrinkCount == 1 && Near(game.pocketWidth, 2.76f * 0.85f) && game.shrinkingPocket.CountdownText == "Shrinks in 100s", "Hole shrinks by 15% at 100 seconds and restarts the visible countdown");
                    CheckOpeningGeometry("First shrink updates the visible opening, physical wall gap and goal trigger together");
                    step = 15;
                }
                else if (step == 15)
                {
                    if (Time.time - started < 199.2f) return;
                    Require(game.shrinkingPocket.ShrinkCount == 1 && Near(game.pocketWidth, 2.76f * 0.85f) && game.shrinkingPocket.CountdownText == "Shrinks in 1s", "Hole holds its new width until the next 100-second boundary");
                    step = 16;
                }
                else if (step == 16)
                {
                    if (Time.time - started < 200.2f) return;
                    Require(!game.Ended && game.shrinkingPocket.ShrinkCount == 2 && Near(game.pocketWidth, 2.76f * 0.85f * 0.85f) && game.shrinkingPocket.CountdownText == "Shrinks in 100s", "Hole visibly shrinks again at 200 seconds and restarts the countdown");
                    CheckOpeningGeometry("Second shrink keeps visible geometry and collision geometry synchronized");
                    game.shrinkingPocket.SetWidth(game.shrinkingPocket.minimumWidth);
                    Require(!game.FitsPocket && game.shrinkingPocket.CountdownText == "Exit at minimum size", "Minimum hole size updates ball fit and displays the minimum-size message");
                    Begin(new Vector2(game.pocketEntryX - 0.2f, 2.25f), Vector2.right);
                    Require(!game.Lost, "Contact with the red strip near the gray wall seam is initially nonlethal");
                    game.shrinkingPocket.SetWidth(2.76f * 0.85f);
                    Require(game.Lost, "A shrinking gray wall that reaches the ball at the red-gray seam burns it immediately");
                    game.ResetBall();
                    Require(!game.Ended && !game.ExitComplete && game.RestartPrompt == "" && game.RoundStarted && game.ball.Moving && game.ball.Body.position.sqrMagnitude < 0.0001f && game.ball.transform.position.sqrMagnitude < 0.0001f && Near(game.ball.Diameter, 3.2f) && game.ball.Diameter > game.pocketWidth && Near(game.ball.Speed, ExpectedSpeed(3.2f)) && Near(game.ball.Body.linearVelocity.magnitude, 4.725f) && Near(game.pocketWidth, 2.76f) && Near(game.shrinkingPocket.SecondsUntilShrink, 100) && game.shrinkingPocket.CountdownText == "Shrinks in 100s" && game.shrinkingPocket.ShrinkCount == 0 && game.deflectionLines.Count == 0 && game.powerUps.ActiveCount == 0 && game.ball.gameObject.activeSelf, "Reset clears end feedback and restores the center start, initial speed, opening and 100-second countdown");
                    report.AppendLine("\nALL CURRENT RULE CHECKS PASSED"); Finish();
                }
            }
            catch (Exception exception)
            {
                report.AppendLine("FAIL: " + exception.Message);
                Debug.LogError(exception);
                Finish();
            }
        }

        static void CheckLossMenuNavigation()
        {
            int deaths = game.DeathCount;
            game.TogglePauseMenu();
            Require(game.InPauseMenu && game.IsPaused && game.Lost && Near(Time.timeScale, 0),
                "Escape opens after a loss while preserving the ended round");
            game.TogglePauseMenu();
            Require(!game.InPauseMenu && !game.IsPaused && game.Lost && game.DeathCount == deaths,
                "Closing the loss menu preserves the loss and does not count another death");
            game.OpenPauseMenu(); game.StartLevel(2);
            Require(!game.InPauseMenu && !game.IsPaused && !game.Ended && game.CurrentLevel == 2
                && game.LevelTitle == "LEVEL TWO" && game.DeathCount == deaths && game.ball.Moving,
                "Selecting LEVEL TWO after a loss closes Escape and starts that level without resetting or adding deaths");
            game.OpenPauseMenu(); game.ShowStartMenu();
            Require(game.InStartMenu && !game.InPauseMenu && !game.IsPaused && !game.RoundStarted
                && !game.ball.gameObject.activeSelf && game.DeathCount == deaths,
                "MAIN MENU clears the Escape menu and gameplay while preserving the session death count");
            game.TogglePauseMenu();
            Require(!game.InPauseMenu && game.InStartMenu && Near(Time.timeScale, 1), "Escape does nothing on the main menu");
            game.StartLevel(1); game.OpenPauseMenu(); game.ResetBall();
            Require(!game.InStartMenu && !game.InPauseMenu && !game.IsPaused && game.CurrentLevel == 1
                && game.LevelTitle == "LEVEL ONE" && game.DeathCount == deaths && game.DeathCountLabel == "Deaths: " + deaths
                && game.ball.Moving && game.ball.Body.position.sqrMagnitude < 0.0001f,
                "Restart clears Escape, relaunches the current level from center and preserves session deaths");
        }

        static void Ready(Vector2 position)
        {
            game.InputEnabled = false; game.powerUps.AutoSpawn = false;
            game.captureLauncher.AutoSpawn = false;
            game.ResetBall(false);
            game.ball.SetDiameter(ElasticBall.BaseDiameter);
            game.ball.transform.position = position; game.ball.Body.position = position;
            Physics2D.SyncTransforms();
        }

        static void StartGoalApproach(int index)
        {
            goalApproachIndex = index;
            if (index == 0) deathsBeforeWin = game.DeathCount;
            Ready(new Vector2(game.pocketEntryX - GoalApproachDistances[index], 0));
            game.ball.SetDiameter(ElasticBall.MinDiameter);
            game.Shoot(Vector2.right); Time.timeScale = 0.5f; step = 7;
        }

        static BoxCollider2D[] BumperColliders()
        {
            var pocket = game.shrinkingPocket;
            return new[] { pocket.upperBumper, pocket.lowerBumper, pocket.upperJaw, pocket.lowerJaw }
                .Select(part => part.GetComponent<BoxCollider2D>()).ToArray();
        }

        static bool BumpersIntact()
        {
            var colliders = BumperColliders();
            return colliders.Select((collider, index) => collider != null && collider.enabled
                && collider.gameObject.activeInHierarchy && collider.gameObject.layer == ShrinkingPocket.BumperLayer
                && collider.GetInstanceID() == bumperIds[index] && collider.GetComponent<SpriteRenderer>().enabled).All(active => active);
        }

        static void StartBumperProbe(int index)
        {
            bumperProbeIndex = index;
            Ready(Vector2.zero);
            game.shrinkingPocket.SetWidth(2.76f * (index < 4 ? 1 : 0.85f));
            int part = index % 4;
            Bounds bounds = BumperColliders()[part].bounds;
            Vector2 position, direction;
            if (part < 2)
            {
                position = new Vector2(game.pocketEntryX - 0.9f, bounds.center.y);
                direction = Vector2.right;
            }
            else
            {
                float sign = part == 2 ? 1 : -1;
                position = new Vector2(game.pocketEntryX + 0.6f, bounds.center.y + sign * (bounds.extents.y + 0.6f));
                direction = Vector2.down * sign;
            }
            game.ball.transform.position = position; game.ball.Body.position = position;
            Physics2D.SyncTransforms();
            expectedBumperDirection = -direction;
            game.Shoot(direction); Time.timeScale = 0.5f;
            started = Time.time; step = 28;
        }

        static void CheckVisibleExit(float diameter, string description)
        {
            var renderer = game.ball.GetComponent<SpriteRenderer>();
            Bounds bounds = renderer.bounds;
            Vector3 lower = game.boardCamera.WorldToViewportPoint(bounds.min);
            Vector3 upper = game.boardCamera.WorldToViewportPoint(bounds.max);
            Require(game.Won && !game.Lost && game.ExitComplete && game.RestartPrompt == "" && game.shrinkingPocket.CountdownText == ""
                && game.ball.gameObject.activeInHierarchy && renderer.enabled && Near(renderer.color.a, 1)
                && Near(game.ball.Diameter, diameter) && Near(game.ball.transform.localScale.x, diameter)
                && Near(game.ball.transform.localScale.y, diameter)
                && game.ball.transform.position.x > game.pocketEntryX + 1.2f && bounds.min.x > game.pocketEntryX
                && lower.x > 0 && upper.x < 1 && lower.y > 0 && upper.y < 1 && lower.z > 0, description);
        }

        static void StartTimedLine()
        {
            game.deflectionLines.Begin(new Vector2(4, 2));
            game.deflectionLines.Drag(new Vector2(4, 5));
            timedLine = game.deflectionLines.GetComponentsInChildren<EdgeCollider2D>().Single().gameObject;
            lineStarted = Time.time;
        }

        static bool LineIsActive(GameObject line) => line != null && line.activeInHierarchy
            && line.GetComponent<LineRenderer>().enabled && line.GetComponent<EdgeCollider2D>().enabled;
        static bool LineIsGone(GameObject line) => line == null || !line.activeInHierarchy;
        static bool LineEndsAt(GameObject line, Vector2 endpoint)
        {
            if (!LineIsActive(line)) return false;
            var edge = line.GetComponent<EdgeCollider2D>();
            Vector2 colliderEnd = edge.transform.TransformPoint(edge.points[1]);
            Vector2 renderedEnd = line.GetComponent<LineRenderer>().GetPosition(1);
            return (colliderEnd - endpoint).sqrMagnitude < 0.0001f
                && (renderedEnd - endpoint).sqrMagnitude < 0.0001f;
        }

        static void CheckAutomaticStarts()
        {
            var savedState = UnityEngine.Random.state;
            try
            {
                bool validStarts = true, variedHeadings = false;
                Vector2 firstDirection = Vector2.zero;
                for (int i = 0; i < 12; i++)
                {
                    game.ResetBall();
                    Vector2 velocity = game.ball.Body.linearVelocity;
                    Vector2 direction = velocity.normalized;
                    validStarts &= game.RoundStarted && game.ball.Moving && !game.Ended && !game.ExitComplete && game.RestartPrompt == ""
                        && game.shrinkingPocket.CountdownText == "Shrinks in 100s"
                        && game.ball.Body.position.sqrMagnitude < 0.0001f && game.ball.transform.position.sqrMagnitude < 0.0001f
                        && Near(game.ball.Diameter, 3.2f) && game.ball.Diameter > game.pocketWidth
                        && !game.FitsPocket && Near(game.ball.Speed, ExpectedSpeed(3.2f))
                        && Near(velocity.magnitude, 4.725f) && Near(direction.magnitude, 1);
                    if (i == 0) firstDirection = direction;
                    else variedHeadings |= Vector2.Dot(firstDirection, direction) < 0.95f;
                }
                Require(validStarts, "Every new round launches automatically from the arena center with an oversized ball moving at 4.725 units per second");
                Require(variedHeadings, "Repeated new rounds choose different random unit headings without player aim");
                Require(game.aimLine == null || !game.aimLine.enabled, "Automatic rounds have no player aiming guide");
                Vector2 runningVelocity = game.ball.Body.linearVelocity;
                game.Shoot(-runningVelocity);
                Require((game.ball.Body.linearVelocity - runningVelocity).sqrMagnitude < 0.0001f, "A second launch request cannot redirect a round that is already running");
            }
            finally { UnityEngine.Random.state = savedState; }
        }

        static void CheckOpeningGeometry(string description)
        {
            var pocket = game.shrinkingPocket;
            float gap = pocket.upperWall.GetComponent<BoxCollider2D>().bounds.min.y
                - pocket.lowerWall.GetComponent<BoxCollider2D>().bounds.max.y;
            float redGap = pocket.upperBumper.GetComponent<BoxCollider2D>().bounds.min.y
                - pocket.lowerBumper.GetComponent<BoxCollider2D>().bounds.max.y;
            float lipGap = pocket.upperJaw.GetComponent<BoxCollider2D>().bounds.min.y
                - pocket.lowerJaw.GetComponent<BoxCollider2D>().bounds.max.y;
            Require(Near(gap, game.pocketWidth + 2 * pocket.bumperLength) && Near(redGap, game.pocketWidth)
                && Near(lipGap, game.pocketWidth) && BumpersIntact()
                && Near(pocket.rim.localScale.y, game.pocketWidth + 0.15f)
                && Near(pocket.well.localScale.y, game.pocketWidth - 0.02f)
                && Near(pocket.mouth.localScale.y, game.pocketWidth)
                && Near(pocket.upperJaw.position.y - pocket.lowerJaw.position.y, game.pocketWidth + 0.075f)
                && Near(pocket.goalTrigger.size.y, game.pocketWidth - 0.22f), description);
        }
        static void Begin(Vector2 position, Vector2 direction)
        {
            Ready(position); game.Shoot(direction); Time.timeScale = 1; started = Time.time;
        }
        static float ExpectedSpeed(float diameter) => game.launchSpeed * ElasticBall.BaseDiameter / diameter;
        static bool Near(float a, float b) => Mathf.Abs(a - b) < 0.015f;
        static void Require(bool condition, string description)
        {
            if (!condition) throw new Exception(description);
            report.AppendLine("PASS: " + description);
        }
        static void Finish()
        {
            File.WriteAllText("PhysicsChecks.txt", report.ToString());
            Debug.Log(report.ToString());
            Time.timeScale = 1; SessionState.SetBool(Pending, false);
            EditorApplication.update -= Tick;
            if (randomStateSaved) { UnityEngine.Random.state = randomState; randomStateSaved = false; }
            if (game != null) { game.InputEnabled = true; game.powerUps.AutoSpawn = true; if (game.captureLauncher != null) game.captureLauncher.AutoSpawn = true; game.ResetBall(); }
            if (Application.isBatchMode) EditorApplication.Exit(report.ToString().Contains("ALL CURRENT RULE CHECKS PASSED") ? 0 : 1);
        }
    }
}
