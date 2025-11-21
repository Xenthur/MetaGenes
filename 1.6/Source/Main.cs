using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using HarmonyLib;

namespace MetaGenes
{
  [StaticConstructorOnStartup]
  //public static class MetaGenes
  internal class MetaGenes : Mod
  {
    //private static readonly Harmony harmony = new("Xenthur.MetaGenes");

    public MetaGenes(ModContentPack content) : base(content)
    {
      Harmony harmony = new("Xenthur.MetaGenes");
      harmony.PatchAll();
    }
    //static MetaGenes()
    //{
    //  harmony.PatchAll();
    //}
  }
}
