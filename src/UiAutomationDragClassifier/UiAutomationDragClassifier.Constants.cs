using Interop.UIAutomationClient;

namespace ThreeFingerDrag;

internal static partial class UiAutomationDragClassifier
{
	// Long-running UIA calls are abandoned after this time. The caller then falls back to the next classifier.
	private static int ClassifyTimeoutMilliseconds => PointerSettings.GetUiAutomationClassifyTimeoutMs();

	// Control types which usually accept direct mouse input, but are not necessarily draggable.
	// Lists/items and data items are intentionally excluded: these commonly support drag-and-drop.
	private static readonly HashSet<int> InteractiveControlTypes =
	[
		UIA_ControlTypeIds.UIA_ButtonControlTypeId,
		UIA_ControlTypeIds.UIA_CalendarControlTypeId,
		UIA_ControlTypeIds.UIA_CheckBoxControlTypeId,
		UIA_ControlTypeIds.UIA_ComboBoxControlTypeId,
		UIA_ControlTypeIds.UIA_EditControlTypeId,
		UIA_ControlTypeIds.UIA_HyperlinkControlTypeId,
		UIA_ControlTypeIds.UIA_ImageControlTypeId,
		UIA_ControlTypeIds.UIA_MenuControlTypeId,
		UIA_ControlTypeIds.UIA_MenuBarControlTypeId,
		UIA_ControlTypeIds.UIA_MenuItemControlTypeId,
		UIA_ControlTypeIds.UIA_RadioButtonControlTypeId,
		UIA_ControlTypeIds.UIA_ScrollBarControlTypeId,
		UIA_ControlTypeIds.UIA_SliderControlTypeId,
		UIA_ControlTypeIds.UIA_SpinnerControlTypeId,
		UIA_ControlTypeIds.UIA_TabControlTypeId,
		UIA_ControlTypeIds.UIA_TabItemControlTypeId,
		UIA_ControlTypeIds.UIA_TextControlTypeId,
		UIA_ControlTypeIds.UIA_ToolBarControlTypeId,
		UIA_ControlTypeIds.UIA_ThumbControlTypeId,
		UIA_ControlTypeIds.UIA_DocumentControlTypeId,
		UIA_ControlTypeIds.UIA_SplitButtonControlTypeId,
		UIA_ControlTypeIds.UIA_HeaderItemControlTypeId,
		UIA_ControlTypeIds.UIA_TableControlTypeId,
	];

	// Patterns that mark a custom control as interactive when its control type is generic, such as a Pane or Custom element.
	private static readonly int[] SupplementaryInteractivePatternIds =
	[
		UIA_PatternIds.UIA_InvokePatternId,
		UIA_PatternIds.UIA_TogglePatternId,
		UIA_PatternIds.UIA_SelectionItemPatternId,
		UIA_PatternIds.UIA_ExpandCollapsePatternId,
	];
}
