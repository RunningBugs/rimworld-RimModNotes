# What is Permanent Darkness?

Permanent Darkness is a singleton game condition that has several stages:
1. After getting triggered, there's a global single state variable representing stage 1, stage 2, stage 3 of the condition.
    1. stage 1: Weather transition to WeatherDefs.PermanentDarkness_Stage1, sending a letter to the player about initialPhaseLetter
    2. stage 2: Weather not changing, sending a letter to the player warning about stage 2 is coming using a letter about DarknessWarningLetter
    3. stage 3: Weather transition to WeatherDefs.PermanentDarkness_Stage2, sending a letter to the player about mainPhaseLetter
2. In stage 3, the game will be in a permanent darkness state.

## Current GameCondition_PermanentDarkness Functionality Summary

The current implementation provides the following features:

### Core Weather Control
- **Inherits from GameCondition_ForceWeather**: Forces specific weather on affected maps
- **Weather Stages**: 
  - Stage 1: `WeatherDefs.PermanentDarkness_Stage1` (initial phase)
  - Stage 2: `WeatherDefs.PermanentDarkness_Stage2` (main phase after delay)
- **Weather Switching Logic**: Uses `MainPhaseStartTick` to determine when to switch from stage 1 to stage 2

### Visual Effects
- **Sky Effects**: Sets sky target to complete darkness (0f brightness) with eclipse colors
- **Sky Overlays**: Uses `WeatherOverlay_UnnaturalDarkness` for visual darkness effects
- **Darkness Control**: Optional user settings for darkness level and shadow rendering via `ModSettingsUI.settings`

### Letter System
- **initialLetter**: Sent when initial phase starts
- **mainWarning**: Warning letter sent before main phase (10000 ticks before)
- **mainLetter**: Sent when main phase begins
- **Letter Content**: Uses `RulePackDefs.PermanentDarkness` for dynamic text generation

### Hediff Application
- **Darkness Exposure**: Applies `HediffDefs.PD_DarknessExposure` to affected pawns
- **Affected Pawns**: Humanlike colonists and colony mutants that are spawned and not downed
- **Detection Logic**: Uses `InUnnaturalDarkness()` and `UnnaturalDarknessAt()` to check if pawns are in darkness
- **Light Threshold**: Considers areas with 0f ground glow as "unnatural darkness"

### Map Coverage Issues (Current Problems)
- **Multi-Map Registration**: Attempts to register condition on all maps but causes conflicts
- **State Synchronization**: Tries to sync letter flags across multiple instances
- **Manager Conflicts**: GameCondition instances can't be properly shared across multiple GameConditionManagers

### Timing and Control
- **PermanentDarknessController**: Manages global start tick and delay timing
- **Phase Timing**: 
  - InitPhaseStartTick: When condition begins
  - MainPhaseStartTick: InitPhaseStartTick + random delay (0.5-0.75 days)
- **Permanent Flag**: Set to true to prevent automatic expiration

# Implementation plan

1. We need to have a global controller
    - The controller will be activated (ticking) when gamecondition_permanentdarkness is active
    - otherwise it will be deactivated (not ticking or ticking with almost nop)
    - The controller will apply force weather change for every single map that making sure all maps are in the same weather state
    - The we know permenant darkness is forced ended in any of the map, it will propagate the ending to all maps through this controller, and after all things are ended, it will reset the global controller and deactivate it