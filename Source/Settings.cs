﻿using UnityEngine;
using Verse;

using static Celsius.LogUtility;

namespace Celsius
{
    public class Settings : ModSettings
    {
        public static bool UseVanillaTemperatureColors;
        public static bool ShowTemperatureTooltip;
        public static bool FreezingAndMeltingEnabled;
        public static bool AutoignitionEnabled;
        public static bool PawnWeatherEffects;
        public static int TicksPerUpdate;
        public static float ConductivityPowerBase;
        public static float ConvectionConductivityEffect;
        public static float EnvironmentDiffusionFactor;
        public static float RoofInsulation;
        public static float RoofDiffusionFactor;
        public static float HeatPushMultiplier;
        public static float HeatPushEffect;
        public static int TemperatureDisplayDigits;
        public static string TemperatureDisplayFormatString;
        public static bool DebugMode = Prefs.LogVerbose;

        public const int TicksPerUpdate_Default = 120;
        public const float ConductivityPowerBase_Default = 0.5f;
        public const float ConvectionConductivityEffect_Default = 10;
        public const float EnvironmentDiffusionFactor_Default = 0.10f;
        public const float RoofInsulation_Default = 3;
        public const float HeatPushEffect_Base = 0.15f;
        public const int TemperatureDisplayDigits_Default = 0;

        public Settings() => Reset();

        public override void ExposeData()
        {
            Scribe_Values.Look(ref UseVanillaTemperatureColors, "UseVanillaTemperatureColors");
            Scribe_Values.Look(ref ShowTemperatureTooltip, "ShowTemperatureTooltip", true);
            Scribe_Values.Look(ref FreezingAndMeltingEnabled, "FreezingAndMeltingEnabled", false);
            Scribe_Values.Look(ref AutoignitionEnabled, "AutoignitionEnabled", true);
            Scribe_Values.Look(ref PawnWeatherEffects, "PawnWeatherEffects", true);
            Scribe_Values.Look(ref TicksPerUpdate, "TicksPerUpdate", TicksPerUpdate_Default);
            Scribe_Values.Look(ref ConvectionConductivityEffect, "ConvectionConductivityEffect", ConvectionConductivityEffect_Default);
            Scribe_Values.Look(ref EnvironmentDiffusionFactor, "EnvironmentDiffusionFactor", EnvironmentDiffusionFactor_Default);
            Scribe_Values.Look(ref RoofInsulation, "RoofInsulation", RoofInsulation_Default);
            Scribe_Values.Look(ref HeatPushMultiplier, "HeatPushMultiplier", 1);
            Scribe_Values.Look(ref TemperatureDisplayDigits, "TemperatureDisplayDigits", TemperatureDisplayDigits_Default);
            Scribe_Values.Look(ref DebugMode, "DebugMode", forceSave: true);
            if (Scribe.mode == LoadSaveMode.LoadingVars)
                TemperatureUtility.SettingsChanged();
        }

        public static void Reset()
        {
            Log("Settings reset.");
            UseVanillaTemperatureColors = false;
            ShowTemperatureTooltip = true;
            FreezingAndMeltingEnabled = true;
            AutoignitionEnabled = true;
            PawnWeatherEffects = true;
            TicksPerUpdate = TicksPerUpdate_Default;
            ConvectionConductivityEffect = ConvectionConductivityEffect_Default;
            EnvironmentDiffusionFactor = EnvironmentDiffusionFactor_Default;
            RoofInsulation = RoofInsulation_Default;
            HeatPushMultiplier = 1;
            TemperatureDisplayDigits = TemperatureDisplayDigits_Default;
            TemperatureUtility.SettingsChanged();
            Print();
        }

        internal static void RecalculateValues()
        {
            TemperatureDisplayFormatString = $"F{TemperatureDisplayDigits}";
            ConductivityPowerBase = Mathf.Pow(ConductivityPowerBase_Default, (float)TicksPerUpdate_Default / TicksPerUpdate);
            RoofDiffusionFactor = EnvironmentDiffusionFactor * Mathf.Pow(ConductivityPowerBase, RoofInsulation);
            HeatPushEffect = HeatPushEffect_Base * HeatPushMultiplier;
        }

        public static void Print()
        {
            if (!DebugMode)
                return;
            Log($"UseVanillaTemperatureColors: {UseVanillaTemperatureColors}");
            Log($"ShowTemperatureTooltip: {ShowTemperatureTooltip}");
            Log($"FreezingAndMeltingEnabled: {FreezingAndMeltingEnabled}");
            Log($"AutoignitionEnabled: {AutoignitionEnabled}");
            Log($"PawnWeatherEffects: {PawnWeatherEffects}");
            Log($"TicksPerUpdate: {TicksPerUpdate}");
            Log($"ConductivityPowerBase: {ConductivityPowerBase}");
            Log($"ConvectionConductivityEffect: {ConvectionConductivityEffect}");
            Log($"EnvironmentDiffusionFactor: {EnvironmentDiffusionFactor}");
            Log($"RoofInsulation: {RoofInsulation}");
            Log($"RoofDiffusionFactor: {RoofDiffusionFactor}");
            Log($"HeatPushMultiplier: {HeatPushMultiplier.ToStringPercent()}");
            Log($"HeatPushEffect: {HeatPushEffect}");
            Log($"TemperatureDisplayDigits: {TemperatureDisplayDigits}");
        }
    }
}
