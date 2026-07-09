using System;
using System.Collections.Generic;
using AZ.Exhibition;
using Rokid.UXR.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AZ.Atlas
{
    [DisallowMultipleComponent]
    public sealed class AtlasFocusController : MonoBehaviour
    {
        private const string PlanetRayTargetName = "Atlas Ray Target";
        private const float PanelCanvasScale = 0.00105f;
        private const int PanelRenderLayer = 0;
        private const float DefaultConstellationNameHitBoxScale = 1f;

        private readonly Dictionary<string, SolarSystemTarget> solarSystemTargets =
            new Dictionary<string, SolarSystemTarget>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ConstellationTarget> constellationTargets =
            new Dictionary<string, ConstellationTarget>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, StarTarget> starTargets =
            new Dictionary<string, StarTarget>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<TMP_Text, StarTarget> starTargetsByLabel =
            new Dictionary<TMP_Text, StarTarget>();

        private Camera observerCamera;
        private AtlasInfoCatalog catalog;
        private AtlasARStargazingController stargazingController;
        private TMP_FontAsset font;
        private float panelDistance = 1.5f;
        private float panelHorizontalOffset = 0.48f;
        private float panelVerticalOffset = 0.03f;
        private float panelFollowSmoothing = 10f;
        private float constellationNameHitBoxScale = DefaultConstellationNameHitBoxScale;

        private string selectedKey;
        private bool selectedConstellation;
        private bool selectedStar;
        private Transform activeTarget;

        private Canvas panelCanvas;
        private CanvasGroup panelGroup;
        private RectTransform panelRect;
        private TMP_Text panelTitle;
        private TMP_Text panelSummary;
        private TMP_Text panelDetailOne;
        private TMP_Text panelDetailTwo;
        private Image panelConstellationImage;
        private Button panelCloseButton;
        private float panelTargetAlpha;
        private float nextObservationRefreshTime;
        private AtlasMissionController missionController;

        public bool IsInfoPanelVisible => !string.IsNullOrEmpty(selectedKey);
        public bool IsMissionActive
        {
            get
            {
                if (missionController == null)
                {
                    missionController = FindObjectOfType<AtlasMissionController>(true);
                }

                return missionController != null && missionController.IsMissionActive;
            }
        }

        public event Action<string, AtlasMissionTargetKind, string> TargetSelected;

        public bool SetConstellationNameHitBoxScale(float scale)
        {
            float normalizedScale = NormalizeConstellationNameHitBoxScale(scale);
            if (Mathf.Abs(constellationNameHitBoxScale - normalizedScale) < 0.0001f)
            {
                return false;
            }

            constellationNameHitBoxScale = normalizedScale;
            return true;
        }

        public void SetInfoPanelPositionSettings(
            float distance,
            float horizontalOffset,
            float verticalOffset,
            float followSmoothing)
        {
            panelDistance = Mathf.Max(0.5f, distance);
            panelHorizontalOffset = horizontalOffset;
            panelVerticalOffset = verticalOffset;
            panelFollowSmoothing = Mathf.Max(0.1f, followSmoothing);
        }

        private float GetConstellationNameHitBoxScale()
        {
            return NormalizeConstellationNameHitBoxScale(constellationNameHitBoxScale);
        }

        private static float NormalizeConstellationNameHitBoxScale(float scale)
        {
            if (scale <= 0f)
            {
                return DefaultConstellationNameHitBoxScale;
            }

            return Mathf.Clamp(scale, 0.2f, 4f);
        }

        public void Initialize(
            Camera camera,
            AtlasInfoCatalog infoCatalog,
            TMP_FontAsset textFont,
            float distance,
            float horizontalOffset,
            float verticalOffset,
            float followSmoothing,
            float constellationHitBoxScale,
            AtlasARStargazingController skyController)
        {
            observerCamera = camera;
            catalog = infoCatalog;
            stargazingController = skyController;
            font = textFont;
            SetInfoPanelPositionSettings(
                distance,
                horizontalOffset,
                verticalOffset,
                followSmoothing);
            SetConstellationNameHitBoxScale(constellationHitBoxScale);
            EnsureInfoPanel();
        }

        private void LateUpdate()
        {
            RefreshSelectedObservation();
            UpdatePanelFade();
            UpdateInfoPanelPose();
        }

        public void RegisterSolarSystemBody(
            string key,
            string displayName,
            GameObject bodyRoot,
            bool missionEligible = true)
        {
            if (string.IsNullOrEmpty(key) || bodyRoot == null)
            {
                return;
            }

            if (!solarSystemTargets.TryGetValue(key, out SolarSystemTarget target) ||
                target == null ||
                target.root == null)
            {
                target = new SolarSystemTarget
                {
                    key = key,
                    displayName = displayName,
                    root = bodyRoot,
                    missionEligible = missionEligible
                };
                solarSystemTargets[key] = target;
            }
            else
            {
                target.displayName = displayName;
                target.root = bodyRoot;
                target.missionEligible = missionEligible;
            }

            if (string.Equals(key, "sun", StringComparison.OrdinalIgnoreCase))
            {
                target.missionKind = AtlasMissionTargetKind.Star;
            }
            else if (string.Equals(key, "moon", StringComparison.OrdinalIgnoreCase))
            {
                target.missionKind = AtlasMissionTargetKind.Moon;
            }
            else
            {
                target.missionKind = AtlasMissionTargetKind.Planet;
            }

            EnsurePlanetRayTarget(target);
        }

        public void RegisterStar(
            string key,
            string displayName,
            TMP_Text label,
            bool missionEligible,
            double azimuthDegrees,
            double altitudeDegrees,
            float magnitude,
            double rightAscensionDegrees,
            double declinationDegrees,
            float distanceLightYears,
            string spectralType,
            string constellationCode)
        {
            if (string.IsNullOrEmpty(key) ||
                string.IsNullOrWhiteSpace(displayName) ||
                label == null)
            {
                return;
            }

            if (!starTargets.TryGetValue(key, out StarTarget target) ||
                target == null)
            {
                target = new StarTarget { key = key };
                starTargets[key] = target;
            }

            target.displayName = displayName;
            target.label = label;
            target.azimuthDegrees = azimuthDegrees;
            target.altitudeDegrees = altitudeDegrees;
            target.magnitude = magnitude;
            target.rightAscensionDegrees = rightAscensionDegrees;
            target.declinationDegrees = declinationDegrees;
            target.distanceLightYears = distanceLightYears;
            target.spectralType = spectralType;
            target.constellationCode = constellationCode;
            // The mission treats the Sun as its only selectable star.
            target.missionEligible = false;
            starTargetsByLabel[label] = target;
            EnsureStarRayTarget(target);
        }

        public void RegisterConstellation(
            string key,
            string displayName,
            string majorStars,
            Transform skyParent,
            Vector3[] starLocalPositions,
            Vector3 labelLocalPosition,
            TMP_Text[] starNameLabels,
            TMP_Text constellationNameLabel,
            bool missionEligible = true)
        {
            if (string.IsNullOrEmpty(key) ||
                skyParent == null ||
                starLocalPositions == null ||
                starLocalPositions.Length < 2)
            {
                return;
            }

            if (!constellationTargets.TryGetValue(key, out ConstellationTarget target) ||
                target == null)
            {
                target = new ConstellationTarget { key = key };
                constellationTargets[key] = target;
            }

            target.displayName = displayName;
            target.majorStars = majorStars;
            target.skyParent = skyParent;
            target.starLocalPositions = starLocalPositions;
            target.labelLocalPosition = labelLocalPosition;
            target.starNameLabels = starNameLabels ?? Array.Empty<TMP_Text>();
            target.constellationNameLabel = constellationNameLabel;
            target.missionEligible = missionEligible;

            EnsureConstellationRayTarget(target);
        }

        public void ToggleInfoPanel(string key, bool constellation)
        {
            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            if (IsInfoPanelVisible &&
                selectedConstellation == constellation &&
                string.Equals(selectedKey, key, StringComparison.OrdinalIgnoreCase))
            {
                HideInfoPanel();
                return;
            }

            if (constellation)
            {
                if (!constellationTargets.TryGetValue(key, out ConstellationTarget target) ||
                    target == null ||
                    target.rayTarget == null)
                {
                    return;
                }

                selectedKey = key;
                selectedConstellation = true;
                selectedStar = false;
                activeTarget = target.rayTarget.transform;
                ShowInfo(BuildConstellationInfo(target));
                return;
            }

            if (solarSystemTargets.TryGetValue(key, out SolarSystemTarget body) &&
                body != null &&
                body.root != null)
            {
                selectedKey = key;
                selectedConstellation = false;
                selectedStar = false;
                activeTarget = body.root.transform;
                ShowInfo(BuildSolarSystemInfo(body));
                return;
            }

            if (starTargets.TryGetValue(key, out StarTarget star) &&
                star != null &&
                star.label != null)
            {
                selectedKey = key;
                selectedConstellation = false;
                selectedStar = true;
                activeTarget = star.label.transform;
                ShowInfo(BuildStarInfo(star));
            }
        }

        public void NotifyMissionTargetSelected(
            string key,
            AtlasMissionTargetKind kind,
            string displayName)
        {
            TargetSelected?.Invoke(key, kind, displayName);
        }

        public void CollectMissionCandidates(List<AtlasMissionTarget> results)
        {
            if (results == null)
            {
                return;
            }

            results.Clear();
            foreach (KeyValuePair<string, SolarSystemTarget> pair in solarSystemTargets)
            {
                SolarSystemTarget target = pair.Value;
                if (target == null ||
                    !target.missionEligible ||
                    target.root == null ||
                    !target.root.activeInHierarchy)
                {
                    continue;
                }

                results.Add(new AtlasMissionTarget(
                    target.key,
                    target.displayName,
                    target.missionKind));
            }

            foreach (KeyValuePair<string, StarTarget> pair in starTargets)
            {
                StarTarget target = pair.Value;
                if (target == null ||
                    !target.missionEligible ||
                    target.label == null ||
                    !target.label.gameObject.activeInHierarchy ||
                    target.rayTarget == null ||
                    !target.rayTarget.activeInHierarchy)
                {
                    continue;
                }

                results.Add(new AtlasMissionTarget(
                    target.key,
                    target.displayName,
                    AtlasMissionTargetKind.Star));
            }

            foreach (KeyValuePair<string, ConstellationTarget> pair in constellationTargets)
            {
                ConstellationTarget target = pair.Value;
                if (target == null ||
                    !target.missionEligible ||
                    target.rayTarget == null ||
                    !target.rayTarget.activeInHierarchy)
                {
                    continue;
                }

                results.Add(new AtlasMissionTarget(
                    target.key,
                    target.displayName,
                    AtlasMissionTargetKind.Constellation));
            }
        }

        public void HideInfoPanel()
        {
            selectedKey = null;
            selectedConstellation = false;
            selectedStar = false;
            activeTarget = null;
            panelTargetAlpha = 0f;
        }

        private void EnsurePlanetRayTarget(SolarSystemTarget target)
        {
            Collider bodyCollider = FindExistingBodyCollider(target.root);
            if (bodyCollider != null)
            {
                RemoveGeneratedPlanetRayTarget(target.root.transform.Find(PlanetRayTargetName));
                bodyCollider.enabled = true;
                ConfigurePlanetSelectable(target, bodyCollider);
                return;
            }

            Transform targetTransform = target.root.transform.Find(PlanetRayTargetName);
            GameObject targetObject;
            if (targetTransform == null)
            {
                targetObject = new GameObject(PlanetRayTargetName);
                targetObject.transform.SetParent(target.root.transform, false);
            }
            else
            {
                targetObject = targetTransform.gameObject;
            }

            targetObject.layer = target.root.layer;
            targetObject.transform.localPosition = Vector3.zero;
            targetObject.transform.localRotation = Quaternion.identity;
            targetObject.transform.localScale = Vector3.one;

            BoxCollider collider = targetObject.GetComponent<BoxCollider>();
            if (collider == null)
            {
                collider = targetObject.AddComponent<BoxCollider>();
            }

            CalculateMainBodyLocalBounds(target.root, out Bounds localBounds);
            Vector3 padding = Vector3.one * Mathf.Max(0.02f, localBounds.size.magnitude * 0.08f);
            float largestWorldScale = Mathf.Max(
                0.0001f,
                Mathf.Max(
                    Mathf.Abs(target.root.transform.lossyScale.x),
                    Mathf.Max(
                        Mathf.Abs(target.root.transform.lossyScale.y),
                        Mathf.Abs(target.root.transform.lossyScale.z))));
            float minimumLocalHitSize = 0.16f / largestWorldScale;
            collider.center = localBounds.center;
            collider.size = MaxComponents(
                localBounds.size + padding,
                Vector3.one * minimumLocalHitSize);

            ConfigurePlanetSelectable(target, collider);
        }

        private void ConfigurePlanetSelectable(SolarSystemTarget target, Collider targetCollider)
        {
            AtlasSelectableTarget selectable = target.root.GetComponent<AtlasSelectableTarget>();
            if (selectable == null)
            {
                selectable = target.root.AddComponent<AtlasSelectableTarget>();
            }

            selectable.Configure(
                this,
                target.key,
                false,
                targetCollider,
                true,
                target.missionEligible ? target.key : null,
                target.displayName,
                target.missionKind);
            target.selectable = selectable;
        }

        private void RemoveGeneratedPlanetRayTarget(Transform targetTransform)
        {
            if (targetTransform == null)
            {
                return;
            }

            targetTransform.gameObject.SetActive(false);
            if (Application.isPlaying)
            {
                Destroy(targetTransform.gameObject);
            }
            else
            {
                DestroyImmediate(targetTransform.gameObject);
            }
        }

        private void EnsureStarRayTarget(StarTarget target)
        {
            GameObject hitTarget = ConfigureLabelHitTarget(
                target.label,
                "Atlas Star Mission Hit");
            if (hitTarget == null)
            {
                return;
            }

            AtlasSelectableTarget selectable =
                hitTarget.GetComponent<AtlasSelectableTarget>();
            selectable.Configure(
                this,
                target.key,
                false,
                hitTarget.GetComponent<Collider>(),
                true,
                target.missionEligible ? target.key : null,
                target.displayName,
                AtlasMissionTargetKind.Star);
            target.rayTarget = hitTarget;
            target.selectable = selectable;
        }

        private void EnsureConstellationRayTarget(ConstellationTarget target)
        {
            if (target.rayTarget != null &&
                target.rayTarget.name.StartsWith(
                    "Atlas Constellation Target ",
                    StringComparison.Ordinal))
            {
                Collider legacyCollider = target.rayTarget.GetComponent<Collider>();
                if (legacyCollider != null)
                {
                    legacyCollider.enabled = false;
                }

                target.rayTarget = null;
                target.selectable = null;
            }

            for (int i = 0; i < target.starNameLabels.Length; i++)
            {
                DisableLabelHitTarget(target.starNameLabels[i], "Atlas Star Mission Hit");
            }

            if (target.constellationNameLabel != null)
            {
                target.rayTarget = ConfigureConstellationLabelHitTarget(
                    target,
                    target.constellationNameLabel,
                    $"Atlas Constellation Name Hit {target.key}",
                    false);
                target.selectable =
                    target.rayTarget != null
                        ? target.rayTarget.GetComponent<AtlasSelectableTarget>()
                        : null;
            }
        }

        private GameObject ConfigureConstellationLabelHitTarget(
            ConstellationTarget target,
            TMP_Text label,
            string hitTargetName,
            bool starNameTarget)
        {
            GameObject hitTarget = ConfigureLabelHitTarget(
                label,
                hitTargetName,
                starNameTarget ? DefaultConstellationNameHitBoxScale : GetConstellationNameHitBoxScale());
            if (hitTarget == null)
            {
                return null;
            }

            string missionKey = target.missionEligible ? target.key : null;
            string missionName = target.displayName;
            AtlasMissionTargetKind missionKind =
                AtlasMissionTargetKind.Constellation;
            if (starNameTarget &&
                starTargetsByLabel.TryGetValue(label, out StarTarget starTarget) &&
                starTarget != null)
            {
                missionKey = starTarget.missionEligible ? starTarget.key : null;
                missionName = starTarget.displayName;
                missionKind = AtlasMissionTargetKind.Star;
                starTarget.rayTarget = hitTarget;
                starTarget.selectable =
                    hitTarget.GetComponent<AtlasSelectableTarget>();

                AtlasSelectableTarget starSelectable =
                    hitTarget.GetComponent<AtlasSelectableTarget>();
                starSelectable.Configure(
                    this,
                    starTarget.key,
                    false,
                    hitTarget.GetComponent<Collider>(),
                    true,
                    target.missionEligible ? target.key : null,
                    target.displayName,
                    AtlasMissionTargetKind.Constellation);
                return hitTarget;
            }

            AtlasSelectableTarget selectable =
                hitTarget.GetComponent<AtlasSelectableTarget>();
            selectable.Configure(
                this,
                target.key,
                true,
                hitTarget.GetComponent<Collider>(),
                true,
                missionKey,
                missionName,
                missionKind);
            return hitTarget;
        }

        private static GameObject ConfigureLabelHitTarget(
            TMP_Text label,
            string hitTargetName,
            float hitBoxScale = DefaultConstellationNameHitBoxScale)
        {
            if (label == null)
            {
                return null;
            }

            Transform hitTransform = label.transform.Find(hitTargetName);
            GameObject hitTarget;
            if (hitTransform == null)
            {
                hitTarget = new GameObject(hitTargetName);
                hitTarget.transform.SetParent(label.transform, false);
            }
            else
            {
                hitTarget = hitTransform.gameObject;
            }

            hitTarget.SetActive(true);
            hitTarget.layer = label.gameObject.layer;
            hitTarget.transform.localPosition = Vector3.zero;
            hitTarget.transform.localRotation = Quaternion.identity;
            hitTarget.transform.localScale = Vector3.one;

            label.ForceMeshUpdate(true, true);
            Bounds textBounds = label.textBounds;
            Vector3 boundsCenter = textBounds.center;
            Vector3 boundsSize = textBounds.size;
            if (boundsSize.x <= 0.0001f || boundsSize.y <= 0.0001f)
            {
                Vector2 renderedSize = label.GetRenderedValues(false);
                boundsCenter = Vector3.zero;
                boundsSize = new Vector3(
                    Mathf.Max(0.0001f, renderedSize.x),
                    Mathf.Max(0.0001f, renderedSize.y),
                    Mathf.Max(0.0001f, boundsSize.z));
            }

            Vector3 lossyScale = label.transform.lossyScale;
            float scaleX = Mathf.Max(0.000001f, Mathf.Abs(lossyScale.x));
            float scaleY = Mathf.Max(0.000001f, Mathf.Abs(lossyScale.y));
            float scaleZ = Mathf.Max(0.000001f, Mathf.Abs(lossyScale.z));

            BoxCollider collider = hitTarget.GetComponent<BoxCollider>();
            if (collider == null)
            {
                collider = hitTarget.AddComponent<BoxCollider>();
            }

            Vector3 localPadding = new Vector3(
                0.025f / scaleX,
                0.018f / scaleY,
                0.045f / scaleZ);
            Vector3 minimumLocalSize = new Vector3(
                0.1f / scaleX,
                0.06f / scaleY,
                0.045f / scaleZ);
            Vector3 baseColliderSize = MaxComponents(
                boundsSize + localPadding,
                minimumLocalSize);
            float safeHitBoxScale = NormalizeConstellationNameHitBoxScale(hitBoxScale);
            collider.center = boundsCenter;
            collider.size = MaxComponents(
                baseColliderSize * safeHitBoxScale,
                Vector3.one * 0.001f);
            collider.enabled = true;

            AtlasSelectableTarget selectable = hitTarget.GetComponent<AtlasSelectableTarget>();
            if (selectable == null)
            {
                selectable = hitTarget.AddComponent<AtlasSelectableTarget>();
            }
            selectable.enabled = true;

            return hitTarget;
        }

        private static void DisableLabelHitTarget(TMP_Text label, string hitTargetName)
        {
            if (label == null)
            {
                return;
            }

            Transform hitTarget = label.transform.Find(hitTargetName);
            if (hitTarget == null)
            {
                return;
            }

            Collider[] colliders = hitTarget.GetComponents<Collider>();
            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = false;
            }

            MonoBehaviour[] behaviours = hitTarget.GetComponents<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                behaviours[i].enabled = false;
            }

            hitTarget.gameObject.SetActive(false);
        }

        private void EnsureInfoPanel()
        {
            if (panelCanvas != null)
            {
                return;
            }

            AtlasInfoPanelView sceneView = FindObjectOfType<AtlasInfoPanelView>(true);
            if (sceneView != null && sceneView.IsConfigured)
            {
                sceneView.BindCamera(observerCamera);
                panelCanvas = sceneView.PanelCanvas;
                panelGroup = sceneView.CanvasGroup;
                panelRect = sceneView.PanelRect;
                panelRect.localScale = Vector3.one * PanelCanvasScale;
                SetLayerRecursively(panelCanvas.gameObject, PanelRenderLayer);
                panelTitle = sceneView.TitleText;
                panelSummary = sceneView.SummaryText;
                panelDetailOne = sceneView.DetailOneText;
                panelDetailTwo = sceneView.DetailTwoText;
                panelConstellationImage = sceneView.ConstellationImage;
                if (panelConstellationImage == null)
                {
                    panelConstellationImage = CreateImage(
                        "Constellation Image",
                        panelRect,
                        Color.white);
                    sceneView.SetConstellationImage(panelConstellationImage);
                }

                ConfigureConstellationImageRect(panelConstellationImage);
                panelConstellationImage.gameObject.SetActive(false);
                ConfigurePanelTextSizing();
                EnsureInfoPanelCloseButton();
                sceneView.HideImmediate();
                return;
            }

            Debug.LogError(
                "Atlas scene does not contain a configured AtlasInfoPanelView. " +
                "Use AZ/Atlas/Create Or Repair Info Panel.",
                this);

            GameObject canvasObject = new GameObject(
                "Atlas Info Panel",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(CanvasGroup),
                typeof(CanvasRegister));
            canvasObject.transform.SetParent(transform, false);
            SetLayerRecursively(canvasObject, PanelRenderLayer);

            panelCanvas = canvasObject.GetComponent<Canvas>();
            panelCanvas.renderMode = RenderMode.WorldSpace;
            panelCanvas.worldCamera = observerCamera;
            panelCanvas.sortingOrder = 120;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 12f;
            scaler.referencePixelsPerUnit = 100f;

            panelRect = canvasObject.GetComponent<RectTransform>();
            panelRect.sizeDelta = new Vector2(720f, 610f);
            panelRect.localScale = Vector3.one * PanelCanvasScale;

            panelGroup = canvasObject.GetComponent<CanvasGroup>();
            panelGroup.alpha = 0f;
            panelGroup.interactable = false;
            panelGroup.blocksRaycasts = false;

            Image background = CreateImage(
                "Background",
                panelRect,
                new Color(0.025f, 0.035f, 0.055f, 0.91f));
            Stretch(background.rectTransform, Vector2.zero, Vector2.zero);

            panelConstellationImage = CreateImage(
                "Constellation Image",
                panelRect,
                Color.white);
            ConfigureConstellationImageRect(panelConstellationImage);
            panelConstellationImage.gameObject.SetActive(false);

            panelTitle = CreateText(
                "Title",
                panelRect,
                new Vector2(0f, 224f),
                new Vector2(630f, 70f),
                42f,
                FontStyles.Bold,
                TextAlignmentOptions.Left);
            panelSummary = CreateText(
                "Summary",
                panelRect,
                new Vector2(0f, 86f),
                new Vector2(630f, 260f),
                24f,
                FontStyles.Normal,
                TextAlignmentOptions.TopLeft);
            panelDetailOne = CreateText(
                "Detail One",
                panelRect,
                new Vector2(0f, -104f),
                new Vector2(630f, 82f),
                22f,
                FontStyles.Normal,
                TextAlignmentOptions.TopLeft);
            panelDetailTwo = CreateText(
                "Detail Two",
                panelRect,
                new Vector2(0f, -188f),
                new Vector2(630f, 76f),
                22f,
                FontStyles.Normal,
                TextAlignmentOptions.TopLeft);
            panelSummary.enableAutoSizing = true;
            panelSummary.fontSizeMin = 15f;
            panelSummary.fontSizeMax = 24f;
            ConfigurePanelTextSizing();
            EnsureInfoPanelCloseButton();

            canvasObject.SetActive(false);
        }

        private void ConfigurePanelTextSizing()
        {
            if (panelSummary != null)
            {
                panelSummary.enableAutoSizing = true;
                panelSummary.fontSizeMin = 13f;
                panelSummary.fontSizeMax = 24f;
            }

            if (panelDetailOne != null)
            {
                panelDetailOne.enableAutoSizing = true;
                panelDetailOne.fontSizeMin = 12f;
                panelDetailOne.fontSizeMax = 22f;
            }

            if (panelDetailTwo != null)
            {
                panelDetailTwo.enableAutoSizing = true;
                panelDetailTwo.fontSizeMin = 9f;
                panelDetailTwo.fontSizeMax = 22f;
            }
        }

        private void EnsureInfoPanelCloseButton()
        {
            if (panelRect == null)
            {
                return;
            }

            Transform existing = panelRect.Find("Swap3");
            GameObject buttonObject;
            if (existing != null)
            {
                buttonObject = existing.gameObject;
            }
            else
            {
                Button template = FindCloseButtonTemplate();
                buttonObject = template != null
                    ? Instantiate(template.gameObject, panelRect, false)
                    : CreateFallbackCloseButtonObject(panelRect);
                buttonObject.name = "Swap3";
            }

            buttonObject.transform.SetParent(panelRect, false);
            buttonObject.SetActive(true);
            SetLayerRecursively(buttonObject, PanelRenderLayer);

            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            if (rect == null)
            {
                Debug.LogWarning("Atlas info panel close button needs a RectTransform.", buttonObject);
                return;
            }

            rect.anchorMin = Vector2.one;
            rect.anchorMax = Vector2.one;
            rect.pivot = Vector2.one;
            rect.anchoredPosition = new Vector2(-22f, -22f);
            rect.sizeDelta = new Vector2(132f, 58f);
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;

            panelCloseButton = buttonObject.GetComponent<Button>();
            if (panelCloseButton == null)
            {
                panelCloseButton = buttonObject.AddComponent<Button>();
            }

            Graphic targetGraphic = buttonObject.GetComponent<Graphic>();
            if (targetGraphic == null)
            {
                targetGraphic = buttonObject.GetComponentInChildren<Graphic>(true);
            }

            if (targetGraphic != null)
            {
                targetGraphic.raycastTarget = true;
                panelCloseButton.targetGraphic = targetGraphic;
            }

            TMP_Text[] labels = buttonObject.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < labels.Length; i++)
            {
                TMP_Text label = labels[i];
                label.text = "返回";
                label.fontSize = 28f;
                label.alignment = TextAlignmentOptions.Center;
                label.raycastTarget = false;
                if (font != null)
                {
                    label.font = font;
                }
            }

            panelCloseButton.onClick = new Button.ButtonClickedEvent();
            panelCloseButton.onClick.AddListener(HideInfoPanel);
            panelCloseButton.interactable = true;
            ConfigureInfoPanelCloseCollider(buttonObject, rect);
        }

        private void ConfigureInfoPanelCloseCollider(GameObject buttonObject, RectTransform rect)
        {
            if (buttonObject == null || rect == null)
            {
                return;
            }

            BoxCollider boxCollider = buttonObject.GetComponent<BoxCollider>();
            if (boxCollider == null)
            {
                boxCollider = buttonObject.AddComponent<BoxCollider>();
            }

            Vector2 size = rect.sizeDelta;
            if (size.x <= 0.001f || size.y <= 0.001f)
            {
                size = rect.rect.size;
            }

            size.x = Mathf.Max(1f, size.x);
            size.y = Mathf.Max(1f, size.y);
            boxCollider.size = new Vector3(size.x, size.y, 32f);
            boxCollider.center = new Vector3(
                (0.5f - rect.pivot.x) * size.x,
                (0.5f - rect.pivot.y) * size.y,
                0f);
            boxCollider.isTrigger = false;
            boxCollider.enabled = true;

            AtlasInfoPanelCloseTarget closeTarget = buttonObject.GetComponent<AtlasInfoPanelCloseTarget>();
            if (closeTarget == null)
            {
                closeTarget = buttonObject.AddComponent<AtlasInfoPanelCloseTarget>();
            }

            closeTarget.Configure(this, panelCloseButton, boxCollider);
        }

        private Button FindCloseButtonTemplate()
        {
            Button[] buttons = FindObjectsOfType<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                Button button = buttons[i];
                if (button == null || button.transform == null)
                {
                    continue;
                }

                if (panelRect != null && button.transform.IsChildOf(panelRect))
                {
                    continue;
                }

                if (string.Equals(button.name, "ButtonBasic_White_Pull", StringComparison.OrdinalIgnoreCase))
                {
                    return button;
                }
            }

            for (int i = 0; i < buttons.Length; i++)
            {
                Button button = buttons[i];
                if (button == null || button.transform == null)
                {
                    continue;
                }

                if (panelRect != null && button.transform.IsChildOf(panelRect))
                {
                    continue;
                }

                if (button.name.IndexOf("ButtonBasic", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return button;
                }
            }

            return null;
        }

        private GameObject CreateFallbackCloseButtonObject(RectTransform parent)
        {
            GameObject buttonObject = new GameObject(
                "Swap3",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button));
            buttonObject.transform.SetParent(parent, false);

            Image image = buttonObject.GetComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.16f);
            image.raycastTarget = true;

            GameObject labelObject = new GameObject(
                "Label",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(buttonObject.transform, false);
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            Stretch(labelRect, Vector2.zero, Vector2.zero);

            TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
            label.text = "返回";
            label.fontSize = 28f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.raycastTarget = false;
            if (font != null)
            {
                label.font = font;
            }

            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            return buttonObject;
        }


        private static void ConfigureConstellationImageRect(Image image)
        {
            if (image == null)
            {
                return;
            }

            RectTransform rect = image.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(-205f, 112f);
            rect.sizeDelta = new Vector2(210f, 190f);
            image.preserveAspect = true;
            image.raycastTarget = false;
        }

        private void ConfigurePanelLayout(bool showConstellationImage)
        {
            if (panelConstellationImage != null)
            {
                panelConstellationImage.gameObject.SetActive(showConstellationImage);
            }

            ConfigureTextRect(
                panelTitle,
                new Vector2(0f, 252f),
                new Vector2(630f, 58f));
            ConfigureTextRect(
                panelSummary,
                showConstellationImage
                    ? new Vector2(112f, 112f)
                    : new Vector2(0f, 132f),
                showConstellationImage
                    ? new Vector2(370f, 190f)
                    : new Vector2(630f, 160f));
            ConfigureTextRect(
                panelDetailOne,
                new Vector2(0f, -8f),
                new Vector2(630f, 82f));
            ConfigureTextRect(
                panelDetailTwo,
                new Vector2(0f, -172f),
                new Vector2(630f, 224f));
        }

        private static void ConfigureTextRect(
            TMP_Text text,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            RectTransform rect = text != null ? text.rectTransform : null;
            if (rect == null)
            {
                return;
            }

            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }

        private void ShowInfo(InfoContent content)
        {
            EnsureInfoPanel();
            if (panelCanvas == null ||
                panelGroup == null ||
                panelRect == null ||
                panelTitle == null ||
                panelSummary == null ||
                panelDetailOne == null ||
                panelDetailTwo == null)
            {
                Debug.LogError("Atlas info panel references are incomplete.", this);
                return;
            }

            bool wasVisible = panelCanvas.gameObject.activeSelf;
            panelTitle.text = content.title;
            panelSummary.text = content.summary;
            panelDetailOne.text = content.detailOne;
            panelDetailTwo.text = content.detailTwo;
            bool showImage = content.constellationImage != null;
            if (panelConstellationImage != null)
            {
                panelConstellationImage.sprite = content.constellationImage;
            }
            ConfigurePanelLayout(showImage);
            panelCanvas.gameObject.SetActive(true);
            if (!wasVisible)
            {
                panelGroup.alpha = 0f;
            }
            panelGroup.interactable = true;
            panelGroup.blocksRaycasts = true;
            panelTargetAlpha = 1f;
            nextObservationRefreshTime = Time.unscaledTime + 1f;
            UpdateInfoPanelPose(true);
        }

        private void UpdatePanelFade()
        {
            if (panelGroup == null || !panelGroup.gameObject.activeSelf)
            {
                return;
            }

            panelGroup.alpha = Mathf.MoveTowards(
                panelGroup.alpha,
                panelTargetAlpha,
                Time.deltaTime * 3.5f);
            bool visible = panelGroup.alpha > 0.01f;
            panelGroup.interactable = visible;
            panelGroup.blocksRaycasts = visible;

            if (!visible && panelTargetAlpha <= 0f && !IsInfoPanelVisible)
            {
                panelCanvas.gameObject.SetActive(false);
            }
        }

        private void UpdateInfoPanelPose(bool snap = false)
        {
            if (panelCanvas == null ||
                !panelCanvas.gameObject.activeSelf ||
                observerCamera == null)
            {
                return;
            }

            Transform cameraTransform = observerCamera.transform;
            panelRect.localScale = Vector3.one * PanelCanvasScale;
            Vector3 viewDirection = cameraTransform.forward;
            if (viewDirection.sqrMagnitude < 0.0001f)
            {
                viewDirection = Vector3.forward;
            }

            viewDirection.Normalize();
            Vector3 desiredPosition =
                cameraTransform.position +
                viewDirection * panelDistance +
                cameraTransform.right * panelHorizontalOffset +
                cameraTransform.up * panelVerticalOffset;

            panelRect.position = snap
                ? desiredPosition
                : Vector3.Lerp(
                    panelRect.position,
                    desiredPosition,
                    1f - Mathf.Exp(-panelFollowSmoothing * Time.deltaTime));
            Vector3 direction = panelRect.position - cameraTransform.position;
            if (direction.sqrMagnitude > 0.0001f)
            {
                panelRect.rotation = Quaternion.LookRotation(
                    direction.normalized,
                    cameraTransform.up);
            }
        }

        private InfoContent BuildSolarSystemInfo(SolarSystemTarget target)
        {
            string normalizedKey = NormalizeBodyKey(target.key);
            AtlasInfoCatalogEntry entry = catalog != null
                ? catalog.Find(normalizedKey, AtlasInfoEntryType.SolarSystemBody)
                : null;
            string title = entry != null
                ? entry.Title
                : string.IsNullOrEmpty(target.displayName)
                    ? target.key
                    : target.displayName;
            string summary = entry != null && !string.IsNullOrWhiteSpace(entry.summary)
                ? entry.summary
                : "该天体的科普简介尚未在 AtlasInfoCatalog 中填写。";

            if (stargazingController != null &&
                stargazingController.TryGetSolarSystemObservation(
                    normalizedKey,
                    out AtlasARStargazingController.AtlasObservationInfo observation))
            {
                return new InfoContent
                {
                    title = title,
                    summary = summary,
                    detailOne = BuildCurrentPositionText(observation),
                    detailTwo = BuildObservationScheduleText(observation)
                };
            }

            return new InfoContent
            {
                title = title,
                summary = summary,
                detailOne = "当前位置：等待观测点和天体坐标数据。",
                detailTwo = "升落时间、亮度和推荐观测时间将在定位数据可用后显示。"
            };
        }

        private InfoContent BuildConstellationInfo(ConstellationTarget target)
        {
            AtlasInfoCatalogEntry entry = catalog != null
                ? catalog.Find(target.key, AtlasInfoEntryType.Constellation)
                : null;
            GetConstellationCopy(target.key, out string fallbackSummary, out string fallbackMythology);
            string summary = entry != null && !string.IsNullOrWhiteSpace(entry.summary)
                ? entry.summary
                : fallbackSummary;
            string majorStars = entry != null && !string.IsNullOrWhiteSpace(entry.majorStars)
                ? entry.majorStars
                : target.majorStars;
            string mythology = entry != null && !string.IsNullOrWhiteSpace(entry.mythologyAndCulture)
                ? entry.mythologyAndCulture
                : fallbackMythology;
            return new InfoContent
            {
                title = entry != null ? entry.Title : target.displayName,
                summary = summary,
                detailOne = $"代表恒星：{majorStars}",
                detailTwo = $"神话与文化：{mythology}",
                constellationImage = entry != null ? entry.constellationImage : null
            };
        }

        private InfoContent BuildStarInfo(StarTarget target)
        {
            AtlasInfoCatalogEntry entry = catalog != null
                ? catalog.Find(target.key, AtlasInfoEntryType.Star)
                : null;
            string constellationName = GetConstellationDisplayName(
                target.constellationCode,
                target.displayName);
            string title = entry != null ? entry.Title : target.displayName;
            string summary = entry != null && !string.IsNullOrWhiteSpace(entry.summary)
                ? entry.summary
                : string.IsNullOrEmpty(constellationName)
                    ? $"{title}是一颗能够自行发光的恒星。它在天空中的位置会随观测地点和时间变化。"
                    : $"{title}是位于{constellationName}方向的一颗恒星。{constellationName}是从地球视角划分的天空区域，并不是一颗单独的恒星。";

            string horizonSide = target.altitudeDegrees >= 0.0
                ? "地平线上方"
                : "地平线下方";
            string detailOne =
                $"当前方位角：{target.azimuthDegrees:F1}°（{GetAzimuthDirection(target.azimuthDegrees)}）    " +
                $"高度角：{target.altitudeDegrees:F1}°\n" +
                $"距离地平线高度：{horizonSide} {Math.Abs(target.altitudeDegrees):F1}°";

            string distance = target.distanceLightYears > 0.01f
                ? $"{target.distanceLightYears:F1} 光年"
                : "暂无数据";
            string spectralType = string.IsNullOrWhiteSpace(target.spectralType)
                ? "暂无数据"
                : target.spectralType;
            string visibility = GetStarVisibilityText(
                target.altitudeDegrees,
                target.magnitude);
            string recommendation = target.altitudeDegrees < 0.0
                ? "等待它升到地平线上方；高度角超过20°时观测效果更好。"
                : target.altitudeDegrees < 20.0
                    ? "当前较贴近地平线，等待高度角超过20°并避开强光。"
                    : "当前高度适合观测，尽量选择远离城市灯光的区域。";

            string detailTwo =
                $"当前亮度：视星等 {target.magnitude:F2}    可见性：{visibility}\n" +
                $"赤经：{target.rightAscensionDegrees:F2}°    赤纬：{target.declinationDegrees:F2}°\n" +
                $"距离：{distance}    光谱类型：{spectralType}\n" +
                $"推荐观测：{recommendation}";

            return new InfoContent
            {
                title = title,
                summary = summary,
                detailOne = detailOne,
                detailTwo = detailTwo
            };
        }

        private void RefreshSelectedObservation()
        {
            if (selectedConstellation ||
                string.IsNullOrEmpty(selectedKey) ||
                Time.unscaledTime < nextObservationRefreshTime ||
                panelCanvas == null ||
                !panelCanvas.gameObject.activeSelf)
            {
                return;
            }

            nextObservationRefreshTime = Time.unscaledTime + 1f;
            if (selectedStar)
            {
                if (starTargets.TryGetValue(selectedKey, out StarTarget star) &&
                    star != null)
                {
                    ApplyInfoText(BuildStarInfo(star));
                }
                return;
            }

            if (!solarSystemTargets.TryGetValue(
                    selectedKey,
                    out SolarSystemTarget target) ||
                target == null)
            {
                return;
            }

            ApplyInfoText(BuildSolarSystemInfo(target));
        }

        private void ApplyInfoText(InfoContent content)
        {
            panelTitle.text = content.title;
            panelSummary.text = content.summary;
            panelDetailOne.text = content.detailOne;
            panelDetailTwo.text = content.detailTwo;
        }

        private static string BuildCurrentPositionText(
            AtlasARStargazingController.AtlasObservationInfo observation)
        {
            string horizonSide = observation.altitudeDegrees >= 0.0
                ? "地平线上方"
                : "地平线下方";
            return
                $"当前方位角：{observation.azimuthDegrees:F1}°（{GetAzimuthDirection(observation.azimuthDegrees)}）    " +
                $"高度角：{observation.altitudeDegrees:F1}°\n" +
                $"距离地平线高度：{horizonSide} {Math.Abs(observation.altitudeDegrees):F1}°";
        }

        private static string BuildObservationScheduleText(
            AtlasARStargazingController.AtlasObservationInfo observation)
        {
            string rise = observation.hasRise
                ? FormatUtc(observation.riseUtc)
                : observation.alwaysAboveHorizon
                    ? "未来48小时持续在地平线上"
                    : "未来48小时无升起";
            string set = observation.hasSet
                ? FormatUtc(observation.setUtc)
                : observation.alwaysAboveHorizon
                    ? "未来48小时不落"
                    : "未来48小时无落下";
            string transit = observation.hasTransit
                ? $"{FormatUtc(observation.transitUtc)}（最高 {observation.transitAltitudeDegrees:F1}°）"
                : "暂无";
            string visibility = GetVisibilityText(observation);
            string recommendation = GetRecommendedObservationText(observation);

            return
                $"升起：{rise}    过中天：{transit}\n" +
                $"落下：{set}\n" +
                $"当前亮度：视星等 {observation.magnitude:F1}    可见性：{visibility}\n" +
                $"推荐观测时间：{recommendation}";
        }

        private static string GetVisibilityText(
            AtlasARStargazingController.AtlasObservationInfo observation)
        {
            if (observation.altitudeDegrees < 0.0)
            {
                return "位于地平线下，当前不可见";
            }

            if (observation.key == "sun")
            {
                return "可见，禁止裸眼或无滤光设备直视";
            }

            if (observation.altitudeDegrees < 10.0)
            {
                return "贴近地平线，易受建筑和大气影响";
            }

            if (observation.sunAltitudeDegrees > -6.0 && observation.magnitude > -3.0f)
            {
                return "天光较强，肉眼较难观察";
            }

            if (observation.magnitude <= 1.5f)
            {
                return "条件良好时肉眼清晰可见";
            }

            if (observation.magnitude <= 6.0f)
            {
                return "暗空环境下可见，双筒镜效果更好";
            }

            return "通常需要双筒镜或望远镜";
        }

        private static string GetStarVisibilityText(
            double altitudeDegrees,
            float magnitude)
        {
            if (altitudeDegrees < 0.0)
            {
                return "位于地平线下，当前不可见";
            }

            if (altitudeDegrees < 10.0)
            {
                return "贴近地平线，易受建筑和大气影响";
            }

            if (magnitude <= 1.5f)
            {
                return "肉眼清晰可见";
            }

            if (magnitude <= 3.5f)
            {
                return "普通夜空下肉眼可见";
            }

            if (magnitude <= 6.0f)
            {
                return "需要较暗天空，城市中可能不可见";
            }

            return "通常需要双筒镜或望远镜";
        }

        private static string GetConstellationDisplayName(
            string constellationCode,
            string starDisplayName)
        {
            if (!string.IsNullOrWhiteSpace(starDisplayName))
            {
                int suffix = starDisplayName.IndexOf('座');
                if (suffix >= 1)
                {
                    return starDisplayName.Substring(0, suffix + 1);
                }
            }

            switch ((constellationCode ?? string.Empty).Trim())
            {
                case "And": return "仙女座";
                case "Aqr": return "宝瓶座";
                case "Aql": return "天鹰座";
                case "Ari": return "白羊座";
                case "Aur": return "御夫座";
                case "Boo": return "牧夫座";
                case "Cnc": return "巨蟹座";
                case "CVn": return "猎犬座";
                case "CMa": return "大犬座";
                case "CMi": return "小犬座";
                case "Cap": return "摩羯座";
                case "Car": return "船底座";
                case "Cas": return "仙后座";
                case "Cen": return "半人马座";
                case "Cep": return "仙王座";
                case "Cet": return "鲸鱼座";
                case "Cyg": return "天鹅座";
                case "Gem": return "双子座";
                case "Her": return "武仙座";
                case "Leo": return "狮子座";
                case "Lib": return "天秤座";
                case "Lyr": return "天琴座";
                case "Ori": return "猎户座";
                case "Oph": return "蛇夫座";
                case "Peg": return "飞马座";
                case "Per": return "英仙座";
                case "Psc": return "双鱼座";
                case "Sgr": return "人马座";
                case "Sco": return "天蝎座";
                case "Tau": return "金牛座";
                case "UMa": return "大熊座";
                case "UMi": return "小熊座";
                case "Vir": return "处女座";
                default: return string.Empty;
            }
        }

        private static string GetRecommendedObservationText(
            AtlasARStargazingController.AtlasObservationInfo observation)
        {
            if (observation.key == "sun")
            {
                return "日出后至日落前；必须使用合格的太阳滤光设备。";
            }

            string transit = observation.hasTransit
                ? FormatUtc(observation.transitUtc)
                : "高度角较高时";
            if (observation.key == "mercury" || observation.key == "venus")
            {
                return $"日出前或日落后的暮光时段，优先选择接近 {transit} 的高高度时刻。";
            }

            if (observation.key == "moon")
            {
                return $"月球升起后、接近 {transit} 且高度角超过20°时。";
            }

            return $"当地夜间、接近 {transit} 前后约1小时。";
        }

        private static string FormatUtc(DateTime utc)
        {
            return utc.ToUniversalTime().ToString("MM-dd HH:mm 'UTC'");
        }

        private static string GetAzimuthDirection(double azimuthDegrees)
        {
            string[] directions =
            {
                "北", "东北", "东", "东南",
                "南", "西南", "西", "西北"
            };
            int index = (int)Math.Floor((azimuthDegrees + 22.5) / 45.0) % 8;
            return directions[index < 0 ? index + 8 : index];
        }

        private static string NormalizeBodyKey(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            switch (value.Trim().ToLowerInvariant())
            {
                case "\u592a\u9633":
                    return "sun";
                case "\u6c34\u661f":
                    return "mercury";
                case "\u91d1\u661f":
                    return "venus";
                case "\u5730\u7403":
                    return "earth";
                case "\u706b\u661f":
                    return "mars";
                case "\u6728\u661f":
                    return "jupiter";
                case "\u571f\u661f":
                    return "saturn";
                case "\u5929\u738b\u661f":
                    return "uranus";
                case "\u6d77\u738b\u661f":
                    return "neptune";
                case "\u6708\u7403":
                case "\u6708\u4eae":
                    return "moon";
                default:
                    return value.Trim().ToLowerInvariant();
            }
        }

        private static void GetConstellationCopy(
            string key,
            out string summary,
            out string mythology)
        {
            switch (key)
            {
                case "big-dipper":
                    summary = "\u5317\u6597\u4e03\u661f\u662f\u5927\u718a\u5ea7\u4e2d\u4e03\u9897\u4eae\u661f\u7ec4\u6210\u7684\u8457\u540d\u661f\u7fa4\uff0c\u6597\u53e3\u4e24\u661f\u7684\u8fde\u7ebf\u53ef\u7528\u6765\u5bfb\u627e\u5317\u6781\u661f\u3002";
                    mythology = "\u4e2d\u56fd\u53e4\u4ee3\u661f\u5b98\u4f53\u7cfb\uff0c\u5e38\u88ab\u89c6\u4e3a\u6307\u793a\u5b63\u8282\u3001\u65f6\u8fb0\u4e0e\u65b9\u5411\u7684\u5929\u7a7a\u6807\u5fd7\u3002";
                    break;
                case "orion":
                    summary = "\u730e\u6237\u5ea7\u8de8\u8d8a\u5929\u8d64\u9053\uff0c\u4e09\u9897\u6574\u9f50\u7684\u8170\u5e26\u661f\u4f7f\u5b83\u6210\u4e3a\u51ac\u5b63\u591c\u7a7a\u4e2d\u6700\u6613\u8fa8\u8ba4\u7684\u661f\u5ea7\u4e4b\u4e00\u3002";
                    mythology = "\u53e4\u5e0c\u814a\u795e\u8bdd\u4e2d\u7684\u730e\u4eba\u4fc4\u91cc\u7fc1\uff0c\u5e38\u4e0e\u5929\u874e\u5ea7\u7684\u6545\u4e8b\u76f8\u8054\u7cfb\u3002";
                    break;
                case "cassiopeia":
                    summary = "\u4ed9\u540e\u5ea7\u4ee5\u9192\u76ee\u7684 W \u6216 M \u5f62\u72b6\u8457\u79f0\uff0c\u5728\u5317\u534a\u7403\u4e2d\u9ad8\u7eac\u5730\u533a\u591a\u6570\u65f6\u5019\u53ef\u89c1\u3002";
                    mythology = "\u53e4\u5e0c\u814a\u795e\u8bdd\u4e2d\u57c3\u585e\u4fc4\u6bd4\u4e9a\u738b\u540e\u5361\u897f\u5965\u4f69\u5a05\uff0c\u56e0\u5938\u8000\u7f8e\u8c8c\u800c\u53d7\u5230\u60e9\u7f5a\u3002";
                    break;
                case "cygnus":
                    summary = "\u5929\u9e45\u5ea7\u7684\u4eae\u661f\u6784\u6210\u201c\u5317\u5341\u5b57\u201d\uff0c\u5176\u4e3b\u661f\u5929\u6d25\u56db\u4e5f\u662f\u590f\u5b63\u5927\u4e09\u89d2\u7684\u4e00\u89d2\u3002";
                    mythology = "\u53e4\u5e0c\u814a\u4f20\u8bf4\u4e2d\u5e38\u4e0e\u5b99\u65af\u5316\u8eab\u7684\u5929\u9e45\uff0c\u6216\u82f1\u96c4\u4fc4\u83f2\u6602\u7684\u6545\u4e8b\u76f8\u5173\u3002";
                    break;
                case "lyra":
                    summary = "\u5929\u7434\u5ea7\u867d\u9762\u79ef\u4e0d\u5927\uff0c\u5374\u5305\u542b\u590f\u5b63\u591c\u7a7a\u660e\u4eae\u7684\u7ec7\u5973\u661f\uff0c\u5b83\u540c\u6837\u662f\u590f\u5b63\u5927\u4e09\u89d2\u7684\u4e00\u89d2\u3002";
                    mythology = "\u53e4\u5e0c\u814a\u795e\u8bdd\u4e2d\u97f3\u4e50\u5bb6\u4fc4\u83f2\u65af\u7684\u91cc\u62c9\u7434\uff1b\u4e2d\u56fd\u4f20\u8bf4\u5219\u5c06\u7ec7\u5973\u661f\u4e0e\u725b\u90ce\u7ec7\u5973\u6545\u4e8b\u76f8\u8054\u3002";
                    break;
                case "aries":
                    summary = "\u767d\u7f8a\u5ea7\u662f\u9ec4\u9053\u5341\u4e8c\u661f\u5ea7\u4e4b\u4e00\uff0c\u4e09\u9897\u4e3b\u8981\u4eae\u661f\u7ec4\u6210\u4e00\u6761\u77ed\u800c\u5f2f\u66f2\u7684\u661f\u94fe\uff0c\u5317\u534a\u7403\u79cb\u51ac\u5b63\u8f83\u5bb9\u6613\u5bfb\u627e\u3002\u6700\u4eae\u661f\u5a04\u5bbf\u4e09\u5448\u6a59\u9ec4\u8272\uff1b\u661f\u5ea7\u6574\u4f53\u8f83\u6697\uff0c\u9002\u5408\u4ece\u6634\u661f\u56e2\u4e0e\u4ed9\u5973\u5ea7\u4e4b\u95f4\u5b9a\u4f4d\u3002";
                    mythology = "\u53e4\u5e0c\u814a\u795e\u8bdd\u4e2d\uff0c\u5b83\u8c61\u5f81\u62ef\u6551\u4f5b\u91cc\u514b\u7d22\u65af\u4e0e\u8d6b\u52d2\u7684\u91d1\u7f8a\uff0c\u5176\u91d1\u8272\u7f8a\u6bdb\u540e\u6765\u6210\u4e3a\u963f\u5c14\u6208\u82f1\u96c4\u8fdc\u5f81\u5bfb\u627e\u7684\u91d1\u7f8a\u6bdb\u3002";
                    break;
                case "cancer":
                    summary = "\u5de8\u87f9\u5ea7\u4f4d\u4e8e\u53cc\u5b50\u5ea7\u4e0e\u72ee\u5b50\u5ea7\u4e4b\u95f4\uff0c\u672c\u8eab\u8f83\u6697\uff0c\u4f46\u5305\u542b\u8089\u773c\u53ef\u89c1\u7684\u8702\u5de2\u661f\u56e2M44\u4ee5\u53ca\u53e4\u8001\u7684\u758f\u6563\u661f\u56e2M67\u3002\u5929\u6c14\u6674\u6717\u4e14\u5149\u6c61\u67d3\u8f83\u4f4e\u65f6\uff0cM44\u4f1a\u5448\u73b0\u6726\u80e7\u5149\u6591\u3002";
                    mythology = "\u53e4\u5e0c\u814a\u795e\u8bdd\u4e2d\uff0c\u5de8\u87f9\u5728\u8d6b\u62c9\u514b\u52d2\u65af\u4e0e\u4e5d\u5934\u86c7\u6218\u6597\u65f6\u53d7\u8d6b\u62c9\u6d3e\u9063\u524d\u53bb\u963b\u6320\uff0c\u6218\u8d25\u540e\u88ab\u7f6e\u4e8e\u5929\u7a7a\u3002";
                    break;
                case "virgo":
                    summary = "\u5904\u5973\u5ea7\uff0c\u53c8\u53eb\u5ba4\u5973\u5ea7\uff0c\u662f\u5168\u5929\u9762\u79ef\u7b2c\u4e8c\u5927\u7684\u661f\u5ea7\uff0c\u4e5f\u662f\u6700\u5927\u7684\u9ec4\u9053\u661f\u5ea7\u3002\u89d2\u5bbf\u4e00\u660e\u4eae\u4e14\u63a5\u8fd1\u9ec4\u9053\uff1b\u661f\u5ea7\u5317\u90e8\u901a\u5411\u5904\u5973\u5ea7\u661f\u7cfb\u56e2\uff0c\u5176\u4e2d\u5305\u62ec\u5de8\u692d\u5706\u661f\u7cfbM87\u3002";
                    mythology = "\u5e38\u89c1\u5e0c\u814a\u4f20\u7edf\u5c06\u5b83\u4e0e\u519c\u4e1a\u5973\u795e\u5fb7\u58a8\u5fd2\u8033\u3001\u73c0\u8033\u585e\u798f\u6d85\uff0c\u6216\u624b\u6301\u6b63\u4e49\u5929\u79e4\u7684\u963f\u65af\u7279\u8d56\u4e9a\u8054\u7cfb\u8d77\u6765\u3002";
                    break;
                case "libra":
                    summary = "\u5929\u79e4\u5ea7\u4f4d\u4e8e\u5904\u5973\u5ea7\u4e0e\u5929\u874e\u5ea7\u4e4b\u95f4\uff0c\u662f\u9ec4\u9053\u5341\u4e8c\u661f\u5ea7\u4e2d\u552f\u4e00\u4ee5\u5668\u7269\u547d\u540d\u7684\u661f\u5ea7\u3002\u56db\u9897\u4e3b\u661f\u5f62\u6210\u8f83\u6697\u7684\u56db\u8fb9\u5f62\uff0c\u53e4\u4ee3\u4e00\u5ea6\u88ab\u89c6\u4e3a\u5929\u874e\u7684\u53cc\u87af\u3002";
                    mythology = "\u5b83\u901a\u5e38\u88ab\u89e3\u91ca\u4e3a\u6b63\u4e49\u5973\u795e\u963f\u65af\u7279\u8d56\u4e9a\u624b\u4e2d\u7684\u5929\u79e4\uff0c\u8c61\u5f81\u8861\u91cf\u3001\u516c\u6b63\u4e0e\u79cb\u5206\u65f6\u663c\u591c\u5e73\u8861\u3002";
                    break;
                case "scorpius":
                    summary = "\u5929\u874e\u5ea7\u662f\u590f\u5b63\u5357\u5929\u6700\u6613\u8fa8\u8ba4\u7684\u9ec4\u9053\u661f\u5ea7\u4e4b\u4e00\uff0c\u7ea2\u8272\u7684\u5fc3\u5bbf\u4e8c\u4f4d\u4e8e\u874e\u8eab\u4e2d\u592e\uff0c\u5f2f\u66f2\u661f\u94fe\u4e00\u76f4\u5ef6\u4f38\u81f3\u5c3e\u94a9\u3002\u9644\u8fd1\u7684M6\u4e0eM7\u90fd\u662f\u660e\u4eae\u758f\u6563\u661f\u56e2\u3002";
                    mythology = "\u53e4\u5e0c\u814a\u795e\u8bdd\u4e2d\uff0c\u5de8\u874e\u88ab\u6d3e\u53bb\u5236\u670d\u730e\u4eba\u4fc4\u91cc\u7fc1\uff0c\u56e0\u6b64\u5929\u874e\u5ea7\u4e0e\u730e\u6237\u5ea7\u5728\u5929\u7a7a\u4e2d\u5e38\u5448\u6b64\u5347\u5f7c\u843d\u3002";
                    break;
                case "leo":
                    summary = "\u72ee\u5b50\u5ea7\u662f\u6625\u5b63\u9192\u76ee\u7684\u9ec4\u9053\u661f\u5ea7\uff0c\u524d\u90e8\u7684\u9570\u5200\u5f62\u661f\u7fa4\u4ece\u8f69\u8f95\u5341\u56db\u5411\u4e0a\u5f2f\u66f2\uff0c\u540e\u90e8\u6784\u6210\u4e09\u89d2\u5f62\u3002\u661f\u5ea7\u9644\u8fd1\u53ef\u89c2\u6d4bM65\u3001M66\u4e0eNGC 3628\u7ec4\u6210\u7684\u72ee\u5b50\u5ea7\u4e09\u91cd\u661f\u7cfb\u3002";
                    mythology = "\u53e4\u5e0c\u814a\u795e\u8bdd\u4e2d\uff0c\u5b83\u8c61\u5f81\u5200\u67aa\u4e0d\u5165\u7684\u6d85\u58a8\u4e9a\u72ee\u5b50\uff0c\u540e\u6765\u88ab\u8d6b\u62c9\u514b\u52d2\u65af\u5b8c\u6210\u7b2c\u4e00\u9879\u4f1f\u4e1a\u65f6\u51fb\u8d25\u3002";
                    break;
                case "pegasus":
                    summary = "\u98de\u9a6c\u5ea7\u4ee5\u201c\u79cb\u5b63\u56db\u8fb9\u5f62\u201d\u8457\u79f0\uff0c\u662f\u5317\u534a\u7403\u79cb\u5b63\u5bfb\u627e\u591a\u4e2a\u661f\u5ea7\u7684\u91cd\u8981\u8d77\u70b9\u3002";
                    mythology = "\u53e4\u5e0c\u814a\u795e\u8bdd\u4e2d\u4ece\u7f8e\u675c\u838e\u8840\u6db2\u4e2d\u8bde\u751f\u7684\u6709\u7ffc\u795e\u9a6c\u4f69\u52a0\u7d22\u65af\u3002";
                    break;
                case "taurus":
                    summary = "\u91d1\u725b\u5ea7\u662f\u9192\u76ee\u7684\u51ac\u5b63\u9ec4\u9053\u661f\u5ea7\uff0c\u6bd5\u5bbf\u4e94\u4e0e\u6bd5\u661f\u56e2\u5f62\u6210\u725b\u8138\u7684V\u5f62\u8f6e\u5ed3\uff0c\u9644\u8fd1\u7684\u6634\u661f\u56e2\u8089\u773c\u4e5f\u5341\u5206\u663e\u773c\u3002\u5929\u5173\u9644\u8fd1\u8fd8\u6709\u8d85\u65b0\u661f\u9057\u8ff9\u87f9\u72b6\u661f\u4e91M1\u3002";
                    mythology = "\u53e4\u5e0c\u814a\u795e\u8bdd\u4e2d\uff0c\u5b99\u65af\u5316\u4f5c\u767d\u725b\u63a5\u8fd1\u6b27\u7f57\u5df4\u516c\u4e3b\uff0c\u5e76\u5c06\u5979\u5e26\u5f80\u514b\u91cc\u7279\u5c9b\u3002";
                    break;
                case "gemini":
                    summary = "\u53cc\u5b50\u5ea7\u7531\u5317\u6cb3\u4e8c\u548c\u5317\u6cb3\u4e09\u6807\u51fa\u53cc\u80de\u80ce\u7684\u5934\u90e8\uff0c\u5411\u5357\u5ef6\u4f38\u51fa\u4e24\u5217\u661f\u94fe\uff0c\u662f\u5317\u534a\u7403\u51ac\u5b63\u7684\u91cd\u8981\u9ec4\u9053\u661f\u5ea7\u3002\u5176\u533a\u57df\u5185\u7684M35\u662f\u9002\u5408\u53cc\u7b52\u671b\u8fdc\u955c\u89c2\u6d4b\u7684\u758f\u6563\u661f\u56e2\u3002";
                    mythology = "\u53e4\u5e0c\u814a\u795e\u8bdd\u4e2d\uff0c\u5b83\u4ee3\u8868\u611f\u60c5\u6df1\u539a\u7684\u5361\u65af\u6258\u5c14\u4e0e\u6ce2\u5415\u514b\u65af\uff1b\u4e00\u4eba\u51e1\u4fd7\u3001\u4e00\u4eba\u4e0d\u673d\uff0c\u6700\u7ec8\u5171\u540c\u5347\u4e0a\u5929\u7a7a\u3002";
                    break;
                case "sagittarius":
                    summary = "\u4eba\u9a6c\u5ea7\u4f4d\u4e8e\u94f6\u6cb3\u6700\u6d53\u5bc6\u7684\u65b9\u5411\uff0c\u4e3b\u661f\u7ec4\u6210\u8457\u540d\u7684\u8336\u58f6\u5f62\u661f\u7fa4\uff0c\u58f6\u5634\u671d\u5411\u94f6\u6cb3\u7cfb\u4e2d\u5fc3\u3002\u533a\u57df\u5185\u805a\u96c6\u4e86\u7901\u6e56\u661f\u4e91M8\u3001\u4e09\u53f6\u661f\u4e91M20\u4e0e\u7403\u72b6\u661f\u56e2M22\u3002";
                    mythology = "\u897f\u65b9\u4f20\u7edf\u628a\u5b83\u63cf\u7ed8\u4e3a\u6301\u5f13\u7684\u4eba\u9a6c\u5c04\u624b\uff1b\u90e8\u5206\u5e0c\u814a\u4f20\u8bf4\u8ba4\u4e3a\u5b83\u5bf9\u5e94\u64c5\u957f\u5c04\u7bad\u5e76\u53d1\u660e\u5f13\u672f\u7684\u514b\u6d1b\u6258\u65af\u3002";
                    break;
                case "capricornus":
                    summary = "\u6469\u7faf\u5ea7\u662f\u8f83\u6697\u7684\u9ec4\u9053\u661f\u5ea7\uff0c\u51e0\u9897\u4e3b\u661f\u7ec4\u6210\u5bbd\u9614\u7684\u4e09\u89d2\u5f62\uff0c\u5317\u534a\u7403\u590f\u672b\u81f3\u79cb\u5b63\u8f83\u9002\u5408\u89c2\u6d4b\u3002\u7403\u72b6\u661f\u56e2M30\u4f4d\u4e8e\u5176\u4e1c\u90e8\uff0c\u53ef\u7528\u5c0f\u578b\u671b\u8fdc\u955c\u5bfb\u627e\u3002";
                    mythology = "\u53e4\u5e0c\u814a\u4f20\u7edf\u5c06\u5b83\u63cf\u7ed8\u4e3a\u534a\u7f8a\u534a\u9c7c\u7684\u6d77\u7f8a\uff0c\u5e38\u4e0e\u7267\u795e\u6f58\u4e3a\u8eb2\u907f\u602a\u7269\u800c\u8dc3\u5165\u6c34\u4e2d\u53d8\u5f62\u7684\u6545\u4e8b\u8054\u7cfb\u3002";
                    break;
                case "aquarius":
                    summary = "\u5b9d\u74f6\u5ea7\u9762\u79ef\u5f88\u5927\u4f46\u6574\u4f53\u8f83\u6697\uff0c\u79cb\u5b63\u591c\u7a7a\u4e2d\u53ef\u7531\u5b9d\u74f6\u53e3\u4e0e\u5411\u5357\u6d41\u6dcc\u7684\u661f\u94fe\u8fa8\u8ba4\u3002\u661f\u5ea7\u5185\u6709\u7403\u72b6\u661f\u56e2M2\u548c\u8457\u540d\u7684\u87ba\u65cb\u661f\u4e91NGC 7293\u3002";
                    mythology = "\u5b83\u5e38\u88ab\u89e3\u91ca\u4e3a\u4e3a\u4f17\u795e\u659f\u9152\u7684\u5c11\u5e74\u4f3d\u502a\u58a8\u5f97\u65af\uff0c\u4e5f\u4e0e\u4e22\u5361\u5229\u7fc1\u6d2a\u6c34\u7b49\u53e4\u4ee3\u6d2a\u6c34\u4f20\u8bf4\u76f8\u8054\u7cfb\u3002";
                    break;
                case "pisces":
                    summary = "\u53cc\u9c7c\u5ea7\u7531\u4e24\u6761\u8f83\u6697\u661f\u94fe\u6784\u6210\uff0c\u4e24\u6761\u7ef3\u7d22\u5728\u5916\u5c4f\u4e03\u76f8\u4ea4\uff0c\u5317\u534a\u7403\u79cb\u5b63\u53ef\u89c1\u3002\u5176\u533a\u57df\u4e2d\u7684M74\u662f\u4e00\u5ea7\u6b63\u9762\u671d\u5411\u5730\u7403\u7684\u87ba\u65cb\u661f\u7cfb\u3002";
                    mythology = "\u53e4\u5e0c\u814a\u795e\u8bdd\u4e2d\uff0c\u963f\u4f5b\u6d1b\u72c4\u5fd2\u4e0e\u5384\u6d1b\u65af\u4e3a\u8eb2\u907f\u602a\u7269\u5316\u4f5c\u4e24\u6761\u9c7c\uff0c\u5e76\u7528\u7ef3\u7d22\u76f8\u8fde\u4ee5\u514d\u5f7c\u6b64\u5931\u6563\u3002";
                    break;
                case "aquila":
                    summary = "\u5929\u9e70\u5ea7\u6cbf\u94f6\u6cb3\u5206\u5e03\uff0c\u4e3b\u661f\u725b\u90ce\u661f\u662f\u590f\u5b63\u5927\u4e09\u89d2\u7684\u4e00\u89d2\uff0c\u4e24\u4fa7\u8f85\u661f\u4f7f\u5176\u5f88\u6613\u8fa8\u8ba4\u3002";
                    mythology = "\u53e4\u5e0c\u814a\u795e\u8bdd\u4e2d\u5b99\u65af\u7684\u795e\u9e70\uff1b\u4e2d\u56fd\u6587\u5316\u4e2d\u725b\u90ce\u661f\u5219\u5c5e\u4e8e\u725b\u90ce\u7ec7\u5973\u4f20\u8bf4\u3002";
                    break;
                case "canis-major":
                    summary = "\u5927\u72ac\u5ea7\u4f4d\u4e8e\u730e\u6237\u5ea7\u4e1c\u5357\u65b9\uff0c\u62e5\u6709\u5168\u5929\u6700\u4eae\u6052\u661f\u5929\u72fc\u661f\uff0c\u662f\u51ac\u5b63\u661f\u7a7a\u7684\u663e\u8457\u6807\u5fd7\u3002";
                    mythology = "\u53e4\u5e0c\u814a\u795e\u8bdd\u4e2d\u730e\u4eba\u4fc4\u91cc\u7fc1\u7684\u730e\u72ac\uff0c\u4e0e\u730e\u6237\u5ea7\u4e00\u540c\u8ffd\u9010\u5929\u5154\u3002";
                    break;
                default:
                    summary = "\u8be5\u661f\u5ea7\u7531\u591a\u9897\u89c6\u7ebf\u65b9\u5411\u76f8\u8fd1\u7684\u6052\u661f\u6784\u6210\uff0c\u5176\u5b9e\u9645\u7a7a\u95f4\u8ddd\u79bb\u53ef\u80fd\u5dee\u5f02\u5f88\u5927\u3002";
                    mythology = "\u4e0d\u540c\u6587\u5316\u5bf9\u8fd9\u7247\u661f\u7a7a\u6709\u4e0d\u540c\u7684\u547d\u540d\u4e0e\u4f20\u8bf4\u3002";
                    break;
            }

            mythology = GetConstellationCultureCopy(key, mythology);
        }

        private static string GetConstellationCultureCopy(string key, string fallback)
        {
            switch (key)
            {
                case "big-dipper":
                    return "\u5728\u4e2d\u56fd\u4f20\u7edf\u4e2d\uff0c\u5317\u6597\u4e03\u661f\u5c5e\u4e8e\u7d2b\u5fae\u57a3\uff0c\u88ab\u7528\u6765\u8fa8\u8ba4\u5317\u65b9\u3001\u5224\u65ad\u65f6\u8fb0\u548c\u5b63\u8282\uff0c\u5e76\u884d\u751f\u51fa\u6597\u6bcd\u4e0e\u5317\u6597\u4fe1\u4ef0\u3002\u53e4\u4eba\u4ee5\u6597\u67c4\u6307\u5411\u6982\u62ec\u56db\u5b63\u53d8\u5316\uff1b\u5728\u5e0c\u814a\u4f20\u7edf\u4e2d\u5b83\u5c5e\u4e8e\u5927\u718a\u5ea7\uff0c\u8bb8\u591a\u6b27\u4e9a\u4e0e\u5317\u7f8e\u6587\u5316\u53c8\u628a\u8fd9\u7ec4\u661f\u770b\u6210\u718a\u3001\u8f66\u3001\u7281\u6216\u957f\u67c4\u52fa\u3002";
                case "orion":
                    return "\u5e0c\u814a\u795e\u8bdd\u4e2d\u7684\u730e\u4eba\u4fc4\u91cc\u7fc1\u4ee5\u52c7\u6b66\u548c\u81ea\u8d1f\u8457\u79f0\uff0c\u5e38\u4e0e\u5929\u874e\u7684\u6545\u4e8b\u76f8\u8fde\uff0c\u56e0\u6b64\u730e\u6237\u5ea7\u4e0e\u5929\u874e\u5ea7\u5728\u591c\u7a7a\u4e2d\u6b64\u5347\u5f7c\u843d\u3002\u5728\u4e2d\u56fd\u4f20\u7edf\u4e2d\uff0c\u5176\u4e3b\u4f53\u5c5e\u4e8e\u53c2\u5bbf\uff0c\u53c2\u5bbf\u4e0e\u5fc3\u5bbf\u7684\u51fa\u6ca1\u5173\u7cfb\u5f62\u6210\u201c\u53c2\u5546\u4e0d\u76f8\u89c1\u201d\u7684\u6587\u5316\u610f\u8c61\uff1b\u4e16\u754c\u591a\u5730\u4e5f\u628a\u4e09\u9897\u8170\u5e26\u661f\u89c6\u4e3a\u730e\u4eba\u3001\u6218\u58eb\u6216\u4e09\u4f4d\u4eba\u7269\u3002";
                case "cassiopeia":
                    return "\u5e0c\u814a\u795e\u8bdd\u4e2d\uff0c\u738b\u540e\u5361\u897f\u5965\u4f69\u5a05\u56e0\u5938\u8000\u7f8e\u8c8c\u89e6\u6012\u6d77\u795e\uff0c\u88ab\u7f5a\u5750\u5728\u5b9d\u5ea7\u4e0a\u7ed5\u5317\u5929\u65cb\u8f6c\uff0c\u6545\u4e8b\u4e0e\u4ed9\u5973\u5ea7\u3001\u82f1\u4ed9\u5ea7\u3001\u4ed9\u738b\u5ea7\u548c\u9cb8\u9c7c\u5ea7\u76f8\u8fde\u3002\u5728\u4e2d\u56fd\u661f\u5b98\u4e2d\uff0c\u8fd9\u7247\u5929\u533a\u5305\u542b\u738b\u826f\u3001\u9601\u9053\u7b49\u661f\u5b98\uff0c\u8c61\u5f81\u5fa1\u8005\u4e0e\u5bab\u5ef7\u9053\u8def\uff1b\u5176\u9192\u76ee\u7684W\u5f62\u4e5f\u88ab\u4e0d\u540c\u6587\u5316\u60f3\u8c61\u4e3a\u738b\u5ea7\u3001\u624b\u638c\u3001\u9e7f\u89d2\u6216\u9a6f\u9e7f\u3002";
                case "cygnus":
                    return "\u5e0c\u814a\u4f20\u7edf\u5e38\u628a\u5929\u9e45\u5ea7\u4e0e\u5b99\u65af\u5316\u8eab\u3001\u4fc4\u8033\u752b\u65af\u6216\u5fe0\u8bda\u53cb\u4eba\u7684\u6545\u4e8b\u8054\u7cfb\u8d77\u6765\uff0c\u4eae\u661f\u7ec4\u6210\u7684\u5317\u5341\u5b57\u5728\u6b27\u6d32\u6587\u5316\u4e2d\u4e5f\u5177\u6709\u5b97\u6559\u8c61\u5f81\u3002\u5728\u4e2d\u56fd\u4f20\u7edf\u4e2d\uff0c\u5929\u6d25\u661f\u5b98\u6a2a\u8de8\u94f6\u6cb3\uff0c\u8c61\u5f81\u94f6\u6cb3\u4e0a\u7684\u6e21\u53e3\u4e0e\u6865\u6881\uff1b\u5929\u6d25\u56db\u53c8\u4e0e\u7ec7\u5973\u661f\u3001\u725b\u90ce\u661f\u5171\u540c\u6784\u6210\u590f\u5b63\u5927\u4e09\u89d2\u3002";
                case "lyra":
                    return "\u5e0c\u814a\u795e\u8bdd\u4e2d\uff0c\u5929\u7434\u5ea7\u4ee3\u8868\u4fc4\u8033\u752b\u65af\u7684\u4e03\u5f26\u7434\uff0c\u4ed6\u7684\u97f3\u4e50\u80fd\u611f\u52a8\u4eba\u795e\uff0c\u6b7b\u540e\u7434\u88ab\u5347\u4e0a\u5929\u7a7a\u3002\u5728\u4e2d\u56fd\u6587\u5316\u4e2d\uff0c\u7ec7\u5973\u661f\u4ee3\u8868\u7ec7\u5973\uff0c\u4e0e\u725b\u90ce\u661f\u9694\u94f6\u6cb3\u76f8\u671b\uff0c\u5f62\u6210\u4e03\u5915\u3001\u9e4a\u6865\u548c\u4e5e\u5de7\u7b49\u957f\u671f\u6d41\u4f20\u7684\u6c11\u4fd7\uff1b\u5728\u90e8\u5206\u592a\u5e73\u6d0b\u6587\u5316\u4e2d\uff0c\u7ec7\u5973\u661f\u8fd8\u66fe\u7528\u4e8e\u6807\u8bb0\u65b0\u5e74\u4e0e\u519c\u65f6\u3002";
                case "scorpius":
                    return "\u5e0c\u814a\u795e\u8bdd\u4e2d\uff0c\u5de8\u874e\u5949\u547d\u5236\u670d\u81ea\u8d1f\u7684\u730e\u4eba\u4fc4\u91cc\u7fc1\uff0c\u4e24\u8005\u56e0\u6b64\u88ab\u5b89\u6392\u5728\u5929\u7a7a\u4e24\u7aef\u3002\u4e2d\u56fd\u4f20\u7edf\u628a\u8fd9\u7247\u661f\u533a\u5206\u4e3a\u623f\u5bbf\u3001\u5fc3\u5bbf\u548c\u5c3e\u5bbf\uff0c\u5fc3\u5bbf\u4e8c\u88ab\u89c6\u4f5c\u201c\u5927\u706b\u201d\uff0c\u53e4\u4eba\u4ee5\u5b83\u7684\u660f\u89c1\u548c\u897f\u6d41\u5224\u65ad\u5b63\u8282\u4e0e\u519c\u65f6\uff1b\u201c\u4e03\u6708\u6d41\u706b\u201d\u6240\u6307\u6b63\u662f\u8fd9\u9897\u7ea2\u8272\u4eae\u661f\u7684\u4f4d\u7f6e\u53d8\u5316\u3002";
                case "leo":
                    return "\u5e0c\u814a\u795e\u8bdd\u4e2d\uff0c\u72ee\u5b50\u5ea7\u8c61\u5f81\u5200\u67aa\u4e0d\u5165\u7684\u6d85\u58a8\u4e9a\u72ee\u5b50\uff0c\u8d6b\u62c9\u514b\u52d2\u65af\u5c06\u5176\u5236\u670d\u5e76\u62ab\u4e0a\u72ee\u76ae\uff0c\u5b8c\u6210\u5341\u4e8c\u9879\u4f1f\u4e1a\u7684\u7b2c\u4e00\u9879\u3002\u5728\u4e2d\u56fd\u4f20\u7edf\u4e2d\uff0c\u8fd9\u7247\u5929\u533a\u5206\u5c5e\u8f69\u8f95\u3001\u592a\u5fae\u57a3\u7b49\u661f\u5b98\uff0c\u8f69\u8f95\u5341\u56db\u66fe\u88ab\u79f0\u4e3a\u5e1d\u738b\u4e4b\u661f\uff1b\u53e4\u5df4\u6bd4\u4f26\u3001\u6ce2\u65af\u7b49\u6587\u5316\u540c\u6837\u5f88\u65e9\u5c31\u628a\u8fd9\u91cc\u89c6\u4e3a\u72ee\u5b50\u3002";
                case "pegasus":
                    return "\u98de\u9a6c\u4f69\u52a0\u7d22\u65af\u4ece\u7f8e\u675c\u838e\u7684\u8840\u4e2d\u8bde\u751f\uff0c\u540e\u6765\u5e2e\u52a9\u82f1\u96c4\u67cf\u52d2\u6d1b\u4e30\u51fb\u8d25\u5947\u7f8e\u62c9\uff0c\u6700\u7ec8\u6210\u4e3a\u5b99\u65af\u8fd0\u9001\u96f7\u7535\u7684\u795e\u517d\u3002\u79cb\u5b63\u56db\u8fb9\u5f62\u4e2d\u7684\u58c1\u5bbf\u4e8c\u5b9e\u9645\u4e0a\u5c5e\u4e8e\u4ed9\u5973\u5ea7\uff0c\u4f53\u73b0\u53e4\u4ee3\u661f\u56fe\u4e0e\u73b0\u4ee3\u661f\u5ea7\u8fb9\u754c\u7684\u5dee\u5f02\uff1b\u4e2d\u56fd\u4f20\u7edf\u5c06\u6b64\u533a\u57df\u5206\u5165\u5ba4\u5bbf\u3001\u58c1\u5bbf\u7b49\u661f\u5b98\uff0c\u4e0e\u5bab\u5ba4\u3001\u57ce\u5899\u548c\u8425\u9020\u8c61\u5f81\u76f8\u5173\u3002";
                case "taurus":
                    return "\u5e0c\u814a\u795e\u8bdd\u4e2d\uff0c\u5b99\u65af\u5316\u4f5c\u767d\u725b\u5e26\u8d70\u6b27\u7f57\u5df4\u516c\u4e3b\uff1b\u66f4\u65e9\u7684\u7f8e\u7d22\u4e0d\u8fbe\u7c73\u4e9a\u6587\u5316\u4e5f\u628a\u8fd9\u91cc\u89c6\u4f5c\u5929\u725b\u3002\u5728\u4e2d\u56fd\u4f20\u7edf\u4e2d\uff0c\u6bd5\u5bbf\u4e0e\u6634\u5bbf\u540c\u5c5e\u897f\u65b9\u767d\u864e\uff0c\u6634\u661f\u56e2\u5e38\u7528\u4e8e\u8282\u4ee4\u548c\u519c\u65f6\u5224\u65ad\uff1b\u5929\u5173\u9644\u8fd11054\u5e74\u51fa\u73b0\u7684\u5ba2\u661f\uff0c\u540e\u6765\u5f62\u6210\u4eca\u5929\u7684\u87f9\u72b6\u661f\u4e91\uff0c\u5e76\u88ab\u4e2d\u56fd\u53f2\u7c4d\u8be6\u7ec6\u8bb0\u5f55\u3002";
                case "gemini":
                    return "\u5e0c\u814a\u795e\u8bdd\u4e2d\uff0c\u5361\u65af\u6258\u5c14\u4e0e\u6ce2\u5415\u514b\u65af\u4e00\u51e1\u4e00\u795e\uff0c\u5144\u5f1f\u60c5\u8c0a\u4f7f\u4ed6\u4eec\u5171\u4eab\u6c38\u751f\uff0c\u6210\u4e3a\u822a\u6d77\u8005\u7684\u5b88\u62a4\u8c61\u5f81\u3002\u5728\u4e2d\u56fd\u4f20\u7edf\u4e2d\uff0c\u4e24\u9897\u4eae\u661f\u5206\u522b\u5c5e\u4e8e\u5317\u6cb3\u661f\u5b98\uff0c\u5468\u56f4\u8fd8\u5206\u5e03\u4e95\u5bbf\u3001\u5929\u6a3d\u7b49\u661f\u5b98\uff1b\u53cc\u5b50\u610f\u8c61\u5728\u5df4\u6bd4\u4f26\u4f20\u7edf\u4e2d\u4e5f\u4e0e\u6210\u5bf9\u7684\u795e\u660e\u76f8\u5173\u3002";
                case "aquila":
                    return "\u5e0c\u814a\u795e\u8bdd\u4e2d\uff0c\u5929\u9e70\u5e38\u88ab\u89c6\u4f5c\u5b99\u65af\u7684\u795e\u9e1f\uff0c\u8d1f\u8d23\u643a\u5e26\u96f7\u9706\u6216\u628a\u4f3d\u502a\u58a8\u5f97\u65af\u5e26\u4e0a\u5965\u6797\u5339\u65af\u5c71\u3002\u5728\u4e2d\u56fd\u6587\u5316\u4e2d\uff0c\u6cb3\u9f13\u4e8c\u66f4\u5e7f\u4e3a\u4eba\u77e5\u7684\u540d\u5b57\u662f\u725b\u90ce\u661f\uff0c\u5b83\u4e0e\u7ec7\u5973\u661f\u3001\u94f6\u6cb3\u548c\u4e03\u5915\u4f20\u8bf4\u7d27\u5bc6\u76f8\u8fde\uff0c\u4e24\u65c1\u7684\u6cb3\u9f13\u4e00\u3001\u6cb3\u9f13\u4e09\u5e38\u88ab\u89e3\u91ca\u4e3a\u725b\u90ce\u7684\u5b69\u5b50\u3002";
                case "canis-major":
                    return "\u5e0c\u814a\u4f20\u7edf\u628a\u5927\u72ac\u5ea7\u89c6\u4e3a\u730e\u4eba\u4fc4\u91cc\u7fc1\u7684\u730e\u72ac\uff0c\u4e5f\u6709\u4eba\u5c06\u5b83\u4e0e\u901f\u5ea6\u65e0\u4eba\u80fd\u53ca\u7684\u795e\u72ac\u83b1\u62c9\u666e\u8054\u7cfb\u8d77\u6765\u3002\u5929\u72fc\u661f\u7684\u5055\u65e5\u5347\u5728\u53e4\u57c3\u53ca\u66fe\u9884\u793a\u5c3c\u7f57\u6cb3\u6cdb\u6ee5\u548c\u65b0\u5e74\u5230\u6765\uff1b\u5728\u4e2d\u56fd\u4f20\u7edf\u4e2d\uff0c\u5929\u72fc\u8c61\u5f81\u8fb9\u7586\u5a01\u80c1\uff0c\u9644\u8fd1\u7684\u5f27\u77e2\u661f\u5b98\u5219\u50cf\u4e00\u5f20\u6307\u5411\u5929\u72fc\u7684\u5f13\u3002";
                case "aries":
                    return "\u5e0c\u814a\u795e\u8bdd\u4e2d\uff0c\u91d1\u7f8a\u6551\u8d70\u4f5b\u91cc\u514b\u7d22\u65af\u4e0e\u8d6b\u52d2\uff0c\u5176\u91d1\u8272\u7f8a\u6bdb\u540e\u6765\u6210\u4e3a\u963f\u5c14\u6208\u82f1\u96c4\u8fdc\u5f81\u7684\u76ee\u6807\u3002\u767d\u7f8a\u5ea7\u66fe\u9760\u8fd1\u53e4\u4ee3\u6625\u5206\u70b9\uff0c\u56e0\u6b64\u5728\u897f\u65b9\u5360\u661f\u4e0e\u5386\u6cd5\u6587\u5316\u4e2d\u5177\u6709\u201c\u9ec4\u9053\u8d77\u70b9\u201d\u7684\u8c61\u5f81\uff1b\u5728\u4e2d\u56fd\u4f20\u7edf\u4e2d\uff0c\u8fd9\u7247\u5929\u533a\u4e3b\u8981\u5bf9\u5e94\u5a04\u5bbf\u3001\u80c3\u5bbf\u7b49\u661f\u5b98\uff0c\u4e0e\u7267\u517b\u3001\u4ed3\u5eea\u548c\u796d\u7940\u76f8\u5173\u3002";
                case "cancer":
                    return "\u5e0c\u814a\u795e\u8bdd\u4e2d\uff0c\u8d6b\u62c9\u6d3e\u5de8\u87f9\u534f\u52a9\u4e5d\u5934\u86c7\u963b\u6320\u8d6b\u62c9\u514b\u52d2\u65af\uff0c\u5de8\u87f9\u867d\u88ab\u8e29\u6b7b\u4ecd\u88ab\u5347\u4e0a\u5929\u7a7a\u3002\u5728\u4e2d\u56fd\u4f20\u7edf\u4e2d\uff0c\u5de8\u87f9\u5ea7\u533a\u57df\u5305\u542b\u9b3c\u5bbf\uff0c\u5176\u4e2d\u8702\u5de2\u661f\u56e2M44\u88ab\u79f0\u4e3a\u79ef\u5c38\u6c14\uff0c\u53cd\u6620\u53e4\u4eba\u5bf9\u6726\u80e7\u661f\u56e2\u7684\u72ec\u7279\u60f3\u8c61\uff1b\u53e4\u4ee3\u4e24\u6cb3\u4e0e\u57c3\u53ca\u6587\u5316\u4e5f\u5e38\u4ee5\u7532\u58f3\u52a8\u7269\u8868\u793a\u592a\u9633\u8f6c\u5411\u3002";
                case "virgo":
                    return "\u5904\u5973\u5ea7\u5728\u5e0c\u814a\u7f57\u9a6c\u4f20\u7edf\u4e2d\u53ef\u5bf9\u5e94\u519c\u4e1a\u5973\u795e\u5fb7\u58a8\u5fd2\u8033\u3001\u73c0\u8033\u585e\u798f\u6d85\uff0c\u6216\u624b\u6301\u6b63\u4e49\u5929\u79e4\u7684\u963f\u65af\u7279\u8d56\u4e9a\uff0c\u56e0\u6b64\u517c\u6709\u4e30\u6536\u4e0e\u6b63\u4e49\u7684\u8c61\u5f81\u3002\u5728\u4e2d\u56fd\u4f20\u7edf\u4e2d\uff0c\u8fd9\u7247\u5e7f\u9614\u5929\u533a\u8de8\u8d8a\u89d2\u5bbf\u3001\u4ea2\u5bbf\u53ca\u592a\u5fae\u57a3\uff0c\u89d2\u5bbf\u4e00\u88ab\u89c6\u4e3a\u4e1c\u65b9\u82cd\u9f99\u4e4b\u89d2\uff1b\u5b83\u4e5f\u957f\u671f\u7528\u4e8e\u6625\u5b63\u8282\u4ee4\u5224\u65ad\u3002";
                case "libra":
                    return "\u5929\u79e4\u5ea7\u65e9\u671f\u66fe\u88ab\u89c6\u4e3a\u5929\u874e\u7684\u53cc\u87af\uff0c\u540e\u6765\u5728\u7f57\u9a6c\u65f6\u4ee3\u6210\u4e3a\u72ec\u7acb\u7684\u5929\u79e4\uff0c\u8c61\u5f81\u6b63\u4e49\u3001\u79e9\u5e8f\u4ee5\u53ca\u663c\u591c\u5e73\u8861\u3002\u5b83\u5e38\u4e0e\u5904\u5973\u5ea7\u6240\u4ee3\u8868\u7684\u6b63\u4e49\u5973\u795e\u76f8\u914d\uff1b\u5728\u4e2d\u56fd\u4f20\u7edf\u4e2d\uff0c\u8fd9\u7247\u533a\u57df\u5206\u5c5e\u6c10\u5bbf\u3001\u4ea2\u5bbf\u7b49\u661f\u5b98\uff0c\u5305\u542b\u5929\u8f90\u3001\u9635\u8f66\u7b49\u4e0e\u8f66\u9a6c\u548c\u519b\u9635\u6709\u5173\u7684\u610f\u8c61\u3002";
                case "sagittarius":
                    return "\u897f\u65b9\u4f20\u7edf\u5c06\u4eba\u9a6c\u5ea7\u63cf\u7ed8\u4e3a\u6301\u5f13\u5c04\u624b\uff0c\u90e8\u5206\u6545\u4e8b\u628a\u5b83\u4e0e\u53d1\u660e\u5f13\u672f\u7684\u514b\u6d1b\u6258\u65af\u8054\u7cfb\uff0c\u800c\u4e0d\u4e00\u5b9a\u662f\u8457\u540d\u7684\u5580\u620e\u3002\u4e2d\u56fd\u4f20\u7edf\u4e2d\uff0c\u5176\u201c\u8336\u58f6\u201d\u4e3b\u661f\u5927\u591a\u5c5e\u4e8e\u5357\u6597\u516d\u661f\uff0c\u5373\u4e8c\u5341\u516b\u5bbf\u4e2d\u7684\u6597\u5bbf\uff0c\u6c11\u95f4\u6709\u201c\u5357\u6597\u6ce8\u751f\u3001\u5317\u6597\u6ce8\u6b7b\u201d\u7684\u8bf4\u6cd5\uff1b\u94f6\u6cb3\u4e2d\u5fc3\u65b9\u5411\u4e5f\u8ba9\u8fd9\u91cc\u5728\u591a\u79cd\u6587\u5316\u4e2d\u6210\u4e3a\u5929\u6cb3\u4e0e\u795e\u57df\u7684\u5bc6\u96c6\u533a\u57df\u3002";
                case "capricornus":
                    return "\u6469\u7faf\u5ea7\u7684\u5f62\u8c61\u662f\u534a\u7f8a\u534a\u9c7c\u7684\u6d77\u7f8a\uff0c\u6e90\u81ea\u53e4\u5df4\u6bd4\u4f26\u7684\u795e\u6027\u751f\u7269\uff0c\u5e0c\u814a\u6545\u4e8b\u53c8\u5c06\u5b83\u4e0e\u7267\u795e\u6f58\u8eb2\u907f\u602a\u7269\u65f6\u7684\u53d8\u5f62\u8054\u7cfb\u8d77\u6765\u3002\u51ac\u81f3\u70b9\u66fe\u4f4d\u4e8e\u6b64\u661f\u5ea7\uff0c\u56e0\u5c81\u5dee\u73b0\u5df2\u79fb\u52a8\uff0c\u4f46\u201c\u5357\u56de\u5f52\u7ebf\u201d\u7684\u897f\u6587\u540d\u79f0\u4ecd\u4fdd\u7559\u6469\u7faf\u5370\u8bb0\uff1b\u4e2d\u56fd\u4f20\u7edf\u4e2d\u8fd9\u91cc\u5305\u62ec\u725b\u5bbf\u3001\u5792\u58c1\u9635\u7b49\u661f\u5b98\u3002";
                case "aquarius":
                    return "\u5b9d\u74f6\u5ea7\u5728\u7f8e\u7d22\u4e0d\u8fbe\u7c73\u4e9a\u4f20\u7edf\u4e2d\u4e0e\u638c\u7ba1\u6c34\u6e90\u7684\u795e\u6709\u5173\uff0c\u5e0c\u814a\u795e\u8bdd\u5219\u5e38\u628a\u6301\u74f6\u8005\u89e3\u91ca\u4e3a\u4e3a\u4f17\u795e\u659f\u9152\u7684\u4f3d\u502a\u58a8\u5f97\u65af\uff0c\u4e5f\u8054\u7cfb\u6d2a\u6c34\u6545\u4e8b\u3002\u5b83\u4f4d\u4e8e\u53e4\u4eba\u6240\u8c13\u201c\u5929\u6d77\u201d\u533a\u57df\uff0c\u5468\u56f4\u805a\u96c6\u53cc\u9c7c\u3001\u9cb8\u9c7c\u7b49\u6c34\u8c61\u661f\u5ea7\uff1b\u4e2d\u56fd\u4f20\u7edf\u5c06\u8fd9\u91cc\u5206\u4e3a\u5973\u5bbf\u3001\u865a\u5bbf\u3001\u5371\u5bbf\u7b49\uff0c\u5e76\u5305\u542b\u575f\u5893\u3001\u7fbd\u6797\u519b\u548c\u6c34\u5229\u76f8\u5173\u661f\u5b98\u3002";
                case "pisces":
                    return "\u5e0c\u814a\u795e\u8bdd\u4e2d\uff0c\u963f\u4f5b\u6d1b\u72c4\u5fd2\u4e0e\u5384\u6d1b\u65af\u4e3a\u8eb2\u907f\u602a\u7269\u5316\u4f5c\u4e24\u6761\u9c7c\uff0c\u5e76\u4ee5\u7ef3\u76f8\u8fde\uff1b\u66f4\u65e9\u7684\u897f\u4e9a\u4f20\u7edf\u4e5f\u6709\u4e24\u9c7c\u76f8\u7cfb\u7684\u56fe\u50cf\u3002\u6625\u5206\u70b9\u76ee\u524d\u4f4d\u4e8e\u53cc\u9c7c\u5ea7\u533a\u57df\uff0c\u4f7f\u5b83\u5728\u5386\u6cd5\u548c\u201c\u5c81\u5dee\u65f6\u4ee3\u201d\u8ba8\u8bba\u4e2d\u5e38\u88ab\u63d0\u53ca\uff1b\u5728\u4e2d\u56fd\u4f20\u7edf\u4e2d\uff0c\u8fd9\u7247\u5929\u533a\u6d89\u53ca\u594e\u5bbf\u3001\u58c1\u5bbf\uff0c\u661f\u5b98\u5916\u5c4f\u8c61\u5f81\u5bab\u5ba4\u5916\u56f4\u7684\u5c4f\u969c\u3002";
                default:
                    return fallback;
            }
        }

        private static Collider FindExistingBodyCollider(GameObject root)
        {
            if (root == null)
            {
                return null;
            }

            Collider rootCollider = SelectBestBodyCollider(root.GetComponents<Collider>());
            if (rootCollider != null)
            {
                return rootCollider;
            }

            return SelectBestBodyCollider(root.GetComponentsInChildren<Collider>(true));
        }

        private static Collider SelectBestBodyCollider(Collider[] colliders)
        {
            if (colliders == null)
            {
                return null;
            }

            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] is SphereCollider && IsExistingBodyCollider(colliders[i]))
                {
                    return colliders[i];
                }
            }

            for (int i = 0; i < colliders.Length; i++)
            {
                if (IsExistingBodyCollider(colliders[i]))
                {
                    return colliders[i];
                }
            }

            return null;
        }

        private static bool IsExistingBodyCollider(Collider collider)
        {
            if (collider == null)
            {
                return false;
            }

            if (collider.transform.name == PlanetRayTargetName)
            {
                return false;
            }

            if (collider.GetComponentInParent<ExhibitionPlanetarySystem>() != null)
            {
                return false;
            }

            return true;
        }

        private static void CalculateMainBodyLocalBounds(GameObject root, out Bounds bounds)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            bool initialized = false;
            bounds = new Bounds(Vector3.zero, Vector3.one);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (!IsMainBodyRenderer(renderer))
                {
                    continue;
                }

                Bounds rendererBounds = renderer.bounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 worldCorner = GetBoundsCorner(rendererBounds, corner);
                    Vector3 localCorner = root.transform.InverseTransformPoint(worldCorner);
                    if (!initialized)
                    {
                        bounds = new Bounds(localCorner, Vector3.zero);
                        initialized = true;
                    }
                    else
                    {
                        bounds.Encapsulate(localCorner);
                    }
                }
            }

            if (!initialized)
            {
                bounds = new Bounds(Vector3.zero, Vector3.one);
            }
        }

        private static bool IsMainBodyRenderer(Renderer renderer)
        {
            if (renderer == null ||
                renderer is ParticleSystemRenderer ||
                renderer is LineRenderer)
            {
                return false;
            }

            return renderer.GetComponentInParent<ExhibitionPlanetarySystem>() == null;
        }

        private static Vector3 GetBoundsCorner(Bounds bounds, int index)
        {
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            return new Vector3(
                (index & 1) == 0 ? min.x : max.x,
                (index & 2) == 0 ? min.y : max.y,
                (index & 4) == 0 ? min.z : max.z);
        }

        private static Vector3 Average(Vector3[] points)
        {
            Vector3 sum = Vector3.zero;
            for (int i = 0; i < points.Length; i++)
            {
                sum += points[i];
            }

            return sum / Mathf.Max(1, points.Length);
        }

        private static Vector3 MaxComponents(Vector3 value, Vector3 minimum)
        {
            return new Vector3(
                Mathf.Max(value.x, minimum.x),
                Mathf.Max(value.y, minimum.y),
                Mathf.Max(value.z, minimum.z));
        }

        private TMP_Text CreateText(
            string objectName,
            RectTransform parent,
            Vector2 anchoredPosition,
            Vector2 size,
            float fontSize,
            FontStyles style,
            TextAlignmentOptions alignment)
        {
            GameObject textObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = Color.white;
            text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;
            if (font != null)
            {
                text.font = font;
            }

            return text;
        }

        private static Image CreateImage(
            string objectName,
            RectTransform parent,
            Color color)
        {
            GameObject imageObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            imageObject.transform.SetParent(parent, false);
            Image image = imageObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
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

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            root.layer = layer;
            for (int i = 0; i < root.transform.childCount; i++)
            {
                SetLayerRecursively(root.transform.GetChild(i).gameObject, layer);
            }
        }

        private sealed class SolarSystemTarget
        {
            public string key;
            public string displayName;
            public GameObject root;
            public AtlasSelectableTarget selectable;
            public bool missionEligible;
            public AtlasMissionTargetKind missionKind;
        }

        private sealed class StarTarget
        {
            public string key;
            public string displayName;
            public TMP_Text label;
            public GameObject rayTarget;
            public AtlasSelectableTarget selectable;
            public bool missionEligible;
            public double azimuthDegrees;
            public double altitudeDegrees;
            public float magnitude;
            public double rightAscensionDegrees;
            public double declinationDegrees;
            public float distanceLightYears;
            public string spectralType;
            public string constellationCode;
        }

        private sealed class ConstellationTarget
        {
            public string key;
            public string displayName;
            public string majorStars;
            public Transform skyParent;
            public Vector3[] starLocalPositions = Array.Empty<Vector3>();
            public Vector3 labelLocalPosition;
            public TMP_Text[] starNameLabels = Array.Empty<TMP_Text>();
            public TMP_Text constellationNameLabel;
            public GameObject rayTarget;
            public AtlasSelectableTarget selectable;
            public bool missionEligible;
        }

        public struct AtlasMissionTarget
        {
            public string key;
            public string displayName;
            public AtlasMissionTargetKind kind;

            public AtlasMissionTarget(
                string targetKey,
                string targetDisplayName,
                AtlasMissionTargetKind targetKind)
            {
                key = targetKey;
                displayName = targetDisplayName;
                kind = targetKind;
            }
        }

        public enum AtlasMissionTargetKind
        {
            Planet,
            Moon,
            Star,
            Constellation
        }

        private struct InfoContent
        {
            public string title;
            public string summary;
            public string detailOne;
            public string detailTwo;
            public Sprite constellationImage;
        }
    }
}
