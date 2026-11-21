using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace BobosJourney
{
    /// <summary>Helpers for building the hand-inked uGUI in code.</summary>
    public static class UIKit
    {
        public static readonly Color Ink = new Color(0.1f, 0.06f, 0.08f);
        public static readonly Color Cream = new Color(0.97f, 0.92f, 0.8f);
        public static readonly Color Bone = new Color(0.93f, 0.89f, 0.8f);
        public static readonly Color Gold = new Color(1f, 0.79f, 0.36f);
        public static readonly Color Ember = new Color(0.86f, 0.36f, 0.24f);
        public static readonly Color Dusk = new Color(0.16f, 0.1f, 0.2f);

        static BoboArt Art => BoboArt.Instance;

        static Font font;

        /// <summary>True when the comic lettering font is assigned (it has its own weight and slant, so no faux bold).</summary>
        public static bool HasComicFont => Art && Art.font;

        public static Font Font
        {
            get
            {
                if (font == null) font = HasComicFont ? Art.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return font;
            }
        }

        public static Canvas Canvas(string name, int order)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();
            return canvas;
        }

        public static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null) return;
            var eventSystem = new GameObject("EventSystem", typeof(EventSystem));
            eventSystem.AddComponent<InputSystemUIInputModule>();
        }

        public static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Stretch(string name, Transform parent, float inset = 0)
        {
            var rt = Rect(name, parent, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
            return rt;
        }

        public static Image Image(RectTransform rt, Color color, Sprite sprite = null, bool sliced = false)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.sprite = sprite;
            if (sliced && sprite) img.type = UnityEngine.UI.Image.Type.Sliced;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>Hand-inked frame. The fill colour tints the paper; the line work stays ink.</summary>
        public static RectTransform Card(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Color? fill = null)
        {
            var rt = Rect(name, parent, anchor, anchor, pivot, pos, size);
            var bg = Stretch("Fill", rt, -10f); // the sprite's margin holds the overshooting strokes
            Image(bg, fill ?? Cream, Art.inkBox, true).pixelsPerUnitMultiplier = 2f; // thinner corners on small boxes
            return rt;
        }

        /// <summary>A sheet of torn, burnt-edged parchment with a drop shadow.</summary>
        public static RectTransform Paper(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, float tilt = 0f)
        {
            var rt = Rect(name, parent, anchor, anchor, new Vector2(0.5f, 0.5f), pos, size);
            rt.localRotation = Quaternion.Euler(0, 0, tilt);
            var shadow = Stretch("Shade", rt, -6f);
            shadow.anchoredPosition = new Vector2(10, -14);
            Image(shadow, new Color(0, 0, 0, 0.45f), Art.paperTorn, true);
            Image(Stretch("Sheet", rt, -6f), Color.white, Art.paperTorn, true);
            return rt;
        }

        /// <summary>A dry-brush ink stroke, for highlights, underlines and banners.</summary>
        public static Image Brush(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, Color color, float tilt = 0f)
        {
            var rt = Rect(name, parent, anchor, anchor, new Vector2(0.5f, 0.5f), pos, size);
            rt.localRotation = Quaternion.Euler(0, 0, tilt);
            return Image(rt, color, Art.brush);
        }

        public static Text Label(string name, Transform parent, string text, int size, Color color, TextAnchor align = TextAnchor.MiddleCenter, float outline = 0f, Color? outlineColor = null)
        {
            var rt = Stretch(name, parent);
            var label = rt.gameObject.AddComponent<Text>();
            label.font = Font;
            label.text = text;
            label.fontSize = size;
            label.fontStyle = HasComicFont ? FontStyle.Normal : FontStyle.Bold;
            if (HasComicFont)
            {
                label.fontSize = Mathf.RoundToInt(size * 1.12f);
                label.lineSpacing = 0.95f;
            }
            label.color = color;
            label.alignment = align;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            if (outline > 0)
            {
                var inkColor = outlineColor ?? Ink;
                // thin keyline plus an offset shadow, like lettering inked over colour
                var keyline = rt.gameObject.AddComponent<Outline>();
                keyline.effectColor = inkColor;
                keyline.effectDistance = new Vector2(outline * 0.6f, -outline * 0.6f);
                var shadow = rt.gameObject.AddComponent<Shadow>();
                shadow.effectColor = inkColor;
                shadow.effectDistance = new Vector2(outline * 0.9f, -outline * 1.4f);
            }
            return label;
        }

        /// <summary>Inked menu entry; a brush stroke sweeps in behind it when hovered or selected.</summary>
        public static Button MenuItem(Transform parent, string text, Vector2 pos, Vector2 size, System.Action onClick, Color highlight)
        {
            var rt = Rect("Item_" + text, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
            var stroke = Brush("Stroke", rt, new Vector2(0.5f, 0.5f), new Vector2(6, -2), size + new Vector2(70, 26), Color.white, Random.Range(-2f, 2f));
            stroke.raycastTarget = true;
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = stroke;
            var colors = button.colors;
            colors.normalColor = new Color(highlight.r, highlight.g, highlight.b, 0f);
            colors.highlightedColor = new Color(highlight.r, highlight.g, highlight.b, 0.9f);
            colors.selectedColor = new Color(highlight.r, highlight.g, highlight.b, 0.9f);
            colors.pressedColor = new Color(highlight.r * 0.8f, highlight.g * 0.8f, highlight.b * 0.8f, 1f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.onClick.AddListener(() =>
            {
                Sfx.Play(Sfx.Id.Page, 0.6f, 1.3f);
                onClick();
            });
            Label("Text", rt, text, 46, Ink);
            return button;
        }

        /// <summary>Same as MenuItem, with the highlight defaulting to Gold.</summary>
        public static Button Button(Transform parent, string text, Vector2 pos, Vector2 size, System.Action onClick, Color? fill = null)
            => MenuItem(parent, text, pos, size, onClick, fill ?? Gold);

        static Texture2D boboDrawing;

        /// <summary>Hand-drawn Bobo cut-out, loaded from Resources/BoboPortrait.</summary>
        public static Texture2D BoboDrawing => boboDrawing ? boboDrawing : (boboDrawing = Resources.Load<Texture2D>("BoboPortrait"));

        /// <summary>A hand-drawn cut-out with its own outline, shown as is (no mask or ring).</summary>
        public static RectTransform Drawing(Transform parent, Texture tex, Vector2 anchor, Vector2 pos, float size)
        {
            var rt = Rect("Drawing", parent, anchor, anchor, new Vector2(0.5f, 0.5f), pos, new Vector2(size, size));
            // soft shadow so the drawing reads on any background
            var shadow = Stretch("Shadow", rt).gameObject.AddComponent<RawImage>();
            shadow.texture = tex;
            shadow.color = new Color(0, 0, 0, 0.45f);
            shadow.raycastTarget = false;
            shadow.rectTransform.anchoredPosition = new Vector2(3, -4);
            var raw = Stretch("Art", rt).gameObject.AddComponent<RawImage>();
            raw.texture = tex;
            raw.raycastTarget = false;
            return rt;
        }

        /// <summary>Round portrait of a character's painted face inside a hand-inked ring.</summary>
        public static RectTransform Portrait(Transform parent, Texture face, Vector2 anchor, Vector2 pos, float size, Color backing)
        {
            var rt = Rect("Portrait", parent, anchor, anchor, new Vector2(0.5f, 0.5f), pos, new Vector2(size, size));
            var mask = Stretch("Mask", rt);
            Image(mask, backing, Art.circle);
            mask.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var faceRt = Stretch("Face", mask);
            var raw = faceRt.gameObject.AddComponent<RawImage>();
            raw.texture = face;
            raw.uvRect = new Rect(0.405f, 0.31f, 0.19f, 0.38f); // the front of the ball, where the face is painted
            raw.raycastTarget = false;
            Image(Stretch("Ring", rt, -4f), Color.white, Art.inkRing);
            return rt;
        }
    }
}
