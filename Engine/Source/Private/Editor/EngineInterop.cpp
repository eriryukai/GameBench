// Copyright (c) CreationArt. All Rights Reserved.
#include "stdafx.h"
#include "Renderer/Renderer.h"
#include <string>
#include <cstring>

#if WITH_EDITOR
namespace
{
	struct EditorSession
	{
		Renderer Render;
		std::string Error;
	};
	thread_local std::string s_CreateError;

	template<typename Action>
	int Invoke(EditorSession* session, Action action)
	{
		if (!session) return 0;
		try
		{
			session->Error.clear();
			return action();
		}
		catch (const std::exception& error) { session->Error = error.what(); }
		catch (...) { session->Error = "Unexpected native renderer failure."; }
		return 0;
	}
}

#define EDITOR_API extern "C" __declspec(dllexport)

EDITOR_API void* Engine_CreateEditorSession()
{
	try { return new EditorSession(); }
	catch (const std::exception& error) { s_CreateError = error.what(); }
	catch (...) { s_CreateError = "Could not allocate an editor session."; }
	return nullptr;
}

EDITOR_API void Engine_DestroyEditorSession(EditorSession* session)
{
	delete session;
}

EDITOR_API const char* Engine_GetEditorError(EditorSession* session)
{
	return session ? session->Error.c_str() : s_CreateError.c_str();
}

EDITOR_API int Engine_InitializeViewport(EditorSession* session, const uint8_t* luid)
{
	return Invoke(session, [&]
	{
		if (!luid) throw std::runtime_error("Avalonia did not provide an adapter LUID.");
		LUID adapter{};
		std::memcpy(&adapter, luid, sizeof(adapter));
		session->Render.InitializeViewport(adapter);
		return 1;
	});
}

EDITOR_API int Engine_ResetViewport(EditorSession* session, uint32_t width, uint32_t height)
{
	return Invoke(session, [&] { session->Render.ResetViewport(width, height); return 1; });
}

// 1 = produced frame, 2 = compositor still owns the next slot, 0 = error.
EDITOR_API int Engine_RenderViewport(EditorSession* session, EditorViewportFrame* output)
{
	return Invoke(session, [&]
	{
		if (!output) throw std::runtime_error("A frame output is required.");
		return session->Render.RenderViewport(*output) ? 1 : 2;
	});
}
#endif
