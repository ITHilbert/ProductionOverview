using System;
using System.Collections.Generic;
using System.Globalization;
using Mafi;
using Mafi.Core.Entities.Static;
using Mafi.Localization;
using Mafi.Unity.Camera;
using Mafi.Unity.Entities;
using Mafi.Unity.Ui;
using Mafi.Unity.UiToolkit.Component;
using Mafi.Unity.UiToolkit.Library;
using UnityEngine;
using Button = Mafi.Unity.UiToolkit.Library.Button;
using Column = Mafi.Unity.UiToolkit.Library.Column;
using Label = Mafi.Unity.UiToolkit.Library.Label;

namespace ProductionOverview;

public sealed class ProductionOverviewWindow : Window
{
	private const int MAX_ROWS = 300;

	private const int TYPE_BELOW_THRESHOLD = 80;

	private const int ICON_SIZE = 32;

	private const int COL_NAME = 300;

	private const int COL_EFFICIENCY = 170;

	private const int COL_ISSUE = 230;

	private const int COL_STATE = 150;

	private const int BAR_WIDTH = 100;

	/// <summary>"Below 80 %" and "Worst" in the type tab, narrower to leave room for the hide button.</summary>
	private const int COL_TYPE_STAT = 120;

	private const int CHECKBOX_WIDTH = 40;

	private const float HIDDEN_OPACITY = 0.45f;

	private static readonly int[] THRESHOLDS = { 100, 95, 80, 50 };

	private static readonly ColorRgba ROW_ALT_BG = ColorRgba.Black.SetA(45);

	private static readonly ColorRgba HEADER_BG = new ColorRgba(2040098, 140);

	private static readonly ColorRgba BAR_BG = ColorRgba.White.SetA(25);

	private static readonly ColorRgba GOOD = new ColorRgba(0x4CAF50);

	private static readonly ColorRgba MEDIUM = new ColorRgba(0xE0B020);

	private static readonly ColorRgba BAD = new ColorRgba(0xE05040);

	private static readonly ColorRgba MUTED = new ColorRgba(0x9A9A9A);

	private static readonly ColorRgba HIGHLIGHT = ColorRgba.CornflowerBlue;

	private readonly ProductionStatsCollector m_collector;

	private readonly CameraController m_camera;

	private readonly InspectorsManager m_inspectors;

	private readonly EntitiesRenderingManager m_rendering;

	private readonly TabContainer m_tabs;

	private readonly Column m_buildingRows = new Column().AlignItemsStretch();

	private readonly Column m_typeRows = new Column().AlignItemsStretch();

	private readonly Label m_summary = new Label();

	private readonly TextField m_filter = new TextField();

	private readonly Dropdown<StatsPeriod> m_period;

	private readonly Dropdown<int> m_threshold;

	private readonly Dropdown<SortMode> m_sort;

	private readonly List<ulong> m_highlights = new List<ulong>();

	private List<BuildingStats> m_stats = new List<BuildingStats>();

	private bool m_hidePaused = true;

	private bool m_hideOutputFull = true;

	private bool m_hideAuxiliary = true;

	private readonly HiddenTypes m_hiddenTypes = new HiddenTypes();

	private readonly Label m_hiddenTypesInfo = new Label();

	private readonly ButtonText m_showAllTypes;

	private const float STALE_AFTER_SECONDS = 120f;

	private float m_lastRefresh = -1f;

