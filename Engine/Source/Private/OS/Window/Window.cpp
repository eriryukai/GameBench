// Copyright (c) CreationArt. All Rights Reserved.

#include "stdafx.h"
#include "Window.h"

#if MORTAR_WINDOW_USE_GLFW
#include "Platform/GLFWWindow.h"
#elif MORTAR_PLATFORM_WINDOWS
#include "Platform/WindowsWindow.h"
#else
#error "No window backend defined."
#endif

Window::Window() = default;

Window::~Window()
{
	Destroy();
}

bool Window::Create(const WindowSpecification& Spec)
{
	m_Spec = Spec;

#if MORTAR_WINDOW_USE_GLFW
	m_NativeHandle = GLFWWindow::Create(Spec);
	return m_NativeHandle != nullptr;
#elif MORTAR_PLATFORM_WINDOWS
	m_NativeHandle = WindowsWindow::Create(Spec);
	return m_NativeHandle != nullptr;
#else
	return false;
#endif
}

void Window::Destroy()
{
	if (!m_NativeHandle)
	{
		return;
	}

#if MORTAR_WINDOW_USE_GLFW
	GLFWWindow::Destroy(m_NativeHandle);
#elif MORTAR_PLATFORM_WINDOWS
	WindowsWindow::Destroy(static_cast<HWND>(m_NativeHandle));
#endif
	m_NativeHandle = nullptr;
}

void Window::PumpMessages()
{
	if (!m_NativeHandle)
	{
		return;
	}

#if MORTAR_WINDOW_USE_GLFW
	GLFWWindow::PumpMessages(m_bShouldClose);
#elif MORTAR_PLATFORM_WINDOWS
	WindowsWindow::PumpMessages(m_bShouldClose);
#endif
}

bool Window::ShouldClose() const
{
	return m_bShouldClose;
}

void* Window::GetNativeHandle() const
{
	return m_NativeHandle;
}

uint32_t Window::GetWidth() const
{
	return m_Spec.GetWidth();
}

uint32_t Window::GetHeight() const
{
	return m_Spec.GetHeight();
}
