<!--
Copyright (C) 2026 SharpEmu Emulator Project
SPDX-License-Identifier: GPL-2.0-or-later
-->

# Controller Input

SharpEmu reads controllers through the Silk.NET input context owned by the
VideoOut window. This keeps window events and device polling on one host thread
while guest `scePadRead` calls consume a lock-free snapshot.

The implementation supports hot-plugging and prioritizes a controller whose
reported name contains `DualSense`, `PS5` or `Wireless Controller`. Otherwise,
it selects the first standard gamepad. Input is neutral while the VideoOut
window is unfocused.

## DualSense Setup

1. Connect the DualSense over USB, or pair it over Bluetooth in the host
   operating system.
2. Start a title and wait for the `SharpEmu - VideoOut` window.
3. Keep the VideoOut window focused while using the controller.
4. Enable `SHARPEMU_LOG_PAD=1` when verifying button transitions.

The host log reports the selected device index, name and deadzone.

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
- A compact seqlock snapshot transfers state between the host and guest threads
  without locks or allocations in the read path.
- Stick deadzones are radial, preserving direction and full-scale range.
- Button-state files are refreshed at most 60 times per second.
- Vibration updates are coalesced and sent only when intensity changes.

## Current Limitations

- Basic vibration is forwarded only when the selected Silk.NET backend exposes
  vibration motors. The current GLFW backend may report none.
- Adaptive triggers, speaker output, light bar control, motion sensors and
  touch coordinates require a future native DualSense backend.
- The standardized gamepad API does not expose the DualSense touch surface, so
  the Back/Create control is mapped to the guest touch-pad click.