	public ProductionOverviewWindow(ProductionStatsCollector collector, CameraController camera, InspectorsManager inspectors, EntitiesRenderingManager rendering)
		: base(Tr.WindowTitle)
	{
		m_collector = collector;
		m_camera = camera;
		m_inspectors = inspectors;
		m_rendering = rendering;

		WindowSize(new Px(1000f), new Px(760f));
		MakeMovableAndEnablePositionSaving();
		EnablePinning();
		OnCloseStart += _ => clearHighlights();

		m_period = new Dropdown<StatsPeriod>((StatsPeriod p, int _, bool _) => new Label(Tr.PeriodName(p)));
		m_period.SetOptions(StatsPeriod.CurrentMonth, StatsPeriod.LastMonth, StatsPeriod.Last3Months, StatsPeriod.Last12Months);
		m_period.SetValue(StatsPeriod.Last3Months);
		m_period.OnValueChanged((StatsPeriod _, int _) => Refresh());

		m_threshold = new Dropdown<int>((int t, int _, bool _) => new Label(Tr.ThresholdName(t)));
		m_threshold.SetOptions(THRESHOLDS);
		m_threshold.SetValue(100);
		m_threshold.OnValueChanged((int _, int _) => rebuild());

		m_sort = new Dropdown<SortMode>((SortMode m, int _, bool _) => new Label(Tr.SortName(m)));
		m_sort.SetOptions(SortMode.EfficiencyAsc, SortMode.EfficiencyDesc, SortMode.Name, SortMode.Type);
		m_sort.SetValue(SortMode.EfficiencyAsc);
		m_sort.OnValueChanged((SortMode _, int _) => rebuild());

		m_filter.CharLimit(80).Placeholder(Tr.FilterPlaceholder).FlexGrow(1f).MinWidth(200.px())
			.OnValueChanged(_ => rebuild());

		Row controls1 = new Row(2.pt()).AlignItemsCenter();
		controls1.Add(m_filter);
		controls1.Add(new Label(Tr.Period));
		controls1.Add(m_period.NoShrink().MinWidth(170.px()));
		controls1.Add(new ButtonText(Button.General, Tr.Refresh, Refresh));

		Row controls2 = new Row(2.pt()).AlignItemsCenter();
		controls2.Add(new Label(Tr.Show));
		controls2.Add(m_threshold.NoShrink().MinWidth(130.px()));
		controls2.Add(new Label(Tr.Sort));
		controls2.Add(m_sort.NoShrink().MinWidth(250.px()));

		Row controls3 = new Row(2.pt()).AlignItemsCenter();
		controls3.Add(new Toggle(standalone: true).Value(m_hidePaused).OnValueChanged(v =>
		{
			m_hidePaused = v;
			rebuild();
		}));
		controls3.Add(new Label(Tr.HidePaused).MarginRight(4.pt()));
		controls3.Add(new Toggle(standalone: true).Value(m_hideOutputFull).OnValueChanged(v =>
		{
			m_hideOutputFull = v;
			rebuild();
		}).Tooltip(Tr.HideOutputFullTooltip));
		controls3.Add(new Label(Tr.HideOutputFull).Tooltip(Tr.HideOutputFullTooltip).MarginRight(4.pt()));
		controls3.Add(new Toggle(standalone: true).Value(m_hideAuxiliary).OnValueChanged(v =>
		{
			m_hideAuxiliary = v;
			rebuild();
		}).Tooltip(Tr.HideAuxiliaryTooltip));
		controls3.Add(new Label(Tr.HideAuxiliary).Tooltip(Tr.HideAuxiliaryTooltip));
		controls3.Add(m_hiddenTypesInfo.Color(MUTED).MarginLeft(4.pt()));
		m_showAllTypes = new ButtonText(Button.General, Tr.ResetTypes, () =>
		{
			m_hiddenTypes.Reset();
			rebuild();
		}).Tooltip(Tr.ResetTypesTooltip);
		controls3.Add(m_showAllTypes);

		Panel top = new Panel(noBolts: true).ReducedPadding().BodyGap(2.pt()).AlignSelfStretch();
		top.Body.Add(controls1);
		top.Body.Add(controls2);
		top.Body.Add(controls3);
		top.Body.Add(m_summary);
		Body.Add(top);

		m_tabs = new TabContainer();
		m_tabs.Add(Tr.TabBuildings, m_buildingRows);
		m_tabs.Add(Tr.TabByType, m_typeRows);
		m_tabs.OnTabActivate(rebuild);
		Body.Add(m_tabs);
	}

	/// <summary>Re-reads the statistics from the game and redraws the lists.</summary>
	public void Refresh()
	{
		m_stats = m_collector.Collect(m_period.SelectedValue);
		m_lastRefresh = Time.realtimeSinceStartup;
		rebuild();
	}

	/// <summary>
	/// Keeps the list (and its scroll position) when the window is reopened shortly after jumping to a building.
	/// </summary>
	public void RefreshIfStale()
	{
		if (m_lastRefresh < 0f || Time.realtimeSinceStartup - m_lastRefresh > STALE_AFTER_SECONDS)
		{
			Refresh();
		}
	}

	private int threshold => m_threshold.SelectedValue <= 0 ? 100 : m_threshold.SelectedValue;

