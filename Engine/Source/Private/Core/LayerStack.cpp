// Copyright (c) CreationArt. All Rights Reserved.

#include "stdafx.h"
#include "LayerStack.h"

#include <algorithm>

LayerStack::LayerStack() = default;

LayerStack::~LayerStack() = default;

void LayerStack::PushLayer(Layer* layer)
{
	if (!layer)
	{
		return;
	}

	m_Layers.emplace(m_Layers.begin() + m_LayerInsertIndex, layer);
	m_LayerInsertIndex++;
}

void LayerStack::PushOverlay(Layer* overlay)
{
	if (!overlay)
	{
		return;
	}

	m_Layers.emplace_back(overlay);
}

void LayerStack::PopLayer(Layer* layer)
{
	auto it = std::find(m_Layers.begin(), m_Layers.end(), layer);
	if (it == m_Layers.end())
	{
		return;
	}

	m_Layers.erase(it);

	// Only layers that sit in front of the overlay block occupy an insert slot, and
	// the erase above may have removed one of them. Never underflow.
	if (m_LayerInsertIndex > 0)
	{
		m_LayerInsertIndex--;
	}
}

void LayerStack::PopOverlay(Layer* overlay)
{
	auto it = std::find(m_Layers.begin(), m_Layers.end(), overlay);
	if (it != m_Layers.end())
	{
		m_Layers.erase(it);
	}
}

void LayerStack::Clear()
{
	m_Layers.clear();
	m_LayerInsertIndex = 0;
}

Layer* LayerStack::operator[](size_t index)
{
	return m_Layers[index];
}

const Layer* LayerStack::operator[](size_t index) const
{
	return m_Layers[index];
}
