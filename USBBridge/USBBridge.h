// USBBridge.h

#pragma once

#include <windows.h>
#include <winusb.h>

using namespace System;
using namespace System::IO;

namespace USBBridge
{
	public ref class WinUSBDevice
	{
		HANDLE hDevice;
		WINUSB_INTERFACE_HANDLE hUsbDevice;
	private:
		static HRESULT OpenDevice(const GUID *lpGuid, HANDLE% hDevice, WINUSB_INTERFACE_HANDLE% hUsbDevice);
		static HRESULT CloseDevice(HANDLE hDevice, WINUSB_INTERFACE_HANDLE hUsbDevice);
	public:
		WinUSBDevice(Guid^ guid);
		~WinUSBDevice();
		UInt32 ReadEndpoint(Byte endpoint, array<Byte>^ buffer, UInt32 length);
		UInt32 ControlTransfer(Byte requestType, Byte request, UInt16 value, UInt16 index, UInt16 length, array<Byte>^ buffer, UInt32 transferlength);
	};
}
