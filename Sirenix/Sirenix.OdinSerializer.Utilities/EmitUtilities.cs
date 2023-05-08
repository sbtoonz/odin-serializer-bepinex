using System;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace Sirenix.OdinSerializer.Utilities;

public static class EmitUtilities
{
	public delegate void InstanceRefMethodCaller<InstanceType>(ref InstanceType instance);

	public delegate void InstanceRefMethodCaller<InstanceType, TArg1>(ref InstanceType instance, TArg1 arg1);

	private static Assembly EngineAssembly = typeof(UnityEngine.Object).Assembly;

	public static bool CanEmit => true;

	private static bool EmitIsIllegalForMember(MemberInfo member)
	{
		if (member.DeclaringType != null)
		{
			return member.DeclaringType.Assembly == EngineAssembly;
		}
		return false;
	}

	public static Func<FieldType> CreateStaticFieldGetter<FieldType>(FieldInfo fieldInfo)
	{
		if (fieldInfo == null)
		{
			throw new ArgumentNullException("fieldInfo");
		}
		if (!fieldInfo.IsStatic)
		{
			throw new ArgumentException("Field must be static.");
		}
		fieldInfo = fieldInfo.DeAliasField();
		if (fieldInfo.IsLiteral)
		{
			FieldType value = (FieldType)fieldInfo.GetValue(null);
			return () => value;
		}
		if (EmitIsIllegalForMember(fieldInfo))
		{
			return () => (FieldType)fieldInfo.GetValue(null);
		}
		DynamicMethod dynamicMethod = new DynamicMethod(fieldInfo.ReflectedType.FullName + ".get_" + fieldInfo.Name, typeof(FieldType), new Type[0], restrictedSkipVisibility: true);
		ILGenerator iLGenerator = dynamicMethod.GetILGenerator();
		iLGenerator.Emit(OpCodes.Ldsfld, fieldInfo);
		iLGenerator.Emit(OpCodes.Ret);
		return (Func<FieldType>)dynamicMethod.CreateDelegate(typeof(Func<FieldType>));
	}

	public static Func<object> CreateWeakStaticFieldGetter(FieldInfo fieldInfo)
	{
		if (fieldInfo == null)
		{
			throw new ArgumentNullException("fieldInfo");
		}
		if (!fieldInfo.IsStatic)
		{
			throw new ArgumentException("Field must be static.");
		}
		fieldInfo = fieldInfo.DeAliasField();
		if (EmitIsIllegalForMember(fieldInfo))
		{
			return () => fieldInfo.GetValue(null);
		}
		DynamicMethod dynamicMethod = new DynamicMethod(fieldInfo.ReflectedType.FullName + ".get_" + fieldInfo.Name, typeof(object), new Type[0], restrictedSkipVisibility: true);
		ILGenerator iLGenerator = dynamicMethod.GetILGenerator();
		iLGenerator.Emit(OpCodes.Ldsfld, fieldInfo);
		if (fieldInfo.FieldType.IsValueType)
		{
			iLGenerator.Emit(OpCodes.Box, fieldInfo.FieldType);
		}
		iLGenerator.Emit(OpCodes.Ret);
		return (Func<object>)dynamicMethod.CreateDelegate(typeof(Func<object>));
	}

	public static Action<FieldType> CreateStaticFieldSetter<FieldType>(FieldInfo fieldInfo)
	{
		if (fieldInfo == null)
		{
			throw new ArgumentNullException("fieldInfo");
		}
		if (!fieldInfo.IsStatic)
		{
			throw new ArgumentException("Field must be static.");
		}
		fieldInfo = fieldInfo.DeAliasField();
		if (fieldInfo.IsLiteral)
		{
			throw new ArgumentException("Field cannot be constant.");
		}
		if (EmitIsIllegalForMember(fieldInfo))
		{
			return delegate(FieldType value)
			{
				fieldInfo.SetValue(null, value);
			};
		}
		DynamicMethod dynamicMethod = new DynamicMethod(fieldInfo.ReflectedType.FullName + ".set_" + fieldInfo.Name, null, new Type[1] { typeof(FieldType) }, restrictedSkipVisibility: true);
		ILGenerator iLGenerator = dynamicMethod.GetILGenerator();
		iLGenerator.Emit(OpCodes.Ldarg_0);
		iLGenerator.Emit(OpCodes.Stsfld, fieldInfo);
		iLGenerator.Emit(OpCodes.Ret);
		return (Action<FieldType>)dynamicMethod.CreateDelegate(typeof(Action<FieldType>));
	}

	public static Action<object> CreateWeakStaticFieldSetter(FieldInfo fieldInfo)
	{
		if (fieldInfo == null)
		{
			throw new ArgumentNullException("fieldInfo");
		}
		if (!fieldInfo.IsStatic)
		{
			throw new ArgumentException("Field must be static.");
		}
		fieldInfo = fieldInfo.DeAliasField();
		if (EmitIsIllegalForMember(fieldInfo))
		{
			return delegate(object value)
			{
				fieldInfo.SetValue(null, value);
			};
		}
		DynamicMethod dynamicMethod = new DynamicMethod(fieldInfo.ReflectedType.FullName + ".set_" + fieldInfo.Name, null, new Type[1] { typeof(object) }, restrictedSkipVisibility: true);
		ILGenerator iLGenerator = dynamicMethod.GetILGenerator();
		iLGenerator.Emit(OpCodes.Ldarg_0);
		if (fieldInfo.FieldType.IsValueType)
		{
			iLGenerator.Emit(OpCodes.Unbox_Any, fieldInfo.FieldType);
		}
		else
		{
			iLGenerator.Emit(OpCodes.Castclass, fieldInfo.FieldType);
		}
		iLGenerator.Emit(OpCodes.Stsfld, fieldInfo);
		iLGenerator.Emit(OpCodes.Ret);
		return (Action<object>)dynamicMethod.CreateDelegate(typeof(Action<object>));
	}

