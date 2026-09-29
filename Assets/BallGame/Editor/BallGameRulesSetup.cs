using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BallGame.Editor
{
    [InitializeOnLoad]
    public static class BallGameRulesSetup
    {
        const string Root = "Assets/BallGame";
        static BallGameRulesSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                var game = Object.FindFirstObjectByType<BallGameController>();
                if (game != null && game.rulesVersion < 5) ApplyRules();
            };
        }

        [MenuItem("Ball Game/Apply Current Game Rules")]
        public static void ApplyRules()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var game = Object.FindFirstObjectByType<BallGameController>();
            if (game == null) return;
            var wallPhysics = PhysicsAsset("Burning walls", 0);
            var linePhysics = PhysicsAsset("Deflection lines", 1);
            var lineMaterial = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/Player lines.mat");
            if (lineMaterial == null)
            {
                lineMaterial = new Material(Shader.Find("Sprites/Default"));
                AssetDatabase.CreateAsset(lineMaterial, Root + "/Materials/Player lines.mat");
            }
            var tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            tags.FindProperty("layers").GetArrayElementAtIndex(8).stringValue = "Cushions";
            tags.FindProperty("layers").GetArrayElementAtIndex(9).stringValue = "Deflectors";
            tags.ApplyModifiedPropertiesWithoutUndo();
            game.ball.GetComponent<CircleCollider2D>().sharedMaterial = wallPhysics;
            game.ball.name = "BALL • dynamic size and speed";
            foreach (var wall in Object.FindObjectsByType<BoxCollider2D>(FindObjectsSortMode.None))
            {
                if (wall.gameObject.layer != 8) continue;
                wall.sharedMaterial = wallPhysics;
                wall.GetComponent<SpriteRenderer>().color = game.arenaVersion >= 2 ? new Color(0.8f, 0.8f, 0.8f) : new Color(0.38f, 0.12f, 0.085f);
            }
            foreach (var sprite in Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
                if (sprite.name.Contains("cushion highlight")) sprite.color = new Color(0.92f, 0.34f, 0.16f);

            var spawner = game.GetComponent<PowerUpSpawner>();
            if (spawner == null) spawner = game.gameObject.AddComponent<PowerUpSpawner>();
            spawner.game = game;
            spawner.circleSprite = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "/Sprites/Circle.png");
            spawner.squareSprite = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "/Sprites/Square.png");
            spawner.spriteMaterial = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/Table sprites.mat");
            var lines = game.GetComponent<DeflectionLines>();
            if (lines == null) lines = game.gameObject.AddComponent<DeflectionLines>();
            lines.game = game; lines.lineMaterial = lineMaterial; lines.bounceMaterial = linePhysics;
            var pocket = game.GetComponent<ShrinkingPocket>();
            if (pocket == null) pocket = game.gameObject.AddComponent<ShrinkingPocket>();
            pocket.game = game;
            pocket.shrinkInterval = 100;
            pocket.upperWall = Part("Right boundary upper") ?? Part("Right cushion upper");
            pocket.lowerWall = Part("Right boundary lower") ?? Part("Right cushion lower");
            pocket.upperJaw = Part("Exit upper edge") ?? Part("Top pocket jaw");
            pocket.lowerJaw = Part("Exit lower edge") ?? Part("Bottom pocket jaw");
            pocket.rim = Part("Exit outline") ?? Part("Pocket brass surround");
            pocket.well = Part("Exit interior") ?? Part("Pocket dark well");
            pocket.mouth = Part("Exit threshold") ?? Part("Open pocket mouth");
            pocket.goalTrigger = game.pocketCenter.GetComponent<BoxCollider2D>();
            game.powerUps = spawner; game.deflectionLines = lines; game.shrinkingPocket = pocket;
            game.aimLine.sharedMaterial = lineMaterial;
            game.aimLine.name = "AIM • first contact guide";
            game.rulesVersion = 5;
            EditorUtility.SetDirty(game);
            EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
            EditorSceneManager.SaveScene(game.gameObject.scene);
            AssetDatabase.SaveAssets();
            Debug.Log("BALL GAME: Burning walls, size pickups, drawing, and the 100-second shrinking hole are saved.");
        }

        static Transform Part(string name) => GameObject.Find(name)?.transform;
        static PhysicsMaterial2D PhysicsAsset(string name, float bounce)
        {
            string path = Root + "/Materials/" + name + ".physicsMaterial2D";
            var material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(path);
            if (material == null) { material = new PhysicsMaterial2D(name); AssetDatabase.CreateAsset(material, path); }
            material.friction = 0; material.bounciness = bounce;
            material.bounceCombine = PhysicsMaterialCombine2D.Maximum;
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
