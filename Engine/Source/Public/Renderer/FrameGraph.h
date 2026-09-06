// Copyright (c) CreationArt. All Rights Reserved.

#pragma once

#include <string>
#include <cstdint>

class Window;

struct FrameGraphSpecification
{
	// Command recorder for the current frame.
	void* NativeCommandList = nullptr;

	// The color target to render into.
	void* NativeTargetView = nullptr;

	// Render area / viewport size in pixels.
	uint32_t Width = 0;
	uint32_t Height = 0;
};

class FrameGraph
{
public:
	explicit FrameGraph(std::string debugName)
		: m_DebugName(std::move(debugName))
	{
	}

	virtual ~FrameGraph() = default;

	virtual void Initialize() {}
	virtual void Execute(const FrameGraphSpecification& spec) = 0;

	void SetWindow(Window* window) { m_Window = window; }
	Window* GetWindow() const { return m_Window; }

	const std::string& GetDebugName() const { return m_DebugName; }

protected:
	std::string m_DebugName;
	Window* m_Window = nullptr;
};
