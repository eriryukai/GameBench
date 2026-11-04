#include "stdafx.h"
#include "Renderer/ImGui/ImGuiRenderer.h"

namespace
{
	ImGuiKey ToImGuiKey(ViewportKey Key)
	{
		if (Key >= ViewportKey::A && Key <= ViewportKey::Z)
		{
			return static_cast<ImGuiKey>(ImGuiKey_A + (static_cast<int>(Key) - static_cast<int>(ViewportKey::A)));
		}
		if (Key >= ViewportKey::D0 && Key <= ViewportKey::D9)
		{
			return static_cast<ImGuiKey>(ImGuiKey_0 + (static_cast<int>(Key) - static_cast<int>(ViewportKey::D0)));
		}
		if (Key >= ViewportKey::F1 && Key <= ViewportKey::F12)
		{
			return static_cast<ImGuiKey>(ImGuiKey_F1 + (static_cast<int>(Key) - static_cast<int>(ViewportKey::F1)));
		}

		switch (Key)
		{
		case ViewportKey::Enter: return ImGuiKey_Enter;
		case ViewportKey::Escape: return ImGuiKey_Escape;
		case ViewportKey::Tab: return ImGuiKey_Tab;
		case ViewportKey::Backspace: return ImGuiKey_Backspace;
		case ViewportKey::Space: return ImGuiKey_Space;
		case ViewportKey::Left: return ImGuiKey_LeftArrow;
		case ViewportKey::Right: return ImGuiKey_RightArrow;
		case ViewportKey::Up: return ImGuiKey_UpArrow;
		case ViewportKey::Down: return ImGuiKey_DownArrow;
		case ViewportKey::Home: return ImGuiKey_Home;
		case ViewportKey::End: return ImGuiKey_End;
		case ViewportKey::PageUp: return ImGuiKey_PageUp;
		case ViewportKey::PageDown: return ImGuiKey_PageDown;
		case ViewportKey::Delete: return ImGuiKey_Delete;
		case ViewportKey::Insert: return ImGuiKey_Insert;
		case ViewportKey::LeftShift: return ImGuiKey_LeftShift;
		case ViewportKey::RightShift: return ImGuiKey_RightShift;
		case ViewportKey::LeftCtrl: return ImGuiKey_LeftCtrl;
		case ViewportKey::RightCtrl: return ImGuiKey_RightCtrl;
		case ViewportKey::LeftAlt: return ImGuiKey_LeftAlt;
		case ViewportKey::RightAlt: return ImGuiKey_RightAlt;
		case ViewportKey::Comma: return ImGuiKey_Comma;
		case ViewportKey::Period: return ImGuiKey_Period;
		case ViewportKey::Minus: return ImGuiKey_Minus;
		case ViewportKey::Equal: return ImGuiKey_Equal;
		case ViewportKey::Semicolon: return ImGuiKey_Semicolon;
		case ViewportKey::Slash: return ImGuiKey_Slash;
		case ViewportKey::Backslash: return ImGuiKey_Backslash;
		case ViewportKey::Apostrophe: return ImGuiKey_Apostrophe;
		case ViewportKey::Grave: return ImGuiKey_GraveAccent;
		case ViewportKey::LeftBracket: return ImGuiKey_LeftBracket;
		case ViewportKey::RightBracket: return ImGuiKey_RightBracket;
		default: return ImGuiKey_None;
		}
	}
}

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
	if (bGlfwInitialized)
	{
		ImGui_ImplGlfw_Shutdown();
		bGlfwInitialized = false;
	}
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

	ApplyPendingInput();

	if (!bGlfwInitialized)
	{
		ImGuiIO& IO = ImGui::GetIO();
		if (Specification.ViewportWidth > 0 && Specification.ViewportHeight > 0)
		{
			IO.DisplaySize = ImVec2(
				static_cast<float>(Specification.ViewportWidth),
				static_cast<float>(Specification.ViewportHeight));
		}
		IO.DeltaTime = Specification.DeltaTime;
	}

	ImGui_ImplDX12_NewFrame();
	if (bGlfwInitialized)
	{
		ImGui_ImplGlfw_NewFrame();
	}
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

