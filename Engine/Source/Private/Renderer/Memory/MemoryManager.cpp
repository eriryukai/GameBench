#include "stdafx.h"
#include "Renderer/Memory/MemoryManager.h"

namespace Graphic {

	namespace Memory
	{
		MemoryManager::MemoryManager()
		{
			
		}

		MemoryManager::~MemoryManager()
		{

		}

		bool MemoryManager::Initialize()
		{
			bHasInitialized = true;
			return bHasInitialized;
		}

		bool MemoryManager::UnInitialize()
		{
			bHasInitialized = false;
			return bHasInitialized;
		}

		bool MemoryManager::HasInitialized()
		{
			return bHasInitialized;
		}

		MemorySpecification MemoryManager::GetSpecification() const
		{
			return Specification;
		}
	}
}
