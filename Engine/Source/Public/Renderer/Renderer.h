// Copyright (c) CreationArt. All Rights Reserved.

#pragma once

#include <d3d12.h>
#include <dxgi1_6.h>
#include <wrl/client.h>

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
protected:
	bool InitializeRHI();

private:
	void CreateDevice(const LUID* adapterLuid = nullptr);
	void CreateCommandQueue();
	void CreateSwapChain();
	void CreateRenderTargetViews();
	void CreateCommandObjects();
	void CreateSyncObjects();
	void CreateTrianglePipeline();

	// Per frame / resize.
	void ResizeSwapChain();
	void WaitForGpu();
	void MoveToNextFrame();
	void WaitForFence(ID3D12Fence* fence, UINT64 value);

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
	Microsoft::WRL::ComPtr<ID3D12RootSignature> m_RootSignature;
	Microsoft::WRL::ComPtr<ID3D12PipelineState> m_PipelineState;
	UINT m_RtvDescriptorSize = 0;
	DXGI_FORMAT m_RtvFormat = DXGI_FORMAT_R8G8B8A8_UNORM;

	// Synchronization objects.
	UINT   m_FrameIndex = 0;
	HANDLE m_FenceEvent = nullptr;
	Microsoft::WRL::ComPtr<ID3D12Fence> m_Fence;
	UINT64 m_FenceValues[FrameCount] = {};
	UINT64 m_NextFenceValue = 1;
	UINT m_Width = 0;
	UINT m_Height = 0;
};
