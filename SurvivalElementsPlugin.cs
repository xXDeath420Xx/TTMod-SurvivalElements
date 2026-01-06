using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using EquinoxsModUtils;
using EquinoxsModUtils.Additions;
using HarmonyLib;
using UnityEngine;
using TechtonicaFramework.API;
using TechtonicaFramework.Health;
using TechtonicaFramework.Core;

namespace SurvivalElements
{
    /// <summary>
    /// SurvivalElements - Adds survival mechanics to Techtonica
    /// Features: Machine health/damage, repair tools, power surges, environmental threats
    /// </summary>
    [BepInPlugin(MyGUID, PluginName, VersionString)]
    [BepInDependency("com.equinox.EquinoxsModUtils")]
    [BepInDependency("com.equinox.EMUAdditions")]
    [BepInDependency("com.certifired.TechtonicaFramework")]
    public class SurvivalElementsPlugin : BaseUnityPlugin
    {
        public const string MyGUID = "com.certifired.SurvivalElements";
        public const string PluginName = "SurvivalElements";
        public const string VersionString = "1.0.2";

        private static readonly Harmony Harmony = new Harmony(MyGUID);
        public static ManualLogSource Log;
        public static SurvivalElementsPlugin Instance;

        // Configuration
        public static ConfigEntry<bool> EnableMachineHealth;
        public static ConfigEntry<float> DefaultMachineHP;
        public static ConfigEntry<bool> EnablePowerSurges;
        public static ConfigEntry<float> PowerSurgeChance;
        public static ConfigEntry<float> PowerSurgeDamage;
        public static ConfigEntry<bool> EnableOverloadDamage;
        public static ConfigEntry<float> OverloadDamageRate;
        public static ConfigEntry<bool> DebugMode;

        // Repair Tool
        public const string RepairToolName = "Repair Tool";
        public const string RepairToolUnlock = "Repair Tool Tech";

        // Track machines with health
        private static HashSet<uint> healthyMachines = new HashSet<uint>();

        // Power surge timer
        private float powerSurgeTimer = 0f;
        private float powerSurgeInterval = 60f; // Check every 60 seconds

        private void Awake()
        {
            Instance = this;
            Log = Logger;
            Log.LogInfo($"{PluginName} v{VersionString} loading...");

            InitializeConfig();
            Harmony.PatchAll();

            // Register repair tool with EMUAdditions
            RegisterRepairTool();

            // Hook events
            EMU.Events.GameDefinesLoaded += OnGameDefinesLoaded;
            // Framework events will be enabled when TechtonicaFramework is fully operational
            // FrameworkEvents.OnMachineDestroyed += OnMachineDestroyed;
            // FrameworkEvents.OnMachineRepaired += OnMachineRepaired;

            Log.LogInfo($"{PluginName} v{VersionString} loaded!");
        }

        private void InitializeConfig()
        {
            EnableMachineHealth = Config.Bind("Machine Health", "Enable Machine Health", true,
                "Enable health system for machines");

            DefaultMachineHP = Config.Bind("Machine Health", "Default Machine HP", 100f,
                new ConfigDescription("Default health points for machines", new AcceptableValueRange<float>(10f, 1000f)));

            EnablePowerSurges = Config.Bind("Power Surges", "Enable Power Surges", true,
                "Random power surges can damage machines on the power grid");

            PowerSurgeChance = Config.Bind("Power Surges", "Surge Chance Per Minute", 0.05f,
                new ConfigDescription("Chance of power surge per minute (0.05 = 5%)", new AcceptableValueRange<float>(0f, 1f)));

            PowerSurgeDamage = Config.Bind("Power Surges", "Surge Damage", 25f,
                new ConfigDescription("Damage dealt by power surges", new AcceptableValueRange<float>(1f, 100f)));

            EnableOverloadDamage = Config.Bind("Overload", "Enable Overload Damage", true,
                "Machines take damage when power grid is overloaded");

            OverloadDamageRate = Config.Bind("Overload", "Overload Damage Rate", 5f,
                new ConfigDescription("Damage per second during overload", new AcceptableValueRange<float>(0.1f, 50f)));

            DebugMode = Config.Bind("General", "Debug Mode", false, "Enable debug logging");
        }

