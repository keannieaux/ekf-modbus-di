using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShnoSetting.Core.Modbus;
using ShnoSetting.Core.Profiles;
using ShnoSetting.Core.Schedule;
using ShnoSetting.Core.Services;
using ShnoSetting.Core.Settings;

namespace ShnoSetting.Core.ViewModels;

/// <summary>
/// Корневая ViewModel: подключение, выбор профиля, жизненный цикл сервисов,
/// маршаллинг событий опроса на UI-поток.
/// Создаётся на UI-потоке (захватывает SynchronizationContext).
/// </summary>
public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly AppSettings _settings;
    private readonly SettingsStore _settingsStore;
    private readonly ProfileProvider _profileProvider;
    private readonly SynchronizationContext? _uiContext = SynchronizationContext.Current;

    private ModbusDispatcher? _dispatcher;
    private PollingService? _polling;

    public ObservableCollection<ControllerProfile> Profiles { get; } = new();

    [ObservableProperty] private ControllerProfile? _selectedProfile;
    [ObservableProperty] private string _ip;
    [ObservableProperty] private int _port;
    [ObservableProperty] private int _pollPeriodMs;
    [ObservableProperty] private bool _isConnected;
    [ObservableProperty] private string _status = "Не подключено";

    public InputsViewModel Inputs { get; }
    public FeedersViewModel Feeders { get; }
    public StartersViewModel Starters { get; } = new();
    public MetersViewModel Meters { get; } = new();
    public ClockViewModel Clock { get; } = new();
    public ScheduleViewModel Schedule { get; } = new();

    public MainViewModel(AppSettings settings, SettingsStore settingsStore, ProfileProvider profileProvider)
    {
        _settings = settings;
        _settingsStore = settingsStore;
        _profileProvider = profileProvider;

        _ip = settings.Ip;
        _port = settings.Port;
        _pollPeriodMs = settings.PollPeriodMs;

        Inputs = new InputsViewModel();
        Feeders = new FeedersViewModel(Inputs);

        foreach (var profile in profileProvider.LoadAll())
            Profiles.Add(profile);
        _selectedProfile = Profiles.FirstOrDefault(p => p.Name == settings.ProfileName)
            ?? Profiles.FirstOrDefault();
        if (_selectedProfile is not null)
            Feeders.UnusedSelectorValue = _selectedProfile.DiscreteInputs.UnusedSelectorValue;
    }

    [RelayCommand]
    private async Task ConnectAsync()
    {
        if (SelectedProfile is null) { Status = "Не выбран профиль контроллера"; return; }
        if (IsConnected) return;

        SaveSettings();
        DisposeConnection();

        var profile = SelectedProfile;
        _dispatcher = new ModbusDispatcher();
        _dispatcher.ConnectionChanged += OnConnectionChanged;

        try
        {
            Status = "Подключение…";
            await _dispatcher.ConnectAsync(Ip, Port);

            // Сервисы под профиль
            var inputs = new DiscreteInputsService(_dispatcher, profile);
            var starters = new StartersService(_dispatcher, profile);
            var meters = new MetersService(_dispatcher, profile);
            var clock = new ClockService(_dispatcher, profile);
            var schedule = new ScheduleService(_dispatcher, profile);

            Inputs.Attach(inputs);
            Starters.Attach(starters);
            Meters.Attach(meters);
            Clock.Attach(clock);
            Schedule.Attach(schedule);

            // Циклический опрос
            _polling = new PollingService(_dispatcher, inputs, starters, meters, clock)
            {
                PeriodMs = PollPeriodMs > 0 ? PollPeriodMs : 1000
            };
            _polling.Polled += OnPolled;
            _polling.PollError += OnPollError;
            _polling.Enabled = true;
        }
        catch (Exception ex)
        {
            Status = "Ошибка подключения: " + ex.Message;
        }
    }

    [RelayCommand]
    private async Task DisconnectAsync()
    {
        if (_dispatcher is not null)
            await _dispatcher.DisconnectAsync();
        DisposeConnection();
        Status = "Не подключено";
    }

    partial void OnPollPeriodMsChanged(int value)
    {
        if (_polling is not null && value > 0)
            _polling.PeriodMs = value;
    }

    partial void OnSelectedProfileChanged(ControllerProfile? value)
    {
        if (value is not null)
        {
            Feeders.UnusedSelectorValue = value.DiscreteInputs.UnusedSelectorValue;
            SaveSettings();
        }
    }

    // ------------------------------------------------------------------

    private void OnConnectionChanged(bool connected)
        => PostToUi(() =>
        {
            IsConnected = connected;
            Status = connected
                ? $"Подключено к {Ip}:{Port}"
                : "Соединение потеряно, переподключение…";
        });

    private void OnPolled(PollResult result)
        => PostToUi(() =>
        {
            Inputs.ApplyStates(result.Inputs);
            Inputs.ApplyConfig(result.InputConfig);
            Starters.ApplyFeedback(result.StarterFeedback);
            Meters.ApplyData(result.Meters);
            Clock.ApplyTime(result.PlcTime);
        });

    private void OnPollError(string message)
        => PostToUi(() => Status = "Ошибка опроса: " + message);

    private void PostToUi(Action action)
    {
        if (_uiContext is not null && SynchronizationContext.Current != _uiContext)
            _uiContext.Post(_ => action(), null);
        else
            action();
    }

    private void SaveSettings()
    {
        _settings.Ip = Ip;
        _settings.Port = Port;
        _settings.PollPeriodMs = PollPeriodMs;
        _settings.ProfileName = SelectedProfile?.Name;
        _settingsStore.Save(_settings);
    }

    private void DisposeConnection()
    {
        _polling?.Dispose();
        _polling = null;
        if (_dispatcher is not null)
        {
            _dispatcher.ConnectionChanged -= OnConnectionChanged;
            _dispatcher.Dispose();
            _dispatcher = null;
        }
        IsConnected = false;
    }

    public void Dispose()
    {
        SaveSettings();
        DisposeConnection();
    }
}
