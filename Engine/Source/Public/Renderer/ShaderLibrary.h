#pragma once

class ShaderLibrary
{
public:
	ShaderLibrary() = default;
	
	std::vector<uint8_t> Load(const std::string& name, const std::string& entryPoint) const;
};
