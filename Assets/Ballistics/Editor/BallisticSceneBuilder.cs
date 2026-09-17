using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ballistics.Editor
{
    public static class BallisticSceneBuilder
    {
        public const string ScenePath = "Assets/Ballistics/Scenes/BallisticLab.unity";

        [MenuItem("Ballistics/Crear o reconstruir escena")]
        public static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
        }

        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Salir de Play antes de construir.");
            foreach (string folder in new[] { "Scenes", "Prefabs", "Materials", "UI" })
                Directory.CreateDirectory("Assets/Ballistics/" + folder);
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var groundMat = Material("Ground", new Color(0.13f, 0.20f, 0.26f));
            var metal = Material("Launcher", new Color(0.24f, 0.36f, 0.42f));
            var orange = Material("Target", new Color(0.96f, 0.54f, 0.22f));
            var blue = Material("TargetAlternate", new Color(0.17f, 0.64f, 0.70f));
            var mint = Material("Projectile", new Color(0.40f, 0.94f, 0.73f));
            var trailMat = Material("Trajectory", new Color(0.4f, 0.85f, 0.74f), true);
            var contact = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>("Assets/Ballistics/Materials/Contact.physicsMaterial");
            if (contact == null)
            {
                contact = new PhysicsMaterial("Contact") { dynamicFriction = 0.65f, staticFriction = 0.7f, bounciness = 0.08f };
                AssetDatabase.CreateAsset(contact, "Assets/Ballistics/Materials/Contact.physicsMaterial");
            }

            var ground = Cube("Suelo", new Vector3(12, -0.3f, 0), new Vector3(90, 0.6f, 16), groundMat);
            ground.GetComponent<Collider>().sharedMaterial = contact;
            Cube("Base de lanzamiento", new Vector3(0, 0.35f, 0), new Vector3(1.8f, 0.7f, 1.5f), metal);
            var pivot = new GameObject("Pivote - ángulo").transform;
            pivot.position = new Vector3(0, 1.0f, 0);
            var barrel = Cube("Cañón visual", Vector3.zero, new Vector3(1.5f, 0.35f, 0.35f), metal);
            Object.DestroyImmediate(barrel.GetComponent<Collider>());
            barrel.transform.SetParent(pivot, false);
            barrel.transform.localPosition = new Vector3(0.6f, 0, 0);
            var muzzle = new GameObject("Boca de disparo").transform;
            muzzle.SetParent(pivot, false); muzzle.localPosition = new Vector3(1.7f, 0, 0);
            pivot.rotation = Quaternion.Euler(0, 0, 30);

            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "Proyectil"; ball.transform.localScale = Vector3.one * 0.48f;
            ball.GetComponent<Renderer>().sharedMaterial = mint;
            ball.GetComponent<Collider>().sharedMaterial = contact;
            var rb = ball.AddComponent<Rigidbody>();
            rb.linearDamping = 0; rb.angularDamping = 0.05f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.solverIterations = 12; rb.solverVelocityIterations = 6;
            ball.AddComponent<Projectile>();
            var trail = ball.AddComponent<TrailRenderer>();
            trail.time = 2.5f; trail.startWidth = 0.09f; trail.endWidth = 0.01f;
            trail.sharedMaterial = trailMat; trail.minVertexDistance = 0.05f;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; trail.receiveShadows = false;
            var projectilePrefab = PrefabUtility.SaveAsPrefabAsset(ball, "Assets/Ballistics/Prefabs/Projectile.prefab");
            Object.DestroyImmediate(ball);

            var structure = new GameObject("Estructura - 9 piezas");
            for (int column = 0; column < 3; column++)
            {
                Rigidbody below = null;
                for (int row = 0; row < 3; row++)
                {
                    var block = Cube($"Bloque {column + 1}.{row + 1}", new Vector3(column * 1.5f, 0.55f + row * 1.1f, 0), new Vector3(0.8f, 1.1f, 0.9f), (row + column) % 2 == 0 ? orange : blue);
                    block.transform.SetParent(structure.transform, false);
                    block.GetComponent<Collider>().sharedMaterial = contact;
                    var body = block.AddComponent<Rigidbody>(); body.mass = 0.8f;
                    body.solverIterations = 16; body.solverVelocityIterations = 8;
                    body.interpolation = RigidbodyInterpolation.Interpolate;
                    body.collisionDetectionMode = CollisionDetectionMode.Continuous;
                    var joint = block.AddComponent<FixedJoint>();
                    joint.connectedBody = below;
                    joint.breakForce = 65; joint.breakTorque = 45;
                    joint.enableCollision = false;
                    if (below == null)
                    {
                        // World anchor follows a prefab spawned at its final position.
                        // Connect to a kinematic foundation inside the prefab instead of world-space coordinates.
                        var anchor = new GameObject($"Anclaje {column + 1}");
                        anchor.transform.SetParent(structure.transform, false);
                        anchor.transform.localPosition = new Vector3(column * 1.5f, 0, 0);
                        var anchorBody = anchor.AddComponent<Rigidbody>(); anchorBody.isKinematic = true;
                        joint.connectedBody = anchorBody;
                    }
                    block.AddComponent<TargetPiece>(); below = body;
                }
            }
            var targetPrefab = PrefabUtility.SaveAsPrefabAsset(structure, "Assets/Ballistics/Prefabs/TargetStructure.prefab");
            Object.DestroyImmediate(structure);

            var controller = new GameObject("Simulador").AddComponent<BallisticSession>();
            controller.projectilePrefab = projectilePrefab.GetComponent<Projectile>();
            controller.barrelPivot = pivot; controller.muzzle = muzzle;
            var line = new GameObject("Trayectoria ideal").AddComponent<LineRenderer>();
            line.sharedMaterial = trailMat; line.startWidth = 0.025f; line.endWidth = 0.025f;
            line.useWorldSpace = true; line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false; controller.preview = line;

            // This visible scene object is the editable template cloned for every attempt.
            var preview = (GameObject)PrefabUtility.InstantiatePrefab(targetPrefab);
            preview.name = "Objetivos - plantilla editable";
            preview.transform.position = new Vector3(12, 0, 0);
            controller.structureTemplate = preview;

            var camera = new GameObject("Main Camera").AddComponent<Camera>(); camera.tag = "MainCamera";
            camera.transform.position = new Vector3(7.2f, 9.5f, -27);
            camera.transform.LookAt(new Vector3(7.2f, 4.0f, 0));
            camera.orthographic = true; camera.orthographicSize = 10.5f;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0.055f, 0.09f, 0.14f);
            camera.gameObject.AddComponent<AudioListener>();
            var light = new GameObject("Sun").AddComponent<Light>(); light.type = LightType.Directional;
            light.intensity = 2.0f; light.shadows = LightShadows.Soft; light.transform.rotation = Quaternion.Euler(45, -30, 0);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.62f, 0.72f);

            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/Ballistics/UI/SimulatorPanel.asset");
            if (panel == null) { panel = ScriptableObject.CreateInstance<PanelSettings>(); AssetDatabase.CreateAsset(panel, "Assets/Ballistics/UI/SimulatorPanel.asset"); }
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize; panel.referenceResolution = new Vector2Int(1280, 720);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight; panel.match = 0.5f;
            var ui = new GameObject("UI Toolkit - HUD");
            var doc = ui.AddComponent<UIDocument>(); doc.panelSettings = panel;
            doc.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Ballistics/UI/Simulator.uxml");
            ui.AddComponent<BallisticHUD>().session = controller;
            EditorUtility.SetDirty(panel);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            PlayerSettings.runInBackground = true;
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = controller.gameObject;
            Debug.Log("Ballistic Lab creado: " + ScenePath);
        }

        private static GameObject Cube(string name, Vector3 position, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
            go.transform.position = position; go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = material; return go;
        }

        private static Material Material(string name, Color color, bool unlit = false)
        {
            string path = "Assets/Ballistics/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color); EditorUtility.SetDirty(material); return material;
        }
    }
}

