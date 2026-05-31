// =============================================================================
// RimWorldAccess Universal Patcher
// Vollständig blind-zugänglich | Fully Blind-Accessible
// Version 2.0 - Barrierefreie Version mit Bump-Sounds, vollständiger
//               Tastaturnavigation (Pfeiltasten, Enter, Escape, Tab)
//               und akustischem Feedback an Menügrenzen.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using HarmonyLib;
using Verse;
using UnityEngine;
using RimWorldAccess;

namespace RimWorldAccess_UniversalPatcher
{
    // =========================================================================
    // SPRACHHELFER - Gibt Texte auf Deutsch oder Englisch zurück je nach
    //               Spracheinstellung des Spielers
    // LANGUAGE HELPER - Returns text in German or English based on game language
    // =========================================================================
    public static class L10n
    {
        private static bool? _isGerman = null;

        public static bool IsGerman
        {
            get
            {
                if (!_isGerman.HasValue)
                {
                    try
                    {
                        string lang = LanguageDatabase.activeLanguage?.folderName ?? "";
                        _isGerman = lang.Contains("German") || lang.Contains("Deutsch");
                    }
                    catch
                    {
                        _isGerman = false;
                    }
                }
                return _isGerman.Value;
            }
        }

        public static string Get(string german, string english)
        {
            return IsGerman ? german : english;
        }
    }

    // =========================================================================
    // BUMP-SOUND HELPER - Erzeugt akustisches Feedback an Menügrenzen
    // BUMP-SOUND HELPER - Produces acoustic feedback at menu boundaries
    // =========================================================================
    public static class BumpSound
    {
        // Versuche, einen RimWorld-Sound zu spielen, der als "Bump" klingt
        // Try to play a RimWorld sound that acts as a "bump" indicator
        public static void PlayBump()
        {
            try
            {
                // RimWorld hat eingebaute Sounds für UI-Feedback
                // Wir nutzen "ClickReject" als Bump-Sound für Grenzen
                SoundDef bumpSnd = SoundDef.Named("ClickReject");
                if (bumpSnd != null)
                {
                    bumpSnd.PlayOneShotOnCamera(null);
                }
            }
            catch
            {
                // Kein Absturz bei fehlendem Sound / No crash on missing sound
            }
        }

        public static void PlaySelect()
        {
            try
            {
                SoundDef selectSnd = SoundDef.Named("Click");
                if (selectSnd != null)
                {
                    selectSnd.PlayOneShotOnCamera(null);
                }
            }
            catch { }
        }

        public static void PlayOpen()
        {
            try
            {
                SoundDef openSnd = SoundDef.Named("DialogBoxAppear");
                if (openSnd != null)
                {
                    openSnd.PlayOneShotOnCamera(null);
                }
            }
            catch { }
        }

        public static void PlayClose()
        {
            try
            {
                SoundDef closeSnd = SoundDef.Named("Click");
                if (closeSnd != null)
                {
                    closeSnd.PlayOneShotOnCamera(null);
                }
            }
            catch { }
        }
    }

    // =========================================================================
    // LIVE-ÜBERSETZUNG (optional, asynchron / optional, asynchronous)
    // =========================================================================
    public static class TranslationEngine
    {
        private static Dictionary<string, string> cache = new Dictionary<string, string>();
        private static HashSet<string> pending = new HashSet<string>();
        private static string cacheFilePath;

        public static void Init()
        {
            try
            {
                cacheFilePath = Path.Combine(Application.persistentDataPath, "UniversalPatcherTranslationCache.txt");
                if (File.Exists(cacheFilePath))
                {
                    var lines = File.ReadAllLines(cacheFilePath);
                    foreach (var line in lines)
                    {
                        var parts = line.Split(new string[] { "|||" }, StringSplitOptions.None);
                        if (parts.Length == 2) cache[parts[0]] = parts[1];
                    }
                    Log.Message("[UniversalAccessPatcher] Translation cache loaded: " + cache.Count + " entries.");
                }
            }
            catch (Exception e)
            {
                Log.Warning("[UniversalAccessPatcher] Could not load translation cache: " + e.Message);
            }
        }

