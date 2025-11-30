using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using Verse;

namespace XenotypePlusPlus
{
  public class GermlineCompProperties : CompProperties
  {
    public GermlineCompProperties()
    {
      compClass = typeof(GermlineComp);
    }
  }

  public class GermlineComp : ThingComp
  {
    public GermlineCompProperties Props => (GermlineCompProperties)props;
    private XenotypeDef germline;
    public string germlineName;
    public XenotypeIconDef iconDef;
    public bool hybrid;
    [Unsaved(false)]
    private CustomXenotype cachedCustomXenotype;

    [Unsaved(false)]
    private bool? cachedHasCustomXenotype;

    public bool UniqueXenotype => !germlineName.NullOrEmpty();

    public string XenotypeLabel
    {
      get
      {
        if (!UniqueXenotype)
        {
          return germline.label;
        }
        return germlineName ?? ((string)"Unique".Translate());
      }
    }

    public string XenotypeLabelCap => XenotypeLabel.CapitalizeFirst();

    public Texture2D XenotypeIcon
    {
      get
      {
        if (!ModsConfig.BiotechActive)
        {
          return null;
        }
        if (iconDef != null)
        {
          return iconDef.Icon;
        }
        if (UniqueXenotype)
        {
          return XenotypeIconDefOf.Basic.Icon;
        }
        return germline.Icon;
      }
    }

    public CustomXenotype CustomXenotype
    {
      get
      {
        if (parent is not Pawn pawn)
        {
          return null;
        }
        if (germline != XenotypeDefOf.Baseliner || hybrid || Current.ProgramState != ProgramState.Playing)
        {
          return null;
        }
        if (!cachedHasCustomXenotype.HasValue)
        {
          cachedHasCustomXenotype = false;
          foreach (CustomXenotype customXenotype in Current.Game.customXenotypeDatabase.customXenotypes)
          {
            if (customXenotype.inheritable && GeneUtility.PawnIsCustomXenotype(pawn, customXenotype))
            {
              cachedHasCustomXenotype = true;
              cachedCustomXenotype = customXenotype;
              break;
            }
          }
        }
        return cachedCustomXenotype;
      }
    }

    public void SetGermline(XenotypeDef newGermline)
    {
      germline = newGermline;
      germlineName = null;
      iconDef = null;
    }

    public void FindGermline()
    {
      if (parent is not Pawn pawn)
      {
        return;
      }
      if (pawn.genes.Xenotype.inheritable)
      {
        germline = pawn.genes.Xenotype;
        germlineName = null;
        iconDef = null;
      }
      else
      {
        germline ??= XenotypeDefOf.Baseliner;
      }
    }

    public override void PostExposeData()
    {
      base.PostExposeData();

      if (Scribe.mode == LoadSaveMode.Saving)
      {
        if (germline == null)
        {
          FindGermline();
        }
        Scribe_Defs.Look(ref germline, "germline");
        Scribe_Values.Look(ref germlineName, "germlineName");
        Scribe_Values.Look(ref hybrid, "hybrid", defaultValue: false);
        Scribe_Defs.Look(ref iconDef, "iconDef");
      }
      else if (Scribe.mode == LoadSaveMode.LoadingVars)
      {
        Scribe_Defs.Look(ref germline, "germline");
        Scribe_Values.Look(ref germlineName, "germlineName");
        Scribe_Values.Look(ref hybrid, "hybrid", defaultValue: false);
        Scribe_Defs.Look(ref iconDef, "iconDef");
        if (germline == null)
        {
          FindGermline();
        }
      }
    }

    public XenotypeDef Germline
    {
      get
      {
        if (germline == null)
        {
          FindGermline();
        }

        return germline;
      }
    }

    public string XenotypeDescShort
    {
      get
      {
        if (UniqueXenotype)
        {
          return "UniqueXenotypeDesc".Translate();
        }
        if (!germline.descriptionShort.NullOrEmpty())
        {
          return germline.descriptionShort + "\n\n" + "MoreInfoInInfoScreen".Translate().Colorize(ColoredText.SubtleGrayColor);
        }
        return germline.description;
      }
    }

