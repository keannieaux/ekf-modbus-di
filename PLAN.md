# План реализации: программа настройки ПЛК ШНО

Документ-основание: [SPEC.md](SPEC.md).
Scope данной работы: **сервисный слой** (Modbus, модели, профили, сервисы, ViewModels). Views — другой разработчик.

Старый код заготовки (`PLc.cs`, `DiPoint.cs`, старые `MainViewModel`/`DiPointViewModel`) **удаляется/переписывается** — он не соответствует ТЗ (адресация захардкожена, блокирующий I/O, дублирование логики).

---

## 1. Архитектура

### 1.1. Структура решения

```
ShnoSetting/
├── ShnoSetting.Core/              ← НОВЫЙ проект: class library (net10.0), весь backend
│   ├── Modbus/                    ← транспортный слой
│   ├── Profiles/                  ← JSON-профили контроллеров (модель + загрузчик)
│   ├── Services/                  ← прикладные сервисы по блокам ТЗ
│   ├── Schedule/                  ← график: маппинг режимов, упаковка слов, CSV
│   ├── Settings/                  ← настройки приложения (имена входов, IP и т.д.)
│   └── ViewModels/                ← ViewModels для привязки в UI
├── ShnoSetting.Core.Tests/        ← НОВЫЙ проект: xUnit, тесты чистой логики (без железа)
├── AvaloniaApplication1/          ← UI-проект: только App/Program/Views + ссылка на Core
└── profiles/
    └── ekf.default.json           ← профиль контроллера по умолчанию (адреса-заглушки)
```

Обоснование отдельной сборки Core:
- Жёсткая граница с UI-разработчиком: он трогает только Avalonia-проект.
- Core тестируется без UI и без железа.
- CommunityToolkit.Mvvm подключается в Core — ViewModels живут там.

### 1.2. Слои и поток данных

```
Views (Avalonia)  →  ViewModels  →  Services  →  ModbusDispatcher  →  EasyModbus
                         ↑              ↑              │
                     AppSettings    Profiles      PollingService (низкий приоритет)
```

Ключевые решения:

1. **ModbusDispatcher** — единственный владелец `ModbusClient`. EasyModbus синхронный и не thread-safe, поэтому:
   - Все операции ставятся в очередь команд, один consumer-цикл исполняет их последовательно на фоновом потоке (`Task.Run` вокруг синхронных вызовов).
   - Два приоритета: **запись (высокий)** и опрос (низкий). Запись вытесняет poll-запросы.
   - Наружу — только async API (`Task<int[]> ReadHoldingRegistersAsync(...)` и т.д.).
2. **IModbusTransport** — интерфейс поверх диспетчера. В тестах подменяется Fake'ом, что позволяет гонять интеграционные сценарии (например, round-trip графика) без ПЛК.
3. **Reconnect**: при ошибке связи диспетчер помечает соединение разорванным, публикует статус, периодически пытается переподключиться (тот же цикл, backoff).
4. **PollingService**: таймер (период из настроек, default 1000 мс) формирует план блочных чтений из профиля и отправляет в диспетчер с низким приоритетом. Пропускает тик, если предыдущий цикл ещё не завершён.

---

## 2. Модель профиля контроллера (JSON)

`ControllerProfile` (System.Text.Json, `profiles/*.json` рядом с exe):

```jsonc
{
  "name": "EKF",
  "discreteInputs": {
    "count": 64,
    "rawCoilsBase": 0,        // входные значения (coil)
    "outputCoilsBase": 0,     // выходные значения после НО/НЗ (coil)
    "selectorRegsBase": 0,    // селекторы (holding)
    "noNcCoilsBase": 0        // НО/НЗ (coil)
  },
  "starters": {
    "count": 4,
    "controlReg": 0,          // общий регистр маски
    "durationReg": 0,         // общий регистр длительности ручного режима
    "feedbackCoilsBase": 0    // обратная связь, 4 coil
  },
  "meters": {
    "slotCount": 6,
    "typeRegsBase": 0,        // тип счётчика, 6 регистров
    "addressRegsBase": 0,     // адрес счётчика, 6 регистров
    "dataBlocksBase": 0,      // начало блоков данных
    "dataBlockStride": 0,     // шаг между слотами (регистров)
    // смещения внутри блока слота:
    "voltageOffset": 0,       // 3 float = 6 рег
    "currentOffset": 6,       // 3 float
    "powerOffset": 12,        // 4 float (3 фазы + общая)
    "energyOffset": 20,       // 1 float
    "serialOffset": 22,       // 1 DINT
    "commStatusCoilsBase": 0  // статус связи, 6 coil
  },
  "clock": {
    "readRegsBase": 0,        // год..сек, 6 регистров
    "writeRegsBase": 0,       // отдельные 6 регистров записи
    "syncTriggerCoil": 0      // триггер «применить»
  },
  "schedule": {
    "monthsBase": 7000,       // база 1-го месяца
    "monthStride": 248,       // 31 день × 8 регистров
    "intervalsPerDay": 8
  }
}
```

