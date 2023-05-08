using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using Sirenix.OdinSerializer.Utilities;
using Sirenix.OdinSerializer.Utilities.Unsafe;

namespace Sirenix.OdinSerializer;

public class BinaryDataWriter : BaseDataWriter
{
	private struct Struct256Bit
	{
		public decimal d1;

		public decimal d2;
	}

	private static readonly Dictionary<Type, Delegate> PrimitiveGetBytesMethods = new Dictionary<Type, Delegate>(FastTypeComparer.Instance)
	{
		{
			typeof(char),
			(Action<byte[], int, char>)delegate(byte[] b, int i, char v)
			{
				ProperBitConverter.GetBytes(b, i, v);
			}
		},
		{
			typeof(byte),
			(Action<byte[], int, byte>)delegate(byte[] b, int i, byte v)
			{
				b[i] = v;
			}
		},
		{
			typeof(sbyte),
			(Action<byte[], int, sbyte>)delegate(byte[] b, int i, sbyte v)
			{
				b[i] = (byte)v;
			}
		},
		{
			typeof(bool),
			(Action<byte[], int, bool>)delegate(byte[] b, int i, bool v)
			{
				b[i] = (byte)(v ? 1 : 0);
			}
		},
		{
			typeof(short),
			new Action<byte[], int, short>(ProperBitConverter.GetBytes)
		},
		{
			typeof(int),
			new Action<byte[], int, int>(ProperBitConverter.GetBytes)
		},
		{
			typeof(long),
			new Action<byte[], int, long>(ProperBitConverter.GetBytes)
		},
		{
			typeof(ushort),
			new Action<byte[], int, ushort>(ProperBitConverter.GetBytes)
		},
		{
			typeof(uint),
			new Action<byte[], int, uint>(ProperBitConverter.GetBytes)
		},
		{
			typeof(ulong),
			new Action<byte[], int, ulong>(ProperBitConverter.GetBytes)
		},
		{
			typeof(decimal),
			new Action<byte[], int, decimal>(ProperBitConverter.GetBytes)
		},
		{
			typeof(float),
			new Action<byte[], int, float>(ProperBitConverter.GetBytes)
		},
		{
			typeof(double),
			new Action<byte[], int, double>(ProperBitConverter.GetBytes)
		},
		{
			typeof(Guid),
			new Action<byte[], int, Guid>(ProperBitConverter.GetBytes)
		}
	};

	private static readonly Dictionary<Type, int> PrimitiveSizes = new Dictionary<Type, int>(FastTypeComparer.Instance)
	{
		{
			typeof(char),
			2
		},
		{
			typeof(byte),
			1
		},
		{
			typeof(sbyte),
			1
		},
		{
			typeof(bool),
			1
		},
		{
			typeof(short),
			2
		},
		{
			typeof(int),
			4
		},
		{
			typeof(long),
			8
		},
		{
			typeof(ushort),
			2
		},
		{
			typeof(uint),
			4
		},
		{
			typeof(ulong),
			8
		},
		{
			typeof(decimal),
			16
		},
		{
			typeof(float),
			4
		},
		{
			typeof(double),
			8
		},
		{
			typeof(Guid),
			16
		}
	};

	private readonly byte[] small_buffer = new byte[16];

	private readonly byte[] buffer = new byte[102400];

	private int bufferIndex;

	private readonly Dictionary<Type, int> types = new Dictionary<Type, int>(16, FastTypeComparer.Instance);

	public bool CompressStringsTo8BitWhenPossible;

	private static readonly Dictionary<Type, Action<BinaryDataWriter, object>> PrimitiveArrayWriters = new Dictionary<Type, Action<BinaryDataWriter, object>>(FastTypeComparer.Instance)
	{
		{
			typeof(char),
			WritePrimitiveArray_char
		},
		{
			typeof(sbyte),
			WritePrimitiveArray_sbyte
		},
		{
			typeof(short),
			WritePrimitiveArray_short
		},
		{
			typeof(int),
			WritePrimitiveArray_int
		},
		{
			typeof(long),
			WritePrimitiveArray_long
		},
		{
			typeof(byte),
			WritePrimitiveArray_byte
		},
		{
			typeof(ushort),
			WritePrimitiveArray_ushort
		},
		{
			typeof(uint),
			WritePrimitiveArray_uint
		},
		{
			typeof(ulong),
			WritePrimitiveArray_ulong
		},
		{
			typeof(decimal),
			WritePrimitiveArray_decimal
		},
		{
			typeof(bool),
			WritePrimitiveArray_bool
		},
		{
			typeof(float),
			WritePrimitiveArray_float
		},
		{
			typeof(double),
			WritePrimitiveArray_double
		},
		{
			typeof(Guid),
			WritePrimitiveArray_Guid
		}
	};

	public BinaryDataWriter()
		: base(null, null)
	{
	}

	public BinaryDataWriter(Stream stream, SerializationContext context)
		: base(stream, context)
	{
	}

	public override void BeginArrayNode(long length)
	{
		EnsureBufferSpace(9);
		buffer[bufferIndex++] = 6;
		UNSAFE_WriteToBuffer_8_Int64(length);
		PushArray();
	}

	public override void BeginReferenceNode(string name, Type type, int id)
	{
		if (name != null)
		{
			EnsureBufferSpace(1);
			buffer[bufferIndex++] = 1;
			WriteStringFast(name);
		}
		else
		{
			EnsureBufferSpace(1);
			buffer[bufferIndex++] = 2;
		}
		WriteType(type);
		EnsureBufferSpace(4);
		UNSAFE_WriteToBuffer_4_Int32(id);
		PushNode(name, id, type);
	}

	public override void BeginStructNode(string name, Type type)
	{
		if (name != null)
		{
			EnsureBufferSpace(1);
			buffer[bufferIndex++] = 3;
			WriteStringFast(name);
		}
		else
		{
			EnsureBufferSpace(1);
			buffer[bufferIndex++] = 4;
		}
		WriteType(type);
		PushNode(name, -1, type);
	}

	public override void Dispose()
	{
		FlushToStream();
	}

	public override void EndArrayNode()
	{
		PopArray();
		EnsureBufferSpace(1);
		buffer[bufferIndex++] = 7;
	}

	public override void EndNode(string name)
	{
		PopNode(name);
		EnsureBufferSpace(1);
		buffer[bufferIndex++] = 5;
	}

