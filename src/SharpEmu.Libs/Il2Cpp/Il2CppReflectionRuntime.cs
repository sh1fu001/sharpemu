// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace SharpEmu.Libs.Il2Cpp;

/// <summary>
/// Reflection and serialization-oriented portions of the Unity 2019.4 IL2CPP embedding API.
/// Kept beside the core allocator/runtime so the metadata graph remains the single source of truth.
/// </summary>
public sealed partial class Il2CppRuntime
{
    private const uint TypeVoid = 0x01;
    private const uint TypeBoolean = 0x02;
    private const uint TypeChar = 0x03;
    private const uint TypeI1 = 0x04;
    private const uint TypeU1 = 0x05;
    private const uint TypeI2 = 0x06;
    private const uint TypeU2 = 0x07;
    private const uint TypeI4 = 0x08;
    private const uint TypeU4 = 0x09;
    private const uint TypeI8 = 0x0A;
    private const uint TypeU8 = 0x0B;
    private const uint TypeR4 = 0x0C;
    private const uint TypeR8 = 0x0D;
    private const uint TypeString = 0x0E;
    private const uint TypePointer = 0x0F;
    private const uint TypeByRef = 0x10;
    private const uint TypeValueType = 0x11;
    private const uint TypeClass = 0x12;
    private const uint TypeArray = 0x14;
    private const uint TypeGenericInstance = 0x15;
    private const uint TypeNativeInt = 0x18;
    private const uint TypeNativeUInt = 0x19;
    private const uint TypeObject = 0x1C;
    private const uint TypeSzArray = 0x1D;

    private const uint FieldAttributeStatic = 0x10;
    private const uint TypeAttributeInterface = 0x20;
    private const int Class_StaticFields = 0xB8;

    private readonly Dictionary<nint, nint> _typeNameByHandle = new();
    private readonly Dictionary<nint, nint> _qualifiedTypeNameByHandle = new();
    private readonly Dictionary<nint, nint> _typeObjectByHandle = new();
    private readonly Dictionary<nint, nint> _typeHandleByObject = new();
    private readonly Dictionary<nint, nint> _methodObjectByHandle = new();
    private readonly Dictionary<nint, nint> _methodHandleByObject = new();
    private readonly Dictionary<nint, (nint ElementClass, uint Rank, bool Bounded)> _arrayShapeByClass = new();
    private readonly Dictionary<nint, nint> _staticFieldsByClass = new();
    private readonly Dictionary<(nint Method, int Ordinal), nint> _parameterNameByMethod = new();
    private readonly Dictionary<int, nint> _propertyByIndex = new();
    private readonly Dictionary<nint, int> _propertyIndexByHandle = new();
    private readonly Dictionary<string, nint> _internedStrings = new(StringComparer.Ordinal);

    public uint GetTypeKind(nint typeHandle)
    {
        lock (_gate)
        {
            var index = GetTypeRegistrationIndexLocked(typeHandle);
            return index < 0 || _codeRegistration is null ? 0 : _codeRegistration.GetTypeKind(index);
        }
    }

    public uint GetTypeAttributes(nint typeHandle)
    {
        lock (_gate)
        {
            var index = GetTypeRegistrationIndexLocked(typeHandle);
            return index < 0 || _codeRegistration is null ? 0 : _codeRegistration.GetTypeAttributes(index);
        }
    }

    public bool IsTypeByRef(nint typeHandle)
    {
        lock (_gate)
        {
            var index = GetTypeRegistrationIndexLocked(typeHandle);
            return index >= 0 &&
                   _codeRegistration is not null &&
                   (_codeRegistration.GetTypeBitfield(index) & (1u << 30)) != 0;
        }
    }

    public bool IsTypePointer(nint typeHandle) => GetTypeKind(typeHandle) == TypePointer;

    public nint GetTypeClassOrElementClass(nint typeHandle)
    {
        lock (_gate)
        {
            var index = GetTypeRegistrationIndexLocked(typeHandle);
            if (index < 0 || _codeRegistration is null)
            {
                return 0;
            }

            var kind = _codeRegistration.GetTypeKind(index);
            if (kind is TypePointer or TypeByRef or TypeSzArray)
            {
                var elementType = unchecked((nint)_codeRegistration.GetTypeData(index));
                return ResolveClassFromTypeLocked(elementType);
            }

            if (kind == TypeArray &&
                TryReadPointer(_codeRegistration.GetTypeData(index), out var arrayElementType))
            {
                return ResolveClassFromTypeLocked(unchecked((nint)arrayElementType));
            }

            return ResolveClassFromTypeLocked(typeHandle);
        }
    }