	public static ValueGetter<InstanceType, FieldType> CreateInstanceFieldGetter<InstanceType, FieldType>(FieldInfo fieldInfo)
	{
		if (fieldInfo == null)
		{
			throw new ArgumentNullException("fieldInfo");
		}
		if (fieldInfo.IsStatic)
		{
			throw new ArgumentException("Field cannot be static.");
		}
		fieldInfo = fieldInfo.DeAliasField();
		if (EmitIsIllegalForMember(fieldInfo))
		{
			return delegate(ref InstanceType classInstance)
			{
				return (FieldType)fieldInfo.GetValue(classInstance);
			};
		}
		DynamicMethod dynamicMethod = new DynamicMethod(fieldInfo.ReflectedType.FullName + ".get_" + fieldInfo.Name, typeof(FieldType), new Type[1] { typeof(InstanceType).MakeByRefType() }, restrictedSkipVisibility: true);
		ILGenerator iLGenerator = dynamicMethod.GetILGenerator();
		if (typeof(InstanceType).IsValueType)
		{
			iLGenerator.Emit(OpCodes.Ldarg_0);
			iLGenerator.Emit(OpCodes.Ldfld, fieldInfo);
		}
		else
		{
			iLGenerator.Emit(OpCodes.Ldarg_0);
			iLGenerator.Emit(OpCodes.Ldind_Ref);
			iLGenerator.Emit(OpCodes.Ldfld, fieldInfo);
		}
		iLGenerator.Emit(OpCodes.Ret);
		return (ValueGetter<InstanceType, FieldType>)dynamicMethod.CreateDelegate(typeof(ValueGetter<InstanceType, FieldType>));
	}

	public static WeakValueGetter<FieldType> CreateWeakInstanceFieldGetter<FieldType>(Type instanceType, FieldInfo fieldInfo)
	{
		if (fieldInfo == null)
		{
			throw new ArgumentNullException("fieldInfo");
		}
		if (instanceType == null)
		{
			throw new ArgumentNullException("instanceType");
		}
		if (fieldInfo.IsStatic)
		{
			throw new ArgumentException("Field cannot be static.");
		}
		fieldInfo = fieldInfo.DeAliasField();
		if (EmitIsIllegalForMember(fieldInfo))
		{
			return delegate(ref object classInstance)
			{
				return (FieldType)fieldInfo.GetValue(classInstance);
			};
		}
		DynamicMethod dynamicMethod = new DynamicMethod(fieldInfo.ReflectedType.FullName + ".get_" + fieldInfo.Name, typeof(FieldType), new Type[1] { typeof(object).MakeByRefType() }, restrictedSkipVisibility: true);
		ILGenerator iLGenerator = dynamicMethod.GetILGenerator();
		if (instanceType.IsValueType)
		{
			iLGenerator.Emit(OpCodes.Ldarg_0);
			iLGenerator.Emit(OpCodes.Ldind_Ref);
			iLGenerator.Emit(OpCodes.Unbox_Any, instanceType);
			iLGenerator.Emit(OpCodes.Ldfld, fieldInfo);
		}
		else
		{
			iLGenerator.Emit(OpCodes.Ldarg_0);
			iLGenerator.Emit(OpCodes.Ldind_Ref);
			iLGenerator.Emit(OpCodes.Castclass, instanceType);
			iLGenerator.Emit(OpCodes.Ldfld, fieldInfo);
		}
		iLGenerator.Emit(OpCodes.Ret);
		return (WeakValueGetter<FieldType>)dynamicMethod.CreateDelegate(typeof(WeakValueGetter<FieldType>));
	}

	public static WeakValueGetter CreateWeakInstanceFieldGetter(Type instanceType, FieldInfo fieldInfo)
	{
		if (fieldInfo == null)
		{
			throw new ArgumentNullException("fieldInfo");
		}
		if (instanceType == null)
		{
			throw new ArgumentNullException("instanceType");
		}
		if (fieldInfo.IsStatic)
		{
			throw new ArgumentException("Field cannot be static.");
		}
		fieldInfo = fieldInfo.DeAliasField();
		if (EmitIsIllegalForMember(fieldInfo))
		{
			return delegate(ref object classInstance)
			{
				return fieldInfo.GetValue(classInstance);
			};
		}
		DynamicMethod dynamicMethod = new DynamicMethod(fieldInfo.ReflectedType.FullName + ".get_" + fieldInfo.Name, typeof(object), new Type[1] { typeof(object).MakeByRefType() }, restrictedSkipVisibility: true);
		ILGenerator iLGenerator = dynamicMethod.GetILGenerator();
		if (instanceType.IsValueType)
		{
			iLGenerator.Emit(OpCodes.Ldarg_0);
			iLGenerator.Emit(OpCodes.Ldind_Ref);
			iLGenerator.Emit(OpCodes.Unbox_Any, instanceType);
			iLGenerator.Emit(OpCodes.Ldfld, fieldInfo);
			if (fieldInfo.FieldType.IsValueType)
			{
				iLGenerator.Emit(OpCodes.Box, fieldInfo.FieldType);
			}
		}
		else
		{
			iLGenerator.Emit(OpCodes.Ldarg_0);
			iLGenerator.Emit(OpCodes.Ldind_Ref);
			iLGenerator.Emit(OpCodes.Castclass, instanceType);
			iLGenerator.Emit(OpCodes.Ldfld, fieldInfo);
			if (fieldInfo.FieldType.IsValueType)
			{
				iLGenerator.Emit(OpCodes.Box, fieldInfo.FieldType);
			}
		}
		iLGenerator.Emit(OpCodes.Ret);
		return (WeakValueGetter)dynamicMethod.CreateDelegate(typeof(WeakValueGetter));
	}

	public static ValueSetter<InstanceType, FieldType> CreateInstanceFieldSetter<InstanceType, FieldType>(FieldInfo fieldInfo)
	{
		if (fieldInfo == null)
		{
			throw new ArgumentNullException("fieldInfo");
		}
		if (fieldInfo.IsStatic)
		{
			throw new ArgumentException("Field cannot be static.");
		}
		fieldInfo = fieldInfo.DeAliasField();
		if (EmitIsIllegalForMember(fieldInfo))
		{
			return delegate(ref InstanceType classInstance, FieldType value)
			{
				if (typeof(InstanceType).IsValueType)
				{
					object obj = classInstance;
					fieldInfo.SetValue(obj, value);
					classInstance = (InstanceType)obj;
				}
				else
				{
					fieldInfo.SetValue(classInstance, value);
				}
			};
		}
		DynamicMethod dynamicMethod = new DynamicMethod(fieldInfo.ReflectedType.FullName + ".set_" + fieldInfo.Name, null, new Type[2]
		{
			typeof(InstanceType).MakeByRefType(),
			typeof(FieldType)
		}, restrictedSkipVisibility: true);
		ILGenerator iLGenerator = dynamicMethod.GetILGenerator();
		if (typeof(InstanceType).IsValueType)
		{
			iLGenerator.Emit(OpCodes.Ldarg_0);
			iLGenerator.Emit(OpCodes.Ldarg_1);
			iLGenerator.Emit(OpCodes.Stfld, fieldInfo);
		}
		else
		{
			iLGenerator.Emit(OpCodes.Ldarg_0);
			iLGenerator.Emit(OpCodes.Ldind_Ref);
			iLGenerator.Emit(OpCodes.Ldarg_1);
			iLGenerator.Emit(OpCodes.Stfld, fieldInfo);
		}
		iLGenerator.Emit(OpCodes.Ret);
		return (ValueSetter<InstanceType, FieldType>)dynamicMethod.CreateDelegate(typeof(ValueSetter<InstanceType, FieldType>));
	}