	private void rebuild()
	{
		clearHighlights();
		List<BuildingStats> visible = updateHeader();
		if (m_tabs.ActiveTabIndex.GetValueOrDefault() == 1)
		{
			// Excluded types stay in this list (greyed out), so their checkbox can be ticked again.
			rebuildTypes(filtered(applyTypeSelection: false));
		}
		else
		{
			rebuildBuildings(visible);
		}
	}

	/// <summary>Summary line and type selection info. Returns the buildings that pass all filters.</summary>
	private List<BuildingStats> updateHeader()
	{
		List<BuildingStats> visible = filtered(applyTypeSelection: true);
		updateSummary(visible);
		int hiddenTypeCount = countHiddenTypes();
		m_hiddenTypesInfo.Value(Tr.HiddenTypesInfo(hiddenTypeCount));
		m_hiddenTypesInfo.Visible(m_hideAuxiliary && hiddenTypeCount > 0);
		m_showAllTypes.Visible(m_hiddenTypes.OverrideCount > 0);
		return visible;
	}

	private int countHiddenTypes()
	{
		HashSet<string> hidden = new HashSet<string>();
		foreach (BuildingStats s in m_stats)
		{
			if (isAuxiliary(s))
			{
				hidden.Add(s.ProtoId);
			}
		}
		return hidden.Count;
	}

	private List<BuildingStats> filtered(bool applyTypeSelection)
	{
		string filter = m_filter.GetText()?.Trim() ?? "";
		int limit = threshold;
		List<BuildingStats> result = new List<BuildingStats>();
		foreach (BuildingStats s in m_stats)
		{
			if (s.Entity.IsDestroyed || !s.IsConstructed)
			{
				continue;
			}
			if (m_hidePaused && s.IsPaused)
			{
				continue;
			}
			if (m_hideOutputFull && s.MainIssueIsOutputFull)
			{
				continue;
			}
			if (applyTypeSelection && m_hideAuxiliary && isAuxiliary(s))
			{
				continue;
			}
			if (limit < 100 && (!s.HasData || s.Efficiency * 100f >= limit))
			{
				continue;
			}
			if (filter.Length > 0 && !contains(s.Name, filter) && !contains(s.TypeName, filter))
			{
				continue;
			}
			result.Add(s);
		}
		return result;
	}

	/// <summary>Built because something needs it, not to produce: waste disposal and types the player hid.</summary>
	private bool isAuxiliary(BuildingStats s) => m_hiddenTypes.IsHidden(s.ProtoId, s.IsWasteDisposal);

	private void updateSummary(List<BuildingStats> visible)
	{
		long productive = 0, total = 0;
		int below = 0;
		int limit = threshold < 100 ? threshold : TYPE_BELOW_THRESHOLD;
		foreach (BuildingStats s in visible)
		{
			if (!s.HasData)
			{
				continue;
			}
			productive += s.Productive;
			total += s.Total;
			if (s.Efficiency * 100f < limit)
			{
				below++;
			}
		}
		string avg = total > 0 ? formatPercent((float)productive / total) : "–";
		m_summary.Value(Tr.Summary(visible.Count, avg, below, limit));
	}

	// ---------------------------------------------------------------- Buildings tab

	private void rebuildBuildings(List<BuildingStats> visible)
	{
		visible.Sort(compare);
		m_buildingRows.Clear();
		m_buildingRows.Add(headerRow(Tr.ColName, Tr.ColEfficiency, Tr.ColMainIssue, Tr.ColState));
		if (visible.Count == 0)
		{
			m_buildingRows.Add(new Label(Tr.NothingFound).FontItalic().Padding(3.pt()));
			return;
		}
		int count = Math.Min(visible.Count, MAX_ROWS);
		for (int i = 0; i < count; i++)
		{
			m_buildingRows.Add(buildingRow(visible[i], i));
		}
		if (visible.Count > MAX_ROWS)
		{
			m_buildingRows.Add(new Label(Tr.Truncated(MAX_ROWS, visible.Count)).Padding(3.pt()));
		}
	}