    public nint GetTypeName(nint typeHandle, bool assemblyQualified)
    {
        if (typeHandle == 0)
        {
            lock (_gate)
            {
                return GetEmptyCStringLocked();
            }
        }

        lock (_gate)
        {
            var cache = assemblyQualified ? _qualifiedTypeNameByHandle : _typeNameByHandle;
            if (cache.TryGetValue(typeHandle, out var existing))
            {
                return existing;
            }

            var text = BuildTypeNameLocked(typeHandle);
            if (assemblyQualified)
            {
                var klass = ResolveClassFromTypeLocked(typeHandle);
                var assemblyPointer = GetClassAssemblyName(klass);
                var assembly = assemblyPointer == 0
                    ? string.Empty
                    : Marshal.PtrToStringUTF8(assemblyPointer) ?? string.Empty;
                if (assembly.Length != 0)
                {
                    text += ", " + assembly;
                }
            }

            var pointer = AllocateCString(text);
            cache[typeHandle] = pointer;
            return pointer;
        }
    }

    public unsafe nint GetTypeObject(nint typeHandle)
    {
        if (typeHandle == 0)
        {
            return 0;
        }

        lock (_gate)
        {
            if (_typeObjectByHandle.TryGetValue(typeHandle, out var existing))
            {
                return existing;
            }

            // RuntimeType's managed payload is not inspected by Unity's native serializer; it only
            // needs a stable object identity that can be passed back to the embedding API.
            var runtimeTypeClass = GetClassFromName("System", "RuntimeType");
            if (runtimeTypeClass == 0)
            {
                runtimeTypeClass = GetClassFromName("System", "Type");
            }

            var block = (nint*)AllocateBlock(0x20);
            block[0] = runtimeTypeClass;
            block[2] = typeHandle;
            var handle = (nint)block;
            _typeObjectByHandle[typeHandle] = handle;
            _typeHandleByObject[handle] = typeHandle;
            return handle;
        }
    }

    public nint GetClassFromSystemType(nint typeObject)
    {
        lock (_gate)
        {
            return _typeHandleByObject.TryGetValue(typeObject, out var typeHandle)
                ? ResolveClassFromTypeLocked(typeHandle)
                : 0;
        }
    }

    public unsafe nint GetMethodObject(nint methodHandle)
    {
        if (methodHandle == 0)
        {
            return 0;
        }

        lock (_gate)
        {
            if (_methodObjectByHandle.TryGetValue(methodHandle, out var existing))
            {
                return existing;
            }

            var reflectionClass = GetClassFromName("System.Reflection", "RuntimeMethodInfo");
            var block = (nint*)AllocateBlock(0x20);
            block[0] = reflectionClass;
            block[2] = methodHandle;
            var handle = (nint)block;
            _methodObjectByHandle[methodHandle] = handle;
            _methodHandleByObject[handle] = methodHandle;
            return handle;
        }
    }

    public nint GetMethodFromReflection(nint reflectionObject)
    {
        lock (_gate)
        {
            return _methodHandleByObject.TryGetValue(reflectionObject, out var method)
                ? method
                : 0;
        }
    }

    public bool IsClassValueType(nint classHandle)
    {
        lock (_gate)
        {
            return TryGetClassRegistrationIndexLocked(classHandle, out var registrationIndex) &&
                   _codeRegistration!.GetTypeKind(registrationIndex) == TypeValueType;
        }
    }

    public bool IsClassInterface(nint classHandle) =>
        (GetClassFlags(classHandle) & TypeAttributeInterface) != 0;

    public bool IsClassEnum(nint classHandle)
    {
        var enumClass = GetClassFromName("System", "Enum");
        return enumClass != 0 && GetClassParent(classHandle) == enumClass;
    }

    public uint GetClassValueSize(nint classHandle, out uint alignment)
    {
        alignment = 1;
        if (classHandle == 0)
        {
            return 0;
        }

        lock (_gate)
        {
            if (!IsClassValueType(classHandle))
            {
                alignment = (uint)IntPtr.Size;
                return (uint)IntPtr.Size;
            }

            var instanceSize = GetInstanceSize(classHandle);
            var size = instanceSize > ObjectHeaderSize ? instanceSize - ObjectHeaderSize : instanceSize;
            alignment = GetNaturalAlignment(size);
            return size;
        }
    }

