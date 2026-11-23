# App, input and camera: where the arguments live

All under `Assets/Sturm/Runtime/`.

| Topic | File · symbol |
| :-- | :-- |
| Matte texture owned by the app, sRGB format | `App/AppController.cs` `OnEnable` |
| Retrying a frame RVM was too busy for | `App/AppController.cs` `Update` |
| Rim light glide rate | `App/AppController.cs` `RimTransitionRate` |
| Clearing the matte on reset | `App/AppController.cs` `OnResetRequested` |
| Preset as a group of layers plus rim light | `App/EffectPreset.cs` |
| Overlay placement and hiding | `App/AppView.cs` header, `Assets/Sturm/UI/Main.uss` |
| Reset events and why video loops raise one | `Input/InputSource.cs` `ResetRequested`, `RestartVideoLoop` |
| Stale VideoPlayer callbacks | `Input/InputSource.cs` `IsCurrentVideoEvent` |
| Origin in RvmInputSource | `Input/InputSource.cs` header |
| Camera drift: why it barely moves | `Camera/CameraDrift.cs` header |
