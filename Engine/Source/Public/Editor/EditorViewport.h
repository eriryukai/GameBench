// Copyright (c) CreationArt. All Rights Reserved.
#pragma once

#if WITH_EDITOR
#include <cstdint>

// x64 C ABI shared with Editor/Source/Native/EngineSession.cs. Handles are borrowed:
// the compositor must release its imports before ResetViewport or Destroy.
struct EditorViewportFrame
{
	uint32_t Width;
	uint32_t Height;
	uint32_t Format;
	uint32_t Slot;
	uint64_t Generation;
	void* ImageHandle;
};

static_assert(sizeof(EditorViewportFrame) == 32);
#endif
