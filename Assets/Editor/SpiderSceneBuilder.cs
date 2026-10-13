using System.IO;
using SpiderPlayground;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace SpiderPlaygroundEditor
{
    [InitializeOnLoad]
    public static class SpiderSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/SpiderPlayground.unity";
        private const string InitialSceneOpenedKey = "SpiderPlayground.InitialSceneOpened";

        static SpiderSceneBuilder()
        {
            EditorApplication.delayCall += OpenInitialScene;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
                EditorApplication.delayCall += OpenInitialScene;
        }

        private static void OpenInitialScene()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += OpenInitialScene;
                return;
            }

            if (SessionState.GetBool(InitialSceneOpenedKey, false)) return;

            Scene current = SceneManager.GetActiveScene();
            if (!IsUntouchedStarterScene(current)) return;

            // Unity can restore its empty Untitled scene in Play mode. Stop it first,
            // then open the saved game scene when Edit mode resumes.
            if (EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = false;
                return;
            }
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            if (File.Exists(ScenePath))
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            else
                BuildScene();

            SessionState.SetBool(InitialSceneOpenedKey, true);
        }

        private static bool IsUntouchedStarterScene(Scene scene)
        {
            if (!scene.IsValid() || !string.IsNullOrEmpty(scene.path) || scene.name != "Untitled")
                return false;

            GameObject[] roots = scene.GetRootGameObjects();
            if (roots.Length > 3) return false;
            foreach (GameObject root in roots)
            {
                if (root.name != "Main Camera" && root.name != "Directional Light" &&
                    root.name != "Global Volume") return false;
            }
            return true;
        }

        [MenuItem("Spider Game/Open Demo Scene")]
        public static void OpenDemoScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            if (File.Exists(ScenePath))
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            else
                BuildScene();

            SessionState.SetBool(InitialSceneOpenedKey, true);
        }

        [MenuItem("Spider Game/Rebuild Demo Scene")]
        public static void BuildScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            EnsureFolder("Assets/Scenes");
            EnsureFolder("Assets/Materials");

            Material groundMaterial = GetMaterial("Ground", new Color(0.15f, 0.19f, 0.23f));
            Material gridMaterial = GetMaterial("Grid", new Color(0.25f, 0.31f, 0.36f));
            Material buildingMaterial = GetMaterial("Building", new Color(0.31f, 0.37f, 0.46f));
            Material roofMaterial = GetMaterial("Roof", new Color(0.41f, 0.48f, 0.56f));
            Material redMaterial = GetMaterial("SuitRed", new Color(0.78f, 0.09f, 0.12f));
            Material blueMaterial = GetMaterial("SuitBlue", new Color(0.06f, 0.16f, 0.47f));
            Material leftWebMaterial = GetWebMaterial("LeftWeb");
            Material rightWebMaterial = GetWebMaterial("RightWeb");

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.52f, 0.56f, 0.65f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.18f, 0.27f, 0.39f);
            RenderSettings.fogDensity = 0.003f;

            GameObject lightObject = new GameObject("Sun");
            Light sun = lightObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.4f;
            lightObject.transform.rotation = Quaternion.Euler(48f, -28f, 0f);

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Ground";
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(240f, 1f, 240f);
            ground.GetComponent<Renderer>().sharedMaterial = groundMaterial;

            for (int i = -10; i <= 10; i++)
            {
                CreateGridLine("Grid X " + i, new Vector3(i * 10f, 0.008f, 0f),
                    new Vector3(0.035f, 0.012f, 200f), gridMaterial);
                CreateGridLine("Grid Z " + i, new Vector3(0f, 0.008f, i * 10f),
                    new Vector3(200f, 0.012f, 0.035f), gridMaterial);
            }

            CreateBuilding("Central Block - Replace with Building Later",
                new Vector3(0f, 19f, 0f), new Vector3(22f, 38f, 22f),
                buildingMaterial, roofMaterial);
            CreateBuilding("Second Block - Replace with Building Later",
                new Vector3(35f, 16f, 18f), new Vector3(18f, 32f, 18f),
                buildingMaterial, roofMaterial);

            GameObject player = new GameObject("Spider Hero");
            player.transform.position = new Vector3(0f, 0.08f, -36f);
            CapsuleCollider capsule = player.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 1f, 0f);
            capsule.height = 2f;
            capsule.radius = 0.35f;
            Rigidbody body = player.AddComponent<Rigidbody>();
            body.mass = 1.2f;
            body.linearDamping = 0.12f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.constraints = RigidbodyConstraints.FreezeRotation;

            GameObject appearance = new GameObject("Simple Faceless Body");
            appearance.transform.SetParent(player.transform, false);
            Transform bodyRoot = appearance.transform;
            CreatePart("Torso", PrimitiveType.Capsule, bodyRoot,
                new Vector3(0f, 1.31f, 0f), new Vector3(0.55f, 0.38f, 0.32f),
                Quaternion.identity, redMaterial);
            CreatePart("Hips", PrimitiveType.Sphere, bodyRoot,
                new Vector3(0f, 0.94f, 0f), new Vector3(0.47f, 0.25f, 0.32f),
                Quaternion.identity, blueMaterial);
            CreatePart("Faceless Head", PrimitiveType.Sphere, bodyRoot,
                new Vector3(0f, 1.82f, 0f), new Vector3(0.27f, 0.32f, 0.28f),
                Quaternion.identity, redMaterial);
            CreatePart("Left Arm", PrimitiveType.Capsule, bodyRoot,
                new Vector3(-0.38f, 1.18f, 0.03f), new Vector3(0.15f, 0.32f, 0.15f),
                Quaternion.Euler(0f, 0f, -9f), redMaterial);
            CreatePart("Right Arm", PrimitiveType.Capsule, bodyRoot,
                new Vector3(0.38f, 1.18f, 0.03f), new Vector3(0.15f, 0.32f, 0.15f),
                Quaternion.Euler(0f, 0f, 9f), redMaterial);
            Transform leftHand = CreatePart("Left Hand", PrimitiveType.Sphere, bodyRoot,
                new Vector3(-0.37f, 0.86f, 0.06f), new Vector3(0.15f, 0.16f, 0.17f),
                Quaternion.identity, redMaterial).transform;
            Transform rightHand = CreatePart("Right Hand", PrimitiveType.Sphere, bodyRoot,
                new Vector3(0.37f, 0.86f, 0.06f), new Vector3(0.15f, 0.16f, 0.17f),
                Quaternion.identity, redMaterial).transform;
            CreatePart("Left Leg", PrimitiveType.Capsule, bodyRoot,
                new Vector3(-0.19f, 0.52f, 0f), new Vector3(0.19f, 0.45f, 0.19f),
                Quaternion.identity, blueMaterial);
            CreatePart("Right Leg", PrimitiveType.Capsule, bodyRoot,
                new Vector3(0.19f, 0.52f, 0f), new Vector3(0.19f, 0.45f, 0.19f),
                Quaternion.identity, blueMaterial);

            GameObject cameraObject = new GameObject("Player Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = player.transform.position + new Vector3(0f, 2.42f, -5.2f);
            Camera playerCamera = cameraObject.AddComponent<Camera>();
            playerCamera.clearFlags = CameraClearFlags.SolidColor;
            playerCamera.backgroundColor = new Color(0.18f, 0.27f, 0.39f);
            playerCamera.fieldOfView = 95f;
            playerCamera.nearClipPlane = 0.03f;
            playerCamera.farClipPlane = 600f;
            cameraObject.AddComponent<AudioListener>();

            Transform firstLeft = CreatePart("First Person Left Hand", PrimitiveType.Sphere,
                cameraObject.transform, new Vector3(-0.34f, -0.26f, 0.65f),
                new Vector3(0.17f, 0.17f, 0.25f), Quaternion.identity, redMaterial).transform;
            Transform firstRight = CreatePart("First Person Right Hand", PrimitiveType.Sphere,
                cameraObject.transform, new Vector3(0.34f, -0.26f, 0.65f),
                new Vector3(0.17f, 0.17f, 0.25f), Quaternion.identity, redMaterial).transform;

            SpiderController controller = player.AddComponent<SpiderController>();
            controller.Configure(playerCamera, leftHand, rightHand, firstLeft, firstRight,
                appearance.GetComponentsInChildren<Renderer>(),
                cameraObject.GetComponentsInChildren<Renderer>(), leftWebMaterial, rightWebMaterial);
            foreach (Renderer handRenderer in cameraObject.GetComponentsInChildren<Renderer>())
                handRenderer.enabled = false;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("Spider Game scene is ready. Press Play to swing between the blocks.");
        }

        private static void CreateBuilding(string name, Vector3 position, Vector3 size,
            Material buildingMaterial, Material roofMaterial)
        {
            GameObject building = GameObject.CreatePrimitive(PrimitiveType.Cube);
            building.name = name;
            building.transform.position = position;
            building.transform.localScale = size;
            building.GetComponent<Renderer>().sharedMaterial = buildingMaterial;
            building.AddComponent<WebAttachSurface>();

            GameObject roof = GameObject.CreatePrimitive(PrimitiveType.Quad);
            roof.name = "Roof Surface";
            roof.transform.position = position + Vector3.up * (size.y * 0.5f + 0.012f);
            roof.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            roof.transform.localScale = new Vector3(size.x, size.z, 1f);
            roof.transform.SetParent(building.transform, true);
            Renderer roofRenderer = roof.GetComponent<Renderer>();
            roofRenderer.sharedMaterial = roofMaterial;
            roofRenderer.shadowCastingMode = ShadowCastingMode.Off;
            roofRenderer.receiveShadows = true;
            Object.DestroyImmediate(roof.GetComponent<Collider>());
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static Material GetMaterial(string name, Color color)
        {
            string path = "Assets/Materials/" + name + ".mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            Shader shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            Material material = new Material(shader);
            material.name = name;
            material.color = color;
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static Material GetWebMaterial(string name)
        {
            string path = "Assets/Materials/" + name + ".mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            Material material = new Material(shader);
            material.name = name;
            material.color = Color.white;
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void CreateGridLine(string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject line = GameObject.CreatePrimitive(PrimitiveType.Cube);
            line.name = name;
            line.transform.position = position;
            line.transform.localScale = scale;
            line.GetComponent<Renderer>().sharedMaterial = material;
            Object.DestroyImmediate(line.GetComponent<Collider>());
        }

        private static GameObject CreatePart(string name, PrimitiveType shape, Transform parent,
            Vector3 localPosition, Vector3 localScale, Quaternion localRotation, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(shape);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            part.transform.localRotation = localRotation;
            part.GetComponent<Renderer>().sharedMaterial = material;
            Object.DestroyImmediate(part.GetComponent<Collider>());
            return part;
        }
    }
}
