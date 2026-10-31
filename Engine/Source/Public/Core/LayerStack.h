// Copyright (c) CreationArt. All Rights Reserved.

#pragma once

#include "Layer.h"

#include <cstddef>
#include <vector>

class LayerStack
{
public:
	LayerStack();
	~LayerStack();

	void PushLayer(Layer* layer);
	void PushOverlay(Layer* overlay);
	void PopLayer(Layer* layer);
	void PopOverlay(Layer* overlay);

	// Drops every reference without detaching or deleting. Only the owner of the
	// layers is allowed to call this, once it has dealt with them itself.
	void Clear();

	Layer* operator[](size_t index);
	const Layer* operator[](size_t index) const;

	size_t Size() const { return m_Layers.size(); }

	std::vector<Layer*>::iterator begin() { return m_Layers.begin(); }
	std::vector<Layer*>::iterator end() { return m_Layers.end(); }
	std::vector<Layer*>::const_iterator begin() const { return m_Layers.begin(); }
	std::vector<Layer*>::const_iterator end() const { return m_Layers.end(); }

private:
	std::vector<Layer*> m_Layers;
	size_t m_LayerInsertIndex = 0;
};
