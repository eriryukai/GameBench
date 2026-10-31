// Copyright (c) CreationArt. All Rights Reserved.

#pragma once

#include "Core/Application.h"

// The game's root framework class. EOC configures an Application while the
// Engine library owns the platform entry point and drives the runtime loop.
class EOCApplication final : public Application
{
public:
	explicit EOCApplication(const ApplicationSpecification& specification);
};
