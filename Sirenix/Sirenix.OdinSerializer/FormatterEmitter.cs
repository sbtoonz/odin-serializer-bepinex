using System;
using System.Collections.Generic;
using System.Configuration.Assemblies;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using Sirenix.OdinSerializer.Utilities;
using UnityEngine;

namespace Sirenix.OdinSerializer;

public static class FormatterEmitter
{
	[EmittedFormatter]
	public abstract class AOTEmittedFormatter<T> : EasyBaseFormatter<T>
	{
	}

	public abstract class EmptyAOTEmittedFormatter<T> : AOTEmittedFormatter<T>
	{
		protected override void ReadDataEntry(ref T value, string entryName, EntryType entryType, IDataReader reader)
		{
			reader.SkipEntry();
		}

		protected override void WriteDataEntries(ref T value, IDataWriter writer)
		{
		}
	}

	public delegate void ReadDataEntryMethodDelegate<T>(ref T value, string entryName, EntryType entryType, IDataReader reader);

	public delegate void WriteDataEntriesMethodDelegate<T>(ref T value, IDataWriter writer);

	[EmittedFormatter]
	public sealed class RuntimeEmittedFormatter<T> : EasyBaseFormatter<T>
	{
		public readonly ReadDataEntryMethodDelegate<T> Read;

		public readonly WriteDataEntriesMethodDelegate<T> Write;

		public RuntimeEmittedFormatter(ReadDataEntryMethodDelegate<T> read, WriteDataEntriesMethodDelegate<T> write)
		{
			Read = read;
			Write = write;
		}

		protected override void ReadDataEntry(ref T value, string entryName, EntryType entryType, IDataReader reader)
		{
			Read(ref value, entryName, entryType, reader);
		}

		protected override void WriteDataEntries(ref T value, IDataWriter writer)
		{
			Write(ref value, writer);
		}
	}

	private static int helperFormatterNameId;

	public const string PRE_EMITTED_ASSEMBLY_NAME = "OdinSerializer.AOTGenerated";

	public const string RUNTIME_EMITTED_ASSEMBLY_NAME = "OdinSerializer.RuntimeEmitted";

	private static readonly object LOCK = new object();

	private static readonly DoubleLookupDictionary<ISerializationPolicy, Type, IFormatter> Formatters = new DoubleLookupDictionary<ISerializationPolicy, Type, IFormatter>();

	private static AssemblyBuilder runtimeEmittedAssembly;

	private static ModuleBuilder runtimeEmittedModule;

	public static IFormatter GetEmittedFormatter(Type type, ISerializationPolicy policy)
	{
		if (type == null)
		{
			throw new ArgumentNullException("type");
		}
		if (policy == null)
		{
			policy = SerializationPolicies.Strict;
		}
		IFormatter value = null;
		if (!Formatters.TryGetInnerValue(policy, type, out value))
		{
			lock (LOCK)
			{
				if (Formatters.TryGetInnerValue(policy, type, out value))
				{
					return value;
				}
				EnsureRuntimeAssembly();
				try
				{
					value = CreateGenericFormatter(type, runtimeEmittedModule, policy);
				}
				catch (Exception exception)
				{
					Debug.LogError("The following error occurred while emitting a formatter for the type " + type.Name);
					Debug.LogException(exception);
				}
				Formatters.AddInner(policy, type, value);
				return value;
			}
		}
		return value;
	}

	private static void EnsureRuntimeAssembly()
	{
		if (runtimeEmittedAssembly == null)
		{
			AssemblyName assemblyName = new AssemblyName("OdinSerializer.RuntimeEmitted");
			assemblyName.CultureInfo = CultureInfo.InvariantCulture;
			assemblyName.Flags = AssemblyNameFlags.None;
			assemblyName.ProcessorArchitecture = ProcessorArchitecture.MSIL;
			assemblyName.VersionCompatibility = AssemblyVersionCompatibility.SameDomain;
			runtimeEmittedAssembly = AppDomain.CurrentDomain.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
		}
		if (runtimeEmittedModule == null)
		{
			bool emitSymbolInfo = false;
			runtimeEmittedModule = runtimeEmittedAssembly.DefineDynamicModule("OdinSerializer.RuntimeEmitted", emitSymbolInfo);
		}
	}

