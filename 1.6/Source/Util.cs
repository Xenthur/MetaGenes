using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace MetaGenes
{
  public static class Util
  {
    public static void RemoveAllOfThisGene(this Gene gene)
    {
      gene.pawn.genes.Xenogenes.RemoveAll((Gene g) => g.def == gene.def);
      gene.pawn.genes.Endogenes.RemoveAll((Gene g) => g.def == gene.def);
    }
  }

  public class GeneTemplate : Def
  {
    public GeneCategoryDef displayCategory;
    public float selectionWeight = 1f;
    public bool canGenerateInGeneSet = true;
    public string keyTag = "";
    public List<string> customEffectDescriptions = [];
  }
}
