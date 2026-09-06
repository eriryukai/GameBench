// Copyright (c) CreationArt. All Rights Reserved.

#include "stdafx.h"
#include "Renderer/Platform/DX12/DX12TriangleFrameGraph.h"
#include "Renderer/ShaderLibrary.h"
#include "OS/Window/Window.h"

#include <d3d12.h>
#include <d3dx12/d3dx12.h>
#include <wrl/client.h>

using Microsoft::WRL::ComPtr;

namespace
{
	void ThrowIfFailed(HRESULT hr, const char* what)
	{
		if (FAILED(hr))
		{
			char buf[128];
			sprintf_s(buf, "%s (HRESULT 0x%08X)", what, static_cast<unsigned>(hr));
			throw std::runtime_error(buf);
		}
	}
}

DX12TriangleFrameGraph::DX12TriangleFrameGraph()
	: FrameGraph("TriangleFrameGraph")
{
}

void DX12TriangleFrameGraph::Initialize(ID3D12Device* device, DXGI_FORMAT rtvFormat)
{
	m_Device = device;
	m_RtvFormat = rtvFormat;

	// Empty root signature: the shader takes no external resources.
	{
		CD3DX12_ROOT_SIGNATURE_DESC rootSignatureDesc;
		rootSignatureDesc.Init(0, nullptr, 0, nullptr, D3D12_ROOT_SIGNATURE_FLAG_ALLOW_INPUT_ASSEMBLER_INPUT_LAYOUT);

		ComPtr<ID3DBlob> signature;
		ComPtr<ID3DBlob> error;
		ThrowIfFailed(D3D12SerializeRootSignature(&rootSignatureDesc, D3D_ROOT_SIGNATURE_VERSION_1, &signature, &error),
			"D3D12SerializeRootSignature failed");
		ThrowIfFailed(device->CreateRootSignature(0, signature->GetBufferPointer(), signature->GetBufferSize(), IID_PPV_ARGS(&m_RootSignature)),
			"CreateRootSignature failed");
	}

	// Pipeline state object.
	{
		const ShaderLibrary shaderLibrary;
		const std::vector<uint8_t> vsBytecode = shaderLibrary.Load("Triangle", "VSMain");
		const std::vector<uint8_t> psBytecode = shaderLibrary.Load("Triangle", "PSMain");

		D3D12_GRAPHICS_PIPELINE_STATE_DESC psoDesc = {};
		psoDesc.InputLayout = { nullptr, 0 };
		psoDesc.pRootSignature = m_RootSignature.Get();
		psoDesc.VS = CD3DX12_SHADER_BYTECODE(vsBytecode.data(), vsBytecode.size());
		psoDesc.PS = CD3DX12_SHADER_BYTECODE(psBytecode.data(), psBytecode.size());
		psoDesc.RasterizerState = CD3DX12_RASTERIZER_DESC(D3D12_DEFAULT);
		psoDesc.BlendState = CD3DX12_BLEND_DESC(D3D12_DEFAULT);
		psoDesc.DepthStencilState.DepthEnable = FALSE;
		psoDesc.DepthStencilState.StencilEnable = FALSE;
		psoDesc.SampleMask = UINT_MAX;
		psoDesc.PrimitiveTopologyType = D3D12_PRIMITIVE_TOPOLOGY_TYPE_TRIANGLE;
		psoDesc.NumRenderTargets = 1;
		psoDesc.RTVFormats[0] = rtvFormat;
		psoDesc.SampleDesc.Count = 1;
		ThrowIfFailed(device->CreateGraphicsPipelineState(&psoDesc, IID_PPV_ARGS(&m_PipelineState)),
			"CreateGraphicsPipelineState failed");
	}
}

void DX12TriangleFrameGraph::Execute(const FrameGraphSpecification& spec)
{
	auto* commandList = static_cast<ID3D12GraphicsCommandList*>(spec.NativeCommandList);
	const auto rtvHandle = *static_cast<D3D12_CPU_DESCRIPTOR_HANDLE*>(spec.NativeTargetView);

	// Negative-height viewport to flip Y axis so shared Triangle.hlsl renders upright.
	const CD3DX12_VIEWPORT viewport(
		0.0f,
		static_cast<float>(m_Window->GetHeight()),
		static_cast<float>(m_Window->GetWidth()),
		-static_cast<float>(m_Window->GetHeight()));
	const CD3DX12_RECT scissor(0, 0, static_cast<LONG>(m_Window->GetWidth()), static_cast<LONG>(m_Window->GetHeight()));

	commandList->SetGraphicsRootSignature(m_RootSignature.Get());
	commandList->SetPipelineState(m_PipelineState.Get());
	commandList->RSSetViewports(1, &viewport);
	commandList->RSSetScissorRects(1, &scissor);

	commandList->OMSetRenderTargets(1, &rtvHandle, FALSE, nullptr);

	const float clearColor[] = { 0.0f, 0.0f, 0.0f, 1.0f };
	commandList->ClearRenderTargetView(rtvHandle, clearColor, 0, nullptr);

	commandList->IASetPrimitiveTopology(D3D_PRIMITIVE_TOPOLOGY_TRIANGLELIST);
	commandList->DrawInstanced(3, 1, 0, 0);
}
