#include "stdafx.h"
#include "Renderer/ShaderLibrary.h"
#include "GraphicUtility.h"

std::vector<uint8_t> ShaderLibrary::Load(const std::string& name, const std::string& entryPoint) const
{
	const std::string fileName = name + "_" + entryPoint + ".dxil";
	const std::filesystem::path path = GraphicUtility::ExecutableDirectory() / "Shaders" / "DXIL" / fileName;
	return GraphicUtility::ReadBinary(path);
}
