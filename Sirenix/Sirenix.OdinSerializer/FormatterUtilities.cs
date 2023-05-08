using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using Sirenix.OdinSerializer.Utilities;
using UnityEngine;
using UnityEngine.Serialization;

namespace Sirenix.OdinSerializer;

public static class FormatterUtilities
{
	private static readonly DoubleLookupDictionary<ISerializationPolicy, Type, MemberInfo[]> MemberArrayCache;

	private static readonly DoubleLookupDictionary<ISerializationPolicy, Type, Dictionary<string, MemberInfo>> MemberMapCache;

	private static readonly object LOCK;

	private static readonly HashSet<Type> PrimitiveArrayTypes;

	private static readonly FieldInfo UnityObjectRuntimeErrorStringField;

	private const string UnityObjectRuntimeErrorString = "The variable nullValue of {0} has not been assigned.\nYou probably need to assign the nullValue variable of the {0} script in the inspector.";

	static FormatterUtilities()
	{
		MemberArrayCache = new DoubleLookupDictionary<ISerializationPolicy, Type, MemberInfo[]>();
		MemberMapCache = new DoubleLookupDictionary<ISerializationPolicy, Type, Dictionary<string, MemberInfo>>();
		LOCK = new object();
		PrimitiveArrayTypes = new HashSet<Type>(FastTypeComparer.Instance)
		{
			typeof(char),
			typeof(sbyte),
			typeof(short),
			typeof(int),
			typeof(long),
			typeof(byte),
			typeof(ushort),
			typeof(uint),
			typeof(ulong),
			typeof(decimal),
			typeof(bool),
			typeof(float),
			typeof(double),
			typeof(Guid)
		};
	}

	public static Dictionary<string, MemberInfo> GetSerializableMembersMap(Type type, ISerializationPolicy policy)
	{
		if (policy == null)
		{
			policy = SerializationPolicies.Strict;
		}
		lock (LOCK)
		{
			if (!MemberMapCache.TryGetInnerValue(policy, type, out var value))
			{
				value = FindSerializableMembersMap(type, policy);
				MemberMapCache.AddInner(policy, type, value);
				return value;
			}
			return value;
		}
	}

	public static MemberInfo[] GetSerializableMembers(Type type, ISerializationPolicy policy)
	{
		if (policy == null)
		{
			policy = SerializationPolicies.Strict;
		}
		lock (LOCK)
		{
			if (!MemberArrayCache.TryGetInnerValue(policy, type, out var value))
			{
				List<MemberInfo> list = new List<MemberInfo>();
				FindSerializableMembers(type, list, policy);
				value = list.ToArray();
				MemberArrayCache.AddInner(policy, type, value);
				return value;
			}
			return value;
		}
	}

	public static UnityEngine.Object CreateUnityNull(Type nullType, Type owningType)
	{
		if (nullType == null || owningType == null)
		{
			throw new ArgumentNullException();
		}
		if (!nullType.ImplementsOrInherits(typeof(UnityEngine.Object)))
		{
			throw new ArgumentException("Type " + nullType.Name + " is not a Unity object.");
		}
		if (!owningType.ImplementsOrInherits(typeof(UnityEngine.Object)))
		{
			throw new ArgumentException("Type " + owningType.Name + " is not a Unity object.");
		}
		UnityEngine.Object @object = (UnityEngine.Object)FormatterServices.GetUninitializedObject(nullType);
		if (UnityObjectRuntimeErrorStringField != null)
		{
			UnityObjectRuntimeErrorStringField.SetValue(@object, string.Format(CultureInfo.InvariantCulture, "The variable nullValue of {0} has not been assigned.\nYou probably need to assign the nullValue variable of the {0} script in the inspector.", owningType.Name));
		}
		return @object;
	}

	public static bool IsPrimitiveType(Type type)
	{
		if (!type.IsPrimitive && !type.IsEnum && !(type == typeof(decimal)) && !(type == typeof(string)))
		{
			return type == typeof(Guid);
		}
		return true;
	}

	public static bool IsPrimitiveArrayType(Type type)
	{
		return PrimitiveArrayTypes.Contains(type);
	}

	public static Type GetContainedType(MemberInfo member)
	{
		if (member is FieldInfo)
		{
			return (member as FieldInfo).FieldType;
		}
		if (member is PropertyInfo)
		{
			return (member as PropertyInfo).PropertyType;
		}
		throw new ArgumentException("Can't get the contained type of a " + member.GetType().Name);
	}

