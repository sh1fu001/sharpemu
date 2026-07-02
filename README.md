<!--
Copyright (C) 2026 SharpEmu Emulator Project
SPDX-License-Identifier: GPL-2.0-or-later
-->

# SharpEmu

<p align="center">
  <img src=".github/images/logo.png" width="30%" height="30%" alt="SharpEmu logo" />
</p>

<p align="center">
  An experimental PlayStation 5 emulator for Windows, Linux and macOS.
</p>

> [!WARNING]
> Currently the primary development target is Windows.

> [!WARNING]
> This project is in a very early alpha stage. No commercial games are currently playable.

## Info

SharpEmu is an emulator project currently in its early stages of development.

This project is developed purely for research and educational purposes. There
are no commercial goals associated with it. We enjoy learning about system
architecture and reverse engineering.

SharpEmu focuses exclusively on the PlayStation 5. Our goal is **not** to
emulate PS4 games, as there is already an excellent emulator dedicated to that
platform: **ShadPS4**.

## Status

The emulator can currently load the `eboot.bin` of real games, execute native CPU instructions, and partially handle kernel-related functionality. However, several critical components are still missing.

Current capabilities include:

- Loading `eboot.bin` and `.elf` files
- Executing native CPU instructions
- Reading basic game metadata (title, version, etc.)
- Loading system modules (`prx` / `sys_module`)
- Partial support for some kernel functions
- `Fiber` and `AMPR` exports
- PlayGo scenarios
- Initial loading of game files
- Initial AGC shader and resource submission support
- Video output in some titles

Some games have reached like `sceVideoOut` and AGC stages.

Currently the project primarily targets Windows. Cross-platform support for
Linux and macOS is planned, but development remains focused on Windows to
simplify early-stage debugging and iteration.

## Usage

1. Build or publish the project, or download a tagged release.
2. Open PowerShell.
3. Run:

   ```powershell
   .\SharpEmu "eboot.bin" 2>&1 | Tee-Object -FilePath "log.txt"
   ```

## Controller Input

SharpEmu accepts standard gamepads through the VideoOut window, including a
DualSense connected over USB or Bluetooth when the host input backend exposes
it as a gamepad. Buttons, both sticks and both analog triggers are forwarded to
`scePad`; keyboard controls remain available as a fallback.

- Controller setup, mappings and troubleshooting:
  [`docs/controller-input.md`](docs/controller-input.md)
- Set `SHARPEMU_GAMEPAD_INDEX` to select a specific host controller.
- Set `SHARPEMU_GAMEPAD_DEADZONE` to tune the radial stick deadzone
  (default: `0.12`).
- Set `SHARPEMU_LOG_PAD=1` to log input transitions.

## Running the Test Game

The development test title is the open-source homebrew game LBreakoutHD. Its
game files are not included in this repository.

1. Restore and build SharpEmu from the repository root:

   ```powershell
   dotnet restore SharpEmu.slnx --locked-mode
   dotnet build SharpEmu.slnx --configuration Release --no-restore
   ```

