// Copyright (c) CreationArt. All Rights Reserved.

#include "stdafx.h"
#include "Renderer/Renderer.h"
#include "Renderer/ShaderLibrary.h"
#include "OS/Window/Window.h"

#include <d3d12.h>
#include <d3dx12/d3dx12.h>
#include <dxgi1_6.h>
#include <wrl/client.h>

using Microsoft::WRL::ComPtr;

Renderer::Renderer()
{
}

Renderer::~Renderer()
{
	UnInitialize();
}

void Renderer::Initialize(Window* window)
{
	m_Window = window;

	if (!InitializeRHI())
	{
		bInitialize = false;
		return;
	}

	bInitialize = true;
}

void Renderer::UnInitialize()
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

	bInitialize = false;
	for (auto& target : m_RenderTargets) target.Reset();
	for (auto& allocator : m_CommandAllocators) allocator.Reset();
	m_CommandList.Reset();
	m_PipelineState.Reset();
	m_RootSignature.Reset();
	m_RtvHeap.Reset();
	m_SwapChain.Reset();
	m_Fence.Reset();
	m_CommandQueue.Reset();
	m_Device.Reset();
	m_Factory.Reset();
	m_FrameIndex = 0;
	m_NextFenceValue = 1;
	m_Width = m_Height = 0;
	std::fill(std::begin(m_FenceValues), std::end(m_FenceValues), 0);
}

bool Renderer::IsInitialized()
{
	return bInitialize;
}

void Renderer::BeginRender()
{
	if (!bInitialize)
	{
		return;
	}

	HRESULT hr = m_CommandAllocators[m_FrameIndex]->Reset();
	if (FAILED(hr))
	{
		return;
	}
	hr = m_CommandList->Reset(m_CommandAllocators[m_FrameIndex].Get(), nullptr);
	if (FAILED(hr))
	{
		return;
	}

	// Transition the current back buffer PRESENT -> RENDER_TARGET.
	const CD3DX12_RESOURCE_BARRIER toRenderTarget = CD3DX12_RESOURCE_BARRIER::Transition(
		m_RenderTargets[m_FrameIndex].Get(), D3D12_RESOURCE_STATE_PRESENT, D3D12_RESOURCE_STATE_RENDER_TARGET);
	m_CommandList->ResourceBarrier(1, &toRenderTarget);
}

void Renderer::ExecuteRender()
{
}

void Renderer::Present()
{
	if (!bInitialize)
	{
		return;
	}

	// Transition back RENDER_TARGET -> PRESENT.
	const CD3DX12_RESOURCE_BARRIER toPresent = CD3DX12_RESOURCE_BARRIER::Transition(
		m_RenderTargets[m_FrameIndex].Get(), D3D12_RESOURCE_STATE_RENDER_TARGET, D3D12_RESOURCE_STATE_PRESENT);
	m_CommandList->ResourceBarrier(1, &toPresent);

	HRESULT hr = m_CommandList->Close();
	if (FAILED(hr))
	{
		return;
	}
	ID3D12CommandList* ppCommandLists[] = { m_CommandList.Get() };
	m_CommandQueue->ExecuteCommandLists(_countof(ppCommandLists), ppCommandLists);

	hr = m_SwapChain->Present(m_SyncInterval, 0);
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

void Renderer::EndRender()
{
}

bool Renderer::InitializeRHI()
{
	CreateDevice();
	if (!m_Factory || !m_Device)
	{
		return false;
	}

	CreateCommandQueue();
	if (!m_CommandQueue)
	{
		return false;
	}

	CreateSwapChain();
	if (!m_SwapChain)
	{
		return false;
	}

	CreateRenderTargetViews();
	if (!m_RtvHeap)
	{
		return false;
	}
	for (UINT n = 0; n < FrameCount; n++)
	{
		if (!m_RenderTargets[n])
		{
			return false;
		}
	}

	CreateCommandObjects();
	if (!m_CommandList)
	{
		return false;
	}
	for (UINT n = 0; n < FrameCount; n++)
	{
		if (!m_CommandAllocators[n])
		{
			return false;
		}
	}

	CreateSyncObjects();
	if (!m_Fence || m_FenceEvent == nullptr)
	{
		return false;
	}

	return true;
}

void Renderer::SetVSyncEnabled(bool bEnabled)
{
	m_SyncInterval = bEnabled ? 1 : 0;
}

void Renderer::Tick()
{
	if (!bInitialize || !m_Window)
	{
		return;
	}

	uint32_t width = 0, height = 0;
	m_Window->PollFramebufferSize(width, height);
	if (width == 0 || height == 0)
	{
		// Minimized or occluded: idle instead of spinning a 100% CPU loop.
		m_Window->WaitEventsTimeout(0.05);
		return;
	}

	if (m_Width != width || m_Height != height)
	{
		m_Width = width;
		m_Height = height;
		ResizeSwapChain();
	}

	BeginRender();
	ExecuteRender();
	Present();
	EndRender();
}

void Renderer::CreateDevice(const LUID* adapterLuid)
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
	ComPtr<IDXGIAdapter1> hardwareAdapter;
	if (adapterLuid)
	{
		hr = m_Factory->EnumAdapterByLuid(*adapterLuid, IID_PPV_ARGS(&hardwareAdapter));
		if (FAILED(hr))
		{
			return;
		}
	}
	else
		GetHardwareAdapter(m_Factory.Get(), &hardwareAdapter);

	hr = D3D12CreateDevice(hardwareAdapter.Get(), D3D_FEATURE_LEVEL_11_0, IID_PPV_ARGS(&m_Device));
	if (FAILED(hr))
	{
		return;
	}
}

