// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using System.Buffers.Binary;
using SharpEmu.HLE;

namespace SharpEmu.Libs.Il2Cpp;

/// <summary>
/// Remaining Unity 2019.4 reflection APIs used to construct native serialization type trees.
/// </summary>
public static partial class Il2CppRuntimeExports
{
    [SysAbiExport(Nid = "__il2cpp_dyn_class_is_valuetype", ExportName = "il2cpp_class_is_valuetype", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int ClassIsValueType(CpuContext ctx) =>
        ReturnBoolean(ctx, Il2CppRuntime.Instance.IsClassValueType(unchecked((nint)ctx[CpuRegister.Rdi])));

    [SysAbiExport(Nid = "__il2cpp_dyn_class_is_blittable", ExportName = "il2cpp_class_is_blittable", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int ClassIsBlittable(CpuContext ctx) =>
        ReturnBoolean(ctx, Il2CppRuntime.Instance.IsClassBlittable(unchecked((nint)ctx[CpuRegister.Rdi])));

    [SysAbiExport(Nid = "__il2cpp_dyn_class_is_interface", ExportName = "il2cpp_class_is_interface", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int ClassIsInterface(CpuContext ctx) =>
        ReturnBoolean(ctx, Il2CppRuntime.Instance.IsClassInterface(unchecked((nint)ctx[CpuRegister.Rdi])));

    [SysAbiExport(Nid = "__il2cpp_dyn_class_has_references", ExportName = "il2cpp_class_has_references", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int ClassHasReferences(CpuContext ctx) =>
        ReturnBoolean(ctx, Il2CppRuntime.Instance.ClassHasReferences(unchecked((nint)ctx[CpuRegister.Rdi])));

    [SysAbiExport(Nid = "__il2cpp_dyn_class_is_enum", ExportName = "il2cpp_class_is_enum", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int ClassIsEnum(CpuContext ctx) =>
        ReturnBoolean(ctx, Il2CppRuntime.Instance.IsClassEnum(unchecked((nint)ctx[CpuRegister.Rdi])));

    [SysAbiExport(Nid = "__il2cpp_dyn_class_enum_basetype", ExportName = "il2cpp_class_enum_basetype", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int ClassEnumBaseType(CpuContext ctx) =>
        ReturnPointer(ctx, Il2CppRuntime.Instance.GetClassEnumBaseType(unchecked((nint)ctx[CpuRegister.Rdi])));

    [SysAbiExport(Nid = "__il2cpp_dyn_class_get_element_class", ExportName = "il2cpp_class_get_element_class", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int ClassGetElementClass(CpuContext ctx) =>
        ReturnPointer(ctx, Il2CppRuntime.Instance.GetClassElementClass(unchecked((nint)ctx[CpuRegister.Rdi])));

    [SysAbiExport(Nid = "__il2cpp_dyn_class_get_rank", ExportName = "il2cpp_class_get_rank", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int ClassGetRank(CpuContext ctx)
    {
        ctx[CpuRegister.Rax] = Il2CppRuntime.Instance.GetClassRank(unchecked((nint)ctx[CpuRegister.Rdi]));
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_class_get_data_size", ExportName = "il2cpp_class_get_data_size", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int ClassGetDataSize(CpuContext ctx)
    {
        ctx[CpuRegister.Rax] = Il2CppRuntime.Instance.GetClassDataSize(unchecked((nint)ctx[CpuRegister.Rdi]));
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_class_get_static_field_data", ExportName = "il2cpp_class_get_static_field_data", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int ClassGetStaticFieldData(CpuContext ctx) =>
        ReturnPointer(ctx, Il2CppRuntime.Instance.GetClassStaticFieldData(unchecked((nint)ctx[CpuRegister.Rdi])));

    [SysAbiExport(Nid = "__il2cpp_dyn_class_get_bitmap_size", ExportName = "il2cpp_class_get_bitmap_size", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int ClassGetBitmapSize(CpuContext ctx)
    {
        ctx[CpuRegister.Rax] = 0;
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_class_get_bitmap", ExportName = "il2cpp_class_get_bitmap", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int ClassGetBitmap(CpuContext ctx)
    {
        ctx[CpuRegister.Rax] = 0;
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_class_from_system_type", ExportName = "il2cpp_class_from_system_type", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int ClassFromSystemType(CpuContext ctx) =>
        ReturnPointer(ctx, Il2CppRuntime.Instance.GetClassFromSystemType(unchecked((nint)ctx[CpuRegister.Rdi])));

    [SysAbiExport(Nid = "__il2cpp_dyn_class_get_interfaces", ExportName = "il2cpp_class_get_interfaces", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int ClassGetInterfaces(CpuContext ctx)
    {
        var iteratorAddress = ctx[CpuRegister.Rsi];
        if (iteratorAddress == 0 ||
            !ctx.TryReadUInt64(iteratorAddress, out var ordinal) ||
            ordinal > int.MaxValue)
        {
            return ReturnPointer(ctx, 0);
        }

        var result = Il2CppRuntime.Instance.GetInterfaceAtOrdinal(
            unchecked((nint)ctx[CpuRegister.Rdi]),
            (int)ordinal);
        if (result != 0)
        {
            _ = ctx.TryWriteUInt64(iteratorAddress, ordinal + 1);
        }

        return ReturnPointer(ctx, result);
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_class_get_properties", ExportName = "il2cpp_class_get_properties", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int ClassGetProperties(CpuContext ctx)
    {
        var iteratorAddress = ctx[CpuRegister.Rsi];
        if (iteratorAddress == 0 ||
            !ctx.TryReadUInt64(iteratorAddress, out var ordinal) ||
            ordinal > int.MaxValue)
        {
            return ReturnPointer(ctx, 0);
        }

        var result = Il2CppRuntime.Instance.GetPropertyAtOrdinal(
            unchecked((nint)ctx[CpuRegister.Rdi]),
            (int)ordinal);
        if (result != 0)
        {
            _ = ctx.TryWriteUInt64(iteratorAddress, ordinal + 1);
        }

        return ReturnPointer(ctx, result);
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_class_get_property_from_name", ExportName = "il2cpp_class_get_property_from_name", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int ClassGetPropertyFromName(CpuContext ctx)
    {
        Il2CppStrings.TryReadAscii(ctx, ctx[CpuRegister.Rsi], 512, out var wanted);
        for (var ordinal = 0; ; ordinal++)
        {
            var property = Il2CppRuntime.Instance.GetPropertyAtOrdinal(
                unchecked((nint)ctx[CpuRegister.Rdi]),
                ordinal);
            if (property == 0)
            {
                return ReturnPointer(ctx, 0);
            }

            var name = Il2CppRuntime.Instance.GetPropertyName(property);
            if (name != 0 &&
                string.Equals(
                    System.Runtime.InteropServices.Marshal.PtrToStringUTF8(name),
                    wanted,
                    StringComparison.Ordinal))
            {
                return ReturnPointer(ctx, property);
            }
        }
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_class_get_events", ExportName = "il2cpp_class_get_events", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int ClassGetEvents(CpuContext ctx) => ReturnPointer(ctx, 0);

    [SysAbiExport(Nid = "__il2cpp_dyn_class_for_each", ExportName = "il2cpp_class_for_each", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int ClassForEach(CpuContext ctx)
    {
        // Guest callbacks require a scheduler re-entry. Unity's serializer does not rely on this
        // enumeration path once image_get_class is available.
        ctx[CpuRegister.Rax] = 0;
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_method_get_param", ExportName = "il2cpp_method_get_param", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int MethodGetParameter(CpuContext ctx) =>
        ReturnPointer(ctx, Il2CppRuntime.Instance.GetMethodParameterType(
            unchecked((nint)ctx[CpuRegister.Rdi]),
            unchecked((int)ctx[CpuRegister.Rsi])));

    [SysAbiExport(Nid = "__il2cpp_dyn_method_get_param_name", ExportName = "il2cpp_method_get_param_name", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int MethodGetParameterName(CpuContext ctx) =>
        ReturnPointer(ctx, Il2CppRuntime.Instance.GetMethodParameterName(
            unchecked((nint)ctx[CpuRegister.Rdi]),
            unchecked((int)ctx[CpuRegister.Rsi])));

    [SysAbiExport(Nid = "__il2cpp_dyn_method_get_object", ExportName = "il2cpp_method_get_object", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int MethodGetObject(CpuContext ctx) =>
        ReturnPointer(ctx, Il2CppRuntime.Instance.GetMethodObject(unchecked((nint)ctx[CpuRegister.Rdi])));

    [SysAbiExport(Nid = "__il2cpp_dyn_method_get_from_reflection", ExportName = "il2cpp_method_get_from_reflection", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int MethodGetFromReflection(CpuContext ctx) =>
        ReturnPointer(ctx, Il2CppRuntime.Instance.GetMethodFromReflection(unchecked((nint)ctx[CpuRegister.Rdi])));

    [SysAbiExport(Nid = "__il2cpp_dyn_property_get_flags", ExportName = "il2cpp_property_get_flags", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int PropertyGetFlags(CpuContext ctx)
    {
        ctx[CpuRegister.Rax] = Il2CppRuntime.Instance.GetPropertyFlags(unchecked((nint)ctx[CpuRegister.Rdi]));
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_property_get_get_method", ExportName = "il2cpp_property_get_get_method", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int PropertyGetGetMethod(CpuContext ctx) =>
        ReturnPointer(ctx, Il2CppRuntime.Instance.GetPropertyGetMethod(unchecked((nint)ctx[CpuRegister.Rdi])));

    [SysAbiExport(Nid = "__il2cpp_dyn_property_get_set_method", ExportName = "il2cpp_property_get_set_method", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int PropertyGetSetMethod(CpuContext ctx) =>
        ReturnPointer(ctx, Il2CppRuntime.Instance.GetPropertySetMethod(unchecked((nint)ctx[CpuRegister.Rdi])));

    [SysAbiExport(Nid = "__il2cpp_dyn_property_get_name", ExportName = "il2cpp_property_get_name", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int PropertyGetName(CpuContext ctx) =>
        ReturnPointer(ctx, Il2CppRuntime.Instance.GetPropertyName(unchecked((nint)ctx[CpuRegister.Rdi])));

    [SysAbiExport(Nid = "__il2cpp_dyn_property_get_parent", ExportName = "il2cpp_property_get_parent", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int PropertyGetParent(CpuContext ctx) =>
        ReturnPointer(ctx, Il2CppRuntime.Instance.GetPropertyParent(unchecked((nint)ctx[CpuRegister.Rdi])));

    [SysAbiExport(Nid = "__il2cpp_dyn_type_get_object", ExportName = "il2cpp_type_get_object", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int TypeGetObject(CpuContext ctx) =>
        ReturnPointer(ctx, Il2CppRuntime.Instance.GetTypeObject(unchecked((nint)ctx[CpuRegister.Rdi])));

    [SysAbiExport(Nid = "__il2cpp_dyn_type_get_type", ExportName = "il2cpp_type_get_type", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int TypeGetType(CpuContext ctx)
    {
        ctx[CpuRegister.Rax] = Il2CppRuntime.Instance.GetTypeKind(unchecked((nint)ctx[CpuRegister.Rdi]));
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_type_get_class_or_element_class", ExportName = "il2cpp_type_get_class_or_element_class", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int TypeGetClassOrElementClass(CpuContext ctx) =>
        ReturnPointer(ctx, Il2CppRuntime.Instance.GetTypeClassOrElementClass(unchecked((nint)ctx[CpuRegister.Rdi])));

    [SysAbiExport(Nid = "__il2cpp_dyn_type_get_name", ExportName = "il2cpp_type_get_name", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int TypeGetName(CpuContext ctx) =>
        ReturnPointer(ctx, Il2CppRuntime.Instance.GetTypeName(
            unchecked((nint)ctx[CpuRegister.Rdi]),
            assemblyQualified: false));

    [SysAbiExport(Nid = "__il2cpp_dyn_type_get_assembly_qualified_name", ExportName = "il2cpp_type_get_assembly_qualified_name", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int TypeGetAssemblyQualifiedName(CpuContext ctx) =>
        ReturnPointer(ctx, Il2CppRuntime.Instance.GetTypeName(
            unchecked((nint)ctx[CpuRegister.Rdi]),
            assemblyQualified: true));

    [SysAbiExport(Nid = "__il2cpp_dyn_type_is_byref", ExportName = "il2cpp_type_is_byref", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int TypeIsByRef(CpuContext ctx) =>
        ReturnBoolean(ctx, Il2CppRuntime.Instance.IsTypeByRef(unchecked((nint)ctx[CpuRegister.Rdi])));

    [SysAbiExport(Nid = "__il2cpp_dyn_type_get_attrs", ExportName = "il2cpp_type_get_attrs", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int TypeGetAttributes(CpuContext ctx)
    {
        ctx[CpuRegister.Rax] = Il2CppRuntime.Instance.GetTypeAttributes(unchecked((nint)ctx[CpuRegister.Rdi]));
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_type_equals", ExportName = "il2cpp_type_equals", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int TypeEquals(CpuContext ctx) =>
        ReturnBoolean(ctx, ctx[CpuRegister.Rdi] == ctx[CpuRegister.Rsi]);

    [SysAbiExport(Nid = "__il2cpp_dyn_type_is_static", ExportName = "il2cpp_type_is_static", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int TypeIsStatic(CpuContext ctx)
    {
        const uint fieldAttributeStatic = 0x10;
        return ReturnBoolean(
            ctx,
            (Il2CppRuntime.Instance.GetTypeAttributes(unchecked((nint)ctx[CpuRegister.Rdi])) &
             fieldAttributeStatic) != 0);
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_type_is_pointer_type", ExportName = "il2cpp_type_is_pointer_type", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int TypeIsPointer(CpuContext ctx) =>
        ReturnBoolean(ctx, Il2CppRuntime.Instance.IsTypePointer(unchecked((nint)ctx[CpuRegister.Rdi])));

    [SysAbiExport(Nid = "__il2cpp_dyn_type_get_name_chunked", ExportName = "il2cpp_type_get_name_chunked", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int TypeGetNameChunked(CpuContext ctx)
    {
        ctx[CpuRegister.Rax] = 0;
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_object_unbox", ExportName = "il2cpp_object_unbox", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int ObjectUnbox(CpuContext ctx) =>
        ReturnPointer(ctx, ctx[CpuRegister.Rdi] == 0 ? 0 : unchecked((nint)(ctx[CpuRegister.Rdi] + 16)));

    [SysAbiExport(Nid = "__il2cpp_dyn_value_box", ExportName = "il2cpp_value_box", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int ValueBox(CpuContext ctx) =>
        ReturnPointer(ctx, Il2CppRuntime.Instance.BoxValue(
            unchecked((nint)ctx[CpuRegister.Rdi]),
            unchecked((nint)ctx[CpuRegister.Rsi])));

    [SysAbiExport(Nid = "__il2cpp_dyn_object_get_virtual_method", ExportName = "il2cpp_object_get_virtual_method", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int ObjectGetVirtualMethod(CpuContext ctx) =>
        ReturnPointer(ctx, unchecked((nint)ctx[CpuRegister.Rsi]));

    [SysAbiExport(Nid = "__il2cpp_dyn_string_intern", ExportName = "il2cpp_string_intern", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int StringIntern(CpuContext ctx) =>
        ReturnPointer(ctx, Il2CppRuntime.Instance.InternString(unchecked((nint)ctx[CpuRegister.Rdi])));

    [SysAbiExport(Nid = "__il2cpp_dyn_string_is_interned", ExportName = "il2cpp_string_is_interned", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int StringIsInterned(CpuContext ctx) =>
        ReturnPointer(ctx, Il2CppRuntime.Instance.FindInternedString(unchecked((nint)ctx[CpuRegister.Rdi])));

    [SysAbiExport(Nid = "__il2cpp_dyn_array_new_full", ExportName = "il2cpp_array_new_full", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int ArrayNewFull(CpuContext ctx)
    {
        var arrayClass = unchecked((nint)ctx[CpuRegister.Rdi]);
        var rank = Math.Max(Il2CppRuntime.Instance.GetClassRank(arrayClass), 1u);
        var lengths = ctx[CpuRegister.Rsi];
        ulong total = 1;
        for (var i = 0u; i < rank; i++)
        {
            if (lengths == 0 ||
                !ctx.TryReadUInt64(lengths + i * sizeof(ulong), out var length) ||
                length > 0x1000_0000 ||
                (length != 0 && total > 0x1000_0000 / length))
            {
                return ReturnPointer(ctx, 0);
            }

            total *= length;
        }

        return ReturnPointer(ctx, Il2CppRuntime.Instance.NewArray(arrayClass, total));
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_field_get_value", ExportName = "il2cpp_field_get_value", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int FieldGetValue(CpuContext ctx)
    {
        CopyFieldToGuest(
            ctx,
            unchecked((nint)ctx[CpuRegister.Rsi]),
            unchecked((nint)ctx[CpuRegister.Rdi]),
            ctx[CpuRegister.Rdx]);
        ctx[CpuRegister.Rax] = 0;
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_field_static_get_value", ExportName = "il2cpp_field_static_get_value", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int FieldStaticGetValue(CpuContext ctx)
    {
        CopyFieldToGuest(
            ctx,
            unchecked((nint)ctx[CpuRegister.Rdi]),
            0,
            ctx[CpuRegister.Rsi]);
        ctx[CpuRegister.Rax] = 0;
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_field_set_value", ExportName = "il2cpp_field_set_value", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int FieldSetValue(CpuContext ctx)
    {
        CopyGuestToField(
            ctx,
            unchecked((nint)ctx[CpuRegister.Rsi]),
            unchecked((nint)ctx[CpuRegister.Rdi]),
            ctx[CpuRegister.Rdx]);
        ctx[CpuRegister.Rax] = 0;
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_field_static_set_value", ExportName = "il2cpp_field_static_set_value", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int FieldStaticSetValue(CpuContext ctx)
    {
        CopyGuestToField(
            ctx,
            unchecked((nint)ctx[CpuRegister.Rdi]),
            0,
            ctx[CpuRegister.Rsi]);
        ctx[CpuRegister.Rax] = 0;
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_field_get_value_object", ExportName = "il2cpp_field_get_value_object", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int FieldGetValueObject(CpuContext ctx)
    {
        var field = unchecked((nint)ctx[CpuRegister.Rdi]);
        var storage = Il2CppRuntime.Instance.GetFieldStorage(
            field,
            unchecked((nint)ctx[CpuRegister.Rsi]),
            out _);
        if (storage == 0)
        {
            return ReturnPointer(ctx, 0);
        }

        if (Il2CppRuntime.Instance.IsFieldReference(field))
        {
            return ReturnPointer(
                ctx,
                ctx.TryReadUInt64(unchecked((ulong)storage), out var value)
                    ? unchecked((nint)value)
                    : 0);
        }

        return ReturnPointer(
            ctx,
            Il2CppRuntime.Instance.BoxValue(
                Il2CppRuntime.Instance.GetFieldValueClass(field),
                storage));
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_field_set_value_object", ExportName = "il2cpp_field_set_value_object", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int FieldSetValueObject(CpuContext ctx)
    {
        var field = unchecked((nint)ctx[CpuRegister.Rsi]);
        var valueObject = ctx[CpuRegister.Rdx];
        var source = Il2CppRuntime.Instance.IsFieldReference(field) || valueObject == 0
            ? valueObject
            : valueObject + 16;
        if (Il2CppRuntime.Instance.IsFieldReference(field))
        {
            Span<byte> pointer = stackalloc byte[sizeof(ulong)];
            BinaryPrimitives.WriteUInt64LittleEndian(pointer, valueObject);
            var storage = Il2CppRuntime.Instance.GetFieldStorage(
                field,
                unchecked((nint)ctx[CpuRegister.Rdi]),
                out _);
            if (storage != 0)
            {
                _ = ctx.Memory.TryWrite(unchecked((ulong)storage), pointer);
            }
        }
        else
        {
            CopyGuestToField(
                ctx,
                field,
                unchecked((nint)ctx[CpuRegister.Rdi]),
                source);
        }

        ctx[CpuRegister.Rax] = 0;
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_runtime_class_init", ExportName = "il2cpp_runtime_class_init", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int RuntimeClassInit(CpuContext ctx)
    {
        _ = Il2CppRuntime.Instance.GetClassStaticFieldData(unchecked((nint)ctx[CpuRegister.Rdi]));
        ctx[CpuRegister.Rax] = 0;
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_runtime_object_init", ExportName = "il2cpp_runtime_object_init", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int RuntimeObjectInit(CpuContext ctx)
    {
        ctx[CpuRegister.Rax] = 0;
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_runtime_object_init_exception", ExportName = "il2cpp_runtime_object_init_exception", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int RuntimeObjectInitException(CpuContext ctx)
    {
        if (ctx[CpuRegister.Rsi] != 0)
        {
            _ = ctx.TryWriteUInt64(ctx[CpuRegister.Rsi], 0);
        }

        ctx[CpuRegister.Rax] = 0;
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_runtime_invoke_convert_args", ExportName = "il2cpp_runtime_invoke_convert_args", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int RuntimeInvokeConvertArguments(CpuContext ctx) => RuntimeInvoke(ctx);

    [SysAbiExport(Nid = "__il2cpp_dyn_stats_dump_to_file", ExportName = "il2cpp_stats_dump_to_file", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int StatsDumpToFile(CpuContext ctx) => ReturnBoolean(ctx, false);

    [SysAbiExport(Nid = "__il2cpp_dyn_stats_get_value", ExportName = "il2cpp_stats_get_value", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int StatsGetValue(CpuContext ctx)
    {
        ctx[CpuRegister.Rax] = 0;
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_exception_from_name_msg", ExportName = "il2cpp_exception_from_name_msg", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int ExceptionFromNameMessage(CpuContext ctx)
    {
        Il2CppStrings.TryReadAscii(ctx, ctx[CpuRegister.Rsi], 512, out var namespaze);
        Il2CppStrings.TryReadAscii(ctx, ctx[CpuRegister.Rdx], 512, out var name);
        var klass = Il2CppRuntime.Instance.GetClassFromName(
            unchecked((nint)ctx[CpuRegister.Rdi]),
            namespaze,
            name);
        return ReturnPointer(ctx, Il2CppRuntime.Instance.NewObject(klass));
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_get_exception_argument_null", ExportName = "il2cpp_get_exception_argument_null", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int GetExceptionArgumentNull(CpuContext ctx) =>
        ReturnPointer(
            ctx,
            Il2CppRuntime.Instance.NewObject(
                Il2CppRuntime.Instance.GetClassFromName("System", "ArgumentNullException")));

    [SysAbiExport(Nid = "__il2cpp_dyn_raise_exception", ExportName = "il2cpp_raise_exception", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int RaiseException(CpuContext ctx)
    {
        ctx[CpuRegister.Rax] = 0;
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(Nid = "__il2cpp_dyn_unhandled_exception", ExportName = "il2cpp_unhandled_exception", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int UnhandledException(CpuContext ctx) => RaiseException(ctx);

    [SysAbiExport(Nid = "__il2cpp_dyn_format_exception", ExportName = "il2cpp_format_exception", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int FormatException(CpuContext ctx) => FormatEmptyText(ctx);

    [SysAbiExport(Nid = "__il2cpp_dyn_format_stack_trace", ExportName = "il2cpp_format_stack_trace", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int FormatStackTrace(CpuContext ctx) => FormatEmptyText(ctx);

    [SysAbiExport(Nid = "__il2cpp_dyn_unity_liveness_calculation_begin", ExportName = "il2cpp_unity_liveness_calculation_begin", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int LivenessCalculationBegin(CpuContext ctx) =>
        ReturnPointer(ctx, Il2CppRuntime.Instance.GetOpaqueHandle("liveness"));

    [SysAbiExport(Nid = "__il2cpp_dyn_unity_liveness_calculation_end", ExportName = "il2cpp_unity_liveness_calculation_end", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int LivenessCalculationEnd(CpuContext ctx) => ReturnPointer(ctx, 0);

    [SysAbiExport(Nid = "__il2cpp_dyn_unity_liveness_calculation_from_root", ExportName = "il2cpp_unity_liveness_calculation_from_root", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int LivenessCalculationFromRoot(CpuContext ctx) => ReturnPointer(ctx, 0);

    [SysAbiExport(Nid = "__il2cpp_dyn_unity_liveness_calculation_from_statics", ExportName = "il2cpp_unity_liveness_calculation_from_statics", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int LivenessCalculationFromStatics(CpuContext ctx) => ReturnPointer(ctx, 0);

    [SysAbiExport(Nid = "__il2cpp_dyn_capture_memory_snapshot", ExportName = "il2cpp_capture_memory_snapshot", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int CaptureMemorySnapshot(CpuContext ctx) => ReturnPointer(ctx, 0);

    [SysAbiExport(Nid = "__il2cpp_dyn_free_captured_memory_snapshot", ExportName = "il2cpp_free_captured_memory_snapshot", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int FreeCapturedMemorySnapshot(CpuContext ctx) => ReturnPointer(ctx, 0);

    [SysAbiExport(Nid = "__il2cpp_dyn_debug_get_method_info", ExportName = "il2cpp_debug_get_method_info", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libIl2Cpp")]
    public static int DebugGetMethodInfo(CpuContext ctx) => ReturnBoolean(ctx, false);

    private static void CopyFieldToGuest(CpuContext ctx, nint field, nint instance, ulong destination)
    {
        var source = Il2CppRuntime.Instance.GetFieldStorage(field, instance, out var size);
        if (source == 0 || destination == 0 || size == 0 || size > 1024 * 1024)
        {
            return;
        }

        var bytes = new byte[(int)size];
        if (ctx.Memory.TryRead(unchecked((ulong)source), bytes))
        {
            _ = ctx.Memory.TryWrite(destination, bytes);
        }
    }

    private static void CopyGuestToField(CpuContext ctx, nint field, nint instance, ulong source)
    {
        var destination = Il2CppRuntime.Instance.GetFieldStorage(field, instance, out var size);
        if (destination == 0 || source == 0 || size == 0 || size > 1024 * 1024)
        {
            return;
        }

        var bytes = new byte[(int)size];
        if (ctx.Memory.TryRead(source, bytes))
        {
            _ = ctx.Memory.TryWrite(unchecked((ulong)destination), bytes);
        }
    }

    private static int FormatEmptyText(CpuContext ctx)
    {
        var buffer = ctx[CpuRegister.Rsi];
        if (buffer != 0 && ctx[CpuRegister.Rdx] != 0)
        {
            Span<byte> terminator = stackalloc byte[1];
            _ = ctx.Memory.TryWrite(buffer, terminator);
        }

        ctx[CpuRegister.Rax] = 0;
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }
}
