#include "stdafx.h"
#include "Renderer/RendererFactory.h"

#include <d3d12.h>
#include <d3dx12/d3dx12.h>
#include <dxgi1_6.h>
#include <wrl/client.h>

#include "OS/Window/Window.h"

using Microsoft::WRL::ComPtr;

RendererFactory::RendererFactory()
{
}

RendererFactory::~RendererFactory()
{
	UnInitialize();
}

void RendererFactory::Initialize()
{
	CreateDevice();
	CreateCommandQueue();
	CreateSwapChain();
	CreateRenderTargetViews();
	CreateCommandObjects();
	CreateSyncObjects();
}

void RendererFactory::UnInitialize()
{
	if (m_Device && m_Fence && m_FenceEvent)
	{
		try { WaitForGpu(); }
		catch (const std::exception& error) { OutputDebugStringA(error.what()); }
	}

	if (m_FenceEvent)
	{
		CloseHandle(m_FenceEvent);
		m_FenceEvent = nullptr;
	}

	for (auto& target : m_RenderTargets) 
		target.Reset();
	for (auto& allocator : m_CommandAllocators) 
		allocator.Reset();

	m_CommandList.Reset();
	m_PipelineState.Reset();
	m_RootSignature.Reset();
	m_SwapChainRtvHeap.Reset();
	m_SwapChain.Reset();
	m_Fence.Reset();
	m_CommandQueue.Reset();
	m_Device.Reset();
	m_Factory.Reset();
	m_FrameIndex = 0;
	m_NextFenceValue = 1;
	m_Width = m_Height = 0;

	std::fill(std::begin(m_FenceValues), std::end(m_FenceValues), 0);

	m_Window = nullptr;
}

void RendererFactory::SetWindowDimensions(UINT Width, UINT Height)
{
	m_Width = Width;
	m_Height = Height;
}

void RendererFactory::SetWindow(Window* Window)
{
	m_Window = Window;
}

void RendererFactory::SetVSyncEnabled(bool bEnabled)
{
	m_SyncInterval = bEnabled ? 1 : 0;
}

void RendererFactory::CreateDevice(const LUID* adapterLuid)
{
	UINT dxgiFactoryFlags = 0;

#if defined(_DEBUG)
	{
		ComPtr<ID3D12Debug> debugController;
		if (SUCCEEDED(D3D12GetDebugInterface(IID_PPV_ARGS(&debugController))))
		{
			debugController->EnableDebugLayer();
			dxgiFactoryFlags |= DXGI_CREATE_FACTORY_DEBUG;
		}
	}
#endif

	HRESULT hr = CreateDXGIFactory2(dxgiFactoryFlags, IID_PPV_ARGS(&m_Factory));
	if (FAILED(hr))
	{
		return;
	}
	Microsoft::WRL::ComPtr<IDXGIAdapter1> hardwareAdapter;
	if (adapterLuid)
	{
		hr = m_Factory->EnumAdapterByLuid(*adapterLuid, IID_PPV_ARGS(&hardwareAdapter));
		if (FAILED(hr))
		{
			return;
		}
	}
	else
	{
		GetHardwareAdapter(m_Factory.Get(), &hardwareAdapter);
	}

	hr = D3D12CreateDevice(hardwareAdapter.Get(), D3D_FEATURE_LEVEL_11_0, IID_PPV_ARGS(&m_Device));
	if (FAILED(hr))
	{
		return;
	}
}

void RendererFactory::CreateCommandQueue()
{
	D3D12_COMMAND_QUEUE_DESC queueDesc = {};
	queueDesc.Flags = D3D12_COMMAND_QUEUE_FLAG_NONE;
	queueDesc.Type = D3D12_COMMAND_LIST_TYPE_DIRECT;

	HRESULT hr = m_Device->CreateCommandQueue(&queueDesc, IID_PPV_ARGS(&m_CommandQueue));
	if (FAILED(hr))
	{
		std::print("Fails to create command queue in %s", __FUNCTION__);
		return;
	}
}

