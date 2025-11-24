using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Verse;

namespace MetaGenes
{
  [HarmonyPatch]
  public static class GainXenotypePatch
  {
    public static HashSet<Pawn> pawnsToUpdate = [];

    public static MethodBase TargetMethod()
    {
      return AccessTools.Method(typeof(Pawn), "Tick");
    }

    [HarmonyPostfix]
    public static void SetXenotypeFromGene(Pawn __instance)
    {
      if (!pawnsToUpdate.Contains(__instance))
      {
        return;
      }
      Pawn_GeneTracker genes = __instance.genes;
      List<XenotypeDef> germlines = [.. genes.GenesListForReading.Where((g) => g is GainXenotypeGene xg && xg.GetGeneXenotype().inheritable).Select((g) => ((GainXenotypeGene)g).GetGeneXenotype())];
      List<XenotypeDef> xenogerms = [.. genes.GenesListForReading.Where((g) => g is GainXenotypeGene xg && !xg.GetGeneXenotype().inheritable).Select((g) => ((GainXenotypeGene)g).GetGeneXenotype())];
      XenotypeDef currentXenotype = genes.Xenotype;

      if (!germlines.Empty())
      {
        XenotypeDef selectedGermline = germlines.RandomElement();
        if (currentXenotype.defName == "Baseliner" || currentXenotype.inheritable)
        {
          genes.SetXenotypeDirect(selectedGermline);
          genes.iconDef = null;
        }
        GeneDef currentHairGene = genes.GetHairColorGene();
        for (int num = genes.Endogenes.Count - 1; num >= 0; num--)
        {
          if (genes.Endogenes[num].def != genes.GetMelaninGene())
          {
            genes.RemoveGene(genes.Endogenes[num]);
          }
        }
        for (int i = 0; i < selectedGermline.genes.Count; i++)
        {
          genes.AddGene(selectedGermline.genes[i], false);
        }

        if (genes.GetHairColorGene() == null)
        {
          currentHairGene ??= PawnHairColors.ClosestHairColorGene(__instance.story.HairColor, __instance.story.SkinColorBase);
          if (currentHairGene != null)
          {
            genes.AddGene(currentHairGene, xenogene: false);
          }
        }
      }

      if (!xenogerms.Empty())
      {
        XenotypeDef selectedXenogerm = xenogerms.RandomElement();
        genes.SetXenotype(selectedXenogerm);
      }

      foreach (Gene gene in genes.GenesListForReading.Where((g) => g is GainXenotypeGene xg))
      {
        gene.RemoveAllOfThisGene();
      }

      pawnsToUpdate.Remove(__instance);
    }
  }

  public class GainXenotypeGene : Gene
  {
    private GainXenotypeExtension extension;

    public override void Tick()
    {
      GainXenotypePatch.pawnsToUpdate.Add(pawn);
    }

    public XenotypeDef GetGeneXenotype()
    {
      extension = def.GetModExtension<GainXenotypeExtension>() ?? new GainXenotypeExtension();
      return extension.xenotype;
    }

  }

  public class GainXenotypeExtension : DefModExtension
  {
    public XenotypeDef xenotype = DefDatabase<XenotypeDef>.GetNamedSilentFail("Hussar");
  }

  [HarmonyPatch]
  public static class GainXenotypeDefGenerator
  {
    public static readonly CachedTexture germline = new("GeneIcons/BF_GainGermline");
    public static readonly CachedTexture xenogerm = new("GeneIcons/BF_GainXenogerm");

    [HarmonyPatch(typeof(GeneDefGenerator), nameof(GeneDefGenerator.ImpliedGeneDefs))]
    [HarmonyPriority(Priority.VeryLow)]
    [HarmonyPostfix]
    public static void ImpliedGeneDefs_Postfix(ref IEnumerable<GeneDef> __result)
    {
      var resultList = __result.ToList();

      foreach (var geneDef in GenerateXenotypeGenes())
      {
        geneDef.geneClass = typeof(GainXenotypeGene);
        resultList.Add(geneDef);
      }
      __result = resultList;
    }

