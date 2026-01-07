using System;
using System.Collections;
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
using TechtonicaFramework.TechTree;

namespace SurvivalElements
{
    /// <summary>
    /// SurvivalElements - Adds survival mechanics to Techtonica
    /// Features: Machine health/damage, repair tools, power surges, food, hunger, player health
    /// </summary>
    [BepInPlugin(MyGUID, PluginName, VersionString)]
    [BepInDependency("com.equinox.EquinoxsModUtils")]
    [BepInDependency("com.equinox.EMUAdditions")]
    [BepInDependency("com.certifired.TechtonicaFramework")]
    public class SurvivalElementsPlugin : BaseUnityPlugin
    {
        public const string MyGUID = "com.certifired.SurvivalElements";
        public const string PluginName = "SurvivalElements";
        public const string VersionString = "2.6.0";

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

        // Food & Cooking
        public const string FoodUnlock = "Sustenance Tech";
        public const string RawMeatName = "Raw Meat";
        public const string CookedMeatName = "Cooked Meat";
        public const string PlantStewName = "Plant Stew";
        public const string EnergyBarName = "Energy Bar";
        public const string NutrientPasteName = "Nutrient Paste";

        // Track machines with health - maps instanceId to GameObject for visual updates
        private static HashSet<uint> healthyMachines = new HashSet<uint>();
        private static Dictionary<uint, GameObject> machineVisuals = new Dictionary<uint, GameObject>();
        private static Dictionary<GameObject, uint> visualToMachineId = new Dictionary<GameObject, uint>();

        // Power surge timer
        private float powerSurgeTimer = 0f;
        private float powerSurgeInterval = 60f; // Check every 60 seconds

        // Hunger system
        public static float CurrentHunger { get; private set; } = 100f;
        public static float MaxHunger { get; private set; } = 100f;
        private float hungerDecayTimer = 0f;

        // Player health system
        public static float PlayerHealth { get; private set; } = 100f;
        public static float PlayerMaxHealth { get; private set; } = 100f;
        private float healthRegenTimer = 0f;

        // UI references
        private static HungerHealthUI uiInstance;
        private static DeathScreenUI deathScreenInstance;
        private static MachineHealthDisplayUI machineHealthUI;

        // Death tracking
        public static string LastDamageSource { get; private set; } = "unknown";
        public static bool IsDead { get; private set; } = false;
        private static Vector3 elevatorRespawnPosition = Vector3.zero;
        private static bool hasFoundElevator = false;

        private void Awake()
        {
            Instance = this;
            Log = Logger;
            Log.LogInfo($"{PluginName} v{VersionString} loading...");

            InitializeConfig();
            Harmony.PatchAll();

            // Register repair tool with EMUAdditions
            RegisterRepairTool();

            // Register food items
            RegisterFoodItems();

            // Hook events
            EMU.Events.GameDefinesLoaded += OnGameDefinesLoaded;
            EMU.Events.GameLoaded += OnGameLoaded;
            EMU.Events.TechTreeStateLoaded += OnTechTreeStateLoaded;
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

            // Hunger config
            EnableHunger = Config.Bind("Hunger", "Enable Hunger System", true,
                "Enable hunger that depletes over time");

            HungerDecayRate = Config.Bind("Hunger", "Hunger Decay Per Minute", 2f,
                new ConfigDescription("Hunger points lost per minute", new AcceptableValueRange<float>(0.1f, 10f)));

            StarvationDamage = Config.Bind("Hunger", "Starvation Damage Per Second", 1f,
                new ConfigDescription("Damage per second when starving", new AcceptableValueRange<float>(0.1f, 10f)));

            // Player health config
            EnablePlayerHealth = Config.Bind("Player Health", "Enable Player Health", true,
                "Enable player health system with damage and regeneration");

            PlayerHealthRegen = Config.Bind("Player Health", "Health Regen Per Second", 0.5f,
                new ConfigDescription("Health regeneration when well-fed", new AcceptableValueRange<float>(0f, 5f)));

            ShowHealthHungerUI = Config.Bind("UI", "Show Health/Hunger UI", true,
                "Display health and hunger bars on screen");

            RespawnDelay = Config.Bind("Death", "Respawn Delay", 5f,
                new ConfigDescription("Seconds to wait before respawning", new AcceptableValueRange<float>(1f, 30f)));

            RespawnAtElevator = Config.Bind("Death", "Respawn At Elevator", true,
                "Respawn at the main elevator instead of death location");

            DebugMode = Config.Bind("General", "Debug Mode", false, "Enable debug logging");
        }

        // Additional config entries
        public static ConfigEntry<bool> EnableHunger;
        public static ConfigEntry<float> HungerDecayRate;
        public static ConfigEntry<float> StarvationDamage;
        public static ConfigEntry<bool> EnablePlayerHealth;
        public static ConfigEntry<float> PlayerHealthRegen;
        public static ConfigEntry<bool> ShowHealthHungerUI;
        public static ConfigEntry<float> RespawnDelay;
        public static ConfigEntry<bool> RespawnAtElevator;

