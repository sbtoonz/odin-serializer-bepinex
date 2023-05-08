using System;
using System.Runtime.InteropServices;

namespace Sirenix.OdinSerializer.Utilities.Unsafe;

public static class UnsafeUtilities
{
	private struct Struct256Bit
	{
		public decimal d1;

		public decimal d2;
	}

	public static T[] StructArrayFromBytes<T>(byte[] bytes, int byteLength) where T : struct
	{
		return StructArrayFromBytes<T>(bytes, 0, 0);
	}

	public static T[] StructArrayFromBytes<T>(byte[] bytes, int byteLength, int byteOffset) where T : struct
	{
		if (bytes == null)
		{
			throw new ArgumentNullException("bytes");
		}
		if (byteLength <= 0)
		{
			throw new ArgumentException("Byte length must be larger than zero.");
		}
		if (byteOffset < 0)
		{
			throw new ArgumentException("Byte offset must be larger than or equal to zero.");
		}
		int num = Marshal.SizeOf(typeof(T));
		if (byteOffset % 8 != 0)
		{
			throw new ArgumentException("Byte offset must be divisible by " + 8 + " (IE, sizeof(ulong))");
		}
		if (byteLength + byteOffset >= bytes.Length)
		{
			throw new ArgumentException("Given byte array of size " + bytes.Length + " is not large enough to copy requested number of bytes " + byteLength + ".");
		}
		if ((byteLength - byteOffset) % num != 0)
		{
			throw new ArgumentException("The length in the given byte array (" + bytes.Length + ", and " + (bytes.Length - byteOffset) + " minus byteOffset " + byteOffset + ") to convert to type " + typeof(T).Name + " is not divisible by the size of " + typeof(T).Name + " (" + num + ").");
		}
		T[] array = new T[(bytes.Length - byteOffset) / num];
		MemoryCopy(bytes, array, byteLength, byteOffset, 0);
		return array;
	}

	public static byte[] StructArrayToBytes<T>(T[] array) where T : struct
	{
		byte[] bytes = null;
		return StructArrayToBytes(array, ref bytes, 0);
	}

	public static byte[] StructArrayToBytes<T>(T[] array, ref byte[] bytes, int byteOffset) where T : struct
	{
		if (array == null)
		{
			throw new ArgumentNullException("array");
		}
		if (byteOffset < 0)
		{
			throw new ArgumentException("Byte offset must be larger than or equal to zero.");
		}
		int num = Marshal.SizeOf(typeof(T)) * array.Length;
		if (bytes == null)
		{
			bytes = new byte[num + byteOffset];
		}
		else if (bytes.Length + byteOffset > num)
		{
			throw new ArgumentException("Byte array must be at least " + (bytes.Length + byteOffset) + " long with the given byteOffset.");
		}
		MemoryCopy(array, bytes, num, 0, byteOffset);
		return bytes;
	}

	public unsafe static string StringFromBytes(byte[] buffer, int charLength, bool needs16BitSupport)
	{
		int num = (needs16BitSupport ? (charLength * 2) : charLength);
		if (buffer.Length < num)
		{
			throw new ArgumentException("Buffer is not large enough to contain the given string; a size of at least " + num + " is required.");
		}
		GCHandle gCHandle = default(GCHandle);
		string text = new string(' ', charLength);
		try
		{
			gCHandle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
			if (needs16BitSupport)
			{
				if (BitConverter.IsLittleEndian)
				{
					fixed (char* ptr2 = text)
					{
						ushort* ptr = (ushort*)gCHandle.AddrOfPinnedObject().ToPointer();
						ushort* ptr3 = (ushort*)ptr2;
						for (int i = 0; i < num; i += 2)
						{
							*(ptr3++) = *(ptr++);
						}
						return text;
					}
				}
				fixed (char* ptr5 = text)
				{
					byte* ptr4 = (byte*)gCHandle.AddrOfPinnedObject().ToPointer();
					byte* ptr6 = (byte*)ptr5;
					for (int j = 0; j < num; j += 2)
					{
						*ptr6 = ptr4[1];
						ptr6[1] = *ptr4;
						ptr4 += 2;
						ptr6 += 2;
					}
					return text;
				}
			}
			if (BitConverter.IsLittleEndian)
			{
				fixed (char* ptr8 = text)
				{
					byte* ptr7 = (byte*)gCHandle.AddrOfPinnedObject().ToPointer();
					byte* ptr9 = (byte*)ptr8;
					for (int k = 0; k < num; k++)
					{
						*(ptr9++) = *(ptr7++);
						ptr9++;
					}
					return text;
				}
			}
			fixed (char* ptr11 = text)
			{
				byte* ptr10 = (byte*)gCHandle.AddrOfPinnedObject().ToPointer();
				byte* ptr12 = (byte*)ptr11;
				for (int l = 0; l < num; l++)
				{
					ptr12++;
					*(ptr12++) = *(ptr10++);
				}
				return text;
			}
		}
		finally
		{
			if (gCHandle.IsAllocated)
			{
				gCHandle.Free();
			}
		}
	}

