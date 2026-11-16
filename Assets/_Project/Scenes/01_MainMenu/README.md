# mainMenu

Главное меню игры. Вся вёрстка меню лежит в сцене `MainMenu.unity` и префабе
`Prefabs/SceneCard.prefab`; код только переключает панели и заполняет карточки.

## Что внутри

- `MainMenu.unity` — Canvas, панели, кнопки, `VerticalLayoutGroup`,
  `GridLayoutGroup` и `ScrollRect`. Все ссылки и события кнопок назначены в сцене.
- `Prefabs/SceneCard.prefab` — карточка сцены: превью, акт, название, статус.
- `Scripts/MainMenuController.cs` — переключение панелей и запуск сцен.
- `Scripts/SceneCardView.cs` — заполнение одной карточки.
- `Scripts/MenuSceneCatalog.cs` / `MenuSceneEntry.cs` — каталог сцен.
- `Scripts/MenuProgressStore.cs` — наличие сохранения и пройденные сцены.
- `Scripts/MenuBootIntegration.cs` / `MenuLaunchRequest.cs` — запуск игры через Boot.
- `Shaders/UIGrayscale.shader` и `M_LockedPreview.mat` — чёрно-белые непройденные сцены.
- `SceneImages/` — картинки-превью сцен.
- `Background/MENU_Background.unity` — пример 3D-сцены фона (её можно заменить своей).
- `Background/Materials/` — материалы фоновой сцены.

## Настройка

1. Список сцен: `Config/MENU_SceneCatalog.asset`.
   Для каждой сцены задаются акт, название, `sceneId`, `sceneName` и превью.
2. На объекте `MainMenu` в сцене `MainMenu.unity`:
   - `Developer Unlock All Scenes` — разрешить выбор любой сцены;
   - `New Game Scene Name` — стартовая сцена новой игры;
   - `Background Scene Name` — имя вашей 3D-сцены фона (по умолчанию
     `MENU_Background`). Сцена должна быть в Build Settings.
3. Картинки сцен: `Texture Type = Sprite (2D and UI)`, затем спрайт назначается
   в поле `Preview` каталога сцен.

Непройденные сцены всегда показываются чёрно-белыми, независимо от флага
разработчика.
