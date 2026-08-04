using System;
using System.Collections.ObjectModel;
using AvaloniaApplication1.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EasyModbus;

namespace AvaloniaApplication1.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    // Базовые адреса блоков в терминах контроллера, до пересчёта в Modbus.
    private const int SelectBase = 10000;   // V10000 -> holding registers
    private const int BitBase = 0;          // M0     -> coils
    private const int NoNcBase = 10000;     // M10000 -> coils
    private ModbusClient? _client;
    [ObservableProperty] public partial string Status { get; set; } = "";
    [ObservableProperty] public partial string Ip { get; set; } = "192.168.1.245";
    [ObservableProperty] public partial int Port { get; set; } = 502;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(WriteCommand))]
    public partial bool IsLoaded { get; set; }

    public ObservableCollection<DiPointViewModel> Points { get; } = new();

    public MainViewModel()
    {
        // Временно: список из Excel-таблицы (X0..X63). Позже переедет в файл.
        string[] names = new string[64];
        names[0] = "Контроль доступа";
        names[1] = "Наличие питания";
        names[2] = "Авто режим";
        for (int i = 3; i < names.Length; i++)
            names[i] = $"Фидер {i - 2}";   // X3 -> Фидер 1 ... X63 -> Фидер 61

        for (int i = 0; i < names.Length; i++)
            Points.Add(new DiPointViewModel(new DiPoint { Name = names[i], Index = i }));
    }

    [RelayCommand]
    private void Read() => ReadFromDevice();
    
    private bool ReadFromDevice()
    {
        try
        {
            ModbusClient client = GetClient();
            int n = Points.Count;
            
            int[] selects = client.ReadHoldingRegisters(PLc.V(SelectBase), n);
            bool[] bits = client.ReadCoils(PLc.M(BitBase), n);
            bool[] noNc = client.ReadCoils(PLc.M(NoNcBase), n);

            for (int i = 0; i < n; i++)
            {
                Points[i].Select = selects[i] & 0xFFFF;
                Points[i].Bit = bits[i];
                Points[i].IsNc = noNc[i];
            }

            Status = $"Прочитано {n} строк";
            IsLoaded = true;
            return true;
        }
        catch (Exception ex)
        {
            Disconnect();
            Status = "Ошибка: " + ex.Message;
            return false;
        }
    }

    [RelayCommand(CanExecute = nameof(IsLoaded))]
    private void Write()
    {
        try
        {
            ModbusClient client = GetClient();
            int n = Points.Count;

            int[] selects = new int[n];
            bool[] noNc = new bool[n];
            for (int i = 0; i < n; i++)
            {
                selects[i] = Points[i].Select;
                noNc[i] = Points[i].IsNc;
            }

            client.WriteMultipleRegisters(PLc.V(SelectBase), selects);
            client.WriteMultipleCoils(PLc.M(NoNcBase), noNc);

            if(ReadFromDevice()) Status = $"Записано {n} строк";
        }
        catch (Exception ex)
        {
            Disconnect();
            Status = "Ошибка: "+ex.Message;
        }
    }
    
    private ModbusClient GetClient()
    {
        if (_client is {Connected: true} && _client.IPAddress == Ip && _client.Port == Port) 
            return _client;
        
        Disconnect();

        _client = new ModbusClient(Ip, Port)
        {
            UnitIdentifier = 1,
            ConnectionTimeout = 2000
        };
        _client.Connect();
        return _client;
    }

    private void Disconnect()
    {
        if (_client is null) return;
        try
        {
            if(_client.Connected) _client.Disconnect();
        }
        catch
        {

        }
        
        _client = null;
        
    }
}
