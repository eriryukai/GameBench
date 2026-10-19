// Copyright (c) CreationArt. All Rights Reserved.

#include "ScriptGen.h"

ScriptGen::ScriptGen()
{
}

ScriptGen::~ScriptGen()
{
	UnInitialize();
}

void ScriptGen::Initialize()
{
	if (IsInitialized())
	{
		return;
	}

	bInitialize = true;
}

void ScriptGen::UnInitialize()
{
	if (!IsInitialized())
	{
		return;
	}

	bInitialize = false;
}