namespace Sirenix.OdinSerializer.Utilities;

public delegate void ValueSetter<InstanceType, FieldType>(ref InstanceType instance, FieldType value);