    public void TryUseCustom(CustomXenotype custom)
    {
      if (custom != null && custom.inheritable)
      {
        germlineName = custom.name;
        iconDef = custom.iconDef;
      }
    }


    public GermlineComp Clone()
    {
      return new GermlineComp
      {
        germline = this.germline,
        germlineName = this.germlineName,
        iconDef = this.iconDef,
        hybrid = this.hybrid,
        cachedCustomXenotype = this.cachedCustomXenotype,
        cachedHasCustomXenotype = this.cachedHasCustomXenotype
      };
    }
  }


  [HarmonyPatch]
  public static class XenotypeChanges
  {
    public static bool SeparateGermline(this Pawn pawn, out GermlineComp germlineData)
    {
      germlineData = pawn.GetComp<GermlineComp>();
      if (germlineData.CustomXenotype != null)
      {
        return germlineData.CustomXenotype != pawn.genes.CustomXenotype;
      }
      return germlineData.Germline != pawn.genes.Xenotype || germlineData.germlineName != pawn.genes.xenotypeName; 
    }

    [HarmonyPatch(typeof(GeneUIUtility), nameof(GeneUIUtility.DrawGenesInfo))]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> DrawGenesInfoPatch(IEnumerable<CodeInstruction> instructions)
    {
      var drawMethod = AccessTools.Method(typeof(XenotypeChanges), nameof(TryDrawGermline));

      foreach (CodeInstruction instruction in instructions)
      {
        if (instruction.opcode == OpCodes.Call && instruction.operand is MethodInfo methodInfo && methodInfo.DeclaringType == typeof(BiostatsTable) && methodInfo.Name == nameof(BiostatsTable.Draw))
        {
          yield return new CodeInstruction(OpCodes.Pop);
          yield return new CodeInstruction(OpCodes.Ldarg_1);
          yield return new CodeInstruction(OpCodes.Call, drawMethod);
        }
        else
        {
          yield return instruction;
        }
      }
    }

    public static void TryDrawGermline(Rect tableRect, int gcx, int met, int arc, bool drawMax, bool ignoreLimits, Thing target)
    {
      if (target is not Pawn pawn)
      {
        return;
      }

      bool drawGermline = pawn.SeparateGermline(out GermlineComp germlineData);

      if (drawGermline)
      {
        tableRect.width -= 140f;
        Rect germlineRect = new(tableRect.xMax + 4f, tableRect.y + Text.LineHeight / 2f, 140f, Text.LineHeight);
        Text.Anchor = TextAnchor.UpperCenter;
        Widgets.Label(germlineRect, germlineData.XenotypeLabelCap);
        Text.Anchor = TextAnchor.UpperLeft;
        Rect position = new(germlineRect.center.x - 17f, germlineRect.yMax + 4f, 34f, 34f);
        GUI.color = XenotypeDef.IconColor;
        GUI.DrawTexture(position, germlineData.XenotypeIcon);
        GUI.color = Color.white;
        germlineRect.yMax = position.yMax;
        if (Mouse.IsOver(germlineRect))
        {
          Widgets.DrawHighlight(germlineRect);
          TooltipHandler.TipRegion(germlineRect, () => ("Xenotype".Translate() + ": " + germlineData.XenotypeLabelCap).Colorize(ColoredText.TipSectionTitleColor) + "\n\n" + germlineData.XenotypeDescShort, 883938493);
        }
        if (Widgets.ButtonInvisible(germlineRect) && !germlineData.UniqueXenotype)
        {
          Find.WindowStack.Add(new Dialog_InfoCard(germlineData.Germline));
        }
      }

      BiostatsTable.Draw(tableRect, gcx, met, arc, drawMax, ignoreLimits);

      if (drawGermline)
      {
        tableRect.width += 140f;
      }

    }

    [HarmonyPatch(typeof(Pawn_GeneTracker), nameof(Pawn_GeneTracker.SetXenotype))]
    [HarmonyPatch(typeof(Pawn_GeneTracker), nameof(Pawn_GeneTracker.SetXenotypeDirect))]
    [HarmonyPostfix]
    public static void UpdateGermline(Pawn_GeneTracker __instance, XenotypeDef xenotype)
    {
      if (xenotype.IsGermline())
      {
        __instance.pawn.GetComp<GermlineComp>().SetGermline(xenotype);
      }
      else
      {
        __instance.pawn.GetComp<GermlineComp>().FindGermline();
      }
    }

