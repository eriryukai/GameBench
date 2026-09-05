// Copyright (c) CreationArt. All Rights Reserved.

#pragma once

class Renderer
{
public:
	Renderer();
	~Renderer();

	void Initialize();
	void Tick();
	void UnInitialize();

	void BeginRender();
	void ExecuteRender();
	void Present();
	void EndRender();

	bool IsInitialized();
protected:
	bool InitializeRHI();
private:
	bool bInitialize = false;
};