        private static void SaveCache()
        {
            try
            {
                var lines = new List<string>();
                foreach (var kvp in cache)
                    lines.Add(kvp.Key + "|||" + kvp.Value);
                File.WriteAllLines(cacheFilePath, lines.ToArray());
            }
            catch { }
        }

        public static string Translate(string original)
        {
            if (string.IsNullOrEmpty(original) || original.Length <= 1) return original;

            // Nur übersetzen wenn Spiel auf Deutsch eingestellt ist
            // Only translate if game is set to German
            if (!L10n.IsGerman) return original;

            bool hasLetters = false;
            foreach (char c in original)
                if (char.IsLetter(c)) { hasLetters = true; break; }
            if (!hasLetters) return original;

            lock (cache)
            {
                if (cache.ContainsKey(original))
                    return cache[original];

                if (!pending.Contains(original))
                {
                    pending.Add(original);
                    ThreadPool.QueueUserWorkItem(state => DoTranslation(original));
                }
            }

            return original;
        }

        private static void DoTranslation(string text)
        {
            try
            {
                string url = "https://translate.googleapis.com/translate_a/single?client=gtx&sl=en&tl=de&dt=t&q=" + Uri.EscapeDataString(text);
                using (var wc = new WebClient())
                {
                    wc.Encoding = System.Text.Encoding.UTF8;
                    string json = wc.DownloadString(url);
                    int start = json.IndexOf("[\"") + 2;
                    int end = json.IndexOf("\",\"", start);
                    if (start > 1 && end > start)
                    {
                        string de = json.Substring(start, end - start);
                        de = de.Replace("\\\"", "\"").Replace("\\n", "\n")
                               .Replace("\\u003c", "<").Replace("\\u003e", ">");

                        lock (cache)
                        {
                            cache[text] = de;
                            pending.Remove(text);
                            SaveCache();
                        }
                    }
                }
            }
            catch
            {
                lock (cache) { pending.Remove(text); }
            }
        }
    }

    // =========================================================================
    // UI-ELEMENT - Repräsentiert ein erkanntes UI-Element
    // UI-ELEMENT - Represents a detected UI element
    // =========================================================================
    public struct UIElement
    {
        public string Text;
        public Rect Rect;
        public string Type; // "Button" oder "Label"
    }

    // =========================================================================
    // GLOBALER ZUSTAND - Speichert alle gefundenen UI-Elemente
    // GLOBAL STATE - Stores all found UI elements
    // =========================================================================
    public static class UniversalAccessState
    {
        public static List<UIElement> ElementsCurrentFrame = new List<UIElement>();
        public static List<UIElement> ElementsLastFrame = new List<UIElement>();
        public static UIElement? PendingClickTarget = null;
    }

    // =========================================================================
    // PATCHER-EINSTIEG - Wird beim Mod-Start aufgerufen
    // PATCHER ENTRY - Called at mod startup
    // =========================================================================
    [StaticConstructorOnStartup]
    public static class Patcher
    {
        static Patcher()
        {
            Log.Message("[UniversalAccessPatcher] Initializing v2.0 Accessibility Patcher...");
            TranslationEngine.Init();
            var harmony = new Harmony("com.tachyon.universalaccesspatcher");
            harmony.PatchAll();
            Log.Message("[UniversalAccessPatcher] All patches applied. Press F11 to open the accessibility menu.");
            Log.Message("[UniversalAccessPatcher] Navigation: Arrow Keys = move, Enter = activate, Escape = close.");
        }
    }