	public static WeakValueSetter<FieldType> CreateWeakInstanceFieldSetter<FieldType>(Type instanceType, FieldInfo fieldInfo)
	{
		if (fieldInfo == null)
		{
			throw new ArgumentNullException("fieldInfo");
		}
		if (instanceType == null)
		{
			throw new ArgumentNullException("instanceType");
		}
		if (fieldInfo.IsStatic)
		{
			throw new ArgumentException("Field cannot be static.");
		}
		fieldInfo = fieldInfo.DeAliasField();
		if (EmitIsIllegalForMember(fieldInfo))
		{
			return delegate(ref object classInstance, FieldType value)
			{
				fieldInfo.SetValue(classInstance, value);
			};
		}
		DynamicMethod dynamicMethod = new DynamicMethod(fieldInfo.ReflectedType.FullName + ".set_" + fieldInfo.Name, null, new Type[2]
		{
			typeof(object).MakeByRefType(),
			typeof(FieldType)
		}, restrictedSkipVisibility: true);
		ILGenerator iLGenerator = dynamicMethod.GetILGenerator();
		if (instanceType.IsValueType)
		{
			LocalBuilder local = iLGenerator.DeclareLocal(instanceType);
			iLGenerator.Emit(OpCodes.Ldarg_0);
			iLGenerator.Emit(OpCodes.Ldind_Ref);
			iLGenerator.Emit(OpCodes.Unbox_Any, instanceType);
			iLGenerator.Emit(OpCodes.Stloc, local);
			iLGenerator.Emit(OpCodes.Ldloca_S, local);
			iLGenerator.Emit(OpCodes.Ldarg_1);
			iLGenerator.Emit(OpCodes.Stfld, fieldInfo);
			iLGenerator.Emit(OpCodes.Ldarg_0);
			iLGenerator.Emit(OpCodes.Ldloc, local);
			iLGenerator.Emit(OpCodes.Box, instanceType);
			iLGenerator.Emit(OpCodes.Stind_Ref);
		}
		else
		{
			iLGenerator.Emit(OpCodes.Ldarg_0);
			iLGenerator.Emit(OpCodes.Ldind_Ref);
			iLGenerator.Emit(OpCodes.Castclass, instanceType);
			iLGenerator.Emit(OpCodes.Ldarg_1);
			iLGenerator.Emit(OpCodes.Stfld, fieldInfo);
		}
		iLGenerator.Emit(OpCodes.Ret);
		return (WeakValueSetter<FieldType>)dynamicMethod.CreateDelegate(typeof(WeakValueSetter<FieldType>));
	}

	public static WeakValueSetter CreateWeakInstanceFieldSetter(Type instanceType, FieldInfo fieldInfo)
	{
		if (fieldInfo == null)
		{
			throw new ArgumentNullException("fieldInfo");
		}
		if (instanceType == null)
		{
			throw new ArgumentNullException("instanceType");
		}
		if (fieldInfo.IsStatic)
		{
			throw new ArgumentException("Field cannot be static.");
		}
		fieldInfo = fieldInfo.DeAliasField();
		if (EmitIsIllegalForMember(fieldInfo))
		{
			return delegate(ref object classInstance, object value)
			{
				fieldInfo.SetValue(classInstance, value);
			};
		}
		DynamicMethod dynamicMethod = new DynamicMethod(fieldInfo.ReflectedType.FullName + ".set_" + fieldInfo.Name, null, new Type[2]
		{
			typeof(object).MakeByRefType(),
			typeof(object)
		}, restrictedSkipVisibility: true);
		ILGenerator iLGenerator = dynamicMethod.GetILGenerator();
		if (instanceType.IsValueType)
		{
			LocalBuilder local = iLGenerator.DeclareLocal(instanceType);
			iLGenerator.Emit(OpCodes.Ldarg_0);
			iLGenerator.Emit(OpCodes.Ldind_Ref);
			iLGenerator.Emit(OpCodes.Unbox_Any, instanceType);
			iLGenerator.Emit(OpCodes.Stloc, local);
			iLGenerator.Emit(OpCodes.Ldloca_S, local);
			iLGenerator.Emit(OpCodes.Ldarg_1);
			if (fieldInfo.FieldType.IsValueType)
			{
				iLGenerator.Emit(OpCodes.Unbox_Any, fieldInfo.FieldType);
			}
			else
			{
				iLGenerator.Emit(OpCodes.Castclass, fieldInfo.FieldType);
			}
			iLGenerator.Emit(OpCodes.Stfld, fieldInfo);
			iLGenerator.Emit(OpCodes.Ldarg_0);
			iLGenerator.Emit(OpCodes.Ldloc, local);
			iLGenerator.Emit(OpCodes.Box, instanceType);
			iLGenerator.Emit(OpCodes.Stind_Ref);
		}
		else
		{
			iLGenerator.Emit(OpCodes.Ldarg_0);
			iLGenerator.Emit(OpCodes.Ldind_Ref);
			iLGenerator.Emit(OpCodes.Castclass, instanceType);
			iLGenerator.Emit(OpCodes.Ldarg_1);
			if (fieldInfo.FieldType.IsValueType)
			{
				iLGenerator.Emit(OpCodes.Unbox_Any, fieldInfo.FieldType);
			}
			else
			{
				iLGenerator.Emit(OpCodes.Castclass, fieldInfo.FieldType);
			}
			iLGenerator.Emit(OpCodes.Stfld, fieldInfo);
		}
		iLGenerator.Emit(OpCodes.Ret);
		return (WeakValueSetter)dynamicMethod.CreateDelegate(typeof(WeakValueSetter));
	}

