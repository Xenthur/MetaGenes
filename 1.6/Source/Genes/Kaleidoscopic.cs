using Verse;

namespace XenotypePlusPlus
{
  public class KaleidoscopicGene : RandomGeneGene
  {
    public override void TickInterval(int delta)
    {
      base.TickInterval(delta);
      KaleidoscopicExtension kaleiExtension = def.GetModExtension<KaleidoscopicExtension>() ?? new();
      extension = kaleiExtension;
      if (Rand.MTBEventOccurs(kaleiExtension.mtbDays, 60000f, delta))
      {
        if (Rand.Bool || !RemoveRandomGenes(true))
        {
          AddRandomGenes(true);
        }
      }
    }
  }

  public class KaleidoscopicExtension : RandomGeneExtension
  {
    public float mtbDays = 5;
  }
}