    public uint GetClassDataSize(nint classHandle)
    {
        lock (_gate)
        {
            if (!_typeIndexByClass.TryGetValue(classHandle, out var typeIndex) ||
                _codeRegistration is null)
            {
                return 0;
            }

            var nativeSize = _codeRegistration.GetNativeSize(typeIndex);
            return nativeSize != 0 && nativeSize != uint.MaxValue
                ? nativeSize
                : GetClassValueSize(classHandle, out _);
        }
    }

    public nint GetClassElementClass(nint classHandle)
    {
        lock (_gate)
        {
            if (_arrayShapeByClass.TryGetValue(classHandle, out var shape))
            {
                return shape.ElementClass;
            }

            if (_metadata is null ||
                _codeRegistration is null ||
                !_typeIndexByClass.TryGetValue(classHandle, out var typeIndex))
            {
                return 0;
            }

            var elementRegistrationIndex = _metadata.GetTypeElementTypeIndex(typeIndex);
            return elementRegistrationIndex < 0
                ? 0
                : ResolveClassFromTypeLocked(unchecked((nint)_codeRegistration.GetTypePointer(
                    elementRegistrationIndex)));
        }
    }

    public uint GetClassRank(nint classHandle)
    {
        lock (_gate)
        {
            return _arrayShapeByClass.TryGetValue(classHandle, out var shape) ? shape.Rank : 0;
        }
    }

    public nint GetClassEnumBaseType(nint classHandle)
    {
        lock (_gate)
        {
            if (!IsClassEnum(classHandle) ||
                _metadata is null ||
                _codeRegistration is null ||
                !_typeIndexByClass.TryGetValue(classHandle, out var typeIndex))
            {
                return 0;
            }

            var valueField = _metadata.FindFieldIndex(typeIndex, "value__");
            var fieldType = valueField < 0 ? -1 : _metadata.GetFieldTypeIndex(valueField);
            return fieldType < 0
                ? 0
                : unchecked((nint)_codeRegistration.GetTypePointer(fieldType));
        }
    }

    public nint GetInterfaceAtOrdinal(nint classHandle, int ordinal)
    {
        lock (_gate)
        {
            if (_metadata is null ||
                _codeRegistration is null ||
                ordinal < 0 ||
                !_typeIndexByClass.TryGetValue(classHandle, out var typeIndex))
            {
                return 0;
            }

            var interfaceTypeIndex = _metadata.GetInterfaceTypeIndex(typeIndex, ordinal);
            return interfaceTypeIndex < 0
                ? 0
                : ResolveClassFromTypeLocked(unchecked((nint)_codeRegistration.GetTypePointer(
                    interfaceTypeIndex)));
        }
    }

    public bool ClassHasReferences(nint classHandle)
    {
        lock (_gate)
        {
            return ClassHasReferencesLocked(classHandle, new HashSet<nint>());
        }
    }

    public bool IsClassBlittable(nint classHandle)
    {
        if (!IsClassValueType(classHandle))
        {
            return false;
        }

        lock (_gate)
        {
            return !ClassHasReferencesLocked(classHandle, new HashSet<nint>());
        }
    }

    public unsafe nint GetClassStaticFieldData(nint classHandle)
    {
        if (classHandle == 0)
        {
            return 0;
        }

        lock (_gate)
        {
            if (_staticFieldsByClass.TryGetValue(classHandle, out var existing))
            {
                return existing;
            }

            if (!_typeIndexByClass.TryGetValue(classHandle, out var typeIndex) ||
                _codeRegistration is null)
            {
                return 0;
            }

            var size = _codeRegistration.GetStaticFieldsSize(typeIndex);
            if (size == 0)
            {
                return 0;
            }

            var block = AllocateBlock(size);
            _staticFieldsByClass[classHandle] = block;
            *(nint*)((byte*)classHandle + Class_StaticFields) = block;
            return block;
        }
    }

