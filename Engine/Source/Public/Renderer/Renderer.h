// Copyright (c) CreationArt. All Rights Reserved.

#pragma once

#include <d3d12.h>
#include <dxgi1_6.h>
#include <wrl/client.h>

#include <memory>
#include <vector>

class FrameGraph;
class Window;

class Renderer
{
public:
	Renderer();
	~Renderer();

	void Initialize(Window* window);
	void Tick();
	void UnInitialize();

	void BeginRender();
	void ExecuteRender();
	void Present();
	void EndRender();

	bool IsInitialized();

	void AddFrameGraph(std::shared_ptr<FrameGraph> frameGraph);

protected:
	bool InitializeRHI();

private:
	void CreateDevice();
	void CreateCommandQueue();
	void CreateSwapChain();
	void CreateRenderTargetViews();
	void CreateCommandObjects();
	void CreateSyncObjects();

	// Per frame / resize.
	void ResizeSwapChain();
	void WaitForGpu();
	void MoveToNextFrame();

	void GetHardwareAdapter(IDXGIFactory1* factory, IDXGIAdapter1** ppAdapter);

private:
	static const UINT FrameCount = 2;

	bool bInitialize = false;
	Window* m_Window = nullptr;

	// Pipeline objects.
	Microsoft::WRL::ComPtr<IDXGIFactory4>        m_Factory;
	Microsoft::WRL::ComPtr<ID3D12Device>         m_Device;
	Microsoft::WRL::ComPtr<ID3D12CommandQueue>   m_CommandQueue;
	Microsoft::WRL::ComPtr<IDXGISwapChain3>      m_SwapChain;
	Microsoft::WRL::ComPtr<ID3D12DescriptorHeap> m_RtvHeap;
	Microsoft::WRL::ComPtr<ID3D12Resource>       m_RenderTargets[FrameCount];
	Microsoft::WRL::ComPtr<ID3D12CommandAllocator>    m_CommandAllocators[FrameCount];
	Microsoft::WRL::ComPtr<ID3D12GraphicsCommandList> m_CommandList;
	UINT m_RtvDescriptorSize = 0;
	DXGI_FORMAT m_RtvFormat = DXGI_FORMAT_R8G8B8A8_UNORM;

	// Synchronization objects.
	UINT   m_FrameIndex = 0;
	HANDLE m_FenceEvent = nullptr;
	Microsoft::WRL::ComPtr<ID3D12Fence> m_Fence;
	UINT64 m_FenceValues[FrameCount] = {};

	// Frame graphs.
	std::vector<std::shared_ptr<FrameGraph>> m_FrameGraphs;
};
