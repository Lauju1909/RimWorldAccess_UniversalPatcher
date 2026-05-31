using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Verse;
using UnityEngine;
using LudeonTK; // Typically where DebugActionAttribute lives
using RimWorldAccess;

namespace RimWorldAccess_UniversalPatcher
{
    public class AccessibleDevMenu : Window
    {
        private List<DebugActionDef> actions = new List<DebugActionDef>();
        private List<DebugActionDef> filteredActions = new List<DebugActionDef>();
        private int selectedIndex = 0;
        private string searchText = "";

        public override Vector2 InitialSize => new Vector2(500f, 650f);

        public AccessibleDevMenu()
        {
            this.forcePause = false;
            this.doCloseX = true;
            this.closeOnClickedOutside = true;
            this.absorbInputAroundWindow = true;
            this.doWindowBackground = true;
            this.draggable = true;

            PopulateActions();
            
            TolkHelper.Speak(L10n.Get(
                $"Entwicklermenü geöffnet. {filteredActions.Count} Befehle verfügbar. Suchleiste fokussiert.",
                $"Developer menu opened. {filteredActions.Count} commands available. Search bar focused."
            ), RimWorldAccess.SpeechPriority.High);
        }

        private class DebugActionDef
        {
            public string Category;
            public string Name;
            public Action Action;
            public string TranslatedCategory => TranslationEngine.Translate(Category);
            public string TranslatedName => TranslationEngine.Translate(Name);
            public string DisplayName => $"[{TranslatedCategory}] {TranslatedName}";
        }

        private void PopulateActions()
        {
            actions.Clear();
            
            // RimWorld 1.4/1.5 changed how debug actions are registered, they might be in Dialog_Debug
            // or in a specific DebugAction attribute. Let's find all methods with an attribute named DebugActionAttribute.
            var asm = typeof(Verse.Game).Assembly;
            var methods = asm.GetTypes()
                .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance))
                .Where(m => m.GetCustomAttributes(false).Any(a => a.GetType().Name == "DebugActionAttribute"));

            foreach (var method in methods)
            {
                var attr = method.GetCustomAttributes(false).First(a => a.GetType().Name == "DebugActionAttribute");
                
                // Get properties or fields of the attribute
                var attrType = attr.GetType();
                var nameProp = attrType.GetProperty("name") ?? attrType.GetProperty("Name");
                var nameField = attrType.GetField("name") ?? attrType.GetField("Name");
                string name = (nameProp?.GetValue(attr) as string) ?? (nameField?.GetValue(attr) as string) ?? method.Name;

                var categoryProp = attrType.GetProperty("category") ?? attrType.GetProperty("Category");
                var categoryField = attrType.GetField("category") ?? attrType.GetField("Category");
                string category = (categoryProp?.GetValue(attr) as string) ?? (categoryField?.GetValue(attr) as string) ?? "General";

                actions.Add(new DebugActionDef
                {
                    Category = category,
                    Name = name,
                    Action = () => 
                    {
                        try {
                            if (method.IsStatic) {
                                method.Invoke(null, null);
                            } else {
                                // Some debug actions might need an instance, typically they are static.
                                // We will just ignore instance methods for now or try to create instance if parameterless.
                            }
                        } catch (Exception e) {
                            Log.Error("Error executing debug action: " + e);
                        }
                    }
                });
            }

            // Fallback if the above doesn't work well due to LudeonTK changes:
            // Use Dialog_Debug if available.

            actions = actions.OrderBy(a => a.Category).ThenBy(a => a.Name).ToList();
            filteredActions = new List<DebugActionDef>(actions);
        }

        private void UpdateFilter()
        {
            if (string.IsNullOrEmpty(searchText))
            {
                filteredActions = new List<DebugActionDef>(actions);
            }
            else
            {
                filteredActions = actions.Where(a => a.DisplayName.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            }
            if (selectedIndex > filteredActions.Count) selectedIndex = filteredActions.Count; // +1 for search bar
        }

        private void SpeakCurrentElement()
        {
            if (selectedIndex == 0)
            {
                TolkHelper.Speak(L10n.Get(
                    $"Suchleiste. Wert: {searchText}. Bitte tippen.",
                    $"Search bar. Value: {searchText}. Please type."
                ), RimWorldAccess.SpeechPriority.Normal);
            }
            else
            {
                int actionIndex = selectedIndex - 1;
                if (actionIndex >= 0 && actionIndex < filteredActions.Count)
                {
                    var action = filteredActions[actionIndex];
                    string msg = L10n.Get(
                        $"{action.DisplayName}. Knopf. Element {selectedIndex} von {filteredActions.Count}.",
                        $"[{action.Category}] {action.Name}. Button. Item {selectedIndex} of {filteredActions.Count}."
                    );
                    TolkHelper.Speak(msg, RimWorldAccess.SpeechPriority.Normal);
                }
            }
        }

        public override void DoWindowContents(Rect inRect)
        {
            if (Event.current != null && Event.current.type == EventType.KeyDown)
            {
                KeyCode key = Event.current.keyCode;

                if (key == KeyCode.DownArrow || key == KeyCode.Tab)
                {
                    if (selectedIndex >= filteredActions.Count)
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
                    if (selectedIndex == 0)
                    {
                        TolkHelper.Speak(L10n.Get("Suchleiste aktiviert.", "Search bar activated."), RimWorldAccess.SpeechPriority.High);
                    }
                    else
                    {
                        int actionIndex = selectedIndex - 1;
                        if (actionIndex >= 0 && actionIndex < filteredActions.Count)
                        {
                            var action = filteredActions[actionIndex];
                            BumpSound.PlaySelect();
                            TolkHelper.Speak(L10n.Get($"{action.TranslatedName} ausgeführt.", $"{action.Name} executed."), RimWorldAccess.SpeechPriority.High);
                            action.Action?.Invoke();
                            this.Close();
                        }
                    }
                    Event.current.Use();
                }
                else if (key == KeyCode.Backspace)
                {
                    if (selectedIndex == 0 && searchText.Length > 0)
                    {
                        searchText = searchText.Substring(0, searchText.Length - 1);
                        UpdateFilter();
                        SpeakCurrentElement();
                        Event.current.Use();
                    }
                }
                // Handle typing for search bar
                else if (selectedIndex == 0 && Event.current.character != 0)
                {
                    searchText += Event.current.character;
                    UpdateFilter();
                    SpeakCurrentElement();
                    Event.current.Use();
                }
            }
        }
    }
}
