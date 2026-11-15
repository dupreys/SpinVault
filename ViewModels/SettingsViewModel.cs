using System.Collections.ObjectModel;
using ColecaoMidias.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ColecaoMidias.ViewModels;

/// <summary>Janela de Configurações: backup automático e verificação de atualização.</summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly AppSettings _settings;
    private readonly AutoBackupService _backup;
    private readonly Func<Task<UpdateCheckResult>> _checkUpdates;
    private readonly bool _loading = true;
    private UpdateInfo? _update;

    public SettingsViewModel(AppSettings settings, AutoBackupService backup, Func<Task<UpdateCheckResult>> checkUpdates, string versionText)
    {
        _settings = settings;
        _backup = backup;
        _checkUpdates = checkUpdates;
        VersionText = versionText;

        autoBackupEnabled = settings.AutoBackupEnabled;
        updateCheckEnabled = settings.UpdateCheckEnabled;

        AddOptions(IntervalOptions, new[] { (1, "Todo dia"), (7, "Toda semana"), (30, "Todo mês") },
                   settings.BackupIntervalDays, days => _settings.BackupIntervalDays = days);
        AddOptions(KeepOptions, new[] { (3, "3"), (5, "5"), (10, "10"), (20, "20"), (30, "30") },
                   settings.BackupKeep, keep => _settings.BackupKeep = keep);

        _loading = false;
        RefreshBackupTexts();
    }

    public string VersionText { get; }
    public ObservableCollection<ChoiceItem> IntervalOptions { get; } = new();
    public ObservableCollection<ChoiceItem> KeepOptions { get; } = new();

    [ObservableProperty] private bool autoBackupEnabled;
    [ObservableProperty] private bool updateCheckEnabled;
    [ObservableProperty] private string folderText = "";
    [ObservableProperty] private string folderHint = "";
    [ObservableProperty] private string lastBackupText = "";
    [ObservableProperty] private bool lastBackupFailed;
    [ObservableProperty] private bool isBackingUp;
    [ObservableProperty] private bool isChecking;
    [ObservableProperty] private string updateText = "";
    [ObservableProperty] private bool hasUpdate;
    [ObservableProperty] private bool usingCustomFolder;

    private void AddOptions(ObservableCollection<ChoiceItem> target, (int Value, string Label)[] items, int current, Action<int> apply)
    {
        // Se o valor salvo não é um dos botões (arquivo editado à mão), escolhe o mais próximo.
        var chosen = items.OrderBy(i => Math.Abs(i.Value - current)).First().Value;
        foreach (var (value, label) in items)
        {
            var option = new ChoiceItem(value.ToString(), label) { IsSelected = value == chosen };
            option.Chosen += c =>
            {
                if (_loading) return;
                apply(int.Parse(c.Key));
                Persist();
            };
            target.Add(option);
        }
    }

    partial void OnAutoBackupEnabledChanged(bool value)
    {
        if (_loading) return;
        _settings.AutoBackupEnabled = value;
        Persist();
    }

    partial void OnUpdateCheckEnabledChanged(bool value)
    {
        if (_loading) return;
        _settings.UpdateCheckEnabled = value;
        Persist();
    }

    private static void Persist() => SettingsStore.Save();

    private void RefreshBackupTexts()
    {
        UsingCustomFolder = !string.IsNullOrWhiteSpace(_settings.BackupFolder);
        FolderText = _backup.Folder;
        FolderHint = UsingCustomFolder
            ? "Pasta escolhida por você. Se for do OneDrive, o backup também vai para a nuvem."
            : "Pasta padrão do programa. Escolha uma pasta do OneDrive para ter uma cópia na nuvem.";

        var count = _backup.CountBackups();
        var stored = count == 1 ? "1 backup guardado" : $"{count} backups guardados";

        if (!string.IsNullOrEmpty(_settings.LastBackupError))
        {
            LastBackupFailed = true;
            LastBackupText = $"O último backup falhou: {_settings.LastBackupError}";
        }
        else if (_settings.LastBackupUtc is { } last)
        {
            LastBackupFailed = false;
            LastBackupText = $"Último backup: {last.ToLocalTime():dd/MM/yyyy 'às' HH:mm} · {stored}.";
        }
        else
        {
            LastBackupFailed = false;
            LastBackupText = "Nenhum backup automático foi feito ainda.";
        }
    }

    [RelayCommand]
    private void ChooseFolder()
    {
        var picked = Dialogs.PickFolder("Escolha a pasta dos backups automáticos", _backup.Folder);
        if (picked is null) return;

        _settings.BackupFolder = picked;
        Persist();
        RefreshBackupTexts();
    }

    [RelayCommand]
    private void UseDefaultFolder()
    {
        _settings.BackupFolder = null;
        Persist();
        RefreshBackupTexts();
    }

    [RelayCommand]
    private void OpenFolder() => Dialogs.OpenFolder(_backup.Folder);

    [RelayCommand]
    private async Task BackupNowAsync()
    {
        IsBackingUp = true;
        try
        {
            var outcome = await Task.Run(() => _backup.RunNow());
            RefreshBackupTexts();
            if (outcome.Result == BackupResult.Failed)
                Dialogs.Error("Não foi possível fazer o backup", outcome.Error ?? "Erro desconhecido.");
            else if (outcome.Result == BackupResult.Busy)
                Dialogs.Info("Backup em andamento", "Já existe um backup sendo feito. Aguarde terminar.");
        }
        finally
        {
            IsBackingUp = false;
        }
    }

    [RelayCommand]
    private async Task CheckUpdatesAsync()
    {
        IsChecking = true;
        UpdateText = "Verificando…";
        try
        {
            var result = await _checkUpdates();
            _update = result.Available;
            HasUpdate = _update is not null;
            UpdateText = result.Message;
        }
        finally
        {
            IsChecking = false;
        }
    }

    [RelayCommand]
    private void DownloadUpdate()
    {
        if (_update is null) return;
        Dialogs.OpenUrl(_update.DownloadUrl ?? _update.PageUrl);
    }
}