        private void RegisterRepairTool()
        {
            // Register unlock for Repair Tool
            EMUAdditions.AddNewUnlock(new NewUnlockDetails
            {
                category = Unlock.TechCategory.Logistics,
                coreTypeNeeded = ResearchCoreDefinition.CoreType.Green,
                coreCountNeeded = 50,
                description = "A handheld tool that repairs damaged machines. Hold near a damaged machine to restore its health.",
                displayName = RepairToolUnlock,
                requiredTier = TechTreeState.ResearchTier.Tier0,
                treePosition = 0
            });

            // Register the Repair Tool resource
            EMUAdditions.AddNewResource(new NewResourceDetails
            {
                name = RepairToolName,
                description = "Handheld repair tool. Aim at damaged machines and hold to repair them.",
                craftingMethod = CraftingMethod.Assembler,
                craftTierRequired = 0,
                headerTitle = "Equipment",
                // subHeaderTitle inherited from parent
                maxStackCount = 1,
                sortPriority = 100,
                unlockName = RepairToolUnlock,
                parentName = "Scanner" // Use Scanner as visual base
            });

            // Recipe for Repair Tool
            EMUAdditions.AddNewRecipe(new NewRecipeDetails
            {
                GUID = MyGUID,
                craftingMethod = CraftingMethod.Assembler,
                craftTierRequired = 0,
                duration = 10f,
                unlockName = RepairToolUnlock,
                ingredients = new List<RecipeResourceInfo>
                {
                    new RecipeResourceInfo("Iron Frame", 5),
                    new RecipeResourceInfo("Copper Wire", 10),
                    new RecipeResourceInfo("Iron Components", 5)
                },
                outputs = new List<RecipeResourceInfo>
                {
                    new RecipeResourceInfo(RepairToolName, 1)
                },
                sortPriority = 100
            });

            LogDebug("Repair Tool registered");
        }

        private void OnGameDefinesLoaded()
        {
            // Link unlock to resource
            var repairToolInfo = EMU.Resources.GetResourceInfoByName(RepairToolName);
            if (repairToolInfo != null)
            {
                repairToolInfo.unlock = EMU.Unlocks.GetUnlockByName(RepairToolUnlock);
                LogDebug($"Repair Tool linked to unlock");
            }
        }

        private void Update()
        {
            if (!EnableMachineHealth.Value) return;

            // Power surge checks
            if (EnablePowerSurges.Value)
            {
                powerSurgeTimer += Time.deltaTime;
                if (powerSurgeTimer >= powerSurgeInterval)
                {
                    powerSurgeTimer = 0f;
                    CheckPowerSurge();
                }
            }

            // Repair tool usage
            CheckRepairToolUsage();
        }

        private void CheckPowerSurge()
        {
            if (UnityEngine.Random.value < PowerSurgeChance.Value)
            {
                TriggerPowerSurge();
            }
        }

        private void TriggerPowerSurge()
        {
            Log.LogInfo("POWER SURGE! Machines on the grid are taking damage!");

            // Damage random machines that have health
            int damagedCount = 0;
            foreach (uint machineId in healthyMachines)
            {
                // 30% chance to affect each machine
                if (UnityEngine.Random.value < 0.3f)
                {
                    FrameworkAPI.DamageMachine(machineId, PowerSurgeDamage.Value, DamageType.PowerSurge);
                    damagedCount++;
                }
            }

            if (damagedCount > 0)
            {
                LogDebug($"Power surge damaged {damagedCount} machines");
                // TODO: Show notification to player
            }
        }