	public static object GetMemberValue(MemberInfo member, object obj)
	{
		if (member is FieldInfo)
		{
			return (member as FieldInfo).GetValue(obj);
		}
		if (member is PropertyInfo)
		{
			return (member as PropertyInfo).GetGetMethod(nonPublic: true).Invoke(obj, null);
		}
		throw new ArgumentException("Can't get the value of a " + member.GetType().Name);
	}

	public static void SetMemberValue(MemberInfo member, object obj, object value)
	{
		if (member is FieldInfo)
		{
			(member as FieldInfo).SetValue(obj, value);
			return;
		}
		if (member is PropertyInfo)
		{
			MethodInfo setMethod = (member as PropertyInfo).GetSetMethod(nonPublic: true);
			if (setMethod != null)
			{
				setMethod.Invoke(obj, new object[1] { value });
				return;
			}
			throw new ArgumentException("Property " + member.Name + " has no setter");
		}
		throw new ArgumentException("Can't set the value of a " + member.GetType().Name);
	}

	private static Dictionary<string, MemberInfo> FindSerializableMembersMap(Type type, ISerializationPolicy policy)
	{
		Dictionary<string, MemberInfo> dictionary = GetSerializableMembers(type, policy).ToDictionary((MemberInfo n) => n.Name, (MemberInfo n) => n);
		foreach (MemberInfo item in dictionary.Values.ToList())
		{
			foreach (FormerlySerializedAsAttribute attribute in item.GetAttributes<FormerlySerializedAsAttribute>())
			{
				if (!dictionary.ContainsKey(attribute.oldName))
				{
					dictionary.Add(attribute.oldName, item);
				}
			}
		}
		return dictionary;
	}

	private static void FindSerializableMembers(Type type, List<MemberInfo> members, ISerializationPolicy policy)
	{
		if (type.BaseType != typeof(object) && type.BaseType != null)
		{
			FindSerializableMembers(type.BaseType, members, policy);
		}
		foreach (MemberInfo member in from n in type.GetMembers(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
			where n is FieldInfo || n is PropertyInfo
			select n)
		{
			if (policy.ShouldSerializeMember(member))
			{
				bool flag = members.Any((MemberInfo n) => n.Name == member.Name);
				if (MemberIsPrivate(member) && flag)
				{
					members.Add(GetPrivateMemberAlias(member));
				}
				else if (flag)
				{
					members.Add(GetPrivateMemberAlias(member));
				}
				else
				{
					members.Add(member);
				}
			}
		}
	}

	public static MemberInfo GetPrivateMemberAlias(MemberInfo member, string prefixString = null, string separatorString = null)
	{
		if (member is FieldInfo)
		{
			if (separatorString != null)
			{
				return new MemberAliasFieldInfo(member as FieldInfo, prefixString ?? member.DeclaringType.Name, separatorString);
			}
			return new MemberAliasFieldInfo(member as FieldInfo, prefixString ?? member.DeclaringType.Name);
		}
		if (member is PropertyInfo)
		{
			if (separatorString != null)
			{
				return new MemberAliasPropertyInfo(member as PropertyInfo, prefixString ?? member.DeclaringType.Name, separatorString);
			}
			return new MemberAliasPropertyInfo(member as PropertyInfo, prefixString ?? member.DeclaringType.Name);
		}
		if (member is MethodInfo)
		{
			if (separatorString != null)
			{
				return new MemberAliasMethodInfo(member as MethodInfo, prefixString ?? member.DeclaringType.Name, separatorString);
			}
			return new MemberAliasMethodInfo(member as MethodInfo, prefixString ?? member.DeclaringType.Name);
		}
		throw new NotImplementedException();
	}

	private static bool MemberIsPrivate(MemberInfo member)
	{
		if (member is FieldInfo)
		{
			return (member as FieldInfo).IsPrivate;
		}
		if (member is PropertyInfo)
		{
			PropertyInfo obj = member as PropertyInfo;
			MethodInfo getMethod = obj.GetGetMethod();
			MethodInfo setMethod = obj.GetSetMethod();
			if (getMethod != null && setMethod != null && getMethod.IsPrivate)
			{
				return setMethod.IsPrivate;
			}
			return false;
		}
		if (member is MethodInfo)
		{
			return (member as MethodInfo).IsPrivate;
		}
		throw new NotImplementedException();
	}
}
