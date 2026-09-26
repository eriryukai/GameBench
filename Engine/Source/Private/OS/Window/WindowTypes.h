// Copyright (c) CreationArt. All Rights Reserved.

#pragma once

#include <cstdint>
#include <filesystem>
#include <string>

struct WindowSpecification
{
	void SetTitle(const std::string& InTitle) { Title = InTitle; }
	void SetWidth(uint32_t InWidth) { Width = InWidth; }
	void SetHeight(uint32_t InHeight) { Height = InHeight; }
	void SetPlatformData(void* InData) { PlatformData = InData; }
	void SetDecorated(bool bInDecorated) { Decorated = bInDecorated; }
	void SetFullscreen(bool bInFullscreen) { Fullscreen = bInFullscreen; }
	void SetResizable(bool bInResizable) { Resizable = bInResizable; }
	void SetStartMaximized(bool bInStartMaximized) { StartMaximized = bInStartMaximized; }
	void SetIconPath(const std::filesystem::path& InIconPath) { IconPath = InIconPath; }

	const std::string& GetTitle() const { return Title; }
	uint32_t GetWidth() const { return Width; }
	uint32_t GetHeight() const { return Height; }
	void* GetPlatformData() const { return PlatformData; }
	bool GetDecorated() const { return Decorated; }
	bool GetFullscreen() const { return Fullscreen; }
	bool GetResizable() const { return Resizable; }
	bool GetStartMaximized() const { return StartMaximized; }
	const std::filesystem::path& GetIconPath() const { return IconPath; }

private:
	std::string Title;
	uint32_t Width = 0;
	uint32_t Height = 0;
	void* PlatformData = nullptr;
	bool Decorated = true;
	bool Fullscreen = false;
	bool Resizable = true;
	bool StartMaximized = false;
	std::filesystem::path IconPath;
};
