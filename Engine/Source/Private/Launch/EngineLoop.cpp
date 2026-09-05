// Copyright (c) CreationArt. All Rights Reserved.

#include "stdafx.h"
#include "EngineLoop.h"
#include "Renderer/Renderer.h"

EngineLoop GEngineLoop;

EngineLoop::~EngineLoop()
{
    if (m_bIsInitialized)
    {
        Exit();
    }
}

int EngineLoop::PreInitialize(int argc, char** argv)
{
    std::print("EngineLoop::PreInit");

    (void)argc;
    (void)argv;

    return 0;
}

int EngineLoop::Initialize()
{
    std::print("EngineLoop::Init");

    // Renderer
    {
        m_Renderer = std::make_unique<Renderer>();
        if (!m_Renderer)
        {
            //@TODO: Add Log and exceptions
            m_bIsInitialized = false;
        }
        m_Renderer->Initialize();
        if (!m_Renderer->IsInitialized())
        {
            //@TODO: Add Log and exceptions
            m_bIsInitialized = false;
        }
    }

    m_bIsInitialized = true;

    return 0;
}

void EngineLoop::Tick()
{
    if (!m_bIsInitialized)
    {
        return;
    }

    // Renderer
    {
        m_Renderer->Tick();
    }
}

void EngineLoop::Exit()
{
    if (!m_bIsInitialized)
    {
        return;
    }

    std::print("EngineLoop::Exit");

    m_bIsInitialized = false;
}

void EngineLoop::RequestExit(bool bForce)
{
    (void)bForce;
    m_bIsRunning = false;
}
