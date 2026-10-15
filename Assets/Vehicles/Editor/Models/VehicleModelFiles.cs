using System.Collections.Generic;

namespace TycoWorld.Vehicles.Authoring
{
    /// <summary>Where each downloaded vehicle model lives (spec vehicles-3a §1–§2): id → author folder → .glb path.</summary>
    public static class VehicleModelFiles
    {
        public const string Folder = "Assets/Vehicles/Art/ThirdParty";

        static readonly (string id, string author)[] Table =
        {
            ("vario", "Han66st"), ("supra", "iwak.rebus"), ("sedan", "Han66st"), ("hatch", "Han66st"), ("lmpv1", "Han66st"),
            ("lmpv2", "Han66st"), ("mpv2", "Han66st"), ("van", "Han66st"), ("l300", "Han66st"), ("pickup", "Han66st"),
            ("police", "Han66st"), ("angkot", "ardaradestareza"), ("carry", "alwayshasbean"), ("elf", "ann_fenek")
        };

        static readonly List<string> ids = new List<string>();

        public static IReadOnlyList<string> Ids
        {
            get
            {
                if (ids.Count == 0) foreach (var row in Table) ids.Add(row.id);
                return ids;
            }
        }

        public static string Author(string id)
        {
            foreach (var row in Table) if (row.id == id) return row.author;
            return null;
        }

        public static string Path(string id) => Folder + "/" + Author(id) + "/" + id + ".glb";
    }
}
