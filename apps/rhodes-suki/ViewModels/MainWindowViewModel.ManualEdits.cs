using System.Globalization;
using RhodesSuki.Models;
using RhodesSuki.Services;

namespace RhodesSuki.ViewModels;

public sealed partial class MainWindowViewModel
{
    private sealed record ManualRunDraft(int Ingot, int Difficulty, string SquadId, string EffectId);
    private sealed record TournamentDraft(int Score, int Withdrawals, string Memo);

    private ManualRunDraft? _loadedManualRunDraft;
    private TournamentDraft? _loadedTournamentDraft;
    private string _manualDraftCampaignId = "";
    private bool _hasEditedManualRunValues;
    private bool _hasEditedTournamentInfo;
    private bool _isRefreshingManualDrafts;

    private ManualRunDraft ReadManualRunDraft() => new(
        ManualIngot, ManualDifficulty, SelectedManualSquad?.Id ?? "", SelectedManualSquadRandomEffect?.Id ?? "");

    private TournamentDraft ReadTournamentDraft() => new(
        ManualTournamentScore, ManualTournamentWithdrawals, ManualTournamentMemo.Trim());

    public bool HasUnappliedManualRunValues => _hasEditedManualRunValues && (
        ManualIngot != _runState.Ingot
        || ManualDifficulty != CurrentManualDifficulty()
        || (!string.IsNullOrEmpty(SelectedManualSquad?.Id)
            && !string.Equals(SelectedManualSquad.Name, _runState.Squad, StringComparison.Ordinal))
        || (!string.IsNullOrEmpty(SelectedManualSquadRandomEffect?.Id)
            && !string.Equals(SelectedManualSquadRandomEffect.Name, _runState.SquadRandomEffect, StringComparison.Ordinal)));

    public bool HasUnappliedTournamentInfo => _hasEditedTournamentInfo && ReadTournamentDraft() != new TournamentDraft(
        _runState.TournamentInfo?.Score ?? 0,
        _runState.TournamentInfo?.Withdrawals ?? 0,
        (_runState.TournamentInfo?.Memo ?? "").Trim());

    public bool HasUnappliedRunEdits => HasUnappliedManualRunValues || HasUnappliedTournamentInfo;

    public string UnappliedRunEditsLabel => (HasUnappliedManualRunValues, HasUnappliedTournamentInfo) switch
    {
        (true, true) => "未反映：手動補正・大会メモ",
        (true, false) => "未反映：手動補正",
        (false, true) => "未反映：大会メモ",
        _ => "未反映の変更なし",
    };

    public string ManualRunValuesStatusLabel => HasUnappliedManualRunValues ? "未反映の変更あり" : "変更なし";
    public string ManualRunValuesButtonLabel => HasUnappliedManualRunValues ? "変更を反映" : "反映";
    public string TournamentInfoStatusLabel => HasUnappliedTournamentInfo ? "未反映の変更あり" : "変更なし";
    public string TournamentInfoButtonLabel => "大会情報を反映";

    private int CurrentManualDifficulty() => Math.Clamp(
        int.TryParse(_runState.Difficulty, NumberStyles.Integer, CultureInfo.InvariantCulture, out var difficulty) ? difficulty : 1,
        1, ManualDifficultyMaximum);

    private void NotifyManualRunValuesChanged()
    {
        if (!_isRefreshingManualDrafts)
            _hasEditedManualRunValues = _loadedManualRunDraft is not null && ReadManualRunDraft() != _loadedManualRunDraft;
        OnPropertyChanged(nameof(HasUnappliedManualRunValues));
        OnPropertyChanged(nameof(ManualRunValuesStatusLabel));
        OnPropertyChanged(nameof(ManualRunValuesButtonLabel));
        NotifyUnappliedRunEditsChanged();
    }

    private void NotifyTournamentInfoChanged()
    {
        if (!_isRefreshingManualDrafts)
            _hasEditedTournamentInfo = _loadedTournamentDraft is not null && ReadTournamentDraft() != _loadedTournamentDraft;
        OnPropertyChanged(nameof(HasUnappliedTournamentInfo));
        OnPropertyChanged(nameof(TournamentInfoStatusLabel));
        OnPropertyChanged(nameof(TournamentInfoButtonLabel));
        NotifyUnappliedRunEditsChanged();
    }

    private void NotifyUnappliedRunEditsChanged()
    {
        OnPropertyChanged(nameof(HasUnappliedRunEdits));
        OnPropertyChanged(nameof(UnappliedRunEditsLabel));
    }