    // =========================================================================
    // ROOT UPDATE PATCH - Fängt F11-Taste im Update-Loop ab
    // ROOT UPDATE PATCH - Catches F11 key in the update loop
    // =========================================================================
    [HarmonyPatch(typeof(Root), "Update")]
    public static class Root_Update_Patch
    {
        public static void Postfix()
        {
            if (Input.GetKeyDown(KeyCode.F11))
            {
                if (Find.WindowStack != null)
                {
                    // Prüfe ob bereits ein Zugänglichkeitsmenü offen ist
                    // Check if an accessibility menu is already open
                    foreach (Window w in Find.WindowStack.Windows)
                    {
                        if (w is UniversalAccessMenu)
                        {
                            w.Close();
                            return;
                        }
                    }

                    List<UIElement> validElements = new List<UIElement>();
                    foreach (var el in UniversalAccessState.ElementsLastFrame)
                    {
                        if (!string.IsNullOrEmpty(el.Text) && el.Text.Trim().Length > 0)
                            validElements.Add(el);
                    }

                    if (validElements.Count > 0)
                    {
                        BumpSound.PlayOpen();
                        Find.WindowStack.Add(new UniversalAccessMenu(validElements));
                    }
                    else
                    {
                        TolkHelper.Speak(
                            L10n.Get(
                                "Keine klickbaren Elemente auf diesem Bildschirm gefunden.",
                                "No clickable elements found on this screen."
                            ),
                            SpeechPriority.High
                        );
                    }
                }
            }
        }
    }

    // =========================================================================
    // UIROOT PATCH - Verwaltet den Frame-Wechsel der UI-Element-Liste
    // UIROOT PATCH - Manages frame switching of the UI element list
    // =========================================================================
    [HarmonyPatch(typeof(UIRoot), "UIRootOnGUI")]
    public static class UIRoot_Patch
    {
        public static void Prefix()
        {
            if (Event.current != null && Event.current.type == EventType.Repaint)
            {
                UniversalAccessState.ElementsLastFrame = new List<UIElement>(UniversalAccessState.ElementsCurrentFrame);
                UniversalAccessState.ElementsCurrentFrame.Clear();
            }
        }
    }

    // =========================================================================
    // BUTTON PATCH - Fängt alle Buttons ab und registriert sie
    // BUTTON PATCH - Intercepts all buttons and registers them
    // =========================================================================
    [HarmonyPatch(typeof(Widgets), "ButtonText", new Type[] { typeof(Rect), typeof(string), typeof(bool), typeof(bool), typeof(bool), typeof(TextAnchor?) })]
    public static class Widgets_ButtonText_Patch
    {
        public static bool Prefix(Rect rect, ref string label, ref bool __result)
        {
            // Übersetze den Button-Text falls nötig
            // Translate button text if needed
            label = TranslationEngine.Translate(label);

            // Simuliere einen Klick falls dieser Button das Ziel ist
            // Simulate a click if this button is the target
            if (UniversalAccessState.PendingClickTarget.HasValue)
            {
                var target = UniversalAccessState.PendingClickTarget.Value;
                if (target.Type == "Button" && target.Text == label && target.Rect == rect)
                {
                    UniversalAccessState.PendingClickTarget = null;
                    __result = true;
                    return false;
                }
            }
            return true;
        }

        public static void Postfix(Rect rect, string label)
        {
            if (Event.current != null && Event.current.type == EventType.Repaint)
            {
                UniversalAccessState.ElementsCurrentFrame.Add(new UIElement
                {
                    Text = label,
                    Rect = rect,
                    Type = "Button"
                });
            }
        }
    }

    // =========================================================================
    // LABEL PATCH - Fängt alle Labels ab und registriert sie
    // LABEL PATCH - Intercepts all labels and registers them
    // =========================================================================
    [HarmonyPatch(typeof(Widgets), "Label", new Type[] { typeof(Rect), typeof(string) })]
    public static class Widgets_Label_Patch
    {
        public static void Prefix(Rect rect, ref string label)
        {
            label = TranslationEngine.Translate(label);
        }

        public static void Postfix(Rect rect, string label)
        {
            if (Event.current != null && Event.current.type == EventType.Repaint)
            {
                UniversalAccessState.ElementsCurrentFrame.Add(new UIElement
                {
                    Text = label,
                    Rect = rect,
                    Type = "Label"
                });
            }
        }
    }