	private static void WritePrimitiveArray_byte(BinaryDataWriter writer, object o)
	{
		byte[] array = o as byte[];
		writer.EnsureBufferSpace(9);
		writer.buffer[writer.bufferIndex++] = 8;
		writer.UNSAFE_WriteToBuffer_4_Int32(array.Length);
		writer.UNSAFE_WriteToBuffer_4_Int32(1);
		writer.FlushToStream();
		writer.Stream.Write(array, 0, array.Length);
	}

	private unsafe static void WritePrimitiveArray_sbyte(BinaryDataWriter writer, object o)
	{
		sbyte[] array = o as sbyte[];
		int num = 1;
		int num2 = array.Length * num;
		writer.EnsureBufferSpace(9);
		writer.buffer[writer.bufferIndex++] = 8;
		writer.UNSAFE_WriteToBuffer_4_Int32(array.Length);
		writer.UNSAFE_WriteToBuffer_4_Int32(num);
		if (writer.TryEnsureBufferSpace(num2))
		{
			fixed (byte* ptr2 = writer.buffer)
			{
				fixed (sbyte* ptr = array)
				{
					void* from = ptr;
					void* to = ptr2 + writer.bufferIndex;
					UnsafeUtilities.MemoryCopy(from, to, num2);
				}
			}
			writer.bufferIndex += num2;
			return;
		}
		writer.FlushToStream();
		using Buffer<byte> buffer = Buffer<byte>.Claim(num2);
		UnsafeUtilities.MemoryCopy(array, buffer.Array, num2, 0, 0);
		writer.Stream.Write(buffer.Array, 0, num2);
	}

	private unsafe static void WritePrimitiveArray_bool(BinaryDataWriter writer, object o)
	{
		bool[] array = o as bool[];
		int num = 1;
		int num2 = array.Length * num;
		writer.EnsureBufferSpace(9);
		writer.buffer[writer.bufferIndex++] = 8;
		writer.UNSAFE_WriteToBuffer_4_Int32(array.Length);
		writer.UNSAFE_WriteToBuffer_4_Int32(num);
		if (writer.TryEnsureBufferSpace(num2))
		{
			fixed (byte* ptr2 = writer.buffer)
			{
				fixed (bool* ptr = array)
				{
					void* from = ptr;
					void* to = ptr2 + writer.bufferIndex;
					UnsafeUtilities.MemoryCopy(from, to, num2);
				}
			}
			writer.bufferIndex += num2;
			return;
		}
		writer.FlushToStream();
		using Buffer<byte> buffer = Buffer<byte>.Claim(num2);
		UnsafeUtilities.MemoryCopy(array, buffer.Array, num2, 0, 0);
		writer.Stream.Write(buffer.Array, 0, num2);
	}

	private unsafe static void WritePrimitiveArray_char(BinaryDataWriter writer, object o)
	{
		char[] array = o as char[];
		int num = 2;
		int num2 = array.Length * num;
		writer.EnsureBufferSpace(9);
		writer.buffer[writer.bufferIndex++] = 8;
		writer.UNSAFE_WriteToBuffer_4_Int32(array.Length);
		writer.UNSAFE_WriteToBuffer_4_Int32(num);
		if (writer.TryEnsureBufferSpace(num2))
		{
			if (BitConverter.IsLittleEndian)
			{
				fixed (byte* ptr2 = writer.buffer)
				{
					fixed (char* ptr = array)
					{
						void* from = ptr;
						void* to = ptr2 + writer.bufferIndex;
						UnsafeUtilities.MemoryCopy(from, to, num2);
					}
				}
				writer.bufferIndex += num2;
			}
			else
			{
				for (int i = 0; i < array.Length; i++)
				{
					writer.UNSAFE_WriteToBuffer_2_Char(array[i]);
				}
			}
			return;
		}
		writer.FlushToStream();
		using Buffer<byte> buffer = Buffer<byte>.Claim(num2);
		if (BitConverter.IsLittleEndian)
		{
			UnsafeUtilities.MemoryCopy(array, buffer.Array, num2, 0, 0);
		}
		else
		{
			byte[] array2 = buffer.Array;
			for (int j = 0; j < array.Length; j++)
			{
				ProperBitConverter.GetBytes(array2, j * num, array[j]);
			}
		}
		writer.Stream.Write(buffer.Array, 0, num2);
	}

	private unsafe static void WritePrimitiveArray_short(BinaryDataWriter writer, object o)
	{
		short[] array = o as short[];
		int num = 2;
		int num2 = array.Length * num;
		writer.EnsureBufferSpace(9);
		writer.buffer[writer.bufferIndex++] = 8;
		writer.UNSAFE_WriteToBuffer_4_Int32(array.Length);
		writer.UNSAFE_WriteToBuffer_4_Int32(num);
		if (writer.TryEnsureBufferSpace(num2))
		{
			if (BitConverter.IsLittleEndian)
			{
				fixed (byte* ptr2 = writer.buffer)
				{
					fixed (short* ptr = array)
					{
						void* from = ptr;
						void* to = ptr2 + writer.bufferIndex;
						UnsafeUtilities.MemoryCopy(from, to, num2);
					}
				}
				writer.bufferIndex += num2;
			}
			else
			{
				for (int i = 0; i < array.Length; i++)
				{
					writer.UNSAFE_WriteToBuffer_2_Int16(array[i]);
				}
			}
			return;
		}
		writer.FlushToStream();
		using Buffer<byte> buffer = Buffer<byte>.Claim(num2);
		if (BitConverter.IsLittleEndian)
		{
			UnsafeUtilities.MemoryCopy(array, buffer.Array, num2, 0, 0);
		}
		else
		{
			byte[] array2 = buffer.Array;
			for (int j = 0; j < array.Length; j++)
			{
				ProperBitConverter.GetBytes(array2, j * num, array[j]);
			}
		}
		writer.Stream.Write(buffer.Array, 0, num2);
	}

