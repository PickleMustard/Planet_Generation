public class GenericPercent : IPercent
{
    public int PercentTotal {get; set;}
    public int PercentCurrent { get; set; }
    public void Reset() { PercentCurrent = 0; }
}
