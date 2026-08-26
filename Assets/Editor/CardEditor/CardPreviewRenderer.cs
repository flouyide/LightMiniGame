using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using LightMiniGame.CardEditor;

namespace LightMiniGame.CardEditor.Editor
{
    /// <summary>
    /// 卡牌离线渲染器：把一张 CardEntry 用「卡牌.prefab」模板 + 三层美术 + 描述框位置
    /// 渲染成卡牌图片（Texture2D），供卡牌编辑器内实时预览“最终效果”。
    /// 复用 CardDisplay.ApplyCardEntry —— 与战斗/导出一致。
    ///
    /// 资源管理：每次渲染完毕后会立即销毁临时相机/画布/实例/RenderTexture，
    /// 不长期保留任何隐藏对象，避免残留相机干扰游戏运行。
    /// </summary>
    public static class CardPreviewRenderer
    {
        private const int PreviewLayer = 31;
        private const float CardW = 180f;
        private const float CardH = 252f;
        private const float TexScale = 2f;

        private static Texture2D _cache;
        private static string _cacheKey;

        /// <summary>项目统一的卡面模板。</summary>
        public static string TemplatePathFor(CardType type) => "Assets/Prefabs/Battle/Cards/卡牌.prefab";

        private static string KeyOf(CardEntry c, bool lowSanity)
        {
            if (c == null) return null;
            return $"{c.cardName}|{c.cardType}|{c.grade}|{c.cardArt}|{c.descBoxSprite}|{c.typeBoxSprite}" +
                   $"|{c.descBoxOffsetX}|{c.descBoxOffsetY}|{c.descBoxHeight}|{c.descBoxInset}" +
                   $"|{c.GetDescription(lowSanity)}|{lowSanity}|{c.keyword}";
        }

        /// <summary>渲染卡面为 Texture2D（带缓存：数据没变直接回缓存）。</summary>
        public static Texture2D Render(CardEntry card, bool lowSanity)
        {
            string key = KeyOf(card, lowSanity);
            if (key == null) return null;
            if (_cache != null && _cacheKey == key) return _cache;
            _cache = DoRender(card, lowSanity);
            _cacheKey = key;
            return _cache;
        }

        private static Texture2D DoRender(CardEntry card, bool lowSanity)
        {
            var tplPath = TemplatePathFor(card.cardType);
            var tpl = AssetDatabase.LoadAssetAtPath<GameObject>(tplPath);
            if (tpl == null)
            {
                Debug.LogWarning($"[卡牌预览] 模板缺失：{tplPath}");
                return null;
            }

            // ---- 一次性资源（渲染完立即释放） ----
            var root = new GameObject("__CardPreviewRoot__");
            root.hideFlags = HideFlags.HideAndDontSave;
            root.transform.position = new Vector3(0f, 0f, -500f);

            var camGo = new GameObject("CardPreviewCam", typeof(Camera));
            camGo.transform.SetParent(root.transform, false);
            camGo.transform.localPosition = new Vector3(0f, 0f, -100f);
            SetLayerRecursive(camGo.transform, PreviewLayer);
            var cam = camGo.GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = CardH / 2f + 1f;
            cam.transform.rotation = Quaternion.identity;
            cam.cullingMask = 1 << PreviewLayer;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 500f;

            int w = (int)(CardW * TexScale), h = (int)(CardH * TexScale);
            var rt = new RenderTexture(w, h, 16, RenderTextureFormat.ARGB32);
            rt.name = "CardPreviewRT";
            cam.targetTexture = rt;

            GameObject canvasGo = null;
            GameObject inst = null;
            try
            {
                // WorldSpace Canvas
                canvasGo = new GameObject("CardPreviewCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
                canvasGo.transform.SetParent(root.transform, false);
                SetLayerRecursive(canvasGo.transform, PreviewLayer);
                var canvas = canvasGo.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                var cRect = canvasGo.GetComponent<RectTransform>();
                cRect.sizeDelta = new Vector2(CardW, CardH);
                var scaler = canvasGo.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                scaler.scaleFactor = 1f;
                canvas.worldCamera = cam;

                // 实例（普通复制，不污染场景）
                inst = (GameObject)Object.Instantiate(tpl);
                inst.name = "CardPreviewInstance";
                inst.transform.SetParent(canvasGo.transform, false);
                SetLayerRecursive(inst.transform, PreviewLayer);

                var instRt = inst.transform as RectTransform;
                if (instRt != null)
                {
                    instRt.anchorMin = instRt.anchorMax = new Vector2(0.5f, 0.5f);
                    instRt.pivot = new Vector2(0.5f, 0.5f);
                    instRt.anchoredPosition = Vector2.zero;
                    instRt.sizeDelta = new Vector2(CardW, CardH);
                }

                var display = inst.GetComponentInChildren<CardDisplay>(true);
                if (display != null)
                    display.ApplyCardEntry(card, lowSanity);

                Canvas.ForceUpdateCanvases();
                var oldActive = RenderTexture.active;
                cam.targetTexture = rt;
                cam.Render();
                var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0f, 0f, rt.width, rt.height), 0, 0);
                tex.Apply();
                RenderTexture.active = oldActive;
                cam.targetTexture = null;
                return tex;
            }
            finally
            {
                if (inst != null) Object.DestroyImmediate(inst);
                if (canvasGo != null) Object.DestroyImmediate(canvasGo);
                if (camGo != null) Object.DestroyImmediate(camGo);
                if (rt != null) { rt.Release(); Object.DestroyImmediate(rt); }
                if (root != null) Object.DestroyImmediate(root);
            }
        }

        /// <summary>释放缓存（窗口关闭时调用）。</summary>
        public static void Cleanup()
        {
            if (_cache != null) { Object.DestroyImmediate(_cache); _cache = null; }
            _cacheKey = null;
        }

        private static void SetLayerRecursive(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            for (int i = 0; i < t.childCount; i++)
                SetLayerRecursive(t.GetChild(i), layer);
        }
    }
}