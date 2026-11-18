# references

Сюда можно положить библиотеки для сборки, если не хочешь задавать переменные окружения.
Они **не выкладываются в репозиторий** (см. `.gitignore`).

- Из `SCPSL_Data/Managed` выделенного сервера: `LabApi.dll`, `Assembly-CSharp.dll`, `Assembly-CSharp-firstpass.dll`,
  `CommandSystem.Core.dll`, `Mirror.dll`, `Pooling.dll`, `NorthwoodLib.dll`, `YamlDotNet.dll`, `UnityEngine*.dll`,
  `Unity.TextMeshPro.dll`, `mscorlib.dll`, `System*.dll`, `netstandard.dll` — проще скопировать всю папку `Managed` сюда.
- Для EXILED-версии — в `references/exiled/`: `Exiled.API.dll` и `Exiled.Events.dll` из [релиза EXILED](https://github.com/ExMod-Team/EXILED/releases).
