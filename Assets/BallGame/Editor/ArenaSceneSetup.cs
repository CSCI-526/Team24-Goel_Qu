using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BallGame.Editor
{
    [InitializeOnLoad]
    public static class ArenaSceneSetup
    {
        const string Root = "Assets/BallGame";
        static Sprite square, circle;
        static Material material;
        static Transform arena;

        static ArenaSceneSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                var game = Object.FindFirstObjectByType<BallGameController>();
                if (game != null && game.rulesVersion >= 5 && game.arenaVersion < 13) Apply();
            };
        }

        [MenuItem("Ball Game/Apply Prototype Layout")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var game = Object.FindFirstObjectByType<BallGameController>();
            if (game == null || game.shrinkingPocket == null) return;
            square = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "/Sprites/Square.png");
            circle = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "/Sprites/Circle.png");
            material = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/Table sprites.mat");
            var pocket = game.shrinkingPocket;
            pocket.initialWidth = 2.76f;
            pocket.shrinkInterval = 100;
            pocket.bumperLength = 1.2f;
            var tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            tags.FindProperty("layers").GetArrayElementAtIndex(ShrinkingPocket.BumperLayer).stringValue = "Exit bumpers";
            tags.ApplyModifiedPropertiesWithoutUndo();
            arena = pocket.upperWall.parent;
            var keep = new HashSet<Transform> { pocket.upperWall, pocket.lowerWall, pocket.upperJaw, pocket.lowerJaw, pocket.rim, pocket.well, pocket.mouth, pocket.upperBumper, pocket.lowerBumper };
            var walls = new List<BoxCollider2D>();
            foreach (var wall in Object.FindObjectsByType<BoxCollider2D>(FindObjectsSortMode.None))
                if (wall.gameObject.layer == 8) { walls.Add(wall); keep.Add(wall.transform); }
            // Remove decorative objects while preserving the linked gameplay geometry.
            for (int i = arena.childCount - 1; i >= 0; i--)
                if (!keep.Contains(arena.GetChild(i))) Object.DestroyImmediate(arena.GetChild(i).gameObject);
            arena.name = "ARENA • 36 x 19.8";
            game.arenaHalfSize = new Vector2(18, 9.9f);
            game.startPosition = Vector3.zero;
            game.pocketEntryX = 18;
            game.initialBallDiameter = 3.2f;
            game.launchSpeed = 30.24f;
            game.deflectionLines.lineLifetime = 2;
            game.powerUps.shrinkPickupLifetime = 12;
            game.powerUps.growPickupLifetime = 5;
            var capture = game.GetComponent<CaptureLauncher>();
            if (capture == null) capture = game.gameObject.AddComponent<CaptureLauncher>();
            capture.game = game;
            game.captureLauncher = capture;
            game.name = "GAME • automatic launch and rules";
            float w = game.arenaHalfSize.x, h = game.arenaHalfSize.y;

            Rect("Arena floor", Vector2.zero, new Vector2(w * 2, h * 2), new Color(0.08f, 0.08f, 0.08f), 5);

            foreach (var wall in walls)
            {
                var part = wall.transform;
                if (part == pocket.upperWall || part == pocket.lowerWall)
                {
                    part.name = part == pocket.upperWall ? "Right boundary upper" : "Right boundary lower";
                    part.position = new Vector3(w + 0.12f, part.position.y, 0);
                    part.localScale = new Vector3(0.24f, part.localScale.y, 1);
                }
                else if (part.name.StartsWith("Top"))
                {
                    part.name = "Top boundary"; part.position = new Vector3(0, h + 0.12f, 0);
                    part.localScale = new Vector3(w * 2 + 0.48f, 0.24f, 1);
                }
                else if (part.name.StartsWith("Bottom"))
                {
                    part.name = "Bottom boundary"; part.position = new Vector3(0, -h - 0.12f, 0);
                    part.localScale = new Vector3(w * 2 + 0.48f, 0.24f, 1);
                }
                else
                {
                    part.name = "Left boundary"; part.position = new Vector3(-w - 0.12f, 0, 0);
                    part.localScale = new Vector3(0.24f, h * 2, 1);
                }
                wall.size = Vector2.one;
                for (int i = part.childCount - 1; i >= 0; i--)
                    Object.DestroyImmediate(part.GetChild(i).gameObject);
                var wallSprite = part.GetComponent<SpriteRenderer>();
                wallSprite.sprite = square;
                wallSprite.color = new Color(0.8f, 0.8f, 0.8f);
            }

            ConfigureExit(pocket.rim, "Exit outline", new Vector2(w + 0.46f, 0), Color.white);
            ConfigureExit(pocket.well, "Exit interior", new Vector2(w + 0.46f, 0), new Color(0.08f, 0.08f, 0.08f));
            ConfigureExit(pocket.mouth, "Exit threshold", new Vector2(w + 0.03f, 0), new Color(0.08f, 0.08f, 0.08f));
            var bouncePhysics = game.deflectionLines.bounceMaterial;
            pocket.upperBumper = ConfigureBumper(pocket.upperBumper, "Exit upper safe wall", new Vector2(w + 0.12f, 0), new Vector2(0.24f, pocket.bumperLength), bouncePhysics);
            pocket.lowerBumper = ConfigureBumper(pocket.lowerBumper, "Exit lower safe wall", new Vector2(w + 0.12f, 0), new Vector2(0.24f, pocket.bumperLength), bouncePhysics);
            ConfigureBumper(pocket.upperJaw, "Exit upper edge", new Vector2(w + 0.6f, 0), new Vector2(1.2f, 0.075f), bouncePhysics);
            ConfigureBumper(pocket.lowerJaw, "Exit lower edge", new Vector2(w + 0.6f, 0), new Vector2(1.2f, 0.075f), bouncePhysics);
            pocket.rim.GetComponent<SpriteRenderer>().enabled = false;
            pocket.upperJaw.position = new Vector3(w + 0.6f, pocket.upperJaw.position.y, 0);
            pocket.lowerJaw.position = new Vector3(w + 0.6f, pocket.lowerJaw.position.y, 0);
            pocket.mouth.localScale = new Vector3(0.8f, pocket.initialWidth, 1);
            pocket.upperJaw.localScale = pocket.lowerJaw.localScale = new Vector3(1.2f, 0.075f, 1);
            pocket.rectangularExit = true;
            game.pocketCenter.position = new Vector3(w + 0.42f, 0, 0);
            pocket.ResetOpening();
            game.ball.ResetSize(game.launchSpeed, game.initialBallDiameter);
            game.ball.transform.position = game.startPosition;
            game.ball.Body.position = game.startPosition;
            game.ball.GetComponent<SpriteRenderer>().sprite = circle;
            game.ball.GetComponent<SpriteRenderer>().color = Color.white;
            game.boardCamera.backgroundColor = new Color(0.08f, 0.08f, 0.08f);
            var cameraFit = game.boardCamera.GetComponent<ArenaCamera>();
            if (cameraFit == null) cameraFit = game.boardCamera.gameObject.AddComponent<ArenaCamera>();
            cameraFit.game = game; cameraFit.Fit();
            game.aimLine.enabled = false;
            game.aimLine.positionCount = 0;
            game.trail.enabled = false;
            game.arenaVersion = 13;
            Physics2D.SyncTransforms();
            EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
            EditorSceneManager.SaveScene(game.gameObject.scene);
            if (SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.in2DMode = true;
                SceneView.lastActiveSceneView.LookAt(Vector3.zero, Quaternion.identity, 24);
            }
            Debug.Log("BALL GAME: Saved prototype arena with red elastic exit bumpers.");
        }

        static Transform ConfigureBumper(Transform part, string name, Vector2 position, Vector2 size, PhysicsMaterial2D physics)
        {
            Color red = new Color(1, 0.2f, 0.2f);
            if (part == null) part = Rect(name, position, size, red, 13).transform;
            ConfigureExit(part, name, position, red);
            part.localScale = new Vector3(size.x, size.y, 1);
            part.gameObject.layer = ShrinkingPocket.BumperLayer;
            var collider = part.GetComponent<BoxCollider2D>();
            if (collider == null) collider = part.gameObject.AddComponent<BoxCollider2D>();
            collider.size = Vector2.one;
            collider.offset = Vector2.zero;
            collider.isTrigger = false;
            collider.sharedMaterial = physics;
            return part;
        }

        static void ConfigureExit(Transform part, string name, Vector2 position, Color color)
        {
            part.name = name; part.position = position;
            var sprite = part.GetComponent<SpriteRenderer>(); sprite.sprite = square; sprite.color = color;
        }
        static GameObject Rect(string name, Vector2 position, Vector2 size, Color color, int order)
        {
            var go = new GameObject(name); go.transform.SetParent(arena);
            go.transform.position = position; go.transform.localScale = new Vector3(size.x, size.y, 1);
            var sprite = go.AddComponent<SpriteRenderer>(); sprite.sprite = square; sprite.sharedMaterial = material;
            sprite.color = color; sprite.sortingOrder = order;
            return go;
        }
    }
}