	public static WeakValueGetter CreateWeakInstancePropertyGetter(Type instanceType, PropertyInfo propertyInfo)
	{
		if (propertyInfo == null)
		{
			throw new ArgumentNullException("propertyInfo");
		}
		if (instanceType == null)
		{
			throw new ArgumentNullException("instanceType");
		}
		propertyInfo = propertyInfo.DeAliasProperty();
		if (propertyInfo.GetIndexParameters().Length != 0)
		{
			throw new ArgumentException("Property must not have any index parameters");
		}
		MethodInfo getMethod = propertyInfo.GetGetMethod(nonPublic: true);
		if (getMethod == null)
		{
			throw new ArgumentException("Property must have a getter.");
		}
		if (getMethod.IsStatic)
		{
			throw new ArgumentException("Property cannot be static.");
		}
		if (EmitIsIllegalForMember(propertyInfo))
		{
			return delegate(ref object classInstance)
			{
				return propertyInfo.GetValue(classInstance, null);
			};
		}
		DynamicMethod dynamicMethod = new DynamicMethod(propertyInfo.ReflectedType.FullName + ".get_" + propertyInfo.Name, typeof(object), new Type[1] { typeof(object).MakeByRefType() }, restrictedSkipVisibility: true);
		ILGenerator iLGenerator = dynamicMethod.GetILGenerator();
		if (instanceType.IsValueType)
		{
			iLGenerator.Emit(OpCodes.Ldarg_0);
			iLGenerator.Emit(OpCodes.Ldind_Ref);
			iLGenerator.Emit(OpCodes.Unbox_Any, instanceType);
			if (getMethod.IsVirtual || getMethod.IsAbstract)
			{
				iLGenerator.Emit(OpCodes.Callvirt, getMethod);
			}
			else
			{
				iLGenerator.Emit(OpCodes.Call, getMethod);
			}
			if (propertyInfo.PropertyType.IsValueType)
			{
				iLGenerator.Emit(OpCodes.Box, propertyInfo.PropertyType);
			}
		}
		else
		{
			iLGenerator.Emit(OpCodes.Ldarg_0);
			iLGenerator.Emit(OpCodes.Ldind_Ref);
			iLGenerator.Emit(OpCodes.Castclass, instanceType);
			if (getMethod.IsVirtual || getMethod.IsAbstract)
			{
				iLGenerator.Emit(OpCodes.Callvirt, getMethod);
			}
			else
			{
				iLGenerator.Emit(OpCodes.Call, getMethod);
			}
			if (propertyInfo.PropertyType.IsValueType)
			{
				iLGenerator.Emit(OpCodes.Box, propertyInfo.PropertyType);
			}
		}
		iLGenerator.Emit(OpCodes.Ret);
		return (WeakValueGetter)dynamicMethod.CreateDelegate(typeof(WeakValueGetter));
	}

	public static WeakValueSetter CreateWeakInstancePropertySetter(Type instanceType, PropertyInfo propertyInfo)
	{
		if (propertyInfo == null)
		{
			throw new ArgumentNullException("propertyInfo");
		}
		if (instanceType == null)
		{
			throw new ArgumentNullException("instanceType");
		}
		propertyInfo = propertyInfo.DeAliasProperty();
		if (propertyInfo.GetIndexParameters().Length != 0)
		{
			throw new ArgumentException("Property must not have any index parameters");
		}
		MethodInfo setMethod = propertyInfo.GetSetMethod(nonPublic: true);
		if (setMethod.IsStatic)
		{
			throw new ArgumentException("Property cannot be static.");
		}
		if (EmitIsIllegalForMember(propertyInfo))
		{
			return delegate(ref object classInstance, object value)
			{
				propertyInfo.SetValue(classInstance, value, null);
			};
		}
		DynamicMethod dynamicMethod = new DynamicMethod(propertyInfo.ReflectedType.FullName + ".set_" + propertyInfo.Name, null, new Type[2]
		{
			typeof(object).MakeByRefType(),
			typeof(object)
		}, restrictedSkipVisibility: true);
		ILGenerator iLGenerator = dynamicMethod.GetILGenerator();
		if (instanceType.IsValueType)
		{
			LocalBuilder local = iLGenerator.DeclareLocal(instanceType);
			iLGenerator.Emit(OpCodes.Ldarg_0);
			iLGenerator.Emit(OpCodes.Ldind_Ref);
			iLGenerator.Emit(OpCodes.Unbox_Any, instanceType);
			iLGenerator.Emit(OpCodes.Stloc, local);
			iLGenerator.Emit(OpCodes.Ldloca_S, local);
			iLGenerator.Emit(OpCodes.Ldarg_1);
			if (propertyInfo.PropertyType.IsValueType)
			{
				iLGenerator.Emit(OpCodes.Unbox_Any, propertyInfo.PropertyType);
			}
			else
			{
				iLGenerator.Emit(OpCodes.Castclass, propertyInfo.PropertyType);
			}
			if (setMethod.IsVirtual || setMethod.IsAbstract)
			{
				iLGenerator.Emit(OpCodes.Callvirt, setMethod);
			}
			else
			{
				iLGenerator.Emit(OpCodes.Call, setMethod);
			}
			iLGenerator.Emit(OpCodes.Ldarg_0);
			iLGenerator.Emit(OpCodes.Ldloc, local);
			iLGenerator.Emit(OpCodes.Box, instanceType);
			iLGenerator.Emit(OpCodes.Stind_Ref);
		}
		else
		{
			iLGenerator.Emit(OpCodes.Ldarg_0);
			iLGenerator.Emit(OpCodes.Ldind_Ref);
			iLGenerator.Emit(OpCodes.Castclass, instanceType);
			iLGenerator.Emit(OpCodes.Ldarg_1);
			if (propertyInfo.PropertyType.IsValueType)
			{
				iLGenerator.Emit(OpCodes.Unbox_Any, propertyInfo.PropertyType);
			}
			else
			{
				iLGenerator.Emit(OpCodes.Castclass, propertyInfo.PropertyType);
			}
			if (setMethod.IsVirtual || setMethod.IsAbstract)
			{
				iLGenerator.Emit(OpCodes.Callvirt, setMethod);
			}
			else
			{
				iLGenerator.Emit(OpCodes.Call, setMethod);
			}
		}
		iLGenerator.Emit(OpCodes.Ret);
		return (WeakValueSetter)dynamicMethod.CreateDelegate(typeof(WeakValueSetter));
	}

