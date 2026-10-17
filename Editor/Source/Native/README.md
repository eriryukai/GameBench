# Native interop

This folder is reserved for everything that talks to the native C++ engine
(`Engine.exe`) from the managed editor: the single interop entry point and any IPC
client / session code.

Nothing lives here yet. The editor is currently a standalone UI shell, and the
engine is launched separately. When interop lands, it belongs here so that
`Source/Managed/` stays free of native concerns.
