// Mirrors Python class RebarSetDef
namespace ToolsByGimhan.RebarGenerator.Models
{
    public sealed class RebarSetDef
    {
        public double YLocal      { get; }  // Local Z (height) position in ft
        public int    Qty         { get; }  // Number of bars in this set
        public string TypeName    { get; }  // RebarBarType name
        public double BarDia      { get; }  // Bar diameter in ft
        public double ArrayLen    { get; }  // Lateral span between outermost bar centres (ft)
        public double StartXLocal { get; }  // Local Y of first bar (ft)
        public string LayerTag    { get; }  // "T1", "T2", "B1", "B2"

        public RebarSetDef(
            double yLocal, int qty, string typeName, double barDia,
            double arrayLen = 0.0, double startXLocal = 0.0, string layerTag = "")
        {
            YLocal = yLocal; Qty = qty; TypeName = typeName ?? string.Empty;
            BarDia = barDia; ArrayLen = arrayLen; StartXLocal = startXLocal;
            LayerTag = layerTag ?? string.Empty;
        }
    }
}
