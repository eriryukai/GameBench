// Copyright (c) CreationArt. All Rights Reserved.

#pragma once

#include <cstdint>

// Input events sent by the managed viewport to the native ImGui backend.
// Keep this layout mirrored by Editor/Source/Native/EditorIpc/EditorProtocol.cs.
enum class ViewportInputType : uint32_t
{
	MouseMove = 0,
	MouseButton = 1,
	MouseWheel = 2,
	Key = 3,
	Text = 4,
	Focus = 5,
};

enum class ViewportKey : int32_t
{
	None = 0,

	A = 1, B = 2, C = 3, D = 4, E = 5, F = 6, G = 7, H = 8, I = 9,
	J = 10, K = 11, L = 12, M = 13, N = 14, O = 15, P = 16, Q = 17,
	R = 18, S = 19, T = 20, U = 21, V = 22, W = 23, X = 24, Y = 25, Z = 26,

	D0 = 27, D1 = 28, D2 = 29, D3 = 30, D4 = 31,
	D5 = 32, D6 = 33, D7 = 34, D8 = 35, D9 = 36,

	Enter = 37,
	Escape = 38,
	Tab = 39,
	Backspace = 40,
	Space = 41,
	Left = 42,
	Right = 43,
	Up = 44,
	Down = 45,
	Home = 46,
	End = 47,
	PageUp = 48,
	PageDown = 49,
	Delete = 50,
	Insert = 51,
	LeftShift = 52,
	RightShift = 53,
	LeftCtrl = 54,
	RightCtrl = 55,
	LeftAlt = 56,
	RightAlt = 57,

	F1 = 58, F2 = 59, F3 = 60, F4 = 61, F5 = 62, F6 = 63,
	F7 = 64, F8 = 65, F9 = 66, F10 = 67, F11 = 68, F12 = 69,

	Comma = 70,
	Period = 71,
	Minus = 72,
	Equal = 73,
	Semicolon = 74,
	Slash = 75,
	Backslash = 76,
	Apostrophe = 77,
	Grave = 78,
	LeftBracket = 79,
	RightBracket = 80,
};

struct ViewportInputEvent
{
	ViewportInputType Type = ViewportInputType::MouseMove;
	float X = 0.0f;
	float Y = 0.0f;
	int32_t Button = 0;
	int32_t Down = 0;
	float Wheel = 0.0f;
	int32_t Key = 0;
	uint32_t Modifiers = 0;
	uint32_t Codepoint = 0;
};

static_assert(sizeof(ViewportInputEvent) == 36, "ViewportInputEvent layout changed");
