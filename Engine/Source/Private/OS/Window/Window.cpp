#include "stdafx.h"
#include "Window.h"

#if defined(MORTAR_PLATFORM_WINDOWS)
#include "Platform/WindowsWindow.h"
#endif

Window::Window() = default;

Window::~Window()
{
	Destroy();
}

bool Window::Create(const WindowSpecification& Spec)
{
	m_Spec = Spec;

#if defined(MORTAR_PLATFORM_WINDOWS)
	m_NativeHandle = WindowsWindow::Create(Spec);
	return m_NativeHandle != nullptr;
#else
	#error "No platform defined. Define a platform macro (e.g. MORTAR_PLATFORM_WINDOWS)."
	return false;
#endif
}

void Window::Destroy()
{
	if (!m_NativeHandle)
	{
		return;
	}

#if defined(MORTAR_PLATFORM_WINDOWS)
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

#if defined(MORTAR_PLATFORM_WINDOWS)
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
