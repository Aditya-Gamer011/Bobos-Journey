using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BobosJourney
{
    /// <summary>In-game HUD: height gauge, zone titles, callouts, control hints, pause menu and chapter title card.</summary>
    public class HUD : MonoBehaviour
    {
        const float PxPerMetre = 7f;
        const float GaugeHalf = 300f;

        Text heightText, bestText, zoneTitle, zoneSub, toastText, graphicsLabel;
        RectTransform gauge, ticksRoot, bestMark, flagMark, portrait, toastRoot, zoneRoot, pauseRoot, pausePage;
        Image zoneBrush, toastSplat;
        CanvasGroup zoneGroup, hintGroup, toastGroup, flashGroup;
        RawImage gaugeLine;
        readonly List<(RectTransform rt, Text label, Image mark)> ticks = new List<(RectTransform, Text, Image)>();
        Coroutine zoneRoutine, toastRoutine;
        float shownHeight;

        public System.Action OnResume, OnRestart, OnQuit, OnGraphics;

        public void SetGraphicsLabel(string level)
        {
            if (graphicsLabel) graphicsLabel.text = "GRAPHICS: " + level.ToUpperInvariant();
        }

        void Awake()
        {
            var art = BoboArt.Instance;
            var canvas = UIKit.Canvas("HUD", 10).transform;
            canvas.SetParent(transform, false);

            // climb gauge on the left edge
            gauge = UIKit.Rect("Gauge", canvas, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(70, 10), new Vector2(60, GaugeHalf * 2));
            var lineRt = UIKit.Stretch("Line", gauge);
            gaugeLine = lineRt.gameObject.AddComponent<RawImage>();
            gaugeLine.texture = art.inkLine;
            gaugeLine.color = new Color(UIKit.Bone.r, UIKit.Bone.g, UIKit.Bone.b, 0.85f);
            gaugeLine.raycastTarget = false;
            ticksRoot = UIKit.Stretch("Ticks", gauge);
            for (int i = 0; i < 12; i++)
            {
                var tick = UIKit.Rect("Tick", ticksRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(120, 30));
                var mark = UIKit.Brush("Mark", tick, new Vector2(0, 0.5f), new Vector2(4, 0), new Vector2(36, 12), UIKit.Bone, Random.Range(-6f, 6f));
                var label = UIKit.Label("Num", tick, "", 20, new Color(UIKit.Bone.r, UIKit.Bone.g, UIKit.Bone.b, 0.8f), TextAnchor.MiddleLeft);
                ((RectTransform)label.transform).offsetMin = new Vector2(30, 0);
                ticks.Add((tick, label, mark));
            }
            bestMark = UIKit.Rect("Best", ticksRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(160, 30));
            UIKit.Brush("Mark", bestMark, new Vector2(0, 0.5f), new Vector2(-6, 0), new Vector2(56, 16), UIKit.Ember, 3f);
            UIKit.Label("Text", bestMark, "BEST", 20, UIKit.Ember, TextAnchor.MiddleLeft, 1.5f).rectTransform.offsetMin = new Vector2(34, 0);
            flagMark = UIKit.Rect("Flag", ticksRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(160, 30));
            UIKit.Brush("Mark", flagMark, new Vector2(0, 0.5f), new Vector2(-6, 0), new Vector2(56, 16), UIKit.Gold, -3f);
            UIKit.Label("Text", flagMark, "FLAG", 20, UIKit.Gold, TextAnchor.MiddleLeft, 1.5f).rectTransform.offsetMin = new Vector2(34, 0);

            // Bobo stays in the middle of the gauge; the marks scroll past him
            portrait = UIKit.Drawing(gauge, UIKit.BoboDrawing, new Vector2(0.5f, 0.5f), Vector2.zero, 80);
            var heightRoot = UIKit.Rect("Height", gauge, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 0.5f), new Vector2(52, 10), new Vector2(360, 80));
            heightText = UIKit.Label("Value", heightRoot, "0M", 64, UIKit.Cream, TextAnchor.MiddleLeft, 3f);
            var bestRoot = UIKit.Rect("BestValue", gauge, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 0.5f), new Vector2(56, -34), new Vector2(360, 30));
            bestText = UIKit.Label("Value", bestRoot, "", 20, new Color(UIKit.Bone.r, UIKit.Bone.g, UIKit.Bone.b, 0.75f), TextAnchor.MiddleLeft, 1.2f);

            // zone title, brushed across the top
            zoneRoot = UIKit.Rect("Zone", canvas, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -46), new Vector2(900, 150));
            zoneGroup = zoneRoot.gameObject.AddComponent<CanvasGroup>();
            zoneGroup.alpha = 0;
            zoneBrush = UIKit.Brush("Stroke", zoneRoot, new Vector2(0.5f, 0.5f), new Vector2(0, -12), new Vector2(980, 170), new Color(0.06f, 0.03f, 0.07f, 0.88f), -1.5f);
            zoneBrush.type = Image.Type.Filled;
            zoneBrush.fillMethod = Image.FillMethod.Horizontal;
            zoneBrush.fillOrigin = 0;
            var sub = UIKit.Rect("Sub", zoneRoot, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-10, -8), new Vector2(600, 30));
            zoneSub = UIKit.Label("Text", sub, "", 22, UIKit.Gold, TextAnchor.MiddleCenter);
            var title = UIKit.Rect("Title", zoneRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -18), new Vector2(900, 80));
            zoneTitle = UIKit.Label("Text", title, "", 58, UIKit.Cream, TextAnchor.MiddleCenter);

            // callouts, with an ink splat behind the word
            toastRoot = UIKit.Rect("Toast", canvas, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 180), new Vector2(900, 180));
            toastGroup = toastRoot.gameObject.AddComponent<CanvasGroup>();
            toastGroup.alpha = 0;
            var splatRt = UIKit.Rect("Splat", toastRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(560, 300));
            toastSplat = UIKit.Image(splatRt, new Color(0.08f, 0.04f, 0.1f, 0.85f), art.splat);
            toastText = UIKit.Label("Text", toastRoot, "", 86, UIKit.Gold, TextAnchor.MiddleCenter, 3.5f);

            // control hints, bottom left
            var hint = UIKit.Rect("Controls", canvas, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(150, 34), new Vector2(1400, 60));
            hintGroup = hint.gameObject.AddComponent<CanvasGroup>();
            var controls = new[]
            {
                ("WASD", "move"), ("MOUSE", "look"), ("SPACE", "jump"), ("SPACE", "as you land = bounce higher"), ("R", "last flag"), ("ESC", "pause"),
            };
            float x = 0;
            foreach (var (key, what) in controls)
            {
                float keyWidth = 26 + key.Length * 15;
                var cap = UIKit.Card("Key", hint, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(x, 0), new Vector2(keyWidth, 44), UIKit.Bone);
                UIKit.Label("K", cap, key, 20, UIKit.Ink);
                float textWidth = 18 + what.Length * 11.5f;
                var labelRt = UIKit.Rect("What", hint, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(x + keyWidth + 10, 0), new Vector2(textWidth, 44));
                UIKit.Label("T", labelRt, what.ToUpperInvariant(), 20, UIKit.Bone, TextAnchor.MiddleLeft, 1.2f);
                x += keyWidth + textWidth + 26;
            }

            // respawn flash
            var flash = UIKit.Stretch("Flash", canvas);
            UIKit.Image(flash, Color.white);
            flashGroup = flash.gameObject.AddComponent<CanvasGroup>();
            flashGroup.alpha = 0;
            flashGroup.blocksRaycasts = false;

            BuildPause(canvas);
            StartCoroutine(HideHintLater());
        }

        void BuildPause(Transform canvas)
        {
            pauseRoot = UIKit.Stretch("Pause", canvas);
            UIKit.Image(pauseRoot, new Color(0.04f, 0.02f, 0.06f, 0.72f)).raycastTarget = true;
            pausePage = UIKit.Paper("Page", pauseRoot, new Vector2(0.5f, 0.5f), new Vector2(0, -6), new Vector2(660, 860), -2.2f);

            var title = UIKit.Rect("Title", pausePage, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -78), new Vector2(560, 90));
            UIKit.Brush("Underline", title, new Vector2(0.5f, 0), new Vector2(0, -4), new Vector2(360, 30), new Color(UIKit.Ember.r, UIKit.Ember.g, UIKit.Ember.b, 0.85f), 1.5f);
            UIKit.Label("Text", title, "CATCH YOUR BREATH", 60, UIKit.Ink);
            var quote = UIKit.Rect("Quote", pausePage, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -182), new Vector2(520, 60));
            var quoteText = UIKit.Label("Text", quote, "\"...you must... bounce back.\"", 26, new Color(0.35f, 0.24f, 0.2f));
            quoteText.fontStyle = FontStyle.Italic;

            UIKit.Drawing(pausePage, UIKit.BoboDrawing, new Vector2(0.5f, 0), new Vector2(190, 96), 98);

            float y = 110;
            UIKit.MenuItem(pausePage, "KEEP CLIMBING", new Vector2(-10, y), new Vector2(420, 70), () => OnResume?.Invoke(), UIKit.Gold);
            y -= 88;
            UIKit.MenuItem(pausePage, "BACK TO LAST FLAG", new Vector2(-10, y), new Vector2(460, 70), () => OnRestart?.Invoke(), new Color(0.6f, 0.78f, 0.95f));
            y -= 88;
            var gfx = UIKit.MenuItem(pausePage, "GRAPHICS", new Vector2(-10, y), new Vector2(460, 70), () => OnGraphics?.Invoke(), new Color(0.6f, 0.85f, 0.6f));
            y -= 88;
            graphicsLabel = gfx.GetComponentInChildren<Text>();
            if (Application.platform != RuntimePlatform.WebGLPlayer) // a web page can't quit itself
                UIKit.MenuItem(pausePage, "QUIT", new Vector2(-10, y), new Vector2(220, 70), () => OnQuit?.Invoke(), new Color(0.75f, 0.7f, 0.7f));
            pauseRoot.gameObject.SetActive(false);
        }

        IEnumerator HideHintLater()
        {
            yield return new WaitForSeconds(16f);
            for (float t = 0; t < 1f; t += Time.deltaTime)
            {
                hintGroup.alpha = 1f - t;
                yield return null;
            }
            hintGroup.alpha = 0;
        }

        public void SetHeight(float h, float best, float toNext, float progress)
        {
            shownHeight = Mathf.Lerp(shownHeight, h, 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime));
            float shown = Mathf.Max(0, shownHeight);
            heightText.text = Mathf.RoundToInt(shown) + "M";
            bestText.text = "BEST " + Mathf.RoundToInt(best) + "M";

            // a mark every 10 m
            float first = Mathf.Floor((shown - GaugeHalf / PxPerMetre) / 10f) * 10f;
            for (int i = 0; i < ticks.Count; i++)
            {
                float v = first + i * 10f;
                float y = (v - shown) * PxPerMetre;
                var (rt, label, mark) = ticks[i];
                bool show = v >= 0 && Mathf.Abs(y) < GaugeHalf - 10f;
                rt.gameObject.SetActive(show);
                if (!show) continue;
                rt.anchoredPosition = new Vector2(-14, y);
                float fade = 1f - Mathf.InverseLerp(GaugeHalf * 0.55f, GaugeHalf - 10f, Mathf.Abs(y));
                float near = Mathf.Abs(y) < 40f ? 0f : 1f; // keep clear of the portrait
                label.text = ((int)v % 50 == 0) ? ((int)v).ToString() : "";
                label.color = new Color(UIKit.Bone.r, UIKit.Bone.g, UIKit.Bone.b, 0.8f * fade * near);
                mark.color = new Color(UIKit.Bone.r, UIKit.Bone.g, UIKit.Bone.b, 0.85f * fade);
                mark.rectTransform.sizeDelta = new Vector2((int)v % 50 == 0 ? 46 : 30, 16);
            }
            PlaceMark(bestMark, best, shown, best > shown + 2f);
            PlaceMark(flagMark, shown + toNext, shown, toNext > 1f);
        }

        void PlaceMark(RectTransform mark, float value, float shown, bool visible)
        {
            float y = (value - shown) * PxPerMetre;
            bool show = visible && Mathf.Abs(y) < GaugeHalf - 10f;
            mark.gameObject.SetActive(show);
            if (show) mark.anchoredPosition = new Vector2(-14, y);
        }

        public void ShowZone(int index, string name)
        {
            zoneSub.text = "- ZONE " + (index + 1) + " -";
            zoneTitle.text = name.ToUpperInvariant();
            if (zoneRoutine != null) StopCoroutine(zoneRoutine);
            zoneRoutine = StartCoroutine(ZoneRoutine());
        }

        IEnumerator ZoneRoutine()
        {
            zoneGroup.alpha = 1;
            zoneTitle.color = new Color(UIKit.Cream.r, UIKit.Cream.g, UIKit.Cream.b, 0);
            zoneSub.color = new Color(UIKit.Gold.r, UIKit.Gold.g, UIKit.Gold.b, 0);
            // brush sweeps across first, then the lettering fades in
            for (float t = 0; t < 0.45f; t += Time.deltaTime)
            {
                zoneBrush.fillAmount = Mathf.SmoothStep(0, 1, t / 0.45f);
                yield return null;
            }
            zoneBrush.fillAmount = 1;
            for (float t = 0; t < 0.35f; t += Time.deltaTime)
            {
                float k = t / 0.35f;
                zoneTitle.color = new Color(UIKit.Cream.r, UIKit.Cream.g, UIKit.Cream.b, k);
                zoneSub.color = new Color(UIKit.Gold.r, UIKit.Gold.g, UIKit.Gold.b, k);
                yield return null;
            }
            yield return new WaitForSeconds(3f);
            for (float t = 0; t < 0.7f; t += Time.deltaTime)
            {
                zoneGroup.alpha = 1f - t / 0.7f;
                yield return null;
            }
            zoneGroup.alpha = 0;
        }

        public void Toast(string text, Color color, float size = 86, float hold = 1.0f)
        {
            toastText.text = text;
            toastText.color = color;
            toastText.fontSize = Mathf.RoundToInt(size * (UIKit.HasComicFont ? 1.12f : 1f));
            toastSplat.color = new Color(color.r * 0.18f, color.g * 0.12f, color.b * 0.2f, 0.85f);
            if (toastRoutine != null) StopCoroutine(toastRoutine);
            toastRoutine = StartCoroutine(ToastRoutine(hold));
        }

        IEnumerator ToastRoutine(float hold)
        {
            toastRoot.localRotation = Quaternion.Euler(0, 0, Random.Range(-6f, 6f));
            toastSplat.rectTransform.localRotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));
            for (float t = 0; t < 0.2f; t += Time.deltaTime)
            {
                float k = t / 0.2f;
                toastGroup.alpha = k;
                toastSplat.rectTransform.localScale = Vector3.one * Mathf.LerpUnclamped(0.2f, 1f, EaseOutBack(k));
                toastText.rectTransform.localScale = Vector3.one * Mathf.LerpUnclamped(1.6f, 1f, k);
                yield return null;
            }
            toastGroup.alpha = 1;
            toastSplat.rectTransform.localScale = Vector3.one;
            toastText.rectTransform.localScale = Vector3.one;
            yield return new WaitForSeconds(hold);
            for (float t = 0; t < 0.35f; t += Time.deltaTime)
            {
                toastGroup.alpha = 1f - t / 0.35f;
                yield return null;
            }
            toastGroup.alpha = 0;
        }

        public void Flash(Color c)
        {
            flashGroup.GetComponent<Image>().color = c;
            StartCoroutine(FlashRoutine());
        }

        IEnumerator FlashRoutine()
        {
            flashGroup.alpha = 0.85f;
            for (float t = 0; t < 0.45f; t += Time.unscaledDeltaTime)
            {
                flashGroup.alpha = 0.85f * (1f - t / 0.45f);
                yield return null;
            }
            flashGroup.alpha = 0;
        }

        public void SetPaused(bool paused)
        {
            pauseRoot.gameObject.SetActive(paused);
            if (paused)
            {
                var first = pauseRoot.GetComponentInChildren<Button>();
                if (first) first.Select();
                StartCoroutine(DropPage());
            }
        }

        IEnumerator DropPage()
        {
            for (float t = 0; t < 0.28f; t += Time.unscaledDeltaTime)
            {
                float k = t / 0.28f;
                pausePage.anchoredPosition = new Vector2(0, Mathf.Lerp(80, -6, EaseOutBack(k)));
                pausePage.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(-7f, -2.2f, k));
                yield return null;
            }
            pausePage.anchoredPosition = new Vector2(0, -6);
            pausePage.localRotation = Quaternion.Euler(0, 0, -2.2f);
        }

        RectTransform cardRoot;
        CanvasGroup cardGroup;
        Image cardUnderline;
        Text cardChapter, cardTitle, cardTagline;

        /// <summary>Full-screen title card, visible immediately so the first frame is the card, not the level.</summary>
        public void ShowChapterCard(string chapter, string title, string tagline)
        {
            var canvas = UIKit.Canvas("ChapterCard", 100).transform;
            canvas.SetParent(transform, false);
            cardRoot = UIKit.Stretch("Card", canvas);
            UIKit.Image(cardRoot, Color.black).raycastTarget = true;
            cardGroup = cardRoot.gameObject.AddComponent<CanvasGroup>();

            var chapterRt = UIKit.Rect("Chapter", cardRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 150), new Vector2(900, 70));
            cardChapter = UIKit.Label("Text", chapterRt, chapter, 46, Color.white);
            // short ink dashes either side: "- CHAPTER 1 -"
            UIKit.Brush("DashL", chapterRt, new Vector2(0.5f, 0.5f), new Vector2(-190, -2), new Vector2(110, 14), Color.white, 2f);
            UIKit.Brush("DashR", chapterRt, new Vector2(0.5f, 0.5f), new Vector2(190, -2), new Vector2(110, 14), Color.white, -2f).rectTransform.localScale = new Vector3(-1, 1, 1);

            var titleRt = UIKit.Rect("Title", cardRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 30), new Vector2(1700, 160));
            cardTitle = UIKit.Label("Text", titleRt, title, 104, Color.white);

            cardUnderline = UIKit.Brush("Underline", cardRoot, new Vector2(0.5f, 0.5f), new Vector2(10, -58), new Vector2(1180, 50), Color.white, -1f);
            cardUnderline.type = Image.Type.Filled;
            cardUnderline.fillMethod = Image.FillMethod.Horizontal;
            cardUnderline.fillOrigin = 0;
            cardUnderline.fillAmount = 0;

            var taglineRt = UIKit.Rect("Tagline", cardRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -140), new Vector2(1200, 50));
            cardTagline = UIKit.Label("Text", taglineRt, tagline, 30, new Color(0.72f, 0.72f, 0.72f));
            cardTagline.fontStyle = FontStyle.Italic;

            var skip = UIKit.Rect("Skip", cardRoot, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-40, 30), new Vector2(400, 40));
            UIKit.Label("Text", skip, "SPACE TO SKIP", 20, new Color(0.5f, 0.5f, 0.5f), TextAnchor.LowerRight);

            SetCardTextAlpha(0f);
        }

        void SetCardTextAlpha(float a)
        {
            if (!cardRoot) return;
            cardChapter.color = new Color(1, 1, 1, a);
            cardTitle.color = new Color(1, 1, 1, a);
            cardTagline.color = new Color(0.72f, 0.72f, 0.72f, a);
            foreach (var img in cardRoot.GetComponentsInChildren<Image>())
                if (img.name.StartsWith("Dash")) img.color = new Color(1, 1, 1, a);
        }

        /// <summary>Fades the card text in, sweeps the underline, then holds (Space / click skips).</summary>
        public IEnumerator HoldChapterCard(float seconds)
        {
            if (!cardRoot) yield break;
            Sfx.Play(Sfx.Id.Page, 0.6f, 0.9f);
            for (float t = 0; t < 0.6f; t += Time.deltaTime)
            {
                float k = t / 0.6f;
                SetCardTextAlpha(k);
                cardTitle.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.06f, 1f, k);
                yield return null;
            }
            SetCardTextAlpha(1f);
            cardTitle.rectTransform.localScale = Vector3.one;
            for (float t = 0; t < 0.45f; t += Time.deltaTime)
            {
                cardUnderline.fillAmount = Mathf.SmoothStep(0, 1, t / 0.45f);
                yield return null;
            }
            cardUnderline.fillAmount = 1;
            for (float t = 0; t < seconds; t += Time.deltaTime)
            {
                if (t > 0.3f && GameInput.AdvanceDown) break;
                yield return null;
            }
        }

        public IEnumerator FadeChapterCard()
        {
            if (!cardRoot) yield break;
            for (float t = 0; t < 0.9f; t += Time.deltaTime)
            {
                cardGroup.alpha = 1f - Mathf.SmoothStep(0, 1, t / 0.9f);
                yield return null;
            }
            Destroy(cardRoot.parent.gameObject);
            cardRoot = null;
        }

        static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1;
            return 1 + c3 * Mathf.Pow(x - 1, 3) + c1 * Mathf.Pow(x - 1, 2);
        }
    }
}
