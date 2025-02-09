﻿using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

using static Celsius.LogUtility;

namespace Celsius
{
    static class FreezeMeltUtility
    {
        public static TerrainThermalProperties GetTerrainThermalProperties(this TerrainDef terrain) => terrain.GetModExtension<TerrainThermalProperties>();

        public static bool Freezable(this TerrainDef terrain)
        {
            TerrainThermalProperties terrainProps = terrain.GetTerrainThermalProperties();
            return terrainProps != null && terrainProps.phaseTransition == PhaseTransitionType.Freeze;
        }

        public static bool Meltable(this TerrainDef terrain)
        {
            TerrainThermalProperties terrainProps = terrain.GetTerrainThermalProperties();
            return terrainProps != null && terrainProps.phaseTransition == PhaseTransitionType.Melt;
        }

        public static bool FreezesAt(this TerrainDef terrain, float temperature)
        {
            TerrainThermalProperties terrainProps = terrain.GetTerrainThermalProperties();
            return terrainProps != null && terrainProps.FreezesAt(temperature);
        }

        public static bool MeltsAt(this TerrainDef terrain, float temperature)
        {
            TerrainThermalProperties terrainProps = terrain.GetTerrainThermalProperties();
            return terrainProps != null && terrainProps.MeltsAt(temperature);
        }

        /// <summary>
        /// Returns best guess for what kind of water terrain should be placed in a cell (if ice melts there)
        /// </summary>
        public static TerrainDef BestUnderIceTerrain(this IntVec3 cell, Map map)
        {
            TerrainDef terrain = map.terrainGrid.UnderTerrainAt(cell), underTerrain;
            if (terrain != null)
                return terrain;

            terrain = map.terrainGrid.TerrainAt(cell).GetTerrainThermalProperties()?.turnsInto;
            if (terrain != null)
                return terrain;

            bool foundGround = false;
            List<IntVec3> adjacentCells = GenAdjFast.AdjacentCells8Way(cell);
            for (int i = 0; i < 8; i++)
            {
                IntVec3 c = adjacentCells[i];
                if (!c.InBounds(map))
                    continue;
                terrain = c.GetTerrain(map);
                if (terrain.Freezable())
                    return terrain;
                underTerrain = map.terrainGrid.UnderTerrainAt(c);
                if (underTerrain != null && underTerrain.Freezable())
                    return underTerrain;
                if (!terrain.Meltable() || (underTerrain != null && !underTerrain.Meltable()))
                    foundGround = true;
            }

            if (foundGround)
                return map.Biome == BiomeDefOf.SeaIce ? TerrainDefOf.WaterOceanShallow : TerrainDefOf.WaterShallow;
            return map.Biome == BiomeDefOf.SeaIce ? TerrainDefOf.WaterOceanDeep : TerrainDefOf.WaterDeep;
        }

        /// <summary>
        /// Turns the given cell into ice
        /// </summary>
        public static void FreezeTerrain(this IntVec3 cell, Map map, bool log = false)
        {
            TerrainDef terrain = cell.GetTerrain(map);
            if (log)
                Log($"{terrain} freezes at {cell}.");
            map.terrainGrid.SetTerrain(cell, terrain.GetTerrainThermalProperties()?.turnsInto ?? TerrainDefOf.Ice);
            map.terrainGrid.SetUnderTerrain(cell, terrain);
        }

        /// <summary>
        /// Turns the given cell into the appropriate kind of water terrain; 
        /// </summary>
        public static void MeltTerrain(this IntVec3 cell, Map map, bool log = false)
        {
            int index = map.cellIndices.CellToIndex(cell);
            TerrainDef meltedTerrain = cell.BestUnderIceTerrain(map);
            // Removing things that can't stay on the melted terrain
            List<Thing> things = map.thingGrid.ThingsListAtFast(index);
            for (int i = things.Count - 1; i >= 0; i--)
            {
                Thing thing = things[i];
                if (!thing.def.destroyable)
                    continue;
                if (meltedTerrain.passability == Traversability.Impassable)
                    if (thing is Pawn pawn)
                    {
                        Log($"{pawn.LabelCap} drowns in {meltedTerrain.label}.");
                        pawn.health?.AddHediff(DefOf.Celsius_Hediff_Drown, dinfo: new DamageInfo(DefOf.Celsius_Damage_Drown, 1));
                        pawn.Corpse?.Destroy();
                    }
                    else
                    {
                        Log($"{thing.LabelCap} sinks in {meltedTerrain.label}.");
                        CompDissolution compDissolution = thing.TryGetComp<CompDissolution>();
                        if (compDissolution != null)
                        {
                            Log($"Applying dissolution effects of {thing.def}.");
                            compDissolution.TriggerDissolutionEvent(thing.stackCount);
                        }
                        else thing.Destroy();
                    }
                else
                {
                    TerrainAffordanceDef terrainAffordance = thing.TerrainAffordanceNeeded;
                    if (terrainAffordance != null && !meltedTerrain.affordances.Contains(terrainAffordance))
                    {
                        Log($"{thing.def}'s terrain affordance: {terrainAffordance}. {meltedTerrain.LabelCap} provides: {meltedTerrain.affordances.Select(def => def.defName).ToCommaList()}. {thing.LabelCap} can't stand on {meltedTerrain.label} and is destroyed.");
                        if (thing is Building_Grave grave && grave.HasAnyContents)
                        {
                            Log($"Grave with {grave.ContainedThing?.LabelShort} is uncovered due to melting.");
                            grave.EjectContents();
                        }
                        thing.Destroy();
                    }
                }
            }
            if (map.snowGrid.GetDepth(cell) > 0)
                map.snowGrid.SetDepth(cell, 0);

            // Changing terrain
            if (map.terrainGrid.UnderTerrainAt(index) == null)
                map.terrainGrid.SetUnderTerrain(cell, meltedTerrain);
            if (log)
                Log($"Ice at {cell} melts into {meltedTerrain}.");
            map.terrainGrid.RemoveTopLayer(cell, false);
        }

        // Based on vanilla formula: 0.0058 * T * [T / 10] for 0.06% of cells every tick
        public static float SnowChangeAmountAt(float temperature) => -0.00058f * 0.0006f * temperature * Mathf.Clamp(temperature, 0, 10);
    }
}
