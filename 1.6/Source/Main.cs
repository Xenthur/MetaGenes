using Verse;
using HarmonyLib;

namespace XenotypePlusPlus
{
  [StaticConstructorOnStartup]
  internal class XenotypePlusPlus : Mod
  {
    public XenotypePlusPlus(ModContentPack content) : base(content)
    {
      Harmony harmony = new("Xenthur.XenotypePlusPlus");
      harmony.PatchAll();
    }
  }
}