	public static Action<PropType> CreateStaticPropertySetter<PropType>(PropertyInfo propertyInfo)
	{
		if (propertyInfo == null)
		{
			throw new ArgumentNullException("fieldInfo");
		}
		propertyInfo = propertyInfo.DeAliasProperty();
		if (propertyInfo.GetIndexParameters().Length != 0)
		{
			throw new ArgumentException("Property must not have any index parameters");
		}
		MethodInfo setMethod = propertyInfo.GetSetMethod(nonPublic: true);
		if (setMethod == null)
		{
			throw new ArgumentException("Property must have a set method.");
		}
		if (!setMethod.IsStatic)
		{
			throw new ArgumentException("Property must be static.");
		}
		if (EmitIsIllegalForMember(propertyInfo))
		{
			return delegate(PropType value)
			{
				propertyInfo.SetValue(null, value, null);
			};
		}
		DynamicMethod dynamicMethod = new DynamicMethod(propertyInfo.ReflectedType.FullName + ".set_" + propertyInfo.Name, null, new Type[1] { typeof(PropType) }, restrictedSkipVisibility: true);
		ILGenerator iLGenerator = dynamicMethod.GetILGenerator();
		iLGenerator.Emit(OpCodes.Ldarg_0);
		iLGenerator.Emit(OpCodes.Call, setMethod);
		iLGenerator.Emit(OpCodes.Ret);
		return (Action<PropType>)dynamicMethod.CreateDelegate(typeof(Action<PropType>));
	}

	public static Func<PropType> CreateStaticPropertyGetter<PropType>(PropertyInfo propertyInfo)
	{
		if (propertyInfo == null)
		{
			throw new ArgumentNullException("propertyInfo");
		}
		propertyInfo = propertyInfo.DeAliasProperty();
		if (propertyInfo.GetIndexParameters().Length != 0)
		{
			throw new ArgumentException("Property must not have any index parameters");
		}
		MethodInfo getMethod = propertyInfo.GetGetMethod(nonPublic: true);
		if (getMethod == null)
		{
			throw new ArgumentException("Property must have a get method.");
		}
		if (!getMethod.IsStatic)
		{
			throw new ArgumentException("Property must be static.");
		}
		if (EmitIsIllegalForMember(propertyInfo))
		{
			return () => (PropType)propertyInfo.GetValue(null, null);
		}
		DynamicMethod dynamicMethod = new DynamicMethod(propertyInfo.ReflectedType.FullName + ".get_" + propertyInfo.Name, typeof(PropType), new Type[0], restrictedSkipVisibility: true);
		ILGenerator iLGenerator = dynamicMethod.GetILGenerator();
		iLGenerator.Emit(OpCodes.Call, getMethod);
		Type returnType = propertyInfo.GetReturnType();
		if (returnType.IsValueType && !typeof(PropType).IsValueType)
		{
			iLGenerator.Emit(OpCodes.Box, returnType);
		}
		iLGenerator.Emit(OpCodes.Ret);
		return (Func<PropType>)dynamicMethod.CreateDelegate(typeof(Func<PropType>));
	}

	public static ValueSetter<InstanceType, PropType> CreateInstancePropertySetter<InstanceType, PropType>(PropertyInfo propertyInfo)
	{
		if (propertyInfo == null)
		{
			throw new ArgumentNullException("fieldInfo");
		}
		propertyInfo = propertyInfo.DeAliasProperty();
		if (propertyInfo.GetIndexParameters().Length != 0)
		{
			throw new ArgumentException("Property must not have any index parameters");
		}
		MethodInfo setMethod = propertyInfo.GetSetMethod(nonPublic: true);
		if (setMethod == null)
		{
			throw new ArgumentException("Property must have a set method.");
		}
		if (setMethod.IsStatic)
		{
			throw new ArgumentException("Property cannot be static.");
		}
		if (EmitIsIllegalForMember(propertyInfo))
		{
			return delegate(ref InstanceType classInstance, PropType value)
			{
				if (typeof(InstanceType).IsValueType)
				{
					object obj = classInstance;
					propertyInfo.SetValue(obj, value, null);
					classInstance = (InstanceType)obj;
				}
				else
				{
					propertyInfo.SetValue(classInstance, value, null);
				}
			};
		}
		DynamicMethod dynamicMethod = new DynamicMethod(propertyInfo.ReflectedType.FullName + ".set_" + propertyInfo.Name, null, new Type[2]
		{
			typeof(InstanceType).MakeByRefType(),
			typeof(PropType)
		}, restrictedSkipVisibility: true);
		ILGenerator iLGenerator = dynamicMethod.GetILGenerator();
		if (typeof(InstanceType).IsValueType)
		{
			iLGenerator.Emit(OpCodes.Ldarg_0);
			iLGenerator.Emit(OpCodes.Ldarg_1);
			iLGenerator.Emit(OpCodes.Callvirt, setMethod);
		}
		else
		{
			iLGenerator.Emit(OpCodes.Ldarg_0);
			iLGenerator.Emit(OpCodes.Ldind_Ref);
			iLGenerator.Emit(OpCodes.Ldarg_1);
			iLGenerator.Emit(OpCodes.Callvirt, setMethod);
		}
		iLGenerator.Emit(OpCodes.Ret);
		return (ValueSetter<InstanceType, PropType>)dynamicMethod.CreateDelegate(typeof(ValueSetter<InstanceType, PropType>));
	}

	public static ValueGetter<InstanceType, PropType> CreateInstancePropertyGetter<InstanceType, PropType>(PropertyInfo propertyInfo)
	{
		if (propertyInfo == null)
		{
			throw new ArgumentNullException("propertyInfo");
		}
		propertyInfo = propertyInfo.DeAliasProperty();
		if (propertyInfo.GetIndexParameters().Length != 0)
		{
			throw new ArgumentException("Property must not have any index parameters");
		}
		MethodInfo getMethod = propertyInfo.GetGetMethod(nonPublic: true);
		if (getMethod == null)
		{
			throw new ArgumentException("Property must have a get method.");
		}
		if (getMethod.IsStatic)
		{
			throw new ArgumentException("Property cannot be static.");
		}
		if (EmitIsIllegalForMember(propertyInfo))
		{
			return delegate(ref InstanceType classInstance)
			{
				return (PropType)propertyInfo.GetValue(classInstance, null);
			};
		}
		DynamicMethod dynamicMethod = new DynamicMethod(propertyInfo.ReflectedType.FullName + ".get_" + propertyInfo.Name, typeof(PropType), new Type[1] { typeof(InstanceType).MakeByRefType() }, restrictedSkipVisibility: true);
		ILGenerator iLGenerator = dynamicMethod.GetILGenerator();
		if (typeof(InstanceType).IsValueType)
		{
			iLGenerator.Emit(OpCodes.Ldarg_0);
			iLGenerator.Emit(OpCodes.Callvirt, getMethod);
		}
		else
		{
			iLGenerator.Emit(OpCodes.Ldarg_0);
			iLGenerator.Emit(OpCodes.Ldind_Ref);
			iLGenerator.Emit(OpCodes.Callvirt, getMethod);
		}
		iLGenerator.Emit(OpCodes.Ret);
		return (ValueGetter<InstanceType, PropType>)dynamicMethod.CreateDelegate(typeof(ValueGetter<InstanceType, PropType>));
	}