    // =========================================================================
    // UNIVERSELLES ZUGÄNGLICHKEITSMENÜ
    // UNIVERSAL ACCESSIBILITY MENU
    //
    // Navigation:
    //   Pfeil Unten / Down Arrow = nächstes Element / next element
    //   Pfeil Oben  / Up Arrow   = vorheriges Element / previous element
    //   Tab                      = nächstes Element (wie Pfeil Unten)
    //   Enter / Return           = Element aktivieren / activate element
    //   Escape                   = Menü schließen / close menu
    //   F11                      = Menü schließen (Toggle)
    //
    // Akustisches Feedback / Acoustic Feedback:
    //   - Bump-Sound an Listenende und Listenanfang
    //   - Öffnen/Schließen Sound
    //   - Klick-Sound bei Aktivierung
    // =========================================================================
    public class UniversalAccessMenu : Window
    {
        private List<UIElement> elements;
        private int selectedIndex = 0;
        private float scrollPosition = 0f;
        private const float RowHeight = 28f;
        private const float ScrollPadding = 10f;

        public override Vector2 InitialSize => new Vector2(500f, 650f);

        // Fenstereigenschaften / Window properties
        public UniversalAccessMenu(List<UIElement> elements)
        {
            this.elements = elements;
            this.forcePause = false;          // Spiel läuft weiter / Game continues
            this.doCloseX = true;             // X-Knopf zum Schließen / X button to close
            this.closeOnClickedOutside = true; // Klick außen schließt / Click outside closes
            this.absorbInputAroundWindow = true;
            this.doWindowBackground = true;
            this.draggable = true;

            if (this.elements.Count > 0)
            {
                // Beim Öffnen: Gesamtanzahl und erstes Element ansagen
                // On open: announce total count and first element
                string openMsg = L10n.Get(
                    string.Format("Zugänglichkeitsmenü geöffnet. {0} Elemente. ", elements.Count),
                    string.Format("Accessibility menu opened. {0} elements. ", elements.Count)
                );
                TolkHelper.Speak(openMsg, SpeechPriority.High);

                // Kleinen Delay dann erstes Element ansagen
                // Small delay then announce first element
                SpeakCurrentElement();
            }
            else
            {
                TolkHelper.Speak(
                    L10n.Get("Keine Elemente gefunden.", "No elements found."),
                    SpeechPriority.High
                );
            }
        }

        // Spricht das aktuelle Element an / Speaks the current element
        private void SpeakCurrentElement()
        {
            if (elements.Count == 0) return;

            var el = elements[selectedIndex];
            string typeName;

            if (el.Type == "Button")
                typeName = L10n.Get("Knopf", "Button");
            else
                typeName = L10n.Get("Text", "Text");

            string msg = L10n.Get(
                string.Format("{0}. {1}. Element {2} von {3}.", el.Text, typeName, selectedIndex + 1, elements.Count),
                string.Format("{0}. {1}. Item {2} of {3}.", el.Text, typeName, selectedIndex + 1, elements.Count)
            );

            TolkHelper.Speak(msg, SpeechPriority.Normal);
        }

        // Spricht eine Grenz-Ansage und spielt den Bump-Sound
        // Speaks a boundary announcement and plays the bump sound
        private void SpeakBoundary(bool isTop)
        {
            BumpSound.PlayBump();
            string msg;
            if (isTop)
                msg = L10n.Get("Listenanfang.", "Beginning of list.");
            else
                msg = L10n.Get("Listenende.", "End of list.");

            TolkHelper.Speak(msg, SpeechPriority.High);
        }

