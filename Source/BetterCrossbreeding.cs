using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Xml;
using HarmonyLib;
using RimWorld;
using Verse;

namespace DZY.CrossBreeding
{
    [StaticConstructorOnStartup]
    public static class HarmonyPatches
    {
        static HarmonyPatches()
        {
            var harmony = new Harmony("DizzyEevee.BetterCrossbreeding");
            int i = 0;
            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (def.HasComp<CompHatcher>())
                {
                    def.comps.Add(new CompProperties(typeof(CompParentKindDef)));
                    i++;
                }
            }
            Log.Message("BetterCrossbreeding added CompParentKindDef to " + i + " egg defs.");
            //foreach (PawnKindDef def in DefDatabase<PawnKindDef>.AllDefsListForReading)
            //{

            //    if (def.HasModExtension<Extension>())
            //    {
            //        Extension x = def.GetModExtension<Extension>();
            //        foreach (PawnKindDefOutcomes outcome in x.outcomes)
            //        {
            //            Log.Message(outcome.kindDef.ToString());
            //            Log.Message(outcome.behavior);
            //            for (int i = 0; i < outcome.childrenKinds.Count; i++)
            //            {
            //                Log.Message(outcome.childrenKinds[i].ToString());
            //                Log.Message(outcome.childrenWeights[i].ToString());
            //            }
            //        }
            //    }
            //}
            harmony.PatchAll();
        }
    }

    public class BehaviorAndChildren
    {
        public void LoadDataFromXmlCustom(XmlNode xmlRoot)
        {
            XmlHelper.ParseElements(this, xmlRoot, "behavior", "children");
        }
    }
    public class PawnKindDefOutcomes
    {
        public PawnKindDef kindDef;
        public string behavior;
        public List<PawnKindDef> childrenKinds = [];
        public List<int> childrenWeights = [];
        public void LoadDataFromXmlCustom(XmlNode xmlRoot)
        {
            //XmlHelper.ParseElements(this, xmlRoot, "kindDef", "outcome");
            DirectXmlCrossRefLoader.RegisterObjectWantsCrossRef(this, "kindDef", xmlRoot.Name);
            foreach (XmlNode childNode in xmlRoot.ChildNodes)
            {
                if (childNode is XmlComment)
                {
                    continue;
                }
                behavior = childNode.Name;
                foreach (XmlNode childNode2 in childNode.ChildNodes)
                {
                    if (childNode2 is XmlComment)
                    {
                        continue;
                    }
                    DirectXmlCrossRefLoader.RegisterListWantsCrossRef(childrenKinds, childNode2.Name);
                    if (childNode2.InnerText != "")
                    {
                        childrenWeights.Add(Int32.Parse(childNode2.InnerText));
                    }
                    else
                    {
                        childrenWeights.Add(1);
                    }
                }
            }
        }
    }

    public class GameComponentBreedingDictionary : GameComponent
    {
        public Dictionary<int, PawnKindDef> dict = [];

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref dict, "dict");
        }
        public GameComponentBreedingDictionary(Game game) { }
    }
    public class Extension : DefModExtension
    {
        public List<PawnKindDefOutcomes> outcomes = [];// Values: Paternal, Maternal, Random, Other, OtherRandom
    }
    public class CompParentKindDef : ThingComp
    {
        public PawnKindDef fatherKindDef;
        public PawnKindDef motherKindDef;

        public override bool AllowStackWith(Thing other)
        {
            CompParentKindDef comp = ((ThingWithComps)other).GetComp<CompParentKindDef>();
            if (fatherKindDef != comp.fatherKindDef)
            {
                return false;
            }

            return base.AllowStackWith(other);
        }
        public override void PostSplitOff(Thing piece)
        {
            CompParentKindDef comp = ((ThingWithComps)piece).GetComp<CompParentKindDef>();
            comp.fatherKindDef = fatherKindDef;
            comp.motherKindDef = motherKindDef;
        }
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Defs.Look(ref fatherKindDef, "fatherKindDef");
            Scribe_Defs.Look(ref motherKindDef, "motherKindDef");
        }
        public override string CompInspectStringExtra()
        {
            if (fatherKindDef != null)
            {
                return "Father: " + fatherKindDef.ToString();
            }
            else return null;
        }
    }
    public static class CrossbreedingUtility
    {
        public static PawnGenerationRequest GetPawnKindDefForCrossbreeding(PawnGenerationRequest request, Pawn mother, Pawn father)
        {
            if (mother == null)
            {
                return request;
            }
            Extension extension = mother.kindDef.GetModExtension<Extension>();
            if (extension == null)
            {
                return request;
            }
            PawnKindDef fatherKindDef;
            if (father == null)
            {
                GameComponentBreedingDictionary component = Current.Game.GetComponent<GameComponentBreedingDictionary>();
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
            List<PawnKindDef> validFathers = [];
            foreach (PawnKindDefOutcomes x in extension.outcomes)
            {
                validFathers.Add(x.kindDef);
            }
            if (validFathers.Contains(fatherKindDef))
            {
                int index = validFathers.IndexOf(fatherKindDef);
                switch (extension.outcomes[index].behavior)
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
                        List<PawnKindDefWeight> x = [];
                        for (int i = 0; i < extension.outcomes[index].childrenKinds.Count; i++)
                        {
                            x.Add(new PawnKindDefWeight
                            {
                                kindDef = extension.outcomes[index].childrenKinds[i],
                                weight = extension.outcomes[index].childrenWeights[i]
                            }
                            );
                        }
                        request.KindDef = x.RandomElementByWeight(w => w.weight).kindDef;
                        return request;
                }
            }
            return request;
        }
        public static PawnGenerationRequest GetPawnKindDefForCrossbreeding_Egg(PawnGenerationRequest request, CompHatcher comp)
        {
            CompParentKindDef parentKindDef = comp.parent.GetComp<CompParentKindDef>();
            PawnKindDef father = parentKindDef.fatherKindDef;
            PawnKindDef mother = parentKindDef.motherKindDef;
            if (father == null || mother == null)
            {
                return request;
            }
            Extension extension = mother.GetModExtension<Extension>();
            if (extension == null)
            {
                return request;
            }
            List<PawnKindDef> validFathers = [];
            foreach (PawnKindDefOutcomes x in extension.outcomes)
            {
                validFathers.Add(x.kindDef);
            }
            if (validFathers.Contains(father))
            {
                int index = validFathers.IndexOf(father);
                switch (extension.outcomes[index].behavior)
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
                        List<PawnKindDefWeight> x = [];
                        for (int i = 0; i < extension.outcomes[index].childrenKinds.Count; i++)
                        {
                            x.Add(new PawnKindDefWeight
                            {
                                kindDef = extension.outcomes[index].childrenKinds[i],
                                weight = extension.outcomes[index].childrenWeights[i]
                            }
                            );
                        }
                        request.KindDef = x.RandomElementByWeight(w => w.weight).kindDef;
                        return request;
                }
            }
            return request;
        }
        public static void RemoveDictEntry(Pawn pawn)
        {
            GameComponentBreedingDictionary component = Current.Game.GetComponent<GameComponentBreedingDictionary>();
            component.dict.Remove(pawn.thingIDNumber);
        }
    }

    [HarmonyPatch(typeof(Hediff_Pregnant), nameof(Hediff_Pregnant.DoBirthSpawn))]
    public static class DoBirthSpawn_Patch
    {
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> DoBirthSpawn_Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator il)
        {
            MethodInfo genPawn = AccessTools.Method(typeof(PawnGenerator), nameof(PawnGenerator.GeneratePawn), [typeof(PawnGenerationRequest)]);
            MethodInfo getKindDef = AccessTools.Method(typeof(CrossbreedingUtility), nameof(CrossbreedingUtility.GetPawnKindDefForCrossbreeding));
            MethodInfo removeDictEntry = AccessTools.Method(typeof(CrossbreedingUtility), nameof(CrossbreedingUtility.RemoveDictEntry));
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
    public static class CompHatcher_Patch
    {
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Hatch_Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator il)
        {
            FieldInfo getMother = AccessTools.Field(typeof(CompHatcher), ("hatcheeParent"));
            FieldInfo getFather = AccessTools.Field(typeof(CompHatcher), ("otherParent"));
            MethodInfo genPawn = AccessTools.Method(typeof(PawnGenerator), nameof(PawnGenerator.GeneratePawn), [typeof(PawnGenerationRequest)]);
            MethodInfo getKindDef = AccessTools.Method(typeof(CrossbreedingUtility), nameof(CrossbreedingUtility.GetPawnKindDefForCrossbreeding_Egg));
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
    public static class Pregnant_PostAdd_Patch
    {
        [HarmonyPostfix]
        public static void Pregnant_PostAdd_Postfix(Hediff_Pregnant __instance)
        {
            if (!__instance.pawn.RaceProps.Humanlike)
            {
                GameComponentBreedingDictionary component = Current.Game.GetComponent<GameComponentBreedingDictionary>();
                component.dict.Add(__instance.pawn.thingIDNumber, __instance.Father.kindDef);
            }
        }
    }
    [HarmonyPatch(typeof(CompEggLayer), nameof(CompEggLayer.Fertilize))]
    public static class Fertilize_Patch
    {
        [HarmonyPostfix]
        public static void Fertilize_Postfix(CompEggLayer __instance, Pawn male)
        {

            GameComponentBreedingDictionary component = Current.Game.GetComponent<GameComponentBreedingDictionary>();
            component.dict.Add(__instance.parent.thingIDNumber, male.kindDef);
        }


        [HarmonyPatch(typeof(CompEggLayer), nameof(CompEggLayer.ProduceEgg))]
        public static class ProduceEgg_Patch
        {
            [HarmonyPostfix]
            public static void ProduceEgg_Postfix(CompEggLayer __instance, Thing __result)
            {
                GameComponentBreedingDictionary component = Current.Game.GetComponent<GameComponentBreedingDictionary>();
                CompParentKindDef comp1 = __result.TryGetComp<CompParentKindDef>();
                if (comp1 != null)
                {
                    comp1.fatherKindDef = component.dict.TryGetValue(__instance.parent.thingIDNumber);
                    Pawn mother = __instance.parent as Pawn;
                    comp1.motherKindDef = mother.kindDef;
                    component.dict.Remove(__instance.parent.thingIDNumber);
                }
            }
        }
    }
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Destroy))]
    public static class PawnDestroy_Patch
    {
        [HarmonyPrefix]
        public static bool PawnDestroy_Prefix(Pawn __instance)
        {
            GameComponentBreedingDictionary component = Current.Game.GetComponent<GameComponentBreedingDictionary>();
            component.dict.Remove(__instance.thingIDNumber);
            return true;
        }
    }
}