	public static Func<InstanceType, ReturnType> CreateMethodReturner<InstanceType, ReturnType>(MethodInfo methodInfo)
	{
		if (methodInfo == null)
		{
			throw new ArgumentNullException("methodInfo");
		}
		if (methodInfo.IsStatic)
		{
			throw new ArgumentException("Given method '" + methodInfo.Name + "' is static when it has to be an instance method.");
		}
		methodInfo = methodInfo.DeAliasMethod();
		return (Func<InstanceType, ReturnType>)Delegate.CreateDelegate(typeof(Func<InstanceType, ReturnType>), methodInfo);
	}

	public static Action CreateStaticMethodCaller(MethodInfo methodInfo)
	{
		if (methodInfo == null)
		{
			throw new ArgumentNullException("methodInfo");
		}
		if (!methodInfo.IsStatic)
		{
			throw new ArgumentException("Given method '" + methodInfo.Name + "' is an instance method when it has to be static.");
		}
		if (methodInfo.GetParameters().Length != 0)
		{
			throw new ArgumentException("Given method cannot have any parameters.");
		}
		methodInfo = methodInfo.DeAliasMethod();
		return (Action)Delegate.CreateDelegate(typeof(Action), methodInfo);
	}

	public static Action<object, TArg1> CreateWeakInstanceMethodCaller<TArg1>(MethodInfo methodInfo)
	{
		if (methodInfo == null)
		{
			throw new ArgumentNullException("methodInfo");
		}
		if (methodInfo.IsStatic)
		{
			throw new ArgumentException("Given method '" + methodInfo.Name + "' is static when it has to be an instance method.");
		}
		ParameterInfo[] parameters = methodInfo.GetParameters();
		if (parameters.Length != 1)
		{
			throw new ArgumentException("Given method '" + methodInfo.Name + "' must have exactly one parameter.");
		}
		if (parameters[0].ParameterType != typeof(TArg1))
		{
			throw new ArgumentException("The first parameter of the method '" + methodInfo.Name + "' must be of type " + typeof(TArg1)?.ToString() + ".");
		}
		methodInfo = methodInfo.DeAliasMethod();
		if (EmitIsIllegalForMember(methodInfo))
		{
			return delegate(object classInstance, TArg1 arg)
			{
				methodInfo.Invoke(classInstance, new object[1] { arg });
			};
		}
		Type declaringType = methodInfo.DeclaringType;
		DynamicMethod dynamicMethod = new DynamicMethod(methodInfo.ReflectedType.FullName + ".call_" + methodInfo.Name, null, new Type[2]
		{
			typeof(object),
			typeof(TArg1)
		}, restrictedSkipVisibility: true);
		ILGenerator iLGenerator = dynamicMethod.GetILGenerator();
		if (declaringType.IsValueType)
		{
			LocalBuilder local = iLGenerator.DeclareLocal(declaringType);
			iLGenerator.Emit(OpCodes.Ldarg_0);
			iLGenerator.Emit(OpCodes.Unbox_Any, declaringType);
			iLGenerator.Emit(OpCodes.Stloc, local);
			iLGenerator.Emit(OpCodes.Ldloca_S, local);
			iLGenerator.Emit(OpCodes.Ldarg_1);
			iLGenerator.Emit(OpCodes.Call, methodInfo);
		}
		else
		{
			iLGenerator.Emit(OpCodes.Ldarg_0);
			iLGenerator.Emit(OpCodes.Castclass, declaringType);
			iLGenerator.Emit(OpCodes.Ldarg_1);
			iLGenerator.Emit(OpCodes.Callvirt, methodInfo);
		}
		iLGenerator.Emit(OpCodes.Ret);
		return (Action<object, TArg1>)dynamicMethod.CreateDelegate(typeof(Action<object, TArg1>));
	}

	public static Action<object> CreateWeakInstanceMethodCaller(MethodInfo methodInfo)
	{
		if (methodInfo == null)
		{
			throw new ArgumentNullException("methodInfo");
		}
		if (methodInfo.IsStatic)
		{
			throw new ArgumentException("Given method '" + methodInfo.Name + "' is static when it has to be an instance method.");
		}
		if (methodInfo.GetParameters().Length != 0)
		{
			throw new ArgumentException("Given method cannot have any parameters.");
		}
		methodInfo = methodInfo.DeAliasMethod();
		if (EmitIsIllegalForMember(methodInfo))
		{
			return delegate(object classInstance)
			{
				methodInfo.Invoke(classInstance, null);
			};
		}
		Type declaringType = methodInfo.DeclaringType;
		DynamicMethod dynamicMethod = new DynamicMethod(methodInfo.ReflectedType.FullName + ".call_" + methodInfo.Name, null, new Type[1] { typeof(object) }, restrictedSkipVisibility: true);
		ILGenerator iLGenerator = dynamicMethod.GetILGenerator();
		if (declaringType.IsValueType)
		{
			LocalBuilder local = iLGenerator.DeclareLocal(declaringType);
			iLGenerator.Emit(OpCodes.Ldarg_0);
			iLGenerator.Emit(OpCodes.Unbox_Any, declaringType);
			iLGenerator.Emit(OpCodes.Stloc, local);
			iLGenerator.Emit(OpCodes.Ldloca_S, local);
			iLGenerator.Emit(OpCodes.Call, methodInfo);
		}
		else
		{
			iLGenerator.Emit(OpCodes.Ldarg_0);
			iLGenerator.Emit(OpCodes.Castclass, declaringType);
			iLGenerator.Emit(OpCodes.Callvirt, methodInfo);
		}
		if (methodInfo.ReturnType != null && methodInfo.ReturnType != typeof(void))
		{
			iLGenerator.Emit(OpCodes.Pop);
		}
		iLGenerator.Emit(OpCodes.Ret);
		return (Action<object>)dynamicMethod.CreateDelegate(typeof(Action<object>));
	}