    [HarmonyPatch(typeof(PawnGenerator), "GenerateGenes")]
    [HarmonyPostfix]
    public static void InitCustomXenotype(Pawn pawn, PawnGenerationRequest request)
    {
      if (request.ForcedCustomXenotype != null)
      {
        pawn.GetComp<GermlineComp>().TryUseCustom(request.ForcedCustomXenotype);
      }
    }

    [HarmonyPatch(typeof(GameComponent_PawnDuplicator), nameof(GameComponent_PawnDuplicator.Duplicate))]
    [HarmonyPostfix]
    public static void DuplicateGermline(Pawn pawn, Pawn __result)
    {
      GermlineComp existing = __result.GetComp<GermlineComp>();
      if (existing == null)
      {
        __result.AllComps.Add(pawn.GetComp<GermlineComp>().Clone());
      }
      else
      {
        __result.AllComps.Replace(__result.GetComp<GermlineComp>(), pawn.GetComp<GermlineComp>().Clone());
      }
    }

    [HarmonyPatch(typeof(Pawn_GeneTracker), nameof(Pawn_GeneTracker.SetXenotype))]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> PreserveXenotype(IEnumerable<CodeInstruction> instructions, ILGenerator il)
    {
      var validateMethod = AccessTools.Method(typeof(XenotypeChanges), nameof(KeepXenotype));

      bool flag = false;
      bool flag2 = false;
      Label label = il.DefineLabel();
      List<CodeInstruction> codes = [.. instructions];
      for (int i = 0; i < codes.Count; i++)
      {
        if (!flag && codes[i].opcode == OpCodes.Ldarg_1)
        {
          flag = true;
          yield return codes[i];
          yield return new CodeInstruction(OpCodes.Call, validateMethod);
          yield return new CodeInstruction(OpCodes.Brtrue, label);
          yield return new CodeInstruction(OpCodes.Ldarg_0);
          yield return new CodeInstruction(OpCodes.Ldarg_1);
        }
        else if (!flag2 && i + 1 < codes.Count && codes[i + 1].opcode == OpCodes.Call && codes[i + 1].operand is MethodInfo methodInfo && methodInfo.Name == nameof(Pawn_GeneTracker.ClearXenogenes))
        {
          flag2 = true;
          yield return new CodeInstruction(OpCodes.Nop).WithLabels(label);
          yield return codes[i];
        }
        else
        {
          yield return codes[i];
        }
      }
    }

    [HarmonyPatch(typeof(Pawn_GeneTracker), nameof(Pawn_GeneTracker.SetXenotypeDirect))]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> PreserveXenotypeDirect(IEnumerable<CodeInstruction> instructions, ILGenerator il)
    {
      var validateMethod = AccessTools.Method(typeof(XenotypeChanges), nameof(KeepXenotype));

      bool flag = false;
      bool flag2 = false;
      Label label = il.DefineLabel();
      List<CodeInstruction> codes = [.. instructions];
      for (int i = 0; i < codes.Count; i++)
      {
        if (!flag && codes[i].opcode == OpCodes.Ldarg_1)
        {
          flag = true;
          yield return codes[i];
          yield return new CodeInstruction(OpCodes.Call, validateMethod);
          yield return new CodeInstruction(OpCodes.Brtrue, label);
          yield return new CodeInstruction(OpCodes.Ldarg_0);
          yield return new CodeInstruction(OpCodes.Ldarg_1);
        }
        else if (!flag2 && codes[i].opcode == OpCodes.Ret)
        {
          flag2 = true;
          yield return new CodeInstruction(OpCodes.Nop).WithLabels(label);
          yield return codes[i];
        }
        else
        {
          yield return codes[i];
        }
      }
    }

    public static bool KeepXenotype(Pawn_GeneTracker genes, XenotypeDef xenotype)
    {
      return xenotype.IsGermline() && !genes.Xenotype.IsGermline();
    }

