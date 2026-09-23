// Copyright (c) CreationArt. All Rights Reserved.

#include "stdafx.h"
#include "Renderer/Renderer.h"
#include "OS/Window/Window.h"

#include <d3d12.h>
#include <d3dx12/d3dx12.h>
#include <dxgi1_6.h>
#include <wrl/client.h>

#define GLFW_INCLUDE_NONE
#include <GLFW/glfw3.h>
#define GLFW_EXPOSE_NATIVE_WIN32
#include <GLFW/glfw3native.h>

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
		WaitForGpu();
	}

	if (m_FenceEvent)
	{
		CloseHandle(m_FenceEvent);
		m_FenceEvent = nullptr;
	}

	bInitialize = false;
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
	if (!bInitialize)
	{
		return;
	}
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

	hr = m_SwapChain->Present(1, 0);
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
	CreateCommandQueue();
	CreateSwapChain();
	CreateRenderTargetViews();
	CreateCommandObjects();
	CreateSyncObjects();

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

// ---------------------------------------------------------------------------
// DX12 bring-up
// ---------------------------------------------------------------------------

void Renderer::CreateDevice()
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
	swapChainDesc.Format = m_RtvFormat;
	swapChainDesc.BufferUsage = DXGI_USAGE_RENDER_TARGET_OUTPUT;
	swapChainDesc.SwapEffect = DXGI_SWAP_EFFECT_FLIP_DISCARD;
	swapChainDesc.SampleDesc.Count = 1;

	const HWND hwnd = glfwGetWin32Window(static_cast<GLFWwindow*>(m_Window->GetNativeHandle()));

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
	HRESULT hr = m_Device->CreateFence(m_FenceValues[m_FrameIndex], D3D12_FENCE_FLAG_NONE, IID_PPV_ARGS(&m_Fence));
	if (FAILED(hr))
	{
		return;
	}

	m_FenceValues[m_FrameIndex]++;

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

	hr = m_SwapChain->ResizeBuffers(FrameCount, m_Window->GetWidth(), m_Window->GetHeight(), m_RtvFormat, desc.Flags);
	if (FAILED(hr))
	{
		return;
	}

	m_FrameIndex = m_SwapChain->GetCurrentBackBufferIndex();
	CreateRenderTargetViews();
}

void Renderer::WaitForGpu()
{
	const UINT64 fenceToWaitFor = m_FenceValues[m_FrameIndex];
	HRESULT hr = (m_CommandQueue->Signal(m_Fence.Get(), fenceToWaitFor));
	if (FAILED(hr))
	{
		return;
	}

	hr = m_Fence->SetEventOnCompletion(fenceToWaitFor, m_FenceEvent);
	if (FAILED(hr))
	{
		return;
	}
	WaitForSingleObject(m_FenceEvent, INFINITE);

	m_FenceValues[m_FrameIndex]++;
}

void Renderer::MoveToNextFrame()
{
	const UINT64 currentFenceValue = m_FenceValues[m_FrameIndex];
	HRESULT hr = m_CommandQueue->Signal(m_Fence.Get(), currentFenceValue);
	if (FAILED(hr))
	{
		return;
	}

	m_FrameIndex = m_SwapChain->GetCurrentBackBufferIndex();

	if (m_Fence->GetCompletedValue() < m_FenceValues[m_FrameIndex])
	{
		hr = m_Fence->SetEventOnCompletion(m_FenceValues[m_FrameIndex], m_FenceEvent);
		if (FAILED(hr))
		{
			return;
		}

		WaitForSingleObject(m_FenceEvent, INFINITE);
	}

	m_FenceValues[m_FrameIndex] = currentFenceValue + 1;
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
