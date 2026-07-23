using System.Reflection;
using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using MaaFramework.Binding;
using RhodesSuki.Models;
using RhodesSuki.Services;
using RhodesSuki.ViewModels;
using SkiaSharp;

var tests = new (string Name, Action Run)[]
{
    ("MAA OCR best_result becomes an OCR candidate", OcrBestResult),
    ("MAA OCR filtered_results are preferred over all_results", OcrFilteredResults),
    ("MAA TemplateMatch count becomes a template candidate", TemplateCount),
    ("MAA hit without detail falls back to a simple candidate", HitFallback),
    ("Candidate preview exposes stable debugger identity", CandidatePreviewIdentity),
    ("MAA candidate API extraction preserves structured ids", CandidateApiExtraction),
    ("MAA candidate merger supplements missing local candidates safely", CandidateMergerSupplementsLocalCandidates),
    ("MAA candidate merger rejects conflicting API relics for one local OCR row", CandidateMergerPrefersLocalRelicForSameEvidence),
    ("MAA candidate merger preserves local relic usage evidence", CandidateMergerPreservesRelicUsageEvidence),
    ("MAA candidate merger preserves local reserve operator counts", CandidateMergerPreservesReserveOperatorCounts),
    ("MAA candidate merger keeps only the profession-resolved Amiya form", CandidateMergerPrefersResolvedAmiyaForm),
    ("MAA candidate merger prefers local thought counts over legacy API slot tracking", CandidateMergerPrefersLocalThoughtCounts),
    ("MAA candidate merger keeps campaign-specific run status fields", CandidateMergerKeepsCampaignRunStatusFields),
    ("Recognition workflow runs resource tasks in plan order", RecognitionWorkflowRunsResourceTasks),
    ("Recognition workflow converts API and local candidates behind one seam", RecognitionWorkflowConvertsCandidates),
    ("Recognition workflow falls back to local candidates when API is unavailable", RecognitionWorkflowLocalFallback),
    ("Recognition workflow applies candidates through API state first", RecognitionWorkflowApplyCandidatesViaApi),
    ("Run catalog projects authoritative API state without a disk round-trip", RunCatalogProjectsAuthoritativeStateJson),
    ("Recognition workflow falls back to local candidate apply when API fails", RecognitionWorkflowApplyCandidatesLocalFallback),
    ("Recognition workflow handles empty candidate apply without API calls", RecognitionWorkflowApplyCandidatesEmpty),
    ("Local MAA candidate converter extracts run status candidates", LocalCandidateConverterRunStatus),
    ("Local MAA candidate converter ignores the generic ingot ROI for Sui", LocalCandidateConverterIgnoresGenericSuiIngot),
    ("Local MAA candidate converter keeps the best duplicate run status field", LocalCandidateConverterRunStatusBestDuplicate),
    ("Local MAA candidate converter normalizes numeric OCR drift", LocalCandidateConverterNormalizesNumericDrift),
    ("Local MAA candidate converter normalizes measured squad OCR drift", LocalCandidateConverterNormalizesSquadDrift),
    ("Local MAA candidate converter prefers squad icon templates over conflicting OCR", LocalCandidateConverterPrefersSquadIconTemplate),
    ("Local MAA candidate converter keeps the strongest Mizuki squad icon", LocalCandidateConverterKeepsStrongestMizukiSquadIcon),
    ("Local MAA candidate converter decodes batched squad template results", LocalCandidateConverterDecodesBatchedSquadTemplate),
    ("Local MAA candidate converter decodes batched Sui squad template results", LocalCandidateConverterDecodesBatchedSuiSquadTemplate),
    ("Local MAA candidate converter bounds and repairs anchored Sarkaz difficulty", LocalCandidateConverterBoundsSarkazDifficulty),
    ("Local MAA candidate converter bounds anchored Sui difficulty", LocalCandidateConverterBoundsSuiDifficulty),
    ("Local MAA candidate converter extracts random squad effect candidates", LocalCandidateConverterRunStatusSquadRandomEffect),
    ("Local MAA candidate converter extracts exact operator name candidates", LocalCandidateConverterOperators),
    ("Local MAA candidate converter counts duplicate reserve operators per frame", LocalCandidateConverterCountsReserveOperators),
    ("MAA Amiya role resolver targets the profession icon beside a detected card", MaaAmiyaRoleResolverTargetsProfessionIcon),
    ("Local MAA candidate converter disambiguates Amiya forms by profession", LocalCandidateConverterDisambiguatesAmiyaForms),
    ("Local MAA candidate converter extracts current campaign relic candidates", LocalCandidateConverterRelics),
    ("Local MAA relic matching keeps modified Phantom variants distinct", LocalCandidateConverterPrefersModifiedPhantomRelic),
    ("Local MAA candidate converter preserves duplicate IS5 thought candidates", LocalCandidateConverterThoughts),
    ("Local MAA candidate converter extracts IS5 age candidates", LocalCandidateConverterAge),
    ("Local MAA candidate converter combines multiple IS2 hallucinations", LocalCandidateConverterPhantomHallucinations),
    ("Local MAA candidate converter resolves a single IS2 performance", LocalCandidateConverterPhantomPerformance),
    ("Local MAA candidate converter extracts IS3 Mizuki special values", LocalCandidateConverterMizukiSpecials),
    ("Local MAA candidate converter ignores IS3 rejection operator prose", LocalCandidateConverterPrefersMizukiRejectionTemplate),
    ("Local MAA candidate converter extracts IS4 revelation candidates", LocalCandidateConverterRevelation),
    ("Local MAA candidate converter extracts Sui ingot and ticket values", LocalCandidateConverterSuiBaseValues),
    ("Local MAA candidate converter resolves normal and awakened Sui seasonal hours", LocalCandidateConverterSuiSeasonalHours),
    ("Local MAA candidate converter repairs the stylized Sui ticket six", LocalCandidateConverterRepairsSuiTicketSix),
    ("Local MAA candidate converter extracts IS6 coin candidates", LocalCandidateConverterCoins),
    ("Sui active coin image recognizer classifies slots and counts duplicates", SuiActiveCoinImageRecognizer),
    ("Sui active coin panel OCR counts duplicate names", SuiActiveCoinOcrCandidateCounts),
    ("Sui Catch Wind detail resolver locates the visible owned coin card", SuiCatchWindDetailResolverLocatesCard),
    ("Sui Catch Wind detail resolver maps description direction without guessing", SuiCatchWindDetailResolverMapsDirection),
    ("Sui Catch Wind detail resolver randomizes its targeted tap area", SuiCatchWindDetailResolverRandomizesTap),
    ("Sui owned coin image recognizer ignores dim unowned slots", SuiOwnedCoinImageRecognizer),
    ("Sui owned coin image recognizer finishes a representative frame promptly", SuiOwnedCoinImageRecognizerPerformance),
    ("Sui owned coin recognizer OCRs only unresolved colored slots", SuiOwnedCoinOcrFallbackPlanner),
    ("Sui owned coin candidate merger preserves duplicate counts across scroll frames", SuiOwnedCoinCandidateCounts),
    ("Sui owned coin OCR merger preserves duplicate counts without summing overlapping frames", SuiOwnedCoinOcrCandidateCounts),
    ("Sui owned coin status recognizer anchors status icons to OCR names", SuiOwnedCoinStatusRecognizer),
    ("Local MAA candidate converter dispatches all profile task results", LocalCandidateConverterAllProfiles),
    ("ADB presets include MuMu and Google Play Games developer defaults", AdbPresets),
    ("ADB presets include current MuMu nx_main layouts", AdbPresetCurrentMumuLayouts),
    ("ADB presets infer MuMu install roots from current process layouts", AdbPresetMumuProcessLayout),
    ("ADB presets prefer the ADB layout matching the running MuMu version", AdbPresetMumuRunningVersion),
    ("ADB method catalog maps emulator presets to fast lossless MAA methods", AdbMethodCatalog),
    ("ADB config JSON normalizer accepts only object payloads", AdbConfigJsonNormalizer),
    ("ADB connection resolver builds MuMu and LD extras config", AdbConnectionResolverBuildsExtras),
    ("ADB connection resolver adopts MaaToolkit device recommendations", AdbConnectionResolverUsesToolkit),
    ("ADB connection resolver discovers MaaToolkit devices through one boundary", AdbConnectionResolverDiscoversToolkit),
    ("ADB device output parses serials and usable state", AdbDeviceParsing),
    ("ADB candidate registry keeps the runtime picker focused", AdbCandidateRegistry),
    ("Suki ADB detection workflow summarizes selected runtime path and devices", SukiAdbDetectionWorkflow),
    ("Suki ADB detection workflow promotes auto detected emulator presets", SukiAdbDetectionPresetPromotion),
    ("ADB detect API client parses runtime, candidates, and devices", AdbApiDetectionParsing),
    ("ADB test API client parses resolution and screenshot details", AdbApiTestParsing),
    ("Suki local ADB detector connects Google Play Games TCP serial", SukiLocalAdbDetectGooglePlay),
    ("Suki local ADB detector prefers explicit MuMu adb path", SukiLocalAdbDetectExplicitMumu),
    ("Suki local ADB detector uses an existing MuMu device without stale TCP probes", SukiLocalAdbDetectExistingMumuDevice),
    ("Suki ADB connection test workflow reports controller and capture outcomes", SukiAdbConnectionTestWorkflow),
    ("ADB diagnostics checklist explains setup failures and capture readiness", AdbDiagnosticsChecklist),
    ("ADB diagnostics copy text includes report-ready runtime details", AdbDiagnosticsCopyText),
    ("Preview URL builder normalizes RHODES app routes", PreviewUrlBuilder),
    ("Managed Node runtime prefers the executable beside the app", ManagedNodeRuntimePrefersAppRuntime),
    ("Managed Node runtime installs only a checksum-verified archive", ManagedNodeRuntimeInstallsVerifiedArchive),
    ("Managed Node runtime rejects archive path traversal", ManagedNodeRuntimeRejectsArchiveTraversal),
    ("Suki settings store round-trips ADB and profile values", SukiSettingsStore),
    ("Suki settings store migrates unusable manual PATH adb settings", SukiSettingsStoreMigratesBareManualAdb),
    ("RHODES API status probe parses health and state payloads", RhodesApiStatusParsing),
    ("Optional runtime probe parses GLM and Ollama status payloads", OptionalRuntimeStatusParsing),
    ("Suki optional runtime action workflow reports runtime and API outcomes", SukiOptionalRuntimeActionWorkflow),
    ("Suki runtime probe workflow aggregates API, optional runtime, and Hyper-V statuses", SukiRuntimeProbeWorkflowAggregatesStatuses),
    ("Runtime capability registry exposes stable core and optional capabilities", RuntimeCapabilityRegistry),
    ("Workspace registry exposes stable Suki navigation", WorkspaceRegistry),
    ("Workspace registry hides developer workspaces in public debug distributions", PublicDebugWorkspaceRegistry),
    ("Workspace layout registry covers every Suki workspace", WorkspaceLayoutRegistry),
    ("Workspace action registry maps UI commands to workflows", WorkspaceActionRegistry),
    ("Product surface registry assigns every major app element to a workspace", ProductSurfaceRegistry),
    ("Output part registry defines OBS sidecar display blocks", OutputPartRegistry),
    ("Overlay layout catalog keeps custom OBS parts inside a 1920x1080 canvas", OverlayLayoutCatalog),
    ("Runtime workspace registry exposes focused setup sections", RuntimeWorkspaceRegistry),
    ("Recognition workspace registry exposes the MAA action flow", RecognitionWorkspaceRegistry),
    ("OCR engine catalog exposes only MAA-OCR plus optional GLM", OcrEngineCatalog),
    ("Hypervisor probe parses Google Play Games readiness states", HypervisorStatusParsing),
    ("MAAFramework runtime probe reports native and VC++ diagnostics", MaaFrameworkRuntimeDiagnostics),
    ("MAA recognition resource paths diagnose OCR asset readiness", MaaRecognitionResourcePathDiagnostics),
    ("MAA offline session initializes without an ADB controller", MaaOfflineSessionInitializesWithoutAdb),
    ("Recognition navigation restores profile open and close steps", RecognitionNavigationLoadsProfileSteps),
    ("Recognition navigation randomizes taps inside configured areas", RecognitionNavigationRandomizesTapAreas),
    ("Recognition scroll plan loads operator passes and randomizes swipe areas", RecognitionScrollPlanLoadsOperatorPasses),
    ("Recognition runtime plan removes legacy operator OCR and completes relic scans by owned count", RecognitionRuntimePlanUsesFocusedTasks),
    ("Relic owned count reader extracts the footer count from MAA OCR evidence", RelicOwnedCountReaderExtractsFooterCount),
    ("Recognition retry policy retries only missing or low-confidence live frames", RecognitionRetryPolicyTargetsLowConfidenceFrames),
    ("Mizuki undetected policy preserves prior horde and rejection values", MizukiUndetectedPolicyPreservesPriorValues),
    ("Phantom, Mizuki, and Sami require manual difficulty instead of OCR", ManualDifficultyCampaignPolicy),
    ("Recognition frame fingerprint ignores tiny noise and detects layout changes", RecognitionFrameFingerprintIsPerceptual),
    ("MAA OCR preprocessing crops and scales configured ROI", MaaOcrPreprocessingCropsAndScalesRoi),
    ("MAA Catch Wind detail preprocessing inverts dark prose for OCR", MaaCatchWindDetailPreprocessingInvertsDarkProse),
    ("MAA operator OCR preprocessing masks and trims the name ROI", MaaOperatorOcrPreprocessingMasksAndTrimsNameRoi),
    ("MAA Japanese operator replaceFull rules resolve local operator ids", MaaJapaneseOperatorRulesResolveLocalIds),
    ("MAA template OCR config exposes operator card offsets", MaaTemplateOcrConfigExposesOperatorOffsets),
    ("MAA template OCR expander builds dynamic name regions", MaaTemplateOcrExpanderBuildsDynamicRegions),
    ("MAA template OCR expander restores weak operator anchors on the detected card grid", MaaTemplateOcrExpanderRestoresWeakGridAlignedOperatorAnchors),
    ("MAA thought load OCR expander targets displayed card values", MaaThoughtLoadOcrExpanderTargetsDisplayedValues),
    ("Operator scan tracker skips resolved cards and stops on repeated viewports", OperatorScanTrackerCachesResolvedCards),
    ("Mizuki rejection card detector identifies the purple operator name", MizukiRejectionCardDetectorIdentifiesPurpleBand),
    ("Mizuki rejection targets expand reserve operator recruit instances", MizukiRejectionTargetsExpandReserveInstances),
    ("MAA recognition probe payloads target retained fields", RecognitionProbePayloadsTargetRetainedFields),
    ("MAA recognition invocation separates algorithm from parameters", MaaRecognitionInvocationSeparatesAlgorithm),
    ("MAA task diagnostics summarize counts and OCR previews", TaskDiagnostics),
    ("MAA OCR detail rows expose raw OCR result groups", OcrDetailRowsExposeRawGroups),
    ("MAA ROI detail rows expose rect, roi, and point boxes", RoiDetailRowsExposeRectVariants),
    ("MAA ROI preview projector scales actual image coordinates to 1280x720", RoiPreviewProjectorScalesImageCoordinates),
    ("MAA ROI edit draft evidence uses stable JSON shape", RoiEditDraftEvidence),
    ("MAA ROI adjustment session log preserves scan-scoped drafts", RoiAdjustmentSessionLog),
    ("MAA ROI draft source updater maps generated entries back to source ROI", RoiDraftSourceUpdater),
    ("MAA ROI draft batch updater applies multiple source files atomically", RoiDraftBatchSourceUpdater),
    ("MAA generated resource builder converts source JSON to pipeline nodes", MaaGeneratedResourceBuilder),
    ("MAA ROI selection matcher links OCR detail rows to ROI previews", RoiSelectionMatcherLinksOcrRows),
    ("MAA native resource task evidence uses recognition scan shape", MaaNativeEvidenceLog),
    ("Recognition scan history loads API and MAA native evidence logs", RecognitionScanHistoryLoadsUnifiedLogs),
    ("Recognition frame record store saves screenshot metadata and state snapshot", RecognitionFrameRecordStoreSavesFrame),
    ("Recognition frame record history loads saved frames for debugger replay", FrameRecordHistoryLoadsRecent),
    ("Bug report import extracts ZIPs into replayable frame history", BugReportImportExtractsReplayFrames),
    ("Bug report bundle collects debug artifacts without optional runtimes", BugReportBundleCollectsDebugArtifacts),
    ("Evidence preview tree uses compact typed nodes", EvidencePreviewTreeUsesCompactTypedNodes),
    ("Resource task preview exposes source and profile summaries", ResourceTaskSummary),
    ("Resource catalog reads checked-in pipeline nodes", ResourceCatalogReadsPipelineNodes),
    ("Resource catalog exports recognition payloads for frame replay", ResourceCatalogExportsReplayPayloads),
    ("Resource catalog validates MAA interface contract", ResourceCatalogValidatesInterfaceContract),
    ("Resource profile groups keep operational recognition order", ResourceProfileOrder),
    ("Resource profiles use interface groups", ResourceProfilesUseInterfaceGroups),
    ("Resource profile task filtering follows interface presets", ResourceProfileTaskFilteringFollowsInterfacePresets),
    ("Distribution policy keeps campaign themes selectable in every build", PublicDebugPolicyRestrictsSarkazScope),
    ("Run field registry exposes retained base and campaign-specific fields", RunFieldRegistryRetainedFields),
    ("Run catalog loads campaigns, operators, relics, and current selections", RunCatalogLoadsChoices),
    ("Run catalog preserves Sui coin status and count entries", RunCatalogPreservesSuiCoinEntries),
    ("Run catalog exposes Sarkaz difficulty and random squad options", RunCatalogSarkazManualOptions),
    ("Run catalog derives normal Sui seasonal-hour ranks from difficulty", RunCatalogSuiSeasonalHourDifficultyVariants),
    ("Run catalog exposes Phantom performances and hallucinations", RunCatalogPhantomManualOptions),
    ("Run catalog exposes Mizuki rejection reaction icons", RunCatalogMizukiRejectionIcons),
    ("Run catalog restores individual Mizuki rejection targets", RunCatalogMizukiRejectionTargetInstances),
    ("Mizuki operator presentation marks rejection and evolution targets in IS3", MizukiOperatorPresentationMarksTargets),
    ("Run catalog exposes Mizuki horde call icons", RunCatalogMizukiHordeCallIcons),
    ("Hallucination catalog exposes Wiki effects and normalizes overlapping OCR", HallucinationCatalogWikiOptions),
    ("Run catalog exposes Sarkaz boss selections from campaign data", RunCatalogSarkazBossSelections),
    ("Run catalog gates Sui floor 6 and END5 routes behind their relics", RunCatalogSuiEnd5BossSelections),
    ("Run state store persists and clears campaign boss selections", RunStateStoreBossSelections),
    ("Startup reset keeps ADB settings and starts a clean Phantom run", StartupResetKeepsAdbAndStartsPhantom),
    ("Choice pane drag scrolling clamps the target offset", ChoicePaneDragScrollMath),
    ("Choice catalog registry builds operator and relic workspace models", ChoiceCatalogRegistryBuildsWorkspaceModels),
    ("Choice filters support selected-first, hidden exclusions, and selected-only", ChoiceFilters),
    ("Run-saving relics stay first and persist their used flag", RelicUsagePriorityAndPersistence),
    ("Operator taxonomy keeps Integrated Strategies class and branch order", OperatorTaxonomyOrder),
    ("Run state store persists selected choices and display preferences", ChoicePersistence),
    ("Reserve operators alone persist multiple recruited counts", ReserveOperatorCounts),
    ("Run state store can replace state from API JSON", StateApiReplacement),
    ("State API client can apply Suki ADB settings into current state JSON", StateApiAdbSettingsApply),
    ("Suki state sync workflow saves choices, ADB, and output preferences through API state", SukiStateSyncWorkflowSettingsSuccess),
    ("Suki state sync workflow reports API failures without local state replacement", SukiStateSyncWorkflowSettingsFailure),
    ("Suki state sync workflow saves current IS context through API state", SukiStateSyncWorkflowRunContextSuccess),
    ("Suki state sync workflow reports IS context API failures without replacement", SukiStateSyncWorkflowRunContextFailure),
    ("Suki state sync workflow imports API state into local storage", SukiStateSyncWorkflowImportSuccess),
    ("Suki state sync workflow reports API import failures without replacement", SukiStateSyncWorkflowImportFailure),
    ("State API client can apply Suki display preferences into current state JSON", StateApiSukiPreferencesApply),
    ("State API client can apply selected choices into current state JSON", StateApiChoicesApply),
    ("State API client can apply current campaign into current state JSON", StateApiRunContextApply),
    ("State API client can apply recognition candidates into current state JSON", StateApiCandidatesApply),
    ("Run state store switches current campaign without stale run values", RunContextPersistence),
    ("Recognition candidate applier persists safe run status fields", CandidateRunStatusApply),
    ("Recognition candidate applier can apply Sarkaz random squad effect", CandidateRunStatusApplySquadRandomEffect),
    ("Recognition candidate applier persists Phantom performance and hallucinations", CandidateRunStatusApplyPhantomSpecials),
    ("Recognition candidate applier rejects run status candidates from other campaigns", CandidateRunStatusRejectsOtherCampaign),
    ("Recognition candidate applier keeps current campaign run status duplicates", CandidateRunStatusKeepsCurrentCampaignDuplicate),
    ("Recognition candidate applier applies campaign before dependent run fields", CandidateCampaignApplyFirst),
    ("Recognition candidate applier keeps the best duplicate run status candidate", CandidateRunStatusApplyBestDuplicate),
    ("Recognition candidate applier can select operator and relic candidates", CandidateChoiceApply),
    ("Recognition candidate applier persists reserve operator counts", CandidateReserveOperatorCountApply),
    ("Recognition candidate applier updates run-saving relic usage", CandidateRelicUsageApply),
    ("Recognition candidate applier replaces stale Amiya forms", CandidateAmiyaRoleReplacementApply),
    ("Recognition candidate applier can apply IS5 thought and age candidates", CandidateIs5SpecialApply),
    ("Recognition candidate applier clears IS5 age when detection returns none", CandidateIs5AgeClearApply),
    ("Recognition candidate applier persists IS3 Mizuki special values", CandidateMizukiSpecialApply),
    ("Recognition candidate applier preserves IS3 rejection targets on effect-only refresh", CandidateMizukiRejectionEffectOnlyPreservesTargets),
    ("Recognition candidate applier replaces the IS6 seasonal hour selection", CandidateSuiSeasonalHoursApply),
    ("Recognition candidate applier keeps normal Sui seasonal hours tied to difficulty", CandidateSuiSeasonalHoursFollowDifficulty),
    ("Recognition candidate applier replaces the IS6 manual support martial effects", CandidateSuiSupportMartialApply),
    ("Recognition candidate applier updates IS3 rejection targets after operator scan", CandidateMizukiRejectionTargetOnlyApply),
    ("Recognition candidate applier persists individual reserve rejection targets", CandidateMizukiReserveRejectionTargetApply),
    ("Recognition candidate applier persists individual Mizuki evolution targets", CandidateMizukiEvolutionTargetApply),
    ("Recognition candidate applier can apply IS4 revelation and IS6 coin candidates", CandidateOtherSpecialApply),
    ("Recognition candidate applier replaces manual Sui coins with statuses", CandidateManualSuiValuesApply),
    ("Choice rows group filtered items into up to four panes", ChoiceRows),
};

var failures = new List<string>();
foreach (var test in tests)
{
    try
    {
        test.Run();
        Console.WriteLine($"ok - {test.Name}");
    }
    catch (Exception ex)
    {
        failures.Add($"{test.Name}: {ex.Message}");
        Console.Error.WriteLine($"not ok - {test.Name}: {ex.Message}");
    }
}

if (failures.Count > 0)
{
    Console.Error.WriteLine();
    Console.Error.WriteLine($"{failures.Count} failure(s)");
    Environment.Exit(1);
}

Console.WriteLine($"{tests.Length} Suki service tests passed.");

static void OcrBestResult()
{
    var previews = RhodesMaaResultPreview.FromTaskResults(
    [
        new MaaTaskRunResult(
            "RhodesOcrRegion_operator_name",
            "Succeeded",
            true,
            "ocr detail",
            """prefix {"best_result":{"text":"テンニンカ","score":0.91}}""",
            "OCR",
            true),
    ]);

    Equal(1, previews.Count, "preview count");
    Equal("ocr", previews[0].Kind, "kind");
    Equal("テンニンカ", previews[0].Value, "value");
    Equal(0.91, previews[0].Confidence, "confidence");
}

static void OcrFilteredResults()
{
    var previews = RhodesMaaResultPreview.FromTaskResults(
    [
        new MaaTaskRunResult(
            "RhodesOcrRegion_operator_name",
            "Succeeded",
            true,
            "ocr detail",
            """{"all_results":[{"text":"ノイズ","score":0.4}],"filtered_results":[{"text":"グム","score":0.88}]}""",
            "OCR",
            true),
    ]);

    Equal(1, previews.Count, "preview count");
    Equal("グム", previews[0].Value, "value");
    Equal(0.88, previews[0].Confidence, "confidence");
}

static void TemplateCount()
{
    var previews = RhodesMaaResultPreview.FromTaskResults(
    [
        new MaaTaskRunResult(
            "RhodesTemplate_thought_branch",
            "Succeeded",
            true,
            "template detail",
            """{"filtered_results":[{"score":0.87,"count":2}]}""",
            "TemplateMatch",
            true),
    ]);

    Equal(1, previews.Count, "preview count");
    Equal("template", previews[0].Kind, "kind");
    Equal("2", previews[0].Value, "value");
    Equal(0.87, previews[0].Confidence, "confidence");
}

static void HitFallback()
{
    var previews = RhodesMaaResultPreview.FromTaskResults(
    [
        new MaaTaskRunResult(
            "RhodesTemplate_run_status_ingot",
            "Succeeded",
            true,
            "template hit",
            "",
            "TemplateMatch",
            true),
    ]);

    Equal(1, previews.Count, "preview count");
    Equal("maa", previews[0].Kind, "kind");
    Equal("hit", previews[0].Value, "value");
}

static void CandidatePreviewIdentity()
{
    var runStatus = new MaaCandidatePreview("runStatus", "希望", "3", "3", 0.9, Field: "hope");
    var thought = new MaaCandidatePreview(
        "thought",
        "枯れ木と若枝",
        "thought_a",
        "枯れ木と若枝",
        0.8,
        CampaignId: "is5_sarkaz",
        RecognitionKey: "thought:is5_sarkaz:thought_a",
        ThoughtId: "thought_a");

    Equal("hope", runStatus.Identity, "run status identity");
    Equal("thought_a", thought.Identity, "thought identity");
    Equal("thought:thought_a", thought.DebugDetail.Split(" · ").First(part => part.StartsWith("thought:", StringComparison.Ordinal)), "thought debug detail");
    Equal(true, thought.DebugDetail.Contains("campaign:is5_sarkaz", StringComparison.Ordinal), "campaign debug detail");
}

static void CandidateApiExtraction()
{
    var candidates = RhodesMaaCandidateApiClient.ExtractCandidatePreviews(
        """
        {
          "result": {
            "candidates": [
              {
                "kind": "runStatus",
                "field": "hope",
                "value": 3,
                "rawText": "3",
                "confidence": 0.94
              },
              {
                "kind": "thought",
                "name": "枯れ木と若枝",
                "value": "fallback",
                "rawText": "枯れ木と若枝",
                "confidence": 0.91,
                "campaignId": "is5_sarkaz",
                "recognitionKey": "thought:is5_sarkaz:thought_a",
                "thoughtId": "thought_a"
              },
              {
                "kind": "age",
                "label": "時代",
                "value": "age_prime",
                "rawText": "全盛期",
                "confidence": 0.88,
                "ageId": "age_prime"
              },
              {
                "kind": "revelation",
                "label": "啓示板",
                "value": "fallback",
                "rawText": "修辞A",
                "confidence": 0.84,
                "campaignId": "is4_sami",
                "fieldId": "revelationBoard",
                "slotKind": "rhetoric",
                "effectId": "rhetoric_a"
              },
              {
                "kind": "coin",
                "name": "通宝A",
                "value": "fallback_coin",
                "rawText": "通宝A",
                "confidence": 0.83,
                "campaignId": "is6_sui",
                "fieldId": "coins",
                "coinId": "coin_a",
                "statusId": "status_a",
                "face": "back",
                "count": 2
              }
            ]
          }
        }
        """);

    Equal(4, candidates.Count, "api candidate count");
    Equal(false, candidates.Any(item => item.Field == "hope"), "api abandoned run field omitted");
    Equal("thought_a", candidates[0].ThoughtId, "thought id");
    Equal("thought_a", candidates[0].Identity, "thought identity");
    Equal("age_prime", candidates[1].AgeId, "age id");
    Equal("rhetoric_a", candidates[2].EffectId, "revelation effect id");
    Equal("coin_a", candidates[3].CoinId, "coin id");
    Equal("status_a", candidates[3].StatusId, "coin status id");
    Equal("back", candidates[3].Face, "coin face");
    Equal(2, candidates[3].Count, "coin count");
}

static void CandidateMergerSupplementsLocalCandidates()
{
    var merged = RhodesMaaCandidateMerger.Merge(
        [
            new MaaCandidatePreview("runStatus", "源石錐", "20", "20", 0.94, Field: "ingot"),
            new MaaCandidatePreview("operator", "グム", "gummy", "グム", 0.91, OperatorId: "gummy"),
            new MaaCandidatePreview("thought", "枯れ木と若枝", "fallback", "枯れ木と若枝", 0.91, CampaignId: "is5_sarkaz", ThoughtId: "thought_a"),
            new MaaCandidatePreview("revelation", "修辞A", "fallback", "修辞A", 0.91, CampaignId: "is4_sami", FieldId: "revelationBoard", SlotKind: "rhetoric", EffectId: "rhetoric_a"),
            new MaaCandidatePreview("coin", "通宝A", "fallback", "通宝A", 0.91, CampaignId: "is6_sui", CoinId: "coin_a", Count: 1),
        ],
        [
            new MaaCandidatePreview("runStatus", "源石錐", "21", "21", 0.99, Field: "ingot"),
            new MaaCandidatePreview("runStatus", "等級", "18", "18", 0.92, Field: "difficulty"),
            new MaaCandidatePreview("operator", "グム", "gummy", "グム", 0.99, OperatorId: "gummy"),
            new MaaCandidatePreview("operator", "セイリュウ", "purestream", "セイリュウ", 0.88, OperatorId: "purestream"),
            new MaaCandidatePreview("thought", "枯れ木と若枝", "fallback", "枯れ木と若枝", 0.88, CampaignId: "is5_sarkaz", ThoughtId: "thought_a"),
            new MaaCandidatePreview("thought", "走る都市", "fallback", "走る都市", 0.87, CampaignId: "is5_sarkaz", ThoughtId: "thought_b"),
            new MaaCandidatePreview("age", "天災の時代（全盛期）", "age_prime", "天災の時代（全盛期）", 0.9, CampaignId: "is5_sarkaz", AgeId: "age_prime"),
            new MaaCandidatePreview("revelation", "修辞A", "fallback", "修辞A", 0.99, CampaignId: "is4_sami", FieldId: "revelation", SlotKind: "rhetoric", EffectId: "rhetoric_a"),
            new MaaCandidatePreview("revelation", "本因A", "fallback", "本因A", 0.89, CampaignId: "is4_sami", FieldId: "revelation", SlotKind: "cause", EffectId: "cause_a"),
            new MaaCandidatePreview("coin", "通宝A", "fallback", "通宝A", 0.99, CampaignId: "is6_sui", CoinId: "coin_a", Count: 1),
            new MaaCandidatePreview("coin", "通宝A裏", "fallback", "通宝A", 0.89, CampaignId: "is6_sui", CoinId: "coin_a", Face: "back", Count: 1),
        ]);

    Equal("ingot|difficulty", string.Join("|", merged.Where(item => item.Kind == "runStatus").Select(item => item.Field)), "merged run status fields");
    Equal("gummy|purestream", string.Join("|", merged.Where(item => item.Kind == "operator").Select(item => item.OperatorId)), "merged operators");
    Equal("thought_a|thought_b", string.Join("|", merged.Where(item => item.Kind == "thought").Select(item => item.ThoughtId)), "local MAA thought set replaces primary thought candidates");
    Equal("age_prime", string.Join("|", merged.Where(item => item.Kind == "age").Select(item => item.AgeId)), "local age supplemented");
    Equal("rhetoric_a|cause_a", string.Join("|", merged.Where(item => item.Kind == "revelation").Select(item => item.EffectId)), "merged revelation candidates");
    Equal("coin_a", string.Join("|", merged.Where(item => item.Kind == "coin").Select(item => item.CoinId)), "merged coin candidates ignore obsolete face");
}

static void CandidateMergerPrefersLocalRelicForSameEvidence()
{
    var merged = RhodesMaaCandidateMerger.Merge(
        [
            new MaaCandidatePreview(
                "relic",
                "",
                "緋滲む貴石",
                "緋滲む貴石（改)",
                0.98,
                RelicId: "is2_phantom_relic_185",
                CampaignId: "is2_phantom",
                RecognitionKey: "relic:is2_phantom_relic_185"),
            new MaaCandidatePreview(
                "relic",
                "",
                "緋滲む貴石（改）",
                "緋滲む貴石（改)",
                0.98,
                RelicId: "is2_phantom_relic_186",
                CampaignId: "is2_phantom",
                RecognitionKey: "relic:is2_phantom_relic_186"),
        ],
        [
            new MaaCandidatePreview(
                "relic",
                "緋滲む貴石（改）",
                "is2_phantom_relic_186",
                "緋滲む貴石（改)",
                0.96,
                RelicId: "is2_phantom_relic_186",
                CampaignId: "is2_phantom",
                RecognitionKey: "maa-local:relic:is2_phantom_relic_186"),
            new MaaCandidatePreview(
                "relic",
                "苦行者のシュー",
                "is2_phantom_relic_080",
                "苦行者のシユ正",
                0.89,
                RelicId: "is2_phantom_relic_080",
                CampaignId: "is2_phantom",
                RecognitionKey: "maa-local:relic:is2_phantom_relic_080"),
        ]);

    Equal(
        "is2_phantom_relic_186|is2_phantom_relic_080",
        string.Join("|", merged.Where(item => item.Kind == "relic").Select(item => item.RelicId)),
        "local relic resolution removes a conflicting API variant from the same OCR row");
}

static void CandidateMergerPreservesRelicUsageEvidence()
{
    var merged = RhodesMaaCandidateMerger.Merge(
        [
            new MaaCandidatePreview(
                "relic",
                "「時の果て」",
                "is3_mizuki_relic_228",
                "[時の果て",
                0.98,
                RelicId: "is3_mizuki_relic_228",
                CampaignId: "is3_mizuki"),
        ],
        [
            new MaaCandidatePreview(
                "relic",
                "「時の果て」",
                "is3_mizuki_relic_228",
                "[時の果て",
                0.97,
                RelicId: "is3_mizuki_relic_228",
                CampaignId: "is3_mizuki",
                StateId: "used"),
        ]);

    Equal("used", merged.Single().StateId, "local used marker is retained on the merged relic");
}

static void CandidateMergerPreservesReserveOperatorCounts()
{
    var merged = RhodesMaaCandidateMerger.Merge(
        [
            new MaaCandidatePreview(
                "operator",
                "予備隊員-術師",
                "reserve_caster",
                "予備隊員-術師",
                0.94,
                OperatorId: "reserve_caster"),
        ],
        [
            new MaaCandidatePreview(
                "operator",
                "予備隊員-術師",
                "reserve_caster",
                "予備隊員-術師",
                0.92,
                OperatorId: "reserve_caster",
                Count: 2),
        ]);

    Equal(1, merged.Count, "reserve operator remains a single candidate");
    Equal(2, merged.Single().Count, "local per-frame reserve count survives API candidate merge");
}

static void CandidateMergerPrefersResolvedAmiyaForm()
{
    var merged = RhodesMaaCandidateMerger.Merge(
        [
            new MaaCandidatePreview("operator", "アーミヤ", "amiya", "アーミヤ", 0.92, OperatorId: "amiya"),
            new MaaCandidatePreview("operator", "アーミヤ(前衛)", "amiya2", "アーミヤ", 0.92, OperatorId: "amiya2"),
            new MaaCandidatePreview("operator", "アーミヤ(医療)", "amiya3", "アーミヤ", 0.92, OperatorId: "amiya3"),
        ],
        [
            new MaaCandidatePreview(
                "operator",
                "アーミヤ(前衛)",
                "amiya2",
                "アーミヤ",
                0.99,
                OperatorId: "amiya2",
                RecognitionKey: "maa-local:operator-role:amiya2"),
        ]);

    Equal(
        "amiya2",
        string.Join("|", merged.Where(item => item.Kind == "operator").Select(item => item.OperatorId)),
        "profession-resolved Amiya replaces ambiguous API forms");
}

static void CandidateMergerPrefersLocalThoughtCounts()
{
    var merged = RhodesMaaCandidateMerger.Merge(
        [
            Thought("爆破", "thought_blast", "api:blast:0"),
            Thought("摂食", "thought_eat", "api:eat:0"),
            Thought("摂食", "thought_eat", "api:eat:1"),
            Thought("爆破", "thought_blast", "api:blast:1"),
            Thought("散逸", "thought_scatter", "api:scatter:0"),
            Thought("巫術", "thought_witchcraft", "api:witchcraft:0"),
        ],
        [
            Thought("爆破", "thought_blast", "maa-local:blast:0"),
            Thought("巫術", "thought_witchcraft", "maa-local:witchcraft:0"),
            Thought("散逸", "thought_scatter", "maa-local:scatter:0"),
            Thought("摂食", "thought_eat", "maa-local:eat:0"),
        ]);

    Equal(
        "爆破|巫術|散逸|摂食",
        string.Join("|", merged.Where(item => item.Kind == "thought").Select(item => item.Label)),
        "local MAA thought candidates replace legacy API slot counts");

    static MaaCandidatePreview Thought(string label, string id, string key) => new(
        "thought",
        label,
        id,
        label,
        0.99,
        CampaignId: "is5_sarkaz",
        RecognitionKey: key,
        ThoughtId: id);
}

static void CandidateMergerKeepsCampaignRunStatusFields()
{
    var merged = RhodesMaaCandidateMerger.Merge(
        [
            new MaaCandidatePreview("runStatus", "源石錐", "20", "20", 0.94, Field: "ingot"),
            new MaaCandidatePreview("runStatus", "サーミ等級", "12", "12", 0.94, Field: "difficulty", CampaignId: "is4_sami"),
        ],
        [
            new MaaCandidatePreview("runStatus", "サルカズ等級", "18", "18", 0.92, Field: "difficulty", CampaignId: "is5_sarkaz"),
            new MaaCandidatePreview("runStatus", "サーミ等級低信頼", "14", "14", 0.80, Field: "difficulty", CampaignId: "is4_sami"),
            new MaaCandidatePreview("runStatus", "源石錐低信頼", "21", "21", 0.80, Field: "ingot"),
        ]);

    Equal(
        "ingot::20|difficulty:is4_sami:12|difficulty:is5_sarkaz:18",
        string.Join("|", merged.Where(item => item.Kind == "runStatus").Select(item => $"{item.Field}:{item.CampaignId}:{item.Value}")),
        "campaign-specific run status fields");
}

static void RecognitionWorkflowRunsResourceTasks()
{
    var plan = new MaaResourceExecutionPlan(
        "runStatusFull",
        "基礎情報",
        "interface preset",
        ["TaskA", "TaskB"],
        [
            new MaaResourceTaskPreview("TaskA", "A", "first"),
            new MaaResourceTaskPreview("TaskB", "B", "second"),
        ],
        "");
    Equal(MaaResourceExecutionPlan.ReadyState, plan.State, "execution plan defaults ready");
    Equal("実行可能", plan.StateLabel, "execution plan ready label");
    Equal(true, plan.Summary.StartsWith("実行可能:", StringComparison.Ordinal), "execution plan summary includes state");
    var invoked = new List<string>();
    var observed = new List<string>();

    var result = RhodesRecognitionWorkflow.RunResourceTasksAsync(
        plan,
        (entry, _) =>
        {
            invoked.Add(entry);
            return Task.FromResult(new MaaTaskRunResult(entry, "Succeeded", true, $"detail:{entry}"));
        },
        taskResult => observed.Add(taskResult.Entry)).GetAwaiter().GetResult();

    Equal(true, result.Succeeded, "workflow task execution succeeds");
    Equal("TaskA|TaskB", string.Join("|", invoked), "workflow invokes tasks in plan order");
    Equal("TaskA|TaskB", string.Join("|", observed), "workflow reports tasks in plan order");
    Equal("TaskA|TaskB", string.Join("|", result.TaskResults.Select(item => item.Entry)), "workflow returns task results");
    Equal("基礎情報 / tasks=2 / interface preset", result.Summary, "workflow execution summary");
}

static void RecognitionWorkflowConvertsCandidates()
{
    var apiResult = new RhodesMaaCandidateApiResult(
        [new MaaCandidatePreview("runStatus", "源石錐", "20", "20", 0.94, Field: "ingot")],
        "");
    var taskResults = new[]
    {
        OcrTask("RhodesOcrRegion_run_difficulty_grade", "18", 0.93),
    };

    var conversion = RhodesRecognitionWorkflow.ConvertCandidates("runStatusFull", taskResults, apiResult);

    Equal("api+local", conversion.Source, "workflow conversion source");
    Equal(1, conversion.ApiCandidateCount, "workflow api candidate count");
    Equal(1, conversion.LocalCandidateCount, "workflow local candidate count");
    Equal(1, conversion.SupplementalCandidateCount, "workflow supplemental candidate count");
    Equal("ingot|difficulty", string.Join("|", conversion.Candidates.Select(item => item.Field)), "workflow merged fields");
    Equal("候補化しました: 2件 (ローカル補完 +1)", conversion.StatusMessage, "workflow merged status message");
}

static void RecognitionWorkflowLocalFallback()
{
    var apiResult = new RhodesMaaCandidateApiResult([], "connection refused");
    var taskResults = new[]
    {
        OcrTask("RhodesTemplate_runStatusFull_run_ingot", "2O", 0.96),
    };

    var conversion = RhodesRecognitionWorkflow.ConvertCandidates("runStatusFull", taskResults, apiResult);

    Equal("local", conversion.Source, "workflow local fallback source");
    Equal("ingot", conversion.Candidates.Single().Field, "workflow local fallback field");
    Equal("20", conversion.Candidates.Single().Value, "workflow local fallback value");
    Equal("候補化APIに接続できないためローカル候補化しました: 1件", conversion.StatusMessage, "workflow local fallback message");
}

static void RecognitionWorkflowApplyCandidatesViaApi()
{
    var savedState = "";
    var replacedState = "";
    var localFallbackCount = 0;
    var result = RhodesRecognitionWorkflow.ApplyCandidatesAsync(
        [new MaaCandidatePreview("runStatus", "源石錐", "20", "20", 0.9, Field: "ingot")],
        _ => Task.FromResult(new RhodesStateApiResult("""{ "version": 1, "run": { "campaignId": "is5_sarkaz" } }""", "")),
        (stateJson, _) =>
        {
            savedState = stateJson;
            return Task.FromResult(new RhodesStateApiResult(stateJson, ""));
        },
        (stateJson, _) =>
        {
            replacedState = stateJson;
            return Task.CompletedTask;
        },
        (_, _) =>
        {
            localFallbackCount++;
            return Task.FromResult(SukiCandidateApplySummary.Empty);
        }).GetAwaiter().GetResult();

    Equal(1, result.Summary.AppliedCount, "workflow api apply count");
    Equal(false, result.LocalFallbackUsed, "workflow api apply avoids local fallback");
    Equal(true, result.ShouldReloadRunState, "workflow api apply reloads state");
    Equal("1件: ingot", result.LastCandidateApplySummary, "workflow api last summary");
    Equal("状態へ反映し、APIへ同期しました: 1件 (ingot)", result.StatusMessage, "workflow api status message");
    Equal(0, localFallbackCount, "workflow api local fallback calls");
    Equal(true, savedState.Contains("\"ingot\":20", StringComparison.Ordinal), "workflow api saved ingot");
    Equal(savedState, replacedState, "workflow api replaced local state");
    Equal(savedState, result.StateJson, "workflow exposes authoritative saved state");
    Equal("接続済み", result.ApiStatus?.State, "workflow api status");
}

static void RunCatalogProjectsAuthoritativeStateJson()
{
    const string hordeCallId = "is3_mizuki_selectable_hordeCall_mcasci14";
    const string rejectionId = "is3_mizuki_selectable_rejectionReaction_mcasci22";
    var catalog = RhodesRunCatalog.LoadFromStateJson(
        $$"""
        {
          "version": 1,
          "run": {
            "campaignId": "is3_mizuki",
            "special": {
              "is3_mizuki": {
                "hordeCalls": ["{{hordeCallId}}"],
                "rejectionReaction": {
                  "effectId": "{{rejectionId}}",
                  "operatorIds": ["exusiai2", "hoshiguma2"]
                }
              }
            }
          },
          "operators": ["exusiai2", "hoshiguma2"],
          "relics": []
        }
        """);

    var specialFields = catalog.Current.SpecialFields ?? [];
    var hordeCalls = specialFields.Single(field => field.FieldId == "hordeCalls");
    var rejection = specialFields.Single(field => field.FieldId == "rejectionReaction");
    Equal(hordeCallId, (hordeCalls.SelectedIds ?? []).Single(), "authoritative horde call projected");
    Equal(rejectionId, (rejection.SelectedIds ?? []).Single(), "authoritative rejection effect projected");
    Equal(
        "exusiai2|hoshiguma2",
        string.Join('|', rejection.OperatorIds ?? []),
        "authoritative rejection operators projected");
}

static void RecognitionWorkflowApplyCandidatesLocalFallback()
{
    var localFallbackCount = 0;
    var result = RhodesRecognitionWorkflow.ApplyCandidatesAsync(
        [new MaaCandidatePreview("runStatus", "源石錐", "20", "20", 0.9, Field: "ingot")],
        _ => Task.FromResult(new RhodesStateApiResult("", "connection refused")),
        (_, _) => throw new InvalidOperationException("save should not run"),
        (_, _) => throw new InvalidOperationException("replace should not run"),
        (_, _) =>
        {
            localFallbackCount++;
            return Task.FromResult(new SukiCandidateApplySummary(1, 0, ["ingot"]));
        }).GetAwaiter().GetResult();

    Equal(1, result.Summary.AppliedCount, "workflow fallback apply count");
    Equal(true, result.LocalFallbackUsed, "workflow fallback flag");
    Equal(true, result.ShouldReloadRunState, "workflow fallback reloads state");
    Equal("connection refused", result.ApiError, "workflow fallback api error");
    Equal("接続失敗", result.ApiStatus?.State, "workflow fallback api status");
    Equal(1, localFallbackCount, "workflow fallback calls local apply");
    Equal("状態へ反映しました: 1件 (ingot) / API同期失敗: connection refused", result.StatusMessage, "workflow fallback message");
}

static void RecognitionWorkflowApplyCandidatesEmpty()
{
    var result = RhodesRecognitionWorkflow.ApplyCandidatesAsync(
        [],
        _ => throw new InvalidOperationException("fetch should not run"),
        (_, _) => throw new InvalidOperationException("save should not run"),
        (_, _) => throw new InvalidOperationException("replace should not run"),
        (_, _) => throw new InvalidOperationException("local should not run")).GetAwaiter().GetResult();

    Equal(0, result.Summary.AppliedCount, "workflow empty apply count");
    Equal(false, result.ShouldReloadRunState, "workflow empty does not reload");
    Equal("反映なし: 候補0件", result.LastCandidateApplySummary, "workflow empty last summary");
    Equal("反映する候補がありません。", result.StatusMessage, "workflow empty status");
}

static void LocalCandidateConverterRunStatus()
{
    var candidates = RhodesMaaLocalCandidateConverter.FromTaskResults(
        "runStatusFull",
        [
            M("RhodesCandidate_is5_sarkaz_map_select_campaign", "サルカズの炉辺奇談", 0.98),
            M("RhodesOcrRegion_run_hope_current", "3", 0.94),
            M("RhodesOcrRegion_run_hope_max", "8", 0.95),
            M("RhodesTemplate_runStatusFull_run_ingot", "2O", 0.96),
            M("RhodesOcrRegion_run_life_points", "4", 0.91),
            M("RhodesOcrRegion_run_shield", "図", 0.89),
            M("RhodesOcrRegion_run_command_level", "I", 0.88),
            M("RhodesTemplate_runStatusFull_run_idea_current", "7", 0.90),
            M("RhodesOcrRegion_run_difficulty_grade", "18", 0.93),
            M("RhodesOcrRegion_run_squad_name", "指揮分隊", 0.92),
            M("RhodesOcrRegion_operator_name", "グム", 0.99),
        ]);

    Equal("ingot|idea|difficulty|squadId", string.Join("|", candidates.Select(item => item.Field)), "local run fields");
    Equal("20|7|18|is5_sarkaz_squad_04", string.Join("|", candidates.Select(item => item.Value)), "local run values");
    Equal("is5_sarkaz", candidates.Single(item => item.Field == "idea").CampaignId, "idea campaign id");
    Equal("is5_sarkaz", candidates.Single(item => item.Field == "squadId").CampaignId, "squad campaign id");
    Equal(false, candidates.Any(item => item.Field == "campaignId"), "campaign id is context only, not a retained candidate");
    Equal("maa-local:ingot:run.ingot", candidates.Single(item => item.Field == "ingot").RecognitionKey, "local recognition key");

    var decoratedSquad = RhodesMaaLocalCandidateConverter.FromTaskResults(
        "runStatusFull",
        [
            M("RhodesCandidate_is5_sarkaz_map_select_campaign", "サルカズの炉辺奇談", 0.98),
            M("RhodesOcrRegion_run_squad_name", "C奇想天外分隊）", 0.99),
        ]);
    Equal("奇想天外分隊", decoratedSquad.Single(item => item.Field == "squadId").Label, "decorated squad OCR resolves by unique containment");

    var positionSquad = RhodesMaaLocalCandidateConverter.FromTaskResults(
        "runStatusFull",
        [
            M("RhodesCandidate_is5_sarkaz_map_select_campaign", "サルカズの炉辺奇談", 0.98),
            M("RhodesOcrRegion_run_squad_name", "_", 0.31),
            M("RhodesOcrRegion_run_squad_card", "スポット更新回数+1、初期構想+Ⅰ、初期希望 +1。各スポットの初回更新時に黄想を消眷し ない", 0.83),
        ]);
    Equal("位置測定分隊", positionSquad.Single(item => item.Field == "squadId").Label, "squad effect OCR resolves position squad");
    Equal("is5_sarkaz_squad_03", positionSquad.Single(item => item.Field == "squadId").Value, "squad effect OCR resolves position squad id");

    var ambiguousSquad = RhodesMaaLocalCandidateConverter.FromTaskResults(
        "runStatusFull",
        [
            M("RhodesCandidate_is5_sarkaz_map_select_campaign", "サルカズの炉辺奇談", 0.98),
            M("RhodesOcrRegion_run_squad_card", "初期希望+1", 0.90),
        ]);
    Equal(false, ambiguousSquad.Any(item => item.Field == "squadId"), "short squad effect fragment is not applied");

    var mizukiSquad = RhodesMaaLocalCandidateConverter.FromTaskResults(
        "runStatusFull",
        [M("RhodesOcrRegion_run_squad_name", "最大活用分隊", 0.96)],
        "is3_mizuki");
    Equal("is3_mizuki_squad_02", mizukiSquad.Single(item => item.Field == "squadId").Value, "selected campaign resolves Mizuki squad without a campaign marker");
    Equal("is3_mizuki", mizukiSquad.Single(item => item.Field == "squadId").CampaignId, "selected campaign is retained on Mizuki squad");

    static MaaTaskRunResult M(string entry, string text, double score)
    {
        return new MaaTaskRunResult(
            entry,
            "Succeeded",
            true,
            "detail",
            $"TaskId=1; detail={{\"best\":{{\"text\":\"{text}\",\"score\":{score.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}}}",
            "OCR",
            true);
    }
}

static void LocalCandidateConverterIgnoresGenericSuiIngot()
{
    var candidates = RhodesMaaLocalCandidateConverter.FromTaskResults(
        "runStatusFull",
        [M("RhodesTemplate_runStatusFull_run_ingot", "3", 0.96)],
        "is6_sui");

    Equal(false, candidates.Any(item => item.Field == "ingot"), "Sui ignores the shared ingot ROI");

    static MaaTaskRunResult M(string entry, string text, double score)
    {
        return new MaaTaskRunResult(
            entry,
            "Succeeded",
            true,
            "detail",
            $"{{\"best\":{{\"text\":\"{text}\",\"score\":{score.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}}}",
            "OCR",
            true);
    }
}

static void LocalCandidateConverterRunStatusBestDuplicate()
{
    var candidates = RhodesMaaLocalCandidateConverter.FromTaskResults(
        "runStatusFull",
        [
            M("RhodesOcrRegion_run_ingot", "12", 0.40),
            M("RhodesTemplate_runStatusFull_run_ingot", "20", 0.96),
        ]);

    Equal("ingot", string.Join("|", candidates.Select(item => item.Field)), "best duplicate fields");
    Equal("20", string.Join("|", candidates.Select(item => item.Value)), "best duplicate values");

    static MaaTaskRunResult M(string entry, string text, double score)
    {
        return new MaaTaskRunResult(
            entry,
            "Succeeded",
            true,
            "detail",
            $"TaskId=1; detail={{\"best\":{{\"text\":\"{text}\",\"score\":{score.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}}}",
            "OCR",
            true);
    }
}

static void LocalCandidateConverterNormalizesNumericDrift()
{
    var candidates = RhodesMaaLocalCandidateConverter.FromTaskResults(
        "runStatusFull",
        [
            M("RhodesOcrRegion_run_ingot", "2u", 0.91),
            M("RhodesOcrRegion_run_difficulty_grade", "I8", 0.82),
        ]);

    Equal("20", candidates.Single(item => item.Field == "ingot").Value, "lowercase u normalized to zero");
    Equal("18", candidates.Single(item => item.Field == "difficulty").Value, "uppercase i normalized to one");

    static MaaTaskRunResult M(string entry, string text, double score) => new(
        entry,
        "Succeeded",
        true,
        "detail",
        $"{{\"best\":{{\"text\":\"{text}\",\"score\":{score.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}}}",
        "OCR",
        true);
}

static void LocalCandidateConverterNormalizesSquadDrift()
{
    var result = new MaaTaskRunResult(
        "RhodesOcrRegion_run_squad_name",
        "Succeeded",
        true,
        "detail",
        """{"best":{"text":"c破悪成金分隊","score":0.88}}""",
        "OCR",
        true);
    var candidate = RhodesMaaLocalCandidateConverter.FromTaskResults("runStatusFull", [result]).Single();

    Equal("squadId", candidate.Field, "squad drift field");
    Equal("破棘成金分隊", candidate.Label, "squad drift label");
    Equal("is5_sarkaz_squad_14", candidate.Value, "squad drift id");

    var measuredRoiResult = new MaaTaskRunResult(
        "RhodesOcrRegion_run_squad_name",
        "Succeeded",
        true,
        "detail",
        """{"best":{"text":"調棘成金分秒","score":0.78}}""",
        "OCR",
        true);
    var measuredRoiCandidate = RhodesMaaLocalCandidateConverter.FromTaskResults(
        "runStatusFull",
        [measuredRoiResult]).Single();
    Equal("破棘成金分隊", measuredRoiCandidate.Label, "measured squad ROI drift label");
}

static void LocalCandidateConverterPrefersSquadIconTemplate()
{
    var candidates = RhodesMaaLocalCandidateConverter.FromTaskResults(
        "runStatusFull",
        [
            M("RhodesCandidate_is5_sarkaz_map_select_campaign", "サルカズの炉辺奇談", 0.98, "OCR"),
            M("RhodesOcrRegion_run_squad_name", "支援分隊", 0.99, "OCR"),
            M(
                "RhodesTemplate_runStatusFull_run_squad_icon_is5_sarkaz_squad_14",
                "",
                0.83,
                "TemplateMatch"),
        ]);

    var squad = candidates.Single(item => item.Field == "squadId");
    Equal("is5_sarkaz_squad_14", squad.Value, "right-half icon template wins over conflicting squad OCR");
    Equal("破棘成金分隊", squad.Label, "icon template resolves the local squad label");

    static MaaTaskRunResult M(string entry, string text, double score, string algorithm)
    {
        var detail = algorithm == "TemplateMatch"
            ? System.Text.Json.JsonSerializer.Serialize(new
            {
                best = new { box = new[] { 62, 636, 42, 84 }, score },
                filtered = new[] { new { box = new[] { 62, 636, 42, 84 }, score } },
            })
            : System.Text.Json.JsonSerializer.Serialize(new { best = new { text, score } });
        return new MaaTaskRunResult(entry, "Succeeded", true, "detail", detail, algorithm, true);
    }
}

static void LocalCandidateConverterKeepsStrongestMizukiSquadIcon()
{
    var candidates = RhodesMaaLocalCandidateConverter.FromTaskResults(
        "runStatusFull",
        [
            Template("RhodesTemplate_runStatusFull_run_squad_icon_is3_mizuki_squad_01", 0.664720),
            Template("RhodesTemplate_runStatusFull_run_squad_icon_is3_mizuki_squad_03", 0.711742),
            Template("RhodesTemplate_runStatusFull_run_squad_icon_is3_mizuki_squad_12", 0.667841),
        ],
        "is3_mizuki");

    var squad = candidates.Single(item => item.Field == "squadId");
    Equal("is3_mizuki_squad_03", squad.Value, "strongest Mizuki icon resolves the squad");
    Equal("人文主義分隊", squad.Label, "strongest Mizuki icon resolves the label");

    static MaaTaskRunResult Template(string entry, double score)
    {
        var detail = System.Text.Json.JsonSerializer.Serialize(new
        {
            best = new { box = new[] { 15, 645, 67, 67 }, score },
            filtered = new[] { new { box = new[] { 15, 645, 67, 67 }, score } },
        });
        return new MaaTaskRunResult(entry, "Succeeded", true, "detail", detail, "TemplateMatch", true);
    }
}

static void LocalCandidateConverterDecodesBatchedSquadTemplate()
{
    var childResults = new JsonArray();
    for (var index = 0; index < 17; index++)
    {
        childResults.Add(index == 13
            ? JsonNode.Parse("""
              {
                "algorithm": "TemplateMatch",
                "box": [62, 636, 42, 84],
                "detail": {
                  "best": { "box": [62, 636, 42, 84], "score": 0.905478 }
                }
              }
              """)
            : JsonNode.Parse("""{ "algorithm": "TemplateMatch" }"""));
    }

    var candidates = RhodesMaaLocalCandidateConverter.FromTaskResults(
        "runStatusFull",
        [
            new MaaTaskRunResult(
                "RhodesCandidate_is5_sarkaz_map_select_campaign",
                "Succeeded",
                true,
                "detail",
                """{"best":{"text":"サルカズの炉辺奇談","score":0.98}}""",
                "OCR",
                true),
            new MaaTaskRunResult(
                "RhodesTemplate_runStatusFull_run_squad_icon_is5_sarkaz_batch",
                "Succeeded",
                true,
                "detail",
                childResults.ToJsonString(),
                "Or",
                true),
        ]);

    var squad = candidates.Single(item => item.Field == "squadId");
    Equal("is5_sarkaz_squad_14", squad.Value, "batch child index resolves squad id");
    Equal("破棘成金分隊", squad.Label, "batch child resolves squad label");
    Equal(true, squad.Confidence is > 0.90 and < 0.91, "batch template keeps native score");
}

static void LocalCandidateConverterDecodesBatchedSuiSquadTemplate()
{
    var childResults = new JsonArray();
    for (var index = 0; index < 19; index++)
    {
        childResults.Add(index == 18
            ? JsonNode.Parse("""
              {
                "algorithm": "TemplateMatch",
                "box": [62, 636, 42, 84],
                "detail": {
                  "best": { "box": [62, 636, 42, 84], "score": 0.913 }
                }
              }
              """)
            : JsonNode.Parse("""{ "algorithm": "TemplateMatch" }"""));
    }

    var candidates = RhodesMaaLocalCandidateConverter.FromTaskResults(
        "runStatusFull",
        [
            new MaaTaskRunResult(
                "RhodesTemplate_runStatusFull_run_squad_icon_is6_sui_batch",
                "Succeeded",
                true,
                "detail",
                childResults.ToJsonString(),
                "Or",
                true),
        ],
        "is6_sui");

    var squad = candidates.Single(item => item.Field == "squadId");
    Equal("is6_sui_squad_19", squad.Value, "Sui batch child index resolves squad id");
    Equal("商人分隊", squad.Label, "Sui batch child resolves squad label");
    Equal(true, squad.Confidence is > 0.91 and < 0.92, "Sui batch template keeps native score");
}

static void LocalCandidateConverterBoundsSarkazDifficulty()
{
    var repaired = RhodesMaaLocalCandidateConverter.FromTaskResults(
        "runStatusFull",
        [
            M("RhodesCandidate_is5_sarkaz_map_select_campaign", "サルカズの炉辺奇談", 0.98),
            M("run.difficulty.grade.anchor.0", "78", 0.96),
        ]);
    Equal("18", repaired.Single(item => item.Field == "difficulty").Value, "anchored 78 is repaired to Sarkaz difficulty 18");

    var rejected = RhodesMaaLocalCandidateConverter.FromTaskResults(
        "runStatusFull",
        [
            M("RhodesCandidate_is5_sarkaz_map_select_campaign", "サルカズの炉辺奇談", 0.98),
            M("run.difficulty.grade.anchor.0", "98", 0.96),
        ]);
    Equal(false, rejected.Any(item => item.Field == "difficulty"), "unexplained out-of-range difficulty remains rejected");

    var accepted = RhodesMaaLocalCandidateConverter.FromTaskResults(
        "runStatusFull",
        [
            M("RhodesCandidate_is5_sarkaz_map_select_campaign", "サルカズの炉辺奇談", 0.98),
            M("run.difficulty.grade.anchor.0", "18", 0.96),
        ]);
    Equal("18", accepted.Single(item => item.Field == "difficulty").Value, "anchored Sarkaz difficulty 18 is accepted");

    var caretDrift = RhodesMaaLocalCandidateConverter.FromTaskResults(
        "runStatusFull",
        [
            M("RhodesCandidate_is5_sarkaz_map_select_campaign", "サルカズの炉辺奇談", 0.98),
            M("run.difficulty.grade.anchor.0", "^8", 0.71),
        ]);
    Equal("18", caretDrift.Single(item => item.Field == "difficulty").Value, "anchored caret-shaped leading one is repaired");

    static MaaTaskRunResult M(string entry, string text, double score) => new(
        entry,
        "Succeeded",
        true,
        "detail",
        System.Text.Json.JsonSerializer.Serialize(new { best = new { text, score } }),
        "OCR",
        true);
}

static void LocalCandidateConverterBoundsSuiDifficulty()
{
    var accepted = RhodesMaaLocalCandidateConverter.FromTaskResults(
        "runStatusFull",
        [M("run.difficulty.grade.anchor.0", "18", 0.96)],
        "is6_sui");
    Equal("18", accepted.Single(item => item.Field == "difficulty").Value, "anchored Sui difficulty 18 is accepted");

    var repaired = RhodesMaaLocalCandidateConverter.FromTaskResults(
        "runStatusFull",
        [M("run.difficulty.grade.anchor.0", "78", 0.96)],
        "is6_sui");
    Equal("18", repaired.Single(item => item.Field == "difficulty").Value, "anchored Sui 78 is repaired to difficulty 18");

    var rejected = RhodesMaaLocalCandidateConverter.FromTaskResults(
        "runStatusFull",
        [M("run.difficulty.grade.anchor.0", "79", 0.96)],
        "is6_sui");
    Equal(false, rejected.Any(item => item.Field == "difficulty"), "Sui rejects values beyond difficulty 18");

    static MaaTaskRunResult M(string entry, string text, double score) => new(
        entry,
        "Succeeded",
        true,
        "detail",
        System.Text.Json.JsonSerializer.Serialize(new { best = new { text, score } }),
        "OCR",
        true);
}

static void LocalCandidateConverterRunStatusSquadRandomEffect()
{
    var candidates = RhodesMaaLocalCandidateConverter.FromTaskResults(
        "runStatusFull",
        [
            M("RhodesCandidate_is5_sarkaz_map_select_campaign", "サルカズの炉辺奇談", 0.98),
            M("RhodesOcrRegion_run_squad_name", "奇 想 天 外 分 隊", 0.92),
            M("RhodesOcrRegion_run_squad_card", "★4以上の【術師】を招集時に消費する希望-2、昇進時に消費する希望-1、【術師】を初めて招集する際、昇進済の状態で招集できる。初めから「生還者の契約」を所持", 0.90),
        ]);

    Equal("squadId|squadRandomEffectOptionId", string.Join("|", candidates.Select(item => item.Field)), "local squad random fields");
    Equal("is5_sarkaz_squad_16|is5_sarkaz_mimic_02", string.Join("|", candidates.Select(item => item.Value)), "local squad random values");

    var iconAnchoredCandidates = RhodesMaaLocalCandidateConverter.FromTaskResults(
        "runStatusFull",
        [
            M("RhodesCandidate_is5_sarkaz_map_select_campaign", "サルカズの炉辺奇談", 0.98),
            Template("RhodesTemplate_runStatusFull_run_squad_icon_is5_sarkaz_squad_16", 0.87),
            M("RhodesOcrRegion_run_squad_name", "_", 0.31),
            M("RhodesOcrRegion_run_squad_card", "★4以上の【術師】を招集時に消費する希望-2、昇進時に消費する希望-1、【術師】を初めて招集する際、昇進済の状態で招集できる。初めから「生還者の契約」を所持", 0.90),
        ]);
    Equal(
        "is5_sarkaz_squad_16|is5_sarkaz_mimic_02",
        string.Join("|", iconAnchoredCandidates.Select(item => item.Value)),
        "squad icon identifies the random-effect squad without readable name OCR");

    static MaaTaskRunResult M(string entry, string text, double score)
    {
        return new MaaTaskRunResult(
            entry,
            "Succeeded",
            true,
            "detail",
            $"TaskId=1; detail={{\"best\":{{\"text\":\"{text}\",\"score\":{score.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}}}",
            "OCR",
            true);
    }

    static MaaTaskRunResult Template(string entry, double score) => new(
        entry,
        "Succeeded",
        true,
        "detail",
        System.Text.Json.JsonSerializer.Serialize(new
        {
            best = new { box = new[] { 62, 636, 42, 84 }, score },
            filtered = new[] { new { box = new[] { 62, 636, 42, 84 }, score } },
        }),
        "TemplateMatch",
        true);
}

static void LocalCandidateConverterOperators()
{
    Equal(
        RhodesOperatorOcrNormalizer.Normalize("トラゴーディア"),
        RhodesOperatorOcrNormalizer.Normalize("下ラコ―ディア"),
        "measured Tragodia OCR normalization");
    var candidates = RhodesMaaLocalCandidateConverter.FromTaskResults(
        "operatorsFull",
        [
            M("RhodesRunStatusTopBarOcr", "メイ", 0.99),
            M("RhodesOcrRegion_operator_name_left_1", "グム", 0.91),
            M("RhodesOcrRegion_operator_name_center_1", "セイリュウ", 0.92),
            M("RhodesOcrRegion_operator_name_right_1", "テンニンカ", 0.93),
            M("RhodesOcrRegion_operator_name_left_2", "ワイルド メイン", 0.94),
            M("operator.card.name.0", "—■ウタゲー）", 0.62),
            M("operator.card.name.1", "――ディピカ）", 0.78),
            M("operator.card.name.2", "―セイリュゥ", 0.95),
            M("operator.card.name.3", "―歴陣鋭棺フェン", 0.91),
            M("operator.card.name.4", "富斬業ホシグマー", 0.90),
            M("operator.card.name.5", "―ランディゴ", 0.90),
            M("operator.card.name.6", "シ+――", 0.89),
            M("operator.card.name.7", "フメィ―", 0.52),
            M("operator.card.name.8", "■ユリチニル", 0.75),
            M("operator.card.name.9", "下ラコ―ディア", 0.90),
            M("operator.card.name.10", "ウァン", 0.96),
            M("operator.card.name.11", "赤刃明霄チェン", 0.96),
            M("operator.card.name.12", "タラクサカム", 0.96),
            M("operator.card.name.13", "ジュー", 0.96),
        ]);

    Equal("gummy|purestream|myrtle|wildmane|utage|deepcolor|fang2|hoshiguma2|indigo|may|mlynar|phantom2|wang|ch_en3|taraxacum|ju", string.Join("|", candidates.Select(item => item.OperatorId)), "operator ids");
    Equal(16, candidates.Count, "operator candidate count");
    Equal(false, candidates.Any(item => item.OperatorId is "fang" or "hoshiguma" or "dusk"), "partial card does not split alternate operators");
    Equal("maa-local:operator:gummy", candidates[0].RecognitionKey, "operator recognition key");

    static MaaTaskRunResult M(string entry, string text, double score)
    {
        var encodedText = System.Text.Json.JsonSerializer.Serialize(text);
        return new MaaTaskRunResult(
            entry,
            "Succeeded",
            true,
            "detail",
            $"{{\"filtered_results\":[{{\"text\":{encodedText},\"score\":{score.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}]}}",
            "OCR",
            true);
    }
}

static void LocalCandidateConverterCountsReserveOperators()
{
    var candidates = RhodesMaaLocalCandidateConverter.FromTaskResults(
        "operatorsFull",
        [
            M("RhodesTemplate_operatorsFull_operator_card_name", "", 0.99),
            M("operator.card.name.3", "予備隊員-術師", 0.92),
            M("operator.card.name.6", "予備隊員-術師", 0.91),
            M("operator.card.name.1", "グム", 0.96),
            M("RhodesTemplate_operatorsFull_operator_card_name", "", 0.99),
            M("operator.card.name.5", "予備隊員-術師", 0.93),
            M("operator.card.name.6", "予備隊員-術師", 0.94),
            M("operator.card.name.1", "グム", 0.95),
            M("RhodesTemplate_operatorsFull_operator_card_name", "", 0.99),
            M("operator.card.name.3", "予備隊員-術師", 0.90),
            M("operator.card.name.1", "グム", 0.95),
        ]);

    var reserve = candidates.Single(item => item.OperatorId == "reserve_caster");
    var gummy = candidates.Single(item => item.OperatorId == "gummy");
    Equal(2, reserve.Count, "maximum simultaneous reserve caster cards");
    Equal(0, gummy.Count, "ordinary operator duplicates do not become recruited counts");

    static MaaTaskRunResult M(string entry, string text, double score)
    {
        var encodedText = System.Text.Json.JsonSerializer.Serialize(text);
        return new MaaTaskRunResult(
            entry,
            "Succeeded",
            true,
            "detail",
            $"{{\"filtered_results\":[{{\"text\":{encodedText},\"score\":{score.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}]}}",
            "OCR",
            true);
    }
}

static void MaaAmiyaRoleResolverTargetsProfessionIcon()
{
    var nameRequest = new MaaDynamicOcrRequest(
        "operator.card.name.0",
        661,
        309,
        180,
        23,
        1,
        0.99);
    var nameResult = M("operator.card.name.0", "アーミヤ", 0.99);

    var request = RhodesMaaAmiyaRoleResolver.BuildRequest(nameRequest, nameResult);

    Equal(true, request is not null, "Amiya creates a profession request");
    Equal("operator.card.amiya-role.0", request?.Entry, "profession request keeps the card slot");
    using var payload = JsonDocument.Parse(request?.PayloadJson ?? "{}");
    var alternatives = payload.RootElement.GetProperty("any_of").EnumerateArray().ToArray();
    Equal("Or", payload.RootElement.GetProperty("recognition").GetString(), "profession request uses one composite match");
    Equal(3, alternatives.Length, "profession request compares three Amiya forms");
    Equal("run/AmiyaRoleCaster.png", alternatives[0].GetProperty("template").GetString(), "caster template first");
    Equal("run/AmiyaRoleMedic.png", alternatives[1].GetProperty("template").GetString(), "medic template second");
    Equal("run/AmiyaRoleWarrior.png", alternatives[2].GetProperty("template").GetString(), "warrior template third");
    Equal(
        "426|219|64|64",
        string.Join("|", alternatives[0].GetProperty("roi").EnumerateArray().Select(item => item.GetInt32())),
        "profession ROI is anchored to the measured operator card offset");
    Equal(
        null,
        RhodesMaaAmiyaRoleResolver.BuildRequest(nameRequest, M("operator.card.name.0", "クォーツ", 0.99)),
        "non-Amiya cards do not pay for profession matching");

    static MaaTaskRunResult M(string entry, string text, double score) => new(
        entry,
        "Succeeded",
        true,
        "detail",
        $"{{\"filtered_results\":[{{\"text\":{JsonSerializer.Serialize(text)},\"score\":{score.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}]}}",
        "OCR",
        true);
}

static void LocalCandidateConverterDisambiguatesAmiyaForms()
{
    Equal("amiya", Resolve(0), "caster Amiya id");
    Equal("amiya3", Resolve(1), "medic Amiya id");
    Equal("amiya2", Resolve(2), "warrior Amiya id");

    var fallback = RhodesMaaLocalCandidateConverter.FromTaskResults(
        "operatorsFull",
        [Name("operator.card.name.0", "アーミヤ")]);
    Equal("amiya", fallback.Single().OperatorId, "missing profession evidence preserves caster fallback");

    var mixedEvidence = RhodesMaaLocalCandidateConverter.FromTaskResults(
        "operatorsFull",
        [
            Name("RhodesOcrRegion_operator_name_left_1", "アーミヤ"),
            Name("operator.card.name.0", "アーミヤ"),
            Role("operator.card.amiya-role.0", 2),
        ]);
    Equal(
        "amiya2",
        string.Join("|", mixedEvidence.Select(item => item.OperatorId)),
        "profession evidence suppresses generic Amiya fallback candidates");
    Equal(
        "maa-local:operator-role:amiya2",
        mixedEvidence.Single().RecognitionKey,
        "profession-resolved Amiya candidate records its evidence source");

    static string? Resolve(int roleIndex)
    {
        var results = new List<MaaTaskRunResult>
        {
            Name("operator.card.name.0", "アーミヤ"),
            Role("operator.card.amiya-role.0", roleIndex),
        };
        return RhodesMaaLocalCandidateConverter.FromTaskResults("operatorsFull", results).Single().OperatorId;
    }

    static MaaTaskRunResult Name(string entry, string text) => new(
        entry,
        "Succeeded",
        true,
        "detail",
        $"{{\"filtered_results\":[{{\"text\":{JsonSerializer.Serialize(text)},\"score\":0.99}}]}}",
        "OCR",
        true);

    static MaaTaskRunResult Role(string entry, int roleIndex)
    {
        var children = Enumerable.Range(0, 3)
            .Select(index => index == roleIndex
                ? new { best = new { box = new[] { 438, 229, 32, 32 }, score = 0.96 } }
                : null)
            .ToArray();
        return new MaaTaskRunResult(
            entry,
            "Succeeded",
            true,
            "detail",
            JsonSerializer.Serialize(new { result = children }),
            "Or",
            true);
    }
}

static void LocalCandidateConverterRelics()
{
    var catalog = RhodesRunCatalog.LoadDefault();
    var relics = catalog.Relics
        .Where(item => item.CampaignId == catalog.Current.CampaignId)
        .Take(2)
        .ToArray();
    Equal(true, relics.Length >= 2, "current campaign relic fixtures");

    var candidates = RhodesMaaLocalCandidateConverter.FromTaskResults(
        "relicsFull",
        [
            M("RhodesRunStatusTopBarOcr", relics[0].Name, 0.99),
            M("RhodesOcrRegion_relic_list_text", $"No.001 {relics[0].Name}\n{relics[1].Name}", 0.88),
        ]);

    Equal($"{relics[0].Id}|{relics[1].Id}", string.Join("|", candidates.Select(item => item.RelicId)), "relic ids");
    Equal("relic|relic", string.Join("|", candidates.Select(item => item.Kind)), "relic kinds");
    Equal(catalog.Current.CampaignId, candidates[0].CampaignId, "relic campaign id");
    Equal($"maa-local:relic:{relics[0].Id}", candidates[0].RecognitionKey, "relic recognition key");

    var kanaDriftCandidates = RhodesMaaLocalCandidateConverter.FromTaskResults(
        "relicsFull",
        [M("RhodesOcrRegion_relic_list_text", "イレーシユのうわ言\n探索者のリユツク", 0.96)]);
    Equal(
        "イレーシュのうわ言|探索者のリュック",
        string.Join("|", kanaDriftCandidates.Select(item => item.Label)),
        "relic small kana drift");

    var measuredAnchorDrift = RhodesMaaLocalCandidateConverter.FromTaskResults(
        "relicsFull",
        [M("RhodesOcrRegion_relic_list_text", "五秒前のための錯", 0.996)]);
    Equal("is5_sarkaz_relic_223", measuredAnchorDrift.Single().RelicId, "long relic name tolerates one measured kanji drift");

    var liveRelicDrift = RhodesMaaLocalCandidateConverter.FromTaskResults(
        "relicsFull",
        [M("RhodesOcrRegion_relic_list_text", "幸運のコイン導き\n奇妙なくるくるお面", 0.97)]);
    Equal(
        "幸運のコイン|奇妙なぐるぐるお面",
        string.Join("|", liveRelicDrift.Select(item => item.Label)),
        "live relic suffix and voiced-kana drift");

    var middleDotDrift = RhodesMaaLocalCandidateConverter.FromTaskResults(
        "relicsFull",
        [M("RhodesOcrRegion_relic_list_text", "リーダーモーガン…ラム", 0.95)]);
    Equal("リーダーモーガン・ラム", middleDotDrift.Single().Label, "relic middle dot ellipsis drift");

    var latinCaseDrift = RhodesMaaLocalCandidateConverter.FromTaskResults(
        "relicsFull",
        [M("RhodesOcrRegion_relic_list_text", "BIaZeのチェーンソ\nBaZeのチェーンソ", 0.90)],
        "is6_sui");
    Equal(
        "Blazeのチェーンソー",
        latinCaseDrift.Single().Label,
        "relic latin case and trailing long-vowel drift");

    var liveLatinHomoglyphDrift = RhodesMaaLocalCandidateConverter.FromTaskResults(
        "relicsFull",
        [M("RhodesOcrRegion_relic_list_text", "Mwс2", 0.91)],
        "is3_mizuki");
    Equal(
        "Mvc2",
        liveLatinHomoglyphDrift.Single().Label,
        "relic Latin name tolerates Cyrillic homoglyph OCR drift");

    var measuredMizukiRelicDrift = RhodesMaaLocalCandidateConverter.FromTaskResults(
        "relicsFull",
        [M("RhodesOcrRegion_relic_list_text", "“輝カしきカジミエーシュi\nプレインスーヒ―圭ャンディ", 0.90)],
        "is3_mizuki");
    Equal(
        "is3_mizuki_relic_120|is3_mizuki_relic_004",
        string.Join("|", measuredMizukiRelicDrift.Select(item => item.RelicId)),
        "measured Mizuki relic OCR drift keeps both visible relics");

    var singleRelicDetail = RhodesMaaLocalCandidateConverter.FromTaskResults(
        "relicsFull",
        [M("RhodesOcrRegion_relic_detail_name", "意欲の天秤", 0.93)]);
    Equal("is5_sarkaz_relic_225", singleRelicDetail.Single().RelicId, "single relic detail screen name");

    var publicDebugReport = RhodesMaaLocalCandidateConverter.FromTaskResults(
        "relicsFull",
        [
            MR(
                "RhodesOcrRegion_relic_list_text",
                [
                    ("支援補給所", 0.997779),
                    ("奥義の手", 0.999364),
                    ("折戟・鋒刃", 0.999927),
                    ("赤い蝶リボン", 0.998948),
                    ("「門]5と「救難]", 0.842835),
                    ("破壊協議命制圧", 0.866392),
                    ("理想の時代への未練.5", 0.916961),
                ])
        ]);
    Equal(
        "支援補給所|奥義の手|折戟・鋒刃|赤い蝶リボン|「門」と「救難」|破壊協議・制圧|理想の時代への未練",
        string.Join("|", publicDebugReport.Select(item => item.Label)),
        "public debug relic report converts recognized names");

    var usedRunSavingRelic = RhodesMaaLocalCandidateConverter.FromTaskResults(
        "relicsFull",
        [
            MRS(
                "RhodesOcrRegion_relic_list_text",
                [
                    ("[時の果て", 0.976846, 968, 367, 94, 20),
                    ("使用", 0.999977, 1156, 369, 33, 19),
                ]),
        ],
        "is3_mizuki");
    Equal("is3_mizuki_relic_228", usedRunSavingRelic.Single().RelicId, "run-saving relic id");
    Equal("used", usedRunSavingRelic.Single().StateId, "same-row used marker is attached to the relic");

    var unusedRunSavingRelic = RhodesMaaLocalCandidateConverter.FromTaskResults(
        "relicsFull",
        [MRS("RhodesOcrRegion_relic_list_text", [("[時の果て", 0.98, 968, 367, 94, 20)])],
        "is3_mizuki");
    Equal("unused", unusedRunSavingRelic.Single().StateId, "visible run-saving relic without marker is unused");

    var usedGateAndRescue = RhodesMaaLocalCandidateConverter.FromTaskResults(
        "relicsFull",
        [
            MRS(
                "RhodesOcrRegion_relic_list_text",
                [
                    ("「門」と「救難」", 0.98, 915, 248, 142, 20),
                    ("使用済", 0.99, 1150, 248, 48, 20),
                ]),
        ],
        "is5_sarkaz");
    Equal("is5_sarkaz_relic_265", usedGateAndRescue.Single().RelicId, "gate and rescue relic id");
    Equal("used", usedGateAndRescue.Single().StateId, "gate and rescue used marker is attached to the relic");

    static MaaTaskRunResult M(string entry, string text, double score)
    {
        var encodedText = System.Text.Json.JsonSerializer.Serialize(text);
        return new MaaTaskRunResult(
            entry,
            "Succeeded",
            true,
            "detail",
            $"{{\"filtered_results\":[{{\"text\":{encodedText},\"score\":{score.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}]}}",
            "OCR",
            true);
    }

    static MaaTaskRunResult MR(string entry, IReadOnlyList<(string Text, double Score)> rows)
    {
        var filtered = System.Text.Json.JsonSerializer.Serialize(rows.Select(row => new
        {
            text = row.Text,
            score = row.Score,
        }));
        return new MaaTaskRunResult(entry, "Succeeded", true, "detail", $"{{\"filtered\":{filtered}}}", "OCR", true);
    }

    static MaaTaskRunResult MRS(
        string entry,
        IReadOnlyList<(string Text, double Score, int X, int Y, int Width, int Height)> rows)
    {
        var filtered = JsonSerializer.Serialize(rows.Select(row => new
        {
            text = row.Text,
            score = row.Score,
            box = new[] { row.X, row.Y, row.Width, row.Height },
        }));
        return new MaaTaskRunResult(entry, "Succeeded", true, "detail", $"{{\"filtered\":{filtered}}}", "OCR", true);
    }
}

static void LocalCandidateConverterPrefersModifiedPhantomRelic()
{
    var relics = RhodesRunCatalog.LoadDefault().Relics
        .Where(item => item.Id is "is2_phantom_relic_181" or "is2_phantom_relic_182")
        .ToArray();
    Equal(2, relics.Length, "Phantom base and modified relic fixtures");

    var converterType = typeof(RhodesMaaLocalCandidateConverter);
    var normalize = converterType.GetMethod("NormalizeRelicName", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("NormalizeRelicName was not found.");
    var resolve = converterType.GetMethod("ResolveRelic", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("ResolveRelic was not found.");
    var byNormalizedName = relics.ToDictionary(
        item => (string)(normalize.Invoke(null, [item.Name]) ?? ""),
        item => item,
        StringComparer.Ordinal);
    var normalizedOcr = (string)(normalize.Invoke(null, ["リターニアの王第（改)"]) ?? "");
    var resolved = resolve.Invoke(null, [normalizedOcr, byNormalizedName]) as SukiChoiceItem;

    Equal("is2_phantom_relic_182", resolved?.Id, "modified suffix removes base relic ambiguity");
}

static void LocalCandidateConverterThoughts()
{
    var candidates = RhodesMaaLocalCandidateConverter.FromTaskResults(
        "is5ThoughtFull",
        [
            M(
                "RhodesOcrRegion_is5_thought_list_text",
                [
                    ("枯れ木と若枝", 0.91),
                    ("枯れ木と若枝", 0.88),
                    ("走る都市", 0.86),
                ]),
        ]);

    Equal(
        "is5_sarkaz_selectable_thought_legacy_08|is5_sarkaz_selectable_thought_legacy_08|is5_sarkaz_selectable_thought_insp_20",
        string.Join("|", candidates.Select(item => item.ThoughtId)),
        "thought ids");
    Equal("thought|thought|thought", string.Join("|", candidates.Select(item => item.Kind)), "thought kinds");
    Equal("is5_sarkaz", candidates[0].CampaignId, "thought campaign id");
    Equal("maa-local:thought:is5_sarkaz_selectable_thought_legacy_08:0", candidates[0].RecognitionKey, "thought recognition key");

    var overlappingFrames = RhodesMaaLocalCandidateConverter.FromTaskResults(
        "is5ThoughtFull",
        [
            M("RhodesOcrRegion_is5_thought_list_text", [("純白の花びら", 0.99), ("文字なき約定", 0.98)]),
            M("RhodesOcrRegion_is5_thought_list_text", [("純白の花びら", 0.97), ("走る都市", 0.96)]),
        ]);
    Equal(
        "純白の花びら|文字なき約定|走る都市",
        string.Join("|", overlappingFrames.Select(item => item.Label)),
        "overlapping thought frames do not double-count the same visible card");

    var trackedRepeatedRows = RhodesMaaLocalCandidateConverter.FromTaskResults(
        "is5ThoughtFull",
        [
            MB(
                "RhodesOcrRegion_is5_thought_list_text",
                [
                    ("築壁", 0.99, 478, 120),
                    ("巫術", 0.99, 910, 120),
                    ("走る都市", 0.99, 478, 240),
                    ("侵略", 0.99, 910, 240),
                    ("枯れ木と若枝", 0.99, 478, 520),
                    ("枯れ木と若枝", 0.99, 910, 520),
                ]),
            MB("RhodesOcrRegion_is5_thought_list_text", ThoughtRows(420, 540)),
            MB("RhodesOcrRegion_is5_thought_list_text", ThoughtRows(320, 440, 560)),
            MB("RhodesOcrRegion_is5_thought_list_text", ThoughtRows(220, 340, 460, 580)),
            MB("RhodesOcrRegion_is5_thought_list_text", ThoughtRows(120, 240, 360, 480, 600)),
            MB("RhodesOcrRegion_is5_thought_list_text", ThoughtRows(140, 260, 380, 500, 620)),
            MB("RhodesOcrRegion_is5_thought_list_text", ThoughtRows(140, 260, 380, 500, 620)),
        ]);
    Equal(16, trackedRepeatedRows.Count, "small-step thought tracking counts the full list across identical scrolled rows");
    Equal(
        12,
        trackedRepeatedRows.Count(item => item.Label == "枯れ木と若枝"),
        "small-step thought tracking restores repeated cards beyond one viewport");

    var legacyLargeStepRows = RhodesMaaLocalCandidateConverter.FromTaskResults(
        "is5ThoughtFull",
        [
            MB(
                "RhodesOcrRegion_is5_thought_list_text",
                [
                    ("築壁", 0.99, 478, 174),
                    ("巫術", 0.99, 910, 179),
                    ("走る都市", 0.99, 478, 299),
                    ("侵略", 0.99, 910, 298),
                    ("枯れ木と若枝", 0.99, 478, 539),
                    ("枯れ木と若枝", 0.99, 910, 537),
                ]),
            MB("RhodesOcrRegion_is5_thought_list_text", ThoughtRows(136, 256, 375, 496)),
            MB("RhodesOcrRegion_is5_thought_list_text", ThoughtRows(110, 230, 352, 470)),
            MB("RhodesOcrRegion_is5_thought_list_text", ThoughtRows(118, 238, 358, 479)),
            MB("RhodesOcrRegion_is5_thought_list_text", ThoughtRows(119, 240, 361, 480)),
        ]);
    Equal(
        8,
        legacyLargeStepRows.Count(item => item.Label == "枯れ木と若枝"),
        "legacy large-step evidence remains capped at the visible maximum instead of overcounting");

    var displayedLoadReconciled = RhodesMaaLocalCandidateConverter.FromTaskResults(
        "is5ThoughtFull",
        [
            M("RhodesOcrRegion_is5_thought_load_current", [("49", 0.99)]),
            M(
                "RhodesOcrRegion_is5_thought_list_text",
                [
                    ("築壁", 0.99),
                    ("巫術", 0.99),
                    ("走る都市", 0.99),
                    ("侵略", 0.99),
                    ("枯れ木と若枝", 0.99),
                    ("枯れ木と若枝", 0.99),
                ]),
            M("RhodesOcrRegion_is5_thought_list_text", Enumerable.Repeat(("枯れ木と若枝", 0.99), 8).ToArray()),
            M("thought.card.load.is5_sarkaz_selectable_thought_insp_01", [("4", 0.99)]),
            M("thought.card.load.is5_sarkaz_selectable_thought_insp_08", [("3", 0.99)]),
            M("thought.card.load.is5_sarkaz_selectable_thought_insp_20", [("3", 0.99)]),
            M("thought.card.load.is5_sarkaz_selectable_thought_insp_13", [("3", 0.99)]),
            M("thought.card.load.is5_sarkaz_selectable_thought_legacy_08", [("3", 0.99)]),
        ]);
    Equal(
        12,
        displayedLoadReconciled.Count(item => item.Label == "枯れ木と若枝"),
        "displayed thought loads reconcile hidden duplicates after relic and age modifiers");

    static IReadOnlyList<(string Text, double Score, int X, int Y)> ThoughtRows(params int[] rows) =>
        rows.SelectMany(y => new[]
        {
            ("枯れ木と若枝", 0.99, 478, y),
            ("枯れ木と若枝", 0.99, 910, y),
        }).ToArray();

    static MaaTaskRunResult M(string entry, IReadOnlyList<(string Text, double Score)> rows)
    {
        var resultRows = rows.Select(row =>
            $"{{\"text\":{System.Text.Json.JsonSerializer.Serialize(row.Text)},\"score\":{row.Score.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}");
        return new MaaTaskRunResult(
            entry,
            "Succeeded",
            true,
            "detail",
            $"{{\"filtered_results\":[{string.Join(",", resultRows)}]}}",
            "OCR",
            true);
    }

    static MaaTaskRunResult MB(string entry, IReadOnlyList<(string Text, double Score, int X, int Y)> rows)
    {
        var resultRows = rows.Select(row =>
            $"{{\"text\":{System.Text.Json.JsonSerializer.Serialize(row.Text)},\"score\":{row.Score.ToString(System.Globalization.CultureInfo.InvariantCulture)},\"box\":[{row.X},{row.Y},120,20]}}");
        return new MaaTaskRunResult(
            entry,
            "Succeeded",
            true,
            "detail",
            $"{{\"filtered_results\":[{string.Join(",", resultRows)}]}}",
            "OCR",
            true);
    }
}

static void LocalCandidateConverterAge()
{
    var candidates = RhodesMaaLocalCandidateConverter.FromTaskResults(
        "is5AgeFull",
        [
            M("RhodesRunStatusTopBarOcr", "魔王の時代（形成期）", 0.99),
            M("RhodesOcrRegion_is5_age_detail_text", "天 災 の 時 代（全 盛 期）\n最大HP+200%", 0.91),
        ]);

    Equal(1, candidates.Count, "age candidate count");
    Equal("age", candidates[0].Kind, "age kind");
    Equal("is5_sarkaz_selectable_age_is5_age_01_formation", candidates[0].AgeId, "age group representative id");
    Equal("is5_sarkaz", candidates[0].CampaignId, "age campaign id");
    Equal("maa-local:age:is5_sarkaz_selectable_age_is5_age_01_formation", candidates[0].RecognitionKey, "age group recognition key");

    var liveAge = RhodesMaaLocalCandidateConverter.FromTaskResults(
        "is5AgeFull",
        [M("RhodesOcrRegion_is5_age_detail_text", "苦難の時代 5歩後に終了\nすべての味方ユニットが配置時に残りHPの70%を失う", 0.99)]);
    Equal("is5_sarkaz_selectable_age_is5_age_03_formation", liveAge.Single().AgeId, "live age OCR resolves the age group only");

    static MaaTaskRunResult M(string entry, string text, double score)
    {
        var encodedText = System.Text.Json.JsonSerializer.Serialize(text);
        return new MaaTaskRunResult(
            entry,
            "Succeeded",
            true,
            "detail",
            $"{{\"filtered_results\":[{{\"text\":{encodedText},\"score\":{score.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}]}}",
            "OCR",
            true);
    }
}

static void LocalCandidateConverterPhantomHallucinations()
{
    var candidates = RhodesMaaLocalCandidateConverter.FromTaskResults(
        "is2HallucinationsFull",
        [
            M(
                "RhodesOcrRegion_is2_hallucination_top",
                [
                    ("偏 執 的 な", 0.97),
                    ("敏感な", 0.93),
                    ("偏執的な", 0.91),
                ]),
        ]);

    Equal(1, candidates.Count, "hallucination candidate count");
    Equal("runStatus", candidates[0].Kind, "hallucination candidate kind");
    Equal("hallucinations", candidates[0].Field, "hallucination field");
    Equal("is2_phantom", candidates[0].CampaignId, "hallucination campaign");
    Equal("偏執 / 敏感", candidates[0].Value, "hallucinations normalize to Wiki names and deduplicate");
    Equal(0.97, candidates[0].Confidence, "hallucination confidence");
    Equal("maa-local:hallucinations:偏執|敏感", candidates[0].RecognitionKey, "hallucination recognition key");

    static MaaTaskRunResult M(string entry, IReadOnlyList<(string Text, double Score)> rows)
    {
        var resultRows = rows.Select(row =>
            $"{{\"text\":{System.Text.Json.JsonSerializer.Serialize(row.Text)},\"score\":{row.Score.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}");
        return new MaaTaskRunResult(
            entry,
            "Succeeded",
            true,
            "detail",
            $"{{\"filtered_results\":[{string.Join(",", resultRows)}]}}",
            "OCR",
            true);
    }
}

static void LocalCandidateConverterPhantomPerformance()
{
    var candidates = RhodesMaaLocalCandidateConverter.FromTaskResults(
        "is2PerformanceFull",
        [
            M(
                "RhodesOcrRegion_is2_performance_bottom",
                [
                    ("現在の演目", 0.99),
                    ("利染砂の「^、リアの輝き「", 0.83),
                ]),
        ]);

    Equal(1, candidates.Count, "performance candidate count");
    Equal("runStatus", candidates[0].Kind, "performance candidate kind");
    Equal("performanceId", candidates[0].Field, "performance field");
    Equal("is2_phantom", candidates[0].CampaignId, "performance campaign");
    Equal("is2_phantom_performance_pcsp20", candidates[0].Value, "performance id");
    Equal(0.83, candidates[0].Confidence, "performance confidence");
    Equal("maa-local:performance:is2_phantom_performance_pcsp20", candidates[0].RecognitionKey, "performance recognition key");

    var normal = RhodesMaaLocalCandidateConverter.FromTaskResults(
        "is2PerformanceFull",
        [M("RhodesOcrRegion_is2_performance_bottom", [("現在の演目", 0.99), ("『ヘリアの輝き』", 0.95)])]);
    Equal("is2_phantom_performance_pcsp7", normal.Single().Value, "normal performance remains distinct from crimson performance");

    var none = RhodesMaaLocalCandidateConverter.FromTaskResults(
        "is2PerformanceFull",
        [M("RhodesOcrRegion_is2_performance_bottom", [("現在の演目", 0.99)])]);
    Equal(0, none.Count, "performance header alone produces no candidate");

    static MaaTaskRunResult M(string entry, IReadOnlyList<(string Text, double Score)> rows)
    {
        var resultRows = rows.Select(row =>
            $"{{\"text\":{System.Text.Json.JsonSerializer.Serialize(row.Text)},\"score\":{row.Score.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}");
        return new MaaTaskRunResult(
            entry,
            "Succeeded",
            true,
            "detail",
            $"{{\"filtered_results\":[{string.Join(",", resultRows)}]}}",
            "OCR",
            true);
    }
}

static void LocalCandidateConverterMizukiSpecials()
{
    Equal(
        "予備隊具ー近距離",
        RhodesOperatorOcrNormalizer.Normalize("予備隊具ー近距離"),
        "observed Mizuki reserve operator OCR normalization");
    Equal(
        "予備隊員ー近距離",
        RhodesOperatorOcrNormalizer.Normalize("予備隊員-近距離"),
        "reserve operator catalog name normalization");
    var key = RhodesMaaLocalCandidateConverter.FromTaskResults(
        "is3KeyFull",
        [
            M("RhodesOcrRegion_is3_ingot_value", [("24", 0.97)]),
            M("RhodesOcrRegion_is3_key_value", [("2", 0.96)]),
        ]);
    Equal(2, key.Count, "Mizuki top-right candidate count");
    Equal("runStatus", key[0].Kind, "Mizuki ingot candidate kind");
    Equal("ingot", key[0].Field, "Mizuki ingot field");
    Equal("24", key[0].Value, "Mizuki ingot value");
    Equal("mizuki", key[1].Kind, "Mizuki key candidate kind");
    Equal("key", key[1].FieldId, "Mizuki key field");
    Equal("2", key[1].Value, "Mizuki key value");

    var measuredIngotDrift = RhodesMaaLocalCandidateConverter.FromTaskResults(
        "is3KeyFull",
        [M("RhodesOcrRegion_is3_ingot_value", [("2z", 0.781726)])]);
    Equal("27", measuredIngotDrift.Single().Value, "measured Mizuki ingot trailing z is repaired to seven");

    var panel = RhodesMaaLocalCandidateConverter.FromTaskResults(
        "is3LightHordeFull",
        [
            M("RhodesOcrRegion_is3_light_value", [("15", 0.95)]),
            M(
                "RhodesOcrRegion_is3_horde_call_list_text",
                [
                    ("呼び声栄枯", 0.93),
                    ("呼び声：給養", 0.91),
                    ("吟び声：適応", 0.90),
                    ("呼び声：栄枯", 0.89),
                ]),
        ]);
    Equal(4, panel.Count, "Mizuki light and unique Horde's Call candidates");
    Equal("15", panel.Single(candidate => candidate.FieldId == "light").Value, "Mizuki light value");
    Equal(
        "is3_mizuki_selectable_hordeCall_mcasci15|is3_mizuki_selectable_hordeCall_mcasci16|is3_mizuki_selectable_hordeCall_mcasci19",
        string.Join('|', panel.Where(candidate => candidate.FieldId == "hordeCalls").Select(candidate => candidate.EffectId)),
        "Mizuki Horde's Call ids");

    var measuredLightDrift = RhodesMaaLocalCandidateConverter.FromTaskResults(
        "is3LightHordeFull",
        [M("RhodesOcrRegion_is3_light_value", [("E", 0.813498)])]);
    Equal("8", measuredLightDrift.Single().Value, "observed Mizuki light E drift is repaired to 8");

    var rejection = RhodesMaaLocalCandidateConverter.FromTaskResults(
        "is3RejectionFull",
        [
            M(
                "RhodesOcrRegion_is3_rejection_name",
                [
                    ("発動済の拒絶反応", 0.96),
                    ("散漫と異変", 0.94),
                    ("拒絶反応を起こしたオペレーターは全て", 0.91),
                ]),
            MSpatial(
                "RhodesOcrRegion_is3_rejection_operators",
                [
                    ("距離", 0.90, 193),
                    ("影響するオペレー夕ー：クルースォフェン、予備隊具ー近", 0.92, 94),
                ]),
        ]);
    Equal(
        "is3_mizuki_selectable_rejectionReaction_mcasci24",
        rejection.Single(candidate => !string.IsNullOrWhiteSpace(candidate.EffectId)).EffectId,
        "Mizuki rejection id");
    Equal(0, rejection.Count(candidate => !string.IsNullOrWhiteSpace(candidate.OperatorId)), "rejection prose does not infer affected operators");
    Equal(1, rejection.Count, "Mizuki rejection detail only creates the reaction candidate");

    static MaaTaskRunResult M(string entry, IReadOnlyList<(string Text, double Score)> rows)
    {
        var resultRows = rows.Select(row =>
            $"{{\"text\":{System.Text.Json.JsonSerializer.Serialize(row.Text)},\"score\":{row.Score.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}");
        return new MaaTaskRunResult(
            entry,
            "Succeeded",
            true,
            "detail",
            $"{{\"filtered_results\":[{string.Join(",", resultRows)}]}}",
            "OCR",
            true);
    }

    static MaaTaskRunResult MSpatial(string entry, IReadOnlyList<(string Text, double Score, int Y)> rows)
    {
        var resultRows = rows.Select(row =>
            $"{{\"text\":{System.Text.Json.JsonSerializer.Serialize(row.Text)},\"score\":{row.Score.ToString(System.Globalization.CultureInfo.InvariantCulture)},\"box\":[0,{row.Y},100,24]}}");
        return new MaaTaskRunResult(
            entry,
            "Succeeded",
            true,
            "detail",
            $"{{\"filtered_results\":[{string.Join(",", resultRows)}]}}",
            "OCR",
            true);
    }
}

static void LocalCandidateConverterPrefersMizukiRejectionTemplate()
{
    var childResults = new JsonArray();
    for (var index = 0; index < 10; index++)
    {
        childResults.Add(index == 9
            ? JsonNode.Parse("""
              {
                "algorithm": "TemplateMatch",
                "box": [711, 374, 79, 60],
                "detail": {
                  "best": { "box": [711, 374, 79, 60], "score": 0.934 }
                }
              }
              """)
            : JsonNode.Parse("""{ "algorithm": "TemplateMatch" }"""));
    }

    var candidates = RhodesMaaLocalCandidateConverter.FromTaskResults(
        "is3RejectionFull",
        [
            new MaaTaskRunResult(
                "RhodesTemplate_is3RejectionFull_is3_rejection_icon_batch",
                "Succeeded",
                true,
                "detail",
                childResults.ToJsonString(),
                "Or",
                true),
            M("RhodesOcrRegion_is3_rejection_name", "注意散漫", 0.99),
            M("RhodesOcrRegion_is3_rejection_operators", "影響するオペレーター：クルース、フェン", 0.98),
        ]);

    var effect = candidates.Single(candidate => !string.IsNullOrWhiteSpace(candidate.EffectId));
    Equal("is3_mizuki_selectable_rejectionReaction_mcasci9", effect.EffectId, "rejection OCR remains authoritative when a template conflicts");
    Equal("注意散漫", effect.Label, "rejection template cannot override a high-confidence name");
    Equal(true, effect.Confidence is > 0.98 and < 1.00, "rejection OCR keeps native score");
    Equal(0, candidates.Count(candidate => !string.IsNullOrWhiteSpace(candidate.OperatorId)), "detail prose never creates rejection targets");

    static MaaTaskRunResult M(string entry, string text, double score)
    {
        var encodedText = System.Text.Json.JsonSerializer.Serialize(text);
        return new MaaTaskRunResult(
            entry,
            "Succeeded",
            true,
            "detail",
            $"{{\"filtered_results\":[{{\"text\":{encodedText},\"score\":{score.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}]}}",
            "OCR",
            true);
    }
}

static void LocalCandidateConverterRevelation()
{
    var candidates = RhodesMaaLocalCandidateConverter.FromTaskResults(
        "is4RevelationFull",
        [
            M(
                "RhodesOcrRegion_is4_revelation_list_text",
                [
                    ("歌唱", 0.91),
                    ("追放者", 0.88),
                    ("存続", 0.86),
                ]),
        ]);

    Equal(
        "is4_sami_selectable_revelationBoard_is4_kvama1|is4_sami_selectable_revelationBoard_is4_aestar1|is4_sami_selectable_revelationBoard_is4_rhetoric1",
        string.Join("|", candidates.Select(item => item.EffectId)),
        "revelation effect ids");
    Equal("cause|structure|rhetoric", string.Join("|", candidates.Select(item => item.SlotKind)), "revelation slot kinds");
    Equal("revelation|revelation|revelation", string.Join("|", candidates.Select(item => item.Kind)), "revelation kinds");
    Equal("is4_sami", candidates[0].CampaignId, "revelation campaign id");
    Equal("revelation", candidates[0].FieldId, "revelation field id");
    Equal("maa-local:revelation:is4_sami_selectable_revelationBoard_is4_kvama1:0", candidates[0].RecognitionKey, "revelation recognition key");

    static MaaTaskRunResult M(string entry, IReadOnlyList<(string Text, double Score)> rows)
    {
        var resultRows = rows.Select(row =>
            $"{{\"text\":{System.Text.Json.JsonSerializer.Serialize(row.Text)},\"score\":{row.Score.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}");
        return new MaaTaskRunResult(
            entry,
            "Succeeded",
            true,
            "detail",
            $"{{\"filtered_results\":[{string.Join(",", resultRows)}]}}",
            "OCR",
            true);
    }
}

static void LocalCandidateConverterCoins()
{
    var candidates = RhodesMaaLocalCandidateConverter.FromTaskResults(
        "is6CoinsFull",
        [
            M(
                "RhodesOcrRegion_is6_coin_list_text",
                [
                    ("大炎通宝", 0.91),
                    ("苦寒", 0.88),
                    (",衡-早熱", 0.93),
                    ("町=弱氷", 0.98),
                ]),
        ]);

    Equal(
        "is6_sui_selectable_coin_is6_copper_b01|is6_sui_selectable_coin_is6_copper_f01|is6_sui_selectable_coin_is6_copper_f05|is6_sui_selectable_coin_is6_copper_f07",
        string.Join("|", candidates.Select(item => item.CoinId)),
        "coin ids");
    Equal("coin|coin|coin|coin", string.Join("|", candidates.Select(item => item.Kind)), "coin kinds");
    Equal("is6_sui", candidates[0].CampaignId, "coin campaign id");
    Equal("coins", candidates[0].FieldId, "coin field id");
    Equal(1, candidates[0].Count, "coin count");
    Equal(
        1,
        candidates.Single(item => item.CoinId.EndsWith("is6_copper_f05", StringComparison.Ordinal)).Count,
        "one OCR row cannot duplicate a short coin alias");
    Equal("", candidates[0].Face, "coin face is unused");
    Equal("maa-local:coin:is6_sui_selectable_coin_is6_copper_b01:0", candidates[0].RecognitionKey, "coin recognition key");

    static MaaTaskRunResult M(string entry, IReadOnlyList<(string Text, double Score)> rows)
    {
        var resultRows = rows.Select(row =>
            $"{{\"text\":{System.Text.Json.JsonSerializer.Serialize(row.Text)},\"score\":{row.Score.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}");
        return new MaaTaskRunResult(
            entry,
            "Succeeded",
            true,
            "detail",
            $"{{\"filtered_results\":[{string.Join(",", resultRows)}]}}",
            "OCR",
            true);
    }
}

static void LocalCandidateConverterSuiBaseValues()
{
    var candidates = RhodesMaaLocalCandidateConverter.FromTaskResults(
        "is6BaseFull",
        [
            M("RhodesOcrRegion_is6_ingot_value", "18", 0.94),
            M("RhodesOcrRegion_is6_ticket_value", "3", 0.92),
        ],
        "is6_sui");

    Equal("ingot|ticket", string.Join("|", candidates.Select(item => item.Field)), "Sui base fields");
    Equal("18|3", string.Join("|", candidates.Select(item => item.Value)), "Sui base values");

    var state = JsonNode.Parse("""{ "run": { "campaignId": "is6_sui" } }""")!.AsObject();
    var summary = RhodesRecognitionCandidateApplier.Apply(
        state,
        candidates,
        DateTimeOffset.Parse("2026-07-21T00:00:00Z"));
    Equal(2, summary.AppliedCount, "Sui base apply count");
    Equal(18, state["run"]!["ingot"]!.GetValue<int>(), "Sui ingot persisted");
    Equal(3, state["run"]!["special"]!["is6_sui"]!["ticket"]!.GetValue<int>(), "Sui ticket persisted");

    static MaaTaskRunResult M(string entry, string text, double score)
    {
        var encodedText = JsonSerializer.Serialize(text);
        return new MaaTaskRunResult(
            entry,
            "Succeeded",
            true,
            "detail",
            $"{{\"filtered_results\":[{{\"text\":{encodedText},\"score\":{score.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}]}}",
            "OCR",
            true);
    }
}

static void LocalCandidateConverterSuiSeasonalHours()
{
    var normal = RhodesMaaLocalCandidateConverter.FromTaskResults(
        "is6SeasonalHours",
        [
            M(
                "RhodesOcrRegion_is6_seasonal_hour_detail_text",
                [
                    ("巳農", 0.97, 20),
                    ("LV.3 入骨", 0.95, 54),
                    ("オペレーターの配置コスト+4", 0.94, 86),
                ]),
        ],
        "is6_sui");

    Equal(1, normal.Count, "normal seasonal hour candidate count");
    Equal("sui", normal[0].Kind, "seasonal hour candidate kind");
    Equal("seasonalHours", normal[0].FieldId, "seasonal hour field");
    Equal(
        "is6_sui_selectable_seasonalHours_is6sst6_nyuukotsu",
        normal[0].EffectId,
        "displayed rank resolves the normal seasonal hour variant");

    var dogPainting = RhodesMaaLocalCandidateConverter.FromTaskResults(
        "is6SeasonalHours",
        [
            M(
                "RhodesOcrRegion_is6_seasonal_hour_detail_text",
                [
                    ("戌絵", 0.98, 20),
                    ("LV.2 明瞭", 0.96, 54),
                    ("先鋒と医療の初回配置時、即座に便符が貼り付く", 0.93, 86),
                ]),
        ],
        "is6_sui");

    Equal(3, dogPainting.Count, "Dog Painting seasonal hour and target professions are retained");
    Equal(
        "is6_sui_selectable_seasonalHours_is6sst11_meiryou",
        dogPainting.Single(candidate => candidate.FieldId == "seasonalHours").EffectId,
        "Dog Painting displayed rank resolves the seasonal hour variant");
    Equal(
        "先鋒|医療",
        string.Join("|", dogPainting
            .Where(candidate => candidate.FieldId == "seasonalHourTargets")
            .Select(candidate => candidate.EffectId)),
        "Dog Painting OCR extracts the affected professions");

    var awakened = RhodesMaaLocalCandidateConverter.FromTaskResults(
        "is6SeasonalHours",
        [
            M(
                "RhodesOcrRegion_is6_seasonal_hour_detail_text",
                [
                    ("巳農", 0.97, 20),
                    ("醒覚", 0.96, 54),
                    ("最初に配置するオペレーターの配置コスト-6", 0.94, 86),
                    ("午商", 0.96, 150),
                    ("醒覚", 0.95, 184),
                    ("ランダムな商品1つの販売価格が100%低下", 0.92, 216),
                ]),
        ],
        "is6_sui");

    Equal(2, awakened.Count, "multiple awakened seasonal hours are retained");
    Equal(
        "is6_sui_selectable_seasonalHours_is6sst6_awakening|is6_sui_selectable_seasonalHours_is6sst7_awakening",
        string.Join("|", awakened.Select(candidate => candidate.EffectId)),
        "displayed awakening overrides normal rank inference");

    var effectOnly = RhodesMaaLocalCandidateConverter.FromTaskResults(
        "is6SeasonalHours",
        [
            M(
                "RhodesOcrRegion_is6_seasonal_hour_detail_text",
                [
                    ("醒覚", 0.96, 54),
                    ("最初に配置するオペレーターの配置コスト-6", 0.94, 86),
                ]),
        ],
        "is6_sui");

    Equal(1, effectOnly.Count, "effect-only seasonal hour candidate count");
    Equal(
        "is6_sui_selectable_seasonalHours_is6sst6_awakening",
        effectOnly[0].EffectId,
        "unique effect text identifies the seasonal hour before the shared awakening label");

    static MaaTaskRunResult M(string entry, IReadOnlyList<(string Text, double Score, int Y)> rows)
    {
        var resultRows = rows.Select(row =>
            $"{{\"text\":{JsonSerializer.Serialize(row.Text)},\"score\":{row.Score.ToString(System.Globalization.CultureInfo.InvariantCulture)},\"box\":[0,{row.Y},420,28]}}");
        return new MaaTaskRunResult(
            entry,
            "Succeeded",
            true,
            "detail",
            $"{{\"filtered_results\":[{string.Join(",", resultRows)}]}}",
            "OCR",
            true);
    }
}

static void LocalCandidateConverterRepairsSuiTicketSix()
{
    var candidates = RhodesMaaLocalCandidateConverter.FromTaskResults(
        "is6BaseFull",
        [M("RhodesOcrRegion_is6_ticket_value", "E", 0.91)],
        "is6_sui");

    var candidate = candidates.Single();
    Equal("ticket", candidate.Field, "Sui ticket field");
    Equal("6", candidate.Value, "stylized ticket six repaired");
    Equal("E", candidate.RawText, "raw OCR retained for diagnostics");

    static MaaTaskRunResult M(string entry, string text, double score)
    {
        var encodedText = JsonSerializer.Serialize(text);
        return new MaaTaskRunResult(
            entry,
            "Succeeded",
            true,
            "detail",
            $"{{\"filtered_results\":[{{\"text\":{encodedText},\"score\":{score.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}]}}",
            "OCR",
            true);
    }
}

static void SuiActiveCoinImageRecognizer()
{
    var options = RhodesRunCatalog.LoadSpecialEffectOptions("is6_sui", "coin");
    var first = options.Single(option => option.Id.EndsWith("is6_copper_b08", StringComparison.Ordinal));
    var second = options.Single(option => option.Id.EndsWith("is6_copper_b09", StringComparison.Ordinal));
    using var frame = new SKBitmap(1280, 720, SKColorType.Bgra8888, SKAlphaType.Premul);
    frame.Erase(new SKColor(31, 42, 41));
    using (var canvas = new SKCanvas(frame))
    {
        Draw(canvas, first.ImagePath, new SKRect(532, 207, 632, 307));
        Draw(canvas, first.ImagePath, new SKRect(532, 327, 632, 427));
        Draw(canvas, second.ImagePath, new SKRect(532, 467, 632, 567));
    }

    var result = RhodesSuiCoinImageRecognizer.Recognize(EncodePng(frame), options);
    Equal(true, result.Hit, "active coin image result hit");
    Equal(true, RhodesSuiCoinImageRecognizer.TryRead(result, out var fieldId, out var detections), "active coin image result readable");
    Equal("activeCoins", fieldId, "active coin field id");
    Equal(3, detections.Count, $"active coin slot count ({result.RecognitionDetailJson})");
    Equal(
        $"{first.Id}|{first.Id}|{second.Id}",
        string.Join("|", detections.OrderBy(item => item.SlotIndex).Select(item => item.CoinId)),
        "active coin slot ids");

    var candidates = RhodesMaaLocalCandidateConverter.FromTaskResults(
        "is6ActiveCoinsFull",
        [result],
        "is6_sui");
    Equal(2, candidates.Count, "active coin kind count");
    Equal(2, candidates.Single(candidate => candidate.CoinId == first.Id).Count, "duplicate active coin count");
    Equal(1, candidates.Single(candidate => candidate.CoinId == second.Id).Count, "single active coin count");
    Equal(true, candidates.All(candidate => candidate.FieldId == "activeCoins"), "active coin candidate field");
    Equal(true, candidates.All(candidate => string.IsNullOrWhiteSpace(candidate.Face)), "active coin face is unused");

    var rowRequests = RhodesSuiCoinImageRecognizer.PlanActivePanelOcrRequests(
        RhodesSuiCoinImageRecognizer.InspectActive(EncodePng(frame), options));
    Equal(3, rowRequests.Count, "visible active coin rows schedule OCR fallback");
    Equal("RhodesDynamic_is6.active_coin_list_text.slot0", rowRequests[0].Entry, "active coin row OCR entry");
    Equal(620, rowRequests[0].X, "active coin row OCR starts before variable-length heading");
    Equal(470, rowRequests[0].Width, "active coin row OCR includes direction prose");
    Equal(false, rowRequests[0].OnlyRecognition, "active coin row OCR detects text inside the row");

    static void Draw(SKCanvas canvas, string path, SKRect destination)
    {
        using var bitmap = SKBitmap.Decode(path);
        canvas.DrawBitmap(bitmap, destination);
    }
}

static void SuiActiveCoinOcrCandidateCounts()
{
    var options = RhodesRunCatalog.LoadSpecialEffectOptions("is6_sui", "coin");
    var statuses = RhodesRunCatalog.LoadSpecialEffectOptions("is6_sui", "coinStatus");
    var duplicate = options.Single(option => option.Id.EndsWith("is6_copper_b08", StringComparison.Ordinal));
    var single = options.Single(option => option.Id.EndsWith("is6_copper_b09", StringComparison.Ordinal));
    var catchWindLeft = options.Single(option => option.Id.EndsWith("is6_copper_e19", StringComparison.Ordinal));
    var catchWindUp = options.Single(option => option.Id.EndsWith("is6_copper_e21", StringComparison.Ordinal));
    var greatYanCoin = options.Single(option => option.Name == "大炎通宝");
    var moveMountain = options.Single(option => option.Name == "山を移すこと難し");
    var guarded = statuses.Single(option => option.Name == "存護");
    var rusted = statuses.Single(option => option.Name == "錆色");
    var ocrResult = new MaaTaskRunResult(
        "RhodesOcrRegion_is6_active_coin_list_text",
        "Succeeded",
        true,
        "activeCoins=3",
        JsonSerializer.Serialize(new
        {
            filtered_results = new[]
            {
                new { text = duplicate.Name, score = 0.96, box = new[] { 650, 230, 230, 32 } },
                new { text = duplicate.Name, score = 0.95, box = new[] { 650, 350, 230, 32 } },
                new { text = single.Name, score = 0.97, box = new[] { 650, 470, 230, 32 } },
                new { text = "存護：銭匣に収まる際、シールド値+2", score = 0.95, box = new[] { 650, 520, 300, 32 } },
                new { text = "炎通宝に変化する", score = 0.92, box = new[] { 650, 280, 300, 32 } },
                new { text = "にこ「大炎通宝」に変化する", score = 0.89, box = new[] { 650, 640, 300, 32 } },
            },
        }),
        "OCR",
        true);

    var candidates = RhodesMaaLocalCandidateConverter.FromTaskResults(
        "is6ActiveCoinsFull",
        [ocrResult],
        "is6_sui");

    Equal(2, candidates.Count, "active coin OCR candidate kinds");
    Equal(2, candidates.Single(candidate => candidate.CoinId == duplicate.Id).Count, "duplicate active coin OCR count");
    Equal(1, candidates.Single(candidate => candidate.CoinId == single.Id).Count, "single active coin OCR count");
    Equal(guarded.Id, candidates.Single(candidate => candidate.CoinId == single.Id).StatusId, "active coin OCR status");
    Equal(false, candidates.Any(candidate => candidate.Label == "大炎通宝"), "coin names mentioned only in descriptions are ignored");
    Equal(true, candidates.All(candidate => candidate.FieldId == "activeCoins"), "active coin OCR field");

    var directionalResult = new MaaTaskRunResult(
        "RhodesOcrRegion_is6_active_coin_list_text",
        "Succeeded",
        true,
        "activeCoins=2",
        JsonSerializer.Serialize(new
        {
            filtered_results = new[]
            {
                new { text = "衡-捕風", score = 0.94, box = new[] { 650, 230, 230, 32 } },
                new { text = "振り出されると、オペレーターの配置方向が左向きの場合", score = 0.91, box = new[] { 650, 280, 620, 32 } },
                new { text = "攻撃速度+40", score = 0.93, box = new[] { 650, 320, 240, 32 } },
                new { text = single.Name, score = 0.97, box = new[] { 650, 470, 230, 32 } },
            },
        }),
        "OCR",
        true);
    var directionalCandidates = RhodesMaaLocalCandidateConverter.FromTaskResults(
        "is6ActiveCoinsFull",
        [directionalResult],
        "is6_sui");

    Equal(2, directionalCandidates.Count, "directional active coin OCR candidate count");
    Equal(
        catchWindLeft.Id,
        directionalCandidates.Single(candidate => candidate.Label.StartsWith("捕風", StringComparison.Ordinal)).CoinId,
        "direction omitted from Catch Wind heading is recovered from its description");

    var degradedDirectionalResult = new MaaTaskRunResult(
        "RhodesOcrRegion_is6_active_coin_list_text",
        "Succeeded",
        true,
        "activeCoins=3",
        JsonSerializer.Serialize(new
        {
            filtered_results = new[]
            {
                new { text = "大炎通宝", score = 0.993, box = new[] { 650, 14, 230, 32 } },
                new { text = "衡-捕風", score = 0.91, box = new[] { 650, 370, 230, 32 } },
                new { text = "辰り出きれる上オへレー夕ーの配置", score = 0.88, box = new[] { 650, 472, 330, 32 } },
                new { text = "コきの場合攻撃速度+40", score = 0.90, box = new[] { 650, 536, 300, 32 } },
                new { text = "錆色\"振り出され包こッスボツを通1", score = 0.86, box = new[] { 650, 603, 330, 32 } },
                new { text = "F”原石錐+T", score = 0.82, box = new[] { 650, 669, 250, 32 } },
                new { text = "菓-山を移すこと難し", score = 0.904, box = new[] { 650, 798, 300, 32 } },
            },
        }),
        "OCR",
        true);
    var degradedCandidates = RhodesMaaLocalCandidateConverter.FromTaskResults(
        "is6ActiveCoinsFull",
        [degradedDirectionalResult],
        "is6_sui");

    Equal(3, degradedCandidates.Count, "degraded active coin OCR preserves every card boundary");
    Equal("", degradedCandidates.Single(candidate => candidate.CoinId == greatYanCoin.Id).StatusId, "Catch Wind status does not bleed into the previous coin");
    Equal(rusted.Id, degradedCandidates.Single(candidate => candidate.CoinId == catchWindUp.Id).StatusId, "degraded Catch Wind direction and status are recovered together");
    Equal("", degradedCandidates.Single(candidate => candidate.CoinId == moveMountain.Id).StatusId, "following coin remains unmodified");

    var rowFallbackResults = new[]
    {
        ActiveRow(0, new[]
        {
            new { text = "大炎通宝", score = 0.99, box = new[] { 25, 20, 180, 30 } },
        }),
        ActiveRow(1, new[]
        {
            new { text = "衡-捕風", score = 0.98, box = new[] { 25, 18, 180, 30 } },
            new { text = "振り出されると、オペレーターの配置方向カ“上", score = 0.95, box = new[] { 25, 58, 420, 30 } },
            new { text = "回きの場合攻撃速匿+40", score = 0.93, box = new[] { 25, 82, 280, 28 } },
            new { text = "錆色", score = 0.97, box = new[] { 25, 100, 90, 28 } },
        }),
        ActiveRow(2, new[]
        {
            new { text = "厲-山を移すこと難し", score = 0.98, box = new[] { 25, 20, 300, 30 } },
        }),
    };
    var rowFallbackCandidates = RhodesMaaLocalCandidateConverter.FromTaskResults(
        "is6ActiveCoinsFull",
        [degradedDirectionalResult, .. rowFallbackResults],
        "is6_sui");

    Equal(3, rowFallbackCandidates.Sum(candidate => candidate.Count), "row OCR fallback restores all visible active coins");
    Equal(3, rowFallbackCandidates.Count, "row OCR fallback ignores stale whole-panel candidates");
    Equal(catchWindUp.Id, rowFallbackCandidates.Single(candidate => candidate.Label.StartsWith("捕風", StringComparison.Ordinal)).CoinId, "row OCR fallback resolves Catch Wind direction from its own prose");
    Equal(rusted.Id, rowFallbackCandidates.Single(candidate => candidate.CoinId == catchWindUp.Id).StatusId, "row OCR fallback keeps status in the same row");

    static MaaTaskRunResult ActiveRow<T>(int slot, T[] rows) => new(
        $"RhodesDynamic_is6.active_coin_list_text.slot{slot}",
        "Succeeded",
        true,
        $"slot={slot}",
        JsonSerializer.Serialize(new { filtered_results = rows }),
        "OCR",
        true);
}

static void SuiCatchWindDetailResolverLocatesCard()
{
    var detailRequest = RhodesSuiCatchWindDetailResolver.BuildDetailRequest();
    Equal(1, detailRequest.Scale, "Catch Wind description keeps native scale for fast multi-line OCR");
    using var detailPayload = JsonDocument.Parse(detailRequest.PayloadJson);
    Equal(
        false,
        detailPayload.RootElement.GetProperty("only_rec").GetBoolean(),
        "Catch Wind description enables OCR text detection because the prose spans multiple lines");
    Equal(
        120,
        detailPayload.RootElement.GetProperty("roi")[3].GetInt32(),
        "Catch Wind detail ROI includes the lower status line");

    var wholeListResult = new MaaTaskRunResult(
        "RhodesOcrRegion_is6_coin_list_text",
        "Succeeded",
        true,
        "捕風",
        JsonSerializer.Serialize(new
        {
            filtered_results = new[]
            {
                new { text = "衡-捕風", score = 0.91, box = new[] { 598, 607, 175, 44 } },
            },
        }),
        "OCR",
        true);
    var latestEvidenceResult = new MaaTaskRunResult(
        "RhodesOcrRegion_is6_coin_list_text",
        "Succeeded",
        true,
        "捕風",
        JsonSerializer.Serialize(new
        {
            filtered_results = new[]
            {
                new { text = "衡-捕風", score = 0.977, box = new[] { 634, 611, 139, 38 } },
            },
        }),
        "OCR",
        true);
    var slotResult = new MaaTaskRunResult(
        "RhodesDynamic_is6.coin_list_text.slot5",
        "Succeeded",
        true,
        "捕風",
        JsonSerializer.Serialize(new { best_result = new { text = "衡-捕風", score = 0.95 } }),
        "OCR",
        true);

    Equal(3, RhodesSuiCatchWindDetailResolver.FindVisibleSlot([wholeListResult]), "whole-list OCR box maps Catch Wind to owned slot 3");
    Equal(3, RhodesSuiCatchWindDetailResolver.FindVisibleSlot([latestEvidenceResult]), "latest real Catch Wind OCR box maps to owned slot 3");
    Equal(5, RhodesSuiCatchWindDetailResolver.FindVisibleSlot([slotResult]), "slot OCR entry preserves the owned slot index");
    Equal(null, RhodesSuiCatchWindDetailResolver.FindVisibleSlot([]), "missing Catch Wind is not targeted");
}

static void SuiCatchWindDetailResolverMapsDirection()
{
    var options = RhodesRunCatalog.LoadSpecialEffectOptions("is6_sui", "coin");
    var statuses = RhodesRunCatalog.LoadSpecialEffectOptions("is6_sui", "coinStatus");
    var rusted = statuses.Single(status => status.Name == "錆色");
    var detailResult = new MaaTaskRunResult(
        RhodesSuiCatchWindDetailResolver.DetailEntry,
        "Succeeded",
        true,
        "上向き",
        JsonSerializer.Serialize(new
        {
            filtered_results = new[]
            {
                new { text = "振り出されると、オペレーターの配置方向が上向きの場合、攻撃速度+40", score = 0.94 },
                new { text = "錆色：振り出されると、スポットを通過するたび、源石錐+1", score = 0.96 },
            },
        }),
        "OCR",
        true);

    var detection = RhodesSuiCatchWindDetailResolver.ResolveDetection(detailResult, 3, options, statuses);
    Equal(true, detection is not null, "Catch Wind direction description resolves a detection");
    Equal(true, detection!.CoinId.EndsWith("is6_copper_e21", StringComparison.Ordinal), "upward Catch Wind maps to e21");
    Equal("捕風（上）", detection.Label, "resolved Catch Wind keeps directional label");
    Equal(3, detection.SlotIndex, "resolved Catch Wind keeps its physical slot");
    Equal(rusted.Id, detection.StatusId, "resolved Catch Wind keeps the status named in its detail prose");
    var preservedStatus = statuses.Single(status => status.Name == "存護");
    var statusImageResult = RhodesSuiCoinImageRecognizer.CreateOwnedResult(
    [
        new RhodesSuiCoinImageDetection(
            options[0].Id,
            options[0].Name,
            0.98,
            3,
            new MaaRoi(421, 306, 84, 84),
            StatusId: preservedStatus.Id),
    ]);
    Equal(
        preservedStatus.Id,
        RhodesSuiCatchWindDetailResolver.FindVisibleStatusId([statusImageResult], 3),
        "Catch Wind reads the status already identified from the visible card image");
    var imagePreferredDetection = RhodesSuiCatchWindDetailResolver.ResolveDetection(
        detailResult,
        3,
        options,
        statuses,
        preservedStatus.Id);
    Equal(
        preservedStatus.Id,
        imagePreferredDetection!.StatusId,
        "the visible-card image status takes priority over detail OCR fallback");
    var candidates = RhodesMaaLocalCandidateConverter.FromTaskResults(
        "is6CoinsFull",
        [RhodesSuiCoinImageRecognizer.CreateOwnedResult([detection])],
        "is6_sui");
    Equal(1, candidates.Count, "directional Catch Wind detail becomes one owned coin candidate");
    Equal(detection.CoinId, candidates.Single().CoinId, "directional Catch Wind detail preserves the resolved coin id");

    var ambiguousResult = detailResult with
    {
        RecognitionDetailJson = JsonSerializer.Serialize(new
        {
            filtered_results = new[]
            {
                new { text = "振り出されると、オペレーターの攻撃速度+40", score = 0.94 },
            },
        }),
    };
    Equal(null, RhodesSuiCatchWindDetailResolver.ResolveDetection(ambiguousResult, 3, options, statuses), "directionless prose is never guessed");
}

static void SuiCatchWindDetailResolverRandomizesTap()
{
    var step = RhodesSuiCatchWindDetailResolver.BuildTapStep(3);
    Equal("tap", step.Type, "Catch Wind target is a tap step");
    Equal(true, step.Width > 1 && step.Height > 1, "Catch Wind target uses an area instead of a fixed point");

    var first = RhodesRecognitionNavigation.RandomTapPoint(step, new Random(1));
    var second = RhodesRecognitionNavigation.RandomTapPoint(step, new Random(2));
    Equal(true, first != second, "Catch Wind target point varies inside the card area");
    Equal(true, first.X >= step.X && first.X < step.X + step.Width, "random Catch Wind x remains inside the card");
    Equal(true, first.Y >= step.Y && first.Y < step.Y + step.Height, "random Catch Wind y remains inside the card");
}

static void SuiOwnedCoinImageRecognizer()
{
    var options = RhodesRunCatalog.LoadSpecialEffectOptions("is6_sui", "coin");
    var first = options.Single(option => option.Id.EndsWith("is6_copper_b03", StringComparison.Ordinal));
    var second = options.Single(option => option.Id.EndsWith("is6_copper_b08", StringComparison.Ordinal));
    var unowned = options.Single(option => option.Id.EndsWith("is6_copper_b09", StringComparison.Ordinal));
    using var frame = new SKBitmap(1280, 720, SKColorType.Bgra8888, SKAlphaType.Premul);
    frame.Erase(new SKColor(230, 220, 217));
    using (var canvas = new SKCanvas(frame))
    {
        Draw(canvas, first.ImagePath, new SKRect(548, 158, 654, 264));
        Draw(canvas, second.ImagePath, new SKRect(828, 158, 934, 264));
        Draw(canvas, unowned.ImagePath, new SKRect(548, 433, 654, 539), 0.28f);
    }

    var result = RhodesSuiCoinImageRecognizer.RecognizeOwned(EncodePng(frame), options);
    Equal(true, result.Hit, "owned coin image result hit");
    Equal(true, RhodesSuiCoinImageRecognizer.TryRead(result, out var fieldId, out var detections), "owned coin image result readable");
    Equal("coins", fieldId, "owned coin field id");
    Equal(
        $"{first.Id}|{second.Id}",
        string.Join("|", detections.OrderBy(item => item.SlotIndex).Select(item => item.CoinId)),
        $"owned coin ids ({result.RecognitionDetailJson})");

    var candidates = RhodesMaaLocalCandidateConverter.FromTaskResults(
        "is6CoinsFull",
        [result],
        "is6_sui");
    Equal(2, candidates.Count, "owned coin candidate count");
    Equal(true, candidates.All(candidate => candidate.FieldId == "coins"), "owned coin candidate field");

    static void Draw(SKCanvas canvas, string path, SKRect destination, float opacity = 1f)
    {
        using var bitmap = SKBitmap.Decode(path);
        using var paint = new SKPaint { Color = SKColors.White.WithAlpha((byte)Math.Round(255 * opacity)) };
        canvas.DrawBitmap(bitmap, destination, paint);
    }
}

static void SuiOwnedCoinImageRecognizerPerformance()
{
    var options = RhodesRunCatalog.LoadSpecialEffectOptions("is6_sui", "coin");
    var ids = new[] { "is6_copper_b03", "is6_copper_b08", "is6_copper_b09", "is6_copper_f13" };
    var coins = ids.Select(id => options.Single(option => option.Id.EndsWith(id, StringComparison.Ordinal))).ToArray();
    using var frame = new SKBitmap(1280, 720, SKColorType.Bgra8888, SKAlphaType.Premul);
    frame.Erase(new SKColor(230, 220, 217));
    using (var canvas = new SKCanvas(frame))
    {
        Draw(canvas, coins[0].ImagePath, new SKRect(548, 158, 654, 264));
        Draw(canvas, coins[1].ImagePath, new SKRect(828, 158, 934, 264));
        Draw(canvas, coins[2].ImagePath, new SKRect(1090, 158, 1196, 264));
        Draw(canvas, coins[3].ImagePath, new SKRect(410, 295, 516, 401));
    }

    var encoded = EncodePng(frame);
    _ = RhodesSuiCoinImageRecognizer.RecognizeOwned(encoded);
    var stopwatch = Stopwatch.StartNew();
    var result = RhodesSuiCoinImageRecognizer.RecognizeOwned(encoded);
    stopwatch.Stop();

    Equal(true, result.Hit, "representative owned coin frame is recognized");
    Equal(true, stopwatch.Elapsed < TimeSpan.FromSeconds(2), $"owned coin frame elapsed {stopwatch.Elapsed.TotalMilliseconds:0}ms");

    static void Draw(SKCanvas canvas, string path, SKRect destination)
    {
        using var bitmap = SKBitmap.Decode(path);
        canvas.DrawBitmap(bitmap, destination);
    }
}

static void SuiOwnedCoinCandidateCounts()
{
    var options = RhodesRunCatalog.LoadSpecialEffectOptions("is6_sui", "coin");
    var statuses = RhodesRunCatalog.LoadSpecialEffectOptions("is6_sui", "coinStatus");
    var duplicate = options.Single(option => option.Id.EndsWith("is6_copper_b03", StringComparison.Ordinal));
    var single = options.Single(option => option.Id.EndsWith("is6_copper_b08", StringComparison.Ordinal));
    var status = statuses.Single(option => option.Id.EndsWith("is6_gild2", StringComparison.Ordinal));
    var firstFrame = ImageResult(
        Detection(duplicate, 0, 0.91),
        Detection(duplicate, 1, 0.90));
    var secondFrame = ImageResult(
        Detection(duplicate, 0, 0.92),
        Detection(single, 1, 0.89));

    var candidates = RhodesMaaLocalCandidateConverter.FromTaskResults(
        "is6CoinsFull",
        [firstFrame, secondFrame],
        "is6_sui");

    Equal(2, candidates.Single(candidate => candidate.CoinId == duplicate.Id).Count, "duplicate owned coin count uses the maximum visible count");
    Equal(1, candidates.Single(candidate => candidate.CoinId == single.Id).Count, "single owned coin count remains one");

    var statusAcrossFrames = RhodesMaaLocalCandidateConverter.FromTaskResults(
        "is6CoinsFull",
        [
            ImageResult(Detection(duplicate, 0, 0.91, status.Id)),
            ImageResult(Detection(duplicate, 0, 0.93)),
        ],
        "is6_sui");

    Equal(1, statusAcrossFrames.Count, "the same coin is not duplicated when status detection varies across overlapping frames");
    Equal(status.Id, statusAcrossFrames.Single().StatusId, "the detected coin status wins over a plain overlapping observation");
    Equal(1, statusAcrossFrames.Single().Count, "one physical status coin remains one entry");

    static object Detection(SukiSpecialEffectOption option, int slotIndex, double score, string statusId = "") => new
    {
        coinId = option.Id,
        label = option.Name,
        score,
        slotIndex,
        roi = new[] { 0, 0, 106, 106 },
        statusId,
    };

    static MaaTaskRunResult ImageResult(params object[] detections) => new(
        RhodesSuiCoinImageRecognizer.OwnedEntry,
        "Succeeded",
        true,
        $"ownedCoins={detections.Length}",
        JsonSerializer.Serialize(new { fieldId = "coins", detections }),
        "ImageClassification",
        detections.Length > 0);
}

static void SuiOwnedCoinOcrCandidateCounts()
{
    var options = RhodesRunCatalog.LoadSpecialEffectOptions("is6_sui", "coin");
    var duplicate = options.Single(option => option.Id.EndsWith("is6_copper_b03", StringComparison.Ordinal));
    var single = options.Single(option => option.Id.EndsWith("is6_copper_b08", StringComparison.Ordinal));
    var firstFrame = OcrResult(duplicate.Name, duplicate.Name, single.Name);
    var secondFrame = OcrResult(duplicate.Name, single.Name);

    var candidates = RhodesMaaLocalCandidateConverter.FromTaskResults(
        "is6CoinsFull",
        [firstFrame, secondFrame],
        "is6_sui");

    Equal(2, candidates.Single(candidate => candidate.CoinId == duplicate.Id).Count, "duplicate OCR coin count uses the maximum visible count");
    Equal(1, candidates.Single(candidate => candidate.CoinId == single.Id).Count, "overlapping OCR frames do not inflate coin count");

    var liveFirstFrame = OcrResult(
        "衝-志違げんと叡",
        "園嵐−西の寵真",
        "東の欠角",
        "奇土金を生ず",
        "水生じ木護る",
        "金寒く水衍く",
        "金寒く水衍く",
        "火灼き土沃す",
        "南に山を見る");
    var liveShiftedFrame = OcrResult(
        "火灼き土沃す",
        "南に山を見る",
        "投木炎延");
    var liveCandidates = RhodesMaaLocalCandidateConverter.FromTaskResults(
        "is6CoinsFull",
        [liveFirstFrame, liveShiftedFrame],
        "is6_sui");

    Equal(9, liveCandidates.Count, "live two-frame OCR keeps nine unique held coin names");
    Equal(10, liveCandidates.Sum(candidate => candidate.Count), "live two-frame OCR keeps all ten held coins");
    Equal(2, liveCandidates.Single(candidate => candidate.Label == "金寒く水衍く").Count, "live duplicate coin count is preserved");

    static MaaTaskRunResult OcrResult(params string[] names) => new(
        "RhodesOcrRegion_is6_coin_list_text",
        "Succeeded",
        true,
        $"coins={names.Length}",
        JsonSerializer.Serialize(new
        {
            filtered_results = names.Select((name, index) => new
            {
                text = name,
                score = 0.91,
                box = new[] { 100 + (index * 200), 100, 180, 30 },
            }),
        }),
        "OCR",
        true);
}

static void SuiOwnedCoinStatusRecognizer()
{
    var coins = RhodesRunCatalog.LoadSpecialEffectOptions("is6_sui", "coin");
    var statuses = RhodesRunCatalog.LoadSpecialEffectOptions("is6_sui", "coinStatus");
    var coin = coins.Single(option => option.Id.EndsWith("is6_copper_f13", StringComparison.Ordinal));
    var status = statuses.Single(option => option.Id.EndsWith("is6_gild2", StringComparison.Ordinal));
    var ocr = OcrResult(
        ("衛-志遂げんと配", 564, 612, 244, 32),
        ("志遂げんと欲す", 1124, 612, 244, 32));

    using var frame = new SKBitmap(1280, 720, SKColorType.Bgra8888, SKAlphaType.Premul);
    frame.Erase(new SKColor(230, 220, 217));
    using (var canvas = new SKCanvas(frame))
    {
        Draw(canvas, coin.ImagePath, new SKRect(410, 295, 516, 401));
        Draw(canvas, status.ImagePath, new SKRect(475, 302, 519, 348));
        Draw(canvas, coin.ImagePath, new SKRect(690, 295, 796, 401));
    }

    var result = RhodesSuiCoinStatusRecognizer.RecognizeOwned(
        EncodePng(frame),
        [ocr],
        coins,
        statuses);
    Equal(true, RhodesSuiCoinImageRecognizer.TryRead(result, out var fieldId, out var detections), "status result readable");
    Equal("coins", fieldId, "status result field");
    Equal(2, detections.Count, "both OCR anchored coins retained");
    Equal(status.Id, detections[0].StatusId, $"first coin receives the detected status ({result.RecognitionDetailJson})");
    Equal("", detections[1].StatusId, "second coin does not hallucinate a status");

    var candidates = RhodesMaaLocalCandidateConverter.FromTaskResults(
        "is6CoinsFull",
        [ocr, result],
        "is6_sui");
    Equal(2, candidates.Count, "same coin with different statuses remains two entries");
    Equal(true, candidates.Any(candidate => candidate.CoinId == coin.Id && candidate.StatusId == status.Id), "status coin candidate retained");
    Equal(true, candidates.Any(candidate => candidate.CoinId == coin.Id && string.IsNullOrWhiteSpace(candidate.StatusId)), "plain coin candidate retained");

    var ambiguousStatus = statuses.Single(option => option.Id.EndsWith("is6_gild5", StringComparison.Ordinal));
    using (var ambiguousFrame = new SKBitmap(1280, 720, SKColorType.Bgra8888, SKAlphaType.Premul))
    {
        ambiguousFrame.Erase(new SKColor(230, 220, 217));
        using (var canvas = new SKCanvas(ambiguousFrame))
        {
            Draw(canvas, coin.ImagePath, new SKRect(410, 295, 516, 401));
            Draw(canvas, ambiguousStatus.ImagePath, new SKRect(475, 302, 519, 348));
        }
        var ambiguousResult = RhodesSuiCoinStatusRecognizer.RecognizeOwned(
            EncodePng(ambiguousFrame),
            [OcrResult(("志遂げんと欲す", 564, 612, 244, 32))],
            coins,
            statuses);
        Equal(true, RhodesSuiCoinImageRecognizer.TryRead(ambiguousResult, out _, out var ambiguousDetections), "ambiguous status result readable");
        Equal(ambiguousStatus.Id, ambiguousDetections.Single().StatusId, "an actual ambiguous status survives the stricter threshold");
    }

    static MaaTaskRunResult OcrResult(params (string Text, int X, int Y, int Width, int Height)[] rows) => new(
        "RhodesOcrRegion_is6_coin_list_text",
        "Succeeded",
        true,
        $"coins={rows.Length}",
        JsonSerializer.Serialize(new
        {
            filtered_results = rows.Select(row => new
            {
                text = row.Text,
                score = 0.91,
                box = new[] { row.X, row.Y, row.Width, row.Height },
            }),
        }),
        "OCR",
        true);

    static void Draw(SKCanvas canvas, string path, SKRect destination)
    {
        using var bitmap = SKBitmap.Decode(path);
        canvas.DrawBitmap(bitmap, destination);
    }
}

static void SuiOwnedCoinOcrFallbackPlanner()
{
    var requests = RhodesSuiCoinImageRecognizer.PlanOwnedNameOcrRequests(
    [
        new RhodesSuiCoinImageDetection("resolved", "resolved", 0.81, 0, new MaaRoi(0, 0, 1, 1), RunnerUpScore: 0.70, VisualStrength: 0.95),
        new RhodesSuiCoinImageDetection("uncertain", "uncertain", 0.64, 3, new MaaRoi(0, 0, 1, 1), RunnerUpScore: 0.63, VisualStrength: 0.72),
        new RhodesSuiCoinImageDetection("dim", "dim", 0.78, 4, new MaaRoi(0, 0, 1, 1), RunnerUpScore: 0.74, VisualStrength: 0.50),
    ]);

    Equal(1, requests.Count, "only unresolved colored slot receives OCR");
    Equal("RhodesDynamic_is6.coin_list_text.slot3", requests[0].Entry, "owned coin OCR entry identifies slot");
    Equal("363,395,200,30", $"{requests[0].X},{requests[0].Y},{requests[0].Width},{requests[0].Height}", "owned coin name ROI");

    var options = RhodesRunCatalog.LoadSpecialEffectOptions("is6_sui", "coin");
    var imageCoin = options.Single(option => option.Id.EndsWith("is6_copper_b03", StringComparison.Ordinal));
    var ocrCoin = options.Single(option => option.Id.EndsWith("is6_copper_f13", StringComparison.Ordinal));
    var imageResult = new MaaTaskRunResult(
        RhodesSuiCoinImageRecognizer.OwnedEntry,
        "Succeeded",
        true,
        "ownedCoins=1",
        JsonSerializer.Serialize(new
        {
            fieldId = "coins",
            detections = new[]
            {
                new
                {
                    coinId = imageCoin.Id,
                    label = imageCoin.Name,
                    score = 0.91,
                    slotIndex = 0,
                    roi = new[] { 0, 0, 1, 1 },
                    statusId = "",
                },
            },
        }),
        "ImageClassification",
        true);
    var ocrResult = new MaaTaskRunResult(
        requests[0].Entry,
        "Succeeded",
        true,
        "detail",
        JsonSerializer.Serialize(new
        {
            filtered_results = new[] { new { text = "回衡-志遂げんとび", score = 0.88 } },
        }),
        "OCR",
        true);
    var candidates = RhodesMaaLocalCandidateConverter.FromTaskResults(
        "is6CoinsFull",
        [imageResult, ocrResult],
        "is6_sui");

    Equal(2, candidates.Count, "image and fallback OCR coin candidates merge");
    Equal(true, candidates.Any(candidate => candidate.CoinId == imageCoin.Id), "image coin retained");
    Equal(true, candidates.Any(candidate => candidate.CoinId == ocrCoin.Id), "status-prefixed OCR coin added");

    var broadOcr = new MaaTaskRunResult(
        "RhodesOcrRegion_is6_coin_list_text",
        "Succeeded",
        true,
        "detail",
        JsonSerializer.Serialize(new
        {
            filtered_results = new[]
            {
                new
                {
                    text = "衡-大炎通宝",
                    score = 0.92,
                    box = new[] { 762, 324, 400, 64 },
                },
            },
        }),
        "OCR",
        true);
    var missingRequests = RhodesSuiCoinImageRecognizer.PlanMissingOwnedNameOcrRequests(
    [
        new RhodesSuiCoinImageDetection("resolved", "resolved", 0.81, 0, new MaaRoi(0, 0, 1, 1), RunnerUpScore: 0.70, VisualStrength: 0.95),
        new RhodesSuiCoinImageDetection("unresolved", "unresolved", 0.64, 3, new MaaRoi(0, 0, 1, 1), RunnerUpScore: 0.63, VisualStrength: 0.72),
        new RhodesSuiCoinImageDetection("dim", "dim", 0.64, 4, new MaaRoi(0, 0, 1, 1), RunnerUpScore: 0.63, VisualStrength: 0.50),
        new RhodesSuiCoinImageDetection("empty", "empty", 0.64, 5, new MaaRoi(0, 0, 1, 1), RunnerUpScore: 0.63, VisualStrength: 0.30),
    ],
    [broadOcr]);
    Equal(2, missingRequests.Count, "broad OCR leaves only unresolved occupied slots for focused OCR");
    Equal(
        "RhodesDynamic_is6.coin_list_text.slot3|RhodesDynamic_is6.coin_list_text.slot4",
        string.Join("|", missingRequests.Select(request => request.Entry)),
        "dim held slots remain eligible while empty slots are excluded");
}

static void LocalCandidateConverterAllProfiles()
{
    var catalog = RhodesRunCatalog.LoadDefault();
    var relic = catalog.Relics.First(item => item.CampaignId == catalog.Current.CampaignId);
    var candidates = RhodesMaaLocalCandidateConverter.FromTaskResults(
        null,
        [
            M("RhodesOcrRegion_run_ingot", "20", 0.94),
            M("RhodesOcrRegion_operator_name_left_1", "グム", 0.91),
            M("RhodesOcrRegion_relic_list_text", relic.Name, 0.90),
            M("RhodesOcrRegion_is4_revelation_list_text", "歌唱", 0.89),
            M("RhodesOcrRegion_is5_thought_list_text", "走る都市", 0.89),
            M("RhodesOcrRegion_is5_age_detail_text", "天災の時代（全盛期）", 0.88),
            M("RhodesOcrRegion_is6_coin_list_text", "大炎通宝", 0.87),
        ]);

    Equal("ingot", string.Join("|", candidates.Where(item => item.Kind == "runStatus").Select(item => item.Field)), "all profile run fields");
    Equal("gummy", string.Join("|", candidates.Where(item => item.Kind == "operator").Select(item => item.OperatorId)), "all profile operator");
    Equal(relic.Id, string.Join("|", candidates.Where(item => item.Kind == "relic").Select(item => item.RelicId)), "all profile relic");
    Equal("is4_sami_selectable_revelationBoard_is4_kvama1", string.Join("|", candidates.Where(item => item.Kind == "revelation").Select(item => item.EffectId)), "all profile revelation");
    Equal("is5_sarkaz_selectable_thought_insp_20", string.Join("|", candidates.Where(item => item.Kind == "thought").Select(item => item.ThoughtId)), "all profile thought");
    Equal("is5_sarkaz_selectable_age_is5_age_01_formation", string.Join("|", candidates.Where(item => item.Kind == "age").Select(item => item.AgeId)), "all profile age group representative");
    Equal("is6_sui_selectable_coin_is6_copper_b01", string.Join("|", candidates.Where(item => item.Kind == "coin").Select(item => item.CoinId)), "all profile coin");

    static MaaTaskRunResult M(string entry, string text, double score)
    {
        var encodedText = System.Text.Json.JsonSerializer.Serialize(text);
        return new MaaTaskRunResult(
            entry,
            "Succeeded",
            true,
            "detail",
            $"{{\"filtered_results\":[{{\"text\":{encodedText},\"score\":{score.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}]}}",
            "OCR",
            true);
    }
}

static void AdbPresets()
{
    var presets = RhodesAdbPresetCatalog.DefaultPresets();
    var mumu = presets.Single(preset => preset.Id == "mumu");
    var googlePlay = presets.Single(preset => preset.Id == "google-play-games-dev");

    Equal("127.0.0.1:16384", mumu.Serial, "MuMu serial");
    Equal("127.0.0.1:6520", googlePlay.Serial, "Google Play Games developer serial");
}

static void AdbPresetCurrentMumuLayouts()
{
    var paths = RhodesAdbPresetCatalog.MuMuAdbPathsFromInstallRoot(@"C:\Program Files\Netease\MuMuPlayer");

    Equal(true, paths.Contains(@"C:\Program Files\Netease\MuMuPlayer\nx_main\adb.exe", StringComparer.OrdinalIgnoreCase), "MuMu 5 adb path");
    Equal(true, paths.Contains(@"C:\Program Files\Netease\MuMuPlayer\shell\adb.exe", StringComparer.OrdinalIgnoreCase), "legacy MuMu adb path");
    Equal(true, paths.Contains(@"C:\Program Files\Netease\MuMuPlayer\nx_device\15.0\shell\adb.exe", StringComparer.OrdinalIgnoreCase), "MuMu 15 shell adb path");
}

static void AdbPresetMumuProcessLayout()
{
    Equal(
        @"C:\Program Files\Netease\MuMuPlayer",
        RhodesAdbPresetCatalog.ResolveMuMuInstallRootFromProcessPath(
            @"C:\Program Files\Netease\MuMuPlayer\nx_device\12.0\shell\MuMuNxDevice.exe"),
        "current MuMu process install root");
    Equal(
        @"C:\Program Files\Netease\MuMu Player 12",
        RhodesAdbPresetCatalog.ResolveMuMuInstallRootFromProcessPath(
            @"C:\Program Files\Netease\MuMu Player 12\shell\MuMuPlayer.exe"),
        "legacy MuMu process install root");
    Equal(
        @"C:\Program Files\Netease\MuMuPlayer",
        RhodesAdbPresetCatalog.ResolveMuMuInstallRootFromUninstallString(
            "\"C:\\Program Files\\Netease\\MuMuPlayer\\uninstall.exe\" /S"),
        "MuMu registry uninstall install root");
}

static void AdbPresetMumuRunningVersion()
{
    var legacyProcess = @"C:\Program Files\Netease\MuMu Player 12\shell\MuMuPlayer.exe";
    var currentProcess = @"C:\Program Files\Netease\MuMuPlayer\nx_device\12.0\shell\MuMuNxDevice.exe";

    Equal(
        @"C:\Program Files\Netease\MuMu Player 12\shell\adb.exe",
        RhodesAdbPresetCatalog.MuMuAdbPathsFromProcessPath(legacyProcess).First(),
        "legacy running MuMu prefers shell adb");
    Equal(
        @"C:\Program Files\Netease\MuMuPlayer\nx_device\12.0\shell\adb.exe",
        RhodesAdbPresetCatalog.MuMuAdbPathsFromProcessPath(currentProcess).First(),
        "current running MuMu prefers the active device shell adb");

    var sorted = RhodesAdbLocalDetector.SortCandidates(
        [
            new MaaAdbPathCandidatePreview(@"C:\Program Files\Netease\MuMuPlayer\nx_main\adb.exe", "known-path", "mumu", true, true, ""),
            new MaaAdbPathCandidatePreview(@"C:\Program Files\Netease\MuMu Player 12\shell\adb.exe", "process", "mumu", true, true, ""),
        ],
        new RhodesAdbApiSettings(true, "auto", "adb", ""));
    Equal("process", sorted[0].Source, "running MuMu process candidate wins when both layouts remain installed");

    var staleSettingsSorted = RhodesAdbLocalDetector.SortCandidates(
        [
            new MaaAdbPathCandidatePreview(@"C:\Program Files\Netease\MuMuPlayer\shell\adb.exe", "settings", "mumu", true, true, ""),
            new MaaAdbPathCandidatePreview(@"C:\Program Files\Netease\MuMuPlayer\nx_main\adb.exe", "process", "mumu", true, true, ""),
        ],
        new RhodesAdbApiSettings(
            true,
            "mumu",
            @"C:\Program Files\Netease\MuMuPlayer\shell\adb.exe",
            "127.0.0.1:16384"));
    Equal(
        @"C:\Program Files\Netease\MuMuPlayer\nx_main\adb.exe",
        staleSettingsSorted[0].Path,
        "running current MuMu overrides a stale saved legacy adb path");

    var manualPath = @"D:\Android\platform-tools\adb.exe";
    var manualSorted = RhodesAdbLocalDetector.SortCandidates(
        [
            new MaaAdbPathCandidatePreview(manualPath, "settings", "custom", true, true, ""),
            new MaaAdbPathCandidatePreview(@"C:\Program Files\Netease\MuMuPlayer\nx_main\adb.exe", "process", "mumu", true, true, ""),
        ],
        new RhodesAdbApiSettings(true, "auto", manualPath, "emulator-5556"));
    Equal(manualPath, manualSorted[0].Path, "auto mode keeps an explicitly selected custom adb path");
}

static void AdbMethodCatalog()
{
    var input = SukiAdbMethodCatalog.FindInput(SukiAdbMethodCatalog.DefaultInputMethodIdForPreset("mumu"));
    var screencap = SukiAdbMethodCatalog.FindScreencap(SukiAdbMethodCatalog.DefaultScreencapMethodIdForPreset("mumu"));
    var ldInput = SukiAdbMethodCatalog.FindInput(SukiAdbMethodCatalog.DefaultInputMethodIdForPreset("ldplayer"));
    var ldScreencap = SukiAdbMethodCatalog.FindScreencap(SukiAdbMethodCatalog.DefaultScreencapMethodIdForPreset("ldplayer"));
    var googlePlay = SukiAdbMethodCatalog.FindScreencap(SukiAdbMethodCatalog.DefaultScreencapMethodIdForPreset("google-play-games-dev"));

    Equal(SukiAdbMethodCatalog.FastEmulatorMethodId, input.Id, "mumu input method");
    Equal(true, input.Value.HasFlag(AdbInputMethods.EmulatorExtras), "mumu input emulator extras");
    Equal(SukiAdbMethodCatalog.FastEmulatorMethodId, screencap.Id, "mumu screencap method");
    Equal(true, screencap.Value.HasFlag(AdbScreencapMethods.EmulatorExtras), "mumu screencap emulator extras");
    Equal(false, screencap.Value.HasFlag(AdbScreencapMethods.MinicapDirect), "mumu screencap avoids minicap direct");
    Equal(SukiAdbMethodCatalog.DefaultInputMethodId, ldInput.Id, "ldplayer input stays default");
    Equal(SukiAdbMethodCatalog.FastEmulatorMethodId, ldScreencap.Id, "ldplayer screencap fast");
    Equal(SukiAdbMethodCatalog.DefaultScreencapMethodId, googlePlay.Id, "google play stays default");
}

static void AdbConfigJsonNormalizer()
{
    Equal("{}", SukiAdbConfigJson.Normalize(""), "blank config");
    Equal("""{"touch":"adb"}""", SukiAdbConfigJson.Normalize(""" { "touch" : "adb" } """), "object config");
    ThrowsInvalidOperation(() => SukiAdbConfigJson.Normalize("not json"), "invalid json");
    ThrowsInvalidOperation(() => SukiAdbConfigJson.Normalize("[1,2]"), "array json");
}

static void AdbConnectionResolverBuildsExtras()
{
    var fastInput = SukiAdbMethodCatalog.FindInput(SukiAdbMethodCatalog.FastEmulatorMethodId).Value;
    var fastScreencap = SukiAdbMethodCatalog.FindScreencap(SukiAdbMethodCatalog.FastEmulatorMethodId).Value;
    var mumu = RhodesMaaSession.DefaultAdbOptions(
        @"C:\Program Files\Netease\MuMu Player 12\shell\adb.exe",
        "emulator-5556",
        "{}",
        fastInput,
        fastScreencap,
        "mumu");

    var resolvedMumu = RhodesMaaAdbConnectionResolver.ApplyPresetExtras(mumu);
    var mumuConfig = JsonNode.Parse(resolvedMumu.AdbConfigJson)!.AsObject();
    var mumuExtras = mumuConfig["extras"]!["mumu"]!.AsObject();
    Equal(true, mumuExtras["enable"]!.GetValue<bool>(), "MuMu extras enabled");
    Equal(@"C:\Program Files\Netease\MuMu Player 12", mumuExtras["path"]!.GetValue<string>(), "MuMu root path");
    Equal(1, mumuExtras["index"]!.GetValue<int>(), "MuMu emulator serial index");

    var currentMumu = RhodesMaaSession.DefaultAdbOptions(
        @"C:\Program Files\Netease\MuMuPlayer\nx_main\adb.exe",
        "127.0.0.1:16384",
        "{}",
        fastInput,
        fastScreencap,
        "mumu");
    var currentMumuConfig = JsonNode.Parse(RhodesMaaAdbConnectionResolver.ApplyPresetExtras(currentMumu).AdbConfigJson)!.AsObject();
    Equal(@"C:\Program Files\Netease\MuMuPlayer", currentMumuConfig["extras"]!["mumu"]!["path"]!.GetValue<string>(), "current MuMu root path");

    var manual = mumu with
    {
        AdbConfigJson = """{"extras":{"mumu":{"path":"D:/MuMu","index":9}},"custom":{"keep":true}}""",
    };
    var resolvedManual = RhodesMaaAdbConnectionResolver.ApplyPresetExtras(manual);
    var manualConfig = JsonNode.Parse(resolvedManual.AdbConfigJson)!.AsObject();
    Equal("D:/MuMu", manualConfig["extras"]!["mumu"]!["path"]!.GetValue<string>(), "manual MuMu path preserved");
    Equal(9, manualConfig["extras"]!["mumu"]!["index"]!.GetValue<int>(), "manual MuMu index preserved");
    Equal(true, manualConfig["custom"]!["keep"]!.GetValue<bool>(), "manual custom config preserved");

    var ld = RhodesMaaSession.DefaultAdbOptions(
        @"C:\leidian\LDPlayer9\adb.exe",
        "127.0.0.1:5557",
        "{}",
        AdbInputMethods.Default,
        fastScreencap,
        "ldplayer");
    var resolvedLd = RhodesMaaAdbConnectionResolver.ApplyPresetExtras(ld);
    var ldConfig = JsonNode.Parse(resolvedLd.AdbConfigJson)!.AsObject();
    Equal(true, ldConfig["extras"]!["ld"]!["enable"]!.GetValue<bool>(), "LD extras enabled");
    Equal(@"C:\leidian\LDPlayer9", ldConfig["extras"]!["ld"]!["path"]!.GetValue<string>(), "LD root path");
    Equal(1, ldConfig["extras"]!["ld"]!["index"]!.GetValue<int>(), "LD TCP serial index");
}

static void AdbConnectionResolverUsesToolkit()
{
    var requested = RhodesMaaSession.DefaultAdbOptions(
        "adb",
        "",
        """{"custom":{"keep":true}}""",
        AdbInputMethods.Default,
        AdbScreencapMethods.Default,
        "auto");
    var toolkitDevice = new AdbDeviceInfo(
        "MuMu Player",
        @"C:\Program Files\Netease\MuMu Player 12\shell\adb.exe",
        "127.0.0.1:16384",
        AdbScreencapMethods.EmulatorExtras | AdbScreencapMethods.RawWithGzip,
        AdbInputMethods.EmulatorExtras | AdbInputMethods.AdbShell,
        """{"extras":{"mumu":{"enable":true,"path":"C:/MuMu","index":0}}}""");

    var resolution = RhodesMaaAdbConnectionResolver.ResolveToolkitDevice(requested, [toolkitDevice]);
    Equal(true, resolution.DeviceResolved, "single Toolkit device resolved");
    Equal("127.0.0.1:16384", resolution.Options.AdbSerial, "Toolkit serial adopted");
    Equal(toolkitDevice.AdbPath, resolution.Options.AdbPath, "Toolkit adb path adopted");
    Equal(toolkitDevice.ScreencapMethods, resolution.Options.ScreencapMethod, "Toolkit screencap adopted");
    Equal(toolkitDevice.InputMethods, resolution.Options.InputMethod, "Toolkit input adopted");
    var config = JsonNode.Parse(resolution.Options.AdbConfigJson)!.AsObject();
    Equal(true, config["custom"]!["keep"]!.GetValue<bool>(), "manual config merged over Toolkit config");
    Equal(true, config["extras"]!["mumu"]!["enable"]!.GetValue<bool>(), "Toolkit extras retained");
}

static void AdbConnectionResolverDiscoversToolkit()
{
    var requested = RhodesMaaSession.DefaultAdbOptions(
        "C:/Tools/adb.exe",
        "127.0.0.1:16384",
        "{}",
        AdbInputMethods.Default,
        AdbScreencapMethods.Default,
        "mumu");
    var calledPath = "";
    var toolkitDevice = new AdbDeviceInfo(
        "MuMu Player",
        "C:/Tools/adb.exe",
        "127.0.0.1:16384",
        AdbScreencapMethods.EmulatorExtras | AdbScreencapMethods.RawWithGzip,
        AdbInputMethods.EmulatorExtras | AdbInputMethods.AdbShell,
        """{"extras":{"mumu":{"enable":true,"path":"C:/MuMu","index":0}}}""");

    var resolution = RhodesMaaAdbConnectionResolver.ResolveToolkitAsync(
        requested,
        (adbPath, _) =>
        {
            calledPath = adbPath;
            return Task.FromResult<IReadOnlyList<AdbDeviceInfo>>([toolkitDevice]);
        }).GetAwaiter().GetResult();

    Equal("C:/Tools/adb.exe", calledPath, "Toolkit receives selected adb path");
    Equal(true, resolution.DeviceResolved, "Toolkit async resolution succeeded");
    Equal("127.0.0.1:16384", resolution.Options.AdbSerial, "Toolkit async serial");
}

static void AdbDeviceParsing()
{
    var devices = RhodesAdbDeviceProbe.ParseDevices(
        """
        List of devices attached
        127.0.0.1:16384 device product:MuMu model:MuMu_Player transport_id:1
        emulator-5554 offline transport_id:2
        """);

    Equal(2, devices.Count, "device count");
    Equal("127.0.0.1:16384", devices[0].Serial, "first serial");
    Equal(true, devices[0].IsUsable, "first usable");
    Equal("offline", devices[1].State, "second state");
    Equal(false, devices[1].IsUsable, "second usable");
}

static void AdbCandidateRegistry()
{
    var candidates = new[]
    {
        new MaaAdbPathCandidatePreview("C:/Missing/MuMu/adb.exe", "known-path", "mumu", false, false, ""),
        new MaaAdbPathCandidatePreview("C:/Missing/Saved/adb.exe", "settings", "custom", false, false, ""),
        new MaaAdbPathCandidatePreview("C:/Tools/adb.exe", "path", "custom", false, false, "old failure"),
        new MaaAdbPathCandidatePreview("C:/Tools/adb.exe", "mumu", "mumu", true, true, ""),
        new MaaAdbPathCandidatePreview("adb", "path", "custom", false, false, "PATH lookup failed"),
        new MaaAdbPathCandidatePreview("adb", "settings", "custom", false, false, "saved PATH lookup failed")
    };

    var normalized = RhodesAdbCandidateRegistry.Normalize(candidates, path =>
        path.Equals("C:/Tools/adb.exe", StringComparison.OrdinalIgnoreCase));

    Equal(
        "C:/Tools/adb.exe|adb|C:/Missing/Saved/adb.exe",
        string.Join("|", normalized.Select(item => item.Path)),
        "focused candidate list");
    Equal(true, normalized[0].Available, "duplicate prefers available candidate");
    Equal("settings", normalized[1].Source, "explicit PATH adb remains visible for diagnostics");
    Equal(false, normalized.Any(item => item.Source == "path" && item.Path.Equals("adb", StringComparison.OrdinalIgnoreCase)), "unavailable auto PATH adb is hidden");
    Equal(false, normalized.Any(item => item.Path.Contains("Missing/MuMu", StringComparison.Ordinal)), "missing auto probe is hidden");

    var selected = RhodesAdbCandidateRegistry.SelectDefault(normalized, "C:/Tools/adb.exe");
    Equal("C:/Tools/adb.exe", selected?.Path ?? "", "current path selection");

    selected = RhodesAdbCandidateRegistry.SelectDefault(normalized, "C:/Other/adb.exe");
    Equal("C:/Tools/adb.exe", selected?.Path ?? "", "first selectable fallback");
}

static void SukiAdbDetectionWorkflow()
{
    var snapshot = RhodesSukiAdbDetectionWorkflow.DetectAsync(
        new RhodesAdbApiSettings(true, "mumu", "C:/Missing/adb.exe", ""),
        (_, _) => Task.FromResult(new RhodesAdbLocalDetectionResult(
            "C:/Tools/MuMu/adb.exe",
            "C:/Tools/MuMu/adb.exe",
            "127.0.0.1:16384",
            [
                new MaaAdbPathCandidatePreview("C:/Tools/MuMu/adb.exe", "known-path", "mumu", true, true, ""),
                new MaaAdbPathCandidatePreview("C:/Missing/adb.exe", "settings", "custom", false, false, "")
            ],
            [new MaaAdbDevicePreview("127.0.0.1:16384", "device", "product:Hapburn")],
            new RhodesAdbConnectPreview("127.0.0.1:16384", true, "connected"),
            "")),
        path => path.Contains("Tools", StringComparison.OrdinalIgnoreCase)).GetAwaiter().GetResult();

    Equal("C:/Tools/MuMu/adb.exe", snapshot.AdbPath, "workflow adb path");
    Equal("127.0.0.1:16384", snapshot.AdbSerial, "workflow serial");
    Equal("C:/Tools/MuMu/adb.exe", snapshot.SelectedAdbPathCandidate?.Path ?? "", "workflow selected candidate");
    Equal(2, snapshot.AdbCandidates.Count, "workflow candidates");
    Equal(1, snapshot.Devices.Count, "workflow devices");
    Equal("Sukiローカル検出: ADB候補2件 / 端末1件", snapshot.DetectionSummary, "workflow summary");
    Equal("選択中: C:/Tools/MuMu/adb.exe / 127.0.0.1:16384 / connect 127.0.0.1:16384", snapshot.DetectionDetail, "workflow detail");
    Equal("ADB検出OK", snapshot.SessionState, "workflow session state");
    Equal("Sukiローカル検出で端末を取得しました: 1件", snapshot.StatusMessage, "workflow status");
}

static void SukiAdbDetectionPresetPromotion()
{
    var mumu = new MaaAdbPathCandidatePreview("C:/Tools/MuMu/adb.exe", "known-path", "mumu", true, true, "");
    var googlePlay = new MaaAdbPathCandidatePreview("C:/Tools/Google/adb.exe", "known-path", "google-play-games-dev", true, true, "");
    var custom = new MaaAdbPathCandidatePreview("C:/Tools/adb.exe", "manual", "custom", true, true, "");

    Equal("mumu", RhodesSukiAdbDetectionWorkflow.ResolveDetectedPresetId("auto", mumu), "auto adopts mumu");
    Equal("google-play-games-dev", RhodesSukiAdbDetectionWorkflow.ResolveDetectedPresetId("custom", googlePlay), "custom adopts google play");
    Equal("google-play-games-dev", RhodesSukiAdbDetectionWorkflow.ResolveDetectedPresetId("", googlePlay), "empty adopts google play");
    Equal("bluestacks", RhodesSukiAdbDetectionWorkflow.ResolveDetectedPresetId("bluestacks", mumu), "explicit preset is not overwritten");
    Equal("auto", RhodesSukiAdbDetectionWorkflow.ResolveDetectedPresetId("auto", custom), "custom candidate does not change auto");
    Equal("auto", RhodesSukiAdbDetectionWorkflow.ResolveDetectedPresetId("auto", null), "missing candidate keeps current");
}

static void AdbApiDetectionParsing()
{
    var result = RhodesAdbApiClient.ExtractDetectionResult(
        """
        {
          "runtime": {
            "adbPath": "M:/Program Files/Netease/MuMu Player 12/shell/adb.exe",
            "serial": "127.0.0.1:16384"
          },
          "selectedAdbPath": "M:/Program Files/Netease/MuMu Player 12/shell/adb.exe",
          "adbCandidates": [
            {
              "path": "M:/Program Files/Netease/MuMu Player 12/shell/adb.exe",
              "source": "mumu",
              "preset": "mumu",
              "exists": true,
              "available": true,
              "error": null
            }
          ],
          "devices": [
            {
              "serial": "127.0.0.1:16384",
              "state": "device",
              "detail": "product:Hapburn model:HBN_AL00"
            }
          ]
        }
        """);

    Equal(true, result.Succeeded, "detect succeeded");
    Equal("M:/Program Files/Netease/MuMu Player 12/shell/adb.exe", result.RuntimeAdbPath, "runtime adb path");
    Equal("127.0.0.1:16384", result.RuntimeSerial, "runtime serial");
    Equal(1, result.AdbCandidates.Count, "candidate count");
    Equal(true, result.AdbCandidates[0].Available, "candidate available");
    Equal(1, result.Devices.Count, "device count");
    Equal(true, result.Devices[0].IsUsable, "device usable");
}

static void AdbApiTestParsing()
{
    var result = RhodesAdbApiClient.ExtractTestResult(
        """
        {
          "ok": true,
          "runtime": {
            "adbPath": "adb",
            "serial": "127.0.0.1:6520"
          },
          "resolution": {
            "width": 1280,
            "height": 720
          },
          "screenshot": {
            "bytes": 123456,
            "capturedAt": "2026-07-01T00:00:00.000Z",
            "path": "O:/debug/adb-test.png"
          }
        }
        """);

    Equal(true, result.Succeeded, "test succeeded");
    Equal("127.0.0.1:6520", result.RuntimeSerial, "runtime serial");
    Equal(1280, result.Width, "width");
    Equal(720, result.Height, "height");
    Equal(123456L, result.ScreenshotBytes, "screenshot bytes");
    Equal("O:/debug/adb-test.png", result.ScreenshotPath, "screenshot path");
}

static void SukiLocalAdbDetectGooglePlay()
{
    var calls = new List<string>();
    var connected = false;
    var result = RhodesAdbLocalDetector.DetectAsync(
        new RhodesAdbApiSettings(true, "google-play-games-dev", "", ""),
        fileExists: _ => false,
        runCommand: (adbPath, args, _) =>
        {
            calls.Add($"{adbPath} {string.Join(" ", args)}");
            if (args[0] == "version")
                return Task.FromResult(new RhodesAdbCommandResult(0, "Android Debug Bridge version 1.0.41", ""));
            if (args[0] == "connect")
            {
                connected = true;
                return Task.FromResult(new RhodesAdbCommandResult(0, "connected to 127.0.0.1:6520", ""));
            }
            if (args[0] == "devices")
                return Task.FromResult(new RhodesAdbCommandResult(0, connected
                    ? "List of devices attached\n127.0.0.1:6520 device product:gpg model:Google_Play_Games\n"
                    : "List of devices attached\n", ""));
            throw new InvalidOperationException("unexpected command");
        }).GetAwaiter().GetResult();

    Equal(true, result.Succeeded, "local detect succeeded");
    Equal("adb", result.SelectedAdbPath, "selected adb");
    Equal("127.0.0.1:6520", result.RuntimeSerial, "runtime serial");
    Equal("127.0.0.1:6520", result.Connect?.Address ?? "", "connect address");
    Equal(true, calls.Any(call => call == "adb connect 127.0.0.1:6520"), "connect called");
}

static void SukiLocalAdbDetectExplicitMumu()
{
    var selectedPath = "M:/Program Files/Netease/MuMu Player 12/shell/adb.exe";
    var result = RhodesAdbLocalDetector.DetectAsync(
        new RhodesAdbApiSettings(true, "auto", selectedPath, ""),
        fileExists: path => path.Equals(selectedPath, StringComparison.OrdinalIgnoreCase),
        runCommand: (adbPath, args, _) =>
        {
            if (args[0] == "version")
                return Task.FromResult(new RhodesAdbCommandResult(0, "Android Debug Bridge version 1.0.41", ""));
            if (args[0] == "connect")
                return Task.FromResult(new RhodesAdbCommandResult(0, $"connected to {args[1]}", ""));
            if (args[0] == "devices")
                return Task.FromResult(new RhodesAdbCommandResult(0, "List of devices attached\n127.0.0.1:16384 device product:MuMu model:MuMu_Player\n", ""));
            throw new InvalidOperationException("unexpected command");
        }).GetAwaiter().GetResult();

    Equal(true, result.Succeeded, "local detect succeeded");
    Equal(selectedPath, result.SelectedAdbPath, "selected adb path");
    Equal("127.0.0.1:16384", result.RuntimeSerial, "runtime serial");
}

static void SukiLocalAdbDetectExistingMumuDevice()
{
    var selectedPath = "M:/Program Files/Netease/MuMu Player 12/nx_main/adb.exe";
    var calls = new List<string>();
    var result = RhodesAdbLocalDetector.DetectAsync(
        new RhodesAdbApiSettings(true, "mumu", selectedPath, "127.0.0.1:16384"),
        fileExists: path => path.Equals(selectedPath, StringComparison.OrdinalIgnoreCase),
        runCommand: (adbPath, args, _) =>
        {
            calls.Add(string.Join(" ", args));
            if (args[0] == "version")
                return Task.FromResult(new RhodesAdbCommandResult(0, "Android Debug Bridge version 1.0.41", ""));
            if (args[0] == "connect")
                return Task.FromResult(new RhodesAdbCommandResult(1, "", "connection refused"));
            if (args[0] == "devices")
                return Task.FromResult(new RhodesAdbCommandResult(0, "List of devices attached\nemulator-5556 device product:Hapburn model:HBN_AL00\n", ""));
            throw new InvalidOperationException("unexpected command");
        }).GetAwaiter().GetResult();

    Equal(true, result.Succeeded, "existing MuMu device detection succeeded");
    Equal("emulator-5556", result.RuntimeSerial, "usable device replaces stale saved serial");
    Equal(false, calls.Any(call => call.StartsWith("connect ", StringComparison.Ordinal)), "existing device skips stale TCP connect probes");
}

static void SukiAdbConnectionTestWorkflow()
{
    var noAdb = RhodesSukiAdbConnectionTestWorkflow.NoAvailableAdb("候補なし");
    Equal("ADB接続テスト失敗", noAdb.SessionState, "no adb state");
    Equal("候補なし", noAdb.SessionDetail, "no adb detail");
    Equal("利用可能なADBが見つかりません: 候補なし", noAdb.StatusMessage, "no adb status");

    var controllerFailure = RhodesSukiAdbConnectionTestWorkflow.FromController(
        new MaaSessionSnapshot("failed", "DLL was not found", "", "", false, false, false));
    Equal("MAAFramework未準備", controllerFailure.SessionState, "controller framework failure state");
    Equal("ADB接続前にMAAFramework runtimeを確認してください: DLL was not found", controllerFailure.StatusMessage, "controller framework failure status");

    var nativePreflightFailure = RhodesSukiAdbConnectionTestWorkflow.FromController(
        new MaaSessionSnapshot("MAAFramework ネイティブ未配置", "runtime=win-x64; missing=MaaFramework.dll", "", "", false, false, false));
    Equal("MAAFramework未準備", nativePreflightFailure.SessionState, "preflight failure state");
    Equal(true, nativePreflightFailure.StatusMessage.Contains("missing=MaaFramework.dll", StringComparison.Ordinal), "preflight failure detail");

    var captureSuccess = RhodesSukiAdbConnectionTestWorkflow.FromCapture(
        new MaaCaptureResult("Succeeded", true, "750,914 bytes", [1, 2, 3]),
        "127.0.0.1:16384",
        "1280x720 (16:9)",
        "controller OK");
    Equal("ADB接続OK", captureSuccess.SessionState, "capture success state");
    Equal("127.0.0.1:16384 / 1280x720 (16:9)", captureSuccess.SessionDetail, "capture success detail");
    Equal("ADB接続テスト成功: 1280x720 (16:9)", captureSuccess.StatusMessage, "capture success status");

    var captureFailure = RhodesSukiAdbConnectionTestWorkflow.FromCapture(
        new MaaCaptureResult("Failed", false, "Cached image を取得できませんでした。", []),
        "127.0.0.1:16384",
        "-",
        "controller OK");
    Equal("ADB接続OK / 撮影失敗", captureFailure.SessionState, "capture failure state");
    Equal("Cached image を取得できませんでした。", captureFailure.SessionDetail, "capture failure detail");
}

static void SukiSettingsStore()
{
    var directory = Path.Combine(Path.GetTempPath(), "rhodes-suki-tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(directory);
    var path = Path.Combine(directory, "settings.json");
    try
    {
        RhodesSukiSettingsStore.Save(
            new RhodesSukiSettings(
                "C:/Tools/adb.exe",
                "127.0.0.1:16384",
                """{"touch":"adb"}""",
                "http://127.0.0.1:5173",
                "mumu",
                "operatorsFull",
                SukiAdbMethodCatalog.FastEmulatorMethodId,
                SukiAdbMethodCatalog.FastEmulatorMethodId,
                OverlayLayout:
                [
                    new SukiOverlayLayoutState("status", true, 40, 30, 1200, 120, 2),
                ]),
            path);

        var loaded = RhodesSukiSettingsStore.Load(path);
        Equal("C:/Tools/adb.exe", loaded.AdbPath, "adb path");
        Equal("127.0.0.1:16384", loaded.AdbSerial, "adb serial");
        Equal("mumu", loaded.SelectedAdbPresetId, "preset");
        Equal("operatorsFull", loaded.SelectedResourceProfileId, "profile");
        Equal(SukiAdbMethodCatalog.FastEmulatorMethodId, loaded.AdbInputMethodId, "input method");
        Equal(SukiAdbMethodCatalog.FastEmulatorMethodId, loaded.AdbScreencapMethodId, "screencap method");
        Equal(1, loaded.OverlayLayout?.Count ?? 0, "overlay layout count");
        Equal(40, loaded.OverlayLayout?[0].X ?? -1, "overlay layout x");
    }
    finally
    {
        Directory.Delete(directory, true);
    }
}

static void SukiSettingsStoreMigratesBareManualAdb()
{
    var normalized = RhodesSukiSettingsStore.Normalize(new RhodesSukiSettings(
        AdbPath: "adb",
        AdbSerial: "",
        SelectedAdbPresetId: "custom"));

    Equal("auto", normalized.SelectedAdbPresetId, "bare manual adb preset migrates to auto");
    Equal("adb", normalized.AdbPath, "bare adb path remains portable");

    var explicitManual = RhodesSukiSettingsStore.Normalize(new RhodesSukiSettings(
        AdbPath: "C:/Tools/adb.exe",
        AdbSerial: "127.0.0.1:16384",
        SelectedAdbPresetId: "custom"));
    Equal("custom", explicitManual.SelectedAdbPresetId, "explicit manual adb preset is preserved");
}

static void OptionalRuntimeStatusParsing()
{
    var installed = RhodesOptionalRuntimeProbe.ParseStatusJson(
        """{"status":"ready","installed":true,"installing":false,"installRoot":"D:/state/glm-ocr-runtime"}""",
        "GLM-OCR");
    var missing = RhodesOptionalRuntimeProbe.ParseStatusJson(
        """{"status":"missing","installed":false,"installing":false,"installRoot":"D:/state/ollama-runtime"}""",
        "Ollama");
    var installing = RhodesOptionalRuntimeProbe.ParseStatusJson(
        """{"status":"installing","installed":false,"installing":true}""",
        "GLM-OCR");

    Equal("導入済み", installed.State, "installed state");
    Equal(true, installed.Installed, "installed flag");
    Equal(true, installed.Detail.Contains("D:/state/glm-ocr-runtime", StringComparison.Ordinal), "installed detail");
    Equal("未導入", missing.State, "missing state");
    Equal(false, missing.Installed, "missing flag");
    Equal("導入中", installing.State, "installing state");
    Equal(true, installing.Installing, "installing flag");
    var actionStatus = RhodesOptionalRuntimeProbe.ParseStatusJson(
        """{"status":"partial","installed":true,"installing":true,"root":"D:/state/ollama-runtime"}""",
        "Ollama");
    Equal("導入中", actionStatus.State, "action status installing");
    Equal(true, actionStatus.Installed, "action status installed");
}

static void SukiOptionalRuntimeActionWorkflow()
{
    var success = RhodesSukiOptionalRuntimeActionWorkflow.RunAsync(
        "GLM-OCR導入",
        (_, _) => Task.FromResult(new SukiOptionalRuntimeActionResult(
            new SukiOptionalRuntimeStatus("GLM-OCR", "導入済み", "ready", true, false),
            "")),
        "http://127.0.0.1:5173").GetAwaiter().GetResult();

    Equal("導入済み", success.RuntimeStatus.State, "success runtime state");
    Equal("接続済み", success.ApiStatus.State, "success API state");
    Equal(true, success.ApiStatus.Installed, "success API available flag");
    Equal("GLM-OCR導入: 導入済み", success.StatusMessage, "success message");

    var failure = RhodesSukiOptionalRuntimeActionWorkflow.RunAsync(
        "Ollama起動",
        (_, _) => Task.FromResult(new SukiOptionalRuntimeActionResult(
            new SukiOptionalRuntimeStatus("Ollama", "操作失敗", "connection refused", false, false),
            "connection refused")),
        "http://127.0.0.1:5173").GetAwaiter().GetResult();

    Equal("操作失敗", failure.RuntimeStatus.State, "failure runtime state");
    Equal("接続失敗", failure.ApiStatus.State, "failure API state");
    Equal(false, failure.ApiStatus.Installed, "failure API available flag");
    Equal("Ollama起動失敗: connection refused", failure.StatusMessage, "failure message");
}

static void AdbDiagnosticsChecklist()
{
    var missing = RhodesAdbDiagnosticsChecklist.Build(new RhodesAdbDiagnosticsInput(
        "",
        false,
        false,
        0,
        0,
        "",
        null,
        "",
        null,
        "",
        0,
        0));

    Equal(6, missing.Count, "step count");
    Equal("失敗", missing[0].State, "missing adb path fails");
    Equal(true, missing[0].NextAction.Contains("adb.exe", StringComparison.Ordinal), "missing adb guidance");
    Equal("未確認", missing[1].State, "adb launch waits for detection");
    Equal("未確認", missing[5].State, "resolution waits for capture");

    var adbPath = Path.Combine(Path.GetTempPath(), $"rhodes-adb-{Guid.NewGuid():N}.exe");
    try
    {
        File.WriteAllText(adbPath, "fake");
        var readyWithWarnings = RhodesAdbDiagnosticsChecklist.Build(new RhodesAdbDiagnosticsInput(
            adbPath,
            true,
            true,
            2,
            2,
            "127.0.0.1:16384 / emulator-5554",
            false,
            "DLL not found",
            false,
            "capture failed",
            1920,
            1080));

        Equal("OK", readyWithWarnings[0].State, "existing adb path ok");
        Equal("OK", readyWithWarnings[1].State, "adb launch ok");
        Equal("注意", readyWithWarnings[2].State, "multiple devices require explicit serial");
        Equal(true, readyWithWarnings[2].NextAction.Contains("serial", StringComparison.Ordinal), "multiple device guidance");
        Equal("失敗", readyWithWarnings[3].State, "maa failure shown");
        Equal(true, readyWithWarnings[3].NextAction.Contains("VC++", StringComparison.Ordinal), "maa dll guidance");
        Equal("失敗", readyWithWarnings[4].State, "capture failure shown");
        Equal("注意", readyWithWarnings[5].State, "16:9 non-1280 warns");
        Equal(true, readyWithWarnings[5].Detail.Contains("1920x1080", StringComparison.Ordinal), "resolution detail");

        var exactBaseResolution = RhodesAdbDiagnosticsChecklist.Build(new RhodesAdbDiagnosticsInput(
            adbPath,
            true,
            true,
            1,
            1,
            "127.0.0.1:16384",
            true,
            "connected",
            true,
            "capture ok",
            1280,
            720));

        Equal("OK", exactBaseResolution[2].State, "single usable device ok");
        Equal("OK", exactBaseResolution[3].State, "maa ready ok");
        Equal("OK", exactBaseResolution[4].State, "capture ready ok");
        Equal("OK", exactBaseResolution[5].State, "base resolution ok");
    }
    finally
    {
        if (File.Exists(adbPath))
            File.Delete(adbPath);
    }
}

static void AdbDiagnosticsCopyText()
{
    var steps = RhodesAdbDiagnosticsChecklist.Build(new RhodesAdbDiagnosticsInput(
        "M:/Program Files/Netease/MuMu Player 12/shell/adb.exe",
        true,
        true,
        2,
        2,
        "127.0.0.1:16384 / emulator-5554",
        false,
        "DLL not found",
        false,
        "capture failed",
        1920,
        1080));

    var text = RhodesAdbDiagnosticsChecklist.BuildCopyText(
        new RhodesAdbDiagnosticsCopyInput(
            "IS#5 サルカズの炉辺奇談",
            "MuMu Player",
            "M:/Program Files/Netease/MuMu Player 12/shell/adb.exe",
            "127.0.0.1:16384",
            "MuMu/LD高速撮影",
            "emulator-fast",
            "MuMu高速入力",
            "emulator-fast",
            "MAA-OCR",
            "maa-ocr",
            "1920x1080",
            "capture failed",
            "失敗",
            "DLL not found",
            "MuMu検出",
            "candidate selected"),
        steps);

    Equal(true, text.Contains("RHODES OBS COMMANDER3373 ADB診断", StringComparison.Ordinal), "copy header");
    Equal(true, text.Contains("IS: IS#5 サルカズの炉辺奇談", StringComparison.Ordinal), "copy campaign");
    Equal(true, text.Contains("ADB preset: MuMu Player", StringComparison.Ordinal), "copy preset");
    Equal(true, text.Contains("serial: 127.0.0.1:16384", StringComparison.Ordinal), "copy serial");
    Equal(true, text.Contains("撮影方式: MuMu/LD高速撮影 (emulator-fast)", StringComparison.Ordinal), "copy screencap");
    Equal(true, text.Contains("入力方式: MuMu高速入力 (emulator-fast)", StringComparison.Ordinal), "copy input");
    Equal(true, text.Contains("OCR: MAA-OCR (maa-ocr)", StringComparison.Ordinal), "copy ocr");
    Equal(true, text.Contains("capture: 1920x1080 / capture failed", StringComparison.Ordinal), "copy capture");
    Equal(true, text.Contains("MAA: 失敗 / DLL not found", StringComparison.Ordinal), "copy maa failure");
    Equal(true, text.Contains("ADB detection: MuMu検出 / candidate selected", StringComparison.Ordinal), "copy detection");
    Equal(true, text.Contains("3. 端末検出 [注意]", StringComparison.Ordinal), "copy step state");
    Equal(true, text.Contains("next: 使用する端末の「使用」を押してserialを固定してください。", StringComparison.Ordinal), "copy next action");
}

static void PreviewUrlBuilder()
{
    Equal("http://127.0.0.1:5173/sidecar", RhodesPreviewUrlBuilder.Build("", ""), "default preview url");
    Equal("http://127.0.0.1:5173/sidecar", RhodesPreviewUrlBuilder.Build("http://127.0.0.1:5173/", "sidecar"), "sidecar url");
    Equal("http://localhost:8080/overlay?layout=compact", RhodesPreviewUrlBuilder.Build("http://localhost:8080", "/overlay?layout=compact"), "overlay url");
}

static void ManagedNodeRuntimePrefersAppRuntime()
{
    var baseDirectory = Directory.CreateTempSubdirectory("rhodes-node-runtime-").FullName;
    try
    {
        var runtime = new RhodesNodeRuntimeManager(baseDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(runtime.ManagedNodeExecutablePath)!);
        File.WriteAllText(runtime.ManagedNodeExecutablePath, "node");

        var status = runtime.Probe();

        Equal(true, status.IsAvailable, "managed node available");
        Equal(true, status.IsManaged, "managed node selected");
        Equal("導入済み", status.State, "managed node state");
        Equal(runtime.ManagedNodeExecutablePath, runtime.ResolveNodeExecutable(), "managed node executable");
    }
    finally
    {
        Directory.Delete(baseDirectory, recursive: true);
    }
}

static void ManagedNodeRuntimeInstallsVerifiedArchive()
{
    var baseDirectory = Directory.CreateTempSubdirectory("rhodes-node-install-").FullName;
    try
    {
        using var archiveStream = new MemoryStream();
        using (var archive = new ZipArchive(archiveStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry($"{RhodesNodeRuntimeManager.DistributionDirectoryName}/node.exe");
            using var writer = new StreamWriter(entry.Open());
            writer.Write("node");
        }

        var bytes = archiveStream.ToArray();
        var expectedHash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        using var installStream = new MemoryStream(bytes);
        var runtime = new RhodesNodeRuntimeManager(baseDirectory);

        var result = runtime.InstallArchiveAsync(installStream, expectedHash).GetAwaiter().GetResult();

        Equal(true, result.Succeeded, "verified node install result");
        Equal(true, File.Exists(runtime.ManagedNodeExecutablePath), "verified node executable");
        Equal(true, result.Status.IsManaged, "verified node managed status");
    }
    finally
    {
        Directory.Delete(baseDirectory, recursive: true);
    }
}

static void ManagedNodeRuntimeRejectsArchiveTraversal()
{
    var baseDirectory = Directory.CreateTempSubdirectory("rhodes-node-traversal-").FullName;
    try
    {
        using var archiveStream = new MemoryStream();
        using (var archive = new ZipArchive(archiveStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("../outside.txt");
            using var writer = new StreamWriter(entry.Open());
            writer.Write("blocked");
        }

        var bytes = archiveStream.ToArray();
        var expectedHash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        using var installStream = new MemoryStream(bytes);
        var runtime = new RhodesNodeRuntimeManager(baseDirectory);

        var result = runtime.InstallArchiveAsync(installStream, expectedHash).GetAwaiter().GetResult();

        Equal(false, result.Succeeded, "traversal install result");
        Equal(false, File.Exists(Path.Combine(baseDirectory, "outside.txt")), "traversal output absent");
        Equal(false, File.Exists(runtime.ManagedNodeExecutablePath), "traversal node absent");
    }
    finally
    {
        Directory.Delete(baseDirectory, recursive: true);
    }
}

static void RhodesApiStatusParsing()
{
    var health = RhodesApiStatusProbe.ParseHealthJson(
        """
        {
          "ok": true,
          "version": "0.1.0",
          "state": {
            "campaignId": "is5_sarkaz",
            "operators": 3,
            "relics": 2,
            "pendingSuggestions": 1
          },
          "recognition": {
            "active": true,
            "activeProfileId": "operatorsFull"
          }
        }
        """);
    Equal("接続済み", health.State, "health state");
    Equal(true, health.Installed, "health connected flag");
    Equal(true, health.Detail.Contains("version=0.1.0", StringComparison.Ordinal), "health version detail");
    Equal(true, health.Detail.Contains("campaign=is5_sarkaz", StringComparison.Ordinal), "health campaign detail");
    Equal(true, health.Detail.Contains("operators=3", StringComparison.Ordinal), "health operators detail");
    Equal(true, health.Detail.Contains("scan=running", StringComparison.Ordinal), "health scan detail");

    var state = RhodesApiStatusProbe.ParseStateJson(
        """
        {
          "updatedAt": "2026-07-01T00:00:00Z",
          "run": { "campaignId": "is6_sui" },
          "operators": ["gummy", "rain"],
          "relics": ["relic_a"]
        }
        """);
    Equal("接続済み", state.State, "state fallback state");
    Equal(true, state.Detail.Contains("campaign=is6_sui", StringComparison.Ordinal), "state campaign detail");
    Equal(true, state.Detail.Contains("operators=2", StringComparison.Ordinal), "state operators detail");
    Equal(true, state.Detail.Contains("relics=1", StringComparison.Ordinal), "state relics detail");

    var master = RhodesApiStatusProbe.ParseMasterJson(
        """
        {
          "campaigns": [{ "id": "is5_sarkaz" }],
          "operators": [{ "id": "gummy" }, { "id": "rain" }],
          "relics": [{ "id": "relic_a" }]
        }
        """,
        localCampaigns: 1,
        localOperators: 2,
        localRelics: 1);
    Equal("一致", master.State, "master matched state");
    Equal(true, master.Detail.Contains("operators api=2 local=2", StringComparison.Ordinal), "master operator detail");

    var mismatchedMaster = RhodesApiStatusProbe.ParseMasterJson(
        """{ "campaigns": [], "operators": [], "relics": [] }""",
        localCampaigns: 1,
        localOperators: 2,
        localRelics: 1);
    Equal("差分あり", mismatchedMaster.State, "master mismatch state");
}

static void SukiRuntimeProbeWorkflowAggregatesStatuses()
{
    var snapshot = RhodesSukiRuntimeProbeWorkflow.ProbeAsync(
        _ => Task.FromResult(new SukiOptionalRuntimeStatus("RHODES API", "接続済み", "api OK", true, false)),
        _ => Task.FromResult(new SukiOptionalRuntimeStatus("Master Data", "一致", "master OK", true, false)),
        _ => Task.FromResult(new SukiOptionalRuntimeProbeSnapshot(
            new SukiOptionalRuntimeStatus("GLM-OCR", "導入済み", "glm OK", true, false),
            new SukiOptionalRuntimeStatus("Ollama", "未導入", "ollama optional", false, false))),
        _ => Task.FromResult(new SukiHypervisorStatus("確認済み", "hyperv OK", true, false, "info"))).GetAwaiter().GetResult();

    Equal("接続済み", snapshot.Api.State, "runtime workflow api");
    Equal("一致", snapshot.Master.State, "runtime workflow master");
    Equal("導入済み", snapshot.Glm.State, "runtime workflow glm");
    Equal("未導入", snapshot.Ollama.State, "runtime workflow ollama");
    Equal("確認済み", snapshot.Hypervisor.State, "runtime workflow hypervisor");
    Equal(
        "ランタイム状態: API=接続済み, Master=一致, GLM=導入済み, Ollama=未導入, Hyper-V=確認済み",
        snapshot.StatusMessage,
        "runtime workflow message");
}

static void RuntimeCapabilityRegistry()
{
    var capabilities = RhodesRuntimeCapabilityRegistry.Build(new SukiRuntimeCapabilityContext(
        AdbState: "選択済み",
        AdbDetail: "MuMu Player / 127.0.0.1:16384 / MuMu高速撮影",
        MaaFrameworkStatus: new IntegrationStatus("MAAFramework", "参照済み", "runtime OK", true),
        MaaOcrState: "準備済み",
        MaaOcrDetail: "43 task / runStatusFull",
        GlmStatus: new SukiOptionalRuntimeStatus("GLM-OCR", "未導入", "optional", false, false),
        OllamaStatus: new SukiOptionalRuntimeStatus("Ollama", "導入済み", "ready", true, false),
        HypervisorStatus: new SukiHypervisorStatus("確認済み", "platform OK", true, false, "info"))).ToArray();

    Equal("adb|maa|maa-ocr|glm|ollama|hyperv", string.Join("|", capabilities.Select(item => item.Id)), "runtime capability order");
    Equal("CORE|CORE|OCR|OPTIONAL|OPTIONAL|PLATFORM", string.Join("|", capabilities.Select(item => item.Tag)), "runtime capability tags");
    Equal(false, capabilities.Single(item => item.Id == "adb").IsOptional, "adb is required");
    Equal(true, capabilities.Single(item => item.Id == "glm").IsOptional, "missing GLM is optional download");
    Equal(false, capabilities.Single(item => item.Id == "ollama").IsOptional, "installed Ollama does not show as pending optional download");
    Equal("認識", capabilities.Single(item => item.Id == "maa-ocr").PrimaryAction, "maa ocr action");
}

static void WorkspaceRegistry()
{
    var items = RhodesWorkspaceRegistry.Items;

    Equal("run|special|choices|recognition|output|runtime|debug", string.Join("|", items.Select(item => item.Id)), "workspace order");
    Equal("IS特殊値", RhodesWorkspaceRegistry.TitleFor("special"), "special title");
    Equal("ランタイム", RhodesWorkspaceRegistry.TitleFor("runtime"), "runtime title");
    Equal("ラン取得値", RhodesWorkspaceRegistry.TitleFor("unknown"), "unknown title fallback");
    Equal("choices", RhodesWorkspaceRegistry.Normalize("choices"), "known workspace normalize");
    Equal("run", RhodesWorkspaceRegistry.Normalize("bad"), "unknown workspace fallback");
    Equal(true, RhodesWorkspaceRegistry.IsKnown("recognition"), "known workspace");
    Equal(false, RhodesWorkspaceRegistry.IsKnown("legacy"), "unknown workspace");
}

static void PublicDebugWorkspaceRegistry()
{
    var validationItems = RhodesWorkspaceRegistry.ItemsFor(RhodesDistributionProfile.Validation);
    var publicDebugItems = RhodesWorkspaceRegistry.ItemsFor(RhodesDistributionProfile.PublicDebug);

    Equal("run|special|choices|output", string.Join("|", validationItems.Select(item => item.Id)), "validation workspace order");
    Equal("run|special|choices|output", string.Join("|", publicDebugItems.Select(item => item.Id)), "public debug workspace order");
    Equal(false, publicDebugItems.Any(item => item.Id == "recognition"), "public debug hides recognition workspace");
    Equal(false, publicDebugItems.Any(item => item.Id == "debug"), "public debug hides debug workspace");

    var directory = Path.Combine(Path.GetTempPath(), "rhodes-distribution-profile", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(directory);
    try
    {
        Equal("validation", RhodesDistributionProfile.Load(directory).Channel, "missing profile defaults to validation");
        File.WriteAllText(
            Path.Combine(directory, RhodesDistributionProfile.FileName),
            "{\"schemaVersion\":1,\"channel\":\"public-debug\"}");
        Equal("public-debug", RhodesDistributionProfile.Load(directory).Channel, "packaged profile selects public debug");
    }
    finally
    {
        Directory.Delete(directory, true);
    }
}

static void WorkspaceLayoutRegistry()
{
    var layouts = RhodesWorkspaceLayoutRegistry.Items;

    Equal("run|special|choices|recognition|output|runtime|debug", string.Join("|", layouts.Select(item => item.WorkspaceId)), "layout workspace order");
    Equal(0, RhodesWorkspaceLayoutRegistry.Validate().Count, "workspace layout validation");
    Equal("ラン取得値", RhodesWorkspaceLayoutRegistry.For("run").Header.Title, "run header title");
    Equal("values|boss|campaign", string.Join("|", RhodesWorkspaceLayoutRegistry.For("run").Sections.Select(item => item.Id)), "run sections");
    Equal("values|manual|recognition", string.Join("|", RhodesWorkspaceLayoutRegistry.For("special").Sections.Select(item => item.Id)), "special sections");
    Equal("catalog|filters|selection", string.Join("|", RhodesWorkspaceLayoutRegistry.For("choices").Sections.Select(item => item.Id)), "choices sections");
    Equal("parts|preview", string.Join("|", RhodesWorkspaceLayoutRegistry.For("output").Sections.Select(item => item.Id)), "output sections");
    Equal("logs|migration", string.Join("|", RhodesWorkspaceLayoutRegistry.For("debug").Sections.Select(item => item.Id)), "debug sections");
    Equal("connection|detection|diagnostics|optional", string.Join("|", RhodesWorkspaceLayoutRegistry.Runtime.Sections.Select(item => item.Id)), "runtime sections");
    Equal("profile|execution|review|evidence", string.Join("|", RhodesWorkspaceLayoutRegistry.Recognition.Sections.Select(item => item.Id)), "recognition sections");
}

static void WorkspaceActionRegistry()
{
    var actions = RhodesWorkspaceActionRegistry.Items;

    Equal(0, RhodesWorkspaceActionRegistry.Validate().Count, "workspace action validation");
    var profileCommand = RhodesWorkspaceActionRegistry.ParseCommandName("RunProfileRecognitionAndApplyCommand(runStatusFull)");
    Equal("RunProfileRecognitionAndApplyCommand", profileCommand.CommandName, "workspace action command parser name");
    Equal("runStatusFull", profileCommand.CommandParameter, "workspace action command parser parameter");
    Equal(null, RhodesWorkspaceActionRegistry.ParseCommandName("ConnectAndCaptureCommand").CommandParameter, "workspace action command parser no parameter");
    Equal(
        "run|special|choices|recognition|output|runtime|debug",
        string.Join("|", actions.Select(item => item.WorkspaceId).Distinct()),
        "action workspace coverage");
    Equal(
        "runtime.auto-detect|runtime.connect-capture|runtime.probe-status|runtime.save-settings",
        string.Join("|", RhodesWorkspaceActionRegistry.ForWorkspace("runtime").Select(item => item.Id)),
        "runtime actions");
    Equal(
        true,
        RhodesWorkspaceActionRegistry.ForWorkspace("runtime").Single(item => item.Id == "runtime.connect-capture").RequiresMaaSession,
        "connect and capture uses maa");
    Equal(
        "MAA",
        RhodesWorkspaceActionRegistry.ForWorkspace("runtime").Single(item => item.Id == "runtime.connect-capture").MaaRequirementLabel,
        "connect and capture maa label");
    var runAndApply = RhodesWorkspaceActionRegistry.ForWorkspace("recognition").Single(item => item.Id == "recognition.run-and-apply");
    Equal(true, runAndApply.RequiresMaaSession, "run and apply uses maa");
    Equal(true, runAndApply.WritesState, "run and apply writes state");
    Equal("state更新", runAndApply.StateWriteLabel, "run and apply state label");
    Equal("maa-resource.tasker+state-api.apply", runAndApply.Workflow, "run and apply workflow");
    var preview = new SukiWorkspaceActionPreview(runAndApply, new AsyncRelayCommand(() => Task.CompletedTask), null);
    Equal("認識して反映", preview.Label, "workspace action preview label");
    Equal("実行", preview.ActionButtonLabel, "workspace action preview executable label");
    Equal("未接続", new SukiWorkspaceActionPreview(runAndApply, null, null).ActionButtonLabel, "workspace action preview missing command label");
    Equal(
        false,
        actions.Any(item => item.CommandName.Contains("RunSelectedProfileAdbScanCommand", StringComparison.Ordinal)),
        "retired adb scan command is not exposed");
}

static void ProductSurfaceRegistry()
{
    var items = RhodesProductSurfaceRegistry.Items;

    Equal(0, RhodesProductSurfaceRegistry.Validate().Count, "surface registry validation");
    Equal(
        "run|special|choices|recognition|output|runtime|debug",
        string.Join("|", items.Select(item => item.WorkspaceId).Distinct()),
        "surface workspace coverage");
    Equal(
        "run.base",
        string.Join("|", RhodesProductSurfaceRegistry.ForWorkspace("run").Select(item => item.Id)),
        "run surfaces");
    Equal(
        "run.special",
        string.Join("|", RhodesProductSurfaceRegistry.ForWorkspace("special").Select(item => item.Id)),
        "special surfaces");
    Equal(
        "choices.operators|choices.relics",
        string.Join("|", RhodesProductSurfaceRegistry.ForWorkspace("choices").Select(item => item.Id)),
        "choice surfaces");
    Equal(
        "recognition.profiles|recognition.candidates|recognition.evidence|recognition.roi-adjustment",
        string.Join("|", RhodesProductSurfaceRegistry.ForWorkspace("recognition").Select(item => item.Id)),
        "recognition surfaces");
    Equal(true, items.Single(item => item.Id == "runtime.adb").Provenance.Contains("MAAFramework", StringComparison.Ordinal), "adb provenance");
    Equal(true, items.Single(item => item.Id == "runtime.maa-framework").Provenance.Contains("Maa.Framework", StringComparison.Ordinal), "maa framework provenance");
    Equal(true, items.Single(item => item.Id == "runtime.maa-ocr").Provenance.Contains("MAAFramework", StringComparison.Ordinal), "maa ocr provenance");
    Equal("optional", items.Single(item => item.Id == "runtime.glm-ocr").ReviewPolicy, "glm review policy");
    Equal("optional", items.Single(item => item.Id == "runtime.ollama").ReviewPolicy, "ollama review policy");
    Equal("platform-diagnostic", items.Single(item => item.Id == "runtime.hyper-v").ReviewPolicy, "hyper-v review policy");
    Equal(
        "runtime.adb|runtime.maa-framework|runtime.maa-ocr|runtime.glm-ocr|runtime.ollama|runtime.hyper-v",
        string.Join("|", RhodesProductSurfaceRegistry.ForWorkspace("runtime").Select(item => item.Id)),
        "runtime surfaces");
    Equal(
        "debug.evidence|debug.logs|debug.roi-sessions",
        string.Join("|", RhodesProductSurfaceRegistry.ForWorkspace("debug").Select(item => item.Id)),
        "debug surfaces");
    Equal(true, items.Single(item => item.Id == "output.obs-parts").CanShowOnOutput, "output surface");
    Equal(false, items.Single(item => item.Id == "recognition.roi-adjustment").CanShowOnOutput, "roi adjustment is debugger only");
}

static void OutputPartRegistry()
{
    var descriptors = RhodesOutputPartRegistry.Descriptors;
    var previews = RhodesOutputPartRegistry.BuildDefaultPreviews();

    Equal(
        "operators|relics|run|special|recognition",
        string.Join("|", descriptors.Select(item => item.Id)),
        "output part order");
    Equal(
        "choices.operators|choices.relics|run.base|run.special|recognition.candidates",
        string.Join("|", descriptors.Select(item => item.BindingPath)),
        "output part binding paths");
    Equal(0, RhodesOutputPartRegistry.Validate().Count, "output part validation");
    Equal("招集オペレーター", previews[0].Label, "operator output label");
    Equal(true, previews[0].Enabled, "operator output enabled");
    Equal(true, previews[1].ScrollEnabled, "relic output scroll enabled");
    Equal(260, previews.Single(item => item.Id == "run").Width, "run output width");
    Equal(false, previews.Single(item => item.Id == "recognition").Enabled, "recognition output disabled by default");
}

static void OverlayLayoutCatalog()
{
    var defaults = RhodesOverlayLayoutCatalog.BuildDefaultStates();
    Equal("status|relics|operators|effects|bosses|special", string.Join("|", defaults.Select(item => item.Id)), "layout part order");
    Equal(0, RhodesOverlayLayoutCatalog.Validate(defaults).Count, "default layout validation");

    var normalized = RhodesOverlayLayoutCatalog.Normalize(
    [
        new SukiOverlayLayoutState("status", false, -40, 2000, 4000, 10, 99),
        new SukiOverlayLayoutState("unknown", true, 10, 10, 100, 100, 1),
    ]);
    Equal(6, normalized.Count, "normalized layout count");
    var status = normalized.Single(item => item.Id == "status");
    Equal(false, status.Enabled, "status enabled");
    Equal(0, status.X, "status x clamped");
    Equal(1000, status.Y, "status y clamped");
    Equal(1920, status.Width, "status width clamped");
    Equal(80, status.Height, "status height clamped");
    Equal(6, status.ZIndex, "status z index clamped");
}

static void RuntimeWorkspaceRegistry()
{
    var layout = RhodesRuntimeWorkspaceRegistry.Layout;

    Equal("ランタイム", layout.Header.Title, "runtime header title");
    Equal("connection|detection|diagnostics|optional", string.Join("|", layout.Sections.Select(item => item.Id)), "runtime section order");
    Equal("接続設定", layout.Connection.Title, "connection title");
    Equal("検出結果", layout.Detection.Title, "detection title");
    Equal("診断", layout.Diagnostics.Title, "diagnostics title");
    Equal("任意OCR", layout.OptionalRuntime.Title, "optional title");
    Equal(true, layout.Connection.Detail.Contains("ADB", StringComparison.Ordinal), "connection explains ADB");
    Equal(true, layout.Diagnostics.Detail.Contains("MAA", StringComparison.Ordinal), "diagnostics explains MAA");
}

static void RecognitionWorkspaceRegistry()
{
    var layout = RhodesRecognitionWorkspaceRegistry.Layout;

    Equal("認識ワークフロー", layout.Header.Title, "recognition header title");
    Equal("profile|execution|review|evidence", string.Join("|", layout.Sections.Select(item => item.Id)), "recognition section order");
    Equal("プロファイル選択", layout.Profile.Title, "profile title");
    Equal("実行", layout.Execution.Title, "execution title");
    Equal("候補確認", layout.Review.Title, "review title");
    Equal("検証情報", layout.Evidence.Title, "evidence title");
    Equal(true, layout.Header.Detail.Contains("MAA Resource task", StringComparison.Ordinal), "header explains MAA Resource task");
    Equal(true, layout.Profile.Detail.Contains("1280x720", StringComparison.Ordinal), "profile explains base resolution");
    Equal(true, layout.Execution.Detail.Contains("候補化API", StringComparison.Ordinal), "execution explains candidate API");
}

static void OcrEngineCatalog()
{
    Equal("maa-ocr|glm-ocr", string.Join("|", SukiOcrEngineCatalog.Options.Select(option => option.Id)), "ocr options");
    Equal("maa-ocr", SukiOcrEngineCatalog.Normalize(""), "blank ocr engine");
    Equal("maa-ocr", SukiOcrEngineCatalog.Normalize("profile"), "legacy profile ocr engine");
    Equal("maa-ocr", SukiOcrEngineCatalog.Normalize("paddle"), "legacy paddle ocr engine");
    Equal("glm-ocr", SukiOcrEngineCatalog.Normalize("windows-glm"), "glm alias");
}

static void HypervisorStatusParsing()
{
    var ready = RhodesHypervisorProbe.ParseStatusJson(
        """
        {
          "platform": "win32",
          "supported": true,
          "available": true,
          "requiresBiosChange": false,
          "severity": "ok",
          "message": "Hyper-V/Windows Hypervisorは有効です。"
        }
        """);
    Equal("有効", ready.State, "ready state");
    Equal(true, ready.Available, "ready available");
    Equal("ok", ready.Severity, "ready severity");

    var bios = RhodesHypervisorProbe.ParseStatusJson(
        """
        {
          "platform": "win32",
          "supported": true,
          "available": false,
          "requiresBiosChange": true,
          "severity": "error",
          "message": "BIOS/UEFIでCPU仮想化支援を有効化してください。"
        }
        """);
    Equal("BIOS要確認", bios.State, "bios state");
    Equal(true, bios.RequiresBiosChange, "bios flag");
    Equal(true, bios.Detail.Contains("BIOS", StringComparison.Ordinal), "bios guidance detail");

    var windowsFeature = RhodesHypervisorProbe.ParseStatusJson(
        """
        {
          "platform": "win32",
          "supported": true,
          "available": false,
          "requiresBiosChange": false,
          "severity": "warning",
          "message": "Windowsの機能でHyper-Vを有効化してください。"
        }
        """);
    Equal("Windows機能要確認", windowsFeature.State, "windows feature state");
    Equal(false, windowsFeature.RequiresBiosChange, "windows feature bios flag");
    Equal(true, windowsFeature.Detail.Contains("Hyper-V", StringComparison.Ordinal), "windows feature detail");
}

static void MaaFrameworkRuntimeDiagnostics()
{
    var tempBase = Path.Combine(Path.GetTempPath(), "rhodes-maa-runtime-probe", Guid.NewGuid().ToString("N"));
    try
    {
        Directory.CreateDirectory(tempBase);
        var facts = MaaFrameworkRuntimeProbe.BuildFacts(tempBase);
        Equal(true, facts.NativeRuntimeDirectory.Contains(Path.Combine("runtimes", "win-x64", "native"), StringComparison.OrdinalIgnoreCase), "facts native directory");
        Equal(true, facts.MissingNativeFiles.Count > 0, "facts missing native files");

        var status = MaaFrameworkRuntimeProbe.ProbeAppBaseDirectory(tempBase);
        Equal("ネイティブ未配置", status.State, "base directory missing native state");
        Equal(false, status.IsReady, "base directory missing native ready");
    }
    finally
    {
        if (Directory.Exists(tempBase))
            Directory.Delete(tempBase, true);
    }

    var missingNative = MaaFrameworkRuntimeProbe.BuildStatus(new MaaFrameworkRuntimeProbeFacts(
        "MaaFramework.Binding",
        "5.8.0.0",
        "win-x64",
        @"C:\app\runtimes\win-x64\native",
        ["MaaFramework.dll", "opencv_world4_maa.dll"],
        true,
        []));
    Equal("MAAFramework", missingNative.Name, "native status name");
    Equal("ネイティブ未配置", missingNative.State, "native missing state");
    Equal(false, missingNative.IsReady, "native missing ready");
    Equal(true, missingNative.Detail.Contains("MaaFramework.dll", StringComparison.Ordinal), "native missing detail");
    Equal(true, missingNative.Detail.Contains(@"C:\app\runtimes\win-x64\native", StringComparison.Ordinal), "native path detail");

    var missingVc = MaaFrameworkRuntimeProbe.BuildStatus(new MaaFrameworkRuntimeProbeFacts(
        "MaaFramework.Binding",
        "5.8.0.0",
        "win-x64",
        @"C:\app\runtimes\win-x64\native",
        [],
        true,
        ["vcruntime140_1.dll"]));
    Equal("VC++不足", missingVc.State, "vc missing state");
    Equal(false, missingVc.IsReady, "vc missing ready");
    Equal(true, missingVc.Detail.Contains("Visual C++ 2015-2022", StringComparison.Ordinal), "vc guidance detail");
    Equal(true, missingVc.Detail.Contains("vcruntime140_1.dll", StringComparison.Ordinal), "vc missing detail");

    var ok = MaaFrameworkRuntimeProbe.BuildStatus(new MaaFrameworkRuntimeProbeFacts(
        "MaaFramework.Binding",
        "5.8.0.0",
        "win-x64",
        @"C:\app\runtimes\win-x64\native",
        [],
        true,
        []));
    Equal("参照済み", ok.State, "ok state");
    Equal(true, ok.IsReady, "ok ready");
    Equal(true, ok.Detail.Contains("VC++ runtime OK", StringComparison.Ordinal), "ok vc detail");
}

static void MaaRecognitionResourcePathDiagnostics()
{
    var directory = Path.Combine(Path.GetTempPath(), $"rhodes-maa-resource-{Guid.NewGuid():N}");
    try
    {
        var missing = RhodesMaaPaths.MissingRecognitionResourceFiles(directory);
        Equal(true, missing.Contains("model/ocr/det.onnx"), "missing detector model");
        Equal(true, missing.Contains("model/ocr/rec.onnx"), "missing jp rec model");
        Equal(true, missing.Contains("model/ocr/keys.txt"), "missing jp keys");
        Equal(true, RhodesMaaPaths.RecognitionResourceStatusDetail(directory).Contains("missing=", StringComparison.Ordinal), "missing status detail");

        foreach (var relative in missing)
        {
            var path = Path.Combine(directory, relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "ok");
        }

        Equal(0, RhodesMaaPaths.MissingRecognitionResourceFiles(directory).Count, "all resource files present");
        Equal(true, RhodesMaaPaths.RecognitionResourceStatusDetail(directory).Contains("asset OK", StringComparison.Ordinal), "ready status detail");
    }
    finally
    {
        if (Directory.Exists(directory))
            Directory.Delete(directory, true);
    }
}

static void MaaOfflineSessionInitializesWithoutAdb()
{
    using var session = new RhodesMaaSession();
    var options = RhodesMaaSession.DefaultAdbOptions();
    var snapshot = session.InitializeOfflineAsync(options).GetAwaiter().GetResult();

    Equal(true, snapshot.IsReady, $"offline session ready: {snapshot.Detail}");
    Equal(true, session.IsTaskerReady, "offline tasker ready");
    Equal(true, session.IsControllerReady, "offline controller connected");
    Equal(true, session.Tasker?.IsInitialized, "offline tasker initialized");
    Equal(true, session.Tasker?.Resource?.IsLoaded, "offline recognition resource loaded before returning");

    var templatePath = Path.Combine(
        AppContext.BaseDirectory,
        "resource",
        "base",
        "image",
        "run",
        "IngotIcon.png");
    using var template = SKBitmap.Decode(templatePath);
    Equal(true, template is not null, "packaged template decodes");
    using var templateFrame = new SKBitmap(1280, 720);
    templateFrame.Erase(SKColors.Black);
    using (var canvas = new SKCanvas(templateFrame))
        canvas.DrawBitmap(template!, 200, 100);
    var templateRecognition = session.RunResourceRecognitionAsync(
        "offline-template-smoke",
        """{"recognition":"TemplateMatch","roi":[0,0,1280,720],"template":"run/IngotIcon.png","threshold":0.8,"method":5}""",
        EncodePng(templateFrame)).GetAwaiter().GetResult();
    Equal(true, templateRecognition.Succeeded && templateRecognition.Hit, $"packaged TemplateMatch hit: {templateRecognition.Detail}");
    Equal(true, session.Tasker?.IsInitialized, "offline tasker remains initialized after direct recognition");

    using var ocrFrame = new SKBitmap(120, 60);
    ocrFrame.Erase(SKColors.Black);
    using var font = new SKFont(SKTypeface.Default, 48);
    using (var canvas = new SKCanvas(ocrFrame))
    using (var paint = new SKPaint { Color = SKColors.White, IsAntialias = true })
        canvas.DrawText("18", 4, 48, SKTextAlign.Left, font, paint);
    var ocrRecognition = session.RunResourceRecognitionAsync(
        "offline-ocr-smoke",
        """{"recognition":"OCR","roi":[0,0,120,60],"only_rec":true,"threshold":0.1}""",
        EncodePng(ocrFrame),
        scaleOverride: 4).GetAwaiter().GetResult();
    var ocrRows = RhodesMaaOcrDetailRows.FromTaskResults([ocrRecognition]);
    Equal(true, ocrRecognition.Succeeded && ocrRecognition.Hit, $"packaged MAA-OCR hit: {ocrRecognition.Detail}");
    Equal(true, ocrRows.Any(row => row.Text == "18"), $"packaged MAA-OCR reads 18: {ocrRecognition.RecognitionDetailJson}");

    var amiyaRoleTemplatePath = Path.Combine(
        AppContext.BaseDirectory,
        "resource",
        "base",
        "image",
        "run",
        "AmiyaRoleMedic.png");
    using var amiyaRoleTemplate = SKBitmap.Decode(amiyaRoleTemplatePath);
    Equal(true, amiyaRoleTemplate is not null, "packaged Amiya medic role template decodes");
    using var amiyaFrame = new SKBitmap(1280, 720);
    amiyaFrame.Erase(SKColors.Black);
    using (var canvas = new SKCanvas(amiyaFrame))
        canvas.DrawBitmap(amiyaRoleTemplate!, 438, 229);
    var amiyaNameResult = new MaaTaskRunResult(
        "operator.card.name.0",
        "Succeeded",
        true,
        "detail",
        """{"filtered_results":[{"text":"アーミヤ","score":0.99}]}""",
        "OCR",
        true);
    var amiyaRoleRequest = RhodesMaaAmiyaRoleResolver.BuildRequest(
        new MaaDynamicOcrRequest("operator.card.name.0", 661, 309, 180, 23, 1, 0.99),
        amiyaNameResult);
    var amiyaRoleRecognition = session.RunResourceRecognitionAsync(
        amiyaRoleRequest!.Entry,
        amiyaRoleRequest.PayloadJson,
        EncodePng(amiyaFrame)).GetAwaiter().GetResult();
    Equal(
        true,
        amiyaRoleRecognition.Succeeded && amiyaRoleRecognition.Hit,
        $"packaged Amiya role templates hit through MAA Or recognition: {amiyaRoleRecognition.Detail}");
    Equal(
        "amiya3",
        RhodesMaaAmiyaRoleResolver.ResolveOperatorId([amiyaNameResult, amiyaRoleRecognition], 0),
        $"MAA Or recognition resolves medic Amiya: {amiyaRoleRecognition.RecognitionDetailJson}");
}

static void RecognitionNavigationLoadsProfileSteps()
{
    var path = Path.Combine(AppContext.BaseDirectory, "data", "recognition", "scan-profiles.json");
    var plan = RhodesRecognitionNavigation.LoadFromJson(File.ReadAllText(path), "runStatusFull");

    Equal(2, plan.OpenSteps.Count, "run status open step count");
    Equal(2, plan.RestoreSteps.Count, "run status restore step count");
    Equal("tap", plan.OpenSteps[0].Type, "first open step type");
    Equal(30, plan.OpenSteps[0].X, "open tap area x");
    Equal(650, plan.OpenSteps[0].Y, "open tap area y");
    Equal(54, plan.OpenSteps[0].Width, "open tap area width");
    Equal(62, plan.OpenSteps[0].Height, "open tap area height");
    Equal(false, plan.OpenSteps.Concat(plan.RestoreSteps).Any(step => step.Type == "back"), "back action prohibited");

    var activeCoins = RhodesRecognitionNavigation.LoadFromJson(File.ReadAllText(path), "is6ActiveCoinsFull");
    Equal(2, activeCoins.OpenSteps.Count, "active coin panel open steps");
    Equal(2, activeCoins.RestoreSteps.Count, "active coin panel restore steps");
    Equal("tap", activeCoins.OpenSteps[0].Type, "active coin panel opens by tap");
    Equal(515, activeCoins.OpenSteps[0].X, "active coin tap area x");
    Equal(650, activeCoins.OpenSteps[0].Y, "active coin tap area y");
    Equal(54, activeCoins.OpenSteps[0].Width, "active coin tap area targets only the first active slot");
    Equal(false, activeCoins.OpenSteps.Concat(activeCoins.RestoreSteps).Any(step => step.Type == "back"), "active coin panel never uses Android back");

    var lightAndHorde = RhodesRecognitionNavigation.LoadFromJson(File.ReadAllText(path), "is3LightHordeFull");
    Equal(610, lightAndHorde.OpenSteps[0].X, "Mizuki light panel tap starts inside the title button");
    Equal(26, lightAndHorde.OpenSteps[0].Y, "Mizuki light panel tap excludes the top window edge");
    Equal(100, lightAndHorde.OpenSteps[0].Width, "Mizuki light panel tap stays inside the title button width");
    Equal(30, lightAndHorde.OpenSteps[0].Height, "Mizuki light panel tap stays inside the title button height");
    Equal(
        true,
        Enumerable.Range(0, 64)
            .Select(index => RhodesRecognitionNavigation.RandomTapPoint(lightAndHorde.OpenSteps[0], new Random(index + 3373)))
            .All(point => point.X is >= 610 and < 710 && point.Y is >= 26 and < 56),
        "Mizuki light panel randomized taps remain on the title button");
}

static void RecognitionNavigationRandomizesTapAreas()
{
    var step = new RhodesRecognitionNavigationStep("tap", "test", 12, 634, 84, 86, 0);
    var random = new Random(3373);
    var points = Enumerable.Range(0, 24)
        .Select(_ => RhodesRecognitionNavigation.RandomTapPoint(step, random))
        .ToArray();

    Equal(true, points.All(point => point.X >= 12 && point.X < 96), "random x inside area");
    Equal(true, points.All(point => point.Y >= 634 && point.Y < 720), "random y inside area");
    Equal(true, points.Distinct().Count() > 1, "tap points vary");
}

static void RecognitionScrollPlanLoadsOperatorPasses()
{
    var path = Path.Combine(AppContext.BaseDirectory, "data", "recognition", "scan-profiles.json");
    var passes = RhodesRecognitionScrollPlan.LoadFromJson(File.ReadAllText(path), "operatorsFull");

    Equal(2, passes.Count, "operator scroll pass count");
    Equal("right", passes[0].Direction, "first direction");
    Equal(30, passes[0].MaxScrolls, "max scrolls");
    Equal(1, passes[0].MinScrolls, "adaptive minimum scrolls");
    Equal(3, passes[0].CandidateStableEndCount, "candidate stable end count");
    Equal(false, passes[1].MirrorPreviousPassScrolls, "operator reverse is independent");

    var random = new Random(3373);
    var points = Enumerable.Range(0, 20)
        .Select(_ => RhodesRecognitionScrollPlan.RandomSwipe(passes[0], random))
        .ToArray();
    Equal(true, points.All(point => point.StartX >= 1040 && point.StartX < 1170), "start x inside area");
    Equal(true, points.All(point => point.StartY >= 170 && point.StartY < 530), "start y inside area");
    Equal(true, points.All(point => point.EndX >= 760 && point.EndX < 920), "end x inside area");
    Equal(true, points.All(point => point.EndY >= 170 && point.EndY < 530), "end y inside area");
    Equal(true, points.Distinct().Count() > 1, "swipe points vary");

    var thoughtPasses = RhodesRecognitionScrollPlan.LoadFromJson(File.ReadAllText(path), "is5ThoughtFull");
    Equal(false, thoughtPasses[1].CollectCandidates, "thought restore pass does not collect");
    Equal(true, thoughtPasses[1].MirrorPreviousPassScrolls, "thought restore mirrors forward pass");
    Equal(12, thoughtPasses[0].MaxScrolls, "thought scan has enough bounded small steps to reach the list end");
    var thoughtSwipes = Enumerable.Range(0, 50)
        .Select(_ => RhodesRecognitionScrollPlan.RandomSwipe(thoughtPasses[0], random))
        .ToArray();
    Equal(
        true,
        thoughtSwipes.All(point => point.StartY > point.EndY && point.StartY - point.EndY < 120),
        "thought scan randomizes within a sub-row downward distance");

    var relicPasses = RhodesRecognitionScrollPlan.LoadFromJson(File.ReadAllText(path), "relicsFull");
    Equal(2, relicPasses[0].CandidateStableEndCount, "relic pass also stops when candidates stabilize");
    Equal(true, relicPasses.All(pass => pass.StartArea.X >= 520), "relic swipes avoid Android edge gesture area");

    var coinPasses = RhodesRecognitionScrollPlan.LoadFromJson(File.ReadAllText(path), "is6CoinsFull");
    Equal(2, coinPasses.Count, "held coin scan covers both horizontal directions");
    Equal("right", coinPasses[0].Direction, "held coin first direction");
    Equal("left", coinPasses[1].Direction, "held coin reverse direction");
    Equal(true, coinPasses.All(pass => pass.CollectCandidates), "both held coin passes collect visible names");
    Equal(false, coinPasses[1].MirrorPreviousPassScrolls, "held coin reverse reaches the opposite edge independently");
}

static void RecognitionRuntimePlanUsesFocusedTasks()
{
    var operatorTasks = new[]
    {
        new MaaResourceTaskPreview("RhodesOcrRegion_operator_name_left_1", "legacy", ""),
        new MaaResourceTaskPreview("RhodesTemplate_operatorsFull_operator_card_name", "template", ""),
    };
    var operatorPlan = new MaaResourceExecutionPlan(
        "operatorsFull", "operators", "test", operatorTasks.Select(task => task.Entry).ToArray(), operatorTasks, "");
    var focusedOperator = RhodesRecognitionRuntimePlan.PrepareInitial(operatorPlan);
    Equal("RhodesTemplate_operatorsFull_operator_card_name", string.Join("|", focusedOperator.TaskEntries), "operator runtime task");
    Equal(
        false,
        RhodesRecognitionRuntimePlan.IsTargetScreenConfirmed("operatorsFull", []),
        "operator scan rejects a screen without card anchors");
    Equal(
        false,
        RhodesRecognitionRuntimePlan.IsTargetScreenConfirmed(
            "operatorsFull",
            [new MaaTaskRunResult("RhodesOcrRegion_operator_name_left_1", "Succeeded", true, "", "{}", "OCR", true)]),
        "legacy OCR does not confirm the operator screen");
    Equal(
        true,
        RhodesRecognitionRuntimePlan.IsTargetScreenConfirmed(
            "operatorsFull",
            [new MaaTaskRunResult("RhodesTemplate_operatorsFull_operator_card_name", "Succeeded", true, "", "{}", "TemplateMatch", true)]),
        "operator card anchor confirms the operator screen");
    Equal(true, RhodesRecognitionRuntimePlan.IsTargetScreenConfirmed("runStatusFull", []), "run status keeps independent top and footer values without squad panel");
    Equal(
        true,
        RhodesRecognitionRuntimePlan.IsTargetScreenConfirmed(
            "runStatusFull",
            [new MaaTaskRunResult("RhodesScreen_run_squad_info_panel", "Succeeded", true, "", "{}", "OCR", true)]),
        "squad panel confirms run status screen");
    Equal(false, RhodesRecognitionRuntimePlan.IsTargetScreenConfirmed("is5AgeFull", []), "age scan requires opened age detail");
    Equal(false, RhodesRecognitionRuntimePlan.IsTargetScreenConfirmed("relicsFull", []), "relic scan requires opened relic list");
    Equal(false, RhodesRecognitionRuntimePlan.IsTargetScreenConfirmed("is6CoinsFull", []), "held coin scan requires opened coin list");
    Equal(
        true,
        RhodesRecognitionRuntimePlan.IsTargetScreenConfirmed(
            "is6CoinsFull",
            [new MaaTaskRunResult(
                "RhodesOcrRegion_is6_coin_list_text",
                "Succeeded",
                true,
                "",
                "{\"filtered_results\":[{\"text\":\"北の刺面\",\"score\":0.94}]}",
                "OCR",
                true)]),
        "coin name OCR confirms the opened held coin list");
    Equal(
        false,
        RhodesRecognitionRuntimePlan.IsTargetScreenConfirmed(
            "is6CoinsFull",
            [new MaaTaskRunResult(
                RhodesSuiCoinImageRecognizer.OwnedEntry,
                "Succeeded",
                true,
                "ownedCoins=1",
                "{\"fieldId\":\"coins\",\"detections\":[{\"coinId\":\"is6_sui_selectable_coin_is6_copper_b01\",\"label\":\"大炎通宝\",\"score\":0.91,\"slotIndex\":0,\"roi\":[0,0,106,106],\"statusId\":\"\"}]}",
                "ImageClassification",
                true)]),
        "coin image classification does not confirm the held coin list");
    Equal(
        true,
        RhodesRecognitionRuntimePlan.IsTargetScreenConfirmed(
            "relicsFull",
            [new MaaTaskRunResult("RhodesScreen_relic_list", "Succeeded", true, "", "{}", "OCR", true)]),
        "relic close button confirms opened relic list");
    Equal(
        true,
        RhodesRecognitionRuntimePlan.IsTargetScreenConfirmed(
            "relicsFull",
            [new MaaTaskRunResult(
                "RhodesOcrRegion_relic_list_text",
                "Succeeded",
                true,
                "",
                "{\"filtered\":[{\"text\":\"支援補給所\",\"score\":0.99}]}",
                "OCR",
                true)]),
        "relic name OCR confirms the list when close-button OCR drifts");
    Equal(
        true,
        RhodesRecognitionRuntimePlan.IsTargetScreenConfirmed(
            "relicsFull",
            [new MaaTaskRunResult(
                "RhodesOcrRegion_relic_detail_name",
                "Succeeded",
                true,
                "",
                "{\"filtered\":[{\"text\":\"意欲の天秤\",\"score\":0.93}]}",
                "OCR",
                true)]),
        "single relic detail name confirms the opened relic screen");
    Equal(
        false,
        RhodesRecognitionRuntimePlan.IsTargetScreenConfirmed(
            "relicsFull",
            [new MaaTaskRunResult(
                "RhodesOcrRegion_relic_list_text",
                "Succeeded",
                true,
                "",
                "{\"filtered\":[{\"text\":\"思わぬ遭遇\",\"score\":0.99}]}",
                "OCR",
                true)]),
        "unrelated map OCR does not confirm the relic list");
    Equal(
        false,
        RhodesRecognitionRuntimePlan.HasReachedScrollEnd(3, 1, 0, 2, 3, 3),
        "candidate stability alone does not stop a moving variable-length list");
    Equal(
        true,
        RhodesRecognitionRuntimePlan.HasReachedScrollEnd(3, 1, 2, 2, 1, 3),
        "stable visual frame stops at the list edge");
    Equal(
        true,
        RhodesRecognitionRuntimePlan.HasReachedScrollEnd(4, 1, 1, 2, 3, 3),
        "candidate stability may stop only after the frame also stops moving");

    var relicTasks = new[]
    {
        new MaaResourceTaskPreview("RhodesRelicButton", "button", ""),
        new MaaResourceTaskPreview("RhodesScreen_run_map_footer_relic", "count", ""),
        new MaaResourceTaskPreview("RhodesScreen_relic_list", "screen", ""),
        new MaaResourceTaskPreview("RhodesOcrRegion_relic_list_text", "list", ""),
        new MaaResourceTaskPreview("RhodesOcrRegion_relic_detail_name", "detail", ""),
    };
    var relicPlan = new MaaResourceExecutionPlan(
        "relicsFull", "relics", "test", relicTasks.Select(task => task.Entry).ToArray(), relicTasks, "");
    var relicPreNavigation = RhodesRecognitionRuntimePlan.PreparePreNavigation(relicPlan);
    Equal("RhodesScreen_run_map_footer_relic", string.Join("|", relicPreNavigation.TaskEntries), "relic count runs before opening the list");
    var focusedRelic = RhodesRecognitionRuntimePlan.PrepareInitial(relicPlan);
    Equal("RhodesScreen_relic_list|RhodesOcrRegion_relic_list_text|RhodesOcrRegion_relic_detail_name", string.Join("|", focusedRelic.TaskEntries), "relic list tasks exclude the map footer count");
    Equal(false, RhodesRecognitionRuntimePlan.ShouldSkipScroll("relicsFull", 9), "candidate count alone never skips relic scroll");
    Equal(true, RhodesRecognitionRuntimePlan.ShouldSkipScroll("relicsFull", 9, 9), "matching owned count skips relic scroll");
    Equal(false, RhodesRecognitionRuntimePlan.ShouldSkipScroll("relicsFull", 8, 9), "missing relic continues scroll");
    Equal(false, RhodesRecognitionRuntimePlan.ShouldSkipScroll("relicsFull", 10, 9), "excess false positive does not finish early");
    Equal(true, RhodesRecognitionRuntimePlan.IsKnownNonScrollableRelicList("relicsFull", 9, "is5_sarkaz"), "non-Phantom relics fit without scrolling through three rows");
    Equal(false, RhodesRecognitionRuntimePlan.IsKnownNonScrollableRelicList("relicsFull", 10, "is5_sarkaz"), "fourth relic row remains scrollable");
    Equal(false, RhodesRecognitionRuntimePlan.IsKnownNonScrollableRelicList("relicsFull", 6, "is2_phantom"), "Phantom keeps its existing relic scroll behavior");
    Equal(true, RhodesRecognitionRuntimePlan.ShouldRetryRelicFrameWithoutScroll("relicsFull", 8, 9, "is5_sarkaz"), "incomplete visible relic list retries without a swipe");
    Equal(false, RhodesRecognitionRuntimePlan.ShouldRetryRelicFrameWithoutScroll("relicsFull", 9, 9, "is5_sarkaz"), "complete visible relic list does not retry");
    Equal(false, RhodesRecognitionRuntimePlan.ShouldRetryRelicFrameWithoutScroll("relicsFull", 5, 6, "is2_phantom"), "Phantom does not use the stationary relic retry");
    Equal(false, RhodesRecognitionRuntimePlan.ShouldRetryRelicFrameWithoutScroll("relicsFull", 5, null, "is5_sarkaz"), "unknown relic total does not assume the list is complete");
    Equal(true, RhodesRecognitionRuntimePlan.ShouldEndRelicPassAfterImmobileProbe("relicsFull", "is5_sarkaz", 1, 2), "first immobile relic probe ends the current direction");
    Equal(false, RhodesRecognitionRuntimePlan.ShouldEndRelicPassAfterImmobileProbe("relicsFull", "is5_sarkaz", 1, 3), "moving relic frame continues scanning");
    Equal(false, RhodesRecognitionRuntimePlan.ShouldEndRelicPassAfterImmobileProbe("relicsFull", "is2_phantom", 1, 0), "Phantom keeps its existing edge detection");
    Equal(false, RhodesRecognitionRuntimePlan.ShouldEndRelicPassAfterImmobileProbe("operatorsFull", "is5_sarkaz", 1, 0), "operator scans are unchanged");
    Equal(true, RhodesRecognitionRuntimePlan.HasReachedExpectedCandidateCount("relicsFull", 9, 9), "matching owned count completes scan");
    Equal(false, RhodesRecognitionRuntimePlan.HasReachedExpectedCandidateCount("relicsFull", 8, 9), "incomplete count remains active");
    Equal(true, RhodesRecognitionRuntimePlan.IsScrollProfile("is5ThoughtFull"), "thought list uses scroll recognition");
    Equal(true, RhodesRecognitionRuntimePlan.IsScrollProfile("is6CoinsFull"), "owned coin board uses horizontal scroll recognition");
}

static void RelicOwnedCountReaderExtractsFooterCount()
{
    var evidence = RhodesRelicOwnedCountReader.FromTaskResults(
        [
            new MaaTaskRunResult(
                RhodesRelicOwnedCountReader.Entry,
                "Succeeded",
                true,
                "detail",
                """
                {
                  "all": [
                    { "text": "13", "score": 0.92, "box": [12, 3, 24, 35] },
                    { "text": "秘宝", "score": 0.98, "box": [40, 4, 42, 34] }
                  ],
                  "best": { "text": "秘宝", "score": 0.98 }
                }
                """,
                "OCR",
                true),
        ]);

    Equal(true, evidence is not null, "relic footer count evidence exists");
    Equal(13, evidence!.Count, "relic footer count");
    Equal("13", evidence.RawText, "relic footer raw text");
    Equal(true, evidence.Confidence > 0.9, "relic footer confidence");
}

static void RecognitionRetryPolicyTargetsLowConfidenceFrames()
{
    var completeRun = new[]
    {
        C("ingot", 0.91),
        C("difficulty", 0.92),
        C("squadId", 0.90),
        C("idea", 0.89),
    };
    Equal(false, RhodesRecognitionRetryPolicy.Evaluate("runStatusFull", [], completeRun).ShouldRetry, "complete run frame does not retry");
    Equal(true, RhodesRecognitionRetryPolicy.Evaluate("runStatusFull", [], completeRun.Where(item => item.Field != "squadId")).ShouldRetry, "missing required run field retries");
    Equal(true, RhodesRecognitionRetryPolicy.Evaluate("runStatusFull", [], completeRun.Select(item => item.Field == "idea" ? item with { Confidence = 0.55 } : item)).ShouldRetry, "low-confidence run field retries");
    Equal(true, RhodesRecognitionRetryPolicy.Evaluate("relicsFull", [], []).ShouldRetry, "empty relic frame retries once");
    Equal(true, RhodesRecognitionRetryPolicy.Evaluate("is3LightHordeFull", [], [], "is3_mizuki").ShouldRetry, "missing Mizuki horde call retries once");
    Equal(true, RhodesRecognitionRetryPolicy.Evaluate("is3RejectionFull", [], [], "is3_mizuki").ShouldRetry, "missing Mizuki rejection retries once");
    Equal(
        false,
        RhodesRecognitionRetryPolicy.Evaluate(
            "is3LightHordeFull",
            [],
            [new MaaCandidatePreview("mizuki", "呼び声：改造", "horde", "呼び声：改造", 0.91, CampaignId: "is3_mizuki", FieldId: "hordeCalls", EffectId: "horde")],
            "is3_mizuki").ShouldRetry,
        "recognized Mizuki horde call does not retry");
    Equal(false, RhodesRecognitionRetryPolicy.Evaluate("is5ThoughtFull", [], []).ShouldRetry, "empty thought list may be valid");

    static MaaCandidatePreview C(string field, double confidence) => new(
        "runStatus",
        field,
        "1",
        "1",
        confidence,
        Field: field);
}

static void MizukiUndetectedPolicyPreservesPriorValues()
{
    Equal(
        true,
        RhodesMizukiUndetectedPolicy.GetPreservationWarning("is3LightHordeFull", [])?.Contains("保持", StringComparison.Ordinal) == true,
        "missing horde call preserves the previous value");
    Equal(
        true,
        RhodesMizukiUndetectedPolicy.GetPreservationWarning("is3RejectionFull", [])?.Contains("保持", StringComparison.Ordinal) == true,
        "missing rejection reaction preserves the previous value");
    Equal(
        null,
        RhodesMizukiUndetectedPolicy.GetPreservationWarning(
            "is3LightHordeFull",
            [new MaaCandidatePreview("mizuki", "呼び声：改造", "horde", "呼び声：改造", 0.91, CampaignId: "is3_mizuki", FieldId: "hordeCalls", EffectId: "horde")]),
        "recognized horde call needs no preservation warning");
}

static void ManualDifficultyCampaignPolicy()
{
    Equal(true, RhodesMaaRecognitionPolicy.RequiresManualDifficulty("is2_phantom"), "Phantom difficulty is manual");
    Equal(true, RhodesMaaRecognitionPolicy.RequiresManualDifficulty("is3_mizuki"), "Mizuki difficulty is manual");
    Equal(true, RhodesMaaRecognitionPolicy.RequiresManualDifficulty("is4_sami"), "Sami difficulty is manual");
    Equal(false, RhodesMaaRecognitionPolicy.RequiresManualDifficulty("is5_sarkaz"), "Sarkaz difficulty remains recognizable");

    var phantomRun = new[]
    {
        C("ingot", 0.91),
        C("squadId", 0.90),
    };
    Equal(
        false,
        RhodesRecognitionRetryPolicy.Evaluate("runStatusFull", [], phantomRun, "is2_phantom").ShouldRetry,
        "Phantom run does not retry for unavailable difficulty and Sarkaz idea");
    Equal(
        true,
        RhodesRecognitionRetryPolicy.Evaluate("runStatusFull", [], phantomRun, "is5_sarkaz").ShouldRetry,
        "Sarkaz run still requires campaign-specific fields");

    static MaaCandidatePreview C(string field, double confidence) => new(
        "runStatus",
        field,
        "1",
        "1",
        confidence,
        Field: field);
}

static void RecognitionFrameFingerprintIsPerceptual()
{
    using var baseline = new SKBitmap(128, 72);
    baseline.Erase(SKColors.Black);
    using (var canvas = new SKCanvas(baseline))
        canvas.DrawRect(new SKRect(0, 0, 64, 72), new SKPaint { Color = SKColors.White });
    var baselineBytes = EncodePng(baseline);

    using var noise = baseline.Copy();
    noise.SetPixel(127, 71, SKColors.DarkGray);
    var noiseBytes = EncodePng(noise);

    using var changed = new SKBitmap(128, 72);
    changed.Erase(SKColors.Black);
    using (var canvas = new SKCanvas(changed))
        canvas.DrawRect(new SKRect(64, 0, 128, 72), new SKPaint { Color = SKColors.White });
    var changedBytes = EncodePng(changed);

    var area = new RhodesRecognitionSwipeArea(0, 0, 1280, 720);
    var baselineHash = RhodesRecognitionFrameFingerprint.Compute(baselineBytes, area);
    Equal(true, RhodesRecognitionFrameFingerprint.Distance(
        baselineHash,
        RhodesRecognitionFrameFingerprint.Compute(noiseBytes, area)) <= 2, "tiny noise remains stable");
    Equal(true, RhodesRecognitionFrameFingerprint.Distance(
        baselineHash,
        RhodesRecognitionFrameFingerprint.Compute(changedBytes, area)) > 2, "layout change differs");
}

static byte[] EncodePng(SKBitmap bitmap)
{
    using var image = SKImage.FromBitmap(bitmap);
    using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
    return encoded.ToArray();
}

static void MaaOcrPreprocessingCropsAndScalesRoi()
{
    using var source = new SKBitmap(4, 4);
    source.Erase(SKColors.Black);
    source.SetPixel(1, 1, SKColors.White);
    using var image = SKImage.FromBitmap(source);
    using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);

    var prepared = RhodesMaaRecognitionImagePreprocessor.Prepare(
        encoded.ToArray(),
        "OCR",
        """{"roi":[1,1,2,2],"only_rec":true}""",
        3);

    using var scaled = SKBitmap.Decode(prepared.EncodedImage);
    Equal(6, scaled.Width, "scaled crop width");
    Equal(6, scaled.Height, "scaled crop height");
    var parameters = JsonNode.Parse(prepared.ParametersJson)!.AsObject();
    Equal(0, parameters["roi"]![0]!.GetValue<int>(), "prepared roi x");
    Equal(0, parameters["roi"]![1]!.GetValue<int>(), "prepared roi y");
    Equal(6, parameters["roi"]![2]!.GetValue<int>(), "prepared roi width");
    Equal(6, parameters["roi"]![3]!.GetValue<int>(), "prepared roi height");
}

static void MaaCatchWindDetailPreprocessingInvertsDarkProse()
{
    using var source = new SKBitmap(6, 4);
    source.Erase(new SKColor(245, 245, 245));
    source.SetPixel(2, 1, new SKColor(45, 45, 45));
    source.SetPixel(3, 1, new SKColor(130, 130, 130));

    var prepared = RhodesMaaRecognitionImagePreprocessor.Prepare(
        EncodePng(source),
        "OCR",
        """{"roi":[1,0,4,3],"only_rec":true}""",
        2,
        RhodesSuiCatchWindDetailResolver.DetailEntry);

    using var binary = SKBitmap.Decode(prepared.EncodedImage);
    Equal(8, binary.Width, "Catch Wind detail crop width is scaled");
    Equal(6, binary.Height, "Catch Wind detail crop height is scaled");
    Equal(SKColors.Black, binary.GetPixel(0, 0), "light detail background becomes black");
    Equal(SKColors.White, binary.GetPixel(2, 2), "dark detail prose becomes white");
    Equal(SKColors.White, binary.GetPixel(4, 2), "antialiased dark prose remains foreground");
}

static void MaaOperatorOcrPreprocessingMasksAndTrimsNameRoi()
{
    using var source = new SKBitmap(16, 10);
    source.Erase(new SKColor(28, 28, 28));
    using (var canvas = new SKCanvas(source))
    {
        canvas.DrawRect(new SKRect(0, 0, 12, 1), new SKPaint { Color = SKColors.White });
        canvas.DrawRect(new SKRect(13, 0, 16, 10), new SKPaint { Color = SKColors.White });
        canvas.DrawRect(new SKRect(4, 4, 6, 8), new SKPaint { Color = SKColors.White });
        canvas.DrawRect(new SKRect(8, 4, 11, 8), new SKPaint { Color = new SKColor(220, 220, 220) });
    }

    var prepared = RhodesMaaRecognitionImagePreprocessor.Prepare(
        EncodePng(source),
        "OCR",
        """{"roi":[0,0,16,10],"only_rec":true}""",
        1,
        "operator.card.name.0");

    using var cropped = SKBitmap.Decode(prepared.EncodedImage);
    Equal(11, cropped.Width, "operator expanded bounding width");
    Equal(8, cropped.Height, "operator expanded bounding height");
    Equal(new SKColor(28, 28, 28), cropped.GetPixel(0, 0), "raw background around the name is preserved");
    Equal(SKColors.White, cropped.GetPixel(2, 2), "bright name pixel preserved");
    Equal(new SKColor(28, 28, 28), cropped.GetPixel(5, 2), "raw gap between glyphs is preserved");
    var parameters = JsonNode.Parse(prepared.ParametersJson)!.AsObject();
    Equal(0, parameters["roi"]![0]!.GetValue<int>(), "operator prepared roi x");
    Equal(11, parameters["roi"]![2]!.GetValue<int>(), "operator prepared roi width");

    using var edgeGlyph = new SKBitmap(10, 8);
    edgeGlyph.Erase(new SKColor(28, 28, 28));
    using (var canvas = new SKCanvas(edgeGlyph))
        canvas.DrawRect(new SKRect(8, 3, 10, 7), new SKPaint { Color = SKColors.White });
    var edgePrepared = RhodesMaaRecognitionImagePreprocessor.Prepare(
        EncodePng(edgeGlyph),
        "OCR",
        """{"roi":[0,0,10,8],"only_rec":true}""",
        1,
        "operator.card.name.1");
    using var edgeCrop = SKBitmap.Decode(edgePrepared.EncodedImage);
    Equal(4, edgeCrop.Width, "small right-edge glyph keeps its expanded bounding box");
    Equal(true, edgeCrop.Pixels.Any(pixel => pixel == SKColors.White), "small glyph touching the right edge is preserved");
}

static void MaaJapaneseOperatorRulesResolveLocalIds()
{
    Equal("purestream", RhodesOperatorOcrNormalizer.ResolveOfficialOperatorId("セイリュゥ"), "MAA purestream regex");
    Equal("gummy", RhodesOperatorOcrNormalizer.ResolveOfficialOperatorId("グで"), "MAA gummy regex");
    Equal("phantom2", RhodesOperatorOcrNormalizer.ResolveOfficialOperatorId("トラコーディア"), "MAA Tragodia regex");
    Equal("pepe", RhodesOperatorOcrNormalizer.ResolveOfficialOperatorId("ペペ"), "exact short operator name remains resolvable");
    Equal(null, RhodesOperatorOcrNormalizer.ResolveOfficialOperatorId("ペペ罠アコルト"), "short operator prefix cannot consume a noisy card ROI");
    Equal(null, RhodesOperatorOcrNormalizer.ResolveOfficialOperatorId("存在しない名前"), "unknown OCR remains unresolved");
}

static void MaaTemplateOcrConfigExposesOperatorOffsets()
{
    var config = RhodesMaaResourceCatalog.LoadTemplateOcrConfig("RhodesTemplate_operatorsFull_operator_card_name");

    Equal(true, config is not null, "operator template OCR config exists");
    Equal("operator.card.name", config!.IdPrefix, "operator id prefix");
    Equal(26, config.OffsetX, "operator offset x");
    Equal(-3, config.OffsetY, "operator offset y");
    Equal(180, config.Width, "operator OCR width");
    Equal(23, config.Height, "operator OCR height");
    Equal(1, config.Scale, "operator OCR scale");
    Equal(16, config.MaxMatches, "operator max matches");
}

static void MaaTemplateOcrExpanderBuildsDynamicRegions()
{
    var config = new MaaTemplateOcrConfig("operator.card.name", 26, -3, 180, 23, 1, 16);
    var requests = RhodesMaaTemplateOcrExpander.BuildRequests(
        config,
        """
        {
          "all": [{"box":[999,999,29,22],"score":0.50}],
          "filtered": [
            {"box":[635,175,29,22],"score":0.99},
            {"box":[635,312,29,22],"score":0.98},
            {"box":[1124,175,29,22],"score":0.97}
          ]
        }
        """);

    Equal(3, requests.Count, "dynamic OCR request count");
    Equal("operator.card.name.0", requests[0].Entry, "dynamic entry name");
    Equal(661, requests[0].X, "dynamic ROI x");
    Equal(172, requests[0].Y, "dynamic ROI y");
    Equal(180, requests[0].Width, "dynamic ROI width");
    Equal(23, requests[0].Height, "dynamic ROI height");
    Equal(1, requests[0].Scale, "dynamic ROI scale");
    Equal(309, requests[1].Y, "second dynamic ROI y");
    Equal(1150, requests[2].X, "right-edge card ROI x");
    Equal(130, requests[2].Width, "right-edge card ROI is clipped to the visible name");
}

static void MaaTemplateOcrExpanderRestoresWeakGridAlignedOperatorAnchors()
{
    var config = new MaaTemplateOcrConfig("operator.card.name", 26, -3, 180, 23, 1, 16);
    var requests = RhodesMaaTemplateOcrExpander.BuildRequests(
        config,
        """
        {
          "all": [
            {"box":[696,175,29,22],"score":0.88},
            {"box":[1111,175,29,22],"score":0.639185},
            {"box":[696,312,29,22],"score":0.97},
            {"box":[1111,312,29,22],"score":0.97},
            {"box":[696,449,29,22],"score":0.97},
            {"box":[1111,449,29,22],"score":0.97},
            {"box":[696,586,29,22],"score":0.97},
            {"box":[1111,586,29,22],"score":0.97},
            {"box":[975,95,29,22],"score":0.633},
            {"box":[560,369,29,22],"score":0.633}
          ],
          "filtered": [
            {"box":[696,175,29,22],"score":0.88},
            {"box":[696,312,29,22],"score":0.97},
            {"box":[1111,312,29,22],"score":0.97},
            {"box":[696,449,29,22],"score":0.97},
            {"box":[1111,449,29,22],"score":0.97},
            {"box":[696,586,29,22],"score":0.97},
            {"box":[1111,586,29,22],"score":0.97}
          ]
        }
        """);

    Equal(8, requests.Count, "weak grid-aligned anchor is restored");
    var restored = requests.Single(request => request.X == 1137 && request.Y == 172);
    Equal(0.639185, restored.TemplateScore, "restored anchor score");
    Equal(143, restored.Width, "restored right-edge ROI keeps its visible width");
    Equal(false, requests.Any(request => request.X == 1001 && request.Y == 92), "off-grid horizontal false match is rejected");
    Equal(false, requests.Any(request => request.X == 586 && request.Y == 366), "off-grid vertical false match is rejected");
}

static void MaaThoughtLoadOcrExpanderTargetsDisplayedValues()
{
    var requests = RhodesMaaThoughtLoadOcrExpander.BuildRequests(
        """
        {
          "filtered": [
            {"box":[478,136,117,17],"score":0.99,"text":"枯れ木と若枝"},
            {"box":[910,136,118,17],"score":0.98,"text":"枯れ木と若枝"},
            {"box":[478,256,45,17],"score":0.97,"text":"築壁"}
          ]
        }
        """);

    Equal(2, requests.Count, "one displayed-load OCR request per unique thought type");
    Equal("thought.card.load.is5_sarkaz_selectable_thought_legacy_08", requests[0].Entry, "repeated thought load entry");
    Equal(423, requests[0].X, "load ROI targets the number at the card icon bottom-right");
    Equal(196, requests[0].Y, "load ROI targets the number below the thought title");
    Equal(48, requests[0].Width, "load ROI width");
    Equal(42, requests[0].Height, "load ROI height");
    Equal(4, requests[0].Scale, "small displayed load is enlarged for MAA-OCR");

    Equal(
        true,
        RhodesMaaThoughtLoadOcrExpander.TryReadDisplayedLoad(
            """{"filtered":[{"text":"３","score":0.99}]}""",
            out var displayedLoad),
        "full-width displayed load is accepted");
    Equal(3, displayedLoad, "displayed load is normalized to an integer");
    Equal(
        true,
        RhodesMaaThoughtLoadOcrExpander.TryReadDisplayedLoad(
            """{"filtered":[{"text":"0","score":0.99}]}""",
            out var zeroLoad),
        "relic or age modifiers may reduce a displayed thought load to zero");
    Equal(0, zeroLoad, "zero displayed load remains a valid post-modifier value");
    Equal(
        false,
        RhodesMaaThoughtLoadOcrExpander.TryReadDisplayedLoad(
            """{"filtered":[{"text":"負荷","score":0.99}]}""",
            out _),
        "non-numeric OCR hit remains unresolved for a later frame");

    var excluded = RhodesMaaThoughtLoadOcrExpander.BuildRequests(
        """{"filtered":[{"box":[478,136,117,17],"score":0.99,"text":"枯れ木と若枝"}]}""",
        ["is5_sarkaz_selectable_thought_legacy_08"]);
    Equal(0, excluded.Count, "resolved thought loads are not OCRed again while scrolling");
}

static void OperatorScanTrackerCachesResolvedCards()
{
    using var firstFrame = new SKBitmap(1280, 720);
    firstFrame.Erase(SKColors.Black);
    using (var canvas = new SKCanvas(firstFrame))
    {
        canvas.DrawRect(new SKRect(110, 105, 142, 134), new SKPaint { Color = SKColors.White });
        canvas.DrawRect(new SKRect(310, 105, 358, 134), new SKPaint { Color = SKColors.White });
    }

    var requests = new[]
    {
        new MaaDynamicOcrRequest("operator.card.name.0", 100, 100, 100, 40, 4, 0.9),
        new MaaDynamicOcrRequest("operator.card.name.1", 300, 100, 100, 40, 4, 0.9),
    };
    var tracker = new RhodesOperatorScanTracker(maxAttemptsPerCard: 2);
    var first = tracker.Select(EncodePng(firstFrame), requests);

    Equal(2, first.WorkItems.Count, "first viewport OCRs every visible card");
    Equal(0, first.StableViewportCount, "first viewport is not repeated");
    tracker.RecordResult(first.WorkItems[0].Fingerprint, resolved: true);
    tracker.RecordResult(first.WorkItems[1].Fingerprint, resolved: false);

    var repeated = tracker.Select(EncodePng(firstFrame), requests);
    Equal(1, repeated.WorkItems.Count, "repeated viewport retries only the unresolved card");
    Equal("operator.card.name.1", repeated.WorkItems[0].Request.Entry, "resolved card is skipped");
    Equal(1, repeated.StableViewportCount, "same card set confirms viewport stability");
    tracker.RecordResult(repeated.WorkItems[0].Fingerprint, resolved: false);
    Equal(true, tracker.CanStopCurrentViewport, "viewport can stop after unresolved card reaches retry limit");
    Equal(true, tracker.CanStopScan, "scan can stop when every observed card is resolved or exhausted");

    using var changedFrame = firstFrame.Copy();
    using (var canvas = new SKCanvas(changedFrame))
        canvas.DrawRect(new SKRect(365, 105, 392, 134), new SKPaint { Color = SKColors.White });
    var changed = tracker.Select(EncodePng(changedFrame), requests);
    Equal(1, changed.WorkItems.Count, "new card fingerprint remains eligible for OCR");
    Equal(0, changed.StableViewportCount, "changed card set resets viewport stability");
}

static void MizukiRejectionCardDetectorIdentifiesPurpleBand()
{
    var request = new MaaDynamicOcrRequest("operator.card.name.0", 1076, 309, 180, 23, 1, 0.99);
    using var affectedFrame = new SKBitmap(1280, 720);
    affectedFrame.Erase(SKColors.Black);
    using (var canvas = new SKCanvas(affectedFrame))
        canvas.DrawRect(new SKRect(1076, 309, 1256, 332), new SKPaint { Color = new SKColor(158, 92, 190) });

    var affected = RhodesMizukiRejectionCardDetector.Detect(EncodePng(affectedFrame), request);
    Equal(true, affected.IsAffected, "purple rejection band detected");
    Equal(true, affected.PurpleRatio > 0.9, "purple ratio retained as evidence");

    using var normalFrame = new SKBitmap(1280, 720);
    normalFrame.Erase(SKColors.Black);
    using (var canvas = new SKCanvas(normalFrame))
    {
        canvas.DrawRect(new SKRect(1026, 225, 1186, 263), new SKPaint { Color = new SKColor(86, 57, 114) });
        canvas.DrawRect(new SKRect(1076, 309, 1256, 332), new SKPaint { Color = SKColors.White });
    }

    var normal = RhodesMizukiRejectionCardDetector.Detect(EncodePng(normalFrame), request);
    Equal(false, normal.IsAffected, "normal white name is not a rejection target even on a purple rarity band");

    using var weakPurpleNoiseFrame = new SKBitmap(1280, 720);
    weakPurpleNoiseFrame.Erase(SKColors.Black);
    using (var canvas = new SKCanvas(weakPurpleNoiseFrame))
        canvas.DrawRect(new SKRect(1076, 309, 1087, 332), new SKPaint { Color = new SKColor(158, 92, 190) });

    var weakPurpleNoise = RhodesMizukiRejectionCardDetector.Detect(EncodePng(weakPurpleNoiseFrame), request);
    Equal(false, weakPurpleNoise.IsAffected, "small purple background contamination is not a rejection target");

    using var shortPurpleNameFrame = new SKBitmap(1280, 720);
    shortPurpleNameFrame.Erase(SKColors.Black);
    using (var canvas = new SKCanvas(shortPurpleNameFrame))
    {
        var paint = new SKPaint { Color = new SKColor(158, 92, 190) };
        canvas.DrawRect(new SKRect(1080, 314, 1086, 326), paint);
        canvas.DrawRect(new SKRect(1092, 314, 1098, 326), paint);
        canvas.DrawRect(new SKRect(1104, 314, 1110, 326), paint);
        canvas.DrawRect(new SKRect(1116, 314, 1122, 326), paint);
    }

    var shortPurpleName = RhodesMizukiRejectionCardDetector.Detect(EncodePng(shortPurpleNameFrame), request);
    Equal(true, shortPurpleName.IsAffected, "short purple name such as Durin is retained below the full ROI ratio threshold");

    using var yellowNameFrame = new SKBitmap(1280, 720);
    yellowNameFrame.Erase(SKColors.Black);
    using (var canvas = new SKCanvas(yellowNameFrame))
    {
        var paint = new SKPaint { Color = new SKColor(255, 210, 40) };
        canvas.DrawRect(new SKRect(1080, 314, 1088, 326), paint);
        canvas.DrawRect(new SKRect(1094, 314, 1102, 326), paint);
        canvas.DrawRect(new SKRect(1108, 314, 1116, 326), paint);
        canvas.DrawRect(new SKRect(1122, 314, 1130, 326), paint);
    }

    var yellowName = RhodesMizukiRejectionCardDetector.Detect(EncodePng(yellowNameFrame), request);
    Equal(true, yellowName.IsEvolution, "yellow operator name is detected as an evolution target");

    var marker = RhodesMizukiRejectionCardDetector.CreateTaskResult(
        request,
        new MaaCandidatePreview(
            "operator",
            "予備隊員-狙撃",
            "reserve_sniper",
            "予備隊員-狙撃",
            0.99,
            OperatorId: "reserve_sniper"),
        affected,
        operatorInstance: 2);
    var candidates = RhodesMaaLocalCandidateConverter.FromTaskResults(
        "operatorsFull",
        [marker],
        "is3_mizuki");
    Equal("reserve_sniper", candidates.Single().OperatorId, "purple card marker becomes a rejection target candidate");
    Equal(2, candidates.Single().OperatorInstance, "purple card marker keeps its recruit instance");

    var evolutionMarker = RhodesMizukiRejectionCardDetector.CreateEvolutionTaskResult(
        request,
        new MaaCandidatePreview(
            "operator",
            "予備隊員-重装",
            "reserve_defender",
            "予備隊員-重装",
            0.99,
            OperatorId: "reserve_defender"),
        yellowName,
        operatorInstance: 2);
    var evolutionCandidates = RhodesMaaLocalCandidateConverter.FromTaskResults(
        "operatorsFull",
        [evolutionMarker],
        "is3_mizuki");
    Equal("operatorEvolution", evolutionCandidates.Single().FieldId, "yellow card marker becomes an evolution target candidate");
    Equal("reserve_defender", evolutionCandidates.Single().OperatorId, "evolution candidate keeps its operator id");
    Equal(2, evolutionCandidates.Single().OperatorInstance, "evolution candidate keeps its recruit instance");
}

static void MizukiRejectionTargetsExpandReserveInstances()
{
    var reserve = new SukiChoiceItem(
        "operator", "reserve_sniper", "予備隊員-狙撃", "★3 狙撃 / 速射手", "狙撃", "速射手", "", "", 3, 1, false)
    {
        IsSelected = true,
        SelectionCount = 3,
    };
    var gummy = new SukiChoiceItem(
        "operator", "gummy", "グム", "★4 重装 / 庇護衛士", "重装", "庇護衛士", "", "", 4, 2, false)
    {
        IsSelected = true,
    };

    var targets = RhodesRecruitedOperatorTargetCatalog.Build(
        [reserve, gummy],
        [
            new SukiOperatorTargetRef("reserve_sniper", 2),
            new SukiOperatorTargetRef("gummy", 1),
        ]);

    Equal(4, targets.Count, "three reserve recruits and one regular operator become four target cards");
    Equal(
        "reserve_sniper#1|reserve_sniper#2|reserve_sniper#3|gummy#1",
        string.Join('|', targets.Select(target => target.TargetKey)),
        "target card keys retain recruit instances");
    Equal(
        "予備隊員-狙撃 1人目|予備隊員-狙撃 2人目|予備隊員-狙撃 3人目|グム",
        string.Join('|', targets.Select(target => target.Name)),
        "reserve target labels identify each recruit");
    Equal(
        "reserve_sniper#2|gummy#1",
        string.Join('|', targets.Where(target => target.IsSelected).Select(target => target.TargetKey)),
        "only explicitly stored recruit instances are selected");

    var legacyTargets = RhodesRecruitedOperatorTargetCatalog.Build(
        [reserve],
        operatorTargets: null,
        legacyOperatorIds: ["reserve_sniper"]);
    Equal(
        "reserve_sniper#1",
        legacyTargets.Single(target => target.IsSelected).TargetKey,
        "legacy aggregate target migrates to the first recruit instance");
}

static void RecognitionProbePayloadsTargetRetainedFields()
{
    var payloads = RhodesRecognitionProbe.DefaultPayloads();
    Equal(5, payloads.Count, "probe payload count");
    Equal(
        "FullFrame OCR|Ingot OCR|IS#5 Idea OCR|Operator Name OCR|TemplateMatch",
        string.Join("|", payloads.Select(item => item.Name)),
        "probe names");
    Equal(false, payloads.Any(item => item.Name == "TopBar OCR"), "removed top bar probe");
    Equal(false, payloads.Any(item => item.Purpose.Contains("希望", StringComparison.Ordinal)), "removed hope probe text");

    var ingot = JsonNode.Parse(payloads.Single(item => item.Name == "Ingot OCR").Payload)!.AsObject();
    var ingotRoi = ingot["roi"]!.AsArray();
    Equal(1170, ingotRoi[0]!.GetValue<int>(), "ingot x");
    Equal(4, ingotRoi[1]!.GetValue<int>(), "ingot y");
    Equal(110, ingotRoi[2]!.GetValue<int>(), "ingot width");
    Equal(58, ingotRoi[3]!.GetValue<int>(), "ingot height");

    var idea = JsonNode.Parse(payloads.Single(item => item.Name == "IS#5 Idea OCR").Payload)!.AsObject();
    var ideaRoi = idea["roi"]!.AsArray();
    Equal(852, ideaRoi[0]!.GetValue<int>(), "idea x");
    Equal(680, ideaRoi[1]!.GetValue<int>(), "idea y");
    Equal(36, ideaRoi[2]!.GetValue<int>(), "idea width");
    Equal(38, ideaRoi[3]!.GetValue<int>(), "idea height");
}

static void MaaRecognitionInvocationSeparatesAlgorithm()
{
    var parsed = RhodesMaaRecognitionInvocation.TryParse(
        """{"recognition":"OCR","roi":[10,20,30,40],"only_rec":true}""",
        out var invocation,
        out var error);

    Equal(true, parsed, "recognition invocation parsed");
    Equal("", error, "recognition invocation error");
    Equal("OCR", invocation.Type, "recognition algorithm");
    var parameters = JsonNode.Parse(invocation.ParametersJson)!.AsObject();
    Equal(false, parameters.ContainsKey("recognition"), "algorithm removed from parameters");
    Equal(10, parameters["roi"]![0]!.GetValue<int>(), "ROI preserved");

    Equal(
        false,
        RhodesMaaRecognitionInvocation.TryParse("""{"roi":[0,0,1280,720]}""", out _, out _),
        "missing algorithm rejected");
}

static void TaskDiagnostics()
{
    var diagnostics = RhodesMaaTaskDiagnostics.Summarize(
        [
        new MaaTaskRunResult(
            "RhodesOcrRegion_operator_name",
            "Succeeded",
            true,
            "ocr detail",
            """{"filtered_results":[{"text":"グム","score":0.88}]}""",
            "OCR",
            true),
        new MaaTaskRunResult(
            "RhodesTemplate_run_status_ingot",
            "Succeeded",
            true,
            "template detail",
            """{"filtered_results":[{"score":0.91}]}""",
            "TemplateMatch",
            true),
        new MaaTaskRunResult(
            "RhodesBrokenTask",
            "Failed",
            false,
            "missing task",
            "",
            "",
            false),
    ]);

    Equal(3, diagnostics.Total, "total");
    Equal(2, diagnostics.Succeeded, "succeeded");
    Equal(2, diagnostics.Hit, "hit");
    Equal(1, diagnostics.Failed, "failed");
    Equal(1, diagnostics.OcrCandidateCount, "ocr candidates");
    Equal(1, diagnostics.TemplateCandidateCount, "template candidates");
    Equal(true, diagnostics.Lines.Any(line => line.Contains("グム", StringComparison.Ordinal)), "ocr line");
    Equal(true, diagnostics.Lines.Any(line => line.Contains("RhodesBrokenTask", StringComparison.Ordinal)), "failed line");
}

static void OcrDetailRowsExposeRawGroups()
{
    var rows = RhodesMaaOcrDetailRows.FromTaskResults(
        [
            new MaaTaskRunResult(
                "RhodesOcrRegion_test",
                "Succeeded",
                true,
                "",
                """
                prefix {"result":{"filtered_results":[{"text":"グム","score":0.91}],"best_result":{"text":"グム","confidence":"0.88"},"all_results":[{"text":"クム","score":0.4}]}}
                """,
                "OCR",
                true),
        ]);

    Equal(3, rows.Count, "ocr detail row count");
    Equal("filtered", rows[0].Source, "filtered source");
    Equal("グム", rows[0].Text, "filtered text");
    Equal(0.91, rows[0].Score, "filtered score");
    Equal("best", rows[1].Source, "best source");
    Equal(0.88, rows[1].Score, "best score");
    Equal("all", rows[2].Source, "all source");
    Equal("RhodesOcrRegion_test", rows[2].Entry, "row entry");
}

static void RoiDetailRowsExposeRectVariants()
{
    var rows = RhodesMaaRoiDetailRows.FromTaskResults(
        [
            new MaaTaskRunResult(
                "RhodesOcrRegion_roi",
                "Succeeded",
                true,
                "",
                """
                {"result":{"rect":[1,2,30,40],"filtered_results":[{"text":"A","roi":{"x":5,"y":6,"width":70,"height":80}}],"best_result":{"text":"B","box":[[10,20],[30,20],[30,50],[10,50]]}}}
                """,
                "OCR",
                true),
        ]);

    Equal(3, rows.Count, "roi row count");
    Equal("detail.rect", rows[0].Source, "detail rect source");
    Equal("1,2 30x40", rows[0].BoundsLabel, "detail bounds");
    Equal("rect", rows[0].Kind, "detail rect kind");
    Equal("診断枠", rows[0].EditKindLabel, "detail rect label");
    Equal("filtered.roi", rows[1].Source, "filtered roi source");
    Equal("5,6 70x80", rows[1].BoundsLabel, "filtered bounds");
    Equal(true, rows[1].IsResourceRoiCandidate, "filtered roi edit candidate");
    Equal("Resource ROI候補", rows[1].EditKindLabel, "filtered roi label");
    Equal("[5,6,70,80]", rows[1].RoiJson, "filtered roi json");
    Equal("best.box", rows[2].Source, "best box source");
    Equal("10,20 20x30", rows[2].BoundsLabel, "box bounds");
    Equal("OCR文字枠", rows[2].EditKindLabel, "box label");
}

static void RoiPreviewProjectorScalesImageCoordinates()
{
    var sourceRows = new[]
    {
        new MaaRoiDetailRow("entry", "filtered.roi", 800, 450, 160, 90, "[800,450,160,90]"),
    };

    var projected = RhodesMaaRoiPreviewProjector.Project(sourceRows, new MaaBaseResolution(1280, 720), 1600, 900);

    Equal(1, projected.Count, "projected count");
    Equal(640.0, projected[0].X, "projected x");
    Equal(360.0, projected[0].Y, "projected y");
    Equal(128.0, projected[0].Width, "projected width");
    Equal(72.0, projected[0].Height, "projected height");
    Equal("1600x900->1280x720", projected[0].ScaleLabel, "projected scale label");
    Equal("entry|filtered.roi|[800,450,160,90]|640,360,128,72", projected[0].Key, "projected key");
    Equal("Resource ROI候補", projected[0].EditKindLabel, "projected edit label");
    Equal("[640,360,128,72]", projected[0].ProjectedRoiJson, "projected roi json");
    var draft = MaaRoiEditDraft.FromPreview(projected[0]);
    Equal(true, draft.HasSelection, "draft has selection");
    Equal("編集候補", draft.StatusLabel, "draft status");
    Equal("[640,360,128,72]", draft.RoiJson, "draft roi json");
    Equal("entry / filtered.roi", draft.Detail, "draft detail");
    Equal("未選択", MaaRoiEditDraft.FromPreview(null).StatusLabel, "empty draft status");

    var oneToOne = RhodesMaaRoiPreviewProjector.Project(sourceRows, new MaaBaseResolution(1280, 720), 0, 0);
    Equal(800.0, oneToOne[0].X, "one-to-one x");
    Equal("1:1", oneToOne[0].ScaleLabel, "one-to-one label");
}

static void RoiSelectionMatcherLinksOcrRows()
{
    var roiRows = new[]
    {
        new MaaRoiPreviewRow("entry-a", "best.box", 1, 2, 3, 4, "best", "1:1"),
        new MaaRoiPreviewRow("entry-b", "filtered.roi", 5, 6, 7, 8, "other", "1:1"),
        new MaaRoiPreviewRow("entry-a", "filtered.roi", 9, 10, 11, 12, "target", "1:1"),
    };
    var ocrRow = new MaaOcrDetailRow("entry-a", "グム", 0.91, "filtered", "OCR");

    var selected = RhodesMaaRoiSelectionMatcher.MatchForOcrDetail(roiRows, ocrRow);

    Equal("target", selected?.Raw, "matched roi row");
    Equal(null, RhodesMaaRoiSelectionMatcher.MatchForOcrDetail(roiRows, new MaaOcrDetailRow("entry-c", "グム", 0.91, "filtered", "OCR")), "missing match");
    Equal("target", RhodesMaaRoiSelectionMatcher.MatchForTaskResult(roiRows, new MaaTaskRunResult("entry-a", "Succeeded", true, ""))?.Raw, "matched task roi");
    Equal("target", RhodesMaaRoiSelectionMatcher.MatchForLogRow(roiRows, new RhodesRecognitionScanLogRow("maa-task", "", "entry-a", "", "", "", ""))?.Raw, "matched log roi");
}

static void RoiEditDraftEvidence()
{
    var draft = new MaaRoiEditDraft("RhodesOcrRegion_test", "filtered.roi", "[10,20,30,40]", true);
    var exportedAt = DateTimeOffset.Parse("2026-07-01T00:00:00Z");
    var json = RhodesMaaRoiEditDraftLog.BuildJson(draft, "runStatusFull", exportedAt);
    var root = JsonNode.Parse(json)!.AsObject();
    Equal(1, root["schemaVersion"]!.GetValue<int>(), "roi draft schema version");
    Equal("maa-roi-edit-draft", root["kind"]!.GetValue<string>(), "roi draft kind");
    Equal("runStatusFull", root["profileId"]!.GetValue<string>(), "roi draft profile");
    Equal("RhodesOcrRegion_test", root["draft"]!.AsObject()["entry"]!.GetValue<string>(), "roi draft entry");
    Equal("[10,20,30,40]", root["draft"]!.AsObject()["roiJson"]!.GetValue<string>(), "roi draft json");

    var directory = Path.Combine(Path.GetTempPath(), $"rhodes-suki-roi-draft-{Guid.NewGuid():N}");
    try
    {
        var file = RhodesMaaRoiEditDraftLog.SaveAsync(draft, "runStatusFull", directory, exportedAt).GetAwaiter().GetResult();
        Equal(true, File.Exists(file), "roi draft file exists");
        Equal(true, Path.GetFileName(file).Contains("runStatusFull", StringComparison.Ordinal), "roi draft filename profile");
    }
    finally
    {
        if (Directory.Exists(directory))
            Directory.Delete(directory, true);
    }
}

static void RoiAdjustmentSessionLog()
{
    var drafts = new[]
    {
        new MaaRoiBatchDraftPreview(
            new MaaRoiEditDraft("RhodesOcrRegion_run_hope_current", "detail.roi", "[10,20,30,40]", true),
            true,
            "確認済み",
            "差分: [1,2,3,4] -> [10,20,30,40]"),
        new MaaRoiBatchDraftPreview(
            new MaaRoiEditDraft("RhodesTemplate_runStatusFull_run_ingot", "detail.roi", "[50,60,70,80]", true),
            false,
            "対象外",
            ""),
    };
    var batchResult = new MaaRoiBatchApplyResult(
        true,
        "ROIドラフトを1件適用できます。",
        1,
        [new MaaRoiDraftApplyResult(true, "ok", "data/recognition/maa-tasks.json", "run.hope.current", "[1,2,3,4]", "[10,20,30,40]")]);
    var comparisonRows = new[]
    {
        new MaaRoiRescanComparisonRow("changed", "希望", "希望=1", "希望=2", "field:hope"),
        new MaaRoiRescanComparisonRow("added", "源石錐", "-", "源石錐=20", "field:ingot"),
    };
    var createdAt = DateTimeOffset.Parse("2026-07-01T00:00:00Z");
    var json = RhodesMaaRoiAdjustmentSessionLog.BuildJson(
        drafts,
        "runStatusFull",
        "recognition.json",
        "capture.png",
        batchResult,
        createdAt,
        "再スキャン比較: 追加1 / 変化1 / 消失0",
        comparisonRows,
        "before.json",
        "after.json");
    var root = JsonNode.Parse(json)!.AsObject();
    Equal(1, root["schemaVersion"]!.GetValue<int>(), "roi session schema version");
    Equal("maa-roi-adjustment-session", root["kind"]!.GetValue<string>(), "roi session kind");
    Equal("runStatusFull", root["profileId"]!.GetValue<string>(), "roi session profile");
    Equal("recognition.json", root["scanLogPath"]!.GetValue<string>(), "roi session scan log path");
    Equal(2, root["drafts"]!.AsArray().Count, "roi session draft count");
    Equal("確認済み", root["drafts"]!.AsArray()[0]!.AsObject()["stateLabel"]!.GetValue<string>(), "roi session draft state");
    Equal("再スキャン比較: 追加1 / 変化1 / 消失0", root["comparisonSummary"]!.GetValue<string>(), "roi session comparison summary");
    Equal(2, root["comparisonRows"]!.AsArray().Count, "roi session comparison row count");
    Equal("before.json", root["comparisonBeforeLogPath"]!.GetValue<string>(), "roi session comparison before path");
    Equal("after.json", root["comparisonAfterLogPath"]!.GetValue<string>(), "roi session comparison after path");

    var directory = Path.Combine(Path.GetTempPath(), $"rhodes-suki-roi-session-{Guid.NewGuid():N}");
    try
    {
        var file = RhodesMaaRoiAdjustmentSessionLog.SaveAsync(
            drafts,
            "runStatusFull",
            "recognition.json",
            "capture.png",
            batchResult,
            directory,
            createdAt,
            "再スキャン比較: 追加1 / 変化1 / 消失0",
            comparisonRows,
            "before.json",
            "after.json").GetAwaiter().GetResult();
        Equal(true, File.Exists(file), "roi session file exists");
        Equal(true, Path.GetFileName(file).Contains("runStatusFull", StringComparison.Ordinal), "roi session filename profile");
        var loaded = RhodesMaaRoiAdjustmentSessionLog.Load(file);
        Equal(2, loaded.DraftCount, "loaded roi session draft count");
        Equal(1, loaded.IncludedCount, "loaded roi session included count");
        Equal(2, loaded.ComparisonCount, "loaded roi session comparison count");
        Equal("希望", loaded.SafeComparisonRows[0].Label, "loaded roi session comparison label");
        Equal("before.json", loaded.SafeComparisonBeforeLogPath, "loaded roi session comparison before path");
        Equal("after.json", loaded.SafeComparisonAfterLogPath, "loaded roi session comparison after path");
        Equal("RhodesOcrRegion_run_hope_current", loaded.Drafts[0].ToPreview().Entry, "loaded roi session preview entry");
        Equal("確認済み", loaded.Drafts[0].ToPreview().StateLabel, "loaded roi session preview state");
        var newerFile = RhodesMaaRoiAdjustmentSessionLog.SaveAsync(
            drafts,
            "operatorsFull",
            "recognition-2.json",
            "capture-2.png",
            null,
            directory,
            createdAt.AddMinutes(10)).GetAwaiter().GetResult();
        var recent = RhodesMaaRoiAdjustmentSessionLog.LoadRecent(directory, limit: 8);
        Equal(2, recent.Count, "roi session recent count");
        Equal(newerFile, recent[0].SessionPath, "roi session recent order");
        Equal("operatorsFull", recent[0].ProfileId, "roi session recent profile");
        Equal(2, recent[0].DraftCount, "roi session recent draft count");
        Equal(1, recent[0].IncludedCount, "roi session recent included count");
        Equal(true, recent[0].Detail.Contains("2候補", StringComparison.Ordinal), "roi session recent detail");
        Equal(2, recent[1].ComparisonCount, "roi session recent comparison count");
    }
    finally
    {
        if (Directory.Exists(directory))
            Directory.Delete(directory, true);
    }
}

static void RoiDraftSourceUpdater()
{
    var source = """
    {
      "version": 1,
      "ocrRegions": [
        { "id": "run.hope.current", "roi": [1,2,3,4] },
        { "id": "run.hope.max", "roi": [5,6,7,8] }
      ]
    }
    """;
    var draft = new MaaRoiEditDraft("RhodesOcrRegion_run_hope_current", "filtered.roi", "[10,20,30,40]", true);
    var result = RhodesMaaRoiDraftSourceUpdater.ApplyToMaaTasksJson(source, draft, out var updated);

    Equal(true, result.Succeeded, "roi source update succeeded");
    Equal("data/recognition/maa-tasks.json", result.SourcePath, "roi source path");
    Equal("run.hope.current", result.TargetId, "roi source target");
    Equal("[1,2,3,4]", result.PreviousRoi, "roi previous value");
    Equal("[10,20,30,40]", result.UpdatedRoi, "roi updated value");
    Equal(true, result.HasDiff, "roi diff visible");
    Equal("差分: [1,2,3,4] -> [10,20,30,40]", result.DiffSummary, "roi diff summary");
    var root = JsonNode.Parse(updated)!.AsObject();
    var regions = root["ocrRegions"]!.AsArray();
    Equal(10, regions[0]!.AsObject()["roi"]!.AsArray()[0]!.GetValue<int>(), "updated roi x");
    Equal(5, regions[1]!.AsObject()["roi"]!.AsArray()[0]!.GetValue<int>(), "unrelated roi unchanged");

    var boxDraft = new MaaRoiEditDraft("RhodesOcrRegion_run_hope_current", "best.box", "[10,20,30,40]", false);
    Equal(false, RhodesMaaRoiDraftSourceUpdater.ApplyToMaaTasksJson(source, boxDraft, out _).Succeeded, "box draft rejected");
    var missingDraft = new MaaRoiEditDraft("RhodesOcrRegion_missing", "filtered.roi", "[10,20,30,40]", true);
    Equal(false, RhodesMaaRoiDraftSourceUpdater.ApplyToMaaTasksJson(source, missingDraft, out _).Succeeded, "missing draft rejected");

    var scanSource = """
    {
      "version": 1,
      "profiles": [
        {
          "id": "runStatusFull",
          "templateOcrRegions": [
            {
              "idPrefix": "run.ingot",
              "templatePath": "assets/recognition/templates/run/IngotIcon.png",
              "searchRoi": { "x": 1, "y": 2, "width": 3, "height": 4 }
            },
            {
              "idPrefix": "run.idea.current",
              "templatePath": "assets/recognition/templates/run/IdeaIcon.png",
              "searchRoi": { "x": 5, "y": 6, "width": 7, "height": 8 }
            }
          ]
        }
      ]
    }
    """;
    var templateDraft = new MaaRoiEditDraft("RhodesTemplate_runStatusFull_run_ingot", "detail.roi", "[11,22,33,44]", true);
    var templateResult = RhodesMaaRoiDraftSourceUpdater.ApplyToScanProfilesJson(scanSource, templateDraft, out var updatedScan);
    Equal(true, templateResult.Succeeded, "scan profile roi update succeeded");
    Equal("data/recognition/scan-profiles.json", templateResult.SourcePath, "scan profile source path");
    Equal("runStatusFull.run.ingot", templateResult.TargetId, "scan profile target");
    Equal("[1,2,3,4]", templateResult.PreviousRoi, "scan profile previous roi");
    Equal("[11,22,33,44]", templateResult.UpdatedRoi, "scan profile updated roi");
    var scanRoot = JsonNode.Parse(updatedScan)!.AsObject();
    var searchRoi = scanRoot["profiles"]!.AsArray()[0]!.AsObject()["templateOcrRegions"]!.AsArray()[0]!.AsObject()["searchRoi"]!.AsObject();
    Equal(11, searchRoi["x"]!.GetValue<int>(), "scan profile roi x");
    Equal(5, scanRoot["profiles"]!.AsArray()[0]!.AsObject()["templateOcrRegions"]!.AsArray()[1]!.AsObject()["searchRoi"]!.AsObject()["x"]!.GetValue<int>(), "unrelated scan profile roi unchanged");
    Equal(true, RhodesMaaRoiDraftSourceUpdater.UsesScanProfilesSource(templateDraft), "template draft resolves scan profiles");
    var sourceResult = RhodesMaaRoiDraftSourceUpdater.ApplyToSourceJson(scanSource, templateDraft, out _);
    Equal(true, sourceResult.Succeeded, "generic source update delegates scan profile");

    var directory = Path.Combine(Path.GetTempPath(), $"rhodes-suki-roi-source-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    try
    {
        var file = Path.Combine(directory, "maa-tasks.json");
        File.WriteAllText(file, source);
        var fileResult = RhodesMaaRoiDraftSourceUpdater.ApplyToMaaTasksFileAsync(file, draft).GetAwaiter().GetResult();
        Equal(true, fileResult.Succeeded, "roi source file update succeeded");
        Equal(true, File.Exists(fileResult.BackupPath), "roi source backup exists");
        var saved = JsonNode.Parse(File.ReadAllText(file))!.AsObject();
        Equal(10, saved["ocrRegions"]!.AsArray()[0]!.AsObject()["roi"]!.AsArray()[0]!.GetValue<int>(), "saved roi x");

        var scanFile = Path.Combine(directory, "scan-profiles.json");
        File.WriteAllText(scanFile, scanSource);
        var scanFileResult = RhodesMaaRoiDraftSourceUpdater.ApplyToSourceFileAsync(scanFile, templateDraft).GetAwaiter().GetResult();
        Equal(true, scanFileResult.Succeeded, "scan profile source file update succeeded");
        Equal(true, File.Exists(scanFileResult.BackupPath), "scan profile source backup exists");
        var savedScan = JsonNode.Parse(File.ReadAllText(scanFile))!.AsObject();
        Equal(11, savedScan["profiles"]!.AsArray()[0]!.AsObject()["templateOcrRegions"]!.AsArray()[0]!.AsObject()["searchRoi"]!.AsObject()["x"]!.GetValue<int>(), "saved scan profile roi x");
    }
    finally
    {
        Directory.Delete(directory, true);
    }
}

static void RoiDraftBatchSourceUpdater()
{
    var maaTasks = """
    {
      "version": 1,
      "ocrRegions": [
        { "id": "run.hope.current", "roi": [1,2,3,4] },
        { "id": "run.hope.max", "roi": [5,6,7,8] }
      ]
    }
    """;
    var scanProfiles = """
    {
      "version": 1,
      "profiles": [
        {
          "id": "runStatusFull",
          "templateOcrRegions": [
            {
              "idPrefix": "run.ingot",
              "templatePath": "assets/recognition/templates/run/IngotIcon.png",
              "searchRoi": { "x": 10, "y": 20, "width": 30, "height": 40 }
            }
          ]
        }
      ]
    }
    """;
    var drafts = new[]
    {
        new MaaRoiEditDraft("RhodesOcrRegion_run_hope_current", "detail.roi", "[101,102,103,104]", true),
        new MaaRoiEditDraft("RhodesTemplate_runStatusFull_run_ingot", "detail.roi", "[201,202,203,204]", true),
    };
    var draftPreview = new MaaRoiBatchDraftPreview(drafts[0], false, "適用済み", "差分あり");
    Equal(false, draftPreview.IsIncluded, "batch draft preview included state");
    Equal("適用済み", draftPreview.StateLabel, "batch draft preview state label");
    Equal("差分あり", draftPreview.StateDetail, "batch draft preview state detail");

    var result = RhodesMaaRoiDraftBatchSourceUpdater.ApplyToSourceJsons(
        maaTasks,
        scanProfiles,
        drafts,
        out var updatedMaaTasks,
        out var updatedScanProfiles);

    Equal(true, result.Succeeded, "batch roi update succeeded");
    Equal(2, result.AppliedCount, "batch roi applied count");
    Equal("ROI一括適用: 2件", result.Summary, "batch roi summary");
    Equal(101, JsonNode.Parse(updatedMaaTasks)!["ocrRegions"]!.AsArray()[0]!.AsObject()["roi"]!.AsArray()[0]!.GetValue<int>(), "batch updated maa roi");
    Equal(201, JsonNode.Parse(updatedScanProfiles)!["profiles"]!.AsArray()[0]!.AsObject()["templateOcrRegions"]!.AsArray()[0]!.AsObject()["searchRoi"]!.AsObject()["x"]!.GetValue<int>(), "batch updated scan roi");

    var failed = RhodesMaaRoiDraftBatchSourceUpdater.ApplyToSourceJsons(
        maaTasks,
        scanProfiles,
        [drafts[0], new MaaRoiEditDraft("RhodesOcrRegion_missing", "detail.roi", "[1,1,1,1]", true)],
        out var failedMaaTasks,
        out var failedScanProfiles);
    Equal(false, failed.Succeeded, "batch roi update fails atomically");
    Equal(1, failed.AppliedCount, "batch roi reports successful attempts before failure");
    Equal(maaTasks, failedMaaTasks, "failed batch keeps maa source unchanged");
    Equal(scanProfiles, failedScanProfiles, "failed batch keeps scan source unchanged");

    var directory = Path.Combine(Path.GetTempPath(), $"rhodes-suki-roi-batch-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    try
    {
        var maaTasksPath = Path.Combine(directory, "maa-tasks.json");
        var scanProfilesPath = Path.Combine(directory, "scan-profiles.json");
        File.WriteAllText(maaTasksPath, maaTasks);
        File.WriteAllText(scanProfilesPath, scanProfiles);
        var fileResult = RhodesMaaRoiDraftBatchSourceUpdater.ApplyToSourceFilesAsync(maaTasksPath, scanProfilesPath, drafts).GetAwaiter().GetResult();
        Equal(true, fileResult.Succeeded, "batch roi file update succeeded");
        Equal(true, File.Exists(fileResult.MaaTasksBackupPath), "batch maa backup exists");
        Equal(true, File.Exists(fileResult.ScanProfilesBackupPath), "batch scan backup exists");
        Equal(true, fileResult.BackupSummary.Contains("maa=", StringComparison.Ordinal), "batch backup summary maa");
        Equal(101, JsonNode.Parse(File.ReadAllText(maaTasksPath))!["ocrRegions"]!.AsArray()[0]!.AsObject()["roi"]!.AsArray()[0]!.GetValue<int>(), "batch file updated maa roi");
        Equal(201, JsonNode.Parse(File.ReadAllText(scanProfilesPath))!["profiles"]!.AsArray()[0]!.AsObject()["templateOcrRegions"]!.AsArray()[0]!.AsObject()["searchRoi"]!.AsObject()["x"]!.GetValue<int>(), "batch file updated scan roi");
    }
    finally
    {
        Directory.Delete(directory, true);
    }
}

static void MaaGeneratedResourceBuilder()
{
    var generated = RhodesMaaGeneratedResourceBuilder.BuildJson(
        File.ReadAllText(Path.Combine("data", "recognition", "maa-tasks.json")),
        File.ReadAllText(Path.Combine("data", "recognition", "scan-profiles.json")));
    var root = JsonNode.Parse(generated)!.AsObject();
    Equal(true, root.ContainsKey("RhodesGeneratedEmpty"), "generated empty node");
    Equal("OCR", root["RhodesOcrRegion_run_ingot"]!.AsObject()["recognition"]!.GetValue<string>(), "generated ingot recognition");
    Equal(1170, root["RhodesOcrRegion_run_ingot"]!.AsObject()["roi"]!.AsArray()[0]!.GetValue<int>(), "generated ingot roi x");
    Equal("OCR", root["RhodesScreen_relic_list"]!.AsObject()["recognition"]!.GetValue<string>(), "generated relic target screen recognition");
    Equal(324, root["RhodesOcrRegion_run_squad_name"]!.AsObject()["roi"]!.AsArray()[1]!.GetValue<int>(), "generated squad name roi y");
    Equal(false, root.ContainsKey("RhodesOcrRegion_run_hope_current"), "discarded hope node omitted");
    Equal(false, root["RhodesOcrRegion_relic_list_text"]!.AsObject()["only_rec"]!.GetValue<bool>(), "relic list enables text detection");
    Equal(24, root["RhodesOcrRegion_relic_list_text"]!.AsObject()["roi"]!.AsArray()[1]!.GetValue<int>(), "relic list includes first row names");
    Equal(600, root["RhodesOcrRegion_relic_list_text"]!.AsObject()["roi"]!.AsArray()[3]!.GetValue<int>(), "relic list covers full visible height");
    Equal("OCR", root["RhodesOcrRegion_relic_detail_name"]!.AsObject()["recognition"]!.GetValue<string>(), "single relic detail name recognition");
    Equal(false, root["RhodesOcrRegion_is5_thought_list_text"]!.AsObject()["only_rec"]!.GetValue<bool>(), "thought list enables text detection");
    Equal(false, root["RhodesOcrRegion_is6_coin_list_text"]!.AsObject()["only_rec"]!.GetValue<bool>(), "held coin list enables multi-line text detection");
    Equal("OCR", root["RhodesOcrRegion_is6_active_coin_list_text"]!.AsObject()["recognition"]!.GetValue<string>(), "active coin panel name recognition");
    Equal(620, root["RhodesOcrRegion_is6_active_coin_list_text"]!.AsObject()["roi"]!.AsArray()[0]!.GetValue<int>(), "active coin panel name roi x");
    Equal(175, root["RhodesOcrRegion_is6_active_coin_list_text"]!.AsObject()["roi"]!.AsArray()[1]!.GetValue<int>(), "active coin panel name roi includes the first row");
    Equal(470, root["RhodesOcrRegion_is6_active_coin_list_text"]!.AsObject()["roi"]!.AsArray()[2]!.GetValue<int>(), "active coin panel name roi includes direction prose");
    Equal(170, root["RhodesOcrRegion_is5_thought_load_current"]!.AsObject()["roi"]!.AsArray()[0]!.GetValue<int>(), "thought total load roi targets the large numeric value");
    Equal(230, root["RhodesOcrRegion_is5_thought_load_current"]!.AsObject()["roi"]!.AsArray()[1]!.GetValue<int>(), "thought total load roi excludes the heading text");
    Equal(1055, root["RhodesOcrRegion_is3_ingot_value"]!.AsObject()["roi"]!.AsArray()[0]!.GetValue<int>(), "Mizuki ingot roi includes the complete two-digit value");
    Equal(75, root["RhodesOcrRegion_is3_ingot_value"]!.AsObject()["roi"]!.AsArray()[2]!.GetValue<int>(), "Mizuki ingot roi leaves OCR margin around the value");
    Equal(315, root["RhodesOcrRegion_is3_rejection_name"]!.AsObject()["roi"]!.AsArray()[1]!.GetValue<int>(), "Mizuki rejection roi includes both measured reaction name positions");
    Equal(105, root["RhodesOcrRegion_is3_rejection_name"]!.AsObject()["roi"]!.AsArray()[3]!.GetValue<int>(), "Mizuki rejection roi covers heading and reaction name without relying on one vertical layout");
    Equal(false, root["RhodesOcrRegion_is3_rejection_name"]!.AsObject()["only_rec"]!.GetValue<bool>(), "Mizuki rejection roi returns separate heading and name rows");
    Equal("TemplateMatch", root["RhodesTemplate_runStatusFull_run_ingot"]!.AsObject()["recognition"]!.GetValue<string>(), "generated template recognition");
    Equal("run/IngotIcon.png", root["RhodesTemplate_runStatusFull_run_ingot"]!.AsObject()["template"]!.GetValue<string>(), "generated template path");
    var squadBatch = root["RhodesTemplate_runStatusFull_run_squad_icon_is5_sarkaz_batch"]!.AsObject();
    Equal("Or", squadBatch["recognition"]!.GetValue<string>(), "generated Sarkaz squad recognition is batched");
    Equal(17, squadBatch["any_of"]!.AsArray().Count, "generated Sarkaz squad batch child count");
    Equal(
        "run/SquadIconRight_is5_sarkaz_squad_14.png",
        squadBatch["any_of"]!.AsArray()[13]!.AsObject()["template"]!.GetValue<string>(),
        "generated Sarkaz squad batch preserves template order");
    Equal(
        "is5_sarkaz_squad_14",
        squadBatch["attach"]!.AsObject()["templateIds"]!.AsArray()[13]!.GetValue<string>(),
        "generated Sarkaz squad batch preserves squad ids");
    Equal(true, root.ContainsKey("RhodesTemplate_runStatusFull_run_squad_icon_is6_sui_batch"), "generated resource includes the Sui squad batch");
    var suiSquadBatch = root["RhodesTemplate_runStatusFull_run_squad_icon_is6_sui_batch"]!.AsObject();
    Equal("Or", suiSquadBatch["recognition"]!.GetValue<string>(), "generated Sui squad recognition is batched");
    Equal(19, suiSquadBatch["any_of"]!.AsArray().Count, "generated Sui squad batch child count");
    Equal(
        "run/SquadIconRight_is6_sui_squad_19.png",
        suiSquadBatch["any_of"]!.AsArray()[18]!.AsObject()["template"]!.GetValue<string>(),
        "generated Sui squad batch preserves template order");
    Equal(
        "is6_sui_squad_19",
        suiSquadBatch["attach"]!.AsObject()["templateIds"]!.AsArray()[18]!.GetValue<string>(),
        "generated Sui squad batch preserves squad ids");
    var mizukiSquadEntries = root
        .Where(item => item.Key.StartsWith("RhodesTemplate_runStatusFull_run_squad_icon_is3_mizuki_squad_", StringComparison.Ordinal))
        .Select(item => item.Value!.AsObject())
        .ToArray();
    Equal(13, mizukiSquadEntries.Length, "generated Mizuki squad template count");
    Equal(3, mizukiSquadEntries[0]["method"]!.GetValue<int>(), "Mizuki squad templates use measured correlation matching");
    Equal("[8,638,86,82]", mizukiSquadEntries[0]["roi"]!.ToJsonString(), "Mizuki squad ROI excludes roll count");
    Equal(
        "run/DifficultyFlag.png",
        root["RhodesTemplate_runStatusFull_run_difficulty_grade_anchor"]!.AsObject()["template"]!.GetValue<string>(),
        "generated difficulty anchor template");
    Equal(
        "run/DifficultyFlag_is6_sui.png",
        root["RhodesTemplate_runStatusFull_run_difficulty_grade_is6_sui_anchor"]!.AsObject()["template"]!.GetValue<string>(),
        "generated Sui difficulty anchor template");
    Equal(
        NormalizeLineEndings(File.ReadAllText(Path.Combine("apps", "rhodes-suki", "resource", "base", "pipeline", "rhodes-generated.json"))),
        NormalizeLineEndings(generated),
        "C# resource generator matches checked-in JS-generated pipeline");

    var guardedGenerated = RhodesMaaGeneratedResourceBuilder.BuildJson(
        """
        {
          "screens": [
            { "id": "run.hope.panel", "recognition": { "type": "OCR", "roi": [1,2,3,4] } }
          ],
          "candidates": [
            { "id": "run.safe.candidate", "recognition": { "type": "OCR", "roi": [1,2,3,4] }, "candidate": { "kind": "runStatus", "field": "hope" } }
          ],
          "ocrRegions": [
            { "id": "run.hope.current", "roi": [1,2,3,4] },
            { "id": "run.life.points", "roi": [1,2,3,4] },
            { "id": "run.shield", "roi": [1,2,3,4] },
            { "id": "run.command.level", "roi": [1,2,3,4] },
            { "id": "run.safe", "roi": [1,2,3,4] },
            { "id": "run.ingot", "roi": [5,6,7,8] }
          ]
        }
        """,
        """
        {
          "profiles": [
            {
              "id": "runStatusFull",
              "templateOcrRegions": [
                { "idPrefix": "run.top.hope", "templatePath": "assets/recognition/templates/run/IngotIcon.png", "searchRoi": { "x": 1, "y": 2, "width": 3, "height": 4 } },
                { "idPrefix": "run.ingot", "templatePath": "assets/recognition/templates/run/IngotIcon.png", "searchRoi": { "x": 5, "y": 6, "width": 7, "height": 8 } }
              ]
            }
          ]
        }
        """);
    var guardedRoot = JsonNode.Parse(guardedGenerated)!.AsObject();
    Equal(false, guardedRoot.ContainsKey("RhodesScreen_run_hope_panel"), "guarded screen hope omitted");
    Equal(false, guardedRoot.ContainsKey("RhodesCandidate_run_safe_candidate"), "guarded candidate hope field omitted");
    Equal(false, guardedRoot.ContainsKey("RhodesOcrRegion_run_hope_current"), "guarded hope region omitted");
    Equal(false, guardedRoot.ContainsKey("RhodesOcrRegion_run_life_points"), "guarded life region omitted");
    Equal(false, guardedRoot.ContainsKey("RhodesOcrRegion_run_shield"), "guarded shield region omitted");
    Equal(false, guardedRoot.ContainsKey("RhodesOcrRegion_run_command_level"), "guarded command level region omitted");
    Equal(false, guardedRoot.ContainsKey("RhodesOcrRegion_run_safe"), "guarded unknown run value omitted");
    Equal(false, guardedRoot.ContainsKey("RhodesTemplate_runStatusFull_run_top_hope"), "guarded hope template omitted");
    Equal("OCR", guardedRoot["RhodesOcrRegion_run_ingot"]!.AsObject()["recognition"]!.GetValue<string>(), "guarded ingot retained");
    Equal("TemplateMatch", guardedRoot["RhodesTemplate_runStatusFull_run_ingot"]!.AsObject()["recognition"]!.GetValue<string>(), "guarded ingot template retained");
    Equal(true, RhodesMaaRecognitionPolicy.IsRetainedRecognitionSource("run.idea.current"), "policy retains IS special value");
    Equal(false, RhodesMaaRecognitionPolicy.IsRetainedRecognitionSource("run.safe"), "policy rejects unknown run values");
    Equal(false, RhodesMaaRecognitionPolicy.IsRetainedRecognitionSource("run.safe", "commandLevel"), "policy rejects discarded candidate fields");
    Equal(false, RhodesMaaRecognitionPolicy.IsPublishableEntry("RhodesOcrRegion_run_safe"), "policy rejects unknown run resource entries");
    Equal(false, RhodesMaaRecognitionPolicy.IsPublishableEntry("RhodesOcrRegion_run_shield"), "policy rejects discarded resource entries");
    Equal(true, RhodesMaaRecognitionPolicy.IsPublishableEntry("RhodesOcrRegion_run_ingot"), "policy retains ingot resource entries");
    Equal(true, RhodesMaaRecognitionPolicy.IsPublishableEntry("RhodesTemplate_runStatusFull_run_squad_icon_is5_sarkaz_batch"), "policy retains squad icon template family");
    Equal(true, RhodesMaaRecognitionPolicy.IsPublishableEntry("RhodesRunStatusIdeaIcon"), "policy retains manual special icon entry");
    var targetPolicyPath = Path.Combine(
        Directory.GetCurrentDirectory(),
        RhodesMaaRecognitionPolicy.TargetPolicySourcePath.Replace('/', Path.DirectorySeparatorChar));
    using var targetPolicyDocument = JsonDocument.Parse(File.ReadAllText(targetPolicyPath));
    var manifestAbandonedFields = targetPolicyDocument.RootElement
        .GetProperty("runRecognition")
        .GetProperty("abandonedFields")
        .EnumerateArray()
        .Select(item => item.GetString() ?? "")
        .Where(item => !string.IsNullOrWhiteSpace(item))
        .OrderBy(item => item, StringComparer.Ordinal);
    Equal(
        string.Join("|", manifestAbandonedFields),
        string.Join("|", RhodesMaaRecognitionPolicy.AbandonedRunFields.OrderBy(item => item, StringComparer.Ordinal)),
        "policy abandoned fields from manifest");
    var manifestCandidateKinds = targetPolicyDocument.RootElement
        .GetProperty("recognitionTargets")
        .GetProperty("retainedCandidateKinds")
        .EnumerateArray()
        .Select(item => item.GetString() ?? "")
        .Where(item => !string.IsNullOrWhiteSpace(item));
    Equal(
        "age|coin|mizuki|operator|relic|revelation|runStatus|sui|thought",
        string.Join("|", manifestCandidateKinds.OrderBy(item => item, StringComparer.Ordinal)),
        "manifest candidate kinds encode retained recognition targets");
    Equal(
        string.Join("|", manifestCandidateKinds.OrderBy(item => item, StringComparer.Ordinal)),
        string.Join("|", RhodesMaaRecognitionPolicy.RetainedCandidateKinds.OrderBy(item => item, StringComparer.Ordinal)),
        "policy candidate kinds from manifest");
    var manifestRetainedRunStatusFields = targetPolicyDocument.RootElement
        .GetProperty("runRecognition")
        .GetProperty("retainedFields")
        .EnumerateArray()
        .Select(item => item.GetString() ?? "")
        .Where(item => !string.IsNullOrWhiteSpace(item));
    Equal(
        "difficulty|hallucinations|idea|ingot|performanceId|squadId|squadRandomEffectOptionId|ticket",
        string.Join("|", manifestRetainedRunStatusFields.OrderBy(item => item, StringComparer.Ordinal)),
        "manifest run status fields encode retained run recognition targets");
    Equal(
        string.Join("|", manifestRetainedRunStatusFields.OrderBy(item => item, StringComparer.Ordinal)),
        string.Join("|", RhodesMaaRecognitionPolicy.RetainedRunStatusFields.OrderBy(item => item, StringComparer.Ordinal)),
        "policy run status fields from manifest");
    Equal(true, RhodesMaaRecognitionPolicy.IsRetainedCandidate(new MaaCandidatePreview("operator", "グム", "gum", "グム", 0.9, OperatorId: "gum")), "policy retains operator candidates");
    Equal(true, RhodesMaaRecognitionPolicy.IsRetainedCandidate(new MaaCandidatePreview("relic", "秘宝", "relic", "秘宝", 0.9, RelicId: "relic")), "policy retains relic candidates");
    Equal(true, RhodesMaaRecognitionPolicy.IsRetainedCandidate(new MaaCandidatePreview("runStatus", "源石錐", "20", "20", 0.9, Field: "ingot")), "policy retains ingot candidates");
    Equal(false, RhodesMaaRecognitionPolicy.IsRetainedCandidate(new MaaCandidatePreview("runStatus", "希望", "3", "3", 0.9, Field: "hope")), "policy rejects abandoned hope candidates");
    Equal(false, RhodesMaaRecognitionPolicy.IsRetainedCandidate(new MaaCandidatePreview("status", "耐久値", "4", "4", 0.9, Field: "lifePoints")), "policy rejects unknown candidate kinds");
    Equal(true, RhodesMaaRecognitionPolicy.RetainedRunRecognitionIds.Contains("run.ingot"), "policy retained fields from manifest");
    Equal(true, File.Exists(targetPolicyPath),
        "target policy manifest exists");
    Equal(true, File.Exists(Path.Combine(
        AppContext.BaseDirectory,
        RhodesMaaRecognitionPolicy.TargetPolicySourcePath.Replace('/', Path.DirectorySeparatorChar))),
        "target policy manifest is copied to app output");

    var directory = Path.Combine(Path.GetTempPath(), $"rhodes-suki-generated-resource-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    try
    {
        var maaTasks = Path.Combine(directory, "maa-tasks.json");
        var scanProfiles = Path.Combine(directory, "scan-profiles.json");
        var output = Path.Combine(directory, "rhodes-generated.json");
        File.WriteAllText(maaTasks, File.ReadAllText(Path.Combine("data", "recognition", "maa-tasks.json")));
        File.WriteAllText(scanProfiles, File.ReadAllText(Path.Combine("data", "recognition", "scan-profiles.json")));
        File.WriteAllText(output, "{}");
        var result = RhodesMaaGeneratedResourceBuilder.RegenerateFileAsync(maaTasks, scanProfiles, output).GetAwaiter().GetResult();
        Equal(true, result.Succeeded, "regenerate resource succeeded");
        Equal(true, File.Exists(result.BackupPath), "regenerate resource backup");
        Equal(true, result.NodeCount > 10, "regenerate resource node count");
        Equal(true, File.ReadAllText(output).Contains("RhodesOcrRegion_run_ingot", StringComparison.Ordinal), "regenerated resource output");
        Equal(false, File.ReadAllText(output).Contains("RhodesOcrRegion_run_hope_current", StringComparison.Ordinal), "discarded hope output omitted");
    }
    finally
    {
        Directory.Delete(directory, true);
    }
}

static void MaaNativeEvidenceLog()
{
    var json = RhodesMaaRecognitionEvidenceLog.BuildJson(
        [
            new MaaTaskRunResult(
                "RhodesOcrRegion_operator_name",
                "Succeeded",
                true,
                "ocr detail",
                """{"filtered_results":[{"text":"グム","score":0.88}]}""",
                "OCR",
                true),
            new MaaTaskRunResult("RhodesBrokenTask", "Failed", false, "missing task", "", "", false),
        ],
        [
            new MaaCandidatePreview("operator", "グム", "gummy", "グム", 0.88, OperatorId: "gummy"),
        ],
        "operatorsFull",
        DateTimeOffset.Parse("2026-07-01T00:00:00Z"),
        DateTimeOffset.Parse("2026-07-01T00:00:02Z"),
        "request-a",
        "scan-a",
        "O:/debug/native-capture.png",
        12345,
        "オペレーター",
        ["RhodesOperatorNameOcr", "RhodesOcrRegion_operator_name"],
        new MaaRecognitionRuntimeEvidence(
            "mumu",
            "MuMu Player",
            "M:/Program Files/Netease/MuMu Player 12/shell/adb.exe",
            "127.0.0.1:16384",
            "{}",
            "emulator-fast",
            "MuMu高速入力",
            "EmulatorExtras",
            "EmulatorExtras, Maatouch, MinitouchAndAdbKey, AdbShell",
            "emulator-fast",
            "MuMu/LD高速撮影",
            "EmulatorExtras",
            "EmulatorExtras, RawWithGzip, Encode, EncodeToFileAndPull",
            "maa-ocr",
            "MAA-OCR",
            "MAAFramework系OCRを使います。",
            "接続済み",
            "MAA Controller LinkStart: Succeeded",
            "resource",
            "agent",
            1280,
            720,
            "1280x720 (16:9)",
            true),
        new MaaResourceExecutionPlan(
            "operatorsFull",
            "オペレーター",
            "profile preset",
            ["RhodesOperatorNameOcr", "RhodesOcrRegion_operator_name"],
            [
                new MaaResourceTaskPreview("RhodesOperatorNameOcr", "オペレーター", "", ["operatorsFull"], "manual"),
                new MaaResourceTaskPreview("RhodesOcrRegion_operator_name", "オペレーター名", "", ["operatorsFull"], "generated"),
            ],
            "",
            MaaResourceExecutionPlan.ReadyState),
        new MaaResourceContractSnapshot(true, 43, 7, 7, []),
        "frame-a",
        "O:/debug/frame-a.json",
        "O:/debug/frame-a-state.json",
        new SukiCandidateApplySummary(
            1,
            1,
            ["operator:gummy"],
            [
                new SukiCandidateApplyOutcome(0, "operator", "グム", "gummy", "gummy", "applied", "operator:gummy", ""),
                new SukiCandidateApplyOutcome(1, "operator", "不明", "", "", "ignored", "", "missing-operator-id"),
            ]),
        true,
        "api down");

    var root = JsonNode.Parse(json)!.AsObject();
    Equal(1, root["schemaVersion"]!.GetValue<int>(), "evidence schema version");
    Equal("operatorsFull", root["profileId"]!.GetValue<string>(), "evidence profile");
    Equal("オペレーター", root["profileLabel"]!.GetValue<string>(), "evidence profile label");
    Equal("suki-maa-native", root["source"]!.GetValue<string>(), "evidence source");
    var counts = root["counts"]!.AsObject();
    Equal(1, counts["candidates"]!.GetValue<int>(), "evidence candidate count");
    Equal(2, counts["resourceTasks"]!.GetValue<int>(), "evidence task count");
    Equal(2, counts["presetTasks"]!.GetValue<int>(), "evidence preset task count");
    Equal(1, counts["failedResourceTasks"]!.GetValue<int>(), "evidence failed count");
    Equal(3, counts["log"]!.GetValue<int>(), "evidence log count includes capture");
    Equal("capture", root["log"]!.AsArray()[0]!.AsObject()["event"]!.GetValue<string>(), "evidence capture log event");
    Equal("O:/debug/native-capture.png", root["log"]!.AsArray()[0]!.AsObject()["path"]!.GetValue<string>(), "evidence capture log path");
    Equal("frame-a", root["log"]!.AsArray()[0]!.AsObject()["frameId"]!.GetValue<string>(), "evidence capture frame id");
    Equal("maa-task", root["log"]!.AsArray()[1]!.AsObject()["event"]!.GetValue<string>(), "evidence task log event");
    var evidence = root["evidence"]!.AsObject();
    Equal("maa-resource-task-results", evidence["kind"]!.GetValue<string>(), "evidence kind");
    Equal("operatorsFull", evidence["profile"]!.AsObject()["id"]!.GetValue<string>(), "evidence profile object id");
    Equal("オペレーター", evidence["profile"]!.AsObject()["label"]!.GetValue<string>(), "evidence profile object label");
    Equal(2, evidence["profile"]!.AsObject()["presetTaskEntries"]!.AsArray().Count, "evidence preset entries");
    var executionPlan = evidence["profile"]!.AsObject()["executionPlan"]!.AsObject();
    Equal(MaaResourceExecutionPlan.ReadyState, executionPlan["state"]!.GetValue<string>(), "evidence execution plan state");
    Equal("実行可能", executionPlan["stateLabel"]!.GetValue<string>(), "evidence execution plan state label");
    Equal(true, executionPlan["canRun"]!.GetValue<bool>(), "evidence execution plan can run");
    Equal("profile preset", executionPlan["source"]!.GetValue<string>(), "evidence execution plan source");
    Equal(2, executionPlan["taskCount"]!.GetValue<int>(), "evidence execution plan task count");
    Equal(2, executionPlan["taskEntries"]!.AsArray().Count, "evidence execution plan entries");
    Equal("O:/debug/native-capture.png", evidence["capture"]!.AsObject()["path"]!.GetValue<string>(), "evidence capture path");
    Equal("frame-a", evidence["capture"]!.AsObject()["frameId"]!.GetValue<string>(), "evidence capture frame id");
    Equal("O:/debug/frame-a.json", evidence["capture"]!.AsObject()["metadataPath"]!.GetValue<string>(), "evidence capture metadata path");
    Equal("O:/debug/frame-a-state.json", evidence["capture"]!.AsObject()["stateSnapshotPath"]!.GetValue<string>(), "evidence capture state snapshot path");
    var contract = evidence["contract"]!.AsObject();
    Equal(true, contract["isValid"]!.GetValue<bool>(), "evidence contract valid");
    Equal("OK", contract["state"]!.GetValue<string>(), "evidence contract state");
    Equal(43, contract["taskCount"]!.GetValue<int>(), "evidence contract task count");
    Equal(7, contract["groupCount"]!.GetValue<int>(), "evidence contract group count");
    Equal(7, contract["presetCount"]!.GetValue<int>(), "evidence contract preset count");
    Equal(0, contract["errors"]!.AsArray().Count, "evidence contract errors");
    var runtime = evidence["runtime"]!.AsObject();
    Equal("mumu", runtime["adbPresetId"]!.GetValue<string>(), "evidence runtime adb preset");
    Equal("127.0.0.1:16384", runtime["adbSerial"]!.GetValue<string>(), "evidence runtime serial");
    Equal("emulator-fast", runtime["adbScreencapMethodId"]!.GetValue<string>(), "evidence runtime screencap");
    Equal("emulator-fast", runtime["adbInputMethodId"]!.GetValue<string>(), "evidence runtime input");
    Equal("maa-ocr", runtime["ocrEngineId"]!.GetValue<string>(), "evidence runtime OCR");
    Equal("1280x720 (16:9)", runtime["baseResolution"]!.GetValue<string>(), "evidence runtime base resolution");
    Equal(true, runtime["isControllerReady"]!.GetValue<bool>(), "evidence runtime controller ready");
    Equal(2, evidence["diagnostics"]!.AsObject()["total"]!.GetValue<int>(), "evidence diagnostics");
    var stateApply = evidence["stateApply"]!.AsObject();
    Equal(1, stateApply["appliedCount"]!.GetValue<int>(), "evidence state apply applied count");
    Equal(1, stateApply["ignoredCount"]!.GetValue<int>(), "evidence state apply ignored count");
    Equal("operator:gummy", stateApply["appliedFields"]!.AsArray()[0]!.GetValue<string>(), "evidence state apply field");
    Equal(true, stateApply["localFallbackUsed"]!.GetValue<bool>(), "evidence state apply fallback");
    Equal("api down", stateApply["apiError"]!.GetValue<string>(), "evidence state apply api error");
    var stateApplyOutcomes = stateApply["outcomes"]!.AsArray();
    Equal(2, stateApplyOutcomes.Count, "evidence state apply outcome count");
    Equal("applied", stateApplyOutcomes[0]!.AsObject()["outcome"]!.GetValue<string>(), "evidence state apply outcome applied");
    Equal("missing-operator-id", stateApplyOutcomes[1]!.AsObject()["ignoredReason"]!.GetValue<string>(), "evidence state apply ignored reason");
}

static void RecognitionScanHistoryLoadsUnifiedLogs()
{
    var directory = Path.Combine(Path.GetTempPath(), $"rhodes-suki-history-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    try
    {
        File.WriteAllText(
            Path.Combine(directory, "recognition-2026-07-01T00-00-00-000Z-relicsFull-api.json"),
            """
            {
              "schemaVersion": 1,
              "requestId": "api-request",
              "scanId": "api-scan",
              "profileId": "relicsFull",
              "profileLabel": "秘宝スキャン",
              "source": "adb",
              "status": "completed",
              "startedAt": "2026-07-01T00:00:00.000Z",
              "completedAt": "2026-07-01T00:00:01.000Z",
              "counts": { "candidates": 1, "suggestions": 0, "autoApplied": 0, "log": 4 },
              "candidates": [
                { "kind": "relic", "label": "テスト秘宝", "value": "test_relic", "rawText": "テスト秘宝", "confidence": 0.8, "relicId": "test_relic" }
              ],
              "log": [
                { "event": "capture", "at": "2026-07-01T00:00:00.100Z", "stage": "scan", "path": "O:/debug/shot.png" },
                { "event": "recognize", "at": "2026-07-01T00:00:00.900Z", "count": 1 }
              ]
            }
            """);
        File.WriteAllText(
            Path.Combine(directory, "recognition-2026-07-01T00-00-02-000Z-operatorsFull-native.json"),
            RhodesMaaRecognitionEvidenceLog.BuildJson(
                [
                    new MaaTaskRunResult("RhodesOcrRegion_operator_name", "Succeeded", true, "", """{"filtered_results":[{"text":"グム","score":0.91}]}""", "OCR", true),
                ],
                [
                    new MaaCandidatePreview("operator", "グム", "gummy", "グム", 0.91, OperatorId: "gummy"),
                ],
                "operatorsFull",
                DateTimeOffset.Parse("2026-07-01T00:00:02Z"),
                DateTimeOffset.Parse("2026-07-01T00:00:03Z"),
                "native-request",
                "native-scan",
                "O:/debug/native-shot.png",
                54321,
                "オペレーター",
                ["RhodesOperatorNameOcr"],
                executionPlan: new MaaResourceExecutionPlan(
                    "operatorsFull",
                    "オペレーター",
                    "profile preset",
                    ["RhodesOperatorNameOcr"],
                    [
                        new MaaResourceTaskPreview("RhodesOperatorNameOcr", "オペレーター名", "", ["operatorsFull"], "generated"),
                    ],
                    "",
                    MaaResourceExecutionPlan.ReadyState),
                contract: new MaaResourceContractSnapshot(true, 43, 7, 7, []),
                frameId: "frame-native",
                frameMetadataPath: "O:/debug/frame-native.json",
                stateSnapshotPath: "O:/debug/frame-native-state.json"));
        File.WriteAllText(Path.Combine(directory, "recognition-broken.json"), "{");

        var history = RhodesRecognitionScanHistory.LoadRecent(directory, limit: 8);

        Equal(2, history.Count, "history count");
        Equal("operatorsFull", history[0].ProfileId, "newest profile");
        Equal("オペレーター", history[0].ProfileLabel, "newest profile label");
        Equal("suki-maa-native", history[0].Source, "native source");
        Equal(1, history[0].CandidateCount, "native candidate count");
        Equal(1, history[0].ResourceTaskCount, "native task count");
        Equal(1, history[0].PresetTaskCount, "native preset task count");
        Equal(MaaResourceExecutionPlan.ReadyState, history[0].ExecutionPlanState, "native execution plan state");
        Equal("実行可能", history[0].ExecutionPlanStateLabel, "native execution plan label");
        Equal("profile preset", history[0].ExecutionPlanSource, "native execution plan source");
        Equal(1, history[0].ExecutionPlanTaskCount, "native execution plan task count");
        Equal(true, history[0].Summary.Contains("plan=実行可能/profile preset/tasks=1", StringComparison.Ordinal), "native summary execution plan");
        Equal("OK", history[0].ContractState, "native contract state");
        Equal("OK task=43 group=7 preset=7", history[0].ContractSummary, "native contract summary");
        Equal("interface.json / resource/base/pipeline", history[0].ContractDetail, "native contract detail");
        Equal(0, history[0].ContractErrorCount, "native contract error count");
        Equal(true, history[0].Summary.Contains("contract=OK/OK task=43 group=7 preset=7", StringComparison.Ordinal), "native summary contract");
        Equal(true, history[0].Detail.Contains("interface.json / resource/base/pipeline", StringComparison.Ordinal), "native detail contract");
        Equal("秘宝スキャン", history[1].DisplayProfile, "api profile label");
        Equal(1, history[1].CandidateCount, "api candidate count");
        Equal(4, history[1].LogCount, "api log count");
        Equal("", history[1].ExecutionPlanSummary, "api execution plan omitted");
        Equal("", history[1].ContractStatusSummary, "api contract omitted");

        var nativePayload = RhodesRecognitionScanHistory.LoadPayload(history[0].LogPath);
        Equal(true, nativePayload.Succeeded, "native payload succeeded");
        Equal("operatorsFull", nativePayload.ProfileId, "native payload profile id");
        Equal("オペレーター", nativePayload.ProfileLabel, "native payload profile label");
        Equal("suki-maa-native", nativePayload.Source, "native payload source");
        Equal("completed", nativePayload.Status, "native payload status");
        Equal("frame-native", nativePayload.FrameId, "native payload frame id");
        Equal("O:/debug/frame-native.json", nativePayload.FrameMetadataPath, "native payload frame metadata path");
        Equal("O:/debug/frame-native-state.json", nativePayload.StateSnapshotPath, "native payload state snapshot path");
        Equal(1, nativePayload.Candidates.Count, "native payload candidates");
        Equal(1, nativePayload.TaskResults.Count, "native payload task results");
        Equal(2, nativePayload.LogRows.Count, "native payload log rows");
        Equal("capture", nativePayload.LogRows[0].DisplayName, "native payload capture event");
        Equal("O:/debug/native-shot.png", nativePayload.LogRows[0].Path, "native payload capture path");
        Equal(true, nativePayload.LogRows[0].HasImagePath, "native payload capture image path");
        Equal("maa-task", nativePayload.LogRows[1].DisplayName, "native payload log event");
        Equal("RhodesOcrRegion_operator_name", nativePayload.LogRows[1].Entry, "native payload log entry");
        Equal("グム", nativePayload.Candidates[0].Label, "native payload candidate label");
        Equal("O:/debug/native-shot.png", nativePayload.FirstImagePath, "native payload first image path");

        var apiPayload = RhodesRecognitionScanHistory.LoadPayload(history[1].LogPath);
        Equal(true, apiPayload.Succeeded, "api payload succeeded");
        Equal("relicsFull", apiPayload.ProfileId, "api payload profile id");
        Equal("秘宝スキャン", apiPayload.ProfileLabel, "api payload profile label");
        Equal("adb", apiPayload.Source, "api payload source");
        Equal("completed", apiPayload.Status, "api payload status");
        Equal(1, apiPayload.Candidates.Count, "api payload candidates");
        Equal(0, apiPayload.TaskResults.Count, "api payload task results");
        Equal(2, apiPayload.LogRows.Count, "api payload log rows");
        Equal("capture", apiPayload.LogRows[0].DisplayName, "api payload log event");
        Equal("O:/debug/shot.png", apiPayload.LogRows[0].Path, "api payload log path");
        Equal(true, apiPayload.LogRows[0].HasImagePath, "api payload image path");
        Equal("O:/debug/shot.png", apiPayload.FirstImagePath, "api payload first image path");
    }
    finally
    {
        Directory.Delete(directory, recursive: true);
    }
}

static void BugReportBundleCollectsDebugArtifacts()
{
    var root = Path.Combine(Path.GetTempPath(), $"rhodes-suki-bug-report-{Guid.NewGuid():N}");
    var debugRoot = Path.Combine(root, RhodesSukiDebugPaths.DebugLogDirectoryName);
    var destination = Path.Combine(debugRoot, RhodesSukiDebugPaths.BugReportsDirectoryName);
    var frameDirectory = Path.Combine(debugRoot, RhodesSukiDebugPaths.FrameRecordsDirectoryName);
    var recognitionDirectory = Path.Combine(debugRoot, RhodesSukiDebugPaths.RecognitionScansDirectoryName);
    var roiDraftDirectory = Path.Combine(debugRoot, RhodesSukiDebugPaths.RoiDraftsDirectoryName);
    var roiSessionDirectory = Path.Combine(debugRoot, RhodesSukiDebugPaths.RoiSessionsDirectoryName);
    var glmDirectory = Path.Combine(debugRoot, "glm-ocr-runtime");
    var nodeDirectory = Path.Combine(debugRoot, RhodesNodeRuntimeManager.RuntimeDirectoryName);
    var oldBugReportDirectory = Path.Combine(debugRoot, RhodesSukiDebugPaths.BugReportsDirectoryName);
    Directory.CreateDirectory(recognitionDirectory);
    Directory.CreateDirectory(roiDraftDirectory);
    Directory.CreateDirectory(roiSessionDirectory);
    Directory.CreateDirectory(glmDirectory);
    Directory.CreateDirectory(nodeDirectory);
    Directory.CreateDirectory(oldBugReportDirectory);

    try
    {
        Directory.CreateDirectory(frameDirectory);
        var recognitionLogPath = Path.Combine(recognitionDirectory, "recognition-2026-07-01T00-00-00-000Z-runStatusFull-test.json");
        var capturePath = Path.Combine(debugRoot, "adb-capture.png");
        var statePath = Path.Combine(root, "current-state.json");
        var settingsPath = Path.Combine(root, "suki-settings.json");
        File.WriteAllText(Path.Combine(debugRoot, "main.log"), "main log");
        File.WriteAllText(Path.Combine(frameDirectory, "frame-test.json"), """{ "schemaVersion": 1, "frameId": "test" }""");
        File.WriteAllBytes(Path.Combine(frameDirectory, "frame-test.png"), [137, 80, 78, 71]);
        File.WriteAllText(Path.Combine(frameDirectory, "frame-test-state.json"), """{ "version": 1 }""");
        File.WriteAllText(recognitionLogPath, """{ "schemaVersion": 1, "profileId": "runStatusFull" }""");
        File.WriteAllText(Path.Combine(roiDraftDirectory, "draft.json"), """{ "kind": "roi-draft" }""");
        File.WriteAllText(Path.Combine(roiSessionDirectory, "session.json"), """{ "kind": "roi-session" }""");
        File.WriteAllBytes(capturePath, [137, 80, 78, 71]);
        File.WriteAllText(statePath, """{ "version": 1, "run": { "campaignId": "is5_sarkaz" } }""");
        File.WriteAllText(settingsPath, """{ "AdbSerial": "127.0.0.1:16384" }""");
        File.WriteAllText(Path.Combine(glmDirectory, "model.bin"), "model");
        File.WriteAllText(Path.Combine(nodeDirectory, "node.exe"), "node");
        File.WriteAllText(Path.Combine(debugRoot, "native.dll"), "native");
        File.WriteAllText(Path.Combine(oldBugReportDirectory, "old.zip"), "old");

        var result = RhodesBugReportBundle.CreateAsync(new RhodesBugReportBundleRequest
        {
            DebugLogDirectory = debugRoot,
            DestinationDirectory = destination,
            StatePath = statePath,
            SettingsPath = settingsPath,
            LatestCapturePath = capturePath,
            LatestRecognitionLogPath = recognitionLogPath,
            Metadata = new Dictionary<string, string>
            {
                ["adbPreset"] = "MuMu Player",
                ["adbSerial"] = "127.0.0.1:16384",
                ["profileId"] = "runStatusFull",
            },
            Now = DateTimeOffset.Parse("2026-07-01T00:00:00Z"),
        }).GetAwaiter().GetResult();

        Equal(true, result.Success, "bug report success");
        Equal(true, File.Exists(result.ZipPath), "bug report zip exists");
        Equal(true, result.IncludedEntries.Contains("debug/main.log"), "main log included");
        Equal(true, result.IncludedEntries.Contains("debug/Frame Records/frame-test.json"), "frame metadata included");
        Equal(true, result.IncludedEntries.Contains("debug/Frame Records/frame-test.png"), "frame image included");
        Equal(true, result.IncludedEntries.Contains("debug/Frame Records/frame-test-state.json"), "frame state snapshot included");
        Equal(true, result.IncludedEntries.Contains("debug/Recognition Scans/recognition-2026-07-01T00-00-00-000Z-runStatusFull-test.json"), "recognition log included");
        Equal(true, result.IncludedEntries.Contains("debug/ROI Drafts/draft.json"), "roi draft included");
        Equal(true, result.IncludedEntries.Contains("debug/ROI Sessions/session.json"), "roi session included");
        Equal(true, result.IncludedEntries.Contains("state/current-state.json"), "state included");
        Equal(true, result.IncludedEntries.Contains("state/suki-settings.json"), "settings included");
        Equal(true, result.IncludedEntries.Contains("resource/interface.json"), "interface included");
        Equal(true, result.IncludedEntries.Contains("resource/pipeline/rhodes.json"), "manual pipeline included");
        Equal(true, result.IncludedEntries.Contains("resource/pipeline/rhodes-generated.json"), "generated pipeline included");
        Equal(true, result.IncludedEntries.Contains("resource/recognition/maa-tasks.json"), "maa tasks included");
        Equal(true, result.IncludedEntries.Contains("resource/recognition/scan-profiles.json"), "scan profiles included");
        Equal(true, result.SkippedEntries.Any(entry => entry.Path.EndsWith("glm-ocr-runtime", StringComparison.OrdinalIgnoreCase)), "glm runtime skipped");
        Equal(true, result.SkippedEntries.Any(entry => entry.Path.EndsWith(RhodesNodeRuntimeManager.RuntimeDirectoryName, StringComparison.OrdinalIgnoreCase)), "node runtime skipped");
        Equal(true, result.SkippedEntries.Any(entry => entry.Path.EndsWith("native.dll", StringComparison.OrdinalIgnoreCase)), "dll skipped");

        using var archive = ZipFile.OpenRead(result.ZipPath);
        var entries = archive.Entries.Select(entry => entry.FullName).ToArray();
        Equal(true, entries.Contains("manifest.json"), "manifest included");
        Equal(true, entries.Contains("README.txt"), "readme included");
        Equal(true, entries.Contains("resource/interface.json"), "interface zip entry");
        Equal(true, entries.Contains("resource/pipeline/rhodes.json"), "manual pipeline zip entry");
        Equal(true, entries.Contains("resource/pipeline/rhodes-generated.json"), "generated pipeline zip entry");
        Equal(false, entries.Any(entry => entry.Contains("old.zip", StringComparison.OrdinalIgnoreCase)), "old zip excluded");
        Equal(false, entries.Any(entry => entry.Contains("glm-ocr-runtime", StringComparison.OrdinalIgnoreCase)), "glm runtime excluded from zip");
        Equal(false, entries.Any(entry => entry.Contains(RhodesNodeRuntimeManager.RuntimeDirectoryName, StringComparison.OrdinalIgnoreCase)), "node runtime excluded from zip");
        Equal(false, entries.Any(entry => entry.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)), "dll excluded from zip");

        var manifestEntry = archive.GetEntry("manifest.json") ?? throw new InvalidOperationException("manifest missing");
        using var manifestReader = new StreamReader(manifestEntry.Open());
        var manifest = JsonNode.Parse(manifestReader.ReadToEnd())!.AsObject();
        Equal("Avalonia/Suki", manifest["distributionShell"]!.GetValue<string>(), "distribution shell");
        Equal("MAA-OCR", manifest["ocrDefault"]!.GetValue<string>(), "default OCR");
        Equal(true, !string.IsNullOrWhiteSpace(manifest["dotnetRuntime"]!.GetValue<string>()), "dotnet runtime manifest");
        Equal(true, !string.IsNullOrWhiteSpace(manifest["currentCulture"]!.GetValue<string>()), "culture manifest");
        var resourceHashes = manifest["resourceHashes"]!.AsObject();
        Equal(true, resourceHashes["resource/interface.json"]!.GetValue<string>().Length >= 16, "interface hash");
        Equal(true, resourceHashes["resource/pipeline/rhodes.json"]!.GetValue<string>().Length >= 16, "manual pipeline hash");
        Equal(true, resourceHashes["resource/pipeline/rhodes-generated.json"]!.GetValue<string>().Length >= 16, "generated pipeline hash");
        Equal("is5_sarkaz", manifest["publicDebugCampaign"]!.GetValue<string>(), "public debug campaign");
        var publicDebugProfiles = manifest["publicDebugProfiles"]!.AsArray()
            .Select(node => node!.GetValue<string>())
            .ToArray();
        Equal("runStatusFull|operatorsFull|relicsFull|is5ThoughtFull|is5AgeFull", string.Join("|", publicDebugProfiles), "public debug profiles");
        Equal("MuMu Player", manifest["context.adbPreset"]!.GetValue<string>(), "manifest adb preset");
        Equal("127.0.0.1:16384", manifest["context.adbSerial"]!.GetValue<string>(), "manifest adb serial");
        var retainedTargets = manifest["retainedRecognitionTargets"]!.AsArray()
            .Select(node => node!.GetValue<string>())
            .ToArray();
        Equal(true, retainedTargets.Contains("operators"), "operators retained");
        Equal(true, retainedTargets.Contains("relics"), "relics retained");
        Equal(false, retainedTargets.Contains("hope"), "hope not retained");
        var abandonedFields = manifest["abandonedRunFields"]!.AsArray()
            .Select(node => node!.GetValue<string>())
            .ToArray();
        Equal(true, abandonedFields.Contains("hope"), "hope abandoned");
        Equal(true, abandonedFields.Contains("shield"), "shield abandoned");
        Equal(true, abandonedFields.Contains("commandLevel"), "command level abandoned");
    }
    finally
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }
}

static void RecognitionFrameRecordStoreSavesFrame()
{
    var root = Path.Combine(Path.GetTempPath(), $"rhodes-suki-frame-record-{Guid.NewGuid():N}");
    var frameDirectory = Path.Combine(root, RhodesSukiDebugPaths.FrameRecordsDirectoryName);
    var statePath = Path.Combine(root, "current-state.json");
    var now = DateTimeOffset.Parse("2026-07-01T00:00:00Z");
    Directory.CreateDirectory(root);

    try
    {
        File.WriteAllText(statePath, """{ "version": 1, "run": { "campaignId": "is5_sarkaz", "ingot": 20 } }""");
        var frame = RhodesFrameRecordStore.SaveAsync(new RhodesFrameRecordRequest
        {
            FrameDirectory = frameDirectory,
            EncodedImage = [137, 80, 78, 71],
            StatePath = statePath,
            ProfileId = "runStatusFull",
            ProfileLabel = "基礎情報",
            Source = "maa-native",
            AppVersion = "test-version",
            RuntimeSummary = "MuMu / 127.0.0.1:16384",
            Now = now,
            RetentionLimit = 1,
        }).GetAwaiter().GetResult();

        Equal(true, frame.Succeeded, "frame record success");
        Equal(true, File.Exists(frame.ImagePath), "frame image exists");
        Equal(true, File.Exists(frame.MetadataPath), "frame metadata exists");
        Equal(true, File.Exists(frame.StateSnapshotPath), "frame state snapshot exists");
        Equal(true, frame.FrameId.StartsWith("20260701-000000-000-", StringComparison.Ordinal), "frame id timestamp prefix");

        var metadata = JsonNode.Parse(File.ReadAllText(frame.MetadataPath))!.AsObject();
        Equal(1, metadata["schemaVersion"]!.GetValue<int>(), "frame metadata schema");
        Equal(frame.FrameId, metadata["frameId"]!.GetValue<string>(), "metadata frame id");
        Equal("runStatusFull", metadata["profileId"]!.GetValue<string>(), "metadata profile id");
        Equal("基礎情報", metadata["profileLabel"]!.GetValue<string>(), "metadata profile label");
        Equal("maa-native", metadata["source"]!.GetValue<string>(), "metadata source");
        Equal("MuMu / 127.0.0.1:16384", metadata["runtimeSummary"]!.GetValue<string>(), "metadata runtime");
        Equal(frame.ImagePath, metadata["imagePath"]!.GetValue<string>(), "metadata image path");
        Equal(frame.StateSnapshotPath, metadata["stateSnapshotPath"]!.GetValue<string>(), "metadata state snapshot path");

        var second = RhodesFrameRecordStore.SaveAsync(new RhodesFrameRecordRequest
        {
            FrameDirectory = frameDirectory,
            EncodedImage = [137, 80, 78, 71],
            StatePath = statePath,
            ProfileId = "operatorsFull",
            Now = now.AddSeconds(1),
            RetentionLimit = 1,
        }).GetAwaiter().GetResult();

        Equal(true, File.Exists(second.MetadataPath), "second frame metadata exists");
        Equal(false, File.Exists(frame.MetadataPath), "old frame metadata pruned");
        Equal(false, File.Exists(frame.ImagePath), "old frame image pruned");
    }
    finally
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }
}

static void FrameRecordHistoryLoadsRecent()
{
    var root = Path.Combine(Path.GetTempPath(), $"rhodes-frame-history-{Guid.NewGuid():N}");
    var frameDirectory = Path.Combine(root, RhodesSukiDebugPaths.FrameRecordsDirectoryName);
    var statePath = Path.Combine(root, "current-state.json");
    try
    {
        Directory.CreateDirectory(root);
        File.WriteAllText(statePath, """{"campaign":"is5"}""");
        var older = RhodesFrameRecordStore.SaveAsync(new RhodesFrameRecordRequest
        {
            FrameDirectory = frameDirectory,
            EncodedImage = [1, 2, 3],
            StatePath = statePath,
            FrameId = "older",
            ProfileId = "runStatusFull",
            ProfileLabel = "ラン基本値",
            Source = "adb",
            AppVersion = "test",
            RuntimeSummary = "MuMu / 1280x720",
            Now = DateTimeOffset.Parse("2026-07-04T01:02:03Z"),
            RetentionLimit = 8,
        }).GetAwaiter().GetResult();
        Equal(true, older.Succeeded, "older frame saved");

        var newer = RhodesFrameRecordStore.SaveAsync(new RhodesFrameRecordRequest
        {
            FrameDirectory = frameDirectory,
            EncodedImage = [4, 5, 6],
            FrameId = "newer",
            ProfileId = "operatorsFull",
            ProfileLabel = "オペレーター",
            Source = "disk",
            AppVersion = "test",
            RuntimeSummary = "replay",
            Now = DateTimeOffset.Parse("2026-07-04T02:03:04Z"),
            RetentionLimit = 8,
        }).GetAwaiter().GetResult();
        Equal(true, newer.Succeeded, "newer frame saved");

        File.WriteAllText(Path.Combine(frameDirectory, "frame-broken.json"), "{broken");
        var history = RhodesFrameRecordHistory.LoadRecent(frameDirectory, limit: 4);

        Equal(2, history.Count, "history count ignores broken metadata");
        Equal("newer", history[0].FrameId, "newer first");
        Equal("operatorsFull", history[0].ProfileId, "profile id");
        Equal("オペレーター", history[0].ProfileLabel, "profile label");
        Equal("disk", history[0].Source, "source");
        Equal(true, history[0].ImagePath.EndsWith("frame-newer.png", StringComparison.OrdinalIgnoreCase), "image path");
        Equal(true, history[0].Detail.Contains("replay", StringComparison.Ordinal), "runtime detail");
        Equal("older", history[1].FrameId, "older second");
        Equal(true, history[1].StateSnapshotPath.EndsWith("frame-older-state.json", StringComparison.OrdinalIgnoreCase), "state snapshot");
        Equal(true, File.Exists(history[1].StateSnapshotPath), "state snapshot exists");
    }
    finally
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }
}

static void BugReportImportExtractsReplayFrames()
{
    var root = Path.Combine(Path.GetTempPath(), $"rhodes-bug-import-{Guid.NewGuid():N}");
    var sourceRoot = Path.Combine(root, "source");
    var debugRoot = Path.Combine(sourceRoot, "debug");
    var frameDirectory = Path.Combine(debugRoot, RhodesSukiDebugPaths.FrameRecordsDirectoryName);
    var importDestination = Path.Combine(root, "imports");
    var zipPath = Path.Combine(root, "report.zip");
    var statePath = Path.Combine(root, "current-state.json");

    try
    {
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(frameDirectory);
        File.WriteAllText(statePath, """{"run":{"campaignId":"is5_sarkaz"}}""");
        var frame = RhodesFrameRecordStore.SaveAsync(new RhodesFrameRecordRequest
        {
            FrameDirectory = frameDirectory,
            EncodedImage = [137, 80, 78, 71],
            StatePath = statePath,
            FrameId = "zip-frame",
            ProfileId = "runStatusFull",
            ProfileLabel = "基礎情報",
            Source = "test",
            RuntimeSummary = "MuMu / 1280x720",
            Now = DateTimeOffset.Parse("2026-07-07T00:00:00Z"),
            RetentionLimit = 8,
        }).GetAwaiter().GetResult();
        Equal(true, frame.Succeeded, "source frame saved");
        File.WriteAllText(Path.Combine(sourceRoot, "manifest.json"), """{"schemaVersion":1}""");
        ZipFile.CreateFromDirectory(sourceRoot, zipPath);

        var imported = RhodesBugReportImport.ImportAsync(
            zipPath,
            importDestination,
            DateTimeOffset.Parse("2026-07-07T00:01:00Z")).GetAwaiter().GetResult();

        Equal(true, imported.Success, "zip import success");
        Equal(true, Directory.Exists(imported.FrameRecordsDirectory), "imported frame directory exists");
        Equal(true, File.Exists(imported.ManifestPath), "manifest path exists");
        Equal(1, imported.FrameCount, "imported frame count");
        var history = RhodesFrameRecordHistory.LoadRecent(imported.FrameRecordsDirectory);
        Equal(1, history.Count, "history count");
        Equal("zip-frame", history[0].FrameId, "imported frame id");
        Equal("runStatusFull", history[0].ProfileId, "imported profile id");

        var folderImport = RhodesBugReportImport.ImportAsync(sourceRoot, importDestination).GetAwaiter().GetResult();
        Equal(true, folderImport.Success, "folder import success");
        Equal(frameDirectory, folderImport.FrameRecordsDirectory, "folder frame directory");
    }
    finally
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }
}

static void EvidencePreviewTreeUsesCompactTypedNodes()
{
    const string json = """
    {
      "profileId": "operatorsFull",
      "source": "suki-maa-native",
      "status": "completed",
      "counts": { "candidates": 1, "resourceTasks": 1, "log": 1 },
      "candidates": [
        { "kind": "operator", "label": "グム", "value": "gummy", "operatorId": "gummy", "confidence": 0.91 }
      ],
      "evidence": {
        "profile": {
          "id": "operatorsFull",
          "label": "オペレーター",
          "presetTaskEntries": ["RhodesOcrRegion_operator_name"],
          "executionPlan": {
            "state": "ready",
            "stateLabel": "実行可能",
            "canRun": true,
            "source": "profile preset",
            "taskCount": 1,
            "taskEntries": ["RhodesOcrRegion_operator_name"],
            "error": ""
          }
        },
        "contract": {
          "isValid": true,
          "state": "OK",
          "taskCount": 43,
          "groupCount": 7,
          "presetCount": 7,
          "summary": "OK task=43 group=7 preset=7",
          "detail": "interface.json / resource/base/pipeline",
          "errors": []
        },
        "taskResults": [
          {
            "entry": "RhodesOcrRegion_operator_name",
            "status": "Succeeded",
            "hit": true,
            "detail": "ocr detail",
            "recognitionDetailJson": "{}",
            "algorithm": "OCR"
          }
        ]
      },
      "log": [
        { "event": "maa-task", "entry": "RhodesOcrRegion_operator_name", "status": "Succeeded" }
      ]
    }
    """;

    var buildMethod = typeof(MainWindowViewModel).GetMethod(
        "BuildRoiRescanEvidencePreviewNodes",
        BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("BuildRoiRescanEvidencePreviewNodes not found");
    var nodes = (IReadOnlyList<MaaEvidencePreviewNode>)buildMethod.Invoke(
        null,
        [json, null, true, true, true])!;

    Equal("summary", nodes[0].NodeKind, "summary node kind");
    Equal(true, nodes[0].HasVisibleDetail, "summary detail visible");
    Equal(true, nodes[0].PreviewText.Contains("executionPlan: 実行可能 / profile preset / tasks=1 / canRun=true", StringComparison.Ordinal), "summary execution plan");
    Equal(true, nodes[0].PreviewText.Contains("contract: OK / OK task=43 group=7 preset=7 / interface.json / resource/base/pipeline", StringComparison.Ordinal), "summary contract");

    var contract = nodes.Single(node => node.NodeKind == "contract");
    Equal("Contract · OK", contract.Title, "contract node title");
    Equal(true, contract.HasVisibleDetail, "contract node detail visible");
    Equal(true, contract.Detail.Contains("OK task=43 group=7 preset=7", StringComparison.Ordinal), "contract node summary");
    Equal(true, contract.PreviewText.Contains("\"detail\": \"interface.json / resource/base/pipeline\"", StringComparison.Ordinal), "contract node raw detail");

    var candidates = nodes.Single(node => node.Title.StartsWith("Candidates", StringComparison.Ordinal));
    Equal("section", candidates.NodeKind, "candidate section kind");
    Equal("Candidates · 1", candidates.Title, "candidate section title");
    Equal("1", candidates.CountLabel, "candidate section count");
    Equal(false, candidates.HasVisibleDetail, "candidate section detail hidden");

    var candidate = candidates.SafeChildren.Single();
    Equal("candidate", candidate.NodeKind, "candidate node kind");
    Equal("", candidate.CountLabel, "leaf count hidden");
    Equal(true, candidate.HasVisibleDetail, "candidate detail visible");
    Equal("operator:gummy", candidate.CandidateKey, "candidate key");

    var defaultMethod = typeof(MainWindowViewModel).GetMethod(
        "DefaultEvidencePreviewNode",
        BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("DefaultEvidencePreviewNode not found");
    var summaryDefault = (MaaEvidencePreviewNode?)defaultMethod.Invoke(null, [nodes, null]);
    Equal("summary", summaryDefault?.NodeKind, "default summary node");

    var selectedDefault = (MaaEvidencePreviewNode?)defaultMethod.Invoke(
        null,
        [
            nodes,
            new MaaRoiRescanComparisonRow(
                "added",
                "グム",
                "",
                "グム",
                "",
                CandidateKey: "operator:gummy")
        ]);
    Equal("candidate", selectedDefault?.NodeKind, "default selected candidate node");
    Equal("operator:gummy", selectedDefault?.CandidateKey, "default selected candidate key");
}

static void ResourceTaskSummary()
{
    var manual = new MaaResourceTaskPreview("ManualTask", "Manual", "manual purpose");
    var generated = new MaaResourceTaskPreview(
        "GeneratedTask",
        "Generated",
        "generated purpose",
        ["runStatusFull", "operatorsFull"],
        "maa-tasks.ocrRegions");

    Equal("source: manual", manual.SourceSummary, "manual source");
    Equal("profiles: manual", manual.ProfileSummary, "manual profiles");
    Equal("source: maa-tasks.ocrRegions", generated.SourceSummary, "generated source");
    Equal("profiles: runStatusFull, operatorsFull", generated.ProfileSummary, "generated profiles");
}

static void ResourceCatalogReadsPipelineNodes()
{
    var tasks = RhodesMaaResourceCatalog.DefaultTasks();
    var entries = tasks.Select(task => task.Entry).ToHashSet(StringComparer.Ordinal);
    var pipelineDirectory = Path.Combine(AppContext.BaseDirectory, "resource", "base", "pipeline");
    var expectedEntries = PipelineEntries(Path.Combine(pipelineDirectory, "rhodes.json"))
        .Concat(PipelineEntries(Path.Combine(pipelineDirectory, "rhodes-generated.json")))
        .Where(entry => !entry.EndsWith("Empty", StringComparison.Ordinal))
        .Distinct(StringComparer.Ordinal)
        .ToArray();

    Equal(expectedEntries.Length, entries.Count, "resource catalog task count");
    foreach (var entry in expectedEntries)
        Equal(true, entries.Contains(entry), $"resource catalog contains {entry}");
    Equal(false, entries.Contains("RhodesEmpty"), "manual empty omitted");
    Equal(false, entries.Contains("RhodesGeneratedEmpty"), "generated empty omitted");
    var abandonedTokens = new[] { "hope", "maxhope", "life", "lifepoints", "shield", "command", "commandlevel" };
    Equal(false, tasks.Any(task => abandonedTokens.Any(token => NormalizeTaskEntry(task.Entry).Contains(token, StringComparison.Ordinal))),
        "abandoned run values omitted from resource catalog");

    var probe = tasks.Single(task => task.Entry == "RhodesProbe");
    Equal("Probe", probe.Label, "probe label");
    Equal("source: resource/base/pipeline/rhodes.json", probe.SourceSummary, "probe source");

    var idea = tasks.Single(task => task.Entry == "RhodesRunStatusIdeaIcon");
    Equal("profiles: runStatusFull", idea.ProfileSummary, "idea profile");
    Equal("構想値の基準点になるアイコンTemplateMatchです。 / TemplateMatch", idea.Purpose, "idea purpose from interface");

    var operatorOcr = tasks.Single(task => task.Entry == "RhodesOperatorNameOcr");
    Equal("招集カード領域をMAA-OCRで読ませます。 / OCR", operatorOcr.Purpose, "operator OCR purpose from interface");

    var generated = tasks.Single(task => task.Entry == "RhodesTemplate_operatorsFull_operator_card_name");
    Equal("scan-profiles.templateOcrRegions", generated.Source, "generated source");
    Equal("profiles: operatorsFull", generated.ProfileSummary, "generated card profile");
}

static void ResourceCatalogExportsReplayPayloads()
{
    var manualPayload = RhodesMaaResourceCatalog.LoadRecognitionPayloadJson("RhodesOperatorNameOcr");
    Equal(true, !string.IsNullOrWhiteSpace(manualPayload), "manual replay payload exported");
    var manual = JsonNode.Parse(manualPayload)!.AsObject();
    Equal("OCR", manual["recognition"]!.GetValue<string>(), "manual recognition type");
    Equal(true, manual.ContainsKey("roi"), "manual roi retained");
    Equal(false, manual.ContainsKey("action"), "manual action stripped");
    Equal(false, manual.ContainsKey("attach"), "manual attach stripped");

    var generatedPayload = RhodesMaaResourceCatalog.LoadRecognitionPayloadJson("RhodesScreen_run_squad_info_panel");
    Equal(true, !string.IsNullOrWhiteSpace(generatedPayload), "generated replay payload exported");
    var generated = JsonNode.Parse(generatedPayload)!.AsObject();
    Equal("OCR", generated["recognition"]!.GetValue<string>(), "generated recognition type");
    Equal(true, generated.ContainsKey("roi"), "generated roi retained");
    Equal(false, generated.ContainsKey("attach"), "generated attach stripped");
    Equal(3, RhodesMaaResourceCatalog.LoadRecognitionScale("RhodesOcrRegion_operator_list_text"), "generated OCR scale exported");
    Equal(1, RhodesMaaResourceCatalog.LoadRecognitionScale("RhodesMissingEntry"), "missing scale defaults to one");

    Equal("", RhodesMaaResourceCatalog.LoadRecognitionPayloadJson("RhodesMissingEntry"), "missing payload empty");
    Equal("", RhodesMaaResourceCatalog.LoadRecognitionPayloadJson(""), "blank payload empty");
}

static void ResourceCatalogValidatesInterfaceContract()
{
    var contract = RhodesMaaResourceCatalog.ValidateContract();

    Equal(true, contract.IsValid, "resource contract valid");
    Equal("OK", contract.State, "resource contract state");
    Equal(true, contract.TaskCount > 0, "resource contract task count");
    Equal(true, contract.GroupCount >= 7, "resource contract group count");
    Equal(true, contract.PresetCount >= 7, "resource contract preset count");
    Equal(0, contract.Errors.Count, "resource contract errors");
    Equal(true, contract.Summary.Contains("task=", StringComparison.Ordinal), "resource contract summary");
    Equal(true, contract.Detail.Contains("interface.json", StringComparison.Ordinal), "resource contract detail");
}

static void ResourceProfileOrder()
{
    var tasks = new[]
    {
        new MaaResourceTaskPreview("relic", "秘宝", "", ["relicsFull"]),
        new MaaResourceTaskPreview("age", "時代", "", ["is5AgeFull"]),
        new MaaResourceTaskPreview("operator", "オペレーター", "", ["operatorsFull"]),
        new MaaResourceTaskPreview("unknown", "将来追加", "", ["futureProfile"]),
        new MaaResourceTaskPreview("run", "基礎情報", "", ["runStatusFull"]),
        new MaaResourceTaskPreview("thought", "思案", "", ["is5ThoughtFull"]),
        new MaaResourceTaskPreview("revelation", "啓示", "", ["is4RevelationFull"]),
        new MaaResourceTaskPreview("hallucinations", "幻覚", "", ["is2HallucinationsFull"]),
        new MaaResourceTaskPreview("performance", "演目", "", ["is2PerformanceFull"]),
        new MaaResourceTaskPreview("coins", "通宝", "", ["is6CoinsFull"]),
    };

    var profiles = RhodesMaaResourceCatalog.ProfileGroups(tasks).Select(profile => profile.Id).ToArray();
    Equal("all|runStatusFull|operatorsFull|relicsFull|is4RevelationFull|is5ThoughtFull|is5AgeFull|is2HallucinationsFull|is2PerformanceFull|is6CoinsFull|futureProfile", string.Join("|", profiles), "profile order");
}

static void ResourceProfilesUseInterfaceGroups()
{
    var tasks = RhodesMaaResourceCatalog.DefaultTasks();
    var profiles = RhodesMaaResourceCatalog.ProfileGroups(tasks);
    var runStatus = profiles.Single(profile => profile.Id == "runStatusFull");
    var operators = profiles.Single(profile => profile.Id == "operatorsFull");
    var all = profiles.Single(profile => profile.Id == "all");

    Equal("基礎情報", runStatus.Label, "run status interface label");
    Equal(true, runStatus.Description.Contains("源石錐", StringComparison.Ordinal), "run status interface description");
    Equal(false, runStatus.Description.Contains("希望", StringComparison.Ordinal), "run status omits abandoned target names");
    Equal(false, runStatus.Description.Contains("耐久値", StringComparison.Ordinal), "run status omits abandoned target names");
    Equal(false, runStatus.Description.Contains("シールド", StringComparison.Ordinal), "run status omits abandoned target names");
    Equal(false, runStatus.Description.Contains("指揮Lv", StringComparison.Ordinal), "run status omits abandoned target names");
    Equal(true, runStatus.Description.Contains("maa-recognition-target-policy.json", StringComparison.Ordinal), "run status target policy source");
    Equal("source: interface.json group/preset", runStatus.SourceSummary, "run status interface source");
    Equal(true, (runStatus.TaskEntries ?? []).Contains("RhodesOcrRegion_run_ingot"), "run status preset includes ingot");
    Equal(
        true,
        (runStatus.TaskEntries ?? []).Contains("RhodesTemplate_runStatusFull_run_difficulty_grade_anchor"),
        "run status preset includes anchored difficulty OCR");
    Equal(
        true,
        (runStatus.TaskEntries ?? []).Contains("RhodesTemplate_runStatusFull_run_difficulty_grade_is6_sui_anchor"),
        "run status preset includes anchored Sui difficulty OCR");
    Equal(
        true,
        (runStatus.TaskEntries ?? []).Contains("RhodesTemplate_runStatusFull_run_squad_icon_is5_sarkaz_batch"),
        "run status preset includes batched Sarkaz squad icon templates");
    Equal(
        true,
        (runStatus.TaskEntries ?? []).Contains("RhodesTemplate_runStatusFull_run_squad_icon_is6_sui_batch"),
        "run status preset includes batched Sui squad icon templates");
    Equal(
        false,
        (runStatus.TaskEntries ?? []).Contains("RhodesOcrRegion_run_difficulty_grade"),
        "run status preset excludes the legacy fixed difficulty ROI");
    Equal(
        false,
        (runStatus.TaskEntries ?? []).Contains("RhodesOcrRegion_run_difficulty_block"),
        "run status preset excludes the legacy difficulty block OCR");
    Equal(false, (runStatus.TaskEntries ?? []).Contains("RhodesOperatorNameOcr"), "run status preset excludes operator OCR");
    Equal("オペレーター", operators.Label, "operator interface label");
    Equal(true, operators.Description.Contains("オペレーター", StringComparison.Ordinal), "operator interface description");
    Equal("source: interface.json group/preset", operators.SourceSummary, "operator interface source");
    Equal(true, (operators.TaskEntries ?? []).Contains("RhodesOperatorNameOcr"), "operator preset includes operator OCR");
    Equal(false, (operators.TaskEntries ?? []).Contains("RhodesOcrRegion_run_ingot"), "operator preset excludes ingot");
    Equal("すべて", all.Label, "all label remains local");
    Equal("source: local aggregate", all.SourceSummary, "all source");
}

static void ResourceProfileTaskFilteringFollowsInterfacePresets()
{
    var profile = new MaaResourceProfilePreview(
        "operatorsFull",
        "オペレーター",
        1,
        TaskEntries: ["RhodesOcrRegion_run_ingot"]);
    var ingot = new MaaResourceTaskPreview("RhodesOcrRegion_run_ingot", "源石錐", "", ["runStatusFull"]);
    var operatorName = new MaaResourceTaskPreview("RhodesOperatorNameOcr", "オペレーター", "", ["operatorsFull"]);

    Equal(true, RhodesMaaResourceCatalog.TaskAppliesToProfile(ingot, profile), "preset entry included");
    Equal(false, RhodesMaaResourceCatalog.TaskAppliesToProfile(operatorName, profile), "profile id fallback ignored when preset exists");
    Equal(true, RhodesMaaResourceCatalog.TaskAppliesToProfile(operatorName, (MaaResourceProfilePreview?)null), "null profile shows all");

    var plan = RhodesMaaResourceCatalog.BuildExecutionPlan([operatorName, ingot], profile);
    Equal(true, plan.CanRun, "preset execution plan runnable");
    Equal(MaaResourceExecutionPlan.ReadyState, plan.State, "preset execution plan ready state");
    Equal("RhodesOcrRegion_run_ingot", plan.Tasks.Single().Entry, "preset execution plan follows preset entries");
    Equal("RhodesOcrRegion_run_ingot", plan.TaskEntries.Single(), "preset execution plan records preset entries");

    var allPlan = RhodesMaaResourceCatalog.BuildExecutionPlan([operatorName, ingot], new MaaResourceProfilePreview("all", "すべて", 2));
    Equal(false, allPlan.CanRun, "all profile is display only");
    Equal(MaaResourceExecutionPlan.DisplayOnlyState, allPlan.State, "all profile state");
    Equal(true, allPlan.IsDisplayOnly, "all profile display flag");
    Equal(true, allPlan.Error.Contains("一覧表示用", StringComparison.Ordinal), "all plan explains display only");

    var missingPlan = RhodesMaaResourceCatalog.BuildExecutionPlan(
        [operatorName, ingot],
        profile with { TaskEntries = ["RhodesMissingTask"] });
    Equal(false, missingPlan.CanRun, "missing preset task refuses execution");
    Equal(MaaResourceExecutionPlan.MissingTaskState, missingPlan.State, "missing preset task state");
    Equal(true, missingPlan.Error.Contains("RhodesMissingTask", StringComparison.Ordinal), "missing plan names task");

    var unselectedPlan = RhodesMaaResourceCatalog.BuildExecutionPlan([operatorName, ingot], null);
    Equal(MaaResourceExecutionPlan.UnselectedState, unselectedPlan.State, "unselected profile state");

    var emptyPlan = RhodesMaaResourceCatalog.BuildExecutionPlan(
        [operatorName, ingot],
        new MaaResourceProfilePreview("futureProfile", "将来", 0));
    Equal(MaaResourceExecutionPlan.EmptyState, emptyPlan.State, "empty profile state");
}

static void PublicDebugPolicyRestrictsSarkazScope()
{
    var state = new SukiRunStateSnapshot(
        CampaignId: "is6_sui",
        SelectedOperatorIds: new HashSet<string>(StringComparer.Ordinal),
        SelectedRelicIds: new HashSet<string>(StringComparer.Ordinal),
        ExcludedOperatorIds: new HashSet<string>(StringComparer.Ordinal),
        ExcludedRelicIds: new HashSet<string>(StringComparer.Ordinal),
        OperatorShowSelectedFirst: false,
        OperatorHideExcluded: false,
        OperatorSelectedOnly: false,
        RelicShowSelectedFirst: false,
        RelicHideExcluded: false,
        RelicSelectedOnly: false,
        Squad: "指揮分隊",
        Difficulty: "18",
        Ingot: 20);
    var applied = RhodesPublicDebugPolicy.ApplyCampaign(state);
    Equal("is6_sui", applied.CampaignId, "campaign remains selectable");
    Equal("指揮分隊", applied.Squad, "run data is preserved while campaign changes");

    var campaigns = RhodesPublicDebugPolicy.FilterCampaigns([
        new SukiCampaignPreview("is4_sami", 4, "IS#4", "サーミ", []),
        new SukiCampaignPreview("is5_sarkaz", 5, "IS#5", "サルカズの炉辺奇談", []),
        new SukiCampaignPreview("is6_sui", 6, "IS#6", "歳", []),
    ]);
    Equal("is4_sami|is5_sarkaz|is6_sui", string.Join("|", campaigns.Select(item => item.Id)), "all campaign themes remain selectable");

    var profiles = RhodesPublicDebugPolicy.FilterProfiles([
        new MaaResourceProfilePreview("all", "すべて", 9),
        new MaaResourceProfilePreview("is6CoinsFull", "通宝", 1),
        new MaaResourceProfilePreview("relicsFull", "秘宝", 1),
        new MaaResourceProfilePreview("operatorsFull", "オペレーター", 1),
        new MaaResourceProfilePreview("is5AgeFull", "時代", 1),
        new MaaResourceProfilePreview("is4RevelationFull", "啓示", 1),
        new MaaResourceProfilePreview("runStatusFull", "基礎情報", 1),
        new MaaResourceProfilePreview("is5ThoughtFull", "思案", 1),
        new MaaResourceProfilePreview("is2HallucinationsFull", "幻覚", 1),
        new MaaResourceProfilePreview("is2PerformanceFull", "演目", 1),
    ]);
    Equal(
        "runStatusFull|operatorsFull|relicsFull|is5ThoughtFull|is5AgeFull",
        string.Join("|", profiles.Select(item => item.Id)),
        "public debug profiles are Sarkaz-only and executable");
    Equal(false, profiles.Any(item => item.Id == "all"), "all profile is hidden from public debug runtime");
    Equal(false, profiles.Any(item => item.Id is "is4RevelationFull" or "is6CoinsFull"), "other IS special profiles are hidden");
    Equal(false, profiles.Any(item => item.Id == "is2HallucinationsFull"), "Phantom profile stays hidden from public debug runtime");
    Equal(false, profiles.Any(item => item.Id == "is2PerformanceFull"), "Phantom performance stays hidden from public debug runtime");

    var validationState = RhodesPublicDebugPolicy.ApplyCampaign(state, RhodesDistributionProfile.Validation);
    Equal("is6_sui", validationState.CampaignId, "validation build preserves selected campaign");
    Equal(true, RhodesPublicDebugPolicy.IsCampaignAllowed("is4_sami", RhodesDistributionProfile.Validation), "validation build allows another campaign");
    Equal(true, RhodesPublicDebugPolicy.IsCampaignAllowed("is4_sami", RhodesDistributionProfile.PublicDebug), "public debug allows theme selection");
    Equal(true, RhodesPublicDebugPolicy.IsProfileAllowed("is2HallucinationsFull", RhodesDistributionProfile.Validation), "validation build allows Phantom recognition");
    Equal(false, RhodesPublicDebugPolicy.IsProfileAllowed("is2HallucinationsFull", RhodesDistributionProfile.PublicDebug), "public debug rejects Phantom recognition");
    Equal(true, RhodesPublicDebugPolicy.IsProfileAllowed("is2PerformanceFull", RhodesDistributionProfile.Validation), "validation build allows Phantom performance recognition");
    Equal(false, RhodesPublicDebugPolicy.IsProfileAllowed("is2PerformanceFull", RhodesDistributionProfile.PublicDebug), "public debug rejects Phantom performance recognition");

    var validationCampaigns = RhodesPublicDebugPolicy.FilterCampaigns([
        new SukiCampaignPreview("is4_sami", 4, "IS#4", "サーミ", []),
        new SukiCampaignPreview("is5_sarkaz", 5, "IS#5", "サルカズの炉辺奇談", []),
        new SukiCampaignPreview("is6_sui", 6, "IS#6", "歳", []),
    ], RhodesDistributionProfile.Validation);
    Equal("is4_sami|is5_sarkaz|is6_sui", string.Join("|", validationCampaigns.Select(item => item.Id)), "validation build exposes every campaign");

    var validationProfiles = RhodesPublicDebugPolicy.FilterProfiles([
        new MaaResourceProfilePreview("runStatusFull", "基礎情報", 1),
        new MaaResourceProfilePreview("is4RevelationFull", "啓示", 1),
        new MaaResourceProfilePreview("is5ThoughtFull", "思案", 1),
        new MaaResourceProfilePreview("is6CoinsFull", "通宝", 1),
    ], RhodesDistributionProfile.Validation);
    Equal(
        "runStatusFull|is4RevelationFull|is5ThoughtFull|is6CoinsFull",
        string.Join("|", validationProfiles.Select(item => item.Id)),
        "validation build exposes profiles for every campaign");
}

static void StartupResetKeepsAdbAndStartsPhantom()
{
    var state = JsonNode.Parse(
        """
        {
          "version": 1,
          "run": {
            "campaignId": "is5_sarkaz",
            "squadId": "is5_sarkaz_squad_01",
            "difficulty": "18",
            "ingot": 42,
            "special": { "is5_sarkaz": { "idea": 7 } }
          },
          "adb": {
            "connectionPreset": "mumu",
            "adbPath": "M:/MuMu/adb.exe",
            "serial": "127.0.0.1:16384"
          },
          "operators": ["gummy"],
          "relics": ["is5_sarkaz_relic_265"],
          "usedRelicIds": ["is5_sarkaz_relic_265"],
          "bossFlags": ["boss-a"],
          "bossSelections": { "is5_sarkaz": { "floor3BossId": "boss-a" } },
          "pendingSuggestions": [{ "type": "relic" }],
          "effectCalculation": { "summary": { "runResources": ["stale"] } },
          "tournament": {
            "pendingState": { "run": { "campaignId": "is5_sarkaz" } },
            "lastSubmissionAt": "2026-07-18T00:00:00Z",
            "submittedBy": "external-json"
          },
          "staleRuntimeValue": true,
          "preferences": {
            "ocrEngine": "maa-ocr",
            "sukiOverlayLayout": [{ "id": "status", "x": 40 }]
          }
        }
        """)!.AsObject();

    RhodesRunStateStore.ApplyStartupReset(
        state,
        DateTimeOffset.Parse("2026-07-19T00:00:00Z"));

    var run = state["run"]!.AsObject();
    Equal("is2_phantom", run["campaignId"]!.GetValue<string>(), "startup campaign");
    Equal(1, run.Count, "startup run contains only campaign id");
    Equal(0, state["operators"]!.AsArray().Count, "startup operators cleared");
    Equal(0, state["relics"]!.AsArray().Count, "startup relics cleared");
    Equal(0, state["usedRelicIds"]!.AsArray().Count, "startup relic usage cleared");
    Equal(0, state["bossFlags"]!.AsArray().Count, "startup boss flags cleared");
    Equal(0, state["bossSelections"]!.AsObject().Count, "startup boss selections cleared");
    Equal(0, state["pendingSuggestions"]!.AsArray().Count, "startup suggestions cleared");
    Equal(false, state.ContainsKey("effectCalculation"), "startup derived effect calculation cleared");
    Equal(false, state.ContainsKey("staleRuntimeValue"), "startup unknown runtime state cleared");
    Equal(true, state["tournament"]!["pendingState"] is null, "startup tournament pending state cleared");
    Equal("casual", state["mode"]!.GetValue<string>(), "startup mode reset");
    Equal("mumu", state["adb"]!["connectionPreset"]!.GetValue<string>(), "ADB preset preserved");
    Equal("127.0.0.1:16384", state["adb"]!["serial"]!.GetValue<string>(), "ADB serial preserved");
    Equal("maa-ocr", state["preferences"]!["ocrEngine"]!.GetValue<string>(), "display preferences preserved");

    var tempRoot = Path.Combine(Path.GetTempPath(), $"rhodes-startup-state-{Guid.NewGuid():N}");
    Directory.CreateDirectory(tempRoot);
    try
    {
        var preferredStatePath = Path.Combine(tempRoot, "current-state.json");
        File.WriteAllText(preferredStatePath, "{}");
        Equal(
            Path.GetFullPath(preferredStatePath),
            Path.GetFullPath(RhodesRunCatalog.ResolveStatePath(tempRoot)),
            "startup state stays beside the selected data root");
    }
    finally
    {
        Directory.Delete(tempRoot, true);
    }
}

static void RunFieldRegistryRetainedFields()
{
    var state = new SukiRunStateSnapshot(
        CampaignId: "is5_sarkaz",
        SelectedOperatorIds: new HashSet<string>(StringComparer.Ordinal),
        SelectedRelicIds: new HashSet<string>(StringComparer.Ordinal),
        ExcludedOperatorIds: new HashSet<string>(StringComparer.Ordinal),
        ExcludedRelicIds: new HashSet<string>(StringComparer.Ordinal),
        OperatorShowSelectedFirst: false,
        OperatorHideExcluded: false,
        OperatorSelectedOnly: false,
        RelicShowSelectedFirst: false,
        RelicHideExcluded: false,
        RelicSelectedOnly: false,
        Squad: "破棘成金分隊",
        SquadRandomEffect: "ブループリント分隊",
        Difficulty: "18",
        Ingot: 13,
        Idea: 3,
        SpecialFields:
        [
            new SukiSpecialFieldState("is5_sarkaz", "thought", "思案", "effectStackLoadout", "2個", "個数入力", "is5ThoughtFull", ""),
            new SukiSpecialFieldState("is5_sarkaz", "idea", "構想", "number", "3", "数値", "run.idea.current", ""),
            new SukiSpecialFieldState("is5_sarkaz", "age", "時代", "effectSelect", "溶魂の端緒", "候補選択", "is5AgeFull", ""),
            new SukiSpecialFieldState("is4_sami", "collapseValue", "崩壊値", "number", "9", "数値", "is4CollapseValue", "")
        ]);

    var header = RhodesRunFieldRegistry.BuildHeaderStatusChips(state).ToArray();
    Equal("源石錐|IS特殊値|等級|分隊", string.Join("|", header.Select(item => item.Label)), "run header field order");
    Equal(true, header.Single(item => item.Label == "IS特殊値").Value.Contains("構想=3", StringComparison.Ordinal), "campaign special value summarized");
    Equal(false, header.Any(item => item.Label == "構想"), "idea is not split into a separate header chip");

    var previews = RhodesRunFieldRegistry.BuildRunFieldPreviews(state).ToArray();
    Equal("源石錐|等級|分隊|IS特殊値", string.Join("|", previews.Select(item => item.Label)), "run preview field order");
    Equal(true, previews.Single(item => item.Label == "IS特殊値").Value.Contains("時代=溶魂の端緒", StringComparison.Ordinal), "only current campaign special fields are summarized");
    Equal(false, previews.Single(item => item.Label == "IS特殊値").Value.Contains("崩壊値", StringComparison.Ordinal), "other campaign special fields are not summarized");
    Equal(false, previews.Any(item => item.Label is "希望" or "耐久値" or "シールド" or "指揮Lv"), "abandoned run fields are not surfaced");

    var mizukiState = state with
    {
        CampaignId = "is3_mizuki",
        SpecialFields =
        [
            new SukiSpecialFieldState("is3_mizuki", "key", "鍵", "number", "2", "数値", "run.key.current", ""),
            new SukiSpecialFieldState("is3_mizuki", "light", "灯火", "number", "30", "数値", "run.light.current", ""),
            new SukiSpecialFieldState(
                "is3_mizuki",
                "rejectionReaction",
                "拒絶反応",
                "operatorEffectAssignment",
                "造血障害",
                "対象指定",
                "is3RejectionFull",
                "造血障害 / 対象3名",
                EffectId: "is3_mizuki_selectable_rejectionReaction_mcasci24",
                OperatorIds: ["kroos", "reserve_defender"],
                OperatorTargets:
                [
                    new SukiOperatorTargetRef("kroos", 1),
                    new SukiOperatorTargetRef("reserve_defender", 1),
                    new SukiOperatorTargetRef("reserve_defender", 2)
                ]),
            new SukiSpecialFieldState(
                "is3_mizuki",
                "operatorEvolution",
                "進化",
                "operatorMultiSelect",
                "2名",
                "対象指定",
                "operatorsFull",
                "対象2名",
                OperatorIds: ["durin", "reserve_defender"])
        ]
    };

    var mizukiHeader = RhodesRunFieldRegistry.BuildHeaderStatusChips(mizukiState).ToArray();
    Equal("源石錐|鍵|灯火|拒絶反応|等級|分隊", string.Join("|", mizukiHeader.Select(item => item.Label)), "Mizuki header fields are split");
    Equal("2", mizukiHeader.Single(item => item.Label == "鍵").Value, "Mizuki key header value");
    Equal("30", mizukiHeader.Single(item => item.Label == "灯火").Value, "Mizuki light header value");
    Equal("造血障害 / 対象3名", mizukiHeader.Single(item => item.Label == "拒絶反応").Value, "Mizuki rejection header value");
    Equal(false, mizukiHeader.Any(item => item.Label == "IS特殊値"), "Mizuki special values are not cramped into one header");
}

static void RunCatalogLoadsChoices()
{
    var stableDirectory = Path.Combine(Path.GetTempPath(), "rhodes-suki-tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(stableDirectory);
    try
    {
        var stableStatePath = Path.Combine(stableDirectory, "current-state.json");
        File.WriteAllText(
            stableStatePath,
            """
            {
              "run": {
                "campaignId": "is5_sarkaz",
                "special": { "is5_sarkaz": { "idea": 0 } }
              },
              "operators": [],
              "relics": [ "is5_sarkaz_relic_254" ],
              "usedRelicIds": [ "is5_sarkaz_relic_265" ],
              "preferences": { "ocrEngine": "maa-ocr" }
            }
            """);
        var catalog = RhodesRunCatalog.LoadDefault(RhodesRunCatalog.ResolveDataRoot(), stableStatePath);
        var is5 = catalog.Campaigns.Single(campaign => campaign.Id == "is5_sarkaz");
        var is5Relics = catalog.Relics.Where(relic => relic.CampaignId == is5.Id).ToArray();

        Equal(5, catalog.Campaigns.Count, "campaign count");
        Equal("IS#5 サルカズの炉辺奇談", is5.DisplayName, "campaign label");
        var gummy = catalog.Operators.Single(item => item.Name == "グム" && item.OperatorClass == "重装");
        Equal(true, catalog.Operators.Any(item => item.Name == "グム" && item.OperatorClass == "重装"), "operator data");
        Equal(true, File.Exists(gummy.ImagePath), "operator image path");
        Equal(false, gummy.Detail.Contains("入手", StringComparison.Ordinal), "operator obtain method hidden");
        Equal(false, gummy.Detail.Contains("タグ", StringComparison.Ordinal), "operator tags hidden");
        Equal(false, gummy.SearchText.Contains("公開求人", StringComparison.Ordinal), "operator obtain method search hidden");
        Equal(false, gummy.SearchText.Contains("タグ", StringComparison.Ordinal), "operator tag search hidden");
        Equal(296, is5Relics.Length, "is5 relic count");
        Equal(true, File.Exists(is5Relics.First(item => item.Name == "特選獣肉缶詰").ImagePath), "relic image path");
        Equal(true, catalog.Current.SelectedRelicIds.Contains("is5_sarkaz_relic_254"), "current relic selection");
        Equal(true, catalog.Current.UsedRelicIds.Contains("is5_sarkaz_relic_265"), "current used relic state");
        Equal(true, is5Relics.Single(item => item.Id == "is5_sarkaz_relic_265").SupportsUsedFlag, "Gate and Rescue supports used state");
        Equal(true, catalog.Relics.Where(item => item.Name == "「時の果て」").All(item => item.SupportsUsedFlag), "End of Time supports used state in every campaign");
        Equal("is5_sarkaz", catalog.Current.CampaignId, "current campaign");
        Equal(0, catalog.Current.Idea, "current idea");
        Equal("maa-ocr", catalog.Current.OcrEngine, "current ocr engine");

        var tempDirectory = Path.Combine(Path.GetTempPath(), "rhodes-suki-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        try
        {
            var statePath = Path.Combine(tempDirectory, "current-state.json");
            File.WriteAllText(
                statePath,
                """
                {
                  "run": {
                    "campaignId": "is5_sarkaz",
                    "squadId": "is5_sarkaz_squad_16",
                    "squadRandomEffectOptionId": "is5_sarkaz_mimic_02"
                  },
                  "operators": [],
                  "relics": [],
                  "preferences": { "ocrEngine": "glm-ocr" }
                }
                """);

            var squadIdCatalog = RhodesRunCatalog.LoadDefault(RhodesRunCatalog.ResolveDataRoot(), statePath);
            Equal("奇想天外分隊", squadIdCatalog.Current.Squad, "current squad id label");
            Equal("組み合わせ02: #5破壊戦術分隊 + #3精神論分隊", squadIdCatalog.Current.SquadRandomEffect, "current squad option label");
            Equal("glm-ocr", squadIdCatalog.Current.OcrEngine, "state ocr engine");
        }
        finally
        {
            Directory.Delete(tempDirectory, true);
        }

        var is5SpecialFields = (catalog.Current.SpecialFields ?? []).Where(field => field.CampaignId == "is5_sarkaz").ToArray();
        Equal(3, is5SpecialFields.Length, "is5 special field count");
        Equal("構想", is5SpecialFields.Single(field => field.FieldId == "idea").Label, "idea label");
        Equal("0", is5SpecialFields.Single(field => field.FieldId == "idea").Value, "idea value");
        Equal("思案", is5SpecialFields.Single(field => field.FieldId == "thought").Label, "thought label");
        Equal("0個", is5SpecialFields.Single(field => field.FieldId == "thought").Value, "thought value");
        Equal("時代", is5SpecialFields.Single(field => field.FieldId == "age").Label, "age label");
        Equal("未選択", is5SpecialFields.Single(field => field.FieldId == "age").Value, "age value");
        Equal(false, is5SpecialFields.Any(field => field.Label == "想念"), "obsolete idea label");

        var is4SpecialFields = (catalog.Current.SpecialFields ?? []).Where(field => field.CampaignId == "is4_sami").ToArray();
        Equal("is4RevelationFull", is4SpecialFields.Single(field => field.FieldId == "revelation").ProfileId, "is4 revelation profile");
        var is6SpecialFields = (catalog.Current.SpecialFields ?? []).Where(field => field.CampaignId == "is6_sui").ToArray();
        Equal("is6CoinsFull", is6SpecialFields.Single(field => field.FieldId == "coins").ProfileId, "is6 coins profile");
        var seasonalHours = RhodesRunCatalog.LoadSpecialEffectOptions("is6_sui", "seasonalHours");
        var dogPainting = seasonalHours.Single(option => option.Id == "is6_sui_selectable_seasonalHours_is6sst11_meiryou");
        Equal("is6sst11", dogPainting.ParentKey, "seasonal hour parent key");
        Equal("戌絵", dogPainting.ParentName, "seasonal hour parent name");
        Equal("meiryou", dogPainting.VariantRank, "seasonal hour variant rank");
        Equal("明瞭", dogPainting.VariantLabel, "seasonal hour variant label");
    }
    finally
    {
        Directory.Delete(stableDirectory, true);
    }
}

static void RunCatalogPreservesSuiCoinEntries()
{
    var directory = Path.Combine(Path.GetTempPath(), "rhodes-suki-tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(directory);
    try
    {
        var statePath = Path.Combine(directory, "current-state.json");
        File.WriteAllText(
            statePath,
            """
            {
              "run": {
                "campaignId": "is6_sui",
                "special": {
                  "is6_sui": {
                    "ticket": 4,
                    "activeCoins": [
                      { "coinId": "coin_active", "statusId": "status_active", "count": 1 }
                    ],
                    "coins": [
                      { "coinId": "coin_owned", "statusId": "status_a", "count": 2 },
                      { "coinId": "coin_owned", "statusId": "status_b", "count": 1 }
                    ]
                  }
                }
              },
              "operators": [],
              "relics": []
            }
            """);

        var catalog = RhodesRunCatalog.LoadDefault(RhodesRunCatalog.ResolveDataRoot(), statePath);
        var fields = catalog.Current.SpecialFields!
            .Where(field => field.CampaignId == "is6_sui")
            .ToDictionary(field => field.FieldId, StringComparer.Ordinal);

        Equal("4", fields["ticket"].Value, "Sui ticket value");
        Equal(1, fields["activeCoins"].CoinEntries!.Count, "active coin entry count");
        Equal("status_active", fields["activeCoins"].CoinEntries![0].StatusId, "active coin status");
        Equal(2, fields["coins"].CoinEntries!.Count, "owned coin entry count");
        Equal("coin_owned|status_a|2", $"{fields["coins"].CoinEntries![0].CoinId}|{fields["coins"].CoinEntries![0].StatusId}|{fields["coins"].CoinEntries![0].Count}", "owned coin first entry");
        Equal("coin_owned|status_b|1", $"{fields["coins"].CoinEntries![1].CoinId}|{fields["coins"].CoinEntries![1].StatusId}|{fields["coins"].CoinEntries![1].Count}", "owned coin second entry");
    }
    finally
    {
        Directory.Delete(directory, true);
    }
}

static void RunCatalogSarkazManualOptions()
{
    Equal(18, RhodesRunCatalog.MaxDifficultyForCampaign("is5_sarkaz"), "is5 max difficulty");
    Equal(18, RhodesRunCatalog.MaxDifficultyForCampaign("is6_sui"), "current IS6 max difficulty");

    var squad = RhodesRunCatalog.LoadSquadOptions("is5_sarkaz")
        .Single(option => option.Name == "奇想天外分隊");
    var randomEffects = RhodesRunCatalog.LoadSquadRandomEffectOptions("is5_sarkaz", squad.Id);

    Equal(25, randomEffects.Count, "sarkaz random squad effect count");
    Equal("is5_sarkaz_mimic_01", randomEffects[0].Id, "first random effect id");
    Equal(true, randomEffects[0].Name.Contains("組み合わせ01", StringComparison.Ordinal), "first random effect label");
    Equal(0, RhodesRunCatalog.LoadSquadRandomEffectOptions("is5_sarkaz", "is5_sarkaz_squad_01").Count, "non-random squad has no random options");

    var thought = RhodesRunCatalog.LoadSpecialEffectOptions("is5_sarkaz", "thought")
        .Single(option => option.Name == "築壁");
    Equal("妙想", thought.GroupLabel, "thought detail group");
    Equal("使用後、次の戦闘で味方全員が配置時にシールドを1枚獲得", thought.Effect, "thought detail effect");
    Equal("✦3", thought.ThoughtRank, "thought detail rank");
    Equal("▲3", thought.ThoughtLoad, "thought detail load");
    Equal(6, thought.Price, "thought detail price");
    Equal(true, File.Exists(thought.ImagePath), "thought detail image path");
}

static void RunCatalogSuiSeasonalHourDifficultyVariants()
{
    Equal("mourou", RhodesRunCatalog.ResolveSuiSeasonalHourVariantRank(1), "grade 1 seasonal hour rank");
    Equal("mourou", RhodesRunCatalog.ResolveSuiSeasonalHourVariantRank(5), "grade 5 seasonal hour rank");
    Equal("meiryou", RhodesRunCatalog.ResolveSuiSeasonalHourVariantRank(6), "grade 6 seasonal hour rank");
    Equal("meiryou", RhodesRunCatalog.ResolveSuiSeasonalHourVariantRank(11), "grade 11 seasonal hour rank");
    Equal("nyuukotsu", RhodesRunCatalog.ResolveSuiSeasonalHourVariantRank(12), "grade 12 seasonal hour rank");
    Equal("nyuukotsu", RhodesRunCatalog.ResolveSuiSeasonalHourVariantRank(18), "grade 18 seasonal hour rank");
    Equal("朦朧", RhodesRunCatalog.ResolveSuiSeasonalHourVariantLabel(1), "grade 1 seasonal hour label");
    Equal("明瞭", RhodesRunCatalog.ResolveSuiSeasonalHourVariantLabel(6), "grade 6 seasonal hour label");
    Equal("入骨", RhodesRunCatalog.ResolveSuiSeasonalHourVariantLabel(18), "grade 18 seasonal hour label");
}

static void RunCatalogPhantomManualOptions()
{
    var performances = RhodesRunCatalog.LoadPerformanceOptions("is2_phantom");
    Equal(26, performances.Count, "Phantom performance count");
    Equal("is2_phantom_performance_pcsp1", performances[0].Id, "first Phantom performance id");

    var tempRoot = Path.Combine(Path.GetTempPath(), $"rhodes-phantom-state-{Guid.NewGuid():N}");
    Directory.CreateDirectory(tempRoot);
    try
    {
        var statePath = Path.Combine(tempRoot, "current-state.json");
        File.WriteAllText(
            statePath,
            """
            {
              "run": {
                "campaignId": "is2_phantom",
                "performanceId": "is2_phantom_performance_pcsp1",
                "special": {
                  "is2_phantom": {
                    "hallucinations": ["偏執的な", "盲目"]
                  }
                }
              }
            }
            """);
        var state = RhodesRunCatalog.LoadDefault(statePathOverride: statePath).Current;
        Equal("is2_phantom_performance_pcsp1", state.PerformanceId, "Phantom performance state id");
        Equal(true, state.Performance.Contains("凱旋の讃歌", StringComparison.Ordinal), "Phantom performance display name");
        var hallucinations = state.SpecialFields!.Single(field =>
            field.CampaignId == "is2_phantom" && field.FieldId == "hallucinations");
        Equal("2件", hallucinations.Value, "Phantom hallucination count");
        Equal("偏執的な / 盲目", hallucinations.Detail, "Phantom hallucination labels");
    }
    finally
    {
        Directory.Delete(tempRoot, true);
    }
}

static void RunCatalogMizukiRejectionIcons()
{
    var options = RhodesRunCatalog.LoadSpecialEffectOptions("is3_mizuki", "rejectionReaction");

    Equal(10, options.Count, "Mizuki rejection reaction count");
    Equal(true, options.All(option => !string.IsNullOrWhiteSpace(option.ImagePath)), "Mizuki rejection icon path populated");
    Equal(true, options.All(option => File.Exists(option.ImagePath)), "Mizuki rejection icon files exist");
    Equal(
        "MizukiRejection_mcasci8.png",
        Path.GetFileName(options.Single(option => option.Name == "造血障害").ImagePath),
        "Mizuki rejection icon matches its effect id");
}

static void RunCatalogMizukiRejectionTargetInstances()
{
    var tempRoot = Directory.CreateTempSubdirectory("rhodes-mizuki-targets-").FullName;
    try
    {
        var statePath = Path.Combine(tempRoot, "current-state.json");
        File.WriteAllText(
            statePath,
            """
            {
              "run": {
                "campaignId": "is3_mizuki",
                "special": {
                  "is3_mizuki": {
                    "rejectionReaction": {
                      "effectId": "is3_mizuki_selectable_rejectionReaction_mcasci24",
                      "operatorIds": ["reserve_sniper"],
                      "operatorTargets": [
                        { "operatorId": "reserve_sniper", "instance": 2 }
                      ]
                    }
                  }
                }
              },
              "operators": ["reserve_sniper"],
              "operatorCounts": { "reserve_sniper": 3 }
            }
            """);

        var field = RhodesRunCatalog.LoadDefault(statePathOverride: statePath)
            .Current.SpecialFields!
            .Single(item => item.CampaignId == "is3_mizuki" && item.FieldId == "rejectionReaction");
        Equal("reserve_sniper#2", field.OperatorTargets!.Single().TargetKey, "stored recruit instance restored");
        Equal("is3_mizuki_selectable_rejectionReaction_mcasci24", field.EffectId, "stored rejection effect restored");
    }
    finally
    {
        Directory.Delete(tempRoot, true);
    }
}

static void MizukiOperatorPresentationMarksTargets()
{
    var operators = new[]
    {
        new SukiChoiceItem("operator", "kroos", "クルース", "★3 狙撃 / 速射手", "狙撃", "速射手", "", "", 3, 1, false),
        new SukiChoiceItem("operator", "fang", "フェン", "★3 先鋒 / 先駆兵", "先鋒", "先駆兵", "", "", 3, 2, false),
    };
    var fields = new[]
    {
        new SukiSpecialFieldState(
            "is3_mizuki",
            "rejectionReaction",
            "拒絶反応",
            "selectableEffect",
            "造血障害",
            "rejectionReaction",
            "is3RejectionFull",
            "",
            "is3_mizuki_selectable_rejectionReaction_mcasci25",
            OperatorIds: ["kroos"]),
        new SukiSpecialFieldState(
            "is3_mizuki",
            "operatorEvolution",
            "進化",
            "operatorMultiSelect",
            "1名",
            "対象者",
            "operatorsFull",
            "対象1名",
            OperatorIds: ["fang"]),
    };

    RhodesMizukiOperatorPresentation.Apply("is3_mizuki", fields, operators);
    Equal(true, operators[0].IsRejectionReactionTarget, "Mizuki rejection target is marked");
    Equal(false, operators[1].IsRejectionReactionTarget, "unaffected Mizuki operator is not marked");
    Equal(false, operators[0].IsEvolutionTarget, "non-evolution Mizuki operator is not marked");
    Equal(true, operators[1].IsEvolutionTarget, "Mizuki evolution target is marked");

    RhodesMizukiOperatorPresentation.Apply("is5_sarkaz", fields, operators);
    Equal(false, operators[0].IsRejectionReactionTarget, "same stored field is ignored outside Mizuki");
    Equal(false, operators[1].IsEvolutionTarget, "same evolution field is ignored outside Mizuki");
}

static void RunCatalogMizukiHordeCallIcons()
{
    var options = RhodesRunCatalog.LoadSpecialEffectOptions("is3_mizuki", "hordeCall");

    Equal(8, options.Count, "Mizuki horde call count");
    Equal(true, options.All(option => !string.IsNullOrWhiteSpace(option.ImagePath)), "Mizuki horde call icon path populated");
    Equal(true, options.All(option => File.Exists(option.ImagePath)), "Mizuki horde call icon files exist");
    Equal(
        "mcasci12.png",
        Path.GetFileName(options.Single(option => option.Name == "呼び声：探索").ImagePath),
        "Mizuki horde call icon matches its effect id");
}

static void HallucinationCatalogWikiOptions()
{
    var catalog = RhodesHallucinationCatalog.LoadDefault();
    Equal(13, catalog.Options.Count, "Phantom hallucination count");
    Equal(7, catalog.Fusions.Count, "Phantom hallucination fusion count");

    var confusion = catalog.Options.Single(option => option.Name == "錯乱");
    Equal("錯乱した", confusion.MapLabel, "confusion map label");
    Equal(true, confusion.Effect.Contains("思わぬ遭遇", StringComparison.Ordinal), "confusion Wiki effect");

    var normalized = RhodesHallucinationCatalog.NormalizeRecognizedNames(["錯乱した", "迷しの", "錯乱した"]);
    Equal(2, normalized.Count, "overlapping hallucinations stay multi-select and deduplicate");
    Equal("錯乱", normalized[0], "confusion canonical name");
    Equal("迷い", normalized[1], "OCR drift canonical name");

    var fusion = RhodesHallucinationCatalog.ResolveActiveFusions(["迷い", "偏執"]).Single();
    Equal("迷執症", fusion.Name, "fusion name");
    Equal(true, fusion.Effect.Contains("商人", StringComparison.Ordinal), "fusion Wiki effect");
}

static void RunCatalogSarkazBossSelections()
{
    var selections = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
    {
        ["floor3BossId"] = ["is5_f3_silent_merits"],
        ["floor5BossId"] = ["is5_f5_audience"],
    };
    var sections = RhodesBossSelectionCatalog.LoadSections("is5_sarkaz", selections);

    Equal(2, sections.Count, "Sarkaz boss section count");
    var floor3 = sections.Single(section => section.Field == "floor3BossId");
    Equal("3層悪路強敵", floor3.Label, "floor 3 section label");
    Equal(9, floor3.Options.Count(option => !string.IsNullOrWhiteSpace(option.Id)), "floor 3 boss option count");
    Equal("is5_f3_silent_merits", floor3.SelectedOption?.Id, "floor 3 saved selection");
    Equal("武勲を語らず", floor3.SelectedOption?.StageName, "floor 3 stage name");
    Equal(true, File.Exists(floor3.SelectedOption?.ImagePath), "floor 3 boss image path");

    var floor5 = sections.Single(section => section.Field == "floor5BossId");
    Equal(2, floor5.Options.Count(option => !string.IsNullOrWhiteSpace(option.Id)), "floor 5 boss option count");
    Equal("is5_f5_audience", floor5.SelectedOption?.Id, "floor 5 saved selection");
    Equal("「黒き王冠の主」テレシス", floor5.SelectedOption?.BossName, "floor 5 boss name");
}

static void RunCatalogSuiEnd5BossSelections()
{
    var withoutIzayoi = RhodesBossSelectionCatalog.LoadSections(
        "is6_sui",
        null,
        new HashSet<string>(StringComparer.Ordinal));
    Equal(
        "floor3BossId|floor5BossId",
        string.Join('|', withoutIzayoi.Select(section => section.Field)),
        "Sui gated boss sections hidden without route relics");

    var withCloudAndLacquer = RhodesBossSelectionCatalog.LoadSections(
        "is6_sui",
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            ["floor6BossId"] = ["is6_route_end3_black_white"],
        },
        new HashSet<string>(new[] { "is6_sui_relic_215" }, StringComparer.Ordinal));
    var floor6 = withCloudAndLacquer.Single(section => section.Field == "floor6BossId");

    Equal(1, floor6.Options.Count(option => !string.IsNullOrWhiteSpace(option.Id)), "Sui floor 6 boss option count");
    Equal(true, floor6.Helper.Contains("雲と漆", StringComparison.Ordinal), "Sui floor 6 gate guidance");
    Equal("is6_route_end3_black_white", floor6.SelectedOption?.Id, "Sui floor 6 saved selection");
    Equal("歳を謀る者", floor6.SelectedOption?.StageName, "Sui floor 6 route stage");
    Equal("「望」", floor6.SelectedOption?.BossName, "Sui floor 6 route boss");
    Equal(true, File.Exists(floor6.SelectedOption?.ImagePath), "Sui floor 6 boss image path");

    var withIzayoi = RhodesBossSelectionCatalog.LoadSections(
        "is6_sui",
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            ["end5BossVariantId"] = ["is6_end5_zhivian_beasts"],
        },
        new HashSet<string>(new[] { "is6_sui_relic_220" }, StringComparer.Ordinal));
    var end5 = withIzayoi.Single(section => section.Field == "end5BossVariantId");

    Equal(7, end5.Options.Count(option => !string.IsNullOrWhiteSpace(option.Id)), "Sui END5 route pattern count");
    Equal(
        7,
        end5.Options.Count(option => !string.IsNullOrWhiteSpace(option.Id)
            && option.ImagePath.EndsWith(Path.Combine("assets", "bosses", "wikiru", "img", "SQ.jpg"), StringComparison.OrdinalIgnoreCase)),
        "Sui END5 routes share Shuo shell image");
    Equal(true, end5.Helper.Contains("イザヨウ", StringComparison.Ordinal), "Sui END5 gate guidance");
    Equal("is6_end5_zhivian_beasts", end5.SelectedOption?.Id, "Sui END5 saved selection");
    Equal("止変（群獣を役す）", end5.SelectedOption?.StageName, "Sui END5 branch stage");
    Equal("易＋「歳」＋「望」", end5.SelectedOption?.BossName, "Sui END5 branch bosses");
    Equal("選択条件: 不赦＋不息", end5.SelectedOption?.RequiredNote, "Sui END5 branch condition note");
    Equal(true, end5.SelectedOption?.DisplayName.Contains("不赦＋不息", StringComparison.Ordinal) == true, "Sui END5 picker exposes condition note");
}

static void RunStateStoreBossSelections()
{
    var now = DateTimeOffset.Parse("2026-07-18T00:00:00Z");
    var state = JsonNode.Parse(
        """
        {
          "version": 1,
          "run": {
            "campaignId": "is5_sarkaz",
            "special": { "is5_sarkaz": { "thoughtOverlayVisible": false } }
          },
          "bossSelections": {
            "is4_sami": { "floor3BossId": "is4_existing" }
          }
        }
        """)!.AsObject();

    RhodesRunStateStore.ApplyBossSelection(
        state,
        "is5_sarkaz",
        "floor3BossId",
        ["is5_f3_silent_merits"],
        allowsMultiple: false,
        now: now);
    Equal(
        "is5_f3_silent_merits",
        state["bossSelections"]!["is5_sarkaz"]!["floor3BossId"]!.GetValue<string>(),
        "single boss selection stored");

    RhodesRunStateStore.ApplyBossSelection(
        state,
        "is4_sami",
        "manualRouteBossIds",
        ["is4_route_a", "is4_route_b", "is4_route_a"],
        allowsMultiple: true,
        now: now);
    Equal(2, state["bossSelections"]!["is4_sami"]!["manualRouteBossIds"]!.AsArray().Count, "multi boss selection deduplicated");

    RhodesRunStateStore.ClearBossSelectionsForCampaign(state, "is5_sarkaz");
    Equal(false, state["bossSelections"]!["is5_sarkaz"]!.AsObject().ContainsKey("floor3BossId"), "current campaign boss selection cleared");
    Equal("is4_existing", state["bossSelections"]!["is4_sami"]!["floor3BossId"]!.GetValue<string>(), "other campaign boss selection retained");

    state["usedRelicIds"] = new JsonArray("is5_sarkaz_relic_265");
    var clearedJson = RhodesStateApiClient.ClearCurrentRunInStateJson(state.ToJsonString());
    var cleared = JsonNode.Parse(clearedJson)!.AsObject();
    Equal(false, cleared["bossSelections"]!["is5_sarkaz"]!.AsObject().Any(), "API clear removes current campaign bosses");
    Equal(0, cleared["usedRelicIds"]!.AsArray().Count, "API clear removes used relic flags");
}

static void ChoicePaneDragScrollMath()
{
    Equal(140d, RhodesChoicePaneDragScroll.OffsetForDrag(100, -40, 600, 200), "drag upward scrolls down");
    Equal(0d, RhodesChoicePaneDragScroll.OffsetForDrag(10, 80, 600, 200), "drag downward clamps at top");
    Equal(400d, RhodesChoicePaneDragScroll.OffsetForDrag(390, -80, 600, 200), "drag upward clamps at bottom");
}

static void ChoiceCatalogRegistryBuildsWorkspaceModels()
{
    var operators = new[]
    {
        new SukiChoiceItem("operator", "texas", "テキサス", "★5 先鋒 / 先駆兵", "先鋒", "先駆兵", "", "", 5, 0, false),
        new SukiChoiceItem("operator", "gummy", "グム", "★4 重装 / 庇護衛士", "重装", "庇護衛士", "", "", 4, 1, false),
        new SukiChoiceItem("operator", "tulip", "チューリップ", "★5 先鋒 / 先駆兵", "先鋒", "先駆兵", "", "", 5, 2, true)
    };
    operators[1].IsSelected = true;

    var operatorView = RhodesChoiceCatalogRegistry.BuildView(
        "operator",
        operators,
        new SukiChoiceCatalogFilterState(
            SearchText: "",
            Category: "",
            OperatorClass: "先鋒",
            OperatorBranch: "すべて",
            Rarity: "すべて",
            CampaignId: "",
            ShowSelectedFirst: false,
            HideExcluded: false,
            SelectedOnly: false,
            PaneColumns: 4));

    Equal("operators", operatorView.Descriptor.Id, "operator descriptor id");
    Equal("operator", operatorView.Descriptor.Kind, "operator descriptor kind");
    Equal("オペレーター", operatorView.Descriptor.Label, "operator descriptor label");
    Equal("テキサス", operatorView.FilteredItems.Single().Name, "operator view applies filters");
    Equal("1件 / 招集1名", operatorView.Summary, "operator view summary");
    Equal(4, operatorView.Rows.Single().Columns, "operator view rows use pane columns");

    var relics = new[]
    {
        new SukiChoiceItem("relic", "is5_a", "秘宝A", "No.001 食品", "", "", "is5_sarkaz", "食品", 0, 0, false),
        new SukiChoiceItem("relic", "is5_b", "秘宝B", "No.002 書物", "", "", "is5_sarkaz", "書物", 0, 1, false),
        new SukiChoiceItem("relic", "is4_a", "秘宝C", "No.001 食品", "", "", "is4_sami", "食品", 0, 2, false)
    };
    relics[0].IsSelected = true;

    var relicView = RhodesChoiceCatalogRegistry.BuildView(
        "relic",
        relics,
        new SukiChoiceCatalogFilterState(
            SearchText: "",
            Category: "食品",
            OperatorClass: "",
            OperatorBranch: "",
            Rarity: "",
            CampaignId: "is5_sarkaz",
            ShowSelectedFirst: true,
            HideExcluded: false,
            SelectedOnly: false,
            PaneColumns: 2));

    Equal("relics", relicView.Descriptor.Id, "relic descriptor id");
    Equal(true, relicView.Descriptor.IsCampaignScoped, "relic descriptor is campaign scoped");
    Equal("秘宝A", relicView.FilteredItems.Single().Name, "relic view applies campaign and category filters");
    Equal("1件 / 所持1件 / IS内2件", relicView.Summary, "relic view summary");
    Equal(2, relicView.Rows.Single().Columns, "relic view rows use pane columns");
    Equal(false, RhodesChoiceCatalogRegistry.RequiresFullRefreshAfterSelectionMutation(relicView.FilterState), "registry selection keeps list stable for selected-first");
}

static void ChoiceFilters()
{
    var items = new[]
    {
        new SukiChoiceItem("operator", "a", "テンニンカ", "★4 先鋒 / 旗手", "先鋒", "旗手", "", "", 4, 0, false),
        new SukiChoiceItem("operator", "b", "グム", "★4 重装 / 庇護衛士", "重装", "庇護衛士", "", "", 4, 1, false),
        new SukiChoiceItem("operator", "c", "チューリップ", "★5 先鋒 / 先駆兵", "先鋒", "先駆兵", "", "", 5, 2, true),
    };
    items[1].IsSelected = true;
    items[2].IsExcluded = true;

    var selectedFirst = RhodesChoiceFilter.Apply(items, new SukiChoiceFilterOptions(ShowSelectedFirst: true)).ToArray();
    Equal("グム", selectedFirst[0].Name, "selected first");

    var hiddenExcluded = RhodesChoiceFilter.Apply(items, new SukiChoiceFilterOptions(HideExcluded: true)).ToArray();
    Equal(false, hiddenExcluded.Any(item => item.Name == "チューリップ"), "hide excluded");

    var selectedOnly = RhodesChoiceFilter.Apply(items, new SukiChoiceFilterOptions(SelectedOnly: true)).ToArray();
    Equal(1, selectedOnly.Length, "selected only count");
    Equal("グム", selectedOnly[0].Name, "selected only item");

    var searched = RhodesChoiceFilter.Apply(items, new SukiChoiceFilterOptions(SearchText: "旗手")).ToArray();
    Equal("テンニンカ", searched.Single().Name, "search by detail");

    var vanguards = RhodesChoiceFilter.Apply(items, new SukiChoiceFilterOptions(OperatorClass: "先鋒")).ToArray();
    Equal(1, vanguards.Length, "class filter excludes hidden by default");
    Equal("テンニンカ", vanguards[0].Name, "class filter item");

    var rarity4 = RhodesChoiceFilter.Apply(items, new SukiChoiceFilterOptions(Rarity: "★4")).ToArray();
    Equal(2, rarity4.Length, "rarity filter count");

    var taxonomyOrder = RhodesChoiceFilter.Apply(items.Reverse(), new SukiChoiceFilterOptions(SortMode: "職業・職分順")).ToArray();
    Equal("テンニンカ", taxonomyOrder[0].Name, "operator taxonomy sort starts with vanguard");

    var relics = new[]
    {
        new SukiChoiceItem("relic", "r1", "特選獣肉缶詰", "No.001 食品", "", "", "is5_sarkaz", "食品", 0, 1, false),
        new SukiChoiceItem("relic", "r2", "古城の手記", "No.002 書物", "", "", "is5_sarkaz", "書物", 0, 2, false),
        new SukiChoiceItem("relic", "r3", "別IS", "No.001 食品", "", "", "is4_sami", "食品", 0, 1, false),
    };
    var foodRelics = RhodesChoiceFilter.Apply(relics, new SukiChoiceFilterOptions(CampaignId: "is5_sarkaz", Category: "食品")).ToArray();
    Equal(1, foodRelics.Length, "relic category filter count");
    Equal("特選獣肉缶詰", foodRelics[0].Name, "relic category filter item");

    var numberOrder = RhodesChoiceFilter.Apply(relics.Reverse(), new SukiChoiceFilterOptions(CampaignId: "is5_sarkaz", SortMode: "番号順")).ToArray();
    Equal("特選獣肉缶詰", numberOrder[0].Name, "relic number sort");

    Equal(false, RhodesChoiceFilter.RequiresFullRefreshAfterSelectionMutation(new SukiChoiceFilterOptions(HideExcluded: true)), "selection does not rebuild for hide excluded only");
    Equal(false, RhodesChoiceFilter.RequiresFullRefreshAfterSelectionMutation(new SukiChoiceFilterOptions(ShowSelectedFirst: true)), "selection keeps list stable for selected first");
    Equal(true, RhodesChoiceFilter.RequiresFullRefreshAfterSelectionMutation(new SukiChoiceFilterOptions(SelectedOnly: true)), "selection rebuilds for selected only");
    Equal(true, RhodesChoiceFilter.RequiresFullRefreshAfterExclusionMutation(new SukiChoiceFilterOptions(HideExcluded: true)), "exclusion rebuilds for hide excluded");
    Equal(false, RhodesChoiceFilter.RequiresFullRefreshAfterExclusionMutation(new SukiChoiceFilterOptions(ShowSelectedFirst: true)), "exclusion keeps list stable for selected first");
}

static void RelicUsagePriorityAndPersistence()
{
    var normal = new SukiChoiceItem(
        "relic", "normal", "通常秘宝", "No.001", "", "", "is2_phantom", "食品", 0, 1, false)
    {
        IsSelected = true,
    };
    var endOfTime = new SukiChoiceItem(
        "relic", "end-of-time", "「時の果て」", "No.174", "", "", "is2_phantom", "巧者の利器", 0, 174, false,
        supportsUsedFlag: true)
    {
        IsSelected = true,
        IsUsed = true,
    };
    var unownedGate = new SukiChoiceItem(
        "relic", "gate", "「門」と「救難」", "No.265", "", "", "is5_sarkaz", "テラの秘密", 0, 265, false,
        supportsUsedFlag: true)
    {
        IsUsed = true,
    };

    var ordered = RhodesChoiceFilter.Apply(
        [normal, endOfTime],
        new SukiChoiceFilterOptions(CampaignId: "is2_phantom", ShowSelectedFirst: false));
    Equal("end-of-time|normal", string.Join("|", ordered.Select(item => item.Id)), "owned run-saving relic pinned first");
    Equal(true, endOfTime.IsUsageToggleVisible, "owned run-saving relic exposes used toggle");
    Equal(false, unownedGate.IsUsageToggleVisible, "unowned run-saving relic hides used toggle");

    var state = JsonNode.Parse("""{ "run": { "campaignId": "is2_phantom" } }""")!.AsObject();
    RhodesRunStateStore.ApplyChoices(
        state,
        [],
        [normal, endOfTime, unownedGate],
        new SukiChoicePersistenceOptions(false, false, false, false, false, false, 2, 2),
        DateTimeOffset.Parse("2026-07-19T00:00:00Z"));
    Equal("end-of-time", state["usedRelicIds"]!.AsArray().Single()!.GetValue<string>(), "only owned used relic persisted");
}

static void ChoiceRows()
{
    var items = Enumerable.Range(0, 5)
        .Select(index => new SukiChoiceItem("operator", $"op{index}", $"op{index}", "★4 先鋒 / 旗手", "先鋒", "旗手", "", "", 4, index, false))
        .ToArray();

    var rows = RhodesChoiceRows.Build(items, 4).ToArray();
    Equal(2, rows.Length, "four pane row count");
    Equal(4, rows[0].Columns, "four pane column count");
    Equal(4, rows[0].Items.Count, "first row item count");
    Equal(1, rows[1].Items.Count, "last row item count");

    var lowClampRows = RhodesChoiceRows.Build(items, 0).ToArray();
    Equal(1, lowClampRows[0].Columns, "low column clamp");
    Equal(5, lowClampRows.Length, "one pane row count");

    var highClampRows = RhodesChoiceRows.Build(items, 9).ToArray();
    Equal(4, highClampRows[0].Columns, "high column clamp");
    Equal(2, highClampRows.Length, "high clamp row count");
}

static void OperatorTaxonomyOrder()
{
    var classes = RhodesOperatorTaxonomy.SortClasses(
    [
        "医療",
        "先鋒",
        "特殊",
        "術師",
        "前衛",
        "重装",
        "補助",
        "狙撃",
    ]);
    Equal("先鋒|前衛|重装|狙撃|術師|医療|補助|特殊", string.Join("|", classes), "class order");

    var specialBranches = RhodesOperatorTaxonomy.SortBranches(
    [
        ("行商人", "特殊"),
        ("罠師", "特殊"),
        ("執行者", "特殊"),
        ("潜伏者", "特殊"),
        ("鬼才", "特殊"),
        ("傀儡師", "特殊"),
        ("推撃手", "特殊"),
        ("鉤縄師", "特殊"),
        ("錬金士", "特殊"),
        ("巡空者", "特殊"),
    ]);
    Equal("執行者|推撃手|潜伏者|鉤縄師|鬼才|行商人|罠師|傀儡師|錬金士|巡空者", string.Join("|", specialBranches), "specialist branch order");

    var mixedBranches = RhodesOperatorTaxonomy.SortBranches(
    [
        ("巡空者", "特殊"),
        ("医師", "医療"),
        ("先駆兵", "先鋒"),
        ("行商人", "特殊"),
        ("闘士", "前衛"),
        ("重盾衛士", "重装"),
        ("守望者", "医療"),
        ("速射手", "狙撃"),
        ("拡散術師", "術師"),
        ("緩速師", "補助"),
    ]);
    Equal("先駆兵|闘士|重盾衛士|速射手|拡散術師|医師|守望者|緩速師|行商人|巡空者", string.Join("|", mixedBranches), "mixed branch order");
}

static void ChoicePersistence()
{
    var operators = new[]
    {
        new SukiChoiceItem("operator", "gummy", "グム", "★4 重装 / 庇護衛士", "重装", "庇護衛士", "", "", 4, 1, false),
        new SukiChoiceItem("operator", "rain", "レイン", "★5 狙撃 / 速射手", "狙撃", "速射手", "", "", 5, 2, false),
    };
    operators[0].IsSelected = true;
    operators[1].IsExcluded = true;

    var relics = new[]
    {
        new SukiChoiceItem("relic", "is5_sarkaz_relic_001", "秘宝A", "No.001", "", "", "is5_sarkaz", "食品", 0, 1, false),
        new SukiChoiceItem("relic", "is5_sarkaz_relic_002", "秘宝B", "No.002", "", "", "is5_sarkaz", "食品", 0, 2, false),
    };
    relics[0].IsSelected = true;
    relics[1].IsExcluded = true;

    var state = JsonNode.Parse(
        """
        {
          "version": 1,
          "run": { "campaignId": "is5_sarkaz", "hope": 3 },
          "operators": ["old"],
          "relics": [],
          "preferences": { "ocrEngine": "profile" }
        }
        """)!.AsObject();
    var updated = RhodesRunStateStore.ApplyChoices(
        state,
        operators,
        relics,
        new SukiChoicePersistenceOptions(true, true, false, false, true, true, 4, 3),
        DateTimeOffset.Parse("2026-07-01T00:00:00Z"));

    Equal("gummy", updated["operators"]!.AsArray()[0]!.GetValue<string>(), "selected operator");
    Equal("is5_sarkaz_relic_001", updated["relics"]!.AsArray()[0]!.GetValue<string>(), "selected relic");
    var preferences = updated["preferences"]!.AsObject();
    Equal("rain", preferences["operatorExcludedIds"]!.AsArray()[0]!.GetValue<string>(), "operator exclusion");
    Equal("is5_sarkaz_relic_002", preferences["relicExcludedIds"]!.AsArray()[0]!.GetValue<string>(), "relic exclusion");
    Equal(true, preferences["operatorShowSelectedFirst"]!.GetValue<bool>(), "operator selected first preference");
    Equal(true, preferences["operatorHideExcluded"]!.GetValue<bool>(), "operator hide excluded preference");
    Equal(false, preferences["operatorSelectedOnly"]!.GetValue<bool>(), "operator selected only preference");
    Equal(false, preferences["relicShowSelectedFirst"]!.GetValue<bool>(), "relic selected first preference");
    Equal(true, preferences["relicHideExcluded"]!.GetValue<bool>(), "relic hide excluded preference");
    Equal(true, preferences["relicSelectedOnly"]!.GetValue<bool>(), "relic selected only preference");
    Equal(4, preferences["operatorGridColumns"]!.GetValue<int>(), "operator grid columns");
    Equal(3, preferences["relicGridColumns"]!.GetValue<int>(), "relic grid columns");
    Equal("maa-ocr", preferences["ocrEngine"]!.GetValue<string>(), "legacy ocr preference normalized");
    Equal("2026-07-01T00:00:00.0000000Z", updated["updatedAt"]!.GetValue<string>(), "updatedAt");
    Equal(false, updated["run"]!.AsObject().ContainsKey("hope"), "abandoned run value pruned");
}

static void ReserveOperatorCounts()
{
    var reserve = new SukiChoiceItem(
        "operator", "reserve_sniper", "予備隊員-狙撃", "★3 狙撃 / 速射手", "狙撃", "速射手", "", "", 3, 1, false)
    {
        IsSelected = true,
        SelectionCount = 3,
    };
    var gummy = new SukiChoiceItem(
        "operator", "gummy", "グム", "★4 重装 / 庇護衛士", "重装", "庇護衛士", "", "", 4, 2, false)
    {
        IsSelected = true,
        SelectionCount = 4,
    };

    Equal(true, reserve.SupportsMultipleCount, "reserve supports multiple count");
    Equal(false, gummy.SupportsMultipleCount, "regular operator remains single count");
    Equal(3, reserve.EffectiveSelectionCount, "reserve effective count");
    Equal(1, gummy.SelectionCount, "regular operator count remains one");

    var state = JsonNode.Parse(
        """
        {
          "version": 1,
          "run": { "campaignId": "is5_sarkaz" },
          "operators": [],
          "relics": []
        }
        """)!.AsObject();
    var updated = RhodesRunStateStore.ApplyChoices(
        state,
        [reserve, gummy],
        [],
        new SukiChoicePersistenceOptions(false, false, false, false, false, false, 2, 2),
        DateTimeOffset.Parse("2026-07-23T00:00:00Z"));

    Equal(2, updated["operators"]!.AsArray().Count, "operator ids stay unique");
    var counts = updated["operatorCounts"]!.AsObject();
    Equal(3, counts["reserve_sniper"]!.GetValue<int>(), "reserve count persisted separately");
    Equal(false, counts.ContainsKey("gummy"), "regular operator count is not persisted");

    reserve.IsSelected = false;
    Equal(1, reserve.SelectionCount, "deselection resets reserve count");
}

static void StateApiReplacement()
{
    var tempDirectory = Directory.CreateTempSubdirectory("rhodes-suki-state-api-").FullName;
    try
    {
        var statePath = Path.Combine(tempDirectory, "current-state.json");
        RhodesRunStateStore.ReplaceStateJsonAsync(
            """
            {
              "run": {
                "campaignId": "is5_sarkaz",
                "hope": 9,
                "maxHope": 12,
                "special": { "is5_sarkaz": { "idea": 4 } }
              },
              "operators": ["gummy", "reserve_sniper"],
              "operatorCounts": { "reserve_sniper": 3, "reserve_caster": 5, "gummy": 8 },
              "relics": []
            }
            """,
            statePath).GetAwaiter().GetResult();

        var catalog = RhodesRunCatalog.LoadDefault(RhodesRunCatalog.ResolveDataRoot(), statePath);
        Equal("is5_sarkaz", catalog.Current.CampaignId, "api campaign id");
        Equal(null, typeof(SukiRunStateSnapshot).GetProperty("Hope"), "hope snapshot property removed");
        Equal(null, typeof(SukiRunStateSnapshot).GetProperty("MaxHope"), "max hope snapshot property removed");
        Equal(null, typeof(SukiRunStateSnapshot).GetProperty("LifePoints"), "life snapshot property removed");
        Equal(null, typeof(SukiRunStateSnapshot).GetProperty("Shield"), "shield snapshot property removed");
        Equal(null, typeof(SukiRunStateSnapshot).GetProperty("CommandLevel"), "command level snapshot property removed");
        Equal(4, catalog.Current.Idea, "api idea");
        Equal(true, catalog.Current.SelectedOperatorIds.Contains("gummy"), "api selected operator");
        Equal(3, catalog.Current.OperatorCounts["reserve_sniper"], "api reserve count");
        Equal(3, catalog.Operators.Single(item => item.Id == "reserve_sniper").SelectionCount, "reserve count restored into choice");
        Equal(false, catalog.Current.OperatorCounts.ContainsKey("reserve_caster"), "unselected reserve count is discarded");
        Equal(false, catalog.Current.OperatorCounts.ContainsKey("gummy"), "regular operator count ignored by choice model");
        var saved = JsonNode.Parse(File.ReadAllText(statePath))!.AsObject()["run"]!.AsObject();
        Equal(false, saved.ContainsKey("hope"), "replacement prunes stale hope");
        Equal(false, saved.ContainsKey("maxHope"), "replacement prunes stale max hope");
    }
    finally
    {
        Directory.Delete(tempDirectory, true);
    }
}

static void StateApiAdbSettingsApply()
{
    var updated = JsonNode.Parse(RhodesStateApiClient.ApplyAdbSettingsToStateJson(
        """
        {
          "version": 1,
          "run": { "campaignId": "is5_sarkaz", "hope": 3 },
          "adb": { "connectionPreset": "auto", "serial": "" },
          "operators": ["gummy"]
        }
        """,
        new RhodesAdbApiSettings(
            true,
            "google-play-games-dev",
            "C:/Google/adb.exe",
            "127.0.0.1:6520")))!.AsObject();

    var adb = updated["adb"]!.AsObject();
    Equal("google-play-games-dev", adb["connectionPreset"]!.GetValue<string>(), "adb preset");
    Equal("C:/Google/adb.exe", adb["adbPath"]!.GetValue<string>(), "adb path");
    Equal("127.0.0.1:6520", adb["serial"]!.GetValue<string>(), "adb serial");
    Equal(true, adb["restartServerOnFailure"]!.GetValue<bool>(), "adb restart server");
    Equal(5, adb["reconnectAttempts"]!.GetValue<int>(), "adb reconnect attempts");
    Equal("is5_sarkaz", updated["run"]!.AsObject()["campaignId"]!.GetValue<string>(), "run preserved");
    Equal(false, updated["run"]!.AsObject().ContainsKey("hope"), "adb settings prune abandoned run value");
    Equal("gummy", updated["operators"]!.AsArray()[0]!.GetValue<string>(), "operators preserved");
}

static void SukiStateSyncWorkflowSettingsSuccess()
{
    var op = new SukiChoiceItem("operator", "gummy", "グム", "重装", "重装", "守護者", "", "", 4, 10, false)
    {
        IsSelected = true
    };
    var relic = new SukiChoiceItem("relic", "is5_sarkaz_relic_001", "No.1", "秘宝", "", "", "is5_sarkaz", "秘宝", 0, 1, false)
    {
        IsSelected = true
    };
    var savedState = "";
    var replacedState = "";
    var result = RhodesSukiStateSyncWorkflow.SyncSettingsAsync(
        new RhodesSukiStateSyncRequest(
            [op],
            [relic],
            new SukiChoicePersistenceOptions(true, false, false, false, true, false, 4, 4),
            new RhodesAdbApiSettings(true, "mumu", "M:/Program Files/Netease/MuMu Player 12/shell/adb.exe", "127.0.0.1:16384"),
            new SukiOutputPreferences(
                true,
                true,
                true,
                12,
                [new SukiOutputPartState("operators", true, false, true, 360, 140)]),
            "glm-ocr"),
        _ => Task.FromResult(new RhodesStateApiResult(
            """
            {
              "version": 1,
              "mode": "casual",
              "run": { "campaignId": "is5_sarkaz", "hope": 3 },
              "operators": [],
              "relics": []
            }
            """,
            "")),
        (stateJson, _) =>
        {
            savedState = stateJson;
            return Task.FromResult(new RhodesStateApiResult(stateJson, ""));
        },
        (stateJson, _) =>
        {
            replacedState = stateJson;
            return Task.CompletedTask;
        }).GetAwaiter().GetResult();

    Equal(true, result.Succeeded, "sync workflow succeeds");
    Equal(true, result.ShouldReloadRunState, "sync workflow reloads state");
    Equal("接続済み", result.ApiStatus.State, "sync workflow api status");
    Equal(savedState, replacedState, "sync workflow replaces saved state locally");
    var updated = JsonNode.Parse(savedState)!.AsObject();
    Equal("gummy", updated["operators"]!.AsArray()[0]!.GetValue<string>(), "sync workflow operator selection");
    Equal("is5_sarkaz_relic_001", updated["relics"]!.AsArray()[0]!.GetValue<string>(), "sync workflow relic selection");
    Equal("mumu", updated["adb"]!.AsObject()["connectionPreset"]!.GetValue<string>(), "sync workflow adb preset");
    Equal("127.0.0.1:16384", updated["adb"]!.AsObject()["serial"]!.GetValue<string>(), "sync workflow adb serial");
    Equal("glm-ocr", updated["preferences"]!.AsObject()["ocrEngine"]!.GetValue<string>(), "sync workflow ocr engine");
    Equal(true, updated["preferences"]!.AsObject()["sukiOutputSeparateWindow"]!.GetValue<bool>(), "sync workflow output window");
    Equal("tournament", updated["mode"]!.GetValue<string>(), "sync workflow tournament mode");
    Equal(false, updated["run"]!.AsObject().ContainsKey("hope"), "sync workflow prunes abandoned values");
}

static void SukiStateSyncWorkflowSettingsFailure()
{
    var result = RhodesSukiStateSyncWorkflow.SyncSettingsAsync(
        new RhodesSukiStateSyncRequest(
            [],
            [],
            new SukiChoicePersistenceOptions(false, false, false, false, false, false, 2, 2),
            new RhodesAdbApiSettings(true, "auto", "adb", ""),
            new SukiOutputPreferences(false, false, false, 0, []),
            "maa-ocr"),
        _ => Task.FromResult(new RhodesStateApiResult("", "connection refused")),
        (_, _) => throw new InvalidOperationException("save should not run"),
        (_, _) => throw new InvalidOperationException("replace should not run")).GetAwaiter().GetResult();

    Equal(false, result.Succeeded, "sync workflow failure state");
    Equal(false, result.ShouldReloadRunState, "sync workflow failure avoids reload");
    Equal("connection refused", result.Error, "sync workflow failure error");
    Equal("接続失敗", result.ApiStatus.State, "sync workflow failure api status");
}

static void SukiStateSyncWorkflowRunContextSuccess()
{
    var savedState = "";
    var replacedState = "";
    var result = RhodesSukiStateSyncWorkflow.SyncRunContextAsync(
        "is4_sami",
        _ => Task.FromResult(new RhodesStateApiResult(
            """
            {
              "version": 1,
              "run": {
                "campaignId": "is5_sarkaz",
                "special": { "is5_sarkaz": { "idea": 4 } }
              },
              "operators": ["gummy"]
            }
            """,
            "")),
        (stateJson, _) =>
        {
            savedState = stateJson;
            return Task.FromResult(new RhodesStateApiResult(stateJson, ""));
        },
        (stateJson, _) =>
        {
            replacedState = stateJson;
            return Task.CompletedTask;
        }).GetAwaiter().GetResult();

    Equal(true, result.Succeeded, "run context sync succeeds");
    Equal(true, result.ShouldReloadRunState, "run context sync reloads state");
    Equal("接続済み", result.ApiStatus.State, "run context sync api status");
    Equal(savedState, replacedState, "run context replaces saved state locally");
    var updated = JsonNode.Parse(savedState)!.AsObject();
    Equal("is4_sami", updated["run"]!.AsObject()["campaignId"]!.GetValue<string>(), "run context campaign");
    Equal(false, updated["run"]!.AsObject().ContainsKey("special"), "run context clears stale special state");
    Equal("gummy", updated["operators"]!.AsArray()[0]!.GetValue<string>(), "run context preserves operators");
}

static void SukiStateSyncWorkflowRunContextFailure()
{
    var result = RhodesSukiStateSyncWorkflow.SyncRunContextAsync(
        "is4_sami",
        _ => Task.FromResult(new RhodesStateApiResult("", "timeout")),
        (_, _) => throw new InvalidOperationException("save should not run"),
        (_, _) => throw new InvalidOperationException("replace should not run")).GetAwaiter().GetResult();

    Equal(false, result.Succeeded, "run context failure state");
    Equal(false, result.ShouldReloadRunState, "run context failure avoids reload");
    Equal("timeout", result.Error, "run context failure error");
    Equal("接続失敗", result.ApiStatus.State, "run context failure api status");
}

static void SukiStateSyncWorkflowImportSuccess()
{
    var apiState =
        """
        {
          "version": 1,
          "run": { "campaignId": "is5_sarkaz", "special": { "is5_sarkaz": { "idea": 7 } } },
          "operators": ["gummy"]
        }
        """;
    var replacedState = "";
    var result = RhodesSukiStateSyncWorkflow.SyncFromApiAsync(
        _ => Task.FromResult(new RhodesStateApiResult(apiState, "")),
        (stateJson, _) =>
        {
            replacedState = stateJson;
            return Task.CompletedTask;
        }).GetAwaiter().GetResult();

    Equal(true, result.Succeeded, "api import succeeds");
    Equal(true, result.ShouldReloadRunState, "api import reloads state");
    Equal("接続済み", result.ApiStatus.State, "api import status");
    Equal(apiState, replacedState, "api import replaced state");
}

static void SukiStateSyncWorkflowImportFailure()
{
    var result = RhodesSukiStateSyncWorkflow.SyncFromApiAsync(
        _ => Task.FromResult(new RhodesStateApiResult("", "state endpoint unavailable")),
        (_, _) => throw new InvalidOperationException("replace should not run")).GetAwaiter().GetResult();

    Equal(false, result.Succeeded, "api import failure state");
    Equal(false, result.ShouldReloadRunState, "api import failure avoids reload");
    Equal("state endpoint unavailable", result.Error, "api import failure error");
    Equal("接続失敗", result.ApiStatus.State, "api import failure api status");
}

static void StateApiSukiPreferencesApply()
{
    var updated = JsonNode.Parse(RhodesStateApiClient.ApplySukiPreferencesToStateJson(
        """
        {
          "version": 1,
          "mode": "casual",
          "run": { "campaignId": "is5_sarkaz" },
          "preferences": {
            "ocrEngine": "glm-ocr",
            "compactRelicScrollSpeed": 9
          }
        }
        """,
        new SukiChoicePersistenceOptions(
            true,
            true,
            false,
            false,
            true,
            false,
            4,
            3),
        new SukiOutputPreferences(
            true,
            true,
            false,
            42,
            [
                new SukiOutputPartState("operators", true, false, true, 420, 132),
                new SukiOutputPartState("relics", true, true, true, 420, 170),
                new SukiOutputPartState("special", true, true, false, 300, 126),
            ],
            [
                new SukiOverlayLayoutState("status", true, 40, 30, 1200, 120, 2),
                new SukiOverlayLayoutState("operators", true, 1460, 300, 420, 620, 5),
            ],
            BackgroundTransparency: 35,
            ShowPartTitles: false),
        "maa-ocr"))!.AsObject();

    Equal("tournament", updated["mode"]!.GetValue<string>(), "mode tournament");
    var preferences = updated["preferences"]!.AsObject();
    Equal("maa-ocr", preferences["ocrEngine"]!.GetValue<string>(), "ocr engine updated");
    Equal(true, preferences["operatorShowSelectedFirst"]!.GetValue<bool>(), "operator selected first");
    Equal(true, preferences["operatorHideExcluded"]!.GetValue<bool>(), "operator hide excluded");
    Equal(4, preferences["operatorGridColumns"]!.GetValue<int>(), "operator columns");
    Equal(3, preferences["relicGridColumns"]!.GetValue<int>(), "relic columns");
    Equal(30, preferences["compactRelicScrollSpeed"]!.GetValue<int>(), "scroll speed clamped");
    Equal(30, preferences["horizontalOperatorScrollSpeed"]!.GetValue<int>(), "operator scroll speed");
    Equal(true, preferences["sukiOutputSeparateWindow"]!.GetValue<bool>(), "separate window");
    Equal(false, preferences["sukiOutputTransparentBackground"]!.GetValue<bool>(), "transparent background");
    Equal(35, preferences["sukiOutputBackgroundTransparency"]!.GetValue<int>(), "background transparency");
    Equal(false, preferences["sukiOutputShowPartTitles"]!.GetValue<bool>(), "part title visibility");
    Equal(3, preferences["sukiOutputParts"]!.AsArray().Count, "output parts count");
    Equal("operators", preferences["sukiOutputParts"]!.AsArray()[0]!.AsObject()["id"]!.GetValue<string>(), "first output part");
    Equal(6, preferences["sukiOverlayLayout"]!.AsArray().Count, "overlay layout count");
    Equal(1460, preferences["sukiOverlayLayout"]!.AsArray()[2]!.AsObject()["x"]!.GetValue<int>(), "operator layout x");
    Equal(
        true,
        updated["run"]!.AsObject()["special"]!.AsObject()["is5_sarkaz"]!.AsObject()["thoughtOverlayVisible"]!.GetValue<bool>(),
        "special output part controls thought overlay visibility");
}

static void StateApiChoicesApply()
{
    var operators = new[]
    {
        new SukiChoiceItem("operator", "gummy", "グム", "★4 重装 / 庇護衛士", "重装", "庇護衛士", "", "", 4, 1, false),
        new SukiChoiceItem("operator", "rain", "レイン", "★5 狙撃 / 速射手", "狙撃", "速射手", "", "", 5, 2, false),
    };
    operators[0].IsSelected = true;
    operators[1].IsExcluded = true;
    var relics = new[]
    {
        new SukiChoiceItem("relic", "is5_sarkaz_relic_001", "秘宝A", "No.001", "", "", "is5_sarkaz", "食品", 0, 1, false),
        new SukiChoiceItem("relic", "is5_sarkaz_relic_002", "秘宝B", "No.002", "", "", "is5_sarkaz", "食品", 0, 2, false),
    };
    relics[0].IsSelected = true;
    relics[1].IsExcluded = true;

    var updated = JsonNode.Parse(RhodesStateApiClient.ApplyChoicesToStateJson(
        """
        {
          "version": 1,
          "run": { "campaignId": "is5_sarkaz", "hope": 3 },
          "operators": [],
          "relics": [],
          "preferences": { "ocrEngine": "glm-ocr" }
        }
        """,
        operators,
        relics,
        new SukiChoicePersistenceOptions(true, true, false, false, true, true, 4, 3),
        DateTimeOffset.Parse("2026-07-01T00:00:00Z")))!.AsObject();

    Equal("gummy", updated["operators"]!.AsArray()[0]!.GetValue<string>(), "api selected operator");
    Equal("is5_sarkaz_relic_001", updated["relics"]!.AsArray()[0]!.GetValue<string>(), "api selected relic");
    var preferences = updated["preferences"]!.AsObject();
    Equal("rain", preferences["operatorExcludedIds"]!.AsArray()[0]!.GetValue<string>(), "api operator exclusion");
    Equal("is5_sarkaz_relic_002", preferences["relicExcludedIds"]!.AsArray()[0]!.GetValue<string>(), "api relic exclusion");
    Equal("glm-ocr", preferences["ocrEngine"]!.GetValue<string>(), "api ocr preserved");
    Equal(false, updated["run"]!.AsObject().ContainsKey("hope"), "api choices prune abandoned run value");
    Equal("2026-07-01T00:00:00.0000000Z", updated["updatedAt"]!.GetValue<string>(), "api choices updatedAt");
}

static void RunContextPersistence()
{
    var state = JsonNode.Parse(
        """
        {
          "version": 1,
          "run": {
            "campaignId": "is3_mizuki",
            "squad": "分隊A",
            "squadId": "is3_mizuki_squad_01",
            "squadRandomEffectOptionId": "is3_mizuki_effect_01",
            "performanceId": "is2_phantom_performance_pcsp1",
            "difficulty": "等級12",
            "hope": 5,
            "maxHope": 8,
            "ingot": 21,
            "lifePoints": 4,
            "shield": 2,
            "commandLevel": 6,
            "idea": 3,
            "special": { "is3_mizuki": { "light": 20 } }
          },
          "operators": ["gummy"],
          "relics": ["is3_relic_001"],
          "preferences": { "operatorGridColumns": 4 }
        }
        """)!.AsObject();
    var updated = RhodesRunStateStore.ApplyRunContext(
        state,
        "is5_sarkaz",
        DateTimeOffset.Parse("2026-07-01T00:00:00Z"));
    var run = updated["run"]!.AsObject();
    Equal("is5_sarkaz", run["campaignId"]!.GetValue<string>(), "campaign id");
    Equal(false, run.ContainsKey("hope"), "stale hope removed");
    Equal(false, run.ContainsKey("maxHope"), "stale max hope removed");
    Equal(false, run.ContainsKey("squad"), "stale squad removed");
    Equal(false, run.ContainsKey("squadId"), "stale squad id removed");
    Equal(false, run.ContainsKey("squadRandomEffectOptionId"), "stale squad random effect removed");
    Equal(false, run.ContainsKey("performanceId"), "stale performance removed");
    Equal(false, run.ContainsKey("special"), "stale special values removed");
    Equal(false, run.ContainsKey("commandLevel"), "stale command level removed");
    Equal("gummy", updated["operators"]!.AsArray()[0]!.GetValue<string>(), "operators preserved");
    Equal("is3_relic_001", updated["relics"]!.AsArray()[0]!.GetValue<string>(), "relics preserved");
    Equal(4, updated["preferences"]!.AsObject()["operatorGridColumns"]!.GetValue<int>(), "preferences preserved");
    Equal("2026-07-01T00:00:00.0000000Z", updated["updatedAt"]!.GetValue<string>(), "updatedAt");

    var sameCampaign = JsonNode.Parse("""{ "run": { "campaignId": "is5_sarkaz", "hope": 3 } }""")!.AsObject();
    RhodesRunStateStore.ApplyRunContext(sameCampaign, "is5_sarkaz", DateTimeOffset.Parse("2026-07-01T00:00:00Z"));
    Equal(false, sameCampaign["run"]!.AsObject().ContainsKey("hope"), "same campaign prunes abandoned run values");
}

static void StateApiRunContextApply()
{
    var updated = JsonNode.Parse(RhodesStateApiClient.ApplyRunContextToStateJson(
        """
        {
          "version": 1,
          "run": {
            "campaignId": "is3_mizuki",
            "hope": 5,
            "commandLevel": 6,
            "special": { "is3_mizuki": { "light": 20 } }
          },
          "operators": ["gummy"],
          "relics": ["is3_relic_001"],
          "preferences": { "ocrEngine": "glm-ocr" }
        }
        """,
        "is5_sarkaz",
        DateTimeOffset.Parse("2026-07-01T00:00:00Z")))!.AsObject();

    var run = updated["run"]!.AsObject();
    Equal("is5_sarkaz", run["campaignId"]!.GetValue<string>(), "api campaign id");
    Equal(false, run.ContainsKey("hope"), "api stale hope removed");
    Equal(false, run.ContainsKey("special"), "api stale special removed");
    Equal(false, run.ContainsKey("commandLevel"), "api stale command level removed");
    Equal("gummy", updated["operators"]!.AsArray()[0]!.GetValue<string>(), "api operators preserved");
    Equal("glm-ocr", updated["preferences"]!.AsObject()["ocrEngine"]!.GetValue<string>(), "api preferences preserved");
    Equal("2026-07-01T00:00:00.0000000Z", updated["updatedAt"]!.GetValue<string>(), "api updatedAt");
}

static void StateApiCandidatesApply()
{
    var result = RhodesStateApiClient.ApplyCandidatesToStateJson(
        """
        {
          "version": 1,
          "run": {
            "campaignId": "is5_sarkaz",
            "hope": 0,
            "special": { "is5_sarkaz": { "idea": 0 } }
          },
          "operators": []
        }
        """,
        [
            new MaaCandidatePreview("runStatus", "希望", "7", "7", 0.9, Field: "hope", CampaignId: "is5_sarkaz"),
            new MaaCandidatePreview("runStatus", "源石錐", "20", "20", 0.9, Field: "ingot", CampaignId: "is5_sarkaz"),
            new MaaCandidatePreview("runStatus", "構想", "3", "3", 0.9, Field: "idea", CampaignId: "is5_sarkaz"),
        ],
        DateTimeOffset.Parse("2026-07-01T00:00:00Z"));

    var updated = JsonNode.Parse(result.StateJson)!.AsObject();
    Equal(2, result.Summary.AppliedCount, "api candidates applied count");
    Equal(false, updated["run"]!.AsObject().ContainsKey("hope"), "discarded api candidate hope pruned");
    Equal(20, updated["run"]!.AsObject()["ingot"]!.GetValue<int>(), "api candidate ingot");
    Equal(3, updated["run"]!.AsObject()["special"]!.AsObject()["is5_sarkaz"]!.AsObject()["idea"]!.GetValue<int>(), "api candidate idea");
    Equal("2026-07-01T00:00:00.0000000Z", updated["updatedAt"]!.GetValue<string>(), "api candidate updatedAt");
}

static void CandidateRunStatusApply()
{
    var state = JsonNode.Parse(
        """
        {
          "run": {
            "campaignId": "is5_sarkaz",
            "hope": 0,
            "special": { "is5_sarkaz": { "idea": 0 } }
          },
          "operators": ["gummy"]
        }
        """)!.AsObject();
    var candidates = new[]
    {
        new MaaCandidatePreview("runStatus", "希望", "3", "3", 0.94, Field: "hope"),
        new MaaCandidatePreview("runStatus", "希望上限", "8", "8", 0.95, Field: "maxHope"),
        new MaaCandidatePreview("runStatus", "源石錐", "20", "20", 0.96, Field: "ingot"),
        new MaaCandidatePreview("runStatus", "指揮Lv", "0", "0", 0.80, Field: "commandLevel"),
        new MaaCandidatePreview("runStatus", "等級", "18", "18", 0.88, Field: "difficulty"),
        new MaaCandidatePreview("runStatus", "構想", "7", "7", 0.86, Field: "idea", CampaignId: "is5_sarkaz"),
        new MaaCandidatePreview("operator", "グム", "gummy", "グム", 0.91, OperatorId: "gummy"),
        new MaaCandidatePreview("runStatus", "壊れた値", "abc", "abc", 0.20, Field: "shield"),
    };

    var summary = RhodesRecognitionCandidateApplier.ApplyRunStatus(
        state,
        candidates,
        DateTimeOffset.Parse("2026-07-01T00:00:00Z"));

    Equal(3, summary.AppliedCount, "applied count");
    Equal(5, summary.IgnoredCount, "ignored count");
    Equal("ingot|difficulty|idea", string.Join("|", summary.AppliedFields), "applied fields");
    Equal(8, summary.Outcomes.Count, "candidate outcome count");
    Equal("applied", summary.Outcomes.Single(item => item.AppliedField == "ingot").Outcome, "ingot candidate applied outcome");
    Equal("abandoned-run-field", summary.Outcomes.Single(item => item.Label == "希望").IgnoredReason, "hope candidate ignored reason");
    Equal("abandoned-run-field", summary.Outcomes.Single(item => item.Label == "指揮Lv").IgnoredReason, "command level candidate ignored reason");
    Equal("run-status-only", summary.Outcomes.Single(item => item.Label == "グム").IgnoredReason, "operator ignored in run status reason");
    Equal("abandoned-run-field", summary.Outcomes.Single(item => item.Label == "壊れた値").IgnoredReason, "abandoned shield ignored reason");
    var run = state["run"]!.AsObject();
    Equal(20, run["ingot"]!.GetValue<int>(), "ingot");
    Equal(false, run.ContainsKey("hope"), "discarded hope pruned");
    Equal(false, run.ContainsKey("maxHope"), "discarded max hope not written");
    Equal(false, run.ContainsKey("commandLevel"), "discarded command level not written");
    Equal(18, run["difficulty"]!.GetValue<int>(), "difficulty");
    // 等級は多元化珍品(効果バリアント)のtierと結びつく: run.difficulty -> run.difficultyTierId。
    Equal("imaginary", run["difficultyTierId"]!.GetValue<string>(), "difficulty tier derived for is5 curio variants");
    Equal("realistic", RhodesDifficultyTierCatalog.ResolveTierId("is5_sarkaz", 2), "tier boundary 2 is realistic");
    Equal("original", RhodesDifficultyTierCatalog.ResolveTierId("is5_sarkaz", 3), "tier boundary 3 is original");
    Equal("fantastical", RhodesDifficultyTierCatalog.ResolveTierId("is5_sarkaz", 8), "tier boundary 8 is fantastical");
    Equal("imaginary", RhodesDifficultyTierCatalog.ResolveTierId("is5_sarkaz", 9), "tier boundary 9 is imaginary");
    Equal("", RhodesDifficultyTierCatalog.ResolveTierId("is2_phantom", 5), "campaigns without variant curios derive no tier");
    Equal(7, run["special"]!.AsObject()["is5_sarkaz"]!.AsObject()["idea"]!.GetValue<int>(), "idea");
    Equal("gummy", state["operators"]!.AsArray()[0]!.GetValue<string>(), "unrelated selections preserved");
    Equal("2026-07-01T00:00:00.0000000Z", state["updatedAt"]!.GetValue<string>(), "updatedAt");
}

static void CandidateRunStatusApplySquadRandomEffect()
{
    var state = JsonNode.Parse("""{ "run": { "campaignId": "is5_sarkaz" } }""")!.AsObject();
    var candidates = new[]
    {
        new MaaCandidatePreview("runStatus", "奇想天外分隊", "is5_sarkaz_squad_16", "手動入力", 1.0, Field: "squadId", CampaignId: "is5_sarkaz"),
        new MaaCandidatePreview("runStatus", "奇想天外分隊 追加効果", "is5_sarkaz_mimic_02", "手動入力", 1.0, Field: "squadRandomEffectOptionId", CampaignId: "is5_sarkaz"),
    };

    var summary = RhodesRecognitionCandidateApplier.ApplyRunStatus(
        state,
        candidates,
        DateTimeOffset.Parse("2026-07-01T00:00:00Z"));

    Equal(2, summary.AppliedCount, "squad and random effect applied");
    Equal("squadId|squadRandomEffectOptionId", string.Join("|", summary.AppliedFields), "applied fields");
    var run = state["run"]!.AsObject();
    Equal("is5_sarkaz_squad_16", run["squadId"]!.GetValue<string>(), "squad id");
    Equal("is5_sarkaz_mimic_02", run["squadRandomEffectOptionId"]!.GetValue<string>(), "random effect id");
}

static void CandidateRunStatusApplyPhantomSpecials()
{
    var state = JsonNode.Parse("""{ "run": { "campaignId": "is2_phantom" } }""")!.AsObject();
    var candidates = new[]
    {
        new MaaCandidatePreview(
            "runStatus",
            "演目 (手動入力)",
            "is2_phantom_performance_pcsp1",
            "手動入力",
            1.0,
            Field: "performanceId",
            CampaignId: "is2_phantom"),
        new MaaCandidatePreview(
            "runStatus",
            "幻覚 (手動入力)",
            "偏執的な / 盲目 / 偏執的な",
            "手動入力",
            1.0,
            Field: "hallucinations",
            CampaignId: "is2_phantom"),
    };

    var summary = RhodesRecognitionCandidateApplier.ApplyRunStatus(
        state,
        candidates,
        DateTimeOffset.Parse("2026-07-19T00:00:00Z"));

    Equal(2, summary.AppliedCount, "Phantom manual special fields applied");
    var run = state["run"]!.AsObject();
    Equal("is2_phantom_performance_pcsp1", run["performanceId"]!.GetValue<string>(), "performance id");
    var hallucinations = run["special"]!["is2_phantom"]!["hallucinations"]!.AsArray();
    Equal("偏執|盲目", string.Join("|", hallucinations.Select(item => item!.GetValue<string>())), "hallucinations normalized and deduplicated");

    var cleared = RhodesRecognitionCandidateApplier.ApplyRunStatus(
        state,
        [
            RhodesRecognitionCandidateApplier.CreateNoPerformanceCandidate(),
            RhodesRecognitionCandidateApplier.CreateNoHallucinationCandidate(),
        ],
        DateTimeOffset.Parse("2026-07-19T00:01:00Z"));
    Equal(2, cleared.AppliedCount, "Phantom manual special fields clear through the same route");
    Equal(false, run.ContainsKey("performanceId"), "performance cleared");
    Equal(false, run["special"]!["is2_phantom"]!.AsObject().ContainsKey("hallucinations"), "hallucinations cleared");
}

static void CandidateRunStatusRejectsOtherCampaign()
{
    var state = JsonNode.Parse(
        """
        {
          "run": {
            "campaignId": "is5_sarkaz",
            "difficulty": 12,
            "squadId": "is5_sarkaz_squad_01",
            "squadRandomEffectOptionId": "is5_sarkaz_mimic_01"
          }
        }
        """)!.AsObject();
    var candidates = new[]
    {
        new MaaCandidatePreview("runStatus", "別IS等級", "18", "18", 0.99, Field: "difficulty", CampaignId: "is4_sami"),
        new MaaCandidatePreview("runStatus", "別IS分隊", "is4_sami_squad_01", "別IS分隊", 0.99, Field: "squadId", CampaignId: "is4_sami"),
        new MaaCandidatePreview("runStatus", "別IS分隊効果", "is4_sami_option_01", "別IS分隊効果", 0.99, Field: "squadRandomEffectOptionId", CampaignId: "is4_sami"),
        new MaaCandidatePreview("runStatus", "源石錐", "20", "20", 0.90, Field: "ingot"),
    };

    var summary = RhodesRecognitionCandidateApplier.ApplyRunStatus(
        state,
        candidates,
        DateTimeOffset.Parse("2026-07-01T00:00:00Z"));

    Equal(1, summary.AppliedCount, "only neutral run status applied");
    Equal(3, summary.IgnoredCount, "other campaign run status ignored");
    Equal("ingot", string.Join("|", summary.AppliedFields), "applied retained neutral field");
    var run = state["run"]!.AsObject();
    Equal(12, run["difficulty"]!.GetValue<int>(), "difficulty preserved");
    Equal("is5_sarkaz_squad_01", run["squadId"]!.GetValue<string>(), "squad preserved");
    Equal("is5_sarkaz_mimic_01", run["squadRandomEffectOptionId"]!.GetValue<string>(), "squad option preserved");
    Equal(20, run["ingot"]!.GetValue<int>(), "neutral ingot applied");
}

static void CandidateRunStatusKeepsCurrentCampaignDuplicate()
{
    var state = JsonNode.Parse("""{ "run": { "campaignId": "is5_sarkaz" } }""")!.AsObject();
    var candidates = new[]
    {
        new MaaCandidatePreview("runStatus", "別IS等級", "14", "14", 0.99, Field: "difficulty", CampaignId: "is4_sami"),
        new MaaCandidatePreview("runStatus", "サルカズ等級", "18", "18", 0.80, Field: "difficulty", CampaignId: "is5_sarkaz"),
        new MaaCandidatePreview("runStatus", "別IS分隊", "is4_sami_squad_01", "別IS分隊", 0.99, Field: "squadId", CampaignId: "is4_sami"),
        new MaaCandidatePreview("runStatus", "サルカズ分隊", "is5_sarkaz_squad_01", "サルカズ分隊", 0.80, Field: "squadId", CampaignId: "is5_sarkaz"),
    };

    var summary = RhodesRecognitionCandidateApplier.ApplyRunStatus(
        state,
        candidates,
        DateTimeOffset.Parse("2026-07-01T00:00:00Z"));

    Equal(2, summary.AppliedCount, "current campaign values applied");
    Equal(2, summary.IgnoredCount, "other campaign values ignored");
    Equal("difficulty|squadId", string.Join("|", summary.AppliedFields), "applied current campaign fields");
    var run = state["run"]!.AsObject();
    Equal(18, run["difficulty"]!.GetValue<int>(), "current difficulty");
    Equal("is5_sarkaz_squad_01", run["squadId"]!.GetValue<string>(), "current squad");
}

static void CandidateCampaignApplyFirst()
{
    var state = JsonNode.Parse(
        """
        {
          "run": {
            "campaignId": "is3_mizuki",
            "hope": 99,
            "special": { "is3_mizuki": { "light": 20 } }
          },
          "operators": ["gummy"],
          "relics": ["is3_relic_001"]
        }
        """)!.AsObject();
    var candidates = new[]
    {
        new MaaCandidatePreview("runStatus", "希望", "3", "3", 0.94, Field: "hope"),
        new MaaCandidatePreview("runStatus", "源石錐", "20", "20", 0.94, Field: "ingot"),
        new MaaCandidatePreview("runStatus", "統合戦略", "is5_sarkaz", "サルカズの炉辺奇談", 0.99, Field: "campaignId"),
        new MaaCandidatePreview("runStatus", "構想", "7", "7", 0.86, Field: "idea", CampaignId: "is5_sarkaz"),
    };

    var summary = RhodesRecognitionCandidateApplier.ApplyRunStatus(
        state,
        candidates,
        DateTimeOffset.Parse("2026-07-01T00:00:00Z"));

    Equal(3, summary.AppliedCount, "campaign apply count");
    Equal("campaignId|ingot|idea", string.Join("|", summary.AppliedFields), "campaign applied before dependents");
    var run = state["run"]!.AsObject();
    Equal("is5_sarkaz", run["campaignId"]!.GetValue<string>(), "campaign id");
    Equal(false, run.ContainsKey("hope"), "discarded hope ignored after campaign reset");
    Equal(20, run["ingot"]!.GetValue<int>(), "ingot after campaign reset");
    Equal(false, run["special"]!.AsObject().ContainsKey("is3_mizuki"), "old special reset");
    Equal(7, run["special"]!.AsObject()["is5_sarkaz"]!.AsObject()["idea"]!.GetValue<int>(), "new campaign idea");
    Equal("gummy", state["operators"]!.AsArray()[0]!.GetValue<string>(), "operators preserved");
    Equal("is3_relic_001", state["relics"]!.AsArray()[0]!.GetValue<string>(), "relics preserved");
}

static void CandidateRunStatusApplyBestDuplicate()
{
    var state = JsonNode.Parse("""{ "run": { "campaignId": "is5_sarkaz" } }""")!.AsObject();
    var candidates = new[]
    {
        new MaaCandidatePreview("runStatus", "源石錐", "5", "5", 0.95, Field: "ingot"),
        new MaaCandidatePreview("runStatus", "源石錐", "3", "3", 0.40, Field: "ingot"),
    };

    var summary = RhodesRecognitionCandidateApplier.ApplyRunStatus(
        state,
        candidates,
        DateTimeOffset.Parse("2026-07-01T00:00:00Z"));

    Equal(1, summary.AppliedCount, "applied duplicate count");
    Equal(1, summary.IgnoredCount, "ignored duplicate count");
    Equal("ingot", string.Join("|", summary.AppliedFields), "applied duplicate fields");
    Equal(2, summary.Outcomes.Count, "duplicate outcome count");
    Equal("applied", summary.Outcomes[0].Outcome, "best duplicate applied");
    Equal("ignored", summary.Outcomes[1].Outcome, "lower duplicate ignored");
    Equal("lower-confidence-duplicate", summary.Outcomes[1].IgnoredReason, "lower duplicate ignored reason");
    var run = state["run"]!.AsObject();
    Equal(5, run["ingot"]!.GetValue<int>(), "best ingot");
}

static void CandidateChoiceApply()
{
    var state = JsonNode.Parse(
        """
        {
          "run": { "campaignId": "is5_sarkaz" },
          "operators": ["gummy"],
          "relics": ["is5_sarkaz_relic_001"]
        }
        """)!.AsObject();
    var candidates = new[]
    {
        new MaaCandidatePreview("operator", "レイン", "rain", "レイン", 0.92, OperatorId: "rain"),
        new MaaCandidatePreview("operator", "重複", "gummy", "グム", 0.91, OperatorId: "gummy"),
        new MaaCandidatePreview("relic", "秘宝B", "is5_sarkaz_relic_002", "秘宝B", 0.86, RelicId: "is5_sarkaz_relic_002", CampaignId: "is5_sarkaz"),
        new MaaCandidatePreview("relic", "別IS秘宝", "is3_relic_001", "別IS秘宝", 0.86, RelicId: "is3_relic_001", CampaignId: "is3_mizuki"),
        new MaaCandidatePreview("thought", "別IS思案", "thought_001", "思案", 0.86, CampaignId: "is4_sami", ThoughtId: "thought_001"),
    };

    var summary = RhodesRecognitionCandidateApplier.Apply(
        state,
        candidates,
        DateTimeOffset.Parse("2026-07-01T00:00:00Z"));

    Equal(2, summary.AppliedCount, "applied choice count");
    Equal(3, summary.IgnoredCount, "ignored choice count");
    Equal("operator:rain|relic:is5_sarkaz_relic_002", string.Join("|", summary.AppliedFields), "applied choices");
    Equal("duplicate-operator", summary.Outcomes.Single(item => item.Label == "重複").IgnoredReason, "duplicate operator ignored reason");
    Equal("campaign-mismatch", summary.Outcomes.Single(item => item.Label == "別IS秘宝").IgnoredReason, "other campaign relic ignored reason");
    Equal("campaign-mismatch", summary.Outcomes.Single(item => item.Label == "別IS思案").IgnoredReason, "other campaign thought ignored reason");
    Equal("gummy|rain", string.Join("|", state["operators"]!.AsArray().Select(item => item!.GetValue<string>())), "operators");
    Equal("is5_sarkaz_relic_001|is5_sarkaz_relic_002", string.Join("|", state["relics"]!.AsArray().Select(item => item!.GetValue<string>())), "relics");
    Equal("2026-07-01T00:00:00.0000000Z", state["updatedAt"]!.GetValue<string>(), "choice updatedAt");
}

static void CandidateReserveOperatorCountApply()
{
    var state = JsonNode.Parse(
        """
        {
          "run": { "campaignId": "is5_sarkaz" },
          "operators": [],
          "operatorCounts": { "reserve_caster": 5 }
        }
        """)!.AsObject();

    var counted = new MaaCandidatePreview(
        "operator",
        "予備隊員-術師",
        "reserve_caster",
        "予備隊員-術師",
        0.92,
        OperatorId: "reserve_caster",
        Count: 2);
    RhodesRecognitionCandidateApplier.Apply(
        state,
        [counted],
        DateTimeOffset.Parse("2026-07-23T00:00:00Z"));

    Equal("reserve_caster", state["operators"]!.AsArray().Single()!.GetValue<string>(), "reserve operator selected");
    Equal(2, state["operatorCounts"]!.AsObject()["reserve_caster"]!.GetValue<int>(), "recognized reserve count persisted");

    RhodesRecognitionCandidateApplier.Apply(
        state,
        [counted with { Count = 1 }],
        DateTimeOffset.Parse("2026-07-23T00:01:00Z"));
    Equal(false, state["operatorCounts"]!.AsObject().ContainsKey("reserve_caster"), "canonical one-person count is omitted");

    RhodesRecognitionCandidateApplier.Apply(
        state,
        [new MaaCandidatePreview("operator", "グム", "gummy", "グム", 0.92, OperatorId: "gummy", Count: 3)],
        DateTimeOffset.Parse("2026-07-23T00:02:00Z"));
    Equal(false, state["operatorCounts"]!.AsObject().ContainsKey("gummy"), "ordinary operators ignore candidate count");
}

static void CandidateRelicUsageApply()
{
    var state = JsonNode.Parse(
        """
        {
          "run": { "campaignId": "is3_mizuki" },
          "relics": [],
          "usedRelicIds": []
        }
        """)!.AsObject();
    var used = new MaaCandidatePreview(
        "relic",
        "「時の果て」",
        "is3_mizuki_relic_228",
        "[時の果て",
        0.98,
        RelicId: "is3_mizuki_relic_228",
        CampaignId: "is3_mizuki",
        StateId: "used");

    RhodesRecognitionCandidateApplier.Apply(
        state,
        [used],
        DateTimeOffset.Parse("2026-07-19T15:35:40Z"));
    Equal("is3_mizuki_relic_228", state["relics"]!.AsArray().Single()!.GetValue<string>(), "used relic is owned");
    Equal("is3_mizuki_relic_228", state["usedRelicIds"]!.AsArray().Single()!.GetValue<string>(), "used relic flag is applied");

    RhodesRecognitionCandidateApplier.Apply(
        state,
        [used with { StateId = "unused" }],
        DateTimeOffset.Parse("2026-07-19T15:36:00Z"));
    Equal(0, state["usedRelicIds"]!.AsArray().Count, "visible unused relic clears a stale used flag");
}

static void CandidateAmiyaRoleReplacementApply()
{
    var state = JsonNode.Parse(
        """
        {
          "run": { "campaignId": "is5_sarkaz" },
          "operators": ["gummy", "amiya", "amiya2", "amiya3"]
        }
        """)!.AsObject();
    var candidate = new MaaCandidatePreview(
        "operator",
        "アーミヤ(前衛)",
        "amiya2",
        "アーミヤ",
        0.99,
        OperatorId: "amiya2",
        RecognitionKey: "maa-local:operator-role:amiya2");

    var summary = RhodesRecognitionCandidateApplier.Apply(
        state,
        [candidate],
        DateTimeOffset.Parse("2026-07-19T00:00:00Z"));

    Equal(1, summary.AppliedCount, "Amiya role replacement apply count");
    Equal("operator:amiya2", summary.AppliedFields.Single(), "Amiya role replacement applied field");
    Equal(
        "gummy|amiya2",
        string.Join("|", state["operators"]!.AsArray().Select(item => item!.GetValue<string>())),
        "profession-resolved Amiya removes stale forms from state");
}

static void CandidateIs5SpecialApply()
{
    var state = JsonNode.Parse(
        """
        {
          "run": {
            "campaignId": "is5_sarkaz",
            "difficulty": 18,
            "special": { "is5_sarkaz": { "idea": 21 } }
          }
        }
        """)!.AsObject();
    var candidates = new[]
    {
        new MaaCandidatePreview("thought", "枯れ木と若枝", "fallback_a", "枯れ木と若枝", 0.91, CampaignId: "is5_sarkaz", ThoughtId: "thought_a"),
        new MaaCandidatePreview("thought", "枯れ木と若枝", "fallback_a", "枯れ木と若枝", 0.88, CampaignId: "is5_sarkaz", ThoughtId: "thought_a"),
        new MaaCandidatePreview("thought", "走る都市", "thought_b", "走る都市", 0.86, CampaignId: "is5_sarkaz"),
        new MaaCandidatePreview("age", "形成期", "age_formation", "形成期", 0.65, CampaignId: "is5_sarkaz", AgeId: "age_formation"),
        new MaaCandidatePreview("age", "全盛期", "age_prime", "全盛期", 0.95, CampaignId: "is5_sarkaz", AgeId: "age_prime"),
        new MaaCandidatePreview("age", "別IS時代", "age_other", "別IS時代", 0.99, CampaignId: "is4_sami", AgeId: "age_other"),
    };

    var summary = RhodesRecognitionCandidateApplier.Apply(
        state,
        candidates,
        DateTimeOffset.Parse("2026-07-01T00:00:00Z"));

    Equal(5, summary.AppliedCount, "applied is5 special count");
    Equal(1, summary.IgnoredCount, "ignored is5 special count");
    Equal("thought:thought_a|thought:thought_a|thought:thought_b|age:age_formation|age:age_prime", string.Join("|", summary.AppliedFields), "applied is5 special fields");
    var special = state["run"]!.AsObject()["special"]!.AsObject()["is5_sarkaz"]!.AsObject();
    var thought = special["thought"]!.AsArray();
    Equal(2, thought.Count, "thought item count");
    Equal("thought_a", thought[0]!.AsObject()["effectId"]!.GetValue<string>(), "first thought id");
    Equal(2, thought[0]!.AsObject()["count"]!.GetValue<int>(), "first thought count");
    Equal("thought_b", thought[1]!.AsObject()["effectId"]!.GetValue<string>(), "second thought id");
    Equal(1, thought[1]!.AsObject()["count"]!.GetValue<int>(), "second thought count");
    Equal(true, special["thoughtOverlayVisible"]!.GetValue<bool>(), "thought overlay enabled after apply");
    Equal("age_prime", special["age"]!.GetValue<string>(), "best age");
    Equal(21, special["idea"]!.GetValue<int>(), "existing idea preserved");
    Equal("2026-07-01T00:00:00.0000000Z", state["updatedAt"]!.GetValue<string>(), "is5 special updatedAt");
}

static void CandidateIs5AgeClearApply()
{
    var state = JsonNode.Parse(
        """
        {
          "run": {
            "campaignId": "is5_sarkaz",
            "difficulty": 18,
            "special": { "is5_sarkaz": { "age": "is5_sarkaz_selectable_age_is5_age_01_prime" } }
          }
        }
        """)!.AsObject();

    var summary = RhodesRecognitionCandidateApplier.Apply(
        state,
        [RhodesRecognitionCandidateApplier.CreateNoAgeCandidate()],
        DateTimeOffset.Parse("2026-07-18T00:00:00Z"));

    Equal(1, summary.AppliedCount, "age clear apply count");
    Equal($"age:{RhodesRecognitionCandidateApplier.NoAgeId}", summary.AppliedFields.Single(), "age clear field");
    var special = state["run"]!.AsObject()["special"]!.AsObject()["is5_sarkaz"]!.AsObject();
    Equal(true, special["age"] is null, "age is cleared when recognition detects none");
}

static void CandidateMizukiSpecialApply()
{
    var state = JsonNode.Parse(
        """
        {
          "run": { "campaignId": "is3_mizuki" },
          "operators": ["kroos", "fang"]
        }
        """)!.AsObject();
    var candidates = new[]
    {
        new MaaCandidatePreview("mizuki", "鍵", "2", "2", 0.96, CampaignId: "is3_mizuki", FieldId: "key"),
        new MaaCandidatePreview("mizuki", "灯火", "0", "0", 0.95, CampaignId: "is3_mizuki", FieldId: "light"),
        new MaaCandidatePreview("mizuki", "呼び声：栄枯", "is3_mizuki_selectable_hordeCall_mcasci15", "呼び声：栄枯", 0.93, CampaignId: "is3_mizuki", FieldId: "hordeCalls", EffectId: "is3_mizuki_selectable_hordeCall_mcasci15"),
        new MaaCandidatePreview("mizuki", "呼び声：給養", "is3_mizuki_selectable_hordeCall_mcasci16", "呼び声：給養", 0.91, CampaignId: "is3_mizuki", FieldId: "hordeCalls", EffectId: "is3_mizuki_selectable_hordeCall_mcasci16"),
        new MaaCandidatePreview("mizuki", "障害と異変", "is3_mizuki_selectable_rejectionReaction_mcasci25", "障害と異変", 0.94, CampaignId: "is3_mizuki", FieldId: "rejectionReaction", EffectId: "is3_mizuki_selectable_rejectionReaction_mcasci25"),
        new MaaCandidatePreview("mizuki", "クルース", "kroos", "クルース", 0.92, CampaignId: "is3_mizuki", FieldId: "rejectionReaction", OperatorId: "kroos"),
        new MaaCandidatePreview("mizuki", "フェン", "fang", "フェン", 0.92, CampaignId: "is3_mizuki", FieldId: "rejectionReaction", OperatorId: "fang"),
        new MaaCandidatePreview("mizuki", "予備隊員-近距離", "reserve_melee", "予備隊員-近距離", 0.92, CampaignId: "is3_mizuki", FieldId: "rejectionReaction", OperatorId: "reserve_melee"),
    };

    var summary = RhodesRecognitionCandidateApplier.Apply(
        state,
        candidates,
        DateTimeOffset.Parse("2026-07-19T06:30:00Z"));

    Equal(7, summary.AppliedCount, "Mizuki special candidates applied");
    Equal(1, summary.IgnoredCount, "unrecruited rejection target ignored");
    Equal(
        "operator-not-recruited",
        summary.Outcomes.Single(outcome => outcome.Label == "予備隊員-近距離").IgnoredReason,
        "unrecruited rejection target reason");
    var special = state["run"]!["special"]!["is3_mizuki"]!.AsObject();
    Equal(2, special["key"]!.GetValue<int>(), "Mizuki key persisted");
    Equal(0, special["light"]!.GetValue<int>(), "Mizuki light persisted");
    Equal(
        "is3_mizuki_selectable_hordeCall_mcasci15|is3_mizuki_selectable_hordeCall_mcasci16",
        string.Join('|', special["hordeCalls"]!.AsArray().Select(item => item!.GetValue<string>())),
        "Mizuki Horde's Calls persisted");
    var rejection = special["rejectionReaction"]!.AsObject();
    Equal("is3_mizuki_selectable_rejectionReaction_mcasci25", rejection["effectId"]!.GetValue<string>(), "Mizuki rejection persisted");
    Equal(
        "kroos|fang",
        string.Join('|', rejection["operatorIds"]!.AsArray().Select(item => item!.GetValue<string>())),
        "Mizuki rejection targets restricted to recruited operators");
}

static void CandidateMizukiRejectionEffectOnlyPreservesTargets()
{
    var state = JsonNode.Parse(
        """
        {
          "run": {
            "campaignId": "is3_mizuki",
            "special": {
              "is3_mizuki": {
                "rejectionReaction": {
                  "effectId": "is3_mizuki_selectable_rejectionReaction_mcasci22",
                  "operatorIds": ["exusiai2", "hoshiguma2"]
                }
              }
            }
          },
          "operators": ["exusiai2", "hoshiguma2"]
        }
        """)!.AsObject();

    var summary = RhodesRecognitionCandidateApplier.Apply(
        state,
        [new MaaCandidatePreview(
            "mizuki",
            "退行と異変",
            "is3_mizuki_selectable_rejectionReaction_mcasci22",
            "退行と異変",
            0.94,
            CampaignId: "is3_mizuki",
            FieldId: "rejectionReaction",
            EffectId: "is3_mizuki_selectable_rejectionReaction_mcasci22")],
        DateTimeOffset.Parse("2026-07-23T03:00:00Z"));

    Equal(1, summary.AppliedCount, "Mizuki rejection effect-only candidate applied");
    var rejection = state["run"]!["special"]!["is3_mizuki"]!["rejectionReaction"]!.AsObject();
    Equal(
        "exusiai2|hoshiguma2",
        string.Join('|', rejection["operatorIds"]!.AsArray().Select(item => item!.GetValue<string>())),
        "effect-only refresh preserves previously detected rejection targets");
}

static void CandidateSuiSeasonalHoursApply()
{
    var state = JsonNode.Parse(
        """
        {
          "run": {
            "campaignId": "is6_sui",
            "special": {
              "is6_sui": {
                "seasonalHours": ["stale-seasonal-hour"],
                "seasonalHourTargets": ["重装"]
              }
            }
          }
        }
        """)!.AsObject();
    var candidates = new[]
    {
        new MaaCandidatePreview(
            "sui",
            "巳農（醒覚）",
            "is6_sui_selectable_seasonalHours_is6sst6_awakening",
            "巳農 醒覚 配置コスト-6",
            0.96,
            CampaignId: "is6_sui",
            FieldId: "seasonalHours",
            EffectId: "is6_sui_selectable_seasonalHours_is6sst6_awakening"),
        new MaaCandidatePreview(
            "sui",
            "戌絵（明瞭）",
            "is6_sui_selectable_seasonalHours_is6sst11_meiryou",
            "戌絵 明瞭 先鋒 医療",
            0.94,
            CampaignId: "is6_sui",
            FieldId: "seasonalHours",
            EffectId: "is6_sui_selectable_seasonalHours_is6sst11_meiryou"),
        new MaaCandidatePreview(
            "sui",
            "先鋒 (戌絵対象職分)",
            "先鋒",
            "戌絵 明瞭 先鋒 医療",
            0.93,
            CampaignId: "is6_sui",
            FieldId: "seasonalHourTargets",
            EffectId: "先鋒"),
        new MaaCandidatePreview(
            "sui",
            "医療 (戌絵対象職分)",
            "医療",
            "戌絵 明瞭 先鋒 医療",
            0.93,
            CampaignId: "is6_sui",
            FieldId: "seasonalHourTargets",
            EffectId: "医療"),
    };

    var summary = RhodesRecognitionCandidateApplier.Apply(
        state,
        candidates,
        DateTimeOffset.Parse("2026-07-23T00:00:00Z"));

    Equal(4, summary.AppliedCount, "Sui seasonal hour and target profession candidates applied");
    Equal(
        "is6_sui_selectable_seasonalHours_is6sst6_awakening|is6_sui_selectable_seasonalHours_is6sst11_meiryou",
        string.Join('|', state["run"]!["special"]!["is6_sui"]!["seasonalHours"]!.AsArray().Select(item => item!.GetValue<string>())),
        "Sui seasonal hour selection replaced");
    Equal(
        "先鋒|医療",
        string.Join('|', state["run"]!["special"]!["is6_sui"]!["seasonalHourTargets"]!.AsArray().Select(item => item!.GetValue<string>())),
        "Dog Painting target professions replaced");
}

static void CandidateSuiSeasonalHoursFollowDifficulty()
{
    var state = JsonNode.Parse(
        """
        {
          "run": {
            "campaignId": "is6_sui",
            "difficulty": 5,
            "special": {
              "is6_sui": {
                "seasonalHours": [
                  "is6_sui_selectable_seasonalHours_is6sst11_mourou",
                  "is6_sui_selectable_seasonalHours_is6sst6_awakening"
                ]
              }
            }
          }
        }
        """)!.AsObject();

    var summary = RhodesRecognitionCandidateApplier.ApplyRunStatus(
        state,
        [new MaaCandidatePreview("runStatus", "等級", "12", "12", 1.0, Field: "difficulty", CampaignId: "is6_sui")],
        DateTimeOffset.Parse("2026-07-23T00:05:00Z"));

    Equal(1, summary.AppliedCount, "Sui difficulty candidate applied");
    Equal(
        "is6_sui_selectable_seasonalHours_is6sst11_nyuukotsu|is6_sui_selectable_seasonalHours_is6sst6_awakening",
        string.Join('|', state["run"]!["special"]!["is6_sui"]!["seasonalHours"]!.AsArray().Select(item => item!.GetValue<string>())),
        "normal seasonal hours follow difficulty while awakening remains explicit");
}

static void CandidateSuiSupportMartialApply()
{
    var state = JsonNode.Parse(
        """
        {
          "run": {
            "campaignId": "is6_sui",
            "special": {
              "is6_sui": {
                "supportMartial": ["古い効果"]
              }
            }
          }
        }
        """)!.AsObject();
    var candidates = new[]
    {
        RhodesRecognitionCandidateApplier.CreateNoSuiSupportMartialCandidate(),
        new MaaCandidatePreview(
            "sui",
            "配置時に攻撃速度+20 (支武・手動入力)",
            "配置時に攻撃速度+20",
            "手動入力",
            1.0,
            CampaignId: "is6_sui",
            FieldId: "supportMartial",
            EffectId: "配置時に攻撃速度+20"),
        new MaaCandidatePreview(
            "sui",
            "初回配置コスト-3 (支武・手動入力)",
            "初回配置コスト-3",
            "手動入力",
            1.0,
            CampaignId: "is6_sui",
            FieldId: "supportMartial",
            EffectId: "初回配置コスト-3"),
    };

    var summary = RhodesRecognitionCandidateApplier.Apply(
        state,
        candidates,
        DateTimeOffset.Parse("2026-07-23T00:10:00Z"));

    Equal(3, summary.AppliedCount, "Sui support martial candidates applied");
    Equal(
        "配置時に攻撃速度+20|初回配置コスト-3",
        string.Join('|', state["run"]!["special"]!["is6_sui"]!["supportMartial"]!.AsArray().Select(item => item!.GetValue<string>())),
        "Sui support martial effects replaced");
}

static void CandidateMizukiRejectionTargetOnlyApply()
{
    var state = JsonNode.Parse(
        """
        {
          "run": {
            "campaignId": "is3_mizuki",
            "special": {
              "is3_mizuki": {
                "rejectionReaction": {
                  "effectId": "is3_mizuki_selectable_rejectionReaction_mcasci24",
                  "operatorIds": []
                }
              }
            }
          },
          "operators": ["kroos"]
        }
        """)!.AsObject();
    var summary = RhodesRecognitionCandidateApplier.Apply(
        state,
        [
            new MaaCandidatePreview("operator", "スポット", "spot", "スポット", 0.99, OperatorId: "spot"),
            new MaaCandidatePreview("mizuki", "スポット", "spot", "purple-band", 0.96, CampaignId: "is3_mizuki", FieldId: "rejectionReaction", OperatorId: "spot"),
            new MaaCandidatePreview("mizuki", "クルース", "kroos", "purple-band", 0.95, CampaignId: "is3_mizuki", FieldId: "rejectionReaction", OperatorId: "kroos"),
        ],
        DateTimeOffset.Parse("2026-07-19T14:40:00Z"));

    Equal(3, summary.AppliedCount, "operator and target-only candidates applied");
    var rejection = state["run"]!["special"]!["is3_mizuki"]!["rejectionReaction"]!.AsObject();
    Equal(
        "spot|kroos",
        string.Join('|', rejection["operatorIds"]!.AsArray().Select(item => item!.GetValue<string>())),
        "target-only scan updates the existing rejection reaction");
    Equal(true, state["operators"]!.AsArray().Any(item => item!.GetValue<string>() == "spot"), "newly recognized target is recruited in the same apply batch");
}

static void CandidateMizukiReserveRejectionTargetApply()
{
    var state = JsonNode.Parse(
        """
        {
          "run": {
            "campaignId": "is3_mizuki",
            "special": {
              "is3_mizuki": {
                "rejectionReaction": {
                  "effectId": "is3_mizuki_selectable_rejectionReaction_mcasci24",
                  "operatorIds": []
                }
              }
            }
          },
          "operators": ["reserve_sniper"],
          "operatorCounts": { "reserve_sniper": 3 }
        }
        """)!.AsObject();

    var summary = RhodesRecognitionCandidateApplier.Apply(
        state,
        [
            new MaaCandidatePreview(
                "mizuki",
                "予備隊員-狙撃 2人目",
                "reserve_sniper",
                "purple-band",
                0.96,
                OperatorId: "reserve_sniper",
                CampaignId: "is3_mizuki",
                RecognitionKey: "maa-local:mizuki:rejection-card:reserve_sniper:2",
                FieldId: "rejectionReaction",
                OperatorInstance: 2),
        ],
        DateTimeOffset.Parse("2026-07-23T08:00:00Z"));

    Equal(1, summary.AppliedCount, "individual reserve target candidate applied");
    var rejection = state["run"]!["special"]!["is3_mizuki"]!["rejectionReaction"]!.AsObject();
    Equal(
        "reserve_sniper",
        string.Join('|', rejection["operatorIds"]!.AsArray().Select(item => item!.GetValue<string>())),
        "legacy aggregate operator ids stay compatible");
    var target = rejection["operatorTargets"]!.AsArray().Single()!.AsObject();
    Equal("reserve_sniper", target["operatorId"]!.GetValue<string>(), "target operator id");
    Equal(2, target["instance"]!.GetValue<int>(), "target recruit instance");

    var clearSummary = RhodesRecognitionCandidateApplier.Apply(
        state,
        [RhodesRecognitionCandidateApplier.CreateNoMizukiRejectionTargetCandidate()],
        DateTimeOffset.Parse("2026-07-23T08:01:00Z"));
    Equal(1, clearSummary.AppliedCount, "explicit empty target selection applied");
    Equal(0, rejection["operatorIds"]!.AsArray().Count, "aggregate targets cleared");
    Equal(0, rejection["operatorTargets"]!.AsArray().Count, "individual targets cleared");
}

static void CandidateMizukiEvolutionTargetApply()
{
    var state = JsonNode.Parse(
        """
        {
          "run": {
            "campaignId": "is3_mizuki"
          },
          "operators": ["durin", "reserve_defender"],
          "operatorCounts": { "reserve_defender": 2 }
        }
        """)!.AsObject();

    var summary = RhodesRecognitionCandidateApplier.Apply(
        state,
        [
            new MaaCandidatePreview(
                "mizuki",
                "ドゥリン",
                "durin",
                "yellow-name",
                0.96,
                OperatorId: "durin",
                CampaignId: "is3_mizuki",
                RecognitionKey: "maa-local:mizuki:evolution-card:durin:1",
                FieldId: "operatorEvolution",
                OperatorInstance: 1),
            new MaaCandidatePreview(
                "mizuki",
                "予備隊員-重装 2人目",
                "reserve_defender",
                "yellow-name",
                0.95,
                OperatorId: "reserve_defender",
                CampaignId: "is3_mizuki",
                RecognitionKey: "maa-local:mizuki:evolution-card:reserve_defender:2",
                FieldId: "operatorEvolution",
                OperatorInstance: 2),
        ],
        DateTimeOffset.Parse("2026-07-23T09:00:00Z"));

    Equal(2, summary.AppliedCount, "individual evolution target candidates applied");
    var evolution = state["run"]!["special"]!["is3_mizuki"]!["operatorEvolution"]!.AsObject();
    Equal(
        "durin|reserve_defender",
        string.Join('|', evolution["operatorIds"]!.AsArray().Select(item => item!.GetValue<string>())),
        "evolution aggregate operator ids persisted");
    Equal(
        "durin#1|reserve_defender#2",
        string.Join('|', evolution["operatorTargets"]!.AsArray().Select(item =>
        {
            var target = item!.AsObject();
            return $"{target["operatorId"]!.GetValue<string>()}#{target["instance"]!.GetValue<int>()}";
        })),
        "evolution recruit instances persisted");

    var clearSummary = RhodesRecognitionCandidateApplier.Apply(
        state,
        [RhodesRecognitionCandidateApplier.CreateNoMizukiEvolutionTargetCandidate()],
        DateTimeOffset.Parse("2026-07-23T09:01:00Z"));
    Equal(1, clearSummary.AppliedCount, "explicit empty evolution target selection applied");
    Equal(false, state["run"]!["special"]!["is3_mizuki"]!.AsObject().ContainsKey("operatorEvolution"), "evolution targets cleared");
}

static void CandidateOtherSpecialApply()
{
    var revelationState = JsonNode.Parse("""{ "run": { "campaignId": "is4_sami" } }""")!.AsObject();
    var revelationSummary = RhodesRecognitionCandidateApplier.Apply(
        revelationState,
        [
            new MaaCandidatePreview("revelation", "本因A", "fallback", "本因A", 0.9, CampaignId: "is4_sami", FieldId: "revelationBoard", SlotKind: "cause", EffectId: "cause_a"),
            new MaaCandidatePreview("revelation", "構成A", "fallback", "構成A", 0.9, CampaignId: "is4_sami", FieldId: "revelationBoard", SlotKind: "structure", EffectId: "structure_a"),
            new MaaCandidatePreview("revelation", "修辞A", "fallback", "修辞A", 0.9, CampaignId: "is4_sami", FieldId: "revelationBoard", SlotKind: "rhetoric", EffectId: "rhetoric_a", Count: 2),
            new MaaCandidatePreview("coin", "別IS通宝", "coin_a", "通宝A", 0.9, CampaignId: "is6_sui", CoinId: "coin_a"),
        ],
        DateTimeOffset.Parse("2026-07-01T00:00:00Z"));

    Equal(3, revelationSummary.AppliedCount, "applied revelation count");
    Equal(1, revelationSummary.IgnoredCount, "ignored revelation count");
    var board = revelationState["run"]!.AsObject()["special"]!.AsObject()["is4_sami"]!.AsObject()["revelation"]!.AsObject();
    Equal("cause_a", board["causeId"]!.GetValue<string>(), "revelation cause");
    Equal("structure_a", board["structureId"]!.GetValue<string>(), "revelation structure");
    Equal("rhetoric_a", board["rhetorics"]!.AsArray()[0]!.AsObject()["effectId"]!.GetValue<string>(), "revelation rhetoric");
    Equal(2, board["rhetorics"]!.AsArray()[0]!.AsObject()["count"]!.GetValue<int>(), "revelation rhetoric count");

    var coinState = JsonNode.Parse(
        """
        {
          "run": {
            "campaignId": "is6_sui",
            "special": {
              "is6_sui": {
                "coins": [{ "coinId": "stale_coin", "count": 9, "face": "back" }],
                "activeCoins": [{ "coinId": "stale_active", "count": 1 }]
              }
            }
          }
        }
        """)!.AsObject();
    var coinSummary = RhodesRecognitionCandidateApplier.Apply(
        coinState,
        [
            new MaaCandidatePreview("coin", "保有銭A", "fallback", "通宝A", 0.9, CampaignId: "is6_sui", FieldId: "coins", CoinId: "coin_a", Face: "front", Count: 2),
            new MaaCandidatePreview("coin", "保有銭A", "fallback", "通宝A", 0.9, CampaignId: "is6_sui", FieldId: "coins", CoinId: "coin_a", Face: "back", Count: 3),
            new MaaCandidatePreview("coin", "有効銭A", "fallback", "通宝A", 0.9, CampaignId: "is6_sui", FieldId: "activeCoins", CoinId: "coin_a", StatusId: "status_a", Face: "back", Count: 2),
        ],
        DateTimeOffset.Parse("2026-07-01T00:00:00Z"));

    Equal(3, coinSummary.AppliedCount, "applied coin count");
    var sui = coinState["run"]!.AsObject()["special"]!.AsObject()["is6_sui"]!.AsObject();
    var coins = sui["coins"]!.AsArray();
    Equal(1, coins.Count, "owned coin kind count");
    Equal("coin_a", coins[0]!.AsObject()["coinId"]!.GetValue<string>(), "owned coin id");
    Equal(5, coins[0]!.AsObject()["count"]!.GetValue<int>(), "owned duplicate count");
    Equal(false, coins[0]!.AsObject().ContainsKey("face"), "owned coin has no face");
    var activeCoins = sui["activeCoins"]!.AsArray();
    Equal(1, activeCoins.Count, "active coin kind count");
    Equal("coin_a", activeCoins[0]!.AsObject()["coinId"]!.GetValue<string>(), "active coin id");
    Equal(2, activeCoins[0]!.AsObject()["count"]!.GetValue<int>(), "active coin count");
    Equal("status_a", activeCoins[0]!.AsObject()["statusId"]!.GetValue<string>(), "active coin status");
    Equal(false, activeCoins[0]!.AsObject().ContainsKey("face"), "active coin has no face");
}

static void CandidateManualSuiValuesApply()
{
    var state = JsonNode.Parse(
        """
        {
          "run": {
            "campaignId": "is6_sui",
            "special": {
              "is6_sui": {
                "ticket": 1,
                "coins": [{ "coinId": "stale_coin", "count": 9 }],
                "activeCoins": [{ "coinId": "stale_active", "count": 1 }]
              }
            }
          }
        }
        """)!.AsObject();

    var summary = RhodesRecognitionCandidateApplier.Apply(
        state,
        [
            new MaaCandidatePreview("runStatus", "遊覧券 (手動入力)", "7", "手動入力", 1.0, Field: "ticket", CampaignId: "is6_sui"),
            RhodesRecognitionCandidateApplier.CreateNoSuiCoinCandidate("activeCoins"),
            RhodesRecognitionCandidateApplier.CreateNoSuiCoinCandidate("coins"),
            new MaaCandidatePreview("coin", "保有銭A", "fallback", "通宝A", 1.0, CampaignId: "is6_sui", FieldId: "coins", CoinId: "coin_a", StatusId: "status_a", Count: 2),
            new MaaCandidatePreview("coin", "保有銭A", "fallback", "通宝A", 1.0, CampaignId: "is6_sui", FieldId: "coins", CoinId: "coin_a", StatusId: "status_b", Count: 1),
        ],
        DateTimeOffset.Parse("2026-07-22T00:00:00Z"));

    Equal(5, summary.AppliedCount, "manual Sui values apply count");
    var sui = state["run"]!["special"]!["is6_sui"]!.AsObject();
    Equal(7, sui["ticket"]!.GetValue<int>(), "manual ticket");
    Equal(0, sui["activeCoins"]!.AsArray().Count, "manual active coins can be cleared");
    var coins = sui["coins"]!.AsArray();
    Equal(2, coins.Count, "same coin can keep separate statuses");
    Equal(
        "status_a:2|status_b:1",
        string.Join('|', coins.Select(item => $"{item!["statusId"]!.GetValue<string>()}:{item["count"]!.GetValue<int>()}")),
        "coin status and count");
}

static string NormalizeLineEndings(string value)
{
    return value.Replace("\r\n", "\n", StringComparison.Ordinal);
}

static IReadOnlyList<string> PipelineEntries(string path)
{
    using var document = JsonDocument.Parse(File.ReadAllText(path));
    return document.RootElement
        .EnumerateObject()
        .Select(property => property.Name)
        .ToArray();
}

static string NormalizeTaskEntry(string entry)
{
    var normalized = new System.Text.StringBuilder(entry.Length + 8);
    for (var index = 0; index < entry.Length; index++)
    {
        var character = entry[index];
        if (index > 0 && char.IsUpper(character) && char.IsLower(entry[index - 1]))
            normalized.Append('_');
        normalized.Append(char.ToLowerInvariant(character));
    }
    return string.Join("_", normalized.ToString().Split(['_', '.', '-'], StringSplitOptions.RemoveEmptyEntries));
}

static MaaTaskRunResult OcrTask(string entry, string text, double score)
{
    return new MaaTaskRunResult(
        entry,
        "Succeeded",
        true,
        "detail",
        $"TaskId=1; detail={{\"best\":{{\"text\":\"{text}\",\"score\":{score.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}}}",
        "OCR",
        true);
}

static void Equal<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected {expected}, got {actual}");
}

static void ThrowsInvalidOperation(Action action, string label)
{
    try
    {
        action();
    }
    catch (InvalidOperationException)
    {
        return;
    }

    throw new InvalidOperationException($"{label}: expected InvalidOperationException");
}