    [HarmonyPatch(typeof(CharacterCardUtility), "DoTopStack")]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> DrawGermlineStack(IEnumerable<CodeInstruction> instructions)
    {
      int target = -1;
      var method = AccessTools.Method(typeof(XenotypeChanges), nameof(AddGermlineStack));

      List<CodeInstruction> codes = [.. instructions];
      for (int i = 0; i < codes.Count; i++)
      {
        if (codes[i].opcode == OpCodes.Callvirt && codes[i].operand is MethodInfo methodInfo && methodInfo.DeclaringType == typeof(Pawn_GeneTracker) && methodInfo.Name == "get_GenesListForReading")
        {
          target = i + 3;
        }
      }

      if (target > 0)
      {
        codes.InsertRange(target, [
          new CodeInstruction(OpCodes.Ldarg_0),
          CodeInstruction.LoadField(typeof(CharacterCardUtility), "tmpStackElements"),
          new CodeInstruction(OpCodes.Call, method)
        ]);
      }

      return codes;
    }

    private static void AddGermlineStack(Pawn pawn, List<GenUI.AnonymousStackElement> tmpStackElements)
    {
      if (!pawn.SeparateGermline(out GermlineComp germlineData))
      {
        return;
      }
      float num2 = 22f;
      num2 += Text.CalcSize(germlineData.XenotypeLabelCap).x + 14f;
      tmpStackElements.Add(new GenUI.AnonymousStackElement
      {
        drawer = delegate (Rect r)
        {
          Rect rect2 = new(r.x, r.y, r.width, r.height);
          GUI.color = CharacterCardUtility.StackElementBackground;
          GUI.DrawTexture(rect2, BaseContent.WhiteTex);
          GUI.color = Color.white;
          if (Mouse.IsOver(rect2))
          {
            Widgets.DrawHighlight(rect2);
          }
          Rect position = new(r.x + 1f, r.y + 1f, 20f, 20f);
          GUI.color = XenotypeDef.IconColor;
          GUI.DrawTexture(position, germlineData.XenotypeIcon);
          GUI.color = Color.white;
          Widgets.Label(new Rect(r.x + 22f + 5f, r.y, r.width + 22f - 1f, r.height), germlineData.XenotypeLabelCap);
          if (Mouse.IsOver(r))
          {
            TooltipHandler.TipRegion(r, () => ("Xenotype".Translate() + ": " + germlineData.XenotypeLabelCap).Colorize(ColoredText.TipSectionTitleColor) + "\n\n" + germlineData.XenotypeDescShort + "\n\n" + "ViewGenesDesc".Translate(pawn.Named("PAWN")).ToString().StripTags()
              .Colorize(ColoredText.SubtleGrayColor), 883938493);
          }
          if (Widgets.ButtonInvisible(r))
          {
            if (Current.ProgramState == ProgramState.Playing && Find.WindowStack.WindowOfType<Dialog_InfoCard>() == null && Find.WindowStack.WindowOfType<Dialog_GrowthMomentChoices>() == null)
            {
              InspectPaneUtility.OpenTab(typeof(ITab_Genes));
            }
            else
            {
              Find.WindowStack.Add(new Dialog_ViewGenes(pawn));
            }
          }
        },
        width = num2
      });
    }

    [HarmonyPatch(typeof(Ideo), nameof(Ideo.IsPreferredXenotype))]
    [HarmonyPostfix]
    public static void IsGermlinePreferredXenotype(Ideo __instance, ref bool __result, Pawn pawn)
    {
      if (__result)
      {
        return;
      }
      
      if (!__instance.PreferredXenotypes.Any() && !__instance.PreferredCustomXenotypes.Any())
      {
        return;
      }
      GermlineComp germlineData = pawn.GetComp<GermlineComp>();
      if (germlineData == null || (germlineData.Germline == null && germlineData.germlineName == null))
      {
        return;
      }
      if (germlineData.CustomXenotype != null)
      {
        __result = __instance.PreferredCustomXenotypes.Contains(germlineData.CustomXenotype);
      }
      __result = __result || __instance.PreferredXenotypes.Contains(germlineData.Germline);
    }
  }
}
