#pragma once

#include "imgui.h"
#include "imgui_impl_glfw.h"
#include "imgui_impl_dx12.h"

#include <d3d12.h>
#include <wrl/client.h>

#include <cstdint>
#include <vector>

struct ImGuiSpecification
{
	bool bEnabledKeyboardControls = false;
	bool bEnabledGamepadControls = false;
	bool bShowDemoWindow = false;
	void* WindowHandle = nullptr;

	// Renderer resources borrowed by this backend. Dear ImGui owns its own SRV heap.
	ID3D12Device* Device = nullptr;
	ID3D12CommandQueue* CommandQueue = nullptr;
	uint32_t NumFramesInFlight = 2;
	DXGI_FORMAT RTVFormat = DXGI_FORMAT_R8G8B8A8_UNORM;
	DXGI_FORMAT DSVFormat = DXGI_FORMAT_UNKNOWN;
};

class ImGuiRenderer
{
public:
	ImGuiRenderer();
	virtual ~ImGuiRenderer();

	bool Initialize(const ImGuiSpecification& Spec);
	bool UnInitialize();

	void BeginFrame();
	virtual void Render();
	void EndFrame(ID3D12GraphicsCommandList* CommandList);

	const ImGuiSpecification& GetSpecification() const { return Specification; }
	bool IsInitialized() const { return bInitialized; }

protected:
	virtual void DrawDemoUserInterface();

	void SetupStyles();
	bool InitializeBackend();

	bool CreateSrvHeap();
	void DestroySrvHeap();

	void AllocateSrvDescriptor(D3D12_CPU_DESCRIPTOR_HANDLE* OutCpu, D3D12_GPU_DESCRIPTOR_HANDLE* OutGpu);
	void FreeSrvDescriptor(D3D12_CPU_DESCRIPTOR_HANDLE Cpu, D3D12_GPU_DESCRIPTOR_HANDLE Gpu);

	static void SrvDescriptorAlloc(ImGui_ImplDX12_InitInfo* Info, D3D12_CPU_DESCRIPTOR_HANDLE* OutCpu, D3D12_GPU_DESCRIPTOR_HANDLE* OutGpu);
	static void SrvDescriptorFree(ImGui_ImplDX12_InitInfo* Info, D3D12_CPU_DESCRIPTOR_HANDLE Cpu, D3D12_GPU_DESCRIPTOR_HANDLE Gpu);

protected:
	ImGuiSpecification Specification;
	bool bInitialized = false;

	static const UINT SrvHeapSize = 64;
	Microsoft::WRL::ComPtr<ID3D12DescriptorHeap> m_ImGuiSRVHeap;
	D3D12_CPU_DESCRIPTOR_HANDLE m_ImGuiSRVCpuStart = {};
	D3D12_GPU_DESCRIPTOR_HANDLE m_ImGuiSRVGpuStart = {};
	UINT m_ImGuiSRVHandleIncrement = 0;
	std::vector<uint32_t> m_FreeSrvIndices;
};
