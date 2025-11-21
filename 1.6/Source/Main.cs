using Verse;
using HarmonyLib;

namespace MetaGenes
{
  [StaticConstructorOnStartup]
  internal class MetaGenes : Mod
  {
    public MetaGenes(ModContentPack content) : base(content)
    {
      Harmony harmony = new("Xenthur.MetaGenes");
      harmony.PatchAll();
    }
  }
}