	public unsafe static int StringToBytes(byte[] buffer, string value, bool needs16BitSupport)
	{
		int num = (needs16BitSupport ? (value.Length * 2) : value.Length);
		if (buffer.Length < num)
		{
			throw new ArgumentException("Buffer is not large enough to contain the given string; a size of at least " + num + " is required.");
		}
		GCHandle gCHandle = default(GCHandle);
		try
		{
			gCHandle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
			if (needs16BitSupport)
			{
				if (BitConverter.IsLittleEndian)
				{
					fixed (char* ptr = value)
					{
						ushort* ptr2 = (ushort*)ptr;
						ushort* ptr3 = (ushort*)gCHandle.AddrOfPinnedObject().ToPointer();
						for (int i = 0; i < num; i += 2)
						{
							*(ptr3++) = *(ptr2++);
						}
						return num;
					}
				}
				fixed (char* ptr4 = value)
				{
					byte* ptr5 = (byte*)ptr4;
					byte* ptr6 = (byte*)gCHandle.AddrOfPinnedObject().ToPointer();
					for (int j = 0; j < num; j += 2)
					{
						*ptr6 = ptr5[1];
						ptr6[1] = *ptr5;
						ptr5 += 2;
						ptr6 += 2;
					}
					return num;
				}
			}
			if (BitConverter.IsLittleEndian)
			{
				fixed (char* ptr7 = value)
				{
					byte* ptr8 = (byte*)ptr7;
					byte* ptr9 = (byte*)gCHandle.AddrOfPinnedObject().ToPointer();
					for (int k = 0; k < num; k++)
					{
						ptr8++;
						*(ptr9++) = *(ptr8++);
					}
					return num;
				}
			}
			fixed (char* ptr10 = value)
			{
				byte* ptr11 = (byte*)ptr10;
				byte* ptr12 = (byte*)gCHandle.AddrOfPinnedObject().ToPointer();
				for (int l = 0; l < num; l++)
				{
					*(ptr12++) = *(ptr11++);
					ptr11++;
				}
				return num;
			}
		}
		finally
		{
			if (gCHandle.IsAllocated)
			{
				gCHandle.Free();
			}
		}
	}

	public unsafe static void MemoryCopy(void* from, void* to, int bytes)
	{
		byte* ptr = (byte*)to + bytes;
		Struct256Bit* ptr2 = (Struct256Bit*)from;
		Struct256Bit* ptr3 = (Struct256Bit*)to;
		while (ptr3 + 1 <= ptr)
		{
			*(ptr3++) = *(ptr2++);
		}
		byte* ptr4 = (byte*)ptr2;
		byte* ptr5 = (byte*)ptr3;
		while (ptr5 < ptr)
		{
			*(ptr5++) = *(ptr4++);
		}
	}

	public unsafe static void MemoryCopy(object from, object to, int byteCount, int fromByteOffset, int toByteOffset)
	{
		GCHandle gCHandle = default(GCHandle);
		GCHandle gCHandle2 = default(GCHandle);
		if (fromByteOffset % 8 != 0 || toByteOffset % 8 != 0)
		{
			throw new ArgumentException("Byte offset must be divisible by " + 8 + " (IE, sizeof(ulong))");
		}
		try
		{
			int num = byteCount % 8;
			int num2 = (byteCount - num) / 8;
			int num3 = fromByteOffset / 8;
			int num4 = toByteOffset / 8;
			gCHandle = GCHandle.Alloc(from, GCHandleType.Pinned);
			gCHandle2 = GCHandle.Alloc(to, GCHandleType.Pinned);
			ulong* ptr = (ulong*)gCHandle.AddrOfPinnedObject().ToPointer();
			ulong* ptr2 = (ulong*)gCHandle2.AddrOfPinnedObject().ToPointer();
			if (num3 > 0)
			{
				ptr += num3;
			}
			if (num4 > 0)
			{
				ptr2 += num4;
			}
			for (int i = 0; i < num2; i++)
			{
				*(ptr2++) = *(ptr++);
			}
			if (num > 0)
			{
				byte* ptr3 = (byte*)ptr;
				byte* ptr4 = (byte*)ptr2;
				for (int j = 0; j < num; j++)
				{
					*(ptr4++) = *(ptr3++);
				}
			}
		}
		finally
		{
			if (gCHandle.IsAllocated)
			{
				gCHandle.Free();
			}
			if (gCHandle2.IsAllocated)
			{
				gCHandle2.Free();
			}
		}
	}
}
