using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Edgevision.EditorTools
{
    /// <summary>
    /// One-click procedural builder for the Edgevision greybox test room.
    /// Menu: Edgevision > Build Prototype Room.
    ///
    /// Creates and saves Assets/Scenes/PrototypeRoom.unity containing:
    ///   - room shell (floor, 4 walls, ceiling), dividing wall with a narrow
    ///     doorway, and a 2.4 m ledge in front of the exit,
    ///   - a first-person player (CharacterController + camera + scripts),
    ///   - three grabbable cubes of different sizes on the "Grabbable" layer,
    ///   - an exit trigger zone on the ledge with a green emissive pad,
    ///   - lighting and a uGUI HUD (crosshair, prompt, completion panel),
    /// and registers the scene in Build Settings. All component references are
    /// wired automatically. Re-running the menu item rebuilds from scratch.
    ///
    /// Puzzle intent: jump apex is 1.5 m and the ledge is 2.4 m, so the player
    /// needs an intermediate step of roughly 1.1–1.3 m. Every starting cube
    /// (0.5 / 1.0 / 1.5 m) must be resized with the forced-perspective mechanic
    /// to reach that range, then used as a step next to the ledge.
    /// </summary>
    public static class PrototypeRoomBuilder
    {
        private const string ScenesFolder = "Assets/Scenes";
        private const string MaterialsFolder = "Assets/Materials";
        private const string ScenePath = ScenesFolder + "/PrototypeRoom.unity";
        private const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";
        private const string GrabbableLayerName = "Grabbable";

        // Room interior: x in [-12, 12], z in [-6, 6], y in [0, 4].
        private const float RoomLength = 24f;
        private const float RoomWidth = 12f;
        private const float RoomHeight = 4f;
        private const float WallThickness = 1f;

        // Dividing wall with doorway, located at x = 4.
        private const float DoorWallX = 4f;
        private const float DoorWallThickness = 0.5f;
        private const float DoorWidth = 1.4f;
        private const float DoorHeight = 2.3f;

        // Ledge at the east end. Taller than the 1.35 m jump apex -> needs a step.
        private const float LedgeHeight = 2.4f;
        private const float LedgeDepth = 3f;

        [MenuItem("Edgevision/Build Prototype Room")]
        public static void Build()
        {
            EnsureFolder(MaterialsFolder);
            EnsureGrabbableLayer();

            Material floorMat = CreateMaterial("M_Floor", new Color(0.55f, 0.56f, 0.58f));
            Material wallMat = CreateMaterial("M_Wall", new Color(0.72f, 0.72f, 0.74f));
            Material ledgeMat = CreateMaterial("M_Ledge", new Color(0.60f, 0.63f, 0.68f));
            Material goalMat = CreateMaterial("M_Goal", new Color(0.20f, 0.80f, 0.30f));
            MakeEmissive(goalMat, new Color(0.15f, 1.6f, 0.35f));
            Material[] cubeMats =
            {
                CreateMaterial("M_Cube_Small",  new Color(0.85f, 0.55f, 0.35f)),
                CreateMaterial("M_Cube_Medium", new Color(0.45f, 0.65f, 0.90f)),
                CreateMaterial("M_Cube_Large",  new Color(0.75f, 0.45f, 0.70f)),
            };

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var room = new GameObject("Room");
            BuildShell(room.transform, floorMat, wallMat);
            BuildDoorWall(room.transform, wallMat);
            BuildLedge(room.transform, ledgeMat);

            BuildLighting();
            GameUI gameUI = BuildUI();
            BuildPlayer(gameUI);
            BuildCubes(cubeMats);
            BuildGoal(goalMat, gameUI);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();

            Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            Debug.Log("[PrototypeRoomBuilder] Built and saved " + ScenePath +
                      " and added it to Build Settings. Press Play to test.");
        }

        // ---------------------------------------------------------------- room

        private static void BuildShell(Transform parent, Material floorMat, Material wallMat)
        {
            float halfL = RoomLength / 2f, halfW = RoomWidth / 2f, halfT = WallThickness / 2f;
            // Floor top surface sits exactly at y = 0.
            CreateBox("Floor", new Vector3(0f, -halfT, 0f), new Vector3(RoomLength, WallThickness, RoomWidth), floorMat, parent);
            CreateBox("Ceiling", new Vector3(0f, RoomHeight + halfT, 0f), new Vector3(RoomLength, WallThickness, RoomWidth), wallMat, parent);
            CreateBox("WallWest", new Vector3(-halfL - halfT, RoomHeight / 2f, 0f), new Vector3(WallThickness, RoomHeight, RoomWidth), wallMat, parent);
            CreateBox("WallEast", new Vector3(halfL + halfT, RoomHeight / 2f, 0f), new Vector3(WallThickness, RoomHeight, RoomWidth), wallMat, parent);
            CreateBox("WallNorth", new Vector3(0f, RoomHeight / 2f, halfW + halfT), new Vector3(RoomLength + 2f * WallThickness, RoomHeight, WallThickness), wallMat, parent);
            CreateBox("WallSouth", new Vector3(0f, RoomHeight / 2f, -halfW - halfT), new Vector3(RoomLength + 2f * WallThickness, RoomHeight, WallThickness), wallMat, parent);
        }

        private static void BuildDoorWall(Transform parent, Material wallMat)
        {
            float sideWidth = (RoomWidth - DoorWidth) / 2f;      // 5.3
            float sideCenterZ = DoorWidth / 2f + sideWidth / 2f; // 3.35
            CreateBox("DoorWall_Left", new Vector3(DoorWallX, RoomHeight / 2f, -sideCenterZ),
                new Vector3(DoorWallThickness, RoomHeight, sideWidth), wallMat, parent);
            CreateBox("DoorWall_Right", new Vector3(DoorWallX, RoomHeight / 2f, sideCenterZ),
                new Vector3(DoorWallThickness, RoomHeight, sideWidth), wallMat, parent);
            float lintelHeight = RoomHeight - DoorHeight; // 1.7
            CreateBox("DoorWall_Lintel", new Vector3(DoorWallX, DoorHeight + lintelHeight / 2f, 0f),
                new Vector3(DoorWallThickness, lintelHeight, DoorWidth), wallMat, parent);
        }

        private static void BuildLedge(Transform parent, Material ledgeMat)
        {
            float centerX = RoomLength / 2f - LedgeDepth / 2f; // 10.5, spans x in [9, 12]
            CreateBox("Ledge", new Vector3(centerX, LedgeHeight / 2f, 0f),
                new Vector3(LedgeDepth, LedgeHeight, RoomWidth), ledgeMat, parent);
        }

        private static GameObject CreateBox(string name, Vector3 position, Vector3 size, Material mat, Transform parent)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go; // primitive cube already has a BoxCollider
        }

        // ---------------------------------------------------------------- lighting

        private static void BuildLighting()
        {
            var sun = new GameObject("Directional Light");
            Light light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.96f, 0.88f);
            light.intensity = 1.3f;
            light.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(50f, -35f, 0f);

            // Interior fill lights so the greybox reads well without baking.
            CreatePointLight("CeilingLight_A", new Vector3(-5f, RoomHeight - 0.4f, 0f));
            CreatePointLight("CeilingLight_B", new Vector3(7f, RoomHeight - 0.4f, 0f));

            RenderSettings.skybox = null;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.42f, 0.44f, 0.47f);
        }

        private static void CreatePointLight(string name, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            Light l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.98f, 0.92f);
            l.intensity = 1.6f;
            l.range = 14f;
            l.shadows = LightShadows.None;
        }

        // ---------------------------------------------------------------- player

        private static void BuildPlayer(GameUI gameUI)
        {
            InputActionAsset actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            if (actions == null)
                Debug.LogError("[PrototypeRoomBuilder] Could not load " + InputActionsPath +
                               " — assign the input actions asset manually on PlayerController/ObjectGrabber.");

            var player = new GameObject("Player");
            player.tag = "Player";
            player.layer = 2; // built-in "Ignore Raycast": interaction queries never hit the player
            player.transform.position = new Vector3(-9f, 0.1f, 0f);
            player.transform.rotation = Quaternion.LookRotation(Vector3.right); // face the doorway

            var cc = player.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.35f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.slopeLimit = 50f;
            cc.stepOffset = 0.4f;
            cc.skinWidth = 0.05f;

            var camGo = new GameObject("PlayerCamera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(player.transform, false);
            camGo.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            Camera cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 70f;
            cam.nearClipPlane = 0.05f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.12f, 0.12f, 0.14f);
            camGo.AddComponent<AudioListener>();

            var controller = player.AddComponent<PlayerController>();
            var grabber = player.AddComponent<ObjectGrabber>();
            var highlight = player.AddComponent<ObjectHighlight>();

            int grabbableLayer = LayerMask.NameToLayer(GrabbableLayerName);
            if (grabbableLayer < 0)
                Debug.LogError("[PrototypeRoomBuilder] '" + GrabbableLayerName + "' layer missing after setup.");

            SetObjectField(controller, "inputActions", actions);
            SetObjectField(controller, "cameraTransform", camGo.transform);

            SetObjectField(grabber, "inputActions", actions);
            SetObjectField(grabber, "playerCamera", cam);
            SetObjectField(grabber, "highlight", highlight);
            SetObjectField(grabber, "ui", gameUI);
            SetLayerMaskField(grabber, "grabbableMask", 1 << grabbableLayer);
            SetLayerMaskField(grabber, "obstructionMask", 1 << 0); // Default layer: floor, walls, ledge
        }

        // ---------------------------------------------------------------- cubes

        private static void BuildCubes(Material[] cubeMats)
        {
            int layer = LayerMask.NameToLayer(GrabbableLayerName);
            CreateGrabbableCube("Cube_Small",  new Vector3(-7f, 0.25f, -2.5f), 0.5f, cubeMats[0], layer);
            CreateGrabbableCube("Cube_Medium", new Vector3(-7f, 0.50f,  0.0f), 1.0f, cubeMats[1], layer);
            CreateGrabbableCube("Cube_Large",  new Vector3(-7f, 0.75f,  2.5f), 1.5f, cubeMats[2], layer);
        }

        private static void CreateGrabbableCube(string name, Vector3 position, float size, Material mat, int layer)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.layer = layer;
            cube.transform.position = position; // y = size/2 so it rests on the floor
            cube.transform.localScale = Vector3.one * size;
            cube.GetComponent<Renderer>().sharedMaterial = mat;

            var rb = cube.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            cube.AddComponent<ObjectGrabbable>();
            cube.AddComponent<PerspectiveScaler>();
        }

        // ---------------------------------------------------------------- goal

        private static void BuildGoal(Material goalMat, GameUI gameUI)
        {
            var zone = new GameObject("ExitZone");
            // Trigger sits on the ledge top, kept away from the front edge so a
            // mid-jump capsule beside the ledge cannot clip it prematurely.
            zone.transform.position = new Vector3(RoomLength / 2f - 1.1f, LedgeHeight + 1f, 0f);
            var col = zone.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(2.2f, 2f, RoomWidth - 0.2f);

            var trigger = zone.AddComponent<GoalTrigger>();
            SetObjectField(trigger, "ui", gameUI);

            // Visible green pad (no collider) marking the exit.
            GameObject pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pad.name = "ExitPad";
            Object.DestroyImmediate(pad.GetComponent<Collider>());
            pad.transform.SetParent(zone.transform, false);
            pad.transform.localPosition = new Vector3(0f, -0.95f, 0f);
            pad.transform.localScale = new Vector3(2.2f, 0.1f, 4f);
            pad.GetComponent<Renderer>().sharedMaterial = goalMat;
        }

        // ---------------------------------------------------------------- UI

        private static GameUI BuildUI()
        {
            var canvasGo = new GameObject("GameUI", typeof(Canvas), typeof(CanvasScaler));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            Font font = LoadBuiltinFont();

            // Crosshair dot.
            RectTransform crossRt = CreateUIElement("Crosshair", canvasGo.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(6f, 6f));
            var crossImg = crossRt.gameObject.AddComponent<Image>();
            crossImg.color = new Color(1f, 1f, 1f, 0.85f);
            crossImg.raycastTarget = false;

            // Prompt line at the bottom.
            RectTransform promptRt = CreateUIElement("PromptText", canvasGo.transform,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(1000f, 90f));
            var promptText = promptRt.gameObject.AddComponent<Text>();
            promptText.font = font;
            promptText.fontSize = 20;
            promptText.alignment = TextAnchor.MiddleCenter;
            promptText.color = Color.white;
            promptText.horizontalOverflow = HorizontalWrapMode.Overflow;
            promptText.verticalOverflow = VerticalWrapMode.Overflow;
            promptText.raycastTarget = false;
            promptRt.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.8f);

            // Completion overlay (hidden until GoalTrigger fires).
            RectTransform completeRt = CreateUIElement("CompletionPanel", canvasGo.transform,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var completeImg = completeRt.gameObject.AddComponent<Image>();
            completeImg.color = new Color(0f, 0f, 0f, 0.65f);
            completeImg.raycastTarget = false;

            RectTransform completeTextRt = CreateUIElement("CompletionText", completeRt,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1200f, 200f));
            var completeText = completeTextRt.gameObject.AddComponent<Text>();
            completeText.font = font;
            completeText.text = "ROOM COMPLETE!\n<size=24>You solved the forced-perspective puzzle.</size>";
            completeText.supportRichText = true;
            completeText.fontSize = 56;
            completeText.alignment = TextAnchor.MiddleCenter;
            completeText.color = new Color(0.45f, 1f, 0.55f);
            completeText.raycastTarget = false;
            completeTextRt.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.9f);

            completeRt.gameObject.SetActive(false);

            var gameUI = canvasGo.AddComponent<GameUI>();
            SetObjectField(gameUI, "crosshair", crossImg);
            SetObjectField(gameUI, "promptText", promptText);
            SetObjectField(gameUI, "completionRoot", completeRt.gameObject);
            return gameUI;
        }

        private static RectTransform CreateUIElement(string name, Transform parent,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            if (anchorMin == Vector2.zero && anchorMax == Vector2.one)
            {
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero; // full stretch
            }
            return rt;
        }

        private static Font LoadBuiltinFont()
        {
            // Unity 2022+ renamed the built-in font to LegacyRuntime.ttf.
            try
            {
                Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (f != null) return f;
            }
            catch { /* fall through */ }
            try
            {
                Font f = Resources.GetBuiltinResource<Font>("Arial.ttf");
                if (f != null) return f;
            }
            catch { /* fall through */ }
            Debug.LogWarning("[PrototypeRoomBuilder] Built-in font not found; using OS font fallback.");
            return Font.CreateDynamicFontFromOSFont("Arial", 16);
        }

        // ---------------------------------------------------------------- assets & settings

        private static Material CreateMaterial(string name, Color color)
        {
            string path = MaterialsFolder + "/" + name + ".mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
                return existing;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogError("[PrototypeRoomBuilder] URP Lit shader not found — is URP installed?");
                shader = Shader.Find("Standard");
            }
            var mat = new Material(shader) { name = name };
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            else if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", color); // Standard fallback
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static void MakeEmissive(Material mat, Color hdrColor)
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", hdrColor);
            EditorUtility.SetDirty(mat);
        }

        private static void EnsureFolder(string folder)
        {
            if (!AssetDatabase.IsValidFolder(folder))
            {
                string parent = System.IO.Path.GetDirectoryName(folder).Replace('\\', '/');
                string leaf = System.IO.Path.GetFileName(folder);
                AssetDatabase.CreateFolder(parent, leaf);
            }
        }

        private static void EnsureGrabbableLayer()
        {
            if (LayerMask.NameToLayer(GrabbableLayerName) >= 0)
                return;

            Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets == null || assets.Length == 0)
            {
                Debug.LogError("[PrototypeRoomBuilder] Could not open TagManager.asset to add the '" +
                               GrabbableLayerName + "' layer. Add it manually via Edit > Project Settings > Tags and Layers.");
                return;
            }

            var so = new SerializedObject(assets[0]);
            SerializedProperty layers = so.FindProperty("layers");
            for (int i = 8; i < layers.arraySize; i++) // 0-7 are built-in
            {
                SerializedProperty slot = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(slot.stringValue))
                {
                    slot.stringValue = GrabbableLayerName;
                    so.ApplyModifiedProperties();
                    Debug.Log("[PrototypeRoomBuilder] Added layer '" + GrabbableLayerName + "' at slot " + i + ".");
                    return;
                }
            }
            Debug.LogError("[PrototypeRoomBuilder] No free layer slot for '" + GrabbableLayerName + "'.");
        }

        private static void AddSceneToBuildSettings(string scenePath)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Exists(s => s.path == scenePath))
                return;
            scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // ---------------------------------------------------------------- serialized-field wiring helpers

        private static void SetObjectField(Object target, string propertyName, Object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(propertyName);
            if (prop == null)
            {
                Debug.LogError($"[PrototypeRoomBuilder] Field '{propertyName}' not found on {target.GetType().Name}.");
                return;
            }
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetLayerMaskField(Object target, string propertyName, int maskBits)
        {
            var so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(propertyName);
            if (prop == null)
            {
                Debug.LogError($"[PrototypeRoomBuilder] Field '{propertyName}' not found on {target.GetType().Name}.");
                return;
            }
            try
            {
                prop.boxedValue = (LayerMask)maskBits; // Unity 2022.1+
            }
            catch
            {
                SerializedProperty bits = so.FindProperty(propertyName + ".m_Bits");
                if (bits != null) bits.longValue = (uint)maskBits;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