    public nint GetMethodParameterType(nint methodHandle, int ordinal)
    {
        lock (_gate)
        {
            if (!TryGetMethodIndex(methodHandle, out var methodIndex) ||
                _metadata is null ||
                _codeRegistration is null)
            {
                return 0;
            }

            var typeIndex = _metadata.GetMethodParameterTypeIndex(methodIndex, ordinal);
            return typeIndex < 0
                ? 0
                : unchecked((nint)_codeRegistration.GetTypePointer(typeIndex));
        }
    }

    public nint GetMethodParameterName(nint methodHandle, int ordinal)
    {
        lock (_gate)
        {
            if (!TryGetMethodIndex(methodHandle, out var methodIndex) || _metadata is null)
            {
                return GetEmptyCStringLocked();
            }

            var key = (methodHandle, ordinal);
            if (_parameterNameByMethod.TryGetValue(key, out var existing))
            {
                return existing;
            }

            var pointer = AllocateCString(_metadata.GetMethodParameterName(methodIndex, ordinal));
            _parameterNameByMethod[key] = pointer;
            return pointer;
        }
    }

    public nint GetPropertyAtOrdinal(nint classHandle, int ordinal)
    {
        lock (_gate)
        {
            if (_metadata is null ||
                ordinal < 0 ||
                !_typeIndexByClass.TryGetValue(classHandle, out var typeIndex))
            {
                return 0;
            }

            var start = _metadata.GetPropertyStart(typeIndex);
            var count = _metadata.GetPropertyCount(typeIndex);
            return start < 0 || ordinal >= count
                ? 0
                : GetOrCreatePropertyLocked(start + ordinal, classHandle);
        }
    }

    public unsafe nint GetPropertyName(nint propertyHandle)
    {
        lock (_gate)
        {
            return TryGetPropertyIndexLocked(propertyHandle, out _)
                ? *(nint*)((byte*)propertyHandle + 0x08)
                : GetEmptyCStringLocked();
        }
    }

    public nint GetPropertyGetMethod(nint propertyHandle)
    {
        lock (_gate)
        {
            if (!TryGetPropertyIndexLocked(propertyHandle, out var index) || _metadata is null)
            {
                return 0;
            }

            var methodIndex = _metadata.GetPropertyGetMethodIndex(index);
            return methodIndex < 0 ? 0 : GetOrCreateMethod(methodIndex);
        }
    }

    public nint GetPropertySetMethod(nint propertyHandle)
    {
        lock (_gate)
        {
            if (!TryGetPropertyIndexLocked(propertyHandle, out var index) || _metadata is null)
            {
                return 0;
            }

            var methodIndex = _metadata.GetPropertySetMethodIndex(index);
            return methodIndex < 0 ? 0 : GetOrCreateMethod(methodIndex);
        }
    }

    public nint GetPropertyParent(nint propertyHandle)
    {
        lock (_gate)
        {
            if (!TryGetPropertyIndexLocked(propertyHandle, out _))
            {
                return 0;
            }

            unsafe
            {
                return *(nint*)propertyHandle;
            }
        }
    }

    public uint GetPropertyFlags(nint propertyHandle)
    {
        lock (_gate)
        {
            return TryGetPropertyIndexLocked(propertyHandle, out var index) && _metadata is not null
                ? _metadata.GetPropertyAttributes(index)
                : 0;
        }
    }

    public unsafe nint GetFieldStorage(nint fieldHandle, nint objectHandle, out uint size)
    {
        size = 0;
        lock (_gate)
        {
            if (!_fieldByHandle.TryGetValue(fieldHandle, out var field) ||
                _metadata is null)
            {
                return 0;
            }

            var offset = GetFieldOffset(fieldHandle);
            if (offset < 0)
            {
                return 0;
            }

            size = GetTypeValueSizeLocked(_metadata.GetFieldTypeIndex(field.GlobalIndex));
            if ((GetFieldFlags(fieldHandle) & FieldAttributeStatic) != 0)
            {
                var parent = GetFieldParent(fieldHandle);
                var staticData = GetClassStaticFieldData(parent);
                return staticData == 0 ? 0 : staticData + offset;
            }

            return objectHandle == 0 ? 0 : objectHandle + offset;
        }
    }

    public bool IsFieldReference(nint fieldHandle)
    {
        lock (_gate)
        {
            if (!_fieldByHandle.TryGetValue(fieldHandle, out var field) ||
                _metadata is null ||
                _codeRegistration is null)
            {
                return true;
            }

            return IsReferenceTypeKind(
                _codeRegistration.GetTypeKind(_metadata.GetFieldTypeIndex(field.GlobalIndex)));
        }
    }