void RendererFactory::CreateSwapChain()
{
	if (!m_Window)
	{
		std::print("Fails to create the swapchain in %s: no window was set on the factory", __FUNCTION__);
		return;
	}

	DXGI_SWAP_CHAIN_DESC1 swapChainDesc = {};
	swapChainDesc.BufferCount = FrameCount;
	swapChainDesc.Width = m_Window->GetWidth();
	swapChainDesc.Height = m_Window->GetHeight();
	m_Width = swapChainDesc.Width;
	m_Height = swapChainDesc.Height;
	swapChainDesc.Format = m_RtvFormat;
	swapChainDesc.BufferUsage = DXGI_USAGE_RENDER_TARGET_OUTPUT;
	swapChainDesc.SwapEffect = DXGI_SWAP_EFFECT_FLIP_DISCARD;
	swapChainDesc.SampleDesc.Count = 1;

	const HWND hwnd = static_cast<HWND>(m_Window->GetHandle());

	Microsoft::WRL::ComPtr<IDXGISwapChain1> swapChain;
	HRESULT hr = m_Factory->CreateSwapChainForHwnd(
		m_CommandQueue.Get(),
		hwnd,
		&swapChainDesc,
		nullptr,
		nullptr,
		&swapChain);
	if (FAILED(hr))
	{
		std::print("Fails to create swapchain in %s", __FUNCTION__);
		return;
	}

	hr = swapChain.As(&m_SwapChain);
	if (FAILED(hr))
	{
		std::print("Fails to set the newly created swapchain as active swapchain in %s", __FUNCTION__);
		return;
	}
	m_FrameIndex = m_SwapChain->GetCurrentBackBufferIndex();
}

void RendererFactory::CreateRenderTargetViews()
{
	if (!m_SwapChainRtvHeap)
	{
		D3D12_DESCRIPTOR_HEAP_DESC swapChainRtvHeapDesc = {};
		swapChainRtvHeapDesc.NumDescriptors = FrameCount;
		swapChainRtvHeapDesc.Type = D3D12_DESCRIPTOR_HEAP_TYPE_RTV;
		swapChainRtvHeapDesc.Flags = D3D12_DESCRIPTOR_HEAP_FLAG_NONE;

		HRESULT hr = m_Device->CreateDescriptorHeap(&swapChainRtvHeapDesc, IID_PPV_ARGS(&m_SwapChainRtvHeap));
		if (FAILED(hr))
		{
			std::print("Fails to create descriptor heap for render target view for the swapchain back buffer in %s", __FUNCTION__);
			return;
		}
		m_SwapChainRtvDescriptorSize = m_Device->GetDescriptorHandleIncrementSize(D3D12_DESCRIPTOR_HEAP_TYPE_RTV);
	}

	CD3DX12_CPU_DESCRIPTOR_HANDLE rtvHandle(m_SwapChainRtvHeap->GetCPUDescriptorHandleForHeapStart());
	for (UINT n = 0; n < FrameCount; n++)
	{
		HRESULT hr = m_SwapChain->GetBuffer(n, IID_PPV_ARGS(&m_RenderTargets[n]));
		if (FAILED(hr))
		{
			std::print("Fails to get the back buffer for each render targets in %s", __FUNCTION__);
			return;
		}
		m_Device->CreateRenderTargetView(m_RenderTargets[n].Get(), nullptr, rtvHandle);
		if (!m_RenderTargets[n].Get())
		{
			std::print("Fails to create render target view for the current index %d swapchain back buffer in %s", n, __FUNCTION__);
			return;
		}
		rtvHandle.Offset(1, m_SwapChainRtvDescriptorSize);
	}
}

void RendererFactory::CreateCommandObjects()
{
	for (UINT n = 0; n < FrameCount; n++)
	{
		HRESULT hr = m_Device->CreateCommandAllocator(D3D12_COMMAND_LIST_TYPE_DIRECT, IID_PPV_ARGS(&m_CommandAllocators[n]));
		if (FAILED(hr))
		{
			std::print("Fails to create command allocator for current $d frame in  %s", n, __FUNCTION__);
			return;
		}
	}

	HRESULT hr = m_Device->CreateCommandList(0, D3D12_COMMAND_LIST_TYPE_DIRECT, m_CommandAllocators[m_FrameIndex].Get(), nullptr,
		IID_PPV_ARGS(&m_CommandList));
	if (FAILED(hr))
	{
		std::print("Fails to create command list for current frame index %d in %s", m_FrameIndex, __FUNCTION__);
		return;
	}
	hr = m_CommandList->Close();
	if (FAILED(hr))
	{
		return;
	}
}

