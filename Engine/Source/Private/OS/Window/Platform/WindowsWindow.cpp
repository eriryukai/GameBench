// Copyright (c) CreationArt. All Rights Reserved.

#include "stdafx.h"

#if !MORTAR_WINDOW_USE_GLFW && MORTAR_PLATFORM_WINDOWS

#include "WindowsWindow.h"

LRESULT CALLBACK WindowProc(HWND hwnd, UINT uMsg, WPARAM wParam, LPARAM lParam);

void* WindowsWindow::Create(const WindowSpecification& Spec)
{
	const wchar_t CLASS_NAME[] = L"MortarWindowClass";

	WNDCLASSW wc = {};
	wc.lpfnWndProc = WindowProc;
	wc.hInstance = static_cast<HINSTANCE>(Spec.GetPlatformData());
	wc.lpszClassName = CLASS_NAME;

	RegisterClassW(&wc);

	int Width = static_cast<int>(Spec.GetWidth());
	int Height = static_cast<int>(Spec.GetHeight());

	wchar_t WideTitle[256];
	MultiByteToWideChar(CP_UTF8, 0, Spec.GetTitle().c_str(), -1, WideTitle, 256);

	DWORD Style = WS_OVERLAPPEDWINDOW;
	if (!Spec.GetDecorated())
	{
		Style &= ~(WS_DLGFRAME | WS_CAPTION | WS_SYSMENU | WS_THICKFRAME | WS_MINIMIZEBOX | WS_MAXIMIZEBOX);
		Style |= WS_POPUP;
	}

	HWND hwnd = CreateWindowExW(
		0,
		CLASS_NAME,
		WideTitle,
		Style,
		CW_USEDEFAULT, CW_USEDEFAULT, Width, Height,
		nullptr,
		nullptr,
		static_cast<HINSTANCE>(Spec.GetPlatformData()),
		nullptr
	);

	if (hwnd == nullptr)
	{
		return nullptr;
	}

	if (!Spec.GetResizable())
	{
		SetWindowLongPtrW(hwnd, GWL_STYLE, Style & ~WS_THICKFRAME);
	}

	if (!Spec.GetIconPath().empty())
	{
		// Spec.IconPath is plumbed through but not applied yet: the project has no image
		// decoder, so there is nothing that could turn the file into an HICON here.
	}

	ShowWindow(hwnd, SW_SHOW);
	UpdateWindow(hwnd);

	if (Spec.GetStartMaximized())
	{
		ShowWindow(hwnd, SW_MAXIMIZE);
	}

	return static_cast<void*>(hwnd);
}

void WindowsWindow::Destroy(HWND WindowHandle)
{
	if (WindowHandle)
	{
		DestroyWindow(WindowHandle);
	}
}

void WindowsWindow::PumpMessages(bool& bOutShouldClose)
{
	MSG msg = {};
	while (PeekMessage(&msg, nullptr, 0, 0, PM_REMOVE))
	{
		if (msg.message == WM_QUIT)
		{
			bOutShouldClose = true;
			return;
		}
		TranslateMessage(&msg);
		DispatchMessage(&msg);
	}
}

void WindowsWindow::PollFramebufferSize(void* WindowHandle, uint32_t& OutWidth, uint32_t& OutHeight)
{
	OutWidth = 0;
	OutHeight = 0;

	if (!WindowHandle)
	{
		return;
	}

	RECT Rect = {};
	if (!GetClientRect(static_cast<HWND>(WindowHandle), &Rect))
	{
		return;
	}

	OutWidth = static_cast<uint32_t>(Rect.right - Rect.left);
	OutHeight = static_cast<uint32_t>(Rect.bottom - Rect.top);
}

void WindowsWindow::WaitEventsTimeout(double Seconds)
{
	MSG msg = {};
	while (PeekMessage(&msg, nullptr, 0, 0, PM_REMOVE))
	{
		TranslateMessage(&msg);
		DispatchMessage(&msg);
	}

	if (Seconds > 0.0)
	{
		Sleep(static_cast<DWORD>(Seconds * 1000.0));
	}
}

LRESULT CALLBACK WindowProc(HWND hwnd, UINT uMsg, WPARAM wParam, LPARAM lParam)
{
	switch (uMsg)
	{
	case WM_DESTROY:
		PostQuitMessage(0);
		return 0;

	case WM_PAINT:
	{
		PAINTSTRUCT ps;
		HDC hdc = BeginPaint(hwnd, &ps);
		FillRect(hdc, &ps.rcPaint, (HBRUSH)(COLOR_WINDOW + 1));
		EndPaint(hwnd, &ps);
	}
	return 0;
	}
	return DefWindowProc(hwnd, uMsg, wParam, lParam);
}

#endif // !MORTAR_WINDOW_USE_GLFW && MORTAR_PLATFORM_WINDOWS
