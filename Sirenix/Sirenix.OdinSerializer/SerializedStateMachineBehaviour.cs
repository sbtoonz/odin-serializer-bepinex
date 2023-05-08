using UnityEngine;

namespace Sirenix.OdinSerializer;

public abstract class SerializedStateMachineBehaviour : StateMachineBehaviour, ISerializationCallbackReceiver
{
	[SerializeField]
	[HideInInspector]
	private SerializationData serializationData;

	void ISerializationCallbackReceiver.OnAfterDeserialize()
	{
		UnitySerializationUtility.DeserializeUnityObject(this, ref serializationData);
		OnAfterDeserialize();
	}

	void ISerializationCallbackReceiver.OnBeforeSerialize()
	{
		OnBeforeSerialize();
		UnitySerializationUtility.SerializeUnityObject(this, ref serializationData);
	}

	protected virtual void OnAfterDeserialize()
	{
	}

	protected virtual void OnBeforeSerialize()
	{
	}
}
