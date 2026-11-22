# Technical notes

Details behind the [README](../README.md): where battery levels come from, how devices are kept
asleep, and what the app is (and is not) allowed to do.

## Battery sources

| Source | Typical devices | Device traffic |
|---|---|---|
| Windows Bluetooth battery property `{104EA319-6EE2-4701-BD47-8DDBF425BBE5} 2` | Classic Bluetooth audio (HFP), LE devices with the Battery Service | none: the value is cached by Windows |
| Standard HID battery usage (Generic Device Controls `0x06:0x20`, Battery System `0x85:0x65`) | Bluetooth keyboards and mice that declare it, e.g. Keychron K2 | one HID GET_REPORT, only when allowed (below) |

Replies whose report ID differs from the requested one are rejected; some keyboards answer
GET_REPORT with unrelated reports in rotation.

## Power-safe polling

The app must never wake a sleeping device or keep it awake.

- Every minute it reads only state Windows already holds (connection flags, cached battery levels).
- A HID request is sent only to a connected device, and only right after it reconnects (the user
  just woke it) or while the user has been active in the last 60 seconds.
- At most one successful request per device per 30 minutes (configurable), with a 5-minute
  back-off after a failed attempt. Manual "Refresh now" follows the same rules.
- A sleeping classic Bluetooth device keeps its HID collections in Windows but reports
  "not connected" (`{83DA6326-97A6-4088-9453-A1923F573B29} 15`); that flag gates requests.

## Device pictograms

The type comes from the device container category Windows assigns (`Input.Keyboard`,
`Input.Mouse`, `Audio.Speakers`, ...) or, for classic Bluetooth, the Class of Device. The user can
override it per device. Pictograms are glyphs from the Segoe Fluent Icons font (Segoe MDL2 Assets
on Windows 10), drawn in the taskbar's ink colour; muted amber and red mark low and critical charge.
All states: [icon-preview.png](icon-preview.png).

## Security properties

- No network code: no update checks, telemetry or uploads. `NoNetworkGuardTests` fails if the app
  references a `System.Net*` or `System.Diagnostics.Process` assembly, or imports a DLL other than
  `cfgmgr32`, `hid`, `kernel32`, `user32`, `shell32`.
- No child processes, no installer, no auto-update.
- Native DLLs load from System32 only (`DefaultDllImportSearchPaths`).
- Devices are opened with zero access rights; nothing is ever written to a device.
- No third-party packages in the app; MSTest is used by the test project only.
- The exe carries its version and source commit (shown in the menu header), so a binary can be
  traced to the exact source it was built from.

## Files and registry entries

| Location | Content |
|---|---|
| `%APPDATA%\DeviceBatteryTray\settings.json` | language, interval, thresholds, hidden devices, icon types |
| `%LOCALAPPDATA%\DeviceBatteryTray\log.txt` | local diagnostics, capped at 256 KB |
| `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\DeviceBatteryTray` | only when "Start with Windows" is on |

## Localisation

UI text lives in `src/DeviceBatteryTray/Ui/Strings.resx` (English) and `Strings.<lang>.resx`
(fr, es, de, ru, uk, be). The language follows the Windows display language, falls back to
English, and can be changed from the menu. `LocalizationTests` fails if a translation is missing
or a format placeholder is lost.

## Code map

| Area | Files |
|---|---|
| Device tree, properties | `Native/DeviceProperties.cs`, `Battery/DeviceTopology.cs` |
| HID battery reading | `Battery/HidBatterySource.cs`, `Battery/HidValueMath.cs` |
| When to ask a device | `Battery/PollPolicy.cs` |
| Merging and state | `Battery/BatteryPoller.cs` |
| Tray icons and menu | `Ui/TrayIcon.cs`, `Ui/TrayIconManager.cs`, `Ui/IconRenderer.cs`, `Ui/TrayApplicationContext.cs` |

To add a device-specific source, implement discovery and reading next to
`Battery/HidBatterySource.cs`, merge its readings in `Battery/BatteryPoller.cs`, and gate every
request that reaches the device through `Battery/PollPolicy.cs`.
