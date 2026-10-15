using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;

namespace TycoWorld.Vehicles.Authoring
{
    /// <summary>
    /// Gathers every SOURCES.md table in Assets into the in-game credits text (spec vehicles-3a §2), sorted by path. Tables are
    /// read by their header, since each subsystem lists its sources its own way; sounds the project made itself and candidates
    /// that were turned down are left out.
    /// </summary>
    public static class CreditsBuilder
    {
        public const string OutputPath = "Assets/GameSettings/Resources/Credits.txt";
        /// <summary>Marks a row the project synthesised itself (not third-party).</summary>
        public const string OwnWork = "buatan proyek ini";
        /// <summary>Starts the note of a candidate that was looked at and turned down (not in the game).</summary>
        public const string Rejected = "Ditolak";

        static readonly string[] SkippedColumns = { "File", "Perubahan", "Dipakai untuk" };
        static readonly Regex Link = new Regex(@"\[([^\]]+)\]\(([^)]+)\)");

        [MenuItem("Tyco World/Write Credits")]
        public static void Write()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
            File.WriteAllText(OutputPath, Collect());
            AssetDatabase.ImportAsset(OutputPath);
        }

        public static string Collect()
        {
            var sb = new StringBuilder();
            sb.Append("Tyco World memakai aset pihak ketiga berikut. Terima kasih kepada para pembuatnya.\n\n");
            var files = Directory.GetFiles("Assets", "SOURCES.md", SearchOption.AllDirectories)
                .Select(f => f.Replace('\\', '/')).OrderBy(f => f, StringComparer.Ordinal);
            foreach (var file in files)
            {
                var lines = File.ReadAllLines(file);
                string title = lines.Where(l => l.StartsWith("# ")).Select(l => Plain(l.Substring(2).Trim())).FirstOrDefault() ?? file;
                // Files that state their licence in prose (Kenney sounds, ambientCG textures) are all CC0.
                string fileLicence = string.Join("\n", lines).Contains("CC0") ? "CC0 1.0" : "";
                var entries = new List<string>();
                foreach (var (header, cells) in Tables(lines))
                {
                    if (cells.Any(c => c.Contains(OwnWork) || c.StartsWith(Rejected))) continue;
                    var parts = new List<string>();
                    for (int i = 0; i < cells.Length; i++)
                        if (i >= header.Length || Array.IndexOf(SkippedColumns, header[i]) < 0) parts.Add(Plain(cells[i]));
                    if (Array.IndexOf(header, "Lisensi") < 0 && fileLicence.Length > 0) parts.Add(fileLicence);
                    // A pack listed once per file it supplies (the player's sounds) is credited once.
                    string entry = string.Join(" — ", parts);
                    if (!entries.Contains(entry)) entries.Add(entry);
                }
                if (entries.Count == 0) continue;
                sb.Append(title).Append('\n');
                foreach (var e in entries) sb.Append("  • ").Append(e).Append('\n');
                sb.Append('\n');
            }
            return sb.ToString();
        }

        /// <summary>The data rows of every markdown table in <paramref name="lines"/>, as trimmed cells.</summary>
        public static IEnumerable<string[]> Rows(IReadOnlyList<string> lines) => Tables(lines).Select(t => t.cells);

        static IEnumerable<(string[] header, string[] cells)> Tables(IReadOnlyList<string> lines)
        {
            for (int i = 0; i + 1 < lines.Count; i++)
            {
                if (!lines[i].StartsWith("|") || !lines[i + 1].StartsWith("|---")) continue;
                var header = Cells(lines[i]);
                int j = i + 2;
                for (; j < lines.Count && lines[j].StartsWith("|"); j++) yield return (header, Cells(lines[j]));
                i = j - 1;
            }
        }

        static string[] Cells(string row) => row.Split('|').Select(c => c.Trim()).Where(c => c.Length > 0).ToArray();

        /// <summary>Markdown written out for a plain label: [text](url) → "text (url)", no backticks or bold marks.</summary>
        public static string Plain(string cell) => Link.Replace(cell, "$1 ($2)").Replace("`", "").Replace("**", "");
    }
}