    public static List<GeneDef> GenerateXenotypeGenes()
    {
      var result = new List<GeneDef>();
      var allXenotypes = DefDatabase<XenotypeDef>.AllDefsListForReading;

      var gainTemplate = DefDatabase<GeneTemplate>.GetNamed("BF_GainXenotypeTemplate");
      if (gainTemplate == null)
      {
        Log.Warning("MetaGenes DefGen: GenerateXenotypeGenes: Could not find the Gain Xenotype Template. Gain Xenotype genes will not be generated.");
      }

      try
      {
        foreach (var xeno in allXenotypes)
        {
          if (gainTemplate != null)
          {
            var geneExt = new GainXenotypeExtension
            {
              xenotype = xeno
            };

            result.Add(GenerateGainXenotypeGene(xeno, gainTemplate, geneExt));
          }
        }
      }
      catch (Exception e)
      {
        Log.Error($"Exception duing MetaGenes DefGen: GenerateXenotypeGenes.\nGenerating the genes has been aborted.\n{e.Message}\n{e.StackTrace}");
      }

      return result;
    }

    [HarmonyPatch(typeof(GeneUIUtility), "DrawGeneBasics")]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
      bool backgroundPatched = false;
      List<CodeInstruction> codes = instructions.ToList();
      for (int idx = 1; idx < codes.Count; idx++)
      {
        bool runBackgroundPatch = !backgroundPatched && idx > 3 && idx < codes.Count - 2 &&
           codes[idx].IsLdloc() && codes[idx].operand is LocalBuilder lb && lb.LocalIndex == 4 &&
           (codes[idx + 1].opcode == OpCodes.Callvirt && codes[idx + 1].OperandIs(typeof(CachedTexture).GetMethod("get_Texture")));

        if (runBackgroundPatch)
        {
          backgroundPatched = true;
          List<CodeInstruction> newInstructions =
          [
            new CodeInstruction(OpCodes.Ldloc_S, 4), // Load cachedTexture
            new CodeInstruction(OpCodes.Ldarg_0), // Load GeneDef
            new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(GainXenotypeDefGenerator), nameof(GetXenotypeGeneBackground))),
            new CodeInstruction(OpCodes.Stloc_S, 4), // Store cachedTexture
          ];
          codes.InsertRange(idx, newInstructions);
        }
      }

      return codes;

    }

    public static GeneDef GenerateGainXenotypeGene(XenotypeDef xenoDef, GeneTemplate template, DefModExtension extension)
    {
      if (xenoDef == null || template == null || extension == null)
      {
        Log.Error($"MetaGenes DefGen: GenerateXenotypeGene: One of the parameters was null." +
            $"\nXenoDef: {xenoDef}, template: {template}, extension: {extension}");
        return null;
      }

      string defName = $"{xenoDef.defName}_{template.keyTag}";

      var geneDef = new GeneDef
      {
        defName = defName,
        label = String.Format(template.label, xenoDef.label, xenoDef.inheritable ? "germline" : "xenogerm"),
        description = String.Format(template.description, xenoDef.label, xenoDef.inheritable ? "germline" : "xenogerm", xenoDef.inheritable ? "endogenes" : "xenogenes"),
        customEffectDescriptions = [String.Format(template.customEffectDescriptions[0], xenoDef.label, xenoDef.inheritable ? "germline" : "xenogerm")],
        iconPath = xenoDef.iconPath,
        biostatCpx = 0,
        biostatMet = 0,
        displayCategory = template.displayCategory,
        canGenerateInGeneSet = template.canGenerateInGeneSet,
        selectionWeight = template.selectionWeight,
        modExtensions = [extension]
      };

      return geneDef;
    }

    public static CachedTexture GetXenotypeGeneBackground(CachedTexture previous, GeneDef gene)
    {
      if (gene.geneClass == typeof(GainXenotypeGene))
      {
        var extension = gene.GetModExtension<GainXenotypeExtension>() ?? new GainXenotypeExtension();
        return extension.xenotype.inheritable ? germline : xenogerm;
      }
      return previous;
    }
  }
}
