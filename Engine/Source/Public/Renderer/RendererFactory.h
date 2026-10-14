#pragma once

#include <d3d12.h>
#include <dxgi1_6.h>
#include <wrl/client.h>

class Window;

class RendererFactory
{
public:
	RendererFactory();
	~RendererFactory();

	void Initialize();
	void UnInitialize();

	void SetVSyncEnabled(bool bEnabled);

	void CreateDevice(const LUID* adapterLuid = nullptr);
	void CreateCommandQueue();
	void CreateSwapChain();
	void CreateRenderTargetViews();
	void CreateCommandObjects();
	void CreateSyncObjects();
	void GetHardwareAdapter(IDXGIFactory1* factory, IDXGIAdapter1** ppAdapter);
	bool IsRHIValid() const;

	// Per frame. The high level Renderer forwards its BeginRender/EndRender/Present here.
	void BeginFrame();
	void EndFrame();
	void PresentFrame();

	// Per frame / resize.
	void ResizeSwapChain();
	void WaitForGpu();
	void MoveToNextFrame();
	void WaitForFence(ID3D12Fence* fence, UINT64 value);

	// Resources borrowed by higher level code (mesh/imgui passes built on top of this).
	ID3D12Device* GetDevice() const { return m_Device.Get(); }
	ID3D12CommandQueue* GetCommandQueue() const { return m_CommandQueue.Get(); }
	ID3D12GraphicsCommandList* GetCommandList() const { return m_CommandList.Get(); }
	DXGI_FORMAT GetRTVFormat() const { return m_RtvFormat; }
	UINT GetFramesInFlight() const { return FrameCount; }

	UINT GetWindowDimensionsWidth() const { return m_Width; }
	UINT GetWindowDimensionsHeight() const { return m_Height; }
	void SetWindowDimensions(UINT Width, UINT Height);
	void SetWindow(Window* Window);
protected:
	D3D12_CPU_DESCRIPTOR_HANDLE GetCurrentBackBufferRtv() const;

	static const UINT FrameCount = 2;

	Window* m_Window = nullptr;

	// Pipeline objects.
	Microsoft::WRL::ComPtr<IDXGIFactory4>        m_Factory;
	Microsoft::WRL::ComPtr<ID3D12Device>         m_Device;
	Microsoft::WRL::ComPtr<ID3D12CommandQueue>   m_CommandQueue;
	Microsoft::WRL::ComPtr<IDXGISwapChain3>      m_SwapChain;
	Microsoft::WRL::ComPtr<ID3D12DescriptorHeap> m_SwapChainRtvHeap;
	Microsoft::WRL::ComPtr<ID3D12Resource>       m_RenderTargets[FrameCount];
	Microsoft::WRL::ComPtr<ID3D12CommandAllocator>    m_CommandAllocators[FrameCount];
	Microsoft::WRL::ComPtr<ID3D12GraphicsCommandList> m_CommandList;
	Microsoft::WRL::ComPtr<ID3D12RootSignature> m_RootSignature;
	Microsoft::WRL::ComPtr<ID3D12PipelineState> m_PipelineState;
	UINT m_SwapChainRtvDescriptorSize = 0;
	DXGI_FORMAT m_RtvFormat = DXGI_FORMAT_R8G8B8A8_UNORM;

	// Synchronization objects.
	UINT   m_FrameIndex = 0;
	HANDLE m_FenceEvent = nullptr;
	Microsoft::WRL::ComPtr<ID3D12Fence> m_Fence;
	UINT64 m_FenceValues[FrameCount] = {};
	UINT64 m_NextFenceValue = 1;
	UINT m_SyncInterval = 1;

	UINT m_Width = 0;
	UINT m_Height = 0;
};
