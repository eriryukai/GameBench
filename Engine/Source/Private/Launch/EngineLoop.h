// Copyright (c) CreationArt. All Rights Reserved.

#pragma once

class Renderer;

struct EngineLoop
{
    EngineLoop() = default;
    ~EngineLoop();

    int PreInitialize(int argc, char** argv);
    int Initialize();
    void Tick();
    void Exit();
    
    void RequestExit(bool bForce = false);
    bool IsExitRequested() const { return !m_bIsRunning; }
private:
    bool m_bIsRunning = true;
    bool m_bIsInitialized = false;

    std::unique_ptr<Renderer> m_Renderer;
};

extern EngineLoop GEngineLoop;
