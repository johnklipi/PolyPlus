using Polytopia.Data;

namespace PolyPlus.Data
{
    public class AdjacencyImprovementsPlus
    {
        public TileData.EffectType effect { get; set; } = TileData.EffectType.None;
        public TerrainData.Type terrain { get; set; } = TerrainData.Type.None;
    }
}