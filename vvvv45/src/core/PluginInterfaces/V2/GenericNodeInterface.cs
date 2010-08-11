﻿using System;
using System.Runtime.InteropServices;
using VVVV.PluginInterfaces.V1;

namespace VVVV.PluginInterfaces.V2
{
	[Guid("31B5051D-09D5-45E7-BF67-4FEC1E28BF35"),
	 InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	public interface IGenericIO<T>: INodeIOBase
	{
		T GetSlice(int slice);
	}
	
	public class GenericIO<T> : IGenericIO<T>
	{
		public T GetSlice(int slice)
		{
			return default(T);
		}
	}

}