	public static Func<object, TArg1, TResult> CreateWeakInstanceMethodCaller<TResult, TArg1>(MethodInfo methodInfo)
	{
		if (methodInfo == null)
		{
			throw new ArgumentNullException("methodInfo");
		}
		if (methodInfo.IsStatic)
		{
			throw new ArgumentException("Given method '" + methodInfo.Name + "' is static when it has to be an instance method.");
		}
		if (methodInfo.ReturnType != typeof(TResult))
		{
			throw new ArgumentException("Given method '" + methodInfo.Name + "' must return type " + typeof(TResult)?.ToString() + ".");
		}
		ParameterInfo[] parameters = methodInfo.GetParameters();
		if (parameters.Length != 1)
		{
			throw new ArgumentException("Given method '" + methodInfo.Name + "' must have exactly one parameter.");
		}
		if (!typeof(TArg1).InheritsFrom(parameters[0].ParameterType))
		{
			throw new ArgumentException("The first parameter of the method '" + methodInfo.Name + "' must be of type " + typeof(TArg1)?.ToString() + ".");
		}
		methodInfo = methodInfo.DeAliasMethod();
		if (EmitIsIllegalForMember(methodInfo))
		{
			return (object classInstance, TArg1 arg1) => (TResult)methodInfo.Invoke(classInstance, new object[1] { arg1 });
		}
		Type declaringType = methodInfo.DeclaringType;
		DynamicMethod dynamicMethod = new DynamicMethod(methodInfo.ReflectedType.FullName + ".call_" + methodInfo.Name, typeof(TResult), new Type[2]
		{
			typeof(object),
			typeof(TArg1)
		}, restrictedSkipVisibility: true);
		ILGenerator iLGenerator = dynamicMethod.GetILGenerator();
		if (declaringType.IsValueType)
		{
			LocalBuilder local = iLGenerator.DeclareLocal(declaringType);
			iLGenerator.Emit(OpCodes.Ldarg_0);
			iLGenerator.Emit(OpCodes.Unbox_Any, declaringType);
			iLGenerator.Emit(OpCodes.Stloc, local);
			iLGenerator.Emit(OpCodes.Ldloca_S, local);
			iLGenerator.Emit(OpCodes.Ldarg_1);
			iLGenerator.Emit(OpCodes.Call, methodInfo);
		}
		else
		{
			iLGenerator.Emit(OpCodes.Ldarg_0);
			iLGenerator.Emit(OpCodes.Castclass, declaringType);
			iLGenerator.Emit(OpCodes.Ldarg_1);
			iLGenerator.Emit(OpCodes.Callvirt, methodInfo);
		}
		iLGenerator.Emit(OpCodes.Ret);
		return (Func<object, TArg1, TResult>)dynamicMethod.CreateDelegate(typeof(Func<object, TArg1, TResult>));
	}

	public static Func<object, TResult> CreateWeakInstanceMethodCallerFunc<TResult>(MethodInfo methodInfo)
	{
		if (methodInfo == null)
		{
			throw new ArgumentNullException("methodInfo");
		}
		if (methodInfo.IsStatic)
		{
			throw new ArgumentException("Given method '" + methodInfo.Name + "' is static when it has to be an instance method.");
		}
		if (methodInfo.ReturnType != typeof(TResult))
		{
			throw new ArgumentException("Given method '" + methodInfo.Name + "' must return type " + typeof(TResult)?.ToString() + ".");
		}
		if (methodInfo.GetParameters().Length != 0)
		{
			throw new ArgumentException("Given method '" + methodInfo.Name + "' must have no parameter.");
		}
		methodInfo = methodInfo.DeAliasMethod();
		if (EmitIsIllegalForMember(methodInfo))
		{
			return (object classInstance) => (TResult)methodInfo.Invoke(classInstance, null);
		}
		Type declaringType = methodInfo.DeclaringType;
		DynamicMethod dynamicMethod = new DynamicMethod(methodInfo.ReflectedType.FullName + ".call_" + methodInfo.Name, typeof(TResult), new Type[1] { typeof(object) }, restrictedSkipVisibility: true);
		ILGenerator iLGenerator = dynamicMethod.GetILGenerator();
		if (declaringType.IsValueType)
		{
			LocalBuilder local = iLGenerator.DeclareLocal(declaringType);
			iLGenerator.Emit(OpCodes.Ldarg_0);
			iLGenerator.Emit(OpCodes.Unbox_Any, declaringType);
			iLGenerator.Emit(OpCodes.Stloc, local);
			iLGenerator.Emit(OpCodes.Ldloca_S, local);
			iLGenerator.Emit(OpCodes.Call, methodInfo);
		}
		else
		{
			iLGenerator.Emit(OpCodes.Ldarg_0);
			iLGenerator.Emit(OpCodes.Castclass, declaringType);
			iLGenerator.Emit(OpCodes.Callvirt, methodInfo);
		}
		iLGenerator.Emit(OpCodes.Ret);
		return (Func<object, TResult>)dynamicMethod.CreateDelegate(typeof(Func<object, TResult>));
	}

	public static Func<object, TArg, TResult> CreateWeakInstanceMethodCallerFunc<TArg, TResult>(MethodInfo methodInfo)
	{
		if (methodInfo == null)
		{
			throw new ArgumentNullException("methodInfo");
		}
		if (methodInfo.IsStatic)
		{
			throw new ArgumentException("Given method '" + methodInfo.Name + "' is static when it has to be an instance method.");
		}
		if (methodInfo.ReturnType != typeof(TResult))
		{
			throw new ArgumentException("Given method '" + methodInfo.Name + "' must return type " + typeof(TResult)?.ToString() + ".");
		}
		ParameterInfo[] parameters = methodInfo.GetParameters();
		if (parameters.Length != 1)
		{
			throw new ArgumentException("Given method '" + methodInfo.Name + "' must have one parameter.");
		}
		if (!parameters[0].ParameterType.IsAssignableFrom(typeof(TArg)))
		{
			throw new ArgumentException("Given method '" + methodInfo.Name + "' has an invalid parameter type.");
		}
		methodInfo = methodInfo.DeAliasMethod();
		if (EmitIsIllegalForMember(methodInfo))
		{
			return (object classInstance, TArg arg) => (TResult)methodInfo.Invoke(classInstance, new object[1] { arg });
		}
		Type declaringType = methodInfo.DeclaringType;
		DynamicMethod dynamicMethod = new DynamicMethod(methodInfo.ReflectedType.FullName + ".call_" + methodInfo.Name, typeof(TResult), new Type[2]
		{
			typeof(object),
			typeof(TArg)
		}, restrictedSkipVisibility: true);
		ILGenerator iLGenerator = dynamicMethod.GetILGenerator();
		if (declaringType.IsValueType)
		{
			LocalBuilder local = iLGenerator.DeclareLocal(declaringType);
			iLGenerator.Emit(OpCodes.Ldarg_0);
			iLGenerator.Emit(OpCodes.Unbox_Any, declaringType);
			iLGenerator.Emit(OpCodes.Stloc, local);
			iLGenerator.Emit(OpCodes.Ldloca_S, local);
			iLGenerator.Emit(OpCodes.Ldarg_1);
			iLGenerator.Emit(OpCodes.Call, methodInfo);
		}
		else
		{
			iLGenerator.Emit(OpCodes.Ldarg_0);
			iLGenerator.Emit(OpCodes.Castclass, declaringType);
			iLGenerator.Emit(OpCodes.Ldarg_1);
			iLGenerator.Emit(OpCodes.Callvirt, methodInfo);
		}
		iLGenerator.Emit(OpCodes.Ret);
		return (Func<object, TArg, TResult>)dynamicMethod.CreateDelegate(typeof(Func<object, TArg, TResult>));
	}