	private UiComponent buildingRow(BuildingStats s, int index)
	{
		Row row = new Row(3.pt()).AlignItemsCenter().Padding(1.pt(), 2.pt());
		if (index % 2 == 1)
		{
			row.Background(ROW_ALT_BG);
		}

		row.Add(new Icon(s.IconPath).Size(ICON_SIZE.px()).NoShrink());

		Column name = new Column().Width(COL_NAME.px()).NoShrink();
		name.Add(new Label(new LocStrFormatted(s.Name)).FontBold());
		if (s.Name != s.TypeName)
		{
			name.Add(new Label(new LocStrFormatted(s.TypeName)).Color(MUTED).FontSize(11));
		}
		row.Add(name);

		row.Add(efficiencyCell(s));

		Label issue = new Label().Width(COL_ISSUE.px()).NoShrink();
		if (!s.HasData)
		{
			issue.Value(Tr.NoData).Color(MUTED);
		}
		else if (s.Losses.Count == 0)
		{
			issue.Value(Tr.NoIssue).Color(MUTED);
		}
		else
		{
			var main = s.Losses[0];
			issue.Value(new LocStrFormatted($"{main.Label} ({formatPercent((float)main.Count / s.Total)})"));
			issue.Color(lighten(main.Color));
		}
		row.Add(issue);

		string state = !s.IsConstructed ? Tr.UnderConstruction
			: s.IsPaused ? Tr.StateName("Paused")
			: s.StateName != null ? Tr.StateName(s.StateName)
			: "–";
		row.Add(new Label(new LocStrFormatted(state)).Width(COL_STATE.px()).NoShrink());

		row.Tooltip(new LocStrFormatted(tooltipFor(s)));
		row.OnClick(() => goTo(s));
		row.OnMouseEnterLeave(() => highlight(s), clearHighlights);
		return row;
	}

	private static string tooltipFor(BuildingStats s)
	{
		string text = Tr.RowTooltip.Value;
		if (!s.HasData)
		{
			return text + "\n" + Tr.NoDataTooltip.Value;
		}
		text += $"\n\n<b>{Tr.BreakdownHeader}</b>";
		text += "\n" + Tr.Breakdown(Tr.ColEfficiency.Value, formatPercent(s.Efficiency));
		foreach (var loss in s.Losses)
		{
			text += "\n" + Tr.Breakdown(loss.Label, formatPercent((float)loss.Count / s.Total));
		}
		return text;
	}

	private int compare(BuildingStats a, BuildingStats b)
	{
		int result;
		switch (m_sort.SelectedValue)
		{
			case SortMode.EfficiencyDesc:
				result = b.Efficiency.CompareTo(a.Efficiency);
				break;
			case SortMode.Name:
				result = compareText(a.Name, b.Name);
				break;
			case SortMode.Type:
				result = compareText(a.TypeName, b.TypeName);
				if (result == 0)
				{
					result = sortKey(a).CompareTo(sortKey(b));
				}
				break;
			default:
				result = sortKey(a).CompareTo(sortKey(b));
				break;
		}
		return result != 0 ? result : compareText(a.Name, b.Name);
	}

	/// <summary>Buildings without data go to the end when sorting worst-first.</summary>
	private static float sortKey(BuildingStats s) => s.HasData ? s.Efficiency : 2f;

	// ---------------------------------------------------------------- By type tab

	private sealed class TypeStats
	{
		public string TypeName;
		public string IconPath;
		public string ProtoId;
		public bool IsWasteDisposal;
		public readonly List<BuildingStats> Buildings = new List<BuildingStats>();
		public long Productive;
		public long Total;
		public int Below;
		public float Worst = 2f;

		public float Efficiency => Total >= BuildingStats.MIN_SAMPLES ? (float)Productive / Total : -1f;
	}

