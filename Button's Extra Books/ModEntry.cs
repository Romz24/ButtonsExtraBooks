﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using ButtonsExtraBooks.Config;
using ButtonsExtraBooks.Helpers;
using ButtonsExtraBooks.Powers;
using ContentPatcher;
using GenericModConfigMenu;
using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;

namespace ButtonsExtraBooks
{
    /// <summary>The mod entry point.</summary>
    internal sealed class ModEntry : Mod
    {
        internal static IMonitor ModMonitor { get; private set; } = null!;

        internal static IModHelper ModHelper { get; private set; } = null!;
        internal static Harmony Harmony { get; private set; } = null!;
        internal static ModConfig Config { get; private set; } = null!;

        internal static Dictionary<string, Dictionary<string, string>> ContentPackI18n = new();
        
        internal static IContentPatcherAPI ContentPatcher { get; private set; } = null!;

        public override void Entry(IModHelper helper)
        {
            i18n.Init(helper.Translation);
            Config = helper.ReadConfig<ModConfig>();
            ModMonitor = Monitor;
            ModHelper = helper;
            var harmony = new Harmony(base.ModManifest.UniqueID);
            Harmony = harmony;
            harmony.PatchAll();

            helper.Events.GameLoop.OneSecondUpdateTicked += this.OnOneSecondUpdateTicked;
            helper.Events.GameLoop.UpdateTicked += this.InitializeTranslationsFromCP;
            helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
            helper.Events.GameLoop.DayEnding += JunimoScrap.OnDayEnding;
            helper.Events.Display.MenuChanged += this.OnMenuChange;
            helper.Events.Content.AssetRequested += Carols.AddShopModifiers;
            helper.Events.Content.LocaleChanged += this.InitializeTranslationsFromCP;
            helper.Events.Input.ButtonPressed += this.OnButtonPressed;
        }

        private void OnButtonPressed(object sender, ButtonPressedEventArgs e)
        {
            if (!Context.IsWorldReady) return;
        }
        
        private void OnOneSecondUpdateTicked(object sender, OneSecondUpdateTickedEventArgs e)
        {
            if (!Context.IsWorldReady) return;
            if (Game1.player.stats.Get("Spiderbuttons.ButtonsExtraBooks_Debug_RemoveAll") != 0)
                RemovePowers.RemoveAll();
        }

        private void InitializeTranslationsFromCP<T>(object sender, T e)
        {
            if (!ContentPatcher.IsConditionsApiReady) return;
            string lang = LocalizedContentManager.CurrentLanguageCode.ToString();
            if (lang == "mod") lang = LocalizedContentManager.CurrentModLanguage.LanguageCode;
            if (ContentPackI18n.ContainsKey(lang)) return;

            List<string> i18nFiles = ["Books", "Config", "Debug", "Dialogue", "Mail", "Untranslated"];

            ContentPackI18n[lang] = new Dictionary<string, string>();
            foreach (var file in i18nFiles)
            {
                if (!Game1.content.DoesAssetExist<Dictionary<string, string>>($"Mods/Spiderbuttons.ButtonsExtraBooks/Translations/{lang}/{file}")) continue;
                var i18nStrings =
                    Helper.GameContent.Load<Dictionary<string, string>>(
                        $"Mods/Spiderbuttons.ButtonsExtraBooks/Translations/{lang}/{file}");
                if (i18nStrings == null) return;

                foreach (var (key, value) in i18nStrings)
                {
                    ContentPackI18n[lang].TryAdd(key, value);
                }
            }

            if (!ContentPackI18n.ContainsKey("default"))
            {
                ContentPackI18n["default"] = new Dictionary<string, string>();
                foreach (var file in i18nFiles)
                {
                    if (!Game1.content.DoesAssetExist<Dictionary<string, string>>($"Mods/Spiderbuttons.ButtonsExtraBooks/Translations/default/{file}")) continue;
                    var defaultStrings =
                        Helper.GameContent.Load<Dictionary<string, string>>(
                            $"Mods/Spiderbuttons.ButtonsExtraBooks/Translations/default/{file}");
                    if (defaultStrings == null) return;

                    foreach (var (key, value) in defaultStrings)
                    {
                        ContentPackI18n["default"].TryAdd(key, value);
                    }
                }
            }
            
            Helper.Events.GameLoop.UpdateTicked -= InitializeTranslationsFromCP;
        }

