// Copyright (c) CreationArt. All Rights Reserved.

#include "stdafx.h"
#include "Renderer/Renderer.h"
#include "Renderer/ShaderLibrary.h"
#include "OS/Window/Window.h"
#include "Renderer/DX12Check.h"

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
		try { WaitForGpu(); }
		catch (const std::exception& error) { OutputDebugStringA(error.what()); }
	}
#if WITH_EDITOR
	DestroyViewportTargets();
	m_ViewportContext.Reset();
	m_ViewportDevice.Reset();
	m_EditorViewport = false;
#endif

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
	CheckDX12(hr, __FUNCTION__);
	hr = m_CommandList->Reset(m_CommandAllocators[m_FrameIndex].Get(), nullptr);
	CheckDX12(hr, __FUNCTION__);
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
	CD3DX12_CPU_DESCRIPTOR_HANDLE rtv(m_RtvHeap->GetCPUDescriptorHandleForHeapStart(), m_FrameIndex, m_RtvDescriptorSize);
	const float clearColor[] = { 0.035f, 0.065f, 0.11f, 1.0f };
	m_CommandList->OMSetRenderTargets(1, &rtv, FALSE, nullptr);
	m_CommandList->ClearRenderTargetView(rtv, clearColor, 0, nullptr);

	const D3D12_VIEWPORT viewport = { 0.0f, 0.0f, static_cast<float>(m_Width), static_cast<float>(m_Height), 0.0f, 1.0f };
	const D3D12_RECT scissor = { 0, 0, static_cast<LONG>(m_Width), static_cast<LONG>(m_Height) };
	m_CommandList->RSSetViewports(1, &viewport);
	m_CommandList->RSSetScissorRects(1, &scissor);
	m_CommandList->SetGraphicsRootSignature(m_RootSignature.Get());
	m_CommandList->SetPipelineState(m_PipelineState.Get());
	m_CommandList->IASetPrimitiveTopology(D3D_PRIMITIVE_TOPOLOGY_TRIANGLELIST);
	// Triangle.hlsl generates its three vertices from SV_VertexID.
	m_CommandList->DrawInstanced(3, 1, 0, 0);
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
	CheckDX12(hr, __FUNCTION__);
	ID3D12CommandList* ppCommandLists[] = { m_CommandList.Get() };
	m_CommandQueue->ExecuteCommandLists(_countof(ppCommandLists), ppCommandLists);

#if WITH_EDITOR
	if (m_EditorViewport)
	{
		SubmitViewportFrame();
		return;
	}
#endif

	hr = m_SwapChain->Present(1, 0);
	if (hr == DXGI_ERROR_DEVICE_REMOVED || hr == DXGI_ERROR_DEVICE_RESET)
	{
		throw std::runtime_error("DX12 device removed/reset during Present.");
	}
	CheckDX12(hr, __FUNCTION__);
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
	CreateTrianglePipeline();

	return true;
}

void Renderer::Tick()
{
	if (!bInitialize)
	{
		return;
	}

	int width = 0, height = 0;
	glfwGetFramebufferSize(static_cast<GLFWwindow*>(m_Window->GetNativeHandle()), &width, &height);
	if (width <= 0 || height <= 0)
	{
		glfwWaitEventsTimeout(0.05);
		return;
	}
	if (m_Width != static_cast<UINT>(width) || m_Height != static_cast<UINT>(height))
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

// ---------------------------------------------------------------------------
// DX12 bring-up
// ---------------------------------------------------------------------------

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
	CheckDX12(hr, __FUNCTION__);
	ComPtr<IDXGIAdapter1> hardwareAdapter;
	if (adapterLuid)
		CheckDX12(m_Factory->EnumAdapterByLuid(*adapterLuid, IID_PPV_ARGS(&hardwareAdapter)), "Find compositor GPU");
	else
		GetHardwareAdapter(m_Factory.Get(), &hardwareAdapter);

	hr = D3D12CreateDevice(hardwareAdapter.Get(), D3D_FEATURE_LEVEL_11_0, IID_PPV_ARGS(&m_Device));
	CheckDX12(hr, __FUNCTION__);
}

void Renderer::CreateCommandQueue()
{
	D3D12_COMMAND_QUEUE_DESC queueDesc = {};
	queueDesc.Flags = D3D12_COMMAND_QUEUE_FLAG_NONE;
	queueDesc.Type = D3D12_COMMAND_LIST_TYPE_DIRECT;

	HRESULT hr = m_Device->CreateCommandQueue(&queueDesc, IID_PPV_ARGS(&m_CommandQueue));
	CheckDX12(hr, __FUNCTION__);
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

	const HWND hwnd = glfwGetWin32Window(static_cast<GLFWwindow*>(m_Window->GetNativeHandle()));

	ComPtr<IDXGISwapChain1> swapChain;
	HRESULT hr = m_Factory->CreateSwapChainForHwnd(
		m_CommandQueue.Get(),
		hwnd,
		&swapChainDesc,
		nullptr,
		nullptr,
		&swapChain);
	CheckDX12(hr, __FUNCTION__);
	
	hr = swapChain.As(&m_SwapChain);
	CheckDX12(hr, __FUNCTION__);

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
		CheckDX12(hr, __FUNCTION__);

		m_RtvDescriptorSize = m_Device->GetDescriptorHandleIncrementSize(D3D12_DESCRIPTOR_HEAP_TYPE_RTV);
	}

	CD3DX12_CPU_DESCRIPTOR_HANDLE rtvHandle(m_RtvHeap->GetCPUDescriptorHandleForHeapStart());
	for (UINT n = 0; n < FrameCount; n++)
	{
		HRESULT hr = m_SwapChain->GetBuffer(n, IID_PPV_ARGS(&m_RenderTargets[n]));
		CheckDX12(hr, __FUNCTION__);

		m_Device->CreateRenderTargetView(m_RenderTargets[n].Get(), nullptr, rtvHandle);
		rtvHandle.Offset(1, m_RtvDescriptorSize);
	}
}

