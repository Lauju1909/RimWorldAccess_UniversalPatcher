using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Verse;
using UnityEngine;
using RimWorldAccess;

namespace RimWorldAccess_UniversalPatcher
{
    public class AccessibleModSettings : Window
    {
        private List<Mod> modsWithSettings = new List<Mod>();
        private int selectedIndex = 0;

        public override Vector2 InitialSize => new Vector2(500f, 650f);

        public AccessibleModSettings()
        {
            this.forcePause = false;
            this.doCloseX = true;
            this.closeOnClickedOutside = true;
            this.absorbInputAroundWindow = true;
            this.doWindowBackground = true;
            this.draggable = true;
            this.closeOnAccept = false;


            PopulateMods();

            TolkHelper.Speak(L10n.Get(
                $"Mod-Einstellungen geöffnet. {modsWithSettings.Count} Mods verfügbar.",
                $"Mod settings opened. {modsWithSettings.Count} mods available."
            ), RimWorldAccess.SpeechPriority.High);
        }

        private void PopulateMods()
        {
            modsWithSettings.Clear();
            foreach (var mod in LoadedModManager.ModHandles)
            {
                if (!string.IsNullOrEmpty(mod.SettingsCategory()))
                {
                    modsWithSettings.Add(mod);
                }
            }
            modsWithSettings = modsWithSettings.OrderBy(m => m.SettingsCategory()).ToList();
        }

        private void SpeakCurrentElement()
        {
            if (modsWithSettings.Count == 0) return;
            var mod = modsWithSettings[selectedIndex];
            string translatedCategory = TranslationEngine.Translate(mod.SettingsCategory());
            string msg = L10n.Get(
                $"{translatedCategory}. Knopf. Element {selectedIndex + 1} von {modsWithSettings.Count}.",
                $"{mod.SettingsCategory()}. Button. Item {selectedIndex + 1} of {modsWithSettings.Count}."
            );
            TolkHelper.Speak(msg, RimWorldAccess.SpeechPriority.Normal);
        }

        public override void DoWindowContents(Rect inRect)
        {
            if (Event.current != null && Event.current.type == EventType.KeyDown)
            {
                KeyCode key = Event.current.keyCode;

                if (key == KeyCode.DownArrow || key == KeyCode.Tab)
                {
                    if (selectedIndex >= modsWithSettings.Count - 1)
                    {
                        BumpSound.PlayBump();
                        TolkHelper.Speak(L10n.Get("Listenende.", "End of list."), RimWorldAccess.SpeechPriority.High);
                    }
                    else
                    {
                        selectedIndex++;
                        BumpSound.PlaySelect();
                        SpeakCurrentElement();
                    }
                    Event.current.Use();
                }
                else if (key == KeyCode.UpArrow)
                {
                    if (selectedIndex <= 0)
                    {
                        BumpSound.PlayBump();
                        TolkHelper.Speak(L10n.Get("Listenanfang.", "Beginning of list."), RimWorldAccess.SpeechPriority.High);
                    }
                    else
                    {
                        selectedIndex--;
                        BumpSound.PlaySelect();
                        SpeakCurrentElement();
                    }
                    Event.current.Use();
                }
                else if (key == KeyCode.Return || key == KeyCode.KeypadEnter)
                {
                    if (modsWithSettings.Count > 0)
                    {
                        var mod = modsWithSettings[selectedIndex];
                        BumpSound.PlaySelect();
                        string translatedCategory = TranslationEngine.Translate(mod.SettingsCategory());
                        TolkHelper.Speak(L10n.Get(
                            $"{translatedCategory} geöffnet.",
                            $"{mod.SettingsCategory()} opened."
                        ), RimWorldAccess.SpeechPriority.High);
                        
                        // Open the specific mod settings window
                        Find.WindowStack.Add(new AccessibleModSettingsDetail(mod));
                    }
                    Event.current.Use();
                }
            }
        }
    }

    public class AccessibleModSettingsDetail : Window
    {
        private Mod mod;
        private int selectedIndex = 0;
        private List<UIElement> capturedElements = new List<UIElement>();
        private bool initialized = false;

