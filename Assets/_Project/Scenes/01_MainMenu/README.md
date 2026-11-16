# mainMenu

Главное меню игры. Всё, что относится к меню, лежит здесь и не трогает папку `Scripts`.

## Что внутри

- `Scripts/MainMenuController.cs` — строит меню кодом и обрабатывает кнопки:
  - «Продолжить» — показывается только при наличии сохранения (`gamelimon_save_0.json`),
    открывает экран выбора сцен.
  - «Новая игра» — сбрасывает прогресс меню и сохранение, запускает первую сцену.
  - «Настройки» — заглушка, пишет сообщение в консоль.
  - «Выход» — выходит из игры (в редакторе останавливает Play Mode).
- `Scripts/MenuSceneCatalog.cs` — ScriptableObject-каталог сцен.
- `Scripts/MenuSceneEntry.cs` — описание сцены: название акта, название сцены,
  id сцены, имя сцены в Build Settings и картинка-превью.
- `Scripts/MenuProgressStore.cs` — прогресс: наличие сохранения, пройденные сцены
  (PlayerPrefs + текущая сцена из сохранения).
- `Scripts/MenuBootIntegration.cs` — если меню запущено отдельной сценой,
  выбранная сцена стартует через Boot (чтобы работали GameRoot и GameFlow).
  Сцена `MainMenu.unity` добавлена первой в Build Settings.
- `Shaders/UIGrayscale.shader` — чёрно-белый материал для непройденных сцен.
- `SceneImages/` — отдельная папка для картинок сцен. Сейчас там заглушки
  `PREVIEW_A1_S01_Apartment.png` и `PREVIEW_A1_S02_Alley.png`.

## Как настроить

1. Откройте ассет `Assets/_Project/Scenes/01_MainMenu/Config/MENU_SceneCatalog.asset`.
2. В списке `Entries` задайте для каждой сцены:
   - `Act Title` — надпись сверху картинки (например «Акт 1»);
   - `Display Name` — название снизу карточки;
   - `Scene Id` — id из `SceneDefinition` (например `A1_S01`);
   - `Scene Name` — имя сцены в Build Settings;
   - `Preview` — спрайт из `SceneImages/`.
3. На объекте `MainMenu` в сцене `Assets/_Project/Scenes/01_MainMenu/MainMenu.unity`:
   - `Developer Unlock All Scenes` — флаг режима разработчика. Включён — можно
     выбрать любую сцену; выключен — только пройденные.
   - `New Game Scene Name` — с какой сцены начинать новую игру.

Непройденные сцены всегда показываются чёрно-белыми (материал
`M_LockedPreview`), независимо от флага разработчика.