void RendererFactory::CreateSyncObjects()
{
	HRESULT hr = m_Device->CreateFence(0, D3D12_FENCE_FLAG_NONE, IID_PPV_ARGS(&m_Fence));
	if (FAILED(hr))
	{
		std::print("Fails to create fence in %s", __FUNCTION__);
		return;
	}
	m_FenceEvent = CreateEvent(nullptr, FALSE, FALSE, nullptr);
	if (m_FenceEvent == nullptr)
	{
		hr = HRESULT_FROM_WIN32(GetLastError());
		if (FAILED(hr))
		{
			std::print("Fails to create fence event in %s", __FUNCTION__);
			return;
		}
	}

	WaitForGpu();
}

bool RendererFactory::IsRHIValid() const
{
	return m_Device && m_CommandQueue && m_SwapChain && m_SwapChainRtvHeap &&
		m_CommandList && m_CommandAllocators[0] && m_Fence && m_FenceEvent;
}

D3D12_CPU_DESCRIPTOR_HANDLE RendererFactory::GetCurrentBackBufferRtv() const
{
	return CD3DX12_CPU_DESCRIPTOR_HANDLE(m_SwapChainRtvHeap->GetCPUDescriptorHandleForHeapStart(), m_FrameIndex, m_SwapChainRtvDescriptorSize);
}

void RendererFactory::BeginFrame()
{
	if (!m_CommandList || !m_CommandAllocators[m_FrameIndex] || !m_RenderTargets[m_FrameIndex])
	{
		return;
	}

	HRESULT hr = m_CommandAllocators[m_FrameIndex]->Reset();
	if (FAILED(hr))
	{
		std::print("Fails to reset the current frame %d command allocator in %s", m_FrameIndex, __FUNCTION__);
		return;
	}
	hr = m_CommandList->Reset(m_CommandAllocators[m_FrameIndex].Get(), nullptr);
	if (FAILED(hr))
	{
		std::print("Fails to reset the current frame %d command list in %s", m_FrameIndex, __FUNCTION__);
		return;
	}

	// Transition the current back buffer PRESENT -> RENDER_TARGET so it can be drawn to.
	const CD3DX12_RESOURCE_BARRIER BarriertoRenderTarget = CD3DX12_RESOURCE_BARRIER::Transition(
		m_RenderTargets[m_FrameIndex].Get(), D3D12_RESOURCE_STATE_PRESENT, D3D12_RESOURCE_STATE_RENDER_TARGET);
	m_CommandList->ResourceBarrier(1, &BarriertoRenderTarget);

	const D3D12_CPU_DESCRIPTOR_HANDLE rtvHandle = GetCurrentBackBufferRtv();
	m_CommandList->OMSetRenderTargets(1, &rtvHandle, FALSE, nullptr);

	const float clearColor[4] = { 0.0f, 0.0f, 0.0f, 1.0f };
	m_CommandList->ClearRenderTargetView(rtvHandle, clearColor, 0, nullptr);
}

void RendererFactory::EndFrame()
{
	if (!m_CommandList || !m_RenderTargets[m_FrameIndex])
	{
		return;
	}

	// Transition back RENDER_TARGET -> PRESENT before handing the list to the GPU.
	const CD3DX12_RESOURCE_BARRIER BarriertoPresent = CD3DX12_RESOURCE_BARRIER::Transition(
		m_RenderTargets[m_FrameIndex].Get(), D3D12_RESOURCE_STATE_RENDER_TARGET, D3D12_RESOURCE_STATE_PRESENT);
	m_CommandList->ResourceBarrier(1, &BarriertoPresent);

	HRESULT hr = m_CommandList->Close();
	if (FAILED(hr))
	{
		return;
	}

	ID3D12CommandList* ppCommandLists[] = { m_CommandList.Get() };
	m_CommandQueue->ExecuteCommandLists(_countof(ppCommandLists), ppCommandLists);
}

void RendererFactory::PresentFrame()
{
	if (!m_SwapChain || !m_CommandQueue || !m_Fence)
	{
		return;
	}

	HRESULT hr = m_SwapChain->Present(m_SyncInterval, 0);
	if (hr == DXGI_ERROR_DEVICE_REMOVED || hr == DXGI_ERROR_DEVICE_RESET)
	{
		throw std::runtime_error("DX12 device removed/reset during Present.");
	}
	if (FAILED(hr))
	{
		return;
	}

	MoveToNextFrame();
}