	private unsafe static void WritePrimitiveArray_int(BinaryDataWriter writer, object o)
	{
		int[] array = o as int[];
		int num = 4;
		int num2 = array.Length * num;
		writer.EnsureBufferSpace(9);
		writer.buffer[writer.bufferIndex++] = 8;
		writer.UNSAFE_WriteToBuffer_4_Int32(array.Length);
		writer.UNSAFE_WriteToBuffer_4_Int32(num);
		if (writer.TryEnsureBufferSpace(num2))
		{
			if (BitConverter.IsLittleEndian)
			{
				fixed (byte* ptr2 = writer.buffer)
				{
					fixed (int* ptr = array)
					{
						void* from = ptr;
						void* to = ptr2 + writer.bufferIndex;
						UnsafeUtilities.MemoryCopy(from, to, num2);
					}
				}
				writer.bufferIndex += num2;
			}
			else
			{
				for (int i = 0; i < array.Length; i++)
				{
					writer.UNSAFE_WriteToBuffer_4_Int32(array[i]);
				}
			}
			return;
		}
		writer.FlushToStream();
		using Buffer<byte> buffer = Buffer<byte>.Claim(num2);
		if (BitConverter.IsLittleEndian)
		{
			UnsafeUtilities.MemoryCopy(array, buffer.Array, num2, 0, 0);
		}
		else
		{
			byte[] array2 = buffer.Array;
			for (int j = 0; j < array.Length; j++)
			{
				ProperBitConverter.GetBytes(array2, j * num, array[j]);
			}
		}
		writer.Stream.Write(buffer.Array, 0, num2);
	}

	private unsafe static void WritePrimitiveArray_long(BinaryDataWriter writer, object o)
	{
		long[] array = o as long[];
		int num = 8;
		int num2 = array.Length * num;
		writer.EnsureBufferSpace(9);
		writer.buffer[writer.bufferIndex++] = 8;
		writer.UNSAFE_WriteToBuffer_4_Int32(array.Length);
		writer.UNSAFE_WriteToBuffer_4_Int32(num);
		if (writer.TryEnsureBufferSpace(num2))
		{
			if (BitConverter.IsLittleEndian)
			{
				fixed (byte* ptr2 = writer.buffer)
				{
					fixed (long* ptr = array)
					{
						void* from = ptr;
						void* to = ptr2 + writer.bufferIndex;
						UnsafeUtilities.MemoryCopy(from, to, num2);
					}
				}
				writer.bufferIndex += num2;
			}
			else
			{
				for (int i = 0; i < array.Length; i++)
				{
					writer.UNSAFE_WriteToBuffer_8_Int64(array[i]);
				}
			}
			return;
		}
		writer.FlushToStream();
		using Buffer<byte> buffer = Buffer<byte>.Claim(num2);
		if (BitConverter.IsLittleEndian)
		{
			UnsafeUtilities.MemoryCopy(array, buffer.Array, num2, 0, 0);
		}
		else
		{
			byte[] array2 = buffer.Array;
			for (int j = 0; j < array.Length; j++)
			{
				ProperBitConverter.GetBytes(array2, j * num, array[j]);
			}
		}
		writer.Stream.Write(buffer.Array, 0, num2);
	}

	private unsafe static void WritePrimitiveArray_ushort(BinaryDataWriter writer, object o)
	{
		ushort[] array = o as ushort[];
		int num = 2;
		int num2 = array.Length * num;
		writer.EnsureBufferSpace(9);
		writer.buffer[writer.bufferIndex++] = 8;
		writer.UNSAFE_WriteToBuffer_4_Int32(array.Length);
		writer.UNSAFE_WriteToBuffer_4_Int32(num);
		if (writer.TryEnsureBufferSpace(num2))
		{
			if (BitConverter.IsLittleEndian)
			{
				fixed (byte* ptr2 = writer.buffer)
				{
					fixed (ushort* ptr = array)
					{
						void* from = ptr;
						void* to = ptr2 + writer.bufferIndex;
						UnsafeUtilities.MemoryCopy(from, to, num2);
					}
				}
				writer.bufferIndex += num2;
			}
			else
			{
				for (int i = 0; i < array.Length; i++)
				{
					writer.UNSAFE_WriteToBuffer_2_UInt16(array[i]);
				}
			}
			return;
		}
		writer.FlushToStream();
		using Buffer<byte> buffer = Buffer<byte>.Claim(num2);
		if (BitConverter.IsLittleEndian)
		{
			UnsafeUtilities.MemoryCopy(array, buffer.Array, num2, 0, 0);
		}
		else
		{
			byte[] array2 = buffer.Array;
			for (int j = 0; j < array.Length; j++)
			{
				ProperBitConverter.GetBytes(array2, j * num, array[j]);
			}
		}
		writer.Stream.Write(buffer.Array, 0, num2);
	}

	private unsafe static void WritePrimitiveArray_uint(BinaryDataWriter writer, object o)
	{
		uint[] array = o as uint[];
		int num = 4;
		int num2 = array.Length * num;
		writer.EnsureBufferSpace(9);
		writer.buffer[writer.bufferIndex++] = 8;
		writer.UNSAFE_WriteToBuffer_4_Int32(array.Length);
		writer.UNSAFE_WriteToBuffer_4_Int32(num);
		if (writer.TryEnsureBufferSpace(num2))
		{
			if (BitConverter.IsLittleEndian)
			{
				fixed (byte* ptr2 = writer.buffer)
				{
					fixed (uint* ptr = array)
					{
						void* from = ptr;
						void* to = ptr2 + writer.bufferIndex;
						UnsafeUtilities.MemoryCopy(from, to, num2);
					}
				}
				writer.bufferIndex += num2;
			}
			else
			{
				for (int i = 0; i < array.Length; i++)
				{
					writer.UNSAFE_WriteToBuffer_4_UInt32(array[i]);
				}
			}
			return;
		}
		writer.FlushToStream();
		using Buffer<byte> buffer = Buffer<byte>.Claim(num2);
		if (BitConverter.IsLittleEndian)
		{
			UnsafeUtilities.MemoryCopy(array, buffer.Array, num2, 0, 0);
		}
		else
		{
			byte[] array2 = buffer.Array;
			for (int j = 0; j < array.Length; j++)
			{
				ProperBitConverter.GetBytes(array2, j * num, array[j]);
			}
		}
		writer.Stream.Write(buffer.Array, 0, num2);
	}

