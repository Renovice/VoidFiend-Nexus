/*
 * ================================================================================
 * GIGA MOD - ALL 5 MODS MERGED INTO ONE
 * ================================================================================
 * This file contains all 5 individual mods merged together for easier deployment
 * Each mod section is clearly separated with large comment blocks
 * ================================================================================
 */

using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using EntityStates;
using EntityStates.VoidSurvivor.Weapon;
using RoR2;
using RoR2.Skills;
using UnityEngine;
using UnityEngine.AddressableAssets;
using HarmonyLib;
using System;
using RiskOfOptions;
using RiskOfOptions.Options;
using RiskOfOptions.OptionConfigs;
using RoR2.UI;
using R2API;
using System.Reflection;
using System.Collections.Generic;
using BepInEx.Logging;
using System.Collections;
using UnityEngine.SceneManagement;
using System.IO;
using MonoMod.Cil;

#pragma warning disable CS8618 // Non-nullable field warnings
#pragma warning disable CS8601 // Possible null reference assignment
#pragma warning disable CS8600 // Converting null literal
#pragma warning disable CS8625 // Cannot convert null literal

/*
 * ████████████████████████████████████████████████████████████████████████████████
 * ██                                                                             ██
 * ██                         MOD 1: SUPPRESS REWORK                              ██
 * ██                                                                             ██
 * ████████████████████████████████████████████████████████████████████████████████
 */

namespace SuppressRework
{
    [BepInPlugin("com.renovice.suppressrework", "Suppress Rework", "1.4.4")]
    [BepInDependency("com.rune580.riskofoptions")]
    [BepInDependency("com.bepis.r2api")]
    public class SuppressRework : BaseUnityPlugin
    {
        // --- CONFIGURATION WITH RENOVICE'S REQUESTED DEFAULTS ---
        public static ConfigEntry<bool> EnableAllOrNothingScaling = null!;
        public static ConfigEntry<bool> EnableEfficientTrades = null!;
        public static ConfigEntry<float> MaxHealthSacrifice = null!;
        public static ConfigEntry<float> FixedTradeValue = null!;

        public static ConfigEntry<int> MaxChargesNormalMode = null!;
        public static ConfigEntry<int> MaxChargesCorruptedMode = null!;
        public static ConfigEntry<bool> InfiniteChargesNormalMode = null!;
        public static ConfigEntry<bool> InfiniteChargesCorruptedMode = null!;

        // --- Class variables for skill definitions ---
        internal static SkillDef _crushHealthDef;
        internal static SkillDef _crushCorruptionDef;
        internal static BepInEx.Logging.ManualLogSource Log;

        public static SuppressRework Instance { get; private set; }

        public void Awake()
        {
            Instance = this;
            Log = Logger;

            try
            {
                // --- BIND CONFIGURATION WITH RENOVICE'S REQUESTED DEFAULTS ---
                EnableAllOrNothingScaling = Config.Bind("Main", "Enable All-or-Nothing Scaling", false, "If true, the ability consumes your resource bar for a 1:1 reward. If false, it uses a fixed percentage.");
                EnableEfficientTrades = Config.Bind("Main", "Enable Efficient Trades", true, "If true, the ability will not spend more resources than needed to fill the target bar.");
                MaxHealthSacrifice = Config.Bind("All-or-Nothing Mode", "Max Health Sacrifice Percent", 50f, "When 'All-or-Nothing' is ON, this is the MAXIMUM percentage of your health you can sacrifice in one go.");
                FixedTradeValue = Config.Bind("Fixed Trade Mode", "Fixed Trade Percentage", 35f, "When 'All-or-Nothing' is OFF, this is the percentage of health/corruption that will be traded.");

                // Charge configuration with Renovice's requested defaults
                MaxChargesNormalMode = Config.Bind("Charge Settings", "Max Charges - Suppress", 1, "Maximum charges for the suppress ability (spend corruption to heal in normal mode). No cooldown - you get this many per transformation.");
                MaxChargesCorruptedMode = Config.Bind("Charge Settings", "Max Charges - Corrupted Suppress", 2, "Maximum charges for the corrupted suppress ability (sacrifice health for corruption in void mode). No cooldown - you get this many per transformation.");
                InfiniteChargesNormalMode = Config.Bind("Charge Settings", "Infinite Charges - Suppress", true, "If true, suppress ability has infinite charges in normal mode.");
                InfiniteChargesCorruptedMode = Config.Bind("Charge Settings", "Infinite Charges - Corrupted Suppress", false, "If true, corrupted suppress ability has infinite charges in void mode.");

                // --- SET UP RISK OF OPTIONS MENU ---
                SetupRiskOfOptions();

                // --- APPLY HARMONY PATCHES ---
                Harmony harmony = new Harmony("com.renovice.suppressrework");
                harmony.PatchAll();

                // Use a reliable event to ensure game data is loaded
                On.RoR2.SurvivorCatalog.Init += (orig) =>
                {
                    orig();
                    OnGameLoaded();
                };

                Logger.LogInfo("Suppress Rework v1.4.4 - Void Item Corruption Bug Fix loaded successfully!");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error in Awake: {ex}");
            }
        }

        private void SetupRiskOfOptions()
        {
            try
            {
                // Set mod description 
                ModSettingsManager.SetModDescription("A complete overhaul of Voidfiend's Suppress ability with customizable trading mechanics, charge systems, Aegis support, and dynamic tooltips. Now with void item corruption bug fixes!");

                // Set mod icon from embedded PNG
                SetModIcon();

                ModSettingsManager.AddOption(new CheckBoxOption(EnableAllOrNothingScaling));
                ModSettingsManager.AddOption(new CheckBoxOption(EnableEfficientTrades));
                ModSettingsManager.AddOption(new SliderOption(MaxHealthSacrifice, new SliderConfig { min = 1f, max = 100f, FormatString = "{0:0}%" }));
                ModSettingsManager.AddOption(new SliderOption(FixedTradeValue, new SliderConfig { min = 1f, max = 100f, FormatString = "{0:0}%" }));

                ModSettingsManager.AddOption(new IntSliderOption(MaxChargesNormalMode, new IntSliderConfig { min = 0, max = 20 }));
                ModSettingsManager.AddOption(new IntSliderOption(MaxChargesCorruptedMode, new IntSliderConfig { min = 0, max = 20 }));
                ModSettingsManager.AddOption(new CheckBoxOption(InfiniteChargesNormalMode));
                ModSettingsManager.AddOption(new CheckBoxOption(InfiniteChargesCorruptedMode));

                // Event listeners for live updates
                EnableAllOrNothingScaling.SettingChanged += (o, e) => OnSettingChanged();
                EnableEfficientTrades.SettingChanged += (o, e) => OnSettingChanged();
                MaxHealthSacrifice.SettingChanged += (o, e) => OnSettingChanged();
                FixedTradeValue.SettingChanged += (o, e) => OnSettingChanged();
                MaxChargesNormalMode.SettingChanged += (o, e) => OnSettingChanged();
                MaxChargesCorruptedMode.SettingChanged += (o, e) => OnSettingChanged();
                InfiniteChargesNormalMode.SettingChanged += (o, e) => OnSettingChanged();
                InfiniteChargesCorruptedMode.SettingChanged += (o, e) => OnSettingChanged();
            }
            catch (Exception ex)
            {
                Log.LogError($"Error setting up Risk of Options: {ex}");
            }
        }

