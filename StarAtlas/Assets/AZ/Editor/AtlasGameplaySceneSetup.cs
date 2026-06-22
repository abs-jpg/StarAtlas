using AZ.Exhibition;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AZ.Atlas.Editor
{
    [InitializeOnLoad]
    internal static class AtlasGameplaySceneSetup
    {
        private const string AtlasScenePath = "Assets/AZ/Atlas.unity";
        private const string TimeBarName = "Atlas Time Simulation Bar";
        private const string MissionPanelName = "Atlas Mission Panel";
        private const string MissionButtonName = "mission";
        private const string ButtonPrefabPath =
            "Assets/AQY/Prefabs/ButtonBasic_White_Pull.prefab";
        private const string FontGuid = "af641c37b3c25a04a9fc06aa25e0735f";

        static AtlasGameplaySceneSetup()
        {
            EditorApplication.delayCall += SetupActiveAtlasScene;
            EditorSceneManager.sceneOpened += OnSceneOpened;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        [MenuItem("AZ/Atlas/Create Or Repair Gameplay UI")]
        private static void SetupFromMenu()
        {
            SetupActiveAtlasScene();
        }

        private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            if (scene.path == AtlasScenePath)
            {
                EditorApplication.delayCall += SetupActiveAtlasScene;
            }
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.delayCall += SetupActiveAtlasScene;
            }
        }

        private static void SetupActiveAtlasScene()
        {
            if (Application.isPlaying)
            {
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.path != AtlasScenePath)
            {
                return;
            }

            GameObject observerPanel = FindSceneObject(scene, "ObserverPointPanel");
            GameObject missionButtonObject = FindSceneObject(scene, MissionButtonName);
            AtlasARStargazingController skyController =
                Object.FindObjectOfType<AtlasARStargazingController>();
            if (observerPanel == null ||
                missionButtonObject == null ||
                skyController == null)
            {
                Debug.LogWarning(
                    "Atlas gameplay setup needs ObserverPointPanel, mission, and AtlasARStargazingController.");
                return;
            }

            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                AssetDatabase.GUIDToAssetPath(FontGuid));

            AtlasTimeSimulationController timeController =
                CreateOrRepairTimeBar(observerPanel, skyController, font);
            AtlasMissionController missionController =
                CreateOrRepairMissionPanel(scene, missionButtonObject, font);
            RepairMissionButton(missionButtonObject, missionController, font);

            EditorUtility.SetDirty(observerPanel);
            EditorUtility.SetDirty(missionButtonObject);
            EditorUtility.SetDirty(timeController);
            EditorUtility.SetDirty(missionController);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log(
                "Created or repaired Atlas time simulation and AR star-finding mission UI.");
        }

        private static AtlasTimeSimulationController CreateOrRepairTimeBar(
            GameObject observerPanel,
            AtlasARStargazingController skyController,
            TMP_FontAsset font)
        {
            RectTransform panelRect = observerPanel.GetComponent<RectTransform>();
            Transform existing = observerPanel.transform.Find(TimeBarName);
            GameObject barObject = existing != null
                ? existing.gameObject
                : new GameObject(
                    TimeBarName,
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(AtlasTimeSimulationController));
            if (existing == null)
            {
                Undo.RegisterCreatedObjectUndo(barObject, "Create Atlas Time Simulation Bar");
                barObject.transform.SetParent(observerPanel.transform, false);
            }
            else
            {
                AtlasTimeSimulationController existingController =
                    GetOrAdd<AtlasTimeSimulationController>(barObject);
                Slider existingSlider = FindChildComponent<Slider>(
                    barObject.transform,
                    "Time Offset Slider");
                TMP_Text existingTimeText = FindChildComponent<TMP_Text>(
                    barObject.transform,
                    "Simulation Time");
                TMP_Text existingOffsetText = FindChildComponent<TMP_Text>(
                    barObject.transform,
                    "Offset Readout");
                Button existingResetButton = FindChildComponent<Button>(
                    barObject.transform,
                    "Reset To Now");
                RestoreTimeBarLayout(observerPanel, barObject);

                if (existingSlider != null &&
                    existingTimeText != null &&
                    existingOffsetText != null &&
                    existingResetButton != null)
                {
                    existingController.Configure(
                        skyController,
                        existingSlider,
                        existingTimeText,
                        existingOffsetText,
                        existingResetButton);
                    EditorUtility.SetDirty(existingController);
                    return existingController;
                }

                Debug.LogWarning(
                    "Atlas time bar exists but required children are missing. " +
                    "Delete the entire Atlas Time Simulation Bar and run " +
                    "AZ/Atlas/Create Or Repair Gameplay UI to rebuild it.",
                    barObject);
                return existingController;
            }

            barObject.layer = observerPanel.layer;
            RectTransform barRect = barObject.GetComponent<RectTransform>();
            barRect.anchorMin = new Vector2(0f, 0f);
            barRect.anchorMax = new Vector2(1f, 0f);
            barRect.pivot = new Vector2(0.5f, 0f);
            barRect.anchoredPosition = new Vector2(0f, 18f);
            barRect.sizeDelta = new Vector2(-40f, 210f);

            Image background = GetOrAdd<Image>(barObject);
            background.color = new Color(0.02f, 0.055f, 0.09f, 0.94f);
            background.raycastTarget = true;
            background.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>(
                "UI/Skin/Background.psd");
            background.type = Image.Type.Sliced;

            ClearChildren(barObject.transform);

            Image accent = CreateImage(
                "Amber Timeline",
                barRect,
                new Color(1f, 0.52f, 0.12f, 0.95f),
                false);
            SetRect(
                accent.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, 0f),
                new Vector2(0f, 6f));

            TMP_Text title = CreateText(
                "Title",
                barRect,
                "时间推演",
                38f,
                FontStyles.Bold,
                TextAlignmentOptions.Left,
                font,
                new Color(1f, 0.76f, 0.38f, 1f));
            SetRect(
                title.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(30f, -24f),
                new Vector2(280f, 54f));

            TMP_Text timeText = CreateText(
                "Simulation Time",
                barRect,
                "推演时间",
                31f,
                FontStyles.Normal,
                TextAlignmentOptions.Right,
                font,
                new Color(0.88f, 0.94f, 1f, 0.96f));
            SetRect(
                timeText.rectTransform,
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-32f, -26f),
                new Vector2(700f, 48f));

            TMP_Text offsetText = CreateText(
                "Offset Readout",
                barRect,
                "现在",
                28f,
                FontStyles.Bold,
                TextAlignmentOptions.Center,
                font,
                new Color(1f, 0.68f, 0.28f, 1f));
            SetRect(
                offsetText.rectTransform,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -29f),
                new Vector2(260f, 44f));

            Slider slider = CreateTimelineSlider(barRect);
            RectTransform sliderRect = slider.GetComponent<RectTransform>();
            SetRect(
                sliderRect,
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, 72f),
                new Vector2(-330f, 54f));
            slider.minValue = -24f;
            slider.maxValue = 24f;
            slider.value = 0f;
            slider.wholeNumbers = false;

            CreateTimelineLabel(
                barRect,
                "-24 小时",
                new Vector2(96f, 28f),
                TextAlignmentOptions.Left,
                font);
            CreateTimelineLabel(
                barRect,
                "现在",
                new Vector2(0f, 28f),
                TextAlignmentOptions.Center,
                font);
            CreateTimelineLabel(
                barRect,
                "+24 小时",
                new Vector2(-96f, 28f),
                TextAlignmentOptions.Right,
                font);

            Button resetButton = CreateSimpleButton(
                "Reset To Now",
                barRect,
                "回到现在",
                font,
                new Color(1f, 0.52f, 0.12f, 0.2f));
            SetRect(
                resetButton.GetComponent<RectTransform>(),
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(-28f, 50f),
                new Vector2(220f, 64f));

            AtlasTimeSimulationController controller =
                GetOrAdd<AtlasTimeSimulationController>(barObject);
            controller.Configure(
                skyController,
                slider,
                timeText,
                offsetText,
                resetButton);

            RestoreTimeBarLayout(observerPanel, barObject);

            EditorUtility.SetDirty(panelRect);
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void RestoreTimeBarLayout(
            GameObject observerPanel,
            GameObject barObject)
        {
            string[] obsoleteChildren =
            {
                "Star Label Position Slider",
                "Star Label Position Readout",
                "Star Label Position Title"
            };
            for (int i = 0; i < obsoleteChildren.Length; i++)
            {
                Transform child = barObject.transform.Find(obsoleteChildren[i]);
                GameObject obsoleteObject = child != null
                    ? child.gameObject
                    : FindSceneObject(barObject.scene, obsoleteChildren[i]);
                if (obsoleteObject != null)
                {
                    Undo.DestroyObjectImmediate(obsoleteObject);
                }
            }

            RectTransform barRect = barObject.GetComponent<RectTransform>();
            barRect.sizeDelta = new Vector2(-40f, 210f);
            EditorUtility.SetDirty(barRect);
            Transform scrollView = observerPanel.transform.Find("Scroll View");
            if (!(scrollView is RectTransform scrollRect))
            {
                return;
            }

            scrollRect.anchoredPosition = new Vector2(0f, 29f);
            scrollRect.sizeDelta = new Vector2(-40f, -372f);
            EditorUtility.SetDirty(scrollRect);
        }

        private static AtlasMissionController CreateOrRepairMissionPanel(
            Scene scene,
            GameObject missionButtonObject,
            TMP_FontAsset font)
        {
            GameObject existing = FindSceneObject(scene, MissionPanelName);
            GameObject panelObject = existing != null
                ? existing
                : new GameObject(
                    MissionPanelName,
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(CanvasGroup),
                    typeof(AtlasMissionController));
            if (existing == null)
            {
                Undo.RegisterCreatedObjectUndo(panelObject, "Create Atlas Mission Panel");
            }
            else
            {
                AtlasMissionController existingController =
                    GetOrAdd<AtlasMissionController>(panelObject);
                CanvasGroup existingGroup = GetOrAdd<CanvasGroup>(panelObject);
                TMP_Text existingTitle = FindChildComponent<TMP_Text>(
                    panelObject.transform,
                    "Title");
                TMP_Text existingRules = FindChildComponent<TMP_Text>(
                    panelObject.transform,
                    "Rules");
                TMP_Text existingStatus = FindChildComponent<TMP_Text>(
                    panelObject.transform,
                    "Status");
                TMP_Text existingProgress = FindChildComponent<TMP_Text>(
                    panelObject.transform,
                    "Progress");
                Button existingStartButton = FindChildComponent<Button>(
                    panelObject.transform,
                    "Start Mission");
                Button existingExitButton = FindChildComponent<Button>(
                    panelObject.transform,
                    "Exit Mission");
                if (existingExitButton == null)
                {
                    existingExitButton = CreateMissionPrefabButton(
                        panelObject.transform,
                        "Exit Mission",
                        "退出任务",
                        font,
                        missionButtonObject.layer);
                    RectTransform existingExitRect =
                        existingExitButton.GetComponent<RectTransform>();
                    SetRect(
                        existingExitRect,
                        new Vector2(1f, 0f),
                        new Vector2(1f, 0f),
                        new Vector2(1f, 0f),
                        new Vector2(-22f, 22f),
                        new Vector2(180f, 76f));
                    existingExitButton.gameObject.SetActive(false);
                }

                if (existingTitle != null &&
                    existingRules != null &&
                    existingStatus != null &&
                    existingProgress != null &&
                    existingStartButton != null)
                {
                    existingController.Configure(
                        Object.FindObjectOfType<AtlasFocusController>(),
                        panelObject.GetComponent<RectTransform>(),
                        existingGroup,
                        existingTitle,
                        existingRules,
                        existingStatus,
                        existingProgress,
                        existingStartButton,
                        existingExitButton);
                    EditorUtility.SetDirty(existingController);
                    return existingController;
                }

                Debug.LogWarning(
                    "Atlas mission panel exists but required children are missing. " +
                    "Delete the entire Atlas Mission Panel and run " +
                    "AZ/Atlas/Create Or Repair Gameplay UI to rebuild it.",
                    panelObject);
                return existingController;
            }

            panelObject.transform.SetParent(missionButtonObject.transform.parent, false);
            panelObject.layer = missionButtonObject.layer;
            panelObject.SetActive(true);

            RectTransform panelRect = panelObject.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(900f, 620f);

            Image background = GetOrAdd<Image>(panelObject);
            background.color = new Color(0.018f, 0.04f, 0.07f, 0.96f);
            background.raycastTarget = true;
            background.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>(
                "UI/Skin/Background.psd");
            background.type = Image.Type.Sliced;

            CanvasGroup group = GetOrAdd<CanvasGroup>(panelObject);
            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = true;

            ClearChildren(panelObject.transform);

            Image accent = CreateImage(
                "Mission Accent",
                panelRect,
                new Color(1f, 0.52f, 0.12f, 1f),
                false);
            SetRect(
                accent.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                Vector2.zero,
                new Vector2(0f, 7f));

            TMP_Text title = CreateText(
                "Title",
                panelRect,
                "AR 寻星任务",
                48f,
                FontStyles.Bold,
                TextAlignmentOptions.Left,
                font,
                Color.white);
            SetRect(
                title.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(42f, -34f),
                new Vector2(600f, 70f));

            TMP_Text progress = CreateText(
                "Progress",
                panelRect,
                "0 / 3",
                34f,
                FontStyles.Bold,
                TextAlignmentOptions.Right,
                font,
                new Color(1f, 0.68f, 0.28f, 1f));
            SetRect(
                progress.rectTransform,
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-42f, -40f),
                new Vector2(190f, 58f));

            TMP_Text rules = CreateText(
                "Rules",
                panelRect,
                string.Empty,
                29f,
                FontStyles.Normal,
                TextAlignmentOptions.TopLeft,
                font,
                new Color(0.9f, 0.94f, 1f, 0.96f));
            rules.enableAutoSizing = true;
            rules.fontSizeMin = 22f;
            rules.fontSizeMax = 29f;
            SetRect(
                rules.rectTransform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, 30f),
                new Vector2(810f, 330f));

            TMP_Text status = CreateText(
                "Status",
                panelRect,
                "准备好后开始第一轮寻星。",
                30f,
                FontStyles.Normal,
                TextAlignmentOptions.Center,
                font,
                new Color(0.78f, 0.88f, 1f, 1f));
            status.enableAutoSizing = true;
            status.fontSizeMin = 22f;
            status.fontSizeMax = 31f;
            SetRect(
                status.rectTransform,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 120f),
                new Vector2(800f, 94f));

            GameObject buttonPrefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(ButtonPrefabPath);
            GameObject startObject = buttonPrefab != null
                ? (GameObject)PrefabUtility.InstantiatePrefab(
                    buttonPrefab,
                    panelObject.transform)
                : CreateSimpleButton(
                    "Start Mission",
                    panelRect,
                    "开始任务",
                    font,
                    new Color(1f, 1f, 1f, 0.14f)).gameObject;
            startObject.name = "Start Mission";
            startObject.layer = missionButtonObject.layer;
            Button startButton = startObject.GetComponent<Button>();
            startButton.onClick = new Button.ButtonClickedEvent();
            RectTransform startRect = startObject.GetComponent<RectTransform>();
            SetRect(
                startRect,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 24f),
                new Vector2(300f, 90f));
            SetButtonText(startButton, "开始任务", font, 38f);

            Button exitButton = CreateMissionPrefabButton(
                panelObject.transform,
                "Exit Mission",
                "退出任务",
                font,
                missionButtonObject.layer);
            RectTransform exitRect = exitButton.GetComponent<RectTransform>();
            SetRect(
                exitRect,
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(-22f, 22f),
                new Vector2(180f, 76f));
            exitButton.gameObject.SetActive(false);

            AtlasMissionController controller =
                GetOrAdd<AtlasMissionController>(panelObject);
            controller.Configure(
                Object.FindObjectOfType<AtlasFocusController>(),
                panelRect,
                group,
                title,
                rules,
                status,
                progress,
                startButton,
                exitButton);

            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(startButton);
            EditorUtility.SetDirty(exitButton);
            panelObject.SetActive(false);
            return controller;
        }

        private static Button CreateMissionPrefabButton(
            Transform parent,
            string objectName,
            string label,
            TMP_FontAsset font,
            int layer)
        {
            GameObject buttonPrefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(ButtonPrefabPath);
            GameObject buttonObject = buttonPrefab != null
                ? (GameObject)PrefabUtility.InstantiatePrefab(buttonPrefab, parent)
                : CreateSimpleButton(
                    objectName,
                    parent.GetComponent<RectTransform>(),
                    label,
                    font,
                    new Color(1f, 1f, 1f, 0.14f)).gameObject;
            buttonObject.name = objectName;
            buttonObject.layer = layer;

            Button button = buttonObject.GetComponent<Button>();
            button.onClick = new Button.ButtonClickedEvent();
            SetButtonText(button, label, font, 32f);
            return button;
        }

        private static void RepairMissionButton(
            GameObject missionButtonObject,
            AtlasMissionController missionController,
            TMP_FontAsset font)
        {
            Button button = missionButtonObject.GetComponent<Button>();
            if (button == null)
            {
                Debug.LogError("The Atlas mission object does not contain a Button component.");
                return;
            }

            Undo.RecordObject(button, "Replace Atlas Mission Button Action");
            button.onClick = new Button.ButtonClickedEvent();

            AtlasMissionButton missionButton =
                GetOrAdd<AtlasMissionButton>(missionButtonObject);
            missionButton.Configure(missionController);
            SetButtonText(button, "寻星任务", font, 50f);

            EditorUtility.SetDirty(button);
            EditorUtility.SetDirty(missionButton);
        }

        private static Slider CreateTimelineSlider(RectTransform parent)
        {
            GameObject sliderObject = new GameObject(
                "Time Offset Slider",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Slider),
                typeof(ExhibitionSliderRayAdapter));
            sliderObject.transform.SetParent(parent, false);
            sliderObject.layer = parent.gameObject.layer;

            Image hitArea = sliderObject.GetComponent<Image>();
            hitArea.color = new Color(1f, 1f, 1f, 0.002f);
            hitArea.raycastTarget = true;

            RectTransform sliderRect = sliderObject.GetComponent<RectTransform>();
            Image background = CreateImage(
                "Background",
                sliderRect,
                new Color(0.35f, 0.48f, 0.62f, 0.38f),
                true);
            Stretch(background.rectTransform, new Vector2(0f, 18f), new Vector2(0f, -18f));

            GameObject fillAreaObject = new GameObject("Fill Area", typeof(RectTransform));
            fillAreaObject.transform.SetParent(sliderRect, false);
            RectTransform fillArea = fillAreaObject.GetComponent<RectTransform>();
            Stretch(fillArea, new Vector2(10f, 18f), new Vector2(-10f, -18f));

            Image fill = CreateImage(
                "Fill",
                fillArea,
                new Color(1f, 0.5f, 0.1f, 0.95f),
                false);
            Stretch(fill.rectTransform, Vector2.zero, Vector2.zero);

            GameObject handleAreaObject = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleAreaObject.transform.SetParent(sliderRect, false);
            RectTransform handleArea = handleAreaObject.GetComponent<RectTransform>();
            Stretch(handleArea, new Vector2(18f, 0f), new Vector2(-18f, 0f));

            Image handle = CreateImage(
                "Handle",
                handleArea,
                new Color(1f, 0.72f, 0.34f, 1f),
                true);
            handle.rectTransform.sizeDelta = new Vector2(38f, 38f);

            Slider slider = sliderObject.GetComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;

            ExhibitionSliderRayAdapter adapter =
                sliderObject.GetComponent<ExhibitionSliderRayAdapter>();
            adapter.Initialize(slider);
            return slider;
        }

        private static void CreateTimelineLabel(
            RectTransform parent,
            string value,
            Vector2 anchoredPosition,
            TextAlignmentOptions alignment,
            TMP_FontAsset font)
        {
            TMP_Text label = CreateText(
                value,
                parent,
                value,
                24f,
                FontStyles.Normal,
                alignment,
                font,
                new Color(0.68f, 0.78f, 0.88f, 0.9f));
            Vector2 anchor = alignment == TextAlignmentOptions.Left
                ? new Vector2(0f, 0f)
                : alignment == TextAlignmentOptions.Right
                    ? new Vector2(1f, 0f)
                    : new Vector2(0.5f, 0f);
            SetRect(
                label.rectTransform,
                anchor,
                anchor,
                anchor,
                anchoredPosition,
                new Vector2(240f, 42f));
        }

        private static Button CreateSimpleButton(
            string objectName,
            RectTransform parent,
            string labelValue,
            TMP_FontAsset font,
            Color color)
        {
            GameObject buttonObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            buttonObject.layer = parent.gameObject.layer;

            Image image = buttonObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = true;
            image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>(
                "UI/Skin/Background.psd");
            image.type = Image.Type.Sliced;

            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;

            TMP_Text label = CreateText(
                "Label",
                buttonObject.GetComponent<RectTransform>(),
                labelValue,
                28f,
                FontStyles.Bold,
                TextAlignmentOptions.Center,
                font,
                Color.white);
            Stretch(label.rectTransform, new Vector2(8f, 4f), new Vector2(-8f, -4f));
            return button;
        }

        private static Image CreateImage(
            string objectName,
            RectTransform parent,
            Color color,
            bool raycastTarget)
        {
            GameObject imageObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            imageObject.transform.SetParent(parent, false);
            imageObject.layer = parent.gameObject.layer;
            Image image = imageObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = raycastTarget;
            return image;
        }

        private static TMP_Text CreateText(
            string objectName,
            RectTransform parent,
            string value,
            float fontSize,
            FontStyles style,
            TextAlignmentOptions alignment,
            TMP_FontAsset font,
            Color color)
        {
            GameObject textObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            textObject.layer = parent.gameObject.layer;
            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = color;
            text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;
            if (font != null)
            {
                text.font = font;
            }

            return text;
        }

        private static void SetButtonText(
            Button button,
            string value,
            TMP_FontAsset font,
            float fontSize)
        {
            TMP_Text[] labels = button.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < labels.Length; i++)
            {
                labels[i].text = value;
                labels[i].fontSize = fontSize;
                labels[i].color = Color.white;
                if (font != null)
                {
                    labels[i].font = font;
                }

                EditorUtility.SetDirty(labels[i]);
            }
        }

        private static void SetRect(
            RectTransform rect,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 sizeDelta)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;
        }

        private static void Stretch(
            RectTransform rect,
            Vector2 offsetMin,
            Vector2 offsetMax)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static void ClearChildren(Transform parent)
        {
            while (parent.childCount > 0)
            {
                Undo.DestroyObjectImmediate(parent.GetChild(0).gameObject);
            }
        }

        private static GameObject FindSceneObject(Scene scene, string objectName)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                Transform result = FindRecursively(roots[i].transform, objectName);
                if (result != null)
                {
                    return result.gameObject;
                }
            }

            return null;
        }

        private static Transform FindRecursively(Transform current, string objectName)
        {
            if (current.name == objectName)
            {
                return current;
            }

            for (int i = 0; i < current.childCount; i++)
            {
                Transform result = FindRecursively(current.GetChild(i), objectName);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            return component != null ? component : target.AddComponent<T>();
        }

        private static T FindChildComponent<T>(
            Transform parent,
            string objectName) where T : Component
        {
            Transform child = FindRecursively(parent, objectName);
            return child != null ? child.GetComponent<T>() : null;
        }
    }
}
