#include "stdafx.h"
#include "Renderer/ImGui/ImGuiRenderer.h"

ImGuiRenderer::ImGuiRenderer()
{
	
}

ImGuiRenderer::~ImGuiRenderer()
{
	UnInitialize();
}

bool ImGuiRenderer::Initialize(const ImGuiSpecification& Spec)
{
	if (bInitialized)
	{
		return true;
	}

	Specification = Spec;

	IMGUI_CHECKVERSION();
	ImGui::CreateContext();

	ImGuiIO& IO = ImGui::GetIO();
	if (Specification.bEnabledKeyboardControls)
	{
		IO.ConfigFlags |= ImGuiConfigFlags_NavEnableKeyboard;
	}
	if (Specification.bEnabledGamepadControls)
	{
		IO.ConfigFlags |= ImGuiConfigFlags_NavEnableGamepad;
	}

	SetupStyles();

	if (!InitializeBackend())
	{
		ImGui::DestroyContext();
		return false;
	}

	bInitialized = true;
	return true;
}

bool ImGuiRenderer::UnInitialize()
{
	if (!bInitialized)
	{
		return true;
	}

	ImGui_ImplDX12_Shutdown();
	ImGui_ImplGlfw_Shutdown();
	ImGui::DestroyContext();
	DestroySrvHeap();

	bInitialized = false;
	return true;
}

void ImGuiRenderer::BeginFrame()
{
	if (!bInitialized)
	{
		return;
	}

	ImGui_ImplDX12_NewFrame();
	ImGui_ImplGlfw_NewFrame();
	ImGui::NewFrame();
}

void ImGuiRenderer::Render()
{
	if (!bInitialized)
	{
		return;
	}

	DrawDemoUserInterface();
}

void ImGuiRenderer::DrawDemoUserInterface()
{
	if (Specification.bShowDemoWindow)
	{
		ImGui::ShowDemoWindow();
	}
}

void ImGuiRenderer::EndFrame(ID3D12GraphicsCommandList* CommandList)
{
	if (!bInitialized || !CommandList || !m_ImGuiSRVHeap)
	{
		return;
	}

	ImGui::Render();

	ID3D12DescriptorHeap* Heaps[] = { m_ImGuiSRVHeap.Get() };
	CommandList->SetDescriptorHeaps(_countof(Heaps), Heaps);

	ImGui_ImplDX12_RenderDrawData(ImGui::GetDrawData(), CommandList);
}

void ImGuiRenderer::SetupStyles()
{
	ImGui::StyleColorsDark();
}

bool ImGuiRenderer::InitializeBackend()
{
	if (!Specification.WindowHandle || !Specification.Device || !Specification.CommandQueue)
	{
		std::print("[ImGuiRenderer] Initialize failed: missing window or renderer resources.\n");
		return false;
	}

	// GLFW owns the window, so the platform backend hooks GLFW's input callbacks and
	// receives events through the engine's existing glfwPollEvents() pump.
	if (!ImGui_ImplGlfw_InitForOther(static_cast<GLFWwindow*>(Specification.WindowHandle), true))
	{
		std::print("[ImGuiRenderer] ImGui_ImplGlfw_InitForOther failed.\n");
		return false;
	}

	if (!CreateSrvHeap())
	{
		ImGui_ImplGlfw_Shutdown();
		return false;
	}

	ImGui_ImplDX12_InitInfo InitInfo = {};
	InitInfo.Device = Specification.Device;
	InitInfo.CommandQueue = Specification.CommandQueue;
	InitInfo.NumFramesInFlight = static_cast<int>(Specification.NumFramesInFlight);
	InitInfo.RTVFormat = Specification.RTVFormat;
	InitInfo.DSVFormat = Specification.DSVFormat;
	InitInfo.UserData = this;
	InitInfo.SrvDescriptorHeap = m_ImGuiSRVHeap.Get();
	InitInfo.SrvDescriptorAllocFn = &ImGuiRenderer::SrvDescriptorAlloc;
	InitInfo.SrvDescriptorFreeFn = &ImGuiRenderer::SrvDescriptorFree;

	if (!ImGui_ImplDX12_Init(&InitInfo))
	{
		std::print("[ImGuiRenderer] ImGui_ImplDX12_Init failed.\n");
		DestroySrvHeap();
		ImGui_ImplGlfw_Shutdown();
		return false;
	}

	return true;
}