        public override void DoWindowContents(Rect inRect)
        {
            // ---------------------------------------------------------------
            // TASTATURVERARBEITUNG / KEYBOARD PROCESSING
            // ---------------------------------------------------------------
            if (Event.current != null && Event.current.type == EventType.KeyDown)
            {
                KeyCode key = Event.current.keyCode;

                // Pfeil Unten / Tab = Nächstes Element
                if (key == KeyCode.DownArrow || key == KeyCode.Tab)
                {
                    if (elements.Count == 0) { Event.current.Use(); return; }

                    if (selectedIndex >= elements.Count - 1)
                    {
                        // Am Ende der Liste: Bump-Sound und Ansage
                        // At end of list: bump sound and announcement
                        SpeakBoundary(false);
                    }
                    else
                    {
                        selectedIndex++;
                        EnsureVisible();
                        BumpSound.PlaySelect();
                        SpeakCurrentElement();
                    }
                    Event.current.Use();
                }
                // Pfeil Oben = Vorheriges Element
                else if (key == KeyCode.UpArrow)
                {
                    if (elements.Count == 0) { Event.current.Use(); return; }

                    if (selectedIndex <= 0)
                    {
                        // Am Anfang der Liste: Bump-Sound und Ansage
                        // At beginning of list: bump sound and announcement
                        SpeakBoundary(true);
                    }
                    else
                    {
                        selectedIndex--;
                        EnsureVisible();
                        BumpSound.PlaySelect();
                        SpeakCurrentElement();
                    }
                    Event.current.Use();
                }
                // Pos1/Home = Erstes Element
                else if (key == KeyCode.Home)
                {
                    if (elements.Count > 0)
                    {
                        selectedIndex = 0;
                        scrollPosition = 0f;
                        BumpSound.PlaySelect();
                        SpeakCurrentElement();
                    }
                    Event.current.Use();
                }
                // Ende/End = Letztes Element
                else if (key == KeyCode.End)
                {
                    if (elements.Count > 0)
                    {
                        selectedIndex = elements.Count - 1;
                        EnsureVisible();
                        BumpSound.PlaySelect();
                        SpeakCurrentElement();
                    }
                    Event.current.Use();
                }
                // Enter/Return = Element aktivieren
                else if (key == KeyCode.Return || key == KeyCode.KeypadEnter)
                {
                    if (elements.Count > 0)
                    {
                        var el = elements[selectedIndex];
                        if (el.Type == "Button")
                        {
                            BumpSound.PlaySelect();
                            TolkHelper.Speak(
                                L10n.Get(
                                    string.Format("{0} wird gedrückt.", el.Text),
                                    string.Format("Pressing {0}.", el.Text)
                                ),
                                SpeechPriority.High
                            );
                            UniversalAccessState.PendingClickTarget = el;
                            this.Close();
                        }
                        else
                        {
                            // Labels können nicht aktiviert werden
                            // Labels cannot be activated
                            TolkHelper.Speak(
                                L10n.Get(
                                    string.Format("{0} ist ein Text-Element und kann nicht gedrückt werden.", el.Text),
                                    string.Format("{0} is a text element and cannot be activated.", el.Text)
                                ),
                                SpeechPriority.Normal
                            );
                        }
                    }
                    Event.current.Use();
                }
                // Leertaste = Element aktivieren (alternativ zu Enter)
                else if (key == KeyCode.Space)
                {
                    if (elements.Count > 0)
                    {
                        var el = elements[selectedIndex];
                        if (el.Type == "Button")
                        {
                            BumpSound.PlaySelect();
                            TolkHelper.Speak(
                                L10n.Get(
                                    string.Format("{0} wird gedrückt.", el.Text),
                                    string.Format("Pressing {0}.", el.Text)
                                ),
                                SpeechPriority.High
                            );
                            UniversalAccessState.PendingClickTarget = el;
                            this.Close();
                        }
                    }
                    Event.current.Use();
                }
                // Escape = Menü schließen
                else if (key == KeyCode.Escape)
                {
                    BumpSound.PlayClose();
                    TolkHelper.Speak(
                        L10n.Get("Zugänglichkeitsmenü geschlossen.", "Accessibility menu closed."),
                        SpeechPriority.High
                    );
                    this.Close();
                    Event.current.Use();
                }
            }

            // ---------------------------------------------------------------
            // VISUELLE DARSTELLUNG (auch für sehende Helfer)
            // VISUAL DISPLAY (also for sighted helpers)
            // ---------------------------------------------------------------
            DrawHeader(inRect);
            DrawElementList(inRect);
            DrawFooter(inRect);
        }