	public static Action<InstanceType> CreateInstanceMethodCaller<InstanceType>(MethodInfo methodInfo)
	{
		if (methodInfo == null)
		{
			throw new ArgumentNullException("methodInfo");
		}
		if (methodInfo.IsStatic)
		{
			throw new ArgumentException("Given method '" + methodInfo.Name + "' is static when it has to be an instance method.");
		}
		if (methodInfo.GetParameters().Length != 0)
		{
			throw new ArgumentException("Given method cannot have any parameters.");
		}
		if (typeof(InstanceType).IsValueType)
		{
			throw new ArgumentException("This method does not work with struct instances; please use CreateInstanceRefMethodCaller instead.");
		}
		methodInfo = methodInfo.DeAliasMethod();
		return (Action<InstanceType>)Delegate.CreateDelegate(typeof(Action<InstanceType>), methodInfo);
	}

	public static Action<InstanceType, Arg1> CreateInstanceMethodCaller<InstanceType, Arg1>(MethodInfo methodInfo)
	{
		if (methodInfo == null)
		{
			throw new ArgumentNullException("methodInfo");
		}
		if (methodInfo.IsStatic)
		{
			throw new ArgumentException("Given method '" + methodInfo.Name + "' is static when it has to be an instance method.");
		}
		if (methodInfo.GetParameters().Length != 1)
		{
			throw new ArgumentException("Given method must have only one parameter.");
		}
		if (typeof(InstanceType).IsValueType)
		{
			throw new ArgumentException("This method does not work with struct instances; please use CreateInstanceRefMethodCaller instead.");
		}
		methodInfo = methodInfo.DeAliasMethod();
		return (Action<InstanceType, Arg1>)Delegate.CreateDelegate(typeof(Action<InstanceType, Arg1>), methodInfo);
	}

	public static InstanceRefMethodCaller<InstanceType> CreateInstanceRefMethodCaller<InstanceType>(MethodInfo methodInfo)
	{
		if (methodInfo == null)
		{
			throw new ArgumentNullException("methodInfo");
		}
		if (methodInfo.IsStatic)
		{
			throw new ArgumentException("Given method '" + methodInfo.Name + "' is static when it has to be an instance method.");
		}
		if (methodInfo.GetParameters().Length != 0)
		{
			throw new ArgumentException("Given method cannot have any parameters.");
		}
		methodInfo = methodInfo.DeAliasMethod();
		if (EmitIsIllegalForMember(methodInfo))
		{
			return delegate(ref InstanceType instance)
			{
				object obj = instance;
				methodInfo.Invoke(obj, null);
				instance = (InstanceType)obj;
			};
		}
		Type declaringType = methodInfo.DeclaringType;
		DynamicMethod dynamicMethod = new DynamicMethod(methodInfo.ReflectedType.FullName + ".call_" + methodInfo.Name, typeof(void), new Type[1] { typeof(InstanceType).MakeByRefType() }, restrictedSkipVisibility: true);
		ILGenerator iLGenerator = dynamicMethod.GetILGenerator();
		if (declaringType.IsValueType)
		{
			iLGenerator.Emit(OpCodes.Ldarg_0);
			iLGenerator.Emit(OpCodes.Call, methodInfo);
		}
		else
		{
			iLGenerator.Emit(OpCodes.Ldarg_0);
			iLGenerator.Emit(OpCodes.Ldind_Ref);
			iLGenerator.Emit(OpCodes.Callvirt, methodInfo);
		}
		iLGenerator.Emit(OpCodes.Ret);
		return (InstanceRefMethodCaller<InstanceType>)dynamicMethod.CreateDelegate(typeof(InstanceRefMethodCaller<InstanceType>));
	}

	public static InstanceRefMethodCaller<InstanceType, Arg1> CreateInstanceRefMethodCaller<InstanceType, Arg1>(MethodInfo methodInfo)
	{
		if (methodInfo == null)
		{
			throw new ArgumentNullException("methodInfo");
		}
		if (methodInfo.IsStatic)
		{
			throw new ArgumentException("Given method '" + methodInfo.Name + "' is static when it has to be an instance method.");
		}
		if (methodInfo.GetParameters().Length != 1)
		{
			throw new ArgumentException("Given method must have only one parameter.");
		}
		methodInfo = methodInfo.DeAliasMethod();
		if (EmitIsIllegalForMember(methodInfo))
		{
			return delegate(ref InstanceType instance, Arg1 arg1)
			{
				object obj = instance;
				methodInfo.Invoke(obj, new object[1] { arg1 });
				instance = (InstanceType)obj;
			};
		}
		Type declaringType = methodInfo.DeclaringType;
		DynamicMethod dynamicMethod = new DynamicMethod(methodInfo.ReflectedType.FullName + ".call_" + methodInfo.Name, typeof(void), new Type[2]
		{
			typeof(InstanceType).MakeByRefType(),
			typeof(Arg1)
		}, restrictedSkipVisibility: true);
		ILGenerator iLGenerator = dynamicMethod.GetILGenerator();
		if (declaringType.IsValueType)
		{
			iLGenerator.Emit(OpCodes.Ldarg_0);
			iLGenerator.Emit(OpCodes.Ldarg_1);
			iLGenerator.Emit(OpCodes.Call, methodInfo);
		}
		else
		{
			iLGenerator.Emit(OpCodes.Ldarg_0);
			iLGenerator.Emit(OpCodes.Ldind_Ref);
			iLGenerator.Emit(OpCodes.Ldarg_1);
			iLGenerator.Emit(OpCodes.Callvirt, methodInfo);
		}
		iLGenerator.Emit(OpCodes.Ret);
		return (InstanceRefMethodCaller<InstanceType, Arg1>)dynamicMethod.CreateDelegate(typeof(InstanceRefMethodCaller<InstanceType, Arg1>));
	}
}
