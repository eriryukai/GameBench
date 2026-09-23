// Copyright (c) CreationArt. All Rights Reserved.
#include "stdafx.h"
#include "Renderer/Renderer.h"
#include "Renderer/DX12Check.h"
#include <d3dx12/d3dx12.h>

#if WITH_EDITOR
void Renderer::InitializeViewport(const LUID& adapterLuid)
{
	UnInitialize();
	m_EditorViewport = true;
	CreateDevice(&adapterLuid);
	CreateCommandQueue();
	// Avalonia's ANGLE backend imports keyed-mutex D3D11 images, not D3D12 fences.
	// This device only copies the DX12 result into the compositor's shared image.
	Microsoft::WRL::ComPtr<IDXGIAdapter1> adapter;
	CheckDX12(m_Factory->EnumAdapterByLuid(adapterLuid, IID_PPV_ARGS(&adapter)), "Find viewport copy GPU");
	Microsoft::WRL::ComPtr<ID3D11Device> copyDevice;
	CheckDX12(D3D11CreateDevice(adapter.Get(), D3D_DRIVER_TYPE_UNKNOWN, nullptr,
		D3D11_CREATE_DEVICE_BGRA_SUPPORT, nullptr, 0, D3D11_SDK_VERSION,
		&copyDevice, nullptr, &m_ViewportContext), "Create viewport copy device");
	CheckDX12(copyDevice.As(&m_ViewportDevice), "Query viewport copy device");
	D3D12_DESCRIPTOR_HEAP_DESC heap{};
	heap.Type = D3D12_DESCRIPTOR_HEAP_TYPE_RTV;
	heap.NumDescriptors = FrameCount;
	CheckDX12(m_Device->CreateDescriptorHeap(&heap, IID_PPV_ARGS(&m_RtvHeap)), "Create viewport RTV heap");
	m_RtvDescriptorSize = m_Device->GetDescriptorHandleIncrementSize(heap.Type);
	CreateCommandObjects();
	CreateSyncObjects();
	CreateTrianglePipeline();
	bInitialize = true;
}

void Renderer::DestroyViewportTargets()
{
	for (auto& frame : m_ViewportTargets)
	{
		if (frame.ImageHandle) CloseHandle(frame.ImageHandle);
		frame = {};
	}
}

