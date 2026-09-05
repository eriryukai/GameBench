// Copyright (c) CreationArt. All Rights Reserved.

#pragma once

#include "WindowTypes.h"

class Window
{
public:
	Window();
	~Window();

	bool Create(const WindowSpecification& Spec);
	void Destroy();

	void PumpMessages();
	bool ShouldClose() const;

	void* GetNativeHandle() const;
	uint32_t GetWidth() const;
	uint32_t GetHeight() const;

private:
	void* m_NativeHandle = nullptr;
	bool m_bShouldClose = false;
	WindowSpecification m_Spec;
};
