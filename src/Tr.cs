using System;
using Mafi.Localization;

namespace ProductionOverview;

/// <summary>
/// Texts in German and English. German is used when the game runs in German, English otherwise.
/// </summary>
public static class Tr
{
	private static bool s_german;

	public static void Init()
	{
		try
		{
			string file = LocalizationManager.CurrentLangInfo.FileName ?? "";
			s_german = file.StartsWith("de", StringComparison.OrdinalIgnoreCase);
		}
		catch (Exception)
		{
			s_german = false;
		}
	}

	private static LocStrFormatted t(string de, string en) => new LocStrFormatted(s_german ? de : en);

	private static string s(string de, string en) => s_german ? de : en;

	public static LocStrFormatted WindowTitle => t("Produktionsübersicht", "Production overview");
	public static LocStrFormatted TabBuildings => t("Gebäude", "Buildings");
	public static LocStrFormatted TabByType => t("Nach Typ", "By type");
	public static LocStrFormatted FilterPlaceholder => t("<i>Name oder Typ filtern…</i>", "<i>Filter by name or type…</i>");
	public static LocStrFormatted Refresh => t("Aktualisieren", "Refresh");
	public static LocStrFormatted HidePaused => t("Pausierte ausblenden", "Hide paused");
	public static LocStrFormatted HideOutputFull => t("Output voll ausblenden", "Hide output full");
	public static LocStrFormatted HideAuxiliary => t("Hilfsgebäude ausblenden", "Hide auxiliary buildings");
	public static LocStrFormatted HideAuxiliaryTooltip => t(
		"Blendet Gebäude aus, die man nicht für die Produktion baut, sondern weil etwas sie braucht: " +
		"Schornsteine, Gasfackel, Verbrennungsanlage, Abwasserrohr – plus alle Typen, die du im Tab „Nach Typ“ ausgeblendet hast.",
		"Hides buildings you build because something needs them, not to produce: " +
		"smoke stacks, flare, burner, ocean waste dump – plus every type you hid in the \"By type\" tab.");
	public static LocStrFormatted HiddenTypesInfo(int count) => new LocStrFormatted(count == 1
		? s("· 1 Typ ausgeblendet", "· 1 type hidden")
		: s($"· {count} Typen ausgeblendet", $"· {count} types hidden"));
	public static LocStrFormatted ResetTypes => t("Typ-Auswahl zurücksetzen", "Reset type selection");
	public static LocStrFormatted ResetTypesTooltip => t(
		"Alle Haken im Tab „Nach Typ“ auf den Standard zurücksetzen (nur Abfallentsorgung ausgeblendet).",
		"Reset all checkboxes in the \"By type\" tab to the default (only waste disposal hidden).");
	public static LocStrFormatted TypeCheckboxTooltip => t(
		"Haken: Typ wird in der Übersicht berücksichtigt.\nKein Haken: Typ gilt als Hilfsgebäude und wird ausgeblendet (gilt für alle Spielstände).",
		"Checked: type is included in the overview.\nUnchecked: type counts as auxiliary and is hidden (applies to all saves).");
	public static LocStrFormatted TypeCheckboxAutoTooltip => t(
		"Vom Spiel als Abfallentsorgung markiert, daher standardmäßig ausgeblendet. Setze den Haken, um ihn trotzdem anzuzeigen.",
		"Marked by the game as waste disposal, so hidden by default. Tick the box to show it anyway.");
	public static LocStrFormatted AutoAuxiliary => t("<i>Abfallentsorgung</i>", "<i>waste disposal</i>");
	public static LocStrFormatted HideOutputFullTooltip => t(
		"Blendet Gebäude aus, deren Hauptgrund für Stillstand „Ausgabe voll“ ist – sie produzieren mehr, als abgenommen wird.",
		"Hides buildings whose main reason for downtime is \"output full\" – they produce more than is taken away.");
	public static LocStrFormatted Period => t("Zeitraum:", "Period:");
	public static LocStrFormatted Show => t("Zeige:", "Show:");
	public static LocStrFormatted Sort => t("Sortierung:", "Sort:");

	public static LocStrFormatted PeriodName(StatsPeriod p) => p switch
	{
		StatsPeriod.CurrentMonth => t("Laufender Monat", "Current month"),
		StatsPeriod.LastMonth => t("Letzter Monat", "Last month"),
		StatsPeriod.Last3Months => t("Letzte 3 Monate", "Last 3 months"),
		_ => t("Letzte 12 Monate", "Last 12 months"),
	};

