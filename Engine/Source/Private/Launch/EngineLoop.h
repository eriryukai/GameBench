// Copyright (c) CreationArt. All Rights Reserved.

#pragma once

class Renderer;
class Window;

struct EngineLoop
{
    EngineLoop() = default;
    ~EngineLoop();

    int PreInitialize(int argc, char** argv, void* PlatformData = nullptr);
    int Initialize();
    void Tick();
    void Exit();

    void RequestExit(bool bForce = false);
    bool IsExitRequested() const { return !m_bIsRunning; }

private:
    bool m_bIsRunning = true;
    bool m_bIsInitialized = false;

    void* m_PlatformData = nullptr;
    std::unique_ptr<Window> m_Window;
    std::unique_ptr<Renderer> m_Renderer;
};

extern EngineLoop GEngineLoop;