`ProfileProvider`: сканирует каталог `profiles/`, десериализует, отдаёт список + загрузку по имени. Все адреса в профиле — уже **финальные Modbus-адреса** (без магии вида V/M-смещений из старого кода; пересчёт под конкретный ПЛК делает человек при составлении профиля).

---

## 3. Компоненты Core

### 3.1. Modbus/ (транспорт)

| Тип | Назначение |
|---|---|
| `IModbusTransport` | async-методы: ReadCoils/ReadHoldingRegisters/ReadInputRegisters (массивами), WriteSingleCoil/WriteMultipleCoils/WriteMultipleRegisters, событие `ConnectionChanged`, свойство `IsConnected` |
| `ModbusDispatcher` | очередь с приоритетами, consumer-цикл, reconnect c backoff, владелец `ModbusClient` |
| `RegisterConverter` | float/DINT ↔ 2 регистра, **big endian** (word order настраивается на случай различий ПЛК); BCD не нужен (время — обычные целые) |

### 3.2. Services/ (прикладной слой)

| Сервис | Чтение (poll) | Запись (по команде) |
|---|---|---|
| `DiscreteInputsService` | raw coils (64), output coils (64) — 2 блочных чтения | селекторы (64 рег, блоком), НО/НЗ (64 coil, блоком); также Read-конфигурации по кнопке |
| `StartersService` | feedback coils (4) | маска управления (1 рег), длительность (1 рег) |
| `MetersService` | конфиг слотов (тип+адрес, 12 рег) → по активным слотам (адрес≠0) блочное чтение блоков данных + статус coils | запись типа/адреса слота |
| `ClockService` | 6 регистров времени | 6 регистров + триггер-coil (write → pulse) |
| `PollingService` | оркестратор: собирает план чтений, тикает по таймеру, рассылает результаты | — |

### 3.3. Schedule/ (график)

| Тип | Назначение |
|---|---|
| `ScheduleModeMap` | таблица режим↔маска (0/5→0000, 1..16 по SPEC); обратный маппинг, 0000→5 |
| `ScheduleWord` | упаковка/распаковка слова: биты 0–11 время ЧЧММ, биты 12–15 КМ1..КМ4; пропуск = `0x0FFF` |
| `ScheduleCalendar` | адресация: `monthsBase + monthStride×(m−1) + day×8 + slot`; реальные дни месяца, февраль всегда 29; хвосты не трогаем |
| `ScheduleCsv` | парсер/писатель CSV: `;`, 366 строк (валидация), `--:--`, дата dd.MM.yyyy (год игнорируется) |
| `ScheduleService` | `ImportFromCsv(path)` → слова → запись в ПЛК (блоками по месяцам); `ReadToCsv(path)` → канонический CSV (пропуски `--:--`/режим 5) |

### 3.4. Settings/

`AppSettings` (JSON в `%AppData%` или рядом с exe): наименования 64 входов, последние IP/Port, период опроса, выбранный профиль. `SettingsStore` — load/save.

### 3.5. ViewModels/

CommunityToolkit.Mvvm, source generators, валидация где нужна:

