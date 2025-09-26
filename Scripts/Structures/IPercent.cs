public interface IPercent
{
    public int PercentTotal { get; set; }
    public int PercentCurrent { get; set; }
    public float Percent { get => (float)PercentCurrent / (float)PercentTotal; }
}