    public nint GetFieldValueClass(nint fieldHandle)
    {
        lock (_gate)
        {
            return ResolveClassFromTypeLocked(GetFieldType(fieldHandle));
        }
    }

    public unsafe nint BoxValue(nint classHandle, nint valueAddress)
    {
        if (classHandle == 0 || valueAddress == 0)
        {
            return 0;
        }

        var boxed = NewObject(classHandle);
        var size = GetClassValueSize(classHandle, out _);
        if (boxed == 0 || size == 0)
        {
            return boxed;
        }

        Buffer.MemoryCopy(
            (void*)valueAddress,
            (byte*)boxed + ObjectHeaderSize,
            size,
            size);
        return boxed;
    }

    public nint InternString(nint stringHandle)
    {
        if (stringHandle == 0)
        {
            return 0;
        }

        lock (_gate)
        {
            var length = GetStringLength(stringHandle);
            var value = length <= 0
                ? string.Empty
                : Marshal.PtrToStringUni(GetStringChars(stringHandle), length) ?? string.Empty;
            if (_internedStrings.TryGetValue(value, out var existing))
            {
                return existing;
            }

            _internedStrings[value] = stringHandle;
            return stringHandle;
        }
    }

    public nint FindInternedString(nint stringHandle)
    {
        if (stringHandle == 0)
        {
            return 0;
        }

        lock (_gate)
        {
            var length = GetStringLength(stringHandle);
            var value = length <= 0
                ? string.Empty
                : Marshal.PtrToStringUni(GetStringChars(stringHandle), length) ?? string.Empty;
            return _internedStrings.TryGetValue(value, out var existing) ? existing : 0;
        }
    }

    private nint ResolveClassFromTypeLocked(nint typeHandle)
    {
        var registrationIndex = GetTypeRegistrationIndexLocked(typeHandle);
        if (registrationIndex < 0 || _codeRegistration is null)
        {
            return 0;
        }

        var typeDefinitionIndex = _codeRegistration.TryGetClassTypeDefinitionIndex(registrationIndex);
        if (typeDefinitionIndex >= 0)
        {
            return GetOrCreateClass(typeDefinitionIndex);
        }

        var kind = _codeRegistration.GetTypeKind(registrationIndex);
        var primitive = GetPrimitiveClassLocked(kind);
        if (primitive != 0)
        {
            return primitive;
        }

        if (kind is TypePointer or TypeByRef)
        {
            return ResolveClassFromTypeLocked(unchecked((nint)_codeRegistration.GetTypeData(
                registrationIndex)));
        }

        if (kind == TypeSzArray)
        {
            var element = ResolveClassFromTypeLocked(unchecked((nint)_codeRegistration.GetTypeData(
                registrationIndex)));
            return element == 0 ? 0 : GetArrayClass(element, 1);
        }

        if (kind == TypeArray &&
            TryReadPointer(_codeRegistration.GetTypeData(registrationIndex), out var elementType))
        {
            var element = ResolveClassFromTypeLocked(unchecked((nint)elementType));
            var rank = ReadByte(_codeRegistration.GetTypeData(registrationIndex) + 8);
            return element == 0 ? 0 : GetArrayClass(element, Math.Max(rank, (byte)1));
        }

        if (kind == TypeGenericInstance)
        {
            var genericClass = _codeRegistration.GetTypeData(registrationIndex);
            if (TryReadInt32(genericClass, out var genericTypeDefinition) &&
                genericTypeDefinition >= 0 &&
                genericTypeDefinition < (_metadata?.TypeCount ?? 0))
            {
                return GetOrCreateClass(genericTypeDefinition);
            }
        }

        return 0;
    }

