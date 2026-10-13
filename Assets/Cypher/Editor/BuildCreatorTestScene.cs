#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Cypher.Carousel;
using Cypher.Dials;
using Cypher.Entry;
using Cypher.Modes;
using Cypher.DebugTools;

namespace Cypher.EditorTools
{
    /// <summary>
    /// Builds CreatorTest_Scene_01 for the Phase 1 "prove the feel" tests
    /// (brief §6.Phase 1). Run from menu: Cypher → Build CreatorTest_Scene_01.
    ///
    /// Rebuildable: re-running overwrites the scene back to a known-good state.
    /// </summary>
    public static class BuildCreatorTestScene
    {
        private const string ScenePath = "Assets/Cypher/Scenes/CreatorTest_Scene_01.unity";
        private const string RigPrefabPath = "Assets/Player Character Micro Gesture.prefab";

        // Brief §5 palette
        private static readonly Color VoidBlack   = Hex("#080808");
        private static readonly Color MajiCyan    = Hex("#00E5FF");
        private static readonly Color SolarGold   = Hex("#C9A84C");
        private static readonly Color LithicAmber = Hex("#FF6B00");
        private static readonly Color WarmWhite   = Hex("#F2E7D5");

        [MenuItem("Cypher/Build CreatorTest_Scene_01")]
        public static void Build()
        {
            if (File.Exists(ScenePath))
            {
                bool ok = EditorUtility.DisplayDialog(
                    "Overwrite scene?",
                    $"{ScenePath} already exists.\nRebuilding will replace it from scratch.\nContinue?",
                    "Rebuild",
                    "Cancel");
                if (!ok) return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildEnvironment();

            GameObject rig = TryInstantiateRig();
            FindHandsAndMicrogestures(rig, out OVRHand leftHand, out OVRHand rightHand,
                                           out OVRMicrogestureEventSource leftMG,
                                           out OVRMicrogestureEventSource rightMG);

            var carousel = BuildCarouselStation();
            WireCarouselMicrogestures(carousel, leftMG, rightMG);

            BuildEntryStation(out OVRPassthroughLayer passthroughLayer, out CanvasGroup fallbackOverlay);
            WirePassthroughEntryDim(passthroughLayer, fallbackOverlay);

            var dialBuild = BuildDialStation();

            var gate = BuildInputGate(carousel);
            BuildInstrumentPlaceholder(gate);
            BuildGateInstructionPanel();

            var modeToggle = BuildModeToggle(passthroughLayer);

            BuildDebugHUD(carousel, gate, leftHand, rightHand,
                          dialBuild.five, dialBuild.two, dialBuild.pinch);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            Debug.Log($"[Cypher] CreatorTest_Scene_01 built at {ScenePath}");
            EditorUtility.FocusProjectWindow();
            AssetDatabase.Refresh();
        }

        // ---------------------- Environment ----------------------

        private static void BuildEnvironment()
        {
            // Floor
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.position = new Vector3(0, -0.01f, 0);
            floor.transform.localScale = new Vector3(6f, 0.02f, 6f);
            SetColor(floor, new Color(0.1f, 0.1f, 0.1f, 1f));
            Object.DestroyImmediate(floor.GetComponent<BoxCollider>());

            // Directional light
            var sun = new GameObject("Directional Light");
            sun.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
            var light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 0.8f;
            light.color = WarmWhite;
        }

        // ---------------------- Rig ----------------------

        private static GameObject TryInstantiateRig()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RigPrefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[Cypher] Rig prefab not found at {RigPrefabPath}. " +
                                 "Add your OVR rig manually and assign microgesture sources to the carousel.");
                return null;
            }
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = "Rig";
            instance.transform.position = Vector3.zero;
            return instance;
        }

        private static void FindHandsAndMicrogestures(
            GameObject rig,
            out OVRHand leftHand, out OVRHand rightHand,
            out OVRMicrogestureEventSource leftMG, out OVRMicrogestureEventSource rightMG)
        {
            leftHand = rightHand = null;
            leftMG = rightMG = null;
            if (rig == null) return;

            foreach (var hand in rig.GetComponentsInChildren<OVRHand>(includeInactive: true))
            {
                // OVRHand doesn't expose a public hand-type field in all SDK versions; use name as a hint.
                string n = hand.gameObject.name.ToLowerInvariant();
                if (leftHand == null && (n.Contains("left") || n.Contains("l_"))) leftHand = hand;
                else if (rightHand == null && (n.Contains("right") || n.Contains("r_"))) rightHand = hand;
            }

            foreach (var mg in rig.GetComponentsInChildren<OVRMicrogestureEventSource>(includeInactive: true))
            {
                string n = mg.gameObject.name.ToLowerInvariant();
                if (leftMG == null && (n.Contains("left") || n.Contains("l_"))) leftMG = mg;
                else if (rightMG == null && (n.Contains("right") || n.Contains("r_"))) rightMG = mg;
                else if (leftMG == null) leftMG = mg;
                else if (rightMG == null) rightMG = mg;
            }

            if (leftMG == null && leftHand != null) leftMG = leftHand.gameObject.AddComponent<OVRMicrogestureEventSource>();
            if (rightMG == null && rightHand != null) rightMG = rightHand.gameObject.AddComponent<OVRMicrogestureEventSource>();
        }

        // ---------------------- Carousel ----------------------

        private static DepthStackedCarousel BuildCarouselStation()
        {
            var station = new GameObject("CarouselStation");
            station.transform.position = new Vector3(0f, 1.1f, 0.6f);
            var carousel = station.AddComponent<DepthStackedCarousel>();

            var colors = new[] { VoidBlack, MajiCyan, SolarGold, LithicAmber, WarmWhite };
            var titles = new[] { "Story 1", "Story 2", "Story 3", "Story 4", "Story 5" };
            var items = new List<Transform>();

            for (int i = 0; i < 5; i++)
            {
                var item = GameObject.CreatePrimitive(PrimitiveType.Cube);
                item.name = $"Item_{i + 1}";
                item.transform.SetParent(station.transform, worldPositionStays: false);
                item.transform.localScale = Vector3.one * 0.12f;
                SetColor(item, colors[i]);
                Object.DestroyImmediate(item.GetComponent<BoxCollider>());
                item.AddComponent<CarouselItemView>();

                var labelGO = new GameObject("Label");
                labelGO.transform.SetParent(item.transform, worldPositionStays: false);
                labelGO.transform.localPosition = new Vector3(0, 0.6f, -0.55f);
                labelGO.transform.localScale = Vector3.one * 0.25f;
                var tmp = labelGO.AddComponent<TextMeshPro>();
                tmp.text = titles[i];
                tmp.fontSize = 2.2f;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = i == 0 ? Color.white : WarmWhite;

                items.Add(item.transform);
            }

            SetPrivateObject(carousel, "items", items);
            SetPrivateInt(carousel, "startIndex", 2);
            return carousel;
        }

        private static void WireCarouselMicrogestures(
            DepthStackedCarousel carousel,
            OVRMicrogestureEventSource leftMG,
            OVRMicrogestureEventSource rightMG)
        {
            SetPrivateObject(carousel, "rightHandMicrogesture", rightMG);
            SetPrivateObject(carousel, "leftHandMicrogesture", leftMG);
        }

        // ---------------------- Entry ----------------------

        private static void BuildEntryStation(out OVRPassthroughLayer passthroughLayer, out CanvasGroup fallbackOverlay)
        {
            var station = new GameObject("EntryStation");

            // Logo quad in front of the player
            var logo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            logo.name = "LogoQuad";
            logo.transform.SetParent(station.transform);
            logo.transform.localPosition = new Vector3(0f, 1.6f, 1.2f);
            logo.transform.localScale = Vector3.one * 0.3f;
            SetColor(logo, MajiCyan);
            Object.DestroyImmediate(logo.GetComponent<MeshCollider>());

            // Passthrough layer host
            var passthroughGO = new GameObject("PassthroughLayer");
            passthroughGO.transform.SetParent(station.transform);
            passthroughLayer = passthroughGO.AddComponent<OVRPassthroughLayer>();
            passthroughLayer.overlayType = OVROverlay.OverlayType.Underlay;

            // Fallback overlay (world-space Canvas, black)
            var canvasGO = new GameObject("FallbackOverlay");
            canvasGO.transform.SetParent(station.transform);
            canvasGO.transform.localPosition = new Vector3(0f, 1.6f, 1.5f);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();
            fallbackOverlay = canvasGO.AddComponent<CanvasGroup>();
            fallbackOverlay.alpha = 0f;
            var rt = (RectTransform)canvasGO.transform;
            rt.sizeDelta = new Vector2(10f, 10f);
            rt.localScale = Vector3.one * 0.1f;

            var panel = new GameObject("DarkPanel", typeof(RectTransform));
            panel.transform.SetParent(canvasGO.transform, worldPositionStays: false);
            var panelRT = (RectTransform)panel.transform;
            panelRT.anchorMin = Vector2.zero;
            panelRT.anchorMax = Vector2.one;
            panelRT.offsetMin = panelRT.offsetMax = Vector2.zero;
            var img = panel.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 1f);

            station.AddComponent<PassthroughEntryDim>();
        }

        private static void WirePassthroughEntryDim(OVRPassthroughLayer layer, CanvasGroup overlay)
        {
            var dim = Object.FindFirstObjectByType<PassthroughEntryDim>();
            if (dim == null) return;
            SetPrivateObject(dim, "passthroughLayer", layer);
            SetPrivateObject(dim, "fallbackOverlay", overlay);
        }

        // ---------------------- Dials ----------------------

        private struct DialBuild
        {
            public FiveSocketDial five;
            public TwoFingerDial two;
            public PinchAndTwistEventSource pinch;
            public DialLightSliderResponder responder;
        }

        private static DialBuild BuildDialStation()
        {
            var station = new GameObject("DialStation");
            station.transform.position = new Vector3(0.35f, 0.95f, 0.5f);

            var shared = BuildSharedDialTarget(station.transform);

            FiveSocketDial five = BuildFiveSocketDial(station.transform, new Vector3(-0.15f, 0.1f, 0f));
            TwoFingerDial two   = BuildTwoFingerDial(station.transform,   new Vector3( 0.00f, 0.1f, 0f));
            PinchAndTwistEventSource pinch = BuildPinchTwistDial(station.transform, new Vector3( 0.15f, 0.1f, 0f));

            // Wire all three dials' events to the responder.
            AddUnityEventListener(five.events.OnStart,    shared.responder, nameof(DialLightSliderResponder.HandleStart));
            AddUnityEventListener(five.events.OnRotation, shared.responder, nameof(DialLightSliderResponder.HandleRotation));
            AddUnityEventListener(five.events.OnEnd,      shared.responder, nameof(DialLightSliderResponder.HandleEnd));

            AddUnityEventListener(two.events.OnStart,     shared.responder, nameof(DialLightSliderResponder.HandleStart));
            AddUnityEventListener(two.events.OnRotation,  shared.responder, nameof(DialLightSliderResponder.HandleRotation));
            AddUnityEventListener(two.events.OnEnd,       shared.responder, nameof(DialLightSliderResponder.HandleEnd));

            AddUnityEventListener(pinch.OnStartPinchAndTwist, shared.responder, nameof(DialLightSliderResponder.HandleStart));
            AddUnityEventListener(pinch.OnPinchAndTwist,      shared.responder, nameof(DialLightSliderResponder.HandleRotation));
            AddUnityEventListener(pinch.OnEndPinchAndTwist,   shared.responder, nameof(DialLightSliderResponder.HandleEnd));

            return new DialBuild { five = five, two = two, pinch = pinch, responder = shared.responder };
        }

        private struct SharedDialTarget
        {
            public DialLightSliderResponder responder;
            public Light light;
            public Slider slider;
        }

        private static SharedDialTarget BuildSharedDialTarget(Transform parent)
        {
            var hostGO = new GameObject("SharedDialTarget");
            hostGO.transform.SetParent(parent, worldPositionStays: false);
            hostGO.transform.localPosition = new Vector3(0f, 0.35f, 0f);

            // Light
            var lightGO = new GameObject("TargetLight");
            lightGO.transform.SetParent(hostGO.transform, worldPositionStays: false);
            lightGO.transform.localPosition = new Vector3(0f, 0.1f, -0.3f);
            var light = lightGO.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = WarmWhite;
            light.range = 2f;
            light.intensity = 0f;

            // Slider
            var canvasGO = new GameObject("TargetCanvas");
            canvasGO.transform.SetParent(hostGO.transform, worldPositionStays: false);
            canvasGO.transform.localPosition = new Vector3(0f, 0.0f, -0.05f);
            canvasGO.transform.localScale = Vector3.one * 0.003f;
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();
            var canvasRT = (RectTransform)canvasGO.transform;
            canvasRT.sizeDelta = new Vector2(120f, 24f);

            var sliderGO = new GameObject("Slider", typeof(RectTransform));
            sliderGO.transform.SetParent(canvasGO.transform, worldPositionStays: false);
            var sliderRT = (RectTransform)sliderGO.transform;
            sliderRT.sizeDelta = new Vector2(120f, 24f);
            sliderRT.anchoredPosition = Vector2.zero;
            var slider = sliderGO.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 0f;

            // Minimal visuals so the slider is visible without full UGUI setup.
            var bg = new GameObject("Background", typeof(RectTransform));
            bg.transform.SetParent(sliderGO.transform, worldPositionStays: false);
            var bgRT = (RectTransform)bg.transform;
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.offsetMin = bgRT.offsetMax = Vector2.zero;
            var bgImg = bg.AddComponent<Image>();
            bgImg.color = new Color(0.1f, 0.1f, 0.12f, 1f);

            var fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(sliderGO.transform, worldPositionStays: false);
            var faRT = (RectTransform)fillArea.transform;
            faRT.anchorMin = new Vector2(0f, 0.25f);
            faRT.anchorMax = new Vector2(1f, 0.75f);
            faRT.offsetMin = faRT.offsetMax = Vector2.zero;

            var fill = new GameObject("Fill", typeof(RectTransform));
            fill.transform.SetParent(fillArea.transform, worldPositionStays: false);
            var fillRT = (RectTransform)fill.transform;
            fillRT.anchorMin = Vector2.zero;
            fillRT.anchorMax = Vector2.one;
            fillRT.offsetMin = fillRT.offsetMax = Vector2.zero;
            var fillImg = fill.AddComponent<Image>();
            fillImg.color = MajiCyan;

            slider.fillRect = fillRT;
            slider.direction = Slider.Direction.LeftToRight;

            var responder = hostGO.AddComponent<DialLightSliderResponder>();
            SetPrivateObject(responder, "slider", slider);
            SetPrivateObject(responder, "targetLight", light);

            return new SharedDialTarget { responder = responder, light = light, slider = slider };
        }

        private static FiveSocketDial BuildFiveSocketDial(Transform parent, Vector3 localPos)
        {
            var dialGO = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            dialGO.name = "FiveSocketDial";
            dialGO.transform.SetParent(parent, worldPositionStays: false);
            dialGO.transform.localPosition = localPos;
            dialGO.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            dialGO.transform.localScale = new Vector3(0.08f, 0.015f, 0.08f);
            SetColor(dialGO, MajiCyan);
            Object.DestroyImmediate(dialGO.GetComponent<CapsuleCollider>());

            // Five sockets around the top face in local XY plane (dial.forward is local Z after the X-axis rotation above).
            var sockets = new Transform[5];
            string[] names = { "Thumb", "Index", "Middle", "Ring", "Pinky" };
            float radius = 0.04f;
            for (int i = 0; i < 5; i++)
            {
                var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                s.name = $"Socket_{names[i]}";
                s.transform.SetParent(dialGO.transform, worldPositionStays: false);
                float ang = i * (2f * Mathf.PI / 5f);
                // Note: because the cylinder is rotated 90° on X, child local positions map through that rotation;
                // we're placing sockets in a ring on the dial's visible face. Set in world-approximate terms via SetParent after.
                s.transform.localPosition = new Vector3(Mathf.Cos(ang) * radius / 0.08f, 0f, Mathf.Sin(ang) * radius / 0.08f);
                s.transform.localScale = Vector3.one * 0.25f;
                SetColorTransparent(s, new Color(MajiCyan.r, MajiCyan.g, MajiCyan.b, 0.35f));
                Object.DestroyImmediate(s.GetComponent<SphereCollider>());
                sockets[i] = s.transform;
            }

            var dial = dialGO.AddComponent<FiveSocketDial>();
            SetPrivateObject(dial, "dial", dialGO.transform);
            SetPrivateObjectArray(dial, "socketPoints", sockets);
            return dial;
        }

        private static TwoFingerDial BuildTwoFingerDial(Transform parent, Vector3 localPos)
        {
            var dialGO = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            dialGO.name = "TwoFingerDial";
            dialGO.transform.SetParent(parent, worldPositionStays: false);
            dialGO.transform.localPosition = localPos;
            dialGO.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            dialGO.transform.localScale = new Vector3(0.07f, 0.015f, 0.07f);
            SetColor(dialGO, SolarGold);
            Object.DestroyImmediate(dialGO.GetComponent<CapsuleCollider>());

            var dial = dialGO.AddComponent<TwoFingerDial>();
            SetPrivateObject(dial, "dial", dialGO.transform);
            return dial;
        }

        private static PinchAndTwistEventSource BuildPinchTwistDial(Transform parent, Vector3 localPos)
        {
            var dialGO = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            dialGO.name = "PinchTwistDial";
            dialGO.transform.SetParent(parent, worldPositionStays: false);
            dialGO.transform.localPosition = localPos;
            dialGO.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            dialGO.transform.localScale = new Vector3(0.07f, 0.015f, 0.07f);
            SetColor(dialGO, LithicAmber);
            Object.DestroyImmediate(dialGO.GetComponent<CapsuleCollider>());

            return dialGO.AddComponent<PinchAndTwistEventSource>();
        }

        // ---------------------- Input gate + instrument ----------------------

        private static CarouselInputGate BuildInputGate(DepthStackedCarousel carousel)
        {
            var go = new GameObject("CarouselInputGate");
            var gate = go.AddComponent<CarouselInputGate>();
            SetPrivateObject(gate, "carousel", carousel);
            return gate;
        }

        private static void BuildInstrumentPlaceholder(CarouselInputGate gate)
        {
            var instr = GameObject.CreatePrimitive(PrimitiveType.Cube);
            instr.name = "InstrumentPlaceholder";
            instr.transform.position = new Vector3(0f, 1.2f, 0.4f);
            instr.transform.localScale = Vector3.one * 0.3f;
            SetColorTransparent(instr, new Color(MajiCyan.r, MajiCyan.g, MajiCyan.b, 0.25f));
            Object.DestroyImmediate(instr.GetComponent<BoxCollider>());
            instr.SetActive(false);

            // When gate closes (instrument spawned) show the placeholder; when it opens hide it.
            var binder = instr.AddComponent<InstrumentVisibilityBinder>();
            UnityEventTools.AddPersistentListener(gate.OnStateChanged, binder.ApplyFromGateState);
        }

        private static void BuildGateInstructionPanel()
        {
            var panelGO = new GameObject("GateInstructionPanel");
            panelGO.transform.position = new Vector3(-0.6f, 1.3f, 0.4f);
            panelGO.transform.rotation = Quaternion.Euler(0f, 25f, 0f);

            var tmp = panelGO.AddComponent<TextMeshPro>();
            tmp.text = "Gate test:\nPress G to toggle\ninstrument placeholder";
            tmp.fontSize = 2f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = WarmWhite;
            tmp.rectTransform.sizeDelta = new Vector2(2f, 1f);
        }

        // ---------------------- Mode toggle ----------------------

        private static MRVRModeToggle BuildModeToggle(OVRPassthroughLayer passthroughLayer)
        {
            var toggleGO = new GameObject("ModeToggle");
            toggleGO.transform.position = new Vector3(-0.4f, 1.0f, 0.4f);
            var toggle = toggleGO.AddComponent<MRVRModeToggle>();
            SetPrivateObject(toggle, "passthroughLayer", passthroughLayer);

            var label = new GameObject("Label");
            label.transform.SetParent(toggleGO.transform);
            label.transform.localPosition = new Vector3(0f, 0.1f, 0f);
            var tmp = label.AddComponent<TextMeshPro>();
            tmp.text = "MR / VR\n(wire poke button later)";
            tmp.fontSize = 1.4f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = WarmWhite;
            tmp.rectTransform.sizeDelta = new Vector2(1f, 0.5f);

            return toggle;
        }

        // ---------------------- Debug HUD ----------------------

        private static void BuildDebugHUD(
            DepthStackedCarousel carousel,
            CarouselInputGate gate,
            OVRHand leftHand, OVRHand rightHand,
            FiveSocketDial five, TwoFingerDial two, PinchAndTwistEventSource pinch)
        {
            var hudGO = new GameObject("DebugHUDPanel");
            hudGO.transform.position = new Vector3(-0.6f, 1.5f, 0.6f);
            hudGO.transform.rotation = Quaternion.Euler(0f, 35f, 0f);

            TMP_Text focusT      = AddHudText(hudGO.transform, new Vector3(0f, 0.35f, 0f),  "Carousel");
            TMP_Text gateT       = AddHudText(hudGO.transform, new Vector3(0f, 0.25f, 0f),  "Input Gate");
            TMP_Text lConfT      = AddHudText(hudGO.transform, new Vector3(0f, 0.15f, 0f),  "L Hand");
            TMP_Text rConfT      = AddHudText(hudGO.transform, new Vector3(0f, 0.05f, 0f),  "R Hand");
            TMP_Text fiveT       = AddHudText(hudGO.transform, new Vector3(0f, -0.1f, 0f),  "5-Socket");
            TMP_Text twoT        = AddHudText(hudGO.transform, new Vector3(0f, -0.2f, 0f),  "2-Finger");
            TMP_Text pinchT      = AddHudText(hudGO.transform, new Vector3(0f, -0.3f, 0f),  "Pinch-Twist");

            var hud = hudGO.AddComponent<CreatorTestDebugHUD>();
            SetPrivateObject(hud, "carousel", carousel);
            SetPrivateObject(hud, "inputGate", gate);
            SetPrivateObject(hud, "leftHand", leftHand);
            SetPrivateObject(hud, "rightHand", rightHand);
            SetPrivateObject(hud, "fiveSocketDial", five);
            SetPrivateObject(hud, "twoFingerDial", two);
            SetPrivateObject(hud, "pinchTwistSource", pinch);
            SetPrivateObject(hud, "focusText", focusT);
            SetPrivateObject(hud, "gateText", gateT);
            SetPrivateObject(hud, "leftConfidenceText", lConfT);
            SetPrivateObject(hud, "rightConfidenceText", rConfT);
            SetPrivateObject(hud, "fiveSocketText", fiveT);
            SetPrivateObject(hud, "twoFingerText", twoT);
            SetPrivateObject(hud, "pinchTwistText", pinchT);
        }

        private static TMP_Text AddHudText(Transform parent, Vector3 localPos, string placeholder)
        {
            var go = new GameObject(placeholder.Replace(" ", "") + "Text");
            go.transform.SetParent(parent, worldPositionStays: false);
            go.transform.localPosition = localPos;
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = placeholder;
            tmp.fontSize = 1.4f;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.color = WarmWhite;
            tmp.rectTransform.sizeDelta = new Vector2(2f, 0.12f);
            return tmp;
        }

        // ---------------------- Helpers ----------------------

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }

        private static void SetColor(GameObject go, Color c)
        {
            var r = go.GetComponent<Renderer>();
            if (r == null) return;
            var mat = new Material(r.sharedMaterial);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            else if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);
            r.sharedMaterial = mat;
        }

        private static void SetColorTransparent(GameObject go, Color c)
        {
            var r = go.GetComponent<Renderer>();
            if (r == null) return;
            var mat = new Material(r.sharedMaterial);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            else if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);
            // Toggle URP Lit surface type to Transparent if possible.
            if (mat.HasProperty("_Surface"))
            {
                mat.SetFloat("_Surface", 1f);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            r.sharedMaterial = mat;
        }

        private static void SetPrivateObject(Object target, string fieldName, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(fieldName);
            if (prop == null) { Debug.LogWarning($"[Cypher] field '{fieldName}' not found on {target}"); return; }
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetPrivateInt(Object target, string fieldName, int value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(fieldName);
            if (prop == null) { Debug.LogWarning($"[Cypher] field '{fieldName}' not found on {target}"); return; }
            prop.intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetPrivateObject(Object target, string fieldName, IList<Transform> list)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(fieldName);
            if (prop == null) { Debug.LogWarning($"[Cypher] field '{fieldName}' not found on {target}"); return; }
            prop.arraySize = list.Count;
            for (int i = 0; i < list.Count; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = list[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetPrivateObjectArray(Object target, string fieldName, Transform[] arr)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(fieldName);
            if (prop == null) { Debug.LogWarning($"[Cypher] field '{fieldName}' not found on {target}"); return; }
            prop.arraySize = arr.Length;
            for (int i = 0; i < arr.Length; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = arr[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddUnityEventListener(UnityEngine.Events.UnityEvent evt, Object target, string methodName)
        {
            if (evt == null || target == null) return;
            var action = (UnityEngine.Events.UnityAction)System.Delegate.CreateDelegate(
                typeof(UnityEngine.Events.UnityAction), target, methodName);
            UnityEventTools.AddPersistentListener(evt, action);
        }

        private static void AddUnityEventListener(UnityEngine.Events.UnityEvent<float> evt, Object target, string methodName)
        {
            if (evt == null || target == null) return;
            var action = (UnityEngine.Events.UnityAction<float>)System.Delegate.CreateDelegate(
                typeof(UnityEngine.Events.UnityAction<float>), target, methodName);
            UnityEventTools.AddPersistentListener(evt, action);
        }

    }
}
#endif
