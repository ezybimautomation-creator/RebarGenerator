// Mirrors Python class RebarPoint
namespace ToolsByGimhan.RebarGenerator.Models
{
    public sealed class RebarPoint
    {
        public double Lx        { get; }   // Local X offset (ft) from beam/centre
        public double Ly        { get; }   // Local Y offset (ft)
        public double DiameterFt{ get; }   // Bar diameter in feet
        public string TypeName  { get; }   // RebarBarType name

        public RebarPoint(double lx, double ly, double diameterFt, string typeName)
        {
            Lx = lx; Ly = ly; DiameterFt = diameterFt;
            TypeName = typeName ?? string.Empty;
        }
    }
}
