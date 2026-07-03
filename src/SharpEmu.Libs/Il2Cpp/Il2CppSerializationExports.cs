// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using SharpEmu.HLE;

namespace SharpEmu.Libs.Il2Cpp;

/// <summary>
/// Unity-facing IL2CPP helpers used while the native serializer builds its type database. These are
/// separate from the broad embedding API surface because several values are consumed by direct
/// guest-memory accesses and therefore must agree with <see cref="Il2CppRuntime"/>'s object layout.
/// </summary>
public static partial class Il2CppRuntimeExports
{
    private static int ReturnPointer(CpuContext ctx, nint value)
    {
        ctx[CpuRegister.Rax] = unchecked((ulong)value);
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    private static int ReturnBoolean(CpuContext ctx, bool value)
    {
        ctx[CpuRegister.Rax] = value ? 1UL : 0UL;
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(
        Nid = "__il2cpp_dyn_class_get_userdata_offset",
        ExportName = "il2cpp_class_get_userdata_offset",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libIl2Cpp")]
    public static int ClassGetUserDataOffset(CpuContext ctx)
    {
        // size_t il2cpp_class_get_userdata_offset(void)
        ctx[CpuRegister.Rax] = unchecked((ulong)Il2CppRuntime.ClassUserDataOffset);
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(
        Nid = "__il2cpp_dyn_class_set_userdata",
        ExportName = "il2cpp_class_set_userdata",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libIl2Cpp")]
    public static int ClassSetUserData(CpuContext ctx)
    {
        // void il2cpp_class_set_userdata(Il2CppClass* klass, void* userData)
        _ = Il2CppRuntime.Instance.SetClassUserData(
            unchecked((nint)ctx[CpuRegister.Rdi]),
            unchecked((nint)ctx[CpuRegister.Rsi]));
        ctx[CpuRegister.Rax] = 0;
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_class_get_declaring_type", ExportName = "il2cpp_class_get_declaring_type", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int ClassGetDeclaringType(CpuContext ctx) =>
        ReturnPointer(ctx, Il2CppRuntime.Instance.GetClassDeclaringType(
            unchecked((nint)ctx[CpuRegister.Rdi])));

    [SysAbiExport(Nid = "__il2cpp_dyn_class_get_parent", ExportName = "il2cpp_class_get_parent", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int ClassGetParent(CpuContext ctx) =>
        ReturnPointer(ctx, Il2CppRuntime.Instance.GetClassParent(
            unchecked((nint)ctx[CpuRegister.Rdi])));

    [SysAbiExport(Nid = "__il2cpp_dyn_class_get_nested_types", ExportName = "il2cpp_class_get_nested_types", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int ClassGetNestedTypes(CpuContext ctx)
    {
        var iteratorAddress = ctx[CpuRegister.Rsi];
        if (iteratorAddress == 0 ||
            !ctx.TryReadUInt64(iteratorAddress, out var ordinal) ||
            ordinal > int.MaxValue)
        {
            return ReturnPointer(ctx, 0);
        }

        var nested = Il2CppRuntime.Instance.GetNestedTypeAtOrdinal(
            unchecked((nint)ctx[CpuRegister.Rdi]),
            (int)ordinal);
        if (nested != 0)
        {
            _ = ctx.TryWriteUInt64(iteratorAddress, ordinal + 1);
        }

        return ReturnPointer(ctx, nested);
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_class_is_generic", ExportName = "il2cpp_class_is_generic", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int ClassIsGeneric(CpuContext ctx) =>
        ReturnBoolean(ctx, Il2CppRuntime.Instance.IsClassGeneric(
            unchecked((nint)ctx[CpuRegister.Rdi])));

    [SysAbiExport(Nid = "__il2cpp_dyn_class_is_inflated", ExportName = "il2cpp_class_is_inflated", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int ClassIsInflated(CpuContext ctx) => ReturnBoolean(ctx, false);

    [SysAbiExport(Nid = "__il2cpp_dyn_class_get_type", ExportName = "il2cpp_class_get_type", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int ClassGetType(CpuContext ctx) =>
        ReturnPointer(ctx, Il2CppRuntime.Instance.GetClassType(
            unchecked((nint)ctx[CpuRegister.Rdi])));

    [SysAbiExport(Nid = "__il2cpp_dyn_class_get_type_token", ExportName = "il2cpp_class_get_type_token", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int ClassGetTypeToken(CpuContext ctx)
    {
        ctx[CpuRegister.Rax] = Il2CppRuntime.Instance.GetClassToken(
            unchecked((nint)ctx[CpuRegister.Rdi]));
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_class_from_il2cpp_type", ExportName = "il2cpp_class_from_il2cpp_type", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int ClassFromIl2CppType(CpuContext ctx) =>
        ReturnPointer(ctx, Il2CppRuntime.Instance.GetClassFromType(
            unchecked((nint)ctx[CpuRegister.Rdi])));

    [SysAbiExport(Nid = "__il2cpp_dyn_class_from_type", ExportName = "il2cpp_class_from_type", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int ClassFromType(CpuContext ctx) => ClassFromIl2CppType(ctx);

    [SysAbiExport(Nid = "__il2cpp_dyn_class_is_assignable_from", ExportName = "il2cpp_class_is_assignable_from", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int ClassIsAssignableFrom(CpuContext ctx) =>
        ReturnBoolean(ctx, Il2CppRuntime.Instance.IsClassSubclassOf(
            unchecked((nint)ctx[CpuRegister.Rsi]),
            unchecked((nint)ctx[CpuRegister.Rdi]),
            checkInterfaces: true));

    [SysAbiExport(Nid = "__il2cpp_dyn_class_has_parent", ExportName = "il2cpp_class_has_parent", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int ClassHasParent(CpuContext ctx) =>
        ReturnBoolean(ctx, Il2CppRuntime.Instance.IsClassSubclassOf(
            unchecked((nint)ctx[CpuRegister.Rdi]),
            unchecked((nint)ctx[CpuRegister.Rsi]),
            checkInterfaces: false));

    [SysAbiExport(Nid = "__il2cpp_dyn_method_get_return_type", ExportName = "il2cpp_method_get_return_type", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int MethodGetReturnType(CpuContext ctx) =>
        ReturnPointer(ctx, Il2CppRuntime.Instance.GetMethodReturnType(
            unchecked((nint)ctx[CpuRegister.Rdi])));

    [SysAbiExport(Nid = "__il2cpp_dyn_method_get_declaring_type", ExportName = "il2cpp_method_get_declaring_type", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int MethodGetDeclaringType(CpuContext ctx) =>
        ReturnPointer(ctx, Il2CppRuntime.Instance.GetMethodClass(
            unchecked((nint)ctx[CpuRegister.Rdi])));

    [SysAbiExport(Nid = "__il2cpp_dyn_method_is_generic", ExportName = "il2cpp_method_is_generic", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int MethodIsGeneric(CpuContext ctx) =>
        ReturnBoolean(ctx, Il2CppRuntime.Instance.IsMethodGeneric(
            unchecked((nint)ctx[CpuRegister.Rdi])));

    [SysAbiExport(Nid = "__il2cpp_dyn_method_is_inflated", ExportName = "il2cpp_method_is_inflated", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int MethodIsInflated(CpuContext ctx) => ReturnBoolean(ctx, false);

    [SysAbiExport(Nid = "__il2cpp_dyn_method_is_instance", ExportName = "il2cpp_method_is_instance", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int MethodIsInstance(CpuContext ctx)
    {
        const uint MethodAttributeStatic = 0x10;
        var flags = Il2CppRuntime.Instance.GetMethodFlags(
            unchecked((nint)ctx[CpuRegister.Rdi]),
            out _);
        return ReturnBoolean(ctx, (flags & MethodAttributeStatic) == 0);
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_image_get_assembly", ExportName = "il2cpp_image_get_assembly", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int ImageGetAssembly(CpuContext ctx) =>
        ReturnPointer(ctx, Il2CppRuntime.Instance.GetAssemblyFromImage(
            unchecked((nint)ctx[CpuRegister.Rdi])));

    [SysAbiExport(Nid = "__il2cpp_dyn_image_get_name", ExportName = "il2cpp_image_get_name", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int ImageGetName(CpuContext ctx) =>
        ReturnPointer(ctx, Il2CppRuntime.Instance.GetImageName(
            unchecked((nint)ctx[CpuRegister.Rdi])));

    [SysAbiExport(Nid = "__il2cpp_dyn_image_get_filename", ExportName = "il2cpp_image_get_filename", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int ImageGetFilename(CpuContext ctx) => ImageGetName(ctx);

    [SysAbiExport(Nid = "__il2cpp_dyn_image_get_class_count", ExportName = "il2cpp_image_get_class_count", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int ImageGetClassCount(CpuContext ctx)
    {
        ctx[CpuRegister.Rax] = unchecked((uint)Il2CppRuntime.Instance.GetImageClassCount(
            unchecked((nint)ctx[CpuRegister.Rdi])));
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_image_get_class", ExportName = "il2cpp_image_get_class", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int ImageGetClass(CpuContext ctx) =>
        ReturnPointer(ctx, Il2CppRuntime.Instance.GetImageClass(
            unchecked((nint)ctx[CpuRegister.Rdi]),
            unchecked((int)ctx[CpuRegister.Rsi])));

    [SysAbiExport(Nid = "__il2cpp_dyn_image_get_entry_point", ExportName = "il2cpp_image_get_entry_point", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int ImageGetEntryPoint(CpuContext ctx) => ReturnPointer(ctx, 0);

    [SysAbiExport(Nid = "__il2cpp_dyn_class_has_attribute", ExportName = "il2cpp_class_has_attribute", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int ClassHasAttribute(CpuContext ctx) =>
        ReturnBoolean(ctx, Il2CppRuntime.Instance.ClassHasAttribute(
            unchecked((nint)ctx[CpuRegister.Rdi]),
            unchecked((nint)ctx[CpuRegister.Rsi])));

    [SysAbiExport(Nid = "__il2cpp_dyn_method_has_attribute", ExportName = "il2cpp_method_has_attribute", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int MethodHasAttribute(CpuContext ctx) =>
        ReturnBoolean(ctx, Il2CppRuntime.Instance.MethodHasAttribute(
            unchecked((nint)ctx[CpuRegister.Rdi]),
            unchecked((nint)ctx[CpuRegister.Rsi])));

    [SysAbiExport(Nid = "__il2cpp_dyn_custom_attrs_from_class", ExportName = "il2cpp_custom_attrs_from_class", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int CustomAttributesFromClass(CpuContext ctx) =>
        ReturnPointer(ctx, Il2CppRuntime.Instance.GetCustomAttributesForClass(
            unchecked((nint)ctx[CpuRegister.Rdi])));

    [SysAbiExport(Nid = "__il2cpp_dyn_custom_attrs_from_method", ExportName = "il2cpp_custom_attrs_from_method", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int CustomAttributesFromMethod(CpuContext ctx) =>
        ReturnPointer(ctx, Il2CppRuntime.Instance.GetCustomAttributesForMethod(
            unchecked((nint)ctx[CpuRegister.Rdi])));

    [SysAbiExport(Nid = "__il2cpp_dyn_custom_attrs_has_attr", ExportName = "il2cpp_custom_attrs_has_attr", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int CustomAttributesHasAttribute(CpuContext ctx) =>
        ReturnBoolean(ctx, Il2CppRuntime.Instance.CustomAttributesHaveAttribute(
            unchecked((nint)ctx[CpuRegister.Rdi]),
            unchecked((nint)ctx[CpuRegister.Rsi])));

    [SysAbiExport(Nid = "__il2cpp_dyn_custom_attrs_get_attr", ExportName = "il2cpp_custom_attrs_get_attr", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int CustomAttributesGetAttribute(CpuContext ctx) => ReturnPointer(ctx, 0);

    [SysAbiExport(Nid = "__il2cpp_dyn_custom_attrs_construct", ExportName = "il2cpp_custom_attrs_construct", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int CustomAttributesConstruct(CpuContext ctx) => ReturnPointer(ctx, 0);

    [SysAbiExport(Nid = "__il2cpp_dyn_custom_attrs_free", ExportName = "il2cpp_custom_attrs_free", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int CustomAttributesFree(CpuContext ctx) => ReturnPointer(ctx, 0);

    [SysAbiExport(Nid = "__il2cpp_dyn_class_get_fields", ExportName = "il2cpp_class_get_fields", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int ClassGetFields(CpuContext ctx)
    {
        var iteratorAddress = ctx[CpuRegister.Rsi];
        if (iteratorAddress == 0 ||
            !ctx.TryReadUInt64(iteratorAddress, out var ordinal) ||
            ordinal > int.MaxValue)
        {
            return ReturnPointer(ctx, 0);
        }

        var field = Il2CppRuntime.Instance.GetFieldAtOrdinal(
            unchecked((nint)ctx[CpuRegister.Rdi]),
            (int)ordinal);
        if (field != 0)
        {
            _ = ctx.TryWriteUInt64(iteratorAddress, ordinal + 1);
        }

        return ReturnPointer(ctx, field);
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_class_num_fields", ExportName = "il2cpp_class_num_fields", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int ClassNumFields(CpuContext ctx)
    {
        ctx[CpuRegister.Rax] = unchecked((uint)Il2CppRuntime.Instance.GetClassFieldCount(
            unchecked((nint)ctx[CpuRegister.Rdi])));
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_field_get_name", ExportName = "il2cpp_field_get_name", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int FieldGetName(CpuContext ctx) =>
        ReturnPointer(ctx, Il2CppRuntime.Instance.GetFieldName(
            unchecked((nint)ctx[CpuRegister.Rdi])));

    [SysAbiExport(Nid = "__il2cpp_dyn_field_get_parent", ExportName = "il2cpp_field_get_parent", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int FieldGetParent(CpuContext ctx) =>
        ReturnPointer(ctx, Il2CppRuntime.Instance.GetFieldParent(
            unchecked((nint)ctx[CpuRegister.Rdi])));

    [SysAbiExport(Nid = "__il2cpp_dyn_field_get_type", ExportName = "il2cpp_field_get_type", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int FieldGetType(CpuContext ctx) =>
        ReturnPointer(ctx, Il2CppRuntime.Instance.GetFieldType(
            unchecked((nint)ctx[CpuRegister.Rdi])));

    [SysAbiExport(Nid = "__il2cpp_dyn_field_get_flags", ExportName = "il2cpp_field_get_flags", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int FieldGetFlags(CpuContext ctx)
    {
        ctx[CpuRegister.Rax] = Il2CppRuntime.Instance.GetFieldFlags(
            unchecked((nint)ctx[CpuRegister.Rdi]));
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_field_has_attribute", ExportName = "il2cpp_field_has_attribute", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int FieldHasAttribute(CpuContext ctx) =>
        ReturnBoolean(ctx, Il2CppRuntime.Instance.FieldHasAttribute(
            unchecked((nint)ctx[CpuRegister.Rdi]),
            unchecked((nint)ctx[CpuRegister.Rsi])));

    [SysAbiExport(Nid = "__il2cpp_dyn_field_is_literal", ExportName = "il2cpp_field_is_literal", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int FieldIsLiteral(CpuContext ctx)
    {
        const uint FieldAttributeLiteral = 0x40;
        return ReturnBoolean(
            ctx,
            (Il2CppRuntime.Instance.GetFieldFlags(unchecked((nint)ctx[CpuRegister.Rdi])) &
             FieldAttributeLiteral) != 0);
    }
}
