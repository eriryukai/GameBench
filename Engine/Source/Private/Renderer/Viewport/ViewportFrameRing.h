// Copyright (c) CreationArt. All Rights Reserved.

#pragma once

#include "Interop/EditorProtocol.h"

#include <d3d11.h>
#include <d3d12.h>
#include <dxgi1_6.h>
#include <wrl/client.h>

#include <array>
#include <cstdint>
#include <mutex>

// Three-slot D3D12 shared-texture ring used by the editor viewport.
//
// The engine renders into a Free slot, waits for the GPU, marks it Ready and
// publishes it over IPC. The editor imports the slot's NT handle once, copies it
// into its composition surface with UpdateAsync, then sends an acknowledgement.
// A slot is only reused after that acknowledgement.
class ViewportFrameRing
{
public:
	static constexpr uint32_t SlotCount = EditorProtocol::MaxSlots;
	static constexpr DXGI_FORMAT Format = DXGI_FORMAT_R8G8B8A8_UNORM;

	enum class SlotState : uint8_t
	{
		Free,
		Rendering,
		Ready,
		InFlight,
	};

	struct Slot
	{
		// The texture is created on a small D3D11 device because Avalonia's
		// ANGLE importer requires IDXGIKeyedMutex. D3D12 opens the same
		// allocation and performs all rendering commands.
		Microsoft::WRL::ComPtr<ID3D11Texture2D> D3D11Texture;
		Microsoft::WRL::ComPtr<IDXGIKeyedMutex> KeyedMutex;
		Microsoft::WRL::ComPtr<ID3D12Resource> Resource;
		D3D12_CPU_DESCRIPTOR_HANDLE Rtv = {};
		HANDLE SharedHandle = nullptr;
		uint64_t PublishedFrameId = 0;
		SlotState State = SlotState::Free;
		bool bKeyAcquired = false;
	};

	ViewportFrameRing() = default;
	~ViewportFrameRing();

	ViewportFrameRing(const ViewportFrameRing&) = delete;
	ViewportFrameRing& operator=(const ViewportFrameRing&) = delete;

	bool Initialize(ID3D12Device* Device, uint32_t Width, uint32_t Height);
	void Shutdown();

	bool IsInitialized() const { return bInitialized; }
	uint32_t GetWidth() const { return Width; }
	uint32_t GetHeight() const { return Height; }
	DXGI_FORMAT GetFormat() const { return Format; }

	// Returns -1 when all three slots are in flight. Never blocks the render thread.
	int AcquireWritableSlot();
	void PublishSlot(uint32_t SlotIndex, uint64_t FrameId);
	void Acknowledge(uint32_t SlotIndex, uint64_t FrameId);

	Slot& GetSlot(uint32_t SlotIndex) { return Slots[SlotIndex]; }
	const Slot& GetSlot(uint32_t SlotIndex) const { return Slots[SlotIndex]; }

private:
	bool CreateD3D11Device(LUID AdapterLuid);
	void DestroySlots();

	ID3D12Device* Device = nullptr;
	Microsoft::WRL::ComPtr<ID3D11Device> D3D11Device;
	Microsoft::WRL::ComPtr<ID3D12DescriptorHeap> RtvHeap;
	uint32_t RtvDescriptorSize = 0;
	uint32_t Width = 0;
	uint32_t Height = 0;
	bool bInitialized = false;

	std::array<Slot, SlotCount> Slots = {};
	mutable std::mutex SlotsMutex;
};
