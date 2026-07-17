using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using RhodesSuki.Models;

namespace RhodesSuki.Services;

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

    private static readonly Lazy<IReadOnlyDictionary<string, IReadOnlyList<MaaCandidatePreview>>> StaticCandidatePreviewsByEntry =
        new(LoadRecognitionStaticCandidates);

    private static readonly IReadOnlyDictionary<string, (string Field, string Label, int Min, int Max, double Confidence)> RunStatusFields =
        new Dictionary<string, (string Field, string Label, int Min, int Max, double Confidence)>(StringComparer.Ordinal)
        {
            ["run.ingot"] = ("ingot", "源石錐", 0, 9999, 0.84),
            ["run.idea"] = ("idea", "構想", 0, 999, 0.70),
            ["run.idea.current"] = ("idea", "構想", 0, 999, 0.82),
            ["run.difficulty_grade"] = ("difficulty", "等級", 1, 99, 0.78),
        };

    public static IReadOnlyList<MaaCandidatePreview> FromTaskResults(
        string? profileId,
        IEnumerable<MaaTaskRunResult> taskResults)
    {
        IEnumerable<MaaCandidatePreview> candidates;
        if (string.IsNullOrWhiteSpace(profileId) || string.Equals(profileId, "all", StringComparison.Ordinal))
            candidates = AllProfileCandidates(taskResults);

        else if (string.Equals(profileId, "runStatusFull", StringComparison.Ordinal))
            candidates = BestRunStatusCandidates(RunStatusCandidates(taskResults));

        else if (string.Equals(profileId, "operatorsFull", StringComparison.Ordinal))
            candidates = OperatorCandidates(taskResults);

        else if (string.Equals(profileId, "relicsFull", StringComparison.Ordinal))
            candidates = RelicCandidates(taskResults);

        else if (string.Equals(profileId, "is5ThoughtFull", StringComparison.Ordinal))
            candidates = ThoughtCandidates(taskResults);

        else if (string.Equals(profileId, "is5AgeFull", StringComparison.Ordinal))
            candidates = AgeCandidates(taskResults);

        else if (string.Equals(profileId, "is4RevelationFull", StringComparison.Ordinal))
            candidates = RevelationCandidates(taskResults);

        else if (string.Equals(profileId, "is6CoinsFull", StringComparison.Ordinal))
            candidates = CoinCandidates(taskResults);

        else
            candidates = [];

        return candidates.Where(RhodesMaaRecognitionPolicy.IsRetainedCandidate).ToArray();
    }

    private static IReadOnlyList<MaaCandidatePreview> AllProfileCandidates(IEnumerable<MaaTaskRunResult> taskResults)
    {
        var results = taskResults.ToArray();
        var candidates = BestRunStatusCandidates(RunStatusCandidates(results))
            .Concat(OperatorCandidates(results))
            .Concat(RelicCandidates(results))
            .Concat(ThoughtCandidates(results))
            .Concat(AgeCandidates(results))
            .Concat(RevelationCandidates(results))
            .Concat(CoinCandidates(results));
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
            else if ((candidate.Confidence ?? 0) > (existing.Candidate.Confidence ?? 0))
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

    private static IEnumerable<MaaCandidatePreview> RunStatusCandidates(IEnumerable<MaaTaskRunResult> taskResults)
    {
        var results = taskResults as MaaTaskRunResult[] ?? taskResults.ToArray();
        var campaignId = RunStatusCampaignId(results);
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

            var regionId = RunStatusRegionId(taskResult.Entry);
            if (string.IsNullOrWhiteSpace(regionId) || !RunStatusFields.TryGetValue(regionId, out var field))
                continue;

            var textResult = PrimaryTextResult(taskResult.RecognitionDetailJson);
            if (string.IsNullOrWhiteSpace(textResult.Text))
                continue;

            var value = NumericValue(textResult.Text, allowRoman: true);
            if (value is null || value < field.Min || value > field.Max)
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

    private static string RunStatusCampaignId(IReadOnlyList<MaaTaskRunResult> taskResults)
    {
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
            "difficulty" => campaignId,
            _ => "",
        };
    }

    private static string RunStatusRegionId(string entry)
    {
        if (entry.Contains("run.ingot", StringComparison.OrdinalIgnoreCase))
            return "run.ingot";
        if (entry.Contains("run.idea.current", StringComparison.OrdinalIgnoreCase))
            return "run.idea.current";

        return entry switch
        {
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
        var squad = LoadSquads()
            .Where(item => string.Equals(item.CampaignId, campaignId, StringComparison.Ordinal))
            .Where(item => item.RandomEffectOptions.Count > 0)
            .FirstOrDefault(item => normalizedChoiceText.Contains(NormalizeChoiceName(item.Name), StringComparison.Ordinal));
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
        var operators = RhodesRunCatalog.LoadDefault().Operators
            .Where(item => !string.IsNullOrWhiteSpace(item.Id) && !string.IsNullOrWhiteSpace(item.Name))
            .ToArray();
        var byNormalizedName = operators
            .GroupBy(item => RhodesOperatorOcrNormalizer.Normalize(item.Name), StringComparer.Ordinal)
            .Where(group => !string.IsNullOrWhiteSpace(group.Key) && group.Count() == 1)
            .ToDictionary(group => group.Key, group => group.Single(), StringComparer.Ordinal);
        var byId = operators.ToDictionary(item => item.Id, StringComparer.Ordinal);
        var matched = new Dictionary<string, (SukiChoiceItem Operator, string RawText, double? Confidence, int Order)>(
            StringComparer.Ordinal);
        var order = 0;

        foreach (var taskResult in taskResults)
        {
            if (!taskResult.Succeeded || !IsOperatorNameEntry(taskResult.Entry))
                continue;

            foreach (var textResult in PrimaryTextResults(taskResult.RecognitionDetailJson))
            {
                foreach (var token in ChoiceNameTokens(textResult.Text))
                {
                    var normalized = RhodesOperatorOcrNormalizer.Normalize(token.Raw);
                    var officialId = RhodesOperatorOcrNormalizer.ResolveOfficialOperatorId(token.Raw);
                    var op = officialId is not null && byId.TryGetValue(officialId, out var official)
                        ? official
                        : ResolveOperator(normalized, byNormalizedName);

                    if (op is null)
                        continue;

                    if (!matched.TryGetValue(op.Id, out var existing))
                    {
                        matched[op.Id] = (op, token.Raw, textResult.Confidence, order);
                    }
                    else if ((textResult.Confidence ?? 0) > (existing.Confidence ?? 0))
                    {
                        matched[op.Id] = (op, token.Raw, textResult.Confidence, existing.Order);
                    }
                }
            }

            order++;
        }

        foreach (var item in matched.Values.OrderBy(item => item.Order))
        {
            yield return new MaaCandidatePreview(
                "operator",
                item.Operator.Name,
                item.Operator.Id,
                item.RawText,
                Math.Max(0.70, item.Confidence ?? 0),
                OperatorId: item.Operator.Id,
                RecognitionKey: $"maa-local:operator:{item.Operator.Id}");
        }
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

    private static IEnumerable<MaaCandidatePreview> RelicCandidates(IEnumerable<MaaTaskRunResult> taskResults)
    {
        var catalog = RhodesRunCatalog.LoadDefault();
        var campaignId = catalog.Current.CampaignId;
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
        var matched = new Dictionary<string, (SukiChoiceItem Relic, string RawText, double? Confidence, int Order)>(
            StringComparer.Ordinal);
        var order = 0;

        foreach (var taskResult in taskResults)
        {
            if (!taskResult.Succeeded || !IsRelicNameEntry(taskResult.Entry))
                continue;

            foreach (var textResult in PrimaryTextResults(taskResult.RecognitionDetailJson))
            {
                foreach (var token in ChoiceNameTokens(textResult.Text))
                {
                    var relic = ResolveRelic(NormalizeRelicName(token.Raw), byNormalizedName);
                    if (relic is null)
                        continue;

                    if (!matched.TryGetValue(relic.Id, out var existing))
                    {
                        matched[relic.Id] = (relic, token.Raw, textResult.Confidence, order);
                    }
                    else if ((textResult.Confidence ?? 0) > (existing.Confidence ?? 0))
                    {
                        matched[relic.Id] = (relic, token.Raw, textResult.Confidence, existing.Order);
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
                RecognitionKey: $"maa-local:relic:{item.Relic.Id}");
        }
    }

    private static string NormalizeRelicName(string value)
    {
        return NormalizeChoiceName(value)
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
    }

    private static SukiChoiceItem? ResolveRelic(
        string normalized,
        IReadOnlyDictionary<string, SukiChoiceItem> byNormalizedName)
    {
        if (byNormalizedName.TryGetValue(normalized, out var exact))
            return exact;
        if (normalized.Length < 4)
            return null;

        var fuzzy = byNormalizedName
            .Where(item => item.Key.Length >= 4 && Math.Abs(item.Key.Length - normalized.Length) <= 2)
            .Select(item => new
            {
                item.Value,
                CandidateLength = item.Key.Length,
                Distance = EditDistance(normalized, item.Key),
            })
            .Where(item => item.Distance <= (Math.Min(normalized.Length, item.CandidateLength) <= 4 ? 1 : 2))
            .Take(2)
            .ToArray();
        if (fuzzy.Length == 1)
            return fuzzy[0].Value;

        var prefix = byNormalizedName
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

    private static IEnumerable<MaaCandidatePreview> ThoughtCandidates(IEnumerable<MaaTaskRunResult> taskResults)
    {
        var thoughts = LoadSelectableEffects()
            .Where(effect => effect.Slot == "thought" && effect.CampaignId == "is5_sarkaz")
            .ToArray();
        var byNormalizedName = thoughts
            .GroupBy(item => NormalizeChoiceName(item.Name), StringComparer.Ordinal)
            .Where(group => !string.IsNullOrWhiteSpace(group.Key) && group.Count() == 1)
            .ToDictionary(group => group.Key, group => group.Single(), StringComparer.Ordinal);
        var aggregates = new Dictionary<string, (MaaCandidatePreview Candidate, int MaxCount)>(StringComparer.Ordinal);
        var firstSeen = new List<string>();

        foreach (var taskResult in taskResults)
        {
            if (!taskResult.Succeeded || !IsThoughtNameEntry(taskResult.Entry))
                continue;

            var frameCandidates = new List<MaaCandidatePreview>();
            foreach (var textResult in PrimaryTextResults(taskResult.RecognitionDetailJson))
            {
                foreach (var token in ChoiceNameTokens(textResult.Text))
                {
                    if (!byNormalizedName.TryGetValue(token.Normalized, out var thought))
                        continue;

                    frameCandidates.Add(new MaaCandidatePreview(
                        "thought",
                        thought.Name,
                        thought.Id,
                        token.Raw,
                        Math.Max(0.68, textResult.Confidence ?? 0),
                        CampaignId: thought.CampaignId,
                        ThoughtId: thought.Id));
                }
            }

            foreach (var group in frameCandidates.GroupBy(candidate => candidate.ThoughtId, StringComparer.Ordinal))
            {
                var best = group.MaxBy(candidate => candidate.Confidence ?? 0)!;
                var frameCount = group.Count();
                if (!aggregates.TryGetValue(group.Key, out var existing))
                {
                    aggregates[group.Key] = (best, frameCount);
                    firstSeen.Add(group.Key);
                    continue;
                }

                var candidate = (best.Confidence ?? 0) > (existing.Candidate.Confidence ?? 0)
                    ? best
                    : existing.Candidate;
                aggregates[group.Key] = (candidate, Math.Max(existing.MaxCount, frameCount));
            }
        }

        var order = 0;
        foreach (var thoughtId in firstSeen)
        {
            var aggregate = aggregates[thoughtId];
            for (var index = 0; index < aggregate.MaxCount; index++)
            {
                yield return aggregate.Candidate with
                {
                    RecognitionKey = $"maa-local:thought:{thoughtId}:{order}",
                };
                order++;
            }
        }
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

    private static IEnumerable<MaaCandidatePreview> CoinCandidates(IEnumerable<MaaTaskRunResult> taskResults)
    {
        var coins = LoadSelectableEffects()
            .Where(effect => effect.Slot == "coin" && effect.CampaignId == "is6_sui")
            .ToArray();
        var byNormalizedName = coins
            .GroupBy(item => NormalizeChoiceName(item.Name), StringComparer.Ordinal)
            .Where(group => !string.IsNullOrWhiteSpace(group.Key) && group.Count() == 1)
            .ToDictionary(group => group.Key, group => group.Single(), StringComparer.Ordinal);
        var order = 0;

        foreach (var taskResult in taskResults)
        {
            if (!taskResult.Succeeded || !IsCoinNameEntry(taskResult.Entry))
                continue;

            foreach (var textResult in PrimaryTextResults(taskResult.RecognitionDetailJson))
            {
                foreach (var token in ChoiceNameTokens(textResult.Text))
                {
                    if (!byNormalizedName.TryGetValue(token.Normalized, out var coin))
                        continue;

                    yield return new MaaCandidatePreview(
                        "coin",
                        coin.Name,
                        coin.Id,
                        token.Raw,
                        Math.Max(0.68, textResult.Confidence ?? 0),
                        CampaignId: coin.CampaignId,
                        RecognitionKey: $"maa-local:coin:{coin.Id}:{order}",
                        FieldId: "coins",
                        CoinId: coin.Id,
                        Count: 1);
                    order++;
                }
            }
        }
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

    private static bool IsThoughtNameEntry(string entry)
    {
        return entry.Equals("RhodesOcrRegion_is5_thought_list_text", StringComparison.Ordinal)
            || entry.Contains("is5.thought_list_text", StringComparison.OrdinalIgnoreCase);
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

    private static (string Text, double? Confidence) PrimaryTextResult(string value)
    {
        var results = PrimaryTextResults(value);
        if (results.Count > 0)
            return results[0];

        return ("", null);
    }

    private static IReadOnlyList<(string Text, double? Confidence)> PrimaryTextResults(string value)
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

        var results = new List<(string Text, double? Confidence)>();
        foreach (var item in PrimaryResults(root))
        {
            if (item.ValueKind != JsonValueKind.Object)
                continue;

            var text = JsonString(item, "text");
            if (!string.IsNullOrWhiteSpace(text))
            {
                results.Add((
                    text.Trim(),
                    JsonNumber(item, "score") ?? JsonNumber(item, "confidence") ?? JsonNumber(item, "prob")));
            }
        }

        return results;
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

    private static int? NumericValue(string value, bool allowRoman = false)
    {
        var text = NormalizeDigits(value, allowRoman);
        if (string.IsNullOrWhiteSpace(text))
            return null;

        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
            ? number
            : null;
    }

    private static string NormalizeDigits(string value, bool allowRoman)
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
                JsonString(item, "parentName"),
                JsonString(item, "variantLabel")))
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
        string ParentName,
        string VariantLabel);

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
