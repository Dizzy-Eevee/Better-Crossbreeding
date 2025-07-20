using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using static HarmonyLib.Code;

namespace DZY_BetterCrossbreeding
{
    [StaticConstructorOnStartup]
    public static class HarmonyPatches
    {
        static HarmonyPatches()
        {
            var harmony = new Harmony("DizzyEevee.BetterCrossbreeding");
            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (def.HasComp<CompHatcher>())
                {
                    def.comps.Add(new CompProperties(typeof(DZY_CompFatherKindDef)));
                }
            }
            harmony.PatchAll();
        }
    }
    public class DZY_GameComponentBreedingDictionary : GameComponent
    {
        public Dictionary<int, PawnKindDef> dict = [];

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref dict, "dict");
        }
        public DZY_GameComponentBreedingDictionary(Game game) { }
    }
    public class DZY_Crossbreeding_Extension : DefModExtension
    {
        public Dictionary<PawnKindDef, string> inheritanceTypeDictionary = [];// Values: Paternal, Maternal, Random, Other, OtherRandom
        public Dictionary<PawnKindDef, PawnKindDef> childrenOtherDictionary = [];
        public Dictionary<PawnKindDef, List<PawnKindDef>> childrenOtherRandomDictionary = [];
        public Dictionary<PawnKindDef, List<PawnKindDefWeight>> childrenOtherRandomWeightedDictionary = [];
    }
    public class DZY_CompFatherKindDef : ThingComp
    {
        public PawnKindDef fatherKindDef;

        public override bool AllowStackWith(Thing other)
        {
            DZY_CompFatherKindDef comp = ((ThingWithComps)other).GetComp<DZY_CompFatherKindDef>();
            if (fatherKindDef != comp.fatherKindDef)
            {
                return false;
            }

            return base.AllowStackWith(other);
        }
        public override void PostSplitOff(Thing piece)
        {
            DZY_CompFatherKindDef comp = ((ThingWithComps)piece).GetComp<DZY_CompFatherKindDef>();
            comp.fatherKindDef = fatherKindDef;
        }
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Defs.Look(ref fatherKindDef, "fatherKindDef");
        }
        public override string CompInspectStringExtra()
        {
            return "Father: " + fatherKindDef.ToString();
        }
    }
    public static class DZY_Crossbreeding_Utility
    {
        public static PawnGenerationRequest GetPawnKindDefForCrossbreeding(PawnGenerationRequest request, Pawn mother, Pawn father)
        {
            if (mother == null)
            {
                return request;
            }
            DZY_Crossbreeding_Extension extension = mother.kindDef.GetModExtension<DZY_Crossbreeding_Extension>();
            if (extension == null)
            {
                return request;
            }
            PawnKindDef fatherKindDef;
            if (father == null)
            {
                DZY_GameComponentBreedingDictionary component = Current.Game.GetComponent<DZY_GameComponentBreedingDictionary>();
                fatherKindDef = component.dict.TryGetValue(mother.thingIDNumber);
            }
            else
            {
                fatherKindDef = father.kindDef;
            }
            if (fatherKindDef == null)
            {
                return request;
            }
            if (extension.inheritanceTypeDictionary.ContainsKey(fatherKindDef))
            {
                switch (extension.inheritanceTypeDictionary[fatherKindDef])
                {
                    case "Maternal":
                        request.KindDef = mother.kindDef;
                        return request;
                    case "Paternal":
                        request.KindDef = fatherKindDef;
                        return request;
                    case "Random":
                        int rand = UnityEngine.Random.Range(0, 2);
                        switch (rand)
                        {
                            case 0:
                                request.KindDef = mother.kindDef;
                                return request;
                            case 1:
                                request.KindDef = fatherKindDef;
                                return request;
                        }
                        return request;
                    case "Other":
                        if (extension.childrenOtherDictionary[fatherKindDef] != null)
                        {
                            request.KindDef = extension.childrenOtherDictionary[fatherKindDef];
                            return request;
                        }
                        return request;
                    case "OtherRandom":
                        if (extension.childrenOtherRandomDictionary[fatherKindDef] != null)
                        {
                            int rand2 = UnityEngine.Random.Range(0, extension.childrenOtherRandomDictionary[fatherKindDef].Count);
                            request.KindDef = extension.childrenOtherRandomDictionary[fatherKindDef][rand2];
                            return request;
                        }
                        return request;
                    case "OtherRandomWeighted":
                        if (extension.childrenOtherRandomWeightedDictionary[fatherKindDef] != null)
                        {
                            request.KindDef = extension.childrenOtherRandomWeightedDictionary[fatherKindDef].RandomElementByWeight<PawnKindDefWeight>(w => w.weight).kindDef;
                            return request;
                        }
                        return request;
                }
            }
            return request;
        }
        public static PawnGenerationRequest GetPawnKindDefForCrossbreeding_Egg(PawnGenerationRequest request, CompHatcher comp)
        {
            PawnKindDef mother = request.KindDef;
            PawnKindDef father = comp.parent.GetComp<DZY_CompFatherKindDef>().fatherKindDef;
            DZY_Crossbreeding_Extension extension = mother.GetModExtension<DZY_Crossbreeding_Extension>();
            if (extension == null)
            {
                return request;
            }
            if (extension.inheritanceTypeDictionary.ContainsKey(father))
            {
                switch (extension.inheritanceTypeDictionary[father])
                {
                    case "Maternal":
                        request.KindDef = mother;
                        return request;
                    case "Paternal":
                        request.KindDef = father;
                        return request;
                    case "Random":
                        int rand = UnityEngine.Random.Range(0, 2);
                        switch (rand)
                        {
                            case 0:
                                request.KindDef = mother;
                                return request;
                            case 1:
                                request.KindDef = father;
                                return request;
                        }
                        return request;
                    case "Other":
                        if (extension.childrenOtherDictionary[father] != null)
                        {
                            request.KindDef = extension.childrenOtherDictionary[father];
                            return request;
                        }
                        return request;
                    case "OtherRandom":
                        if (extension.childrenOtherRandomDictionary[father] != null)
                        {
                            int rand2 = UnityEngine.Random.Range(0, extension.childrenOtherRandomDictionary[father].Count);
                            request.KindDef = extension.childrenOtherRandomDictionary[father][rand2];
                            return request;
                        }
                        return request;
                    case "OtherRandomWeighted":
                        if (extension.childrenOtherRandomWeightedDictionary[father] != null)
                        {
                            request.KindDef = extension.childrenOtherRandomWeightedDictionary[father].RandomElementByWeight<PawnKindDefWeight>(w => w.weight).kindDef;
                            return request;
                        }
                        return request;
                }
            }
            return request;
        }
        public static void RemoveDictEntry(Pawn pawn)
        {
            DZY_GameComponentBreedingDictionary component = Current.Game.GetComponent<DZY_GameComponentBreedingDictionary>();
            component.dict.Remove(pawn.thingIDNumber);
        }
    }

    [HarmonyPatch(typeof(Hediff_Pregnant), nameof(Hediff_Pregnant.DoBirthSpawn))]
    public static class DZY_Crossbreeding_Transpiler
    {
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> DoBirthSpawn_Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator il)
        {
            MethodInfo genPawn = AccessTools.Method(typeof(PawnGenerator), nameof(PawnGenerator.GeneratePawn), [typeof(PawnGenerationRequest)]);
            MethodInfo getKindDef = AccessTools.Method(typeof(DZY_Crossbreeding_Utility), nameof(DZY_Crossbreeding_Utility.GetPawnKindDefForCrossbreeding));
            MethodInfo removeDictEntry = AccessTools.Method(typeof(DZY_Crossbreeding_Utility), nameof(DZY_Crossbreeding_Utility.RemoveDictEntry));
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
                else if (instruction.opcode == OpCodes.Ret)
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Call, removeDictEntry);
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
        public static IEnumerable<CodeInstruction> Hatch_Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator il)
        {
            FieldInfo getMother = AccessTools.Field(typeof(CompHatcher), ("hatcheeParent"));
            FieldInfo getFather = AccessTools.Field(typeof(CompHatcher), ("otherParent"));
            MethodInfo genPawn = AccessTools.Method(typeof(PawnGenerator), nameof(PawnGenerator.GeneratePawn), [typeof(PawnGenerationRequest)]);
            MethodInfo getKindDef = AccessTools.Method(typeof(DZY_Crossbreeding_Utility), nameof(DZY_Crossbreeding_Utility.GetPawnKindDefForCrossbreeding_Egg));
            bool flag = false;
            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.Calls(genPawn) && !flag)
                {
                    flag = true;
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
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
    [HarmonyPatch(typeof(Hediff_Pregnant), nameof(Hediff_Pregnant.PostAdd))]
    public static class DZY_Crossbreeding_Postfix_Pregnant_PostAdd
    {
        [HarmonyPostfix]
        public static void Pregnant_PostAdd_Postfix(Hediff_Pregnant __instance)
        {
            if (!__instance.pawn.RaceProps.Humanlike)
            {
                DZY_GameComponentBreedingDictionary component = Current.Game.GetComponent<DZY_GameComponentBreedingDictionary>();
                component.dict.Add(__instance.pawn.thingIDNumber, __instance.Father.kindDef);
            }
        }
    }
    [HarmonyPatch(typeof(CompEggLayer), nameof(CompEggLayer.Fertilize))]
    public static class DZY_Crossbreeding_Postfix_Fertilize
    {
        [HarmonyPostfix]
        public static void Fertilize_Postfix(CompEggLayer __instance, Pawn male)
        {

            DZY_GameComponentBreedingDictionary component = Current.Game.GetComponent<DZY_GameComponentBreedingDictionary>();
            component.dict.Add(__instance.parent.thingIDNumber, male.kindDef);
        }


        [HarmonyPatch(typeof(CompEggLayer), nameof(CompEggLayer.ProduceEgg))]
        public static class DZY_Crossbreeding_Postfix_ProduceEgg
        {
            [HarmonyPostfix]
            public static void ProduceEgg_Postfix(CompEggLayer __instance, Thing __result)
            {
                DZY_GameComponentBreedingDictionary component = Current.Game.GetComponent<DZY_GameComponentBreedingDictionary>();
                DZY_CompFatherKindDef comp1 = __result.TryGetComp<DZY_CompFatherKindDef>();
                if (comp1 != null)
                {
                    comp1.fatherKindDef = component.dict.TryGetValue(__instance.parent.thingIDNumber);
                    component.dict.Remove(__instance.parent.thingIDNumber);
                }
            }
        }
    }
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Destroy))]
    public static class DZY_PawnDestroy_Prefix
    {
        [HarmonyPrefix]
        public static bool PawnDestroy_Prefix(Pawn __instance)
        {
            DZY_GameComponentBreedingDictionary component = Current.Game.GetComponent<DZY_GameComponentBreedingDictionary>();
            component.dict.Remove(__instance.thingIDNumber);
            return true;
        }
    }
}