	private void rebuildTypes(List<BuildingStats> visible)
	{
		Dictionary<string, TypeStats> byType = new Dictionary<string, TypeStats>();
		foreach (BuildingStats s in visible)
		{
			if (!byType.TryGetValue(s.ProtoId, out TypeStats t))
			{
				t = new TypeStats { TypeName = s.TypeName, IconPath = s.IconPath, ProtoId = s.ProtoId, IsWasteDisposal = s.IsWasteDisposal };
				byType.Add(s.ProtoId, t);
			}
			t.Buildings.Add(s);
			if (s.HasData)
			{
				t.Productive += s.Productive;
				t.Total += s.Total;
				t.Worst = Math.Min(t.Worst, s.Efficiency);
				if (s.Efficiency * 100f < TYPE_BELOW_THRESHOLD)
				{
					t.Below++;
				}
			}
		}

		List<TypeStats> types = new List<TypeStats>(byType.Values);
		types.Sort((a, b) =>
		{
			bool aHidden = isHidden(a), bHidden = isHidden(b);
			if (aHidden != bHidden)
			{
				return aHidden ? 1 : -1; // Excluded types at the end.
			}
			int r = m_sort.SelectedValue switch
			{
				SortMode.EfficiencyDesc => b.Efficiency.CompareTo(a.Efficiency),
				SortMode.Name or SortMode.Type => 0,
				_ => (a.Efficiency < 0 ? 2f : a.Efficiency).CompareTo(b.Efficiency < 0 ? 2f : b.Efficiency),
			};
			return r != 0 ? r : compareText(a.TypeName, b.TypeName);
		});

		m_typeRows.Clear();
		m_typeRows.Add(headerRow(Tr.ColName, Tr.ColEfficiency, Tr.ColBelow, Tr.ColWorst, COL_TYPE_STAT, COL_TYPE_STAT, withCheckbox: true));
		if (types.Count == 0)
		{
			m_typeRows.Add(new Label(Tr.NothingFound).FontItalic().Padding(3.pt()));
			return;
		}
		for (int i = 0; i < types.Count; i++)
		{
			m_typeRows.Add(typeRow(types[i], i));
		}
	}

	private bool isHidden(TypeStats t) => m_hiddenTypes.IsHidden(t.ProtoId, t.IsWasteDisposal);

	private UiComponent typeRow(TypeStats t, int index)
	{
		Row row = new Row(3.pt()).AlignItemsCenter().Padding(1.pt(), 2.pt());
		if (index % 2 == 1)
		{
			row.Background(ROW_ALT_BG);
		}

		// Only this part filters on click, so the checkbox next to it does not trigger it.
		Row info = new Row(3.pt()).AlignItemsCenter().NoShrink();
		bool hidden = isHidden(t);
		info.Opacity(hidden ? HIDDEN_OPACITY : 1f);

		// The toggle keeps its natural size (squeezing it made it unrecognizable); the wrapper aligns the columns.
		Row checkboxCell = new Row().Width(CHECKBOX_WIDTH.px()).NoShrink().AlignItemsCenter();
		checkboxCell.Add(new Toggle(standalone: true).Value(!hidden).OnValueChanged(included =>
		{
			m_hiddenTypes.SetIncluded(t.ProtoId, t.IsWasteDisposal, included);
			// No full rebuild: the row stays where it is, so several boxes can be ticked in a row.
			info.Opacity(included ? 1f : HIDDEN_OPACITY);
			updateHeader();
		}).Tooltip(t.IsWasteDisposal ? Tr.TypeCheckboxAutoTooltip : Tr.TypeCheckboxTooltip));
		row.Add(checkboxCell);
		info.Add(new Icon(t.IconPath).Size(ICON_SIZE.px()).NoShrink());

		Column name = new Column().Width(COL_NAME.px()).NoShrink();
		name.Add(new Label(new LocStrFormatted(t.TypeName)).FontBold());
		name.Add(new Label(new LocStrFormatted($"{Tr.ColCount.Value}: {t.Buildings.Count}")).Color(MUTED).FontSize(11));
		info.Add(name);

		info.Add(barCell(t.Efficiency));

		info.Add(new Label(new LocStrFormatted($"{t.Below} / {t.Buildings.Count}"))
			.Color(t.Below > 0 ? BAD : MUTED).Width(COL_TYPE_STAT.px()).NoShrink());

		info.Add(new Label(new LocStrFormatted(t.Worst <= 1f ? formatPercent(t.Worst) : "–"))
			.Color(t.Worst <= 1f ? colorFor(t.Worst) : MUTED).Width(COL_TYPE_STAT.px()).NoShrink());

		info.Tooltip(Tr.TypeRowTooltip);
		info.OnClick(() =>
		{
			m_filter.Text(t.TypeName);
			m_tabs.SwitchToTab(0);
			rebuild();
		});
		row.Add(info);
		if (t.IsWasteDisposal)
		{
			row.Add(new Label(Tr.AutoAuxiliary).Color(MUTED).Tooltip(Tr.TypeCheckboxAutoTooltip));
		}

		row.OnMouseEnterLeave(() => highlight(t.Buildings), clearHighlights);
		return row;
	}