	private unsafe static void WritePrimitiveArray_ulong(BinaryDataWriter writer, object o)
	{
		ulong[] array = o as ulong[];
		int num = 8;
		int num2 = array.Length * num;
		writer.EnsureBufferSpace(9);
		writer.buffer[writer.bufferIndex++] = 8;
		writer.UNSAFE_WriteToBuffer_4_Int32(array.Length);
		writer.UNSAFE_WriteToBuffer_4_Int32(num);
		if (writer.TryEnsureBufferSpace(num2))
		{
			if (BitConverter.IsLittleEndian)
			{
				fixed (byte* ptr2 = writer.buffer)
				{
					fixed (ulong* ptr = array)
					{
						void* from = ptr;
						void* to = ptr2 + writer.bufferIndex;
						UnsafeUtilities.MemoryCopy(from, to, num2);
					}
				}
				writer.bufferIndex += num2;
			}
			else
			{
				for (int i = 0; i < array.Length; i++)
				{
					writer.UNSAFE_WriteToBuffer_8_UInt64(array[i]);
				}
			}
			return;
		}
		writer.FlushToStream();
		using Buffer<byte> buffer = Buffer<byte>.Claim(num2);
		if (BitConverter.IsLittleEndian)
		{
			UnsafeUtilities.MemoryCopy(array, buffer.Array, num2, 0, 0);
		}
		else
		{
			byte[] array2 = buffer.Array;
			for (int j = 0; j < array.Length; j++)
			{
				ProperBitConverter.GetBytes(array2, j * num, array[j]);
			}
		}
		writer.Stream.Write(buffer.Array, 0, num2);
	}

	private unsafe static void WritePrimitiveArray_decimal(BinaryDataWriter writer, object o)
	{
		decimal[] array = o as decimal[];
		int num = 16;
		int num2 = array.Length * num;
		writer.EnsureBufferSpace(9);
		writer.buffer[writer.bufferIndex++] = 8;
		writer.UNSAFE_WriteToBuffer_4_Int32(array.Length);
		writer.UNSAFE_WriteToBuffer_4_Int32(num);
		if (writer.TryEnsureBufferSpace(num2))
		{
			if (BitConverter.IsLittleEndian)
			{
				fixed (byte* ptr2 = writer.buffer)
				{
					fixed (decimal* ptr = array)
					{
						void* from = ptr;
						void* to = ptr2 + writer.bufferIndex;
						UnsafeUtilities.MemoryCopy(from, to, num2);
					}
				}
				writer.bufferIndex += num2;
			}
			else
			{
				for (int i = 0; i < array.Length; i++)
				{
					writer.UNSAFE_WriteToBuffer_16_Decimal(array[i]);
				}
			}
			return;
		}
		writer.FlushToStream();
		using Buffer<byte> buffer = Buffer<byte>.Claim(num2);
		if (BitConverter.IsLittleEndian)
		{
			UnsafeUtilities.MemoryCopy(array, buffer.Array, num2, 0, 0);
		}
		else
		{
			byte[] array2 = buffer.Array;
			for (int j = 0; j < array.Length; j++)
			{
				ProperBitConverter.GetBytes(array2, j * num, array[j]);
			}
		}
		writer.Stream.Write(buffer.Array, 0, num2);
	}

	private unsafe static void WritePrimitiveArray_float(BinaryDataWriter writer, object o)
	{
		float[] array = o as float[];
		int num = 4;
		int num2 = array.Length * num;
		writer.EnsureBufferSpace(9);
		writer.buffer[writer.bufferIndex++] = 8;
		writer.UNSAFE_WriteToBuffer_4_Int32(array.Length);
		writer.UNSAFE_WriteToBuffer_4_Int32(num);
		if (writer.TryEnsureBufferSpace(num2))
		{
			if (BitConverter.IsLittleEndian)
			{
				fixed (byte* ptr2 = writer.buffer)
				{
					fixed (float* ptr = array)
					{
						void* from = ptr;
						void* to = ptr2 + writer.bufferIndex;
						UnsafeUtilities.MemoryCopy(from, to, num2);
					}
				}
				writer.bufferIndex += num2;
			}
			else
			{
				for (int i = 0; i < array.Length; i++)
				{
					writer.UNSAFE_WriteToBuffer_4_Float32(array[i]);
				}
			}
			return;
		}
		writer.FlushToStream();
		using Buffer<byte> buffer = Buffer<byte>.Claim(num2);
		if (BitConverter.IsLittleEndian)
		{
			UnsafeUtilities.MemoryCopy(array, buffer.Array, num2, 0, 0);
		}
		else
		{
			byte[] array2 = buffer.Array;
			for (int j = 0; j < array.Length; j++)
			{
				ProperBitConverter.GetBytes(array2, j * num, array[j]);
			}
		}
		writer.Stream.Write(buffer.Array, 0, num2);
	}

	private unsafe static void WritePrimitiveArray_double(BinaryDataWriter writer, object o)
	{
		double[] array = o as double[];
		int num = 8;
		int num2 = array.Length * num;
		writer.EnsureBufferSpace(9);
		writer.buffer[writer.bufferIndex++] = 8;
		writer.UNSAFE_WriteToBuffer_4_Int32(array.Length);
		writer.UNSAFE_WriteToBuffer_4_Int32(num);
		if (writer.TryEnsureBufferSpace(num2))
		{
			if (BitConverter.IsLittleEndian)
			{
				fixed (byte* ptr2 = writer.buffer)
				{
					fixed (double* ptr = array)
					{
						void* from = ptr;
						void* to = ptr2 + writer.bufferIndex;
						UnsafeUtilities.MemoryCopy(from, to, num2);
					}
				}
				writer.bufferIndex += num2;
			}
			else
			{
				for (int i = 0; i < array.Length; i++)
				{
					writer.UNSAFE_WriteToBuffer_8_Float64(array[i]);
				}
			}
			return;
		}
		writer.FlushToStream();
		using Buffer<byte> buffer = Buffer<byte>.Claim(num2);
		if (BitConverter.IsLittleEndian)
		{
			UnsafeUtilities.MemoryCopy(array, buffer.Array, num2, 0, 0);
		}
		else
		{
			byte[] array2 = buffer.Array;
			for (int j = 0; j < array.Length; j++)
			{
				ProperBitConverter.GetBytes(array2, j * num, array[j]);
			}
		}
		writer.Stream.Write(buffer.Array, 0, num2);
	}

