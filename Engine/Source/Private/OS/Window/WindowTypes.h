// Copyright (c) CreationArt. All Rights Reserved.

#pragma once

#include <cstdint>

struct WindowSpecification
{
	void SetTitle(const char* InTitle) { Title = InTitle; }
	void SetWidth(uint32_t InWidth) { Width = InWidth; }
	void SetHeight(uint32_t InHeight) { Height = InHeight; }
	void SetPlatformData(void* InData) { PlatformData = InData; }

	const char* GetTitle() const { return Title; }
	uint32_t GetWidth() const { return Width; }
	uint32_t GetHeight() const { return Height; }
	void* GetPlatformData() const { return PlatformData; }

private:
	const char* Title = "";
	uint32_t Width = 0;
	uint32_t Height = 0;
	void* PlatformData = nullptr;
};