	public static LocStrFormatted ThresholdName(int threshold) => threshold >= 100
		? t("Alle", "All")
		: new LocStrFormatted(s($"Unter {threshold} %", $"Below {threshold} %"));

	public static LocStrFormatted SortName(SortMode m) => m switch
	{
		SortMode.EfficiencyAsc => t("Effizienz ↑ (schlechteste zuerst)", "Efficiency ↑ (worst first)"),
		SortMode.EfficiencyDesc => t("Effizienz ↓", "Efficiency ↓"),
		SortMode.Name => t("Name", "Name"),
		_ => t("Typ", "Type"),
	};

	public static LocStrFormatted ColName => t("Gebäude", "Building");
	public static LocStrFormatted ColEfficiency => t("Effizienz", "Efficiency");
	public static LocStrFormatted ColMainIssue => t("Hauptgrund für Stillstand", "Main reason for downtime");
	public static LocStrFormatted ColState => t("Aktuell", "Now");
	public static LocStrFormatted ColCount => t("Anzahl", "Count");
	public static LocStrFormatted ColBelow => t("Unter 80 %", "Below 80 %");
	public static LocStrFormatted ColWorst => t("Schlechtestes", "Worst");

	public static LocStrFormatted NoData => t("–", "–");
	public static LocStrFormatted NoDataTooltip => t("Noch keine Daten für diesen Zeitraum (neu gebaut oder pausiert).", "No data for this period yet (new or paused).");
	public static LocStrFormatted NoIssue => t("<i>keiner</i>", "<i>none</i>");
	public static LocStrFormatted NothingFound => t("Keine passenden Gebäude gefunden.", "No matching buildings found.");
	public static LocStrFormatted RowTooltip => t("Klicken: zum Gebäude springen und Details öffnen", "Click: fly to the building and open its details");
	public static LocStrFormatted TypeRowTooltip => t("Klicken: nur Gebäude dieses Typs anzeigen", "Click: show only buildings of this type");

	public static LocStrFormatted Summary(int count, string avg, int below, int threshold) => new LocStrFormatted(s(
		$"<b>{count}</b> Gebäude · Ø Effizienz <b>{avg}</b> · <b>{below}</b> unter {threshold} %",
		$"<b>{count}</b> buildings · avg. efficiency <b>{avg}</b> · <b>{below}</b> below {threshold} %"));

	public static LocStrFormatted Truncated(int shown, int total) => new LocStrFormatted(s(
		$"<i>Zeige die ersten {shown} von {total}. Filter verwenden, um weitere zu sehen.</i>",
		$"<i>Showing the first {shown} of {total}. Use the filter to see more.</i>"));

	public static string Breakdown(string label, string percent) => $"{label}: {percent}";

	public static string BreakdownHeader => s("Aufschlüsselung", "Breakdown");

	public static string StateName(string state) => state switch
	{
		"Working" => s("<color=#7fd77f>Arbeitet</color>", "<color=#7fd77f>Working</color>"),
		"Paused" => s("<color=#a0a0a0>Pausiert</color>", "<color=#a0a0a0>Paused</color>"),
		"Broken" => s("<color=#ff6b6b>Defekt</color>", "<color=#ff6b6b>Broken</color>"),
		"NotEnoughWorkers" => s("<color=#ff6b6b>Keine Arbeiter</color>", "<color=#ff6b6b>No workers</color>"),
		"NotEnoughPower" => s("<color=#ff6b6b>Kein Strom</color>", "<color=#ff6b6b>No power</color>"),
		"NotEnoughComputing" => s("<color=#ff6b6b>Keine Rechenleistung</color>", "<color=#ff6b6b>No computing</color>"),
		"NotEnoughInput" or "MissingInput" => s("<color=#ffb347>Input fehlt</color>", "<color=#ffb347>Missing input</color>"),
		"OutputFull" or "FullOutput" => s("<color=#7fa7ff>Output voll</color>", "<color=#7fa7ff>Output full</color>"),
		"NoRecipes" => s("<color=#ff6b6b>Kein Rezept</color>", "<color=#ff6b6b>No recipe</color>"),
		"InvalidPlacement" => s("<color=#ff6b6b>Ungültig platziert</color>", "<color=#ff6b6b>Invalid placement</color>"),
		"Idle" => s("<color=#a0a0a0>Leerlauf</color>", "<color=#a0a0a0>Idle</color>"),
		"None" => "–",
		_ => state,
	};

	public static string UnderConstruction => s("<color=#a0a0a0>Im Bau</color>", "<color=#a0a0a0>Under construction</color>");
}
