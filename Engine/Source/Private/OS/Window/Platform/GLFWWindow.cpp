// Copyright (c) CreationArt. All Rights Reserved.

#include "stdafx.h"

#if MORTAR_WINDOW_USE_GLFW

#include "GLFWWindow.h"

#define GLFW_INCLUDE_NONE
#include <GLFW/glfw3.h>

static GLFWwindow* s_Window = nullptr;

void* GLFWWindow::Create(const WindowSpecification& Spec)
{
	if (!glfwInit())
	{
		return nullptr;
	}

	glfwWindowHint(GLFW_CLIENT_API, GLFW_NO_API);

	s_Window = glfwCreateWindow(
		static_cast<int>(Spec.GetWidth()),
		static_cast<int>(Spec.GetHeight()),
		Spec.GetTitle(),
		nullptr,
		nullptr
	);

	if (!s_Window)
	{
		glfwTerminate();
		return nullptr;
	}

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

#endif // MORTAR_WINDOW_USE_GLFW