	private unsafe static void WritePrimitiveArray_Guid(BinaryDataWriter writer, object o)
	{
		Guid[] array = o as Guid[];
		int num = sizeof(Guid);
		int num2 = array.Length * num;
		writer.EnsureBufferSpace(9);
		writer.buffer[writer.bufferIndex++] = 8;
		writer.UNSAFE_WriteToBuffer_4_Int32(array.Length);
		writer.UNSAFE_WriteToBuffer_4_Int32(num);
		if (writer.TryEnsureBufferSpace(num2))
		{
			if (BitConverter.IsLittleEndian)
			{
				fixed (byte* ptr2 = writer.buffer)
				{
					fixed (Guid* ptr = array)
					{
						void* from = ptr;
						void* to = ptr2 + writer.bufferIndex;
						UnsafeUtilities.MemoryCopy(from, to, num2);
					}
				}
				writer.bufferIndex += num2;
			}
			else
			{
				for (int i = 0; i < array.Length; i++)
				{
					writer.UNSAFE_WriteToBuffer_16_Guid(array[i]);
				}
			}
			return;
		}
		writer.FlushToStream();
		using Buffer<byte> buffer = Buffer<byte>.Claim(num2);
		if (BitConverter.IsLittleEndian)
		{
			UnsafeUtilities.MemoryCopy(array, buffer.Array, num2, 0, 0);
		}
		else
		{
			byte[] array2 = buffer.Array;
			for (int j = 0; j < array.Length; j++)
			{
				ProperBitConverter.GetBytes(array2, j * num, array[j]);
			}
		}
		writer.Stream.Write(buffer.Array, 0, num2);
	}

	public override void WritePrimitiveArray<T>(T[] array)
	{
		if (!PrimitiveArrayWriters.TryGetValue(typeof(T), out var value))
		{
			throw new ArgumentException("Type " + typeof(T).Name + " is not a valid primitive array type.");
		}
		value(this, array);
	}

	public override void WriteBoolean(string name, bool value)
	{
		if (name != null)
		{
			EnsureBufferSpace(1);
			buffer[bufferIndex++] = 43;
			WriteStringFast(name);
			EnsureBufferSpace(1);
			buffer[bufferIndex++] = (byte)(value ? 1 : 0);
		}
		else
		{
			EnsureBufferSpace(2);
			buffer[bufferIndex++] = 44;
			buffer[bufferIndex++] = (byte)(value ? 1 : 0);
		}
	}

	public override void WriteByte(string name, byte value)
	{
		if (name != null)
		{
			EnsureBufferSpace(1);
			buffer[bufferIndex++] = 17;
			WriteStringFast(name);
			EnsureBufferSpace(1);
			buffer[bufferIndex++] = value;
		}
		else
		{
			EnsureBufferSpace(2);
			buffer[bufferIndex++] = 18;
			buffer[bufferIndex++] = value;
		}
	}

	public override void WriteChar(string name, char value)
	{
		if (name != null)
		{
			EnsureBufferSpace(1);
			buffer[bufferIndex++] = 37;
			WriteStringFast(name);
			EnsureBufferSpace(2);
			UNSAFE_WriteToBuffer_2_Char(value);
		}
		else
		{
			EnsureBufferSpace(3);
			buffer[bufferIndex++] = 38;
			UNSAFE_WriteToBuffer_2_Char(value);
		}
	}

	public override void WriteDecimal(string name, decimal value)
	{
		if (name != null)
		{
			EnsureBufferSpace(1);
			buffer[bufferIndex++] = 35;
			WriteStringFast(name);
			EnsureBufferSpace(16);
			UNSAFE_WriteToBuffer_16_Decimal(value);
		}
		else
		{
			EnsureBufferSpace(17);
			buffer[bufferIndex++] = 36;
			UNSAFE_WriteToBuffer_16_Decimal(value);
		}
	}

	public override void WriteDouble(string name, double value)
	{
		if (name != null)
		{
			EnsureBufferSpace(1);
			buffer[bufferIndex++] = 33;
			WriteStringFast(name);
			EnsureBufferSpace(8);
			UNSAFE_WriteToBuffer_8_Float64(value);
		}
		else
		{
			EnsureBufferSpace(9);
			buffer[bufferIndex++] = 34;
			UNSAFE_WriteToBuffer_8_Float64(value);
		}
	}

	public override void WriteGuid(string name, Guid value)
	{
		if (name != null)
		{
			EnsureBufferSpace(1);
			buffer[bufferIndex++] = 41;
			WriteStringFast(name);
			EnsureBufferSpace(16);
			UNSAFE_WriteToBuffer_16_Guid(value);
		}
		else
		{
			EnsureBufferSpace(17);
			buffer[bufferIndex++] = 42;
			UNSAFE_WriteToBuffer_16_Guid(value);
		}
	}

	public override void WriteExternalReference(string name, Guid guid)
	{
		if (name != null)
		{
			EnsureBufferSpace(1);
			buffer[bufferIndex++] = 13;
			WriteStringFast(name);
			EnsureBufferSpace(16);
			UNSAFE_WriteToBuffer_16_Guid(guid);
		}
		else
		{
			EnsureBufferSpace(17);
			buffer[bufferIndex++] = 14;
			UNSAFE_WriteToBuffer_16_Guid(guid);
		}
	}

	public override void WriteExternalReference(string name, int index)
	{
		if (name != null)
		{
			EnsureBufferSpace(1);
			buffer[bufferIndex++] = 11;
			WriteStringFast(name);
			EnsureBufferSpace(4);
			UNSAFE_WriteToBuffer_4_Int32(index);
		}
		else
		{
			EnsureBufferSpace(5);
			buffer[bufferIndex++] = 12;
			UNSAFE_WriteToBuffer_4_Int32(index);
		}
	}

	public override void WriteExternalReference(string name, string id)
	{
		if (id == null)
		{
			throw new ArgumentNullException("id");
		}
		if (name != null)
		{
			EnsureBufferSpace(1);
			buffer[bufferIndex++] = 50;
			WriteStringFast(name);
		}
		else
		{
			EnsureBufferSpace(1);
			buffer[bufferIndex++] = 51;
		}
		WriteStringFast(id);
	}

	public override void WriteInt32(string name, int value)
	{
		if (name != null)
		{
			EnsureBufferSpace(1);
			buffer[bufferIndex++] = 23;
			WriteStringFast(name);
			EnsureBufferSpace(4);
			UNSAFE_WriteToBuffer_4_Int32(value);
		}
		else
		{
			EnsureBufferSpace(5);
			buffer[bufferIndex++] = 24;
			UNSAFE_WriteToBuffer_4_Int32(value);
		}
	}

	public override void WriteInt64(string name, long value)
	{
		if (name != null)
		{
			EnsureBufferSpace(1);
			buffer[bufferIndex++] = 27;
			WriteStringFast(name);
			EnsureBufferSpace(8);
			UNSAFE_WriteToBuffer_8_Int64(value);
		}
		else
		{
			EnsureBufferSpace(9);
			buffer[bufferIndex++] = 28;
			UNSAFE_WriteToBuffer_8_Int64(value);
		}
	}