	public static Type EmitAOTFormatter(Type formattedType, ModuleBuilder moduleBuilder, ISerializationPolicy policy)
	{
		Dictionary<string, MemberInfo> serializableMembersMap = FormatterUtilities.GetSerializableMembersMap(formattedType, policy);
		string name = moduleBuilder.Name + "." + formattedType.GetCompilableNiceFullName() + "__AOTFormatter";
		string helperTypeName = moduleBuilder.Name + "." + formattedType.GetCompilableNiceFullName() + "__FormatterHelper";
		if (serializableMembersMap.Count == 0)
		{
			return moduleBuilder.DefineType(name, TypeAttributes.Public | TypeAttributes.Sealed, typeof(EmptyAOTEmittedFormatter<>).MakeGenericType(formattedType)).CreateType();
		}
		BuildHelperType(moduleBuilder, helperTypeName, formattedType, serializableMembersMap, out var serializerReadMethods, out var serializerWriteMethods, out var serializerFields, out var dictField, out var memberNames);
		TypeBuilder typeBuilder = moduleBuilder.DefineType(name, TypeAttributes.Public | TypeAttributes.Sealed, typeof(AOTEmittedFormatter<>).MakeGenericType(formattedType));
		MethodInfo method = typeBuilder.BaseType.GetMethod("ReadDataEntry", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		MethodBuilder readMethod = typeBuilder.DefineMethod(method.Name, MethodAttributes.Family | MethodAttributes.Virtual, method.ReturnType, (from n in method.GetParameters()
			select n.ParameterType).ToArray());
		method.GetParameters().ForEach(delegate(ParameterInfo n)
		{
			readMethod.DefineParameter(n.Position, n.Attributes, n.Name);
		});
		EmitReadMethodContents(readMethod.GetILGenerator(), formattedType, dictField, serializerFields, memberNames, serializerReadMethods);
		MethodInfo method2 = typeBuilder.BaseType.GetMethod("WriteDataEntries", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		MethodBuilder dynamicWriteMethod = typeBuilder.DefineMethod(method2.Name, MethodAttributes.Family | MethodAttributes.Virtual, method2.ReturnType, (from n in method2.GetParameters()
			select n.ParameterType).ToArray());
		method2.GetParameters().ForEach(delegate(ParameterInfo n)
		{
			dynamicWriteMethod.DefineParameter(n.Position + 1, n.Attributes, n.Name);
		});
		EmitWriteMethodContents(dynamicWriteMethod.GetILGenerator(), formattedType, serializerFields, memberNames, serializerWriteMethods);
		Type result = typeBuilder.CreateType();
		((AssemblyBuilder)moduleBuilder.Assembly).SetCustomAttribute(new CustomAttributeBuilder(typeof(RegisterFormatterAttribute).GetConstructor(new Type[2]
		{
			typeof(Type),
			typeof(int)
		}), new object[2] { typeBuilder, -1 }));
		return result;
	}

	private static IFormatter CreateGenericFormatter(Type formattedType, ModuleBuilder moduleBuilder, ISerializationPolicy policy)
	{
		Dictionary<string, MemberInfo> serializableMembersMap = FormatterUtilities.GetSerializableMembersMap(formattedType, policy);
		if (serializableMembersMap.Count == 0)
		{
			return (IFormatter)Activator.CreateInstance(typeof(EmptyTypeFormatter<>).MakeGenericType(formattedType));
		}
		string helperTypeName = moduleBuilder.Name + "." + formattedType.GetCompilableNiceFullName() + "___" + formattedType.Assembly.GetName().Name + "___FormatterHelper___" + Interlocked.Increment(ref helperFormatterNameId);
		BuildHelperType(moduleBuilder, helperTypeName, formattedType, serializableMembersMap, out var serializerReadMethods, out var serializerWriteMethods, out var serializerFields, out var dictField, out var memberNames);
		Type type = typeof(RuntimeEmittedFormatter<>).MakeGenericType(formattedType);
		Type delegateType = typeof(ReadDataEntryMethodDelegate<>).MakeGenericType(formattedType);
		MethodInfo method = type.GetMethod("ReadDataEntry", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		DynamicMethod dynamicReadMethod = new DynamicMethod("Dynamic_" + formattedType.GetCompilableNiceFullName(), null, (from n in method.GetParameters()
			select n.ParameterType).ToArray(), restrictedSkipVisibility: true);
		method.GetParameters().ForEach(delegate(ParameterInfo n)
		{
			dynamicReadMethod.DefineParameter(n.Position, n.Attributes, n.Name);
		});
		EmitReadMethodContents(dynamicReadMethod.GetILGenerator(), formattedType, dictField, serializerFields, memberNames, serializerReadMethods);
		Delegate @delegate = dynamicReadMethod.CreateDelegate(delegateType);
		Type delegateType2 = typeof(WriteDataEntriesMethodDelegate<>).MakeGenericType(formattedType);
		MethodInfo method2 = type.GetMethod("WriteDataEntries", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		DynamicMethod dynamicWriteMethod = new DynamicMethod("Dynamic_Write_" + formattedType.GetCompilableNiceFullName(), null, (from n in method2.GetParameters()
			select n.ParameterType).ToArray(), restrictedSkipVisibility: true);
		method2.GetParameters().ForEach(delegate(ParameterInfo n)
		{
			dynamicWriteMethod.DefineParameter(n.Position + 1, n.Attributes, n.Name);
		});
		EmitWriteMethodContents(dynamicWriteMethod.GetILGenerator(), formattedType, serializerFields, memberNames, serializerWriteMethods);
		Delegate delegate2 = dynamicWriteMethod.CreateDelegate(delegateType2);
		return (IFormatter)Activator.CreateInstance(type, @delegate, delegate2);
	}

	private static Type BuildHelperType(ModuleBuilder moduleBuilder, string helperTypeName, Type formattedType, Dictionary<string, MemberInfo> serializableMembers, out Dictionary<Type, MethodInfo> serializerReadMethods, out Dictionary<Type, MethodInfo> serializerWriteMethods, out Dictionary<Type, FieldBuilder> serializerFields, out FieldBuilder dictField, out Dictionary<MemberInfo, List<string>> memberNames)
	{
		TypeBuilder typeBuilder = moduleBuilder.DefineType(helperTypeName, TypeAttributes.Public | TypeAttributes.Sealed);
		memberNames = new Dictionary<MemberInfo, List<string>>();
		foreach (KeyValuePair<string, MemberInfo> serializableMember in serializableMembers)
		{
			if (!memberNames.TryGetValue(serializableMember.Value, out var value))
			{
				value = new List<string>();
				memberNames.Add(serializableMember.Value, value);
			}
			value.Add(serializableMember.Key);
		}
		dictField = typeBuilder.DefineField("SwitchLookup", typeof(Dictionary<string, int>), FieldAttributes.Public | FieldAttributes.Static | FieldAttributes.InitOnly);
		List<Type> list = memberNames.Keys.Select((MemberInfo n) => FormatterUtilities.GetContainedType(n)).Distinct().ToList();
		serializerReadMethods = new Dictionary<Type, MethodInfo>(list.Count);
		serializerWriteMethods = new Dictionary<Type, MethodInfo>(list.Count);
		serializerFields = new Dictionary<Type, FieldBuilder>(list.Count);
		foreach (Type item in list)
		{
			string name = item.GetCompilableNiceFullName() + "__Serializer";
			int num = 1;
			while (serializerFields.Values.Any((FieldBuilder n) => n.Name == name))
			{
				num++;
				name = item.GetCompilableNiceFullName() + "__Serializer" + num;
			}
			Type type = typeof(Serializer<>).MakeGenericType(item);
			serializerReadMethods.Add(item, type.GetMethod("ReadValue", BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public));
			serializerWriteMethods.Add(item, type.GetMethod("WriteValue", BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public, null, new Type[3]
			{
				typeof(string),
				item,
				typeof(IDataWriter)
			}, null));
			serializerFields.Add(item, typeBuilder.DefineField(name, type, FieldAttributes.Public | FieldAttributes.Static | FieldAttributes.InitOnly));
		}
		MethodInfo method = typeof(Dictionary<string, int>).GetMethod("Add", BindingFlags.Instance | BindingFlags.Public);
		ConstructorInfo constructor = typeof(Dictionary<string, int>).GetConstructor(Type.EmptyTypes);
		MethodInfo method2 = typeof(Serializer).GetMethod("Get", BindingFlags.Static | BindingFlags.Public, null, new Type[1] { typeof(Type) }, null);
		MethodInfo method3 = typeof(Type).GetMethod("GetTypeFromHandle", BindingFlags.Static | BindingFlags.Public, null, new Type[1] { typeof(RuntimeTypeHandle) }, null);
		ILGenerator iLGenerator = typeBuilder.DefineTypeInitializer().GetILGenerator();
		iLGenerator.Emit(OpCodes.Newobj, constructor);
		int num2 = 0;
		foreach (KeyValuePair<MemberInfo, List<string>> memberName in memberNames)
		{
			foreach (string item2 in memberName.Value)
			{
				iLGenerator.Emit(OpCodes.Dup);
				iLGenerator.Emit(OpCodes.Ldstr, item2);
				iLGenerator.Emit(OpCodes.Ldc_I4, num2);
				iLGenerator.Emit(OpCodes.Call, method);
			}
			num2++;
		}
		iLGenerator.Emit(OpCodes.Stsfld, dictField);
		foreach (KeyValuePair<Type, FieldBuilder> serializerField in serializerFields)
		{
			iLGenerator.Emit(OpCodes.Ldtoken, serializerField.Key);
			iLGenerator.Emit(OpCodes.Call, method3);
			iLGenerator.Emit(OpCodes.Call, method2);
			iLGenerator.Emit(OpCodes.Stsfld, serializerField.Value);
		}
		iLGenerator.Emit(OpCodes.Ret);
		return typeBuilder.CreateType();
	}

	private static void EmitReadMethodContents(ILGenerator gen, Type formattedType, FieldInfo dictField, Dictionary<Type, FieldBuilder> serializerFields, Dictionary<MemberInfo, List<string>> memberNames, Dictionary<Type, MethodInfo> serializerReadMethods)
	{
		MethodInfo method = typeof(IDataReader).GetMethod("SkipEntry", BindingFlags.Instance | BindingFlags.Public);
		MethodInfo method2 = typeof(Dictionary<string, int>).GetMethod("TryGetValue", BindingFlags.Instance | BindingFlags.Public);
		LocalBuilder localBuilder = gen.DeclareLocal(typeof(int));
		Label label = gen.DefineLabel();
		Label label2 = gen.DefineLabel();
		Label label3 = gen.DefineLabel();
		Label[] array = memberNames.Select((KeyValuePair<MemberInfo, List<string>> n) => gen.DefineLabel()).ToArray();
		gen.Emit(OpCodes.Ldarg_1);
		gen.Emit(OpCodes.Ldnull);
		gen.Emit(OpCodes.Ceq);
		gen.Emit(OpCodes.Brtrue, label);
		gen.Emit(OpCodes.Ldsfld, dictField);
		gen.Emit(OpCodes.Ldarg_1);
		gen.Emit(OpCodes.Ldloca, (short)localBuilder.LocalIndex);
		gen.Emit(OpCodes.Callvirt, method2);
		gen.Emit(OpCodes.Brtrue, label2);
		gen.Emit(OpCodes.Br, label);
		gen.MarkLabel(label2);
		gen.Emit(OpCodes.Ldloc, localBuilder);
		gen.Emit(OpCodes.Switch, array);
		int num = 0;
		foreach (MemberInfo key in memberNames.Keys)
		{
			Type containedType = FormatterUtilities.GetContainedType(key);
			PropertyInfo propertyInfo = key as PropertyInfo;
			FieldInfo fieldInfo = key as FieldInfo;
			gen.MarkLabel(array[num]);
			gen.Emit(OpCodes.Ldarg_0);
			if (!formattedType.IsValueType)
			{
				gen.Emit(OpCodes.Ldind_Ref);
			}
			gen.Emit(OpCodes.Ldsfld, serializerFields[containedType]);
			gen.Emit(OpCodes.Ldarg, (short)3);
			gen.Emit(OpCodes.Callvirt, serializerReadMethods[containedType]);
			if (fieldInfo != null)
			{
				gen.Emit(OpCodes.Stfld, fieldInfo.DeAliasField());
			}
			else
			{
				if (!(propertyInfo != null))
				{
					throw new NotImplementedException();
				}
				gen.Emit(OpCodes.Callvirt, propertyInfo.DeAliasProperty().GetSetMethod(nonPublic: true));
			}
			gen.Emit(OpCodes.Br, label3);
			num++;
		}
		gen.MarkLabel(label);
		gen.Emit(OpCodes.Ldarg, (short)3);
		gen.Emit(OpCodes.Callvirt, method);
		gen.MarkLabel(label3);
		gen.Emit(OpCodes.Ret);
	}

	private static void EmitWriteMethodContents(ILGenerator gen, Type formattedType, Dictionary<Type, FieldBuilder> serializerFields, Dictionary<MemberInfo, List<string>> memberNames, Dictionary<Type, MethodInfo> serializerWriteMethods)
	{
		foreach (MemberInfo key in memberNames.Keys)
		{
			Type containedType = FormatterUtilities.GetContainedType(key);
			gen.Emit(OpCodes.Ldsfld, serializerFields[containedType]);
			gen.Emit(OpCodes.Ldstr, key.Name);
			if (key is FieldInfo)
			{
				FieldInfo fieldInfo = key as FieldInfo;
				if (formattedType.IsValueType)
				{
					gen.Emit(OpCodes.Ldarg_0);
					gen.Emit(OpCodes.Ldfld, fieldInfo.DeAliasField());
				}
				else
				{
					gen.Emit(OpCodes.Ldarg_0);
					gen.Emit(OpCodes.Ldind_Ref);
					gen.Emit(OpCodes.Ldfld, fieldInfo.DeAliasField());
				}
			}
			else
			{
				if (!(key is PropertyInfo))
				{
					throw new NotImplementedException();
				}
				PropertyInfo propertyInfo = key as PropertyInfo;
				if (formattedType.IsValueType)
				{
					gen.Emit(OpCodes.Ldarg_0);
					gen.Emit(OpCodes.Call, propertyInfo.DeAliasProperty().GetGetMethod(nonPublic: true));
				}
				else
				{
					gen.Emit(OpCodes.Ldarg_0);
					gen.Emit(OpCodes.Ldind_Ref);
					gen.Emit(OpCodes.Callvirt, propertyInfo.DeAliasProperty().GetGetMethod(nonPublic: true));
				}
			}
			gen.Emit(OpCodes.Ldarg_1);
			gen.Emit(OpCodes.Callvirt, serializerWriteMethods[containedType]);
		}
		gen.Emit(OpCodes.Ret);
	}
}
