using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BallGame.Editor
{
    [InitializeOnLoad]
    public static class BallGameSetup
    {
        const string Root = "Assets/BallGame";
        public const string ScenePath = Root + "/Scenes/Ball Game 2D.unity";
        static PhysicsMaterial2D elastic;
        static Transform table;
        static Sprite square, circle, ballSprite;
        static Material spriteMaterial;

        static BallGameSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if ((!File.Exists(ScenePath) || !File.Exists(Root + "/Materials/2D Pipeline.asset")) && !EditorApplication.isPlayingOrWillChangePlaymode) Build();
            };
        }

        [MenuItem("Ball Game/Rebuild Arena Scene")]
        public static void Build()
        {
            Directory.CreateDirectory(Root + "/Scenes");
            Directory.CreateDirectory(Root + "/Materials");
            Directory.CreateDirectory(Root + "/Sprites");
            AssetDatabase.Refresh();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSettings.defaultBehaviorMode = EditorBehaviorMode.Mode2D;
            var renderer2D = ScriptableObject.CreateInstance<Renderer2DData>();
            AssetDatabase.CreateAsset(renderer2D, Root + "/Materials/2D Renderer.asset");
            var pipeline = UniversalRenderPipelineAsset.Create(renderer2D);
            AssetDatabase.CreateAsset(pipeline, Root + "/Materials/2D Pipeline.asset");
            GraphicsSettings.defaultRenderPipeline = pipeline;
            int currentQuality = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipeline;
            }
            QualitySettings.SetQualityLevel(currentQuality, false);
            PlayerSettings.productName = "ball game";
            PlayerSettings.companyName = "Physics Playground";
            PlayerSettings.defaultScreenWidth = 1200;
            PlayerSettings.defaultScreenHeight = 800;
            PlayerSettings.runInBackground = true;
            Time.fixedDeltaTime = 1f / 120f;
            Physics2D.gravity = Vector2.zero;
            Physics2D.velocityIterations = 12;
            Physics2D.positionIterations = 8;
            Physics2D.bounceThreshold = 0;

            var tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            tags.FindProperty("layers").GetArrayElementAtIndex(8).stringValue = "Cushions";
            tags.ApplyModifiedPropertiesWithoutUndo();
            elastic = new PhysicsMaterial2D("Perfect Elasticity 2D") { bounciness = 1, friction = 0 };
            AssetDatabase.CreateAsset(elastic, Root + "/Materials/Perfect Elasticity 2D.physicsMaterial2D");
            spriteMaterial = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
            AssetDatabase.CreateAsset(spriteMaterial, Root + "/Materials/Table sprites.mat");
            square = MakeSprite("Square", false, false);
            circle = MakeSprite("Circle", true, false);
            ballSprite = MakeSprite("Ivory ball", true, true);
            var feltSprite = MakeFelt();

            Color felt = Hex("12664F"), cushion = Hex("0D4939"), wood = Hex("64412C");
            Color darkWood = Hex("35281F"), gold = Hex("B69A62"), pale = Hex("D4C9A4");
            table = new GameObject("TABLE • 2D single pocket").transform;
            Rect("Table shadow", new Vector2(0.08f, -0.15f), new Vector2(14.1f, 8.35f), Hex("071715"), 0);
            Rect("Dark outer frame", Vector2.zero, new Vector2(13.9f, 8.14f), darkWood, 1);
            Rect("Fine brass border", Vector2.zero, new Vector2(13.74f, 7.98f), gold, 2);
            Rect("Walnut frame", Vector2.zero, new Vector2(13.66f, 7.90f), wood, 3);
            Rect("Inset shadow", Vector2.zero, new Vector2(12.76f, 7.38f), Hex("06271F"), 4);
            var surface = Rect("Green felt • playing surface", Vector2.zero, new Vector2(12, 6.6f), felt, 5);
            surface.GetComponent<SpriteRenderer>().sprite = feltSprite;
            // Soft stripes supply depth while all geometry and physics stay in 2D.
            for (int i = 0; i < 9; i++)
                Rect("Felt nap stripe", new Vector2(-5.33f + i * 1.333f, 0), new Vector2(0.666f, 6.6f), new Color(0.15f, 0.5f, 0.37f, 0.08f), 6);

            Wall("Top cushion", new Vector2(0, 3.42f), new Vector2(12.48f, 0.24f), cushion);
            Wall("Bottom cushion", new Vector2(0, -3.42f), new Vector2(12.48f, 0.24f), cushion);
            Wall("Left cushion", new Vector2(-6.12f, 0), new Vector2(0.24f, 6.6f), cushion);
            Wall("Right cushion upper", new Vector2(6.12f, 2.11f), new Vector2(0.24f, 2.38f), cushion);
            Wall("Right cushion lower", new Vector2(6.12f, -2.11f), new Vector2(0.24f, 2.38f), cushion);
            Rect("Top cushion highlight", new Vector2(0, 3.32f), new Vector2(12, 0.035f), Hex("358267"), 9);
            Rect("Bottom cushion highlight", new Vector2(0, -3.32f), new Vector2(12, 0.035f), Hex("358267"), 9);
            Rect("Left cushion highlight", new Vector2(-6.02f, 0), new Vector2(0.035f, 6.6f), Hex("358267"), 9);

            // One opening, in the right wall. The two right cushion colliders stop at y +/-0.92.
            Disc("Pocket brass surround", new Vector2(6.32f, 0), 1.99f, gold, 10);
            Disc("Pocket dark well", new Vector2(6.32f, 0), 1.82f, Hex("020D0B"), 11);
            Rect("Open pocket mouth", new Vector2(6.01f, 0), new Vector2(0.68f, 1.84f), Hex("020D0B"), 12);
            Rect("Top pocket jaw", new Vector2(6.20f, 0.95f), new Vector2(0.47f, 0.10f), gold, 13);
            Rect("Bottom pocket jaw", new Vector2(6.20f, -0.95f), new Vector2(0.47f, 0.10f), gold, 13);
            // A small chevron identifies the target without adding another hole.
            Segment("Pocket marker upper", new Vector2(6.25f, 0.16f), new Vector2(6.43f, 0), Hex("706449"), 0.024f, 14);
            Segment("Pocket marker lower", new Vector2(6.43f, 0), new Vector2(6.25f, -0.16f), Hex("706449"), 0.024f, 14);

            for (int i = -2; i <= 2; i++)
            {
                Diamond(new Vector2(i * 2, 3.76f), pale);
                Diamond(new Vector2(i * 2, -3.76f), pale);
            }
            Diamond(new Vector2(-6.53f, 1.8f), pale);
            Diamond(new Vector2(-6.53f, -1.8f), pale);
            Diamond(new Vector2(6.53f, 2.3f), pale);
            Diamond(new Vector2(6.53f, -2.3f), pale);
            Color marking = new Color(0.43f, 0.69f, 0.54f, 0.36f);
            Segment("Baulk line", new Vector2(-3.6f, -3.28f), new Vector2(-3.6f, 3.28f), marking, 0.018f, 7);
            for (int i = 0; i < 64; i++)
            {
                float a = (90 + i * 180f / 64) * Mathf.Deg2Rad;
                float b = (90 + (i + 1) * 180f / 64) * Mathf.Deg2Rad;
                Vector2 c = new Vector2(-3.6f, 0);
                Segment("Baulk semicircle", c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 1.22f,
                    c + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * 1.22f, marking, 0.018f, 7);
            }
            Disc("Center spot", Vector2.zero, 0.075f, pale, 7);

            var camera = new GameObject("Main Camera • orthographic 2D").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(0.2f, 0.20f, -10);
            camera.orthographic = true;
            camera.orthographicSize = 6.7f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Hex("0B2022");
            camera.nearClipPlane = 0.1f; camera.farClipPlane = 50;
            camera.gameObject.AddComponent<AudioListener>();

            var game = new GameObject("GAME • aim, shoot, reset").AddComponent<BallGameController>();
            var ballObject = Disc("BALL • perfectly elastic 2D", game.startPosition, 0.5f, Color.white, 25);
            ballObject.transform.SetParent(null);
            ballObject.GetComponent<SpriteRenderer>().sprite = ballSprite;
            var collider = ballObject.AddComponent<CircleCollider2D>();
            collider.radius = 0.5f;
            collider.sharedMaterial = elastic;
            var body = ballObject.AddComponent<Rigidbody2D>();
            body.mass = 1;
            body.gravityScale = 0;
            body.linearDamping = 0; body.angularDamping = 0;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.sleepMode = RigidbodySleepMode2D.NeverSleep;
            var ball = ballObject.AddComponent<ElasticBall>();
            ball.game = game;
            var trail = ballObject.AddComponent<TrailRenderer>();
            trail.sharedMaterial = LineMaterial("Ball trail", Hex("79AD8B"));
            trail.time = 0.35f; trail.startWidth = 0.035f; trail.endWidth = 0.002f;
            trail.minVertexDistance = 0.05f; trail.emitting = false; trail.sortingOrder = 20;
            var guide = new GameObject("AIM • 2D reflection guide").AddComponent<LineRenderer>();
            guide.sharedMaterial = LineMaterial("Aim guide", Hex("91AE78"));
            guide.startWidth = 0.024f; guide.endWidth = 0.007f;
            guide.positionCount = 0; guide.sortingOrder = 18; guide.numCornerVertices = 4;
            var goal = new GameObject("GOAL • single pocket trigger 2D");
            goal.transform.position = new Vector3(6.42f, 0, 0);
            var trigger = goal.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true; trigger.size = new Vector2(0.44f, 1.62f);
            goal.AddComponent<GoalPocket>().game = game;
            game.ball = ball; game.boardCamera = camera; game.trail = trail;
            game.aimLine = guide; game.pocketCenter = goal.transform;

            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath);
            BallGameRulesSetup.ApplyRules();
            ArenaSceneSetup.Apply();
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            if (SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.in2DMode = true;
                SceneView.lastActiveSceneView.LookAt(Vector3.zero, Quaternion.identity, 9);
            }
            Selection.activeGameObject = game.gameObject;
            Debug.Log("BALL GAME 2D: Scene created and saved. Press Play to aim and shoot.");
        }

        static Color Hex(string value) { ColorUtility.TryParseHtmlString("#" + value, out var c); return c; }

        static Sprite MakeSprite(string name, bool round, bool shaded)
        {
            int size = round ? 128 : 8;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2((x + 0.5f) / size * 2 - 1, (y + 0.5f) / size * 2 - 1);
                float alpha = round ? Mathf.Clamp01((1 - p.magnitude) * size * 0.5f) : 1;
                float shade = shaded ? Mathf.Clamp01(0.68f + 0.26f * Mathf.Sqrt(Mathf.Max(0, 1 - p.sqrMagnitude)) + 0.17f * (-p.x + p.y)) : 1;
                texture.SetPixel(x, y, new Color(shade, shade * (shaded ? 0.975f : 1), shade * (shaded ? 0.89f : 1), alpha));
            }
            texture.Apply();
            return SaveSprite(name, texture);
        }

        static Sprite MakeFelt()
        {
            var texture = new Texture2D(512, 512, TextureFormat.RGBA32, false);
            var random = new System.Random(526);
            for (int y = 0; y < 512; y++) for (int x = 0; x < 512; x++)
            {
                float n = 0.93f + (float)random.NextDouble() * 0.07f;
                texture.SetPixel(x, y, new Color(n, n, n, 1));
            }
            texture.Apply();
            return SaveSprite("Felt texture", texture);
        }

        static Sprite SaveSprite(string name, Texture2D texture)
        {
            string path = Root + "/Sprites/" + name + ".png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = name == "Felt texture" ? 512 : name == "Square" ? 8 : 128;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            return sprite;
        }

        static GameObject Rect(string name, Vector2 position, Vector2 size, Color color, int order)
        {
            var go = new GameObject(name); go.transform.SetParent(table);
            go.transform.position = position; go.transform.localScale = new Vector3(size.x, size.y, 1);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = square; renderer.color = color; renderer.sharedMaterial = spriteMaterial; renderer.sortingOrder = order;
            return go;
        }
        static GameObject Disc(string name, Vector2 position, float diameter, Color color, int order)
        {
            var go = Rect(name, position, new Vector2(diameter, diameter), color, order);
            go.GetComponent<SpriteRenderer>().sprite = circle;
            return go;
        }
        static void Wall(string name, Vector2 position, Vector2 size, Color color)
        {
            var go = Rect(name, position, size, color, 8); go.layer = 8;
            var collider = go.AddComponent<BoxCollider2D>(); collider.size = Vector2.one; collider.sharedMaterial = elastic;
        }
        static void Diamond(Vector2 position, Color color)
        {
            Rect("Ivory diamond sight", position, new Vector2(0.095f, 0.095f), color, 15).transform.rotation = Quaternion.Euler(0, 0, 45);
        }
        static void Segment(string name, Vector2 start, Vector2 end, Color color, float width, int order)
        {
            var go = Rect(name, (start + end) * 0.5f, new Vector2(Vector2.Distance(start, end), width), color, order);
            go.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(end.y - start.y, end.x - start.x) * Mathf.Rad2Deg);
        }
        static Material LineMaterial(string name, Color color)
        {
            var mat = new Material(Shader.Find("Sprites/Default")); mat.color = color;
            AssetDatabase.CreateAsset(mat, Root + "/Materials/" + name + ".mat");
            return mat;
        }
    }
}
