namespace ThreeFingerDrag;

internal static partial class MouseInject
{
	// MSLLHOOKSTRUCT.flags bit LLMHF_INJECTED. Set on input injected by any program,
	// including our own mouse_event and SendInput calls.
	private const uint LlmhfInjected = 0x00000001;
}