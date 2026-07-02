<!--
Copyright (C) 2026 SharpEmu Emulator Project
SPDX-License-Identifier: GPL-2.0-or-later
-->

# Controller Input

SharpEmu reads standard controllers through the Silk.NET input context owned by
the VideoOut window. On Windows, a USB DualSense or DualSense Edge is opened
through its native HID interface and takes priority over the standardized
gamepad path. Guest `scePadRead` calls consume thread-safe snapshots without
blocking on host I/O.

The implementation supports hot-plugging and prioritizes a controller whose
reported name contains `DualSense`, `PS5` or `Wireless Controller`. Otherwise,
it selects the first standard gamepad. Input is neutral while the VideoOut
window is unfocused.

## DualSense Setup

1. Connect the DualSense over USB. Bluetooth continues through the standard
   gamepad fallback.
2. Start a title and wait for the `SharpEmu - VideoOut` window.
3. Keep the VideoOut window focused while using the controller.
4. Enable `SHARPEMU_LOG_PAD=1` when verifying button transitions.

The host log reports `Native DualSense HID connected` when the native backend
is active, as well as the standardized device index, name and deadzone.

The native USB backend provides:

- buttons, sticks and analog triggers;
- gyroscope and accelerometer samples;
- both touch contacts and the touch-pad click;
- compatible haptics and guest-controlled light bar;
- DualSense adaptive-trigger resistance report generation.

## Mapping

Silk.NET exposes a standardized gamepad layout. SharpEmu translates that layout
to the PlayStation pad ABI:

| Host gamepad control | Guest control |
| --- | --- |
| A / bottom face button | Cross |
| B / right face button | Circle |
| X / left face button | Square |
| Y / top face button | Triangle |
| Left / right bumper | L1 / R1 |
| Left / right trigger | L2 / R2, analog and digital |
| Left / right stick | Left / right stick |
| Left / right stick click | L3 / R3 |
| D-pad | D-pad |
| Start | Options |
| Back / Create | Touch pad click |

The keyboard fallback remains:

| Keyboard | Guest control |
| --- | --- |
| Arrow keys or WASD | D-pad |
| Enter, Space or Z | Cross |
| Escape, Backspace or X | Circle |
| R | Triangle |
| F | Square |
| P | Options |

## Configuration

Configuration uses environment variables set before starting SharpEmu:

| Variable | Purpose | Default |
| --- | --- | --- |
| `SHARPEMU_GAMEPAD_INDEX` | Select a host gamepad by Silk.NET device index | Prefer DualSense, then first gamepad |
| `SHARPEMU_GAMEPAD_DEADZONE` | Radial stick deadzone from `0.0` to `0.95` | `0.12` |
| `SHARPEMU_DISABLE_GAMEPAD=1` | Disable physical gamepad input | Disabled |
| `SHARPEMU_DISABLE_GAMEPAD_KEYBOARD_COMPAT=1` | Disable mapping pad controls to guest keyboard input | Disabled |
| `SHARPEMU_LOG_PAD=1` | Log guest button-mask transitions | Disabled |
| `SHARPEMU_PAD_STATE_FILE` | OR a hexadecimal button mask into live input | Unset |

Example:

```powershell
$env:SHARPEMU_GAMEPAD_INDEX = "0"
$env:SHARPEMU_GAMEPAD_DEADZONE = "0.10"
$env:SHARPEMU_LOG_PAD = "1"
.\SharpEmu "eboot.bin"
```

## Performance

- Host devices are polled once per window update, rather than once per guest
  `scePadRead`.
- A compact seqlock transfers the common pad state. Motion/touch data use a
  separate synchronized snapshot.
- Stick deadzones are radial, preserving direction and full-scale range.
- Button-state files are refreshed at most 60 times per second.
- Vibration updates are coalesced and sent only when intensity changes.

## Current Limitations

- Native HID access currently targets Windows USB. Bluetooth DualSense input
  works through Silk.NET but does not expose motion, touch or native outputs.
- Adaptive-trigger reports are implemented in the transport, but a game can
  drive them only after its corresponding Gen5 pad-effect export is added.
- Speaker and microphone audio routing are not implemented.
- On non-native controllers, Back/Create remains mapped to touch-pad click
  because standardized gamepad APIs do not expose touch coordinates.

## Keyboard Compatibility

Like the compatibility output used by controller mappers such as DS4Windows,
SharpEmu also exposes common gamepad actions through the guest keyboard API.
This helps PC-derived homebrew titles that open a pad but continue consuming
their keyboard bindings. The native PlayStation pad state remains available at
the same time.

The D-pad and left stick map to the arrow keys. Cross maps to Space, Circle to
Escape, Options to Enter, Triangle to R and Square to F. Set
`SHARPEMU_DISABLE_GAMEPAD_KEYBOARD_COMPAT=1` when a title consumes both APIs
and duplicate actions are undesirable.