	public override void WriteNull(string name)
	{
		if (name != null)
		{
			EnsureBufferSpace(1);
			buffer[bufferIndex++] = 45;
			WriteStringFast(name);
		}
		else
		{
			EnsureBufferSpace(1);
			buffer[bufferIndex++] = 46;
		}
	}

	public override void WriteInternalReference(string name, int id)
	{
		if (name != null)
		{
			EnsureBufferSpace(1);
			buffer[bufferIndex++] = 9;
			WriteStringFast(name);
			EnsureBufferSpace(4);
			UNSAFE_WriteToBuffer_4_Int32(id);
		}
		else
		{
			EnsureBufferSpace(5);
			buffer[bufferIndex++] = 10;
			UNSAFE_WriteToBuffer_4_Int32(id);
		}
	}

	public override void WriteSByte(string name, sbyte value)
	{
		if (name != null)
		{
			EnsureBufferSpace(1);
			buffer[bufferIndex++] = 15;
			WriteStringFast(name);
			EnsureBufferSpace(1);
			buffer[bufferIndex++] = (byte)value;
		}
		else
		{
			EnsureBufferSpace(2);
			buffer[bufferIndex++] = 16;
			buffer[bufferIndex++] = (byte)value;
		}
	}

	public override void WriteInt16(string name, short value)
	{
		if (name != null)
		{
			EnsureBufferSpace(1);
			buffer[bufferIndex++] = 19;
			WriteStringFast(name);
			EnsureBufferSpace(2);
			UNSAFE_WriteToBuffer_2_Int16(value);
		}
		else
		{
			EnsureBufferSpace(3);
			buffer[bufferIndex++] = 20;
			UNSAFE_WriteToBuffer_2_Int16(value);
		}
	}

	public override void WriteSingle(string name, float value)
	{
		if (name != null)
		{
			EnsureBufferSpace(1);
			buffer[bufferIndex++] = 31;
			WriteStringFast(name);
			EnsureBufferSpace(4);
			UNSAFE_WriteToBuffer_4_Float32(value);
		}
		else
		{
			EnsureBufferSpace(5);
			buffer[bufferIndex++] = 32;
			UNSAFE_WriteToBuffer_4_Float32(value);
		}
	}

	public override void WriteString(string name, string value)
	{
		if (name != null)
		{
			EnsureBufferSpace(1);
			buffer[bufferIndex++] = 39;
			WriteStringFast(name);
		}
		else
		{
			EnsureBufferSpace(1);
			buffer[bufferIndex++] = 40;
		}
		WriteStringFast(value);
	}

	public override void WriteUInt32(string name, uint value)
	{
		if (name != null)
		{
			EnsureBufferSpace(1);
			buffer[bufferIndex++] = 25;
			WriteStringFast(name);
			EnsureBufferSpace(4);
			UNSAFE_WriteToBuffer_4_UInt32(value);
		}
		else
		{
			EnsureBufferSpace(5);
			buffer[bufferIndex++] = 26;
			UNSAFE_WriteToBuffer_4_UInt32(value);
		}
	}

	public override void WriteUInt64(string name, ulong value)
	{
		if (name != null)
		{
			EnsureBufferSpace(1);
			buffer[bufferIndex++] = 29;
			WriteStringFast(name);
			EnsureBufferSpace(8);
			UNSAFE_WriteToBuffer_8_UInt64(value);
		}
		else
		{
			EnsureBufferSpace(9);
			buffer[bufferIndex++] = 30;
			UNSAFE_WriteToBuffer_8_UInt64(value);
		}
	}

	public override void WriteUInt16(string name, ushort value)
	{
		if (name != null)
		{
			EnsureBufferSpace(1);
			buffer[bufferIndex++] = 21;
			WriteStringFast(name);
			EnsureBufferSpace(2);
			UNSAFE_WriteToBuffer_2_UInt16(value);
		}
		else
		{
			EnsureBufferSpace(3);
			buffer[bufferIndex++] = 22;
			UNSAFE_WriteToBuffer_2_UInt16(value);
		}
	}

	public override void PrepareNewSerializationSession()
	{
		base.PrepareNewSerializationSession();
		types.Clear();
		bufferIndex = 0;
	}

