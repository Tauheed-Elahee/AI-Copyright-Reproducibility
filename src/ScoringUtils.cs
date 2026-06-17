using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace AICopyrightReproducibility
{
    internal static class ScoringUtils
    {
        public static void ScoreRecord(RunRecord rec, BoundPrompt bound) =>
            ScoreRecord(rec, bound.Sections, bound.Aliases, bound.TitleShort, bound.TitleExtras, bound.ListTask, bound.OrderTask);

        private static void ScoreRecord(
            RunRecord rec,
            string[] sections,
            Dictionary<string, string> aliases,
            string titleShort,
            Dictionary<string, string> titleExtras,
            bool listTask,
            bool orderTask)
        {
            if (listTask && rec.Lists.Length > 0)
            {
                string[] normSections = sections.Select(Norm).ToArray();
                HashSet<string> sectionSet = new HashSet<string>(normSections, StringComparer.Ordinal);
                HashSet<string> covered    = new HashSet<string>(StringComparer.Ordinal);
                int exact = 0, posScore = 0;

                for (int i = 0; i < rec.Lists.Length; i++)
                {
                    string nr = ResolveAlias(Norm(rec.Lists[i]), aliases);
                    if (sectionSet.Contains(nr))
                    {
                        exact++;
                        covered.Add(nr);
                        if (orderTask && i < normSections.Length && nr == normSections[i])
                            posScore++;
                    }
                }

                rec.ExactMatches   = exact;
                rec.Coverage       = covered.Count;
                rec.Hallucinations = rec.Lists.Length - exact;
                rec.Li1First       = rec.Lists.Length > 0
                    && ResolveAlias(Norm(rec.Lists[0]), aliases) == Norm(sections[0]);
                if (orderTask)
                {
                    rec.PositionScore = posScore;
                    Dictionary<string, int> normToPos = normSections
                        .Select((s, i) => (s, i))
                        .ToDictionary(t => t.s, t => t.i, StringComparer.Ordinal);
                    int[] posSeq = rec.Lists
                        .Select(item => ResolveAlias(Norm(item), aliases))
                        .Where(nr => normToPos.ContainsKey(nr))
                        .Select(nr => normToPos[nr])
                        .ToArray();
                    if (posSeq.Length > 0)
                    {
                        int moves = posSeq.Length - LisLength(posSeq);
                        rec.MinMoves = moves;
                        rec.OrderPct = (1f - moves / (float)sections.Length) * 100f;
                    }
                }
            }

            if (!string.IsNullOrEmpty(rec.ContentText) && !string.IsNullOrEmpty(titleShort))
                rec.TitleHit = rec.ContentText.Contains(titleShort, StringComparison.OrdinalIgnoreCase);

            if (!string.IsNullOrEmpty(rec.ContentText) &&
                titleExtras.TryGetValue("textbook.short", out string? tbShort) &&
                !string.IsNullOrEmpty(tbShort))
                rec.TextbookHit = rec.ContentText.Contains(tbShort, StringComparison.OrdinalIgnoreCase);

            if (rec.Status == 200)
            {
                string canonical = string.Join("\n",
                    new[] { rec.TitleHit ? "1" : "0", rec.TextbookHit ? "1" : "0" }.Concat(rec.Lists));
                string sh = HarnessUtils.Sha256Hex(canonical);
                rec.SemanticSha256      = sh;
                rec.SemanticSha256Short = sh[..12];
            }
        }

        private static string Norm(string s) => s.Trim().ToLowerInvariant();

        private static string ResolveAlias(string normed, Dictionary<string, string> aliases) =>
            aliases.TryGetValue(normed, out string? a) ? Norm(a) : normed;

        private static int LisLength(int[] seq)
        {
            int[] tails = new int[seq.Length];
            int len = 0;
            foreach (int x in seq)
            {
                int lo = 0, hi = len;
                while (lo < hi) { int mid = (lo + hi) / 2; if (tails[mid] < x) lo = mid + 1; else hi = mid; }
                tails[lo] = x;
                if (lo == len) len++;
            }
            return len;
        }

        public static void ExtractLogprobs(string rawJson, RunRecord rec, string titleShort)
        {
            if (string.IsNullOrEmpty(rawJson)) return;
            using JsonDocument doc = JsonDocument.Parse(rawJson);
            JsonElement root = doc.RootElement;
            if (!root.TryGetProperty("choices", out JsonElement ch) || ch.GetArrayLength() == 0) return;
            JsonElement c0 = ch[0];
            if (!c0.TryGetProperty("logprobs", out JsonElement lp) ||
                !lp.TryGetProperty("content",  out JsonElement content) ||
                content.ValueKind != JsonValueKind.Array) return;

            var tokens = new List<(int start, float logprob)>();
            int pos = 0;
            StringBuilder sb = new StringBuilder();
            foreach (JsonElement el in content.EnumerateArray())
            {
                string tok = el.GetProperty("token").GetString() ?? "";
                float logprob = (float)el.GetProperty("logprob").GetDouble();
                tokens.Add((pos, logprob));
                sb.Append(tok);
                pos += tok.Length;
            }
            string text = sb.ToString();

            float? LogprobAt(int p)
            {
                for (int i = tokens.Count - 1; i >= 0; i--)
                    if (tokens[i].start <= p) return tokens[i].logprob;
                return null;
            }

            var itemLogprobs = new List<float?>();
            for (int i = 0; i < text.Length - 1; i++)
            {
                if ((i == 0 || text[i - 1] == '\n') && text[i] == '-' && text[i + 1] == ' ')
                    itemLogprobs.Add(LogprobAt(i + 2));
            }

            if (itemLogprobs.Count > 0)
            {
                rec.ListItemLogprobs = itemLogprobs.ToArray();
                List<float> valid = itemLogprobs.Where(x => x.HasValue).Select(x => x!.Value).ToList();
                if (valid.Count > 0)
                    (rec.ListLogprobMean, rec.ListLogprobMedian, rec.ListLogprobMode) = ComputeStats(valid);
            }

            if (!string.IsNullOrEmpty(titleShort))
            {
                int tp = text.IndexOf(titleShort, StringComparison.OrdinalIgnoreCase);
                if (tp >= 0) rec.TitleLogprob = LogprobAt(tp);
            }
        }

        public static (float Mean, float Median, float? Mode) ComputeStats(List<float> vals)
        {
            float mean = vals.Average();
            List<float> sorted = vals.OrderBy(x => x).ToList();
            int mid = sorted.Count / 2;
            float median = sorted.Count % 2 == 0
                ? (sorted[mid - 1] + sorted[mid]) / 2f
                : sorted[mid];
            var modeGroups = vals.GroupBy(x => MathF.Round(x, 1))
                .OrderByDescending(g => g.Count())
                .ThenByDescending(g => g.Key)
                .ToList();
            float? mode = modeGroups[0].Count() > 1 ? (float?)modeGroups[0].Key : null;
            return (mean, median, mode);
        }
    }
}
