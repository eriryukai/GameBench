// Copyright (c) CreationArt. All Rights Reserved.

#pragma once

namespace GraphicUtility
{
	std::filesystem::path ExecutableDirectory()
	{
#if defined(MORTAR_PLATFORM_WINDOWS)
		std::wstring path(32768, L'\0');
		const DWORD length = GetModuleFileNameW(nullptr, path.data(), static_cast<DWORD>(path.size()));
		if (length == 0 || length == path.size())
			throw std::runtime_error("Failed to resolve executable directory for shader loading.");
		path.resize(length);
		return std::filesystem::path(path).parent_path();
#else
		return std::filesystem::current_path();
#endif
	}

	std::vector<uint8_t> ReadBinary(const std::filesystem::path& path)
	{
		std::ifstream file(path, std::ios::binary | std::ios::ate);
		if (!file)
			throw std::runtime_error("Failed to open shader: " + path.string());
		const std::streamsize size = file.tellg();
		if (size <= 0)
			throw std::runtime_error("Shader is empty: " + path.string());
		std::vector<uint8_t> bytes(static_cast<size_t>(size));
		file.seekg(0);
		if (!file.read(reinterpret_cast<char*>(bytes.data()), size))
			throw std::runtime_error("Failed to read shader: " + path.string());
		return bytes;
	}
}