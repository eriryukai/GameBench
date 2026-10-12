#pragma once
#include "Renderer/Memory/MemoryTypes.h"

namespace Graphic {

	namespace Memory {

		class MemoryManager
		{
		public:
			MemoryManager();
			~MemoryManager();

			bool Initialize();
			bool UnInitialize();
			bool HasInitialized();

			MemorySpecification GetSpecification() const;
		private:
			bool bHasInitialized = false;
			MemorySpecification Specification;
		};
	}
}