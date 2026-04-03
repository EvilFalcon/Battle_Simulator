# Battle Simulator (Z-media тестовое задание)

Базовый симулятор сражений двух армий в стиле Army Clash. Платформа: **мобильная**.

---

## Соответствие требованиям ТЗ

- **Платформа** — мобильная (в настройках и сборке).
- **Характеристики юнита** — ATK, HP, SPEED, ATKSPD в `StatsComponent`, расчёт в `UnitStatsCalculator`.
- **Влияние цвета и размера** — модификаторы в SO: `ColorModifiersSO`, `SizeModifiersSO`, `UnitAppearanceSO` (цвет и масштаб).
- **Формы** — CUBE / SPHERE (`UnitFormType`, `FormModifiersSO`).
- **Размер** — SMALL / BIG (`UnitSizeType`, `SizeModifiersSO`).
- **Цвет** — BLUE / GREEN / RED (`UnitColorType`, `ColorModifiersSO`).
- **Расширяемость** — новый цвет/форма/размер: новый enum + поля в SO, без правок формул.
- **Базовые статы** — 100 HP, 10 ATK, 10 SPEED, 1 ATKSPD (`UnitBaseStatsSO`).
- **Модификаторы по ТЗ** — форма/размер/цвет по таблицам из задания.
- **Армии** — по 20 юнитов (`GameSettingsSO.UnitsPerArmy`).
- **Размещение** — плоская поверхность, две армии в зонах спавна (лицом друг к другу).
- **Рандомизация** — кнопка Randomize в главном меню фиксирует сид; Start запускает бой с этим сидом.
- **Выбор цели** — стратегии: ближайший враг (Nearest), обход (Flank), выгодная цель (Advantage).
- **Рукопашный бой** — движение к цели (`MovementSystem`), атака в радиусе (`AttackSystem`).
- **ATKSPD** — задержка между атаками = 1/ATKSPD сек (`AttackSystem`).
- **Урон и попадание** — урон = ATK, попадание 100%.
- **Смерть** — HP ≤ 0 → удаление с поля (`DeathSystem`).
- **Движение** — SPEED единиц/сек по направлению к цели.
- **Конец боя** — после уничтожения одной армии возврат в главное меню.

---

## Архитектура и выбор решений

### Слои и технологии

- **UI:** **MVP** — View только отображает данные и шлёт события, вся логика в Presenter.
- **Игровая логика:** **ECS** на базе **LeoECS** (мир, компоненты, системы).
- **Связка UI ↔ ECS:** **R3** (Observable/Subject) — события боя и счёт юнитов приходят в UI по подпискам, без прямых ссылок между слоями.
- **DI:** **VContainer** — зависимости через конструктор/поля, настройка по сценам через LifetimeScope и ScriptableObject.

Выбор ECS и MVP описан в README ниже; альтернативы (MVC, MVVM и т.д.) не использованы для сохранения единого стиля и предсказуемости обновления состояния.

### Сцены

- **Init** — стартовая: загружает настройки и сцену **Main Menu**.
- **Main Menu** — кнопки Start и Randomize; после Randomize сид фиксируется, по Start загружается **Game**.
- **Game** — симуляция боя; по окончании (победа одной стороны) — возврат в Main Menu.

Настройки собраны в **ScriptableObject** и связаны через корневой **BattleSimSettingsSO**.

---

## Почему не используется AI NavMesh

Движение реализовано **вручную** в `MovementSystem`: юнит каждый кадр двигается по направлению к текущей позиции цели с учётом SPEED и при необходимости обходит союзников (flank waypoint). **Unity AI NavMesh не используется** по следующим причинам:

1. **Плоская арена без навигационной геометрии.** ТЗ предполагает бой на плоской поверхности. Препятствия и сложный рельеф не заданы, поэтому обход статичной геометрии не нужен — достаточно прямого вектора «я → цель».

2. **Динамическая цель.** Цель — живой враг, его позиция меняется каждый кадр. NavMesh рассчитан на движение к фиксированной или реже меняющейся точке и на обход статичных препятствий. Постоянный пересчёт пути к движущейся цели и синхронизация с ECS усложнили бы код без выигрыша в геймплее.

3. **Полный контроль в ECS.** Вся логика (выбор цели, движение, атака, смерть) живёт в системах ECS и обновляется по `deltaTime`. Позиция хранится в `PositionComponent`. Использование NavMeshAgent привязало бы часть состояния к GameObject и к физике/навигации Unity, что усложнило бы порядок обновления и тестирование.

4. **Производительность и масштаб.** При 20×2 юнитах (40 единиц) прямой пересчёт направления и separation (отталкивание) выполняются за один проход по данным. NavMesh потребовал бы baked-данные, агентов на сцене и обновление путей — избыточно для данной постановки.

5. **Обход союзников.** Вместо NavMesh реализован простой обход: если путь к цели перекрыт другим юнитом, для тактики Flank/Advantage ставится waypoint в сторону (перпендикуляр направлению на цель), юнит идёт к waypoint, затем снова к цели. Этого достаточно для «рубки» на открытой плоскости.

Итого: для симуляции рукопашного боя на плоском поле **прямое движение по направлению к цели + separation + waypoint при блокировке** сознательно выбраны вместо NavMesh как более простые, предсказуемые и соответствующие ТЗ.

---

