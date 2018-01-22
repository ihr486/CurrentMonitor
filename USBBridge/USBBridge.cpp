// これは メイン DLL ファイルです。

#include "stdafx.h"

#include "USBBridge.h"

#pragma comment(lib, "setupapi.lib")
#pragma comment(lib, "winusb.lib")

namespace USBBridge
{
WinUSBDevice::WinUSBDevice(Guid^ guid)
{
	array<Byte>^ bGuidData = guid->ToByteArray();
	pin_ptr<Byte> ppGuidData = &(bGuidData[0]);
	HRESULT hr;
	hr = OpenDevice((const GUID *)ppGuidData, this->hDevice, this->hUsbDevice);
	if (hr != S_OK)
		throw gcnew IOException(L"Failed to open WinUSB device.");
}

WinUSBDevice::~WinUSBDevice()
{
	CloseDevice(hDevice, hUsbDevice);
}

UInt32 WinUSBDevice::ReadEndpoint(Byte endpoint, array<Byte>^ buffer, UInt32 length)
{
	pin_ptr<Byte> ppszBuffer = &(buffer[0]);
	ULONG dwLengthTransferred;
	if (!WinUsb_ReadPipe(hUsbDevice, endpoint, ppszBuffer, length, &dwLengthTransferred, NULL))
	{
		throw gcnew IOException(L"Failed to read from WinUSB device.");
	}
	return dwLengthTransferred;
}

UInt32 WinUSBDevice::ControlTransfer(Byte requestType, Byte request, UInt16 value, UInt16 index, UInt16 length, array<Byte>^ buffer, UInt32 transferlength)
{
	pin_ptr<Byte> ppszBuffer = &(buffer[0]);
	ULONG dwLengthTransferred;
	WINUSB_SETUP_PACKET setup_data;
	setup_data.RequestType = requestType;
	setup_data.Request = request;
	setup_data.Value = value;
	setup_data.Index = index;
	setup_data.Length = length;
	if (!WinUsb_ControlTransfer(hUsbDevice, setup_data, ppszBuffer, transferlength, &dwLengthTransferred, NULL))
	{
		throw gcnew IOException(L"Failed to perform a control transfer.");
	}
	return dwLengthTransferred;
}
	
HRESULT WinUSBDevice::CloseDevice(HANDLE hDevice, WINUSB_INTERFACE_HANDLE hUsbDevice)
{
	CloseHandle(hDevice);
	WinUsb_Free(hUsbDevice);
	return S_OK;
}

HRESULT WinUSBDevice::OpenDevice(const GUID *lpGuid, HANDLE% hDeviceOut, WINUSB_INTERFACE_HANDLE% hUsbDeviceOut)
{
	HDEVINFO hDevInfo = SetupDiGetClassDevs(lpGuid, NULL, NULL, DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);
	if (hDevInfo == INVALID_HANDLE_VALUE)
	{
		return HRESULT_FROM_WIN32(GetLastError());
	}

	SP_DEVICE_INTERFACE_DATA did;

	did.cbSize = sizeof(SP_DEVICE_INTERFACE_DATA);
	if (!SetupDiEnumDeviceInterfaces(hDevInfo, NULL, lpGuid, 0, &did))
	{
		SetupDiDestroyDeviceInfoList(hDevInfo);
		return HRESULT_FROM_WIN32(GetLastError());
	}

	DWORD requiredlength;
	if (!SetupDiGetDeviceInterfaceDetail(hDevInfo, &did, NULL, 0, &requiredlength, NULL))
	{
		if (GetLastError() != ERROR_INSUFFICIENT_BUFFER)
		{
			SetupDiDestroyDeviceInfoList(hDevInfo);
			return HRESULT_FROM_WIN32(GetLastError());
		}
	}

	PSP_DEVICE_INTERFACE_DETAIL_DATA pdidd;
	pdidd = (PSP_DEVICE_INTERFACE_DETAIL_DATA)LocalAlloc(LMEM_FIXED, requiredlength);

	pdidd->cbSize = sizeof(SP_DEVICE_INTERFACE_DETAIL_DATA);

	if (!SetupDiGetDeviceInterfaceDetail(hDevInfo, &did, pdidd, requiredlength, &requiredlength, NULL))
	{
		SetupDiDestroyDeviceInfoList(hDevInfo);
		LocalFree(pdidd);
		return HRESULT_FROM_WIN32(GetLastError());
	}

	SetupDiDestroyDeviceInfoList(hDevInfo);

	HANDLE hDevice = CreateFile(pdidd->DevicePath, GENERIC_WRITE | GENERIC_READ, FILE_SHARE_WRITE | FILE_SHARE_READ, NULL, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL | FILE_FLAG_OVERLAPPED, NULL);
	if (hDevice == INVALID_HANDLE_VALUE)
	{
		LocalFree(pdidd);
		return HRESULT_FROM_WIN32(GetLastError());
	}

	LocalFree(pdidd);

	WINUSB_INTERFACE_HANDLE hUsbDevice;
	if (!WinUsb_Initialize(hDevice, &hUsbDevice))
	{
		CloseHandle(hDevice);
		return HRESULT_FROM_WIN32(GetLastError());
	}

	hDeviceOut = hDevice;
	hUsbDeviceOut = hUsbDevice;

	return S_OK;
}
}