    private string BuildTypeNameLocked(nint typeHandle)
    {
        var registrationIndex = GetTypeRegistrationIndexLocked(typeHandle);
        if (registrationIndex < 0 || _codeRegistration is null)
        {
            return string.Empty;
        }

        var kind = _codeRegistration.GetTypeKind(registrationIndex);
        var klass = ResolveClassFromTypeLocked(typeHandle);
        string name;
        if (klass != 0)
        {
            var namePointer = GetClassName(klass);
            var namespacePointer = GetClassNamespace(klass);
            var shortName = namePointer == 0 ? string.Empty : Marshal.PtrToStringUTF8(namePointer) ?? string.Empty;
            var namespaze = namespacePointer == 0 ? string.Empty : Marshal.PtrToStringUTF8(namespacePointer) ?? string.Empty;
            name = namespaze.Length == 0 ? shortName : namespaze + "." + shortName;
        }
        else
        {
            name = kind switch
            {
                TypeVoid => "System.Void",
                TypeBoolean => "System.Boolean",
                TypeChar => "System.Char",
                TypeI1 => "System.SByte",
                TypeU1 => "System.Byte",
                TypeI2 => "System.Int16",
                TypeU2 => "System.UInt16",
                TypeI4 => "System.Int32",
                TypeU4 => "System.UInt32",
                TypeI8 => "System.Int64",
                TypeU8 => "System.UInt64",
                TypeR4 => "System.Single",
                TypeR8 => "System.Double",
                TypeString => "System.String",
                TypeObject => "System.Object",
                TypeNativeInt => "System.IntPtr",
                TypeNativeUInt => "System.UIntPtr",
                _ => string.Empty,
            };
        }

        if (kind == TypePointer)
        {
            var element = unchecked((nint)_codeRegistration.GetTypeData(registrationIndex));
            name = BuildTypeNameLocked(element) + "*";
        }
        else if (kind == TypeSzArray)
        {
            var element = unchecked((nint)_codeRegistration.GetTypeData(registrationIndex));
            name = BuildTypeNameLocked(element) + "[]";
        }
        else if (kind == TypeArray &&
                 TryReadPointer(_codeRegistration.GetTypeData(registrationIndex), out var elementType))
        {
            var rank = Math.Max(ReadByte(_codeRegistration.GetTypeData(registrationIndex) + 8), (byte)1);
            name = BuildTypeNameLocked(unchecked((nint)elementType)) +
                   "[" + new string(',', rank - 1) + "]";
        }

        if ((_codeRegistration.GetTypeBitfield(registrationIndex) & (1u << 30)) != 0)
        {
            name += "&";
        }

        return name;
    }

