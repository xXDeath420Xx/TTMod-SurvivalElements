# Survival Elements

**A comprehensive survival mechanics mod for Techtonica**

Survival Elements transforms the Techtonica experience by adding meaningful survival mechanics including machine health systems, player hunger and health management, a death and respawn system, and a complete food crafting system. This mod adds depth and challenge to your factory-building adventure.

---

## Table of Contents

- [Features](#features)
  - [Machine Health System](#machine-health-system)
  - [Player Survival](#player-survival)
  - [Food and Hunger System](#food-and-hunger-system)
  - [Death and Respawn](#death-and-respawn)
  - [User Interface](#user-interface)
- [How to Use](#how-to-use)
  - [Repair Tool Usage](#repair-tool-usage)
  - [Food Consumption](#food-consumption)
  - [Surviving Power Surges](#surviving-power-surges)
- [Installation](#installation)
- [Configuration](#configuration)
- [Requirements](#requirements)
- [Compatibility](#compatibility)
- [Known Issues](#known-issues)
- [Changelog](#changelog)
- [Credits](#credits)
- [License](#license)
- [Links](#links)

---

## Features

### Machine Health System

- **Machine Health Points**: All machines now have health that can be damaged and must be maintained
- **Default HP**: Machines start with 100 HP (configurable)
- **Visual Damage Indicators**: Machines change color based on health status
  - Healthy (>75%): Normal appearance
  - Damaged (25-75%): Yellow tint
  - Critical (<25%): Red tint with warning indication
- **Machine Health Display**: Look at any machine to see its current health status

### Power Grid Hazards

- **Power Surges**: Random electrical surges can damage machines connected to your power grid
  - Configurable chance per minute (default: 5%)
  - Configurable damage amount (default: 25 HP)
  - Affects approximately 30% of machines on the grid per surge
- **Overload Damage**: When your power grid is overloaded, machines take continuous damage
  - Configurable damage rate (default: 5 HP/second)

### Repair Tool

- **Craftable Equipment**: Research and craft the Repair Tool to maintain your machines
- **Tech Tree Unlock**: Available in the VICTOR tier (Tier 6) under the Modded category
- **Crafting Recipe**:
  - 5x Iron Frame
  - 10x Copper Wire
  - 5x Iron Components
- **Usage**: Aim at damaged machines and hold left mouse button to repair
- **Repair Rate**: Restores 10 HP per second while actively repairing

### Player Survival

#### Health System

- **Player Health**: 100 HP maximum health pool
- **Health Regeneration**: Automatically regenerate health when well-fed (hunger >50%)
  - Configurable regeneration rate (default: 0.5 HP/second)
- **Damage Sources**: Take damage from starvation, environmental hazards, and hostile entities
- **Visual Feedback**: Screen flashes red when taking damage with vignette effect

#### Hunger System

- **Hunger Meter**: 100-point hunger system that depletes over time
- **Decay Rate**: Configurable hunger loss per minute (default: 2 points/minute)
- **Starvation**: When hunger reaches 0, you take continuous damage
  - Configurable starvation damage (default: 1 HP/second)
- **Warning System**: Visual and text warnings at 25% and 0% hunger

### Food and Hunger System

Research the "Sustenance Tech" unlock (available in LIMA tier, Tier 2) to access food crafting.

#### Available Food Items

| Food Item | Hunger Restored | Crafting Station | Ingredients |
|-----------|-----------------|------------------|-------------|
| **Cooked Meat** | 40 | Smelter | 5x Plantmatter |
| **Plant Stew** | 30 | Smelter | 10x Plantmatter, 2x Kindlevine |
| **Energy Bar** | 25 | Assembler | 5x Plantmatter Fiber, 2x Kindlevine Extract |
| **Nutrient Paste** | 50 | Assembler | 5x Biobrick, 20x Plantmatter, 1x Shiverthorn Extract |
| **Raw Meat** | 10 | N/A (drop) | Obtained from wildlife |

### Death and Respawn

- **Death Screen**: Dramatic "YOU DIED" display with death cause and tips
- **Death Causes**: Contextual messages for different death types (starvation, combat, environmental, etc.)
- **Respawn System**:
  - Configurable respawn delay (default: 5 seconds)
  - Option to respawn at the main elevator
  - Death penalty: Respawn at 50% health and 30% hunger
- **Helpful Tips**: Random survival tips displayed on death screen

### User Interface

#### Health and Hunger Bars

- **Position**: Bottom-right corner of the screen
- **Health Bar**: Red gradient bar with heart icon, shows exact HP values
- **Hunger Bar**: Orange gradient bar showing hunger percentage
- **Dynamic Colors**: Bars change color based on status (green > yellow > red)
- **Warning Messages**: Flashing text warnings for critical hunger and health
- **Damage Vignette**: Red screen flash effect when taking damage

#### Machine Health Display

- **Crosshair Targeting**: Look at machines to see their health
- **Health Bar**: Centered above crosshair with percentage display
- **Repair Prompt**: Shows "[LMB] Hold to Repair" when looking at damaged machines
- **Real-time Updates**: Health bar updates while repairing

---

## How to Use

### Repair Tool Usage

1. Research "Repair Tool Tech" in the tech tree (VICTOR tier)
2. Craft the Repair Tool at an Assembler
3. Equip the Repair Tool from your inventory
4. Look at a damaged machine (health bar will appear)
5. Hold left mouse button to repair
6. Release when the machine is fully repaired

### Food Consumption

1. Research "Sustenance Tech" in the tech tree (LIMA tier)
2. Craft food items at a Smelter or Assembler
3. Keep food in your inventory
4. Use food items when hungry to restore hunger points
5. Maintain hunger above 50% for health regeneration

### Surviving Power Surges

1. Monitor your machines for damage indicators (yellow/red tinting)
2. Keep a Repair Tool handy for emergency repairs
3. Consider building redundant machines for critical operations
4. Repair damaged machines before they reach critical health

---

## Installation

### Using r2modman (Recommended)

1. Install [r2modman](https://thunderstore.io/package/ebkr/r2modman/)
2. Select Techtonica as your game
3. Search for "Survival Elements" in the mod browser
4. Click "Download" to install with all dependencies

### Manual Installation

1. Install [BepInEx 5.4.21](https://github.com/BepInEx/BepInEx/releases) or newer
2. Install all required dependencies (see [Requirements](#requirements))
3. Download the latest `SurvivalElements.dll`
4. Place the DLL in your `BepInEx/plugins` folder
5. Launch the game

---

## Configuration

Configuration file is automatically generated at:
`BepInEx/config/com.certifired.SurvivalElements.cfg`

### Machine Health Settings

| Setting | Default | Range | Description |
|---------|---------|-------|-------------|
| Enable Machine Health | true | true/false | Enable health system for machines |
| Default Machine HP | 100 | 10-1000 | Default health points for machines |

### Power Surge Settings

| Setting | Default | Range | Description |
|---------|---------|-------|-------------|
| Enable Power Surges | true | true/false | Random power surges can damage machines |
| Surge Chance Per Minute | 0.05 | 0-1 | Chance of power surge per minute (5% default) |
| Surge Damage | 25 | 1-100 | Damage dealt by power surges |

### Overload Settings

| Setting | Default | Range | Description |
|---------|---------|-------|-------------|
| Enable Overload Damage | true | true/false | Machines take damage when power grid is overloaded |
| Overload Damage Rate | 5 | 0.1-50 | Damage per second during overload |

### Hunger Settings

| Setting | Default | Range | Description |
|---------|---------|-------|-------------|
| Enable Hunger System | true | true/false | Enable hunger that depletes over time |
| Hunger Decay Per Minute | 2 | 0.1-10 | Hunger points lost per minute |
| Starvation Damage Per Second | 1 | 0.1-10 | Damage per second when starving |

### Player Health Settings

| Setting | Default | Range | Description |
|---------|---------|-------|-------------|
| Enable Player Health | true | true/false | Enable player health system |
| Health Regen Per Second | 0.5 | 0-5 | Health regeneration when well-fed |

### UI Settings

| Setting | Default | Description |
|---------|---------|-------------|
| Show Health/Hunger UI | true | Display health and hunger bars on screen |

### Death Settings

| Setting | Default | Range | Description |
|---------|---------|-------|-------------|
| Respawn Delay | 5 | 1-30 | Seconds to wait before respawning |
| Respawn At Elevator | true | true/false | Respawn at the main elevator |

### Debug Settings

| Setting | Default | Description |
|---------|---------|-------------|
| Debug Mode | false | Enable verbose debug logging |

---

## Requirements

### Required Dependencies

| Mod | Minimum Version | Purpose |
|-----|-----------------|---------|
| [BepInEx](https://github.com/BepInEx/BepInEx) | 5.4.21+ | Mod framework |
| [EquinoxsModUtils](https://thunderstore.io/c/techtonica/p/Equinox/EquinoxsModUtils/) | 6.1.3+ | Modding utilities |
| [EMUAdditions](https://thunderstore.io/c/techtonica/p/Equinox/EMUAdditions/) | 2.0.0+ | Custom items and recipes |
| [TechtonicaFramework](https://thunderstore.io/c/techtonica/p/Certifired/TechtonicaFramework/) | 1.0.0+ | Framework for survival mechanics |

### Game Version

- Techtonica (Steam version recommended)
- .NET Framework 4.7.2

---

## Compatibility

### Compatible Mods

- Most content mods that add machines will automatically have health applied
- UI mods that don't modify the bottom-right corner
- Other survival/challenge mods

### Potential Conflicts

- Mods that heavily modify the Player class
- Mods that modify machine building/removal hooks
- UI mods that use the same screen positions

---

## Known Issues

- Machine visual damage tinting may not work on all machine types
- Elevator respawn point detection may fail in some save files
- Raw Meat item registered but wildlife drops not yet implemented

---

## Changelog

### [2.6.1] - Current

- Enhanced player health and hunger systems
- Added death screen with respawn mechanics
- Added machine health display UI when looking at machines
- Improved visual feedback for damage
- Added food items: Cooked Meat, Plant Stew, Energy Bar, Nutrient Paste
- Configurable respawn at elevator option
- Death penalty system (respawn at 50% health, 30% hunger)

### [2.0.0]

- Added hunger system with decay and starvation
- Added player health with regeneration
- Added health/hunger UI bars
- Added power surge and overload damage systems

### [1.0.0] - 2025-01-05

- Initial release
- Repair Tool equipment
- Machine health integration
- Tech tree unlock for Repair Tool

---

## Credits

### Development

- **Certifired** - Primary developer
- **Claude Code** (Anthropic) - AI-assisted development, code architecture, and documentation

### Special Thanks

- **Equinox** - For EquinoxsModUtils and EMUAdditions which make Techtonica modding possible
- **Fire Hose Games** - For creating Techtonica
- **Techtonica Modding Community** - For support and feedback

---

## License

This mod is licensed under the **GNU General Public License v3.0 (GPL-3.0)**.

You are free to:
- Use this mod for personal and commercial purposes
- Modify and distribute this mod
- Include this mod in modpacks

Under the following conditions:
- You must include the original license and copyright notice
- Any modifications must also be licensed under GPL-3.0
- Source code must be made available when distributing

For the full license text, see: [GNU GPL v3.0](https://www.gnu.org/licenses/gpl-3.0.en.html)

---

## Links

### Mod Resources

- [Thunderstore Page](https://thunderstore.io/c/techtonica/p/Certifired/SurvivalElements/)
- [Source Code](https://github.com/certifired/SurvivalElements)
- [Bug Reports & Feature Requests](https://github.com/certifired/SurvivalElements/issues)

### Dependencies

- [BepInEx](https://github.com/BepInEx/BepInEx)
- [EquinoxsModUtils](https://thunderstore.io/c/techtonica/p/Equinox/EquinoxsModUtils/)
- [EMUAdditions](https://thunderstore.io/c/techtonica/p/Equinox/EMUAdditions/)
- [TechtonicaFramework](https://thunderstore.io/c/techtonica/p/Certifired/TechtonicaFramework/)

### Community

- [Techtonica Discord](https://discord.gg/techtonica)
- [Techtonica Modding Discord](https://discord.gg/techtonica-modding)

---

*Made with passion for the Techtonica community*