        private void CheckRepairToolUsage()
        {
            // Check if player is holding repair tool and using it
            var player = Player.instance;
            if (player == null) return;

            // Check if Fire1 is held (repair action)
            if (UnityEngine.Input.GetButton("Fire1"))
            {
                // Raycast to find machine in front of player
                var cam = Camera.main;
                if (cam == null) return;

                Ray ray = cam.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2, 0));
                if (Physics.Raycast(ray, out RaycastHit hit, 5f))
                {
                    // Try to get machine from hit object
                    var machineObj = hit.collider.gameObject;
                    uint machineId = GetMachineIdFromObject(machineObj);

                    if (machineId != 0 && healthyMachines.Contains(machineId))
                    {
                        float healthPercent = FrameworkAPI.GetMachineHealthPercent(machineId);
                        if (healthPercent < 1f)
                        {
                            // Repair the machine
                            FrameworkAPI.HealMachine(machineId, 10f * Time.deltaTime);
                            LogDebug($"Repairing machine {machineId}: {healthPercent * 100:F0}%");
                        }
                    }
                }
            }
        }

        private uint GetMachineIdFromObject(GameObject obj)
        {
            // Try to find machine instance ID from game object
            // This is a simplified implementation - relies on Harmony patches for actual ID lookup
            // The machine ID would be obtained through the game's internal systems
            return 0;
        }

        private void OnMachineDestroyed(uint machineId)
        {
            healthyMachines.Remove(machineId);
            Log.LogWarning($"Machine {machineId} was destroyed!");
            // TODO: Trigger visual destruction, drop salvage, etc.
        }

        private void OnMachineRepaired(uint machineId)
        {
            LogDebug($"Machine {machineId} fully repaired");
        }

        /// <summary>
        /// Register a machine to have health (called by Harmony patches)
        /// </summary>
        public static void RegisterMachineWithHealth(uint machineId, float maxHealth = 0)
        {
            if (!EnableMachineHealth.Value) return;
            if (maxHealth <= 0) maxHealth = DefaultMachineHP.Value;

            FrameworkAPI.RegisterMachineHealth(machineId, maxHealth);
            healthyMachines.Add(machineId);
            LogDebug($"Registered machine {machineId} with {maxHealth} HP");
        }

        /// <summary>
        /// Unregister a machine (called when deconstructed)
        /// </summary>
        public static void UnregisterMachine(uint machineId)
        {
            healthyMachines.Remove(machineId);
        }

        public static void LogDebug(string message)
        {
            if (DebugMode != null && DebugMode.Value)
            {
                Log.LogInfo($"[DEBUG] {message}");
            }
        }
    }

    /// <summary>
    /// Harmony patches for machine health integration
    /// </summary>
    [HarmonyPatch]
    public static class MachineHealthPatches
    {
        /// <summary>
        /// Register machines with health when they're built
        /// </summary>
        [HarmonyPatch(typeof(GridManager), "BuildObj", new Type[] { typeof(GenericMachineInstanceRef) })]
        [HarmonyPostfix]
        public static void OnMachineBuilt(GenericMachineInstanceRef machineRef)
        {
            try
            {
                if (machineRef.IsValid())
                {
                    SurvivalElementsPlugin.RegisterMachineWithHealth(machineRef.instanceId);
                }
            }
            catch (Exception ex)
            {
                SurvivalElementsPlugin.Log.LogError($"Error registering machine health: {ex.Message}");
            }
        }

        /// <summary>
        /// Unregister machines when they're removed
        /// </summary>
        [HarmonyPatch(typeof(GridManager), "RemoveObj", new Type[] { typeof(GenericMachineInstanceRef) })]
        [HarmonyPrefix]
        public static void OnMachineRemoved(GenericMachineInstanceRef machineRef)
        {
            try
            {
                if (machineRef.IsValid())
                {
                    SurvivalElementsPlugin.UnregisterMachine(machineRef.instanceId);
                }
            }
            catch { }
        }
    }

    /// <summary>
    /// Visual feedback for damaged machines
    /// </summary>
    [HarmonyPatch]
    public static class DamageVisualPatches
    {
        // Colors for damage states
        private static readonly Color HealthyColor = Color.white;
        private static readonly Color DamagedColor = new Color(1f, 0.8f, 0.2f); // Yellow-ish
        private static readonly Color CriticalColor = new Color(1f, 0.2f, 0.2f); // Red

        private static Dictionary<uint, float> lastHealthCheck = new Dictionary<uint, float>();

        /// <summary>
        /// Apply damage tint to machines based on health
        /// </summary>
        public static void UpdateMachineVisuals(uint machineId, GameObject visual)
        {
            if (visual == null) return;

            float healthPercent = FrameworkAPI.GetMachineHealthPercent(machineId);

            // Only update if health changed significantly
            if (lastHealthCheck.TryGetValue(machineId, out float lastHealth))
            {
                if (Mathf.Abs(healthPercent - lastHealth) < 0.01f) return;
            }
            lastHealthCheck[machineId] = healthPercent;

            // Determine color based on health
            Color targetColor;
            if (healthPercent > 0.75f)
            {
                targetColor = HealthyColor;
            }
            else if (healthPercent > 0.25f)
            {
                targetColor = Color.Lerp(DamagedColor, HealthyColor, (healthPercent - 0.25f) / 0.5f);
            }
            else
            {
                targetColor = Color.Lerp(CriticalColor, DamagedColor, healthPercent / 0.25f);
            }

            // Apply color to renderers
            var renderers = visual.GetComponentsInChildren<MeshRenderer>(true);
            foreach (var renderer in renderers)
            {
                MaterialPropertyBlock propBlock = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(propBlock);
                propBlock.SetColor("_Color", targetColor);
                propBlock.SetColor("_BaseColor", targetColor);
                renderer.SetPropertyBlock(propBlock);
            }
        }
    }
}
