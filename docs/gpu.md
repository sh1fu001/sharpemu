<!--
Copyright (C) 2026 SharpEmu Emulator Project
SPDX-License-Identifier: GPL-2.0-or-later
-->

# Graphics Pipeline (AGC → Vulkan)

The graphics stack turns PlayStation 5 AGC command buffers and Gen5
GCN/RDNA-style shaders into data Vulkan can present. The long-term path is:

```text
AGC → IR → SPIR-V → Vulkan
```

This document records the current foundation and the remaining work.

## Roadmap

| Phase | Goal | State |
|---|---|---|
| 1. GPU logging | Log AGC submits, dump command buffers and shader metadata, and identify vertex, pixel, hull, and compute programs. | In progress |
| 2. Shader translator | Translate AGC/Gen5 to IR and SPIR-V, add per-instruction tests, and cache translated shaders. | Foundation in place |
| 3. Vulkan backend | Implement descriptor sets, pipeline layouts, textures, render targets, synchronization, and swapchain or VideoOut integration. | Partial; fixed presenter path only |
| 4. Visual debugging | Add wireframe mode, frame dumps, RenderDoc capture, and expected-versus-actual image comparisons. | Not started |

## Phase 1 — GPU Logging

The following functionality exists today:

- **AGC submissions** — `sceAgcDriverSubmitDcb` and `SubmitAcb` are parsed
  packet by packet and recorded in the diagnostic session's
  `gpu_submits.json`.
- **Shader identification** — each draw or dispatch identifies the bound
  vertex, pixel, hull, or compute program directly from the SH registers. See
  [`Gen5PipelineInspector`](../src/SharpEmu.Libs/Agc/Gen5PipelineInspector.cs).
- **Shader metadata** — each distinct program is scanned through its
  `s_endpgm` terminator and fingerprinted. See
  [`Gen5ShaderScanner`](../src/SharpEmu.Libs/Agc/Gen5ShaderScanner.cs).
  `shaders.json` records the stage, address, dword and byte lengths, and hash.
- **Optional disk dumps** — `SHARPEMU_DUMP_GPU=1` writes raw command buffers to
  `logs/gpu/cmdbuffers/` and shader binaries plus metadata to
  `logs/gpu/shaders/`.
- **Known translated case** — the fullscreen barycentric shader used by the
  Demon's Souls video loop is recognized by
  [`Gen5ShaderTranslator`](../src/SharpEmu.Libs/Agc/Gen5ShaderTranslator.cs)
  and presented through the Vulkan presenter.

## Phase 2 — Shader Translator Foundation

The translator front end and supporting infrastructure are present.
Per-instruction SPIR-V lowering remains the primary missing step.

- **GCN → IR decoder** —
  [`GcnDecoder`](../src/SharpEmu.Libs/Agc/Shader/GcnDecoder.cs) classifies
  SOP, VOP, SMEM, VOP3, EXP, and related encodings; resolves 32-bit and 64-bit
  instruction lengths, including trailing literals; extracts opcodes; and
  walks a program through `s_endpgm`.
- **SPIR-V assembler** —
  [`Spirv`](../src/SharpEmu.Libs/Agc/Shader/Spirv.cs) builds byte-correct
  SPIR-V headers, word counts, and string literals, and emits a structurally
  valid minimal module for each stage.
- **On-disk shader cache** —
  [`ShaderTranslationCache`](../src/SharpEmu.Libs/Agc/Shader/ShaderTranslationCache.cs)
  is keyed by stage and hash. Its magic and version header invalidates stale
  blobs when the translator changes.
- **Compilation pipeline** —
  [`Gen5ShaderCompiler`](../src/SharpEmu.Libs/Agc/Shader/Gen5ShaderCompiler.cs)
  performs decode, IR generation, cache lookup, SPIR-V generation, and cache
  storage.

The next step is to replace minimal-module generation with real
instruction-by-instruction lowering so translated shaders reproduce the guest
program.

## Environment Variables

- `SHARPEMU_DUMP_GPU=1` — dump command buffers and shader binaries under
  `logs/gpu/`.
- `SHARPEMU_LOG_AGC=1` — enable verbose AGC packet and shader tracing.

## Next Technical Milestones

1. Lower individual Gen5 instructions from IR to SPIR-V with fixture tests.
2. Validate translated programs against shaders identified by Phase 1.
3. Expand Vulkan resource binding, texture, render-target, and synchronization
   support.
4. Add deterministic visual comparison fixtures.
