// Copyright (c) CreationArt. All Rights Reserved.

#pragma once

#include "Renderer/FrameGraph.h"

#include <d3d12.h>
#include <wrl/client.h>

class DX12TriangleFrameGraph : public FrameGraph
{
public:
	DX12TriangleFrameGraph();

	void Initialize(ID3D12Device* device, DXGI_FORMAT rtvFormat);
	void Execute(const FrameGraphSpecification& spec) override;

private:
	ID3D12Device* m_Device = nullptr;
	DXGI_FORMAT m_RtvFormat = DXGI_FORMAT_UNKNOWN;

	Microsoft::WRL::ComPtr<ID3D12RootSignature> m_RootSignature;
	Microsoft::WRL::ComPtr<ID3D12PipelineState> m_PipelineState;
};