	// ---------------------------------------------------------------- Shared cells

	private static UiComponent headerRow(LocStrFormatted name, LocStrFormatted col2, LocStrFormatted col3, LocStrFormatted col4,
		int col3Width = COL_ISSUE, int col4Width = COL_STATE, bool withCheckbox = false)
	{
		Row row = new Row(3.pt()).AlignItemsCenter().Padding(1.pt(), 2.pt()).Background(HEADER_BG);
		if (withCheckbox)
		{
			row.Add(new UiComponent().Width(CHECKBOX_WIDTH.px()).NoShrink());
		}
		row.Add(new UiComponent().Width(ICON_SIZE.px()).NoShrink());
		row.Add(new Label(name).FontBold().Width(COL_NAME.px()).NoShrink());
		row.Add(new Label(col2).FontBold().Width(COL_EFFICIENCY.px()).NoShrink());
		row.Add(new Label(col3).FontBold().Width(col3Width.px()).NoShrink());
		row.Add(new Label(col4).FontBold().Width(col4Width.px()).NoShrink());
		return row;
	}

	private static UiComponent efficiencyCell(BuildingStats s)
	{
		return barCell(s.Efficiency);
	}

	private static UiComponent barCell(float efficiency)
	{
		Row cell = new Row(2.pt()).AlignItemsCenter().Width(COL_EFFICIENCY.px()).NoShrink();
		UiComponent bar = new UiComponent().Width(BAR_WIDTH.px()).Height(10.px()).Background(BAR_BG).NoShrink();
		if (efficiency >= 0f)
		{
			bar.Add(new UiComponent().Width(Percent.FromFloat(efficiency)).Height(100.Percent()).Background(colorFor(efficiency)));
			cell.Add(bar);
			cell.Add(new Label(new LocStrFormatted(formatPercent(efficiency))).FontBold().Color(colorFor(efficiency)));
		}
		else
		{
			cell.Add(bar);
			cell.Add(new Label(Tr.NoData).Color(MUTED));
		}
		return cell;
	}

	private static ColorRgba colorFor(float efficiency)
	{
		if (efficiency >= 0.9f)
		{
			return GOOD;
		}
		return efficiency >= 0.6f ? MEDIUM : BAD;
	}

	/// <summary>The game's category colors (dark red/orange) are hard to read on the dark panel.</summary>
	private static ColorRgba lighten(ColorRgba c)
	{
		return new ColorRgba((byte)((c.R + 255) / 2), (byte)((c.G + 255) / 2), (byte)((c.B + 255) / 2));
	}

	private static string formatPercent(float ratio)
	{
		return Math.Round(ratio * 100f).ToString(CultureInfo.InvariantCulture) + " %";
	}

	private static bool contains(string value, string filter)
	{
		return value != null && LocalizationManager.CurrentCultureInfo.CompareInfo.IndexOf(value, filter, CompareOptions.IgnoreCase) >= 0;
	}

	private static int compareText(string a, string b)
	{
		return LocalizationManager.CurrentCultureInfo.CompareInfo.Compare(a, b, CompareOptions.IgnoreCase);
	}

	// ---------------------------------------------------------------- Map interaction

	private void goTo(BuildingStats s)
	{
		IStaticEntity entity = s.Entity;
		if (entity.IsDestroyed)
		{
			Refresh();
			return;
		}
		clearHighlights();
		if (!IsPinned)
		{
			// Otherwise the window and the inspector cover the building.
			Close();
		}
		m_camera.PanTo(entity.Position2f);
		m_inspectors.TryActivateFor(entity);
	}

	private void highlight(BuildingStats s)
	{
		clearHighlights();
		addHighlight(s.Entity);
	}

	private void highlight(List<BuildingStats> buildings)
	{
		clearHighlights();
		foreach (BuildingStats s in buildings)
		{
			addHighlight(s.Entity);
		}
	}

	private void addHighlight(IStaticEntity entity)
	{
		if (!entity.IsDestroyed)
		{
			m_highlights.Add(m_rendering.AddHighlight(entity, HIGHLIGHT));
		}
	}

	private void clearHighlights()
	{
		foreach (ulong id in m_highlights)
		{
			m_rendering.RemoveHighlight(id);
		}
		m_highlights.Clear();
	}
}
