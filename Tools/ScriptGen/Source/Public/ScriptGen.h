// Copyright (c) CreationArt. All Rights Reserved.

#pragma once

class ScriptGen
{
public:
	ScriptGen();
	~ScriptGen();

	void Initialize();
	void UnInitialize();

	bool IsInitialized() const { return bInitialize; }
private:
	bool bInitialize = false;
};
