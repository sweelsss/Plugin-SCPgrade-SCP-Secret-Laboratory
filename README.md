# SCPGrade

**Апгрейдер предметов для SCP: Secret Laboratory в стиле CS-апгрейдеров.**
Берёшь предмет в руку, выбираешь, во что его улучшить, крутишь рулетку: выиграл — получаешь предмет дороже, проиграл — предмет сгорает.

Работает на **LabAPI** и на **EXILED** (две сборки из одного кода).

> Автор: **sweelsss** • Discord: **@sweeelsss**

---

## Возможности

- 🖥️ **Личный экран у каждого игрока.** Большой экран SCPGRADE стоит в мире (по умолчанию в **PT-00** — бывшей камере SCP-173 в лёгкой зоне). Каждый игрок рядом видит **свою** копию: свой предмет, свою цель, свою прокрутку. Другие игроки не видят чужие экраны и могут крутить одновременно — очереди нет.
- 📱 **Планшет** — команда `.scpgrade` в консоли игрока (`~`) открывает уменьшенный экран прямо перед тобой. Работает только в **лёгкой и тяжёлой зоне** (настраивается). Закрывается сам, если отойти на 2 м или долго ничего не нажимать.
- 🎯 **Интерфейс как на сайтах-апгрейдерах:** карточка «Ваш предмет», кольцо шанса с зоной выигрыша и треугольной стрелкой, карточка цели, сетка целей с цветами редкости (как в CS), быстрый выбор **x2 / x5 / x10 / 35% / 50% / 75%**.
- 🎲 **Шанс** = ценность твоего предмета ÷ ценность цели × (1 − комиссия), ограничен **1%…80%**.
- 🔊 **Звуки** прокрутки, выигрыша и проигрыша — слышит только сам игрок.
- ⚙️ **Ценности предметов в конфиге** — меняешь число, и всё. Можно подгружать из JSON по ссылке.
- 🍬 **Розовая конфета** SCP-330 — отдельная цель. Мешок SCP-330 в руке оценивается по конфетам внутри.
- 🚫 **Ивентовые предметы не участвуют:** уголь, снежок, диктофон (SCP-1507), фонарь, маршмеллоу, SCP-1509, SCP-021-J и т.п.
- 🌐 **Русский и английский.** Игрок выбирает язык в *Настройки → Сервер → Язык SCPGrade*.
- 📢 Объявление на весь сервер, если кто-то выбил предмет с шансом ≤ 10%.

## Требования

- SCP: Secret Laboratory **14.x** (LabAPI **1.1.7+**)
- Для EXILED-версии — **EXILED 9.15+**

## Установка

Скачай dll из [Releases](../../releases) и поставь **только одну** из двух:

### LabAPI
1. `SCPGrade.dll` → `%AppData%\SCP Secret Laboratory\LabAPI\plugins\global\`
   (на Linux: `~/.config/SCP Secret Laboratory/LabAPI/plugins/global/`).
2. Перезапусти сервер.
3. Конфиг появится в `LabAPI/configs/<порт>/SCPGrade/config.yml`.

### EXILED
1. `SCPGrade.Exiled.dll` → `%AppData%\EXILED\Plugins\`.
2. Перезапусти сервер.
3. Конфиг — в `EXILED/Configs/<порт>-config.yml`, раздел `scpgrade:`.

## Как пользоваться

| Действие | Как |
|---|---|
| Открыть/закрыть планшет | `.scpgrade` в консоли игрока (`~`) |
| Выбрать цель | нажать **E** на карточку внизу или на **x2 / x5 / x10 / 35% / 50% / 75%** |
| Листать цели | стрелки **‹ ›** над сеткой |
| Крутить | **зажать E** на кнопке «Улучшить» |

Улучшается предмет, который у тебя **в руке**.

## Команды администратора (RA, право FacilityManagement)

| Команда | Что делает |
|---|---|
| `upg here` | поставить экран перед собой и **запомнить место относительно комнаты** — сработает в каждом раунде, хоть карта и генерируется заново |
| `upg default` | сбросить сохранённое место → комната из конфига (PT-00) |
| `upg tower` | поставить на Башню (только на этот раунд) |
| `upg tp` | телепортироваться к экрану |
| `upg remove` | убрать экран до конца раунда |
| `upg open` | открыть свой планшет |
| `upg scale <x>` / `upg calib <x>` | подогнать размер / ширину текста |
| `upg reload` | перечитать ценности (и скачать по `values_url`) |
| `upg values` | показать текущие ценности |

> Чтобы экран стоял ровно там, где раньше спавнился SCP-173: встань на это место в PT-00 и введи `upg here`.

## Конфиг (основное)

```yaml
world_screen: true          # личный большой экран у каждого игрока (false — маленькая стойка, открывающая планшет)
screen_room: Lcz173         # комната по умолчанию (Lcz173 = PT-00)
world_screen_scale: 0.6     # размер экрана (1 = 4.4 x 2.75 м)
world_view_distance: 25     # с какого расстояния у игрока появляется его копия экрана
tablet_zones:               # где работает планшет
- LightContainment
- HeavyContainment
tablet_max_distance: 2      # отошёл на 2 м от места открытия — планшет закрылся
min_chance: 1
max_chance: 80
house_edge: 0.1             # комиссия 10%
item_values:                # ценность предметов (0 или удалить строку — предмет выключен)
  Coin: 5
  Medkit: 60
  GunE11SR: 320
  PinkCandy: 350            # розовая конфета SCP-330
  # ...
```

Полный пример — [`examples/config.labapi.yml`](examples/config.labapi.yml).
Пример JSON для `values_url` — [`examples/values.json`](examples/values.json).

## Сборка из исходников

Нужен .NET SDK 8+ (собирает под .NET Framework 4.8).

```bash
# папка SCPSL_Data/Managed выделенного сервера
set SL_REFERENCES=C:\SCPSL\server\SCPSL_Data\Managed
# только для EXILED: папка с Exiled.API.dll и Exiled.Events.dll из релиза EXILED
set EXILED_REFERENCES=C:\EXILED-refs

dotnet build SCPGrade.LabApi.csproj -c Release   # → bin/LabApi/SCPGrade.dll
dotnet build SCPGrade.Exiled.csproj -c Release   # → bin/Exiled/SCPGrade.Exiled.dll
```

Вместо переменных можно положить dll в папку `references/` (см. [`references/README.md`](references/README.md)).

## Как это устроено

- Экран собран из админ-тойсов: `PrimitiveObjectToy` (рамки, кольцо, карточки), `TextToy` (текст), `InteractableToy` (кнопки), `SpeakerToy` (звук).
- «Личность» экрана — через Mirror: объекты спавнятся с `Visibility.ForceHidden`, и в наблюдатели добавляется только владелец. Остальные игроки их не получают вообще.
- Место экрана хранится в локальных координатах комнаты, поэтому переживает генерацию новой карты.

---

## English

**SCPGrade** — a CS-style item upgrader for SCP: Secret Laboratory (LabAPI and EXILED builds).
Hold an item, pick a target, spin the wheel: win — get the more valuable item, lose — your item burns.

- Personal world screen for every player (default spot: **PT-00**, the old SCP-173 chamber) — everyone sees only their own session, no queue.
- Personal tablet via client console command **`.scpgrade`** (Light/Heavy Containment only).
- Chance = your value ÷ target value × (1 − house edge), clamped to 1–80%.
- Item values are configurable (`item_values`), event items are excluded, SCP-330 **Pink Candy** is a target.
- Russian and English UI (player picks it in *Settings → Server*).

**Install:** `SCPGrade.dll` → `LabAPI/plugins/global/` **or** `SCPGrade.Exiled.dll` → `EXILED/Plugins/` (only one of them).

---

Лицензия: [MIT](LICENSE).
