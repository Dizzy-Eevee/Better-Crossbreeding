using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace DZY_BetterCrossbreeding
{
    [StaticConstructorOnStartup]
    public static class HarmonyPatches
    {
        static HarmonyPatches()
        {
            var harmony = new Harmony("DizzyEevee.BetterCrossbreeding");
            harmony.PatchAll();
        }
    }
    public class DZY_Crossbreeding_Extension : DefModExtension
    {
        public Dictionary<PawnKindDef, string> inheritanceTypeDictionary = [];// Values: Paternal, Maternal, Random, Other, OtherRandom
        public Dictionary<PawnKindDef, PawnKindDef> childrenOtherDictionary = []; 
        public Dictionary<PawnKindDef, List<PawnKindDef>> childrenOtherRandomDictionary = [];
    }
    public static class DZY_Crossbreeding_Utility
    {
        public static PawnGenerationRequest DZY_GetPawnKindDefForCrossbreeding(PawnGenerationRequest request, Pawn mother, Pawn father)
        {
            
            DZY_Crossbreeding_Extension extension = mother.kindDef.GetModExtension<DZY_Crossbreeding_Extension>();
            DZY_Crossbreeding_Extension extensionFather = father.kindDef.GetModExtension<DZY_Crossbreeding_Extension>();
            if (extension == null)
            {
                return request;
            }
            if (extension.inheritanceTypeDictionary.ContainsKey(father.kindDef)) 
            {
                switch (extension.inheritanceTypeDictionary[father.kindDef])
                {
                    case "Maternal":
                        request.KindDef = mother.kindDef;
                        return request;
                    case "Paternal":
                        request.KindDef = father.kindDef;
                        return request;
                    case "Random":
                        int rand = Random.Range(0, 2);
                        switch (rand)
                        {
                            case 0:
                                request.KindDef = mother.kindDef;
                                return request;
                            case 1:
                                request.KindDef = father.kindDef;
                                return request;
                        }
                        return request;
                    case "Other":
                        if (extension.childrenOtherDictionary[father.kindDef] != null)
                        {
                            request.KindDef = extension.childrenOtherDictionary[father.kindDef];
                            return request;
                        }
                        return request;
                    case "OtherRandom":
                        if (extension.childrenOtherRandomDictionary[father.kindDef] != null)
                        {
                            int rand2 = Random.Range(0, extension.childrenOtherRandomDictionary[father.kindDef].Count);
                            request.KindDef = extension.childrenOtherRandomDictionary[father.kindDef][rand2];
                            return request;
                        }
                        return request;
                }
            }
            return request;
        }
    }

    [HarmonyPatch(typeof(Hediff_Pregnant), nameof(Hediff_Pregnant.DoBirthSpawn))]
    public static class DZY_Crossbreeding_Transpiler
    {
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> DZY_DoBirthSpawn_Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator il)
        {
            MethodInfo genPawn = AccessTools.Method(typeof(PawnGenerator), nameof(PawnGenerator.GeneratePawn), [typeof(PawnGenerationRequest)]);
            MethodInfo getKindDef = AccessTools.Method(typeof(DZY_Crossbreeding_Utility), nameof(DZY_Crossbreeding_Utility.DZY_GetPawnKindDefForCrossbreeding));
            bool flag = false;
            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.Calls(genPawn) && !flag)
                {
                    flag = true;
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldarg_1);
                    yield return new CodeInstruction(OpCodes.Call, getKindDef);
                    yield return instruction;
                }
                else
                {
                    yield return instruction;
                }
            }
        }

    }
    [HarmonyPatch(typeof(CompHatcher), nameof(CompHatcher.Hatch))]
    public static class DZY_Crossbreeding_Transpiler_Hatch
    {
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> DZY_Hatch_Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator il)
        {
            FieldInfo getMother = AccessTools.Field(typeof(CompHatcher), ("hatcheeParent"));
            FieldInfo getFather = AccessTools.Field(typeof(CompHatcher), ("otherParent"));
            MethodInfo genPawn = AccessTools.Method(typeof(PawnGenerator), nameof(PawnGenerator.GeneratePawn), [typeof(PawnGenerationRequest)]);
            MethodInfo getKindDef = AccessTools.Method(typeof(DZY_Crossbreeding_Utility), nameof(DZY_Crossbreeding_Utility.DZY_GetPawnKindDefForCrossbreeding));
            bool flag = false;
            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.Calls(genPawn) && !flag)
                {
                    flag = true;
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldfld, getMother);
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldfld, getFather);
                    yield return new CodeInstruction(OpCodes.Call, getKindDef);
                    yield return instruction;
                }
                else
                {
                    yield return instruction;
                }
            }
        }

    }
    [HarmonyPatch(typeof(CompHatcher), nameof(CompHatcher.AllowStackWith))]
    public static class DZY_Crosspreeding_Postfix_EggStacking
    {
        [HarmonyPostfix]
        public static void DZY_AllowStackWith_Postfix(CompHatcher __instance, Thing other, ref bool __result)
        {
            if (__result)
            {
                CompHatcher comp = ((ThingWithComps)other).GetComp<CompHatcher>();
                __result = __instance.otherParent == comp.otherParent;
            }
        }
    }

}