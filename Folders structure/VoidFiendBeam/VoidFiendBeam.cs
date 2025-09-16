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

            // Laser Mode, Debug and VFX surface offset moved into the same 'Beam Settings' section
            LazerBeamModeRegular = Config.Bind("Beam Settings", "Enable Laser Mode (Regular Beam)", false, "Eliminates randomization for the regular hand beam, making it behave like a precise laser.");
            LazerBeamModeCorrupt = Config.Bind("Beam Settings", "Enable Laser Mode (Corrupt Beam)", false, "Eliminates randomization for the corrupt hand beam, making it behave like a precise laser.");
            EnableDebugLogging = Config.Bind("Beam Settings", "Enable Debug Logging", false, "Enable detailed logging for troubleshooting.");
            VfxSurfaceOffset = Config.Bind("Beam Settings", "Surface Offset", 0.2f, "How far to push VFX out from terrain surfaces to prevent clipping. Base: 0.2");


            ModSettingsManager.AddOption(new SliderOption(BeamVfxXScale, new SliderConfig() { min = 0.1f, max = 20f, FormatString = "{0:0.0}" }));
            ModSettingsManager.AddOption(new SliderOption(BeamVfxYScale, new SliderConfig() { min = 0.1f, max = 20f, FormatString = "{0:0.0}" }));
            ModSettingsManager.AddOption(new SliderOption(BeamVfxZScale, new SliderConfig() { min = 0.1f, max = 20f, FormatString = "{0:0.0}" }));
            // Beam range slider for corrupt hand beam
            ModSettingsManager.AddOption(new SliderOption(BeamRange, new SliderConfig() { min = 10f, max = 300f, FormatString = "{0:0}" }));

            // Add Risk of Options entries for laser mode, debug, and vfx offset (all under Beam Settings section)
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

                // ===== RECOIL ELIMINATION =====
                On.RoR2.CameraTargetParams.AddRecoil += DisableRecoil_Hook;
                Logger.LogInfo("✅ Recoil elimination hook applied");

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

        // ===== RECOIL ELIMINATION HOOK =====
        /// <summary>
        /// Hook to disable recoil in CameraTargetParams - applies to BOTH beams when Laser Mode is active
        /// </summary>
        private static void DisableRecoil_Hook(On.RoR2.CameraTargetParams.orig_AddRecoil orig, CameraTargetParams self, float verticalMin, float verticalMax, float horizontalMin, float horizontalMax)
        {
            // Get the character body from the camera target params
            CharacterBody characterBody = self.GetComponent<CharacterBody>();

            // Check if we have a valid body, laser mode is on, and if either beam is actively being fired
            if (characterBody != null &&
                ((LaserModeEnabledForCorrupt && isCorruptHandBeamActive) || (LaserModeEnabledForRegular && isRegularHandBeamActive)))
            {
                // Safety check to ensure we're only affecting the Void Fiend
                if (characterBody.bodyIndex == BodyCatalog.FindBodyIndex("VoidSurvivorBody"))
                {
                    if (EnableDebugLogging.Value) StaticLogger.LogInfo($"🎯 BLOCKING recoil during active HandBeam (v[{verticalMin}-{verticalMax}], h[{horizontalMin}-{horizontalMax}])");
                    return; // This is the line that blocks recoil by skipping the original method
                }
            }

            // If the conditions aren't met, run the game's normal recoil logic
            orig(self, verticalMin, verticalMax, horizontalMin, horizontalMax);
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