        private void RegisterRepairTool()
        {
            // Register unlock for Repair Tool - Modded category
            EMUAdditions.AddNewUnlock(new NewUnlockDetails
            {
                category = ModdedTabModule.ModdedCategory,
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

        private void RegisterFoodItems()
        {
            // ========== FOOD UNLOCK ========== Modded category
            EMUAdditions.AddNewUnlock(new NewUnlockDetails
            {
                category = ModdedTabModule.ModdedCategory,
                coreTypeNeeded = ResearchCoreDefinition.CoreType.Green,
                coreCountNeeded = 30,
                description = "Research food preparation techniques to stave off hunger.",
                displayName = FoodUnlock,
                requiredTier = TechTreeState.ResearchTier.Tier0,
                treePosition = 0
            });

            // ========== RAW MEAT ==========
            // Raw meat is obtained from wildlife, not crafted
            // But we need to register it so cooked meat recipe works
            EMUAdditions.AddNewResource(new NewResourceDetails
            {
                name = RawMeatName,
                description = "Raw meat from wildlife. Should be cooked before consumption.",
                craftingMethod = CraftingMethod.Assembler, // Placeholder - item won't actually be craftable without recipe
                craftTierRequired = 0,
                headerTitle = "Food",
                maxStackCount = 50,
                sortPriority = 300,
                unlockName = FoodUnlock,
                parentName = "Plantmatter"
            });

            // ========== COOKED MEAT ==========
            EMUAdditions.AddNewResource(new NewResourceDetails
            {
                name = CookedMeatName,
                description = "Well-cooked meat. Restores 40 hunger when consumed.",
                craftingMethod = CraftingMethod.Smelter,
                craftTierRequired = 0,
                headerTitle = "Food",
                maxStackCount = 30,
                sortPriority = 301,
                unlockName = FoodUnlock,
                parentName = "Plantmatter"
            });

            EMUAdditions.AddNewRecipe(new NewRecipeDetails
            {
                GUID = MyGUID + "_cookedmeat",
                craftingMethod = CraftingMethod.Smelter,
                craftTierRequired = 0,
                duration = 5f,
                unlockName = FoodUnlock,
                ingredients = new List<RecipeResourceInfo>
                {
                    // NOTE: Using vanilla resource to avoid IndexOutOfRangeException
                    // Modded resources as ingredients cause crafting UI crashes
                    new RecipeResourceInfo("Plantmatter", 5)
                },
                outputs = new List<RecipeResourceInfo>
                {
                    new RecipeResourceInfo(CookedMeatName, 1)
                },
                sortPriority = 301
            });

            // ========== PLANT STEW ==========
            EMUAdditions.AddNewResource(new NewResourceDetails
            {
                name = PlantStewName,
                description = "A hearty stew made from local plants. Restores 30 hunger.",
                craftingMethod = CraftingMethod.Smelter,
                craftTierRequired = 0,
                headerTitle = "Food",
                maxStackCount = 20,
                sortPriority = 302,
                unlockName = FoodUnlock,
                parentName = "Plantmatter"
            });

            EMUAdditions.AddNewRecipe(new NewRecipeDetails
            {
                GUID = MyGUID + "_plantstew",
                craftingMethod = CraftingMethod.Smelter,
                craftTierRequired = 0,
                duration = 8f,
                unlockName = FoodUnlock,
                ingredients = new List<RecipeResourceInfo>
                {
                    new RecipeResourceInfo("Plantmatter", 10),
                    new RecipeResourceInfo("Kindlevine", 2)
                },
                outputs = new List<RecipeResourceInfo>
                {
                    new RecipeResourceInfo(PlantStewName, 2)
                },
                sortPriority = 302
            });

            // ========== ENERGY BAR ==========
            EMUAdditions.AddNewResource(new NewResourceDetails
            {
                name = EnergyBarName,
                description = "Compact, long-lasting energy bar. Restores 25 hunger. Great for exploration.",
                craftingMethod = CraftingMethod.Assembler,
                craftTierRequired = 0,
                headerTitle = "Food",
                maxStackCount = 50,
                sortPriority = 303,
                unlockName = FoodUnlock,
                parentName = "Plantmatter"
            });

            EMUAdditions.AddNewRecipe(new NewRecipeDetails
            {
                GUID = MyGUID + "_energybar",
                craftingMethod = CraftingMethod.Assembler,
                craftTierRequired = 0,
                duration = 10f,
                unlockName = FoodUnlock,
                ingredients = new List<RecipeResourceInfo>
                {
                    new RecipeResourceInfo("Plantmatter Fiber", 5),
                    new RecipeResourceInfo("Kindlevine Extract", 2)
                },
                outputs = new List<RecipeResourceInfo>
                {
                    new RecipeResourceInfo(EnergyBarName, 3)
                },
                sortPriority = 303
            });

            // ========== NUTRIENT PASTE ==========
            EMUAdditions.AddNewResource(new NewResourceDetails
            {
                name = NutrientPasteName,
                description = "Processed nutrient paste. Not tasty, but efficient. Restores 50 hunger.",
                craftingMethod = CraftingMethod.Assembler,
                craftTierRequired = 0,
                headerTitle = "Food",
                maxStackCount = 30,
                sortPriority = 304,
                unlockName = FoodUnlock,
                parentName = "Biobrick"
            });

            EMUAdditions.AddNewRecipe(new NewRecipeDetails
            {
                GUID = MyGUID + "_nutrientpaste",
                craftingMethod = CraftingMethod.Assembler,
                craftTierRequired = 0,
                duration = 15f,
                unlockName = FoodUnlock,
                ingredients = new List<RecipeResourceInfo>
                {
                    new RecipeResourceInfo("Biobrick", 5),
                    new RecipeResourceInfo("Plantmatter", 20),
                    new RecipeResourceInfo("Shiverthorn Extract", 1)
                },
                outputs = new List<RecipeResourceInfo>
                {
                    new RecipeResourceInfo(NutrientPasteName, 5)
                },
                sortPriority = 304
            });

            LogDebug("Food items registered");
        }

        private void OnGameLoaded()
        {
            // Reset hunger/health to full on game load
            CurrentHunger = MaxHunger;
            PlayerHealth = PlayerMaxHealth;
            IsDead = false;

            // Create UI if enabled
            if (ShowHealthHungerUI.Value)
            {
                CreateHealthHungerUI();
            }

            // Find elevator for respawn
            FindElevatorRespawnPoint();

            Log.LogInfo("SurvivalElements: Game loaded, hunger/health systems active");
        }

        private void FindElevatorRespawnPoint()
        {
            StartCoroutine(FindElevatorDelayed());
        }

        private IEnumerator FindElevatorDelayed()
        {
            yield return new WaitForSeconds(2f);

            try
            {
                // Try to find the main elevator in the scene
                // Look for "Elevator" tagged or named objects
                var elevators = GameObject.FindObjectsOfType<Transform>();
                foreach (var t in elevators)
                {
                    if (t.name.ToLower().Contains("elevator") && t.name.ToLower().Contains("main"))
                    {
                        elevatorRespawnPosition = t.position + Vector3.up * 2f; // Slightly above
                        hasFoundElevator = true;
                        Log.LogInfo($"Found elevator respawn point: {elevatorRespawnPosition}");
                        yield break;
                    }
                }

                // Fallback: Look for elevator by tag or component
                var player = Player.instance;
                if (player != null)
                {
                    // Use the initial player spawn as fallback respawn
                    elevatorRespawnPosition = player.transform.position;
                    hasFoundElevator = true;
                    Log.LogInfo($"Using player start position as respawn: {elevatorRespawnPosition}");
                }
            }
            catch (Exception ex)
            {
                Log.LogWarning($"Error finding elevator: {ex.Message}");
            }
        }

        private void CreateHealthHungerUI()
        {
            // Create the UI after a short delay to ensure game UI is ready
            StartCoroutine(CreateUIDelayed());
        }

        private IEnumerator CreateUIDelayed()
        {
            yield return new WaitForSeconds(1f);

            // Create health/hunger UI
            var uiObj = new GameObject("HungerHealthUI");
            UnityEngine.Object.DontDestroyOnLoad(uiObj);
            uiInstance = uiObj.AddComponent<HungerHealthUI>();

            // Create death screen UI
            var deathObj = new GameObject("DeathScreenUI");
            UnityEngine.Object.DontDestroyOnLoad(deathObj);
            deathScreenInstance = deathObj.AddComponent<DeathScreenUI>();

            // Create machine health display UI
            if (EnableMachineHealth.Value)
            {
                var machineUIObj = new GameObject("MachineHealthDisplayUI");
                UnityEngine.Object.DontDestroyOnLoad(machineUIObj);
                machineHealthUI = machineUIObj.AddComponent<MachineHealthDisplayUI>();
            }

            LogDebug("Health/Hunger UI, Death Screen, and Machine Health Display created");
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

        private void OnTechTreeStateLoaded()
        {
            // PT level tier mapping (from game):
            // - LIMA: Tier1-Tier4
            // - VICTOR: Tier5-Tier11
            // - XRAY: Tier12-Tier16
            // - SIERRA: Tier17-Tier24

            // Repair Tool: VICTOR (Tier6), position 90 (early-mid game utility)
            // Food Processing: LIMA (Tier2), position 90 (early game survival)
            ConfigureUnlock(RepairToolUnlock, "Iron Components", TechTreeState.ResearchTier.Tier6, 90);
            ConfigureUnlock(FoodUnlock, "Plantmatter", TechTreeState.ResearchTier.Tier2, 90);
            Log.LogInfo("Configured SurvivalElements unlock tiers");
        }

        private void ConfigureUnlock(string unlockName, string spriteSourceName, TechTreeState.ResearchTier tier, int position)
        {
            try
            {
                Unlock unlock = EMU.Unlocks.GetUnlockByName(unlockName);
                if (unlock == null)
                {
                    LogDebug($"Unlock '{unlockName}' not found");
                    return;
                }

                // Set the correct tier explicitly
                unlock.requiredTier = tier;

                // Set explicit position to avoid collisions
                unlock.treePosition = position;

                // Copy sprite from source for proper tech tree icon
                if (unlock.sprite == null)
                {
                    ResourceInfo sourceRes = EMU.Resources.GetResourceInfoByName(spriteSourceName);
                    if (sourceRes != null && sourceRes.sprite != null)
                    {
                        unlock.sprite = sourceRes.sprite;
                    }
                    else
                    {
                        // Try unlock
                        Unlock sourceUnlock = EMU.Unlocks.GetUnlockByName(spriteSourceName);
                        if (sourceUnlock != null && sourceUnlock.sprite != null)
                        {
                            unlock.sprite = sourceUnlock.sprite;
                        }
                    }
                }

                LogDebug($"Configured unlock '{unlockName}': tier={tier}, position={position}");
            }
            catch (Exception ex)
            {
                Log.LogWarning($"Failed to configure unlock {unlockName}: {ex.Message}");
            }
        }

        private void Update()
        {
            // Machine health system
            if (EnableMachineHealth.Value)
            {
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

            // Hunger system
            if (EnableHunger.Value)
            {
                UpdateHunger();
            }

            // Player health system
            if (EnablePlayerHealth.Value)
            {
                UpdatePlayerHealth();
            }
        }

        private void UpdateHunger()
        {
            // Decay hunger over time
            hungerDecayTimer += Time.deltaTime;
            if (hungerDecayTimer >= 60f) // Every minute
            {
                hungerDecayTimer = 0f;
                CurrentHunger = Mathf.Max(0f, CurrentHunger - HungerDecayRate.Value);

                if (CurrentHunger <= 0)
                {
                    Log.LogWarning("STARVING! Take damage until you eat!");
                }
                else if (CurrentHunger <= 25f)
                {
                    Log.LogWarning($"Hunger critically low: {CurrentHunger:F0}%");
                }
            }

            // Starvation damage when hunger is 0
            if (CurrentHunger <= 0 && EnablePlayerHealth.Value)
            {
                DamagePlayer(StarvationDamage.Value * Time.deltaTime, "starvation");
            }
        }

        private void UpdatePlayerHealth()
        {
            // Health regeneration when well-fed (hunger > 50%)
            if (CurrentHunger > 50f && PlayerHealth < PlayerMaxHealth)
            {
                healthRegenTimer += Time.deltaTime;
                if (healthRegenTimer >= 1f)
                {
                    healthRegenTimer = 0f;
                    PlayerHealth = Mathf.Min(PlayerMaxHealth, PlayerHealth + PlayerHealthRegen.Value);
                }
            }
            else
            {
                healthRegenTimer = 0f;
            }

            // Check for death
            if (PlayerHealth <= 0)
            {
                OnPlayerDeath();
            }
        }

        /// <summary>
        /// Damage the player - can be called by other mods (e.g., TurretDefense)
        /// </summary>
        public static void DamagePlayer(float damage, string source = "unknown")
        {
            if (!EnablePlayerHealth.Value) return;
            if (IsDead) return; // Can't damage dead player

            // Track damage source for death message
            LastDamageSource = source;

            PlayerHealth = Mathf.Max(0f, PlayerHealth - damage);

            // Log significant damage (combat-related)
            if (damage >= 5f)
            {
                Log.LogInfo($"Player took {damage:F0} damage from {source}, HP: {PlayerHealth:F0}/{PlayerMaxHealth}");
            }
            else
            {
                LogDebug($"Player took {damage:F1} damage from {source}, health: {PlayerHealth:F0}/{PlayerMaxHealth}");
            }

            // Update UI
            if (uiInstance != null)
            {
                uiInstance.FlashDamage();
            }

            // Check for death immediately
            if (PlayerHealth <= 0)
            {
                Instance?.TriggerDeath();
            }
        }

        /// <summary>
        /// Heal the player
        /// </summary>
        public static void HealPlayer(float amount)
        {
            PlayerHealth = Mathf.Min(PlayerMaxHealth, PlayerHealth + amount);
            LogDebug($"Player healed {amount:F1}, health: {PlayerHealth:F0}/{PlayerMaxHealth}");
        }

        /// <summary>
        /// Restore hunger
        /// </summary>
        public static void RestoreHunger(float amount)
        {
            CurrentHunger = Mathf.Min(MaxHunger, CurrentHunger + amount);
            Log.LogInfo($"Restored {amount:F0} hunger, now: {CurrentHunger:F0}/{MaxHunger}");
        }

        /// <summary>
        /// Get food value for an item
        /// </summary>
        public static float GetFoodValue(string itemName)
        {
            return itemName switch
            {
                CookedMeatName => 40f,
                PlantStewName => 30f,
                EnergyBarName => 25f,
                NutrientPasteName => 50f,
                RawMeatName => 10f, // Eating raw gives less benefit
                _ => 0f
            };
        }

        /// <summary>
        /// Trigger player death - shows death screen and initiates respawn
        /// </summary>
        public void TriggerDeath()
        {
            if (IsDead) return; // Already dead

            IsDead = true;
            Log.LogError($"PLAYER DIED! Cause: {LastDamageSource}");

            // Show death screen
            if (deathScreenInstance != null)
            {
                deathScreenInstance.ShowDeathScreen(LastDamageSource);
            }

            // Start respawn countdown
            StartCoroutine(RespawnAfterDelay());
        }

        private IEnumerator RespawnAfterDelay()
        {
            float delay = RespawnDelay.Value;
            Log.LogInfo($"Respawning in {delay} seconds...");

            yield return new WaitForSeconds(delay);

            PerformRespawn();
        }

        private void PerformRespawn()
        {
            Log.LogInfo("Respawning player...");

            // Reset health/hunger with death penalty
            PlayerHealth = PlayerMaxHealth * 0.5f; // Respawn at 50% health
            CurrentHunger = MaxHunger * 0.3f; // Respawn hungry

            // Teleport to elevator if enabled
            if (RespawnAtElevator.Value && hasFoundElevator)
            {
                var player = Player.instance;
                if (player != null)
                {
                    // Teleport player to elevator
                    var cc = player.GetComponent<CharacterController>();
                    if (cc != null)
                    {
                        cc.enabled = false; // Disable to allow teleport
                        player.transform.position = elevatorRespawnPosition;
                        cc.enabled = true;
                    }
                    else
                    {
                        player.transform.position = elevatorRespawnPosition;
                    }
                    Log.LogInfo($"Teleported player to elevator: {elevatorRespawnPosition}");
                }
            }

            // Hide death screen
            if (deathScreenInstance != null)
            {
                deathScreenInstance.HideDeathScreen();
            }

            // Reset death state
            IsDead = false;
            LastDamageSource = "unknown";

            Log.LogInfo("Player respawned!");
        }

        private void OnPlayerDeath()
        {
            // Legacy method - now just triggers the new death system
            TriggerDeath();
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
            if (obj == null) return 0;

            // First check for our tracker component (fastest)
            var tracker = obj.GetComponentInParent<MachineHealthTracker>();
            if (tracker != null)
            {
                return tracker.MachineId;
            }

            // Check our direct lookup
            if (visualToMachineId.TryGetValue(obj, out uint id))
                return id;

            // Try parent objects (in case we hit a child collider)
            Transform current = obj.transform;
            while (current != null)
            {
                if (visualToMachineId.TryGetValue(current.gameObject, out id))
                    return id;
                current = current.parent;
            }

            return 0;
        }

        /// <summary>
        /// Get machine health data for UI display
        /// </summary>
        public static (bool found, float current, float max, float percent) GetMachineHealthInfo(uint machineId)
        {
            if (!healthyMachines.Contains(machineId))
                return (false, 0, 0, 1f);

            float percent = FrameworkAPI.GetMachineHealthPercent(machineId);
            float max = DefaultMachineHP.Value;
            float current = max * percent;

            return (true, current, max, percent);
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
        public static void RegisterMachineWithHealth(uint machineId, float maxHealth = 0, GameObject visual = null)
        {
            if (!EnableMachineHealth.Value) return;
            if (maxHealth <= 0) maxHealth = DefaultMachineHP.Value;

            FrameworkAPI.RegisterMachineHealth(machineId, maxHealth);
            healthyMachines.Add(machineId);

            // Track visual reference for raycasting and visual updates
            if (visual != null)
            {
                machineVisuals[machineId] = visual;
                visualToMachineId[visual] = machineId;

                // Also add a tracker component for easy identification
                var tracker = visual.GetComponent<MachineHealthTracker>();
                if (tracker == null)
                {
                    tracker = visual.AddComponent<MachineHealthTracker>();
                }
                tracker.MachineId = machineId;
            }

            LogDebug($"Registered machine {machineId} with {maxHealth} HP (visual: {visual?.name ?? "none"})");
        }

        /// <summary>
        /// Unregister a machine (called when deconstructed)
        /// </summary>
        public static void UnregisterMachine(uint machineId)
        {
            healthyMachines.Remove(machineId);

            // Clean up visual tracking
            if (machineVisuals.TryGetValue(machineId, out var visual))
            {
                if (visual != null)
                {
                    visualToMachineId.Remove(visual);
                }
                machineVisuals.Remove(machineId);
            }
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
    /// Simple component to track machine ID on GameObjects
    /// </summary>
    public class MachineHealthTracker : MonoBehaviour
    {
        public uint MachineId;
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
                    // Try to get the visual GameObject from the machine reference
                    GameObject visual = null;
                    try
                    {
                        // Access commonInfo.refGameObj via reflection or direct access
                        var commonInfo = machineRef.GetCommonInfo();
                        if (commonInfo.refGameObj != null)
                        {
                            visual = commonInfo.refGameObj;
                        }
                    }
                    catch (Exception ex)
                    {
                        SurvivalElementsPlugin.LogDebug($"Could not get visual for machine {machineRef.instanceId}: {ex.Message}");
                    }

                    SurvivalElementsPlugin.RegisterMachineWithHealth(machineRef.instanceId, 0, visual);
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

    /// <summary>
    /// Enhanced UI for displaying player health and hunger with modern styling
    /// </summary>
    public class HungerHealthUI : MonoBehaviour
    {
        private GUIStyle healthBarStyle;
        private GUIStyle hungerBarStyle;
        private GUIStyle backgroundStyle;
        private GUIStyle borderStyle;
        private GUIStyle labelStyle;
        private GUIStyle iconStyle;
        private GUIStyle warningStyle;

        private float damageFlashTimer = 0f;
        private bool isFlashing = false;
        private float damageVignetteAlpha = 0f;

        // Textures
        private Texture2D healthTex;
        private Texture2D hungerTex;
        private Texture2D backgroundTex;
        private Texture2D borderTex;
        private Texture2D vignetteTex;

        void Awake()
        {
            // Initialize styles
            healthBarStyle = new GUIStyle();
            hungerBarStyle = new GUIStyle();
            backgroundStyle = new GUIStyle();
            borderStyle = new GUIStyle();
            labelStyle = new GUIStyle();
            iconStyle = new GUIStyle();
            warningStyle = new GUIStyle();
        }

        void OnGUI()
        {
            if (!SurvivalElementsPlugin.ShowHealthHungerUI.Value) return;
            if (SurvivalElementsPlugin.IsDead) return; // Hide during death screen

            // Setup styles if needed
            if (healthTex == null)
            {
                SetupStyles();
            }

            // Draw damage vignette when taking damage
            if (damageVignetteAlpha > 0)
            {
                DrawDamageVignette();
            }

            // Position in bottom-left corner (more visible, doesn't overlap game UI)
            float barWidth = 280f;
            float barHeight = 28f;
            float padding = 15f;
            float borderWidth = 3f;
            float startX = padding;
            float startY = Screen.height - (barHeight * 2 + padding * 3 + 40);

            // ========== HEALTH BAR ==========
            float healthPercent = SurvivalElementsPlugin.PlayerHealth / SurvivalElementsPlugin.PlayerMaxHealth;

            // Outer border
            GUI.color = new Color(0.15f, 0.15f, 0.15f, 0.95f);
            GUI.Box(new Rect(startX - borderWidth, startY - borderWidth,
                barWidth + borderWidth * 2 + 80, barHeight + borderWidth * 2), "", borderStyle);

            // Background
            GUI.color = new Color(0.08f, 0.08f, 0.08f, 0.9f);
            GUI.Box(new Rect(startX, startY, barWidth, barHeight), "", backgroundStyle);

            // Health fill with gradient effect
            Color healthColor = isFlashing ? Color.white : GetHealthColor(healthPercent);
            GUI.color = healthColor;
            GUI.Box(new Rect(startX + 2, startY + 2, (barWidth - 4) * healthPercent, barHeight - 4), "", healthBarStyle);

            // Low health pulsing effect
            if (healthPercent < 0.25f)
            {
                float pulse = Mathf.Sin(Time.time * 5f) * 0.3f + 0.3f;
                GUI.color = new Color(1f, 0f, 0f, pulse);
                GUI.Box(new Rect(startX, startY, barWidth, barHeight), "", backgroundStyle);
            }

            GUI.color = Color.white;

            // Health icon (heart symbol using text)
            GUI.Label(new Rect(startX + 5, startY + 2, 30, barHeight), "\u2665", iconStyle); // ♥

            // Health text overlay
            string healthText = $"{SurvivalElementsPlugin.PlayerHealth:F0} / {SurvivalElementsPlugin.PlayerMaxHealth:F0}";
            GUI.Label(new Rect(startX + barWidth + 10, startY + 4, 80, barHeight), healthText, labelStyle);

            // ========== HUNGER BAR ==========
            startY += barHeight + padding;
            float hungerPercent = SurvivalElementsPlugin.CurrentHunger / SurvivalElementsPlugin.MaxHunger;

            // Outer border
            GUI.color = new Color(0.15f, 0.15f, 0.15f, 0.95f);
            GUI.Box(new Rect(startX - borderWidth, startY - borderWidth,
                barWidth + borderWidth * 2 + 80, barHeight + borderWidth * 2), "", borderStyle);

            // Background
            GUI.color = new Color(0.08f, 0.08f, 0.08f, 0.9f);
            GUI.Box(new Rect(startX, startY, barWidth, barHeight), "", backgroundStyle);

            // Hunger fill
            Color hungerColor = GetHungerColor(hungerPercent);
            GUI.color = hungerColor;
            GUI.Box(new Rect(startX + 2, startY + 2, (barWidth - 4) * hungerPercent, barHeight - 4), "", hungerBarStyle);

            GUI.color = Color.white;

            // Hunger icon (food/drumstick using text)
            GUI.Label(new Rect(startX + 5, startY + 2, 30, barHeight), "\u25CF", iconStyle); // ●

            // Hunger text overlay
            string hungerText = $"{SurvivalElementsPlugin.CurrentHunger:F0}%";
            GUI.Label(new Rect(startX + barWidth + 10, startY + 4, 80, barHeight), hungerText, labelStyle);

            // ========== STATUS WARNINGS ==========
            startY += barHeight + padding;
            if (SurvivalElementsPlugin.CurrentHunger <= 0)
            {
                float blink = Mathf.Sin(Time.time * 8f) > 0 ? 1f : 0.5f;
                GUI.color = new Color(1f, 0.2f, 0.2f, blink);
                GUI.Label(new Rect(startX, startY, 300, 30), "!! STARVING - FIND FOOD !!", warningStyle);
                GUI.color = Color.white;
            }
            else if (SurvivalElementsPlugin.CurrentHunger <= 25f)
            {
                GUI.color = new Color(1f, 0.8f, 0.2f, 1f);
                GUI.Label(new Rect(startX, startY, 300, 30), "Hungry - Eat soon", warningStyle);
                GUI.color = Color.white;
            }

            if (SurvivalElementsPlugin.PlayerHealth <= 25f && SurvivalElementsPlugin.PlayerHealth > 0)
            {
                float blink = Mathf.Sin(Time.time * 6f) > 0 ? 1f : 0.3f;
                GUI.color = new Color(1f, 0f, 0f, blink);
                GUI.Label(new Rect(startX, startY + 25, 300, 30), "!! CRITICAL HEALTH !!", warningStyle);
                GUI.color = Color.white;
            }
        }

        void Update()
        {
            if (isFlashing)
            {
                damageFlashTimer -= Time.deltaTime;
                if (damageFlashTimer <= 0)
                {
                    isFlashing = false;
                }
            }

            // Fade damage vignette
            if (damageVignetteAlpha > 0)
            {
                damageVignetteAlpha -= Time.deltaTime * 2f;
            }
        }

        public void FlashDamage()
        {
            isFlashing = true;
            damageFlashTimer = 0.15f;
            damageVignetteAlpha = 0.4f; // Flash red vignette on damage
        }

        private void DrawDamageVignette()
        {
            if (vignetteTex == null)
            {
                vignetteTex = MakeVignetteTexture(64, 64);
            }

            GUI.color = new Color(1f, 0f, 0f, damageVignetteAlpha);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), vignetteTex, ScaleMode.StretchToFill);
            GUI.color = Color.white;
        }

        private void SetupStyles()
        {
            // Health bar (gradient red)
            healthTex = MakeGradientTexture(32, 8, new Color(0.7f, 0.15f, 0.15f, 1f), new Color(0.9f, 0.3f, 0.3f, 1f));
            healthBarStyle.normal.background = healthTex;

            // Hunger bar (gradient orange)
            hungerTex = MakeGradientTexture(32, 8, new Color(0.7f, 0.45f, 0.1f, 1f), new Color(0.9f, 0.6f, 0.2f, 1f));
            hungerBarStyle.normal.background = hungerTex;

            // Background (dark)
            backgroundTex = MakeTexture(2, 2, new Color(0.05f, 0.05f, 0.05f, 0.85f));
            backgroundStyle.normal.background = backgroundTex;

            // Border
            borderTex = MakeTexture(2, 2, new Color(0.2f, 0.2f, 0.2f, 0.95f));
            borderStyle.normal.background = borderTex;

            // Label
            labelStyle.normal.textColor = Color.white;
            labelStyle.fontSize = 16;
            labelStyle.fontStyle = FontStyle.Bold;
            labelStyle.alignment = TextAnchor.MiddleLeft;

            // Icon style
            iconStyle.normal.textColor = Color.white;
            iconStyle.fontSize = 20;
            iconStyle.fontStyle = FontStyle.Bold;
            iconStyle.alignment = TextAnchor.MiddleLeft;

            // Warning style
            warningStyle.normal.textColor = Color.white;
            warningStyle.fontSize = 18;
            warningStyle.fontStyle = FontStyle.Bold;
            warningStyle.alignment = TextAnchor.MiddleLeft;
        }

        private Color GetHealthColor(float percent)
        {
            if (percent > 0.6f) return new Color(0.2f, 0.85f, 0.3f); // Green
            if (percent > 0.3f) return new Color(0.95f, 0.85f, 0.2f); // Yellow
            return new Color(0.9f, 0.2f, 0.2f); // Red
        }

        private Color GetHungerColor(float percent)
        {
            if (percent > 0.5f) return new Color(0.9f, 0.65f, 0.2f); // Orange
            if (percent > 0.25f) return new Color(0.85f, 0.45f, 0.15f); // Dark orange
            return new Color(0.7f, 0.25f, 0.15f); // Brown/red
        }

        private Texture2D MakeTexture(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; i++)
                pix[i] = col;
            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }

        private Texture2D MakeGradientTexture(int width, int height, Color bottom, Color top)
        {
            Texture2D tex = new Texture2D(width, height);
            for (int y = 0; y < height; y++)
            {
                Color col = Color.Lerp(bottom, top, (float)y / height);
                for (int x = 0; x < width; x++)
                {
                    tex.SetPixel(x, y, col);
                }
            }
            tex.Apply();
            return tex;
        }

        private Texture2D MakeVignetteTexture(int width, int height)
        {
            Texture2D tex = new Texture2D(width, height);
            Vector2 center = new Vector2(width / 2f, height / 2f);
            float maxDist = Mathf.Sqrt(center.x * center.x + center.y * center.y);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    float alpha = Mathf.Clamp01(dist / maxDist);
                    alpha = alpha * alpha; // Stronger at edges
                    tex.SetPixel(x, y, new Color(1f, 0f, 0f, alpha));
                }
            }
            tex.Apply();
            return tex;
        }
    }

    /// <summary>
    /// Death screen UI - Shows "YOU DIED" splash with cause and respawn countdown
    /// </summary>
    public class DeathScreenUI : MonoBehaviour
    {
        private bool isVisible = false;
        private string deathCause = "";
        private float respawnTimer = 0f;

        private GUIStyle titleStyle;
        private GUIStyle subtitleStyle;
        private GUIStyle countdownStyle;
        private GUIStyle tipStyle;
        private Texture2D overlayTex;

        private float fadeAlpha = 0f;
        private float glitchTimer = 0f;

        // Death tips
        private static readonly string[] DeathTips = new string[]
        {
            "Tip: Keep your hunger above 50% to regenerate health.",
            "Tip: Craft food at a Smelter or Assembler to stay alive.",
            "Tip: Watch out for hostile drones and environmental hazards.",
            "Tip: The Repair Tool can fix damaged machines.",
            "Tip: Energy Bars are lightweight and great for exploration.",
            "Tip: Nutrient Paste restores the most hunger.",
            "Tip: Avoid starvation - it slowly drains your health!",
            "Tip: Stay away from radiation zones without protection."
        };
        private string currentTip = "";

        void Awake()
        {
            titleStyle = new GUIStyle();
            subtitleStyle = new GUIStyle();
            countdownStyle = new GUIStyle();
            tipStyle = new GUIStyle();
        }

        void Update()
        {
            if (isVisible)
            {
                // Update respawn timer
                if (respawnTimer > 0)
                {
                    respawnTimer -= Time.deltaTime;
                }

                // Fade in effect
                fadeAlpha = Mathf.MoveTowards(fadeAlpha, 0.85f, Time.deltaTime * 2f);

                // Glitch effect timer
                glitchTimer += Time.deltaTime;
            }
            else
            {
                // Fade out
                fadeAlpha = Mathf.MoveTowards(fadeAlpha, 0f, Time.deltaTime * 3f);
            }
        }

        void OnGUI()
        {
            if (fadeAlpha <= 0) return;

            // Setup styles
            if (overlayTex == null)
            {
                SetupStyles();
            }

            // Dark overlay
            GUI.color = new Color(0f, 0f, 0f, fadeAlpha);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), overlayTex);
            GUI.color = Color.white;

            if (!isVisible) return;

            float centerX = Screen.width / 2f;
            float centerY = Screen.height / 2f;

            // Glitch offset for "YOU DIED" text
            float glitchX = 0;
            float glitchY = 0;
            if (Mathf.Sin(glitchTimer * 15f) > 0.9f)
            {
                glitchX = UnityEngine.Random.Range(-5f, 5f);
                glitchY = UnityEngine.Random.Range(-2f, 2f);
            }

            // ========== YOU DIED ==========
            // Red shadow/glow
            GUI.color = new Color(0.6f, 0f, 0f, 0.5f);
            GUI.Label(new Rect(centerX - 302 + glitchX, centerY - 148 + glitchY, 600, 100), "YOU DIED", titleStyle);
            GUI.Label(new Rect(centerX - 298 + glitchX, centerY - 152 + glitchY, 600, 100), "YOU DIED", titleStyle);

            // Main text
            GUI.color = new Color(0.85f, 0.1f, 0.1f, 1f);
            GUI.Label(new Rect(centerX - 300 + glitchX, centerY - 150 + glitchY, 600, 100), "YOU DIED", titleStyle);
            GUI.color = Color.white;

            // ========== DEATH CAUSE ==========
            string causeText = FormatDeathCause(deathCause);
            GUI.color = new Color(0.8f, 0.8f, 0.8f, 1f);
            GUI.Label(new Rect(centerX - 300, centerY - 50, 600, 50), causeText, subtitleStyle);
            GUI.color = Color.white;

            // ========== RESPAWN COUNTDOWN ==========
            if (respawnTimer > 0)
            {
                string countdownText = $"Respawning in {Mathf.CeilToInt(respawnTimer)}...";
                GUI.color = new Color(1f, 1f, 1f, 0.9f);
                GUI.Label(new Rect(centerX - 200, centerY + 50, 400, 50), countdownText, countdownStyle);
                GUI.color = Color.white;
            }
            else
            {
                GUI.color = new Color(0.5f, 1f, 0.5f, 1f);
                GUI.Label(new Rect(centerX - 200, centerY + 50, 400, 50), "Respawning...", countdownStyle);
                GUI.color = Color.white;
            }

            // ========== DEATH TIP ==========
            GUI.color = new Color(0.7f, 0.7f, 0.7f, 0.8f);
            GUI.Label(new Rect(centerX - 300, centerY + 150, 600, 50), currentTip, tipStyle);
            GUI.color = Color.white;

            // ========== DEATH PENALTY INFO ==========
            GUI.color = new Color(0.6f, 0.6f, 0.6f, 0.7f);
            GUI.Label(new Rect(centerX - 200, centerY + 200, 400, 30),
                "You will respawn with 50% health and 30% hunger.", tipStyle);
            GUI.color = Color.white;
        }

        public void ShowDeathScreen(string cause)
        {
            isVisible = true;
            deathCause = cause;
            respawnTimer = SurvivalElementsPlugin.RespawnDelay.Value;
            glitchTimer = 0f;

            // Pick random tip
            currentTip = DeathTips[UnityEngine.Random.Range(0, DeathTips.Length)];

            SurvivalElementsPlugin.Log.LogInfo($"Death screen shown - Cause: {cause}");
        }

        public void HideDeathScreen()
        {
            isVisible = false;
        }

        private string FormatDeathCause(string cause)
        {
            return cause.ToLower() switch
            {
                "starvation" => "You starved to death.",
                "alien fighter" => "Killed by an Alien Fighter.",
                "alien" => "Killed by hostile aliens.",
                "drone" => "Destroyed by an enemy drone.",
                "turret" => "Shot down by a turret.",
                "radiation" => "Died from radiation exposure.",
                "toxic" => "Poisoned by toxic fumes.",
                "fall" => "Fell to your death.",
                "explosion" => "Killed in an explosion.",
                "power surge" => "Electrocuted by a power surge.",
                "environmental" => "Killed by environmental hazard.",
                _ => $"Killed by: {cause}"
            };
        }

        private void SetupStyles()
        {
            // Overlay texture
            overlayTex = new Texture2D(1, 1);
            overlayTex.SetPixel(0, 0, Color.black);
            overlayTex.Apply();

            // Title - "YOU DIED"
            titleStyle.fontSize = 72;
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.alignment = TextAnchor.MiddleCenter;
            titleStyle.normal.textColor = Color.white;

            // Subtitle - death cause
            subtitleStyle.fontSize = 28;
            subtitleStyle.fontStyle = FontStyle.Normal;
            subtitleStyle.alignment = TextAnchor.MiddleCenter;
            subtitleStyle.normal.textColor = Color.white;

            // Countdown
            countdownStyle.fontSize = 24;
            countdownStyle.fontStyle = FontStyle.Bold;
            countdownStyle.alignment = TextAnchor.MiddleCenter;
            countdownStyle.normal.textColor = Color.white;

            // Tips
            tipStyle.fontSize = 18;
            tipStyle.fontStyle = FontStyle.Italic;
            tipStyle.alignment = TextAnchor.MiddleCenter;
            tipStyle.normal.textColor = Color.white;
        }
    }

    /// <summary>
    /// UI for displaying machine health when looking at machines
    /// </summary>
    public class MachineHealthDisplayUI : MonoBehaviour
    {
        private GUIStyle healthBarStyle;
        private GUIStyle backgroundStyle;
        private GUIStyle labelStyle;
        private GUIStyle repairStyle;
        private Texture2D healthTex;
        private Texture2D backgroundTex;

        private uint currentTargetMachine = 0;
        private string currentTargetName = "";
        private float currentHealthPercent = 1f;
        private bool showRepairPrompt = false;
        private bool isRepairing = false;

        void Awake()
        {
            healthBarStyle = new GUIStyle();
            backgroundStyle = new GUIStyle();
            labelStyle = new GUIStyle();
            repairStyle = new GUIStyle();
        }

        void Update()
        {
            UpdateTargetMachine();
            HandleRepairInput();
        }

        private void UpdateTargetMachine()
        {
            currentTargetMachine = 0;
            currentTargetName = "";
            showRepairPrompt = false;

            var cam = Camera.main;
            if (cam == null) return;

            Ray ray = cam.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f, 0));
            if (Physics.Raycast(ray, out RaycastHit hit, 10f))
            {
                // Check for machine tracker component
                var tracker = hit.collider.GetComponentInParent<MachineHealthTracker>();
                if (tracker != null)
                {
                    currentTargetMachine = tracker.MachineId;
                    currentTargetName = tracker.gameObject.name;

                    var healthInfo = SurvivalElementsPlugin.GetMachineHealthInfo(tracker.MachineId);
                    if (healthInfo.found)
                    {
                        currentHealthPercent = healthInfo.percent;
                        showRepairPrompt = currentHealthPercent < 1f;
                    }
                }
            }
        }