void Renderer::CreateCommandObjects()
{
	for (UINT n = 0; n < FrameCount; n++)
	{
		HRESULT hr = m_Device->CreateCommandAllocator(D3D12_COMMAND_LIST_TYPE_DIRECT, IID_PPV_ARGS(&m_CommandAllocators[n]));
		CheckDX12(hr, __FUNCTION__);
	}

	HRESULT hr = m_Device->CreateCommandList(0, D3D12_COMMAND_LIST_TYPE_DIRECT, m_CommandAllocators[m_FrameIndex].Get(), nullptr,
		IID_PPV_ARGS(&m_CommandList));
	CheckDX12(hr, __FUNCTION__);

	hr = m_CommandList->Close();
	CheckDX12(hr, __FUNCTION__);
}

void Renderer::CreateSyncObjects()
{
	HRESULT hr = m_Device->CreateFence(0, D3D12_FENCE_FLAG_NONE, IID_PPV_ARGS(&m_Fence));
	CheckDX12(hr, __FUNCTION__);

	m_FenceEvent = CreateEvent(nullptr, FALSE, FALSE, nullptr);
	if (m_FenceEvent == nullptr)
	{
		hr = HRESULT_FROM_WIN32(GetLastError());
		CheckDX12(hr, __FUNCTION__);
	}

	WaitForGpu();
}

void Renderer::CreateTrianglePipeline()
{
	const CD3DX12_ROOT_SIGNATURE_DESC rootSignatureDesc(0, nullptr, 0, nullptr);
	ComPtr<ID3DBlob> signature;
	CheckDX12(D3D12SerializeRootSignature(&rootSignatureDesc, D3D_ROOT_SIGNATURE_VERSION_1,
		&signature, nullptr), "Serialize triangle root signature");
	CheckDX12(m_Device->CreateRootSignature(0, signature->GetBufferPointer(), signature->GetBufferSize(),
		IID_PPV_ARGS(&m_RootSignature)), "Create triangle root signature");

	const ShaderLibrary shaders;
	const auto vertexShader = shaders.Load("Triangle", "VSMain");
	const auto pixelShader = shaders.Load("Triangle", "PSMain");
	D3D12_GRAPHICS_PIPELINE_STATE_DESC pipeline = {};
	pipeline.pRootSignature = m_RootSignature.Get();
	pipeline.VS = { vertexShader.data(), vertexShader.size() };
	pipeline.PS = { pixelShader.data(), pixelShader.size() };
	pipeline.BlendState = CD3DX12_BLEND_DESC(D3D12_DEFAULT);
	pipeline.RasterizerState = CD3DX12_RASTERIZER_DESC(D3D12_DEFAULT);
	pipeline.RasterizerState.CullMode = D3D12_CULL_MODE_NONE;
	pipeline.DepthStencilState = CD3DX12_DEPTH_STENCIL_DESC(D3D12_DEFAULT);
	pipeline.DepthStencilState.DepthEnable = FALSE;
	pipeline.DepthStencilState.StencilEnable = FALSE;
	pipeline.SampleMask = UINT_MAX;
	pipeline.PrimitiveTopologyType = D3D12_PRIMITIVE_TOPOLOGY_TYPE_TRIANGLE;
	pipeline.NumRenderTargets = 1;
	pipeline.RTVFormats[0] = m_RtvFormat;
	pipeline.SampleDesc.Count = 1;
	CheckDX12(m_Device->CreateGraphicsPipelineState(&pipeline, IID_PPV_ARGS(&m_PipelineState)),
		"Create triangle pipeline");
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
	CheckDX12(hr, __FUNCTION__);

	hr = m_SwapChain->ResizeBuffers(FrameCount, m_Width, m_Height, m_RtvFormat, desc.Flags);
	CheckDX12(hr, __FUNCTION__);

	m_FrameIndex = m_SwapChain->GetCurrentBackBufferIndex();
	CreateRenderTargetViews();
}

void Renderer::WaitForGpu()
{
	const UINT64 fenceToWaitFor = m_NextFenceValue++;
	HRESULT hr = (m_CommandQueue->Signal(m_Fence.Get(), fenceToWaitFor));
	CheckDX12(hr, __FUNCTION__);

	WaitForFence(m_Fence.Get(), fenceToWaitFor);
}

void Renderer::MoveToNextFrame()
{
	const UINT64 currentFenceValue = m_NextFenceValue++;
	HRESULT hr = m_CommandQueue->Signal(m_Fence.Get(), currentFenceValue);
	CheckDX12(hr, __FUNCTION__);

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
		CheckDX12(m_Device->GetDeviceRemovedReason(), "GPU device removed");
	if (fence->GetCompletedValue() >= value)
		return;
	CheckDX12(fence->SetEventOnCompletion(value, m_FenceEvent), "Set fence completion");
	if (WaitForSingleObject(m_FenceEvent, 5000) != WAIT_OBJECT_0)
		throw std::runtime_error("Timed out waiting for the GPU.");
}