    private bool ClassHasReferencesLocked(nint classHandle, HashSet<nint> visiting)
    {
        if (classHandle == 0 || !visiting.Add(classHandle))
        {
            return false;
        }

        try
        {
            if (!IsClassValueType(classHandle))
            {
                return true;
            }

            if (_metadata is null ||
                _codeRegistration is null ||
                !_typeIndexByClass.TryGetValue(classHandle, out var typeIndex))
            {
                return false;
            }

            var start = _metadata.GetFieldStart(typeIndex);
            var count = _metadata.GetFieldCount(typeIndex);
            for (var i = 0; i < count && start >= 0; i++)
            {
                var globalIndex = start + i;
                var fieldType = _metadata.GetFieldTypeIndex(globalIndex);
                if ((_codeRegistration.GetTypeAttributes(fieldType) & FieldAttributeStatic) != 0)
                {
                    continue;
                }

                var kind = _codeRegistration.GetTypeKind(fieldType);
                if (IsReferenceTypeKind(kind))
                {
                    return true;
                }

                if (kind == TypeValueType)
                {
                    var nested = ResolveClassFromTypeLocked(unchecked((nint)_codeRegistration.GetTypePointer(
                        fieldType)));
                    if (ClassHasReferencesLocked(nested, visiting))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
        finally
        {
            visiting.Remove(classHandle);
        }
    }

    private uint GetTypeValueSizeLocked(int registrationIndex)
    {
        if (_codeRegistration is null || registrationIndex < 0)
        {
            return (uint)IntPtr.Size;
        }

        var kind = _codeRegistration.GetTypeKind(registrationIndex);
        return kind switch
        {
            TypeBoolean or TypeI1 or TypeU1 => 1,
            TypeChar or TypeI2 or TypeU2 => 2,
            TypeI4 or TypeU4 or TypeR4 => 4,
            TypeI8 or TypeU8 or TypeR8 or TypeNativeInt or TypeNativeUInt or TypePointer => 8,
            TypeValueType => GetValueTypePayloadSizeLocked(registrationIndex),
            _ => (uint)IntPtr.Size,
        };
    }

    private uint GetValueTypePayloadSizeLocked(int registrationIndex)
    {
        if (_codeRegistration is null)
        {
            return (uint)IntPtr.Size;
        }

        var definition = _codeRegistration.TryGetClassTypeDefinitionIndex(registrationIndex);
        var instanceSize = definition < 0 ? 0 : _codeRegistration.GetInstanceSize(definition);
        return instanceSize > ObjectHeaderSize
            ? instanceSize - ObjectHeaderSize
            : Math.Max(instanceSize, 1);
    }

    private nint GetPrimitiveClassLocked(uint kind)
    {
        var name = kind switch
        {
            TypeVoid => "Void",
            TypeBoolean => "Boolean",
            TypeChar => "Char",
            TypeI1 => "SByte",
            TypeU1 => "Byte",
            TypeI2 => "Int16",
            TypeU2 => "UInt16",
            TypeI4 => "Int32",
            TypeU4 => "UInt32",
            TypeI8 => "Int64",
            TypeU8 => "UInt64",
            TypeR4 => "Single",
            TypeR8 => "Double",
            TypeString => "String",
            TypeNativeInt => "IntPtr",
            TypeNativeUInt => "UIntPtr",
            TypeObject => "Object",
            _ => null,
        };
        return name is null ? 0 : GetClassFromName("System", name);
    }

    private unsafe nint GetOrCreatePropertyLocked(int propertyIndex, nint parent)
    {
        if (_metadata is null || (uint)propertyIndex >= (uint)_metadata.PropertyCount)
        {
            return 0;
        }

        if (_propertyByIndex.TryGetValue(propertyIndex, out var existing))
        {
            return existing;
        }

        var block = (byte*)AllocateBlock(0x28);
        *(nint*)(block + 0x00) = parent;
        *(nint*)(block + 0x08) = AllocateCString(_metadata.GetPropertyName(propertyIndex));
        var handle = (nint)block;
        _propertyByIndex[propertyIndex] = handle;
        _propertyIndexByHandle[handle] = propertyIndex;
        return handle;
    }

    private bool TryGetPropertyIndexLocked(nint propertyHandle, out int propertyIndex)
    {
        if (propertyHandle != 0 &&
            _propertyIndexByHandle.TryGetValue(propertyHandle, out propertyIndex))
        {
            return true;
        }

        propertyIndex = -1;
        return false;
    }

    private int GetTypeRegistrationIndexLocked(nint typeHandle) =>
        typeHandle == 0 || _codeRegistration is null
            ? -1
            : _codeRegistration.FindTypeRegistrationIndex(unchecked((ulong)typeHandle));

    private bool TryGetClassRegistrationIndexLocked(nint classHandle, out int registrationIndex)
    {
        registrationIndex = -1;
        if (_metadata is null ||
            _codeRegistration is null ||
            !_typeIndexByClass.TryGetValue(classHandle, out var typeDefinitionIndex))
        {
            return false;
        }

        registrationIndex = _metadata.GetTypeByValTypeIndex(typeDefinitionIndex);
        return registrationIndex >= 0;
    }

    private bool TryReadPointer(ulong address, out ulong value)
    {
        value = 0;
        if (_reader is null || address == 0)
        {
            return false;
        }

        Span<byte> buffer = stackalloc byte[sizeof(ulong)];
        if (!_reader.TryReadBytes(address, buffer))
        {
            return false;
        }

        value = BinaryPrimitives.ReadUInt64LittleEndian(buffer);
        return value != 0;
    }

    private bool TryReadInt32(ulong address, out int value)
    {
        value = -1;
        if (_reader is null || address == 0)
        {
            return false;
        }

        Span<byte> buffer = stackalloc byte[sizeof(int)];
        if (!_reader.TryReadBytes(address, buffer))
        {
            return false;
        }

        value = BinaryPrimitives.ReadInt32LittleEndian(buffer);
        return true;
    }

    private byte ReadByte(ulong address)
    {
        if (_reader is null || address == 0)
        {
            return 0;
        }

        Span<byte> buffer = stackalloc byte[1];
        return _reader.TryReadBytes(address, buffer) ? buffer[0] : (byte)0;
    }

    private static bool IsReferenceTypeKind(uint kind) =>
        kind is TypeString or TypeClass or TypeObject or TypeArray or TypeSzArray or
            TypeGenericInstance;

    private static uint GetNaturalAlignment(uint size) =>
        size >= 8 ? 8u : size >= 4 ? 4u : size >= 2 ? 2u : 1u;
}