	public override string GetDataDump()
	{
		if (!Stream.CanRead)
		{
			return "Binary data stream for writing cannot be read; cannot dump data.";
		}
		if (!Stream.CanSeek)
		{
			return "Binary data stream cannot seek; cannot dump data.";
		}
		FlushToStream();
		long position = Stream.Position;
		byte[] bytes = new byte[position];
		Stream.Position = 0L;
		Stream.Read(bytes, 0, (int)position);
		Stream.Position = position;
		return "Binary hex dump: " + ProperBitConverter.BytesToHexString(bytes);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void WriteType(Type type)
	{
		if (type == null)
		{
			EnsureBufferSpace(1);
			buffer[bufferIndex++] = 46;
			return;
		}
		if (types.TryGetValue(type, out var value))
		{
			EnsureBufferSpace(5);
			buffer[bufferIndex++] = 48;
			UNSAFE_WriteToBuffer_4_Int32(value);
			return;
		}
		value = types.Count;
		types.Add(type, value);
		EnsureBufferSpace(5);
		buffer[bufferIndex++] = 47;
		UNSAFE_WriteToBuffer_4_Int32(value);
		WriteStringFast(base.Context.Binder.BindToName(type, base.Context.Config.DebugContext));
	}

	private unsafe void WriteStringFast(string value)
	{
		bool flag = true;
		if (CompressStringsTo8BitWhenPossible)
		{
			flag = false;
			for (int i = 0; i < value.Length; i++)
			{
				if (value[i] > 'ÿ')
				{
					flag = true;
					break;
				}
			}
		}
		int num;
		if (flag)
		{
			num = value.Length * 2;
			if (TryEnsureBufferSpace(num + 5))
			{
				this.buffer[bufferIndex++] = 1;
				UNSAFE_WriteToBuffer_4_Int32(value.Length);
				if (BitConverter.IsLittleEndian)
				{
					fixed (byte* ptr = this.buffer)
					{
						fixed (char* ptr3 = value)
						{
							Struct256Bit* ptr2 = (Struct256Bit*)(ptr + bufferIndex);
							Struct256Bit* ptr4 = (Struct256Bit*)ptr3;
							byte* ptr5 = (byte*)ptr2 + num;
							while (ptr2 + 1 <= ptr5)
							{
								*(ptr2++) = *(ptr4++);
							}
							char* ptr6 = (char*)ptr2;
							char* ptr7 = (char*)ptr4;
							while (ptr6 < ptr5)
							{
								*(ptr6++) = *(ptr7++);
							}
						}
					}
				}
				else
				{
					fixed (byte* ptr8 = this.buffer)
					{
						fixed (char* ptr10 = value)
						{
							byte* ptr9 = ptr8 + bufferIndex;
							byte* ptr11 = (byte*)ptr10;
							for (int j = 0; j < num; j += 2)
							{
								*ptr9 = ptr11[1];
								ptr9[1] = *ptr11;
								ptr11 += 2;
								ptr9 += 2;
							}
						}
					}
				}
				bufferIndex += num;
				return;
			}
			FlushToStream();
			Stream.WriteByte(1);
			ProperBitConverter.GetBytes(small_buffer, 0, value.Length);
			Stream.Write(small_buffer, 0, 4);
			using Buffer<byte> buffer = Buffer<byte>.Claim(num);
			byte[] array = buffer.Array;
			UnsafeUtilities.StringToBytes(array, value, needs16BitSupport: true);
			Stream.Write(array, 0, num);
			return;
		}
		num = value.Length;
		if (TryEnsureBufferSpace(num + 5))
		{
			this.buffer[bufferIndex++] = 0;
			UNSAFE_WriteToBuffer_4_Int32(value.Length);
			for (int k = 0; k < num; k++)
			{
				this.buffer[bufferIndex++] = (byte)value[k];
			}
			return;
		}
		FlushToStream();
		Stream.WriteByte(0);
		ProperBitConverter.GetBytes(small_buffer, 0, value.Length);
		Stream.Write(small_buffer, 0, 4);
		using Buffer<byte> buffer2 = Buffer<byte>.Claim(value.Length);
		byte[] array2 = buffer2.Array;
		for (int l = 0; l < value.Length; l++)
		{
			array2[l] = (byte)value[l];
		}
		Stream.Write(array2, 0, value.Length);
	}

	public override void FlushToStream()
	{
		if (bufferIndex > 0)
		{
			Stream.Write(buffer, 0, bufferIndex);
			bufferIndex = 0;
		}
		base.FlushToStream();
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private unsafe void UNSAFE_WriteToBuffer_2_Char(char value)
	{
		fixed (byte* ptr = buffer)
		{
			if (BitConverter.IsLittleEndian)
			{
				*(char*)(ptr + bufferIndex) = value;
			}
			else
			{
				byte* ptr2 = ptr + bufferIndex;
				byte* ptr3 = (byte*)(&value) + 1;
				*(ptr2++) = *(ptr3--);
				*ptr2 = *ptr3;
			}
		}
		bufferIndex += 2;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private unsafe void UNSAFE_WriteToBuffer_2_Int16(short value)
	{
		fixed (byte* ptr = buffer)
		{
			if (BitConverter.IsLittleEndian)
			{
				*(short*)(ptr + bufferIndex) = value;
			}
			else
			{
				byte* ptr2 = ptr + bufferIndex;
				byte* ptr3 = (byte*)(&value) + 1;
				*(ptr2++) = *(ptr3--);
				*ptr2 = *ptr3;
			}
		}
		bufferIndex += 2;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private unsafe void UNSAFE_WriteToBuffer_2_UInt16(ushort value)
	{
		fixed (byte* ptr = buffer)
		{
			if (BitConverter.IsLittleEndian)
			{
				*(ushort*)(ptr + bufferIndex) = value;
			}
			else
			{
				byte* ptr2 = ptr + bufferIndex;
				byte* ptr3 = (byte*)(&value) + 1;
				*(ptr2++) = *(ptr3--);
				*ptr2 = *ptr3;
			}
		}
		bufferIndex += 2;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private unsafe void UNSAFE_WriteToBuffer_4_Int32(int value)
	{
		fixed (byte* ptr = buffer)
		{
			if (BitConverter.IsLittleEndian)
			{
				*(int*)(ptr + bufferIndex) = value;
			}
			else
			{
				byte* ptr2 = ptr + bufferIndex;
				byte* ptr3 = (byte*)(&value) + 3;
				*(ptr2++) = *(ptr3--);
				*(ptr2++) = *(ptr3--);
				*(ptr2++) = *(ptr3--);
				*ptr2 = *ptr3;
			}
		}
		bufferIndex += 4;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private unsafe void UNSAFE_WriteToBuffer_4_UInt32(uint value)
	{
		fixed (byte* ptr = buffer)
		{
			if (BitConverter.IsLittleEndian)
			{
				*(uint*)(ptr + bufferIndex) = value;
			}
			else
			{
				byte* ptr2 = ptr + bufferIndex;
				byte* ptr3 = (byte*)(&value) + 3;
				*(ptr2++) = *(ptr3--);
				*(ptr2++) = *(ptr3--);
				*(ptr2++) = *(ptr3--);
				*ptr2 = *ptr3;
			}
		}
		bufferIndex += 4;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private unsafe void UNSAFE_WriteToBuffer_4_Float32(float value)
	{
		fixed (byte* ptr = buffer)
		{
			if (BitConverter.IsLittleEndian)
			{
				if (ArchitectureInfo.Architecture_Supports_All_Unaligned_ReadWrites)
				{
					*(float*)(ptr + bufferIndex) = value;
				}
				else
				{
					byte* ptr2 = (byte*)(&value);
					byte* ptr3 = ptr + bufferIndex;
					*(ptr3++) = *(ptr2++);
					*(ptr3++) = *(ptr2++);
					*(ptr3++) = *(ptr2++);
					*ptr3 = *ptr2;
				}
			}
			else
			{
				byte* ptr4 = ptr + bufferIndex;
				byte* ptr5 = (byte*)(&value) + 3;
				*(ptr4++) = *(ptr5--);
				*(ptr4++) = *(ptr5--);
				*(ptr4++) = *(ptr5--);
				*ptr4 = *ptr5;
			}
		}
		bufferIndex += 4;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private unsafe void UNSAFE_WriteToBuffer_8_Int64(long value)
	{
		fixed (byte* ptr = buffer)
		{
			if (BitConverter.IsLittleEndian)
			{
				if (ArchitectureInfo.Architecture_Supports_All_Unaligned_ReadWrites)
				{
					*(long*)(ptr + bufferIndex) = value;
				}
				else
				{
					int* ptr2 = (int*)(&value);
					int* ptr3 = (int*)(ptr + bufferIndex);
					*(ptr3++) = *(ptr2++);
					*ptr3 = *ptr2;
				}
			}
			else
			{
				byte* ptr4 = ptr + bufferIndex;
				byte* ptr5 = (byte*)(&value) + 7;
				*(ptr4++) = *(ptr5--);
				*(ptr4++) = *(ptr5--);
				*(ptr4++) = *(ptr5--);
				*(ptr4++) = *(ptr5--);
				*(ptr4++) = *(ptr5--);
				*(ptr4++) = *(ptr5--);
				*(ptr4++) = *(ptr5--);
				*ptr4 = *ptr5;
			}
		}
		bufferIndex += 8;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private unsafe void UNSAFE_WriteToBuffer_8_UInt64(ulong value)
	{
		fixed (byte* ptr = buffer)
		{
			if (BitConverter.IsLittleEndian)
			{
				if (ArchitectureInfo.Architecture_Supports_All_Unaligned_ReadWrites)
				{
					*(ulong*)(ptr + bufferIndex) = value;
				}
				else
				{
					int* ptr2 = (int*)(&value);
					int* ptr3 = (int*)(ptr + bufferIndex);
					*(ptr3++) = *(ptr2++);
					*ptr3 = *ptr2;
				}
			}
			else
			{
				byte* ptr4 = ptr + bufferIndex;
				byte* ptr5 = (byte*)(&value) + 7;
				*(ptr4++) = *(ptr5--);
				*(ptr4++) = *(ptr5--);
				*(ptr4++) = *(ptr5--);
				*(ptr4++) = *(ptr5--);
				*(ptr4++) = *(ptr5--);
				*(ptr4++) = *(ptr5--);
				*(ptr4++) = *(ptr5--);
				*ptr4 = *ptr5;
			}
		}
		bufferIndex += 8;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private unsafe void UNSAFE_WriteToBuffer_8_Float64(double value)
	{
		fixed (byte* ptr = buffer)
		{
			if (BitConverter.IsLittleEndian)
			{
				if (ArchitectureInfo.Architecture_Supports_All_Unaligned_ReadWrites)
				{
					*(double*)(ptr + bufferIndex) = value;
				}
				else
				{
					int* ptr2 = (int*)(&value);
					int* ptr3 = (int*)(ptr + bufferIndex);
					*(ptr3++) = *(ptr2++);
					*ptr3 = *ptr2;
				}
			}
			else
			{
				byte* ptr4 = ptr + bufferIndex;
				byte* ptr5 = (byte*)(&value) + 7;
				*(ptr4++) = *(ptr5--);
				*(ptr4++) = *(ptr5--);
				*(ptr4++) = *(ptr5--);
				*(ptr4++) = *(ptr5--);
				*(ptr4++) = *(ptr5--);
				*(ptr4++) = *(ptr5--);
				*(ptr4++) = *(ptr5--);
				*ptr4 = *ptr5;
			}
		}
		bufferIndex += 8;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private unsafe void UNSAFE_WriteToBuffer_16_Decimal(decimal value)
	{
		fixed (byte* ptr = buffer)
		{
			if (BitConverter.IsLittleEndian)
			{
				if (ArchitectureInfo.Architecture_Supports_All_Unaligned_ReadWrites)
				{
					*(decimal*)(ptr + bufferIndex) = value;
				}
				else
				{
					int* ptr2 = (int*)(&value);
					int* ptr3 = (int*)(ptr + bufferIndex);
					*(ptr3++) = *(ptr2++);
					*(ptr3++) = *(ptr2++);
					*(ptr3++) = *(ptr2++);
					*ptr3 = *ptr2;
				}
			}
			else
			{
				byte* ptr4 = ptr + bufferIndex;
				byte* ptr5 = (byte*)(&value) + 15;
				*(ptr4++) = *(ptr5--);
				*(ptr4++) = *(ptr5--);
				*(ptr4++) = *(ptr5--);
				*(ptr4++) = *(ptr5--);
				*(ptr4++) = *(ptr5--);
				*(ptr4++) = *(ptr5--);
				*(ptr4++) = *(ptr5--);
				*(ptr4++) = *(ptr5--);
				*(ptr4++) = *(ptr5--);
				*(ptr4++) = *(ptr5--);
				*(ptr4++) = *(ptr5--);
				*(ptr4++) = *(ptr5--);
				*(ptr4++) = *(ptr5--);
				*(ptr4++) = *(ptr5--);
				*(ptr4++) = *(ptr5--);
				*ptr4 = *ptr5;
			}
		}
		bufferIndex += 16;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private unsafe void UNSAFE_WriteToBuffer_16_Guid(Guid value)
	{
		fixed (byte* ptr = buffer)
		{
			if (BitConverter.IsLittleEndian)
			{
				if (ArchitectureInfo.Architecture_Supports_All_Unaligned_ReadWrites)
				{
					*(Guid*)(ptr + bufferIndex) = value;
				}
				else
				{
					int* ptr2 = (int*)(&value);
					int* ptr3 = (int*)(ptr + bufferIndex);
					*(ptr3++) = *(ptr2++);
					*(ptr3++) = *(ptr2++);
					*(ptr3++) = *(ptr2++);
					*ptr3 = *ptr2;
				}
			}
			else
			{
				byte* ptr4 = ptr + bufferIndex;
				byte* ptr5 = (byte*)(&value);
				*(ptr4++) = *(ptr5++);
				*(ptr4++) = *(ptr5++);
				*(ptr4++) = *(ptr5++);
				*(ptr4++) = *(ptr5++);
				*(ptr4++) = *(ptr5++);
				*(ptr4++) = *(ptr5++);
				*(ptr4++) = *(ptr5++);
				*(ptr4++) = *(ptr5++);
				*(ptr4++) = *(ptr5++);
				*(ptr4++) = *ptr5;
				ptr5 += 6;
				*(ptr4++) = *(ptr5--);
				*(ptr4++) = *(ptr5--);
				*(ptr4++) = *(ptr5--);
				*(ptr4++) = *(ptr5--);
				*(ptr4++) = *(ptr5--);
				*ptr4 = *ptr5;
			}
		}
		bufferIndex += 16;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void EnsureBufferSpace(int space)
	{
		int num = buffer.Length;
		if (space > num)
		{
			throw new Exception("Insufficient buffer capacity");
		}
		if (bufferIndex + space > num)
		{
			FlushToStream();
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private bool TryEnsureBufferSpace(int space)
	{
		int num = buffer.Length;
		if (space > num)
		{
			return false;
		}
		if (bufferIndex + space > num)
		{
			FlushToStream();
		}
		return true;
	}
}