bool ImGuiRenderer::CreateSrvHeap()
{
	D3D12_DESCRIPTOR_HEAP_DESC Desc = {};
	Desc.NumDescriptors = SrvHeapSize;
	Desc.Type = D3D12_DESCRIPTOR_HEAP_TYPE_CBV_SRV_UAV;
	Desc.Flags = D3D12_DESCRIPTOR_HEAP_FLAG_SHADER_VISIBLE;

	HRESULT hr = Specification.Device->CreateDescriptorHeap(&Desc, IID_PPV_ARGS(&m_ImGuiSRVHeap));
	if (FAILED(hr))
	{
		std::print("[ImGuiRenderer] Failed to create the SRV descriptor heap.\n");
		return false;
	}

	m_ImGuiSRVCpuStart = m_ImGuiSRVHeap->GetCPUDescriptorHandleForHeapStart();
	m_ImGuiSRVGpuStart = m_ImGuiSRVHeap->GetGPUDescriptorHandleForHeapStart();
	m_ImGuiSRVHandleIncrement = Specification.Device->GetDescriptorHandleIncrementSize(D3D12_DESCRIPTOR_HEAP_TYPE_CBV_SRV_UAV);

	m_FreeSrvIndices.clear();
	m_FreeSrvIndices.reserve(SrvHeapSize);
	// Push in reverse so the first allocation hands out descriptor 0.
	for (uint32_t Index = SrvHeapSize; Index > 0; --Index)
	{
		m_FreeSrvIndices.push_back(Index - 1);
	}

	return true;
}

void ImGuiRenderer::DestroySrvHeap()
{
	m_FreeSrvIndices.clear();
	m_ImGuiSRVHeap.Reset();
}

void ImGuiRenderer::AllocateSrvDescriptor(D3D12_CPU_DESCRIPTOR_HANDLE* OutCpu, D3D12_GPU_DESCRIPTOR_HANDLE* OutGpu)
{
	if (m_FreeSrvIndices.empty())
	{
		OutCpu->ptr = 0;
		OutGpu->ptr = 0;
		return;
	}

	const uint32_t Index = m_FreeSrvIndices.back();
	m_FreeSrvIndices.pop_back();

	OutCpu->ptr = m_ImGuiSRVCpuStart.ptr + static_cast<SIZE_T>(Index) * m_ImGuiSRVHandleIncrement;
	OutGpu->ptr = m_ImGuiSRVGpuStart.ptr + static_cast<UINT64>(Index) * m_ImGuiSRVHandleIncrement;
}

void ImGuiRenderer::FreeSrvDescriptor(D3D12_CPU_DESCRIPTOR_HANDLE Cpu, D3D12_GPU_DESCRIPTOR_HANDLE Gpu)
{
	(void)Gpu;

	if (Cpu.ptr == 0 || m_ImGuiSRVHandleIncrement == 0)
	{
		return;
	}

	const uint32_t Index = static_cast<uint32_t>((Cpu.ptr - m_ImGuiSRVCpuStart.ptr) / m_ImGuiSRVHandleIncrement);
	m_FreeSrvIndices.push_back(Index);
}

void ImGuiRenderer::SrvDescriptorAlloc(ImGui_ImplDX12_InitInfo* Info, D3D12_CPU_DESCRIPTOR_HANDLE* OutCpu, D3D12_GPU_DESCRIPTOR_HANDLE* OutGpu)
{
	static_cast<ImGuiRenderer*>(Info->UserData)->AllocateSrvDescriptor(OutCpu, OutGpu);
}

void ImGuiRenderer::SrvDescriptorFree(ImGui_ImplDX12_InitInfo* Info, D3D12_CPU_DESCRIPTOR_HANDLE Cpu, D3D12_GPU_DESCRIPTOR_HANDLE Gpu)
{
	static_cast<ImGuiRenderer*>(Info->UserData)->FreeSrvDescriptor(Cpu, Gpu);
}