        public override Vector2 InitialSize => new Vector2(500f, 650f);

        public AccessibleModSettingsDetail(Mod mod)
        {
            this.mod = mod;
            this.forcePause = false;
            this.doCloseX = true;
            this.closeOnClickedOutside = true;
            this.absorbInputAroundWindow = true;
            this.doWindowBackground = true;
            this.draggable = true;
            this.closeOnAccept = false;
        }

        private void SpeakCurrentElement()
        {
            if (capturedElements.Count == 0) return;

            var el = capturedElements[selectedIndex];
            string typeName;

            if (el.Type == "Button" || el.Type == "FloatMenuOption" || el.Type == "ButtonImage")
                typeName = L10n.Get("Knopf", "Button");
            else if (el.Type == "Checkbox")
                typeName = L10n.Get(el.IsChecked ? "Checkbox, Aktiviert" : "Checkbox, Deaktiviert", el.IsChecked ? "Checkbox, Checked" : "Checkbox, Unchecked");
            else if (el.Type == "TextField")
                typeName = L10n.Get("Textfeld. Wert: " + el.TextValue, "Text field. Value: " + el.TextValue);
            else if (el.Type == "Slider")
                typeName = L10n.Get("Schieberegler. Wert: " + Math.Round(el.CurrentValue, 2), "Slider. Value: " + Math.Round(el.CurrentValue, 2));
            else
                typeName = L10n.Get("Text", "Text");

            string msg = L10n.Get(
                $"{el.Text}. {typeName}. Element {selectedIndex + 1} von {capturedElements.Count}.",
                $"{el.Text}. {typeName}. Item {selectedIndex + 1} of {capturedElements.Count}."
            );

            TolkHelper.Speak(msg, RimWorldAccess.SpeechPriority.Normal);
        }