        private void HandleRepairInput()
        {
            if (currentTargetMachine == 0 || !showRepairPrompt) return;

            // Check if Fire1 is held (repair action)
            if (Input.GetButton("Fire1"))
            {
                if (!isRepairing)
                {
                    isRepairing = true;
                    FrameworkAPI.StartMachineRepair(currentTargetMachine);
                    SurvivalElementsPlugin.LogDebug($"Started repairing machine {currentTargetMachine}");
                }
            }
            else
            {
                if (isRepairing)
                {
                    isRepairing = false;
                    FrameworkAPI.StopMachineRepair(currentTargetMachine);
                    SurvivalElementsPlugin.LogDebug($"Stopped repairing machine {currentTargetMachine}");
                }
            }
        }

        void OnGUI()
        {
            if (currentTargetMachine == 0) return;

            // Setup styles
            if (healthTex == null)
            {
                SetupStyles();
            }

            // Position in center of screen, above crosshair
            float barWidth = 200f;
            float barHeight = 16f;
            float centerX = Screen.width / 2f;
            float centerY = Screen.height / 2f - 60f;

            // Machine name
            GUI.color = Color.white;
            GUI.Label(new Rect(centerX - 100, centerY - 25, 200, 20), currentTargetName, labelStyle);

            // Background
            GUI.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);
            GUI.Box(new Rect(centerX - barWidth / 2 - 2, centerY - 2, barWidth + 4, barHeight + 4), "", backgroundStyle);