    // 状態再読込と利用者の編集を区別する。別項目の保存・画面移動で下書きを消さない。
    private void RefreshManualRunDraftFields(string campaignId)
    {
        var sameCampaign = string.Equals(_manualDraftCampaignId, campaignId, StringComparison.Ordinal);
        var preserveRun = sameCampaign && HasUnappliedManualRunValues;
        var preserveTournament = sameCampaign && HasUnappliedTournamentInfo;
        var runDraft = ReadManualRunDraft();
        var tournamentDraft = ReadTournamentDraft();
        var previousRun = _loadedManualRunDraft ?? runDraft;
        var previousTournament = _loadedTournamentDraft ?? tournamentDraft;
        _isRefreshingManualDrafts = true;
        try
        {
            if (!sameCampaign || ManualSquadOptions.Count == 0)
                ReplaceCollection(ManualSquadOptions, new[] { SukiSquadOption.KeepCurrent }
                    .Concat(RhodesRunCatalog.LoadSquadOptions(campaignId)));
            var currentSquad = ManualSquadOptions.FirstOrDefault(option =>
                !string.IsNullOrEmpty(option.Id) && option.Name.Equals(_runState.Squad, StringComparison.Ordinal))
                ?? SukiSquadOption.KeepCurrent;
            var currentEffect = RhodesRunCatalog.LoadSquadRandomEffectOptions(campaignId, currentSquad.Id)
                .FirstOrDefault(option => option.Name.Equals(_runState.SquadRandomEffect, StringComparison.Ordinal))
                ?? SukiSquadOption.KeepCurrent;
            var storedRun = new ManualRunDraft(_runState.Ingot, CurrentManualDifficulty(), currentSquad.Id, currentEffect.Id);

            // 編集中のフィールドだけを保持し、未編集フィールドは新しい取得値へ追従させる。
            ManualIngot = preserveRun && runDraft.Ingot != previousRun.Ingot ? runDraft.Ingot : storedRun.Ingot;
            ManualDifficulty = preserveRun && runDraft.Difficulty != previousRun.Difficulty ? runDraft.Difficulty : storedRun.Difficulty;
            var squadId = preserveRun && runDraft.SquadId != previousRun.SquadId ? runDraft.SquadId : storedRun.SquadId;
            SelectedManualSquad = ManualSquadOptions.FirstOrDefault(option => option.Id == squadId) ?? SukiSquadOption.KeepCurrent;
            RefreshManualSquadRandomEffectOptions();
            if (preserveRun && runDraft.EffectId != previousRun.EffectId)
                SelectedManualSquadRandomEffect = ManualSquadRandomEffectOptions.FirstOrDefault(option => option.Id == runDraft.EffectId)
                    ?? SukiSquadOption.KeepCurrent;
            _loadedManualRunDraft = storedRun;

            var storedTournament = new TournamentDraft(_runState.TournamentInfo?.Score ?? 0,
                _runState.TournamentInfo?.Withdrawals ?? 0, (_runState.TournamentInfo?.Memo ?? "").Trim());
            ManualTournamentScore = preserveTournament && tournamentDraft.Score != previousTournament.Score
                ? tournamentDraft.Score : storedTournament.Score;
            ManualTournamentWithdrawals = preserveTournament && tournamentDraft.Withdrawals != previousTournament.Withdrawals
                ? tournamentDraft.Withdrawals : storedTournament.Withdrawals;
            if (!preserveTournament || tournamentDraft.Memo == previousTournament.Memo)
                ManualTournamentMemo = storedTournament.Memo;
            _loadedTournamentDraft = storedTournament;
            _manualDraftCampaignId = campaignId;
        }
        finally
        {
            _isRefreshingManualDrafts = false;
        }
        NotifyManualRunValuesChanged();
        NotifyTournamentInfoChanged();
    }

    private Task RevertManualRunValuesAsync()
    {
        _hasEditedManualRunValues = false;
        RefreshManualRunDraftFields(CurrentCampaignId);
        StatusMessage = "手動補正の入力欄を現在の値に戻しました。";
        return Task.CompletedTask;
    }

    private Task RevertTournamentInfoAsync()
    {
        _hasEditedTournamentInfo = false;
        RefreshManualRunDraftFields(CurrentCampaignId);
        StatusMessage = "大会メモの入力欄を現在の値に戻しました。";
        return Task.CompletedTask;
    }
}