void Renderer::ResetViewport(uint32_t width, uint32_t height)
{
	if (!bInitialize || !m_EditorViewport)
		throw std::runtime_error("The editor renderer is not initialized.");
	if (!width || !height || width > D3D12_REQ_TEXTURE2D_U_OR_V_DIMENSION || height > D3D12_REQ_TEXTURE2D_U_OR_V_DIMENSION)
		throw std::runtime_error("Invalid viewport dimensions.");

	// Caller has awaited presentation and disposed all compositor imports.
	WaitForGpu();
	DestroyViewportTargets();
	for (auto& target : m_RenderTargets) target.Reset();
	m_Width = width;
	m_Height = height;
	m_FrameIndex = 0;
	++m_ViewportGeneration;

	CD3DX12_HEAP_PROPERTIES heap(D3D12_HEAP_TYPE_DEFAULT);
	auto desc = CD3DX12_RESOURCE_DESC::Tex2D(m_RtvFormat, width, height, 1, 1);
	desc.Flags = D3D12_RESOURCE_FLAG_ALLOW_RENDER_TARGET | D3D12_RESOURCE_FLAG_ALLOW_SIMULTANEOUS_ACCESS;
	CD3DX12_CPU_DESCRIPTOR_HANDLE rtv(m_RtvHeap->GetCPUDescriptorHandleForHeapStart());
	for (UINT i = 0; i < FrameCount; ++i)
	{
		auto& frame = m_ViewportTargets[i];
		CheckDX12(m_Device->CreateCommittedResource(&heap, D3D12_HEAP_FLAG_SHARED, &desc,
			D3D12_RESOURCE_STATE_COMMON, nullptr, IID_PPV_ARGS(&m_RenderTargets[i])), "Create shared viewport texture");
		m_Device->CreateRenderTargetView(m_RenderTargets[i].Get(), nullptr, rtv);
		rtv.Offset(1, m_RtvDescriptorSize);
		HANDLE sourceHandle = nullptr;
		CheckDX12(m_Device->CreateSharedHandle(m_RenderTargets[i].Get(), nullptr, GENERIC_ALL, nullptr, &sourceHandle), "Share DX12 render target");
		HRESULT opened = m_ViewportDevice->OpenSharedResource1(sourceHandle, IID_PPV_ARGS(&frame.Source));
		CloseHandle(sourceHandle);
		CheckDX12(opened, "Open DX12 render target for GPU copy");

		D3D11_TEXTURE2D_DESC shared{};
		shared.Width = width;
		shared.Height = height;
		shared.MipLevels = shared.ArraySize = 1;
		shared.Format = m_RtvFormat;
		shared.SampleDesc.Count = 1;
		shared.Usage = D3D11_USAGE_DEFAULT;
		shared.BindFlags = D3D11_BIND_RENDER_TARGET | D3D11_BIND_SHADER_RESOURCE;
		shared.MiscFlags = D3D11_RESOURCE_MISC_SHARED_NTHANDLE | D3D11_RESOURCE_MISC_SHARED_KEYEDMUTEX;
		CheckDX12(m_ViewportDevice->CreateTexture2D(&shared, nullptr, &frame.SharedImage), "Create keyed viewport image");
		CheckDX12(frame.SharedImage.As(&frame.Mutex), "Query viewport keyed mutex");
		Microsoft::WRL::ComPtr<IDXGIResource1> resource;
		CheckDX12(frame.SharedImage.As(&resource), "Query shared viewport resource");
		CheckDX12(resource->CreateSharedHandle(nullptr, DXGI_SHARED_RESOURCE_READ | DXGI_SHARED_RESOURCE_WRITE,
			nullptr, &frame.ImageHandle), "Export keyed viewport image");
	}
}

bool Renderer::RenderViewport(EditorViewportFrame& output)
{
	if (!bInitialize || !m_EditorViewport || !m_RenderTargets[m_FrameIndex])
		throw std::runtime_error("The viewport has no render target.");
	auto& frame = m_ViewportTargets[m_FrameIndex];
	CheckDX12(m_Device->GetDeviceRemovedReason(), "Viewport GPU");
	// Key 0 belongs to the producer; key 1 belongs to Avalonia. Nonblocking acquisition
	// also prevents a failed/abandoned presentation from hanging the engine's GPU queue.
	HRESULT acquired = frame.Mutex->AcquireSync(0, 0);
	if (acquired == WAIT_TIMEOUT) return false;
	if (acquired != S_OK) throw std::runtime_error("Could not acquire the shared viewport image.");
	UINT slot = m_FrameIndex;
	try
	{
		BeginRender();
		ExecuteRender();
		Present();
		EndRender();
	}
	catch (...)
	{
		frame.Mutex->ReleaseSync(0);
		throw;
	}
	output = { m_Width, m_Height, static_cast<uint32_t>(m_RtvFormat), slot, m_ViewportGeneration,
		frame.ImageHandle };
	return true;
}

void Renderer::SubmitViewportFrame()
{
	auto& frame = m_ViewportTargets[m_FrameIndex];
	// Only the fence is waited on the CPU; the pixels stay on the GPU. The keyed mutex
	// prevents both this copy and the next DX12 write racing Avalonia's snapshot.
	WaitForGpu();
	m_ViewportContext->CopyResource(frame.SharedImage.Get(), frame.Source.Get());
	m_ViewportContext->Flush();
	CheckDX12(m_ViewportDevice->GetDeviceRemovedReason(), "Viewport copy device");
	CheckDX12(frame.Mutex->ReleaseSync(1), "Release viewport image to compositor");
	m_FrameIndex = (m_FrameIndex + 1) % FrameCount;
}
#endif
