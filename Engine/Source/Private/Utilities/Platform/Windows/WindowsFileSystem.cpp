// Copyright (c) CreationArt. All Rights Reserved.
// The original code is taken from TheCherno's HazelEngine at
// https://github.com/StudioCherno/Hazel/blob/2024.1/Hazel/Platform/Windows/WindowsFileSystem.cpp.

#include "stdafx.h"
#include "Utilities/FileSystem.h"

#include <ShlObj.h>

#include <cstdlib>
#include <filesystem>

#ifdef MORTARENGINE_PLATFORM_WINDOWS

namespace
{
	std::filesystem::path s_PersistentStoragePath;
}

FileStatus FileSystem::TryOpenFile(const std::filesystem::path& filepath)
{
	HANDLE fileHandle = CreateFileW(filepath.c_str(), GENERIC_READ, 0, nullptr, OPEN_EXISTING, 0, nullptr);
	if (fileHandle == INVALID_HANDLE_VALUE)
	{
		const DWORD errorCode = GetLastError();
		if (errorCode == ERROR_FILE_NOT_FOUND || errorCode == ERROR_PATH_NOT_FOUND)
			return FileStatus::Invalid;
		if (errorCode == ERROR_SHARING_VIOLATION)
			return FileStatus::Locked;

		return FileStatus::OtherError;
	}

	CloseHandle(fileHandle);
	return FileStatus::Success;
}

std::filesystem::path FileSystem::GetPersistentStoragePath()
{
	if (!s_PersistentStoragePath.empty())
		return s_PersistentStoragePath;

	PWSTR roamingFilePath = nullptr;
	const HRESULT result = SHGetKnownFolderPath(FOLDERID_RoamingAppData, KF_FLAG_DEFAULT, nullptr, &roamingFilePath);
	if (FAILED(result) || !roamingFilePath)
		return {};

	s_PersistentStoragePath = roamingFilePath;
	CoTaskMemFree(roamingFilePath);

	s_PersistentStoragePath /= "MortarEngine";

	if (!std::filesystem::exists(s_PersistentStoragePath))
		std::filesystem::create_directories(s_PersistentStoragePath);

	return s_PersistentStoragePath;
}

bool FileSystem::HasEnvironmentVariable(const std::string& key)
{
	HKEY hKey = nullptr;
	LSTATUS lOpenStatus = RegOpenKeyExA(HKEY_CURRENT_USER, "Environment", 0, KEY_READ, &hKey);

	if (lOpenStatus == ERROR_SUCCESS)
	{
		lOpenStatus = RegQueryValueExA(hKey, key.c_str(), nullptr, nullptr, nullptr, nullptr);
		RegCloseKey(hKey);
	}

	return lOpenStatus == ERROR_SUCCESS;
}

bool FileSystem::SetEnvironmentVariable(const std::string& key, const std::string& value)
{
	HKEY hKey = nullptr;
	DWORD createdNewKey = 0;
	LSTATUS lOpenStatus = RegCreateKeyExA(HKEY_CURRENT_USER, "Environment", 0, nullptr, REG_OPTION_NON_VOLATILE, KEY_SET_VALUE, nullptr, &hKey, &createdNewKey);
	if (lOpenStatus == ERROR_SUCCESS)
	{
		LSTATUS lSetStatus = RegSetValueExA(hKey, key.c_str(), 0, REG_SZ, reinterpret_cast<const BYTE*>(value.c_str()), static_cast<DWORD>(value.length() + 1));
		RegCloseKey(hKey);

		if (lSetStatus == ERROR_SUCCESS)
		{
			SendMessageTimeoutA(HWND_BROADCAST, WM_SETTINGCHANGE, 0, reinterpret_cast<LPARAM>("Environment"), SMTO_BLOCK, 100, nullptr);
			return true;
		}
	}

	return false;
}

std::string FileSystem::GetEnvironmentVariable(const std::string& key)
{
	char* value = nullptr;
	size_t valueLength = 0;
	if (_dupenv_s(&value, &valueLength, key.c_str()) == 0 && value)
	{
		std::string result(value);
		std::free(value);
		return result;
	}

	return {};
}

#endif // MORTARENGINE_PLATFORM_WINDOWS
