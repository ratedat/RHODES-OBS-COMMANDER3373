using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using RhodesSuki.Models;

namespace RhodesSuki.Services;

public sealed record RhodesSuiCoinOcrMatch(
    string CoinId,
    string Label,
    string RawText,
    double Confidence,
    MaaRoi OcrBox);

public static class RhodesMaaLocalCandidateConverter
{
    private static readonly IReadOnlyDictionary<string, string> SquadOcrNameAliases =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["医数成金狂隊"] = "破棘成金分隊",
            ["破悪成金分隊"] = "破棘成金分隊",
            ["c破悪成金分隊"] = "破棘成金分隊",
            ["調棘成金分秒"] = "破棘成金分隊",
            ["破棘練成金分時"] = "破棘成金分隊",
            ["調棘成金分ピ"] = "破棘成金分隊",
        };

    private static readonly IReadOnlyDictionary<string, string> CoinOcrSuffixAliases =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["早熱"] = "旱熱",
            ["弱氷"] = "霧氷",
            ["西の睡真"] = "西の廉貞",
            ["西の寵真"] = "西の廉貞",
            ["西の麻真"] = "西の廉貞",
        };

    private static readonly Lazy<IReadOnlyDictionary<string, IReadOnlyList<MaaCandidatePreview>>> StaticCandidatePreviewsByEntry =
        new(LoadRecognitionStaticCandidates);

    private static readonly IReadOnlyDictionary<string, (string Field, string Label, int Min, int Max, double Confidence)> RunStatusFields =
        new Dictionary<string, (string Field, string Label, int Min, int Max, double Confidence)>(StringComparer.Ordinal)
        {
            ["run.ingot"] = ("ingot", "源石錐", 0, 9999, 0.84),
            ["run.idea"] = ("idea", "構想", 0, 999, 0.70),
            ["run.idea.current"] = ("idea", "構想", 0, 999, 0.82),
            ["run.difficulty_grade"] = ("difficulty", "等級", 0, 99, 0.78),
            ["is6.ingot_value"] = ("ingot", "源石錐", 0, 9999, 0.84),
            ["is6.ticket_value"] = ("ticket", "遊覧券", 0, 999, 0.84),
        };

    public static IReadOnlyList<MaaCandidatePreview> FromTaskResults(
        string? profileId,
        IEnumerable<MaaTaskRunResult> taskResults,
        string? activeCampaignId = null)
    {
        IEnumerable<MaaCandidatePreview> candidates;
        if (string.IsNullOrWhiteSpace(profileId) || string.Equals(profileId, "all", StringComparison.Ordinal))
            candidates = AllProfileCandidates(taskResults, activeCampaignId);

        else if (string.Equals(profileId, "runStatusFull", StringComparison.Ordinal))
            candidates = BestRunStatusCandidates(RunStatusCandidates(taskResults, activeCampaignId));

        else if (string.Equals(profileId, "operatorsFull", StringComparison.Ordinal))
        {
            var results = taskResults.ToArray();
            candidates = OperatorCandidates(results)
                .Concat(activeCampaignId == "is3_mizuki"
                    ? MizukiRejectionCardCandidates(results)
                        .Concat(MizukiEvolutionCardCandidates(results))
                    : []);
        }

        else if (string.Equals(profileId, "relicsFull", StringComparison.Ordinal))
            candidates = RelicCandidates(taskResults, activeCampaignId);

        else if (string.Equals(profileId, "is5ThoughtFull", StringComparison.Ordinal))
            candidates = ThoughtCandidates(taskResults);

        else if (string.Equals(profileId, "is5AgeFull", StringComparison.Ordinal))
            candidates = AgeCandidates(taskResults);

        else if (string.Equals(profileId, "is2HallucinationsFull", StringComparison.Ordinal))
            candidates = HallucinationCandidates(taskResults);

        else if (string.Equals(profileId, "is2PerformanceFull", StringComparison.Ordinal))
            candidates = PerformanceCandidates(taskResults);

        else if (string.Equals(profileId, "is3KeyFull", StringComparison.Ordinal))
            candidates = MizukiKeyCandidates(taskResults);

        else if (string.Equals(profileId, "is3LightHordeFull", StringComparison.Ordinal))
            candidates = MizukiLightAndHordeCandidates(taskResults);

        else if (string.Equals(profileId, "is3RejectionFull", StringComparison.Ordinal))
            candidates = MizukiRejectionCandidates(taskResults);

        else if (string.Equals(profileId, "is4RevelationFull", StringComparison.Ordinal))
            candidates = RevelationCandidates(taskResults);

        else if (string.Equals(profileId, "is6BaseFull", StringComparison.Ordinal))
            candidates = BestRunStatusCandidates(RunStatusCandidates(taskResults, "is6_sui"));

        else if (string.Equals(profileId, "is6ActiveCoinsFull", StringComparison.Ordinal))
            candidates = CoinCandidates(taskResults, "activeCoins");

        else if (string.Equals(profileId, "is6CoinsFull", StringComparison.Ordinal))
            candidates = CoinCandidates(taskResults, "coins");

        else if (string.Equals(profileId, "is6SeasonalHours", StringComparison.Ordinal))
            candidates = SuiSeasonalHourCandidates(taskResults);

        else
            candidates = [];

        return candidates.Where(RhodesMaaRecognitionPolicy.IsRetainedCandidate).ToArray();
    }

    private static IReadOnlyList<MaaCandidatePreview> AllProfileCandidates(
        IEnumerable<MaaTaskRunResult> taskResults,
        string? activeCampaignId)
    {
        var results = taskResults.ToArray();
        var candidates = BestRunStatusCandidates(RunStatusCandidates(results, activeCampaignId))
            .Concat(OperatorCandidates(results))
            .Concat(RelicCandidates(results, activeCampaignId))
            .Concat(ThoughtCandidates(results))
            .Concat(AgeCandidates(results))
            .Concat(HallucinationCandidates(results))
            .Concat(PerformanceCandidates(results))
            .Concat(MizukiKeyCandidates(results))
            .Concat(MizukiLightAndHordeCandidates(results))
            .Concat(MizukiRejectionCandidates(results))
            .Concat(activeCampaignId == "is3_mizuki"
                ? MizukiRejectionCardCandidates(results)
                    .Concat(MizukiEvolutionCardCandidates(results))
                : [])
            .Concat(RevelationCandidates(results))
            .Concat(CoinCandidates(results, "activeCoins"))
            .Concat(CoinCandidates(results, "coins"))
            .Concat(SuiSeasonalHourCandidates(results));
        return RhodesMaaCandidateMerger.Merge([], candidates);
    }

    private static IReadOnlyList<MaaCandidatePreview> BestRunStatusCandidates(IEnumerable<MaaCandidatePreview> candidates)
    {
        var bestByField = new Dictionary<string, (MaaCandidatePreview Candidate, int Index)>(StringComparer.Ordinal);
        var index = 0;
        foreach (var candidate in candidates)
        {
            var field = string.IsNullOrWhiteSpace(candidate.Field) ? candidate.Value : candidate.Field;
            if (!bestByField.TryGetValue(field, out var existing))
            {
                bestByField[field] = (candidate, index);
            }
            else if (RunStatusCandidatePriority(candidate) > RunStatusCandidatePriority(existing.Candidate)
                || (RunStatusCandidatePriority(candidate) == RunStatusCandidatePriority(existing.Candidate)
                    && (candidate.Confidence ?? 0) > (existing.Candidate.Confidence ?? 0)))
            {
                bestByField[field] = (candidate, existing.Index);
            }
            index++;
        }

        return bestByField.Values
            .OrderBy(item => item.Index)
            .Select(item => item.Candidate)
            .ToArray();
    }

    private static int RunStatusCandidatePriority(MaaCandidatePreview candidate)
    {
        return candidate.Field.Equals("squadId", StringComparison.Ordinal)
            && candidate.RecognitionKey.StartsWith("maa-local:squad-icon:", StringComparison.Ordinal)
                ? 1
                : 0;
    }

    private static IEnumerable<MaaCandidatePreview> RunStatusCandidates(
        IEnumerable<MaaTaskRunResult> taskResults,
        string? activeCampaignId = null)
    {
        var results = taskResults as MaaTaskRunResult[] ?? taskResults.ToArray();
        var campaignId = RunStatusCampaignId(results, activeCampaignId);
        foreach (var taskResult in results)
        {
            if (!taskResult.Succeeded)
                continue;

            foreach (var staticCandidate in StaticCandidates(taskResult))
            {
                yield return staticCandidate;
            }

            foreach (var squadCandidate in SquadCandidates(taskResult, campaignId))
            {
                yield return squadCandidate;
            }

            foreach (var squadCandidate in SquadIconCandidates(taskResult, campaignId))
            {
                yield return squadCandidate;
            }

            var regionId = RunStatusRegionId(taskResult.Entry);
            if (string.IsNullOrWhiteSpace(regionId) || !RunStatusFields.TryGetValue(regionId, out var field))
                continue;
            if (campaignId.Equals("is6_sui", StringComparison.Ordinal)
                && regionId.Equals("run.ingot", StringComparison.Ordinal))
            {
                continue;
            }
            if (field.Field.Equals("difficulty", StringComparison.Ordinal)
                && RhodesMaaRecognitionPolicy.RequiresManualDifficulty(campaignId))
            {
                continue;
            }

            var textResult = PrimaryTextResult(taskResult.RecognitionDetailJson);
            if (string.IsNullOrWhiteSpace(textResult.Text))
                continue;

            var maximum = field.Field.Equals("difficulty", StringComparison.Ordinal)
                ? RhodesRunCatalog.MaxDifficultyForCampaign(campaignId)
                : field.Max;
            var value = field.Field.Equals("difficulty", StringComparison.Ordinal)
                ? DifficultyValue(textResult.Text, maximum)
                : RunStatusNumericValue(regionId, textResult.Text);
            if (value is null || value < field.Min || value > maximum)
                continue;

            var confidence = Math.Max(field.Confidence, textResult.Confidence ?? 0);
            yield return new MaaCandidatePreview(
                "runStatus",
                field.Label,
                value.Value.ToString(CultureInfo.InvariantCulture),
                textResult.Text,
                confidence,
                Field: field.Field,
                CampaignId: RunStatusFieldCampaignId(field.Field, campaignId),
                RecognitionKey: $"maa-local:{field.Field}:{regionId}");
        }

        foreach (var candidate in SquadRandomEffectCandidates(results, campaignId))
        {
            yield return candidate;
        }

        foreach (var candidate in SquadDescriptionCandidates(results, campaignId))
        {
            yield return candidate;
        }
    }

    private static string RunStatusCampaignId(
        IReadOnlyList<MaaTaskRunResult> taskResults,
        string? activeCampaignId)
    {
        if (!string.IsNullOrWhiteSpace(activeCampaignId))
            return activeCampaignId;

        foreach (var taskResult in taskResults)
        {
            if (!taskResult.Succeeded)
                continue;

            foreach (var candidate in StaticCandidates(taskResult))
            {
                if (candidate.Field.Equals("campaignId", StringComparison.Ordinal))
                    return FirstNonEmpty(candidate.Value, candidate.CampaignId);
            }
        }

        return RhodesRunCatalog.LoadDefault().Current.CampaignId;
    }

    private static string RunStatusFieldCampaignId(string field, string campaignId)
    {
        return field switch
        {
            "idea" => "is5_sarkaz",
            "ticket" => "is6_sui",
            "difficulty" => campaignId,
            _ => "",
        };
    }

    private static string RunStatusRegionId(string entry)
    {
        if (entry.Contains("is6.ingot_value", StringComparison.OrdinalIgnoreCase))
            return "is6.ingot_value";
        if (entry.Contains("is6.ticket_value", StringComparison.OrdinalIgnoreCase))
            return "is6.ticket_value";
        if (entry.Contains("run.ingot", StringComparison.OrdinalIgnoreCase))
            return "run.ingot";
        if (entry.Contains("run.idea.current", StringComparison.OrdinalIgnoreCase))
            return "run.idea.current";
        if (entry.Contains("run.difficulty_grade", StringComparison.OrdinalIgnoreCase)
            || entry.Contains("run.difficulty.grade", StringComparison.OrdinalIgnoreCase))
        {
            return "run.difficulty_grade";
        }

        return entry switch
        {
            "RhodesOcrRegion_is6_ingot_value" => "is6.ingot_value",
            "RhodesOcrRegion_is6_ticket_value" => "is6.ticket_value",
            "RhodesOcrRegion_run_ingot" or "RhodesTemplate_runStatusFull_run_ingot" => "run.ingot",
            "RhodesOcrRegion_run_idea" => "run.idea",
            "RhodesOcrRegion_run_idea_current" or "RhodesTemplate_runStatusFull_run_idea_current" => "run.idea.current",
            "RhodesOcrRegion_run_difficulty_grade" => "run.difficulty_grade",
            _ => "",
        };
    }

    private static IEnumerable<MaaCandidatePreview> SquadCandidates(MaaTaskRunResult taskResult, string campaignId)
    {
        if (!IsSquadNameEntry(taskResult.Entry))
            yield break;

        if (string.IsNullOrWhiteSpace(campaignId))
            yield break;

        var squads = LoadSquads()
            .Where(item => string.Equals(item.CampaignId, campaignId, StringComparison.Ordinal))
            .Where(item => !string.IsNullOrWhiteSpace(item.Id) && !string.IsNullOrWhiteSpace(item.Name))
            .ToArray();
        var byNormalizedName = squads
            .GroupBy(item => NormalizeChoiceName(item.Name), StringComparer.Ordinal)
            .Where(group => !string.IsNullOrWhiteSpace(group.Key) && group.Count() == 1)
            .ToDictionary(group => group.Key, group => group.Single(), StringComparer.Ordinal);

        foreach (var textResult in PrimaryTextResults(taskResult.RecognitionDetailJson))
        {
            foreach (var token in ChoiceNameTokens(textResult.Text))
            {
                var normalized = SquadOcrNameAliases.TryGetValue(token.Normalized, out var alias)
                    ? NormalizeChoiceName(alias)
                    : token.Normalized;
                if (!byNormalizedName.TryGetValue(normalized, out var squad))
                {
                    var contained = byNormalizedName
                        .Where(item => normalized.Contains(item.Key, StringComparison.Ordinal))
                        .Take(2)
                        .ToArray();
                    if (contained.Length != 1)
                        continue;
                    squad = contained[0].Value;
                }

                yield return new MaaCandidatePreview(
                    "runStatus",
                    squad.Name,
                    squad.Id,
                    token.Raw,
                    Math.Max(0.70, textResult.Confidence ?? 0),
                    Field: "squadId",
                    CampaignId: squad.CampaignId,
                    RecognitionKey: $"maa-local:squad:{squad.Id}");
            }
        }
    }

    private static IEnumerable<MaaCandidatePreview> SquadIconCandidates(MaaTaskRunResult taskResult, string campaignId)
    {
        if (!taskResult.Hit || !TryResolveSquadIcon(taskResult, out var squadId, out var score))
            yield break;

        var squad = LoadSquads().FirstOrDefault(item =>
            item.Id.Equals(squadId, StringComparison.Ordinal)
            && (string.IsNullOrWhiteSpace(campaignId) || item.CampaignId.Equals(campaignId, StringComparison.Ordinal)));
        if (squad is null)
            yield break;

        yield return new MaaCandidatePreview(
            "runStatus",
            squad.Name,
            squad.Id,
            $"右半分アイコン一致 {score:0.000}",
            score > 0 ? Math.Clamp(score, 0, 1) : 0.90,
            Field: "squadId",
            CampaignId: squad.CampaignId,
            RecognitionKey: $"maa-local:squad-icon:{squad.Id}");
    }

    private static bool TryResolveSquadIcon(MaaTaskRunResult taskResult, out string squadId, out double score)
    {
        squadId = "";
        score = 0;
        var templateIds = RhodesMaaResourceCatalog.LoadCompositeTemplateIds(taskResult.Entry);
        if (templateIds.Count > 0)
        {
            if (!RhodesMaaCompositeTemplateResult.TryReadFirstHit(taskResult.RecognitionDetailJson, out var hit)
                || hit.Index < 0
                || hit.Index >= templateIds.Count)
            {
                return false;
            }

            squadId = templateIds[hit.Index];
            score = hit.Score;
            return !string.IsNullOrWhiteSpace(squadId);
        }

        if (!TrySquadIdFromIconEntry(taskResult.Entry, out squadId))
            return false;
        score = RhodesMaaCompositeTemplateResult.ReadBestScore(taskResult.RecognitionDetailJson);
        return true;
    }

    private static bool TrySquadIdFromIconEntry(string entry, out string squadId)
    {
        const string generatedMarker = "_run_squad_icon_";
        const string directMarker = "run.squad.icon.";
        squadId = "";
        var generatedIndex = entry.IndexOf(generatedMarker, StringComparison.OrdinalIgnoreCase);
        if (generatedIndex >= 0)
        {
            squadId = entry[(generatedIndex + generatedMarker.Length)..];
            return !string.IsNullOrWhiteSpace(squadId);
        }

        var directIndex = entry.IndexOf(directMarker, StringComparison.OrdinalIgnoreCase);
        if (directIndex < 0)
            return false;

        squadId = entry[(directIndex + directMarker.Length)..];
        return !string.IsNullOrWhiteSpace(squadId);
    }

    private static IEnumerable<MaaCandidatePreview> SquadRandomEffectCandidates(
        IReadOnlyList<MaaTaskRunResult> taskResults,
        string campaignId)
    {
        if (string.IsNullOrWhiteSpace(campaignId))
            yield break;

        var textResults = taskResults
            .Where(taskResult => taskResult.Succeeded && (IsSquadNameEntry(taskResult.Entry) || IsSquadCardEntry(taskResult.Entry)))
            .SelectMany(taskResult => PrimaryTextResults(taskResult.RecognitionDetailJson))
            .Where(result => !string.IsNullOrWhiteSpace(result.Text))
            .ToArray();
        if (textResults.Length == 0)
            yield break;

        var rawText = string.Join(" ", textResults.Select(result => result.Text));
        var normalizedChoiceText = NormalizeChoiceName(rawText);
        var squads = LoadSquads()
            .Where(item => string.Equals(item.CampaignId, campaignId, StringComparison.Ordinal))
            .Where(item => item.RandomEffectOptions.Count > 0)
            .ToArray();
        var iconSquadId = taskResults
            .Where(taskResult => taskResult.Succeeded && taskResult.Hit)
            .Select(taskResult => TryResolveSquadIcon(taskResult, out var squadId, out _) ? squadId : "")
            .FirstOrDefault(squadId => !string.IsNullOrWhiteSpace(squadId));
        var squad = !string.IsNullOrWhiteSpace(iconSquadId)
            ? squads.FirstOrDefault(item => item.Id.Equals(iconSquadId, StringComparison.Ordinal))
            : squads.FirstOrDefault(item => normalizedChoiceText.Contains(NormalizeChoiceName(item.Name), StringComparison.Ordinal));
        if (squad is null)
            yield break;

        var match = FindRandomEffectOption(NormalizeSquadEffectText(rawText), squad.RandomEffectOptions);
        if (match is not { } resolved || string.IsNullOrWhiteSpace(resolved.Option.Id))
            yield break;

        yield return new MaaCandidatePreview(
            "runStatus",
            string.IsNullOrWhiteSpace(resolved.Option.Label) ? "ランダム分隊効果" : resolved.Option.Label,
            resolved.Option.Id,
            FirstNonEmpty(resolved.Option.Effect, resolved.Option.Label, resolved.Option.Id),
            Math.Min(0.93, 0.68 + (resolved.Score / 500.0)),
            Field: "squadRandomEffectOptionId",
            CampaignId: squad.CampaignId,
            RecognitionKey: $"maa-local:squad-option:{resolved.Option.Id}");
    }

    private static IEnumerable<MaaCandidatePreview> SquadDescriptionCandidates(
        IReadOnlyList<MaaTaskRunResult> taskResults,
        string campaignId)
    {
        if (string.IsNullOrWhiteSpace(campaignId))
            yield break;

        var textResults = taskResults
            .Where(taskResult => taskResult.Succeeded && IsSquadCardEntry(taskResult.Entry))
            .SelectMany(taskResult => PrimaryTextResults(taskResult.RecognitionDetailJson))
            .Where(result => !string.IsNullOrWhiteSpace(result.Text))
            .ToArray();
        if (textResults.Length == 0)
            yield break;

        var rawText = string.Join(" ", textResults.Select(result => result.Text));
        var normalizedText = NormalizeSquadEffectText(rawText);
        if (normalizedText.Length < 16)
            yield break;

        var scored = LoadSquads()
            .Where(item => string.Equals(item.CampaignId, campaignId, StringComparison.Ordinal))
            .Where(item => item.EffectSignatures.Count > 0)
            .Select(item => (Squad: item, Score: item.EffectSignatures
                .Select(effect => SquadEffectSimilarity(normalizedText, NormalizeSquadEffectText(effect)))
                .DefaultIfEmpty(0)
                .Max()))
            .OrderByDescending(item => item.Score)
            .ToArray();
        if (scored.Length == 0 || scored[0].Score < 0.48)
            yield break;
        if (scored.Length > 1 && scored[0].Score - scored[1].Score < 0.08)
            yield break;

        var resolved = scored[0];
        yield return new MaaCandidatePreview(
            "runStatus",
            resolved.Squad.Name,
            resolved.Squad.Id,
            rawText,
            Math.Min(0.88, 0.58 + (resolved.Score * 0.30)),
            Field: "squadId",
            CampaignId: resolved.Squad.CampaignId,
            RecognitionKey: $"maa-local:squad-effect:{resolved.Squad.Id}");
    }

    private static double SquadEffectSimilarity(string observed, string expected)
    {
        if (string.IsNullOrWhiteSpace(observed) || expected.Length < 8)
            return 0;
        if (observed.Contains(expected, StringComparison.Ordinal))
            return 1;

        var observedBigrams = CharacterBigrams(observed);
        var expectedBigrams = CharacterBigrams(expected);
        if (observedBigrams.Count == 0 || expectedBigrams.Count == 0)
            return 0;

        var overlap = expectedBigrams.Count(observedBigrams.Contains);
        return overlap / (double)expectedBigrams.Count;
    }

    private static HashSet<string> CharacterBigrams(string value)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index + 1 < value.Length; index++)
        {
            result.Add(value.Substring(index, 2));
        }
        return result;
    }

    private static IEnumerable<MaaCandidatePreview> StaticCandidates(MaaTaskRunResult taskResult)
    {
        if (!StaticCandidatePreviewsByEntry.Value.TryGetValue(taskResult.Entry, out var candidates))
            yield break;

        var rawText = PrimaryTextResult(taskResult.RecognitionDetailJson).Text;
        foreach (var candidate in candidates)
        {
            yield return candidate with
            {
                RawText = string.IsNullOrWhiteSpace(rawText) ? candidate.RawText : rawText,
            };
        }
    }

    private static IEnumerable<MaaCandidatePreview> OperatorCandidates(IEnumerable<MaaTaskRunResult> taskResults)
    {
        var results = taskResults.ToArray();
        var operators = RhodesRunCatalog.LoadDefault().Operators
            .Where(item => !string.IsNullOrWhiteSpace(item.Id) && !string.IsNullOrWhiteSpace(item.Name))
            .ToArray();
        var byNormalizedName = operators
            .GroupBy(item => RhodesOperatorOcrNormalizer.Normalize(item.Name), StringComparer.Ordinal)
            .Where(group => !string.IsNullOrWhiteSpace(group.Key) && group.Count() == 1)
            .ToDictionary(group => group.Key, group => group.Single(), StringComparer.Ordinal);
        var byId = operators.ToDictionary(item => item.Id, StringComparer.Ordinal);
        var matched = new Dictionary<string, (SukiChoiceItem Operator, string RawText, double? Confidence, int Order, bool RoleResolved)>(
            StringComparer.Ordinal);
        var reserveCountsByFrame = new Dictionary<(int FrameIndex, string OperatorId), int>();
        var frameIndex = 0;
        var order = 0;

        for (var taskResultIndex = 0; taskResultIndex < results.Length; taskResultIndex++)
        {
            var taskResult = results[taskResultIndex];
            if (IsOperatorFrameBoundary(taskResult.Entry))
            {
                frameIndex++;
                continue;
            }

            if (!taskResult.Succeeded || !IsOperatorNameEntry(taskResult.Entry))
                continue;

            var countedReserveIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var textResult in PrimaryTextResults(taskResult.RecognitionDetailJson))
            {
                foreach (var token in ChoiceNameTokens(textResult.Text))
                {
                    var normalized = RhodesOperatorOcrNormalizer.Normalize(token.Raw);
                    var officialId = RhodesOperatorOcrNormalizer.ResolveOfficialOperatorId(token.Raw);
                    var roleResolved = false;
                    if (RhodesMaaAmiyaRoleResolver.ContainsLiteralAmiya(taskResult))
                    {
                        var roleOperatorId = RhodesMaaAmiyaRoleResolver.ResolveOperatorId(results, taskResultIndex);
                        if (!string.IsNullOrWhiteSpace(roleOperatorId))
                        {
                            officialId = roleOperatorId;
                            roleResolved = true;
                        }
                    }
                    var op = officialId is not null && byId.TryGetValue(officialId, out var official)
                        ? official
                        : ResolveOperator(normalized, byNormalizedName);

                    if (op is null)
                        continue;

                    if (op.SupportsMultipleCount && countedReserveIds.Add(op.Id))
                    {
                        var countKey = (frameIndex, op.Id);
                        reserveCountsByFrame[countKey] = reserveCountsByFrame.GetValueOrDefault(countKey) + 1;
                    }

                    if (!matched.TryGetValue(op.Id, out var existing))
                    {
                        matched[op.Id] = (op, token.Raw, textResult.Confidence, order, roleResolved);
                    }
                    else if ((roleResolved && !existing.RoleResolved)
                        || (roleResolved == existing.RoleResolved
                            && (textResult.Confidence ?? 0) > (existing.Confidence ?? 0)))
                    {
                        matched[op.Id] = (op, token.Raw, textResult.Confidence, existing.Order, roleResolved);
                    }
                }
            }

            order++;
        }

        var reserveCounts = reserveCountsByFrame
            .GroupBy(item => item.Key.OperatorId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Max(item => item.Value),
                StringComparer.Ordinal);

        var resolvedAmiyaId = matched.Values
            .Where(item => item.RoleResolved && RhodesMaaAmiyaRoleResolver.IsAmiyaOperatorId(item.Operator.Id))
            .OrderByDescending(item => item.Confidence ?? 0)
            .Select(item => item.Operator.Id)
            .FirstOrDefault();
        foreach (var item in matched.Values
            .Where(item => string.IsNullOrWhiteSpace(resolvedAmiyaId)
                || !RhodesMaaAmiyaRoleResolver.IsAmiyaOperatorId(item.Operator.Id)
                || item.Operator.Id.Equals(resolvedAmiyaId, StringComparison.Ordinal))
            .OrderBy(item => item.Order))
        {
            yield return new MaaCandidatePreview(
                "operator",
                item.Operator.Name,
                item.Operator.Id,
                item.RawText,
                Math.Max(0.70, item.Confidence ?? 0),
                OperatorId: item.Operator.Id,
                RecognitionKey: item.RoleResolved
                    ? RhodesMaaAmiyaRoleResolver.RoleRecognitionKey(item.Operator.Id)
                    : $"maa-local:operator:{item.Operator.Id}",
                Count: item.Operator.SupportsMultipleCount
                    ? Math.Max(1, reserveCounts.GetValueOrDefault(item.Operator.Id))
                    : 0);
        }
    }

    private static bool IsOperatorFrameBoundary(string entry)
    {
        return entry.Equals("RhodesTemplate_operatorsFull_operator_card_name", StringComparison.Ordinal)
            || entry.EndsWith("_operatorsFull_operator_card_name", StringComparison.Ordinal);
    }

    private static SukiChoiceItem? ResolveOperator(
        string normalized,
        IReadOnlyDictionary<string, SukiChoiceItem> byNormalizedName)
    {
        if (normalized.Length < 2)
            return null;
        if (byNormalizedName.TryGetValue(normalized, out var exact))
            return exact;

        var fuzzy = byNormalizedName
            .Where(item => item.Key.Length >= 5 && Math.Abs(item.Key.Length - normalized.Length) <= 2)
            .Select(item => new
            {
                item.Key,
                item.Value,
                Distance = EditDistance(normalized, item.Key),
            })
            .Where(item => item.Distance <= Math.Max(1, (int)Math.Ceiling(item.Key.Length * 0.25)))
            .OrderBy(item => item.Distance)
            .ThenByDescending(item => item.Key.Length)
            .Take(2)
            .ToArray();
        if (fuzzy.Length > 0
            && (fuzzy.Length == 1
                || fuzzy[0].Distance < fuzzy[1].Distance
                || fuzzy[0].Key.Length > fuzzy[1].Key.Length))
        {
            return fuzzy[0].Value;
        }

        var contained = byNormalizedName
            .Where(item => item.Key.Length >= 3 && normalized.Contains(item.Key, StringComparison.Ordinal))
            .OrderByDescending(item => item.Key.Length)
            .Take(2)
            .ToArray();
        return contained.Length > 0
            && (contained.Length == 1 || contained[0].Key.Length > contained[1].Key.Length)
                ? contained[0].Value
                : null;
    }

    private static int EditDistance(string left, string right)
    {
        if (left.Length == 0)
            return right.Length;
        if (right.Length == 0)
            return left.Length;

        var previous = Enumerable.Range(0, right.Length + 1).ToArray();
        var current = new int[right.Length + 1];
        for (var leftIndex = 1; leftIndex <= left.Length; leftIndex++)
        {
            current[0] = leftIndex;
            for (var rightIndex = 1; rightIndex <= right.Length; rightIndex++)
            {
                var cost = left[leftIndex - 1] == right[rightIndex - 1] ? 0 : 1;
                current[rightIndex] = Math.Min(
                    Math.Min(current[rightIndex - 1] + 1, previous[rightIndex] + 1),
                    previous[rightIndex - 1] + cost);
            }
            (previous, current) = (current, previous);
        }
        return previous[right.Length];
    }

    private static IEnumerable<MaaCandidatePreview> RelicCandidates(
        IEnumerable<MaaTaskRunResult> taskResults,
        string? activeCampaignId)
    {
        var catalog = RhodesRunCatalog.LoadDefault();
        var campaignId = string.IsNullOrWhiteSpace(activeCampaignId)
            ? catalog.Current.CampaignId
            : activeCampaignId;
        if (string.IsNullOrWhiteSpace(campaignId))
            yield break;

        var relics = catalog.Relics
            .Where(item => string.Equals(item.CampaignId, campaignId, StringComparison.Ordinal))
            .Where(item => !string.IsNullOrWhiteSpace(item.Id) && !string.IsNullOrWhiteSpace(item.Name))
            .ToArray();
        var byNormalizedName = relics
            .GroupBy(item => NormalizeRelicName(item.Name), StringComparer.Ordinal)
            .Where(group => !string.IsNullOrWhiteSpace(group.Key) && group.Count() == 1)
            .ToDictionary(group => group.Key, group => group.Single(), StringComparer.Ordinal);
        var matched = new Dictionary<string, (SukiChoiceItem Relic, string RawText, double? Confidence, int Order, string StateId)>(
            StringComparer.Ordinal);
        var order = 0;

        foreach (var taskResult in taskResults)
        {
            if (!taskResult.Succeeded || !IsRelicNameEntry(taskResult.Entry))
                continue;

            var textResults = PrimaryTextResults(taskResult.RecognitionDetailJson);
            foreach (var textResult in textResults)
            {
                foreach (var token in ChoiceNameTokens(textResult.Text))
                {
                    var relic = ResolveRelic(NormalizeRelicName(token.Raw), byNormalizedName);
                    if (relic is null)
                        continue;

                    var usageState = RelicUsageState(relic, textResult, textResults);

                    if (!matched.TryGetValue(relic.Id, out var existing))
                    {
                        matched[relic.Id] = (relic, token.Raw, textResult.Confidence, order, usageState);
                    }
                    else if ((textResult.Confidence ?? 0) > (existing.Confidence ?? 0))
                    {
                        matched[relic.Id] = (
                            relic,
                            token.Raw,
                            textResult.Confidence,
                            existing.Order,
                            MergeRelicUsageState(existing.StateId, usageState));
                    }
                    else
                    {
                        matched[relic.Id] = existing with
                        {
                            StateId = MergeRelicUsageState(existing.StateId, usageState),
                        };
                    }
                }
            }

            order++;
        }

        foreach (var item in matched.Values.OrderBy(item => item.Order))
        {
            yield return new MaaCandidatePreview(
                "relic",
                item.Relic.Name,
                item.Relic.Id,
                item.RawText,
                Math.Max(0.68, item.Confidence ?? 0),
                RelicId: item.Relic.Id,
                CampaignId: item.Relic.CampaignId,
                RecognitionKey: $"maa-local:relic:{item.Relic.Id}",
                StateId: item.StateId);
        }
    }

    private static string RelicUsageState(
        SukiChoiceItem relic,
        OcrTextResult nameResult,
        IReadOnlyList<OcrTextResult> textResults)
    {
        if (!RhodesRelicUsagePolicy.SupportsUsedFlag(relic.Name)
            || nameResult.X < 0
            || nameResult.Y < 0
            || nameResult.Width <= 0
            || nameResult.Height <= 0)
        {
            return "";
        }

        var nameCenterY = nameResult.Y + (nameResult.Height / 2d);
        var nameRight = nameResult.X + nameResult.Width;
        var markerFound = textResults.Any(result =>
        {
            if (!IsRelicUsedMarker(result.Text)
                || result.X < nameRight + 4
                || result.X > nameResult.X + 330
                || result.Y < 0
                || result.Height <= 0)
            {
                return false;
            }

            var markerCenterY = result.Y + (result.Height / 2d);
            var tolerance = Math.Max(14d, (nameResult.Height + result.Height) * 0.65d);
            return Math.Abs(markerCenterY - nameCenterY) <= tolerance;
        });
        return markerFound ? "used" : "unused";
    }

    private static bool IsRelicUsedMarker(string value)
    {
        var normalized = string.Concat(value
            .Normalize(NormalizationForm.FormKC)
            .Where(character => !char.IsWhiteSpace(character)));
        return normalized.StartsWith("使用", StringComparison.Ordinal)
            && normalized.Length <= 4
            && !normalized.Contains("使用後", StringComparison.Ordinal);
    }

    private static string MergeRelicUsageState(string left, string right)
    {
        if (left.Equals("used", StringComparison.OrdinalIgnoreCase)
            || right.Equals("used", StringComparison.OrdinalIgnoreCase))
        {
            return "used";
        }

        return left.Equals("unused", StringComparison.OrdinalIgnoreCase)
            || right.Equals("unused", StringComparison.OrdinalIgnoreCase)
                ? "unused"
                : "";
    }

    private static string NormalizeRelicName(string value)
    {
        var normalized = NormalizeChoiceName(value)
            .ToLowerInvariant()
            .Replace("...", "", StringComparison.Ordinal)
            .Replace("…", "", StringComparison.Ordinal)
            .Replace("⋯", "", StringComparison.Ordinal)
            .Replace("“", "", StringComparison.Ordinal)
            .Replace("”", "", StringComparison.Ordinal)
            .Replace("‘", "", StringComparison.Ordinal)
            .Replace("’", "", StringComparison.Ordinal)
            .Replace("\"", "", StringComparison.Ordinal)
            .Replace("'", "", StringComparison.Ordinal)
            .Replace('―', 'ー')
            .Replace('—', 'ー')
            .Replace('－', 'ー')
            .Replace('ｰ', 'ー')
            // MAA-OCR may mix the Cyrillic homoglyph into short Latin relic names.
            .Replace('с', 'c')
            .Replace('ァ', 'ア')
            .Replace('ィ', 'イ')
            .Replace('ゥ', 'ウ')
            .Replace('ェ', 'エ')
            .Replace('ォ', 'オ')
            .Replace('ッ', 'ツ')
            .Replace('ャ', 'ヤ')
            .Replace('ュ', 'ユ')
            .Replace('ョ', 'ヨ')
            .Replace('ヮ', 'ワ')
            .Replace('ぁ', 'あ')
            .Replace('ぃ', 'い')
            .Replace('ぅ', 'う')
            .Replace('ぇ', 'え')
            .Replace('ぉ', 'お')
            .Replace('っ', 'つ')
            .Replace('ゃ', 'や')
            .Replace('ゅ', 'ゆ')
            .Replace('ょ', 'よ')
            .Replace('ゎ', 'わ');

        return new string(normalized
            .Select(character => character is >= 'ァ' and <= 'ヶ'
                ? (char)(character - 0x60)
                : character)
            .ToArray());
    }

    private static SukiChoiceItem? ResolveRelic(
        string normalized,
        IReadOnlyDictionary<string, SukiChoiceItem> byNormalizedName)
    {
        if (byNormalizedName.TryGetValue(normalized, out var exact))
            return exact;
        if (normalized.Length < 4)
            return null;

        var requestsModifiedVariant = normalized.EndsWith("改", StringComparison.Ordinal);
        var variantCompatibleNames = byNormalizedName
            .Where(item => item.Key.EndsWith("改", StringComparison.Ordinal) == requestsModifiedVariant)
            .ToArray();
        var candidates = variantCompatibleNames.Length > 0 ? variantCompatibleNames : byNormalizedName.ToArray();

        var fuzzy = candidates
            .Where(item => item.Key.Length >= 4)
            .Select(item => new
            {
                item.Value,
                CandidateLength = item.Key.Length,
                Distance = EditDistance(normalized, item.Key),
            })
            .Select(item => new
            {
                item.Value,
                item.CandidateLength,
                item.Distance,
                MaximumDistance = Math.Min(normalized.Length, item.CandidateLength) switch
                {
                    <= 4 => 1,
                    <= 9 => 2,
                    _ => 3,
                },
            })
            .Where(item => Math.Abs(item.CandidateLength - normalized.Length) <= item.MaximumDistance
                && item.Distance <= item.MaximumDistance)
            .OrderBy(item => item.Distance)
            .ThenBy(item => Math.Abs(item.CandidateLength - normalized.Length))
            .Take(2)
            .ToArray();
        if (fuzzy.Length == 1 || (fuzzy.Length > 1 && fuzzy[0].Distance < fuzzy[1].Distance))
            return fuzzy[0].Value;

        var prefix = candidates
            .Where(item => normalized.StartsWith(item.Key, StringComparison.Ordinal)
                && normalized.Length - item.Key.Length <= 2)
            .Take(2)
            .ToArray();
        return prefix.Length == 1 ? prefix[0].Value : null;
    }

    private static IEnumerable<MaaCandidatePreview> AgeCandidates(IEnumerable<MaaTaskRunResult> taskResults)
    {
        var textResults = taskResults
            .Where(taskResult => taskResult.Succeeded && IsAgeEntry(taskResult.Entry))
            .SelectMany(taskResult => PrimaryTextResults(taskResult.RecognitionDetailJson))
            .Where(result => !string.IsNullOrWhiteSpace(result.Text))
            .ToArray();
        if (textResults.Length == 0)
            yield break;

        var rawText = string.Join("\n", textResults.Select(result => result.Text));
        var normalizedText = NormalizeChoiceName(rawText);
        if (string.IsNullOrWhiteSpace(normalizedText))
            yield break;

        var confidence = textResults.Max(result => result.Confidence ?? 0);
        var ageGroups = LoadSelectableEffects()
            .Where(effect => effect.Slot == "age" && effect.CampaignId == "is5_sarkaz")
            .GroupBy(effect => effect.ParentName, StringComparer.Ordinal)
            .Select(group => group.FirstOrDefault(effect => effect.VariantLabel == "形成期") ?? group.First())
            .ToArray();
        foreach (var effect in ageGroups)
        {
            var parentMatched = new[] { effect.ParentName, effect.GroupLabel }
                .Select(NormalizeChoiceName)
                .Where(alias => alias.Length >= 4)
                .Any(alias => normalizedText.Contains(alias, StringComparison.Ordinal));
            if (!parentMatched)
                continue;

            yield return new MaaCandidatePreview(
                "age",
                effect.Name,
                effect.Id,
                rawText,
                Math.Max(0.70, confidence),
                CampaignId: effect.CampaignId,
                RecognitionKey: $"maa-local:age:{effect.Id}",
                AgeId: effect.Id);
        }
    }

    private static IEnumerable<MaaCandidatePreview> SuiSeasonalHourCandidates(
        IEnumerable<MaaTaskRunResult> taskResults)
    {
        var variants = LoadSelectableEffects()
            .Where(effect => effect.CampaignId == "is6_sui" && effect.Slot == "seasonalHours")
            .ToArray();
        if (variants.Length == 0)
            yield break;

        var groups = variants
            .GroupBy(
                effect => string.IsNullOrWhiteSpace(effect.ParentKey) ? effect.ParentName : effect.ParentKey,
                StringComparer.Ordinal)
            .Select(group => new SuiSeasonalHourGroup(
                group.Key,
                group.First().ParentName,
                group.ToArray()))
            .Where(group => !string.IsNullOrWhiteSpace(group.Key) && !string.IsNullOrWhiteSpace(group.ParentName))
            .ToArray();
        var emitted = new HashSet<string>(StringComparer.Ordinal);
        var order = 0;

        foreach (var taskResult in taskResults)
        {
            if (!taskResult.Succeeded || !IsSuiSeasonalHourEntry(taskResult.Entry))
                continue;

            var rows = PrimaryTextResults(taskResult.RecognitionDetailJson)
                .Where(row => !string.IsNullOrWhiteSpace(row.Text))
                .OrderBy(row => row.Y < 0 ? int.MaxValue : row.Y)
                .ThenBy(row => row.X < 0 ? int.MaxValue : row.X)
                .ToArray();
            if (rows.Length == 0)
                continue;

            var nameRows = rows
                .Select(row => new
                {
                    Row = row,
                    Group = ResolveSuiSeasonalHourGroup(row.Text, groups),
                })
                .Where(item => item.Group is not null)
                .GroupBy(item => item.Group!.Key, StringComparer.Ordinal)
                .Select(group => group.OrderBy(item => item.Row.Y < 0 ? int.MaxValue : item.Row.Y).First())
                .OrderBy(item => item.Row.Y < 0 ? int.MaxValue : item.Row.Y)
                .ToArray();

            if (nameRows.Length == 0)
            {
                var fullText = string.Join("\n", rows.Select(row => row.Text));
                var inferred = ResolveSuiSeasonalHourVariantByEffect(
                    fullText,
                    groups.SelectMany(group => group.Variants).ToArray());
                if (inferred is not null && emitted.Add(inferred.Id))
                {
                    yield return SuiSeasonalHourCandidate(
                        inferred,
                        fullText,
                        rows.Max(row => row.Confidence ?? 0),
                        order++);
                    foreach (var target in SuiSeasonalHourTargetCandidates(
                        inferred,
                        fullText,
                        rows.Max(row => row.Confidence ?? 0)))
                    {
                        yield return target;
                    }
                }
                continue;
            }

            for (var index = 0; index < nameRows.Length; index++)
            {
                var match = nameRows[index];
                var nextY = index + 1 < nameRows.Length ? nameRows[index + 1].Row.Y : int.MaxValue;
                var segmentRows = match.Row.Y < 0
                    ? rows
                    : rows.Where(row => row.Y < 0 || (row.Y >= match.Row.Y - 4 && row.Y < nextY)).ToArray();
                var rawText = string.Join("\n", segmentRows.Select(row => row.Text));
                var variant = ResolveSuiSeasonalHourVariant(rawText, match.Group!.Variants);
                if (variant is null || !emitted.Add(variant.Id))
                    continue;

                yield return SuiSeasonalHourCandidate(
                    variant,
                    rawText,
                    segmentRows.Max(row => row.Confidence ?? 0),
                    order++);
                foreach (var target in SuiSeasonalHourTargetCandidates(
                    variant,
                    rawText,
                    segmentRows.Max(row => row.Confidence ?? 0)))
                {
                    yield return target;
                }
            }
        }
    }

    private static MaaCandidatePreview SuiSeasonalHourCandidate(
        SelectableEffectCandidate variant,
        string rawText,
        double confidence,
        int order) =>
        new(
            "sui",
            variant.Name,
            variant.Id,
            rawText,
            Math.Max(0.72, confidence),
            CampaignId: "is6_sui",
            RecognitionKey: $"maa-local:sui:seasonal-hours:{variant.Id}:{order}",
            FieldId: "seasonalHours",
            EffectId: variant.Id);

    private static IEnumerable<MaaCandidatePreview> SuiSeasonalHourTargetCandidates(
        SelectableEffectCandidate variant,
        string rawText,
        double confidence)
    {
        var normalized = NormalizeChoiceName(rawText);
        if (!variant.ParentKey.Equals("is6sst11", StringComparison.Ordinal)
            && !variant.Id.Contains("_is6sst11_", StringComparison.Ordinal)
            && !normalized.Contains("戌絵", StringComparison.Ordinal))
            yield break;

        var rank = !string.IsNullOrWhiteSpace(variant.VariantRank)
            ? variant.VariantRank
            : variant.Id.Split('_').LastOrDefault() ?? "";
        if (string.IsNullOrWhiteSpace(rank))
        {
            rank = normalized.Contains("入骨", StringComparison.Ordinal) ? "nyuukotsu"
                : normalized.Contains("明瞭", StringComparison.Ordinal) ? "meiryou"
                : normalized.Contains("朦朧", StringComparison.Ordinal) ? "mourou"
                : normalized.Contains("醒覚", StringComparison.Ordinal) ? "awakening"
                : "";
        }
        var limit = rank switch
        {
            "mourou" => 1,
            "meiryou" => 2,
            "nyuukotsu" => 3,
            _ => 0,
        };
        if (limit == 0)
            yield break;

        var targets = new[]
        {
            (Name: "先鋒", Aliases: new[] { "先鋒", "先峰" }),
            (Name: "前衛", Aliases: new[] { "前衛" }),
            (Name: "重装", Aliases: new[] { "重装", "重裝" }),
            (Name: "狙撃", Aliases: new[] { "狙撃", "狙擊" }),
            (Name: "術師", Aliases: new[] { "術師" }),
            (Name: "医療", Aliases: new[] { "医療" }),
            (Name: "補助", Aliases: new[] { "補助" }),
            (Name: "特殊", Aliases: new[] { "特殊" }),
        };

        foreach (var target in targets
            .Where(target => target.Aliases.Any(alias => normalized.Contains(NormalizeChoiceName(alias), StringComparison.Ordinal)))
            .Take(limit))
        {
            yield return new MaaCandidatePreview(
                "sui",
                $"{target.Name} (戌絵対象職分)",
                target.Name,
                rawText,
                Math.Max(0.72, confidence),
                CampaignId: "is6_sui",
                RecognitionKey: $"maa-local:sui:seasonal-hour-target:{target.Name}",
                FieldId: "seasonalHourTargets",
                EffectId: target.Name);
        }
    }

    private static SuiSeasonalHourGroup? ResolveSuiSeasonalHourGroup(
        string text,
        IReadOnlyList<SuiSeasonalHourGroup> groups)
    {
        var normalized = NormalizeChoiceName(text);
        if (string.IsNullOrWhiteSpace(normalized))
            return null;

        var exact = groups
            .Where(group => normalized.Contains(NormalizeChoiceName(group.ParentName), StringComparison.Ordinal))
            .Take(2)
            .ToArray();
        if (exact.Length == 1)
            return exact[0];

        var fuzzy = groups
            .Where(group => normalized.Length <= NormalizeChoiceName(group.ParentName).Length + 2)
            .Select(group => new
            {
                Group = group,
                Distance = BestSubstringEditDistance(normalized, NormalizeChoiceName(group.ParentName)),
            })
            .Where(item => item.Distance <= 1)
            .OrderBy(item => item.Distance)
            .Take(2)
            .ToArray();
        return fuzzy.Length > 0
            && (fuzzy.Length == 1 || fuzzy[0].Distance < fuzzy[1].Distance)
                ? fuzzy[0].Group
                : null;
    }

    private static SelectableEffectCandidate? ResolveSuiSeasonalHourVariant(
        string text,
        IReadOnlyList<SelectableEffectCandidate> variants)
    {
        var normalized = NormalizeChoiceName(text);
        if (string.IsNullOrWhiteSpace(normalized))
            return null;

        // 醒覚は難度による通常段階とは独立している。画面に表示された醒覚を最優先する。
        foreach (var rank in new[] { "awakening", "nyuukotsu", "meiryou", "mourou" })
        {
            var label = rank switch
            {
                "awakening" => "醒覚",
                "nyuukotsu" => "入骨",
                "meiryou" => "明瞭",
                _ => "朦朧",
            };
            if (normalized.Contains(label, StringComparison.Ordinal))
                return variants.FirstOrDefault(variant => variant.VariantRank.Equals(rank, StringComparison.Ordinal));
        }

        var effectMatch = ResolveSuiSeasonalHourVariantByEffect(text, variants);
        if (effectMatch is not null)
            return effectMatch;

        var compact = normalized
            .Replace("LEVEL", "LV", StringComparison.OrdinalIgnoreCase)
            .Replace("．", ".", StringComparison.Ordinal);
        if (compact.Contains("LV.3", StringComparison.OrdinalIgnoreCase)
            || compact.Contains("LV3", StringComparison.OrdinalIgnoreCase)
            || compact.Contains("参", StringComparison.Ordinal))
        {
            return variants.FirstOrDefault(variant => variant.VariantRank == "nyuukotsu");
        }
        if (compact.Contains("LV.2", StringComparison.OrdinalIgnoreCase)
            || compact.Contains("LV2", StringComparison.OrdinalIgnoreCase)
            || compact.Contains("弐", StringComparison.Ordinal))
        {
            return variants.FirstOrDefault(variant => variant.VariantRank == "meiryou");
        }
        if (compact.Contains("LV.1", StringComparison.OrdinalIgnoreCase)
            || compact.Contains("LV1", StringComparison.OrdinalIgnoreCase)
            || compact.Contains("壱", StringComparison.Ordinal))
        {
            return variants.FirstOrDefault(variant => variant.VariantRank == "mourou");
        }

        return null;
    }

    private static SelectableEffectCandidate? ResolveSuiSeasonalHourVariantByEffect(
        string text,
        IReadOnlyList<SelectableEffectCandidate> variants)
    {
        var normalized = NormalizeChoiceName(text);
        if (string.IsNullOrWhiteSpace(normalized))
            return null;

        var matches = variants
            .Where(variant => !string.IsNullOrWhiteSpace(variant.Effect))
            .Select(variant => new
            {
                Variant = variant,
                Effect = NormalizeChoiceName(variant.Effect),
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.Effect))
            .Select(item => new
            {
                item.Variant,
                item.Effect,
                Distance = BestSubstringEditDistance(normalized, item.Effect),
            })
            .Where(item => item.Distance <= Math.Max(1, item.Effect.Length / 8))
            .OrderBy(item => item.Distance)
            .ThenByDescending(item => item.Effect.Length)
            .Take(2)
            .ToArray();
        return matches.Length > 0
            && (matches.Length == 1 || matches[0].Distance < matches[1].Distance)
                ? matches[0].Variant
                : null;
    }

    private static IEnumerable<MaaCandidatePreview> HallucinationCandidates(IEnumerable<MaaTaskRunResult> taskResults)
    {
        var textResults = taskResults
            .Where(taskResult => taskResult.Succeeded && IsHallucinationEntry(taskResult.Entry))
            .SelectMany(taskResult => PrimaryTextResults(taskResult.RecognitionDetailJson))
            .Where(result => !string.IsNullOrWhiteSpace(result.Text))
            .ToArray();
        if (textResults.Length == 0)
            yield break;

        var rawText = string.Join("\n", textResults.Select(result => result.Text));
        var names = RhodesHallucinationCatalog.NormalizeRecognizedNames(
            textResults.Select(result => NormalizeChoiceName(result.Text)));
        if (names.Count == 0)
            yield break;

        var value = string.Join(" / ", names);
        var confidence = textResults.Max(result => result.Confidence ?? 0);
        yield return new MaaCandidatePreview(
            "runStatus",
            "幻覚",
            value,
            rawText,
            Math.Max(0.70, confidence),
            Field: "hallucinations",
            CampaignId: "is2_phantom",
            RecognitionKey: $"maa-local:hallucinations:{string.Join('|', names)}");
    }

    private static IEnumerable<MaaCandidatePreview> PerformanceCandidates(IEnumerable<MaaTaskRunResult> taskResults)
    {
        var textResults = taskResults
            .Where(taskResult => taskResult.Succeeded && IsPerformanceEntry(taskResult.Entry))
            .SelectMany(taskResult => PrimaryTextResults(taskResult.RecognitionDetailJson))
            .Where(result => !string.IsNullOrWhiteSpace(result.Text))
            .ToArray();
        if (textResults.Length == 0)
            yield break;

        var rawText = string.Join("\n", textResults.Select(result => result.Text));
        var performanceRows = textResults
            .Where(result => !NormalizeChoiceName(result.Text).Equals("現在の演目", StringComparison.Ordinal))
            .ToArray();
        var normalizedText = string.Concat(performanceRows.Select(result => NormalizePerformanceOcr(result.Text)));
        if (string.IsNullOrWhiteSpace(normalizedText))
            yield break;

        const string crimsonPrefix = "緋染めの";
        var isCrimson = performanceRows.Any(result =>
            BestSubstringEditDistance(NormalizePerformanceOcr(result.Text), crimsonPrefix) <= 2);
        var matches = RhodesRunCatalog.LoadPerformanceOptions("is2_phantom")
            .Select(option =>
            {
                var japaneseName = NormalizePerformanceOcr(PerformanceJapaneseName(option.Name));
                var crimson = japaneseName.StartsWith(crimsonPrefix, StringComparison.Ordinal);
                var coreName = crimson ? japaneseName[crimsonPrefix.Length..] : japaneseName;
                return new
                {
                    Option = option,
                    IsCrimson = crimson,
                    CoreName = coreName,
                    Distance = BestSubstringEditDistance(normalizedText, coreName),
                };
            })
            .Where(item => item.IsCrimson == isCrimson)
            .OrderBy(item => item.Distance)
            .ThenByDescending(item => item.CoreName.Length)
            .ToArray();
        if (matches.Length == 0 || matches[0].Distance > PerformanceDistanceThreshold(matches[0].CoreName.Length))
            yield break;
        if (matches.Length > 1
            && matches[1].Distance == matches[0].Distance
            && !matches[1].CoreName.Equals(matches[0].CoreName, StringComparison.Ordinal))
        {
            yield break;
        }

        var match = matches[0].Option;
        var confidence = performanceRows
            .Select(result => result.Confidence ?? 0)
            .DefaultIfEmpty(0)
            .Max();
        yield return new MaaCandidatePreview(
            "runStatus",
            match.Name,
            match.Id,
            rawText,
            Math.Max(0.70, confidence),
            Field: "performanceId",
            CampaignId: "is2_phantom",
            RecognitionKey: $"maa-local:performance:{match.Id}");
    }

    private static string PerformanceJapaneseName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        var closingQuote = value.IndexOf('』');
        return closingQuote >= 0 ? value[..(closingQuote + 1)] : value;
    }

    private static string NormalizePerformanceOcr(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        return new string(value
            .Trim()
            .Normalize(NormalizationForm.FormKC)
            .Where(char.IsLetterOrDigit)
            .ToArray());
    }

    private static int BestSubstringEditDistance(string source, string target)
    {
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(target))
            return int.MaxValue;
        if (source.Contains(target, StringComparison.Ordinal))
            return 0;

        var best = EditDistance(source, target);
        var minimumLength = Math.Max(1, target.Length - 2);
        var maximumLength = Math.Min(source.Length, target.Length + 2);
        for (var length = minimumLength; length <= maximumLength; length++)
        {
            for (var start = 0; start + length <= source.Length; start++)
                best = Math.Min(best, EditDistance(source.Substring(start, length), target));
        }
        return best;
    }

    private static int PerformanceDistanceThreshold(int length) => length switch
    {
        <= 2 => 0,
        <= 5 => 1,
        _ => 2,
    };

    private static IEnumerable<MaaCandidatePreview> ThoughtCandidates(IEnumerable<MaaTaskRunResult> taskResults)
    {
        var results = taskResults as MaaTaskRunResult[] ?? taskResults.ToArray();
        var thoughts = LoadSelectableEffects()
            .Where(effect => effect.Slot == "thought" && effect.CampaignId == "is5_sarkaz")
            .ToArray();
        var byNormalizedName = thoughts
            .GroupBy(item => NormalizeChoiceName(item.Name), StringComparer.Ordinal)
            .Where(group => !string.IsNullOrWhiteSpace(group.Key) && group.Count() == 1)
            .ToDictionary(group => group.Key, group => group.Single(), StringComparer.Ordinal);
        var bestCandidates = new Dictionary<string, MaaCandidatePreview>(StringComparer.Ordinal);
        var maxCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var frames = new List<IReadOnlyList<ThoughtOccurrence>>();
        var firstSeen = new List<string>();

        foreach (var taskResult in results)
        {
            if (!taskResult.Succeeded || !IsThoughtNameEntry(taskResult.Entry))
                continue;

            var frameCandidates = new List<ThoughtOccurrence>();
            foreach (var textResult in PrimaryTextResults(taskResult.RecognitionDetailJson))
            {
                foreach (var token in ChoiceNameTokens(textResult.Text))
                {
                    if (!byNormalizedName.TryGetValue(token.Normalized, out var thought))
                        continue;

                    frameCandidates.Add(new ThoughtOccurrence(
                        new MaaCandidatePreview(
                            "thought",
                            thought.Name,
                            thought.Id,
                            token.Raw,
                            Math.Max(0.68, textResult.Confidence ?? 0),
                            CampaignId: thought.CampaignId,
                            ThoughtId: thought.Id),
                        textResult.X,
                        textResult.Y));
                }
            }
            frames.Add(frameCandidates);

            foreach (var group in frameCandidates.GroupBy(item => item.Candidate.ThoughtId, StringComparer.Ordinal))
            {
                var best = group.MaxBy(item => item.Candidate.Confidence ?? 0)!.Candidate;
                var frameCount = group.Count();
                if (!bestCandidates.TryGetValue(group.Key, out var existing))
                {
                    bestCandidates[group.Key] = best;
                    firstSeen.Add(group.Key);
                }
                else if ((best.Confidence ?? 0) > (existing.Confidence ?? 0))
                {
                    bestCandidates[group.Key] = best;
                }
                maxCounts[group.Key] = Math.Max(maxCounts.GetValueOrDefault(group.Key), frameCount);
            }
        }

        var trackedCounts = TrackThoughtOccurrences(frames);
        var reconciledCounts = ReconcileThoughtCountsFromDisplayedLoad(
            results,
            firstSeen,
            maxCounts,
            trackedCounts);
        var order = 0;
        foreach (var thoughtId in firstSeen)
        {
            var count = reconciledCounts.GetValueOrDefault(
                thoughtId,
                Math.Max(maxCounts.GetValueOrDefault(thoughtId), trackedCounts.GetValueOrDefault(thoughtId)));
            for (var index = 0; index < count; index++)
            {
                yield return bestCandidates[thoughtId] with
                {
                    RecognitionKey = $"maa-local:thought:{thoughtId}:{order}",
                };
                order++;
            }
        }
    }

    private static IReadOnlyDictionary<string, int> ReconcileThoughtCountsFromDisplayedLoad(
        IReadOnlyList<MaaTaskRunResult> taskResults,
        IReadOnlyList<string> thoughtIds,
        IReadOnlyDictionary<string, int> maxCounts,
        IReadOnlyDictionary<string, int> trackedCounts)
    {
        var totalLoad = taskResults
            .Where(result => result.Succeeded && IsThoughtTotalLoadEntry(result.Entry))
            .Select(result => PrimaryTextResult(result.RecognitionDetailJson))
            .Select(result => new { Value = NumericValue(result.Text), Confidence = result.Confidence ?? 0 })
            .Where(item => item.Value is >= 0 and <= 999)
            .OrderByDescending(item => item.Confidence)
            .Select(item => item.Value)
            .FirstOrDefault();
        if (totalLoad is null || thoughtIds.Count == 0)
            return new Dictionary<string, int>(StringComparer.Ordinal);

        var displayedLoads = new Dictionary<string, (int Value, double Confidence)>(StringComparer.Ordinal);
        foreach (var taskResult in taskResults.Where(result => result.Succeeded))
        {
            var thoughtId = ThoughtIdFromLoadEntry(taskResult.Entry);
            if (string.IsNullOrWhiteSpace(thoughtId))
                continue;

            var textResult = PrimaryTextResult(taskResult.RecognitionDetailJson);
            var value = NumericValue(textResult.Text);
            if (value is null or < 0 or > 99)
                continue;

            var confidence = textResult.Confidence ?? 0;
            if (!displayedLoads.TryGetValue(thoughtId, out var existing) || confidence > existing.Confidence)
                displayedLoads[thoughtId] = (value.Value, confidence);
        }
        if (thoughtIds.Any(id => !displayedLoads.ContainsKey(id)))
            return new Dictionary<string, int>(StringComparer.Ordinal);

        var counts = thoughtIds.ToDictionary(
            id => id,
            id => Math.Max(maxCounts.GetValueOrDefault(id), trackedCounts.GetValueOrDefault(id)),
            StringComparer.Ordinal);
        var recognizedLoad = counts.Sum(item => item.Value * displayedLoads[item.Key].Value);
        var deficit = totalLoad.Value - recognizedLoad;
        if (deficit <= 0)
            return counts;

        var repeated = counts
            .Where(item => item.Value >= 2)
            .OrderByDescending(item => item.Value)
            .ToArray();
        if (repeated.Length == 0 || repeated.Length > 1 && repeated[0].Value == repeated[1].Value)
            return counts;

        var repeatedId = repeated[0].Key;
        var repeatedLoad = displayedLoads[repeatedId].Value;
        if (repeatedLoad <= 0)
            return counts;

        if (deficit % repeatedLoad != 0)
            return counts;

        var additional = deficit / repeatedLoad;
        if (additional is <= 0 or > 99 || counts[repeatedId] + additional > 99)
            return counts;

        counts[repeatedId] += additional;
        return counts;
    }

    private static IReadOnlyDictionary<string, int> TrackThoughtOccurrences(
        IReadOnlyList<IReadOnlyList<ThoughtOccurrence>> frames)
    {
        var positionedFrames = frames
            .Where(frame => frame.Count > 0 && frame.All(item => item.X >= 0 && item.Y >= 0))
            .ToArray();
        if (positionedFrames.Length == 0)
            return new Dictionary<string, int>(StringComparer.Ordinal);

        var counts = positionedFrames[0]
            .GroupBy(item => item.Candidate.ThoughtId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        var previous = positionedFrames[0];
        foreach (var current in positionedFrames.Skip(1))
        {
            if (IsStableThoughtViewport(previous, current))
            {
                previous = current;
                continue;
            }

            var delta = EstimateThoughtScrollDelta(previous, current);
            if (delta is null)
            {
                foreach (var group in current.GroupBy(item => item.Candidate.ThoughtId, StringComparer.Ordinal))
                    counts[group.Key] = Math.Max(counts.GetValueOrDefault(group.Key), group.Count());
                previous = current;
                continue;
            }

            var usedPrevious = new HashSet<int>();
            var unmatched = new List<ThoughtOccurrence>();
            foreach (var occurrence in current.OrderBy(item => item.Y).ThenBy(item => item.X))
            {
                var match = previous
                    .Select((item, index) => (Item: item, Index: index))
                    .Where(item => !usedPrevious.Contains(item.Index))
                    .Where(item => SameThoughtTrack(item.Item, occurrence))
                    .Select(item => (item.Index, Distance: Math.Abs(item.Item.Y + delta.Value - occurrence.Y)))
                    .Where(item => item.Distance <= 28)
                    .OrderBy(item => item.Distance)
                    .FirstOrDefault((-1, int.MaxValue));
                if (match.Item1 >= 0)
                    usedPrevious.Add(match.Item1);
                else
                    unmatched.Add(occurrence);
            }

            if (unmatched.Count > 0)
            {
                var bottomY = current.Max(item => item.Y);
                foreach (var occurrence in unmatched.Where(item => bottomY - item.Y <= 32))
                {
                    var thoughtId = occurrence.Candidate.ThoughtId;
                    counts[thoughtId] = counts.GetValueOrDefault(thoughtId) + 1;
                }
            }
            previous = current;
        }
        return counts;
    }

    private static bool IsStableThoughtViewport(
        IReadOnlyList<ThoughtOccurrence> previous,
        IReadOnlyList<ThoughtOccurrence> current)
    {
        if (previous.Count != current.Count)
            return false;

        var previousRows = previous.OrderBy(item => item.Candidate.ThoughtId, StringComparer.Ordinal)
            .ThenBy(ThoughtColumn)
            .ThenBy(item => item.Y)
            .ToArray();
        var currentRows = current.OrderBy(item => item.Candidate.ThoughtId, StringComparer.Ordinal)
            .ThenBy(ThoughtColumn)
            .ThenBy(item => item.Y)
            .ToArray();
        return previousRows.Zip(currentRows).All(pair =>
            SameThoughtTrack(pair.First, pair.Second)
            && Math.Abs(pair.First.Y - pair.Second.Y) <= 8);
    }

    private static int? EstimateThoughtScrollDelta(
        IReadOnlyList<ThoughtOccurrence> previous,
        IReadOnlyList<ThoughtOccurrence> current)
    {
        var candidates = previous
            .SelectMany(left => current
                .Where(right => SameThoughtTrack(left, right))
                .Select(right => right.Y - left.Y))
            .Where(delta => delta is >= -180 and <= -30)
            .Distinct()
            .ToArray();
        if (candidates.Length == 0)
            return null;

        return candidates
            .Select(delta => new
            {
                Delta = delta,
                Matches = current.Count(right => previous.Any(left =>
                    SameThoughtTrack(left, right)
                    && Math.Abs(left.Y + delta - right.Y) <= 28)),
            })
            .OrderByDescending(item => item.Matches)
            .ThenBy(item => Math.Abs(Math.Abs(item.Delta) - 90))
            .Select(item => (int?)item.Delta)
            .FirstOrDefault();
    }

    private static bool SameThoughtTrack(ThoughtOccurrence left, ThoughtOccurrence right) =>
        left.Candidate.ThoughtId.Equals(right.Candidate.ThoughtId, StringComparison.Ordinal)
        && ThoughtColumn(left) == ThoughtColumn(right);

    private static int ThoughtColumn(ThoughtOccurrence occurrence) => occurrence.X < 700 ? 0 : 1;

    private static IEnumerable<MaaCandidatePreview> MizukiKeyCandidates(IEnumerable<MaaTaskRunResult> taskResults)
    {
        foreach (var candidate in MizukiIngotCandidates(taskResults))
            yield return candidate;
        foreach (var candidate in MizukiNumericCandidates(taskResults, IsMizukiKeyValueEntry, "key", "鍵", 99, 0.82))
            yield return candidate;
    }

    private static IEnumerable<MaaCandidatePreview> MizukiIngotCandidates(IEnumerable<MaaTaskRunResult> taskResults)
    {
        var best = taskResults
            .Where(taskResult => taskResult.Succeeded && IsMizukiIngotValueEntry(taskResult.Entry))
            .SelectMany(taskResult => PrimaryTextResults(taskResult.RecognitionDetailJson))
            .Select(result => (Result: result, Value: MizukiIngotValue(result.Text)))
            .Where(item => item.Value is >= 0 and <= 9999)
            .OrderByDescending(item => item.Result.Confidence ?? 0)
            .FirstOrDefault();
        if (best.Value is null)
            yield break;

        yield return new MaaCandidatePreview(
            "runStatus",
            "源石錐",
            best.Value.Value.ToString(CultureInfo.InvariantCulture),
            best.Result.Text,
            Math.Max(0.84, best.Result.Confidence ?? 0),
            Field: "ingot",
            CampaignId: "is3_mizuki",
            RecognitionKey: "maa-local:ingot:is3.ingot_value");
    }

    private static int? MizukiIngotValue(string value)
    {
        var normalized = value
            .Normalize(NormalizationForm.FormKC)
            .Replace('z', '7')
            .Replace('Z', '7');
        return NumericValue(normalized);
    }

    private static IEnumerable<MaaCandidatePreview> MizukiLightAndHordeCandidates(IEnumerable<MaaTaskRunResult> taskResults)
    {
        foreach (var candidate in MizukiNumericCandidates(taskResults, IsMizukiLightValueEntry, "light", "灯火", 100, 0.82))
            yield return candidate;

        var effects = LoadSelectableEffects()
            .Where(effect => effect.CampaignId == "is3_mizuki" && effect.Slot == "hordeCall")
            .ToArray();
        var emitted = new HashSet<string>(StringComparer.Ordinal);
        var order = 0;
        foreach (var taskResult in taskResults)
        {
            if (!taskResult.Succeeded || !IsMizukiHordeCallEntry(taskResult.Entry))
                continue;

            foreach (var textResult in PrimaryTextResults(taskResult.RecognitionDetailJson))
            {
                foreach (var effect in effects)
                {
                    if (!MatchesMizukiHordeName(textResult.Text, effect.Name)
                        || !emitted.Add(effect.Id))
                    {
                        continue;
                    }

                    yield return new MaaCandidatePreview(
                        "mizuki",
                        effect.Name,
                        effect.Id,
                        textResult.Text,
                        Math.Max(0.70, textResult.Confidence ?? 0),
                        CampaignId: "is3_mizuki",
                        RecognitionKey: $"maa-local:mizuki:horde:{effect.Id}:{order}",
                        FieldId: "hordeCalls",
                        EffectId: effect.Id);
                    order++;
                }
            }
        }
    }

    private static string NormalizeMizukiHordeName(string value) =>
        NormalizeChoiceName(value)
            .Replace(":", "", StringComparison.Ordinal)
            .Replace("：", "", StringComparison.Ordinal);

    private static bool MatchesMizukiHordeName(string recognizedText, string effectName)
    {
        var normalized = NormalizeMizukiHordeName(recognizedText);
        var canonical = NormalizeMizukiHordeName(effectName);
        if (string.IsNullOrWhiteSpace(canonical))
            return false;
        if (normalized.Contains(canonical, StringComparison.Ordinal))
            return true;

        const string prefix = "呼び声";
        var suffix = canonical.StartsWith(prefix, StringComparison.Ordinal)
            ? canonical[prefix.Length..]
            : canonical;
        return suffix.Length >= 2
            && normalized.Contains("声", StringComparison.Ordinal)
            && normalized.Length <= 12
            && normalized.Contains(suffix, StringComparison.Ordinal);
    }

    private static IEnumerable<MaaCandidatePreview> MizukiRejectionCandidates(IEnumerable<MaaTaskRunResult> taskResults)
    {
        var effects = LoadSelectableEffects()
            .Where(effect => effect.CampaignId == "is3_mizuki" && effect.Slot == "rejectionReaction")
            .ToArray();
        var emittedEffects = new HashSet<string>(StringComparer.Ordinal);
        foreach (var taskResult in taskResults)
        {
            if (!taskResult.Succeeded
                || !IsMizukiRejectionNameEntry(taskResult.Entry))
            {
                continue;
            }

            foreach (var textResult in PrimaryTextResults(taskResult.RecognitionDetailJson))
            {
                var normalized = NormalizeMizukiRejectionName(textResult.Text);
                foreach (var effect in effects)
                {
                    var effectName = NormalizeChoiceName(effect.Name);
                    if (string.IsNullOrWhiteSpace(effectName)
                        || !normalized.Contains(effectName, StringComparison.Ordinal)
                        || !emittedEffects.Add(effect.Id))
                    {
                        continue;
                    }

                    yield return new MaaCandidatePreview(
                        "mizuki",
                        effect.Name,
                        effect.Id,
                        textResult.Text,
                        Math.Max(0.72, textResult.Confidence ?? 0),
                        CampaignId: "is3_mizuki",
                        RecognitionKey: $"maa-local:mizuki:rejection:{effect.Id}",
                        FieldId: "rejectionReaction",
                        EffectId: effect.Id);
                }
            }
        }

    }

    private static IEnumerable<MaaCandidatePreview> MizukiRejectionCardCandidates(
        IEnumerable<MaaTaskRunResult> taskResults)
    {
        var byId = RhodesRunCatalog.LoadDefault().Operators
            .Where(item => !string.IsNullOrWhiteSpace(item.Id))
            .ToDictionary(item => item.Id, StringComparer.Ordinal);
        var emitted = new HashSet<string>(StringComparer.Ordinal);
        foreach (var taskResult in taskResults)
        {
            if (!RhodesMizukiRejectionCardDetector.TryRead(
                    taskResult,
                    out var operatorId,
                    out var label,
                    out var score,
                    out var operatorInstance))
                continue;

            if (byId.TryGetValue(operatorId, out var op))
                label = op.Name;
            if (string.IsNullOrWhiteSpace(label))
                label = operatorId;
            var supportsMultipleCount = operatorId.StartsWith("reserve_", StringComparison.Ordinal)
                || label.StartsWith("予備隊員", StringComparison.Ordinal);
            operatorInstance = supportsMultipleCount ? Math.Max(1, operatorInstance) : 1;
            var targetKey = $"{operatorId}#{operatorInstance}";
            if (!emitted.Add(targetKey))
                continue;
            var displayLabel = supportsMultipleCount
                ? $"{label} {operatorInstance}人目"
                : label;

            yield return new MaaCandidatePreview(
                "mizuki",
                displayLabel,
                operatorId,
                "purple-name",
                score,
                OperatorId: operatorId,
                CampaignId: "is3_mizuki",
                RecognitionKey: $"maa-local:mizuki:rejection-card:{operatorId}:{operatorInstance}",
                FieldId: "rejectionReaction",
                OperatorInstance: operatorInstance);
        }
    }

    private static IEnumerable<MaaCandidatePreview> MizukiEvolutionCardCandidates(
        IEnumerable<MaaTaskRunResult> taskResults)
    {
        var byId = RhodesRunCatalog.LoadDefault().Operators
            .Where(item => !string.IsNullOrWhiteSpace(item.Id))
            .ToDictionary(item => item.Id, StringComparer.Ordinal);
        var emitted = new HashSet<string>(StringComparer.Ordinal);
        foreach (var taskResult in taskResults)
        {
            if (!RhodesMizukiRejectionCardDetector.TryReadEvolution(
                    taskResult,
                    out var operatorId,
                    out var label,
                    out var score,
                    out var operatorInstance))
                continue;

            if (byId.TryGetValue(operatorId, out var op))
                label = op.Name;
            if (string.IsNullOrWhiteSpace(label))
                label = operatorId;
            var supportsMultipleCount = operatorId.StartsWith("reserve_", StringComparison.Ordinal)
                || label.StartsWith("予備隊員", StringComparison.Ordinal);
            operatorInstance = supportsMultipleCount ? Math.Max(1, operatorInstance) : 1;
            var targetKey = $"{operatorId}#{operatorInstance}";
            if (!emitted.Add(targetKey))
                continue;
            var displayLabel = supportsMultipleCount
                ? $"{label} {operatorInstance}人目"
                : label;

            yield return new MaaCandidatePreview(
                "mizuki",
                displayLabel,
                operatorId,
                "yellow-name",
                score,
                OperatorId: operatorId,
                CampaignId: "is3_mizuki",
                RecognitionKey: $"maa-local:mizuki:evolution-card:{operatorId}:{operatorInstance}",
                FieldId: "operatorEvolution",
                OperatorInstance: operatorInstance);
        }
    }

    private static string NormalizeMizukiRejectionName(string value) =>
        NormalizeChoiceName(value)
            .Replace("障害と異空", "障害と異変", StringComparison.Ordinal);

    private static (SelectableEffectCandidate? Effect, double Score) TryResolveMizukiRejectionTemplate(
        MaaTaskRunResult taskResult,
        IReadOnlyList<SelectableEffectCandidate> effects)
    {
        var templateIds = RhodesMaaResourceCatalog.LoadCompositeTemplateIds(taskResult.Entry);
        if (templateIds.Count == 0
            || !RhodesMaaCompositeTemplateResult.TryReadFirstHit(taskResult.RecognitionDetailJson, out var hit)
            || hit.Index < 0
            || hit.Index >= templateIds.Count)
        {
            return (null, 0);
        }

        var effectId = templateIds[hit.Index];
        return (effects.FirstOrDefault(effect => effect.Id.Equals(effectId, StringComparison.Ordinal)), hit.Score);
    }

    private static IEnumerable<MaaCandidatePreview> MizukiNumericCandidates(
        IEnumerable<MaaTaskRunResult> taskResults,
        Func<string, bool> entryPredicate,
        string fieldId,
        string label,
        int maximum,
        double minimumConfidence)
    {
        var best = taskResults
            .Where(taskResult => taskResult.Succeeded && entryPredicate(taskResult.Entry))
            .SelectMany(taskResult => PrimaryTextResults(taskResult.RecognitionDetailJson))
            .Select(result => (
                Result: result,
                Value: NumericValue(
                    result.Text,
                    allowLetterEAsEight: fieldId.Equals("light", StringComparison.Ordinal))))
            .Where(item => item.Value is >= 0 && item.Value <= maximum)
            .OrderByDescending(item => item.Result.Confidence ?? 0)
            .FirstOrDefault();
        if (best.Value is null)
            yield break;

        yield return new MaaCandidatePreview(
            "mizuki",
            label,
            best.Value.Value.ToString(CultureInfo.InvariantCulture),
            best.Result.Text,
            Math.Max(minimumConfidence, best.Result.Confidence ?? 0),
            CampaignId: "is3_mizuki",
            RecognitionKey: $"maa-local:mizuki:{fieldId}",
            FieldId: fieldId);
    }

    private static IEnumerable<MaaCandidatePreview> RevelationCandidates(IEnumerable<MaaTaskRunResult> taskResults)
    {
        var effects = LoadSelectableEffects()
            .Where(effect => effect.Slot == "revelationBoard" && effect.CampaignId == "is4_sami")
            .Select(effect => (Effect: effect, SlotKind: RevelationSlotKind(effect.GroupLabel)))
            .Where(item => !string.IsNullOrWhiteSpace(item.SlotKind))
            .ToArray();
        var byNormalizedName = effects
            .GroupBy(item => NormalizeChoiceName(item.Effect.Name), StringComparer.Ordinal)
            .Where(group => !string.IsNullOrWhiteSpace(group.Key) && group.Count() == 1)
            .ToDictionary(group => group.Key, group => group.Single(), StringComparer.Ordinal);
        var order = 0;

        foreach (var taskResult in taskResults)
        {
            if (!taskResult.Succeeded || !IsRevelationNameEntry(taskResult.Entry))
                continue;

            foreach (var textResult in PrimaryTextResults(taskResult.RecognitionDetailJson))
            {
                foreach (var token in ChoiceNameTokens(textResult.Text))
                {
                    if (!byNormalizedName.TryGetValue(token.Normalized, out var matched))
                        continue;

                    yield return new MaaCandidatePreview(
                        "revelation",
                        matched.Effect.Name,
                        matched.Effect.Id,
                        token.Raw,
                        Math.Max(0.68, textResult.Confidence ?? 0),
                        CampaignId: matched.Effect.CampaignId,
                        RecognitionKey: $"maa-local:revelation:{matched.Effect.Id}:{order}",
                        FieldId: "revelation",
                        SlotKind: matched.SlotKind,
                        EffectId: matched.Effect.Id,
                        Count: 1);
                    order++;
                }
            }
        }
    }

    private static IEnumerable<MaaCandidatePreview> CoinCandidates(
        IEnumerable<MaaTaskRunResult> taskResults,
        string fieldId)
    {
        var results = taskResults as MaaTaskRunResult[] ?? taskResults.ToArray();
        var merged = new Dictionary<string, MaaCandidatePreview>(StringComparer.Ordinal);
        var activeSlotOcrResults = fieldId.Equals("activeCoins", StringComparison.Ordinal)
            ? results
                .Where(result => result.Succeeded && IsActiveCoinSlotOcrEntry(result.Entry))
                .ToArray()
            : [];
        var preferActiveSlotOcr = activeSlotOcrResults.Length > 0;
        var imageEntry = fieldId.Equals("activeCoins", StringComparison.Ordinal)
            ? RhodesSuiCoinImageRecognizer.ActiveEntry
            : RhodesSuiCoinImageRecognizer.OwnedEntry;
        var imageResults = (preferActiveSlotOcr ? [] : results)
            .Where(result => result.Entry.Equals(imageEntry, StringComparison.Ordinal))
            .Select(result => RhodesSuiCoinImageRecognizer.TryRead(result, out var detectedFieldId, out var detections)
                && detectedFieldId.Equals(fieldId, StringComparison.Ordinal)
                    ? detections
                    : null)
            .Where(detections => detections is not null)
            .Cast<IReadOnlyList<RhodesSuiCoinImageDetection>>()
            .ToArray();
        var emittedCoinIds = new HashSet<string>(StringComparer.Ordinal);
        if (imageResults.Length > 0)
        {
            var frameGroups = (fieldId.Equals("activeCoins", StringComparison.Ordinal)
                    ? imageResults.TakeLast(1)
                    : imageResults)
                .SelectMany((detections, frameIndex) => detections
                    .GroupBy(detection => $"{detection.CoinId}\u001f{detection.StatusId}", StringComparer.Ordinal)
                    .Select(group => new
                    {
                        Key = group.Key,
                        FrameIndex = frameIndex,
                        Count = group.Count(),
                        Best = group.OrderByDescending(item => item.Score).First(),
                        Slots = string.Join(",", group.OrderBy(item => item.SlotIndex).Select(item => $"slot{item.SlotIndex + 1}")),
                    }))
                .ToArray();

            foreach (var group in frameGroups.GroupBy(item => item.Key, StringComparer.Ordinal))
            {
                var strongest = group.OrderByDescending(item => item.Best.Score).First();
                var mostVisible = group
                    .OrderByDescending(item => item.Count)
                    .ThenByDescending(item => item.Best.Score)
                    .First();
                var detection = strongest.Best;
                var candidate = new MaaCandidatePreview(
                    "coin",
                    detection.Label,
                    detection.CoinId,
                    mostVisible.Slots,
                    detection.Score,
                    CampaignId: "is6_sui",
                    RecognitionKey: $"maa-local:coin-image:{fieldId}:{detection.CoinId}:{detection.StatusId}",
                    FieldId: fieldId,
                    CoinId: detection.CoinId,
                    StatusId: detection.StatusId,
                    Count: mostVisible.Count);
                merged[group.Key] = candidate;
                emittedCoinIds.Add(detection.CoinId);
            }
            if (fieldId.Equals("coins", StringComparison.Ordinal))
                ReconcileOwnedCoinStatusObservations(merged, imageResults);
        }

        var coins = LoadSelectableEffects()
            .Where(effect => effect.Slot == "coin" && effect.CampaignId == "is6_sui")
            .ToArray();
        var byNormalizedName = coins
            .GroupBy(item => NormalizeChoiceName(item.Name), StringComparer.Ordinal)
            .Where(group => !string.IsNullOrWhiteSpace(group.Key) && group.Count() == 1)
            .ToDictionary(group => group.Key, group => group.Single(), StringComparer.Ordinal);
        var coinStatuses = fieldId.Equals("activeCoins", StringComparison.Ordinal)
            ? LoadSelectableEffects()
                .Where(effect => effect.Slot == "coinStatus" && effect.CampaignId == "is6_sui")
                .ToArray()
            : [];
        var byNormalizedStatusName = coinStatuses
            .GroupBy(item => NormalizeChoiceName(item.Name), StringComparer.Ordinal)
            .Where(group => !string.IsNullOrWhiteSpace(group.Key) && group.Count() == 1)
            .ToDictionary(group => group.Key, group => group.Single(), StringComparer.Ordinal);
        var order = 0;

        var ocrResults = preferActiveSlotOcr ? activeSlotOcrResults : results;
        foreach (var taskResult in ocrResults)
        {
            if (!taskResult.Succeeded || !IsCoinNameEntry(taskResult.Entry, fieldId))
                continue;
            var activeSlotOcr = fieldId.Equals("activeCoins", StringComparison.Ordinal)
                && IsActiveCoinSlotOcrEntry(taskResult.Entry);

            var textResults = PrimaryTextResults(taskResult.RecognitionDetailJson)
                .OrderBy(result => result.Y)
                .ThenBy(result => result.X)
                .ToArray();
            var headingRows = textResults
                .Where(result => LooksLikeCoinHeading(result.Text, byNormalizedName))
                .Select(result => result.Y)
                .Distinct()
                .OrderBy(y => y)
                .ToArray();
            var resolvedCoinRows = new List<(SelectableEffectCandidate Coin, string Raw, double Confidence, int Y)>();
            foreach (var textResult in textResults)
            {
                var resolved = ChoiceNameTokens(textResult.Text)
                    .Select(token => new
                    {
                        Token = token,
                        Coin = ResolveCoin(token.Normalized, byNormalizedName)
                            ?? (fieldId.Equals("activeCoins", StringComparison.Ordinal)
                                ? ResolveDirectionalActiveCoin(
                                    token.Normalized,
                                    textResult,
                                    textResults,
                                    coins,
                                    byNormalizedName)
                                : null),
                    })
                    .FirstOrDefault(item => item.Coin is not null);
                if (resolved?.Coin is not null)
                {
                    if (fieldId.Equals("activeCoins", StringComparison.Ordinal)
                        && !LooksLikeActiveCoinNameRow(
                            resolved.Token.Normalized,
                            NormalizeChoiceName(resolved.Coin.Name)))
                    {
                        continue;
                    }
                    resolvedCoinRows.Add((
                        resolved.Coin,
                        resolved.Token.Raw,
                        Math.Max(0.68, textResult.Confidence ?? 0),
                        textResult.Y));
                }
            }

            var statusRows = byNormalizedStatusName.Count == 0
                ? []
                : textResults
                    .Select(result => new
                    {
                        Result = result,
                        Status = ResolveCoinStatus(NormalizeChoiceName(result.Text), byNormalizedStatusName),
                    })
                    .Where(item => item.Status is not null)
                    .ToArray();
            var frameMatches = resolvedCoinRows
                .Select(match =>
                {
                    var nextY = headingRows
                        .Where(y => y > match.Y)
                        .DefaultIfEmpty(int.MaxValue)
                        .Min();
                    var status = statusRows
                        .Where(item => item.Result.Y > match.Y && item.Result.Y < nextY)
                        .OrderBy(item => item.Result.Y)
                        .Select(item => item.Status)
                        .FirstOrDefault();
                    return (
                        match.Coin,
                        match.Raw,
                        match.Confidence,
                        StatusId: status?.Id ?? "");
                })
                .ToArray();

            foreach (var frameGroup in frameMatches.GroupBy(
                         item => $"{item.Coin.Id}\u001f{item.StatusId}",
                         StringComparer.Ordinal))
            {
                var best = frameGroup.OrderByDescending(item => item.Confidence).First();
                var key = frameGroup.Key;
                var candidate = new MaaCandidatePreview(
                        "coin",
                        best.Coin.Name,
                        best.Coin.Id,
                        best.Raw,
                        best.Confidence,
                        CampaignId: best.Coin.CampaignId,
                        RecognitionKey: $"maa-local:coin:{best.Coin.Id}:{order}",
                        FieldId: fieldId,
                        CoinId: best.Coin.Id,
                        StatusId: best.StatusId,
                        Count: activeSlotOcr ? 1 : frameGroup.Count());
                if (merged.TryGetValue(key, out var existing))
                {
                    var preferred = (candidate.Confidence ?? 0) > (existing.Confidence ?? 0) ? candidate : existing;
                    merged[key] = preferred with
                    {
                        Confidence = Math.Max(existing.Confidence ?? 0, candidate.Confidence ?? 0),
                        Count = activeSlotOcr
                            ? existing.Count + candidate.Count
                            : Math.Max(existing.Count, candidate.Count),
                    };
                }
                else if (!emittedCoinIds.Contains(best.Coin.Id))
                {
                    merged[key] = candidate;
                }
                order++;
            }
        }

        foreach (var candidate in merged.Values)
            yield return candidate;
    }

    private static bool LooksLikeActiveCoinNameRow(string normalizedText, string normalizedCoinName)
    {
        if (string.IsNullOrWhiteSpace(normalizedCoinName))
            return false;

        // Active-coin OCR includes each card's description. Descriptions often mention
        // another coin (notably "大炎通宝に変化する"), so only accept the compact
        // heading row: the coin name plus a short status/type prefix.
        return normalizedText.Length <= normalizedCoinName.Length + 3;
    }

    private static SelectableEffectCandidate? ResolveDirectionalActiveCoin(
        string normalizedHeading,
        OcrTextResult headingRow,
        IReadOnlyList<OcrTextResult> textResults,
        IReadOnlyList<SelectableEffectCandidate> coins,
        IReadOnlyDictionary<string, SelectableEffectCandidate> byNormalizedName)
    {
        if (!LooksLikeCatchWindHeading(normalizedHeading))
            return null;

        var nextHeadingY = textResults
            .Where(row => row.Y > headingRow.Y)
            .Where(row => LooksLikeCoinHeading(row.Text, byNormalizedName))
            .Select(row => row.Y)
            .DefaultIfEmpty(int.MaxValue)
            .Min();
        var descriptionRows = textResults
            .Where(row => row.Y > headingRow.Y && row.Y < nextHeadingY)
            .Select(row => row.Text)
            .ToArray();
        var description = NormalizeChoiceName(string.Join(
            " ",
            descriptionRows));
        var direction = new[] { "左", "右", "上", "下" }
            .FirstOrDefault(value => description.Contains($"{value}向", StringComparison.Ordinal));
        if (string.IsNullOrWhiteSpace(direction)
            && description.Contains("配置", StringComparison.Ordinal)
            && (description.Contains("方向", StringComparison.Ordinal)
                || description.Contains("攻撃速", StringComparison.Ordinal)))
        {
            var uniqueDirections = new[] { "左", "右", "上", "下" }
                .Where(description.Contains)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (uniqueDirections.Length == 1)
                direction = uniqueDirections[0];
        }
        if (string.IsNullOrWhiteSpace(direction))
            return null;

        var directionalName = NormalizeChoiceName($"捕風（{direction}）");
        return coins.FirstOrDefault(coin =>
            NormalizeChoiceName(coin.Name).Equals(directionalName, StringComparison.Ordinal));
    }

    private static bool LooksLikeCoinHeading(
        string text,
        IReadOnlyDictionary<string, SelectableEffectCandidate> byNormalizedName)
    {
        foreach (var token in ChoiceNameTokens(text))
        {
            if (LooksLikeCatchWindHeading(token.Normalized))
                return true;

            var coin = ResolveCoin(token.Normalized, byNormalizedName);
            if (coin is not null
                && LooksLikeActiveCoinNameRow(token.Normalized, NormalizeChoiceName(coin.Name)))
            {
                return true;
            }
        }
        return false;
    }

    private static bool LooksLikeCatchWindHeading(string normalized) =>
        normalized.Contains("捕風", StringComparison.Ordinal)
        && normalized.Length <= NormalizeChoiceName("捕風").Length + 3;

    private static void ReconcileOwnedCoinStatusObservations(
        IDictionary<string, MaaCandidatePreview> merged,
        IReadOnlyList<IReadOnlyList<RhodesSuiCoinImageDetection>> imageResults)
    {
        var coinIds = imageResults
            .SelectMany(frame => frame)
            .Select(detection => detection.CoinId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        foreach (var coinId in coinIds)
        {
            var maximumVisibleCount = imageResults
                .Select(frame => frame.Count(detection => detection.CoinId.Equals(coinId, StringComparison.Ordinal)))
                .DefaultIfEmpty(0)
                .Max();
            var observations = merged
                .Where(pair => pair.Value.CoinId.Equals(coinId, StringComparison.Ordinal))
                .ToArray();
            if (maximumVisibleCount <= 0 || observations.Length == 0)
                continue;

            foreach (var observation in observations)
                merged.Remove(observation.Key);

            var remaining = maximumVisibleCount;
            foreach (var observation in observations
                         .Where(pair => !string.IsNullOrWhiteSpace(pair.Value.StatusId))
                         .OrderByDescending(pair => pair.Value.Confidence ?? 0))
            {
                var count = Math.Min(Math.Max(1, observation.Value.Count), remaining);
                if (count <= 0)
                    continue;
                merged[observation.Key] = observation.Value with { Count = count };
                remaining -= count;
            }

            if (remaining <= 0)
                continue;
            var plain = observations
                .Where(pair => string.IsNullOrWhiteSpace(pair.Value.StatusId))
                .OrderByDescending(pair => pair.Value.Confidence ?? 0)
                .Select(pair => pair.Value)
                .FirstOrDefault();
            if (plain is not null)
                merged[$"{coinId}\u001f"] = plain with { Count = remaining };
        }
    }

    public static IReadOnlyList<RhodesSuiCoinOcrMatch> ResolveSuiCoinOcrMatches(
        MaaTaskRunResult taskResult)
    {
        if (!taskResult.Succeeded || !IsCoinNameEntry(taskResult.Entry))
            return [];

        var coins = LoadSelectableEffects()
            .Where(effect => effect.Slot == "coin" && effect.CampaignId == "is6_sui")
            .ToArray();
        var byNormalizedName = coins
            .GroupBy(item => NormalizeChoiceName(item.Name), StringComparer.Ordinal)
            .Where(group => !string.IsNullOrWhiteSpace(group.Key) && group.Count() == 1)
            .ToDictionary(group => group.Key, group => group.Single(), StringComparer.Ordinal);
        var matches = new List<RhodesSuiCoinOcrMatch>();
        foreach (var textResult in PrimaryTextResults(taskResult.RecognitionDetailJson))
        {
            if (textResult.X < 0 || textResult.Y < 0 || textResult.Width <= 0 || textResult.Height <= 0)
                continue;

            var resolved = ChoiceNameTokens(textResult.Text)
                .Select(token => new
                {
                    Token = token,
                    Coin = ResolveCoin(token.Normalized, byNormalizedName),
                })
                .FirstOrDefault(item => item.Coin is not null);
            if (resolved?.Coin is null)
                continue;

            matches.Add(new RhodesSuiCoinOcrMatch(
                resolved.Coin.Id,
                resolved.Coin.Name,
                resolved.Token.Raw,
                Math.Max(0.68, textResult.Confidence ?? 0),
                new MaaRoi(textResult.X, textResult.Y, textResult.Width, textResult.Height)));
        }
        return matches;
    }

    private static SelectableEffectCandidate? ResolveCoin(
        string normalized,
        IReadOnlyDictionary<string, SelectableEffectCandidate> byNormalizedName)
    {
        if (normalized.Length < 2)
            return null;
        if (byNormalizedName.TryGetValue(normalized, out var exact))
            return exact;

        foreach (var alias in CoinOcrSuffixAliases)
        {
            if (normalized.EndsWith(alias.Key, StringComparison.Ordinal)
                && byNormalizedName.TryGetValue(NormalizeChoiceName(alias.Value), out var aliasedCoin))
            {
                return aliasedCoin;
            }
        }

        var shortSuffix = byNormalizedName
            .Where(item => item.Key.Length == 2 && normalized.EndsWith(item.Key, StringComparison.Ordinal))
            .Take(2)
            .ToArray();
        if (shortSuffix.Length == 1)
            return shortSuffix[0].Value;

        // The status badge overlaps the first glyphs of this long coin name. MAA-OCR
        // consistently keeps the distinctive middle phrase but can replace 遂 with 違
        // and collapse the final 欲す into one glyph. Use that stable anchor instead of
        // globally relaxing edit distance for every coin name.
        if ((normalized.Contains("志遂げん", StringComparison.Ordinal)
                || normalized.Contains("志違げん", StringComparison.Ordinal))
            && byNormalizedName.TryGetValue(NormalizeChoiceName("志遂げんと欲す"), out var ambitionCoin))
        {
            return ambitionCoin;
        }

        var contained = byNormalizedName
            .Where(item => item.Key.Length >= 3 && normalized.Contains(item.Key, StringComparison.Ordinal))
            .OrderByDescending(item => item.Key.Length)
            .Take(2)
            .ToArray();
        if (contained.Length > 0
            && (contained.Length == 1 || contained[0].Key.Length > contained[1].Key.Length))
        {
            return contained[0].Value;
        }

        var fuzzy = byNormalizedName
            .Where(item => item.Key.Length >= 4)
            .Select(item => new
            {
                item.Key,
                item.Value,
                Distance = BestSubstringEditDistance(normalized, item.Key),
            })
            .Where(item => item.Distance <= (item.Key.Length <= 5 ? 1 : 2))
            .OrderBy(item => item.Distance)
            .ThenByDescending(item => item.Key.Length)
            .Take(2)
            .ToArray();
        return fuzzy.Length > 0
            && (fuzzy.Length == 1 || fuzzy[0].Distance < fuzzy[1].Distance)
                ? fuzzy[0].Value
                : null;
    }

    private static bool IsOperatorNameEntry(string entry)
    {
        return entry.Equals("RhodesOperatorNameOcr", StringComparison.Ordinal)
            || entry.Equals("RhodesOcrRegion_operator_name", StringComparison.Ordinal)
            || entry.StartsWith("RhodesOcrRegion_operator_name_", StringComparison.Ordinal)
            || entry.Contains("operator.card.name", StringComparison.OrdinalIgnoreCase)
            || entry.Contains("operator.recruit.name", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRelicNameEntry(string entry)
    {
        return entry.Equals("RhodesOcrRegion_relic_list_text", StringComparison.Ordinal)
            || entry.Equals("RhodesOcrRegion_relic_detail_name", StringComparison.Ordinal)
            || entry.Contains("relic.list_text", StringComparison.OrdinalIgnoreCase)
            || entry.Contains("relic.detail_name", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAgeEntry(string entry)
    {
        return entry.Equals("RhodesOcrRegion_is5_age_detail_text", StringComparison.Ordinal)
            || entry.Equals("RhodesScreen_run_sarkaz_age_detail", StringComparison.Ordinal)
            || entry.Contains("is5.age_detail_text", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSuiSeasonalHourEntry(string entry)
    {
        return entry.Equals("RhodesOcrRegion_is6_seasonal_hour_detail_text", StringComparison.Ordinal)
            || entry.Contains("is6.seasonal_hour_detail_text", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsHallucinationEntry(string entry)
    {
        return entry.Equals("RhodesOcrRegion_is2_hallucination_top", StringComparison.Ordinal)
            || entry.Contains("is2.hallucination_top", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPerformanceEntry(string entry)
    {
        return entry.Equals("RhodesOcrRegion_is2_performance_bottom", StringComparison.Ordinal)
            || entry.Contains("is2.performance_bottom", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMizukiKeyValueEntry(string entry)
    {
        return entry.Equals("RhodesOcrRegion_is3_key_value", StringComparison.Ordinal)
            || entry.Contains("is3.key_value", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMizukiIngotValueEntry(string entry)
    {
        return entry.Equals("RhodesOcrRegion_is3_ingot_value", StringComparison.Ordinal)
            || entry.Contains("is3.ingot_value", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMizukiLightValueEntry(string entry)
    {
        return entry.Equals("RhodesOcrRegion_is3_light_value", StringComparison.Ordinal)
            || entry.Contains("is3.light_value", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMizukiHordeCallEntry(string entry)
    {
        return entry.Equals("RhodesOcrRegion_is3_horde_call_list_text", StringComparison.Ordinal)
            || entry.Contains("is3.horde_call_list_text", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMizukiRejectionNameEntry(string entry)
    {
        return entry.Equals("RhodesOcrRegion_is3_rejection_name", StringComparison.Ordinal)
            || entry.Contains("is3.rejection_name", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMizukiRejectionTemplateEntry(string entry)
    {
        return entry.Equals("RhodesTemplate_is3RejectionFull_is3_rejection_icon_batch", StringComparison.Ordinal)
            || entry.Contains("is3.rejection.icon.batch", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsThoughtNameEntry(string entry)
    {
        return entry.Equals("RhodesOcrRegion_is5_thought_list_text", StringComparison.Ordinal)
            || entry.Contains("is5.thought_list_text", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsThoughtTotalLoadEntry(string entry) =>
        entry.Equals("RhodesOcrRegion_is5_thought_load_current", StringComparison.Ordinal)
        || entry.Contains("is5.thought_load.current", StringComparison.OrdinalIgnoreCase);

    private static string ThoughtIdFromLoadEntry(string entry)
    {
        const string prefix = "thought.card.load.";
        return entry.StartsWith(prefix, StringComparison.Ordinal)
            ? entry[prefix.Length..]
            : "";
    }

    private static bool IsRevelationNameEntry(string entry)
    {
        return entry.Equals("RhodesOcrRegion_is4_revelation_list_text", StringComparison.Ordinal)
            || entry.Contains("is4.revelation_list_text", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCoinNameEntry(string entry)
    {
        return entry.Equals("RhodesOcrRegion_is6_coin_list_text", StringComparison.Ordinal)
            || entry.Contains("is6.coin_list_text", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCoinNameEntry(string entry, string fieldId)
    {
        if (fieldId.Equals("activeCoins", StringComparison.Ordinal))
        {
            return entry.Equals("RhodesOcrRegion_is6_active_coin_list_text", StringComparison.Ordinal)
                || entry.Contains("is6.active_coin_list_text", StringComparison.OrdinalIgnoreCase);
        }

        return IsCoinNameEntry(entry);
    }

    private static bool IsActiveCoinSlotOcrEntry(string entry) =>
        entry.StartsWith("RhodesDynamic_is6.active_coin_list_text.slot", StringComparison.Ordinal);

    private static bool IsSquadNameEntry(string entry)
    {
        return entry.Equals("RhodesOcrRegion_run_squad_name", StringComparison.Ordinal)
            || entry.Contains("run.squad_name", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSquadCardEntry(string entry)
    {
        return entry.Equals("RhodesOcrRegion_run_squad_card", StringComparison.Ordinal)
            || entry.Contains("run.squad_card", StringComparison.OrdinalIgnoreCase);
    }

    private static string RevelationSlotKind(string groupLabel)
    {
        if (groupLabel.Contains("本因", StringComparison.Ordinal))
            return "cause";
        if (groupLabel.Contains("構成", StringComparison.Ordinal))
            return "structure";
        if (groupLabel.Contains("修辞", StringComparison.Ordinal))
            return "rhetoric";
        return "";
    }

    private static IEnumerable<string> AgeAliases(SelectableEffectCandidate effect)
    {
        return new[]
            {
                effect.Name,
                $"{effect.ParentName}{effect.VariantLabel}",
                $"{effect.GroupLabel}{effect.VariantLabel}",
            }
            .Select(NormalizeChoiceName)
            .Where(alias => alias.Length >= 4)
            .Distinct(StringComparer.Ordinal);
    }

    private static IEnumerable<(string Raw, string Normalized)> ChoiceNameTokens(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            yield break;

        var whole = NormalizeChoiceName(value);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        if (whole.Length >= 2)
        {
            seen.Add(whole);
            yield return (value.Trim(), whole);
        }

        var parts = value.Split(
            [' ', '\t', '\r', '\n', '　', ',', '，', '、', '。', ';', '；', ':', '：', '/', '\\', '|'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
            parts = [value.Trim()];

        foreach (var part in parts)
        {
            var normalized = NormalizeChoiceName(part);
            if (normalized.Length >= 2 && seen.Add(normalized))
                yield return (part.Trim(), normalized);
        }
    }

    private static string NormalizeChoiceName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        var normalized = value.Trim().Normalize(NormalizationForm.FormKC);
        var chars = new List<char>();
        foreach (var ch in normalized)
        {
            if (char.IsWhiteSpace(ch))
                continue;

            if (ch is '・' or '･' or '「' or '」' or '『' or '』' or '【' or '】' or '[' or ']' or '(' or ')' or '（' or '）')
                continue;

            chars.Add(ch);
        }
        return new string(chars.ToArray());
    }

    private static string NormalizeSquadEffectText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        var normalized = value.Trim().Normalize(NormalizationForm.FormKC);
        var chars = new List<char>();
        foreach (var ch in normalized)
        {
            if (char.IsWhiteSpace(ch))
                continue;

            if (ch is '「' or '」' or '『' or '』' or '【' or '】' or '[' or ']' or '(' or ')' or '（' or '）'
                or '・' or '･' or '：' or ':' or '．' or '.' or ',' or '，' or '、' or '。' or ';' or '；')
            {
                continue;
            }

            chars.Add(ch switch
            {
                '＋' => '+',
                '−' or '－' or '–' or '—' => '-',
                _ => char.ToLowerInvariant(ch),
            });
        }

        return new string(chars.ToArray());
    }

    private static IReadOnlyList<string> OptionEffectPhrases(string effect)
    {
        return effect
            .Split(['。', '、', '，', ',', '；', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeSquadEffectText)
            .Where(part => part.Length >= 6)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<string> OptionEffectTokens(string effect)
    {
        var bracketTokens = Regex.Matches(effect, "[【「]([^】」]+)[】」]")
            .Select(match => NormalizeSquadEffectText(match.Groups[1].Value))
            .Where(part => part.Length >= 2);
        var normalized = NormalizeSquadEffectText(effect);
        var numericTokens = Regex.Matches(normalized, "[★]?[0-9]+%?|[+-][0-9]+")
            .Select(match => match.Value)
            .Where(part => part.Length >= 2);

        return bracketTokens.Concat(numericTokens)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static (RunSquadRandomEffectOption Option, int Score, int Matches)? ScoreRandomEffectOption(
        string normalizedText,
        RunSquadRandomEffectOption option)
    {
        var normalizedEffect = NormalizeSquadEffectText(option.Effect);
        if (string.IsNullOrWhiteSpace(normalizedEffect))
            return null;

        var score = 0;
        var matches = 0;
        if (normalizedText.Contains(normalizedEffect, StringComparison.Ordinal))
        {
            score += 120;
            matches += 3;
        }

        foreach (var phrase in OptionEffectPhrases(option.Effect))
        {
            if (!normalizedText.Contains(phrase, StringComparison.Ordinal))
                continue;

            score += Math.Min(32, Math.Max(10, phrase.Length / 2));
            matches += 1;
        }

        foreach (var token in OptionEffectTokens(option.Effect))
        {
            if (!normalizedText.Contains(token, StringComparison.Ordinal))
                continue;

            score += token.Length >= 4 ? 18 : 8;
            matches += 1;
        }

        return matches == 0 || score < 20 ? null : (option, score, matches);
    }

    private static (RunSquadRandomEffectOption Option, int Score, int Matches)? FindRandomEffectOption(
        string normalizedText,
        IReadOnlyList<RunSquadRandomEffectOption> options)
    {
        var scored = options
            .Select(option => ScoreRandomEffectOption(normalizedText, option))
            .Where(item => item is not null)
            .Select(item => item!.Value)
            .OrderByDescending(item => item.Score)
            .ThenByDescending(item => item.Matches)
            .ToArray();
        if (scored.Length == 0)
            return null;
        if (scored.Length > 1 && scored[0].Score == scored[1].Score && scored[0].Matches == scored[1].Matches)
            return null;

        return scored[0];
    }

    private static OcrTextResult PrimaryTextResult(string value)
    {
        var results = PrimaryTextResults(value);
        if (results.Count > 0)
            return results[0];

        return new OcrTextResult("", null, -1, -1, 0, 0);
    }

    private static IReadOnlyList<OcrTextResult> PrimaryTextResults(string value)
    {
        using var document = ParseRecognitionDetail(value);
        if (document is null)
            return [];

        var root = document.RootElement;
        if (root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty("result", out var nested)
            && nested.ValueKind == JsonValueKind.Object)
        {
            root = nested;
        }

        var results = new List<OcrTextResult>();
        foreach (var item in PrimaryResults(root))
        {
            if (item.ValueKind != JsonValueKind.Object)
                continue;

            var text = JsonString(item, "text");
            if (!string.IsNullOrWhiteSpace(text))
            {
                var box = JsonBox(item);
                results.Add(new OcrTextResult(
                    text.Trim(),
                    JsonNumber(item, "score") ?? JsonNumber(item, "confidence") ?? JsonNumber(item, "prob"),
                    box.X,
                    box.Y,
                    box.Width,
                    box.Height));
            }
        }

        return results;
    }

    private static (int X, int Y, int Width, int Height) JsonBox(JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Object
            || !item.TryGetProperty("box", out var box)
            || box.ValueKind != JsonValueKind.Array)
        {
            return (-1, -1, 0, 0);
        }

        var values = box.EnumerateArray().Take(4).ToArray();
        return values.Length == 4
            && values.All(value => value.TryGetInt32(out _))
            ? (values[0].GetInt32(), values[1].GetInt32(), values[2].GetInt32(), values[3].GetInt32())
            : (-1, -1, 0, 0);
    }

    private static JsonDocument? ParseRecognitionDetail(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var text = value.Trim();
        var jsonStart = text.IndexOf('{');
        if (jsonStart < 0)
            return null;

        try
        {
            return JsonDocument.Parse(text[jsonStart..]);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static IReadOnlyList<JsonElement> PrimaryResults(JsonElement detail)
    {
        var filtered = ResultArray(detail, "filtered", "filtered_results", "filteredResults");
        if (filtered.Count > 0)
            return filtered;

        if (FirstObject(detail, "best", "best_result", "bestResult") is { } best)
            return [best];

        return ResultArray(detail, "all", "all_results", "allResults");
    }

    private static List<JsonElement> ResultArray(JsonElement detail, params string[] names)
    {
        var results = new List<JsonElement>();
        if (detail.ValueKind != JsonValueKind.Object)
            return results;

        foreach (var name in names)
        {
            if (!detail.TryGetProperty(name, out var property))
                continue;

            if (property.ValueKind == JsonValueKind.Array)
            {
                results.AddRange(property.EnumerateArray());
                return results;
            }

            if (property.ValueKind == JsonValueKind.Object)
            {
                results.Add(property);
                return results;
            }
        }
        return results;
    }

    private static JsonElement? FirstObject(JsonElement detail, params string[] names)
    {
        if (detail.ValueKind != JsonValueKind.Object)
            return null;

        foreach (var name in names)
        {
            if (detail.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.Object)
                return property;
        }
        return null;
    }

    private static SelectableEffectCandidate? ResolveCoinStatus(
        string normalized,
        IReadOnlyDictionary<string, SelectableEffectCandidate> byNormalizedName)
    {
        var contained = byNormalizedName
            .Where(item => normalized.Contains(item.Key, StringComparison.Ordinal))
            .OrderByDescending(item => item.Key.Length)
            .Take(2)
            .ToArray();
        return contained.Length == 1 || (contained.Length > 1 && contained[0].Key.Length > contained[1].Key.Length)
            ? contained[0].Value
            : null;
    }

    private static int? NumericValue(
        string value,
        bool allowRoman = false,
        bool allowLetterEAsEight = false)
    {
        var text = NormalizeDigits(value, allowRoman, allowLetterEAsEight);
        if (string.IsNullOrWhiteSpace(text))
            return null;

        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
            ? number
            : null;
    }

    private static int? RunStatusNumericValue(string regionId, string value)
    {
        var trimmed = value.Trim().Normalize(NormalizationForm.FormKC);
        if (regionId.Equals("is6.ticket_value", StringComparison.Ordinal)
            && trimmed.Equals("E", StringComparison.OrdinalIgnoreCase))
        {
            return 6;
        }

        return NumericValue(value, allowRoman: true);
    }

    private static int? DifficultyValue(string value, int maximum)
    {
        var raw = value.Trim();
        var normalized = NormalizeDigits(value, allowRoman: true);
        if (maximum == 18
            && normalized.Length == 1
            && raw.StartsWith('^')
            && normalized[0] is >= '0' and <= '8')
        {
            normalized = $"1{normalized}";
        }
        if (!int.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
            return null;

        // Difficulty-18 themes can render the narrow leading 1 as 7,
        // so repair only the otherwise impossible two-digit 70-78 range.
        if (maximum == 18 && normalized.Length == 2 && number is >= 70 and <= 78)
            return number - 60;

        return number;
    }

    private static string NormalizeDigits(
        string value,
        bool allowRoman,
        bool allowLetterEAsEight = false)
    {
        var chars = new List<char>();
        foreach (var ch in value)
        {
            if (ch is >= '0' and <= '9')
            {
                chars.Add(ch);
                continue;
            }

            if (ch is >= '０' and <= '９')
            {
                chars.Add((char)('0' + (ch - '０')));
                continue;
            }

            if (ch is 'O' or 'o' or 'Ｏ' or 'ｏ' or 'U' or 'u' or 'Ｕ' or 'ｕ')
            {
                chars.Add('0');
                continue;
            }

            if (ch is '図')
            {
                chars.Add('2');
                continue;
            }

            if (ch is 'イ' or 'ィ' or '亻')
            {
                chars.Add('1');
                continue;
            }

            if (allowLetterEAsEight && ch is 'E' or 'e' or 'Ｅ' or 'ｅ')
            {
                chars.Add('8');
                continue;
            }

            if (allowRoman && ch is 'I' or 'i' or 'L' or 'l' or '一' or '丨')
                chars.Add('1');
        }
        return new string(chars.ToArray());
    }

    private static string JsonString(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? ""
            : "";
    }

    private static double? JsonNumber(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out var property))
            return null;

        if (property.ValueKind == JsonValueKind.Number && property.TryGetDouble(out var number))
            return number;

        if (property.ValueKind == JsonValueKind.String
            && double.TryParse(property.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out number))
        {
            return number;
        }

        return null;
    }

    private static string FirstNonEmpty(params string[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }
        return "";
    }

    private static string JsonValueText(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out var property))
            return "";

        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString() ?? "",
            JsonValueKind.Number => property.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => "",
        };
    }

    private static int JsonInt(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out var property))
            return 0;

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var number))
            return number;

        return property.ValueKind == JsonValueKind.String
            && int.TryParse(property.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out number)
            ? number
            : 0;
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<MaaCandidatePreview>> LoadRecognitionStaticCandidates()
    {
        var path = ResolveDataPath("recognition", "maa-tasks.json");
        if (string.IsNullOrWhiteSpace(path))
            return new Dictionary<string, IReadOnlyList<MaaCandidatePreview>>(StringComparer.Ordinal);

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        if (!root.TryGetProperty("candidates", out var candidates) || candidates.ValueKind != JsonValueKind.Array)
            return new Dictionary<string, IReadOnlyList<MaaCandidatePreview>>(StringComparer.Ordinal);

        var previewsByEntry = new Dictionary<string, List<MaaCandidatePreview>>(StringComparer.Ordinal);
        foreach (var item in candidates.EnumerateArray())
        {
            if (!item.TryGetProperty("candidate", out var candidate) || candidate.ValueKind != JsonValueKind.Object)
                continue;

            var id = JsonString(item, "id");
            if (string.IsNullOrWhiteSpace(id))
                continue;

            var entry = GeneratedNodeName("RhodesCandidate", id);
            var field = JsonString(candidate, "field");
            var label = JsonString(candidate, "label");
            var name = JsonString(candidate, "name");
            var value = JsonValueText(candidate, "value");
            var recognitionKey = JsonString(candidate, "recognitionKey");
            var preview = new MaaCandidatePreview(
                JsonString(candidate, "kind"),
                string.IsNullOrWhiteSpace(label) ? field : label,
                string.IsNullOrWhiteSpace(value) ? name : value,
                "",
                JsonNumber(candidate, "confidence"),
                field,
                JsonString(candidate, "operatorId"),
                JsonString(candidate, "relicId"),
                JsonString(candidate, "campaignId"),
                string.IsNullOrWhiteSpace(recognitionKey) ? $"maa-local:static:{id}" : recognitionKey,
                JsonString(candidate, "thoughtId"),
                JsonString(candidate, "ageId"),
                JsonString(candidate, "fieldId"),
                JsonString(candidate, "slotKind"),
                JsonString(candidate, "effectId"),
                JsonString(candidate, "stateId"),
                JsonString(candidate, "coinId"),
                JsonString(candidate, "statusId"),
                JsonString(candidate, "face"),
                JsonInt(candidate, "count"));

            if (!previewsByEntry.TryGetValue(entry, out var list))
            {
                list = [];
                previewsByEntry[entry] = list;
            }
            list.Add(preview);
        }

        return previewsByEntry.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<MaaCandidatePreview>)pair.Value.ToArray(),
            StringComparer.Ordinal);
    }

    private static string GeneratedNodeName(string prefix, string id)
    {
        var builder = new StringBuilder(prefix);
        builder.Append('_');
        var pendingSeparator = false;
        foreach (var ch in id)
        {
            if (ch is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                if (pendingSeparator && builder[^1] != '_')
                    builder.Append('_');
                builder.Append(ch);
                pendingSeparator = false;
                continue;
            }

            pendingSeparator = builder[^1] != '_';
        }

        if (builder[^1] == '_')
            builder.Length--;
        return builder.ToString();
    }

    private static IReadOnlyList<SelectableEffectCandidate> LoadSelectableEffects()
    {
        var path = ResolveDataPath("selectable-effects.json");
        if (string.IsNullOrWhiteSpace(path))
            return [];

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        if (!root.TryGetProperty("selectableEffects", out var effects) || effects.ValueKind != JsonValueKind.Array)
            return [];

        return effects.EnumerateArray()
            .Select(item => new SelectableEffectCandidate(
                JsonString(item, "id"),
                JsonString(item, "campaignId"),
                JsonString(item, "slot"),
                JsonString(item, "name"),
                JsonString(item, "groupLabel"),
                JsonString(item, "parentKey"),
                JsonString(item, "parentName"),
                JsonString(item, "variantRank"),
                JsonString(item, "variantLabel"),
                JsonString(item, "effect")))
            .Where(item => !string.IsNullOrWhiteSpace(item.Id) && !string.IsNullOrWhiteSpace(item.Name))
            .ToArray();
    }

    private static IReadOnlyList<RunSquadCandidate> LoadSquads()
    {
        var path = ResolveDataPath("squads.json");
        if (string.IsNullOrWhiteSpace(path))
            return [];

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        if (!root.TryGetProperty("squads", out var squads) || squads.ValueKind != JsonValueKind.Array)
            return [];

        return squads.EnumerateArray()
            .Select(item => new RunSquadCandidate(
                JsonString(item, "id"),
                JsonString(item, "campaignId"),
                JsonString(item, "name"),
                ReadSquadEffectSignatures(item),
                ReadRandomEffectOptions(item)))
            .Where(item => !string.IsNullOrWhiteSpace(item.Id) && !string.IsNullOrWhiteSpace(item.Name))
            .ToArray();
    }

    private static IReadOnlyList<string> ReadSquadEffectSignatures(JsonElement squad)
    {
        var effects = new List<string>();
        var baseEffect = JsonString(squad, "effect");
        if (!string.IsNullOrWhiteSpace(baseEffect))
            effects.Add(baseEffect);

        if (squad.ValueKind == JsonValueKind.Object
            && squad.TryGetProperty("upgrades", out var upgrades)
            && upgrades.ValueKind == JsonValueKind.Array)
        {
            effects.AddRange(upgrades.EnumerateArray()
                .Select(item => JsonString(item, "effect"))
                .Where(effect => !string.IsNullOrWhiteSpace(effect)));
        }

        return effects.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static IReadOnlyList<RunSquadRandomEffectOption> ReadRandomEffectOptions(JsonElement squad)
    {
        if (squad.ValueKind != JsonValueKind.Object
            || !squad.TryGetProperty("randomEffectOptions", out var options)
            || options.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return options.EnumerateArray()
            .Select(item => new RunSquadRandomEffectOption(
                JsonString(item, "id"),
                JsonString(item, "label"),
                JsonString(item, "effect")))
            .Where(item => !string.IsNullOrWhiteSpace(item.Id))
            .ToArray();
    }

    private static string ResolveDataPath(params string[] segments)
    {
        foreach (var dataRoot in DataRootCandidates().Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var path = segments.Aggregate(dataRoot, Path.Combine);
            if (File.Exists(path))
                return path;
        }
        return "";
    }

    private static IEnumerable<string> DataRootCandidates()
    {
        foreach (var origin in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var current = new DirectoryInfo(origin);
            for (var i = 0; current is not null && i < 8; i++, current = current.Parent)
            {
                yield return Path.Combine(current.FullName, "data");
            }
        }
    }

    private sealed record SelectableEffectCandidate(
        string Id,
        string CampaignId,
        string Slot,
        string Name,
        string GroupLabel,
        string ParentKey,
        string ParentName,
        string VariantRank,
        string VariantLabel,
        string Effect);

    private sealed record SuiSeasonalHourGroup(
        string Key,
        string ParentName,
        IReadOnlyList<SelectableEffectCandidate> Variants);

    private sealed record OcrTextResult(
        string Text,
        double? Confidence,
        int X,
        int Y,
        int Width,
        int Height);

    private sealed record ThoughtOccurrence(
        MaaCandidatePreview Candidate,
        int X,
        int Y);

    private sealed record RunSquadCandidate(
        string Id,
        string CampaignId,
        string Name,
        IReadOnlyList<string> EffectSignatures,
        IReadOnlyList<RunSquadRandomEffectOption> RandomEffectOptions);

    private sealed record RunSquadRandomEffectOption(
        string Id,
        string Label,
        string Effect);
}
