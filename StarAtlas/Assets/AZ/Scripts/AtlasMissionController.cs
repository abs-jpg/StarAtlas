using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AZ.Atlas
{
    [DisallowMultipleComponent]
    public sealed class AtlasMissionController : MonoBehaviour
    {
        private const int RequiredCorrectTargets = 3;
        private const float FeedbackDelaySeconds = 1.05f;

        [SerializeField] private AtlasFocusController focusController;
        [SerializeField] private RectTransform panelRect;
        [SerializeField] private CanvasGroup panelGroup;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text rulesText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text progressText;
        [SerializeField] private Button startButton;
        [SerializeField] private Button exitButton;

        [Header("Camera HUD")]
        [SerializeField] private bool createCameraHud = true;
        [SerializeField] private Camera hudCamera;
        [SerializeField] private AtlasMissionHudPanel hudPanel;

        private readonly List<AtlasFocusController.AtlasMissionTarget> candidates =
            new List<AtlasFocusController.AtlasMissionTarget>();
        private readonly List<AtlasFocusController.AtlasMissionTarget> unusedCandidates =
            new List<AtlasFocusController.AtlasMissionTarget>();
        private readonly HashSet<string> usedTargetIds =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private AtlasFocusController.AtlasMissionTarget currentTarget;
        private int completedTargets;
        private bool missionActive;
        private bool subscribed;
        private bool acceptingSelection;
        private float nextTargetValidationTime;
        private Coroutine nextTargetRoutine;

        public bool IsMissionActive => missionActive;

        public void Configure(
            AtlasFocusController focus,
            RectTransform rect,
            CanvasGroup group,
            TMP_Text title,
            TMP_Text rules,
            TMP_Text status,
            TMP_Text progress,
            Button start,
            Button exit)
        {
            focusController = focus;
            panelRect = rect;
            panelGroup = group;
            titleText = title;
            rulesText = rules;
            statusText = status;
            progressText = progress;
            startButton = start;
            exitButton = exit;
        }

        private void Awake()
        {
            ResolveReferences();
            BindControls();
            ShowRulesState();
        }

        private void OnEnable()
        {
            ResolveReferences();
            BindControls();
            SubscribeToSelection();
        }

        private void OnDisable()
        {
            UnsubscribeFromSelection();
        }

        private void Update()
        {
            if (!missionActive ||
                !acceptingSelection ||
                focusController == null ||
                Time.unscaledTime < nextTargetValidationTime)
            {
                return;
            }

            nextTargetValidationTime = Time.unscaledTime + 1f;
            focusController.CollectMissionCandidates(candidates);
            for (int i = 0; i < candidates.Count; i++)
            {
                AtlasFocusController.AtlasMissionTarget candidate = candidates[i];
                if (candidate.kind == currentTarget.kind &&
                    string.Equals(
                        candidate.key,
                        currentTarget.key,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            SetStatus("天空位置已变化，正在为你更换一个可见目标。");
            SetHudFeedback("<color=#FFAA45>天空位置变化，正在换题</color>");
            SelectNextTarget();
        }

        private void OnDestroy()
        {
            UnsubscribeFromSelection();
            if (startButton != null)
            {
                startButton.onClick.RemoveListener(StartMission);
            }

            if (exitButton != null)
            {
                exitButton.onClick.RemoveListener(ExitMission);
            }
        }

        public void TogglePanel()
        {
            if (!gameObject.activeSelf)
            {
                ShowPanel();
                return;
            }

            HidePanel();
        }

        public void ShowPanel()
        {
            gameObject.SetActive(true);
            ResolveReferences();
            SubscribeToSelection();
            if (panelGroup != null)
            {
                panelGroup.alpha = 1f;
                panelGroup.interactable = true;
                panelGroup.blocksRaycasts = true;
            }

            if (!missionActive)
            {
                ShowRulesState();
            }
        }

        public void HidePanel()
        {
            missionActive = false;
            acceptingSelection = false;
            completedTargets = 0;
            currentTarget = default;
            StopNextTargetRoutine();
            UnsubscribeFromSelection();
            HideHud();
            if (panelGroup != null)
            {
                panelGroup.alpha = 0f;
                panelGroup.interactable = false;
                panelGroup.blocksRaycasts = false;
            }

            gameObject.SetActive(false);
        }

        public void StartMission()
        {
            ResolveReferences();
            if (focusController == null)
            {
                SetStatus("当前没有可用的天体交互系统。");
                return;
            }

            SubscribeToSelection();
            focusController.CollectMissionCandidates(candidates);
            if (candidates.Count == 0)
            {
                SetStatus("当前天空中没有满足条件的目标，请调整时间或观测地点。");
                return;
            }

            missionActive = true;
            acceptingSelection = false;
            completedTargets = 0;
            usedTargetIds.Clear();
            StopNextTargetRoutine();
            nextTargetValidationTime = Time.unscaledTime + 1f;
            ApplyActiveLayout();
            ShowHud();
            SelectNextTarget();
        }

        public void ExitMission()
        {
            HidePanel();
        }

        private void ResolveReferences()
        {
            if (focusController == null)
            {
                focusController = FindObjectOfType<AtlasFocusController>();
            }
        }

        private void BindControls()
        {
            if (startButton == null)
            {
                startButton = FindButton("Start Mission");
            }

            if (exitButton == null)
            {
                exitButton = FindButton("Exit Mission");
            }

            if (exitButton == null && startButton != null)
            {
                exitButton = Instantiate(startButton, startButton.transform.parent);
                exitButton.name = "Exit Mission";
                exitButton.onClick = new Button.ButtonClickedEvent();
                TMP_Text[] labels = exitButton.GetComponentsInChildren<TMP_Text>(true);
                for (int i = 0; i < labels.Length; i++)
                {
                    labels[i].text = "退出任务";
                    labels[i].fontSize = 32f;
                }

                exitButton.gameObject.SetActive(false);
            }

            if (startButton != null)
            {
                startButton.onClick.RemoveListener(StartMission);
                startButton.onClick.AddListener(StartMission);
            }

            if (exitButton != null)
            {
                exitButton.onClick.RemoveListener(ExitMission);
                exitButton.onClick.AddListener(ExitMission);
            }
        }

        private void SubscribeToSelection()
        {
            if (subscribed || focusController == null)
            {
                return;
            }

            focusController.TargetSelected += OnTargetSelected;
            subscribed = true;
        }

        private void UnsubscribeFromSelection()
        {
            if (!subscribed || focusController == null)
            {
                return;
            }

            focusController.TargetSelected -= OnTargetSelected;
            subscribed = false;
        }

        private void OnTargetSelected(
            string key,
            AtlasFocusController.AtlasMissionTargetKind kind,
            string displayName)
        {
            if (!missionActive || !acceptingSelection)
            {
                return;
            }

            bool correct =
                currentTarget.kind == kind &&
                string.Equals(currentTarget.key, key, StringComparison.OrdinalIgnoreCase);
            if (!correct)
            {
                acceptingSelection = false;
                StopNextTargetRoutine();
                string chosenName = string.IsNullOrWhiteSpace(displayName)
                    ? "未命名目标"
                    : displayName;
                SetStatus(
                    $"<color=#FF6B6B>选择错误</color>：你点的是“{chosenName}”。\n" +
                    $"正确目标是“{currentTarget.displayName}”，正在进入下一题...");
                SetHudFeedback(
                    $"<color=#FF6B6B>错误</color>：点了“{chosenName}”，正确是“{currentTarget.displayName}”");
                SetProgress($"{completedTargets} / {RequiredCorrectTargets}");
                nextTargetRoutine = StartCoroutine(AdvanceToNextTarget(FeedbackDelaySeconds));
                return;
            }

            acceptingSelection = false;
            StopNextTargetRoutine();
            completedTargets++;
            if (completedTargets >= RequiredCorrectTargets)
            {
                missionActive = false;
                if (titleText != null)
                {
                    titleText.text = "任务完成";
                }

                SetStatus(
                    "<color=#62E6A5>选择正确，任务完成。</color>\n" +
                    "三次定位全部正确，你已经完成本轮 AR 寻星。");
                SetHudTarget("任务完成");
                SetHudFeedback("<color=#62E6A5>正确</color>：三次定位全部完成");
                SetProgress("3 / 3");
                SetStartButtonText("再来一轮");
                return;
            }

            SetStatus(
                $"<color=#62E6A5>选择正确</color>：找到“{displayName}”。\n" +
                "正在准备下一个目标...");
            SetHudFeedback(
                $"<color=#62E6A5>正确</color>：找到“{displayName}”，准备下一题");
            nextTargetRoutine = StartCoroutine(AdvanceToNextTarget(FeedbackDelaySeconds));
        }

        private void SelectNextTarget()
        {
            focusController.CollectMissionCandidates(candidates);
            unusedCandidates.Clear();
            for (int i = 0; i < candidates.Count; i++)
            {
                AtlasFocusController.AtlasMissionTarget candidate = candidates[i];
                if (!usedTargetIds.Contains(GetTargetId(candidate)))
                {
                    unusedCandidates.Add(candidate);
                }
            }

            if (unusedCandidates.Count == 0)
            {
                missionActive = false;
                acceptingSelection = false;
                SetStatus("当前天空中没有新的未重复目标，请调整时间或观测地点后重新开始。");
                SetHudTarget("暂无新目标");
                SetHudFeedback("请调整时间或观测地点后重新开始。");
                SetStartButtonText("重新开始");
                return;
            }

            currentTarget =
                unusedCandidates[UnityEngine.Random.Range(0, unusedCandidates.Count)];
            usedTargetIds.Add(GetTargetId(currentTarget));
            acceptingSelection = true;
            string targetType = GetTargetTypeName(currentTarget.kind);
            if (titleText != null)
            {
                titleText.text = "AR 寻星任务";
            }

            SetStatus(
                $"请转动头部寻找{targetType}：<color=#FFAA45>{currentTarget.displayName}</color>\n" +
                "将射线移到名称或模型上并点击确认。");
            SetHudTarget(
                $"目标 {completedTargets + 1}/{RequiredCorrectTargets}：{targetType} {currentTarget.displayName}");
            SetHudFeedback("转头寻找目标，点击名称或模型");
            SetProgress($"{completedTargets} / {RequiredCorrectTargets}");
            SetStartButtonText("重新开始");
        }

        private IEnumerator AdvanceToNextTarget(float delaySeconds)
        {
            yield return new WaitForSecondsRealtime(delaySeconds);
            nextTargetRoutine = null;
            if (missionActive)
            {
                SelectNextTarget();
            }
        }

        private void StopNextTargetRoutine()
        {
            if (nextTargetRoutine == null)
            {
                return;
            }

            StopCoroutine(nextTargetRoutine);
            nextTargetRoutine = null;
        }

        private void ShowRulesState()
        {
            ApplyRulesLayout();
            if (titleText != null)
            {
                titleText.text = "AR 寻星任务";
            }

            if (rulesText != null)
            {
                rulesText.gameObject.SetActive(true);
                rulesText.text =
                    "玩法规则\n" +
                    "1. 目标只会从当前天空中的星座、行星、太阳和月亮中产生。\n" +
                    "2. 转动头部观察真实方位，用 Rokid 射线点击对应模型或名称。\n" +
                    "3. 同一轮不会出现重复目标，连续完成 3 个目标即完成任务。\n" +
                    "4. 调整观测地点或时间推演后，任务目标也会随天空实时变化。";
            }

            SetStatus("准备好后开始第一轮寻星。");
            SetProgress("0 / 3");
            SetStartButtonText("开始任务");
            HideHud();
        }

        private void ApplyRulesLayout()
        {
            if (panelRect != null)
            {
                panelRect.anchorMin = new Vector2(0.5f, 0.5f);
                panelRect.anchorMax = new Vector2(0.5f, 0.5f);
                panelRect.pivot = new Vector2(0.5f, 0.5f);
                panelRect.anchoredPosition = Vector2.zero;
                panelRect.sizeDelta = new Vector2(900f, 620f);
            }

            if (rulesText != null)
            {
                rulesText.gameObject.SetActive(true);
                SetTextRect(
                    rulesText,
                    new Vector2(0f, 30f),
                    new Vector2(810f, 330f));
            }

            SetTextRect(
                titleText,
                new Vector2(42f, -34f),
                new Vector2(600f, 70f),
                new Vector2(0f, 1f));
            SetTextRect(
                progressText,
                new Vector2(-42f, -40f),
                new Vector2(190f, 58f),
                new Vector2(1f, 1f));
            SetTextRect(
                statusText,
                new Vector2(0f, 120f),
                new Vector2(800f, 94f),
                new Vector2(0.5f, 0f));
            SetButtonRect(new Vector2(-22f, 24f), new Vector2(300f, 90f));
            if (exitButton != null)
            {
                exitButton.gameObject.SetActive(false);
            }
        }

        private void ApplyActiveLayout()
        {
            if (panelRect != null)
            {
                panelRect.anchorMin = new Vector2(1f, 1f);
                panelRect.anchorMax = new Vector2(1f, 1f);
                panelRect.pivot = new Vector2(1f, 1f);
                panelRect.anchoredPosition = new Vector2(-36f, -140f);
                panelRect.sizeDelta = new Vector2(680f, 330f);
            }

            if (rulesText != null)
            {
                rulesText.gameObject.SetActive(false);
            }

            SetTextRect(
                titleText,
                new Vector2(30f, -24f),
                new Vector2(470f, 58f),
                new Vector2(0f, 1f));
            SetTextRect(
                progressText,
                new Vector2(-28f, -28f),
                new Vector2(140f, 48f),
                new Vector2(1f, 1f));
            SetTextRect(
                statusText,
                new Vector2(0f, 26f),
                new Vector2(610f, 120f),
                new Vector2(0.5f, 0.5f));
            SetButtonRect(new Vector2(-22f, 22f), new Vector2(270f, 76f));
            if (exitButton != null)
            {
                exitButton.gameObject.SetActive(true);
                RectTransform exitRect = exitButton.GetComponent<RectTransform>();
                exitRect.anchorMin = new Vector2(1f, 0f);
                exitRect.anchorMax = new Vector2(1f, 0f);
                exitRect.pivot = new Vector2(1f, 0f);
                exitRect.anchoredPosition = new Vector2(-22f, 22f);
                exitRect.sizeDelta = new Vector2(180f, 76f);
            }
        }

        private Button FindButton(string objectName)
        {
            Button[] buttons = GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i].name == objectName)
                {
                    return buttons[i];
                }
            }

            return null;
        }

        private static void SetTextRect(
            TMP_Text text,
            Vector2 position,
            Vector2 size,
            Vector2? anchor = null)
        {
            if (text == null)
            {
                return;
            }

            RectTransform rect = text.rectTransform;
            Vector2 resolvedAnchor = anchor ?? new Vector2(0.5f, 0.5f);
            rect.anchorMin = resolvedAnchor;
            rect.anchorMax = resolvedAnchor;
            rect.pivot = resolvedAnchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private void SetButtonRect(Vector2 position, Vector2 size)
        {
            if (startButton == null)
            {
                return;
            }

            RectTransform rect = startButton.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private void SetStatus(string value)
        {
            if (statusText != null)
            {
                statusText.text = value;
            }
        }

        private void SetProgress(string value)
        {
            if (progressText != null)
            {
                progressText.text = value;
            }
        }

        private void SetStartButtonText(string value)
        {
            if (startButton == null)
            {
                return;
            }

            TMP_Text[] labels = startButton.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < labels.Length; i++)
            {
                labels[i].text = value;
            }
        }

        private void ShowHud()
        {
            if (!createCameraHud)
            {
                return;
            }

            EnsureHud();
            hudPanel?.SetVisible(true);
        }

        private void HideHud()
        {
            hudPanel?.SetVisible(false);
        }

        private void SetHudTarget(string value)
        {
            EnsureHud();
            hudPanel?.SetTarget(value);
        }

        private void SetHudFeedback(string value)
        {
            EnsureHud();
            hudPanel?.SetFeedback(value);
        }

        private void EnsureHud()
        {
            if (!createCameraHud)
            {
                return;
            }

            ResolveHudCamera();
            if (hudPanel == null)
            {
                GameObject hudObject = new GameObject(
                    "Atlas Mission Screen HUD",
                    typeof(RectTransform),
                    typeof(Canvas),
                    typeof(CanvasScaler),
                    typeof(GraphicRaycaster),
                    typeof(CanvasGroup),
                    typeof(Image));
                RectTransform hudRect = hudObject.GetComponent<RectTransform>();

                Canvas canvas = hudObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.sortingOrder = 1000;
                canvas.worldCamera = hudCamera;

                CanvasScaler scaler = hudObject.GetComponent<CanvasScaler>();
                scaler.dynamicPixelsPerUnit = 12f;
                scaler.referencePixelsPerUnit = 100f;

                Image background = hudObject.GetComponent<Image>();
                background.color = new Color(0.018f, 0.04f, 0.07f, 0.34f);
                background.raycastTarget = false;

                CanvasGroup hudGroup = hudObject.GetComponent<CanvasGroup>();
                hudGroup.alpha = 0f;
                hudGroup.interactable = false;
                hudGroup.blocksRaycasts = false;

                TMP_Text targetText = CreateHudText(
                    "Mission Target",
                    hudRect,
                    "目标",
                    20f,
                    FontStyles.Bold,
                    TextAlignmentOptions.MidlineLeft,
                    new Color(1f, 0.72f, 0.28f, 1f),
                    new Vector2(12f, 42f),
                    new Vector2(-12f, -6f));

                TMP_Text feedbackText = CreateHudText(
                    "Mission Feedback",
                    hudRect,
                    "准备开始寻星。",
                    18f,
                    FontStyles.Normal,
                    TextAlignmentOptions.MidlineLeft,
                    new Color(0.86f, 0.94f, 1f, 1f),
                    new Vector2(12f, 6f),
                    new Vector2(-12f, -42f));

                hudPanel = hudObject.AddComponent<AtlasMissionHudPanel>();
                hudPanel.Configure(hudCamera, targetText, feedbackText);
                return;
            }

            hudPanel.Configure(
                hudCamera,
                hudPanel.TargetText,
                hudPanel.FeedbackText);
        }

        private TMP_Text CreateHudText(
            string objectName,
            RectTransform parent,
            string value,
            float fontSize,
            FontStyles fontStyle,
            TextAlignmentOptions alignment,
            Color color,
            Vector2 offsetMin,
            Vector2 offsetMax)
        {
            GameObject textObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);

            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;

            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.alignment = alignment;
            text.color = color;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;
            if (titleText != null && titleText.font != null)
            {
                text.font = titleText.font;
            }

            return text;
        }

        private void ResolveHudCamera()
        {
            if (hudCamera != null)
            {
                return;
            }

            hudCamera = Camera.main;
            if (hudCamera != null)
            {
                return;
            }

            Camera[] cameras = FindObjectsOfType<Camera>();
            for (int i = 0; i < cameras.Length; i++)
            {
                if (cameras[i] != null && cameras[i].isActiveAndEnabled)
                {
                    hudCamera = cameras[i];
                    return;
                }
            }
        }

        private static string GetTargetId(
            AtlasFocusController.AtlasMissionTarget target)
        {
            return $"{target.kind}:{target.key}";
        }

        private static string GetTargetTypeName(
            AtlasFocusController.AtlasMissionTargetKind kind)
        {
            switch (kind)
            {
                case AtlasFocusController.AtlasMissionTargetKind.Constellation:
                    return "星座";
                case AtlasFocusController.AtlasMissionTargetKind.Star:
                    return "恒星";
                case AtlasFocusController.AtlasMissionTargetKind.Moon:
                    return "月亮";
                default:
                    return "行星";
            }
        }
    }
}
