// Copyright (c) CreationArt. All Rights Reserved.

#pragma once

// ============================================================
// Platform Selection (set exactly one to 1)
// ============================================================
#define MORTAR_PLATFORM_WINDOWS 1
#define MORTAR_PLATFORM_LINUX   0
#define MORTAR_PLATFORM_MACOS   0

// ============================================================
// Window Backend (set to 1 to enable, 0 to disable)
//   1 = GLFW   (cross-platform, requires GLFW binaries)
//   0 = Native (Win32 on Windows, X11/Wayland on Linux, Cocoa on macOS)
// ============================================================
#define MORTAR_WINDOW_USE_GLFW  1
