// Copyright (c) CreationArt. All Rights Reserved.

#include "stdafx.h"
#include "Renderer/Renderer.h"

Renderer::Renderer()
{
}

Renderer::~Renderer()
{
	UnInitialize();
}

void Renderer::Initialize()
{
	if (!InitializeRHI())
	{
		bInitialize = false;
		return;
	}

	bInitialize = true;
}

void Renderer::UnInitialize()
{
	bInitialize = false;
}

void Renderer::BeginRender()
{
}

void Renderer::ExecuteRender()
{
}

void Renderer::Present()
{
}

void Renderer::EndRender()
{
}

bool Renderer::IsInitialized()
{
	return bInitialize;
}

bool Renderer::InitializeRHI()
{
	return true;
}

void Renderer::Tick()
{
	if (!bInitialize)
	{
		return;
	}

	BeginRender();
	ExecuteRender();
	Present();
	EndRender();
}
