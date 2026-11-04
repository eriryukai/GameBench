// Copyright (c) CreationArt. All Rights Reserved.

#include "stdafx.h"
#include "Renderer/Viewport/ViewportFrameRing.h"

#include <print>

ViewportFrameRing::~ViewportFrameRing()
{
	Shutdown();
}

bool ViewportFrameRing::CreateD3D11Device(LUID AdapterLuid)
{
	Microsoft::WRL::ComPtr<IDXGIFactory4> Factory;
	HRESULT Result = CreateDXGIFactory1(IID_PPV_ARGS(&Factory));
	if (FAILED(Result) || !Factory)
	{
		return false;
	}

	Microsoft::WRL::ComPtr<IDXGIAdapter1> Adapter;
	Result = Factory->EnumAdapterByLuid(AdapterLuid, IID_PPV_ARGS(&Adapter));
	if (FAILED(Result) || !Adapter)
	{
		return false;
	}

	UINT Flags = 0;
#if defined(_DEBUG)
	Flags |= D3D11_CREATE_DEVICE_DEBUG;
#endif

	const D3D_FEATURE_LEVEL Levels[] =
	{
		D3D_FEATURE_LEVEL_11_1,
		D3D_FEATURE_LEVEL_11_0,
		D3D_FEATURE_LEVEL_10_1,
		D3D_FEATURE_LEVEL_10_0,
	};

	D3D_FEATURE_LEVEL ActualLevel = D3D_FEATURE_LEVEL_11_0;
	Result = D3D11CreateDevice(
		Adapter.Get(),
		D3D_DRIVER_TYPE_UNKNOWN,
		nullptr,
		Flags,
		Levels,
		_countof(Levels),
		D3D11_SDK_VERSION,
		&D3D11Device,
		&ActualLevel,
		nullptr);

	if (FAILED(Result) && (Flags & D3D11_CREATE_DEVICE_DEBUG))
	{
		// Debug layers are optional on machines without the D3D11 SDK layers.
		Flags &= ~D3D11_CREATE_DEVICE_DEBUG;
		Result = D3D11CreateDevice(
			Adapter.Get(),
			D3D_DRIVER_TYPE_UNKNOWN,
			nullptr,
			Flags,
			Levels,
			_countof(Levels),
			D3D11_SDK_VERSION,
			&D3D11Device,
			&ActualLevel,
			nullptr);
	}

	if (FAILED(Result) || !D3D11Device)
	{
		return false;
	}

	return true;
}

bool ViewportFrameRing::Initialize(ID3D12Device* InDevice, uint32_t InWidth, uint32_t InHeight)
{
	Shutdown();

	if (!InDevice || InWidth == 0 || InHeight == 0)
	{
		return false;
	}

	Device = InDevice;
	Width = InWidth;
	Height = InHeight;

	if (!CreateD3D11Device(Device->GetAdapterLuid()))
	{
		Shutdown();
		return false;
	}

	D3D12_DESCRIPTOR_HEAP_DESC HeapDesc = {};
	HeapDesc.NumDescriptors = SlotCount;
	HeapDesc.Type = D3D12_DESCRIPTOR_HEAP_TYPE_RTV;
	HeapDesc.Flags = D3D12_DESCRIPTOR_HEAP_FLAG_NONE;

	HRESULT Result = Device->CreateDescriptorHeap(&HeapDesc, IID_PPV_ARGS(&RtvHeap));
	if (FAILED(Result))
	{
		std::print("[ViewportFrameRing] Failed to create the RTV descriptor heap.\n");
		Shutdown();
		return false;
	}

	RtvDescriptorSize = Device->GetDescriptorHandleIncrementSize(D3D12_DESCRIPTOR_HEAP_TYPE_RTV);

	// The shared texture must be created by D3D11 for Avalonia's ANGLE importer
	// to find IDXGIKeyedMutex. D3D12 opens the same allocation for rendering.
	D3D11_TEXTURE2D_DESC TextureDesc = {};
	TextureDesc.Width = Width;
	TextureDesc.Height = Height;
	TextureDesc.MipLevels = 1;
	TextureDesc.ArraySize = 1;
	TextureDesc.Format = Format;
	TextureDesc.SampleDesc.Count = 1;
	TextureDesc.SampleDesc.Quality = 0;
	TextureDesc.Usage = D3D11_USAGE_DEFAULT;
	TextureDesc.BindFlags = D3D11_BIND_RENDER_TARGET | D3D11_BIND_SHADER_RESOURCE;
	TextureDesc.CPUAccessFlags = 0;
	TextureDesc.MiscFlags =
		D3D11_RESOURCE_MISC_SHARED_NTHANDLE |
		D3D11_RESOURCE_MISC_SHARED_KEYEDMUTEX;

	D3D12_CPU_DESCRIPTOR_HANDLE RtvHandle = RtvHeap->GetCPUDescriptorHandleForHeapStart();

	for (uint32_t Index = 0; Index < SlotCount; ++Index)
	{
		Slot& Current = Slots[Index];

		Result = D3D11Device->CreateTexture2D(&TextureDesc, nullptr, &Current.D3D11Texture);
		if (FAILED(Result) || !Current.D3D11Texture)
		{
			Shutdown();
			return false;
		}

		Result = Current.D3D11Texture.As(&Current.KeyedMutex);
		if (FAILED(Result) || !Current.KeyedMutex)
		{
			Shutdown();
			return false;
		}

		Microsoft::WRL::ComPtr<IDXGIResource1> SharedResource;
		Result = Current.D3D11Texture.As(&SharedResource);
		if (FAILED(Result) || !SharedResource)
		{
			Shutdown();
			return false;
		}

		Result = SharedResource->CreateSharedHandle(
			nullptr,
			GENERIC_ALL,
			nullptr,
			&Current.SharedHandle);
		if (FAILED(Result) || Current.SharedHandle == nullptr)
		{
			Shutdown();
			return false;
		}

		Result = Device->OpenSharedHandle(
			Current.SharedHandle,
			IID_PPV_ARGS(&Current.Resource));
		if (FAILED(Result) || !Current.Resource)
		{
			Shutdown();
			return false;
		}

		Device->CreateRenderTargetView(Current.Resource.Get(), nullptr, RtvHandle);
		Current.Rtv = RtvHandle;
		Current.State = SlotState::Free;
		Current.PublishedFrameId = 0;
		Current.bKeyAcquired = false;

		RtvHandle.ptr += RtvDescriptorSize;
	}

	bInitialized = true;
	return true;
}