        private void SetModIcon()
        {
            try
            {
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                using (var stream = assembly.GetManifestResourceStream("V8_supressrework.icon.png"))
                {
                    if (stream != null)
                    {
                        byte[] data = new byte[stream.Length];
                        stream.Read(data, 0, data.Length);

                        Texture2D texture = new Texture2D(2, 2);
                        texture.LoadImage(data);

                        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                        ModSettingsManager.SetModIcon(sprite);

                        Log.LogInfo("Mod icon set successfully");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.LogError($"Failed to set mod icon: {ex}");
            }
        }

        private void OnSettingChanged()
        {
            try
            {
                ModifySkillDefs();
                TooltipRefresher.RefreshTooltipsAfterSettingsChange();
                Log.LogInfo("Settings changed - skill descriptions should update dynamically");
            }
            catch (Exception ex)
            {
                Log.LogError($"Error in OnSettingChanged: {ex}");
            }
        }

        private void OnGameLoaded()
        {
            try
            {
                _crushHealthDef = Addressables.LoadAssetAsync<SkillDef>("RoR2/DLC1/VoidSurvivor/CrushHealth.asset").WaitForCompletion();
                _crushCorruptionDef = Addressables.LoadAssetAsync<SkillDef>("RoR2/DLC1/VoidSurvivor/CrushCorruption.asset").WaitForCompletion();

                if (_crushHealthDef != null)
                {
                    Log.LogInfo($"CrushHealth loaded successfully. Token: {_crushHealthDef.skillDescriptionToken}");
                }
                else
                {
                    Log.LogError("Failed to load CrushHealth skill definition");
                }

                if (_crushCorruptionDef != null)
                {
                    Log.LogInfo($"CrushCorruption loaded successfully. Token: {_crushCorruptionDef.skillDescriptionToken}");
                }
                else
                {
                    Log.LogError("Failed to load CrushCorruption skill definition");
                }

                ModifySkillDefs();
            }
            catch (Exception ex)
            {
                Log.LogError($"Error in OnGameLoaded: {ex}");
            }
        }

        public void ModifySkillDefs()
        {
            try
            {
                if (_crushHealthDef != null)
                {
                    if (InfiniteChargesCorruptedMode.Value)
                    {
                        _crushHealthDef.baseMaxStock = 1;
                        _crushHealthDef.rechargeStock = 1;
                        _crushHealthDef.baseRechargeInterval = 0f;
                        Log.LogInfo("Corrupted Suppress: Infinite charges enabled (instant recharge)");
                    }
                    else
                    {
                        _crushHealthDef.baseMaxStock = MaxChargesCorruptedMode.Value;
                        _crushHealthDef.rechargeStock = 0;
                        _crushHealthDef.baseRechargeInterval = 0f;
                        Log.LogInfo($"Corrupted Suppress: {MaxChargesCorruptedMode.Value} charges, no recharge");
                    }
                }

                if (_crushCorruptionDef != null)
                {
                    if (InfiniteChargesNormalMode.Value)
                    {
                        _crushCorruptionDef.baseMaxStock = 1;
                        _crushCorruptionDef.rechargeStock = 1;
                        _crushCorruptionDef.baseRechargeInterval = 0f;
                        Log.LogInfo("Suppress: Infinite charges enabled (instant recharge)");
                    }
                    else
                    {
                        _crushCorruptionDef.baseMaxStock = MaxChargesNormalMode.Value;
                        _crushCorruptionDef.rechargeStock = 0;
                        _crushCorruptionDef.baseRechargeInterval = 0f;
                        Log.LogInfo($"Suppress: {MaxChargesNormalMode.Value} charges, no recharge");
                    }

                    if (_crushCorruptionDef.GetType().Name == "VoidSurvivorSkillDef")
                    {
                        var minimumCorruptionField = _crushCorruptionDef.GetType().GetField("minimumCorruption");
                        var maximumCorruptionField = _crushCorruptionDef.GetType().GetField("maximumCorruption");

                        if (minimumCorruptionField != null)
                        {
                            minimumCorruptionField.SetValue(_crushCorruptionDef, 0.1f);
                            Log.LogInfo("Set CrushCorruption minimumCorruption to: 0.1");
                        }

                        if (maximumCorruptionField != null)
                        {
                            maximumCorruptionField.SetValue(_crushCorruptionDef, 100f);
                            Log.LogInfo("Set CrushCorruption maximumCorruption to: 100");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.LogError($"Error in ModifySkillDefs: {ex}");
            }
        }
    }

    // CORRUPTION HELPER CLASS - Handles void item corruption bugs
    public static class CorruptionHelper
    {
        /// <summary>
        /// Gets safe corruption values that work even when void items cause corruption system glitches
        /// </summary>
        public static (float current, float minimum, float maximum, float spendable) GetSafeCorruptionValues(VoidSurvivorController voidController)
        {
            if (voidController == null)
                return (0f, 0f, 100f, 0f);

            // Get raw values from the controller
            float rawCorruption = voidController.corruption;
            float rawMinimum = voidController.minimumCorruption;
            float rawMaximum = voidController.maxCorruption;

            // CRITICAL FIX: When void items glitch the system, treat maxCorruption as always 100%
            // The corruption system is designed for maxCorruption = 100%, void items break this assumption
            float safeMaximum = 100f;

            // SAFETY CHECK: Clamp current corruption to reasonable bounds
            // The corruption value can fluctuate wildly when minimumCorruption > maxCorruption
            float safeCurrent = Mathf.Clamp(rawCorruption, 0f, safeMaximum);

            // VOID ITEM PROTECTION: If minimumCorruption exceeds safeMaximum, cap it
            // This prevents the "permanently corrupted" state from breaking our calculations
            float safeMinimum = Mathf.Clamp(rawMinimum, 0f, safeMaximum);

            // SPENDABLE CORRUPTION: Never allow negative values, even when the system glitches
            float safeSpendable = Mathf.Max(0f, safeCurrent - safeMinimum);

            // DEBUG: Log when we detect corruption system instability
            if (rawMinimum > rawMaximum || rawCorruption > rawMaximum || rawCorruption < 0f)
            {
                SuppressRework.Log?.LogWarning($"CORRUPTION GLITCH DETECTED: Raw values - Current: {rawCorruption:F1}%, Min: {rawMinimum:F1}%, Max: {rawMaximum:F1}%");
                SuppressRework.Log?.LogWarning($"USING SAFE VALUES: Current: {safeCurrent:F1}%, Min: {safeMinimum:F1}%, Max: {safeMaximum:F1}%, Spendable: {safeSpendable:F1}%");
            }

            return (safeCurrent, safeMinimum, safeMaximum, safeSpendable);
        }

        /// <summary>
        /// Checks if the corruption system is in a glitched state due to void items
        /// </summary>
        public static bool IsCorruptionSystemGlitched(VoidSurvivorController voidController)
        {
            if (voidController == null) return false;

            return voidController.minimumCorruption > voidController.maxCorruption ||
                   voidController.corruption > voidController.maxCorruption ||
                   voidController.corruption < 0f ||
                   voidController.minimumCorruption > 100f;
        }
    }

    // ENHANCED All-or-Nothing Logic + Hard Resource Blocks + AEGIS SUPPORT + VOID ITEM CORRUPTION BUG FIX
    [HarmonyPatch(typeof(CrushBase), nameof(CrushBase.OnEnter))]
    public static class CrushBase_OnEnter_Patch
    {
        static bool Prefix(CrushBase __instance)
        {
            try
            {
                CharacterBody characterBody = __instance.characterBody;
                if (characterBody == null) return true;

                // Get void controller to check corruption
                VoidSurvivorController voidController = characterBody.GetComponent<VoidSurvivorController>();

                // HARD BLOCK: In Fixed Trade Mode, prevent execution if insufficient resources
                if (!SuppressRework.EnableAllOrNothingScaling.Value)
                {
                    float requiredPercent = SuppressRework.FixedTradeValue.Value;

                    if (__instance is CrushHealth)
                    {
                        HealthComponent healthComponent = characterBody.healthComponent;
                        if (healthComponent != null && healthComponent.fullHealth > 0)
                        {
                            float healthPercentage = (healthComponent.health / healthComponent.fullHealth) * 100f;

                            if (healthPercentage < requiredPercent)
                            {
                                SuppressRework.Log?.LogWarning($"BLOCKED CrushHealth execution: {healthPercentage:F1}% < {requiredPercent}% required");
                                __instance.outer.SetNextStateToMain();
                                return false;
                            }
                        }
                    }
                    else if (__instance is CrushCorruption)
                    {
                        if (voidController != null)
                        {
                            // FIXED: Use safe corruption calculation that handles void item bugs
                            var (current, minimum, maximum, spendable) = CorruptionHelper.GetSafeCorruptionValues(voidController);

                            if (spendable < requiredPercent)
                            {
                                SuppressRework.Log?.LogWarning($"BLOCKED CrushCorruption execution: Only {spendable:F1}% spendable corruption < {requiredPercent}% required");
                                __instance.outer.SetNextStateToMain();
                                return false;
                            }
                        }
                    }
                }

                // Calculate the actual trade values
                float potentialHealFraction = 0f;
                float potentialCorruptionChange = 0f;

                if (SuppressRework.EnableAllOrNothingScaling.Value)
                {
                    if (__instance is CrushHealth)
                    {
                        // Health sacrifice logic (unchanged - this works correctly)
                        HealthComponent healthComponent = characterBody.healthComponent;
                        if (healthComponent == null || healthComponent.fullHealth <= 0)
                        {
                            SuppressRework.Log?.LogWarning("CrushHealth: No health component, aborting");
                            __instance.outer.SetNextStateToMain();
                            return false;
                        }

                        float availableHealthToSacrifice = healthComponent.health - 1f;

                        if (availableHealthToSacrifice <= 0)
                        {
                            SuppressRework.Log?.LogWarning($"CrushHealth: Insufficient health to sacrifice. Current: {healthComponent.health:F1}, need at least 2 HP");
                            __instance.outer.SetNextStateToMain();
                            return false;
                        }

                        float maxAllowedSacrifice = healthComponent.fullHealth * (SuppressRework.MaxHealthSacrifice.Value / 100f);
                        float actualHealthToSacrifice = Math.Min(availableHealthToSacrifice, maxAllowedSacrifice);

                        if (actualHealthToSacrifice <= 0.1f)
                        {
                            SuppressRework.Log?.LogWarning($"CrushHealth: Health sacrifice too small ({actualHealthToSacrifice:F2} HP), aborting");
                            __instance.outer.SetNextStateToMain();
                            return false;
                        }

                        float percentOfMaxHealthSacrificed = actualHealthToSacrifice / healthComponent.fullHealth;
                        potentialHealFraction = -percentOfMaxHealthSacrificed;
                        potentialCorruptionChange = percentOfMaxHealthSacrificed * 100f;

                        SuppressRework.Log?.LogInfo($"CrushHealth All-or-Nothing: Sacrificing {actualHealthToSacrifice:F1} HP ({percentOfMaxHealthSacrificed * 100f:F1}%) for {potentialCorruptionChange:F1}% corruption");
                    }
                    else if (__instance is CrushCorruption)
                    {
                        // FIXED: All-or-Nothing corruption spending with safe void item handling
                        if (voidController == null)
                        {
                            SuppressRework.Log?.LogWarning("CrushCorruption: No void survivor controller, aborting");
                            __instance.outer.SetNextStateToMain();
                            return false;
                        }

                        var (current, minimum, maximum, spendable) = CorruptionHelper.GetSafeCorruptionValues(voidController);

                        if (spendable <= 0.1f)
                        {
                            SuppressRework.Log?.LogWarning($"CrushCorruption: Insufficient spendable corruption ({spendable:F1}%). Current: {current:F1}%, Minimum: {minimum:F1}%");
                            __instance.outer.SetNextStateToMain();
                            return false;
                        }

                        // CRITICAL FIX: Use the SAFE spendable corruption for the trade calculation
                        float percentToHeal = spendable / 100f;
                        potentialHealFraction = percentToHeal;
                        potentialCorruptionChange = -spendable;

                        SuppressRework.Log?.LogInfo($"CrushCorruption All-or-Nothing: Spending {spendable:F1}% corruption (of {current:F1}% total, min {minimum:F1}%) for {percentToHeal * 100f:F1}% heal");
                    }
                }
                else
                {
                    // Fixed Trade Mode with void item corruption bug protection
                    float tradePercent = SuppressRework.FixedTradeValue.Value;
                    if (__instance is CrushHealth)
                    {
                        potentialHealFraction = -(tradePercent / 100f);
                        potentialCorruptionChange = tradePercent;
                        SuppressRework.Log?.LogInfo($"CrushHealth Fixed: sacrificing {tradePercent}% health for {tradePercent}% corruption");
                    }
                    else if (__instance is CrushCorruption)
                    {
                        if (voidController != null)
                        {
                            var (current, minimum, maximum, spendable) = CorruptionHelper.GetSafeCorruptionValues(voidController);

                            // CRITICAL FIX: Use safe spendable amount, capped by trade percent
                            float actualTradeAmount = Math.Min(tradePercent, spendable);

                            // EMERGENCY PROTECTION: If actualTradeAmount is 0 or negative, abort to prevent healing exploit
                            if (actualTradeAmount <= 0.1f)
                            {
                                SuppressRework.Log?.LogWarning($"CrushCorruption Fixed: Emergency abort - trade amount too low ({actualTradeAmount:F1}%)");
                                __instance.outer.SetNextStateToMain();
                                return false;
                            }

                            potentialHealFraction = actualTradeAmount / 100f;
                            potentialCorruptionChange = -actualTradeAmount;

                            SuppressRework.Log?.LogInfo($"CrushCorruption Fixed: spending {actualTradeAmount:F1}% corruption (requested {tradePercent}%, available {spendable:F1}%) for {actualTradeAmount:F1}% heal");
                        }
                        else
                        {
                            // Fallback for when no void controller (shouldn't happen for Voidfiend)
                            potentialHealFraction = tradePercent / 100f;
                            potentialCorruptionChange = -tradePercent;
                        }
                    }
                }

                // FINAL SAFETY CHECK: Prevent any healing exploits by validating the trade direction
                if (__instance is CrushHealth)
                {
                    // CrushHealth should ALWAYS sacrifice health (negative heal) and gain corruption (positive)
                    if (potentialHealFraction > 0f || potentialCorruptionChange < 0f)
                    {
                        SuppressRework.Log?.LogError($"EXPLOIT PREVENTION: CrushHealth attempted invalid trade - heal: {potentialHealFraction:F3}, corruption: {potentialCorruptionChange:F1}");
                        __instance.outer.SetNextStateToMain();
                        return false;
                    }
                }
                else if (__instance is CrushCorruption)
                {
                    // CrushCorruption should ALWAYS heal (positive) and lose corruption (negative)
                    if (potentialHealFraction < 0f || potentialCorruptionChange > 0f)
                    {
                        SuppressRework.Log?.LogError($"EXPLOIT PREVENTION: CrushCorruption attempted invalid trade - heal: {potentialHealFraction:F3}, corruption: {potentialCorruptionChange:F1}");
                        __instance.outer.SetNextStateToMain();
                        return false;
                    }
                }

                // AEGIS-ENHANCED Efficient Trades logic
                if (SuppressRework.EnableEfficientTrades.Value)
                {
                    if (__instance is CrushHealth && potentialCorruptionChange > 0)
                    {
                        if (voidController != null)
                        {
                            var (current, minimum, maximum, spendable) = CorruptionHelper.GetSafeCorruptionValues(voidController);

                            // Account for safe corruption values when calculating corruption room
                            float corruptionRoom = maximum - current;
                            if (potentialCorruptionChange > corruptionRoom)
                            {
                                float scale = corruptionRoom / potentialCorruptionChange;
                                potentialCorruptionChange *= scale;
                                potentialHealFraction *= scale;
                                SuppressRework.Log?.LogInfo($"CrushHealth scaled down due to corruption cap: scale={scale:F2}");
                            }
                        }
                    }
                    else if (__instance is CrushCorruption && potentialHealFraction > 0)
                    {
                        HealthComponent hc = characterBody.healthComponent;
                        if (hc == null) return true;

                        // AEGIS SUPPORT: Check if we have Aegis item
                        bool hasAegis = characterBody.inventory != null &&
                                        characterBody.inventory.GetItemCount(RoR2Content.Items.BarrierOnOverHeal) > 0;

                        if (hasAegis)
                        {
                            // With Aegis, we can heal beyond max health up to the barrier cap
                            float barrierCap = characterBody.maxBarrier;
                            float currentBarrier = hc.barrier;
                            float availableBarrierRoom = barrierCap - currentBarrier;
                            float missingHealth = hc.fullHealth - hc.health;
                            float totalHealingRoom = missingHealth + (availableBarrierRoom * 2f); // *2 because Aegis converts at 50%

                            SuppressRework.Log?.LogInfo($"Aegis detected: Health room={missingHealth:F1}, Barrier room={availableBarrierRoom:F1}, Total room={totalHealingRoom:F1}");

                            float requestedHeal = potentialHealFraction * hc.fullHealth;

                            if (requestedHeal > totalHealingRoom)
                            {
                                float scale = totalHealingRoom / requestedHeal;
                                potentialHealFraction *= scale;
                                potentialCorruptionChange *= scale;
                                SuppressRework.Log?.LogInfo($"CrushCorruption scaled down due to Aegis-aware healing cap: scale={scale:F2}");
                            }
                        }
                        else
                        {
                            // No Aegis: Normal overheal prevention
                            float missingHealth = hc.fullHealth - hc.health;
                            float missingHealthFraction = missingHealth / hc.fullHealth;
                            if (potentialHealFraction > missingHealthFraction)
                            {
                                float scale = missingHealthFraction / potentialHealFraction;
                                potentialHealFraction *= scale;
                                potentialCorruptionChange *= scale;
                                SuppressRework.Log?.LogInfo($"CrushCorruption scaled down due to overheal prevention: scale={scale:F2}");
                            }
                        }
                    }
                }

                __instance.selfHealFraction = potentialHealFraction;
                __instance.corruptionChange = potentialCorruptionChange;

                return true;
            }
            catch (Exception ex)
            {
                SuppressRework.Log?.LogError($"Error in CrushBase_OnEnter_Patch: {ex}");
                return true;
            }
        }
    }

    // UI Blocking for Fixed Trade Mode - Prevents skill usage when insufficient resources (VOID ITEM BUG AWARE)
    [HarmonyPatch]
    public static class VoidSurvivorSkillDef_HasRequiredCorruption_Patch
    {
        static MethodBase TargetMethod()
        {
            var voidSurvivorSkillDefType = AccessTools.TypeByName("RoR2.Skills.VoidSurvivorSkillDef");
            return AccessTools.Method(voidSurvivorSkillDefType, "HasRequiredCorruption");
        }

        static bool Prefix(object __instance, GenericSkill skillSlot, ref bool __result)
        {
            try
            {
                // Only apply resource checking in Fixed Trade Mode
                if (SuppressRework.EnableAllOrNothingScaling.Value)
                {
                    return true; // Use default behavior in All-or-Nothing mode
                }

                if (ReferenceEquals(__instance, SuppressRework._crushHealthDef))
                {
                    CharacterBody characterBody = skillSlot?.characterBody;
                    if (characterBody?.healthComponent != null)
                    {
                        float healthPercentage = (characterBody.healthComponent.health / characterBody.healthComponent.fullHealth) * 100f;
                        float requiredHealth = SuppressRework.FixedTradeValue.Value;

                        __result = healthPercentage >= requiredHealth;
                        return false;
                    }
                }
                else if (ReferenceEquals(__instance, SuppressRework._crushCorruptionDef))
                {
                    CharacterBody characterBody = skillSlot?.characterBody;
                    VoidSurvivorController voidController = characterBody?.GetComponent<VoidSurvivorController>();
                    if (voidController != null)
                    {
                        // FIXED: Use safe corruption calculation that handles void item bugs
                        var (current, minimum, maximum, spendable) = CorruptionHelper.GetSafeCorruptionValues(voidController);
                        float requiredCorruption = SuppressRework.FixedTradeValue.Value;

                        __result = spendable >= requiredCorruption;
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                SuppressRework.Log?.LogError($"Error in HasRequiredCorruption patch: {ex}");
            }

            return true;
        }
    }

    // ULTRA-SAFE Dynamic Tooltips - Zero Performance Impact, No BepInEx Corruption Risk
    [HarmonyPatch(typeof(Language), nameof(Language.GetString), new Type[] { typeof(string) })]
    public static class Language_GetString_Patch
    {
        private static string? _crushHealthToken = null;
        private static string? _crushCorruptionToken = null;
        private static string? _corruptionUpgradeToken = null;

        // Pre-computed hash set for lightning-fast lookups - NO string operations
        private static readonly HashSet<string> _voidSurvivorTokens = new HashSet<string>
        {
            "VOIDSURVIVOR_PRIMARY_DESCRIPTION",
            "VOIDSURVIVOR_SECONDARY_DESCRIPTION",
            "VOIDSURVIVOR_UTILITY_DESCRIPTION",
            "VOIDSURVIVOR_SPECIAL_DESCRIPTION"
        };

        static bool Prefix(string token, ref string __result)
        {
            try
            {
                // NUCLEAR-SAFE EXIT: Only process tokens we absolutely know about
                // NO expensive string operations like Contains() - only exact matches
                if (string.IsNullOrEmpty(token) || token.Length < 15)
                    return true;

                // Initialize our specific tokens when available
                if (_crushHealthToken == null && SuppressRework._crushHealthDef != null)
                {
                    _crushHealthToken = SuppressRework._crushHealthDef.skillDescriptionToken;
                    _crushCorruptionToken = SuppressRework._crushCorruptionDef?.skillDescriptionToken;

                    if (_crushHealthToken != null) _voidSurvivorTokens.Add(_crushHealthToken);
                    if (_crushCorruptionToken != null) _voidSurvivorTokens.Add(_crushCorruptionToken);
                }

                // SUPER TARGETED: Only handle the main menu corruption upgrade tooltip
                if (_corruptionUpgradeToken == null && token.Length > 25 &&
                    token.Contains("Corruption Upgrade") && token.Contains("Transform to crush"))
                {
                    _corruptionUpgradeToken = token;
                    _voidSurvivorTokens.Add(token);
                    SuppressRework.Log?.LogInfo($"Safely cached corruption upgrade token: {token}");
                }

                // Handle the main menu Corruption Upgrade tooltip (this is what wasn't updating)
                if (token == _corruptionUpgradeToken)
                {
                    if (SuppressRework.EnableAllOrNothingScaling.Value)
                    {
                        __result = "<style=cKeywordName>□Corruption Upgrade□</style><style=cSub>Transform to crush a portion of your maximum health to gain an equal percentage of Corruption instead.</style>";
                    }
                    else
                    {
                        string tradeValue = $"{SuppressRework.FixedTradeValue.Value:0}";
                        __result = $"<style=cKeywordName>□Corruption Upgrade□</style><style=cSub>Transform to crush {tradeValue}% health to gain {tradeValue}% Corruption instead.</style>";
                    }
                    return false;
                }

                // ULTRA-FAST CHECK: Only process pre-approved tokens (skip everything else)
                if (!_voidSurvivorTokens.Contains(token))
                    return true; // Skip 99.99% of tokens instantly

                // Only get void mode state for tokens we care about
                bool isInVoidMode = IsPlayerInVoidModeState();

                if (token == _crushCorruptionToken && SuppressRework._crushCorruptionDef != null)
                {
                    if (isInVoidMode)
                    {
                        if (SuppressRework.EnableAllOrNothingScaling.Value)
                        {
                            __result = "Crush a portion of your <style=cIsHealth>maximum health</style> to gain an equal percentage of <style=cIsVoid>Corruption</style>.";
                        }
                        else
                        {
                            string tradeValue = $"{SuppressRework.FixedTradeValue.Value:0}";
                            __result = $"Crush <style=cIsHealth>{tradeValue}% maximum health</style> to gain <style=cIsVoid>{tradeValue}% Corruption</style>.";
                        }
                    }
                    else
                    {
                        if (SuppressRework.EnableAllOrNothingScaling.Value)
                        {
                            __result = "Crush your entire <style=cIsVoid>Corruption</style> bar to heal yourself for an equal percentage of <style=cIsHealing>maximum health</style>. <style=cIsUtility>Works with Aegis barrier conversion!</style>";
                        }
                        else
                        {
                            string tradeValue = $"{SuppressRework.FixedTradeValue.Value:0}";
                            __result = $"Crush <style=cIsVoid>{tradeValue}% Corruption</style> to heal yourself for <style=cIsHealing>{tradeValue}% maximum health</style>. <style=cIsUtility>Works with Aegis barrier conversion!</style>";
                        }
                    }
                    return false;
                }

                // Handle other Voidfiend ability tooltips
                switch (token)
                {
                    case "VOIDSURVIVOR_PRIMARY_DESCRIPTION":
                        __result = isInVoidMode
                            ? "Fire a short-range beam for <style=cIsDamage>300% damage</style>."
                            : "Fire a slowing long-range beam for <style=cIsDamage>300% damage</style>.";
                        return false;

                    case "VOIDSURVIVOR_SECONDARY_DESCRIPTION":
                        __result = isInVoidMode
                            ? "Fire an arcing plasma bomb for <style=cIsDamage>1100% damage</style>."
                            : "Fire a plasma missile for <style=cIsDamage>600% damage</style>. Fully charge it for an <style=cIsDamage>explosive plasma ball</style> instead, dealing <style=cIsDamage>1100% damage</style>.";
                        return false;

                    case "VOIDSURVIVOR_UTILITY_DESCRIPTION":
                        __result = isInVoidMode
                            ? "Disappear into the Void, <style=cIsUtility>cleansing all debuffs</style> while moving at a fast forward angle."
                            : "Disappear into the Void, <style=cIsUtility>cleansing all debuffs</style> while moving in an upward arc.";
                        return false;
                }

                // Handle CrushHealth token (backup case)
                if (token == _crushHealthToken && SuppressRework._crushHealthDef != null)
                {
                    if (SuppressRework.EnableAllOrNothingScaling.Value)
                    {
                        __result = "Crush a portion of your <style=cIsHealth>maximum health</style> to gain an equal percentage of <style=cIsVoid>Corruption</style>.";
                    }
                    else
                    {
                        string tradeValue = $"{SuppressRework.FixedTradeValue.Value:0}";
                        __result = $"Crush <style=cIsHealth>{tradeValue}% maximum health</style> to gain <style=cIsVoid>{tradeValue}% Corruption</style>.";
                    }
                    return false;
                }
            }
            catch (Exception ex)
            {
                SuppressRework.Log?.LogError($"Error in Language_GetString_Patch: {ex}");
            }

            return true;
        }

        private static bool IsPlayerInVoidModeState()
        {
            try
            {
                var localUser = LocalUserManager.GetFirstLocalUser();
                if (localUser?.cachedMasterController?.master?.GetBody() == null)
                    return false;

                CharacterBody playerBody = localUser.cachedMasterController.master.GetBody();

                if (playerBody.baseNameToken != "VOIDSURVIVOR_BODY_NAME")
                    return false;

                VoidSurvivorController voidController = playerBody.GetComponent<VoidSurvivorController>();
                if (voidController == null) return false;

                // Check multiple ways to determine void mode
                if (voidController.corruptedBuffDef != null && playerBody.HasBuff(voidController.corruptedBuffDef))
                    return true;

                if (voidController.corruptionModeStateMachine?.state != null)
                {
                    string currentStateName = voidController.corruptionModeStateMachine.state.GetType().Name;
                    if (currentStateName == "CorruptMode")
                        return true;
                }

                return voidController.corruption >= 100f;
            }
            catch (Exception ex)
            {
                SuppressRework.Log?.LogError($"Error in IsPlayerInVoidModeState: {ex}");
                return false;
            }
        }
    }

    // Helper class for tooltip refresh
    public static class TooltipRefresher
    {
        // Simple approach: Update settings callback forces tooltip refresh
        public static void RefreshTooltipsAfterSettingsChange()
        {
            try
            {
                // Clear any cached language tokens to force fresh lookups
                // This is the safest way to ensure tooltips update
                var languageField = typeof(Language).GetField("currentLanguage", BindingFlags.NonPublic | BindingFlags.Static);
                if (languageField != null)
                {
                    var currentLanguage = languageField.GetValue(null) as Language;
                    if (currentLanguage != null)
                    {
                        // Force language to refresh its token cache
                        var stringsByToken = typeof(Language).GetField("stringsByToken", BindingFlags.NonPublic | BindingFlags.Instance);
                        if (stringsByToken != null)
                        {
                            var tokenDict = stringsByToken.GetValue(stringsByToken);
                            if (tokenDict != null)
                            {
                                // Clear just our skill tokens from the cache
                                var clearMethod = tokenDict.GetType().GetMethod("Remove");
                                if (clearMethod != null && SuppressRework._crushCorruptionDef != null)
                                {
                                    clearMethod.Invoke(tokenDict, new object[] { SuppressRework._crushCorruptionDef.skillDescriptionToken });
                                    if (SuppressRework._crushHealthDef != null)
                                    {
                                        clearMethod.Invoke(tokenDict, new object[] { SuppressRework._crushHealthDef.skillDescriptionToken });
                                    }
                                }
                            }
                        }
                    }
                }

                // Also force refresh any active tooltips in the UI
                var allTooltipProviders = UnityEngine.Object.FindObjectsOfType<TooltipProvider>();
                foreach (var tooltip in allTooltipProviders)
                {
                    if (tooltip.bodyToken != null &&
                        (tooltip.bodyToken.Contains("CRUSH") || tooltip.bodyToken.Contains("SPECIAL")))
                    {
                        // Force this tooltip to refresh by making it dirty
                        var setContentMethod = typeof(TooltipProvider).GetMethod("SetContent", BindingFlags.Public | BindingFlags.Instance);
                        if (setContentMethod != null)
                        {
                            // Get current content and reset it to force refresh
                            var currentContent = tooltip.GetComponent<TooltipProvider>();
                            if (currentContent != null)
                            {
                                setContentMethod.Invoke(tooltip, new object[] { new TooltipContent() });
                            }
                        }
                    }
                }

                SuppressRework.Log?.LogInfo("Cleared tooltip cache to force refresh");
            }
            catch (Exception ex)
            {
                SuppressRework.Log?.LogError($"Error refreshing tooltips: {ex}");
            }
        }
    }
}




/*
 * ████████████████████████████████████████████████████████████████████████████████
 * ██                                                                             ██
 * ██                         MOD 2: VOID FIEND BEAM                              ██
 * ██                                                                             ██
 * ████████████████████████████████████████████████████████████████████████████████
 */

namespace VoidFiendBeam
{
    [BepInPlugin("com.YourName.VoidFiendBeam", "Void Fiend Beam", "2.3.6")]
    [BepInDependency("com.rune580.riskofoptions")]
    public class VoidFiendBeam : BaseUnityPlugin
    {
        public static ConfigEntry<float> BeamRange;
        public static ConfigEntry<float> BeamVfxXScale;
        public static ConfigEntry<float> BeamVfxYScale;
        public static ConfigEntry<float> BeamVfxZScale;
        public static ConfigEntry<bool> LazerBeamModeRegular;
        public static ConfigEntry<bool> LazerBeamModeCorrupt;
        public static ConfigEntry<bool> EnableDebugLogging;
        public static ConfigEntry<float> VfxSurfaceOffset;
        public static BepInEx.Logging.ManualLogSource StaticLogger;

        // Track when hand beams are actively firing
        public static bool isCorruptHandBeamActive = false;
        public static bool isRegularHandBeamActive = false;

        public void Awake()
        {
            // Initialize static logger
            StaticLogger = Logger;

            BeamRange = Config.Bind("Beam Settings", "Beam Range", 75f, "The effective range of the Corrupt Hand Beam. Base game default is 40.");
            BeamVfxXScale = Config.Bind("Beam Settings", "Beam VFX Width Scale", 1f, "The scale of the beam's visual effects (VFX) on the X-axis. Base game default is 1.");
            BeamVfxYScale = Config.Bind("Beam Settings", "Beam VFX Height Scale", 1f, "The scale of the beam's visual effects (VFX) on the Y-axis. Base game default is 1.");
            BeamVfxZScale = Config.Bind("Beam Settings", "Beam VFX Length Scale", 3.25f, "The scale of the beam's visual effects (VFX) on the Z-axis. Base game default is 1.");

            // New Laser Mode settings: separate toggles for Regular and Corrupt hand beams
            LazerBeamModeRegular = Config.Bind("Laser Mode", "Enable Laser Mode (Regular Beam)", false, "Eliminates randomization for the regular hand beam, making it behave like a precise laser.");
            LazerBeamModeCorrupt = Config.Bind("Laser Mode", "Enable Laser Mode (Corrupt Beam)", false, "Eliminates randomization for the corrupt hand beam, making it behave like a precise laser.");
            EnableDebugLogging = Config.Bind("Debug", "Enable Debug Logging", false, "Enable detailed logging for troubleshooting.");
            VfxSurfaceOffset = Config.Bind("VFX Settings", "Surface Offset", 0.2f, "How far to push VFX out from terrain surfaces to prevent clipping. Base: 0.2");
            
            ModSettingsManager.AddOption(new SliderOption(BeamVfxXScale, new SliderConfig() { min = 0.1f, max = 20f, FormatString = "{0:0.0}" }));
            ModSettingsManager.AddOption(new SliderOption(BeamVfxYScale, new SliderConfig() { min = 0.1f, max = 20f, FormatString = "{0:0.0}" }));
            ModSettingsManager.AddOption(new SliderOption(BeamVfxZScale, new SliderConfig() { min = 0.1f, max = 20f, FormatString = "{0:0.0}" }));
            // Beam range slider for corrupt hand beam
            ModSettingsManager.AddOption(new SliderOption(BeamRange, new SliderConfig() { min = 10f, max = 300f, FormatString = "{0:0}" }));

            // Add Risk of Options entries for new settings
            ModSettingsManager.AddOption(new CheckBoxOption(LazerBeamModeRegular));
            ModSettingsManager.AddOption(new CheckBoxOption(LazerBeamModeCorrupt));
            ModSettingsManager.AddOption(new CheckBoxOption(EnableDebugLogging));
            ModSettingsManager.AddOption(new SliderOption(VfxSurfaceOffset, new SliderConfig() { min = 0.0f, max = 1.0f, FormatString = "{0:0.00}" }));

            // Load and set mod icon (simple, explicit approach)
            SetModIcon();

            ApplyMMHookPatches();
            Logger.LogInfo("Void Fiend Beam has awakened with Enhanced Laser Mode (MMHook version)!");
        }

        /// <summary>
        /// Load embedded icon.png and set it in RiskOfOptions via ModSettingsManager.SetModIcon
        /// Mirrors the simple approach used in reference code
        /// </summary>
        private void SetModIcon()
        {
            try
            {
                var asm = System.Reflection.Assembly.GetExecutingAssembly();
                string resourceName = null;
                foreach (var name in asm.GetManifestResourceNames())
                {
                    if (name.EndsWith("icon.png", StringComparison.OrdinalIgnoreCase))
                    {
                        resourceName = name;
                        break;
                    }
                }

                if (resourceName == null) return;

                using (var stream = asm.GetManifestResourceStream(resourceName))
                {
                    if (stream == null) return;

                    byte[] data = new byte[stream.Length];
                    stream.Read(data, 0, data.Length);

                    Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (!UnityEngine.ImageConversion.LoadImage(texture, data)) return;

                    Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));

                    try
                    {
                        ModSettingsManager.SetModIcon(sprite);
                        Logger.LogInfo("VoidFiendBeam: Mod icon set successfully via SetModIcon().");
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning($"VoidFiendBeam: ModSettingsManager.SetModIcon failed: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"VoidFiendBeam: failed to load mod icon: {ex.Message}");
            }
        }

        /// <summary>
        /// Apply all MMHook patches for enhanced laser mode
        /// </summary>
        private void ApplyMMHookPatches()
        {
            try
            {
                // ===== BLOOM ELIMINATION =====
                On.RoR2.CharacterBody.AddSpreadBloom += DisableBloomAccumulation_Hook;
                Logger.LogInfo("✅ Bloom elimination hook applied");

                // ===== BULLET ATTACK MODIFICATIONS =====
                On.RoR2.BulletAttack.Fire += BulletAttack_Fire_Hook;
                Logger.LogInfo("✅ BulletAttack Fire hook applied");

                // ===== CORRUPT HAND BEAM TRACKING =====
                On.EntityStates.VoidSurvivor.Weapon.FireCorruptHandBeam.OnEnter += FireCorruptHandBeam_OnEnter_Tracking;
                On.EntityStates.VoidSurvivor.Weapon.FireCorruptHandBeam.OnExit += FireCorruptHandBeam_OnExit_Tracking;
                Logger.LogInfo("✅ Corrupt hand beam tracking hooks applied");

                
                // ===== REGULAR HAND BEAM TRACKING =====
                On.EntityStates.VoidSurvivor.Weapon.FireHandBeam.OnEnter += FireHandBeam_OnEnter_Tracking;
                On.EntityStates.VoidSurvivor.Weapon.FireHandBeam.OnExit += FireHandBeam_OnExit_Tracking;
                Logger.LogInfo("✅ Regular hand beam tracking hooks applied");

                // ===== CORRUPT HAND BEAM MODIFICATIONS =====
                On.EntityStates.VoidSurvivor.Weapon.FireCorruptHandBeam.OnEnter += FireCorruptHandBeam_OnEnter_Modifications;
                On.EntityStates.VoidSurvivor.Weapon.FireCorruptHandBeam.FireBullet += FireCorruptHandBeam_FireBullet_Hook;
                Logger.LogInfo("✅ Corrupt hand beam modification hooks applied");

                Logger.LogInfo("🎯 All MMHook patches applied successfully!");
            }
            catch (Exception ex)
            {
                Logger.LogError($"❌ Failed to apply MMHook patches: {ex}");
            }
        }

        // ===== BLOOM ELIMINATION HOOK =====
        /// <summary>
        /// Hook to disable bloom accumulation - applies to BOTH beams
        /// </summary>
        private void DisableBloomAccumulation_Hook(On.RoR2.CharacterBody.orig_AddSpreadBloom orig, CharacterBody self, float bloom)
        {
            // Check if laser mode is on and if either beam is actively being fired
            if ((LaserModeEnabledForCorrupt && isCorruptHandBeamActive) || (LaserModeEnabledForRegular && isRegularHandBeamActive))
            {
                // Safety check to ensure we're only affecting the Void Fiend
                if (self.bodyIndex == BodyCatalog.FindBodyIndex("VoidSurvivorBody"))
                {
                    if (EnableDebugLogging.Value) StaticLogger.LogInfo($"🎯 BLOCKING bloom during active HandBeam ({bloom})");
                    return; // This is the line that blocks bloom by skipping the original method
                }
            }

            // If the conditions aren't met, run the game's normal bloom logic
            orig(self, bloom);
        }

        // ===== BULLET ATTACK HOOK =====
        /// <summary>
        /// Hook for BulletAttack.Fire to add world layer, VFX offset, and no-spread for BOTH beams
        /// </summary>
        private void BulletAttack_Fire_Hook(On.RoR2.BulletAttack.orig_Fire orig, BulletAttack self)
        {
            if (self.owner != null)
            {
                var characterBody = self.owner.GetComponent<CharacterBody>();
                if (characterBody != null &&
                    characterBody.bodyIndex == BodyCatalog.FindBodyIndex("VoidSurvivorBody"))
                {
                    // Always add a callback to apply VFX surface offset for terrain hits (world geometry)
                    var originalCallback = self.hitCallback;
                    self.hitCallback = (BulletAttack attack, ref BulletAttack.BulletHit hitInfo) =>
                    {
                        // If no collider, nothing to do
                        if (hitInfo.collider == null)
                        {
                            return originalCallback?.Invoke(attack, ref hitInfo) ?? true;
                        }

                        bool isWorld = (LayerIndex.world.mask & (1 << hitInfo.collider.gameObject.layer)) != 0;

                        // Compute offset position along surface normal
                        Vector3 offsetPosition = hitInfo.point + (hitInfo.surfaceNormal * VfxSurfaceOffset.Value);

                        if (isWorld)
                        {
                            // World: spawn effect manually at offset and suppress further processing (no damage)
                            if (attack.hitEffectPrefab != null)
                            {
                                EffectManager.SimpleEffect(attack.hitEffectPrefab, offsetPosition,
                                    Quaternion.LookRotation(hitInfo.surfaceNormal), true);
                            }
                            return false;
                        }

                        // Enemies: keep original behavior (run original callback which handles damage and VFX)
                        return originalCallback?.Invoke(attack, ref hitInfo) ?? true;
                    };

                    // Always enable world collisions so terrain is considered for VoidSurvivor bullets
                    self.hitMask |= LayerIndex.world.mask;

                    // Laser-only behavior: eliminate spread when Laser Mode is active and a beam is firing
                    if ((LaserModeEnabledForCorrupt && isCorruptHandBeamActive) || (LaserModeEnabledForRegular && isRegularHandBeamActive))
                    {
                        // Eliminate spread for laser mode
                        self.minSpread = 0f;
                        self.maxSpread = 0f;
                    }
                }
            }

            orig(self);
        }

        // ===== CORRUPT HAND BEAM TRACKING HOOKS =====
        /// <summary>
        /// Track when FireCorruptHandBeam starts (equivalent to HarmonyPostfix)
        /// </summary>
        private void FireCorruptHandBeam_OnEnter_Tracking(On.EntityStates.VoidSurvivor.Weapon.FireCorruptHandBeam.orig_OnEnter orig, FireCorruptHandBeam self)
        {
            orig(self); // Call original first (equivalent to Postfix behavior)

            if (!LaserModeEnabledForCorrupt) return;

            var characterBody = self.characterBody;
            if (characterBody != null &&
                characterBody.bodyIndex == BodyCatalog.FindBodyIndex("VoidSurvivorBody"))
            {
                var voidSurvivorController = characterBody.GetComponent<VoidSurvivorController>();
                if (voidSurvivorController != null && voidSurvivorController.isCorrupted)
                {
                    isCorruptHandBeamActive = true;
                    if (EnableDebugLogging.Value) StaticLogger.LogInfo("🟢 Corrupt FireHandBeam STARTED");
                }
            }
        }

        /// <summary>
        /// Track when FireCorruptHandBeam ends (equivalent to HarmonyPostfix)
        /// </summary>
        private void FireCorruptHandBeam_OnExit_Tracking(On.EntityStates.VoidSurvivor.Weapon.FireCorruptHandBeam.orig_OnExit orig, FireCorruptHandBeam self)
        {
            orig(self); // Call original first (equivalent to Postfix behavior)

            if (isCorruptHandBeamActive)
            {
                isCorruptHandBeamActive = false;
                if (EnableDebugLogging.Value) StaticLogger.LogInfo("🔴 Corrupt FireHandBeam ENDED");
            }
        }

        // ===== REGULAR HAND BEAM TRACKING HOOKS =====
        /// <summary>
        /// Track when FireHandBeam starts - RUNS BEFORE original (equivalent to HarmonyPrefix)
        /// </summary>
        private void FireHandBeam_OnEnter_Tracking(On.EntityStates.VoidSurvivor.Weapon.FireHandBeam.orig_OnEnter orig, FireHandBeam self)
        {
            // CRITICAL: Set flag BEFORE calling original to match Prefix timing
            if (LaserModeEnabledForRegular)
            {
                var characterBody = self.characterBody;
                if (characterBody != null &&
                    characterBody.bodyIndex == BodyCatalog.FindBodyIndex("VoidSurvivorBody"))
                {
                    var voidSurvivorController = characterBody.GetComponent<VoidSurvivorController>();
                    // This logic ensures it's the REGULAR beam
                    if (voidSurvivorController != null && !voidSurvivorController.isCorrupted)
                    {
                        if (!isRegularHandBeamActive)
                        {
                            isRegularHandBeamActive = true;
                            if (EnableDebugLogging.Value) StaticLogger.LogInfo("🟢 Regular FireHandBeam STARTED (Pre-Bloom)");
                        }
                    }
                }
            }

            orig(self); // Call original AFTER setting flag (equivalent to Prefix behavior)
        }

        /// <summary>
        /// Track when FireHandBeam ends (equivalent to HarmonyPostfix)
        /// </summary>
        private void FireHandBeam_OnExit_Tracking(On.EntityStates.VoidSurvivor.Weapon.FireHandBeam.orig_OnExit orig, FireHandBeam self)
        {
            orig(self); // Call original first (equivalent to Postfix behavior)

            if (LaserModeEnabledForRegular && isRegularHandBeamActive)
            {
                isRegularHandBeamActive = false;
                if (EnableDebugLogging.Value) StaticLogger.LogInfo("🔴 Regular FireHandBeam ENDED");
            }
        }

        // Removed invalid FireHandBeam.FireBullet hook (that method isn't exposed by FireHandBeam in MMHook).
        // Use existing BulletAttack.Fire hook and the OnEnter/OnExit tracking logs to correlate per-bullet behavior.

        // ===== CORRUPT HAND BEAM MODIFICATION HOOKS =====
        /// <summary>
        /// Modify FireCorruptHandBeam.OnEnter for range and VFX (equivalent to HarmonyPostfix)
        /// </summary>
        private void FireCorruptHandBeam_OnEnter_Modifications(On.EntityStates.VoidSurvivor.Weapon.FireCorruptHandBeam.orig_OnEnter orig, FireCorruptHandBeam self)
        {
            // Apply BEFORE original so the very first frame uses updated values
            var characterBody = self.characterBody;
            if (characterBody != null &&
                characterBody.bodyIndex == BodyCatalog.FindBodyIndex("VoidSurvivorBody"))
            {
                var voidSurvivorController = characterBody.GetComponent<VoidSurvivorController>();
                if (voidSurvivorController != null && voidSurvivorController.isCorrupted)
                {
                    // Always apply corrupt-beam max distance and vfx scaling for corrupted VoidSurvivor
                    self.maxDistance = BeamRange.Value;
                    if (self.beamVfxPrefab != null)
                    {
                        VoidFiendVFXBeam.ScaleBeamVFX(
                            self.beamVfxPrefab,
                            BeamVfxXScale.Value,
                            BeamVfxYScale.Value,
                            BeamVfxZScale.Value
                        );
                    }
                }
            }

            orig(self);
        }

        /// <summary>
        /// Patch FireCorruptHandBeam.FireBullet for consistent distance (equivalent to HarmonyPrefix)
        /// </summary>
        private void FireCorruptHandBeam_FireBullet_Hook(On.EntityStates.VoidSurvivor.Weapon.FireCorruptHandBeam.orig_FireBullet orig, FireCorruptHandBeam self)
        {
            // Modify BEFORE calling original (equivalent to Prefix behavior)
            var characterBody = self.characterBody;
            if (characterBody != null &&
                characterBody.bodyIndex == BodyCatalog.FindBodyIndex("VoidSurvivorBody"))
            {
                var voidSurvivorController = characterBody.GetComponent<VoidSurvivorController>();
                if (voidSurvivorController != null && voidSurvivorController.isCorrupted)
                {
                    // Ensure fired bullets from the corrupt beam use the configured max distance
                    self.minDistance = self.maxDistance;
                }
            }

            orig(self); // Call original AFTER modifications (equivalent to Prefix behavior)
        }


        // Helper to centralize the effective laser mode ON/OFF
        // Laser mode helpers:
        // - Regular beam is controlled by LazerBeamModeRegular
        // - Corrupt beam is controlled by LazerBeamModeCorrupt
        private static bool LaserModeEnabledForRegular => LazerBeamModeRegular.Value;
        private static bool LaserModeEnabledForCorrupt => LazerBeamModeCorrupt.Value;
    }

    // ===== UTILITY CLASS =====
    public static class VoidFiendVFXBeam
    {
        public static void ScaleBeamVFX(GameObject beamVfxPrefab, float newVfxXScale, float newVfxYScale, float newVfxZScale)
        {
            if (beamVfxPrefab == null) return;
            beamVfxPrefab.transform.localScale = new Vector3(newVfxXScale, newVfxYScale, newVfxZScale);
        }
    }
}




/*
 * ████████████████████████████████████████████████████████████████████████████████
 * ██                                                                             ██
 * ██                         MOD 3: VOID VFX UPDATE                              ██
 * ██                                                                             ██
 * ████████████████████████████████████████████████████████████████████████████████
 */

namespace VoidRampSwitcher
{
    [BepInPlugin("com.renovice.voidramp", "Void Ramp Switcher", "1.0.0")]
    public class VoidRampSwitcher : BaseUnityPlugin
    {
        // VoidSurvivor textures
        private Texture2D texRampVoidSurvivorBase1;
        private Texture2D texRampVoidSurvivorBase2;
        private Texture2D texRampVoidSurvivorCorrupted1;
        private Texture2D texRampVoidSurvivorCorrupted2;

        private Harmony harmony;

        // VoidSurvivor system
        private Dictionary<Material, Texture2D> originalTextures = new Dictionary<Material, Texture2D>();

        public void Awake()
        {
            Logger.LogInfo("Void Ramp Switcher loaded!");

            RoR2.RoR2Application.onLoad += () =>
            {
                LoadAllTextures();
                FindAndStoreOriginalTextures();
                harmony = new Harmony("com.renovice.voidramp");
                harmony.PatchAll();
                Logger.LogInfo("Ready to swap all ramps on void mode change");
            };
        }

        private void OnDestroy()
        {
            harmony?.UnpatchSelf();
            RestoreAllOriginalTextures();
        }

        private void LoadAllTextures()
        {
            var allTextures = Resources.FindObjectsOfTypeAll<Texture2D>();

            // VoidSurvivor textures
            texRampVoidSurvivorBase1 = FindTextureByExactName(allTextures, "texRampVoidSurvivorBase1");
            texRampVoidSurvivorBase2 = FindTextureByExactName(allTextures, "texRampVoidSurvivorBase2");
            texRampVoidSurvivorCorrupted1 = FindTextureByExactName(allTextures, "texRampVoidSurvivorCorrupted1");
            texRampVoidSurvivorCorrupted2 = FindTextureByExactName(allTextures, "texRampVoidSurvivorCorrupted2");

            Logger.LogInfo($"Loaded all textures successfully");
        }

        private Texture2D FindTextureByExactName(Texture2D[] textures, string name)
        {
            foreach (var texture in textures)
            {
                if (texture.name.Equals(name, System.StringComparison.OrdinalIgnoreCase))
                    return texture;
            }
            return null;
        }

        // ========== VOIDSURVIVOR SYSTEM ==========
        private void FindAndStoreOriginalTextures()
        {
            var allMaterials = Resources.FindObjectsOfTypeAll<Material>();

            foreach (var mat in allMaterials)
            {
                if (mat.HasProperty("_RemapTex"))
                {
                    var currentTex = mat.GetTexture("_RemapTex") as Texture2D;
                    if (currentTex == texRampVoidSurvivorBase1 || currentTex == texRampVoidSurvivorBase2)
                    {
                        originalTextures[mat] = currentTex;
                        Logger.LogInfo($"Stored VoidSurvivor texture: {mat.name} -> {currentTex.name}");
                    }
                }
            }
        }

        private void RestoreAllOriginalTextures()
        {
            foreach (var kvp in originalTextures)
            {
                if (kvp.Key && kvp.Value)
                {
                    kvp.Key.SetTexture("_RemapTex", kvp.Value);
                }
            }
        }

        private void ReplaceVoidSurvivorRamps()
        {
            if (texRampVoidSurvivorCorrupted1 == null || texRampVoidSurvivorCorrupted2 == null) return;

            foreach (var mat in originalTextures.Keys)
            {
                if (!mat) continue;

                var originalTex = originalTextures[mat];
                if (originalTex == texRampVoidSurvivorBase1)
                {
                    mat.SetTexture("_RemapTex", texRampVoidSurvivorCorrupted1);
                }
                else if (originalTex == texRampVoidSurvivorBase2)
                {
                    mat.SetTexture("_RemapTex", texRampVoidSurvivorCorrupted2);
                }
            }
        }

        // ========== HARMONY PATCHES ==========
        [HarmonyPatch(typeof(EntityStates.VoidSurvivor.CorruptMode.CorruptMode), "OnEnter")]
        public static class CorruptMode_OnEnter_Patch
        {
            static void Postfix()
            {
                var switcher = InstanceFinder.FindInstance<VoidRampSwitcher>();
                if (switcher != null)
                {
                    switcher.ReplaceVoidSurvivorRamps();
                    switcher.Logger.LogInfo("VOID MODE - Applied all ramp swaps");
                }
            }
        }

        [HarmonyPatch(typeof(EntityStates.VoidSurvivor.CorruptMode.CorruptMode), "OnExit")]
        public static class CorruptMode_OnExit_Patch
        {
            static void Postfix()
            {
                var switcher = InstanceFinder.FindInstance<VoidRampSwitcher>();
                if (switcher != null)
                {
                    switcher.RestoreAllOriginalTextures();
                    switcher.Logger.LogInfo("NORMAL MODE - Restored all original ramps");
                }
            }
        }
    }

    public static class InstanceFinder
    {
        public static T FindInstance<T>() where T : BaseUnityPlugin
        {
            foreach (var plugin in BepInEx.Bootstrap.Chainloader.PluginInfos.Values)
            {
                if (plugin.Instance is T instance)
                {
                    return instance;
                }
            }
            return null;
        }
    }
}




/*
 * ████████████████████████████████████████████████████████████████████████████████
 * ██                                                                             ██
 * ██                   MOD 4: OMNISPARK PREFAB CHANGE                            ██
 * ██                                                                             ██
 * ████████████████████████████████████████████████████████████████████████████████
 */

// The BepInPlugin attribute specifies the mod's unique identifier, name, and version.
[BepInPlugin("com.yourname.voidmodemuzzleflash", "Void Mode Muzzleflash Ramp Swap", "1.0.0")]
public class VoidModeMuzzleflashPlugin : BaseUnityPlugin
{
    // This will hold our custom texture once it's loaded from the embedded resource.
    private static Texture2D customRampTexture;

    // Store the original texture so we can restore it when exiting void mode
    private static Texture originalRampTexture;

    // Reference to the material we're modifying
    private static Material targetMaterial;

    // Reference to the void survivor controller if we're playing as Void Fiend
    private static VoidSurvivorController currentVoidController;

    // BuffDef for the corrupted state
    private static BuffDef corruptedBuffDef;

    /// <summary>
    /// The Awake method is called by BepInEx when the plugin is loaded.
    /// </summary>
    public void Awake()
    {
        // Load the custom texture from the embedded resource file.
        LoadCustomTexture();

        // If the texture was loaded successfully, set up our hooks
        if (customRampTexture != null)
        {
            // Hook into the game's loading process to find the material and buff
            RoR2Application.onLoad += OnGameLoad;

            // Hook into character body start to detect when a Void Fiend spawns
            On.RoR2.CharacterBody.Start += CharacterBody_Start;

            // Hook into buff application/removal
            On.RoR2.CharacterBody.AddBuff_BuffIndex += CharacterBody_AddBuff;
            On.RoR2.CharacterBody.RemoveBuff_BuffIndex += CharacterBody_RemoveBuff;

            Logger.LogInfo("Void Mode Muzzleflash Ramp Swap initialized successfully!");
        }
        else
        {
            Logger.LogError("Failed to load custom ramp texture. The mod will not function.");
        }
    }

    /// <summary>
    /// Loads the custom texture from embedded resources.
    /// </summary>
    private void LoadCustomTexture()
    {
        var assembly = Assembly.GetExecutingAssembly();
        string resourceName = "template.OmnisparkRed.png";

        try
        {
            using (Stream resourceStream = assembly.GetManifestResourceStream(resourceName))
            {
                if (resourceStream == null)
                {
                    Logger.LogError($"Embedded resource not found: {resourceName}");
                    return;
                }

                byte[] textureBytes = new byte[resourceStream.Length];
                resourceStream.Read(textureBytes, 0, textureBytes.Length);

                customRampTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                customRampTexture.LoadImage(textureBytes);

                // Ensure the texture persists between scene loads
                DontDestroyOnLoad(customRampTexture);

                Logger.LogInfo("Successfully loaded custom ramp texture.");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error loading embedded resource: {ex}");
        }
    }

    /// <summary>
    /// Called when the game finishes loading. Finds and caches the material and buff.
    /// </summary>
    private void OnGameLoad()
    {
        // Find the target material
        string targetMaterialName = "matOmniHitsparkVoid";
        targetMaterial = Resources.FindObjectsOfTypeAll<Material>()
            .FirstOrDefault(mat => mat.name == targetMaterialName);

        if (targetMaterial)
        {
            // Store the original texture so we can restore it later
            originalRampTexture = targetMaterial.GetTexture("_RemapTex");
            Logger.LogInfo($"Found material '{targetMaterialName}' and cached original texture.");
        }
        else
        {
            Logger.LogWarning($"Could not find material '{targetMaterialName}'.");
        }

        // Find the corrupted buff def used by Void Fiend
        // The buff is typically named "bdVoidSurvivorCorruptMode" or similar
        corruptedBuffDef = Resources.FindObjectsOfTypeAll<BuffDef>()
            .FirstOrDefault(buff => buff.name.Contains("VoidSurvivorCorrupt") ||
                                    buff.name.Contains("bdVoidSurvivorCorruptMode"));

        if (corruptedBuffDef)
        {
            Logger.LogInfo($"Found corrupted buff: {corruptedBuffDef.name}");
        }
        else
        {
            Logger.LogWarning("Could not find Void Survivor corruption buff.");
        }
    }

    /// <summary>
    /// Hook for when a character spawns - check if it's a Void Fiend
    /// </summary>
    private void CharacterBody_Start(On.RoR2.CharacterBody.orig_Start orig, CharacterBody self)
    {
        orig(self);

        // Check if this is the local player's Void Fiend
        if (self.isPlayerControlled && self.baseNameToken == "VOIDSURVIVOR_BODY_NAME")
        {
            currentVoidController = self.GetComponent<VoidSurvivorController>();
            if (currentVoidController)
            {
                Logger.LogInfo("Detected Void Fiend spawn - monitoring corruption state.");

                // Also get the buff from the controller if we haven't found it yet
                if (corruptedBuffDef == null && currentVoidController.corruptedBuffDef != null)
                {
                    corruptedBuffDef = currentVoidController.corruptedBuffDef;
                    Logger.LogInfo($"Got corrupted buff from controller: {corruptedBuffDef.name}");
                }
            }
        }
    }

    /// <summary>
    /// Hook for when a buff is added - check if it's the corruption buff
    /// </summary>
    private void CharacterBody_AddBuff(On.RoR2.CharacterBody.orig_AddBuff_BuffIndex orig, CharacterBody self, BuffIndex buffIndex)
    {
        orig(self, buffIndex);

        // Check if this is our local Void Fiend gaining the corruption buff
        if (corruptedBuffDef != null &&
            buffIndex == corruptedBuffDef.buffIndex &&
            self.isPlayerControlled &&
            self.baseNameToken == "VOIDSURVIVOR_BODY_NAME")
        {
            Logger.LogInfo("Void mode entered - swapping to custom muzzleflash texture!");
            SwapToCustomTexture();
        }
    }

    /// <summary>
    /// Hook for when a buff is removed - check if it's the corruption buff
    /// </summary>
    private void CharacterBody_RemoveBuff(On.RoR2.CharacterBody.orig_RemoveBuff_BuffIndex orig, CharacterBody self, BuffIndex buffIndex)
    {
        orig(self, buffIndex);

        // Check if this is our local Void Fiend losing the corruption buff
        if (corruptedBuffDef != null &&
            buffIndex == corruptedBuffDef.buffIndex &&
            self.isPlayerControlled &&
            self.baseNameToken == "VOIDSURVIVOR_BODY_NAME")
        {
            Logger.LogInfo("Void mode exited - restoring original muzzleflash texture!");
            RestoreOriginalTexture();
        }
    }

    /// <summary>
    /// Swaps the material's ramp texture to our custom one
    /// </summary>
    private void SwapToCustomTexture()
    {
        if (targetMaterial && customRampTexture)
        {
            targetMaterial.SetTexture("_RemapTex", customRampTexture);
            Logger.LogInfo("Successfully applied custom ramp texture.");
        }
    }

    /// <summary>
    /// Restores the material's original ramp texture
    /// </summary>
    private void RestoreOriginalTexture()
    {
        if (targetMaterial && originalRampTexture)
        {
            targetMaterial.SetTexture("_RemapTex", originalRampTexture);
            Logger.LogInfo("Successfully restored original ramp texture.");
        }
    }

    /// <summary>
    /// Clean up when the mod is unloaded
    /// </summary>
    private void OnDestroy()
    {
        // Always restore the original texture when the mod is unloaded
        RestoreOriginalTexture();
    }
}




/*
 * ████████████████████████████████████████████████████████████████████████████████
 * ██                                                                             ██
 * ██                       MOD 5: BILLBOARD FIRE EDITOR                          ██
 * ██                                                                             ██
 * ████████████████████████████████████████████████████████████████████████████████
 */

namespace VoidSurvivorCustomFlame
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    public class VoidSurvivorCustomFlamePlugin : BaseUnityPlugin
    {
        public const string PluginGUID = "com.yourname.voidsurvivor.customflame";
        public const string PluginName = "VoidSurvivor Custom Flame Texture";
        public const string PluginVersion = "1.0.0";

        // Names used in the game assets
        private const string PrefabName = "VoidSurvivorMegaBlasterExplosionSmall";
        private const string ChildName = "BillboardFire";
        private const string OriginalRampName = "texRampVoidSurvivorCorrupted2";
        private const string ReplacementRampName = "texRampVoidSurvivorBase1";

        private static Texture2D customFlameTexture;

        // Tracking created resources to avoid leaks and duplicate work
        private readonly System.Collections.Generic.List<Material> createdMaterials = new System.Collections.Generic.List<Material>();
        private readonly System.Collections.Generic.List<Texture2D> createdTextures = new System.Collections.Generic.List<Texture2D>();
        private readonly System.Collections.Generic.HashSet<int> modifiedPrefabs = new System.Collections.Generic.HashSet<int>();
        private readonly System.Collections.Generic.Dictionary<int, Material> originalSharedMaterials = new System.Collections.Generic.Dictionary<int, Material>();

        public void Awake()
        {
            Logger.LogInfo($"{PluginName} v{PluginVersion} loaded.");

            // Load embedded or external custom texture if present
            LoadCustomTexture();

            // Replace on awake in case assets are already loaded
            ReplaceRampOnPrefabs();

            // Also run replacement each time a scene loads so it's applied before VFX spawn
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;

            // Restore original shared materials for any modified prefabs (best-effort)
            try
            {
                var allPrefabs = Resources.FindObjectsOfTypeAll<GameObject>();
                foreach (var go in allPrefabs)
                {
                    if (go == null) continue;
                    int id = go.GetInstanceID();
                    if (!modifiedPrefabs.Contains(id)) continue;

                    var child = go.transform.Find(ChildName);
                    if (child == null) continue;
                    var psRenderer = child.GetComponent<ParticleSystemRenderer>();
                    if (psRenderer == null) continue;

                    if (originalSharedMaterials.TryGetValue(id, out var origMat) && origMat != null)
                    {
                        // Restore the original shared material
                        psRenderer.sharedMaterial = origMat;
                    }
                }
            }
            catch (Exception e)
            {
                Logger.LogWarning($"Error while restoring original materials: {e}");
            }

            // Destroy any materials/textures we created
            try
            {
                foreach (var mat in createdMaterials)
                {
                    // FIX: Add a null check for robustness. The material might have been destroyed by Unity during scene unload.
                    if (mat != null)
                    {
                        UnityEngine.Object.Destroy(mat);
                    }
                }
                createdMaterials.Clear();

                foreach (var tex in createdTextures)
                {
                    // FIX: Add a null check for robustness.
                    if (tex != null)
                    {
                        UnityEngine.Object.Destroy(tex);
                    }
                }
                createdTextures.Clear();

                modifiedPrefabs.Clear();
                originalSharedMaterials.Clear();
            }
            catch (Exception e)
            {
                Logger.LogWarning($"Error while destroying created resources: {e}");
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            try
            {
                ReplaceRampOnPrefabs();
            }
            catch (Exception e)
            {
                Logger.LogError($"Error in OnSceneLoaded: {e}");
            }
        }

        private void LoadCustomTexture()
        {
            try
            {
                // Try to load embedded resource first
                var asm = Assembly.GetExecutingAssembly();
                string[] resources = asm.GetManifestResourceNames();
                string match = null;
                foreach (var r in resources)
                {
                    if (r.IndexOf("custombillboardflame", StringComparison.OrdinalIgnoreCase) >= 0 && r.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                    {
                        match = r;
                        break;
                    }
                }

                if (match != null)
                {
                    using (var stream = asm.GetManifestResourceStream(match))
                    {
                        if (stream != null)
                        {
                            using (var ms = new System.IO.MemoryStream())
                            {
                                stream.CopyTo(ms);
                                var data = ms.ToArray();
                                var tex = new Texture2D(2, 2);
                                if (tex.LoadImage(data))
                                {
                                    tex.name = "CustomBillboardFlame";
                                    customFlameTexture = tex;
                                    Logger.LogInfo("Loaded embedded CustomBillboardFlame texture.");
                                    return;
                                }
                            }
                        }
                    }
                }

                // Fallback: look for file next to plugin DLL
                string pluginPath = System.IO.Path.GetDirectoryName(asm.Location) ?? string.Empty;
                string filePath = System.IO.Path.Combine(pluginPath, "Custombillboardflame.png");
                if (System.IO.File.Exists(filePath))
                {
                    var bytes = System.IO.File.ReadAllBytes(filePath);
                    var tex = new Texture2D(2, 2);
                    if (tex.LoadImage(bytes))
                    {
                        tex.name = "CustomBillboardFlame";
                        customFlameTexture = tex;
                        Logger.LogInfo($"Loaded CustomBillboardFlame from file: {filePath}");
                        return;
                    }
                }

                Logger.LogInfo("No custom billboard flame texture found (embedded or file). Will fallback to in-game texture if available.");
            }
            catch (Exception e)
            {
                Logger.LogError($"Failed to load custom texture: {e}");
            }
        }

        private void ReplaceRampOnPrefabs()
        {
            try
            {
                // Determine which texture to use: prefer customFlameTexture, else fallback to ReplacementRampName asset
                Texture2D replacementAsset = FindTextureByName(ReplacementRampName);
                Texture2D baseReplacement = customFlameTexture ?? replacementAsset;
                if (baseReplacement == null)
                {
                    Logger.LogWarning($"Replacement ramp texture not found (custom or '{ReplacementRampName}').");
                    return;
                }

                // Search loaded GameObject assets (prefabs) for the prefab name
                var candidates = Resources.FindObjectsOfTypeAll<GameObject>();
                int replacedCount = 0;
                foreach (var go in candidates)
                {
                    if (go == null) continue;
                    if (!go.name.Contains(PrefabName)) continue;

                    int id = go.GetInstanceID();
                    if (modifiedPrefabs.Contains(id))
                    {
                        // already modified this prefab asset
                        continue;
                    }

                    // Check if this asset contains the expected child
                    var child = go.transform.Find(ChildName);
                    if (child == null) continue;

                    var psRenderer = child.GetComponent<ParticleSystemRenderer>();
                    if (psRenderer == null) continue;

                    var sharedMat = psRenderer.sharedMaterial; // original shared material
                    if (sharedMat == null) continue;

                    // If the material already uses our replacement, skip
                    string[] currentTexProps = sharedMat.GetTexturePropertyNames();
                    bool alreadyUsing = false;
                    foreach (var p in currentTexProps)
                    {
                        var t = sharedMat.GetTexture(p);
                        if (t != null && (t == baseReplacement || (customFlameTexture != null && t.name == "CustomBillboardFlame")))
                        {
                            alreadyUsing = true;
                            break;
                        }
                    }
                    if (alreadyUsing) continue;

                    Logger.LogInfo($"Applying replacement ramp to prefab asset '{go.name}' (material '{sharedMat.name}').");

                    // Determine texture property to replace
                    string[] texProps = sharedMat.GetTexturePropertyNames();
                    string targetProp = null;

                    // Try to find property currently using the original ramp
                    foreach (var p in texProps)
                    {
                        var t = sharedMat.GetTexture(p);
                        if (t != null && t.name != null && t.name.IndexOf(OriginalRampName, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            targetProp = p;
                            break;
                        }
                    }

                    // Fallback: look for remap/palette keywords
                    if (targetProp == null)
                    {
                        string[] keywords = new[] { "remap", "palette", "ramp", "remaptex", "_Remap", "_Palette" };
                        foreach (var p in texProps)
                        {
                            foreach (var kw in keywords)
                            {
                                if (p.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    targetProp = p;
                                    break;
                                }
                            }
                            if (targetProp != null) break;
                        }
                    }

                    if (targetProp == null && texProps.Length > 0) targetProp = texProps[0];

                    if (targetProp == null)
                    {
                        Logger.LogWarning($"Could not determine texture property to replace on material '{sharedMat.name}'.");
                        continue;
                    }

                    // Prepare replacement texture instance — clone if we need to mutate its settings
                    Texture2D replacementForMaterial = baseReplacement;
                    var origTex = sharedMat.GetTexture(targetProp) as Texture2D;
                    if (origTex != null && baseReplacement != null)
                    {
                        // Clone the baseReplacement so we don't modify engine/original texture settings
                        try
                        {
                            replacementForMaterial = UnityEngine.Object.Instantiate(baseReplacement);
                            replacementForMaterial.name = (customFlameTexture != null ? "CustomBillboardFlame_Clone" : baseReplacement.name + "_Clone");
                            createdTextures.Add(replacementForMaterial);

                            // copy filtering/wrap from original
                            replacementForMaterial.filterMode = origTex.filterMode;
                            replacementForMaterial.wrapMode = origTex.wrapMode;
#if UNITY_2017_1_OR_NEWER
                            replacementForMaterial.wrapModeU = origTex.wrapModeU;
                            replacementForMaterial.wrapModeV = origTex.wrapModeV;
#endif
                        }
                        catch (Exception)
                        {
                            // fallback to using baseReplacement unmodified
                            replacementForMaterial = baseReplacement;
                        }
                    }

                    // Create a new material instance cloned from sharedMat so we preserve all render settings
                    var newMat = new Material(sharedMat);
                    newMat.name = sharedMat.name + " (CustomRamp)";

                    // Preserve renderQueue and instancing flag
                    newMat.renderQueue = sharedMat.renderQueue;
                    newMat.enableInstancing = sharedMat.enableInstancing;

                    // Copy original texture scale/offset and apply to new material
                    Vector2 scale = sharedMat.GetTextureScale(targetProp);
                    Vector2 offset = sharedMat.GetTextureOffset(targetProp);
                    newMat.SetTextureScale(targetProp, scale);
                    newMat.SetTextureOffset(targetProp, offset);

                    // Set the replacement/custom texture only on the ramp property of the cloned material
                    newMat.SetTexture(targetProp, replacementForMaterial);

                    // Assign the new material to the prefab's renderer (sharedMaterial) so only this prefab asset uses the modified material
                    psRenderer.sharedMaterial = newMat;

                    // Track resources for cleanup and avoid reapplying
                    createdMaterials.Add(newMat);
                    originalSharedMaterials[id] = sharedMat;
                    modifiedPrefabs.Add(id);

                    Logger.LogInfo($"Replaced ramp on prefab '{go.name}' material '{sharedMat.name}' property '{targetProp}' with '{(customFlameTexture != null ? "CustomBillboardFlame" : ReplacementRampName)}'.");

                    replacedCount++;
                }

                if (replacedCount == 0)
                {
                    Logger.LogWarning($"No prefabs named '{PrefabName}' were found to apply the ramp replacement.");
                }
            }
            catch (Exception e)
            {
                Logger.LogError($"Error while replacing ramps on prefabs: {e}");
            }
        }

        private Texture2D FindTextureByName(string name)
        {
            var textures = Resources.FindObjectsOfTypeAll<Texture2D>();
            foreach (var tex in textures)
            {
                if (tex != null && tex.name != null && tex.name.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    return tex;
                }
            }
            return null;
        }
    }
}




/*
 * ████████████████████████████████████████████████████████████████████████████████
 * ██                                                                             ██
 * ██               MOD 6: VOID SURVIVOR BEAM IMPACT COLOR CHANGE                 ██
 * ██                                                                             ██
 * ████████████████████████████████████████████████████████████████████████████████
 */

namespace VoidSurvivorColorMod
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    public class VoidSurvivorColorModPlugin : BaseUnityPlugin
    {
        public const string PluginGUID = "com.bartek.voidsurvivorcolormod";
        public const string PluginName = "VoidSurvivorColorMod";
        public const string PluginVersion = "1.0.0";

        // Individual color controls for each component
        private static readonly Color pointLightColor = new Color(0.471f, 0f, 1f, 0.150f);
        private static readonly Color brightFlashColor = new Color(1f, 0.2968f, 1f, 1f);
        private static readonly Color muzzleflashPointLightColor = new Color(0.471f, 0f, 1f, 0.150f);

        // New color controls for additional effects
        private static readonly Color megaBlasterBigGhostPointLightColor = new Color(0.471f, 0f, 1f, 0.150f);
        private static readonly Color chargeMegaBlasterPointLightColor = new Color(0.471f, 0f, 1f, 0.150f);
        private static readonly Color readyMegaBlasterPointLightColor = new Color(0.471f, 0f, 1f, 0.150f);
        private static readonly Color voidBlinkVfxPointLightColor = new Color(0.471f, 0f, 1f, 0.150f);
        private static readonly Color megaBlasterSmallGhostPointLightColor = new Color(0.471f, 0f, 1f, 0.150f);

        // FIX: Add dictionaries to store original colors to prevent permanent asset modification.
        private readonly Dictionary<Light, Color> originalLightColors = new Dictionary<Light, Color>();
        private readonly Dictionary<ParticleSystem, Color> originalParticleStartColors = new Dictionary<ParticleSystem, Color>();
        private readonly Dictionary<ParticleSystem, ParticleSystem.MinMaxGradient> originalParticleGradients = new Dictionary<ParticleSystem, ParticleSystem.MinMaxGradient>();


        private void Awake()
        {
            Logger.LogInfo($"Plugin {PluginGUID} is loaded!");

            // Hook into when prefabs are registered
            On.RoR2.EffectCatalog.Init += EffectCatalog_Init;
        }

        private void EffectCatalog_Init(On.RoR2.EffectCatalog.orig_Init orig)
        {
            // Call the original method first
            orig();

            // Find and modify the VoidSurvivorBeamImpact prefab
            ModifyVoidSurvivorBeamImpact();

            // Modify new void survivor effects
            ModifyVoidSurvivorMegaBlasterBigGhost();
            ModifyVoidSurvivorChargeMegaBlaster();
            ModifyVoidSurvivorReadyMegaBlaster();
            ModifyVoidBlinkVfx();
            ModifyVoidSurvivorMegaBlasterSmallGhost();
        }

        private void ModifyVoidSurvivorBeamImpact()
        {
            // Try to find the effect in the catalog
            GameObject voidSurvivorBeamImpactPrefab = FindVoidSurvivorBeamImpactPrefab();

            if (voidSurvivorBeamImpactPrefab != null)
            {
                Logger.LogInfo("Found VoidSurvivorBeamImpact prefab, applying color changes...");

                // Modify Point Light
                ModifyPointLight(voidSurvivorBeamImpactPrefab);

                // Modify BrightFlash particle system
                ModifyBrightFlashParticles(voidSurvivorBeamImpactPrefab);

                Logger.LogInfo("VoidSurvivorBeamImpact color modifications applied successfully!");
            }
            else
            {
                Logger.LogError("Could not find VoidSurvivorBeamImpact prefab!");
            }

            // Also modify VoidSurvivorBeamMuzzleflash
            ModifyVoidSurvivorBeamMuzzleflash();
        }

        private GameObject FindVoidSurvivorBeamImpactPrefab()
        {
            // First try to find it directly by name in loaded assets
            GameObject[] allGameObjects = Resources.FindObjectsOfTypeAll<GameObject>();

            foreach (GameObject obj in allGameObjects)
            {
                if (obj.name == "VoidSurvivorBeamImpact")
                {
                    Logger.LogInfo($"Found VoidSurvivorBeamImpact via Resources: {obj.name}");
                    return obj;
                }
            }

            // Try to find it in the effect catalog
            for (int i = 0; i < EffectCatalog.effectCount; i++)
            {
                EffectDef effectDef = EffectCatalog.GetEffectDef((EffectIndex)i);
                if (effectDef?.prefab?.name == "VoidSurvivorBeamImpact")
                {
                    Logger.LogInfo($"Found VoidSurvivorBeamImpact in EffectCatalog at index {i}");
                    return effectDef.prefab;
                }
            }

            Logger.LogWarning("VoidSurvivorBeamImpact prefab not found in standard locations, searching all effects...");

            // Log all effect names for debugging
            for (int i = 0; i < EffectCatalog.effectCount; i++)
            {
                EffectDef effectDef = EffectCatalog.GetEffectDef((EffectIndex)i);
                if (effectDef?.prefab != null && effectDef.prefab.name.ToLower().Contains("void"))
                {
                    Logger.LogInfo($"Found void-related effect: {effectDef.prefab.name}");

                    if (effectDef.prefab.name.Contains("BeamImpact"))
                    {
                        Logger.LogInfo($"Found beam impact effect, checking: {effectDef.prefab.name}");
                        return effectDef.prefab;
                    }
                }
            }

            return null;
        }

        private void ModifyPointLight(GameObject prefab)
        {
            // Find the Point Light child object (tolerant lookup)
            Transform pointLightTransform = FindChildByName(prefab.transform, "Point Light");

            if (pointLightTransform != null)
            {
                Light pointLight = pointLightTransform.GetComponent<Light>();

                if (pointLight != null)
                {
                    // FIX: Store the original color before changing it
                    if (!originalLightColors.ContainsKey(pointLight))
                    {
                        originalLightColors[pointLight] = pointLight.color;
                    }
                    pointLight.color = pointLightColor;
                    Logger.LogInfo($"Changed Point Light color from {originalLightColors[pointLight]} to {pointLightColor}");
                }
                else
                {
                    Logger.LogWarning("Point Light component not found on Point Light GameObject");
                }
            }
            else
            {
                Logger.LogWarning("Point Light child object not found");
            }
        }

        private void ModifyBrightFlashParticles(GameObject prefab)
        {
            // Find the BrightFlash child object
            Transform brightFlashTransform = FindChildByName(prefab.transform, "BrightFlash");

            if (brightFlashTransform != null)
            {
                ParticleSystem particleSystem = brightFlashTransform.GetComponent<ParticleSystem>();

                if (particleSystem != null)
                {
                    var main = particleSystem.main;
                    // FIX: Store the original color before changing it
                    if (!originalParticleStartColors.ContainsKey(particleSystem))
                    {
                        originalParticleStartColors[particleSystem] = main.startColor.color;
                    }
                    main.startColor = brightFlashColor;
                    Logger.LogInfo($"Changed BrightFlash particle system color from {originalParticleStartColors[particleSystem]} to {brightFlashColor}");

                    // Also modify the color over lifetime module if it exists
                    var colorOverLifetime = particleSystem.colorOverLifetime;
                    if (colorOverLifetime.enabled)
                    {
                        // FIX: Store original gradient
                        if (!originalParticleGradients.ContainsKey(particleSystem))
                        {
                            originalParticleGradients[particleSystem] = colorOverLifetime.color;
                        }

                        Gradient gradient = new Gradient();
                        // Create a gradient that starts with our desired color and fades out
                        GradientColorKey[] colorKeys = new GradientColorKey[2];
                        colorKeys[0].color = brightFlashColor;
                        colorKeys[0].time = 0.0f;
                        colorKeys[1].color = brightFlashColor;
                        colorKeys[1].time = 1.0f;

                        GradientAlphaKey[] alphaKeys = new GradientAlphaKey[2];
                        alphaKeys[0].alpha = 1.0f;
                        alphaKeys[0].time = 0.0f;
                        alphaKeys[1].alpha = 0.0f;
                        alphaKeys[1].time = 1.0f;

                        gradient.SetKeys(colorKeys, alphaKeys);
                        colorOverLifetime.color = gradient;
                        Logger.LogInfo("Updated BrightFlash color over lifetime gradient");
                    }
                }
                else
                {
                    Logger.LogWarning("ParticleSystem component not found on BrightFlash GameObject");
                }
            }
            else
            {
                Logger.LogWarning("BrightFlash child object not found");
            }
        }

        private void ModifyVoidSurvivorBeamMuzzleflash()
        {
            // Try to find the muzzleflash effect in the catalog
            GameObject voidSurvivorBeamMuzzleflashPrefab = FindVoidSurvivorBeamMuzzleflashPrefab();

            if (voidSurvivorBeamMuzzleflashPrefab != null)
            {
                Logger.LogInfo("Found VoidSurvivorBeamMuzzleflash prefab, applying color changes...");

                // Modify Point Light
                ModifyMuzzleflashPointLight(voidSurvivorBeamMuzzleflashPrefab);

                Logger.LogInfo("VoidSurvivorBeamMuzzleflash color modifications applied successfully!");
            }
            else
            {
                Logger.LogError("Could not find VoidSurvivorBeamMuzzleflash prefab!");
            }
        }

        private GameObject FindVoidSurvivorBeamMuzzleflashPrefab()
        {
            // First try to find it directly by name in loaded assets
            GameObject[] allGameObjects = Resources.FindObjectsOfTypeAll<GameObject>();

            foreach (GameObject obj in allGameObjects)
            {
                if (obj.name == "VoidSurvivorBeamMuzzleflash")
                {
                    Logger.LogInfo($"Found VoidSurvivorBeamMuzzleflash via Resources: {obj.name}");
                    return obj;
                }
            }

            // Try to find it in the effect catalog
            for (int i = 0; i < EffectCatalog.effectCount; i++)
            {
                EffectDef effectDef = EffectCatalog.GetEffectDef((EffectIndex)i);
                if (effectDef?.prefab?.name == "VoidSurvivorBeamMuzzleflash")
                {
                    Logger.LogInfo($"Found VoidSurvivorBeamMuzzleflash in EffectCatalog at index {i}");
                    return effectDef.prefab;
                }
            }

            Logger.LogWarning("VoidSurvivorBeamMuzzleflash prefab not found in standard locations, searching all effects...");

            // Log all effect names for debugging
            for (int i = 0; i < EffectCatalog.effectCount; i++)
            {
                EffectDef effectDef = EffectCatalog.GetEffectDef((EffectIndex)i);
                if (effectDef?.prefab != null && effectDef.prefab.name.ToLower().Contains("muzzle"))
                {
                    Logger.LogInfo($"Found muzzle-related effect: {effectDef.prefab.name}");

                    if (effectDef.prefab.name.Contains("VoidSurvivor"))
                    {
                        Logger.LogInfo($"Found VoidSurvivor muzzle effect, checking: {effectDef.prefab.name}");
                        return effectDef.prefab;
                    }
                }
            }

            return null;
        }

        private void ModifyMuzzleflashPointLight(GameObject prefab)
        {
            // Find the Point Light child object
            Transform pointLightTransform = FindChildByName(prefab.transform, "Point Light");

            if (pointLightTransform != null)
            {
                Light pointLight = pointLightTransform.GetComponent<Light>();

                if (pointLight != null)
                {
                    // FIX: Store the original color before changing it
                    if (!originalLightColors.ContainsKey(pointLight))
                    {
                        originalLightColors[pointLight] = pointLight.color;
                    }
                    pointLight.color = muzzleflashPointLightColor;
                    Logger.LogInfo($"Changed VoidSurvivorBeamMuzzleflash Point Light color from {originalLightColors[pointLight]} to {muzzleflashPointLightColor}");
                }
                else
                {
                    Logger.LogWarning("Point Light component not found on VoidSurvivorBeamMuzzleflash Point Light GameObject");
                }
            }
            else
            {
                Logger.LogWarning("Point Light child object not found in VoidSurvivorBeamMuzzleflash");
            }
        }

        // NEW METHODS FOR ADDITIONAL EFFECTS

        private void ModifyVoidSurvivorMegaBlasterBigGhost()
        {
            GameObject prefab = FindEffectPrefab("VoidSurvivorMegaBlasterBigGhost");
            if (prefab != null)
            {
                Logger.LogInfo("Found VoidSurvivorMegaBlasterBigGhost prefab, applying color changes...");
                ModifySpecificPointLight(prefab, "Point Light", megaBlasterBigGhostPointLightColor, "VoidSurvivorMegaBlasterBigGhost");
                Logger.LogInfo("VoidSurvivorMegaBlasterBigGhost color modifications applied successfully!");
            }
            else
            {
                Logger.LogError("Could not find VoidSurvivorMegaBlasterBigGhost prefab!");
            }
        }

        private void ModifyVoidSurvivorChargeMegaBlaster()
        {
            GameObject prefab = FindEffectPrefab("VoidSurvivorChargeMegaBlaster");
            if (prefab != null)
            {
                Logger.LogInfo("Found VoidSurvivorChargeMegaBlaster prefab, applying color changes...");
                ModifySpecificPointLight(prefab, "Point Light", chargeMegaBlasterPointLightColor, "VoidSurvivorChargeMegaBlaster");
                Logger.LogInfo("VoidSurvivorChargeMegaBlaster color modifications applied successfully!");
            }
            else
            {
                Logger.LogError("Could not find VoidSurvivorChargeMegaBlaster prefab!");
            }
        }

        private void ModifyVoidSurvivorReadyMegaBlaster()
        {
            GameObject prefab = FindEffectPrefab("VoidSurvivorReadyMegaBlaster");
            if (prefab != null)
            {
                Logger.LogInfo("Found VoidSurvivorReadyMegaBlaster prefab, applying color changes...");
                ModifySpecificPointLight(prefab, "Point Light", readyMegaBlasterPointLightColor, "VoidSurvivorReadyMegaBlaster");
                Logger.LogInfo("VoidSurvivorReadyMegaBlaster color modifications applied successfully!");
            }
            else
            {
                Logger.LogError("Could not find VoidSurvivorReadyMegaBlaster prefab!");
            }
        }

        private void ModifyVoidBlinkVfx()
        {
            GameObject prefab = FindEffectPrefab("VoidBlinkVfx");
            if (prefab != null)
            {
                Logger.LogInfo("Found VoidBlinkVfx prefab, applying color changes...");
                // For VoidBlinkVfx, the path is VoidBlinkVfx/Core/Point Light
                Transform coreTransform = prefab.transform.Find("Core");
                if (coreTransform != null)
                {
                    ModifySpecificPointLight(coreTransform.gameObject, "Point Light", voidBlinkVfxPointLightColor, "VoidBlinkVfx/Core");
                }
                else
                {
                    Logger.LogWarning("Core child object not found in VoidBlinkVfx");
                }
                Logger.LogInfo("VoidBlinkVfx color modifications applied successfully!");
            }
            else
            {
                Logger.LogError("Could not find VoidBlinkVfx prefab!");
            }
        }

        private void ModifyVoidSurvivorMegaBlasterSmallGhost()
        {
            GameObject prefab = FindEffectPrefab("VoidSurvivorMegaBlasterSmallGhost");
            if (prefab != null)
            {
                Logger.LogInfo("Found VoidSurvivorMegaBlasterSmallGhost prefab, applying color changes...");
                ModifySpecificPointLight(prefab, "Point Light", megaBlasterSmallGhostPointLightColor, "VoidSurvivorMegaBlasterSmallGhost");
                Logger.LogInfo("VoidSurvivorMegaBlasterSmallGhost color modifications applied successfully!");
            }
            else
            {
                Logger.LogError("Could not find VoidSurvivorMegaBlasterSmallGhost prefab!");
            }
        }

        // Generic method to find effect prefabs
        private GameObject FindEffectPrefab(string prefabName)
        {
            // First try to find it directly by name in loaded assets
            GameObject[] allGameObjects = Resources.FindObjectsOfTypeAll<GameObject>();

            foreach (GameObject obj in allGameObjects)
            {
                if (obj.name == prefabName)
                {
                    Logger.LogInfo($"Found {prefabName} via Resources: {obj.name}");
                    return obj;
                }
            }

            // Try to find it in the effect catalog
            for (int i = 0; i < EffectCatalog.effectCount; i++)
            {
                EffectDef effectDef = EffectCatalog.GetEffectDef((EffectIndex)i);
                if (effectDef?.prefab?.name == prefabName)
                {
                    Logger.LogInfo($"Found {prefabName} in EffectCatalog at index {i}");
                    return effectDef.prefab;
                }
            }

            Logger.LogWarning($"{prefabName} prefab not found in standard locations");
            return null;
        }

        // Generic method to modify point lights
        private void ModifySpecificPointLight(GameObject prefab, string pointLightChildName, Color newColor, string prefabContext)
        {
            Transform pointLightTransform = FindChildByName(prefab.transform, pointLightChildName);

            if (pointLightTransform != null)
            {
                Light pointLight = pointLightTransform.GetComponent<Light>();

                if (pointLight != null)
                {
                    // FIX: Store the original color before changing it
                    if (!originalLightColors.ContainsKey(pointLight))
                    {
                        originalLightColors[pointLight] = pointLight.color;
                    }
                    pointLight.color = newColor;
                    Logger.LogInfo($"Changed {prefabContext} Point Light color from {originalLightColors[pointLight]} to {newColor}");
                }
                else
                {
                    Logger.LogWarning($"Point Light component not found on {prefabContext} {pointLightChildName} GameObject");
                }
            }
            else
            {
                Logger.LogWarning($"{pointLightChildName} child object not found in {prefabContext}");
            }
        }

        private void OnDestroy()
        {
            // Clean up hooks
            On.RoR2.EffectCatalog.Init -= EffectCatalog_Init;
            
            // FIX: Restore all original colors to prevent asset state pollution
            Logger.LogInfo("Restoring original colors to all modified assets...");
            foreach(var pair in originalLightColors)
            {
                if(pair.Key != null) // Check if the component hasn't been destroyed
                {
                    pair.Key.color = pair.Value;
                }
            }
            foreach(var pair in originalParticleStartColors)
            {
                if(pair.Key != null)
                {
                    var main = pair.Key.main;
                    main.startColor = pair.Value;
                }
            }
            foreach(var pair in originalParticleGradients)
            {
                if(pair.Key != null)
                {
                    var col = pair.Key.colorOverLifetime;
                    col.color = pair.Value;
                }
            }
            Logger.LogInfo("Original asset colors restored.");
        }

        // Helper: tolerant child lookup
        // Searches all children (including inactive) and matches by name case-insensitively.
        // Also strips trailing ' (n)' suffixes Unity sometimes appends (e.g. "BrightFlash (1)").
        private Transform FindChildByName(Transform root, string desiredName)
        {
            if (root == null || string.IsNullOrEmpty(desiredName)) return null;

            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                string name = t.name;

                // Strip trailing " (n)" if present
                int idx = name.IndexOf(" (");
                if (idx >= 0)
                {
                    name = name.Substring(0, idx);
                }

                if (string.Equals(name, desiredName, StringComparison.OrdinalIgnoreCase))
                {
                    return t;
                }
            }

            return null;
        }
    }
}

/*
 * ████████████████████████████████████████████████████████████████████████████████
 * ██                                                                             ██
 * ██                             END OF GIGA MOD                                 ██
 * ██                                                                             ██
 * ████████████████████████████████████████████████████████████████████████████████
 */