void Renderer::CreateCommandQueue()
{
	D3D12_COMMAND_QUEUE_DESC queueDesc = {};
	queueDesc.Flags = D3D12_COMMAND_QUEUE_FLAG_NONE;
	queueDesc.Type = D3D12_COMMAND_LIST_TYPE_DIRECT;

	HRESULT hr = m_Device->CreateCommandQueue(&queueDesc, IID_PPV_ARGS(&m_CommandQueue));
	if (FAILED(hr))
	{
		return;
	}
}

void Renderer::CreateSwapChain()
{
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

	ComPtr<IDXGISwapChain1> swapChain;
	HRESULT hr = m_Factory->CreateSwapChainForHwnd(
		m_CommandQueue.Get(),
		hwnd,
		&swapChainDesc,
		nullptr,
		nullptr,
		&swapChain);
	if (FAILED(hr))
	{
		return;
	}
	hr = swapChain.As(&m_SwapChain);
	if (FAILED(hr))
	{
		return;
	}
	m_FrameIndex = m_SwapChain->GetCurrentBackBufferIndex();
}

void Renderer::CreateRenderTargetViews()
{
	if (!m_RtvHeap)
	{
		D3D12_DESCRIPTOR_HEAP_DESC rtvHeapDesc = {};
		rtvHeapDesc.NumDescriptors = FrameCount;
		rtvHeapDesc.Type = D3D12_DESCRIPTOR_HEAP_TYPE_RTV;
		rtvHeapDesc.Flags = D3D12_DESCRIPTOR_HEAP_FLAG_NONE;

		HRESULT hr = m_Device->CreateDescriptorHeap(&rtvHeapDesc, IID_PPV_ARGS(&m_RtvHeap));
		if (FAILED(hr))
		{
			return;
		}
		m_RtvDescriptorSize = m_Device->GetDescriptorHandleIncrementSize(D3D12_DESCRIPTOR_HEAP_TYPE_RTV);
	}

	CD3DX12_CPU_DESCRIPTOR_HANDLE rtvHandle(m_RtvHeap->GetCPUDescriptorHandleForHeapStart());
	for (UINT n = 0; n < FrameCount; n++)
	{
		HRESULT hr = m_SwapChain->GetBuffer(n, IID_PPV_ARGS(&m_RenderTargets[n]));
		if (FAILED(hr))
		{
			return;
		}
		m_Device->CreateRenderTargetView(m_RenderTargets[n].Get(), nullptr, rtvHandle);
		rtvHandle.Offset(1, m_RtvDescriptorSize);
	}
}

void Renderer::CreateCommandObjects()
{
	for (UINT n = 0; n < FrameCount; n++)
	{
		HRESULT hr = m_Device->CreateCommandAllocator(D3D12_COMMAND_LIST_TYPE_DIRECT, IID_PPV_ARGS(&m_CommandAllocators[n]));
		if (FAILED(hr))
		{
			return;
		}
	}

	HRESULT hr = m_Device->CreateCommandList(0, D3D12_COMMAND_LIST_TYPE_DIRECT, m_CommandAllocators[m_FrameIndex].Get(), nullptr,
		IID_PPV_ARGS(&m_CommandList));
	if (FAILED(hr))
	{
		return;
	}
	hr = m_CommandList->Close();
	if (FAILED(hr))
	{
		return;
	}
}

void Renderer::CreateSyncObjects()
{
	HRESULT hr = m_Device->CreateFence(0, D3D12_FENCE_FLAG_NONE, IID_PPV_ARGS(&m_Fence));
	if (FAILED(hr))
	{
		return;
	}
	m_FenceEvent = CreateEvent(nullptr, FALSE, FALSE, nullptr);
	if (m_FenceEvent == nullptr)
	{
		hr = HRESULT_FROM_WIN32(GetLastError());
		if (FAILED(hr))
		{
			return;
		}
	}

	WaitForGpu();
}

void Renderer::ResizeSwapChain()
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

void Renderer::WaitForGpu()
{
	const UINT64 fenceToWaitFor = m_NextFenceValue++;
	HRESULT hr = (m_CommandQueue->Signal(m_Fence.Get(), fenceToWaitFor));
	if (FAILED(hr))
	{
		return;
	}
	WaitForFence(m_Fence.Get(), fenceToWaitFor);
}

void Renderer::MoveToNextFrame()
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

void Renderer::GetHardwareAdapter(IDXGIFactory1* factory, IDXGIAdapter1** ppAdapter)
{
	*ppAdapter = nullptr;

	ComPtr<IDXGIAdapter1> adapter;
	ComPtr<IDXGIFactory6> factory6;
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

void Renderer::WaitForFence(ID3D12Fence* fence, UINT64 value)
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