        public override void DoWindowContents(Rect inRect)
        {
            // First time or when needing to repaint/capture
            if (Event.current.type == EventType.Repaint)
            {
                UniversalAccessState.ElementsCurrentFrame.Clear();

                // Draw the mod settings invisibly to capture elements
                GUI.color = new Color(0, 0, 0, 0); // Transparent
                mod.DoSettingsWindowContents(inRect);
                GUI.color = Color.white;

                capturedElements = new List<UIElement>(UniversalAccessState.ElementsCurrentFrame);

                if (!initialized)
                {
                    initialized = true;
                    string translatedCategory = TranslationEngine.Translate(mod.SettingsCategory());
                    TolkHelper.Speak(L10n.Get(
                        $"{translatedCategory} geladen. {capturedElements.Count} Elemente gefunden.",
                        $"{mod.SettingsCategory()} loaded. {capturedElements.Count} elements found."
                    ), RimWorldAccess.SpeechPriority.High);
                    if (capturedElements.Count > 0)
                        SpeakCurrentElement();
                }
            }

            // Also draw them again if we need to simulate a click
            if (Event.current.type != EventType.Repaint && UniversalAccessState.PendingClickTarget.HasValue)
            {
                GUI.color = new Color(0, 0, 0, 0);
                mod.DoSettingsWindowContents(inRect);
                GUI.color = Color.white;
                // Pending click is cleared by the patches
            }
            if (Event.current.type != EventType.Repaint && UniversalAccessState.PendingSliderTarget.HasValue)
            {
                GUI.color = new Color(0, 0, 0, 0);
                mod.DoSettingsWindowContents(inRect);
                GUI.color = Color.white;
            }

            if (Event.current != null && Event.current.type == EventType.KeyDown)
            {
                KeyCode key = Event.current.keyCode;

                if (key == KeyCode.DownArrow || key == KeyCode.Tab)
                {
                    if (capturedElements.Count == 0) return;
                    if (selectedIndex >= capturedElements.Count - 1)
                    {
                        BumpSound.PlayBump();
                        TolkHelper.Speak(L10n.Get("Listenende.", "End of list."), RimWorldAccess.SpeechPriority.High);
                    }
                    else
                    {
                        selectedIndex++;
                        BumpSound.PlaySelect();
                        SpeakCurrentElement();
                    }
                    Event.current.Use();
                }
                else if (key == KeyCode.UpArrow)
                {
                    if (capturedElements.Count == 0) return;
                    if (selectedIndex <= 0)
                    {
                        BumpSound.PlayBump();
                        TolkHelper.Speak(L10n.Get("Listenanfang.", "Beginning of list."), RimWorldAccess.SpeechPriority.High);
                    }
                    else
                    {
                        selectedIndex--;
                        BumpSound.PlaySelect();
                        SpeakCurrentElement();
                    }
                    Event.current.Use();
                }
                else if (key == KeyCode.LeftArrow)
                {
                    if (capturedElements.Count > 0)
                    {
                        var el = capturedElements[selectedIndex];
                        if (el.Type == "Slider")
                        {
                            el.CurrentValue -= 0.1f;
                            UniversalAccessState.PendingSliderTarget = el;
                            TolkHelper.Speak(L10n.Get("Wert verringert", "Value decreased"), RimWorldAccess.SpeechPriority.High);
                        }
                        else if (el.Type == "IntRange")
                        {
                            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                                el.IntMax -= 1;
                            else
                                el.IntMin -= 1;
                            UniversalAccessState.PendingSliderTarget = el;
                            TolkHelper.Speak(L10n.Get($"Minimum {el.IntMin}, Maximum {el.IntMax}", $"Minimum {el.IntMin}, Maximum {el.IntMax}"), RimWorldAccess.SpeechPriority.High);
                        }
                    }
                    Event.current.Use();
                }
                else if (key == KeyCode.RightArrow)
                {
                    if (capturedElements.Count > 0)
                    {
                        var el = capturedElements[selectedIndex];
                        if (el.Type == "Slider")
                        {
                            el.CurrentValue += 0.1f;
                            UniversalAccessState.PendingSliderTarget = el;
                            TolkHelper.Speak(L10n.Get("Wert erhöht", "Value increased"), RimWorldAccess.SpeechPriority.High);
                        }
                        else if (el.Type == "IntRange")
                        {
                            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                                el.IntMax += 1;
                            else
                                el.IntMin += 1;
                            UniversalAccessState.PendingSliderTarget = el;
                            TolkHelper.Speak(L10n.Get($"Minimum {el.IntMin}, Maximum {el.IntMax}", $"Minimum {el.IntMin}, Maximum {el.IntMax}"), RimWorldAccess.SpeechPriority.High);
                        }
                    }
                    Event.current.Use();
                }
                else if (key == KeyCode.Return || key == KeyCode.KeypadEnter)
                {
                    if (capturedElements.Count > 0)
                    {
                        var el = capturedElements[selectedIndex];
                        if (el.Type == "Button" || el.Type == "Checkbox" || el.Type == "TextField" || el.Type == "ColorSelector")
                        {
                            BumpSound.PlaySelect();
                            UniversalAccessState.PendingClickTarget = el;
                            if (el.Type == "ColorSelector")
                                TolkHelper.Speak(L10n.Get("Nächste Farbe ausgewählt.", "Next color selected."), RimWorldAccess.SpeechPriority.High);
                            else
                                TolkHelper.Speak(L10n.Get("Ausgeführt.", "Executed."), RimWorldAccess.SpeechPriority.High);
                        }
                        else if (el.Type == "FloatMenuOption")
                        {
                            BumpSound.PlaySelect();
                            if (el.MenuOption != null && !el.MenuOption.Disabled)
                            {
                                el.MenuOption.action?.Invoke();
                            }
                            TolkHelper.Speak(L10n.Get("Ausgewählt.", "Selected."), RimWorldAccess.SpeechPriority.High);
                        }
                        else
                        {
                            TolkHelper.Speak(L10n.Get("Kann nicht gedrückt werden.", "Cannot be pressed."), RimWorldAccess.SpeechPriority.Normal);
                        }
                    }
                    Event.current.Use();
                }
            }
        }
    }
}
