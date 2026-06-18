using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AICopyrightReproducibility.Config;
using AICopyrightReproducibility.Utils;

namespace AICopyrightReproducibility
{
    public static class OutputWriter
    {
        private static string Csv(string? s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            if (s.Contains(',') || s.Contains('"') || s.Contains('\n'))
                return "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
        }

        private static IEnumerable<IGrouping<(string TextLabel, string QueryLabel, string Deployment), RunRecord>> SummaryGroups(List<RunRecord> records) =>
            records.GroupBy(r => (r.TextLabel, r.QueryLabel, r.Deployment))
                   .OrderBy(g => g.Key.TextLabel)
                   .ThenBy(g => g.Key.QueryLabel)
                   .ThenBy(g => g.Key.Deployment);

        private static (List<float> Lpm, List<float> LpmMed, List<float> Tlp) LogprobLists(List<RunRecord> rs) =>
            (rs.Where(r => r.ListLogprobMean.HasValue)  .Select(r => r.ListLogprobMean!.Value).ToList(),
             rs.Where(r => r.ListLogprobMedian.HasValue).Select(r => r.ListLogprobMedian!.Value).ToList(),
             rs.Where(r => r.TitleLogprob.HasValue)     .Select(r => r.TitleLogprob!.Value).ToList());

        public static void WriteRunConfig(RunSnapshot snapshot, string outDir)
        {
            JsonSerializerOptions writeOpts = new JsonSerializerOptions
            {
                WriteIndented        = true,
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                Converters           = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) }
            };
            File.WriteAllText(Path.Combine(outDir, "run-config.json"),
                JsonSerializer.Serialize(snapshot, writeOpts));
        }

        public static void WriteManifestCsv(List<RunRecord> records, string path)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("deployment,set,text_label,query_label,list_task,order_task,index,timestamp_utc,status,duration_ms,retry_count,model," +
                          "system_fingerprint,response_id,created,prompt_tokens,completion_tokens," +
                          "total_tokens,finish_reason,content_sha256,content_sha256_short,semantic_sha256,semantic_sha256_short,raw_file," +
                          "list_count,exact_matches,coverage,hallucinations,li1_first,position_score,min_moves,order_pct,title_hit,textbook_hit,list_logprob_mean,list_logprob_median,list_logprob_mode,title_logprob,error");
            foreach (RunRecord r in records)
            {
                sb.AppendLine(string.Join(",", new string[]
                {
                    Csv(r.Deployment),
                    r.Set.ToString(CultureInfo.InvariantCulture),
                    Csv(r.TextLabel),
                    Csv(r.QueryLabel),
                    r.ListTask  ? "1" : "0",
                    r.OrderTask ? "1" : "0",
                    r.Index.ToString(CultureInfo.InvariantCulture),
                    r.TimestampUtc.ToString("o", CultureInfo.InvariantCulture),
                    r.Status.ToString(CultureInfo.InvariantCulture),
                    r.DurationMs.ToString(CultureInfo.InvariantCulture),
                    r.RetryCount.ToString(CultureInfo.InvariantCulture),
                    Csv(r.Model), Csv(r.SystemFingerprint), Csv(r.ResponseId),
                    Csv(r.Created?.ToString()), Csv(r.PromptTokens?.ToString()),
                    Csv(r.CompletionTokens?.ToString()), Csv(r.TotalTokens?.ToString()),
                    Csv(r.FinishReason), Csv(r.ContentSha256), Csv(r.ContentSha256Short),
                    Csv(r.SemanticSha256), Csv(r.SemanticSha256Short),
                    Csv(r.RawFile),
                    r.Lists.Length.ToString(CultureInfo.InvariantCulture),
                    r.ExactMatches.ToString(CultureInfo.InvariantCulture),
                    r.Coverage.ToString(CultureInfo.InvariantCulture),
                    r.Hallucinations.ToString(CultureInfo.InvariantCulture),
                    r.Li1First ? "1" : "0",
                    r.PositionScore?.ToString(CultureInfo.InvariantCulture) ?? "",
                    r.MinMoves?.ToString(CultureInfo.InvariantCulture) ?? "",
                    r.OrderPct?.ToString("F1", CultureInfo.InvariantCulture) ?? "",
                    r.TitleHit    ? "1" : "0",
                    r.TextbookHit ? "1" : "0",
                    r.ListLogprobMean?.ToString("F4", CultureInfo.InvariantCulture) ?? "",
                    r.ListLogprobMedian?.ToString("F4", CultureInfo.InvariantCulture) ?? "",
                    r.ListLogprobMode?.ToString("F4", CultureInfo.InvariantCulture) ?? "",
                    r.TitleLogprob?.ToString("F4", CultureInfo.InvariantCulture) ?? "",
                    Csv(r.Error)
                }));
            }
            File.WriteAllText(path, sb.ToString());
        }

        public static void WriteSummaryCountsCsv(List<RunRecord> records, string path)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("text_label,query_label,deployment,n,n_ok,n_err," +
                          "exact_matches_mean,coverage_mean,hallucinations_mean," +
                          "li1_first_count,position_score_mean,min_moves_mean," +
                          "title_hit_count,textbook_hit_count," +
                          "list_logprob_mean,list_logprob_median,title_logprob_mean");

            foreach (IGrouping<(string TextLabel, string QueryLabel, string Deployment), RunRecord> g in SummaryGroups(records))
            {
                List<RunRecord> rs   = g.ToList();
                List<RunRecord> ok   = rs.Where(r => r.Status == 200).ToList();
                int             nOk  = ok.Count;
                int             nErr = rs.Count - nOk;
                bool isList  = rs[0].ListTask;
                bool isOrder = rs[0].OrderTask;

                string exact    = isList  && nOk > 0 ? ok.Average(r => r.ExactMatches).ToString("F1", CultureInfo.InvariantCulture)  : "";
                string cov      = isList  && nOk > 0 ? ok.Average(r => r.Coverage).ToString("F1", CultureInfo.InvariantCulture)       : "";
                string halluc   = isList  && nOk > 0 ? ok.Average(r => r.Hallucinations).ToString("F1", CultureInfo.InvariantCulture) : "";
                string li1Cnt   = isList  ? ok.Count(r => r.Li1First).ToString(CultureInfo.InvariantCulture)                          : "";
                string pos      = isOrder && ok.Any(r => r.PositionScore.HasValue)
                    ? ok.Where(r => r.PositionScore.HasValue).Average(r => r.PositionScore!.Value).ToString("F1", CultureInfo.InvariantCulture) : "";
                string minMoves = isOrder && ok.Any(r => r.MinMoves.HasValue)
                    ? ok.Where(r => r.MinMoves.HasValue).Average(r => r.MinMoves!.Value).ToString("F2", CultureInfo.InvariantCulture) : "";
                string titleCnt = !isList ? ok.Count(r => r.TitleHit).ToString(CultureInfo.InvariantCulture)    : "";
                string tbCnt    = !isList ? ok.Count(r => r.TextbookHit).ToString(CultureInfo.InvariantCulture) : "";

                var (lpmVals, lpmMedVals, tlpVals) = LogprobLists(ok);
                string lpm    = lpmVals.Count    > 0 ? lpmVals.Average().ToString("F4", CultureInfo.InvariantCulture)    : "";
                string lpmMed = lpmMedVals.Count > 0 ? lpmMedVals.Average().ToString("F4", CultureInfo.InvariantCulture) : "";
                string tlp    = tlpVals.Count    > 0 ? tlpVals.Average().ToString("F4", CultureInfo.InvariantCulture)    : "";

                sb.AppendLine(string.Join(",", new[]
                {
                    Csv(g.Key.TextLabel), Csv(g.Key.QueryLabel), Csv(g.Key.Deployment),
                    rs.Count.ToString(CultureInfo.InvariantCulture),
                    nOk.ToString(CultureInfo.InvariantCulture),
                    nErr.ToString(CultureInfo.InvariantCulture),
                    exact, cov, halluc, li1Cnt, pos, minMoves, titleCnt, tbCnt,
                    lpm, lpmMed, tlp
                }));
            }
            File.WriteAllText(path, sb.ToString());
        }

        public static void WriteIdentityGroups(List<RunRecord> records, Logger logger)
        {
            logger.Info(new string('-', 80));
            logger.Info("Distinct semantic hashes (identity groups):");
            IEnumerable<IGrouping<string?, RunRecord>> groups = records
                .Where(r => r.Status == 200 && r.SemanticSha256 is not null)
                .GroupBy(r => r.SemanticSha256)
                .OrderByDescending(g => g.Count());
            int gi = 0;
            foreach (IGrouping<string?, RunRecord> g in groups)
            {
                gi++;
                logger.Info(
                    $"  group {gi}: {g.Count()} run(s)  sem={g.Key![..12]}  " +
                    $"[{string.Join(", ", g.Select(r => $"{r.Deployment}/{r.TextLabel}/{r.QueryLabel}#{r.Set}.{r.Index}"))}]");
            }
        }

        public static void WriteConsoleSummary(List<RunRecord> records, Logger logger)
        {
            logger.Info("\nScoring summary (mean per arm × query):");
            logger.Info($"  {"text",-20} {"query",-26} {"deployment",-22} {"n",3}  {"ok",3}  {"err",3}  {"rep",3}  {"perf",4}  {"cov",4}  {"halluc",6}  {"li1",3}  {"ord",5}  {"mm",4}  {"title",5}  {"tbhit",5}  {"lpmean",7}  {"lpmed",7}  {"lpmod",7}  {"tlp",7}");
            logger.Info("  " + new string('-', 182));

            foreach (IGrouping<(string TextLabel, string QueryLabel, string Deployment), RunRecord> sg in SummaryGroups(records))
            {
                List<RunRecord> rs   = sg.ToList();
                List<RunRecord> ok   = rs.Where(r => r.Status == 200).ToList();
                int             nOk  = ok.Count;
                int             nErr = rs.Count - nOk;
                bool isListTask  = rs[0].ListTask;
                bool isOrderTask = rs[0].OrderTask;
                int  total       = rs[0].SectionCount;

                int repCount = ok.Where(r => r.SemanticSha256 is not null)
                                 .GroupBy(r => r.SemanticSha256)
                                 .OrderByDescending(g => g.Count())
                                 .FirstOrDefault()?.Count() ?? 0;
                string rep  = repCount.ToString(CultureInfo.InvariantCulture);

                int perfCount = ok.Count(r =>
                    (!isListTask  || (r.Coverage == r.SectionCount && r.Hallucinations == 0)) &&
                    (!isOrderTask || r.MinMoves == 0) &&
                    (isListTask   || r.TitleHit));
                string perf = perfCount.ToString(CultureInfo.InvariantCulture);

                string cov    = isListTask  && nOk > 0 ? ok.Average(r => r.Coverage).ToString("F1", CultureInfo.InvariantCulture)      : "-";
                string halluc = isListTask  && nOk > 0 ? ok.Average(r => r.Hallucinations).ToString("F1", CultureInfo.InvariantCulture) : "-";
                string li1    = isListTask  ? ok.Count(r => r.Li1First).ToString(CultureInfo.InvariantCulture)                          : "-";
                string ord    = isOrderTask && ok.Any(r => r.OrderPct.HasValue) && total > 0
                    ? (ok.Where(r => r.OrderPct.HasValue).Average(r => r.OrderPct!.Value) / 100f * total).ToString("F1", CultureInfo.InvariantCulture) : "-";
                string mm     = isOrderTask && ok.Any(r => r.MinMoves.HasValue)
                    ? ok.Where(r => r.MinMoves.HasValue).Average(r => r.MinMoves!.Value).ToString("F2", CultureInfo.InvariantCulture) : "-";
                string title  = !isListTask ? ok.Count(r => r.TitleHit).ToString(CultureInfo.InvariantCulture)    : "-";
                string tbhit  = !isListTask ? ok.Count(r => r.TextbookHit).ToString(CultureInfo.InvariantCulture) : "-";

                var (lpmVals, _, tlpVals) = LogprobLists(ok);
                string lpmean, lpmed, lpmod;
                if (lpmVals.Count > 0)
                {
                    var (sMean, sMed, sMod) = ScoringUtils.ComputeStats(lpmVals);
                    lpmean = sMean.ToString("F3", CultureInfo.InvariantCulture);
                    lpmed  = sMed.ToString("F3", CultureInfo.InvariantCulture);
                    lpmod  = sMod?.ToString("F3", CultureInfo.InvariantCulture) ?? "-";
                }
                else { lpmean = lpmed = lpmod = "-"; }
                string tlp = tlpVals.Count > 0 ? tlpVals.Average().ToString("F3", CultureInfo.InvariantCulture) : "-";

                logger.Info($"  {sg.Key.TextLabel,-20} {sg.Key.QueryLabel,-26} {sg.Key.Deployment,-22} {rs.Count,3}  {nOk,3}  {nErr,3}  {rep,3}  {perf,4}  {cov,4}  {halluc,6}  {li1,3}  {ord,5}  {mm,4}  {title,5}  {tbhit,5}  {lpmean,7}  {lpmed,7}  {lpmod,7}  {tlp,7}");
            }
        }

        public static void WriteSummaryPctCsv(List<RunRecord> records, string path)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("text_label,query_label,deployment,n,n_ok,n_err,n_ok%,n_err%,perf%," +
                          "coverage_pct,li1_first_pct,order_pct_mean,title_hit_pct,textbook_hit_pct," +
                          "list_logprob_geomean_pct,list_logprob_arith_pct," +
                          "list_logprob_median_geomean_pct,list_logprob_median_arith_pct," +
                          "title_logprob_geomean_pct,title_logprob_arith_pct");

            foreach (IGrouping<(string TextLabel, string QueryLabel, string Deployment), RunRecord> g in SummaryGroups(records))
            {
                List<RunRecord> rs   = g.ToList();
                List<RunRecord> ok   = rs.Where(r => r.Status == 200).ToList();
                int             nOk  = ok.Count;
                int             nErr = rs.Count - nOk;
                System.Diagnostics.Debug.Assert(rs.All(r => r.SectionCount == rs[0].SectionCount),
                    "All records in a summary group must share the same SectionCount.");
                bool isList  = rs[0].ListTask;
                bool isOrder = rs[0].OrderTask;

                int    totalSections = rs[0].SectionCount;
                string covPct   = isList && totalSections > 0 && nOk > 0
                    ? (ok.Average(r => r.Coverage) / totalSections * 100).ToString("F1", CultureInfo.InvariantCulture) : "";
                string li1Pct   = isList && nOk > 0
                    ? (ok.Count(r => r.Li1First) * 100.0 / nOk).ToString("F1", CultureInfo.InvariantCulture) : "";
                string orderPct = isOrder && ok.Any(r => r.OrderPct.HasValue)
                    ? ok.Where(r => r.OrderPct.HasValue).Average(r => r.OrderPct!.Value).ToString("F1", CultureInfo.InvariantCulture) : "";
                string titlePct = !isList && nOk > 0
                    ? (ok.Count(r => r.TitleHit)    * 100.0 / nOk).ToString("F1", CultureInfo.InvariantCulture) : "";
                string tbPct    = !isList && nOk > 0
                    ? (ok.Count(r => r.TextbookHit) * 100.0 / nOk).ToString("F1", CultureInfo.InvariantCulture) : "";

                var (lpmVals, lpmMedVals, tlpVals) = LogprobLists(ok);

                string lpmGeo      = lpmVals.Count    > 0 ? (MathF.Exp(lpmVals.Average()) * 100f).ToString("F1", CultureInfo.InvariantCulture)             : "";
                string lpmArith    = lpmVals.Count    > 0 ? (lpmVals.Average(v => MathF.Exp(v)) * 100f).ToString("F1", CultureInfo.InvariantCulture)        : "";
                string lpmMedGeo   = lpmMedVals.Count > 0 ? (MathF.Exp(lpmMedVals.Average()) * 100f).ToString("F1", CultureInfo.InvariantCulture)           : "";
                string lpmMedArith = lpmMedVals.Count > 0 ? (lpmMedVals.Average(v => MathF.Exp(v)) * 100f).ToString("F1", CultureInfo.InvariantCulture)     : "";
                string tlpGeo      = tlpVals.Count    > 0 ? (MathF.Exp(tlpVals.Average()) * 100f).ToString("F1", CultureInfo.InvariantCulture)              : "";
                string tlpArith    = tlpVals.Count    > 0 ? (tlpVals.Average(v => MathF.Exp(v)) * 100f).ToString("F1", CultureInfo.InvariantCulture)        : "";

                string nOkPct  = rs.Count > 0 ? (nOk  * 100.0 / rs.Count).ToString("F1", CultureInfo.InvariantCulture) : "";
                string nErrPct = rs.Count > 0 ? (nErr * 100.0 / rs.Count).ToString("F1", CultureInfo.InvariantCulture) : "";
                int    perfCount = ok.Count(r =>
                    (!isList  || (r.Coverage == r.SectionCount && r.Hallucinations == 0)) &&
                    (!isOrder || r.MinMoves == 0) &&
                    (isList   || r.TitleHit));
                string perfPct = nOk > 0 ? (perfCount * 100.0 / nOk).ToString("F1", CultureInfo.InvariantCulture) : "";
                sb.AppendLine(string.Join(",", new[]
                {
                    Csv(g.Key.TextLabel), Csv(g.Key.QueryLabel), Csv(g.Key.Deployment),
                    rs.Count.ToString(CultureInfo.InvariantCulture),
                    nOk.ToString(CultureInfo.InvariantCulture),
                    nErr.ToString(CultureInfo.InvariantCulture),
                    nOkPct, nErrPct, perfPct,
                    covPct, li1Pct, orderPct, titlePct, tbPct,
                    lpmGeo, lpmArith, lpmMedGeo, lpmMedArith,
                    tlpGeo, tlpArith
                }));
            }
            File.WriteAllText(path, sb.ToString());
        }

        public static void WriteConsolePctSummary(List<RunRecord> records, Logger logger)
        {
            // Pre-pass: find max mean_mm per (text, query) for relative mm% normalisation
            var maxMmByQuery = new Dictionary<(string, string), float>();
            foreach (IGrouping<(string TextLabel, string QueryLabel, string Deployment), RunRecord> g in SummaryGroups(records))
            {
                List<RunRecord> rs0 = g.Where(r => r.Status == 200).ToList();
                if (!g.First().OrderTask || !rs0.Any(r => r.MinMoves.HasValue)) continue;
                float meanMm = rs0.Where(r => r.MinMoves.HasValue).Average(r => (float)r.MinMoves!.Value);
                var qk = (g.Key.TextLabel, g.Key.QueryLabel);
                if (!maxMmByQuery.TryGetValue(qk, out float cur) || meanMm > cur)
                    maxMmByQuery[qk] = meanMm;
            }

            logger.Info("\nScoring summary — percentages (mean per arm × query):");
            logger.Info($"  {"text",-20} {"query",-26} {"deployment",-22} {"n",3}  {"ok%",6}  {"err%",6}  {"rep%",6}  {"perf%",6}  {"cov%",6}  {"halluc%",7}  {"li1%",6}  {"ord%",6}  {"mm%",6}  {"title%",7}  {"tbhit%",7}  {"lp_geo",7}  {"lp_ari",7}  {"lpm_geo",7}  {"lpm_ari",7}  {"tlp_geo",7}  {"tlp_ari",7}");
            logger.Info("  " + new string('-', 224));

            foreach (IGrouping<(string TextLabel, string QueryLabel, string Deployment), RunRecord> sg in SummaryGroups(records))
            {
                List<RunRecord> rs   = sg.ToList();
                List<RunRecord> ok   = rs.Where(r => r.Status == 200).ToList();
                int             nOk  = ok.Count;
                int             nErr = rs.Count - nOk;
                bool isList  = rs[0].ListTask;
                bool isOrder = rs[0].OrderTask;
                int  total   = rs[0].SectionCount;

                int repCount2 = ok.Where(r => r.SemanticSha256 is not null)
                                  .GroupBy(r => r.SemanticSha256)
                                  .OrderByDescending(g => g.Count())
                                  .FirstOrDefault()?.Count() ?? 0;
                string repPct  = nOk > 0 ? (repCount2  * 100.0 / nOk).ToString("F1", CultureInfo.InvariantCulture) + "%" : "-";

                int perfCount2 = ok.Count(r =>
                    (!isList  || (r.Coverage == r.SectionCount && r.Hallucinations == 0)) &&
                    (!isOrder || r.MinMoves == 0) &&
                    (isList   || r.TitleHit));
                string perfPct = nOk > 0 ? (perfCount2 * 100.0 / nOk).ToString("F1", CultureInfo.InvariantCulture) + "%" : "-";

                string covPct    = isList && total > 0 && nOk > 0
                    ? (ok.Average(r => r.Coverage) / total * 100).ToString("F1", CultureInfo.InvariantCulture) + "%" : "-";
                string hallucPct = isList && total > 0 && nOk > 0
                    ? (ok.Average(r => r.Hallucinations) / total * 100).ToString("F1", CultureInfo.InvariantCulture) + "%" : "-";
                string li1Pct    = isList && nOk > 0
                    ? (ok.Count(r => r.Li1First) * 100.0 / nOk).ToString("F1", CultureInfo.InvariantCulture) + "%" : "-";
                string ordPct    = isOrder && ok.Any(r => r.OrderPct.HasValue)
                    ? ok.Where(r => r.OrderPct.HasValue).Average(r => r.OrderPct!.Value).ToString("F1", CultureInfo.InvariantCulture) + "%" : "-";

                string mmPct = "-";
                if (isOrder && ok.Any(r => r.MinMoves.HasValue))
                {
                    float meanMm = ok.Where(r => r.MinMoves.HasValue).Average(r => (float)r.MinMoves!.Value);
                    var qk = (sg.Key.TextLabel, sg.Key.QueryLabel);
                    if (maxMmByQuery.TryGetValue(qk, out float maxMm) && maxMm > 0f)
                        mmPct = (meanMm / maxMm * 100f).ToString("F1", CultureInfo.InvariantCulture) + "%";
                    else
                        mmPct = "0.0%";
                }

                string titlePct  = !isList && nOk > 0
                    ? (ok.Count(r => r.TitleHit)    * 100.0 / nOk).ToString("F1", CultureInfo.InvariantCulture) + "%" : "-";
                string tbhitPct  = !isList && nOk > 0
                    ? (ok.Count(r => r.TextbookHit) * 100.0 / nOk).ToString("F1", CultureInfo.InvariantCulture) + "%" : "-";

                var (lpmVals, lpmMedVals, tlpVals) = LogprobLists(ok);
                string lpGeo    = lpmVals.Count    > 0 ? (MathF.Exp(lpmVals.Average()) * 100f).ToString("F1", CultureInfo.InvariantCulture)            + "%" : "-";
                string lpAri    = lpmVals.Count    > 0 ? (lpmVals.Average(v => MathF.Exp(v)) * 100f).ToString("F1", CultureInfo.InvariantCulture)       + "%" : "-";
                string lpmGeo   = lpmMedVals.Count > 0 ? (MathF.Exp(lpmMedVals.Average()) * 100f).ToString("F1", CultureInfo.InvariantCulture)          + "%" : "-";
                string lpmAri   = lpmMedVals.Count > 0 ? (lpmMedVals.Average(v => MathF.Exp(v)) * 100f).ToString("F1", CultureInfo.InvariantCulture)    + "%" : "-";
                string tlpGeo   = tlpVals.Count    > 0 ? (MathF.Exp(tlpVals.Average()) * 100f).ToString("F1", CultureInfo.InvariantCulture)             + "%" : "-";
                string tlpAri   = tlpVals.Count    > 0 ? (tlpVals.Average(v => MathF.Exp(v)) * 100f).ToString("F1", CultureInfo.InvariantCulture)       + "%" : "-";

                string okPct2  = rs.Count > 0 ? (nOk  * 100.0 / rs.Count).ToString("F1", CultureInfo.InvariantCulture) + "%" : "-";
                string errPct2 = rs.Count > 0 ? (nErr * 100.0 / rs.Count).ToString("F1", CultureInfo.InvariantCulture) + "%" : "-";
                logger.Info($"  {sg.Key.TextLabel,-20} {sg.Key.QueryLabel,-26} {sg.Key.Deployment,-22} {rs.Count,3}  {okPct2,6}  {errPct2,6}  {repPct,6}  {perfPct,6}  {covPct,6}  {hallucPct,7}  {li1Pct,6}  {ordPct,6}  {mmPct,6}  {titlePct,7}  {tbhitPct,7}  {lpGeo,7}  {lpAri,7}  {lpmGeo,7}  {lpmAri,7}  {tlpGeo,7}  {tlpAri,7}");
            }
        }
    }
}
