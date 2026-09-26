// Copyright (c) CreationArt. All Rights Reserved.

#include "stdafx.h"

#if MORTAR_WINDOW_USE_GLFW

#include "GLFWWindow.h"

#define GLFW_INCLUDE_NONE
#include <GLFW/glfw3.h>
#define GLFW_EXPOSE_NATIVE_WIN32
#include <GLFW/glfw3native.h>

static GLFWwindow* s_Window = nullptr;

void* GLFWWindow::Create(const WindowSpecification& Spec)
{
	if (!glfwInit())
	{
		return nullptr;
	}

	glfwWindowHint(GLFW_CLIENT_API, GLFW_NO_API);
	glfwWindowHint(GLFW_DECORATED, Spec.GetDecorated() ? GLFW_TRUE : GLFW_FALSE);
	glfwWindowHint(GLFW_RESIZABLE, Spec.GetResizable() ? GLFW_TRUE : GLFW_FALSE);

	GLFWmonitor* Monitor = Spec.GetFullscreen() ? glfwGetPrimaryMonitor() : nullptr;

	// A maximized window cannot be created through a hint and a fullscreen window
	// cannot be maximized, so only apply the hint when the two do not conflict.
	if (Spec.GetStartMaximized() && !Spec.GetFullscreen())
	{
		glfwWindowHint(GLFW_MAXIMIZED, GLFW_TRUE);
	}

	s_Window = glfwCreateWindow(
		static_cast<int>(Spec.GetWidth()),
		static_cast<int>(Spec.GetHeight()),
		Spec.GetTitle().c_str(),
		Monitor,
		nullptr
	);

	if (!s_Window)
	{
		glfwTerminate();
		return nullptr;
	}

	// Spec.IconPath is plumbed through but not applied yet: the project has no image
	// decoder, so there is nothing that could turn the file into a GLFWimage here.

	return static_cast<void*>(s_Window);
}

void GLFWWindow::Destroy(void* WindowHandle)
{
	if (WindowHandle)
	{
		glfwDestroyWindow(static_cast<GLFWwindow*>(WindowHandle));
	}
	glfwTerminate();
	s_Window = nullptr;
}

void GLFWWindow::PumpMessages(bool& bOutShouldClose)
{
	glfwPollEvents();

	if (s_Window && glfwWindowShouldClose(s_Window))
	{
		bOutShouldClose = true;
	}
}

void GLFWWindow::PollFramebufferSize(void* WindowHandle, uint32_t& OutWidth, uint32_t& OutHeight)
{
	OutWidth = 0;
	OutHeight = 0;

	if (!WindowHandle)
	{
		return;
	}

	int Width = 0, Height = 0;
	glfwGetFramebufferSize(static_cast<GLFWwindow*>(WindowHandle), &Width, &Height);

	OutWidth = Width > 0 ? static_cast<uint32_t>(Width) : 0;
	OutHeight = Height > 0 ? static_cast<uint32_t>(Height) : 0;
}

void GLFWWindow::WaitEventsTimeout(double Seconds)
{
	glfwWaitEventsTimeout(Seconds);
}

void* GLFWWindow::GetRenderHandle(void* WindowHandle)
{
	if (!WindowHandle)
	{
		return nullptr;
	}

	return glfwGetWin32Window(static_cast<GLFWwindow*>(WindowHandle));
}

#endif // MORTAR_WINDOW_USE_GLFW
