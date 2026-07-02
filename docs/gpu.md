<!--
Copyright (C) 2026 SharpEmu Emulator Project
SPDX-License-Identifier: GPL-2.0-or-later
-->

# Graphics Pipeline (AGC -> Vulkan)

The graphics stack turns PS5 **AGC** command buffers and **Gen5** (GCN/RDNA-style) shaders into something
Vulkan can present. The game-changer for the project is a stable `AGC -> SPIR-V -> Vulkan` path; this document
tracks the roadmap and what exists today.

## Roadmap

| Phase | Goal | State |
| ----- | ---- | ----- |
| 1. GPU logging | Log AGC submits, dump command buffers, dump shader metadata, identify vertex/pixel/compute | In progress (see below) |
| 2. Shader translator | AGC/Gen5 -> IR -> SPIR-V, per-instruction unit tests, on-disk shader cache | Foundation in place (see below) |
| 3. Vulkan backend | Descriptor sets, pipeline layout, textures, render targets, synchronization, swapchain / video out | Partial (fixed presenter path only) |
| 4. Visual debug | Wireframe mode, frame dump, RenderDoc capture, expected-vs-actual image comparison | Not started |

## Phase 1 — what exists today

- **AGC submits** (`sceAgcDriverSubmitDcb` / `SubmitAcb`) are parsed packet-by-packet and recorded
  (`gpu_submits.json` in the diagnostics session; see the README Diagnostics section).
- **Shader identification**: for each draw/dispatch, the bound programs are identified by stage
  (vertex / pixel / hull / compute) directly from the SH registers
  ([Gen5PipelineInspector](../src/SharpEmu.Libs/Agc/Gen5PipelineInspector.cs)).
- **Shader metadata**: each distinct program's length is found from its `s_endpgm` terminator and
  fingerprinted ([Gen5ShaderScanner](../src/SharpEmu.Libs/Agc/Gen5ShaderScanner.cs)), then recorded once
  (`shaders.json`: stage, address, dword/byte length, hash).
- **Disk dumps** (opt-in): with `SHARPEMU_DUMP_GPU=1`, raw command buffers are written to
  `logs/gpu/cmdbuffers/` and shader binaries + metadata to `logs/gpu/shaders/` (`<stage>_<hash>.sb`/`.txt`).
- The fullscreen barycentric shader used by Demon's Souls' video loop is recognized by
  [Gen5ShaderTranslator](../src/SharpEmu.Libs/Agc/Gen5ShaderTranslator.cs) and presented through the Vulkan
  presenter — the single translated case that exists so far.

## Phase 2 — shader translator (foundation)

The translator front end and supporting infrastructure are in place; per-instruction SPIR-V lowering is the
remaining step. All of the following is unit-tested against real shader bytecode.

- **GCN -> IR decoder** ([GcnDecoder](../src/SharpEmu.Libs/Agc/Shader/GcnDecoder.cs)): classifies each word by
  encoding (SOP*/VOP*/SMEM/VOP3/EXP/...), resolves 32- vs 64-bit length including trailing literals, extracts
  the opcode, and walks a program to its `s_endpgm`.
- **SPIR-V assembler** ([Spirv](../src/SharpEmu.Libs/Agc/Shader/Spirv.cs)): builds byte-correct SPIR-V (header,
  word-count packing, string literals) and emits a structurally valid minimal module per stage.
- **On-disk shader cache** ([ShaderTranslationCache](../src/SharpEmu.Libs/Agc/Shader/ShaderTranslationCache.cs)):
  keyed by stage + hash with a magic/version header, so a translator change invalidates stale blobs.
- **Pipeline** ([Gen5ShaderCompiler](../src/SharpEmu.Libs/Agc/Shader/Gen5ShaderCompiler.cs)):
  decode -> IR -> cache lookup -> SPIR-V -> cache store.

**Next**: replace the minimal-module emit with real GCN-instruction lowering (IR -> SPIR-V) so a translated
shader reproduces the guest program, driven by the shaders Phase 1 already identifies.

## Environment variables

- `SHARPEMU_DUMP_GPU=1` — dump command buffers and shader binaries to `logs/gpu/`.
- `SHARPEMU_LOG_AGC=1` — verbose AGC packet tracing (`agc.*` log lines, including `agc.shader ...`).

## Next: AGC -> SPIR-V -> Vulkan

Phase 1 produces the inputs the rest of the pipeline needs: the shader binaries, their stages and their
addresses. Phase 2 replaces the single hardcoded pattern in `Gen5ShaderTranslator` with a real
Gen5 -> IR -> SPIR-V translator (unit-tested instruction by instruction, cached on disk), and Phase 3 builds
the Vulkan pipeline (descriptor sets, layouts, textures, render targets, synchronization) that consumes it.