        private void OnGameLaunched(object sender, GameLaunchedEventArgs e)
        {
            ContentPatcher = Helper.ModRegistry.GetApi<IContentPatcherAPI>("Pathoschild.ContentPatcher");
            if (ContentPatcher == null)
            {
                Log.Error("ContentPatcher not found. Button's Extra Books requires ContentPatcher to function.");
                return;
            }

            ContentPatcher.RegisterToken(
                mod: ModManifest,
                name: "ConfigAlwaysAvailable",
                getValue: () =>
                {
                    return new[]
                    {
                        Config.AlwaysAvailable.ToString()
                    };
                }
            );
            ContentPatcher.RegisterToken(
                mod: ModManifest,
                name: "ConfigEnableDebugBook",
                getValue: () =>
                {
                    return new[]
                    {
                        Config.DebugBook.ToString()
                    };
                }
            );

            foreach (var power in Assembly.GetExecutingAssembly().GetTypes().Where(t =>
                         t.Namespace == "ButtonsExtraBooks.Powers" && t.IsClass &&
                         !t.IsDefined(typeof(CompilerGeneratedAttribute), false)))
            {
                ContentPatcher.RegisterToken(
                    mod: ModManifest,
                    name: $"ConfigEnable{power.Name}",
                    getValue: () =>
                    {
                        return new[]
                        {
                            Config.GetPowerEnabled(power.Name).ToString()
                        };
                    }
                );
                ContentPatcher.RegisterToken(
                    mod: ModManifest,
                    name: $"ConfigPrice{power.Name}",
                    getValue: () =>
                    {
                        return new[]
                        {
                            Config.GetBookPrice(power.Name).ToString()
                        };
                    }
                );
            }

            ContentPatcher.RegisterToken(
                mod: ModManifest,
                name: "ConfigCheatCodesRequirement",
                getValue: () =>
                {
                    return new[]
                    {
                        Config.CheatCodesRequirement.ToString()
                    };
                }
            );

            ContentPatcher.RegisterToken(
                mod: ModManifest,
                name: "HasCoffeePower",
                getValue: () =>
                {
                    return Context.IsWorldReady
                        ? new[]
                        {
                            Game1.player.stats.Get("Spiderbuttons.ButtonsExtraBooks_Book_Coffee") > 0 ? "true" : "false"
                        }
                        : null;
                }
            );
            
            ContentPatcher.RegisterToken(
                mod: ModManifest,
                name: "ConfigCoffeeMultiplier",
                getValue: () =>
                {
                    return new[]
                    {
                        Config.CoffeeBonus.ToString()
                    };
                }
            );
            
            var configMenu = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (configMenu != null) Config.SetupConfig(configMenu, ModManifest, Helper, Harmony);
            
            GameStateQuery.Register("Spiderbuttons.ButtonsExtraBooks_WEEKLY_GIFTS_LIMIT_REACHED", (query, ctx) =>
            {
                if (!ArgUtility.TryGet(query, 1, out var playerKey, out var error, allowBlank: true, "string playerKey") || !ArgUtility.TryGet(query, 2, out var npcName, out error, allowBlank: true, "string npcName"))
                {
                    return GameStateQuery.Helpers.ErrorResult(query, error);
                }
                
                return GameStateQuery.Helpers.WithPlayer(ctx.Player, playerKey, player =>
                {
                    if (!player.friendshipData.TryGetValue(npcName, out var data)) return false;
                    if (Utils.PlayerHasPower(player, "ExtraGifts"))
                        return data.GiftsThisWeek >= Config.ExtraGiftsBonus;
                    return data.GiftsThisWeek >= 2;
                });
            });
        }

        private void OnMenuChange(object sender, MenuChangedEventArgs e)
        {
            if (e.NewMenu is not LetterViewerMenu letter) return;
            switch (letter.mailTitle)
            {
                case "Spiderbuttons.ButtonsExtraBooks_Mail_JunimoScrap_Crop":
                    letter.itemsToGrab[0].item = JunimoScrap.randomCropInSeason();
                    break;
                case "Spiderbuttons.ButtonsExtraBooks_Mail_JunimoScrap_Gem":
                    letter.itemsToGrab[0].item = JunimoScrap.randomGem();
                    break;
                case "Spiderbuttons.ButtonsExtraBooks_Mail_JunimoScrap_Item":
                    letter.itemsToGrab[0].item = JunimoScrap.randomItem();
                    break;
            }
        }
    }
}