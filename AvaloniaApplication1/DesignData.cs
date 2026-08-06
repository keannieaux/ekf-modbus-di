using System.IO;
using ShnoSetting.Core.Profiles;
using ShnoSetting.Core.Settings;
using ShnoSetting.Core.ViewModels;

namespace AvaloniaApplication1;

// Данные только для превьюера Rider. ViewModels в ShnoSetting.Core требуют
// зависимостей в конструкторе, и без этих классов панели в предпросмотре пустые.

public sealed class DesignMainViewModel : MainViewModel
{
    public DesignMainViewModel()
        : base(new AppSettings(), new SettingsStore("design.json"), new ProfileProvider("profiles"))
    {
        Status = "Предпросмотр: подключения нет";
    }
}

public sealed class DesignInputsViewModel : InputsViewModel
{
    public DesignInputsViewModel()
    {
        for (int i = 0; i < Inputs.Count; i++)
        {
            Inputs[i].RawValue = i % 3 == 0;
            Inputs[i].OutputValue = i % 4 == 0;
            Inputs[i].Selector = i;
            Inputs[i].IsNc = i % 5 == 0;
        }

        Status = "Предпросмотр: 64 входа";
    }
}

public sealed class DesignStartersViewModel : StartersViewModel
{
    public DesignStartersViewModel()
    {
        Starters[0].FeedbackOn = true;
        Starters[0].ManualOn = true;
        Starters[2].ManualOn = true;
        DurationSec = 3725;
    }
}

public sealed class DesignMetersViewModel : MetersViewModel
{
    public DesignMetersViewModel()
    {
        for (int i = 0; i < Slots.Count; i++)
        {
            MeterSlotViewModel slot = Slots[i];
            slot.Address = i < 2 ? i + 1 : 0;
            slot.IsActive = slot.Address != 0;
            slot.CommOk = slot.Address != 0;
            if (!slot.IsActive) continue;

            // В превью видны только активные слоты (как после автоопределения)
            VisibleSlots.Add(slot);

            slot.U1 = 231.4f; slot.U2 = 229.8f; slot.U3 = 230.6f;
            slot.I1 = 12.30f; slot.I2 = 11.75f; slot.I3 = 12.05f;
            slot.P1 = 2.84f; slot.P2 = 2.70f; slot.P3 = 2.78f; slot.PTotal = 8.32f;
            slot.Energy = 14523.75f;
            slot.Serial = 100200 + i;
        }
    }
}

public sealed class DesignScheduleViewModel : ScheduleViewModel
{
    public DesignScheduleViewModel() => Status = "Предпросмотр: график не загружен";
}

public sealed class DesignFeedersViewModel : FeedersViewModel
{
    public DesignFeedersViewModel() : base(new DesignInputsViewModel()) { }
}
