using System;
using Mafi;
using Mafi.Unity;
using Mafi.Unity.InputControl;
using Mafi.Unity.Ui;
using Mafi.Unity.Ui.Hud;
using Mafi.Unity.UiStatic.Toolbar;
using UnityEngine;

namespace ProductionOverview;

public sealed class ProductionOverviewController : IToolbarItemController, IUnityInputController, IInputControllerWithPin
{
	private const string ICON = "Assets/Unity/UserInterface/General/UptimeStats.svg";

	private readonly ProductionOverviewWindow m_window;

	private readonly UiContext m_context;

	private bool m_isActive;

	public ControllerConfig Config => ControllerConfig.Window;

	public bool IsVisible => true;

	public bool IsPinned => m_window.IsPinned;

	public bool DeactivateShortcutsIfNotVisible => false;

	public event Action<IToolbarItemController> VisibilityChanged;

	public ProductionOverviewController(ProductionOverviewWindow window, ToolbarHud toolbar, UiContext context)
	{
		m_window = window;
		m_context = context;
		toolbar.AddMainMenuButton(Tr.WindowTitle, this, ICON, 1450f,
			(ShortcutsManager _) => KeyBindings.FromKey(KbCategory.Windows, ShortcutMode.Game, KeyCode.F9));
		m_window.OnCloseStart += onWindowClose;
		VisibilityChanged?.Invoke(this);
	}

	public void Activate()
	{
		if (m_isActive)
		{
			return;
		}
		m_isActive = true;
		m_window.RefreshIfStale();
		m_window.Open(m_context.UiRoot);
	}

	public void Deactivate()
	{
		if (!m_isActive)
		{
			return;
		}
		m_isActive = false;
		m_window.Close();
	}

	public bool InputUpdate()
	{
		return false;
	}

	private void onWindowClose(Mafi.Unity.UiToolkit.Library.Window _)
	{
		if (m_isActive)
		{
			m_context.InputMgr.DeactivateController(this);
		}
	}
}