## Структура проекта (Assets/BattleSim)

- **Config** — enum (Form/Size/Color), SO модификаторов и базовых статов, `UnitStatsCalculator`, `BattleSimSettingsSO`, `CombatSettingsSO`.
- **Ecs** — `EcsWorld`, компоненты (Unit, Stats, Position, Target, AttackCooldown, UnitViewRef, UnitBounds, UnitState, Waypoint, UnitTactic), системы (TargetSelection, Movement, Attack, Death, WinCheck).
- **Game** — `UnitSpawnService`, `EcsRunner`, `GameBootstrap`, `BattleEventBus`, `BattleSimRuntimeState`, `SceneLoader`, `PathBlockChecker`, интерфейсы, стратегии выбора цели, зоны спавна, GroundSnap, PlayAreaBounds.
- **Presentation** — `UnitView` (MonoBehaviour), `IUnitViewFactory` / `UnitViewFactory`.
- **Mvp** — MainMenu и GameUI: View, Presenter, интерфейсы View.
- **Bootstrap** — `InitLifetimeScope`, `MainMenuLifetimeScope`, `GameLifetimeScope`, entry points.

---

## Логика боя

- **Цель:** выбирается стратегией (Nearest / Flank / Advantage) в `TargetSelectionSystem`; при смерти цели или отсутствии цели пересчёт каждый кадр.
- **Движение:** к цели со скоростью SPEED; при блокировке пути другими юнитами — waypoint в сторону (flank), затем снова к цели; separation — отталкивание при сближении.
- **Атака:** в радиусе (сумма радиусов юнитов + зазор) наносится урон ATK с паузой 1/ATKSPD сек.
- **Расширяемость:** новая форма/размер/цвет — новый enum + поля в SO и при необходимости в `UnitStatsCalculator`/`UnitAppearanceSO`.

---

## Дополнительная функция (одна по ТЗ)

**Отображение количества юнитов по армиям в реальном времени.**  
`WinCheckSystem` каждый кадр публикует `UnitCountChanged(team0, team1)` через `IBattleEventBus`. `GameUIPresenter` подписан через R3 и обновляет текст на экране (например, «Team 1: 15 | Team 2: 12»). Это даёт наглядную обратную связь во время боя и демонстрирует связку ECS → EventBus → R3 → UI без прямых ссылок между слоями.

---


## Производительность

- При 40 юнитах системы выполняют линейные/квадратичные проходы по сущностям (выбор цели, движение, проверка блокировки пути, separation). Известных проблем с производительностью нет; при значительном увеличении числа юнитов можно вынести пространственные запросы в отдельную структуру (например, сетка или дерево).

---

## Время на выполнение ТЗ

Ориентировочно **10–16 часов** (архитектура ECS + MVP, R3, VContainer, настройки в SO, одна дополнительная функция, README и настройка сцен в редакторе).

---

## Чек-лист настройки в редакторе

### 1. Build Settings
- File → Build Settings → добавить сцены **Init**, **Main Menu**, **Game**.
- Первой в списке сделать **Init**.

### 2. ScriptableObject
Создать через **Create → BattleSim**:

- **BattleSimSettings** (BattleSim → Settings Root) — корневой конфиг, ссылки на все остальные SO и Unit Prefabs.
- **Game Settings** (BattleSim → Game Settings) — имена сцен, кол-во юнитов, параметры спавна и земли.
- **Unit Base Stats** (BattleSim → Unit Base Stats) — базовые HP/ATK/SPEED/ATKSPD.
- **Form Modifiers** (BattleSim → Form Modifiers) — бонусы Cube/Sphere.
- **Size Modifiers** (BattleSim → Size Modifiers) — бонусы Small/Big.
- **Color Modifiers** (BattleSim → Color Modifiers) — бонусы Blue/Green/Red.
- **Unit Appearance** (BattleSim → Unit Appearance) — цвета и масштабы по типу.
- **Unit Prefabs** (BattleSim → Unit Prefabs) — Cube Prefab и Sphere Prefab.
- **Combat Settings** (BattleSim → Combat Settings) — радиус атаки, waypoint, тактики.

### 3. Сцена Init
- GameObject → Add Component **InitLifetimeScope** → поле Settings = BattleSimSettings (или оставить пустым, если asset в `Resources/BattleSimSettings`).

### 4. Сцена Main Menu
- Canvas с кнопками **Start** и **Randomize**, текст для сида.
- Компонент **MainMenuView** на объект с кнопками: привязать Start Button, Randomize Button, Seed Text.
- GameObject с **MainMenuLifetimeScope**: Settings, Main Menu View.

### 5. Сцена Game
- Арена (плоскость/пол).
- Два объекта с **Army Spawn Zone** + Collider (левая и правая армия).
- Canvas: статус боя, счёт юнитов; компонент **GameUIView** — привязать Status Text, Unit Count Text (и Seed Text при необходимости).
- GameObject с **GameLifetimeScope**: Settings, Game UI View, Left Spawn Zone, Right Spawn Zone.
- Префабы юнитов с **UnitView** (меш Cube/Sphere, Scale Root, Renderer) — назначить в Unit Prefabs (BattleSimSettings).

### 6. Проверка
- Запустить сцену **Init** → Main Menu → Randomize → Start → бой → после победы одной стороны возврат в Main Menu, счёт юнитов обновляется во время боя.
