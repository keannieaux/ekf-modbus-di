namespace ShnoSetting.Core.Profiles;

/// <summary>
/// Карта адресов Modbus для конкретной модели контроллера.
/// Все адреса — финальные Modbus-адреса (0-based), сериализуется в JSON-профиль.
/// </summary>
public sealed class ControllerProfile
{
    public string Name { get; set; } = "";
    public DiscreteInputsProfile DiscreteInputs { get; set; } = new();
    public StartersProfile Starters { get; set; } = new();
    public MetersProfile Meters { get; set; } = new();
    public ClockProfile Clock { get; set; } = new();
    public ScheduleProfile Schedule { get; set; } = new();
}

public sealed class DiscreteInputsProfile
{
    public int Count { get; set; } = 64;
    /// <summary>Сырые значения входов (coil), база блока из Count штук.</summary>
    public int RawCoilsBase { get; set; }
    /// <summary>Значения входов после применения НО/НЗ (coil), база блока.</summary>
    public int OutputCoilsBase { get; set; }
    /// <summary>Селекторы входов (holding register), база блока.</summary>
    public int SelectorRegsBase { get; set; }
    /// <summary>Флаги НО/НЗ (coil), база блока.</summary>
    public int NoNcCoilsBase { get; set; }
}

public sealed class StartersProfile
{
    public int Count { get; set; } = 4;
    /// <summary>Общий регистр битовой маски включения (бит 0 = КМ1).</summary>
    public int ControlReg { get; set; }
    /// <summary>Общий регистр длительности ручного режима, секунды (0 = выкл).</summary>
    public int DurationReg { get; set; }
    /// <summary>Обратная связь пускателей (coil), база блока из Count штук.</summary>
    public int FeedbackCoilsBase { get; set; }
}

public sealed class MetersProfile
{
    public int SlotCount { get; set; } = 6;
    /// <summary>Тип счётчика (0 = CE318/CE208, 1 = CC301/CC101), база блока регистров.</summary>
    public int TypeRegsBase { get; set; }
    /// <summary>Адрес счётчика (0 = слот отключён), база блока регистров.</summary>
    public int AddressRegsBase { get; set; }
    /// <summary>Начало блоков данных счётчиков.</summary>
    public int DataBlocksBase { get; set; }
    /// <summary>Шаг между блоками данных слотов (в регистрах).</summary>
    public int DataBlockStride { get; set; }
    // Смещения внутри блока слота (в регистрах):
    public int VoltageOffset { get; set; }      // 3 float = 6 регистров
    public int CurrentOffset { get; set; } = 6; // 3 float
    public int PowerOffset { get; set; } = 12;  // 4 float (3 фазы + общая)
    public int EnergyOffset { get; set; } = 20; // 1 float
    public int SerialOffset { get; set; } = 22; // 1 DINT
    /// <summary>Статус связи счётчиков (coil), база блока из SlotCount штук.</summary>
    public int CommStatusCoilsBase { get; set; }
}

public sealed class ClockProfile
{
    /// <summary>Регистры чтения времени: год, месяц, день, часы, минуты, секунды.</summary>
    public int ReadRegsBase { get; set; }
    /// <summary>Отдельные регистры записи времени (тот же порядок).</summary>
    public int WriteRegsBase { get; set; }
    /// <summary>Coil-триггер «применить» для синхронизации.</summary>
    public int SyncTriggerCoil { get; set; }
}

public sealed class ScheduleProfile
{
    /// <summary>Базовый адрес первого месяца.</summary>
    public int MonthsBase { get; set; } = 7000;
    /// <summary>Шаг между месяцами (31 день × 8 регистров).</summary>
    public int MonthStride { get; set; } = 248;
    /// <summary>Интервалов (регистров) на день.</summary>
    public int IntervalsPerDay { get; set; } = 8;
}
