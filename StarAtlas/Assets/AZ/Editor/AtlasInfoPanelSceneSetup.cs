using Rokid.UXR.Utility;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AZ.Atlas.Editor
{
    [InitializeOnLoad]
    internal static class AtlasInfoPanelSceneSetup
    {
        private const string AtlasScenePath = "Assets/AZ/Atlas.unity";
        private const string PanelName = "Atlas Info Panel";
        private const string FontGuid = "af641c37b3c25a04a9fc06aa25e0735f";

        static AtlasInfoPanelSceneSetup()
        {
            EditorApplication.delayCall += SetupActiveAtlasScene;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorSceneManager.sceneOpened += OnSceneOpened;
        }

        [MenuItem("AZ/Atlas/Create Or Repair Info Panel")]
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

            GameObject existing = FindSceneObject(scene, PanelName);
            AtlasInfoPanelView existingView =
                existing != null ? existing.GetComponent<AtlasInfoPanelView>() : null;

            GameObject atlasSystem = FindSceneObject(scene, "AtlasSystem");
            if (atlasSystem == null)
            {
                Debug.LogError("Atlas info panel setup could not find AtlasSystem.");
                return;
            }

            bool repaired = RepairAtlasSystemTransform(atlasSystem.transform);
            if (existingView != null && existingView.IsConfigured)
            {
                repaired |= RepairPanelTransform(existing.GetComponent<RectTransform>(), atlasSystem.transform);
                repaired |= RepairPanelContentLayout(existingView);
                repaired |= SetLayerRecursively(existing, 0);
                if (repaired)
                {
                    EditorUtility.SetDirty(atlasSystem);
                    EditorUtility.SetDirty(existing);
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    Debug.Log("Repaired AtlasSystem and Atlas Info Panel transforms.");
                }

                return;
            }

            bool createdPanel = existing == null;
            GameObject panelObject = createdPanel
                ? new GameObject(
                    PanelName,
                    typeof(RectTransform),
                    typeof(Canvas),
                    typeof(CanvasScaler),
                    typeof(GraphicRaycaster),
                    typeof(CanvasGroup),
                    typeof(CanvasRegister),
                    typeof(AtlasInfoPanelView))
                : existing;
            if (createdPanel)
            {
                Undo.RegisterCreatedObjectUndo(panelObject, "Create Atlas Info Panel");
            }
            panelObject.transform.SetParent(atlasSystem.transform, false);
            panelObject.layer = 0;

            Canvas canvas = GetOrAdd<Canvas>(panelObject);
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 120;

            CanvasScaler scaler = GetOrAdd<CanvasScaler>(panelObject);
            scaler.dynamicPixelsPerUnit = 12f;
            scaler.referencePixelsPerUnit = 100f;
            GetOrAdd<GraphicRaycaster>(panelObject);
            GetOrAdd<CanvasRegister>(panelObject);

            CanvasGroup group = GetOrAdd<CanvasGroup>(panelObject);
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;

            RectTransform panelRect = panelObject.GetComponent<RectTransform>();
            RepairPanelTransform(panelRect, atlasSystem.transform);

            ClearChildren(panelObject.transform);
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                AssetDatabase.GUIDToAssetPath(FontGuid));

            Image background = CreateImage(
                "Background",
                panelRect,
                new Color(0.025f, 0.035f, 0.055f, 0.91f));
            Stretch(background.rectTransform);

            TMP_Text title = CreateText(
                "Title",
                panelRect,
                new Vector2(0f, 224f),
                new Vector2(630f, 70f),
                42f,
                FontStyles.Bold,
                TextAlignmentOptions.Left,
                font);
            title.text = "\u5929\u4f53\u4fe1\u606f";

            TMP_Text summary = CreateText(
                "Summary",
                panelRect,
                new Vector2(0f, 104f),
                new Vector2(630f, 220f),
                24f,
                FontStyles.Normal,
                TextAlignmentOptions.TopLeft,
                font);
            summary.text = "\u70b9\u51fb\u5929\u4f53\u6216\u661f\u5ea7\u540e\u663e\u793a\u8be6\u7ec6\u8d44\u6599\u3002";
            summary.enableAutoSizing = true;
            summary.fontSizeMin = 13f;
            summary.fontSizeMax = 24f;

            TMP_Text detailOne = CreateText(
                "Detail One",
                panelRect,
                new Vector2(0f, -48f),
                new Vector2(630f, 70f),
                22f,
                FontStyles.Normal,
                TextAlignmentOptions.TopLeft,
                font);
            TMP_Text detailTwo = CreateText(
                "Detail Two",
                panelRect,
                new Vector2(0f, -182f),
                new Vector2(630f, 168f),
                22f,
                FontStyles.Normal,
                TextAlignmentOptions.TopLeft,
                font);
            detailOne.enableAutoSizing = true;
            detailOne.fontSizeMin = 12f;
            detailOne.fontSizeMax = 22f;
            detailTwo.enableAutoSizing = true;
            detailTwo.fontSizeMin = 9f;
            detailTwo.fontSizeMax = 22f;

            AtlasInfoPanelView view = GetOrAdd<AtlasInfoPanelView>(panelObject);
            view.Configure(
                canvas,
                group,
                panelRect,
                title,
                summary,
                detailOne,
                detailTwo);
            EditorUtility.SetDirty(view);
            EditorUtility.SetDirty(panelObject);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Created AtlasSystem/Atlas Info Panel in the Atlas scene hierarchy.");
        }

        private static bool RepairAtlasSystemTransform(Transform atlasSystem)
        {
            bool changed =
                atlasSystem.localPosition != Vector3.zero ||
                atlasSystem.localRotation != Quaternion.identity ||
                atlasSystem.localScale != Vector3.one;
            if (!changed)
            {
                return false;
            }

            atlasSystem.localPosition = Vector3.zero;
            atlasSystem.localRotation = Quaternion.identity;
            atlasSystem.localScale = Vector3.one;
            return true;
        }

        private static bool RepairPanelTransform(RectTransform panelRect, Transform atlasSystem)
        {
            if (panelRect == null)
            {
                return false;
            }

            Vector2 expectedSize = new Vector2(720f, 610f);
            Vector3 expectedPosition = new Vector3(0f, 0f, 1.5f);
            Vector3 expectedScale = Vector3.one * 0.00105f;
            bool changed =
                panelRect.parent != atlasSystem ||
                panelRect.sizeDelta != expectedSize ||
                panelRect.localPosition != expectedPosition ||
                panelRect.localRotation != Quaternion.identity ||
                panelRect.localScale != expectedScale;
            if (!changed)
            {
                return false;
            }

            panelRect.SetParent(atlasSystem, false);
            panelRect.sizeDelta = expectedSize;
            panelRect.localPosition = expectedPosition;
            panelRect.localRotation = Quaternion.identity;
            panelRect.localScale = expectedScale;
            return true;
        }

        private static bool RepairPanelContentLayout(AtlasInfoPanelView view)
        {
            bool changed = false;
            changed |= RepairTextLayout(
                view.SummaryText,
                new Vector2(0f, 104f),
                new Vector2(630f, 220f),
                13f,
                24f);
            changed |= RepairTextLayout(
                view.DetailOneText,
                new Vector2(0f, -48f),
                new Vector2(630f, 70f),
                12f,
                22f);
            changed |= RepairTextLayout(
                view.DetailTwoText,
                new Vector2(0f, -182f),
                new Vector2(630f, 168f),
                9f,
                22f);
            return changed;
        }

        private static bool RepairTextLayout(
            TMP_Text text,
            Vector2 position,
            Vector2 size,
            float minimumFontSize,
            float maximumFontSize)
        {
            if (text == null)
            {
                return false;
            }

            RectTransform rect = text.rectTransform;
            bool changed =
                rect.anchoredPosition != position ||
                rect.sizeDelta != size ||
                !text.enableAutoSizing ||
                !Mathf.Approximately(text.fontSizeMin, minimumFontSize) ||
                !Mathf.Approximately(text.fontSizeMax, maximumFontSize);
            if (!changed)
            {
                return false;
            }

            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            text.enableAutoSizing = true;
            text.fontSizeMin = minimumFontSize;
            text.fontSizeMax = maximumFontSize;
            EditorUtility.SetDirty(text);
            return true;
        }

        private static GameObject FindSceneObject(Scene scene, string objectName)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                Transform found = FindRecursively(roots[i].transform, objectName);
                if (found != null)
                {
                    return found.gameObject;
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
                Transform found = FindRecursively(current.GetChild(i), objectName);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            return component != null ? component : target.AddComponent<T>();
        }

        private static void ClearChildren(Transform parent)
        {
            while (parent.childCount > 0)
            {
                Undo.DestroyObjectImmediate(parent.GetChild(0).gameObject);
            }
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
            imageObject.layer = 0;
            Image image = imageObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static TMP_Text CreateText(
            string objectName,
            RectTransform parent,
            Vector2 position,
            Vector2 size,
            float fontSize,
            FontStyles style,
            TextAlignmentOptions alignment,
            TMP_FontAsset font)
        {
            GameObject textObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            textObject.layer = 0;

            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
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

        private static bool SetLayerRecursively(GameObject root, int layer)
        {
            bool changed = root.layer != layer;
            root.layer = layer;
            for (int i = 0; i < root.transform.childCount; i++)
            {
                changed |= SetLayerRecursively(root.transform.GetChild(i).gameObject, layer);
            }

            return changed;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
