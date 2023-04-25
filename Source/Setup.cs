using HarmonyLib;
using RimWorld;
using RimWorld.BaseGen;
using SK;
using SK.Enlighten;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace Celsius
{
    /// <summary>
    /// Harmony patches and Defs preparation on game startup
    /// </summary>
    [StaticConstructorOnStartup]
    internal static class Setup
    {
        static Harmony harmony;

        static Setup()
        {
            // Setting up Harmony
            if (harmony != null)
                return;

            LogUtility.Log($"Initializing Celsius {System.Reflection.Assembly.GetExecutingAssembly().GetName().Version}...", LogLevel.Important);

            harmony = new Harmony("Garwel.Celsius");
            Type type = typeof(Setup);

            harmony.Patch(
                AccessTools.Method("Verse.GenTemperature:TryGetDirectAirTemperatureForCell"),
                prefix: new HarmonyMethod(type.GetMethod($"GenTemperature_TryGetDirectAirTemperatureForCell")));
            harmony.Patch(
                AccessTools.PropertyGetter(typeof(Room), "Temperature"),
                prefix: new HarmonyMethod(type.GetMethod("Room_Temperature_get")));
            #region HSK Added
            harmony.Patch(
                AccessTools.Method($"RimWorld.StatPart_WorkTableTemperature:TransformValue"),
                prefix: new HarmonyMethod(type.GetMethod("TransformValue_Patch")));
            harmony.Patch(
                AccessTools.Method($"RimWorld.StatPart_WorkTableTemperature:Applies", new Type[] { typeof(ThingDef), typeof(Map), typeof(IntVec3) }),
                prefix: new HarmonyMethod(type.GetMethod($"Applies_Patch")));
            harmony.Patch(
                AccessTools.Method($"RimWorld.CompReportWorkSpeed:CompInspectStringExtra"),
                postfix: new HarmonyMethod(type.GetMethod($"CompInspectStringExtra_Patch")));
            #endregion
            harmony.Patch(
                AccessTools.Method("Verse.GenTemperature:PushHeat", new Type[] { typeof(IntVec3), typeof(Map), typeof(float) }),
                prefix: new HarmonyMethod(type.GetMethod("GenTemperature_PushHeat")));
            harmony.Patch(
                AccessTools.Method("Verse.GenTemperature:ControlTemperatureTempChange"),
                postfix: new HarmonyMethod(type.GetMethod("GenTemperature_ControlTemperatureTempChange")));
            harmony.Patch(
                AccessTools.Method("RimWorld.SteadyEnvironmentEffects:MeltAmountAt"),
                postfix: new HarmonyMethod(type.GetMethod("SteadyEnvironmentEffects_MeltAmountAt")));
            harmony.Patch(
                AccessTools.Method("Verse.AttachableThing:Destroy"),
                prefix: new HarmonyMethod(type.GetMethod("AttachableThing_Destroy")));
            harmony.Patch(
                AccessTools.Method("RimWorld.JobGiver_SeekSafeTemperature:ClosestRegionWithinTemperatureRange"),
                prefix: new HarmonyMethod(type.GetMethod("JobGiver_SeekSafeTemperature_ClosestRegionWithinTemperatureRange")));
            harmony.Patch(
                AccessTools.Method("Verse.DangerUtility:GetDangerFor"),
                postfix: new HarmonyMethod(type.GetMethod("DangerUtility_GetDangerFor")));
            harmony.Patch(
                AccessTools.Method("Verse.MapTemperature:TemperatureUpdate"),
                prefix: new HarmonyMethod(type.GetMethod("MapTemperature_TemperatureUpdate")));
            harmony.Patch(
                AccessTools.Method("RimWorld.GlobalControls:TemperatureString"),
                prefix: new HarmonyMethod(type.GetMethod("GlobalControls_TemperatureString")));
            harmony.Patch(
                AccessTools.Method("RimWorld.Building_Door:DoorOpen"),
                postfix: new HarmonyMethod(type.GetMethod("Building_Door_DoorOpen")));
            harmony.Patch(
                AccessTools.Method("RimWorld.Building_Door:DoorTryClose"),
                postfix: new HarmonyMethod(type.GetMethod("Building_Door_DoorTryClose")));
            harmony.Patch(
                AccessTools.Method("RimWorld.CompRitualFireOverlay:CompTick"),
                postfix: new HarmonyMethod(type.GetMethod("CompRitualFireOverlay_CompTick")));

            LogUtility.Log($"Harmony initialization complete.");

            // Adding CompThermal and ThingThermalProperties to all applicable Things
            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefs.Where(def => CompThermal.ShouldApplyTo(def)))
            {
                if (def.IsMeat)
                {
                    if (def.modExtensions == null)
                        def.modExtensions = new List<DefModExtension>();
                }
                def.comps.Add(new CompProperties(typeof(CompThermal)));
            }

            TemperatureUtility.SettingsChanged();
        }

        // Replaces GenTemperature.TryGetDirectAirTemperatureForCell by providing cell-specific temperature
        public static bool GenTemperature_TryGetDirectAirTemperatureForCell(ref bool __result, IntVec3 c, Map map, out float temperature)
        {
            temperature = map.mapTemperature.OutdoorTemp;
            Thing thing = c.GetThingList(map).Find(b => b.TryGetComp<CompReportWorkSpeed>() != null && b.def.altitudeLayer == AltitudeLayer.Building);
            //foreach (var item in c.GetThingList(map))
            //{
            //    Log.Message("Found: " + item.def.defName + ". Has Comp: " + (item.TryGetComp<CompReportWorkSpeed>() != null).ToString() + ". cateogry: " + item.def.category + ". Altitude Layer: " + item.def.altitudeLayer + ". Passibility: " + item.def.passability);
            //}
            if (thing != null)
            {
                float temp = 0;
                int count = 0;
                switch (thing.def.building.buildingTags.Find(t => t.Contains("CelsiusSK_")))
                {
                    case "CelsiusSK_PrioOutdoorOrRoom":
                        if (c.IsInRoom(map))
                        {
                            //TemperatureInfo tmp = new TemperatureInfo(map);
                            temperature = c.GetRoom(map).Temperature;
                        }
                        else
                        {
                            temperature = map.mapTemperature.OutdoorTemp;
                        }

                        __result = true;
                        return false;
                    case "CelsiusSK_EveryBuildingCell":
                        foreach (var cell in GenAdj.CellsOccupiedBy(thing))
                        {
                            temp += cell.GetTemperatureForCell(map);
                            count++;
                        }
                        break;
                    case "CelsiusSK_EveryBuildingCellPlusOne":
                        IntVec2 add = new IntVec2(1, 1);

                        if (thing.def.Size.Area % 2 == 0)
                            add += new IntVec2(2, 2);
                        foreach (var cell in GenAdj.CellsOccupiedBy(thing.Position, thing.Rotation, thing.def.Size + add))
                        {
                            temp += cell.GetTemperatureForCell(map);
                            count++;
                        }
                        break;
                    default:
                        if (thing.def.size.x * thing.def.size.z > 2)
                        {
                            foreach (var cell in GenAdj.CellsOccupiedBy(thing))
                            {
                                temp += cell.GetTemperatureForCell(map);
                                count++;
                            }
                        }
                        break;
                }

                if (count > 0)
                {
                    temperature = temp / count;
                    __result = true;
                    //if (DebugSettings.godMode)
                    //    Log.Message("TryGetDirectAirTemperature is: " + temperature);
                    return false;
                }

                if (!thing.def.IsBlueprint && thing.def.hasInteractionCell)
                {
                    temperature = ThingUtility.InteractionCell(thing.def.interactionCellOffset, c, c.GetFirstThing(map, thing.def).Rotation).GetTemperatureForCell(map);
                    //if (DebugSettings.godMode)
                    //    Log.Message("hasInteractionCell is: " + temperature + ". Loc: " + c);
                    __result = true;
                    return false;
                }
                else if(c.IsInRoom(map))
                {
                    //TemperatureInfo tmp = new TemperatureInfo(map);
                    //if (DebugSettings.godMode)
                    //    Log.Message("Is In Room");
                    temperature = c.GetRoom(map).Temperature;
                    __result = true;
                    return false;
                }
                else if (c.Roofed(map))
                {
                    //if (DebugSettings.godMode)
                    //    Log.Message("Is Roofed");
                    temperature = c.GetTemperatureForCell(map);
                    __result = true;
                    return false;
                }
                else
                {
                    //if (DebugSettings.godMode)
                    //    Log.Message("Is Outdoors");
                    temperature = map.mapTemperature.OutdoorTemp;
                    __result = true;
                    return false;
                }
            }

            if (c.IsInRoom(map))
            {
                //if (DebugSettings.godMode)
                //    Log.Message("Is In Room 2");
                //TemperatureInfo tmp = new TemperatureInfo(map);
                temperature = c.GetRoom(map).Temperature;
                __result = true;
                return false;
            }
            else
            {
                //if (DebugSettings.godMode)
                //    Log.Message("Get Cell Temp");
                temperature = c.GetTemperatureForCell(map);
            }


            __result = true;
            //if (DebugSettings.godMode)
                //Log.Message("TryGetDirectAirTemperature is: " + temperature);
            return false;
        }

        // Replaces Room.Temperature with room's average temperature (e.g. for displaying in the bottom right corner)
        public static bool Room_Temperature_get(ref float __result, Room __instance)
        {
            if (__instance.Map.TemperatureInfo() == null)
                return true;
            __result = __instance.GetTemperature();
            return false;
        }

        // Replaces Applies for workbench temperature check.
        public static bool Applies_Patch(ThingDef tDef, Map map, IntVec3 c)
        {
            if (map == null || tDef.building == null)
                return false;

            float temperatureForCell = 21f;

            temperatureForCell = GenTemperature.GetTemperatureForCell(c, map);
            //if (DebugSettings.godMode)
                //Log.Message("GetTemperatureForCell is: " + temperatureForCell);

            float minTemp = 9.0f;
            float maxTemp = 35.0f;

            if (tDef.statBases != null)
            {
                if (!tDef.statBases.Where(s => s.stat.defName == "MinTemp").EnumerableNullOrEmpty())
                    minTemp = tDef.statBases.Find(s => s.stat.defName == "MinTemp").value;

                if (!tDef.statBases.Where(s => s.stat.defName == "MaxTemp").EnumerableNullOrEmpty())
                    maxTemp = tDef.statBases.Find(s => s.stat.defName == "MaxTemp").value;
            }
            //if (DebugSettings.godMode)
                //Log.Message("Temeprature found is: " + temperatureForCell);
            return (double)temperatureForCell < minTemp || (double)temperatureForCell > maxTemp;


        }

        public static void TransformValue_Patch(StatRequest req, ref float val)
        {
            if (req.HasThing && Applies(req.Thing))
            {
                float WorkRateFactor = 0.7f;

                if (req.Thing.def.statBases != null)
                {
                    if (!req.Thing.def.statBases.Where(s => s.stat.defName == "WorkRateFactor").EnumerableNullOrEmpty())
                        WorkRateFactor = req.Thing.def.statBases.Find(s => s.stat.defName == "WorkRateFactor").value;
                }
                val *= WorkRateFactor;
            }
            return;
        }

        public static bool Applies(Thing t) => t.Spawned && Applies_Patch(t.def, t.Map, t.Position);

        public static void CompInspectStringExtra_Patch(ref string __result, CompReportWorkSpeed __instance)
        {
            //Log.Message("Method Entered");
            if (__result == null)
                return;
            if (__result.Contains("BadTemperature".Translate()) && __instance.parent != null)
            {
                //Log.Message("Altering String");
                __result = __result.ReplaceFirst("BadTemperature".Translate(), "BadTemperature".Translate() + " : " + GenTemperature.GetTemperatureForCell(__instance.parent.Position, __instance.parent.Map).ToStringTemperature(Settings.TemperatureDisplayFormatString));
                //Log.Message("Result = " + __result);
                //Log.Message("Temp is: " + GenTemperature.GetTemperatureForCell(__instance.parent.Position, __instance.parent.Map).RoundToAsInt(1) + ". Translate is: " + "BadTemperature".Translate());
            }
           // Log.Message("Method Exited");

        }

        // Replaces GenTemperature.PushHeat(IntVec3, Map, float) to change temperature at the specific cell instead of the whole room
        public static bool GenTemperature_PushHeat(ref bool __result, IntVec3 c, Map map, float energy) => __result = TemperatureUtility.TryPushHeat(c, map, energy);

        // Attaches to GenTemperature.ControlTemperatureTempChange to implement heat pushing for temperature control things (Heater, Cooler, Vent)
        public static float GenTemperature_ControlTemperatureTempChange(float result, IntVec3 cell, Map map, float energyLimit, float targetTemperature)
        {
            Room room = cell.GetRoom(map);
            float roomTemp = room != null && !room.TouchesMapEdge ? room.GetTemperature() : cell.GetSurroundingTemperature(map);
            if (energyLimit > 0)
                if (roomTemp < targetTemperature - TemperatureUtility.TemperatureChangePrecision)
                {
                    TemperatureUtility.TryPushHeat(cell, map, energyLimit);
                    return energyLimit;
                }
                else return 0;
            else if (roomTemp > targetTemperature + TemperatureUtility.TemperatureChangePrecision)
            {
                TemperatureUtility.TryPushHeat(cell, map, energyLimit);
                return energyLimit;
            }
            else return 0;
        }

        // Disables vanilla snow melting
        public static float SteadyEnvironmentEffects_MeltAmountAt(float result, float temperature) => 0;

        // Attaches to AttachableThing.Destroy to reduce temperature when a Fire is destroyed to the ignition temperature
        public static void AttachableThing_Destroy(AttachableThing __instance)
        {
            if (Settings.AutoignitionEnabled && __instance is Fire)
            {
                TemperatureInfo temperatureInfo = __instance.Map?.TemperatureInfo();
                if (temperatureInfo != null)
                {
                    float temperature = temperatureInfo.GetIgnitionTemperatureForCell(__instance.Position);
                    LogUtility.Log($"Setting temperature at {__instance.Position} to {temperature:F0}C...");
                    temperatureInfo.SetTemperatureForCell(__instance.Position, Mathf.Min(temperatureInfo.GetTemperatureForCell(__instance.Position), temperature));
                }
            }
        }

        // Replaces JobGiver_SeekSafeTemperature.ClosestRegionWithinTemperatureRange to only seek regions with no dangerous cells
        public static bool JobGiver_SeekSafeTemperature_ClosestRegionWithinTemperatureRange(ref Region __result, JobGiver_SeekSafeTemperature __instance, IntVec3 root, Map map, FloatRange tempRange, TraverseParms traverseParms)
        {
            LogUtility.Log($"JobGiver_SeekSafeTemperature_ClosestRegionWithinTemperatureRange for {root.GetFirstPawn(map)} at {root} (t = {root.GetTemperatureForCell(map):F1}C)");
            Region region = root.GetRegion(map, RegionType.Set_Passable);
            if (region == null)
                return false;
            Region foundReg = null;
            RegionProcessor regionProcessor = delegate (Region r)
            {
                if (r.IsDoorway)
                    return false;
                if (r.Cells.All(cell => tempRange.Includes(cell.GetTemperatureForCell(map))))
                {
                    foundReg = r;
                    return true;
                }
                return false;
            };
            RegionTraverser.BreadthFirstTraverse(region, (Region from, Region r) => r.Allows(traverseParms, false), regionProcessor);
            LogUtility.Log($"Safe region found: {foundReg}");
            __result = foundReg;
            return false;
        }

        // Attaches to DangerUtility.GetDangerFor to mark specific (too hot or too cold) cells as dangerous
        public static Danger DangerUtility_GetDangerFor(Danger result, IntVec3 c, Pawn p, Map map)
        {
            float temperature = c.GetTemperatureForCell(map);
            FloatRange range = p.SafeTemperatureRange();
            Danger danger = range.Includes(temperature) ? Danger.None : (range.ExpandedBy(80).Includes(temperature) ? Danger.Some : Danger.Deadly);
            return danger > result ? danger : result;
        }

        // Disable MapTemperature.TemperatureUpdate, because vanilla temperature overlay is not used anymore
        public static bool MapTemperature_TemperatureUpdate() => false;

        // Replaces temperature display in the global controls view (bottom right)
        public static bool GlobalControls_TemperatureString(ref string __result)
        {
            IntVec3 cell = UI.MouseCell();
            TemperatureInfo temperatureInfo = Find.CurrentMap.TemperatureInfo();
            if (temperatureInfo == null || !cell.InBounds(Find.CurrentMap) || cell.Fogged(Find.CurrentMap))
                return true;
            __result = temperatureInfo.GetTemperatureForCell(cell).ToStringTemperature(Settings.TemperatureDisplayFormatString);
            if (temperatureInfo.HasTerrainTemperatures)
            {
                float terrainTemperature = temperatureInfo.GetTerrainTemperature(cell);
                if (!float.IsNaN(terrainTemperature))
                    // Localization Key: Celsius_Terrain - Terrain
                    __result += $" / {"Celsius_Terrain".Translate()} {terrainTemperature.ToStringTemperature(Settings.TemperatureDisplayFormatString)}";
            }
            return false;
        }

        // When door is opening, update its state and thermal values
        public static void Building_Door_DoorOpen(Building_Door __instance)
        {
            CompThermal compThermal = __instance.TryGetComp<CompThermal>();
            if (compThermal != null)
                compThermal.IsOpen = true;
        }

        // When door is closing, update its state and thermal values
        public static void Building_Door_DoorTryClose(Building_Door __instance, bool __result)
        {
            if (__result)
            {
                CompThermal compThermal = __instance.TryGetComp<CompThermal>();
                if (compThermal != null)
                    compThermal.IsOpen = false;
            }
        }

        const float HeatPushPerFireSize = 21;

        // Adds heat push to ritual fires
        public static void CompRitualFireOverlay_CompTick(CompRitualFireOverlay __instance)
        {
            if (GenTicks.TicksAbs % 60 == 0 && __instance.FireSize > 0)
                TemperatureUtility.TryPushHeat(__instance.parent.Position, __instance.parent.Map, __instance.FireSize * HeatPushPerFireSize);
        }
    }
}
