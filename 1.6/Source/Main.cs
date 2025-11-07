using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using HarmonyLib;

namespace MetaGenes
{
  [StaticConstructorOnStartup]
  public static class MetaGenes
  {
    private static Harmony harmony = new Harmony("Xenthur.MetaGenes");
    static MetaGenes()
    {
      harmony.PatchAll();
    }
  }
}