void ViewportFrameRing::Shutdown()
{
	std::lock_guard<std::mutex> Lock(SlotsMutex);

	DestroySlots();
	D3D11Device.Reset();
	Device = nullptr;
	Width = 0;
	Height = 0;
	bInitialized = false;
}

void ViewportFrameRing::DestroySlots()
{
	for (Slot& Current : Slots)
	{
		if (Current.bKeyAcquired && Current.KeyedMutex)
		{
			Current.KeyedMutex->ReleaseSync(0);
			Current.bKeyAcquired = false;
		}

		if (Current.SharedHandle != nullptr)
		{
			CloseHandle(Current.SharedHandle);
			Current.SharedHandle = nullptr;
		}

		Current.Resource.Reset();
		Current.KeyedMutex.Reset();
		Current.D3D11Texture.Reset();
		Current.Rtv = {};
		Current.PublishedFrameId = 0;
		Current.State = SlotState::Free;
	}

	RtvHeap.Reset();
	RtvDescriptorSize = 0;
}

int ViewportFrameRing::AcquireWritableSlot()
{
	std::lock_guard<std::mutex> Lock(SlotsMutex);

	for (uint32_t Index = 0; Index < SlotCount; ++Index)
	{
		Slot& Current = Slots[Index];
		if (Current.State != SlotState::Free || !Current.KeyedMutex)
		{
			continue;
		}

		// The slot was acknowledged by the editor, so key 0 should be free.
		// Use a bounded wait so a stuck consumer cannot deadlock the render
		// thread; skipping a frame is preferable.
		const HRESULT Result = Current.KeyedMutex->AcquireSync(0, 1000);
		if (FAILED(Result))
		{
			continue;
		}

		Current.bKeyAcquired = true;
		Current.State = SlotState::Rendering;
		Current.PublishedFrameId = 0;
		return static_cast<int>(Index);
	}

	return -1;
}

void ViewportFrameRing::PublishSlot(uint32_t SlotIndex, uint64_t FrameId)
{
	if (SlotIndex >= SlotCount)
	{
		return;
	}

	std::lock_guard<std::mutex> Lock(SlotsMutex);

	Slot& Current = Slots[SlotIndex];
	if (Current.bKeyAcquired && Current.KeyedMutex)
	{
		Current.KeyedMutex->ReleaseSync(1);
		Current.bKeyAcquired = false;
	}

	Current.PublishedFrameId = FrameId;
	Current.State = SlotState::Ready;
}

void ViewportFrameRing::Acknowledge(uint32_t SlotIndex, uint64_t FrameId)
{
	if (SlotIndex >= SlotCount)
	{
		return;
	}

	std::lock_guard<std::mutex> Lock(SlotsMutex);

	Slot& Current = Slots[SlotIndex];
	if (Current.State != SlotState::Ready && Current.State != SlotState::InFlight)
	{
		return;
	}

	// The frame id prevents an old acknowledgement from freeing a slot that has
	// already been re-published.
	if (FrameId != 0 && Current.PublishedFrameId != 0 && FrameId != Current.PublishedFrameId)
	{
		return;
	}

	Current.PublishedFrameId = 0;
	Current.State = SlotState::Free;
}
