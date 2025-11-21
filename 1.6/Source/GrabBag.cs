using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace MetaGenes
{
  public class GrabBagGene : Gene
  {
    private GrabBagExtension extension;

    public override void Tick()
    {
      extension = def.GetModExtension<GrabBagExtension>() ?? new GrabBagExtension();
      AddRandomGenes();
      this.RemoveAllOfThisGene();
    }

    private void AddRandomGenes()
    {
      int randomGeneCount = extension.geneCount.RandomInRange;

      for (int i = 0; i < randomGeneCount; i++)
      {
        int totalMet = pawn.genes.GenesListForReading.Where((Gene g) => g.Active).Aggregate(0, (sum, g) => sum + g.def.biostatMet);
        List<GeneDef> validGenes = [.. DefDatabase<GeneDef>.AllDefsListForReading.Where((g) => GrabBagFilter(g, totalMet))];
        if (validGenes.Empty())
        {
          break;
        }
        pawn.genes.AddGene(validGenes.RandomElement(), extension.isXeno);
      }
    }

    private bool GrabBagFilter(GeneDef g, int totalMet)
    {
      return
        g.biostatArc <= 0 &&
        g.biostatMet >= GeneTuning.BiostatRange.TrueMin - totalMet &&
        g.biostatMet <= GeneTuning.BiostatRange.TrueMax - totalMet &&
        g.passOnDirectly &&
        g.canGenerateInGeneSet &&
        !extension.excludedGenes.Contains(g) &&
        (extension.isXeno ? !pawn.genes.HasXenogene(g) : !pawn.genes.HasEndogene(g)) &&
        (g.prerequisite == null || pawn.genes.HasEndogene(g.prerequisite) || (extension.isXeno && pawn.genes.HasXenogene(g.prerequisite)));
    }
  }

  public class GrabBagExtension : DefModExtension
  {
    public bool isXeno = false;
    public IntRange geneCount = new(1, 10);
    public List<GeneDef> excludedGenes = [];
  }
}