- `MainViewModel` — Ip, Port, список профилей + выбранный, Connect/Disconnect, Status, IsConnected, PollPeriodMs; дочерние VM ниже.
- `InputsViewModel` → `DiscreteInputViewModel[64]`: Name, RawValue, OutputValue, Selector (0..64), IsNc; команды ReadConfig/WriteConfig.
- `StartersViewModel` → `StarterViewModel[4]`: FeedbackOn, ManualOn (бит маски); DurationSec; ApplyCommand.
- `MetersViewModel` → `MeterSlotViewModel[6]`: Type, Address, IsActive, U1..U3, I1..I3, P1..P3, PTotal, Energy, Serial, CommOk.
- `ClockViewModel`: PlcTime, SyncToPcCommand.
- `ScheduleViewModel`: ImportCsvCommand, ReadFromPlcCommand (сохранение CSV), Status/прогресс.

Обновление VM из PollingService — через события/сообщения, маршаллинг на UI-поток (`Dispatcher.UIThread` на стороне приложения, в Core — события + `SynchronizationContext`-агностично).

---

## 4. Изменения в Avalonia-проекте (минимальные)

1. Удалить: `Models/PLc.cs`, `Models/DiPoint.cs`, старую логику `MainViewModel`/`DiPointViewModel`.
2. Добавить ссылку на `ShnoSetting.Core`.
3. `App.axaml.cs`: composition root — создание профилей/настроек/диспетчера/сервисов/VM, `DataContext = mainViewModel`.
4. `MainWindow.axaml`: оставить простейший работающий привязанный каркас (референс для UI-разработчика: статус, подключение, вкладки по блокам). Дальше — его работа.

---

## 5. Этапы (порядок реализации)

| # | Этап | Результат |
|---|---|---|
| 1 | Реструктуризация решения: Core + Tests проекты, удаление старого backend-кода | решение собирается |
| 2 | `RegisterConverter` + тесты (float/DINT big endian) | конвертеры |
| 3 | `IModbusTransport`, `ModbusDispatcher` (очередь, приоритет записи, reconnect) | транспорт |
| 4 | Модель профиля + `ProfileProvider` + `ekf.default.json` с адресами-заглушками | профили |
| 5 | `ScheduleModeMap`, `ScheduleWord`, `ScheduleCalendar`, `ScheduleCsv` + **тесты** (маппинг 0–16, упаковка слова, адресация месяцев, round-trip CSV→слова→CSV) | график без ПЛК |
| 6 | `ScheduleService` (запись/чтение через транспорт) + тест round-trip на Fake-транспорте | график целиком |
| 7 | `DiscreteInputsService`, `StartersService`, `ClockService`, `MetersService` | прикладные сервисы |
| 8 | `PollingService` (план чтений, период, пауза под запись) | циклический опрос |
| 9 | `AppSettings`/`SettingsStore` | настройки |
| 10 | ViewModels + composition root в App, каркас MainWindow | связка с UI |
| 11 | Ручная проверка на живом ПЛК / симуляторе Modbus, уточнение адресов в профиле | приёмка |

Этапы 2–6 покрываются unit-тестами сразу; 7–10 тестируются через Fake-транспорт там, где осмысленно.

---

## 6. Тестирование

- **Без железа** (`ShnoSetting.Core.Tests`, xUnit):
  - конвертеры регистров (включая известные эталонные значения float);
  - маппинг режимов (все 17 значений + 0000→5);
  - упаковка/распаковка слова графика, `0x0FFF`-пропуски;
  - адресация: 1 янв → 7000, 1 фев → 7248, 29 фев существует, 30 фев — нет;
  - CSV: парсинг примера из ТЗ, round-trip канонизация;
  - `ScheduleService` round-trip через Fake-транспорт (записали в «ПЛК», прочитали — получили канонический CSV).
- **С железом/симулятором**: локальный Modbus TCP slave-симулятор или реальный ПЛК; финальная сверка адресов профиля.

## 7. Риски и открытые моменты

1. **Адреса в профиле — заглушки** до получения реальной карты памяти ПЛК (блок счётчиков, часы). Структура кода этого не касается — правится только JSON.
2. **Word order big endian** у EasyModbus/конкретного ПЛК — вынести в профиль опциональным флагом, если на приёмке всплывёт перестановка слов.
3. **366 строк CSV строго** — при парсере валидировать и выдавать понятные ошибки (номер строки, причина).
4. Производительность poll-цикла: счётчики — до 6 блоков чтения; при периоде 1 с запаса Modbus TCP хватает, но при необходимости можно читать счётчики реже (вынести делитель частоты в настройки — реализуем только если понадобится).