void ImGuiRenderer::SubmitInput(const std::vector<ViewportInputEvent>& Events)
{
	if (Events.empty())
	{
		return;
	}

	std::lock_guard<std::mutex> Lock(InputMutex);
	PendingInput.insert(PendingInput.end(), Events.begin(), Events.end());
}

void ImGuiRenderer::ApplyPendingInput()
{
	std::vector<ViewportInputEvent> Events;
	{
		std::lock_guard<std::mutex> Lock(InputMutex);
		Events.swap(PendingInput);
	}

	if (Events.empty())
	{
		return;
	}

	ImGuiIO& IO = ImGui::GetIO();

	for (const ViewportInputEvent& Event : Events)
	{
		switch (Event.Type)
		{
		case ViewportInputType::MouseMove:
			IO.AddMousePosEvent(Event.X, Event.Y);
			break;
		case ViewportInputType::MouseButton:
			IO.AddKeyEvent(ImGuiMod_Ctrl, (Event.Modifiers & 2u) != 0);
			IO.AddKeyEvent(ImGuiMod_Shift, (Event.Modifiers & 4u) != 0);
			IO.AddKeyEvent(ImGuiMod_Alt, (Event.Modifiers & 1u) != 0);
			IO.AddKeyEvent(ImGuiMod_Super, (Event.Modifiers & 8u) != 0);
			IO.AddMouseButtonEvent(Event.Button, Event.Down != 0);
			break;
		case ViewportInputType::MouseWheel:
			IO.AddMouseWheelEvent(0.0f, Event.Wheel);
			break;
		case ViewportInputType::Key:
			IO.AddKeyEvent(ImGuiMod_Ctrl, (Event.Modifiers & 2u) != 0);
			IO.AddKeyEvent(ImGuiMod_Shift, (Event.Modifiers & 4u) != 0);
			IO.AddKeyEvent(ImGuiMod_Alt, (Event.Modifiers & 1u) != 0);
			IO.AddKeyEvent(ImGuiMod_Super, (Event.Modifiers & 8u) != 0);
			IO.AddKeyEvent(ToImGuiKey(static_cast<ViewportKey>(Event.Key)), Event.Down != 0);
			break;
		case ViewportInputType::Text:
			IO.AddInputCharacter(Event.Codepoint);
			break;
		case ViewportInputType::Focus:
			IO.AddFocusEvent(Event.Down != 0);
			break;
		}
	}
}

void ImGuiRenderer::SetupStyles()
{
	ImGui::StyleColorsDark();
}

bool ImGuiRenderer::InitializeBackend()
{
	if (!Specification.Device || !Specification.CommandQueue)
	{
		std::print("[ImGuiRenderer] Initialize failed: missing renderer resources.\n");
		return false;
	}

	// Windowed mode uses GLFW for input and display size. Editor/headless mode
	// has no platform window; ImGui is driven by DisplaySize/DeltaTime and the
	// editor input bridge instead.
	if (Specification.WindowHandle)
	{
		if (!ImGui_ImplGlfw_InitForOther(static_cast<GLFWwindow*>(Specification.WindowHandle), true))
		{
			std::print("[ImGuiRenderer] ImGui_ImplGlfw_InitForOther failed.\n");
			return false;
		}
		bGlfwInitialized = true;
	}

	if (!CreateSrvHeap())
	{
		if (bGlfwInitialized)
		{
			ImGui_ImplGlfw_Shutdown();
			bGlfwInitialized = false;
		}
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
		if (bGlfwInitialized)
		{
			ImGui_ImplGlfw_Shutdown();
			bGlfwInitialized = false;
		}
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