void RendererFactory::ResizeSwapChain()
{
	WaitForGpu();

	for (UINT n = 0; n < FrameCount; n++)
	{
		m_RenderTargets[n].Reset();
		m_FenceValues[n] = m_FenceValues[m_FrameIndex];
	}

	DXGI_SWAP_CHAIN_DESC desc = {};
	HRESULT hr = m_SwapChain->GetDesc(&desc);
	if (FAILED(hr))
	{
		return;
	}
	hr = m_SwapChain->ResizeBuffers(FrameCount, m_Width, m_Height, m_RtvFormat, desc.Flags);
	if (FAILED(hr))
	{
		return;
	}
	m_FrameIndex = m_SwapChain->GetCurrentBackBufferIndex();
	CreateRenderTargetViews();
}

void RendererFactory::WaitForGpu()
{
	const UINT64 fenceToWaitFor = m_NextFenceValue++;
	HRESULT hr = (m_CommandQueue->Signal(m_Fence.Get(), fenceToWaitFor));
	if (FAILED(hr))
	{
		return;
	}
	WaitForFence(m_Fence.Get(), fenceToWaitFor);
}

void RendererFactory::MoveToNextFrame()
{
	const UINT64 currentFenceValue = m_NextFenceValue++;
	HRESULT hr = m_CommandQueue->Signal(m_Fence.Get(), currentFenceValue);
	if (FAILED(hr))
	{
		return;
	}
	m_FenceValues[m_FrameIndex] = currentFenceValue;
	m_FrameIndex = m_SwapChain->GetCurrentBackBufferIndex();

	if (m_Fence->GetCompletedValue() < m_FenceValues[m_FrameIndex])
	{
		WaitForFence(m_Fence.Get(), m_FenceValues[m_FrameIndex]);
	}
}

void RendererFactory::GetHardwareAdapter(IDXGIFactory1* factory, IDXGIAdapter1** ppAdapter)
{
	*ppAdapter = nullptr;

	Microsoft::WRL::ComPtr<IDXGIAdapter1> adapter;
	Microsoft::WRL::ComPtr<IDXGIFactory6> factory6;
	if (SUCCEEDED(factory->QueryInterface(IID_PPV_ARGS(&factory6))))
	{
		for (UINT adapterIndex = 0;
			SUCCEEDED(factory6->EnumAdapterByGpuPreference(adapterIndex, DXGI_GPU_PREFERENCE_HIGH_PERFORMANCE, IID_PPV_ARGS(&adapter)));
			++adapterIndex)
		{
			DXGI_ADAPTER_DESC1 desc;
			adapter->GetDesc1(&desc);
			if (desc.Flags & DXGI_ADAPTER_FLAG_SOFTWARE)
				continue;
			if (SUCCEEDED(D3D12CreateDevice(adapter.Get(), D3D_FEATURE_LEVEL_11_0, _uuidof(ID3D12Device), nullptr)))
				break;
		}
	}

	if (adapter.Get() == nullptr)
	{
		for (UINT adapterIndex = 0; SUCCEEDED(factory->EnumAdapters1(adapterIndex, &adapter)); ++adapterIndex)
		{
			DXGI_ADAPTER_DESC1 desc;
			adapter->GetDesc1(&desc);
			if (desc.Flags & DXGI_ADAPTER_FLAG_SOFTWARE)
				continue;
			if (SUCCEEDED(D3D12CreateDevice(adapter.Get(), D3D_FEATURE_LEVEL_11_0, _uuidof(ID3D12Device), nullptr)))
				break;
		}
	}

	*ppAdapter = adapter.Detach();
}

void RendererFactory::WaitForFence(ID3D12Fence* fence, UINT64 value)
{
	if (fence->GetCompletedValue() == UINT64_MAX)
	{
		HRESULT hr = m_Device->GetDeviceRemovedReason();
		if (FAILED(hr))
		{
			return;
		}
	}
	if (fence->GetCompletedValue() >= value)
		return;
	HRESULT hr = fence->SetEventOnCompletion(value, m_FenceEvent);
	if (FAILED(hr))
	{
		return;
	}

	if (WaitForSingleObject(m_FenceEvent, 5000) != WAIT_OBJECT_0)
		throw std::runtime_error("Timed out waiting for the GPU.");
}