2. Create the test-game directory:

   ```powershell
   New-Item -ItemType Directory -Force `
     ".\artifacts\test-games\lbreakouthd"
   ```

3. Extract a legally obtained LBreakoutHD homebrew build into that directory.
   Keep its asset directories next to the executable. The expected layout is:

   ```text
   artifacts/
     test-games/
       lbreakouthd/
         lbreakouthd.elf
         share/
         var/
   ```

4. Optionally connect a DualSense over USB. Enable controller diagnostics when
   testing input:

   ```powershell
   $env:SHARPEMU_LOG_PAD = "1"
   $env:SHARPEMU_LOG_KEYBOARD = "1"
   ```

5. Start the game from the repository root:

   ```powershell
   & ".\artifacts\bin\Release\net10.0\win-x64\SharpEmu.exe" `
     --log-level=info `
     ".\artifacts\test-games\lbreakouthd\lbreakouthd.elf"
   ```

6. Wait for the `SharpEmu - VideoOut` window and keep it focused. Use the
   D-pad or left stick to move, Cross to confirm/fire, Circle to go back and
   Options for Enter. The keyboard fallback uses the arrow keys, Space,
   Escape and Enter.

Successful startup includes these messages:

```text
Native DualSense input active
Vulkan VideoOut presented first frame
```

If the controller is intentionally handled only through the native pad API,
disable the homebrew keyboard-compatibility mapping before launch:

```powershell
$env:SHARPEMU_DISABLE_GAMEPAD_KEYBOARD_COMPAT = "1"
```

Unset that variable, or set it to `0`, for LBreakoutHD.

## Titles Mentioned in Testing

- **Demon's Souls Remake**

  - [Demon's Souls (PPSA01341)](https://github.com/par274/sharpemu/issues/2)
  - The README records a video loop and an initial VideoOut frame. General
    shader translation to SPIR-V/Vulkan remains ongoing.

  ![Demon's Souls VideoOut first frame](./.github/images/des-videoout-shaders.jpg)

- **Poppy Playtime Chapter 1**

  - [Poppy Playtime Chapter 1 (PPSA20591)](https://github.com/par274/sharpemu/issues/3)
  - No reproducible milestone is documented in this README yet.

- **SILENT HILL: The Short Message**

  - [SILENT HILL: The Short Message (PPSA10112)](https://github.com/par274/sharpemu/issues/4)
  - No reproducible milestone is documented in this README yet.

- **Dreaming Sarah**

  - [Dreaming Sarah (PPSA02929)](https://github.com/par274/sharpemu/issues/9)
  - The README records real texture rendering on a splash screen.

  ![Splash texture](./.github/images/videoout_frame_0002_h1_b1.jpg)

> [!IMPORTANT]
> This project does **not** support or condone piracy.
> All games used during development and testing are dumped from consoles that we personally own.
> Users are expected to use legally obtained copies of their games.

## Compatibility Tracking

SharpEmu tracks compatibility per title using a dedicated compatibility matrix
and per-game notes.

- Compatibility matrix: [`docs/compatibility.md`](docs/compatibility.md)
- Per-game notes: [`docs/game-notes/`](docs/game-notes/)
- HLE export coverage: [`docs/hle-exports.md`](docs/hle-exports.md)
- Kernel HLE status: [`docs/kernel-hle-status.md`](docs/kernel-hle-status.md)
- Memory model notes: [`docs/memory.md`](docs/memory.md)
- GPU pipeline notes: [`docs/gpu.md`](docs/gpu.md)
- Controller input: [`docs/controller-input.md`](docs/controller-input.md)

## Diagnostics

Each run writes a structured diagnostics session to
`logs/<TITLE_ID>/<timestamp>/` so that when a title stalls you can see exactly
which NID, syscall, module, memory region or GPU submit was involved rather
than only "it crashed":

- `boot.log` — full captured console/boot output
- `imports_missing.json` — guest imports (NIDs) with no HLE implementation
- `syscalls.json` — raw guest syscalls observed
- `modules.json` — loaded modules and their address ranges
- `memory_map.json` — mapped guest memory regions and protections
- `gpu_submits.json` — recent AGC GPU command-buffer submits
- `shaders.json` — distinct shaders identified (stage, address, size, hash)
- `crash_context.json` — result, crash/trap/fault details and session summary

Pass `--no-diagnostics` (or set `SHARPEMU_NO_DIAGNOSTICS=1`) to disable it.

## HLE Export Coverage

The HLE exports the emulator implements are organized by module (library) and
by NID. A coverage report is generated from those registrations:

- [docs/hle-exports.md](docs/hle-exports.md) — human-readable, grouped by module
- `docs/hle-exports.json` — machine-readable, same data

Regenerate it after adding or changing exports:

```powershell
SharpEmu --export-report
```

This makes it easy to split HLE work across modules (e.g. Kernel vs VideoOut
vs Agc) without overlap.

## Memory Model

Guest memory is the layer the CPU, GPU and kernel all sit on, so it enforces
permissions and reports faults as structured errors rather than raw crashes:

- Strict RX/RW/RWX permission checks, guard pages, page-aligned allocation,
  named shared memory, an mmap-like API, invalid-access tracking and
  lightweight snapshots (see `GuestAddressSpace`).
- Illegal accesses produce a `MEMORY_ACCESS_VIOLATION` block with the faulting
  address, access kind, owning region, module and instruction pointer.

See [docs/memory.md](docs/memory.md) for details.

## Kernel HLE Status

The kernel (`libKernel`) exports are triaged by bring-up priority area (thread
lifecycle, synchronization, event queue, memory mapping, module loading, file
descriptors, time/clock/sleep, process params) and by severity
(BLOCKER / CRITICAL / VISIBLE / COSMETIC / UNKNOWN):

- [docs/kernel-hle-status.md](docs/kernel-hle-status.md) — triage grouped by area
- `docs/kernel-hle-status.json` — machine-readable, same data

Regenerate it with:

```powershell
SharpEmu --kernel-status
```

The point is to focus kernel work on what the already-booting titles need, not
to implement every export.

## GPU / Shader Pipeline

The graphics stack turns AGC command buffers and Gen5 (GCN/RDNA-style) shaders
into Vulkan. Phase 1 (GPU logging) identifies the shaders each submit binds:

- Vertex, pixel, hull and compute shaders are identified from the AGC SH
  registers. Each distinct program is length-scanned and fingerprinted in the
  diagnostics session's `shaders.json`.
- With `SHARPEMU_DUMP_GPU=1`, raw command buffers and shader binaries are dumped
  to `logs/gpu/`.

- Phase 2 foundation: a GCN → IR decoder, a SPIR-V assembler and an on-disk shader cache
  (per-instruction SPIR-V lowering is the next step).

See [docs/gpu.md](docs/gpu.md) for the full roadmap (AGC -> SPIR-V -> Vulkan).

## Build

1. Install the **.NET SDK**.
2. Clone the repository:

   ```bash
   git clone https://github.com/par274/sharpemu.git
   ```

3. Open the solution file (`SharpEmu.slnx`) in **VSCode**.
4. Build the project: `dotnet build` or `dotnet publish`
5. Build artifacts will be located in the `artifacts` directory.

## Disclaimer

SharpEmu is an experimental emulator intended for research and educational purposes.

This project does not contain any copyrighted system firmware, game data, or proprietary PlayStation assets.

## Special Thanks

The following projects were extremely helpful during development:

- **[ShadPS4](https://github.com/shadps4-emu/shadPS4)**
  Helped with understanding the basic architecture of the PlayStation 4.

- **[Kyty](https://github.com/InoriRus/Kyty)**
  One of the few PS5 emulator projects available and very useful for studying
  native code execution.

- **Ryujinx**
  Provided valuable references for filesystem handling and low-level C#
  implementation patterns.

## License

- [**GPL-2.0 license**](https://github.com/par274/sharpemu/blob/main/LICENSE)
