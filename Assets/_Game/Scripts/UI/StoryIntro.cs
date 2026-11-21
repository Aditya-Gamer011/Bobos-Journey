using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BobosJourney
{
    // Drawings load from Resources/Story/panel1..3 (1920x1080). Optional voiceover: Resources/Story/vo_<panel>_<line>;
    // when a clip exists that line stays up for its length. Space / click advances, Esc skips.
    /// <summary>Opening story: hand-drawn panels with word-by-word captions, then a cut to black into Chapter 1.</summary>
    public class StoryIntro : MonoBehaviour
    {
        public string nextScene = "Chapter1";
        public Font font;
        [Tooltip("Preview from a later drawing (0 = from the start). Leave at 0 for the real game.")]
        public int startPanel;

        // [x]...[] colours a run of words: b Bobo, m mother, d father, k king, r soldiers, p portal
        class Beat
        {
            public string text;
            public float size;
            public float extraHold;

            public Beat(string text, float size = 0, float extraHold = 0)
            {
                this.text = text;
                this.size = size;
                this.extraHold = extraHold;
            }
        }

        class Panel
        {
            public string image;   // Resources/Story/<image>; null = black page with centred white text
            public Rect box;       // the drawing's caption area, normalised (0,0 = bottom-left of the drawing)
            public bool lightText; // pale lettering for a dark caption box
            public TextAnchor align = TextAnchor.MiddleLeft;
            public Beat[] beats;
        }

        // drawing 1: the beige box in the bottom-right corner (kept clear of its notch and ink border)
        static readonly Rect CornerBox = Rect.MinMaxRect(1022f / 1920f, 26f / 1080f, 1868f / 1920f, 222f / 1080f);
        // drawing 3: the wide dark plaque at the top centre
        static readonly Rect TopBox = Rect.MinMaxRect(592f / 1920f, 1f - 232f / 1080f, 1438f / 1920f, 1f - 72f / 1080f);
        // drawing 2: the dark plaque in the top-left corner
        static readonly Rect TopLeftBox = Rect.MinMaxRect(98f / 1920f, 1f - 206f / 1080f, 648f / 1920f, 1f - 44f / 1080f);

        static readonly Panel[] Story =
        {
            new Panel { image = "panel1", box = CornerBox, beats = new[] {
                new Beat("In this town, everyone is a ball."),
                new Beat("I am a ball. ([b]My name is Bobo,[] by the way.)"),
                new Beat("My [m]mother[] is a ball. My [d]father[] is a ball."),
                new Beat("And even the [k]King...[] is a ball.", 0, 0.4f),
                new Beat("We lived happily in the Ball Kingdom."),
            }},
            new Panel { image = "panel2", box = TopLeftBox, lightText = true, beats = new[] {
                new Beat("Until one day... the [r]King's soldiers[] attacked us."),
                new Beat("They killed my [m]mother.[]", 0, 0.5f),
                new Beat("And my [d]father[] too.", 0, 0.5f),
                new Beat("They were about to kill me as well..."),
            }},
            new Panel { image = "panel3", box = TopBox, lightText = true, align = TextAnchor.MiddleCenter, beats = new[] {
                new Beat("...but my [d]father[] did something mysterious."),
                new Beat("He used some kind of power... and a [p]portal[] opened beneath me."),
                new Beat("With his final breath, he said to me:"),
                new Beat("\"Bounce... back.\"", 78, 0.8f),
            }},
            new Panel { image = null, beats = new[] {
                new Beat("I was really shook by all that. Filled with terror, grief... and rage."),
                new Beat("But my mind was clear."),
                new Beat("For I knew what I had to do.", 0, 0.6f),
            }},
        };

        static readonly Dictionary<char, string> Tints = new Dictionary<char, string>
        {
            { 'b', "2F6DB5" }, { 'm', "C8427A" }, { 'd', "3F7A33" }, { 'k', "9A6A0C" }, { 'r', "A3241C" }, { 'p', "9B5FD0" },
        };
        const string InkHex = "2B1B12"; // the brown-black of the drawings' outlines
        const float WordInterval = 0.11f;
        const float WordFade = 0.14f;

        RectTransform root;
        CanvasGroup hintGroup;
        Text hintText;
        AsyncOperation loading;
        bool skipping;
        bool currentWhite;
        AudioSource voiceSource;

        void Start()
        {
            if (!font) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvas = UIKit.Canvas("Story", 50);
            root = canvas.GetComponent<RectTransform>();
            UIKit.Image(UIKit.Stretch("Black", root), Color.black);

            var hintRt = UIKit.Rect("Hint", root, Vector2.one, Vector2.one, Vector2.one, new Vector2(-34, -26), new Vector2(520, 44));
            hintText = MakeText(hintRt, 26, TextAnchor.UpperRight);
            hintText.text = "SPACE  next      ESC  skip";
            hintGroup = hintRt.gameObject.AddComponent<CanvasGroup>();

            StartCoroutine(Run());
        }

        void Update()
        {
            if (!skipping && GameInput.SkipDown)
            {
                skipping = true;
                StopAllCoroutines();
                StartCoroutine(Finish(true));
            }
        }

        IEnumerator Run()
        {
            yield return null; // one frame so the canvas has its real size
            RectTransform current = null;
            for (int p = Mathf.Clamp(startPanel, 0, Story.Length - 1); p < Story.Length; p++)
            {
                var panel = Story[p];
                bool last = p == Story.Length - 1;
                if (last && loading == null) BeginLoading(); // stream Chapter 1 in while the closing lines play

                var page = BuildPage(panel, out var zoom, out var caption);
                SetHintColour(panel.image != null, panel.image == null || panel.lightText);
                // old page scrolls up and out, the new one comes up from below
                yield return Scroll(current, page, current == null);
                if (current) Destroy(current.gameObject);
                current = page;

                float pageTime = 0;
                float pageLength = EstimateLength(panel, p);
                for (int b = 0; b < panel.beats.Length; b++)
                {
                    var beat = panel.beats[b];
                    var voice = Resources.Load<AudioClip>($"Story/vo_{p + 1}_{b + 1}");
                    if (voice) VoiceSource.PlayOneShot(voice);
                    var fade = caption.GetComponent<CanvasGroup>();
                    if (!fade) fade = caption.gameObject.AddComponent<CanvasGroup>();
                    fade.alpha = 1;
                    caption.fontSize = beat.size > 0 ? (int)beat.size : (panel.image != null ? 52 : 60);
                    caption.resizeTextMaxSize = caption.fontSize;

                    var words = Tokenise(beat.text);
                    float hold = voice ? voice.length + 0.35f : 1.1f + 0.3f * words.Count + beat.extraHold;
                    float interval = voice ? Mathf.Min(WordInterval * 1.6f, voice.length * 0.75f / Mathf.Max(1, words.Count)) : WordInterval;

                    // hidden words keep their place so lines never reflow
                    float t = 0;
                    float typeTime = interval * (words.Count - 1) + WordFade;
                    bool done = false;
                    while (!done)
                    {
                        if (t > 0.08f && GameInput.AdvanceDown)
                        {
                            t = typeTime;
                            done = true;
                        }
                        caption.text = Compose(words, t, interval);
                        Drift(zoom, ref pageTime, pageLength);
                        if (t >= typeTime) done = true;
                        t += Time.deltaTime;
                        yield return null;
                    }
                    caption.text = Compose(words, 999f, interval);

                    for (float w = 0; w < hold; w += Time.deltaTime)
                    {
                        Drift(zoom, ref pageTime, pageLength);
                        if (w > 0.15f && GameInput.AdvanceDown) break;
                        yield return null;
                    }
                    // lines on the same page swap with a quick fade
                    if (b < panel.beats.Length - 1 || last)
                    {
                        for (float f = 0; f < 0.22f; f += Time.deltaTime)
                        {
                            fade.alpha = 1f - f / 0.22f;
                            Drift(zoom, ref pageTime, pageLength);
                            yield return null;
                        }
                    }
                    caption.text = "";
                    fade.alpha = 1;
                }
            }
            yield return Finish(false);
        }

        RectTransform BuildPage(Panel panel, out RectTransform zoom, out Text caption)
        {
            var page = UIKit.Stretch("Page", root);
            page.SetSiblingIndex(1); // above the black backing, below the hint

            if (panel.image == null)
            {
                UIKit.Image(page, Color.black);
                zoom = null;
                var area = UIKit.Rect("Caption", page, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1300, 400));
                caption = MakeText(area, 60, TextAnchor.MiddleCenter);
                return page;
            }

            // the drawing keeps its 16:9 shape and always covers the screen
            var frame = new GameObject("Frame", typeof(RectTransform)).GetComponent<RectTransform>();
            frame.SetParent(page, false);
            var fit = frame.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = 16f / 9f;

            // the push-in pivots on the caption box, so the box stays put while the rest of the drawing drifts
            zoom = new GameObject("Zoom", typeof(RectTransform)).GetComponent<RectTransform>();
            zoom.SetParent(frame, false);
            zoom.anchorMin = Vector2.zero;
            zoom.anchorMax = Vector2.one;
            zoom.pivot = new Vector2(panel.box.center.x, panel.box.center.y > 0.5f ? 1f : 0f);
            zoom.offsetMin = zoom.offsetMax = Vector2.zero;

            var tex = Resources.Load<Texture2D>("Story/" + panel.image);
            var raw = new GameObject("Drawing", typeof(RectTransform)).AddComponent<RawImage>();
            raw.rectTransform.SetParent(zoom, false);
            raw.rectTransform.anchorMin = Vector2.zero;
            raw.rectTransform.anchorMax = Vector2.one;
            raw.rectTransform.offsetMin = raw.rectTransform.offsetMax = Vector2.zero;
            raw.raycastTarget = false;
            if (tex) raw.texture = tex;
            else BuildPlaceholder(raw, panel);

            var boxRt = new GameObject("Caption", typeof(RectTransform)).GetComponent<RectTransform>();
            boxRt.SetParent(zoom, false);
            boxRt.anchorMin = panel.box.min;
            boxRt.anchorMax = panel.box.max;
            boxRt.offsetMin = boxRt.offsetMax = Vector2.zero;
            caption = MakeText(boxRt, 52, panel.align);
            caption.resizeTextForBestFit = true;
            caption.resizeTextMinSize = 26;
            caption.resizeTextMaxSize = 52;
            caption.lineSpacing = 0.92f;
            return page;
        }

        /// <summary>Stand-in for a missing drawing: dark paper with the caption box where the drawing will have it.</summary>
        void BuildPlaceholder(RawImage raw, Panel panel)
        {
            raw.color = new Color(0.16f, 0.13f, 0.15f);
            var note = UIKit.Rect("Note", raw.rectTransform, new Vector2(0.5f, 0.62f), new Vector2(0.5f, 0.62f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1200, 80));
            var noteText = MakeText(note, 40, TextAnchor.MiddleCenter);
            noteText.text = $"( drawing \"{panel.image}.png\" goes here )";
            noteText.color = new Color(1, 1, 1, 0.35f);
            var box = new GameObject("Box", typeof(RectTransform)).AddComponent<Image>();
            box.rectTransform.SetParent(raw.rectTransform, false);
            box.rectTransform.anchorMin = panel.box.min - new Vector2(0.02f, 0.03f);
            box.rectTransform.anchorMax = panel.box.max + new Vector2(0.02f, 0.03f);
            box.rectTransform.offsetMin = box.rectTransform.offsetMax = Vector2.zero;
            box.color = panel.lightText ? new Color(0.16f, 0.1f, 0.18f) : new Color(0.925f, 0.86f, 0.68f);
            if (panel.lightText) raw.color = new Color(0.4f, 0.11f, 0.2f);
            box.raycastTarget = false;
        }

        IEnumerator Scroll(RectTransform from, RectTransform to, bool first)
        {
            float h = root.rect.height;
            if (first)
            {
                // the first drawing fades up out of black instead of scrolling
                var group = to.gameObject.AddComponent<CanvasGroup>();
                for (float t = 0; t < 1.1f; t += Time.deltaTime)
                {
                    float k = Mathf.SmoothStep(0, 1, t / 1.1f);
                    group.alpha = k;
                    to.anchoredPosition = new Vector2(0, -60f * (1 - k));
                    yield return null;
                }
                group.alpha = 1;
                to.anchoredPosition = Vector2.zero;
                yield break;
            }
            Sfx.Play(Sfx.Id.Whoosh, 0.35f, 0.85f);
            for (float t = 0; t < 0.65f; t += Time.deltaTime)
            {
                float k = EaseInOutCubic(t / 0.65f);
                if (from) from.anchoredPosition = new Vector2(0, h * k);
                to.anchoredPosition = new Vector2(0, -h * (1 - k));
                yield return null;
            }
            if (from) from.anchoredPosition = new Vector2(0, h);
            to.anchoredPosition = Vector2.zero;
        }

        /// <summary>Slow push-in over the whole page so the still drawing never feels static.</summary>
        static void Drift(RectTransform zoom, ref float t, float length)
        {
            t += Time.deltaTime;
            if (!zoom) return;
            float k = Mathf.Clamp01(t / length);
            zoom.localScale = Vector3.one * Mathf.Lerp(1f, 1.085f, k * (2 - k)); // ease-out, never stops dead
        }

        float EstimateLength(Panel panel, int p)
        {
            float total = 0;
            for (int b = 0; b < panel.beats.Length; b++)
            {
                var voice = Resources.Load<AudioClip>($"Story/vo_{p + 1}_{b + 1}");
                int wordCount = Tokenise(panel.beats[b].text).Count;
                total += voice ? voice.length + 0.6f : 1.5f + 0.41f * wordCount + panel.beats[b].extraHold;
            }
            return Mathf.Max(4f, total);
        }

        struct Word
        {
            public string text;
            public string hex;
        }

        static List<Word> Tokenise(string line)
        {
            var list = new List<Word>();
            string tint = null;
            foreach (var raw in line.Split(' '))
            {
                var w = raw;
                if (w.Length > 3 && w[0] == '[' && w[2] == ']')
                {
                    Tints.TryGetValue(w[1], out tint);
                    w = w.Substring(3);
                }
                bool close = w.EndsWith("[]");
                if (close) w = w.Substring(0, w.Length - 2);
                list.Add(new Word { text = w, hex = tint });
                if (close) tint = null;
            }
            return list;
        }

        string Compose(List<Word> words, float t, float interval)
        {
            bool white = currentWhite; // the closing black page uses white text, so tinted names are lightened there
            var sb = new StringBuilder();
            for (int i = 0; i < words.Count; i++)
            {
                float a = Mathf.Clamp01((t - i * interval) / WordFade);
                string hex = words[i].hex ?? (white ? "F4EFE6" : InkHex);
                if (white && words[i].hex != null) hex = Lighten(hex);
                if (i > 0) sb.Append(' ');
                sb.Append("<color=#").Append(hex).Append(((int)(a * 255)).ToString("X2")).Append('>').Append(words[i].text).Append("</color>");
            }
            return sb.ToString();
        }

        AudioSource VoiceSource
        {
            get
            {
                if (!voiceSource)
                {
                    voiceSource = gameObject.AddComponent<AudioSource>();
                    voiceSource.spatialBlend = 0;
                    voiceSource.playOnAwake = false;
                }
                return voiceSource;
            }
        }

        void SetHintColour(bool onDrawing, bool lightText)
        {
            currentWhite = lightText;
            hintText.color = onDrawing && !lightText ? new Color(0.17f, 0.1f, 0.07f, 0.5f) : new Color(1, 1, 1, 0.45f);
        }

        static string Lighten(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out var c);
            c = Color.Lerp(c, Color.white, 0.45f);
            return ColorUtility.ToHtmlStringRGB(c);
        }

        Text MakeText(RectTransform rt, int size, TextAnchor align)
        {
            var text = rt.gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.alignment = align;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.supportRichText = true;
            text.raycastTarget = false;
            text.color = Color.white;
            return text;
        }

        static float EaseInOutCubic(float x) => x < 0.5f ? 4 * x * x * x : 1 - Mathf.Pow(-2 * x + 2, 3) / 2;

        void BeginLoading()
        {
            loading = SceneManager.LoadSceneAsync(nextScene);
            if (loading != null) loading.allowSceneActivation = false;
        }

        IEnumerator Finish(bool skipped)
        {
            // fade to black; Chapter 1 opens on a black title card, so the scene cut is invisible
            var black = UIKit.Image(UIKit.Stretch("FadeOut", root), new Color(0, 0, 0, 0));
            black.raycastTarget = true;
            float duration = skipped ? 0.35f : 0.8f;
            for (float t = 0; t < duration; t += Time.deltaTime)
            {
                black.color = new Color(0, 0, 0, t / duration);
                yield return null;
            }
            black.color = Color.black;
            if (loading == null) BeginLoading();
            yield return new WaitForSeconds(0.15f);
            if (loading != null) loading.allowSceneActivation = true;
            else SceneManager.LoadScene(nextScene);
        }
    }
}
