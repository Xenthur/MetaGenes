namespace XenotypePlusPlus
{
  public class GrabBagGene : RandomGeneGene
  {
    public override void Tick()
    {
      extension = def.GetModExtension<RandomGeneExtension>() ?? new();
      AddRandomGenes();
      this.RemoveAllOfThisGene();
    }
  }
}
