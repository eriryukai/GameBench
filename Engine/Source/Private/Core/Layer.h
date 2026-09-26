// Copyright (c) CreationArt. All Rights Reserved.

#pragma once

#include <string>

class Layer
{
public:
	Layer(const std::string& name = "Layer");
	virtual ~Layer();

	virtual void OnAttach() {}
	virtual void OnDetach() {}
	virtual void OnUpdate() {}
	virtual void OnImGuiRender() {}
	virtual void OnEvent() {}

	inline const std::string& GetName() const { return m_DebugName; }

protected:
	std::string m_DebugName;
};
