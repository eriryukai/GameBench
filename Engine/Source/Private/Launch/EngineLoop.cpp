// Copyright (c) CreationArt. All Rights Reserved.

#include "stdafx.h"
#include "EngineLoop.h"
#include "OS/Window/Window.h"
#include "Renderer/Renderer.h"

EngineLoop GEngineLoop;

EngineLoop::~EngineLoop()
{
    if (m_bIsInitialized)
    {
        Exit();
    }
}

int EngineLoop::PreInitialize(int argc, char** argv, void* PlatformData)
{
    std::print("EngineLoop::PreInit");

    (void)argc;
    (void)argv;

    m_PlatformData = PlatformData;

    return 0;
}

int EngineLoop::Initialize()
{
    std::print("EngineLoop::Init");

    // Window
    {
        m_Window = std::make_unique<Window>();
        if (!m_Window)
        {
            //@TODO: Add Log and exceptions
            m_bIsInitialized = false;
            return 1;
        }

        WindowSpecification WindowSpec;
        WindowSpec.SetTitle("MortarEngine");
        WindowSpec.SetWidth(1280);
        WindowSpec.SetHeight(720);
        WindowSpec.SetPlatformData(m_PlatformData);

        if (!m_Window->Create(WindowSpec))
        {
            //@TODO: Add Log and exceptions
            m_bIsInitialized = false;
            return 1;
        }
    }

    // Renderer
    {
        m_Renderer = std::make_unique<Renderer>();
        if (!m_Renderer)
        {
            //@TODO: Add Log and exceptions
            m_bIsInitialized = false;
            return 1;
        }
        m_Renderer->Initialize(m_Window.get());
        if (!m_Renderer->IsInitialized())
        {
            //@TODO: Add Log and exceptions
            m_bIsInitialized = false;
            return 1;
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

    // Window
    {
        m_Window->PumpMessages();
        if (m_Window->ShouldClose())
        {
            RequestExit();
            return;
        }
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

    m_Renderer.reset();
    m_Window.reset();

    m_bIsInitialized = false;
}

void EngineLoop::RequestExit(bool bForce)
{
    (void)bForce;
    m_bIsRunning = false;
}