            // Health fill
            Color healthColor = GetHealthColor(currentHealthPercent);
            GUI.color = healthColor;
            GUI.Box(new Rect(centerX - barWidth / 2, centerY, barWidth * currentHealthPercent, barHeight), "", healthBarStyle);
            GUI.color = Color.white;

            // Health text
            string healthText = $"{currentHealthPercent * 100:F0}%";
            GUI.Label(new Rect(centerX - 20, centerY - 1, 40, barHeight), healthText, labelStyle);

            // Repair prompt
            if (showRepairPrompt)
            {
                string repairText = isRepairing ? "Repairing..." : "[LMB] Hold to Repair";
                Color promptColor = isRepairing ? new Color(0.5f, 1f, 0.5f) : new Color(1f, 0.8f, 0.2f);
                GUI.color = promptColor;
                GUI.Label(new Rect(centerX - 80, centerY + barHeight + 10, 160, 20), repairText, repairStyle);
                GUI.color = Color.white;
            }
        }

        private Color GetHealthColor(float percent)
        {
            if (percent > 0.6f) return new Color(0.3f, 0.9f, 0.3f); // Green
            if (percent > 0.3f) return new Color(1f, 0.9f, 0.2f);   // Yellow
            return new Color(1f, 0.3f, 0.3f);                        // Red
        }

        private void SetupStyles()
        {
            healthTex = MakeTexture(2, 2, new Color(0.3f, 0.8f, 0.3f, 1f));
            healthBarStyle.normal.background = healthTex;

            backgroundTex = MakeTexture(2, 2, new Color(0.1f, 0.1f, 0.1f, 0.85f));
            backgroundStyle.normal.background = backgroundTex;

            labelStyle.normal.textColor = Color.white;
            labelStyle.fontSize = 14;
            labelStyle.fontStyle = FontStyle.Bold;
            labelStyle.alignment = TextAnchor.MiddleCenter;

            repairStyle.normal.textColor = Color.white;
            repairStyle.fontSize = 12;
            repairStyle.fontStyle = FontStyle.Bold;
            repairStyle.alignment = TextAnchor.MiddleCenter;
        }

        private Texture2D MakeTexture(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; i++)
                pix[i] = col;
            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }
    }
}