        private void DrawHeader(Rect inRect)
        {
            // Titelzeile / Title row
            Text.Font = GameFont.Medium;
            string title = L10n.Get(
                string.Format("Zugänglichkeitsmenü ({0} Elemente)", elements.Count),
                string.Format("Accessibility Menu ({0} elements)", elements.Count)
            );
            Rect titleRect = new Rect(0, 0, inRect.width, 36f);
            Widgets.Label(titleRect, title);
            Text.Font = GameFont.Small;

            // Trennlinie / Separator
            Widgets.DrawLineHorizontal(0, 38f, inRect.width);
        }

        private void DrawElementList(Rect inRect)
        {
            float listTop = 44f;
            float listBottom = inRect.height - 44f;
            float listHeight = listBottom - listTop;
            float totalContentHeight = elements.Count * RowHeight;

            Rect listRect = new Rect(0, listTop, inRect.width, listHeight);
            Rect contentRect = new Rect(0, 0, inRect.width - 20f, totalContentHeight);

            // Scrollbereich / Scroll area
            Widgets.BeginScrollView(listRect, ref scrollPosition, contentRect);

            for (int i = 0; i < elements.Count; i++)
            {
                float yPos = i * RowHeight;
                Rect rowRect = new Rect(0, yPos, contentRect.width, RowHeight - 2f);

                // Hintergrundfarbe für ausgewähltes Element
                // Background color for selected element
                if (i == selectedIndex)
                {
                    // Helles Blau für ausgewähltes Element
                    Widgets.DrawBoxSolid(rowRect, new Color(0.2f, 0.4f, 0.8f, 0.5f));
                }
                else if (i % 2 == 0)
                {
                    // Leichter Zebra-Hintergrund / Light zebra background
                    Widgets.DrawBoxSolid(rowRect, new Color(1f, 1f, 1f, 0.03f));
                }

                // Symbol für den Typ / Symbol for the type
                string typeSymbol = elements[i].Type == "Button" ? "[>]" : "[ ]";
                string displayText = string.Format("{0} {1}", typeSymbol, elements[i].Text);

                // Schriftfarbe / Font color
                Color prevColor = GUI.color;
                if (i == selectedIndex)
                    GUI.color = Color.white;
                else if (elements[i].Type == "Button")
                    GUI.color = new Color(0.9f, 0.9f, 1f);
                else
                    GUI.color = new Color(0.75f, 0.75f, 0.75f);

                // Text rendern (ACHTUNG: Dies würde Widgets.Label-Patch triggern,
                // daher nutzen wir GUI.Label direkt)
                GUI.Label(rowRect, displayText);
                GUI.color = prevColor;
            }

            Widgets.EndScrollView();
        }

        private void DrawFooter(Rect inRect)
        {
            // Trennlinie / Separator
            Widgets.DrawLineHorizontal(0, inRect.height - 42f, inRect.width);

            // Tastenkürzel-Hilfe / Keyboard shortcut help
            Text.Font = GameFont.Tiny;
            Color prevColor = GUI.color;
            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            string helpText = L10n.Get(
                "↑↓ = Navigieren  |  Enter/Leertaste = Aktivieren  |  Escape = Schließen",
                "↑↓ = Navigate  |  Enter/Space = Activate  |  Escape = Close"
            );
            Rect footerRect = new Rect(0, inRect.height - 38f, inRect.width, 20f);
            GUI.Label(footerRect, helpText);
            GUI.color = prevColor;
            Text.Font = GameFont.Small;
        }

        // Stellt sicher dass das ausgewählte Element sichtbar ist
        // Ensures the selected element is visible
        private void EnsureVisible()
        {
            float selectedY = selectedIndex * RowHeight;
            float listHeight = InitialSize.y - 88f; // Abzüglich Header und Footer

            if (selectedY < scrollPosition)
                scrollPosition = selectedY;
            else if (selectedY + RowHeight > scrollPosition + listHeight)
                scrollPosition = selectedY + RowHeight - listHeight;
        }

        // Beim Schließen Meldung ausgeben / Announce on close
        public override void PostClose()
        {
            base.PostClose();
            BumpSound.PlayClose();
        }
    }
}
