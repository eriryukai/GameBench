#include "stdafx.h"
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
	MultiByteToWideChar(CP_UTF8, 0, Spec.GetTitle(), -1, WideTitle, 256);

	HWND hwnd = CreateWindowExW(
		0,
		CLASS_NAME,
		WideTitle,
		WS_OVERLAPPEDWINDOW,
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

	ShowWindow(hwnd, SW_SHOW);
	UpdateWindow(hwnd);

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
