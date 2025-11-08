using Polytopia.Data;

namespace PolyPlus.Data
{
    public class TerrainRequirementsPlus
    {
        public TileData.EffectType effect { get; set; } = TileData.EffectType.None;
        public ImprovementData.Type improvement { get; set; } = ImprovementData.Type.None;
    }
}